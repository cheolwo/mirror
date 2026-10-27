using Color = MudBlazor.Color;
using Ssalddel.Contracts.Common.Orderer;

namespace SellerApp.Services;

// 집단 수요 응답을 읽기만 하며 기존 검색·정렬과 화면 문구를 유지한다.
internal static class 주문자집단수요Projection
{
    public static IReadOnlyList<공동구매자동집단응답> Filter(IReadOnlyList<공동구매자동집단응답> clusteredDemands, string search)
        => clusteredDemands
            .Where(item => string.IsNullOrWhiteSpace(search)
                           || item.자동집단Id.Contains(search, StringComparison.OrdinalIgnoreCase)
                           || item.상품명.Contains(search, StringComparison.OrdinalIgnoreCase)
                           || item.상품키.Contains(search, StringComparison.OrdinalIgnoreCase)
                           || item.배송권명.Contains(search, StringComparison.OrdinalIgnoreCase)
                           || item.현재상태.Contains(search, StringComparison.OrdinalIgnoreCase))
            .OrderBy(item => item.모집종료여부)
            .ThenByDescending(item => item.모집조건충족여부)
            .ThenByDescending(item => item.수정시각Utc)
            .ToArray();

    public static string Quantity(decimal quantity, string? unit)
        => $"{quantity:N0} {Fallback(unit, "단위 미정")}";

    public static string Fallback(string? value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    public static double DemandProgress(공동구매자동집단응답 item)
    {
        if (item.목표수량 is not > 0)
        {
            return item.모집조건충족여부 ? 100d : 0d;
        }

        return Math.Clamp((double)(item.총희망수량 / item.목표수량.Value * 100m), 0d, 100d);
    }

    public static string DemandProgressText(공동구매자동집단응답 item)
    {
        if (item.목표수량 is > 0)
        {
            var remaining = Math.Max(0m, item.목표수량.Value - item.총희망수량);
            return remaining == 0
                ? $"목표 {Quantity(item.목표수량.Value, item.수량단위)} 충족"
                : $"목표까지 {Quantity(remaining, item.수량단위)} 필요";
        }

        if (item.목표참여자수 is > 0)
        {
            var remaining = Math.Max(0, item.목표참여자수.Value - item.참여자수);
            return remaining == 0
                ? $"목표 {item.목표참여자수.Value}명 충족"
                : $"목표까지 {remaining}명 필요";
        }

        return "목표 수량은 모집 원장에서 협의 중입니다.";
    }

    public static string DemandStatus(공동구매자동집단응답 item)
        => item.현재상태 switch
        {
            공동구매자동집단상태코드.수요수집중 => "수요 수집 중",
            공동구매자동집단상태코드.확정대기 => "조건 충족 · 확인 대기",
            공동구매자동집단상태코드.확정 => "집단 확정",
            공동구매자동집단상태코드.모집종료목표미달 => "모집 종료 · 목표 미달",
            _ => Fallback(item.현재상태, "상태 확인 중")
        };

    public static Color DemandStatusColor(공동구매자동집단응답 item)
        => item.현재상태 switch
        {
            공동구매자동집단상태코드.확정대기 => Color.Info,
            공동구매자동집단상태코드.확정 => Color.Success,
            공동구매자동집단상태코드.모집종료목표미달 => Color.Warning,
            _ => Color.Primary
        };

    public static string RecruitmentDeadline(공동구매자동집단응답 item)
        => item.모집종료시각Utc == default
            ? "협의 중"
            : item.모집종료여부
                ? $"{item.모집종료시각Utc.ToLocalTime():MM.dd HH:mm} 종료"
                : $"{item.모집종료시각Utc.ToLocalTime():MM.dd HH:mm}까지";

    public static string LogisticsMode(string? value)
        => string.Equals(
            value,
            공동구매자동수요물류방식코드.후속검토,
            StringComparison.OrdinalIgnoreCase)
            ? "수요 집계 후 검토"
            : Fallback(value, "후속 검토");

    public static string DemandNextAction(공동구매자동집단응답 item)
        => item.모집조건충족여부
            ? "다음 대응: 공급 가능 수량·포장 단위·가격 구간을 확인해 판매 조건 초안을 준비합니다."
            : "다음 대응: 수요 추이를 살펴보고 최소 공급 수량과 가격 구간을 미리 검토합니다.";

}
