using Ssalddel.Contracts.Common.Orderer;

namespace Ssalddel.Ui.Common.Areas.BackOffice.ViewModels;

// 초안 형식만 만든다. 현재 시각 취득과 초안 편집·저장은 ViewModel에 남긴다.
internal static class 같이수입준비초안Factory
{
    public static 같이수입준비원장저장요청 CreateBase(공동구매자동집단요약응답 group)
        => new()
        {
            재료키 = group.상품키,
            재료명 = group.상품명,
            재료품목목록 = [Material(group)],
            도착국가코드 = 같이수입준비국가코드.대한민국,
            기준통화코드 = "KRW",
            미확인항목목록 = ["공급자·견적·품목분류·국가별 규제 자료 확인 필요"]
        };

    public static 같이수입준비재료품목 Material(공동구매자동집단요약응답 group)
        => new()
        {
            재료키 = group.상품키,
            재료명 = group.상품명,
            원천자동집단Id = group.자동집단Id,
            원천Hs코드 = group.HS코드,
            모인수요수량 = group.총희망수량,
            수량단위 = group.수량단위
        };

    public static 같이수입품목분류후보 NewClassification(
        공동구매자동집단요약응답 group,
        같이수입준비원장저장요청 draft,
        DateTimeOffset checkedAtUtc)
        => new()
        {
            후보키 = $"classification-{draft.품목분류후보목록.Count + 1}",
            재료키 = group.상품키,
            관할국가코드 = draft.도착국가코드,
            분류체계코드 = 같이수입준비품목분류체계코드.대상국가체계(draft.도착국가코드),
            품목코드 = group.HS코드,
            검토상태코드 = 같이수입준비검토상태코드.전문가검토필요,
            전문가검토필요 = true,
            확인시각Utc = checkedAtUtc
        };

    public static 같이수입예상비용근거 NewCost(
        string key, string category, string label, string currency, DateTimeOffset checkedAtUtc)
        => new()
        {
            비용키 = key,
            범주코드 = category,
            표시명 = label,
            통화코드 = currency,
            확인시각Utc = checkedAtUtc
        };

    public static string CostLabel(string category)
        => category switch
        {
            같이수입준비비용범주코드.상품원가 => "상품 원가",
            같이수입준비비용범주코드.국제운송보험 => "국제 운송·보험",
            같이수입준비비용범주코드.관세 => "예상 관세",
            같이수입준비비용범주코드.세금 => "예상 수입 세금",
            같이수입준비비용범주코드.국내이행 => "국내 이행 예상비",
            _ => category
        };
}
