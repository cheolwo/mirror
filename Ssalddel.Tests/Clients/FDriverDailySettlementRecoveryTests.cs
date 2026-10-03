using System.Net;
using System.Globalization;
using System.Net.Http.Json;
using FDriverApp.PageModels;
using FDriverApp.Services;
using Ssalddel.Contracts.Food;

namespace Ssalddel.Tests.Clients;

public sealed class FDriverDailySettlementRecoveryTests
{
    [Fact]
    public async Task DailyScreen_UsesEntireServerDay_AndPreservesUnknownDeductionsAndTransfer()
    {
        var session = new FDriverTestSession();
        var api = new FDriverTestWorkspaceApi();
        api.DailySettlement = (date, _) => Task.FromResult(Daily(date, 45));
        var model = FDriverLifecycleTestSupport.Model(session, api);
        await model.InitializeAsync();
        await model.RefreshDailySettlementAsync();

        Assert.Equal(45, model.DailySettlementItems.Count);
        Assert.Contains("45건", model.DailySettlementCountText);
        Assert.Equal("112,500원", model.DailySettlementGrossText);
        Assert.Equal("45건 공제 확인 중", model.DailySettlementDeductionText);
        Assert.Equal("45건 수령액 확인 중", model.DailySettlementNetText);
        Assert.Equal("실제 입금 확인 전", model.DailySettlementPaymentText);
        Assert.True(model.IsDailySettlementLoaded);
        await model.StopMonitoringAsync();
    }

    [Fact]
    public async Task ChangedDate_RejectsLateOldResponse_EvenIfTransportIgnoresCancellation()
    {
        var api = new FDriverTestWorkspaceApi();
        var model = FDriverLifecycleTestSupport.Model(new(), api);
        await model.InitializeAsync();
        var oldDate = DateOnly.FromDateTime(model.SelectedSettlementDate);
        var oldResponse = new TaskCompletionSource<FoodDeliveryDailySettlementDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        var newResponse = new TaskCompletionSource<FoodDeliveryDailySettlementDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        api.DailySettlement = (date, _) => date == oldDate ? oldResponse.Task : newResponse.Task;
        var oldQuery = model.RefreshDailySettlementAsync();
        model.SelectedSettlementDate = model.SelectedSettlementDate.AddDays(-1);
        var newDate = DateOnly.FromDateTime(model.SelectedSettlementDate);
        newResponse.SetResult(Daily(newDate, 2));
        for (var i = 0; i < 50 && !model.IsDailySettlementLoaded; i++) await Task.Delay(5);
        oldResponse.SetResult(Daily(oldDate, 45));
        await oldQuery;

        Assert.Equal(2, model.DailySettlementItems.Count);
        Assert.Equal("5,000원", model.DailySettlementGrossText);
        await model.StopMonitoringAsync();
    }

    [Fact]
    public async Task PageExit_CancelsDailyQuery_AndDoesNotApplyLateResponse()
    {
        var api = new FDriverTestWorkspaceApi();
        var model = FDriverLifecycleTestSupport.Model(new(), api);
        await model.InitializeAsync();
        var response = new TaskCompletionSource<FoodDeliveryDailySettlementDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        var date = DateOnly.FromDateTime(model.SelectedSettlementDate);
        api.DailySettlement = (_, _) => response.Task;
        var query = model.RefreshDailySettlementAsync();
        await model.StopMonitoringAsync();
        response.SetResult(Daily(date, 45));
        await query;
        Assert.Empty(model.DailySettlementItems);
        Assert.False(model.IsDailySettlementLoading);
    }

    [Fact]
    public async Task OtherDriverResponse_IsRejectedInsteadOfDisplayingPrivateSettlement()
    {
        var api = new FDriverTestWorkspaceApi();
        var model = FDriverLifecycleTestSupport.Model(new(), api);
        await model.InitializeAsync();
        api.DailySettlement = (date, _) => { var row = Daily(date, 1); row.DriverId = "another-driver"; return Task.FromResult(row); };
        await model.RefreshDailySettlementAsync();
        Assert.Empty(model.DailySettlementItems);
        Assert.False(model.IsDailySettlementLoaded);
        Assert.Contains("조회하지 못했습니다", model.DailySettlementStatus);
        await model.StopMonitoringAsync();
    }

