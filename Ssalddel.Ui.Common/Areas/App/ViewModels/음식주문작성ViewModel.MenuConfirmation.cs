using CommunityToolkit.Mvvm.ComponentModel;
using Ssalddel.Contracts.Food;
using Ssalddel.Contracts.Restaurants;

namespace Ssalddel.Ui.Common.Areas.App.ViewModels;

public sealed record 음식주문가격변경항목(string 메뉴명, decimal 이전단가, decimal 현재단가);

public sealed partial class 음식주문작성ViewModel
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(제출가능), nameof(가격확인후제출가능))]
    public partial bool 메뉴재확인필요 { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(제출가능), nameof(가격확인후제출가능))]
    public partial bool 메뉴확인중 { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(제출가능))]
    public partial bool 가격변경확인대기 { get; private set; }

    [ObservableProperty]
    public partial string? 메뉴확인안내 { get; private set; }

    [ObservableProperty]
    public partial IReadOnlyList<음식주문가격변경항목> 가격변경목록 { get; private set; } = [];

    private void 메뉴변경거절처리()
    {
        메뉴재확인필요 = true;
        가격변경확인대기 = false;
        가격변경목록 = [];
        메뉴확인안내 = "메뉴나 가격이 변경됐습니다. 최신 메뉴를 확인한 뒤 주문을 계속해 주세요.";
    }

    public void 메뉴확인거절반영(Api작업오류? error)
    {
        if (error is { Http상태코드: 400, 코드: FoodOrderSubmissionErrorCodes.MenuPriceChanged or FoodOrderSubmissionErrorCodes.MenuUnavailable })
            메뉴변경거절처리();
    }

    public bool 메뉴확인시작()
    {
        if (입력잠금 || _등록진행 || 메뉴확인중 || _음식점 is null) return false;
        메뉴확인중 = true;
        return true;
    }

    public void 메뉴확인취소() => 메뉴확인중 = false;

    public void 메뉴확인실패(string? message)
    {
        메뉴확인중 = false;
        메뉴재확인필요 = true;
        메뉴확인안내 = message ?? "최신 메뉴를 확인하지 못했습니다. 다시 확인해 주세요.";
    }

    public void 최신메뉴확인적용(음식점공개상세응답 detail)
    {
        if (입력잠금 || _등록진행 || _음식점?.음식점.Id != detail.음식점.Id) return;
        var selected = 선택항목목록.ToArray();
        var available = detail.메뉴목록.Where(x => !x.품절여부).ToDictionary(x => x.Id);
        var unavailable = selected.Where(x => !available.ContainsKey(x.메뉴Id)).ToArray();
        var changed = selected.Where(x => available.TryGetValue(x.메뉴Id, out var current) && current.판매가 != x.단가)
            .Select(x => new 음식주문가격변경항목(x.메뉴명, x.단가, available[x.메뉴Id].판매가)).ToArray();
        foreach (var item in unavailable) _수량목록.Remove(item.메뉴Id);
        _음식점 = detail;
        메뉴확인중 = false;
        메뉴재확인필요 = false;
        가격변경목록 = changed;
        가격변경확인대기 = changed.Length > 0;
        메뉴확인안내 = unavailable.Length > 0
            ? $"현재 주문할 수 없어 선택에서 제외한 메뉴: {string.Join(", ", unavailable.Select(x => x.메뉴명))}. 다른 메뉴를 선택해 주세요."
            : changed.Length > 0 ? "변경된 메뉴 가격과 주문 금액을 확인해 주세요." : "최신 메뉴를 확인했습니다. 주문 내용을 확인하고 제출해 주세요.";
        작업상태초기화();
        NotifyOrderChanged();
    }

    // 이 메서드는 변경 가격을 확인하는 명시 버튼에서만 호출합니다. GET 재조회는 제출하지 않습니다.
    public bool 변경금액확인()
    {
        if (!가격변경확인대기 || !가격확인후제출가능) return false;
        가격변경확인대기 = false;
        요청내용변경됨();
        return true;
    }

    private void 메뉴확인상태초기화()
    {
        메뉴확인중 = false;
        메뉴재확인필요 = false;
        가격변경확인대기 = false;
        메뉴확인안내 = null;
        가격변경목록 = [];
    }
}
