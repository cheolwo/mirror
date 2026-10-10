using Ssalddel.Contracts.Common.Metadata;

using Ssalddel.Contracts.Common.Drivers;
using Ssalddel.Contracts.Driver.Food;
using Ssalddel.Contracts.Food;
using Ssalddel.Application.Food;
using Ssalddel.Services.Food;
using Microsoft.EntityFrameworkCore;
using 살뜰.Data;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.Services.Dispatch.Engine;
using 살뜰.Services.Options;
using 살뜰.Services.Versioning;
using 살뜰.도메인.공통;

namespace Ssalddel.Application.Driver.Food;

[SsalddelCodeMetadata(
    SsalddelCodeFeatureKeys.FoodDriverWork,
    SsalddelCodeLayer.Contract,
    "현재 업무와 호환 workspace 및 정산·완료 상세 조회 계약을 정의한다.",
    Effects = SsalddelCodeEffect.None,
    FlowOrder = 20,
    StepKey = "contract.food-workspace-port",
    DependsOnStepKeys = ["contract.current-work", "contract.legacy-workspace"],
    ExecutionStage = SsalddelCodeExecutionStage.Definition,
    ReadsFrom = SsalddelCodeDataScope.None,
    WritesTo = SsalddelCodeDataScope.None,
    Boundary = "현재 조회와 정산 조회의 호출을 구별하며 조회 계약 자체는 업무 상태를 전이하지 않는다.")]
public interface IFoodDeliveryDriverWorkspaceUseCase
{
    Task<FoodDeliveryDriverCurrentWorkDto> GetCurrentWorkAsync(string driverId, CancellationToken cancellationToken);
    Task<FoodDeliveryDriverWorkspaceDto> GetAsync(string driverId, CancellationToken cancellationToken);
    Task<FoodDeliveryDailySettlementDto> GetDailySettlementAsync(
        string driverId, DateOnly? completionDateKst, CancellationToken cancellationToken);
    Task<FoodDeliveryCompletedDeliveryDetailDto?> GetCompletedDeliveryDetailAsync(
        string driverId, string settlementId, CancellationToken cancellationToken);
}

/// <summary>정산 조회 실패를 정산 내역이 없는 정상 응답과 구분합니다.</summary>
public sealed class FoodDeliverySettlementUnavailableException : Exception
{
    public const string Code = "FoodSettlementUnavailable";
    public FoodDeliverySettlementUnavailableException()
        : base("정산 정보를 불러오지 못했습니다. 잠시 후 다시 확인해 주세요.") { }
}

[SsalddelCodeMetadata(
    SsalddelCodeFeatureKeys.FoodDriverWork,
    SsalddelCodeLayer.Application,
    "정산을 읽지 않고 현재 제안·수행 시도·조리 상태를 조합한다.",
    ContractType = typeof(IFoodDeliveryDriverWorkspaceUseCase),
    Effects = SsalddelCodeEffect.PersistentRead | SsalddelCodeEffect.PersistentWrite | SsalddelCodeEffect.NetworkCall,
    FlowOrder = 110,
    StepKey = "application.current-work",
    DependsOnStepKeys = ["contract.current-work", "application.driver-offer-query", "application.current-bundle-policy", "application.current-work-projector"],
    ExecutionStage = SsalddelCodeExecutionStage.Query,
    ReadsFrom = SsalddelCodeDataScope.OperationalState,
    WritesTo = SsalddelCodeDataScope.OperationalState,
    Boundary = "GetCurrentWorkAsync는 월 정산·주문별 정산·완료 상세를 호출하지 않는다. SQL 전문 상태는 조회만 하며 위임한 수신 조회가 Redis 투영을 재구성할 수 있다.")]
[SsalddelCodeMetadata(
    SsalddelCodeFeatureKeys.FoodDriverWork,
    SsalddelCodeLayer.Application,
    "기존 workspace 호출에 현재 업무와 월·최근 주문 정산을 함께 반환한다.",
    ContractType = typeof(IFoodDeliveryDriverWorkspaceUseCase),
    Effects = SsalddelCodeEffect.PersistentRead | SsalddelCodeEffect.PersistentWrite | SsalddelCodeEffect.NetworkCall,
    FlowOrder = 115,
    StepKey = "application.legacy-workspace",
    DependsOnStepKeys = ["contract.legacy-workspace", "application.current-work"],
    ExecutionStage = SsalddelCodeExecutionStage.Query,
    ReadsFrom = SsalddelCodeDataScope.OperationalState,
    WritesTo = SsalddelCodeDataScope.OperationalState,
    Boundary = "GetAsync 호환 경계다. 정산 조회 실패는 현재 업무가 없는 정상 응답으로 숨기지 않고 별도 오류로 구분한다. 수령 확인과 실제 지급 완료를 추정하지 않는다.")]
