using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Ssalddel.Application.Driver.Transport;
using Ssalddel.Services.Community;
using Ssalddel.Services.Storage;
using 살뜰.Data;
using 살뜰.Services.Dispatch.Queue;
using 살뜰.도메인.공통;
using 살뜰.도메인.운송;
using 살뜰.도메인.화주;

/// <summary>r114 전용 Simulation 호스트에서만 명시적 1회 AI 추천과 읽기 증거를 제공한다.</summary>
internal sealed partial class CargoJourneyControlStartupFilter(CargoJourneySettings settings) : IStartupFilter
{
    private const string Marker = "R114_AI_LEGAL_DONG/";
    private const string RegionHash = "737d57de3bc595aaafaba81b61044f61d76902c0bf3d2391899ac471837a973b";
    private static readonly SemaphoreSlim ModelGate = new(1, 1);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly Regex MarkerPattern = new("R114_AI_LEGAL_DONG/pickup=([0-9]{10});dropoff=([0-9]{10})(?:;|\\s|$)", RegexOptions.CultureInvariant);
    private static readonly string[] AllowedDrivers = [CargoJourneySeed.DriverId, "cargo-journey-driver-b"];

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use(async (context, nextMiddleware) =>
        {
            const string prefix = "/verification/r114";
            if (!context.Request.Path.StartsWithSegments(prefix)) { await nextMiddleware(context); return; }
            context.Response.Headers.CacheControl = "private, no-store";
            context.Response.Headers.XContentTypeOptions = "nosniff";
            var origin = $"http://127.0.0.1:{CargoJourneySettings.HostPort}";
            var supplied = context.Request.Headers["x-r114-key"].ToString();
            if (context.Connection.RemoteIpAddress is not { } remote || !IPAddress.IsLoopback(remote)
                || context.Request.Host.Value != $"127.0.0.1:{CargoJourneySettings.HostPort}"
                || context.Request.Headers.Origin.Count > 0 && context.Request.Headers.Origin.ToString() != origin
                || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(supplied), Encoding.UTF8.GetBytes(settings.AccessKey)))
            { context.Response.StatusCode = 401; return; }
            try
            {
                settings.ValidateEffectiveConfiguration(context.RequestServices.GetRequiredService<IConfiguration>(), context.RequestServices.GetRequiredService<IHostEnvironment>());
                using var scope = context.RequestServices.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<SsalddelContext>();
                settings.ValidateSqlConnection(db.Database.GetDbConnection().ConnectionString);
                var regions = ValidateRegionProjection();
                if (context.Request.Path == prefix + "/proof" && HttpMethods.IsGet(context.Request.Method))
                {
                    var requestId = NormalizeRequestId(context.Request.Query["requestId"].ToString());
                    var (request, queue, tags) = await ReadCaseAsync(db, requestId, regions, context.RequestAborted);
                    var journal = ReadJournal(JournalPath(requestId));
                    var proof = await ProofAsync(scope.ServiceProvider, db, request, queue, tags, journal, context.RequestAborted);
                    WriteEvidence("proofs", requestId, proof);
                    await context.Response.WriteAsJsonAsync(proof, context.RequestAborted);
                    return;
                }
                if (context.Request.Path == prefix + "/ai-recommend" && HttpMethods.IsPost(context.Request.Method))
                {
                    if (context.Request.ContentLength is > 8192) throw Fail("InputTooLarge", 413);
                    using var input = await ReadCommandAsync(context.Request.Body, context.RequestAborted);
                    var props = input.RootElement.EnumerateObject().ToArray();
                    if (props.Length != 2 || props.Select(x => x.Name).Distinct(StringComparer.Ordinal).Count() != 2
                        || !input.RootElement.TryGetProperty("requestId", out var id) || id.ValueKind != JsonValueKind.String
                        || !input.RootElement.TryGetProperty("requestIdempotencyKey", out var key) || key.ValueKind != JsonValueKind.String)
                        throw Fail("InvalidCommand", 400);
                    var requestId = NormalizeRequestId(id.GetString());
                    if (!Guid.TryParse(key.GetString(), out var parsedKey)) throw Fail("InvalidIdempotencyKey", 400);
                    await RecommendAsync(context, scope.ServiceProvider, db, requestId, parsedKey.ToString("D"), regions);
                    return;
                }
                context.Response.StatusCode = 404;
            }
            catch (R114ControlException error)
            {
                context.Response.StatusCode = error.StatusCode;
                await context.Response.WriteAsJsonAsync(new { code = error.Code, synthetic = true, aiApplied = false,
                    fallbackUsed = false, operationalDispatchEnabled = false, deviceVerified = false }, CancellationToken.None);
            }
            catch (JsonException)
            { context.Response.StatusCode = 400; await context.Response.WriteAsJsonAsync(new { code = "InvalidJson" }, CancellationToken.None); }
            catch (Exception error) when (error is not OutOfMemoryException)
            {
                context.Response.StatusCode = error is OperationCanceledException ? 409 : 500;
                await context.Response.WriteAsJsonAsync(new { code = "LocalVerificationNotCompleted", aiApplied = false,
                    fallbackUsed = false, deviceVerified = false }, CancellationToken.None);
            }
        });
        next(app);
    };

    private async Task RecommendAsync(HttpContext context, IServiceProvider services, SsalddelContext db,
        string requestId, string idempotencyKey, Dictionary<string, string> regions)
    {
        if (settings.CargoPrimaryAI)
        {
            await RecommendPrimaryAsync(context, services, db, requestId, idempotencyKey, regions);
            return;
        }
        var ct = context.RequestAborted;
        var path = JournalPath(requestId);
        await using var lease = await AcquireAsync(path + ".lock", ct);
        var commandHash = Hash(new { requestId, requestIdempotencyKey = idempotencyKey });
        var (request, queue, tags) = await ReadCaseAsync(db, requestId, regions, ct);
        var existing = ReadJournal(path);
        if (existing is not null)
        {
            if (existing.RequestId != requestId || existing.CommandSha256 != commandHash || existing.IdempotencyKey != idempotencyKey)
                throw Fail("IdempotencyPayloadConflict");
            // 결과가 불명확한 호출은 재전송하지 않는다. 모델·추천을 다시 실행하지 않고 정본만 읽는다.
            var proof = await ProofAsync(services, db, request, queue, tags, existing, ct);
            WriteEvidence("replays", requestId, proof);
            await context.Response.WriteAsJsonAsync(new { replay = true, result = PublicResult(existing), proof }, ct);
            return;
        }
        if (queue.추천기록존재()) throw Fail("PreexistingRecommendationNotAiProof");
        if (!await ModelGate.WaitAsync(0, ct)) throw Fail("ModelComparisonBusy");
        R114Journal? journal = null;
        try
        {
            var policy = services.GetRequiredService<용달운송배차업무정책>();
            var reviewedAt = DateTime.UtcNow;
            var before = await policy.후보검토Async(queue, cancellationToken: ct);
            var allowed = before.후보목록.Where(x => x.판정Code == "CandidateAllowed" && x.서버후보 is not null)
                .OrderBy(x => x.기사Id, StringComparer.Ordinal).ToArray();
            if (allowed.Any(x => !AllowedDrivers.Contains(x.기사Id, StringComparer.Ordinal))) throw Fail("ForeignCandidateInTestScope");
            if (allowed.Length is < 1 or > 30) throw Fail("NoComparableAllowedCandidate");
            var tokens = allowed.Select((item, index) => (Token: $"C{index + 1}", Item: item))
                .ToDictionary(x => x.Token, x => x.Item, StringComparer.Ordinal);
            var modelInput = ModelInput(tokens, tags, reviewedAt);
            journal = new R114Journal
            {
                RequestId = requestId, IdempotencyKey = idempotencyKey, CommandSha256 = commandHash,
                Phase = "InferenceStarted", CreatedAtUtc = reviewedAt,
                QueueInputSha256 = CaseFingerprint(request, queue), RegionProjectionSha256 = RegionHash,
                SourceSha256 = SourceFingerprint(), CandidatesSha256 = CandidateFingerprint(before),
                BaselineDriverId = before.선정후보?.DriverId, ExpectedRecommendationRound = queue.추천라운드 + 1,
                ModelInput = modelInput, TokenDriverMap = tokens.ToDictionary(x => x.Key, x => x.Value.기사Id, StringComparer.Ordinal)
            };
            SaveJournal(path, journal);
            journal.Selection = await new CargoAiSelector().SelectAsync(modelInput, ct);
            journal.Phase = "ModelReturned";
            SaveJournal(path, journal);
            if (journal.Selection.Status != "valid" || journal.Selection.CandidateToken is null)
            {
                journal.Phase = journal.Selection.Status == "hold" ? "ModelHeld" : "ModelNotApplied";
                SaveJournal(path, journal);
                var proof = await ProofAsync(services, db, request, queue, tags, journal, ct);
                WriteEvidence("proofs", requestId, proof);
                context.Response.StatusCode = 409;
                await context.Response.WriteAsJsonAsync(new { replay = false, result = PublicResult(journal), proof }, ct);
                return;
            }
            if (!tokens.TryGetValue(journal.Selection.CandidateToken, out var selected)) throw Fail("UnmappedModelCandidate");
            journal.SelectedDriverId = selected.기사Id;
            var fresh = await ReadCaseAsync(db, requestId, regions, ct);
            if (journal.SourceSha256 != SourceFingerprint() || journal.QueueInputSha256 != CaseFingerprint(fresh.Request, fresh.Queue))
                throw Fail("InputChangedAfterInference");
            var currentReview = await policy.후보검토Async(fresh.Queue, cancellationToken: ct);
            if (journal.CandidatesSha256 != CandidateFingerprint(currentReview)
                || !currentReview.현재후보허용됨(journal.SelectedDriverId))
                throw Fail("CandidatesChangedAfterInference");
            var currentCandidate = await services.GetRequiredService<I용달운송대상후보Service>()
                .대상후보조회Async(fresh.Queue, journal.SelectedDriverId, ct);
            if (currentCandidate is null) throw Fail("SelectedCandidateNoLongerEligible");
            journal.Phase = "ApplyStarted";
            SaveJournal(path, journal); // 이 지점부터 응답 유실은 결과 조회만으로 복구한다.
            var applied = await services.GetRequiredService<I배차대기원장전환Service>()
                .추천시작Async(requestId, journal.SelectedDriverId, CargoJourneySettings.VerificationRecommendationTtlSeconds, ct);
            journal.TransitionCode = applied.결과코드;
            journal.Applied = applied.전환여부;
            journal.Phase = applied.전환여부 ? "Applied" : "TransitionRejected";
            SaveJournal(path, journal);
            var canonical = await ReadCaseAsync(db, requestId, regions, ct);
            var finalProof = await ProofAsync(services, db, canonical.Request, canonical.Queue, tags, journal, ct);
            WriteEvidence("proofs", requestId, finalProof);
            context.Response.StatusCode = journal.Applied ? 200 : 409;
            await context.Response.WriteAsJsonAsync(new { replay = false, result = PublicResult(journal), proof = finalProof }, ct);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            if (journal is not null)
            {
                // 불명확한 상태는 성공으로 처리하지 않는다. 이전 원장과 앞 단계 사건을 보존한다.
                journal.ErrorCode = error is R114ControlException control ? control.Code : error is OperationCanceledException ? "RequestInterrupted" : "LocalTestException:" + error.GetType().Name;
                if (journal.Phase != "Applied") journal.Phase = journal.Phase == "ApplyStarted" ? "ApplyResultUnknown" : "NotApplied";
                SaveJournal(path, journal);
            }
            throw;
        }
        finally { ModelGate.Release(); }
    }

    private async Task<(화주운송의뢰 Request, 운송원장 Queue, R114RegionTags Tags)> ReadCaseAsync(
        SsalddelContext db, string requestId, Dictionary<string, string> regions, CancellationToken ct)
    {
        var request = await db.화주운송의뢰.AsNoTracking().SingleOrDefaultAsync(x => x.의뢰Id == requestId, ct);
        var queue = await db.운송원장.AsNoTracking().SingleOrDefaultAsync(x => x.의뢰Id == requestId, ct);
        if (request is null || queue is null) throw Fail("SyntheticCaseNotFound", 404);
        if (request.화주Id != CargoJourneySeed.ShipperId || queue.화주Id != CargoJourneySeed.ShipperId
            || queue.배차업무유형 != 상태값.배차업무유형.용달운송 || !request.화물종류.Contains("R114", StringComparison.Ordinal))
            throw Fail("NotR114SyntheticCase", 403);
        var markerSources = new[] { request.요청사항, request.화물설명, queue.메모 };
        var found = markerSources.Where(x => x is not null && x.Contains(Marker, StringComparison.Ordinal))
            .SelectMany(x => MarkerPattern.Matches(x).Cast<Match>()).Select(x => (Pickup: x.Groups[1].Value, Dropoff: x.Groups[2].Value))
            .Distinct().ToArray();
        if (found.Length != 1 || !regions.TryGetValue(found[0].Pickup, out var pickupName) || !regions.TryGetValue(found[0].Dropoff, out var dropoffName))
            throw Fail("LegalDongScenarioTagsMissingOrConflicting");
        return (request, queue, new(found[0].Pickup, pickupName, found[0].Dropoff, dropoffName));
    }

    private Dictionary<string, string> ValidateRegionProjection()
    {
        var path = Path.Combine(settings.RepositoryRoot, "eng/local-dispatch-ai/cargo-regional-training/legal-dong-regions.json");
        var bytes = File.ReadAllBytes(path);
        if (CargoAiSelector.Hash(bytes) != RegionHash) throw Fail("RegionProjectionChanged");
        using var doc = JsonDocument.Parse(bytes);
        if (doc.RootElement.GetProperty("schemaVersion").GetString() != "cargo-regional-training-legal-dong-regions.v1"
            || doc.RootElement.GetProperty("scope").GetProperty("regionType").GetString() != "LegalDong") throw Fail("RegionProjectionSchema");
        var result = doc.RootElement.GetProperty("regions").EnumerateArray().ToDictionary(x => x.GetProperty("legalDongCode").GetString()!, x => x.GetProperty("displayName").GetString()!, StringComparer.Ordinal);
        if (result.Count != 15 || result.Keys.Any(x => !Regex.IsMatch(x, "^[0-9]{10}$", RegexOptions.CultureInvariant))) throw Fail("RegionProjectionCodes");
        return result;
    }

    private static CargoAiInput ModelInput(Dictionary<string, 용달운송후보검토항목> tokens, R114RegionTags tags, DateTime now)
        => new(tags.PickupCode, tags.DropoffCode, tokens.Select(pair => new CargoAiCandidate(pair.Key, pair.Value.거리Km,
            pair.Value.기본추천점수, pair.Value.대기점수, pair.Value.추천점수,
            MinutesAfter(pair.Value.기존수행완료예상시각Utc, now), MinutesAfter(pair.Value.픽업도착예상시각Utc, now))).ToArray());

    private static decimal? MinutesAfter(DateTime? at, DateTime now)
        => at is null ? null : Math.Round(Math.Max(0m, (decimal)(DateTime.SpecifyKind(at.Value, DateTimeKind.Utc) - now).TotalMinutes), 3);

    private static string CaseFingerprint(화주운송의뢰 request, 운송원장 queue) => Hash(new
    {
        request.의뢰Id, request.화주Id, request.상태, request.결제상태, request.정산상태, request.최종운임,
        request.화물종류, request.화물중량Kg, request.화물부피Cbm, request.화물수량, request.화물길이Mm,
        request.화물폭Mm, request.화물높이Mm, request.화물온도조건, request.운송방식, request.차량종류,
        noteDigest = CargoAiSelector.Hash(Encoding.UTF8.GetBytes(request.요청사항 + "\n" + request.화물설명)),
        request.픽업_시간창_시작일시, request.픽업_시간창_종료일시, request.하차_시간창_시작일시, request.하차_시간창_종료일시,
        request.UpdatedAt, queueState = new
        {
            queue.Id, queue.RowVersion, queue.UpdatedAt, queue.배차업무유형, queue.상태, queue.배차큐단계,
            queue.배차노출상태, queue.현재추천대상기사Id, queue.확정기사Id, queue.기사_운송자, queue.추천라운드,
            queue.추천시작시각, queue.추천만료시각, queue.마지막거절기사Id, queue.생활배송배차방식, queue.생활배송선택판본,
            queue.생활배송수락준비완료, queue.원본의뢰유형, queue.원본의뢰Id, queue.운임,
            queue.픽업_위도, queue.픽업_경도, queue.하차_위도, queue.하차_경도,
            pickupAddressDigest = CargoAiSelector.Hash(Encoding.UTF8.GetBytes(queue.픽업_도로명주소 + "\n" + queue.픽업_상세주소)),
            dropoffAddressDigest = CargoAiSelector.Hash(Encoding.UTF8.GetBytes(queue.하차_도로명주소 + "\n" + queue.하차_상세주소))
        }
    });

    private static string CandidateFingerprint(용달운송후보검토결과 reviewed) => Hash(new
    {
        reviewed.중단사유Code,
        candidates = reviewed.후보목록.OrderBy(x => x.기사Id, StringComparer.Ordinal).Select(x => new
        {
            x.기사Id, x.판정Code, x.거리Km, x.기본추천점수, x.대기점수, x.추천점수, x.픽업기준Code,
            x.기존수행완료예상시각Utc, x.픽업도착예상시각Utc
        })
    });

    private string SourceFingerprint()
    {
        var relative = new[] { "eng/CargoAiDeviceVerificationR114/CargoAiSelector.cs", "eng/CargoAiDeviceVerificationR114/CargoJourneyControlStartupFilter.cs",
            "Ssalddel/Services/Dispatch/Queue/용달운송배차업무정책.cs", "Ssalddel/Services/Dispatch/Queue/용달운송후보검토결과.cs",
            "Ssalddel/Services/Dispatch/Queue/배차대기원장전환Service.cs", "Ssalddel/Services/Dispatch/Queue/배차대기원장전환Service.Recommendation.cs" };
        if (settings.CargoPrimaryAI)
            relative = relative.Concat(new[]
            {
                "eng/CargoAiDeviceVerificationR114/CargoJourneyControlStartupFilter.PrimaryAI.cs",
                "eng/CargoAiDeviceVerificationR114/CargoJourneyFactory.cs",
                "eng/CargoAiDeviceVerificationR114/R114Seed.cs",
                "eng/Ssalddel.DispatchTraining/DispatchTrainingFixtureAdapters.cs",
                "Ssalddel/Services/Dispatch/Queue/용달운송배차업무정책.AI.cs",
                "Ssalddel/Services/Dispatch/Queue/용달운송배차업무정책.IdlePickup.cs",
                "Ssalddel/Services/Dispatch/AI/OllamaCargoDispatchCandidateAI.cs",
                "Ssalddel/Services/Dispatch/AI/CargoDispatchCandidateSelection.cs"
            }).ToArray();
        var snapshot = relative.OrderBy(x => x, StringComparer.Ordinal).Select(path => new { path, sha256 = CargoAiSelector.Hash(File.ReadAllBytes(Path.Combine(settings.RepositoryRoot, path))) }).ToArray();
        WriteEvidence("sources", "source", snapshot);
        return Hash(snapshot);
    }

    private async Task<object> ProofAsync(IServiceProvider services, SsalddelContext db, 화주운송의뢰 request,
        운송원장 queue, R114RegionTags tags, R114Journal? journal, CancellationToken ct)
    {
        var outbox = await db.배차추천알림Outbox.AsNoTracking().Where(x => x.의뢰Id == request.의뢰Id)
            .OrderBy(x => x.Id).Select(x => new { x.Id, driverId = x.기사Id, round = x.추천라운드,
                status = x.발송상태, attempts = x.시도횟수, readAtUtc = x.읽은시각, createdAtUtc = x.CreatedAt }).ToArrayAsync(ct);
        var audits = await db.운송이벤트.AsNoTracking().Where(x => x.의뢰Id == request.의뢰Id && x.이벤트타입 == 운송이벤트유형.배차엔진판단감사)
            .OrderBy(x => x.Id).Select(x => new { x.Id, x.이벤트시각, x.메타데이터 }).ToArrayAsync(ct);
        var photos = await PhotoProofAsync(services.GetRequiredService<IObjectStorageService>(), queue, ct);
        var primary = settings.CargoPrimaryAI ? await PrimaryProofAsync(db, request.의뢰Id, queue, ct) : null;
        var candidateObservation = settings.CargoPrimaryAI
            ? WriteCandidateObservation(request.의뢰Id, await services.GetRequiredService<용달운송배차업무정책>().후보검토Async(queue, cancellationToken: ct))
            : null;
        object mongo;
        try
        {
            var state = await services.GetRequiredService<I운송원장Mongo동기화Service>().상태조회Async(request.의뢰Id, ct);
            mongo = new { available = true, ledgerId = state.원장Id, exists = state.Mongo원장존재, state = state.Mongo원장상태,
                stepKey = state.현재단계Key, rdbProjectionExists = state.Rdb운송실행투영존재 };
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch { mongo = new { available = false, code = "MongoProofUnavailable" }; }
        var canonicalMatches = journal?.SelectedDriverId is { } selected && queue.추천라운드 == journal.ExpectedRecommendationRound
            && (queue.현재추천대상기사Id == selected || queue.확정기사Id == selected || queue.기사_운송자 == selected);
        var applicationAttemptRecorded = ApplicationAttemptRecorded(journal);
        return new
        {
            schemaVersion = "r114-ai-cargo-canonical-proof.v1", atUtc = DateTime.UtcNow, runStableId = settings.RunStableId,
            database = CargoJourneySettings.DatabaseName, synthetic = true, legalDongTags = tags,
            regionProjectionSha256 = RegionHash, legalDongSnapshotCollectedDate = "2026-08-11", sourceEffectiveDate = (string?)null,
            actualBoundaryVerified = false, actualRouteVerified = false, deviceVerified = false, weightsTrained = false,
            productionDispatchEnabled = false, actualPaymentVerified = false, actualFcmVerified = false,
            fallbackUsed = primary?.FallbackUsed ?? false,
            cargoPrimaryAI = settings.CargoPrimaryAI, actualCargoAIStatus = primary?.Status ?? "NotPrimaryProfile", actualCargoAI = primary,
            currentCandidateObservation = candidateObservation,
            request = new { id = request.의뢰Id, state = request.상태, paymentState = request.결제상태, settlementState = request.정산상태 },
            transport = new { id = queue.Id, state = queue.상태, stage = queue.배차큐단계, exposure = queue.배차노출상태,
                selectedDriverId = queue.현재추천대상기사Id, confirmedDriverId = queue.확정기사Id, performingDriverId = queue.기사_운송자,
                round = queue.추천라운드, expiresAtUtc = queue.추천만료시각, frozenPayout = queue.기사지급예정액 },
            aiAttempt = journal is null ? null : PublicResult(journal),
            aiSelectionMatchesCanonical = primary?.CanonicalMatches ?? canonicalMatches,
            aiApplicationAttemptRecorded = primary is null ? applicationAttemptRecorded : primary.Phase is "ApplyStarted" or "ApplyResultUnknown" or "Applied",
            aiApplyResponseConfirmed = primary is null ? journal is { Phase: "Applied", Applied: true } : primary is { Phase: "Applied", RecommendationApplied: true },
            aiRecommendationVerified = primary?.AiSelectionApplied ?? (applicationAttemptRecorded && canonicalMatches),
            notificationOutbox = outbox,
            decisionAudits = audits.Select(x => new { x.Id, atUtc = x.이벤트시각, payloadSha256 = CargoAiSelector.Hash(Encoding.UTF8.GetBytes(x.메타데이터)) }),
            photoEvidence = photos, mongo
        };
    }

    /// <summary>추천 적용 전에 끝난 모델 비교·거절을 나중의 우연한 기사/차수 일치로 승격하지 않는다.</summary>
    internal static bool ApplicationAttemptRecorded(R114Journal? journal)
    {
        if (journal?.Selection is not { Status: "valid", CandidateToken: { Length: > 0 } token }
            || journal.SelectedDriverId is not { Length: > 0 } driver || journal.ExpectedRecommendationRound <= 0
            || !journal.TokenDriverMap.TryGetValue(token, out var mapped) || mapped != driver)
            return false;
        return journal.Phase is "ApplyStarted" or "ApplyResultUnknown"
            || journal is { Phase: "Applied", Applied: true, TransitionCode: 배차대기원장전환결과코드.추천시작됨 };
    }

    private static async Task<IReadOnlyList<object>> PhotoProofAsync(IObjectStorageService storage, 운송원장 queue, CancellationToken ct)
    {
        var result = new List<object>();
        try
        {
            using var attachments = JsonDocument.Parse(queue.첨부_json);
            if (attachments.RootElement.ValueKind != JsonValueKind.Array) return [new { code = "PhotoAttachmentsNotArray" }];
            var entries = attachments.RootElement.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.Object).ToArray();
            foreach (var receipt in entries.Where(x => String(x, "kind") == 화물운송사진증빙Policy.UploadReceiptKind).Take(20))
            {
                var kind = String(receipt, "photoKind");
                var objectName = String(receipt, "objectName");
                var digest = String(receipt, "sha256");
                var length = receipt.TryGetProperty("bytes", out var size) && size.TryGetInt64(out var count) ? count : -1;
                var bound = receipt.TryGetProperty("transportId", out var id) && id.TryGetInt64(out var transportId) && transportId == queue.Id
                    && String(receipt, "requestId") == queue.의뢰Id && String(receipt, "uploadedBy") == queue.기사_운송자
                    && receipt.TryGetProperty("recommendationRound", out var round) && round.TryGetInt32(out var n) && n == queue.추천라운드;
                var completionLinked = entries.Any(x => String(x, "kind") == kind && String(x, "objectName") == objectName
                    && String(x, "uploadedBy") == queue.기사_운송자);
                var storageVerified = false;
                if (bound && length is > 0 and <= 화물운송사진증빙Policy.MaximumBytes && digest.Length == 64 && objectName.Length is > 0 and < 1024)
                {
                    try
                    {
                        var bytes = await storage.DownloadAsync(String(receipt, "containerName"), objectName, ct);
                        storageVerified = bytes.LongLength == length && 화물운송사진증빙Policy.Sha(bytes) == digest && 화물운송사진증빙Policy.IsPhoto(bytes);
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                    catch { /* 저장소 미확인은 업로드·완료 증거로 승격하지 않는다. */ }
                }
                result.Add(new { photoKind = kind, sha256 = digest, bytes = length, bindingVerified = bound,
                    completionLinked, storageIntegrityVerified = storageVerified, actualCameraCaptureVerifiedByThisEndpoint = false });
            }
        }
        catch (JsonException) { result.Add(new { code = "PhotoAttachmentsInvalid" }); }
        return result;
    }

    private string JournalPath(string requestId)
    {
        var directory = Path.Combine(settings.ArtifactRoot, ".private", "r114-ai");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, CargoAiSelector.Hash(Encoding.UTF8.GetBytes(requestId)) + ".json");
    }

    private static R114Journal? ReadJournal(string path)
    {
        if (!File.Exists(path)) return null;
        try
        {
            var value = JsonSerializer.Deserialize<R114Journal>(File.ReadAllBytes(path), Json) ?? throw Fail("AttemptJournalInvalid");
            if (value.Schema != "r114-ai-attempt.v1" || value.Sequence < 1 || string.IsNullOrWhiteSpace(value.LastEventSha256)) throw Fail("AttemptJournalInvalid");
            var eventPath = Path.Combine(path + ".events", $"{value.Sequence:D4}-{value.LastEventSha256}.json");
            if (!File.Exists(eventPath) || CargoAiSelector.Hash(File.ReadAllBytes(eventPath)) != value.LastEventSha256) throw Fail("AttemptJournalEventMissing");
            using var lastEvent = JsonDocument.Parse(File.ReadAllBytes(eventPath));
            var currentHash = value.LastEventSha256;
            value.LastEventSha256 = lastEvent.RootElement.GetProperty("previousEventSha256").GetString();
            if (Hash(value) != Hash(lastEvent.RootElement.GetProperty("attempt"))) throw Fail("AttemptJournalStateMismatch");
            value.LastEventSha256 = currentHash;
            return value;
        }
        catch (JsonException) { throw Fail("AttemptJournalInvalid"); }
    }

    private static void SaveJournal(string path, R114Journal journal)
    {
        journal.Sequence++;
        journal.UpdatedAtUtc = DateTime.UtcNow;
        var eventBytes = JsonSerializer.SerializeToUtf8Bytes(new { previousEventSha256 = journal.LastEventSha256, attempt = journal }, Json);
        var digest = CargoAiSelector.Hash(eventBytes);
        var eventDirectory = path + ".events";
        Directory.CreateDirectory(eventDirectory);
        DurableWrite(Path.Combine(eventDirectory, $"{journal.Sequence:D4}-{digest}.json"), eventBytes, overwrite: false);
        journal.LastEventSha256 = digest;
        DurableWrite(path, JsonSerializer.SerializeToUtf8Bytes(journal, Json), overwrite: true);
    }

    private string WriteEvidence(string category, string requestId, object evidence)
    {
        var directory = Path.Combine(settings.ArtifactRoot, ".private", "r114-ai", category);
        Directory.CreateDirectory(directory);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(evidence, Json);
        var hash = CargoAiSelector.Hash(bytes);
        var path = Path.Combine(directory, $"{CargoAiSelector.Hash(Encoding.UTF8.GetBytes(requestId))[..16]}-{hash}.json");
        if (File.Exists(path))
        {
            if (!File.ReadAllBytes(path).AsSpan().SequenceEqual(bytes)) throw Fail("EvidenceCollision");
        }
        else DurableWrite(path, bytes, overwrite: false);
        return hash;
    }

    private static void DurableWrite(string path, byte[] bytes, bool overwrite)
    {
        var temporary = path + ".writing-" + Guid.NewGuid().ToString("N");
        using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { file.Write(bytes); file.Flush(flushToDisk: true); }
        File.Move(temporary, path, overwrite);
    }

    private static async Task<FileStream> AcquireAsync(string path, CancellationToken ct)
    {
        for (var attempt = 0; ; attempt++)
        {
            try { return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException) when (attempt < 20) { await Task.Delay(25, ct); }
            catch (IOException) { throw Fail("RequestInProgress"); }
        }
    }

    private static async Task<JsonDocument> ReadCommandAsync(Stream input, CancellationToken ct)
    {
        using var bytes = new MemoryStream();
        var buffer = new byte[1024];
        while (true)
        {
            var count = await input.ReadAsync(buffer, ct);
            if (count == 0) break;
            if (bytes.Length + count > 8192) throw Fail("InputTooLarge", 413);
            bytes.Write(buffer, 0, count);
        }
        return JsonDocument.Parse(bytes.ToArray(), new JsonDocumentOptions { MaxDepth = 4 });
    }

    private static object PublicResult(R114Journal journal) => new
    {
        journal.RequestId, journal.Phase, journal.CommandSha256, journal.SourceSha256, journal.QueueInputSha256,
        journal.CandidatesSha256, journal.BaselineDriverId, journal.SelectedDriverId, journal.ExpectedRecommendationRound,
        journal.Applied, journal.TransitionCode, journal.ErrorCode, journal.Sequence, journal.LastEventSha256,
        model = journal.Selection is null ? null : new { journal.Selection.Status, journal.Selection.CandidateToken, journal.Selection.ReasonCodes,
            journal.Selection.Model, journal.Selection.ModelDigest, journal.Selection.InputSha256, journal.Selection.PromptSha256,
            journal.Selection.ElapsedMilliseconds, journal.Selection.ErrorCode, journal.Selection.RunId },
        fallbackUsed = false, weightsTrained = false, deviceVerified = false
    };

    private static string NormalizeRequestId(string? value) => Guid.TryParse(value, out var id) ? id.ToString("D") : throw Fail("InvalidRequestId", 400);
    private static string String(JsonElement obj, string key) => obj.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : "";
    private static string Hash<T>(T value) => CargoAiSelector.Hash(JsonSerializer.SerializeToUtf8Bytes(value, Json));
    private static R114ControlException Fail(string code, int status = 409) => new(code, status);
}

