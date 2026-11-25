using RestaurantDeskApp.Services;
using RestaurantDeskApp.Models.Restaurant;

namespace Ssalddel.Tests.Clients;

public sealed partial class RestaurantReceiptRecoveryTests
{
    [Theory]
    [InlineData(null, 2)]
    [InlineData(1, 1)]
    public async Task R26_주문수락후에도선택장수분을한작업으로요청한다(int? selected, int expected)
    {
        await using var f = await PageFixture.CreateAsync();
        f.CopyStore.Value = selected;
        await f.CallAsync("AcceptAndPrintAsync");
        Assert.Equal(1, f.Desk.Acceptances);
        Assert.Equal(1, f.Printer.Calls);
        Assert.Equal(1, f.Desk.Requests);
        Assert.Equal(expected, f.DocumentService.LastDraft!.PrintLayout.ReceiptCopies);
    }

    [Theory]
    [InlineData(null, 2)]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    public async Task R26_기본과선택장수는한번의출력요청에만적용하고주문을다시수락하지않는다(int? selected, int expected)
    {
        await using var f = await PageFixture.CreateAsync();
        f.CopyStore.Value = selected;
        await f.CallAsync("PrintReceiptAsync");
        Assert.Equal(expected, f.DocumentService.LastDraft!.PrintLayout.ReceiptCopies);
        Assert.Equal(expected, f.DocumentService.LastDraft.PrintLayout.ReceiptCopyLabels.Count);
        Assert.Equal(1, f.Printer.Calls);
        Assert.Equal(1, f.Desk.Requests);
        Assert.Equal(0, f.Desk.Acceptances);
        Assert.Contains($"{expected}장분", f.Get<string>("message"));
        Assert.Contains("실제 인쇄 결과", f.Get<string>("message"));
    }

    [Fact]
    public async Task R26_최신주문조회대기중설정변경은이미시작한출력장수를바꾸지않는다()
    {
        await using var f = await PageFixture.CreateAsync();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var result = new TaskCompletionSource<음식점주문수락결과>(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Desk.Prepare = _ => { started.SetResult(); return result.Task; };
        var print = f.CallAsync("PrintReceiptAsync");
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        f.CopyStore.Value = 1;
        result.SetResult(Prepared("A"));
        await print;
        Assert.Equal(2, f.DocumentService.LastDraft!.PrintLayout.ReceiptCopies);
        Assert.Equal(1, f.Printer.Calls);
        Assert.Equal(0, f.Desk.Acceptances);
    }

    [Fact]
    public async Task R26_설정조회실패때는출력만보류하고사용자가선택한주문수락과분리한다()
    {
        await using var f = await PageFixture.CreateAsync();
        f.CopyStore.FailRead = true;
        await f.CallAsync("PrintReceiptAsync");
        await f.CallAsync("AcceptAndPrintAsync");
        Assert.Equal(0, f.Desk.Prepares);
        Assert.Equal(1, f.Desk.Acceptances);
        Assert.Equal(0, f.Printer.Calls);
        Assert.Equal(0, f.Desk.Requests);
        Assert.Contains("주문 확인은 완료", f.Get<string>("message"));
        Assert.Contains("출력만 보류", f.Get<string>("message"));
    }
}