public sealed partial class FoodDeliveryDriverWorkspaceUseCase : IFoodDeliveryDriverWorkspaceUseCase
{
    private readonly SsalddelContext _db;
    private readonly I음식배달기사업무Service _driverWork;
    private readonly I배달기사월정산UseCase _settlements;
    private readonly ISsalddelExecutionModePolicy _executionMode;
    private readonly IVersionFeatureFlagService _featureFlags;
    private readonly TimeProvider _timeProvider;
    private readonly FoodDeliveryCompletedDetailAccessOptions _completedDetailAccessOptions;

    public FoodDeliveryDriverWorkspaceUseCase(
        SsalddelContext db,
        I음식배달기사업무Service driverWork,
        I배달기사월정산UseCase settlements,
        ISsalddelExecutionModePolicy executionMode,
        IVersionFeatureFlagService featureFlags,
        TimeProvider? timeProvider = null,
        Microsoft.Extensions.Options.IOptions<FoodDeliveryCompletedDetailAccessOptions>? completedDetailAccessOptions = null)
    {
        _db = db;
        _driverWork = driverWork;
        _settlements = settlements;
        _executionMode = executionMode;
        _featureFlags = featureFlags;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _completedDetailAccessOptions = completedDetailAccessOptions?.Value ?? new FoodDeliveryCompletedDetailAccessOptions();
    }

    public async Task<FoodDeliveryDriverCurrentWorkDto> GetCurrentWorkAsync(
        string driverId,
        CancellationToken cancellationToken)
    {
        var workItems = await _driverWork.제안조회Async(driverId, cancellationToken);
        var offers = workItems
            .Where(x => x.Status == DriverWorkOfferStatus.Recommended)
            .Select(ToOffer)
            .ToArray();
        var activeWork = workItems
            .Where(x => x.Status is DriverWorkOfferStatus.Accepted
                or DriverWorkOfferStatus.MovingToPickup
                or DriverWorkOfferStatus.MovingToDropoff)
            .ToDictionary(x => x.OfferId, StringComparer.Ordinal);
        var activeIds = activeWork.Keys.ToArray();
        var activeQueues = activeIds.Length == 0
            ? []
            : await _db.운송원장
                .AsNoTracking()
                .Where(x => Enumerable.Contains(activeIds, x.의뢰Id)
                            && x.배차업무유형 == 상태값.배차업무유형.음식배달
                            && (x.기사_운송자 == driverId || x.확정기사Id == driverId))
                .OrderBy(x => x.CreatedAt)
                .ToListAsync(cancellationToken);
        var activeAttempts = activeIds.Length == 0
            ? new Dictionary<string, 살뜰.도메인.음식.음식배달시도>(StringComparer.Ordinal)
            : (await _db.음식배달시도
                .AsNoTracking()
                .Where(x => Enumerable.Contains(activeIds, x.제안Id)
                            && x.기사Id == driverId
                            && x.중단시각Utc == null
                            && x.전달완료시각Utc == null)
                .OrderByDescending(x => x.시도순번)
                .ToListAsync(cancellationToken))
                .GroupBy(x => x.제안Id, StringComparer.Ordinal)
                .ToDictionary(x => x.Key, x => x.First(), StringComparer.Ordinal);
        var activeOrderNumbers = activeQueues.Select(ResolveOrderNo).Distinct().ToArray();
        var preparationByOrder = activeOrderNumbers.Length == 0
            ? new Dictionary<string, 음식주문현재조리상태>(StringComparer.Ordinal)
            : (await _db.음식주문.AsNoTracking().Include(x => x.상태이력)
                .Where(x => Enumerable.Contains(activeOrderNumbers, x.주문번호))
                .ToListAsync(cancellationToken))
                .ToDictionary(x => x.주문번호, 음식주문현재조리Policy.계산, StringComparer.Ordinal);
        var active = activeQueues
            .Where(x => activeWork.ContainsKey(x.의뢰Id))
            .Select(x => ToActiveDelivery(
                x,
                activeWork[x.의뢰Id],
                activeAttempts.GetValueOrDefault(x.의뢰Id),
                preparationByOrder.GetValueOrDefault(ResolveOrderNo(x))))
            .ToArray();

        var dispatchAutomationEnabled = _executionMode.IsOperational
                                        && _featureFlags.IsEnabled(
                                            VersionFeatureFlagKeys.FoodDeliveryWorkflow);

        return new FoodDeliveryDriverCurrentWorkDto
        {
            DriverId = driverId,
            Recommendations = offers,
            ActiveDeliveries = active,
            BundleCandidates = BuildBundleCandidates(offers),
            MaxActiveDeliveries = 음식배달기사활성업무Policy.MaxActiveDeliveries,
            DispatchAutomationEnabled = dispatchAutomationEnabled,
            DispatchAutomationNotice = ResolveDispatchAutomationNotice(dispatchAutomationEnabled),
            UpdatedAtUtc = _timeProvider.GetUtcNow().UtcDateTime
        };
    }

