using System.Text.Json;
using System.Text.RegularExpressions;
using RestaurantDeskApp.Models.Restaurant;
using RestaurantDeskApp.Services;
using Ssalddel.Contracts.Food;

namespace Ssalddel.Tests.Clients;

/// <summary>표의 작은 소스 계약과 실제 Presentation 회귀입니다. Razor 픽셀·Windows 창·프린터 실행 검증이 아닙니다.</summary>
public sealed class RestaurantDesktopTableR28Tests
{
    [Fact]
    public void PC주문표는의미있는caption과여섯열을갖고_기존필터의주문만표시한다()
    {
        var table = TableMarkup();

        Assert.Matches(@"<caption\b[^>]*>\s*현재 수신함 주문\s*</caption>", table);
        var headers = Regex.Matches(table, @"<th\b[^>]*>(.*?)</th>", RegexOptions.Singleline | RegexOptions.CultureInvariant)
            .Select(x => Regex.Replace(x.Groups[1].Value, @"\s+", " ").Trim()).ToArray();
        Assert.Equal(new[] { "접수", "주문번호", "메뉴 · 수량", "주문금액", "조리예상", "처리상태" }, headers);
        Assert.Contains("<thead", table, StringComparison.Ordinal);
        Assert.Contains("<tbody", table, StringComparison.Ordinal);
        Assert.Contains("workspace.VisibleOrders(orders)", table, StringComparison.Ordinal);
        Assert.Contains("pos-order-row", table, StringComparison.Ordinal);
    }

    [Fact]
    public void 표선택은키보드native버튼과기존잠금을사용하고_행클릭이업무명령을직접실행하지않는다()
    {
        var source = Read("RestaurantDeskApp", "Components", "Pages", "OrderInbox.razor");
        var table = TableMarkup();
        var button = Regex.Matches(table, @"<button\b[\s\S]*?</button>", RegexOptions.CultureInvariant)
            .Select(x => x.Value).Single(x => x.Contains("aria-pressed", StringComparison.Ordinal));

        Assert.Contains("type=\"button\"", button, StringComparison.Ordinal);
        Assert.Contains("aria-label=", button, StringComparison.Ordinal);
        Assert.Contains("주문 ", button, StringComparison.Ordinal);
        Assert.Contains(" 보기", button, StringComparison.Ordinal);
        Assert.Contains("order.주문번호", button, StringComparison.Ordinal);
        Assert.Contains("workspace.SelectedOrderNo", button, StringComparison.Ordinal);
        Assert.Contains("disabled=", button, StringComparison.Ordinal);
        Assert.Contains("workspace.CommandInProgress", button, StringComparison.Ordinal);
        Assert.Contains("workLocked", button, StringComparison.Ordinal);
        Assert.Contains("SelectOrderAsync(order.주문번호)", table, StringComparison.Ordinal);
        Assert.Matches(@"if\s*\(\s*!workLocked\s*\)\s*workspace\.Select\s*\(\s*orderNo\s*\)", source);
        Assert.Contains("workspace.SetCommandInProgress(busy)", source, StringComparison.Ordinal);
        foreach (var command in new[] { "AcceptAndPrintAsync(", "PrintReceiptAsync(", "조리시작Async(", "픽업준비완료Async(" })
            Assert.DoesNotContain(command, table, StringComparison.Ordinal);
    }

