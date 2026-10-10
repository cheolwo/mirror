using System.Globalization;

namespace Ssalddel.Ui.Common.Areas.App.Components.Food;

/// <summary>과거 관측의 읽기 전용 표시 입력. 현재 판매 메뉴나 주문 등록 계약으로 변환하지 않습니다.</summary>
public sealed record OrdererRecordedMenuObservation
{
    public string Id { get; init; } = string.Empty;
    public string? SourceRecordId { get; init; }
    public string BranchId { get; init; } = string.Empty;
    public string BranchName { get; init; } = string.Empty;
    public string MenuName { get; init; } = string.Empty;
    public IReadOnlyList<string> Options { get; init; } = [];
    public DateOnly? ServiceDate { get; init; }
    public int? Quantity { get; init; }
    public decimal? UnitPriceKrw { get; init; }
    public decimal? DisplayedItemAmountKrw { get; init; }
    public decimal? OrderAmountKrw { get; init; }
    public string? ImageUrl { get; init; }
    public string? ImageMenuName { get; init; }
    public string? ImageSourceUrl { get; init; }
    public bool ImageMenuMatchConfirmed { get; init; }
    public bool ImageOptionsMatchConfirmed { get; init; }
    public bool PublicUseApproved { get; init; }
}

public static class OrdererRecordedMenuPresentation
{
    public const string HistoricalPriceNotice = "과거 주문에서 확인한 기록입니다. 현재 판매가·판매 여부를 뜻하지 않습니다.";
    public const string ReferenceImageNotice = "공식 메뉴 참고 이미지입니다. 당시 상품·지점·옵션이 모두 같은 사진으로 확인된 것은 아닙니다.";

    public static string Amount(decimal? value)
        => value is > 0 ? $"{value.Value.ToString("N0", CultureInfo.GetCultureInfo("ko-KR"))}원" : "미확인";

    public static string Date(DateOnly? date) => date?.ToString("yyyy.MM.dd", CultureInfo.InvariantCulture) ?? "날짜 미확인";
    public static string Quantity(int? value) => value is > 0 ? $"{value.Value}개" : "수량 미확인";
    public static string Options(OrdererRecordedMenuObservation item)
        => item.Options.Count == 0 ? "기록 없음" : string.Join(" · ", item.Options);

    public static IReadOnlyList<OrdererRecordedMenuObservation> Filter(
        IReadOnlyList<OrdererRecordedMenuObservation> items, string? branchId, string? searchText)
    {
        var query = searchText?.Trim() ?? string.Empty;
        return items.Where(item => (string.IsNullOrWhiteSpace(branchId) || item.BranchId == branchId)
                && (query.Length == 0 || item.MenuName.Contains(query, StringComparison.OrdinalIgnoreCase)
                    || item.BranchName.Contains(query, StringComparison.OrdinalIgnoreCase)
                    || item.Options.Any(option => option.Contains(query, StringComparison.OrdinalIgnoreCase))))
            .ToArray();
    }
}