    /// <summary>이전 workspace API의 정산 포함 응답을 유지합니다.</summary>
    public async Task<FoodDeliveryDriverWorkspaceDto> GetAsync(string driverId, CancellationToken cancellationToken)
    {
        var current = await GetCurrentWorkAsync(driverId, cancellationToken);
        var settlementResult = await _settlements.당월조회Async(driverId, driverId, cancellationToken);
        if (settlementResult.IsFailed) throw new FoodDeliverySettlementUnavailableException();
        var settlement = settlementResult.Value;
        var orderSettlements = await _db.음식주문기사정산.AsNoTracking()
            .Include(x => x.지급검증목록).Where(x => x.기사Id == driverId)
            .OrderByDescending(x => x.전달완료시각Utc).Take(40).ToListAsync(cancellationToken);
        return new FoodDeliveryDriverWorkspaceDto
        {
            DriverId = current.DriverId, Recommendations = current.Recommendations,
            ActiveDeliveries = current.ActiveDeliveries, BundleCandidates = current.BundleCandidates,
            MaxActiveDeliveries = current.MaxActiveDeliveries,
            DispatchAutomationEnabled = current.DispatchAutomationEnabled,
            DispatchAutomationNotice = current.DispatchAutomationNotice, UpdatedAtUtc = current.UpdatedAtUtc,
            Settlement = settlement,
            OrderSettlements = orderSettlements.Select(x => 음식주문기사정산Recorder.ToDto(
                x, currentExecutionModeCode: _executionMode.Mode.ToString())).ToArray()
        };
    }

    private string ResolveDispatchAutomationNotice(bool enabled)
    {
        if (enabled)
        {
            return "자동 기사 추천이 활성화되어 있습니다.";
        }

        return !_executionMode.IsOperational
            ? "시뮬레이션 모드에서는 자동 기사 추천이 실행되지 않습니다."
            : "음식 배달 기능이 비활성화되어 자동 기사 추천이 실행되지 않습니다.";
    }

    private static FoodDeliveryDriverOfferDto ToOffer(DriverWorkOfferDto offer)
        => FoodDeliveryCurrentWorkProjector.ToOffer(offer);

    private static FoodDeliveryDriverActiveDeliveryDto ToActiveDelivery(살뜰.도메인.운송.운송원장 transport,
        DriverWorkOfferDto offer, 살뜰.도메인.음식.음식배달시도? attempt, 음식주문현재조리상태? preparation)
        => FoodDeliveryCurrentWorkProjector.ToActiveDelivery(transport, offer, attempt, preparation);

    private static string ResolveOrderNo(살뜰.도메인.운송.운송원장 transport)
        => string.IsNullOrWhiteSpace(transport.원본의뢰Id) ? transport.의뢰Id : transport.원본의뢰Id;

    private static IReadOnlyList<FoodDeliveryBundleCandidateDto> BuildBundleCandidates(IReadOnlyList<FoodDeliveryDriverOfferDto> offers)
        => FoodDeliveryCurrentBundlePolicy.Build(offers);
}
