using MudBlazor;
using RestaurantDeskApp.Models.Restaurant;
using Ssalddel.Contracts.Food;

namespace Ssalddel.Tests.Clients;

/// <summary>실제 상세 처리 코드와 기존 페이지 대역을 사용한다. 실제 Razor·서버·기기·프린터 완료 증거가 아니다.</summary>
public sealed partial class RestaurantReceiptRecoveryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task R27_기존수락복구는인쇄하지않고사용자가전표출력을선택해야한번요청한다(bool unexpectedDraft)
    {
        await using var f = await PageFixture.CreateAsync();
        f.CopyStore.FailRead = unexpectedDraft;
        f.Desk.Accept = _ => Task.FromResult(RecoveredForR27(unexpectedDraft));

        await f.CallAsync("AcceptAndPrintAsync");

        Assert.Equal(1, f.Desk.Acceptances);
        AssertNoR27Print(f);
        Assert.False(f.Get<bool>("readFailed"));
        Assert.Equal(Severity.Info, f.Get<Severity>("messageSeverity"));
        Assert.Contains("복구", f.Get<string>("message"));
        Assert.Contains("전표 출력", f.Get<string>("message"));
        Assert.Equal(30, f.Get<int>("preparationMinutes"));
        var restored = f.Get<음식점주문DeskItem?>("order");
        Assert.NotNull(restored); Assert.Equal("A", restored.주문번호); Assert.Equal("기사배정", restored.상태);
        Assert.NotNull(restored.상세주문?.음식점수락시각Utc);

        // 복구 때 설정이 확인되지 않았어도 수락 상태는 복원한다. 인쇄는 별도 명시 행동이다.
        f.CopyStore.FailRead = false;
        await f.CallAsync("PrintReceiptAsync");

        Assert.Equal(1, f.Desk.Acceptances);
        Assert.Equal(1, f.Desk.Prepares); Assert.Equal(1, f.Printer.Calls); Assert.Equal(1, f.Desk.Requests);
        Assert.Equal(2, f.DocumentService.LastDraft!.PrintLayout.ReceiptCopies);
        Assert.Contains("실제 인쇄 결과", f.Get<string>("message"));
    }

    [Theory]
    [InlineData("wrong-order")]
    [InlineData("missing-acceptance-time")]
    [InlineData("cancelled")]
    public async Task R27_확인되지않은복구결과를성공이나인쇄로바꾸지않는다(string invalid)
    {
        await using var f = await PageFixture.CreateAsync();
        var result = RecoveredForR27(unexpectedDraft: true);
        var detail = result.주문!.상세주문!;
        if (invalid == "wrong-order") detail.주문번호 = "another-order";
        if (invalid == "missing-acceptance-time") detail.음식점수락시각Utc = null;
        if (invalid == "cancelled") { detail.상태 = 음식주문상태코드.취소; result.주문.상태 = 음식주문상태코드.취소; }
        f.Desk.Accept = _ => Task.FromResult(result);

        await f.CallAsync("AcceptAndPrintAsync");

        Assert.Equal(1, f.Desk.Acceptances); AssertNoR27Print(f);
        Assert.True(f.Get<bool>("readFailed"));
        Assert.NotEqual(Severity.Success, f.Get<Severity>("messageSeverity"));
        Assert.Contains("확인", f.Get<string>("message"));
        Assert.DoesNotContain("복구했습니다", f.Get<string>("message"));
    }

    [Theory]
    [InlineData("selection")]
    [InlineData("account")]
    [InlineData("disposed")]
    public async Task R27_수락복구의늦은응답은대상이나계정이바뀌거나화면이끝나면출력하지않는다(string transition)
    {
        await using var f = await PageFixture.CreateAsync();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var response = new TaskCompletionSource<음식점주문수락결과>(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Desk.Accept = _ => { entered.TrySetResult(); return response.Task; };
        var command = f.CallAsync("AcceptAndPrintAsync");
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        if (transition == "selection") await f.MountAsync("B");
        else if (transition == "account") await f.Session.ApplyAsync(Token("another-owner"));
        else f.Page.Dispose();
        response.SetResult(RecoveredForR27(unexpectedDraft: true));
        await command.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(1, f.Desk.Acceptances); AssertNoR27Print(f);
        if (transition == "selection") Assert.Equal("B", f.Get<음식점주문DeskItem?>("order")?.주문번호);
        Assert.DoesNotContain("복구했습니다", f.Get<string?>("message") ?? string.Empty);
    }

    [Fact]
    public async Task R27_수락조회실패후명시새로고침은서버상태만복원하고자동재수락이나출력을하지않는다()
    {
        await using var f = await PageFixture.CreateAsync();
        var pending = f.Get<음식점주문DeskItem>("order");
        pending.상태 = 음식주문상태코드.주문대기;
        pending.상세주문!.상태 = 음식주문상태코드.주문대기;
        pending.상세주문.음식점수락시각Utc = null;
        f.Desk.Accept = _ => throw new HttpRequestException("controlled state check unavailable");

        await f.CallAsync("AcceptAndPrintAsync");

        Assert.True(f.Get<bool>("readFailed")); Assert.Equal(1, f.Desk.Acceptances); AssertNoR27Print(f);
        Assert.DoesNotContain("controlled", f.Get<string>("message"));
        var readsBefore = f.Desk.Reads;

        // 기존 fixture의 GET 정본은 수락 시각이 확인된 주문이다. 새로고침은 POST를 호출하지 않는다.
        await f.CallAsync("RefreshAsync");

        Assert.True(f.Desk.Reads > readsBefore);
        Assert.False(f.Get<bool>("readFailed"));
        Assert.NotNull(f.Get<음식점주문DeskItem?>("order")?.상세주문?.음식점수락시각Utc);
        Assert.Equal(1, f.Desk.Acceptances); AssertNoR27Print(f);
    }

    private static 음식점주문수락결과 RecoveredForR27(bool unexpectedDraft)
    {
        var detail = Detail(); detail.조리예상분 = 30;
        var item = new 음식점주문DeskItem
        {
            주문번호 = "A", 상태 = detail.상태, 상세주문 = detail,
            추천조리예상분 = 30, 선택조리예상분 = 30
        };
        return new()
        {
            성공 = true, 수락복구됨 = true, 주문 = item, 상세주문 = detail,
            전표Draft = unexpectedDraft ? new RestaurantDeskApp.Services.음식점전표DraftFactory().Create주문전표Draft(detail) : null
        };
    }

    private static void AssertNoR27Print(PageFixture fixture)
    {
        Assert.Equal(0, fixture.Desk.Prepares); Assert.Equal(0, fixture.Printer.Calls);
        Assert.Equal(0, fixture.Desk.Requests); Assert.Null(fixture.DocumentService.LastDraft);
    }
}
