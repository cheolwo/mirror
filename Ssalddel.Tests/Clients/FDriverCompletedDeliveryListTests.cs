using System.Net;
using FDriverApp.PageModels;
using FDriverApp.Services;
using Ssalddel.Client.Infrastructure.Security;
using Ssalddel.Contracts.Food;

namespace Ssalddel.Tests.Clients;

public sealed class FDriverCompletedDeliveryListTests
{
    private static readonly DateOnly Day = new(2026, 10, 4);

    [Theory]
    [InlineData(0, 1, 0)]
    [InlineData(1, 1, 1)]
    [InlineData(20, 1, 20)]
    [InlineData(21, 2, 20)]
    [InlineData(55, 3, 20)]
    public async Task FullDayFinancialRows_ArePagedTwentyAtATime_WithoutFetchingPrivateDetails(int count, int pages, int visible)
    {
        var (model, api, _, _) = Create();
        var calls = 0;
        api.DailySettlement = (date, _) => { calls++; return Task.FromResult(Daily(date, count)); };
        api.CompletedDetail = (_, _) => throw new InvalidOperationException("Private detail must not be prefetched.");
        await model.ActivateAsync();
        Assert.Equal(1, calls);
        Assert.Equal(visible, model.Items.Count);
        Assert.Equal(pages, model.PageCount);
        Assert.Equal(1, model.PageNumber);
        Assert.Equal($"완료 {count:N0}건", model.TotalCountText);
        Assert.Equal(count == 0, model.HasNoItems);
        Assert.False(model.HasPreviousPage);
        Assert.Equal(pages > 1, model.HasNextPage);
        if (pages > 1)
        {
            model.NextPageCommand.Execute(null);
            Assert.Equal(2, model.PageNumber);
            Assert.True(model.HasPreviousPage);
            Assert.True(model.Items.Count <= 20);
            Assert.Equal(1, calls);
        }
    }

    [Fact]
    public async Task RowsSortByCompletionNewestFirst_ThenStableSettlementId()
    {
        var (model, api, _, _) = Create();
        var daily = Daily(Day, 3);
        daily.OrderSettlements[0].CompletedAtUtc = daily.OrderSettlements[1].CompletedAtUtc;
        daily.OrderSettlements = daily.OrderSettlements.Reverse().ToArray();
        api.DailySettlement = (_, _) => Task.FromResult(daily);
        await model.ActivateAsync();
        Assert.Equal(["settlement-002", "settlement-000", "settlement-001"], model.Items.Select(x => x.SettlementId).ToArray());
    }

    [Fact]
    public async Task DetailNavigationPassesOnlySelectedIdentity_AndReturnPreservesDatePageAndFinancialCache()
    {
        var (model, api, _, navigator) = Create();
        var calls = 0;
        api.DailySettlement = (date, _) => { calls++; return Task.FromResult(Daily(date, 55)); };
        await model.ActivateAsync();
        model.NextPageCommand.Execute(null);
        var selected = model.Items[3];
        var cachedRow = model.Items[0];
        await selected.OpenDetailsCommand!.ExecuteAsync(null);
        Assert.Equal(new FDriverCompletedDeliveryNavigationTarget(selected.SettlementId, Day,
            selected.Display.OrderNo, selected.DeliveryAttemptId), Assert.Single(navigator.Details));
        model.Deactivate();
        await model.ActivateAsync();
        Assert.Equal(1, calls);
        Assert.Equal(2, model.PageNumber);
        Assert.Equal(Day, DateOnly.FromDateTime(model.SelectedDate));
        Assert.Same(cachedRow, model.Items[0]);
    }

    [Fact]
    public async Task ChangingDateResetsToPageOne_WhileSettingSameDatePreservesPage()
    {
        var (model, api, _, _) = Create();
        var calls = 0;
        api.DailySettlement = (date, _) => { calls++; return Task.FromResult(Daily(date, 55)); };
        await model.ActivateAsync();
        model.NextPageCommand.Execute(null);
        model.SetDate(Day);
        Assert.Equal(2, model.PageNumber);
        Assert.Equal(1, calls);
        model.SetDate(Day.AddDays(-1));
        Assert.Equal(1, model.PageNumber);
        Assert.Equal(2, calls);
        Assert.True(model.IsLoaded);
    }