internal sealed record R114RegionTags(string PickupCode, string PickupName, string DropoffCode, string DropoffName)
{
    public string Meaning => "scenario_labels_from_20260811_snapshot_not_spatial_assignments";
}

internal sealed class R114Journal
{
    public string Schema { get; init; } = "r114-ai-attempt.v1";
    public string RequestId { get; init; } = "";
    public string IdempotencyKey { get; init; } = "";
    public string CommandSha256 { get; init; } = "";
    public string Phase { get; set; } = "not_started";
    public DateTime CreatedAtUtc { get; init; }
    public DateTime UpdatedAtUtc { get; set; }
    public string QueueInputSha256 { get; init; } = "";
    public string CandidatesSha256 { get; init; } = "";
    public string SourceSha256 { get; init; } = "";
    public string RegionProjectionSha256 { get; init; } = "";
    public string? BaselineDriverId { get; init; }
    public int ExpectedRecommendationRound { get; init; }
    public CargoAiInput? ModelInput { get; init; }
    public Dictionary<string, string> TokenDriverMap { get; init; } = [];
    public CargoAiSelection? Selection { get; set; }
    public string? SelectedDriverId { get; set; }
    public bool Applied { get; set; }
    public string? TransitionCode { get; set; }
    public string? ErrorCode { get; set; }
    public int Sequence { get; set; }
    public string? LastEventSha256 { get; set; }
}

internal sealed class R114ControlException(string code, int statusCode) : Exception(code)
{
    internal string Code { get; } = code;
    internal int StatusCode { get; } = statusCode;
}

internal static class R114CandidateChecks
{
    internal static bool 현재후보허용됨(this 용달운송후보검토결과 reviewed, string driverId)
        => reviewed.후보목록.Any(x => x.기사Id == driverId && x.판정Code == "CandidateAllowed" && x.서버후보 is not null);
    internal static bool 추천기록존재(this 운송원장 queue)
        => !string.IsNullOrEmpty(queue.현재추천대상기사Id) || !string.IsNullOrEmpty(queue.확정기사Id)
            || queue.배차큐단계 == 상태값.배차큐단계.확정 || !string.IsNullOrEmpty(queue.기사_운송자);
}