    [Theory]
    [InlineData("ko-KR")]
    [InlineData("ar-SA")]
    [InlineData("th-TH")]
    public async Task DailyApi_SendsExplicitKoreanCompletionDate_ToExistingAuthorizedDriverRoute(string culture)
    {
        var originalCulture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
        try
        {
        var session = new FDriverTestSession();
        var date = new DateOnly(2026, 10, 3);
        var handler = new FDriverTestHttpHandler((request, _) =>
        {
            Assert.Equal("/api/v1/driver/food-deliveries/settlements/daily?date=2026-10-03", request.RequestUri!.PathAndQuery);
            Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(Daily(date, 1)) });
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        var api = new FoodDeliveryDriverApiService(http, session, new(http, session));
        Assert.Equal(date, (await api.GetDailySettlementAsync(date)).CompletionDateKst);
        }
        finally { CultureInfo.CurrentCulture = originalCulture; }
    }

    [Theory]
    [InlineData("partial")]
    [InlineData("row-owner")]
    [InlineData("date")]
    public async Task InconsistentDayResponse_IsNotShownAsFullSettlement(string fault)
    {
        var api = new FDriverTestWorkspaceApi();
        var model = FDriverLifecycleTestSupport.Model(new(), api);
        await model.InitializeAsync();
        api.DailySettlement = (date, _) =>
        {
            var result = Daily(date, 45);
            if (fault == "partial") result.OrderSettlements = result.OrderSettlements.Take(40).ToArray();
            if (fault == "row-owner") result.OrderSettlements[0].DriverId = "another-driver";
            if (fault == "date") result.CompletionDateKst = date.AddDays(-1);
            return Task.FromResult(result);
        };
        await model.RefreshDailySettlementAsync();
        Assert.Empty(model.DailySettlementItems);
        Assert.False(model.IsDailySettlementLoaded);
        Assert.Contains("조회하지 못했습니다", model.DailySettlementStatus);
        await model.StopMonitoringAsync();
    }

    [Fact]
    public async Task MalformedJson_IsARecoverableClientError()
    {
        var session = new FDriverTestSession();
        var handler = new FDriverTestHttpHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent("{invalid", System.Text.Encoding.UTF8, "application/json") }));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        var api = new FoodDeliveryDriverApiService(http, session, new(http, session));
        var error = await Assert.ThrowsAsync<FDriverApiException>(() => api.GetDailySettlementAsync(new(2026, 10, 3)));
        Assert.Contains("다시 조회", error.Message);
        Assert.True(session.IsAuthenticated);
    }

    [Fact]
    public void LegacyUnsplitPricing_DoesNotInventZeroPickupOrDropoff()
    {
        var item = FDriverDailySettlementItem.From(new()
        {
            CompletedAtUtc = new(2026, 10, 2, 16, 0, 0, DateTimeKind.Utc),
            PricingBreakdown = new() { EvidenceStatusCode = "Stored", BaseSplitCode = "LegacyUnsplit", BaseAmount = 1400, DistanceAmount = 300 }
        });
        Assert.Contains("픽업·전달 구분 미확인", item.PricingDetails);
        Assert.DoesNotContain("픽업 0원", item.PricingDetails);
        Assert.Contains("10/3 01:00", item.CompletedText);
        item.TogglePricingCommand.Execute(null);
        Assert.True(item.IsPricingExpanded);
    }

    private static FoodDeliveryDailySettlementDto Daily(DateOnly date, int count) => new()
    {
        DriverId = "test-driver", CompletionDateKst = date, CompletedOrderCount = count,
        GrossAmountTotal = count * 2500, KnownGrossAmountTotal = count * 2500,
        UnconfirmedDeductionCount = count, UnconfirmedNetAmountCount = count, UpdatedAtUtc = DateTime.UtcNow,
        OrderSettlements = Enumerable.Range(0, count).Select(i => new FoodDeliveryOrderSettlementDto
        { SettlementId = $"day-{i}", DriverId = "test-driver", RestaurantName = "검증 음식점", GrossAmount = 2500, SettlementStatusCode = "AwaitingDeductions" }).ToArray()
    };
}
