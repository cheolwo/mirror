using FDriverApp.PageModels;
using FDriverApp.Services;
using Ssalddel.Client.Infrastructure.Security;
using Ssalddel.Contracts.Food;

namespace Ssalddel.Tests.Clients;

public sealed class FDriverCompletedDeliveryRowNavigationTests
{
    private static readonly DateOnly Day = new(2026, 10, 4);

    [Fact]
    public async Task ReturningFromDetailPreservesDatePageAndFirstVisibleRowWithoutRefetching()
    {
        var (model, api, _, navigator) = Create();
        var calls = 0;
        api.DailySettlement = (date, _) => { calls++; return Task.FromResult(Daily(date)); };
        await model.ActivateAsync();
        model.NextPageCommand.Execute(null);
        model.RememberScrollPosition(7);
        var firstVisible = model.Items[7];
        var selected = model.Items[9];

        await selected.OpenDetailsCommand!.ExecuteAsync(null);
        model.Deactivate();
        await model.ActivateAsync();

        Assert.Equal(1, calls);
        Assert.Equal(Day, DateOnly.FromDateTime(model.SelectedDate));
        Assert.Equal(2, model.PageNumber);
        Assert.Equal(7, model.ScrollRestoreIndex);
        Assert.Same(firstVisible, model.Items[model.ScrollRestoreIndex!.Value]);
        Assert.Equal(selected.SettlementId, Assert.Single(navigator.Details).SettlementId);
    }

    [Theory]
    [InlineData("Date")]
    [InlineData("Page")]
    [InlineData("Refresh")]
    public async Task ReplacingVisibleRowsClearsTheirPreviousScrollAnchor(string change)
    {
        var (model, _, _, _) = Create();
        await model.ActivateAsync();
        model.RememberScrollPosition(7);
        Assert.Equal(7, model.ScrollRestoreIndex);

        if (change == "Date") model.SetDate(Day.AddDays(-1));
        else if (change == "Page") model.NextPageCommand.Execute(null);
        else await model.RefreshAsync();

        Assert.Null(model.ScrollRestoreIndex);
    }

    [Fact]
    public async Task AnAccountChangeWhileViewingDetailCannotRestoreThePreviousAccountsRow()
    {
        var (model, api, session, _) = Create();
        await model.ActivateAsync();
        model.NextPageCommand.Execute(null);
        model.RememberScrollPosition(7);
        model.Deactivate();
        await session.ApplyAsync(new ClientAuthTokenSnapshot("access-other", DateTime.UtcNow.AddMinutes(30),
            "refresh-other", DateTime.UtcNow.AddHours(1), "another-driver", "기사", ["Driver"]));
        Assert.Null(model.ScrollRestoreIndex);
        api.DailySettlement = (date, _) => Task.FromResult(Daily(date, "another-driver"));

        await model.ActivateAsync();

        Assert.Null(model.ScrollRestoreIndex);
        Assert.Equal(1, model.PageNumber);
        Assert.All(model.Items, row => Assert.Equal("another-driver", row.DriverId));
    }

    [Fact]
    public async Task OffscreenAndOutOfRangeScrollEventsDoNotReplaceTheLastVisibleRow()
    {
        var (model, _, _, _) = Create();
        await model.ActivateAsync();
        model.RememberScrollPosition(7);
        model.RememberScrollPosition(-1);
        model.RememberScrollPosition(model.Items.Count);
        model.Deactivate();
        model.RememberScrollPosition(0);
        await model.ActivateAsync();

        Assert.Equal(7, model.ScrollRestoreIndex);
    }

    private static (FDriverCompletedDeliveryListPageModel Model, FDriverTestWorkspaceApi Api,
        FDriverTestSession Session, Navigator Navigator) Create()
    {
        var api = new FDriverTestWorkspaceApi { DailySettlement = (date, _) => Task.FromResult(Daily(date)) };
        var session = new FDriverTestSession();
        var navigator = new Navigator();
        var model = new FDriverCompletedDeliveryListPageModel(api, session, navigator);
        model.SetDate(Day);
        return (model, api, session, navigator);
    }

    private static FoodDeliveryDailySettlementDto Daily(DateOnly date, string owner = "test-driver")
    {
        var start = DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue).AddHours(-9), DateTimeKind.Utc);
        return new()
        {
            DriverId = owner, CompletionDateKst = date, CompletedOrderCount = 55,
            GrossAmountTotal = 55 * 4180m, KnownGrossAmountTotal = 55 * 4180m,
            PeriodStartAtUtc = start, PeriodEndAtUtc = start.AddDays(1),
            OrderSettlements = Enumerable.Range(0, 55).Select(index => new FoodDeliveryOrderSettlementDto
            {
                SettlementId = $"settlement-{index:D3}", OrderNo = $"order-{index:D3}",
                DeliveryAttemptId = $"attempt-{index:D3}", DriverId = owner, RestaurantName = $"음식점 {index}",
                CompletedAtUtc = start.AddHours(1).AddMinutes(index), GrossAmount = 4180m,
                SettlementStatusCode = "AwaitingDeductions", PricingBreakdown = new() { DistanceKm = 2.6m }
            }).ToArray()
        };
    }

    private sealed class Navigator : IFDriverCompletedDeliveryNavigator
    {
        public List<FDriverCompletedDeliveryNavigationTarget> Details { get; } = [];
        public Task OpenListAsync(DateOnly? date = null) => Task.CompletedTask;
        public Task OpenDetailAsync(FDriverCompletedDeliveryNavigationTarget target)
        {
            Details.Add(target);
            return Task.CompletedTask;
        }
        public Task BackAsync() => Task.CompletedTask;
        public Task ReturnToWorkspaceAsync() => Task.CompletedTask;
    }
}
