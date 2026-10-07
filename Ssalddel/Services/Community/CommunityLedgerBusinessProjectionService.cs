using Microsoft.Extensions.Logging;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.PrivacyRetention;
using Ssalddel.Services.PrivacyRetention;

namespace Ssalddel.Services.Community;

public interface I커뮤니티원장업무투영동기화Service
{
    Task 갱신Async(커뮤니티원장Dto 원장, CancellationToken cancellationToken = default);
}

public interface I원장업무투영동기화Handler
{
    bool 처리대상인가(커뮤니티원장Dto 원장);

    Task 동기화Async(커뮤니티원장Dto 원장, CancellationToken cancellationToken = default);
}

public sealed class 커뮤니티원장업무투영동기화Service : I커뮤니티원장업무투영동기화Service
{
    private readonly IEnumerable<I원장업무투영동기화Handler> _handlers;
    private readonly ILogger<커뮤니티원장업무투영동기화Service> _logger;
    private readonly I개인정보복원차단Service? _privacyBarrier;

    public 커뮤니티원장업무투영동기화Service(
        IEnumerable<I원장업무투영동기화Handler> handlers,
        ILogger<커뮤니티원장업무투영동기화Service> logger,
        I개인정보복원차단Service? privacyBarrier = null)
    {
        _handlers = handlers;
        _logger = logger;
        _privacyBarrier = privacyBarrier;
    }

    public async Task 갱신Async(커뮤니티원장Dto 원장, CancellationToken cancellationToken = default)
    {
        if (!업무투영허용(원장))
        {
            return;
        }
        if (원장.확장속성.ContainsKey(배송원장개인정보파기Service.Marker)) return;
        if (_privacyBarrier is not null)
        {
            var foodNo = 원장.외부참조.GetValueOrDefault("음식주문번호");
            var requestNo = 원장.외부참조.GetValueOrDefault("화주운송의뢰Id");
            var sourceNo = 원장.외부참조.GetValueOrDefault("원천Id");
            if (!string.IsNullOrWhiteSpace(foodNo) && !await _privacyBarrier.복원허용Async(개인정보파기원천Codes.FoodOrder, foodNo, cancellationToken)) return;
            if (!string.IsNullOrWhiteSpace(requestNo) && !await _privacyBarrier.복원허용Async(개인정보파기원천Codes.NeighborhoodDelivery, requestNo, cancellationToken)) return;
            if (원장.외부참조.GetValueOrDefault("원천유형") is "FoodOrder" or "RestaurantFoodOrder" or "음식주문" or "음식점주문"
                && !string.IsNullOrWhiteSpace(sourceNo) && !await _privacyBarrier.복원허용Async(개인정보파기원천Codes.FoodOrder, sourceNo, cancellationToken)) return;
        }

        var failures = new List<Exception>();
        foreach (var handler in _handlers)
        {
            var handlerName = handler.GetType().Name;
            bool 대상;
            try
            {
                대상 = handler.처리대상인가(원장);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "커뮤니티 원장 업무 투영 대상 판정에 실패했습니다. Handler={Handler}, 원장Id={원장Id}", handlerName, 원장.원장Id);
                failures.Add(ex);
                continue;
            }

            if (!대상)
            {
                continue;
            }

            try
            {
                await handler.동기화Async(원장, cancellationToken);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "커뮤니티 원장 업무 투영 동기화에 실패했습니다. Handler={Handler}, 원장Id={원장Id}", handlerName, 원장.원장Id);
                failures.Add(ex);
            }
        }

        if (failures.Count > 0)
        {
            throw new AggregateException("커뮤니티 원장 업무 투영 중 하나 이상의 처리기가 실패했습니다.", failures);
        }
    }

    public static bool 업무투영허용(커뮤니티원장Dto 원장)
    {
        ArgumentNullException.ThrowIfNull(원장);
        var provisional = 원장.확장속성.TryGetValue(
                              CommunityPostProvisionalLedgerPolicy.LedgerMaturityAttributeKey,
                              out var maturity)
                          && string.Equals(
                              maturity,
                              CommunityPostProvisionalLedgerPolicy.LedgerMaturityCode,
                              StringComparison.OrdinalIgnoreCase);
        var nonBinding = 원장.확장속성.TryGetValue(
                             CommunityPostProvisionalLedgerPolicy.BindingEffectAttributeKey,
                             out var bindingEffect)
                         && string.Equals(
                             bindingEffect,
                             CommunityPostProvisionalLedgerPolicy.NonBindingEffectCode,
                             StringComparison.OrdinalIgnoreCase);
        return !provisional && !nonBinding;
    }
}
