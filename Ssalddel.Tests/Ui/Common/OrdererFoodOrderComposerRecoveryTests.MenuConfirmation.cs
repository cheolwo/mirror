using System.Net;
using Ssalddel.Contracts.Food;
using Ssalddel.Contracts.Restaurants;
using Ssalddel.Ui.Common.Areas.App.ViewModels;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.Tests.Ui.Common;

public sealed partial class OrdererFoodOrderComposerRecoveryTests
{
    [Fact]
    public async Task NormalNewSubmissionRequiresServerPriceComparison()
    {
        var services = new FoodServices();
        using var page = await CreatePageAsync(services, new AuthenticationService { SignedIn = true });
        Assert.True(await page.주문등록Async());
        Assert.True(Assert.Single(services.Requests).메뉴가격확인필요);
    }

    [Fact]
    public async Task PriceChangeIsDisplayedBeforeExplicitConfirmationAndNeverPostsOnGet()
    {
        var services = new FoodServices { Write = (_, _) => Task.FromException<음식주문응답>(MenuFailure(FoodOrderSubmissionErrorCodes.MenuPriceChanged)) };
        using var page = await CreatePageAsync(services, new AuthenticationService { SignedIn = true });
        await page.상세.조회Async(101);
        var originalId = page.작성.클라이언트요청Id;
        Assert.False(await page.주문등록Async());
        Assert.True(page.작성.메뉴재확인필요);
        AssertDraft(page.작성, originalId);

        var latest = CreateRestaurant(); latest.메뉴목록[0].판매가 = 5_500m;
        services.ReadDetail = (_, _) => Task.FromResult<음식점공개상세응답?>(latest);
        using var workspace = new TestWorkspace(page);
        await workspace.RecheckMenusAsync();
        Assert.Single(services.Requests);
        Assert.Equal(originalId, page.작성.클라이언트요청Id);
        Assert.Equal(11_000m, page.작성.주문금액);
        Assert.True(page.작성.가격변경확인대기);
        Assert.False(await page.주문등록Async());
        Assert.Single(services.Requests);
        var html = await RenderComposerAsync(page.인증, page.작성);
        Assert.Contains("4,500원 → 5,500원", html);
        Assert.Contains("변경 금액 확인하고 주문", html);
        Assert.Equal("문 앞", page.작성.요청사항);

        services.Write = (_, _) => Task.FromResult(new 음식주문응답 { 주문번호 = "FOOD-CONFIRMED" });
        await workspace.ConfirmPriceAsync();
        Assert.Equal(2, services.Requests.Count);
        var confirmed = services.Requests[1];
        Assert.NotEqual(originalId, confirmed.클라이언트요청Id);
        Assert.Equal(5_500m, Assert.Single(confirmed.상품목록).단가);
        Assert.Equal(2, Assert.Single(confirmed.상품목록).수량);
        Assert.Equal("문 앞", confirmed.수령인정보.요청사항);
        Assert.Equal("FOOD-CONFIRMED", page.작성.등록응답!.주문번호);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task UnavailableMenusReturnToEditingWithExplicitRemovalNotice(bool soldOut)
    {
        var services = new FoodServices { Write = (_, _) => Task.FromException<음식주문응답>(MenuFailure(FoodOrderSubmissionErrorCodes.MenuUnavailable)) };
        using var page = await CreatePageAsync(services, new AuthenticationService { SignedIn = true });
        await page.상세.조회Async(101);
        Assert.False(await page.주문등록Async());
        var latest = CreateRestaurant();
        if (soldOut) latest.메뉴목록[0].품절여부 = true; else latest.메뉴목록 = [];
        services.ReadDetail = (_, _) => Task.FromResult<음식점공개상세응답?>(latest);
        using var workspace = new TestWorkspace(page);
        await workspace.RecheckMenusAsync();
        Assert.False(page.작성.메뉴재확인필요);
        Assert.False(page.작성.메뉴선택됨);
        Assert.Contains("합성 메뉴", page.작성.메뉴확인안내);
        Assert.Contains("선택에서 제외", page.작성.메뉴확인안내);
        Assert.Equal("합성 배달 주소", page.작성.주소);
        Assert.Equal("문 앞", page.작성.요청사항);
        Assert.Single(services.Requests);
    }

    [Fact]
    public async Task MenuRefreshFailureAndRetryPreserveSelectedQuantityAndRecipient()
    {
        var services = new FoodServices { Write = (_, _) => Task.FromException<음식주문응답>(MenuFailure(FoodOrderSubmissionErrorCodes.MenuPriceChanged)) };
        using var page = await CreatePageAsync(services, new AuthenticationService { SignedIn = true });
        await page.상세.조회Async(101);
        var original = page.작성.클라이언트요청Id;
        Assert.False(await page.주문등록Async());
        services.ReadDetail = (_, _) => Task.FromException<음식점공개상세응답?>(new HttpRequestException("메뉴 연결 끊김"));
        using var workspace = new TestWorkspace(page);
        await workspace.RecheckMenusAsync();
        Assert.True(page.작성.메뉴재확인필요);
        Assert.False(page.작성.메뉴확인중);
        AssertDraft(page.작성, original);
        services.ReadDetail = (_, _) => Task.FromResult<음식점공개상세응답?>(CreateRestaurant());
        await workspace.SelectAsync(101);
        AssertDraft(page.작성, original);
        Assert.False(page.작성.메뉴재확인필요);
        Assert.Single(services.Requests);
    }

    [Fact]
    public async Task LateMenuRefreshCannotApplyAfterSwitchingRestaurant()
    {
        var services = new FoodServices { Write = (_, _) => Task.FromException<음식주문응답>(MenuFailure(FoodOrderSubmissionErrorCodes.MenuPriceChanged)) };
        using var page = await CreatePageAsync(services, new AuthenticationService { SignedIn = true });
        await page.상세.조회Async(101);
        Assert.False(await page.주문등록Async());
        var delayed = new TaskCompletionSource<음식점공개상세응답?>(TaskCreationOptions.RunContinuationsAsynchronously);
        services.ReadDetail = (id, _) => id == 101 ? delayed.Task : Task.FromResult<음식점공개상세응답?>(CreateRestaurant(id));
        using var workspace = new TestWorkspace(page);
        var refresh = workspace.RecheckMenusAsync();
        await workspace.SelectAsync(202);
        var latestOld = CreateRestaurant(); latestOld.메뉴목록[0].판매가 = 9_000m;
        delayed.SetResult(latestOld);
        await refresh;
        Assert.Equal(202, page.상세.요청RestaurantId);
        Assert.False(page.작성.가격변경확인대기);
        Assert.False(page.작성.메뉴재확인필요);
        Assert.Empty(page.작성.선택항목목록);
        Assert.Single(services.Requests);
    }

    private static SsalddelApiException MenuFailure(string code)
        => new("메뉴 확인 필요", (int)HttpStatusCode.BadRequest, "주문", "{}", null, errorCode: code);

    [Theory]
    [InlineData(FoodOrderSubmissionErrorCodes.MenuPriceChanged)]
    [InlineData(FoodOrderSubmissionErrorCodes.MenuUnavailable)]
    public async Task ExplicitPendingResubmissionMenuRejectionReturnsToMenuConfirmation(string code)
    {
        var services = new FoodServices();
        services.Write = (_, _) => Task.FromException<음식주문응답>(services.Requests.Count == 1
            ? new HttpRequestException("응답 유실") : MenuFailure(code));
        var store = new MenuPendingStore();
        using var recovery = new FoodOrderSubmissionRecoveryViewModel(store, new MissingSubmissionReader(), services);
        using var page = await CreatePageAsync(services, new AuthenticationService(), recovery);
        await page.상세.조회Async(101);
        var originalId = page.작성.클라이언트요청Id;
        Assert.False(await page.주문등록Async());
        Assert.True(page.작성.입력잠금);
        Assert.NotNull(store.Pending);
        await page.접수결과재확인Async(false);
        Assert.True(recovery.미접수확인됨);
        await page.접수결과재확인Async(true);
        Assert.Equal(2, services.Requests.Count);
        Assert.All(services.Requests, x => Assert.Equal(originalId, x.클라이언트요청Id));
        Assert.Null(store.Pending);
        Assert.False(page.작성.입력잠금);
        Assert.True(page.작성.메뉴재확인필요);
        Assert.False(await page.주문등록Async());
        Assert.Equal(2, services.Requests.Count);
        AssertDraft(page.작성, originalId);
        var html = await RenderComposerAsync(page.인증, page.작성);
        Assert.Contains("최신 메뉴 다시 확인", html);
        using var workspace = new TestWorkspace(page);
        await workspace.RecheckMenusAsync();
        Assert.False(page.작성.메뉴재확인필요);
        Assert.False(recovery.오류발생);
        Assert.Equal(2, services.Requests.Count);
    }

    [Fact]
    public async Task InitialMenuRejectionClearsPersistentPendingWithoutLosingDraft()
    {
        var services = new FoodServices { Write = (_, _) => Task.FromException<음식주문응답>(MenuFailure(FoodOrderSubmissionErrorCodes.MenuPriceChanged)) };
        var store = new MenuPendingStore();
        using var recovery = new FoodOrderSubmissionRecoveryViewModel(store, new MissingSubmissionReader(), services);
        using var page = await CreatePageAsync(services, new AuthenticationService(), recovery);
        var original = page.작성.클라이언트요청Id;
        Assert.False(await page.주문등록Async());
        Assert.Null(store.Pending);
        Assert.Null(recovery.Pending);
        Assert.False(page.작성.입력잠금);
        Assert.True(page.작성.메뉴재확인필요);
        AssertDraft(page.작성, original);
    }

    [Fact]
    public async Task ConfirmedInputRejectionAfterPendingRetryShowsCorrectionInDetailWithoutLockingDraft()
    {
        const string message = "수령인 연락처를 확인해 주세요.";
        var services = new FoodServices();
        services.Write = (_, _) => Task.FromException<음식주문응답>(services.Requests.Count == 1
            ? new HttpRequestException("응답 유실")
            : new SsalddelApiException(message, 400, "주문", "{}", null, errorCode: FoodOrderSubmissionErrorCodes.InputInvalid));
        var store = new MenuPendingStore();
        using var recovery = new FoodOrderSubmissionRecoveryViewModel(store, new MissingSubmissionReader(), services);
        using var page = await CreatePageAsync(services, new AuthenticationService(), recovery);
        var original = page.작성.클라이언트요청Id;
        Assert.False(await page.주문등록Async());
        await page.접수결과재확인Async(false);
        await page.접수결과재확인Async(true);
        Assert.Null(store.Pending);
        Assert.False(page.작성.입력잠금);
        Assert.False(page.작성.메뉴재확인필요);
        AssertDraft(page.작성, original);
        await WithRenderedWorkspaceAsync(page, html =>
        {
            Assert.Contains(message, html());
            Assert.Contains("수령인 이름", html());
            Assert.DoesNotContain("같은 주문의 결과를 다시 확인", html());
            return Task.CompletedTask;
        });
        Assert.Equal(2, services.Requests.Count);
    }

    private sealed class MissingSubmissionReader : I주문자음식주문접수결과Service
    {
        public Task<음식주문접수결과응답?> 접수결과Async(Guid requestId, CancellationToken cancellationToken = default)
            => Task.FromResult<음식주문접수결과응답?>(null);
    }

    private sealed class MenuPendingStore : IFoodOrderPendingSubmissionStore
    {
        public FoodOrderPendingSubmission? Pending { get; private set; }
        public Task<FoodOrderPendingSubmission?> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(Pending);
        public Task SaveAsync(FoodOrderPendingSubmission snapshot, CancellationToken cancellationToken = default)
        { Pending = snapshot; return Task.CompletedTask; }
        public Task ClearAsync(Guid requestId, CancellationToken cancellationToken = default)
        { if (Pending?.Request.클라이언트요청Id == requestId) Pending = null; return Task.CompletedTask; }
    }
}
