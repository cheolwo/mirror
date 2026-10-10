using Ssalddel.Contracts.Common.Community;
using Ssalddel.Services.PrivacyRetention;
using 살뜰.Services.Dispatch.Engine;

namespace Ssalddel.Services.Community;

/// <summary>이전 음식 주문의 출처가 일치하는 배송 참조만 조회합니다. 원장을 저장하거나 권한을 부여하지 않습니다.</summary>
internal static class 음식주문원장참조Resolver
{
    public static async Task<IReadOnlyList<커뮤니티포함원장참조Dto>> 조회Async(
        I커뮤니티원장저장소 store, 커뮤니티원장Dto root, CancellationToken cancellationToken)
    {
        if (root.원장템플릿Key != CommunityLedgerTemplateKeys.FoodOrder
            || root.확장속성.ContainsKey(배송원장개인정보파기Service.Marker))
            return root.포함원장목록;

        var orderNo = 음식주문원장업무투영Snapshot.생성(root)?.주문번호;
        if (string.IsNullOrWhiteSpace(orderNo)) return root.포함원장목록;

        // 운송 원장 ID를 주문번호로 추측하지 않습니다. 기존 의뢰Id/운송번호와 다른
        // ID를 가진 이전 원장도 동일한 출처와 업무 종류로 검증합니다.
        var candidates = await store.원장목록조회Async(new()
        {
            커뮤니티Id = root.커뮤니티Id,
            원장템플릿Key = CommunityLedgerTemplateKeys.FoodDelivery,
            외부참조조건 = new Dictionary<string, string> { ["원천Id"] = orderNo },
            Limit = 200
        }, cancellationToken);
        var references = root.포함원장목록.ToList();
        foreach (var child in candidates.Where(x =>
                     x.원장템플릿Key == CommunityLedgerTemplateKeys.FoodDelivery
                     && !x.확장속성.ContainsKey(배송원장개인정보파기Service.Marker)
                     && x.커뮤니티Id == root.커뮤니티Id
                     && string.Equals(x.외부참조.GetValueOrDefault("원천Id"), orderNo, StringComparison.Ordinal)
                     && 운송의뢰배차원천유형.Is음식점주문(x.외부참조.GetValueOrDefault("원천유형")))
                 .OrderBy(x => x.원장Id, StringComparer.Ordinal))
        {
            if (references.Any(x => string.Equals(x.원장Id, child.원장Id, StringComparison.OrdinalIgnoreCase))) continue;
            references.Add(new()
            {
                원장Id = child.원장Id, 원장템플릿Key = child.원장템플릿Key,
                역할 = 주문원장포함역할.배송, 필수여부 = true,
                표시순서 = references.Select(x => x.표시순서).DefaultIfEmpty(-1).Max() + 1
            });
        }
        return references;
    }
}
