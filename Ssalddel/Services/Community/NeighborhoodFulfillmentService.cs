using System.Security.Cryptography;
using System.Data;
using System.Text;
using System.Text.Json;
using FluentResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Application.Shipper.Request;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Shipper.Request;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Domain.Community;
using 살뜰.Data;
using 살뜰.Services.Options;

namespace Ssalddel.Services.Community;

public interface I생활배송처리처Service
{
    bool Enabled { get; }
    Task<IReadOnlyList<NeighborhoodFulfillmentOption>> 견적Async(NeighborhoodDeliveryRequest request, 화주운송기준운임견적응답 fare, string? requestId, CancellationToken ct);
    Task<Result> 선택검사Async(NeighborhoodDeliveryRequest request, CancellationToken ct);
    Task<Result<NeighborhoodFulfillmentSnapshot>> 최초확정Async(string requestId, NeighborhoodDeliveryRequest request, CancellationToken ct);
    Task<NeighborhoodFulfillmentSnapshot?> 내상태Async(string requestId, CancellationToken ct);
    Task<Result<NeighborhoodDeliveryQuoteResponse>> 내견적Async(string requestId, CancellationToken ct);
    Task<Result<NeighborhoodFulfillmentSnapshot>> 선택Async(string requestId, NeighborhoodFulfillmentChoiceRequest request, CancellationToken ct);
    Task<Result<NeighborhoodFulfillmentSnapshot>> 새로고침Async(string requestId, NeighborhoodFulfillmentActionRequest request, CancellationToken ct);
    Task<Result<NeighborhoodFulfillmentSnapshot>> 취소Async(string requestId, NeighborhoodFulfillmentActionRequest request, CancellationToken ct);
}

/// <summary>서버가 선택·시도를 저장한 뒤 한 공급자를 호출합니다. 불명 결과는 원 시도의 조회로 회복하며 자동 대체 접수를 하지 않습니다.</summary>
[SsalddelCodeMetadata(SsalddelCodeFeatureKeys.OperationalLogisticsOs, SsalddelCodeLayer.Application,
    "생활 배송 처리처의 확인 견적과 단일 수행 시도를 SQL에 보존하고 모의 위탁과 자체 배차를 연결한다.",
    Effects = SsalddelCodeEffect.PersistentRead | SsalddelCodeEffect.PersistentWrite,
    StepKey = "application.neighborhood-fulfillment", FlowOrder = 35,
    ReadsFrom = SsalddelCodeDataScope.OperationalState, WritesTo = SsalddelCodeDataScope.OperationalState,
    Boundary = "Simulation 기능 관문을 통과한 모의 흐름이다. 실제 업체 API·지급·정산 완료를 확정하지 않는다.")]