    [Fact]
    public async Task RefreshPreservesPage_AndClampsItWhenServerDayShrinks()
    {
        var (model, api, _, _) = Create();
        api.DailySettlement = (date, _) => Task.FromResult(Daily(date, 55));
        await model.ActivateAsync();
        model.NextPageCommand.Execute(null);
        model.NextPageCommand.Execute(null);
        Assert.Equal(3, model.PageNumber);
        await model.RefreshAsync();
        Assert.Equal(3, model.PageNumber);
        api.DailySettlement = (date, _) => Task.FromResult(Daily(date, 22));
        await model.RefreshAsync();
        Assert.Equal(2, model.PageNumber);
        Assert.Equal(2, model.PageCount);
        Assert.Equal(2, model.Items.Count);
        Assert.False(model.HasNextPage);
        model.PreviousPageCommand.Execute(null);
        Assert.Equal(1, model.PageNumber);
        Assert.Equal(20, model.Items.Count);
    }

    [Fact]
    public async Task DisplayedGrossUsesServerWholeDayValue_AndUnknownIsNotZero()
    {
        var (model, api, _, _) = Create();
        var daily = Daily(Day, 55);
        daily.GrossAmountTotal = 987654m;
        api.DailySettlement = (_, _) => Task.FromResult(daily);
        await model.ActivateAsync();
        Assert.Equal("배달료 합계 987,654원", model.GrossTotalText);
        daily.GrossAmountTotal = null;
        daily.KnownGrossAmountTotal = 4200m;
        await model.RefreshAsync();
        Assert.Equal("확인된 배달료 4,200원 · 전체 합계 확인 중", model.GrossTotalText);
    }

    [Theory]
    [InlineData("Owner")]
    [InlineData("Date")]
    [InlineData("PartialDay")]
    [InlineData("Count")]
    [InlineData("RowOwner")]
    [InlineData("DuplicateId")]
    [InlineData("MissingId")]
    [InlineData("OtherDayCompletion")]
    [InlineData("MissingAttempt")]
    public async Task InvalidWholeDayResponses_AreRejectedInsteadOfBecomingNormalRows(string scenario)
    {
        var (model, api, _, _) = Create();
        var daily = Daily(Day, 2);
        switch (scenario)
        {
            case "Owner": daily.DriverId = "another-driver"; break;
            case "Date": daily.CompletionDateKst = Day.AddDays(-1); break;
            case "PartialDay": daily.IsFullDayQuery = false; break;
            case "Count": daily.CompletedOrderCount++; break;
            case "RowOwner": daily.OrderSettlements[1].DriverId = "another-driver"; break;
            case "DuplicateId": daily.OrderSettlements[1].SettlementId = daily.OrderSettlements[0].SettlementId; break;
            case "MissingId": daily.OrderSettlements[0].SettlementId = ""; break;
            case "OtherDayCompletion": daily.OrderSettlements[0].CompletedAtUtc = daily.OrderSettlements[0].CompletedAtUtc.AddDays(-1); break;
            case "MissingAttempt": daily.OrderSettlements[0].DeliveryAttemptId = ""; break;
        }
        api.DailySettlement = (_, _) => Task.FromResult(daily);
        await model.ActivateAsync();
        Assert.Empty(model.Items);
        Assert.False(model.IsLoaded);
        Assert.False(model.HasNoItems);
        Assert.Contains("조회하지 못했습니다", model.StatusText);
    }

    [Fact]
    public async Task QueryFailureKeepsTrustedFinancialCacheAndPage_WithExplicitStaleNotice()
    {
        var (model, api, _, _) = Create();
        api.DailySettlement = (date, _) => Task.FromResult(Daily(date, 21));
        await model.ActivateAsync();
        model.NextPageCommand.Execute(null);
        var cached = model.Items[0];
        api.DailySettlement = (_, _) => throw new FDriverApiException("private-error-payload", HttpStatusCode.ServiceUnavailable);
        await model.RefreshAsync();
        Assert.Same(cached, model.Items[0]);
        Assert.Equal(2, model.PageNumber);
        Assert.Contains("이전 조회 결과", model.StatusText);
        Assert.DoesNotContain("private-error-payload", model.StatusText);
    }

    [Theory]
    [InlineData("Deactivate")]
    [InlineData("Pause")]
    public async Task HiddenPageCancelsRequest_AndDiscardsLateResponse(string lifecycle)
    {
        var (model, api, _, _) = Create();
        var response = new TaskCompletionSource<FoodDeliveryDailySettlementDto>();
        CancellationToken token = default;
        api.DailySettlement = (_, requestToken) => { token = requestToken; return response.Task; };
        var pending = model.ActivateAsync();
        Assert.True(model.IsLoading);
        if (lifecycle == "Deactivate") model.Deactivate(); else model.Pause();
        Assert.True(token.IsCancellationRequested);
        response.SetResult(Daily(Day, 1));
        await pending;
        Assert.Empty(model.Items);
        Assert.False(model.IsLoaded);
        Assert.False(model.IsLoading);
    }

