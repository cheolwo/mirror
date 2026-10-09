using System.Data;
using System.Text.Json;
using FluentResults;
using Microsoft.EntityFrameworkCore;
using Ssalddel.ApiMetadata;
using Ssalddel.Contracts.Common.Fulfillment;
using Ssalddel.Contracts.Food;
using Ssalddel.Services.Fulfillment;
using 살뜰.Data;
using 살뜰.Services.Options;
using Ssalddel.Domain.Community;

namespace Ssalddel.Application.Food;

public interface I음식자동수행UseCase
{
    Task<Result<FoodAutoFulfillmentQuoteResponse>> 조건조회Async(long restaurantId, CancellationToken ct);
    Task<Result<AutoFulfillmentSnapshot>> 상태Async(string orderNo, string ownerId, CancellationToken ct);
    Task<Result<AutoFulfillmentSnapshot>> 동의Async(string orderNo, string ownerId, AutoFulfillmentConsent consent, CancellationToken ct);
    Task<Result<AutoFulfillmentSnapshot>> 재확인Async(string orderNo, string ownerId, AutoFulfillmentActionRequest request, CancellationToken ct);
    Task<Result<AutoFulfillmentSnapshot>> 수행취소Async(string orderNo, string ownerId, AutoFulfillmentActionRequest request, CancellationToken ct);
    Task<Result<RestaurantFulfillmentSupportResponse>> 식당설정조회Async(long restaurantId, CancellationToken ct);
    Task<Result<RestaurantFulfillmentSupportResponse>> 식당설정저장Async(long restaurantId, string actorId, RestaurantFulfillmentSupportRequest request, CancellationToken ct);
}