    [Fact]
    public void 표는데스크탑에서만표시하고_좁은화면의기존주문상세링크와동일선택상세를유지한다()
    {
        var source = Read("RestaurantDeskApp", "Components", "Pages", "OrderInbox.razor");
        var css = Read("RestaurantDeskApp", "Components", "Pages", "OrderInbox.razor.css");
        var desktop = Regex.Match(css, @"@media\s*\(\s*min-width\s*:\s*1100px\s*\)", RegexOptions.CultureInvariant);
        Assert.True(desktop.Success);
        Assert.Matches(@"\.pos-table-frame\s*\{[^}]*display\s*:\s*none", css[..desktop.Index]);
        Assert.Matches(@"\.pos-table-frame\s*\{[^}]*display\s*:\s*(block|flex|grid)", css[desktop.Index..]);
        Assert.Contains("pos-mobile-select", source, StringComparison.Ordinal);
        Assert.Contains("/orders/{Uri.EscapeDataString(order.주문번호)}", source, StringComparison.Ordinal);
        Assert.Equal(1, Regex.Matches(source, @"<OrderDetail\b", RegexOptions.CultureInvariant).Count);
        Assert.Contains("OrderNo=\"@workspace.SelectedOrderNo\"", source, StringComparison.Ordinal);
        Assert.Contains("BusyChanged=\"OnDetailBusyChanged\"", source, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(RestaurantPosQueue.All)]
    [InlineData(RestaurantPosQueue.InProgress)]
    [InlineData(RestaurantPosQueue.Attention)]
    public void 표필터조회는긴한글메뉴와주문번호_금액수량과상세입력을자르거나변경하지않는다(RestaurantPosQueue queue)
    {
        var selected = LongOrder();
        var other = new 음식점주문DeskItem
        {
            주문번호 = "OTHER", 상태 = 음식점주문Desk상태코드.주문대기,
            접수시각 = selected.접수시각.AddMinutes(1), 메뉴요약 = "다른 메뉴"
        };
        var original = JsonSerializer.Serialize(selected);
        var workspace = new RestaurantPosPresentation { Queue = queue };
        Assert.True(workspace.Select(selected.주문번호));

        var visible = workspace.VisibleOrders([other, selected]);

        Assert.Contains(visible, x => ReferenceEquals(x, selected));
        Assert.Equal(original, JsonSerializer.Serialize(selected));
        Assert.Equal(selected.주문번호, workspace.SelectedOrderNo);
        Assert.Equal(123_456_789m, selected.주문금액);
        Assert.Equal(137, Assert.Single(selected.상품목록).수량);
        Assert.Equal(90, selected.선택조리예상분);
        Assert.Equal("포장 요청\n소스는 따로 담아 주세요.", selected.상세주문!.수령인정보.요청사항);
    }

    [Fact]
    public void 긴주문번호선택도명령중에는다른행으로바뀌지않고_해제후에만전환한다()
    {
        var selected = LongOrder();
        var original = JsonSerializer.Serialize(selected);
        var workspace = new RestaurantPosPresentation();
        Assert.True(workspace.Select(selected.주문번호));
        workspace.SetCommandInProgress(true);

        Assert.False(workspace.Select("NEXT-ORDER"));
        Assert.Equal(selected.주문번호, workspace.SelectedOrderNo);
        Assert.Equal(original, JsonSerializer.Serialize(selected));
        workspace.SetCommandInProgress(false);
        Assert.True(workspace.Select("NEXT-ORDER"));
        Assert.Equal("NEXT-ORDER", workspace.SelectedOrderNo);
        Assert.Equal(original, JsonSerializer.Serialize(selected));
    }

    [Fact]
    public void 표의처리상태문구는기사배정이나조리허용을새로만들지않는다()
    {
        var item = LongOrder();
        item.상태 = 음식점주문Desk상태코드.기사배정;
        item.배차상태 = 음식주문배차상태코드.기사배정;
        item.AvailableActions = [];
        var original = JsonSerializer.Serialize(item);

        Assert.Equal("조리 시작 대기", RestaurantPosPresentation.StatusLabel(item));

        Assert.False(item.조리시작가능);
        Assert.False(item.수락가능);
        Assert.False(item.픽업준비가능);
        Assert.Equal(original, JsonSerializer.Serialize(item));
    }

    private static 음식점주문DeskItem LongOrder()
    {
        var menu = string.Concat(Enumerable.Repeat("아주 긴 한글 메뉴 & 특수 <표시> · ", 45));
        return new()
        {
            주문번호 = "ORDER-" + new string('A', 180), 상태 = 음식주문상태코드.주문확인,
            접수시각 = new(2026, 10, 28, 12, 0, 0, TimeSpan.FromHours(9)), 메뉴요약 = menu,
            상품목록 = [new() { 상품명 = menu, 수량 = 137, 단가 = 9000m }],
            주문금액 = 123_456_789m, 선택조리예상분 = 90, 추천조리예상분 = 20,
            배차상태 = 음식주문배차상태코드.배차불가,
            상세주문 = new()
            {
                주문번호 = "ORDER-" + new string('A', 180), 상태 = 음식주문상태코드.주문확인,
                Revision = 15, 음식점수락시각Utc = new(2026, 10, 28, 3, 0, 0, DateTimeKind.Utc),
                수령인정보 = new() { 요청사항 = "포장 요청\n소스는 따로 담아 주세요." }
            }
        };
    }

    private static string TableMarkup()
    {
        var source = Read("RestaurantDeskApp", "Components", "Pages", "OrderInbox.razor");
        var match = Regex.Match(source, "<table\\b[^>]*class=\"[^\"]*\\bpos-order-table\\b[^\"]*\"[^>]*>[\\s\\S]*?</table>", RegexOptions.CultureInvariant);
        Assert.True(match.Success, "기존 수신함에서 데스크탑 주문표를 찾을 수 없습니다.");
        return match.Value;
    }

    private static string Read(params string[] path)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "Ssalddel.slnx")))
                return File.ReadAllText(Path.Combine([directory.FullName, .. path]));
        throw new DirectoryNotFoundException("Ssalddel 저장소 루트를 찾지 못했습니다.");
    }
}
