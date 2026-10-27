using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ssalddel.Contracts.Admin.DispatchTraining;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Contracts.Common.Drivers;
using Ssalddel.Contracts.Common.Privacy;
using Ssalddel.Contracts.Driver.Settings;
using Ssalddel.Contracts.Driver.Work;
using 살뜰.Data;
using 살뜰.Services.Dispatch.Queue;
using 살뜰.도메인.공통;

namespace Ssalddel.DispatchTraining;

/// <summary>가상 역할은 기존 인증 API를 이용한다. 운송 상태나 기사 선정을 직접 저장하지 않는다.</summary>
internal sealed class DispatchTrainingActions(DispatchTrainingSettings settings, IServiceScopeFactory scopes,
    DispatchTrainingTraceCollector traces) : IDispatchTrainingActions, IDisposable
{
    private readonly HttpClient http = new(new HttpClientHandler { AllowAutoRedirect = false, UseProxy = false })
        { BaseAddress = new Uri(DispatchTrainingSettings.BaseUrl), Timeout = TimeSpan.FromSeconds(20) };
    private readonly Dictionary<string, string> tokens = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DateTime> gpsTimes = new(StringComparer.Ordinal);
    private readonly JsonSerializerOptions json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private ActorJournal? journal;
    private bool actorsPrepared;
    private DateTime loginRetryAtUtc;
    private DispatchTrainingShadowAI shadow = new();
    private string JournalPath => Path.Combine(settings.ArtifactRoot, "actors.json");

    private async Task EnsureRunAsync(string runId, CancellationToken ct, bool prepareDrivers = true)
    {
        if (journal?.RunId == runId && actorsPrepared) return;
        if (!runId.StartsWith("dispatch-training-", StringComparison.Ordinal) && !runId.StartsWith("dispatch-training:", StringComparison.Ordinal))
            throw new InvalidOperationException("DispatchTrainingRunInvalid");
        if (journal?.RunId != runId)
        {
            actorsPrepared = false;
            var saved = File.Exists(JournalPath) ? JsonSerializer.Deserialize<ActorJournal>(await File.ReadAllTextAsync(JournalPath, ct), json) : null;
            if (saved is not null && saved.RunId != runId)
                File.Copy(JournalPath, Path.Combine(settings.ArtifactRoot, $"actors-{DateTime.UtcNow:yyyyMMddHHmmssfff}.json"));
            journal = saved?.RunId == runId ? saved : new ActorJournal { RunId = runId, StartedAtUtc = DateTime.UtcNow };
            shadow.Dispose(); shadow = new DispatchTrainingShadowAI();
        }
        // Tokens remain in memory; generation/command progress is durable and contains only synthetic data.
        await SaveAsync(ct);
        if (!prepareDrivers) return;
        for (var i = 0; i < DispatchTrainingSeed.InitialDriverCount; i++)
            await PrepareDriverAsync(DispatchTrainingSeed.DriverIds[i], i, ct);
        for (var i = 0; i < journal.SupportDrivers; i++)
            await PrepareDriverAsync(DispatchTrainingSeed.DriverIds[6 + i], 6 + i, ct);
        actorsPrepared = true;
    }

    public async Task<DispatchTrainingSubmissionResult> SubmitOrderAsync(string runId, int index, Guid requestId, CancellationToken ct = default)
    {
        await EnsureRunAsync(runId, ct, prepareDrivers: false);
        var order = journal!.Orders.SingleOrDefault(x => x.ClientRequestId == requestId);
        if (order is null)
        {
            order = new ActorOrder { Index = index, ClientRequestId = requestId, CreatedAtUtc = DateTime.UtcNow };
            journal.Orders.Add(order); await SaveAsync(ct);
        }
        try
        {
            await EnsureRunAsync(runId, ct);
            if (await ReadCanonicalAsync(order, ct)) return new(DispatchTrainingActionOutcomes.Confirmed, order.CanonicalId);
            if (order.SubmissionStarted) return new(DispatchTrainingActionOutcomes.Unknown, ErrorCode: "SubmissionResultUnknown");
            var consent = GuidFor(runId + "|privacy");
            var existing = await ApiAsync(DispatchTrainingSeed.ShipperId, HttpMethod.Get,
                $"api/v1/common/application-privacy-consents/{consent}", null, ct, allow404: true);
            if (existing is null)
                await ApiAsync(DispatchTrainingSeed.ShipperId, HttpMethod.Post, "api/v1/common/application-privacy-consents",
                    new 신청개인정보동의기록Request { 증적Id = consent, 업무Code = 신청개인정보업무Codes.운송대행,
                        출처Code = 신청개인정보출처Codes.생활교류, 동의문버전 = 신청개인정보동의정책.현재버전,
                        수집이용동의 = true, 연령요건확인 = true }, ct);
            var input = DispatchTrainingOrderFixture.Create(index, requestId, consent, order.CreatedAtUtc);
            var quote = await ApiAsync(DispatchTrainingSeed.ShipperId, HttpMethod.Post, NeighborhoodDeliveryRoutes.Api + "/quote", input, ct);
            input.AgreedFareKrw = Decimal(Node(quote, "fare"), "최종운임");
            order.SubmissionStarted = true; await SaveAsync(ct);
            var created = await ApiAsync(DispatchTrainingSeed.ShipperId, HttpMethod.Post, NeighborhoodDeliveryRoutes.Api, input, ct);
            order.CanonicalId = Text(created, "requestId"); await SaveAsync(ct);
            if (string.IsNullOrWhiteSpace(order.CanonicalId)) return new(DispatchTrainingActionOutcomes.Unknown, ErrorCode: "CanonicalIdMissing");
            return new(DispatchTrainingActionOutcomes.Confirmed, order.CanonicalId);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            if (!order.SubmissionStarted)
            {
                order.SubmissionRejected = true;
                Incident(order, "SubmissionNotSent", "접수 요청 전 준비 단계에서 중단됐습니다. 주문 생성 요청은 전송하지 않았습니다.");
                await SaveAsync(CancellationToken.None);
                return new(DispatchTrainingActionOutcomes.Rejected, ErrorCode: "SubmissionNotSent");
            }
            Incident(order, "SubmissionUnknown", "접수 결과를 같은 요청 번호로 확인 중입니다."); await SaveAsync(CancellationToken.None);
            return new(DispatchTrainingActionOutcomes.Unknown, ErrorCode: "SubmissionUnknown");
        }
    }

    public async Task<DispatchTrainingTickResult> TickAsync(string runId, CancellationToken ct = default)
    {
        try { return await TickCoreAsync(runId, ct); }
        catch (Exception error) when (error is not OutOfMemoryException and not OperationCanceledException)
        {
            var frames = new System.Diagnostics.StackTrace(error, true).GetFrames()?.Take(24)
                .Select(x => new { method = x.GetMethod()?.DeclaringType?.FullName + "." + x.GetMethod()?.Name, line = x.GetFileLineNumber() });
            await File.WriteAllTextAsync(Path.Combine(settings.ArtifactRoot, "actor-failure.json"),
                JsonSerializer.Serialize(new { atUtc = DateTime.UtcNow, type = error.GetType().Name, innerType = error.InnerException?.GetType().Name,
                    status = (error as HttpRequestException)?.StatusCode, frames }, json), CancellationToken.None);
            throw;
        }
    }

    private async Task<DispatchTrainingTickResult> TickCoreAsync(string runId, CancellationToken ct)
    {
        await EnsureRunAsync(runId, ct);
        var decisions = new List<DispatchTrainingDecisionDto>();
        foreach (var target in journal!.SupportCommands.Values.Distinct())
        {
            for (var n = journal.SupportDrivers; n < target; n++)
            {
                var id = DispatchTrainingSeed.DriverIds[6 + n];
                var work = await ApiAsync(id, HttpMethod.Get, "api/v1/driver/work/status", null, ct);
                var intent = await ApiAsync(id, HttpMethod.Get, "api/v1/driver/operational-dispatch/availability", null, ct);
                if (Text(work, "workStartClientRequestId") != GuidFor(runId + "|shift|" + id).ToString()
                    || Text(intent, "수신의사Code") != 운영배차수신의사Code.On) break;
                journal.SupportDrivers = n + 1; if (!journal.PreparedDrivers.Contains(id)) journal.PreparedDrivers.Add(id);
                await SaveAsync(ct);
            }
        }
        for (var i = 0; i < 6 + journal!.SupportDrivers; i++)
        {
            var id = DispatchTrainingSeed.DriverIds[i];
            if (journal.StoppedDrivers.Contains(id)) continue;
            if (!gpsTimes.TryGetValue(id, out var at) || DateTime.UtcNow - at > TimeSpan.FromSeconds(30))
            { await LocationAsync(id, i, ct); gpsTimes[id] = DateTime.UtcNow; }
        }
        // The shortage episode is timed independently from order slots, which may be
        // skipped by an intentional pause or server restart.
        if (!journal.ShortageTriggered && DateTime.UtcNow >= journal.StartedAtUtc.AddMinutes(18)
            && journal.Orders.Any(x => x.CanonicalId is not null))
        {
            foreach (var id in DispatchTrainingSeed.DriverIds.Take(6))
            {
                if (journal.ReceivingOffDrivers.Contains(id)) continue;
                var confirmed = await IntentAsync(id, false, ct);
                if (Text(confirmed, "수신의사Code") != 운영배차수신의사Code.Off) throw new InvalidOperationException("ReceivingOffNotConfirmed");
                journal.ReceivingOffDrivers.Add(id); await SaveAsync(ct);
            }
            journal.ShortageTriggered = journal.ReceivingOffDrivers.Count == 6; await SaveAsync(ct);
            Incident(null, "DriverShortage", "신규 수신을 중단한 가상 기사들이 있습니다. 지원 기사 참여를 선택할 수 있습니다.");
        }
        foreach (var order in journal.Orders.ToArray())
        {
            try
            {
                if (order.SubmissionRejected) continue;
                if (order.CanonicalId is null && !await ReadCanonicalAsync(order, ct)) continue;
                await AdvanceAsync(order, ct);
            }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException or InvalidOperationException)
            { Incident(order, "ActorNeedsReview", "이 업무의 API 처리 결과를 확인해야 합니다. 다른 업무는 계속 진행합니다."); }
        }
        while (traces.TryRead(out var item))
        {
            var order = journal.Orders.FirstOrDefault(x => x.CanonicalId == item.RequestId);
            if (order is null) continue;
            var trace = item.Review;
            var traceFingerprint = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(trace)));
            if (order.LastDecisionFingerprint == traceFingerprint) continue;
            order.LastDecisionFingerprint = traceFingerprint;
            var allowed = trace.후보목록.Where(x => x.판정Code == "CandidateAllowed").ToArray();
            var baseline = trace.선정후보?.DriverId;
            var mapped = allowed.OrderBy(x => x.기사Id == baseline ? 0 : 1).Take(30).ToArray();
            var watch = System.Diagnostics.Stopwatch.StartNew();
            DispatchTrainingShadowResult result;
            var primaryAIProfile = settings.Configuration.GetValueOrDefault("CargoDispatchOllama:Enabled") == "true";
            // 실제 선택을 검증하는 프로필에서는 별도 비교 요청으로 같은 모델을 다시 호출/해제하지 않는다.
            if (primaryAIProfile)
                result = new("NotRunPrimarySelectionProfile", null, false);
            else if (mapped.Length > 1 && journal.AiAttempts >= 15)
                result = new("BudgetExhausted", null, false);
            else
            {
                if (mapped.Length > 1) { journal.AiAttempts++; await SaveAsync(ct); }
                result = await shadow.CompareAsync(new("C1", mapped.Select((x, i) => new DispatchTrainingShadowCandidate(
                    $"C{i + 1}", x.추천점수, x.거리Km, x.대기점수)).ToArray()), ct);
            }
            string? selected = null;
            if (result.SelectedToken is { Length: > 1 } token && int.TryParse(token[1..], out var n) && n > 0 && n <= mapped.Length)
                selected = Label(mapped[n - 1].기사Id);
            var ai = new DispatchTrainingAiResultDto { StatusCode = result.StatusCode, RuleDriverId = Label(baseline),
                ShadowDriverId = selected, SameChoice = result.MatchesBaseline, ReasonCode = result.StatusCode,
                ModelName = "qwen3:1.7b", ElapsedMilliseconds = watch.ElapsedMilliseconds };
            var decision = new DispatchTrainingDecisionDto { DecisionId = Guid.NewGuid().ToString("N"),
                OrderId = order.CanonicalId, RecordedAtUtc = DateTime.UtcNow,
                RuleStatusCode = baseline is null ? "NoEligibleCandidate" : "Selected", RuleDriverLabel = Label(baseline),
                AiResult = ai, Summary = $"기존 규칙 후보 {allowed.Length}명 · 기준 {Label(baseline) ?? "대기"} · "
                    + (primaryAIProfile ? "그림자 비교 미실행 · 실제 선택은 정본 감사에서 확인" : "그림자 AI 비교 · 실제 추천 적용은 정본 감사에서 별도 확인"),
                Candidates = trace.후보목록.Select((x, i) => new DispatchTrainingDecisionCandidateDto
                { Token = Array.FindIndex(mapped, p => p.기사Id == x.기사Id) is var tokenIndex && tokenIndex >= 0 ? $"C{tokenIndex + 1}" : $"X{i + 1}",
                    DriverLabel = Label(x.기사Id) ?? "미확인", EligibilityCode = x.판정Code,
                    DistanceKm = x.거리Km is { } d ? (double)d : null,
                    BaseScore = x.기본추천점수 is { } b ? (double)b : null,
                    AgingScore = x.대기점수 is { } a ? (double)a : null,
                    TotalScore = x.추천점수 is { } t ? (double)t : null }).ToArray() };
            decisions.Add(decision); journal.LatestAi = ai;
        }
        var observations = new List<DispatchTrainingOrderObservation>();
        var drivers = new List<DispatchTrainingDriverDto>();
        using (var scope = scopes.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SsalddelContext>();
            foreach (var order in journal.Orders)
            {
                var queue = order.CanonicalId is null ? null : await db.운송원장.AsNoTracking().SingleOrDefaultAsync(x => x.의뢰Id == order.CanonicalId, ct);
                var state = order.SubmissionRejected ? "NotSubmitted" : queue is null ? "Unknown" : queue.상태 == "인수완료" ? "Completed"
                    : queue.상태 == "취소" ? "Failed" : queue.확정기사Id is null ? "Waiting"
                    : queue.상태 is "확정" or "배차확정" or "이동중" ? "Assigned" : "InProgress";
                observations.Add(new() { Index = order.Index, ClientRequestId = order.ClientRequestId, CanonicalOrderId = order.CanonicalId, StateCode = state });
                if (state == "Completed")
                    journal.Incidents = journal.Incidents.Select(x => x.OrderId == order.CanonicalId ? x with { StatusCode = "Resolved" } : x).ToList();
            }
            var runOrders = journal.Orders.Where(x => x.CanonicalId is not null).Select(x => x.CanonicalId!).ToArray();
            for (var i = 0; i < 8; i++)
            {
                var id = DispatchTrainingSeed.DriverIds[i];
                // This run has at most 15 rows. Materialize the explicitly scoped set once;
                // do not let a provider-specific collection parameter hide canonical reads.
                var ownOrders = await db.운송원장.AsNoTracking().Where(x => x.확정기사Id == id).Select(x => new { x.의뢰Id, x.상태 }).ToListAsync(ct);
                var completed = ownOrders.Count(x => runOrders.Contains(x.의뢰Id) && x.상태 == "인수완료");
                var active = ownOrders.Count(x => runOrders.Contains(x.의뢰Id) && x.상태 != "인수완료" && x.상태 != "취소");
                if (journal.ReceivingOffDrivers.Contains(id) && ownOrders.All(x => x.상태 is "인수완료" or "취소")
                    && !journal.StoppedDrivers.Contains(id))
                {
                    try
                    {
                        var work = await ApiAsync(id, HttpMethod.Get, "api/v1/driver/work/status", null, ct);
                        if (Text(work, "status") != DrivingStatus) journal.StoppedDrivers.Add(id);
                        else if (!journal.StopPendingDrivers.Contains(id))
                        {
                            journal.StopPendingDrivers.Add(id); await SaveAsync(ct);
                            await ApiAsync(id, HttpMethod.Post, "api/v1/driver/work/stop", null, ct);
                            work = await ApiAsync(id, HttpMethod.Get, "api/v1/driver/work/status", null, ct);
                            if (Text(work, "status") != DrivingStatus) journal.StoppedDrivers.Add(id);
                        }
                    }
                    catch (Exception error) when (error is HttpRequestException or TaskCanceledException)
                    { Incident(null, "DriverStopUnknown", "가상 기사의 기존 업무 종료 뒤 운행 종료 결과를 확인 중입니다."); }
                }
                drivers.Add(new() { DriverId = Label(id)!, Label = Label(id)!, IsSupport = i >= 6,
                    StatusCode = i >= 6 + journal.SupportDrivers ? "Standby" : journal.ReceivingOffDrivers.Contains(id) ? "ReceivingOff" : "Active",
                    ActiveWorkCount = active, CompletedWorkCount = completed });
            }
        }
        if (journal.SupportDrivers > 0 && observations.All(x => x.StateCode != "Waiting"))
            journal.Incidents = journal.Incidents.Select(x => x.KindCode == "DriverShortage" ? x with { StatusCode = "Resolved" } : x).ToList();
        await SaveAsync(ct);
        return new()
        {
            Orders = observations, WaitingOrders = observations.Count(x => x.StateCode == "Waiting"),
            AssignedOrders = observations.Count(x => x.StateCode == "Assigned"), InProgressOrders = observations.Count(x => x.StateCode == "InProgress"),
            CompletedOrders = observations.Count(x => x.StateCode == "Completed"), FailedOrders = observations.Count(x => x.StateCode == "Failed"),
            UnfinishedOrders = observations.Count(x => x.StateCode is not ("Completed" or "Failed" or "NotSubmitted")),
            ActivatedSupportDriverCount = journal.SupportDrivers, Drivers = drivers, Incidents = journal.Incidents,
            Decisions = decisions, LatestAiResult = journal.LatestAi
        };
    }

    private async Task AdvanceAsync(ActorOrder order, CancellationToken ct)
    {
        if (order.CanonicalId is null) return;
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SsalddelContext>();
        var q = await db.운송원장.AsNoTracking().SingleAsync(x => x.의뢰Id == order.CanonicalId, ct);
        var transition = scope.ServiceProvider.GetRequiredService<I배차대기원장전환Service>();
        if (q.확정기사Id is null)
        {
            if (q.현재추천대상기사Id is null)
            {
                if (q.배차큐단계 == 상태값.배차큐단계.계획배차)
                    await transition.계획배차에서추천으로전환Async(q.의뢰Id, ct);
                else await transition.추천대기처리Async(q.의뢰Id, ct);
                return;
            }
            if (q.추천만료시각 <= DateTime.UtcNow) { await transition.추천만료처리Async(q.의뢰Id, ct); return; }
            var driver = q.현재추천대상기사Id;
            if (journal!.ReceivingOffDrivers.Contains(driver)) return;
            if (order.Index == 5 && order.IgnoredRound is null)
            { order.IgnoredRound = q.추천라운드; Incident(order, "OfferExpired", "가상 기사 미응답으로 추천 만료·재탐색을 확인합니다."); await SaveAsync(ct); }
            if (order.IgnoredRound == q.추천라운드) return;
            if (order.Index == 2 && !order.RejectedOnce)
            {
                order.RejectedOnce = true; await SaveAsync(ct);
                await ApiAsync(driver, HttpMethod.Post, $"api/v1/driver/dispatch-actions/{q.의뢰Id}/reject", new { 사유 = "TimeConstraint" }, ct);
                Incident(order, "DriverDeclined", "가상 기사 거절 후 다음 후보를 탐색합니다."); return;
            }
            var offers = await ApiAsync(driver, HttpMethod.Get, "api/v1/driver/recommendations", null, ct) as JsonArray;
            var offer = offers?.FirstOrDefault(x => Text(x, "의뢰Id") == q.의뢰Id);
            if (offer is null || (int?)Node(offer, "추천라운드") != q.추천라운드) return;
            var warnings = VehicleWarningAcknowledgements(offer);
            await OnceAsync(order, "accept:" + q.추천라운드, driver, HttpMethod.Post,
                $"api/v1/driver/dispatch-actions/{q.의뢰Id}/accept", new { ExpectedRecommendationRound = q.추천라운드,
                    AcknowledgedWarningCodes = warnings }, ct);
            return;
        }
        var actor = q.확정기사Id;
        // Product consent applies to the confirmed assignment, never to an offer candidate.
        var canonical = await ApiAsync(DispatchTrainingSeed.ShipperId, HttpMethod.Get, $"{NeighborhoodDeliveryRoutes.Api}/{q.의뢰Id}", null, ct);
        var disclosure = Node(canonical, "driverDisclosure");
        if ((bool?)Node(disclosure, "consented") != true)
        {
            if ((bool?)Node(disclosure, "canRecordConsent") != true) return;
            var consentKey = $"assigned-disclosure:{q.추천라운드}:{actor}";
            if (!await OnceAsync(order, consentKey, DispatchTrainingSeed.ShipperId, HttpMethod.Post,
                $"{NeighborhoodDeliveryRoutes.Api}/{q.의뢰Id}/driver-disclosure",
                new NeighborhoodDeliveryDisclosureRequest { ClientRequestId = GuidFor(journal!.RunId + "|" + order.Index + consentKey),
                    ConfirmedDriverId = actor, ExpectedRecommendationRound = q.추천라운드, Consented = true }, ct)) return;
        }
        var detail = await ApiAsync(actor, HttpMethod.Get, $"api/v1/driver/transports/{q.Id}", null, ct);
        var state = Text(detail, "상태");
        ReconcileCommands(order, q.추천라운드, actor, state, (bool?)Node(disclosure, "consented") == true);
        if (state == "인수완료") return;
        var step = state switch
        {
            "확정" or "배차확정" or "이동중" => "arrive-pickup",
            "상차지도착" => "pickup-complete",
            "상차완료" or "운송중" => "arrive-dropoff",
            "하차지도착" => "complete", _ => null
        };
        if (step is null) return;
        var lastChanged = DateTime.TryParse(Text(detail, "updatedAt"), out var date) ? DateTime.SpecifyKind(date, DateTimeKind.Utc) : order.CreatedAtUtc;
        if (DateTime.UtcNow - lastChanged < TimeSpan.FromSeconds(20)) return;
        object body = new { };
        if (step is "pickup-complete" or "complete")
        {
            var photoKey = "photo:" + step;
            if (!order.Photos.TryGetValue(photoKey, out var photo))
            {
                if (order.Commands.ContainsKey(photoKey)) { Incident(order, "PhotoUnknown", "합성 사진 업로드 결과를 확인해야 합니다."); return; }
                order.Commands[photoKey] = "Unknown"; await SaveAsync(ct);
                var upload = await UploadAsync(actor, q.Id, step == "pickup-complete", ct);
                photo = new() { ObjectName = Text(upload, "objectName")!, Url = Text(upload, "url")! };
                order.Photos[photoKey] = photo; order.Commands[photoKey] = "Confirmed"; await SaveAsync(ct);
            }
            body = step == "pickup-complete" ? new { 상차사진ObjectName = photo.ObjectName, 상차사진Url = photo.Url }
                : (object)new { 하차사진ObjectName = photo.ObjectName, 하차사진Url = photo.Url };
        }
        await OnceAsync(order, step, actor, HttpMethod.Post, $"api/v1/driver/transports/{q.Id}/{step}", body, ct);
    }

    // Match the role UI's stable acknowledgement contract. Generic recommendation text is not a code.
    internal static string[] VehicleWarningAcknowledgements(JsonNode? offer) =>
        Node(offer, "차량경고") is JsonArray { Count: > 0 } ? ["FreightVehicleAdvisory"] : [];

    private async Task<bool> OnceAsync(ActorOrder order, string key, string actor, HttpMethod method, string path, object? body, CancellationToken ct)
    {
        if (order.Commands.TryGetValue(key, out var outcome))
        {
            if (outcome == "Rejected") Incident(order, "CommandRejected", "거부된 명령은 다시 전송하지 않고 추천 판본과 입력 조건을 검토합니다.");
            else if (outcome != "Confirmed") Incident(order, "CommandUnknown", "미확정 명령은 정본 확인 전 다음 단계로 진행하지 않습니다.");
            return outcome == "Confirmed";
        }
        order.Commands[key] = "Unknown"; await SaveAsync(ct);
        try { await ApiAsync(actor, method, path, body, ct); }
        catch (HttpRequestException error) when (error.StatusCode is { } status && (int)status is >= 400 and < 500)
        {
            order.Commands[key] = "Rejected"; await SaveAsync(CancellationToken.None); throw;
        }
        order.Commands[key] = "Confirmed"; await SaveAsync(ct);
        return true;
    }
    private static void ReconcileCommands(ActorOrder order, int round, string actor, string? state, bool disclosed)
    {
        var stage = state switch { "확정" or "배차확정" or "이동중" => 1, "상차지도착" => 2, "상차완료" or "운송중" => 3,
            "하차지도착" => 4, "인수완료" => 5, _ => 0 };
        if (stage >= 1) Confirm("accept:" + round);
        if (disclosed) Confirm($"assigned-disclosure:{round}:{actor}");
        if (stage >= 2) Confirm("arrive-pickup");
        if (stage >= 3) Confirm("pickup-complete");
        if (stage >= 4) Confirm("arrive-dropoff");
        if (stage >= 5) Confirm("complete");
        void Confirm(string key) { if (order.Commands.GetValueOrDefault(key) == "Unknown") order.Commands[key] = "Confirmed"; }
    }
    private async Task<bool> ReadCanonicalAsync(ActorOrder order, CancellationToken ct)
    {
        var value = await ApiAsync(DispatchTrainingSeed.ShipperId, HttpMethod.Get,
            NeighborhoodDeliveryRoutes.Api + "/by-client-request/" + order.ClientRequestId, null, ct, allow404: true);
        if (value is null) return false;
        order.CanonicalId = Text(value, "requestId"); await SaveAsync(ct); return order.CanonicalId is not null;
    }

    public async Task<DispatchTrainingSupportResult> ActivateSupportDriversAsync(string runId, Guid requestId, int count, CancellationToken ct = default)
    {
        await EnsureRunAsync(runId, ct);
        if (!journal!.SupportCommands.TryGetValue(requestId, out var target))
        { target = Math.Min(2, journal.SupportDrivers + count); journal.SupportCommands[requestId] = target; await SaveAsync(ct); }
        for (var n = journal.SupportDrivers; n < target; n++)
        { await PrepareDriverAsync(DispatchTrainingSeed.DriverIds[6 + n], 6 + n, ct); journal.SupportDrivers = n + 1; await SaveAsync(ct); }
        journal.Incidents = journal.Incidents.Select(x => x.KindCode == "DriverShortage" ? x with { Message = "지원 기사 참여를 요청했습니다. 실제 재추천 결과를 확인합니다." } : x).ToList();
        await SaveAsync(ct); return new(DispatchTrainingActionOutcomes.Confirmed, journal.SupportDrivers);
    }

    private async Task PrepareDriverAsync(string driver, int index, CancellationToken ct)
    {
        var work = await ApiAsync(driver, HttpMethod.Get, "api/v1/driver/work/status", null, ct);
        if (journal!.ReceivingOffDrivers.Contains(driver) && Text(work, "status") != DrivingStatus)
        { if (!journal.StoppedDrivers.Contains(driver)) journal.StoppedDrivers.Add(driver); await SaveAsync(ct); return; }
        if (Text(work, "status") != DrivingStatus)
            await ApiAsync(driver, HttpMethod.Post, "api/v1/driver/work/start", new 기사운행시작요청
            { ClientRequestId = GuidFor(journal!.RunId + "|shift|" + driver), 시작위치 = "가상 픽업 01", 커뮤니티운행공개 = false }, ct);
        var settingsValue = await ApiAsync(driver, HttpMethod.Get, "api/v1/driver/work-participation", null, ct);
        var revision = (long?)Node(Node(settingsValue, "settings"), "revision") ?? 0;
        if (revision == 0)
            await ApiAsync(driver, HttpMethod.Put, "api/v1/driver/work-participation", new DriverWorkParticipationUpdate
            { ExpectedRevision = revision, Settings = new() { QuickDelivery = true, CargoTransport = true,
                VehicleKind = DriverVehicleKinds.Motorcycle, CapacityKg = 20, MaxConcurrentJobs = 1, ReceiveAdditionalOffers = true } }, ct);
        await LocationAsync(driver, index, ct);
        // A resumed run preserves an intentionally disabled driver. New/support actors opt in through the normal API.
        if (!journal!.PreparedDrivers.Contains(driver))
        { await IntentAsync(driver, true, ct); journal.PreparedDrivers.Add(driver); await SaveAsync(ct); }
    }
    private Task<JsonNode?> IntentAsync(string driver, bool on, CancellationToken ct)
        => ApiAsync(driver, HttpMethod.Put, "api/v1/driver/operational-dispatch/availability/intent",
            new 운영배차수신의사변경요청 { 클라이언트요청Id = GuidFor(journal!.RunId + "|intent|" + driver + "|" + on),
                수신의사Code = on ? 운영배차수신의사Code.On : 운영배차수신의사Code.Off }, ct);
    private Task<JsonNode?> LocationAsync(string driver, int index, CancellationToken ct)
        => ApiAsync(driver, HttpMethod.Post, "api/v1/driver/work/location", new 기사위치갱신요청
        { AppKey = 기사앱식별자.CargoYongdalDriverApp, 위도 = 37.58m + index * 0.0001m, 경도 = 127.08m,
            정확도_m = 5, 기록시각 = DateTime.UtcNow }, ct);

    private async Task<JsonNode?> UploadAsync(string actor, long transportId, bool pickup, CancellationToken ct)
    {
        await LoginAsync(actor, ct);
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(pickup ? "TransportPickupComplete" : "TransportDropoffComplete"), "CommandName");
        content.Add(new StringContent(transportId.ToString()), "ReferenceId");
        var bytes = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jmT8AAAAASUVORK5CYII=");
        var image = new ByteArrayContent(bytes); image.Headers.ContentType = new("image/png"); content.Add(image, "File", "synthetic-training.png");
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/v1/files/upload") { Content = content };
        request.Headers.Authorization = new("Bearer", tokens[actor]);
        using var response = await http.SendAsync(request, ct); response.EnsureSuccessStatusCode();
        return JsonNode.Parse(await response.Content.ReadAsStringAsync(ct));
    }
    private async Task LoginAsync(string actor, CancellationToken ct)
    {
        if (tokens.ContainsKey(actor)) return;
        if (DateTime.UtcNow < loginRetryAtUtc) throw new HttpRequestException("TrainingLoginCoolingDown", null, HttpStatusCode.TooManyRequests);
        if (actor != DispatchTrainingSeed.ShipperId && !DispatchTrainingSeed.DriverIds.Contains(actor))
            throw new InvalidOperationException("DispatchTrainingActorInvalid");
        using var response = await http.PostAsJsonAsync("api/v1/auth/login", new { userNameOrEmail = actor, password = settings.AccountPassword }, ct);
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            loginRetryAtUtc = DateTime.UtcNow.Add(response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(60));
            throw new HttpRequestException("TrainingLoginRateLimited", null, response.StatusCode);
        }
        response.EnsureSuccessStatusCode();
        var node = JsonNode.Parse(await response.Content.ReadAsStringAsync(ct));
        tokens[actor] = Text(node, "accessToken") ?? Text(node, "token") ?? throw new HttpRequestException("TrainingLoginTokenMissing");
    }
    private async Task<JsonNode?> ApiAsync(string actor, HttpMethod method, string path, object? body, CancellationToken ct, bool allow404 = false)
    {
        if (!path.StartsWith("api/v1/", StringComparison.Ordinal) || path.Contains("://")) throw new InvalidOperationException("TrainingApiPathInvalid");
        await LoginAsync(actor, ct);
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens[actor]);
        request.Headers.Add("X-App-Key", actor == DispatchTrainingSeed.ShipperId ? "ShipperApp" : "DriverApp");
        if (body is not null) request.Content = JsonContent.Create(body);
        using var response = await http.SendAsync(request, ct);
        if (allow404 && response.StatusCode == HttpStatusCode.NotFound) return null;
        if (!response.IsSuccessStatusCode)
        {
            var failure = await response.Content.ReadAsStringAsync(ct);
            foreach (var secret in tokens.Values.Append(settings.AccountPassword)) failure = failure.Replace(secret, "[omitted]", StringComparison.Ordinal);
            foreach (var pair in settings.Configuration.Where(x => x.Key.Contains("Secret", StringComparison.OrdinalIgnoreCase)
                         || x.Key.Contains("KeyBase64", StringComparison.OrdinalIgnoreCase) || x.Key.Contains("Connection", StringComparison.OrdinalIgnoreCase)))
                if (!string.IsNullOrEmpty(pair.Value)) failure = failure.Replace(pair.Value, "[omitted]", StringComparison.Ordinal);
            await File.WriteAllTextAsync(Path.Combine(settings.ArtifactRoot, "api-failure.json"),
                JsonSerializer.Serialize(new { path, status = (int)response.StatusCode, body = failure[..Math.Min(4096, failure.Length)] }, json), ct);
            throw new HttpRequestException($"TrainingApiStatus:{(int)response.StatusCode}:{path}", null, response.StatusCode);
        }
        var text = await response.Content.ReadAsStringAsync(ct); return string.IsNullOrWhiteSpace(text) ? null : JsonNode.Parse(text);
    }
    private void Incident(ActorOrder? order, string kind, string message)
    {
        var id = (order?.ClientRequestId.ToString("N") ?? journal!.RunId) + ":" + kind;
        if (journal!.Incidents.Any(x => x.IncidentId == id)) return;
        journal.Incidents.Add(new() { IncidentId = id, OrderId = order?.CanonicalId, KindCode = kind,
            StatusCode = "NeedsReview", Message = message, RecordedAtUtc = DateTime.UtcNow });
    }
    private async Task SaveAsync(CancellationToken ct)
    {
        var tmp = JournalPath + ".tmp";
        await File.WriteAllTextAsync(tmp, JsonSerializer.Serialize(journal, json), ct); File.Move(tmp, JournalPath, true);
    }
    private static JsonNode? Node(JsonNode? node, string key) => node is JsonObject o ? o.FirstOrDefault(x => x.Key.Equals(key, StringComparison.OrdinalIgnoreCase)).Value : null;
    private const string DrivingStatus = "운행중";
    private static string? Text(JsonNode? node, string key) => Node(node, key)?.ToString();
    private static decimal? Decimal(JsonNode? node, string key) => decimal.TryParse(Text(node, key), out var value) ? value : null;
    private static Guid GuidFor(string value) => new(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value)).AsSpan(0, 16));
    private static string? Label(string? id) => id is null ? null : "기사 " + id.Split('-').Last();
    public void Dispose() { shadow.Dispose(); http.Dispose(); }

    private sealed class ActorJournal
    {
        public string RunId { get; set; } = ""; public DateTime StartedAtUtc { get; set; }
        public int SupportDrivers { get; set; } public bool ShortageTriggered { get; set; } public int AiAttempts { get; set; }
        public Dictionary<Guid, int> SupportCommands { get; set; } = [];
        public List<string> PreparedDrivers { get; set; } = [];
        public List<string> ReceivingOffDrivers { get; set; } = [];
        public List<string> StoppedDrivers { get; set; } = [];
        public List<string> StopPendingDrivers { get; set; } = [];
        public List<ActorOrder> Orders { get; set; } = [];
        public List<DispatchTrainingIncidentDto> Incidents { get; set; } = [];
        public DispatchTrainingAiResultDto? LatestAi { get; set; }
    }
    private sealed class ActorOrder
    {
        public int Index { get; set; } public Guid ClientRequestId { get; set; } public DateTime CreatedAtUtc { get; set; }
        public string? CanonicalId { get; set; } public bool SubmissionStarted { get; set; } public bool SubmissionRejected { get; set; }
        public bool RejectedOnce { get; set; } public int? IgnoredRound { get; set; }
        public string? LastDecisionFingerprint { get; set; }
        public Dictionary<string, string> Commands { get; set; } = [];
        public Dictionary<string, ActorPhoto> Photos { get; set; } = [];
    }
    private sealed class ActorPhoto { public string ObjectName { get; set; } = ""; public string Url { get; set; } = ""; }
}