    [Fact]
    public async Task DateChangeDiscardsOlderResponse_AndNewDayWins()
    {
        var (model, api, _, _) = Create();
        var response = new TaskCompletionSource<FoodDeliveryDailySettlementDto>();
        api.DailySettlement = (date, _) => date == Day ? response.Task : Task.FromResult(Daily(date, 2));
        var pending = model.ActivateAsync();
        model.SetDate(Day.AddDays(-1));
        Assert.Equal(2, model.Items.Count);
        response.SetResult(Daily(Day, 10));
        await pending;
        Assert.Equal(2, model.Items.Count);
        Assert.Equal(Day.AddDays(-1), DateOnly.FromDateTime(model.SelectedDate));
    }

    [Fact]
    public async Task SameUserTokenRefreshPreservesCache_AccountSwitchClearsItAndRejectsOldCommands()
    {
        var (model, api, session, navigator) = Create();
        api.DailySettlement = (date, _) => Task.FromResult(Daily(date, 21));
        await model.ActivateAsync();
        model.NextPageCommand.Execute(null);
        var cached = model.Items[0];
        await session.ApplyAsync(Token("test-driver"));
        Assert.Same(cached, model.Items[0]);
        Assert.Equal(2, model.PageNumber);
        await session.ApplyAsync(Token("another-driver"));
        Assert.Empty(model.Items);
        Assert.False(model.IsLoaded);
        Assert.Equal(1, model.PageNumber);
        await cached.OpenDetailsCommand!.ExecuteAsync(null);
        Assert.Empty(navigator.Details);
        api.DailySettlement = (date, _) => Task.FromResult(Daily(date, 1, "another-driver"));
        await model.RefreshAsync();
        Assert.Single(model.Items);
        Assert.Equal("another-driver", model.Items[0].DriverId);
    }

    [Fact]
    public async Task AccountSwitchCancelsQuery_AndDoesNotApplyOldOwnerResponse()
    {
        var (model, api, session, _) = Create();
        var response = new TaskCompletionSource<FoodDeliveryDailySettlementDto>();
        CancellationToken token = default;
        api.DailySettlement = (_, requestToken) => { token = requestToken; return response.Task; };
        var pending = model.ActivateAsync();
        await session.ApplyAsync(Token("another-driver"));
        Assert.True(token.IsCancellationRequested);
        response.SetResult(Daily(Day, 10));
        await pending;
        Assert.Empty(model.Items);
        Assert.False(model.IsLoaded);
    }

    [Theory]
    [InlineData("Clear")]
    [InlineData("RemoveDriverRole")]
    public async Task AuthenticationLossClearsCachedRowsImmediately_WithoutApiRefresh(string scenario)
    {
        var (model, api, session, _) = Create();
        api.DailySettlement = (date, _) => Task.FromResult(Daily(date, 21));
        await model.ActivateAsync();
        if (scenario == "Clear") await session.ClearAsync();
        else await session.ApplyAsync(Token("test-driver") with { Roles = [] });
        Assert.Empty(model.Items);
        Assert.True(model.IsAuthenticationRequired);
        Assert.False(model.IsLoaded);
    }

    [Fact]
    public async Task UnauthorizedRefreshClearsRowsAndSession_AndRequiresLogin()
    {
        var (model, api, session, _) = Create();
        api.DailySettlement = (date, _) => Task.FromResult(Daily(date, 21));
        await model.ActivateAsync();
        api.DailySettlement = (_, _) => throw new FDriverApiException("Unauthorized", HttpStatusCode.Unauthorized);
        await model.RefreshAsync();
        Assert.Empty(model.Items);
        Assert.False(model.IsLoaded);
        Assert.True(model.IsAuthenticationRequired);
        Assert.Equal(1, session.ClearCount);
        Assert.False(model.IsLoading);
    }

    [Fact]
    public async Task PagingCannotOpenStaleRowsFromAnotherPage_AndBoundsAreClamped()
    {
        var (model, api, _, navigator) = Create();
        api.DailySettlement = (date, _) => Task.FromResult(Daily(date, 21));
        await model.ActivateAsync();
        var firstPageRow = model.Items[0];
        model.PageNumber = 999;
        Assert.Equal(2, model.PageNumber);
        await firstPageRow.OpenDetailsCommand!.ExecuteAsync(null);
        Assert.Empty(navigator.Details);
        model.PageNumber = 0;
        Assert.Equal(1, model.PageNumber);
    }

    [Fact]
    public async Task PauseAndResumeOfLoadedListPreservesPage_WithoutImplicitRefresh()
    {
        var (model, api, _, _) = Create();
        var calls = 0;
        api.DailySettlement = (date, _) => { calls++; return Task.FromResult(Daily(date, 55)); };
        await model.ActivateAsync();
        model.NextPageCommand.Execute(null);
        model.Pause();
        await model.ResumeAsync();
        Assert.Equal(1, calls);
        Assert.Equal(2, model.PageNumber);
        Assert.Equal(20, model.Items.Count);
    }