public sealed class 생활배송처리처Service(SsalddelContext db, ICurrentUserAccessor currentUser,
    IEnumerable<IFulfillmentProvider> providerServices, IOptions<NeighborhoodFulfillmentOptions> options,
    ISsalddelExecutionModePolicy execution, TimeProvider clock, I화주운송의뢰UseCase transport) : I생활배송처리처Service
{
    private readonly IReadOnlyDictionary<string, IFulfillmentProvider> providers = providerServices.ToDictionary(x => x.Id, StringComparer.Ordinal);
    public bool Enabled => options.Value.Enabled && execution.IsSimulation;
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    public async Task<IReadOnlyList<NeighborhoodFulfillmentOption>> 견적Async(NeighborhoodDeliveryRequest request, 화주운송기준운임견적응답 fare, string? requestId, CancellationToken ct)
    {
        if (!Enabled || string.IsNullOrWhiteSpace(currentUser.UserId)) return [];
        var input = new FulfillmentQuoteInput(request.WeightKg, fare.예상거리Km, fare.최종운임,
            request.Pickup.시간창?.시작일시 ?? Now, request.DesiredCompletionUtc, request.VehicleType, request.Quantity);
        var result = new List<NeighborhoodFulfillmentOption>();
        foreach (var provider in providers.Values)
        {
            FulfillmentProviderQuote quote;
            try { quote = await provider.QuoteAsync(input, ct); }
            catch (Exception ex) when (ex is HttpRequestException or TimeoutException) { quote = new(false, null, null, "처리처 견적을 확인하지 못했습니다."); }
            var available = quote.IsAvailable && quote.CostKrw is >= 0 && quote.CompletionUtc is not null
                && quote.CompletionUtc >= Now && (request.DesiredCompletionUtc is null || quote.CompletionUtc <= request.DesiredCompletionUtc);
            var id = Guid.NewGuid().ToString("N"); var expires = Now.AddMinutes(Math.Clamp(options.Value.QuoteLifetimeMinutes, 1, 30));
            result.Add(new() { ProviderId = provider.Id, ProviderName = provider.Name, QuoteId = id, IsSimulation = provider.IsSimulation,
                IsAvailable = available, UnavailableReason = available ? "" : string.IsNullOrWhiteSpace(quote.Reason) ? "비용·완료 시간을 확인할 수 없습니다." : quote.Reason,
                TotalCostKrw = quote.CostKrw, EstimatedCompletionUtc = quote.CompletionUtc, ExpiresAtUtc = expires });
            if (available) db.NeighborhoodFulfillmentQuotes.Add(new() { QuoteId = id, ActorId = currentUser.UserId!, RequestId = requestId,
                InputDigest = InputDigest(request), ProviderId = provider.Id, CostKrw = quote.CostKrw!.Value,
                WeightKg = input.WeightKg, DistanceKm = input.DistanceKm, DesiredCompletionUtc = request.DesiredCompletionUtc,
                EstimatedCompletionUtc = quote.CompletionUtc!.Value, ExpiresAtUtc = expires, IsSimulation = provider.IsSimulation });
        }
        await db.SaveChangesAsync(ct);
        var sorted = result.OrderBy(x => !x.IsAvailable).ThenBy(x => x.TotalCostKrw ?? decimal.MaxValue)
            .ThenBy(x => x.EstimatedCompletionUtc ?? DateTime.MaxValue).ThenBy(x => x.ProviderId, StringComparer.Ordinal).ToArray();
        var recommendation = sorted.FirstOrDefault(x => x.IsAvailable);
        if (recommendation is not null) recommendation.IsRecommended = true;
        return sorted;
    }

    public async Task<Result> 선택검사Async(NeighborhoodDeliveryRequest request, CancellationToken ct)
    {
        if (!Enabled) return Fail("FulfillmentDisabled", "처리처 선택은 활성화된 Simulation 검증 환경에서만 사용할 수 있습니다.", 403);
        var selected = request.FulfillmentSelection;
        if (selected is null) return Fail("FulfillmentSelectionRequired", "처리처와 비용을 확인해 주세요.", 400);
        return await CheckQuoteAsync(selected, InputDigest(request), null, ct);
    }

    public async Task<Result<NeighborhoodFulfillmentSnapshot>> 최초확정Async(string requestId, NeighborhoodDeliveryRequest request, CancellationToken ct)
    {
        var result = await SelectAsync(requestId, new() { ClientRequestId = request.ClientRequestId, ExpectedRevision = 0,
            Selection = request.FulfillmentSelection! }, InputDigest(request), ct);
        // 원 의뢰 저장 후 프로세스가 중단되어 견적이 만료됐을 때 원 신청을 바꾸거나 재접수하지 않습니다.
        if (result.IsFailed && result.Errors.Any(x => x.Metadata.TryGetValue("ErrorCode", out var code) && code as string == "FulfillmentQuoteExpired")
            && await 내상태Async(requestId, ct) is { StateCode: NeighborhoodFulfillmentStates.AwaitingSelection } pending)
            return Result.Ok(pending);
        return result;
    }

    public async Task<Result<NeighborhoodDeliveryQuoteResponse>> 내견적Async(string requestId, CancellationToken ct)
    {
        if (!Enabled) return Fail<NeighborhoodDeliveryQuoteResponse>("FulfillmentDisabled", "처리처 선택을 사용할 수 없습니다.", 403);
        var source = await OwnedAsync(requestId, ct);
        if (source is null) return Fail<NeighborhoodDeliveryQuoteResponse>("RequestNotFound", "본인 생활 배송을 찾을 수 없습니다.", 404);
        if (source.상태 == 살뜰.도메인.공통.상태값.의뢰상태.취소)
            return Fail<NeighborhoodDeliveryQuoteResponse>("FulfillmentRequestClosed", "취소된 의뢰는 새 처리처에 접수할 수 없습니다.");
        var current = await db.NeighborhoodFulfillments.AsNoTracking().SingleOrDefaultAsync(x => x.SourceKind == "NeighborhoodDelivery" && x.RequestId == requestId, ct);
        if (source.자동처리처수행 || current?.IsAutomatic == true) return Fail<NeighborhoodDeliveryQuoteResponse>("AutomaticFulfillmentManaged", "자동 수행 조건과 상태 화면을 이용해 주세요.");
        if (current is null && (!source.모의처리처수행 || await db.운송원장.AsNoTracking().AnyAsync(x => x.의뢰Id == requestId, ct)))
            return Fail<NeighborhoodDeliveryQuoteResponse>("LegacyFulfillmentLocked", "기존 의뢰의 배차 절차를 먼저 확인해 주세요.");
        var old = current is null ? null : await db.NeighborhoodFulfillmentAttempts.AsNoTracking().SingleAsync(x => x.AttemptId == current.AttemptId, ct);
        if (old is not null && !CanChange(old)) return Fail<NeighborhoodDeliveryQuoteResponse>("FulfillmentLocked", "이전 수행의 취소 또는 실패 확인 후 처리처를 다시 선택할 수 있습니다.");
        var input = FromSource(source, old?.DesiredCompletionUtc ?? source.모의처리희망완료Utc);
        var fare = await transport.기준운임견적Async(new() { 차량종류 = source.차량종류, 상차위도 = source.픽업_위도,
            상차경도 = source.픽업_경도, 하차위도 = source.하차_위도, 하차경도 = source.하차_경도 }, ct);
        if (fare.IsFailed) return Result.Fail<NeighborhoodDeliveryQuoteResponse>(fare.Errors);
        return Result.Ok(new NeighborhoodDeliveryQuoteResponse { Fare = fare.Value, FulfillmentOptions = await 견적Async(input, fare.Value, requestId, ct) });
    }

    public Task<Result<NeighborhoodFulfillmentSnapshot>> 선택Async(string requestId, NeighborhoodFulfillmentChoiceRequest request, CancellationToken ct)
        => SelectAsync(requestId, request, null, ct);

    private async Task<Result<NeighborhoodFulfillmentSnapshot>> SelectAsync(string requestId, NeighborhoodFulfillmentChoiceRequest request, string? initialDigest, CancellationToken ct)
    {
        if (!Enabled) return Fail<NeighborhoodFulfillmentSnapshot>("FulfillmentDisabled", "처리처 선택은 Simulation 검증 환경에서만 사용할 수 있습니다.", 403);
        if (request.ClientRequestId == Guid.Empty) return Fail<NeighborhoodFulfillmentSnapshot>("InvalidFulfillmentChoice", "요청 번호가 필요합니다.", 400);
        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        var source = await OwnedAsync(requestId, ct);
        if (source is null) return Fail<NeighborhoodFulfillmentSnapshot>("RequestNotFound", "본인 생활 배송을 찾을 수 없습니다.", 404);
        if (source.자동처리처수행) return Fail<NeighborhoodFulfillmentSnapshot>("AutomaticFulfillmentManaged", "자동 수행 조건과 상태 화면을 이용해 주세요.");
        if (source.상태 == 살뜰.도메인.공통.상태값.의뢰상태.취소)
            return Fail<NeighborhoodFulfillmentSnapshot>("FulfillmentRequestClosed", "취소된 의뢰는 새 처리처에 접수할 수 없습니다.");
        var digest = Digest(request.Selection);
        var replay = await db.NeighborhoodFulfillmentAttempts.AsNoTracking().SingleOrDefaultAsync(x => x.SourceKind == "NeighborhoodDelivery" && x.RequestId == requestId && x.ClientRequestId == request.ClientRequestId, ct);
        if (replay is not null)
        {
            if (replay.SelectionDigest != digest) return Fail<NeighborhoodFulfillmentSnapshot>("IdempotencyConflict", "같은 요청 번호로 다른 처리처를 선택할 수 없습니다.");
            return Result.Ok((await 내상태Async(requestId, ct))!);
        }
        var current = await db.NeighborhoodFulfillments.SingleOrDefaultAsync(x => x.SourceKind == "NeighborhoodDelivery" && x.RequestId == requestId, ct);
        NeighborhoodFulfillmentAttemptRecord? previous = null;
        if (current is not null)
        {
            if (current.IsAutomatic) return Fail<NeighborhoodFulfillmentSnapshot>("AutomaticFulfillmentManaged", "자동 수행 조건과 상태 화면을 이용해 주세요.");
            await db.Entry(current).ReloadAsync(ct);
            previous = await db.NeighborhoodFulfillmentAttempts.SingleAsync(x => x.AttemptId == current.AttemptId, ct);
            await db.Entry(previous).ReloadAsync(ct);
            if (!CanChange(previous)) return Fail<NeighborhoodFulfillmentSnapshot>("FulfillmentLocked", "이전 수행 결과를 먼저 확인해 주세요. 다른 처리처에 중복 접수하지 않습니다.");
        }
        else if (initialDigest is null && (!source.모의처리처수행 || await db.운송원장.AsNoTracking().AnyAsync(x => x.의뢰Id == requestId, ct)))
            return Fail<NeighborhoodFulfillmentSnapshot>("LegacyFulfillmentLocked", "기존 자체 배차 의뢰는 이 화면에서 재위탁할 수 없습니다.");
        if ((current?.Revision ?? 0) != request.ExpectedRevision) return Fail<NeighborhoodFulfillmentSnapshot>("FulfillmentRevisionConflict", "처리 상태가 바뀌었습니다. 최신 상태를 확인해 주세요.");
        var quoteDigest = initialDigest ?? InputDigest(FromSource(source, previous?.DesiredCompletionUtc ?? source.모의처리희망완료Utc));
        var check = await CheckQuoteAsync(request.Selection, quoteDigest, initialDigest is null ? requestId : null, ct);
        if (check.IsFailed) return Result.Fail<NeighborhoodFulfillmentSnapshot>(check.Errors);
        var quote = await db.NeighborhoodFulfillmentQuotes.AsNoTracking().SingleAsync(x => x.QuoteId == request.Selection.QuoteId, ct);
        var attempt = new NeighborhoodFulfillmentAttemptRecord { AttemptId = Guid.NewGuid().ToString("N"), RequestId = requestId,
            ClientRequestId = request.ClientRequestId, SelectionDigest = digest, QuoteId = quote.QuoteId, ProviderId = quote.ProviderId,
            StateCode = NeighborhoodFulfillmentStates.Submitting, CostKrw = quote.CostKrw, IsSimulation = quote.IsSimulation,
            SharingAgreed = request.Selection.ProviderInformationSharingAgreed, CreatedAtUtc = Now, UpdatedAtUtc = Now,
            DesiredCompletionUtc = quote.DesiredCompletionUtc, EstimatedCompletionUtc = quote.EstimatedCompletionUtc, Revision = 1 };
        AppendHistory(attempt, attempt.StateCode);
        db.NeighborhoodFulfillmentAttempts.Add(attempt);
        if (current is null) { current = new() { RequestId = requestId, ActorId = currentUser.UserId!, Revision = 0 }; db.NeighborhoodFulfillments.Add(current); }
        current.AttemptId = attempt.AttemptId; current.Revision++; current.UpdatedAtUtc = Now;
        try { await db.SaveChangesAsync(ct); if (transaction is not null) await transaction.CommitAsync(ct); }
        catch (DbUpdateException) { db.ChangeTracker.Clear(); return Fail<NeighborhoodFulfillmentSnapshot>("FulfillmentRevisionConflict", "다른 처리처 확정이 먼저 저장됐습니다. 최신 상태를 조회해 주세요."); }
        // SQL 저장 이후 호출. 응답 유실 또는 프로세스 종료에도 Submitting은 재조회만 허용합니다.
        return await InvokeAsync(current, attempt, "submit", ct);
    }

    public async Task<NeighborhoodFulfillmentSnapshot?> 내상태Async(string requestId, CancellationToken ct)
    {
        if (!Enabled) return null;
        var source = await OwnedAsync(requestId, ct);
        if (source is null) return null;
        var current = await db.NeighborhoodFulfillments.AsNoTracking().SingleOrDefaultAsync(x => x.SourceKind == "NeighborhoodDelivery" && x.RequestId == requestId, ct);
        if (source.자동처리처수행 || current?.IsAutomatic == true) return null;
        if (current is null)
            return source.모의처리처수행 ? new() { RequestId = requestId,
                StateCode = source.상태 == 살뜰.도메인.공통.상태값.의뢰상태.취소 ? NeighborhoodFulfillmentStates.Cancelled : NeighborhoodFulfillmentStates.AwaitingSelection,
                Revision = 0, IsSimulation = true, UpdatedAtUtc = source.UpdatedAt,
                CanCancel = source.상태 != 살뜰.도메인.공통.상태값.의뢰상태.취소
                    && !await db.운송원장.AsNoTracking().AnyAsync(x => x.의뢰Id == requestId, ct),
                CanChangeProvider = source.상태 != 살뜰.도메인.공통.상태값.의뢰상태.취소
                    && !await db.운송원장.AsNoTracking().AnyAsync(x => x.의뢰Id == requestId, ct),
                Notice = source.상태 == 살뜰.도메인.공통.상태값.의뢰상태.취소 ? "처리처 접수 전 의뢰가 취소됐습니다. 외부 접수·자체 배차는 실행하지 않았습니다."
                    : "의뢰는 저장됐고 처리처는 아직 접수하지 않았습니다. 새 견적과 조건을 확인해 처리처를 확정해 주세요." } : null;
        var attempt = await db.NeighborhoodFulfillmentAttempts.AsNoTracking().SingleAsync(x => x.AttemptId == current.AttemptId, ct);
        return await SnapshotAsync(current, attempt, ct);
    }

    public Task<Result<NeighborhoodFulfillmentSnapshot>> 새로고침Async(string requestId, NeighborhoodFulfillmentActionRequest request, CancellationToken ct)
        => ActionAsync(requestId, request, cancel: false, ct);
    public Task<Result<NeighborhoodFulfillmentSnapshot>> 취소Async(string requestId, NeighborhoodFulfillmentActionRequest request, CancellationToken ct)
        => ActionAsync(requestId, request, cancel: true, ct);

    private async Task<Result<NeighborhoodFulfillmentSnapshot>> ActionAsync(string requestId, NeighborhoodFulfillmentActionRequest request, bool cancel, CancellationToken ct)
    {
        if (!Enabled) return Fail<NeighborhoodFulfillmentSnapshot>("FulfillmentDisabled", "처리처 기능을 사용할 수 없습니다.", 403);
        if (request.ClientRequestId == Guid.Empty) return Fail<NeighborhoodFulfillmentSnapshot>("InvalidFulfillmentAction", "요청 번호가 필요합니다.", 400);
        var owned = await OwnedAsync(requestId, ct);
        if (owned is null) return Fail<NeighborhoodFulfillmentSnapshot>("RequestNotFound", "본인 생활 배송을 찾을 수 없습니다.", 404);
        if (owned.자동처리처수행) return Fail<NeighborhoodFulfillmentSnapshot>("AutomaticFulfillmentManaged", "자동 수행 조건과 상태 화면을 이용해 주세요.");
        var current = await db.NeighborhoodFulfillments.SingleOrDefaultAsync(x => x.SourceKind == "NeighborhoodDelivery" && x.RequestId == requestId, ct);
        if (current?.IsAutomatic == true) return Fail<NeighborhoodFulfillmentSnapshot>("AutomaticFulfillmentManaged", "자동 수행 조건과 상태 화면을 이용해 주세요.");
        if (current is null)
            return cancel ? await CancelUnsentAsync(requestId, request, ct)
                : await 내상태Async(requestId, ct) is { } pending ? Result.Ok(pending)
                    : Fail<NeighborhoodFulfillmentSnapshot>("FulfillmentNotFound", "처리처 수행 기록을 찾을 수 없습니다.", 404);
        await db.Entry(current).ReloadAsync(ct);
        var attempt = await db.NeighborhoodFulfillmentAttempts.SingleAsync(x => x.AttemptId == current.AttemptId, ct);
        await db.Entry(attempt).ReloadAsync(ct);
        if (cancel && attempt.CancelClientRequestId == request.ClientRequestId) return Result.Ok(await SnapshotAsync(current, attempt, ct));
        if (current.Revision != request.ExpectedRevision) return Fail<NeighborhoodFulfillmentSnapshot>("FulfillmentRevisionConflict", "최신 처리 상태를 다시 확인해 주세요.");
        if (CanChange(attempt) || attempt.StateCode == NeighborhoodFulfillmentStates.Completed) return Result.Ok(await SnapshotAsync(current, attempt, ct));
        if (cancel && !(await SnapshotAsync(current, attempt, ct)).CanCancel)
            return Fail<NeighborhoodFulfillmentSnapshot>("FulfillmentCancellationLocked", "기사 확정 또는 운송 중인 자체 배차는 기존 운영 취소 절차를 먼저 확인해 주세요.");
        if (cancel && attempt.StateCode is not (NeighborhoodFulfillmentStates.Accepted or NeighborhoodFulfillmentStates.InTransit))
            return Fail<NeighborhoodFulfillmentSnapshot>("FulfillmentResultUnknown", "접수·취소 결과를 먼저 재확인해 주세요. 결과 불명 상태에서 새 접수나 취소를 만들지 않습니다.");
        if (cancel)
        {
            attempt.CancelClientRequestId = request.ClientRequestId; attempt.StateCode = NeighborhoodFulfillmentStates.CancelPending;
            AppendHistory(attempt, attempt.StateCode);
        }
        current.Revision++; current.UpdatedAtUtc = Now; attempt.Revision++; attempt.UpdatedAtUtc = Now;
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { db.ChangeTracker.Clear(); return Fail<NeighborhoodFulfillmentSnapshot>("FulfillmentRevisionConflict", "다른 요청이 먼저 상태를 갱신했습니다."); }
        return await InvokeAsync(current, attempt, cancel ? "cancel" : "read", ct);
    }

    private async Task<Result<NeighborhoodFulfillmentSnapshot>> CancelUnsentAsync(string requestId, NeighborhoodFulfillmentActionRequest request, CancellationToken ct)
    {
        await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        var source = await db.화주운송의뢰.SingleOrDefaultAsync(x => x.의뢰Id == requestId && x.주문자UserId == currentUser.UserId
            && x.클라이언트요청Id.StartsWith(NeighborhoodDeliveryRoutes.ClientRequestPrefix), ct);
        if (source is null) return Fail<NeighborhoodFulfillmentSnapshot>("RequestNotFound", "본인 생활 배송을 찾을 수 없습니다.", 404);
        await db.Entry(source).ReloadAsync(ct);
        if (!source.모의처리처수행 || request.ExpectedRevision != 0
            || await db.NeighborhoodFulfillments.AsNoTracking().AnyAsync(x => x.SourceKind == "NeighborhoodDelivery" && x.RequestId == requestId, ct)
            || await db.운송원장.AsNoTracking().AnyAsync(x => x.의뢰Id == requestId, ct))
            return Fail<NeighborhoodFulfillmentSnapshot>("FulfillmentRevisionConflict", "처리처 접수가 시작됐거나 이전 배차 기록이 있습니다. 최신 진행을 확인해 주세요.");
        if (source.상태 != 살뜰.도메인.공통.상태값.의뢰상태.취소)
        {
            source.상태 = 살뜰.도메인.공통.상태값.의뢰상태.취소;
            source.배차상태 = 살뜰.도메인.공통.상태값.배차상태.취소; source.UpdatedAt = Now;
            db.운송이벤트.Add(new() { 의뢰Id = requestId, 이벤트타입 = "NeighborhoodFulfillmentUnsentCancelled",
                메타데이터 = JsonSerializer.Serialize(new { request.ClientRequestId }) });
            await db.SaveChangesAsync(ct);
        }
        if (transaction is not null) await transaction.CommitAsync(ct);
        return Result.Ok((await 내상태Async(requestId, ct))!);
    }

    private async Task<Result<NeighborhoodFulfillmentSnapshot>> InvokeAsync(NeighborhoodFulfillmentRecord current, NeighborhoodFulfillmentAttemptRecord attempt, string action, CancellationToken ct)
    {
        var claimRevision = current.Revision; var attemptRevision = attempt.Revision;
        if (!providers.TryGetValue(attempt.ProviderId, out var provider)) return Fail<NeighborhoodFulfillmentSnapshot>("FulfillmentProviderUnavailable", "이 처리처 연결을 확인할 수 없습니다. 원 접수 결과를 확인해 주세요.", 503);
        var operation = new FulfillmentProviderOperation(attempt.AttemptId, attempt.RequestId,
            attempt.CreatedAtUtc, attempt.DesiredCompletionUtc, attempt.StateCode, attempt.ExternalRequestId, attempt.EstimatedCompletionUtc);
        FulfillmentProviderResult result;
        try
        {
            result = action switch { "submit" => await provider.SubmitAsync(operation, ct), "cancel" => await provider.CancelAsync(operation, ct), _ => await provider.ReadAsync(operation, ct) };
        }
        catch (Exception ex) when (ex is HttpRequestException or TimeoutException or OperationCanceledException or DbUpdateException)
        {
            // 서버 중단/응답 유실은 영속한 시도만 남깁니다. 개인정보와 exception 본문은 기록하지 않습니다.
            result = new(action == "cancel" || attempt.StateCode == NeighborhoodFulfillmentStates.CancelPending
                ? NeighborhoodFulfillmentStates.CancelPending : NeighborhoodFulfillmentStates.ResultUnknown);
        }
        if (!KnownState(result.StateCode) || result.ExternalRequestId?.Length > 191
            || attempt.ProviderId != "internal" && result.StateCode is NeighborhoodFulfillmentStates.Accepted or NeighborhoodFulfillmentStates.InTransit or NeighborhoodFulfillmentStates.Completed
                && string.IsNullOrWhiteSpace(result.ExternalRequestId))
            result = new(attempt.StateCode == NeighborhoodFulfillmentStates.CancelPending ? NeighborhoodFulfillmentStates.CancelPending : NeighborhoodFulfillmentStates.ResultUnknown);
        db.ChangeTracker.Clear();
        current = await db.NeighborhoodFulfillments.SingleAsync(x => x.SourceKind == "NeighborhoodDelivery" && x.RequestId == attempt.RequestId, CancellationToken.None);
        if (current.AttemptId != attempt.AttemptId || current.Revision != claimRevision) return Result.Ok((await 내상태Async(current.RequestId, CancellationToken.None))!);
        attempt = await db.NeighborhoodFulfillmentAttempts.SingleAsync(x => x.AttemptId == attempt.AttemptId, CancellationToken.None);
        if (attempt.Revision != attemptRevision) return Result.Ok(await SnapshotAsync(current, attempt, CancellationToken.None));
        if (!ValidTransition(attempt.StateCode, result.StateCode)) result = new(attempt.StateCode, attempt.ExternalRequestId);
        attempt.StateCode = result.StateCode; attempt.ExternalRequestId = result.ExternalRequestId ?? attempt.ExternalRequestId;
        attempt.UpdatedAtUtc = Now; attempt.Revision++; current.Revision++; current.UpdatedAtUtc = Now;
        AppendHistory(attempt, result.StateCode);
        try { await db.SaveChangesAsync(CancellationToken.None); }
        catch (DbUpdateConcurrencyException) { db.ChangeTracker.Clear(); return Result.Ok((await 내상태Async(current.RequestId, CancellationToken.None))!); }
        return Result.Ok(await SnapshotAsync(current, attempt, CancellationToken.None));
    }

    private async Task<Result> CheckQuoteAsync(NeighborhoodFulfillmentSelection selected, string inputDigest, string? requestId, CancellationToken ct)
    {
        if (!providers.ContainsKey(selected.ProviderId)) return Fail("InvalidFulfillmentProvider", "지원하는 처리처를 선택해 주세요.", 400);
        var quote = await db.NeighborhoodFulfillmentQuotes.AsNoTracking().SingleOrDefaultAsync(x => x.QuoteId == selected.QuoteId && x.ActorId == currentUser.UserId, ct);
        if (quote is null || quote.SourceKind != "NeighborhoodDelivery" || quote.ConsentRevision is not null || quote.InputDigest != inputDigest || quote.RequestId != requestId || quote.ProviderId != selected.ProviderId)
            return Fail("FulfillmentQuoteMismatch", "현재 물품·장소·희망 시간의 서버 견적을 다시 확인해 주세요.");
        if (quote.ExpiresAtUtc <= Now) return Fail("FulfillmentQuoteExpired", "견적 유효기간이 지났습니다. 새 견적을 확인해 주세요.");
        if (quote.CostKrw != selected.ConfirmedCostKrw) return Fail("FulfillmentCostChanged", "처리처 비용을 다시 확인해 주세요.");
        if (selected.ProviderId == "internal" && !selected.DirectPaymentAgreed) return Fail("DirectPaymentAgreementRequired", "자체 배차 비용의 기사 직접 지급 조건을 확인해 주세요.", 400);
        if (selected.ProviderId != "internal" && !selected.ProviderInformationSharingAgreed) return Fail("ProviderSharingAgreementRequired", "선택한 처리처에 배송에 필요한 정보를 제공하는 동의가 필요합니다.", 400);
        return Result.Ok();
    }
    private Task<살뜰.도메인.화주.화주운송의뢰?> OwnedAsync(string id, CancellationToken ct)
        => db.화주운송의뢰.AsNoTracking().SingleOrDefaultAsync(x => x.의뢰Id == id && x.주문자UserId == currentUser.UserId
            && x.클라이언트요청Id.StartsWith(NeighborhoodDeliveryRoutes.ClientRequestPrefix), ct);
    private async Task<NeighborhoodFulfillmentSnapshot> SnapshotAsync(NeighborhoodFulfillmentRecord current, NeighborhoodFulfillmentAttemptRecord attempt, CancellationToken ct)
    {
        var snapshot = new NeighborhoodFulfillmentSnapshot { RequestId = current.RequestId, ProviderId = attempt.ProviderId, ProviderName = providers.GetValueOrDefault(attempt.ProviderId)?.Name ?? "처리처 확인 필요",
            StateCode = attempt.StateCode, Revision = current.Revision, UpdatedAtUtc = DateTime.SpecifyKind(current.UpdatedAtUtc, DateTimeKind.Utc), CostKrw = attempt.CostKrw,
            EstimatedCompletionUtc = DateTime.SpecifyKind(attempt.EstimatedCompletionUtc, DateTimeKind.Utc), IsSimulation = attempt.IsSimulation, AttemptId = attempt.AttemptId,
            ExternalRequestId = attempt.ExternalRequestId, CanCancel = Enabled && attempt.StateCode is NeighborhoodFulfillmentStates.Accepted or NeighborhoodFulfillmentStates.InTransit,
            CanChangeProvider = Enabled && CanChange(attempt), IsOperationalSettlementEligible = false,
            Notice = "Simulation 검증 기록입니다. 실제 업체 접수·배송·비용 지급·정산 완료가 아닙니다." };
        if (attempt.ProviderId == "internal" && snapshot.CanCancel)
        {
            var queue = await db.운송원장.AsNoTracking().SingleOrDefaultAsync(x => x.의뢰Id == current.RequestId, ct);
            var source = await db.화주운송의뢰.AsNoTracking().SingleOrDefaultAsync(x => x.의뢰Id == current.RequestId, ct);
            snapshot.CanCancel = queue?.확정기사Id is null && source?.배차상태 is 살뜰.도메인.공통.상태값.배차상태.미시작 or 살뜰.도메인.공통.상태값.배차상태.매칭중;
        }
        if (snapshot.CanChangeProvider && await db.화주운송의뢰.AsNoTracking().AnyAsync(x => x.의뢰Id == current.RequestId
            && x.상태 == 살뜰.도메인.공통.상태값.의뢰상태.취소, ct)) snapshot.CanChangeProvider = false;
        return snapshot;
    }
    private static bool CanChange(NeighborhoodFulfillmentAttemptRecord attempt) => attempt.StateCode is NeighborhoodFulfillmentStates.Cancelled or NeighborhoodFulfillmentStates.Failed;
    private static bool KnownState(string code) => code is NeighborhoodFulfillmentStates.Submitting or NeighborhoodFulfillmentStates.ResultUnknown
        or NeighborhoodFulfillmentStates.Accepted or NeighborhoodFulfillmentStates.InTransit or NeighborhoodFulfillmentStates.Completed
        or NeighborhoodFulfillmentStates.CancelPending or NeighborhoodFulfillmentStates.Cancelled or NeighborhoodFulfillmentStates.Failed;
    private static bool ValidTransition(string from, string to)
        => to is NeighborhoodFulfillmentStates.ResultUnknown or NeighborhoodFulfillmentStates.CancelPending
            || from is NeighborhoodFulfillmentStates.Submitting or NeighborhoodFulfillmentStates.ResultUnknown
            || from == NeighborhoodFulfillmentStates.CancelPending && to is NeighborhoodFulfillmentStates.Cancelled or NeighborhoodFulfillmentStates.Accepted
            || from == NeighborhoodFulfillmentStates.Accepted && to is NeighborhoodFulfillmentStates.Accepted or NeighborhoodFulfillmentStates.InTransit or NeighborhoodFulfillmentStates.Completed or NeighborhoodFulfillmentStates.Cancelled
            || from == NeighborhoodFulfillmentStates.InTransit && to is NeighborhoodFulfillmentStates.InTransit or NeighborhoodFulfillmentStates.Completed or NeighborhoodFulfillmentStates.Cancelled;
    private void AppendHistory(NeighborhoodFulfillmentAttemptRecord attempt, string state)
    {
        var history = JsonSerializer.Deserialize<List<HistoryEntry>>(attempt.HistoryJson) ?? [];
        history.Add(new(state, Now)); attempt.HistoryJson = JsonSerializer.Serialize(history);
    }
    private sealed record HistoryEntry(string StateCode, DateTime AtUtc);
    internal static string InputDigest(NeighborhoodDeliveryRequest request) => Digest(new { request.CargoName, request.Quantity, request.WeightKg,
        request.VehicleType, request.Pickup, request.Dropoff, request.Notes, request.DesiredCompletionUtc });
    private static string Digest(object value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value))));
    private static NeighborhoodDeliveryRequest FromSource(살뜰.도메인.화주.화주운송의뢰 source, DateTime? deadline)
        => new() { CargoName = source.화물종류, Quantity = source.화물수량 ?? 1, WeightKg = source.화물중량Kg ?? 0, VehicleType = source.차량종류,
            DesiredCompletionUtc = deadline, Notes = source.요청사항,
            Pickup = new() { 주소 = new() { 도로명주소 = source.픽업_도로명주소, 상세주소 = source.픽업_상세주소, 위도 = source.픽업_위도, 경도 = source.픽업_경도 },
                연락처 = new() { 이름 = source.픽업_연락처_이름, 전화번호 = source.픽업_연락처_전화번호 }, 시간창 = new() { 시작일시 = source.픽업_시간창_시작일시, 종료일시 = source.픽업_시간창_종료일시 } },
            Dropoff = new() { 주소 = new() { 도로명주소 = source.하차_도로명주소, 상세주소 = source.하차_상세주소, 위도 = source.하차_위도, 경도 = source.하차_경도 },
                연락처 = new() { 이름 = source.하차_연락처_이름, 전화번호 = source.하차_연락처_전화번호 },
                시간창 = source.하차_시간창_시작일시 is { } start && source.하차_시간창_종료일시 is { } end ? new() { 시작일시 = start, 종료일시 = end } : null } };
    private static Result Fail(string code, string message, int status = 409) => Result.Fail(new Error(message).WithMetadata("ErrorCode", code).WithMetadata("StatusCode", status));
    private static Result<T> Fail<T>(string code, string message, int status = 409) => Result.Fail<T>(new Error(message).WithMetadata("ErrorCode", code).WithMetadata("StatusCode", status));
}