[SsalddelApiWorkflow(SsalddelWorkflow.FoodDelivery)]
[SsalddelUseCase("음식 자동 수행 조건과 결과", Summary = "소유 음식 주문의 사전 동의·수행 결과와 식당별 배송비 분담 설정을 관리합니다. 실제 PG·지원금 청구·기사 지급은 실행하지 않습니다.")]
public sealed class 음식자동수행UseCase(SsalddelContext db, I자동수행Coordinator coordinator,
    ISsalddelExecutionModePolicy execution, TimeProvider clock) : I음식자동수행UseCase
{
    public async Task<Result<FoodAutoFulfillmentQuoteResponse>> 조건조회Async(long restaurantId, CancellationToken ct)
    {
        if (restaurantId <= 0 || !await db.음식점공개프로필.AsNoTracking().AnyAsync(x => x.Id == restaurantId && x.공개여부, ct))
            return Fail<FoodAutoFulfillmentQuoteResponse>("RestaurantNotFound", "공개된 음식점을 찾을 수 없습니다.", 404);
        if (!coordinator.Enabled)
            return Result.Ok(new FoodAutoFulfillmentQuoteResponse { RestaurantId = restaurantId, Enabled = false,
                Notice = "자동 수행 기능이 중지되어 식당 지원 조건을 조회하지 않습니다. " + AutoFulfillmentNotice.Simulation });
        var settings = await db.RestaurantFulfillmentSupports.AsNoTracking().SingleOrDefaultAsync(x => x.RestaurantId == restaurantId, ct);
        return Result.Ok(new FoodAutoFulfillmentQuoteResponse
        {
            RestaurantId = restaurantId,
            Enabled = coordinator.Enabled && settings?.Enabled == true,
            RestaurantSupportKrw = settings?.FixedContributionKrw ?? 0m,
            RestaurantSupportRevision = settings?.Revision ?? 0,
            CostSharingPolicy = settings is null ? null : DeliveryCostSharingStorage.Read(settings.CostSharingPolicyJson, settings.FixedContributionKrw),
            Providers = coordinator.Enabled ? AutoFulfillmentNotice.Providers : [],
            Notice = settings?.Enabled == true ? AutoFulfillmentNotice.Simulation
                : "식당이 배송비 분담 조건을 설정한 뒤 자동 수행에 동의할 수 있습니다. " + AutoFulfillmentNotice.Simulation
        });
    }

    public async Task<Result<AutoFulfillmentSnapshot>> 상태Async(string orderNo, string ownerId, CancellationToken ct)
    {
        if (!await OwnsAsync(orderNo, ownerId, ct)) return NotFound();
        if (!coordinator.Enabled)
        {
            var source = await db.음식주문.AsNoTracking().SingleAsync(x => x.주문번호 == orderNo && x.주문자UserId == ownerId, ct);
            return !source.모의처리처수행 ? Fail<AutoFulfillmentSnapshot>("FulfillmentNotFound", "자동 수행이 등록된 주문이 아닙니다.", 404)
                : Result.Ok(AutoFulfillmentSnapshotProjection.Paused(Key(orderNo),
                    source.상태 is 음식주문상태코드.취소 or 음식주문상태코드.거절
                        or 음식주문상태코드.전달완료 or 음식주문상태코드.수령확인));
        }
        var state = await coordinator.상태Async(Key(orderNo), ct);
        return state is null ? Fail<AutoFulfillmentSnapshot>("FulfillmentNotFound", "자동 수행이 등록된 주문이 아닙니다.", 404) : Result.Ok(state);
    }

    public async Task<Result<AutoFulfillmentSnapshot>> 동의Async(string orderNo, string ownerId, AutoFulfillmentConsent consent, CancellationToken ct)
    {
        if (!await OwnsAsync(orderNo, ownerId, ct)) return NotFound();
        if (!await db.음식주문.AsNoTracking().AnyAsync(x => x.주문번호 == orderNo && x.모의처리처수행, ct))
            return Fail<AutoFulfillmentSnapshot>("LegacyFulfillmentLocked", "기존 주문의 수행 방식을 자동으로 전환하지 않습니다.", 409);
        return await coordinator.사전동의저장Async(Key(orderNo), ownerId, consent, ct);
    }

    public async Task<Result<AutoFulfillmentSnapshot>> 재확인Async(string orderNo, string ownerId, AutoFulfillmentActionRequest request, CancellationToken ct)
        => !await OwnsAsync(orderNo, ownerId, ct) ? NotFound() : await coordinator.재확인Async(Key(orderNo), request, ct);

    public async Task<Result<AutoFulfillmentSnapshot>> 수행취소Async(string orderNo, string ownerId, AutoFulfillmentActionRequest request, CancellationToken ct)
        => !await OwnsAsync(orderNo, ownerId, ct) ? NotFound() : await coordinator.취소Async(Key(orderNo), request, ct);

    public async Task<Result<RestaurantFulfillmentSupportResponse>> 식당설정조회Async(long restaurantId, CancellationToken ct)
    {
        if (restaurantId <= 0) return Fail<RestaurantFulfillmentSupportResponse>("RestaurantScopeRequired", "인증된 식당을 확인해 주세요.", 403);
        var current = await db.RestaurantFulfillmentSupports.AsNoTracking().SingleOrDefaultAsync(x => x.RestaurantId == restaurantId, ct);
        return Result.Ok(current is null ? new RestaurantFulfillmentSupportResponse { RestaurantId = restaurantId } : ToDto(current));
    }

    public async Task<Result<RestaurantFulfillmentSupportResponse>> 식당설정저장Async(long restaurantId, string actorId,
        RestaurantFulfillmentSupportRequest request, CancellationToken ct)
    {
        if (!coordinator.Enabled || !execution.IsSimulation)
            return Fail<RestaurantFulfillmentSupportResponse>("FulfillmentRoutingDisabled", "자동 수행은 활성화된 Simulation에서만 설정할 수 있습니다.", 409);
        if (restaurantId <= 0 || string.IsNullOrWhiteSpace(actorId))
            return Fail<RestaurantFulfillmentSupportResponse>("RestaurantScopeRequired", "인증된 식당과 변경 주체를 확인해 주세요.", 403);
        request = JsonSerializer.Deserialize<RestaurantFulfillmentSupportRequest>(JsonSerializer.Serialize(request))!;
        if (request.CostSharingPolicy is { } requestedPolicy) request.CostSharingPolicy = DeliveryCostSharingCalculator.Clone(requestedPolicy);
        var policy = DeliveryCostSharingCalculator.Effective(request.CostSharingPolicy, request.FixedContributionKrw);
        if (DeliveryCostSharingCalculator.ValidationError(policy) is not null
            || request.CostSharingPolicy is not null && request.FixedContributionKrw != 0 && request.FixedContributionKrw != DeliveryCostSharingStorage.LegacyAmount(policy))
            return Fail<RestaurantFulfillmentSupportResponse>("InvalidCostSharingPolicy", "배송비 분담 방식과 필요한 금액·비율·한도만 확인해 주세요.", 400);
        if (request.ClientRequestId == Guid.Empty || request.ExpectedRevision < 0 || !request.Agreed)
            return Fail<RestaurantFulfillmentSupportResponse>("InvalidRestaurantSupport", "설정 판본·배송비 분담 조건 동의를 확인해 주세요.", 400);
        if (!await db.음식점공개프로필.AsNoTracking().AnyAsync(x => x.Id == restaurantId, ct))
            return Fail<RestaurantFulfillmentSupportResponse>("RestaurantNotFound", "식당을 찾을 수 없습니다.", 404);
        await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        var current = await db.RestaurantFulfillmentSupports.SingleOrDefaultAsync(x => x.RestaurantId == restaurantId, ct);
        if (current is not null)
        {
            await db.Entry(current).ReloadAsync(ct);
            var currentPolicy = DeliveryCostSharingStorage.Read(current.CostSharingPolicyJson, current.FixedContributionKrw);
            if (request.CostSharingPolicy is null && currentPolicy.Mode != DeliveryCostSharingModes.FixedContribution)
                return Fail<RestaurantFulfillmentSupportResponse>("CostSharingPolicyUpgradeRequired", "현재 배송비 분담 정책 전체를 확인할 수 있는 화면에서 변경해 주세요.", 409);
            if (current.ClientRequestId == request.ClientRequestId)
                return DeliveryCostSharingCalculator.Equivalent(currentPolicy, policy) && current.Enabled == request.Enabled && current.ActorId == actorId
                    ? Result.Ok(ToDto(current))
                    : Fail<RestaurantFulfillmentSupportResponse>("IdempotencyConflict", "같은 요청 번호에 다른 지원 조건이 있습니다.", 409);
        }
        if ((current?.Revision ?? 0) != request.ExpectedRevision)
            return Fail<RestaurantFulfillmentSupportResponse>("RestaurantSupportRevisionConflict", "지원 조건이 바뀌었습니다. 다시 조회해 주세요.", 409);
        if (current is null) { current = new() { RestaurantId = restaurantId }; db.RestaurantFulfillmentSupports.Add(current); }
        current.FixedContributionKrw = DeliveryCostSharingStorage.LegacyAmount(policy);
        current.CostSharingPolicyJson = request.CostSharingPolicy is null ? null : DeliveryCostSharingStorage.Write(policy);
        current.Enabled = request.Enabled;
        current.ActorId = actorId; current.ClientRequestId = request.ClientRequestId;
        current.Revision++; current.UpdatedAtUtc = clock.GetUtcNow().UtcDateTime;
        try
        {
            await db.SaveChangesAsync(ct);
            if (tx is not null) await tx.CommitAsync(ct);
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            return Fail<RestaurantFulfillmentSupportResponse>("RestaurantSupportRevisionConflict", "다른 지원 설정이 먼저 저장됐습니다. 다시 조회해 주세요.", 409);
        }
        return Result.Ok(ToDto(current));
    }

    private Task<bool> OwnsAsync(string orderNo, string ownerId, CancellationToken ct)
        => db.음식주문.AsNoTracking().AnyAsync(x => x.주문번호 == orderNo && x.주문자UserId == ownerId && ownerId != "", ct);
    private static FulfillmentSourceKey Key(string orderNo) => new(FulfillmentSourceKinds.FoodOrder, orderNo);
    private static Result<AutoFulfillmentSnapshot> NotFound() => Fail<AutoFulfillmentSnapshot>("FoodOrderNotFound", "본인 음식 주문을 찾을 수 없습니다.", 404);
    private static RestaurantFulfillmentSupportResponse ToDto(RestaurantFulfillmentSupportRecord x) => new()
    { RestaurantId = x.RestaurantId, Revision = x.Revision, Enabled = x.Enabled, FixedContributionKrw = x.FixedContributionKrw,
        CostSharingPolicy = DeliveryCostSharingStorage.Read(x.CostSharingPolicyJson, x.FixedContributionKrw), UpdatedAtUtc = x.UpdatedAtUtc };
    private static Result<T> Fail<T>(string code, string message, int status) => Result.Fail<T>(new Error(message).WithMetadata("ErrorCode", code).WithMetadata("StatusCode", status));
}