    [Fact]
    public async Task DeactivatedListDoesNotReceiveSessionEvents_ReturnChecksNewOwnerBeforeUsingCache()
    {
        var (model, api, session, _) = Create();
        api.DailySettlement = (date, _) => Task.FromResult(Daily(date, 21));
        await model.ActivateAsync();
        model.NextPageCommand.Execute(null);
        var oldRow = Assert.Single(model.Items);
        model.Deactivate();
        await session.ApplyAsync(Token("another-driver"));
        // 화면을 떠난 금융 cache는 보존하되 세션 이벤트가 이전 화면을 계속 붙잡거나 변경하지 않습니다.
        Assert.Same(oldRow, Assert.Single(model.Items));
        Assert.Equal(2, model.PageNumber);
        api.DailySettlement = (date, _) => Task.FromResult(Daily(date, 3, "another-driver"));
        await model.ActivateAsync();
        Assert.Equal(3, model.Items.Count);
        Assert.Equal(1, model.PageNumber);
        Assert.All(model.Items, row => Assert.Equal("another-driver", row.DriverId));
        model.Deactivate();
    }

    [Theory]
    [InlineData("Clear")]
    [InlineData("RemoveDriverRole")]
    public async Task AuthChangesWhileOffscreenAreCheckedOnActivate_WithoutShowingOldRows(string scenario)
    {
        var (model, api, session, _) = Create();
        var calls = 0;
        api.DailySettlement = (date, _) => { calls++; return Task.FromResult(Daily(date, 21)); };
        await model.ActivateAsync();
        model.NextPageCommand.Execute(null);
        model.Deactivate();
        if (scenario == "Clear") await session.ClearAsync();
        else await session.ApplyAsync(Token("test-driver") with { Roles = [] });
        Assert.Single(model.Items);
        await model.ActivateAsync();
        Assert.Empty(model.Items);
        Assert.True(model.IsAuthenticationRequired);
        Assert.False(model.IsLoaded);
        Assert.Equal(1, model.PageNumber);
        Assert.Equal(1, calls);
        model.Deactivate();
    }

    private static (FDriverCompletedDeliveryListPageModel Model, FDriverTestWorkspaceApi Api,
        FDriverTestSession Session, Navigator Navigator) Create()
    {
        var api = new FDriverTestWorkspaceApi();
        var session = new FDriverTestSession();
        var navigator = new Navigator();
        var model = new FDriverCompletedDeliveryListPageModel(api, session, navigator);
        model.SetDate(Day);
        return (model, api, session, navigator);
    }

    private static FoodDeliveryDailySettlementDto Daily(DateOnly date, int count, string owner = "test-driver")
    {
        var start = DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue).AddHours(-9), DateTimeKind.Utc);
        return new()
        {
            DriverId = owner, CompletionDateKst = date, CompletedOrderCount = count, GrossAmountTotal = count * 4180m,
            KnownGrossAmountTotal = count * 4180m, PeriodStartAtUtc = start, PeriodEndAtUtc = start.AddDays(1),
            OrderSettlements = Enumerable.Range(0, count).Select(index => new FoodDeliveryOrderSettlementDto
            {
                SettlementId = $"settlement-{index:D3}", OrderNo = $"order-{index:D3}",
                DeliveryAttemptId = $"attempt-{index:D3}", DriverId = owner, RestaurantName = $"음식점 {index}",
                CompletedAtUtc = start.AddHours(1).AddMinutes(index), GrossAmount = 4180m,
                SettlementStatusCode = "AwaitingDeductions", PricingBreakdown = new() { DistanceKm = 2.6m }
            }).ToArray()
        };
    }

    private static ClientAuthTokenSnapshot Token(string id) => new("access-new", DateTime.UtcNow.AddMinutes(30),
        "refresh-new", DateTime.UtcNow.AddHours(1), id, "기사", ["Driver"]);

    private sealed class Navigator : IFDriverCompletedDeliveryNavigator
    {
        public List<FDriverCompletedDeliveryNavigationTarget> Details { get; } = [];
        public Task OpenListAsync(DateOnly? date = null) => Task.CompletedTask;
        public Task OpenDetailAsync(FDriverCompletedDeliveryNavigationTarget target) { Details.Add(target); return Task.CompletedTask; }
        public Task BackAsync() => Task.CompletedTask;
        public Task ReturnToWorkspaceAsync() => Task.CompletedTask;
    }
}
