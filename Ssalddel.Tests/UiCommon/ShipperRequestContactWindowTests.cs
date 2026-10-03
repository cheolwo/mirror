using Ssalddel.Contracts.Shipper.Request;
using Ssalddel.Ui.Common.Areas.App.ViewModels;

namespace Ssalddel.Tests.UiCommon;

public sealed class ShipperRequestContactWindowTests
{
    [Fact]
    public void 상차시간창은_현행서버규칙대로필수이고_하차시간창과연락처는선택이다()
    {
        var state = CompleteState();
        state.상차시간창시작일시 = null;
        state.상차시간창종료일시 = null;
        Assert.False(state.서버등록가능);
        Assert.Contains(state.필수입력오류목록, item => item.단계 == ShipperRequestAuthoringStep.Transport && item.내용.Contains("상차 시간창"));
        state.상차시간창시작일시 = new(2030, 10, 3, 9, 0, 0);
        state.상차시간창종료일시 = new(2030, 10, 3, 10, 0, 0);
        Assert.True(state.서버등록가능);
        Assert.Null(state.하차시간창오류);
    }

    [Theory]
    [InlineData(true, null, 1)]
    [InlineData(true, 0, null)]
    [InlineData(true, 0, 0)]
    [InlineData(true, 1, 0)]
    [InlineData(false, null, 1)]
    [InlineData(false, 0, null)]
    [InlineData(false, 0, 0)]
    [InlineData(false, 1, 0)]
    public void 입력한시간창의누락짝과잘못된순서는_운송단계에서등록을막는다(bool pickup, int? startHour, int? endHour)
    {
        var state = CompleteState();
        var day = new DateTime(2030, 10, 3, 9, 0, 0);
        var start = startHour.HasValue ? day.AddHours(startHour.Value) : (DateTime?)null;
        var end = endHour.HasValue ? day.AddHours(endHour.Value) : (DateTime?)null;
        if (pickup) { state.상차시간창시작일시 = start; state.상차시간창종료일시 = end; }
        else { state.하차시간창시작일시 = start; state.하차시간창종료일시 = end; }
        Assert.False(state.서버등록가능);
        Assert.False(state.운송정보입력됨);
        Assert.Contains(state.필수입력오류목록, item => item.단계 == ShipperRequestAuthoringStep.Transport && item.내용.Contains(pickup ? "상차" : "하차"));
    }

    [Fact]
    public void 날짜를넘는시간창과개별담당자는_단계이동Draft왕복에서그대로남는다()
    {
        var source = CompleteState();
        source.상차시간창시작일시 = new(2030, 10, 3, 23, 30, 0);
        source.상차시간창종료일시 = new(2030, 10, 4, 1, 0, 0);
        source.하차시간창시작일시 = new(2030, 10, 4, 2, 0, 0);
        source.하차시간창종료일시 = new(2030, 10, 4, 3, 0, 0);
        source.상차연락처이름 = "상차 담당자";
        source.상차연락처전화번호 = "010-1111-2222";
        source.하차연락처이름 = "하차 담당자";
        source.하차연락처전화번호 = "010-3333-4444";
        var restored = new 운송의뢰작성ViewModel();
        restored.ApplyDraft(source.ToDraft());
        Assert.True(restored.서버등록가능);
        Assert.Equal(source.상차시간창시작일시, restored.상차시간창시작일시);
        Assert.Equal(source.상차시간창종료일시, restored.상차시간창종료일시);
        Assert.Equal(source.하차시간창시작일시, restored.하차시간창시작일시);
        Assert.Equal(source.하차시간창종료일시, restored.하차시간창종료일시);
        Assert.Equal(source.상차연락처이름, restored.상차연락처이름);
        Assert.Equal(source.상차연락처전화번호, restored.상차연락처전화번호);
        Assert.Equal(source.하차연락처이름, restored.하차연락처이름);
        Assert.Equal(source.하차연락처전화번호, restored.하차연락처전화번호);
        restored.Reset();
        Assert.Null(restored.상차시간창시작일시);
        Assert.Null(restored.상차시간창종료일시);
        Assert.Null(restored.하차시간창시작일시);
        Assert.Null(restored.하차시간창종료일시);
        Assert.Empty(restored.상차연락처전화번호);
        Assert.Empty(restored.하차연락처전화번호);
    }

    [Fact]
    public void 초기날짜값을_실제예약날짜로등록하지않는다()
    {
        var state = CompleteState();
        state.상차시간창시작일시 = DateTime.MinValue;
        Assert.False(state.서버등록가능);
        Assert.Contains("올바른 날짜", state.상차시간창오류);
    }

    internal static 운송의뢰작성ViewModel CompleteState() => new()
    {
        화물종류 = "검증 화물", 화물수량 = 1,
        상차도로명주소 = "검증 상차지", 하차도로명주소 = "검증 하차지",
        상차시간창시작일시 = new(2030, 10, 3, 9, 0, 0),
        상차시간창종료일시 = new(2030, 10, 3, 10, 0, 0),
        차량종류 = "1톤 카고", 결제수단 = "카드", 결제예정금액 = 50000
    };
}
