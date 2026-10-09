using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Ssalddel.Contracts.Common.Fulfillment;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Domain.Community;
using 살뜰.Data;
using 살뜰.Services.Options;

namespace Ssalddel.Services.Fulfillment;

/// <summary>원 요청당 한 수행을 저장한 뒤 실행합니다. 모의 업체이며 실청구·기사 정산을 확정하지 않습니다.</summary>
[SsalddelCodeMetadata(SsalddelCodeFeatureKeys.OperationalLogisticsOs, SsalddelCodeLayer.Application,
    "생활 배송·음식 배달의 사전 조건과 한 수행 시도를 보존하고 플랫폼 선정·모의 접수·회복을 조율한다.",
    Effects = SsalddelCodeEffect.PersistentRead | SsalddelCodeEffect.PersistentWrite,
    StepKey = "application.fulfillment-routing", FlowOrder = 36,
    ReadsFrom = SsalddelCodeDataScope.OperationalState, WritesTo = SsalddelCodeDataScope.OperationalState,
    Boundary = "Simulation 및 기능 관문을 요구한다. 실제 업체 API·청구·지급·정산 완료가 아니다.")]
public sealed class 자동수행Coordinator(SsalddelContext db, IEnumerable<I수행원천Adapter> sourceAdapters,
    IEnumerable<I자동수행Provider> providerServices, IOptions<FulfillmentRoutingOptions> options,
    ISsalddelExecutionModePolicy execution, TimeProvider clock) : I자동수행Coordinator
{
    private readonly IReadOnlyDictionary<string, I수행원천Adapter> sources = sourceAdapters.ToDictionary(x => x.SourceKind, StringComparer.Ordinal);
    private readonly IReadOnlyDictionary<string, I자동수행Provider> providers = providerServices.ToDictionary(x => x.Id, StringComparer.Ordinal);
    public bool Enabled => options.Value.Enabled && execution.IsSimulation;
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    public IReadOnlyList<AutoFulfillmentProviderDescriptor> 수행처후보(string sourceKind)
        => !Enabled || !sources.ContainsKey(sourceKind) ? [] : providers.Values
            .Where(x => x.원천지원(sourceKind) && (!AssumedFulfillmentProviderCatalog.IsAssumed(x.Id)
                || options.Value.AssumedProvidersEnabled && sourceKind == FulfillmentSourceKinds.NeighborhoodDelivery))
            .Select(x => new AutoFulfillmentProviderDescriptor(x.Id, x.Name, true)).ToArray();

    public async Task<Result<AutoFulfillmentSnapshot>> 사전동의저장Async(FulfillmentSourceKey key, string ownerId, AutoFulfillmentConsent request, CancellationToken ct = default)
    {
        if (!Enabled) return Disabled();
        // 화면이 보낸 mutable DTO를 기다리는 동안 바꿔도 이 접수의 확인 조건은 변하지 않습니다.
        request = JsonSerializer.Deserialize<AutoFulfillmentConsent>(JsonSerializer.Serialize(request))!;
        if (request.ConfirmedCostSharingPolicy is { } confirmedPolicy)
            request.ConfirmedCostSharingPolicy = DeliveryCostSharingCalculator.Clone(confirmedPolicy);
        if (!sources.TryGetValue(key.SourceKind, out var adapter)) return Fail("FulfillmentSourceUnknown", "수행 원천을 확인해 주세요.", 400);
        if (request.ClientRequestId == Guid.Empty || string.IsNullOrWhiteSpace(ownerId)) return Fail("InvalidFulfillmentConsent", "동의 요청 번호와 본인을 확인해 주세요.", 400);
        await using var tx = db.Database.IsRelational() && db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        var source = await adapter.읽기Async(key, ct);
        if (source is null || source.OwnerId != ownerId) return Fail("FulfillmentSourceNotFound", "본인 요청을 찾을 수 없습니다.", 404);
        var current = await CurrentAsync(key, ct);
        if (current is { IsAutomatic: false }) return Fail("ManualFulfillmentLocked", "기존 수행은 자동 조건으로 전환하지 않습니다.");
        var consent = await ConsentAsync(key, ct);
        var digest = Digest(request);
        var receipts = JsonSerializer.Deserialize<List<ConsentReceipt>>(consent?.ReceiptsJson ?? "[]") ?? [];
        var replay = receipts.SingleOrDefault(x => x.Id == request.ClientRequestId);
        if (replay is not null) return replay.Digest == digest ? Result.Ok((await 상태Async(key, ct))!) : Fail("FulfillmentIdempotencyConflict", "같은 동의 요청 번호의 내용이 다릅니다.");
        if (source.IsClosed) return Fail("FulfillmentSourceClosed", "종료된 원 요청은 새 수행을 시작하지 않습니다.");
        if ((consent?.Revision ?? 0) != request.ExpectedRevision) return Fail("FulfillmentConsentRevisionConflict", "최신 동의 조건을 다시 확인해 주세요.");
        if (!request.Revoked)
        {
            var availableProviderIds = 수행처후보(key.SourceKind).Select(x => x.ProviderId).ToHashSet(StringComparer.Ordinal);
            if (request.ConfirmedCostSharingPolicy is { } policy && DeliveryCostSharingCalculator.ValidationError(policy) is not null)
                return Fail("InvalidCostSharingPolicy", "배송비 분담 방식과 필요한 값만 확인해 주세요.", 400);
            if (request.NoticeVersion != AutoFulfillmentNotice.Version || request.MaxUserChargeKrw < 0
                || !request.AutomaticSubmissionAgreed || !request.ProviderInformationSharingAgreed
                || request.AllowedProviderIds.Count < 1 || request.AllowedProviderIds.Distinct(StringComparer.Ordinal).Count() != request.AllowedProviderIds.Count
                || request.AllowedProviderIds.Any(x => !availableProviderIds.Contains(x))
                || request.InformationFields.Distinct(StringComparer.Ordinal).Count() != request.InformationFields.Count
                || request.InformationFields.Count != AutoFulfillmentNotice.InformationFields.Count
                || request.InformationFields.Except(AutoFulfillmentNotice.InformationFields, StringComparer.Ordinal).Any()
                || request.AllowedProviderIds.Contains("internal") && key.SourceKind == FulfillmentSourceKinds.NeighborhoodDelivery && !request.DirectPaymentAgreed)
                return Fail("FulfillmentConsentRequired", "비용 상한·허용 처리처·정보 제공 범위와 자동 접수 조건을 확인해 주세요.", 400);
            if (request.DesiredCompletionUtc is { } deadline && (deadline.Kind != DateTimeKind.Utc || deadline <= Now))
                return Fail("FulfillmentDeadlineExpired", "희망 완료 시각을 미래 UTC 시각으로 확인해 주세요.", 400);
            var attempt = current is not null && !string.IsNullOrEmpty(current.AttemptId)
                ? await db.NeighborhoodFulfillmentAttempts.AsNoTracking().SingleAsync(x => x.AttemptId == current.AttemptId, ct) : null;
            if (AutoFulfillmentConsentRenewalPolicy.LockedReason(source.IsClosed, source.HasCookingStarted, attempt?.StateCode, current?.StateCode) is { } renewalLockedReason)
                return Fail(renewalLockedReason, "현재 단계에서는 새 자동 접수 조건을 변경할 수 없습니다. 진행 상태를 확인하고 필요한 취소·변경은 협의를 요청해 주세요.");
            if (consent is not null)
            {
                var frozenPolicy = DeliveryCostSharingStorage.Read(consent.FrozenCostSharingPolicyJson, consent.FrozenRestaurantSupportKrw);
                if (frozenPolicy.Mode != DeliveryCostSharingModes.FixedContribution && request.ConfirmedCostSharingPolicy is null)
                    return Fail("CostSharingPolicyConfirmationRequired", "원 주문에 동결된 배송비 분담 정책을 다시 확인해 주세요.");
                if (request.ConfirmedCostSharingPolicy is { } renewedPolicy && !DeliveryCostSharingCalculator.Equivalent(frozenPolicy, renewedPolicy))
                    return Fail("FrozenCostSharingPolicyChanged", "기존 주문의 배송비 분담 조건은 동의 갱신으로 바뀌지 않습니다.");
                if (request.ConfirmedRestaurantSupportKrw is { } renewedSupport && renewedSupport != consent.FrozenRestaurantSupportKrw)
                    return Fail("FrozenCostSharingPolicyChanged", "원 주문에 동결된 정액 지원액을 확인해 주세요.");
                if (request.ConfirmedRestaurantSupportRevision is { } renewedRevision && renewedRevision != consent.RestaurantSupportRevision)
                    return Fail("FrozenCostSharingPolicyChanged", "원 주문에 동결된 지원 판본을 확인해 주세요.");
            }
        }
        if (consent is null)
        {
            decimal support = 0; long? supportRevision = null; string? frozenPolicyJson = null;
            if (source.RestaurantId is { } restaurantId)
            {
                var setting = await db.RestaurantFulfillmentSupports.AsNoTracking().SingleOrDefaultAsync(x => x.RestaurantId == restaurantId, ct);
                if (setting is null || !setting.Enabled) return Fail("RestaurantSupportRequired", "음식점의 명시적 배송 지원 설정을 먼저 확인해야 합니다.");
                var settingPolicy = DeliveryCostSharingStorage.Read(setting.CostSharingPolicyJson, setting.FixedContributionKrw);
                if (DeliveryCostSharingCalculator.ValidationError(settingPolicy) is not null)
                    return Fail("InvalidCostSharingPolicy", "음식점의 배송비 분담 설정을 확인해야 합니다.");
                if (settingPolicy.Mode != DeliveryCostSharingModes.FixedContribution && request.ConfirmedCostSharingPolicy is null)
                    return Fail("CostSharingPolicyConfirmationRequired", "음식점의 배송비 분담 정책 전체를 확인해 주세요.");
                if (request.ConfirmedRestaurantSupportRevision != setting.Revision
                    || request.ConfirmedCostSharingPolicy is { } proposal && !DeliveryCostSharingCalculator.Equivalent(proposal, settingPolicy)
                    || request.ConfirmedCostSharingPolicy is null && request.ConfirmedRestaurantSupportKrw != setting.FixedContributionKrw)
                    return Fail("RestaurantSupportChanged", "음식점 지원액과 판본을 다시 확인해 주세요.");
                support = setting.FixedContributionKrw; supportRevision = setting.Revision;
                frozenPolicyJson = setting.CostSharingPolicyJson is not null || request.ConfirmedCostSharingPolicy is not null
                    ? DeliveryCostSharingStorage.Write(settingPolicy) : null;
            }
            else if (request.ConfirmedCostSharingPolicy is { } lifePolicy
                && !DeliveryCostSharingCalculator.Equivalent(lifePolicy, DeliveryCostSharingCalculator.Effective(null, 0)))
                return Fail("InvalidCostSharingPolicy", "생활 배송의 기존 본인 부담 조건은 음식점 분담 정책으로 바꾸지 않습니다.", 400);
            consent = new() { SourceKind = key.SourceKind, RequestId = key.SourceId, OwnerId = ownerId,
                FrozenRestaurantSupportKrw = support, RestaurantSupportRevision = supportRevision,
                FrozenCostSharingPolicyJson = frozenPolicyJson, InputDigest = source.InputDigest };
            db.AutoFulfillmentConsents.Add(consent);
        }
        // 지원 S와 입력 지문은 원 요청 최초값으로 유지합니다. 동의 갱신/leg 변경은 새 지원을 만들지 않습니다.
        consent.ClientRequestId = request.ClientRequestId; consent.Revision++;
        if (!request.Revoked)
        {
        consent.MaxUserChargeKrw = request.MaxUserChargeKrw;
        consent.DesiredCompletionUtc = request.DesiredCompletionUtc; consent.AllowedProvidersJson = JsonSerializer.Serialize(request.AllowedProviderIds);
        consent.InformationFieldsJson = JsonSerializer.Serialize(request.InformationFields); consent.SharingAgreed = request.ProviderInformationSharingAgreed;
        consent.AutomaticSubmissionAgreed = request.AutomaticSubmissionAgreed; consent.DirectPaymentAgreed = request.DirectPaymentAgreed;
        consent.NoticeVersion = request.NoticeVersion;
        }
        consent.Revoked = request.Revoked; consent.UpdatedAtUtc = Now;
        receipts.Add(new(request.ClientRequestId, digest)); consent.ReceiptsJson = JsonSerializer.Serialize(receipts);
        if (current is null)
        {
            current = new() { SourceKind = key.SourceKind, RequestId = key.SourceId, ActorId = ownerId, IsAutomatic = true };
            db.NeighborhoodFulfillments.Add(current);
        }
        current.Revision++; current.UpdatedAtUtc = Now;
        if (string.IsNullOrEmpty(current.AttemptId)) current.StateCode = source.ReadyForRouting ? AutoFulfillmentStates.AwaitingRouting : AutoFulfillmentStates.AwaitingSource;
        current.ReviewCode = request.Revoked ? "ConsentRevoked" : null;
        try { await db.SaveChangesAsync(ct); if (tx is not null) await tx.CommitAsync(ct); }
        catch (DbUpdateException) { db.ChangeTracker.Clear(); return Fail("FulfillmentConsentRevisionConflict", "다른 요청이 먼저 동의를 변경했습니다."); }
        return Result.Ok((await 상태Async(key, ct))!);
    }

    public async Task 예약Async(FulfillmentSourceKey key, Guid operationId, CancellationToken ct = default)
    {
        if (!Enabled) return;
        if (operationId == Guid.Empty) throw new ArgumentException("OperationId is required.");
        var id = Digest(new { key.SourceKind, key.SourceId, operationId });
        if (await db.AutoFulfillmentOutbox.AnyAsync(x => x.Id == id, ct)) return;
        db.AutoFulfillmentOutbox.Add(new() { Id = id, SourceKind = key.SourceKind, RequestId = key.SourceId, OperationId = operationId, UpdatedAtUtc = Now });
        await db.SaveChangesAsync(ct);
    }

    public async Task<Result<AutoFulfillmentSnapshot>> 선정실행Async(FulfillmentSourceKey key, Guid operationId, CancellationToken ct = default)
    {
        if (!Enabled) return Disabled();
        if (operationId == Guid.Empty) return Fail("InvalidFulfillmentAction", "요청 번호가 필요합니다.", 400);
        // 등록·허용된 유한 후보마다 같은 동의 판본에서 한 번만 접수하고 마지막에 소진 여부를 판정합니다.
        var consent = await db.AutoFulfillmentConsents.AsNoTracking().SingleOrDefaultAsync(
            x => x.SourceKind == key.SourceKind && x.RequestId == key.SourceId, ct);
        var allowedIds = JsonSerializer.Deserialize<string[]>(consent?.AllowedProvidersJson ?? "[]") ?? [];
        var availableIds = 수행처후보(key.SourceKind).Select(x => x.ProviderId).ToHashSet(StringComparer.Ordinal);
        var candidateCount = allowedIds.Distinct(StringComparer.Ordinal).Count(availableIds.Contains);
        for (var pass = 0; pass <= candidateCount; pass++)
        {
            var selected = await SelectOneAsync(key, operationId, ct);
            if (selected.IsFailed || selected.Value.Attempt is null) return selected.IsFailed
                ? Result.Fail<AutoFulfillmentSnapshot>(selected.Errors) : Result.Ok((await 상태Async(key, ct))!);
            var result = await InvokeAsync(key, selected.Value.Attempt, selected.Value.Revision, selected.Value.Action, ct);
            if (result.IsFailed || !Terminal(result.Value.StateCode)) return result;
            operationId = Guid.NewGuid();
        }
        return Result.Ok((await 상태Async(key, ct))!);
    }

    private async Task<Result<Selection>> SelectOneAsync(FulfillmentSourceKey key, Guid operationId, CancellationToken ct)
    {
        if (!sources.TryGetValue(key.SourceKind, out var adapter)) return FailSelection("FulfillmentSourceUnknown");
        await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        var source = await adapter.읽기Async(key, ct);
        var consent = await ConsentAsync(key, ct); var current = await CurrentAsync(key, ct);
        if (source is null || consent is null || current is null || !current.IsAutomatic) return FailSelection("AutomaticFulfillmentNotFound");
        var old = string.IsNullOrEmpty(current.AttemptId) ? null : await db.NeighborhoodFulfillmentAttempts.SingleAsync(x => x.AttemptId == current.AttemptId, ct);
        if (old is not null && !Terminal(old.StateCode))
            return Result.Ok(new Selection(old.StateCode == AutoFulfillmentStates.Completed ? null : old.AttemptId,
                current.Revision, old.StateCode == AutoFulfillmentStates.Submitting ? "recover" : "read"));
        string? review = source.IsClosed ? "SourceClosed" : consent.Revoked ? "ConsentRevoked"
            : !source.ReadyForRouting ? "SourceNotReady" : source.HasCookingStarted ? "CookingAlreadyStarted"
            : source.InputDigest != consent.InputDigest ? "SourceChanged"
            : consent.DesiredCompletionUtc <= Now ? "DeadlineExpired" : null;
        var all = await db.NeighborhoodFulfillmentAttempts.AsNoTracking().Where(x => x.SourceKind == key.SourceKind && x.RequestId == key.SourceId && x.ConsentRevision != null).ToListAsync(ct);
        if (all.Any(x => Terminal(x.StateCode) && x.ConfirmedIncurredCostKrw is null)) review ??= "IncurredCostUnknown";
        if (all.Any(x => !Terminal(x.StateCode) && x.StateCode != AutoFulfillmentStates.Completed)) review ??= "ActiveExecutionExists";
        var incurred = all.Where(x => Terminal(x.StateCode)).Sum(x => x.ConfirmedIncurredCostKrw ?? 0);
        var costPolicy = DeliveryCostSharingStorage.Read(consent.FrozenCostSharingPolicyJson, consent.FrozenRestaurantSupportKrw);
        if (DeliveryCostSharingCalculator.ValidationError(costPolicy) is not null) review ??= "InvalidCostSharingPolicy";
        else if (DeliveryCostSharingCalculator.Calculate(incurred, costPolicy) is { IsAllowed: false, ReasonCode: { } costReason }) review ??= costReason;
        AutoFulfillmentQuote? best = null;
        if (review is null)
        {
            var availableIds = 수행처후보(key.SourceKind).Select(x => x.ProviderId).ToHashSet(StringComparer.Ordinal);
            var allowed = (JsonSerializer.Deserialize<string[]>(consent.AllowedProvidersJson) ?? [])
                .Distinct(StringComparer.Ordinal).Where(availableIds.Contains).ToArray();
            var used = all.Where(x => x.ConsentRevision == consent.Revision).Select(x => x.ProviderId).ToHashSet(StringComparer.Ordinal);
            var quotes = new List<AutoFulfillmentQuote>();
            foreach (var id in allowed.Where(x => !used.Contains(x)))
            {
                if (!providers.TryGetValue(id, out var provider)) continue;
                try { quotes.Add(await provider.견적Async(source, consent.DesiredCompletionUtc, ct)); }
                catch (Exception ex) when (ex is HttpRequestException or TimeoutException) { quotes.Add(new(id, provider.Name, false, null, null, "QuoteUnknown")); }
            }
            best = 수행처선정Engine.선정(quotes, Now, consent.DesiredCompletionUtc, incurred, costPolicy, consent.MaxUserChargeKrw);
            if (best is null)
            {
                var timeFeasible = quotes.Where(x => x.Available && x.TotalCostKrw is >= 0 && x.CompletionUtc >= Now
                    && (consent.DesiredCompletionUtc is null || x.CompletionUtc <= consent.DesiredCompletionUtc)).ToList();
                review = timeFeasible.Count > 0 && timeFeasible.All(x => DeliveryCostSharingCalculator.Calculate(incurred + x.TotalCostKrw!.Value, costPolicy)
                        .ReasonCode == "RestaurantChargeLimitExceeded")
                    ? "RestaurantChargeLimitExceeded" : allowed.Length == 0 ? "ProvidersUnavailable"
                    : allowed.All(used.Contains) ? "ProvidersExhausted" : "NoFeasibleProvider";
            }
        }
        if (review is not null)
        {
            current.StateCode = source.IsClosed ? AutoFulfillmentStates.Cancelled
                : review == "SourceNotReady" ? AutoFulfillmentStates.AwaitingSource : AutoFulfillmentStates.NeedsReview;
            current.ReviewCode = review; current.Revision++; current.UpdatedAtUtc = Now;
            await db.SaveChangesAsync(ct); if (tx is not null) await tx.CommitAsync(ct);
            return Result.Ok(new Selection(null, current.Revision));
        }
        var quote = new NeighborhoodFulfillmentQuoteRecord { QuoteId = Guid.NewGuid().ToString("N"), SourceKind = key.SourceKind,
            RequestId = key.SourceId, ActorId = consent.OwnerId, ConsentRevision = consent.Revision, SourceRevision = source.SourceRevision,
            InputDigest = source.InputDigest, Leg = source.Leg, ProviderId = best!.ProviderId, CostKrw = best.TotalCostKrw!.Value,
            WeightKg = source.WeightKg, DistanceKm = source.DistanceKm!.Value, DesiredCompletionUtc = consent.DesiredCompletionUtc,
            EstimatedCompletionUtc = best.CompletionUtc!.Value, ExpiresAtUtc = Now.AddMinutes(Math.Clamp(options.Value.QuoteLifetimeMinutes, 1, 30)), IsSimulation = true };
        db.NeighborhoodFulfillmentQuotes.Add(quote);
        var attempt = new NeighborhoodFulfillmentAttemptRecord { AttemptId = Guid.NewGuid().ToString("N"), SourceKind = key.SourceKind,
            RequestId = key.SourceId, ClientRequestId = operationId, ConsentRevision = consent.Revision, SourceRevision = source.SourceRevision,
            SelectionDigest = Digest(new { consent.Revision, quote.QuoteId, quote.ProviderId }), QuoteId = quote.QuoteId, ProviderId = quote.ProviderId,
            StateCode = AutoFulfillmentStates.Submitting, CostKrw = quote.CostKrw, IsSimulation = true, SharingAgreed = consent.SharingAgreed,
            Leg = source.Leg, DesiredCompletionUtc = consent.DesiredCompletionUtc, EstimatedCompletionUtc = quote.EstimatedCompletionUtc,
            Revision = 1, CreatedAtUtc = Now, UpdatedAtUtc = Now };
        History(attempt, attempt.StateCode); db.NeighborhoodFulfillmentAttempts.Add(attempt);
        current.AttemptId = attempt.AttemptId; current.StateCode = attempt.StateCode; current.ReviewCode = null;
        current.SelectionReason = "시간·가용성·누적 부담 상한 충족 → 최저 총비용 → 완료 시각 → 처리처 ID";
        current.Revision++; current.UpdatedAtUtc = Now;
        try { await db.SaveChangesAsync(ct); if (tx is not null) await tx.CommitAsync(ct); }
        catch (DbUpdateException) { db.ChangeTracker.Clear(); return FailSelection("FulfillmentRevisionConflict"); }
        return Result.Ok(new Selection(attempt.AttemptId, current.Revision));
    }

    public async Task<AutoFulfillmentSnapshot?> 상태Async(FulfillmentSourceKey key, CancellationToken ct = default)
    {
        if (!Enabled) return null;
        var current = await db.NeighborhoodFulfillments.AsNoTracking().SingleOrDefaultAsync(x => x.SourceKind == key.SourceKind && x.RequestId == key.SourceId, ct);
        var consent = await db.AutoFulfillmentConsents.AsNoTracking().SingleOrDefaultAsync(x => x.SourceKind == key.SourceKind && x.RequestId == key.SourceId, ct);
        if (current is { IsAutomatic: false }) return null;
        var source = sources.TryGetValue(key.SourceKind, out var adapter) ? await adapter.읽기Async(key, ct) : null;
        if (current is null || consent is null)
        {
            if (source is null) return null;
            // 최초 동의가 아직 없을 때만 현재 지원 설정을 확인 제안으로 제공합니다. 실제 주문 동결은 동의 저장에서 수행합니다.
            var support = source.RestaurantId is { } restaurantId
                ? await db.RestaurantFulfillmentSupports.AsNoTracking().SingleOrDefaultAsync(x => x.RestaurantId == restaurantId && x.Enabled, ct) : null;
            var renewalLockedReason = AutoFulfillmentConsentRenewalPolicy.LockedReason(source.IsClosed, source.HasCookingStarted, null, current?.StateCode);
            return new()
            {
                SourceKind = key.SourceKind, SourceId = key.SourceId, SourceClosed = source.IsClosed,
                StateCode = source.IsClosed ? AutoFulfillmentStates.Cancelled : AutoFulfillmentStates.NeedsReview,
                Revision = current?.Revision ?? 0, ConsentRevision = 0, UpdatedAtUtc = Now,
                ReviewCode = "InitialConsentRequired", CanRenewConsent = renewalLockedReason is null,
                ConsentRenewalLockedReasonCode = renewalLockedReason,
                FrozenRestaurantSupportKrw = support?.FixedContributionKrw ?? 0, RestaurantSupportRevision = support?.Revision,
                CostSharingPolicy = support is null ? null : DeliveryCostSharingStorage.Read(support.CostSharingPolicyJson, support.FixedContributionKrw),
                SelectionReason = "원 요청은 저장됐지만 자동 조건 확정을 완료하지 못했습니다. 지원액은 현재 설정의 확인 제안이며 아직 주문에 동결되지 않았습니다. 조건을 새로 확인해 주세요."
            };
        }
        var attempt = string.IsNullOrEmpty(current.AttemptId) ? null : await db.NeighborhoodFulfillmentAttempts.AsNoTracking().SingleAsync(x => x.AttemptId == current.AttemptId, ct);
        var all = await db.NeighborhoodFulfillmentAttempts.AsNoTracking().Where(x => x.SourceKind == key.SourceKind && x.RequestId == key.SourceId && x.ConsentRevision != null).ToListAsync(ct);
        var snapshot = AutoFulfillmentSnapshotProjection.Create(current, consent, attempt, all,
            source is null || source.IsClosed, source?.CanCancel == true, source?.HasCookingStarted == true);
        if (attempt is not null && providers.TryGetValue(attempt.ProviderId, out var provider))
            snapshot.ProviderName = provider.Name;
        return snapshot;
    }
    public async Task<Result<AutoFulfillmentSnapshot>> 재확인Async(FulfillmentSourceKey key, AutoFulfillmentActionRequest request, CancellationToken ct = default)
        => await ActionAsync(key, request, false, ct);
    public async Task<Result<AutoFulfillmentSnapshot>> 취소Async(FulfillmentSourceKey key, AutoFulfillmentActionRequest request, CancellationToken ct = default)
        => await ActionAsync(key, request, true, ct);
    private async Task<Result<AutoFulfillmentSnapshot>> ActionAsync(FulfillmentSourceKey key, AutoFulfillmentActionRequest request, bool cancel, CancellationToken ct)
    {
        if (!Enabled) return Disabled();
        if (request.ClientRequestId == Guid.Empty) return Fail("InvalidFulfillmentAction", "요청 번호가 필요합니다.", 400);
        await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        var current = await CurrentAsync(key, ct);
        if (current is null || !current.IsAutomatic) return Fail("AutomaticFulfillmentNotFound", "자동 수행을 찾을 수 없습니다.", 404);
        var a = string.IsNullOrEmpty(current.AttemptId) ? null : await db.NeighborhoodFulfillmentAttempts.SingleAsync(x => x.AttemptId == current.AttemptId, ct);
        if (a?.ResultOperationId == request.ClientRequestId.ToString("N") || cancel && a?.CancelClientRequestId == request.ClientRequestId)
        {
            if (a is null || Terminal(a.StateCode) || a.StateCode == AutoFulfillmentStates.Completed) return Result.Ok((await 상태Async(key, ct))!);
            if (tx is not null) await tx.CommitAsync(ct);
            return await InvokeAsync(key, a.AttemptId, current.Revision, "read", ct);
        }
        if (current.Revision != request.ExpectedRevision) return Fail("FulfillmentRevisionConflict", "최신 수행 상태를 다시 확인해 주세요.");
        if (a is null || Terminal(a.StateCode))
        {
            if (tx is not null) await tx.CommitAsync(ct);
            return cancel ? Fail("FulfillmentNotSubmitted", "진행 중 접수가 없습니다. 자동 동의를 철회하면 새 접수를 멈춥니다.") : await 선정실행Async(key, request.ClientRequestId, ct);
        }
        if (a.StateCode == AutoFulfillmentStates.Completed) return Result.Ok((await 상태Async(key, ct))!);
        if (cancel)
        {
            var snapshot = await 상태Async(key, ct);
            if (snapshot?.CanCancel != true) return Fail("FulfillmentCancellationLocked", "배정·조리·접수 결과를 먼저 확인해야 합니다. 원 주문 취소와 공급자 취소는 별개입니다.");
            a.StateCode = AutoFulfillmentStates.CancelPending; current.StateCode = a.StateCode; a.CancelClientRequestId = request.ClientRequestId;
        }
        a.ResultOperationId = request.ClientRequestId.ToString("N"); a.Revision++; a.UpdatedAtUtc = Now;
        current.Revision++; current.UpdatedAtUtc = Now; var revision = current.Revision;
        await db.SaveChangesAsync(ct); if (tx is not null) await tx.CommitAsync(ct);
        var result = await InvokeAsync(key, a.AttemptId, revision, cancel ? "cancel" : "read", ct);
        return result.IsSuccess && Terminal(result.Value.StateCode) ? await 선정실행Async(key, Guid.NewGuid(), ct) : result;
    }

    private async Task<Result<AutoFulfillmentSnapshot>> InvokeAsync(FulfillmentSourceKey key, string attemptId, long revision, string action, CancellationToken ct)
    {
        var a = await db.NeighborhoodFulfillmentAttempts.AsNoTracking().SingleAsync(x => x.AttemptId == attemptId, ct);
        if (!providers.TryGetValue(a.ProviderId, out var provider) || !sources.TryGetValue(key.SourceKind, out var adapter)) return Fail("FulfillmentProviderUnknown", "원 접수 처리처를 확인해야 합니다.");
        FulfillmentExecutionResult? recovered = null;
        if (action == "recover")
        {
            try { recovered = await provider.조회Async(Context(key, a, revision), adapter, ct); }
            catch (Exception ex) when (ex is HttpRequestException or TimeoutException) { recovered = new(AutoFulfillmentStates.ResultUnknown); }
            // r25 모의 및 내부 adapter는 동일 attempt의 멱등 접수를 보장합니다. 새 시도·다른 처리처로 바꾸지 않습니다.
            if (recovered.StateCode == AutoFulfillmentStates.ResultUnknown) { recovered = null; action = "submit"; }
            else action = "read";
        }
        if (action == "submit")
        {
            // 저장 후 호출 전에 철회·원 주문 변경이 들어오면 같은 시도를 미접수 실패로 닫습니다.
            db.ChangeTracker.Clear();
            await using var gate = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
            var selected = await CurrentAsync(key, ct); var permission = await ConsentAsync(key, ct);
            var source = await adapter.읽기Async(key, ct);
            var quote = await db.NeighborhoodFulfillmentQuotes.AsNoTracking().SingleAsync(x => x.QuoteId == a.QuoteId, ct);
            var liveAttempt = await db.NeighborhoodFulfillmentAttempts.SingleAsync(x => x.AttemptId == a.AttemptId, ct);
            if (selected?.AttemptId != a.AttemptId || selected.Revision != revision || liveAttempt.Revision != a.Revision)
                return Result.Ok((await 상태Async(key, ct))!);
            var code = permission?.Revoked != false || permission.Revision != a.ConsentRevision ? "ConsentChangedBeforeSend"
                : source is null || source.IsClosed || !source.ReadyForRouting || source.HasCookingStarted ? "SourceClosedBeforeSend"
                : source.InputDigest != permission.InputDigest || source.InputDigest != quote.InputDigest || source.SourceRevision != a.SourceRevision || source.Leg != a.Leg ? "SourceChangedBeforeSend"
                : quote.ExpiresAtUtc <= Now || permission.DesiredCompletionUtc <= Now ? "QuoteExpiredBeforeSend" : null;
            if (code is not null)
            {
                liveAttempt.StateCode = AutoFulfillmentStates.Failed; liveAttempt.ConfirmedIncurredCostKrw = 0;
                liveAttempt.Revision++; liveAttempt.UpdatedAtUtc = Now; History(liveAttempt, liveAttempt.StateCode);
                selected.StateCode = liveAttempt.StateCode; selected.ReviewCode = code; selected.Revision++; selected.UpdatedAtUtc = Now;
                await db.SaveChangesAsync(ct); if (gate is not null) await gate.CommitAsync(ct);
                return Result.Ok((await 상태Async(key, ct))!);
            }
            if (gate is not null) await gate.CommitAsync(ct);
        }
        var context = Context(key, a, revision);
        FulfillmentExecutionResult result;
        try
        {
            result = recovered ?? (action switch { "submit" => await provider.접수Async(context, adapter, ct), "cancel" => await provider.취소Async(context, adapter, ct), _ => await provider.조회Async(context, adapter, ct) });
        }
        catch (Exception ex) when (ex is HttpRequestException or TimeoutException or OperationCanceledException or DbUpdateException)
        { result = new(action == "cancel" || a.StateCode == AutoFulfillmentStates.CancelPending ? AutoFulfillmentStates.CancelPending : AutoFulfillmentStates.ResultUnknown); }
        db.ChangeTracker.Clear();
        await using var applyTx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, CancellationToken.None) : null;
        var current = await CurrentAsync(key, CancellationToken.None);
        if (current?.AttemptId != attemptId || current.Revision != revision) return Result.Ok((await 상태Async(key, CancellationToken.None))!);
        var live = await db.NeighborhoodFulfillmentAttempts.SingleAsync(x => x.AttemptId == attemptId, CancellationToken.None);
        if (live.Revision != a.Revision) return Result.Ok((await 상태Async(key, CancellationToken.None))!);
        if (!Known(result.StateCode) || result.ExternalRequestId?.Length > 191 || result.ConfirmedIncurredCostKrw is < 0
            || a.ProviderId != "internal" && result.StateCode is AutoFulfillmentStates.Accepted or AutoFulfillmentStates.DriverAssigned or AutoFulfillmentStates.InTransit or AutoFulfillmentStates.Completed
                && string.IsNullOrWhiteSpace(result.ExternalRequestId))
            result = new(a.StateCode == AutoFulfillmentStates.CancelPending ? AutoFulfillmentStates.CancelPending : AutoFulfillmentStates.ResultUnknown);
        if (a.StateCode == AutoFulfillmentStates.CancelPending && result.StateCode == AutoFulfillmentStates.ResultUnknown)
            result = new(AutoFulfillmentStates.CancelPending, live.ExternalRequestId);
        if (result.StateCode == AutoFulfillmentStates.Cancelled && live.ProviderId != "internal")
        {
            var released = await adapter.외부증거반영Async(new(Context(key, live, revision), FulfillmentEvidenceKinds.Cancelled,
                (result.ExternalRequestId ?? live.AttemptId) + ":cancel:" + live.Revision, null, Now), CancellationToken.None);
            if (released.IsFailed) result = new(AutoFulfillmentStates.CancelPending, live.ExternalRequestId);
        }
        if (!TransitionAllowed(live.StateCode, result.StateCode)) result = new(live.StateCode, live.ExternalRequestId, live.ConfirmedIncurredCostKrw);
        live.StateCode = result.StateCode; live.ExternalRequestId = result.ExternalRequestId ?? live.ExternalRequestId;
        if (Terminal(result.StateCode) || result.StateCode == AutoFulfillmentStates.Completed) live.ConfirmedIncurredCostKrw = result.StateCode == AutoFulfillmentStates.Completed ? result.ConfirmedIncurredCostKrw ?? live.CostKrw : result.ConfirmedIncurredCostKrw;
        live.Revision++; live.UpdatedAtUtc = Now; History(live, result.StateCode);
        current.StateCode = live.StateCode; current.Revision++; current.UpdatedAtUtc = Now;
        try { await db.SaveChangesAsync(CancellationToken.None); if (applyTx is not null) await applyTx.CommitAsync(CancellationToken.None); }
        catch (DbUpdateConcurrencyException) { db.ChangeTracker.Clear(); }
        return Result.Ok((await 상태Async(key, CancellationToken.None))!);
    }

    public async Task<bool> 내부수행허용Async(FulfillmentSourceKey key, string? attemptId = null, CancellationToken ct = default)
    {
        if (!Enabled) return false;
        var c = await db.NeighborhoodFulfillments.AsNoTracking().SingleOrDefaultAsync(x => x.SourceKind == key.SourceKind && x.RequestId == key.SourceId, ct);
        if (c is null || !c.IsAutomatic || string.IsNullOrEmpty(c.AttemptId) || attemptId is not null && c.AttemptId != attemptId) return false;
        if (c.StateCode is not (AutoFulfillmentStates.Submitting or AutoFulfillmentStates.Accepted or AutoFulfillmentStates.DriverAssigned or AutoFulfillmentStates.InTransit)) return false;
        return await db.NeighborhoodFulfillmentAttempts.AsNoTracking().AnyAsync(x => x.AttemptId == c.AttemptId && x.ProviderId == "internal"
            && (x.StateCode == AutoFulfillmentStates.Submitting || x.StateCode == AutoFulfillmentStates.Accepted || x.StateCode == AutoFulfillmentStates.DriverAssigned || x.StateCode == AutoFulfillmentStates.InTransit), ct);
    }

    public async Task<Result<AutoFulfillmentSnapshot>> 모의증거기록Async(string attemptId, FulfillmentMockEvidenceRequest request, CancellationToken ct = default)
    {
        if (!Enabled) return Disabled();
        if (request.ClientRequestId == Guid.Empty || request.ExternalDriverReference?.Length > 100 || request.ConfirmedIncurredCostKrw is < 0)
            return Fail("InvalidMockEvidence", "모의 사건 입력을 확인해 주세요.", 400);
        await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        var job = await db.FulfillmentMockJobs.SingleOrDefaultAsync(x => x.AttemptId == attemptId, ct);
        if (job is null) return Fail("MockJobNotFound", "모의 접수 기록을 찾을 수 없습니다.", 404);
        var key = new FulfillmentSourceKey(job.SourceKind, job.RequestId); var current = await CurrentAsync(key, ct);
        var digest = Digest(request);
        if (job.LastClientRequestId == request.ClientRequestId) return job.LastRequestDigest == digest ? Result.Ok((await 상태Async(key, ct))!) : Fail("FulfillmentIdempotencyConflict", "같은 모의 사건 번호의 내용이 다릅니다.");
        if (current?.AttemptId != attemptId || current.Revision != request.ExpectedRevision) return Fail("FulfillmentRevisionConflict", "현재 수행 판본과 모의 사건이 다릅니다.");
        var a = await db.NeighborhoodFulfillmentAttempts.SingleAsync(x => x.AttemptId == attemptId, ct);
        var source = await sources[key.SourceKind].읽기Async(key, ct);
        if (source is null || source.IsClosed || source.Leg != a.Leg || a.StateCode != job.StateCode) return Fail("MockEvidenceSourceChanged", "종료 또는 변경된 수행 차수에는 사건을 적용하지 않습니다.");
        var next = request.EvidenceKind switch
        {
            FulfillmentEvidenceKinds.DriverAssigned when job.StateCode == AutoFulfillmentStates.Accepted && !string.IsNullOrWhiteSpace(request.ExternalDriverReference) => AutoFulfillmentStates.DriverAssigned,
            FulfillmentEvidenceKinds.AssignmentReleased when job.StateCode == AutoFulfillmentStates.DriverAssigned => AutoFulfillmentStates.Accepted,
            FulfillmentEvidenceKinds.PickedUp when job.StateCode == AutoFulfillmentStates.DriverAssigned => AutoFulfillmentStates.InTransit,
            FulfillmentEvidenceKinds.Delivered when job.StateCode == AutoFulfillmentStates.InTransit => AutoFulfillmentStates.Completed,
            FulfillmentEvidenceKinds.Rejected when job.StateCode is AutoFulfillmentStates.Accepted or AutoFulfillmentStates.DriverAssigned => AutoFulfillmentStates.Failed,
            FulfillmentEvidenceKinds.Cancelled when job.StateCode is AutoFulfillmentStates.Accepted or AutoFulfillmentStates.DriverAssigned => AutoFulfillmentStates.Cancelled,
            _ => null
        };
        if (next is null) return Fail("InvalidMockTransition", "접수·배정·픽업·전달 순서를 확인해 주세요.");
        var evidence = new FulfillmentExecutionEvidence(Context(key, a, current.Revision), request.EvidenceKind,
            job.ExternalRequestId + ":" + (job.Revision + 1), request.ExternalDriverReference ?? job.ExternalDriverReference, Now);
        var applied = await sources[key.SourceKind].외부증거반영Async(evidence, ct);
        if (applied.IsFailed) return Result.Fail<AutoFulfillmentSnapshot>(applied.Errors);
        if (request.EvidenceKind is FulfillmentEvidenceKinds.DriverAssigned or FulfillmentEvidenceKinds.AssignmentReleased) job.AssignmentRevision++;
        job.ExternalDriverReference = next == AutoFulfillmentStates.DriverAssigned ? request.ExternalDriverReference
            : next == AutoFulfillmentStates.Accepted || Terminal(next) ? null : job.ExternalDriverReference;
        job.StateCode = next; job.Revision++; job.UpdatedAtUtc = Now; job.LastClientRequestId = request.ClientRequestId; job.LastRequestDigest = digest;
        job.ConfirmedIncurredCostKrw = next == AutoFulfillmentStates.Completed ? a.CostKrw
            : Terminal(next) ? request.ConfirmedIncurredCostKrw : job.ConfirmedIncurredCostKrw;
        a.StateCode = next; a.ConfirmedIncurredCostKrw = job.ConfirmedIncurredCostKrw; a.Revision++; a.UpdatedAtUtc = Now; History(a, next);
        current.StateCode = next; current.Revision++; current.UpdatedAtUtc = Now;
        await db.SaveChangesAsync(ct); if (tx is not null) await tx.CommitAsync(ct);
        return Terminal(next) ? await 선정실행Async(key, Guid.NewGuid(), ct) : Result.Ok((await 상태Async(key, ct))!);
    }

    public async Task<int> 대기처리Async(int take = 100, CancellationToken ct = default)
    {
        if (!Enabled) return 0;
        var expired = Now.AddMinutes(-5);
        var ids = await db.AutoFulfillmentOutbox.AsNoTracking().Where(x => x.StateCode == "Pending" || x.StateCode == "Processing" && x.UpdatedAtUtc <= expired)
            .OrderBy(x => x.UpdatedAtUtc).Take(Math.Clamp(take, 1, 100)).Select(x => x.Id).ToListAsync(ct);
        var count = 0;
        foreach (var id in ids)
        {
            var row = await db.AutoFulfillmentOutbox.AsNoTracking().SingleAsync(x => x.Id == id, ct);
            var claim = row.ClaimRevision + 1; var now = Now;
            var claimed = await db.AutoFulfillmentOutbox.Where(x => x.Id == id && x.ClaimRevision == row.ClaimRevision && (x.StateCode == "Pending" || x.StateCode == "Processing" && x.UpdatedAtUtc <= expired))
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.StateCode, "Processing").SetProperty(x => x.ClaimRevision, claim).SetProperty(x => x.UpdatedAtUtc, now), ct);
            if (claimed == 0) continue;
            Result<AutoFulfillmentSnapshot> result;
            try { result = await 선정실행Async(new(row.SourceKind, row.RequestId), row.OperationId, ct); }
            catch (Exception ex) when (ex is DbUpdateException or TimeoutException or HttpRequestException) { result = Fail("RoutingRetryRequired", "원 요청의 수행 상태를 다시 확인합니다."); }
            var status = result.IsSuccess && result.Value.StateCode is not (AutoFulfillmentStates.Submitting or AutoFulfillmentStates.ResultUnknown or AutoFulfillmentStates.CancelPending) ? "Succeeded" : "Pending";
            var code = result.IsFailed ? result.Errors.First().Metadata.GetValueOrDefault("ErrorCode") as string : null;
            var completedAt = Now;
            await db.AutoFulfillmentOutbox.Where(x => x.Id == id && x.StateCode == "Processing" && x.ClaimRevision == claim)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.StateCode, status).SetProperty(x => x.LastErrorCode, code).SetProperty(x => x.UpdatedAtUtc, completedAt), CancellationToken.None);
            count++;
        }
        // 같은 수행을 재조회합니다. 모의 job은 시간 경과로 진행하지 않으며 원천 기사 상태만 확인합니다.
        var active = await db.NeighborhoodFulfillments.AsNoTracking().Where(x => x.IsAutomatic
            && (x.StateCode == AutoFulfillmentStates.AwaitingRouting || x.StateCode == AutoFulfillmentStates.Submitting || x.StateCode == AutoFulfillmentStates.ResultUnknown
                || x.StateCode == AutoFulfillmentStates.Accepted || x.StateCode == AutoFulfillmentStates.DriverAssigned
                || x.StateCode == AutoFulfillmentStates.InTransit || x.StateCode == AutoFulfillmentStates.CancelPending))
            .OrderBy(x => x.UpdatedAtUtc).Take(Math.Clamp(take, 1, 100)).Select(x => new { x.SourceKind, x.RequestId }).ToListAsync(ct);
        foreach (var item in active) await 선정실행Async(new(item.SourceKind, item.RequestId), Guid.NewGuid(), ct);
        return count;
    }

    private Task<NeighborhoodFulfillmentRecord?> CurrentAsync(FulfillmentSourceKey key, CancellationToken ct) => db.NeighborhoodFulfillments.SingleOrDefaultAsync(x => x.SourceKind == key.SourceKind && x.RequestId == key.SourceId, ct);
    private Task<AutoFulfillmentConsentRecord?> ConsentAsync(FulfillmentSourceKey key, CancellationToken ct) => db.AutoFulfillmentConsents.SingleOrDefaultAsync(x => x.SourceKind == key.SourceKind && x.RequestId == key.SourceId, ct);
    private static FulfillmentAttemptContext Context(FulfillmentSourceKey key, NeighborhoodFulfillmentAttemptRecord a, long revision)
        => new(key, a.AttemptId, a.ProviderId, revision, a.ConsentRevision ?? 0, a.Leg, a.CostKrw, a.EstimatedCompletionUtc, a.ExternalRequestId);
    private static bool Terminal(string state) => state is AutoFulfillmentStates.Cancelled or AutoFulfillmentStates.Failed;
    private static bool Known(string state) => state is AutoFulfillmentStates.Accepted or AutoFulfillmentStates.DriverAssigned or AutoFulfillmentStates.InTransit
        or AutoFulfillmentStates.Completed or AutoFulfillmentStates.Cancelled or AutoFulfillmentStates.Failed or AutoFulfillmentStates.ResultUnknown or AutoFulfillmentStates.CancelPending;
    private static bool TransitionAllowed(string from, string to) => !Terminal(from) && from != AutoFulfillmentStates.Completed
        && (to is AutoFulfillmentStates.ResultUnknown or AutoFulfillmentStates.CancelPending || from is AutoFulfillmentStates.Submitting or AutoFulfillmentStates.ResultUnknown or AutoFulfillmentStates.CancelPending
            || from == AutoFulfillmentStates.Accepted && to is AutoFulfillmentStates.Accepted or AutoFulfillmentStates.DriverAssigned or AutoFulfillmentStates.Cancelled or AutoFulfillmentStates.Failed
            || from == AutoFulfillmentStates.DriverAssigned && to is AutoFulfillmentStates.DriverAssigned or AutoFulfillmentStates.Accepted or AutoFulfillmentStates.InTransit or AutoFulfillmentStates.Cancelled or AutoFulfillmentStates.Failed
            || from == AutoFulfillmentStates.InTransit && to is AutoFulfillmentStates.InTransit or AutoFulfillmentStates.Completed);
    private void History(NeighborhoodFulfillmentAttemptRecord a, string state)
    {
        var h = JsonSerializer.Deserialize<List<HistoryItem>>(a.HistoryJson) ?? []; h.Add(new(state, Now)); a.HistoryJson = JsonSerializer.Serialize(h);
    }
    internal static string Digest(object value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value))));
    private static Result<AutoFulfillmentSnapshot> Disabled() => Fail("FulfillmentRoutingDisabled", "자동 수행은 기능을 켠 Simulation 검증 환경에서만 사용할 수 있습니다.", 403);
    private static Result<AutoFulfillmentSnapshot> Fail(string code, string message, int status = 409) => Result.Fail<AutoFulfillmentSnapshot>(new Error(message).WithMetadata("ErrorCode", code).WithMetadata("StatusCode", status));
    private static Result<Selection> FailSelection(string code) => Result.Fail<Selection>(new Error("현재 수행 조건을 확인해 주세요.").WithMetadata("ErrorCode", code).WithMetadata("StatusCode", 409));
    private sealed record Selection(string? Attempt, long Revision, string Action = "submit");
    private sealed record ConsentReceipt(Guid Id, string Digest);
    private sealed record HistoryItem(string StateCode, DateTime AtUtc);
}
