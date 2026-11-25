using RestaurantDeskApp.Models.Restaurant;

namespace Ssalddel.Tests.Clients;

public sealed partial class RestaurantReceiptRecoveryTests
{
    [Theory]
    [InlineData(null, 2)]
    [InlineData(1, 1)]
    public async Task R28_명시수락의기본프린터출력은한작업에설정장수분만보내고창안내와구분한다(int? copies, int expected)
    {
        await using var f = await PageFixture.CreateAsync();
        f.Printer.Automatic = true;
        f.CopyStore.Value = copies;
        await f.CallAsync("AcceptAndPrintAsync");
        Assert.Equal(1, f.Desk.Acceptances);
        Assert.Equal(1, f.Printer.Calls);
        Assert.Equal(1, f.Desk.Requests);
        Assert.Equal(expected, f.DocumentService.LastDraft!.PrintLayout.ReceiptCopies);
        Assert.Contains("기본 프린터", f.Get<string>("message"));
        Assert.Contains("실제 인쇄 결과", f.Get<string>("message"));
        Assert.DoesNotContain("인쇄 창", f.Get<string>("message"));
    }

    [Fact]
    public async Task R28_기본프린터실패는수락을취소하거나자동재전송하지않고명시재출력만허용한다()
    {
        await using var f = await PageFixture.CreateAsync();
        f.Printer.Automatic = true;
        f.Printer.FailNext = true;
        await f.CallAsync("AcceptAndPrintAsync");
        Assert.Equal(1, f.Desk.Acceptances);
        Assert.Equal(1, f.Printer.Calls);
        Assert.Equal(0, f.Desk.Requests);
        Assert.NotNull(f.Get<음식점주문DeskItem?>("order")?.상세주문?.음식점수락시각Utc);
        Assert.Contains("출력 장치를 확인", f.Get<string>("message"));
        await f.CallAsync("PrintReceiptAsync");
        Assert.Equal(1, f.Desk.Acceptances);
        Assert.Equal(2, f.Printer.Calls);
        Assert.Equal(1, f.Desk.Requests);
        Assert.DoesNotContain("인쇄 창", f.Get<string>("message"));
    }

    [Fact]
    public async Task R28_기본프린터모드에서도서버수락복구는자동출력하지않는다()
    {
        await using var f = await PageFixture.CreateAsync();
        f.Printer.Automatic = true;
        f.Desk.Accept = _ => Task.FromResult(RecoveredForR27(unexpectedDraft: true));
        await f.CallAsync("AcceptAndPrintAsync");
        Assert.True(f.Get<음식점주문DeskItem?>("order")?.상세주문?.음식점수락시각Utc.HasValue);
        Assert.Equal(0, f.Printer.Calls);
        Assert.Equal(0, f.Desk.Requests);
        Assert.Null(f.DocumentService.LastDraft);
    }
}
