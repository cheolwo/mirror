using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Food;
using 살뜰.Data;

namespace Ssalddel.Services.Food;

public interface I음식주문메뉴검증Service
{
    Task<음식주문등록요청> 서버기준요청생성Async(
        음식주문등록요청 request,
        CancellationToken cancellationToken);
}

/// <summary>
/// 주문자가 선택한 공개 메뉴 ID를 서버 원장과 대조하고 메뉴명·단가 스냅샷을
/// 서버 값으로 다시 만듭니다. 클라이언트가 보낸 표시명과 금액은 주문 금액의
/// 근거로 사용하지 않습니다.
/// </summary>
public sealed class 음식주문메뉴검증Service(
    SsalddelContext db) : I음식주문메뉴검증Service
{
    public async Task<음식주문등록요청> 서버기준요청생성Async(
        음식주문등록요청 request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var restaurant = await db.음식점공개프로필
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.Id == request.음식점Id && item.공개여부,
                cancellationToken)
            ?? throw new 음식주문입력확인Exception("공개된 음식점을 찾을 수 없습니다.", FoodOrderSubmissionErrorCodes.MenuUnavailable);

        if (!restaurant.주문가능여부)
        {
            throw new 음식주문입력확인Exception("현재 주문을 받을 수 없는 음식점입니다.", FoodOrderSubmissionErrorCodes.MenuUnavailable);
        }

        if (request.상품목록 is null)
            throw new 음식주문입력확인Exception("주문할 메뉴를 한 개 이상 선택해 주세요.");
        var requestedItems = request.상품목록.ToArray();
        if (requestedItems.Length == 0)
        {
            throw new 음식주문입력확인Exception("주문할 메뉴를 한 개 이상 선택해 주세요.");
        }

        if (requestedItems.Length > 50)
        {
            throw new 음식주문입력확인Exception("한 주문에서 선택할 수 있는 메뉴 종류는 50개 이하입니다.");
        }

        if (requestedItems.Any(item => item is null || item.메뉴Id is null or <= 0 || item.수량 is <= 0 or > 100))
        {
            throw new 음식주문입력확인Exception("공개 메뉴 ID와 수량을 확인해 주세요.");
        }

        var menuIds = requestedItems.Select(item => item.메뉴Id!.Value).ToList();
        if (menuIds.Distinct().Count() != menuIds.Count)
        {
            throw new 음식주문입력확인Exception("같은 메뉴를 중복해 제출할 수 없습니다.");
        }

        var menus = await db.음식점메뉴
            .AsNoTracking()
            .Where(item => menuIds.Contains(item.Id)
                           && item.음식점공개프로필Id == request.음식점Id
                           && item.공개여부)
            .ToDictionaryAsync(item => item.Id, cancellationToken);

        if (menus.Count != menuIds.Count)
        {
            throw new 음식주문입력확인Exception("선택한 음식점의 공개 메뉴가 아닌 항목이 포함되어 있습니다.", FoodOrderSubmissionErrorCodes.MenuUnavailable);
        }

        var soldOut = menus.Values.FirstOrDefault(item => item.품절여부);
        if (soldOut is not null)
        {
            throw new 음식주문입력확인Exception($"현재 품절된 메뉴입니다: {soldOut.메뉴명}", FoodOrderSubmissionErrorCodes.MenuUnavailable);
        }

        if (request.메뉴가격확인필요
            && requestedItems.Any(item => item.단가 != menus[item.메뉴Id!.Value].판매가))
            throw new 음식주문메뉴가격변경Exception();

        var canonicalItems = requestedItems
            .Select(item =>
            {
                var menu = menus[item.메뉴Id!.Value];
                return new 음식주문상품Dto
                {
                    메뉴Id = menu.Id,
                    상품명 = menu.메뉴명,
                    수량 = item.수량,
                    단가 = menu.판매가
                };
            })
            .ToArray();
        var total = canonicalItems.Sum(item => item.단가 * item.수량);
        if (total < restaurant.최소주문금액)
        {
            throw new 음식주문입력확인Exception(
                $"최소 주문 금액은 {restaurant.최소주문금액:N0}원입니다. 현재 서버 계산 금액은 {total:N0}원입니다.");
        }

        return new 음식주문등록요청
        {
            클라이언트요청Id = request.클라이언트요청Id,
            음식점Id = restaurant.Id,
            주문자UserId = request.주문자UserId,
            수령인정보 = request.수령인정보,
            상품목록 = canonicalItems,
            결제수단 = request.결제수단,
            메뉴가격확인필요 = request.메뉴가격확인필요
        };
    }
}
