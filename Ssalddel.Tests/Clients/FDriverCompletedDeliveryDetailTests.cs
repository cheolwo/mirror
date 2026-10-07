using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using FDriverApp.PageModels;
using FDriverApp.Services;
using Ssalddel.Client.Infrastructure.Security;
using Ssalddel.Contracts.Food;

namespace Ssalddel.Tests.Clients;

public sealed class FDriverCompletedDeliveryDetailTests
{
    private static readonly DateTime ServerNow = new(2026, 10, 4, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task OwnCompletedDelivery_ShowsFeeSnapshotAndThreeSeparateInformationGroups()
    {
        var (model, api, _, _) = Ready();
        api.CompletedDetail = (_, _) => Task.FromResult(Detail());
        await model.ActivateAsync();
        var view = Assert.IsType<FDriverCompletedDeliveryDetailItem>(model.Detail);
        Assert.True(model.HasTarget);
        Assert.Equal("4,180원", view.Settlement.Display.GrossText);
        Assert.Contains("픽업 700원 · 전달 700원", view.Settlement.PricingDetails);
        Assert.Equal("요금 기준 거리 2.6km", view.Settlement.DistanceText);
        Assert.True(view.HasOrderDetails);
        Assert.Contains("메뉴 1 · 2개 · 단가 10,000원", view.MenuText);
        Assert.Equal("20,000원", view.TotalOrderAmountText);
        Assert.True(view.HasCustomerDetails);
        Assert.Equal("수령인 1", view.CustomerName);
        Assert.Equal("010-0000-0000", view.CustomerPhone);
        Assert.Contains("10/4 09:01", model.AccessNotice);
        model.Deactivate();
        AssertPrivateCleared(view);
    }

    [Theory]
    [InlineData("Expired")]
    [InlineData("PolicyNotConfigured")]
    [InlineData("CompletionEvidenceUnavailable")]
    [InlineData("UnknownFutureStatus")]
    public async Task NonAllowedResponse_NeverDisplaysPrivateFields_EvenIfPayloadContainsThem(string status)
    {
        var (model, api, _, _) = Ready();
        api.CompletedDetail = (_, _) => Task.FromResult(Detail(status));
        await model.ActivateAsync();
        var view = Assert.IsType<FDriverCompletedDeliveryDetailItem>(model.Detail);
        AssertPrivateCleared(view);
        Assert.Equal("4,180원", view.Settlement.Display.GrossText);
        Assert.False(model.IsLoading);
        model.Deactivate();
    }

    [Theory]
    [InlineData("missing-expiry")]
    [InlineData("missing-server-now")]
    [InlineData("unspecified-server-now")]
    [InlineData("unspecified-expiry")]
    [InlineData("boundary")]
    [InlineData("overflow-expiry")]
    public async Task AllowedResponse_WithoutValidFutureUtcDeadline_IsClosed(string fault)
    {
        var (model, api, _, _) = Ready();
        var detail = Detail();
        if (fault == "missing-expiry") detail.DetailExpiresAtUtc = null;
        if (fault == "missing-server-now") detail.ServerNowUtc = default;
        if (fault == "unspecified-server-now") detail.ServerNowUtc = DateTime.SpecifyKind(ServerNow, DateTimeKind.Unspecified);
        if (fault == "unspecified-expiry") detail.DetailExpiresAtUtc = DateTime.SpecifyKind(ServerNow.AddMinutes(1), DateTimeKind.Unspecified);
        if (fault == "boundary") detail.DetailExpiresAtUtc = ServerNow;
        if (fault == "overflow-expiry") detail.DetailExpiresAtUtc = DateTime.SpecifyKind(DateTime.MaxValue, DateTimeKind.Utc);
        api.CompletedDetail = (_, _) => Task.FromResult(detail);
        await model.ActivateAsync();
        AssertPrivateCleared(model.Detail!);
        Assert.Equal("4,180원", model.Detail!.Settlement.Display.GrossText);
        model.Deactivate();
    }

    [Fact]
    public async Task ExpiryUsesMonotonicTime_ClearsPreviouslyBoundObject_AndRetainsFinancials()
    {
        var (model, api, _, clock) = Ready();
        api.CompletedDetail = (_, _) => Task.FromResult(Detail(expiresIn: TimeSpan.FromSeconds(10)));
        await model.ActivateAsync();
        var previouslyBound = model.Detail!;
        clock.JumpWallClock(TimeSpan.FromDays(-100));
        clock.Advance(TimeSpan.FromSeconds(9));
        Assert.True(previouslyBound.HasCustomerDetails);
        clock.Advance(TimeSpan.FromSeconds(1));
        await Until(() => !previouslyBound.HasCustomerDetails);
        AssertPrivateCleared(previouslyBound);
        Assert.Same(previouslyBound, model.Detail);
        Assert.Equal("4,180원", previouslyBound.Settlement.Display.GrossText);
        Assert.Contains("열람 기간이 지났습니다", model.AccessNotice);
        model.Deactivate();
    }

    [Fact]
    public async Task RequestElapsedTime_IsSubtractedBeforeDisplayingPrivateDetails()
    {
        var (model, api, _, clock) = Ready();
        var response = new TaskCompletionSource<FoodDeliveryCompletedDeliveryDetailDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        api.CompletedDetail = (_, _) => response.Task;
        var query = model.ActivateAsync();
        clock.Advance(TimeSpan.FromSeconds(12));
        response.SetResult(Detail(expiresIn: TimeSpan.FromSeconds(10)));
        await query;
        AssertPrivateCleared(model.Detail!);
        model.Deactivate();
    }

    [Theory]
    [InlineData("background")]
    [InlineData("manual-workspace")]
    public async Task SeparateMainWorkspaceRefresh_CannotChangeDetailSelectionOrOriginalExpiry(string source)
    {
        var (detailModel, detailApi, session, clock) = Ready();
        var mainApi = new FDriverTestWorkspaceApi();
        var main = FDriverLifecycleTestSupport.Model(session, mainApi);
        await main.InitializeAsync();
        main.Navigation.Select(FDriverWorkspaceSection.Settlement);
        var detailQueries = 0;
        detailApi.CompletedDetail = (_, _) =>
        {
            detailQueries++;
            return Task.FromResult(Detail(expiresIn: TimeSpan.FromSeconds(10)));
        };
        await detailModel.ActivateAsync();
        var previouslyBound = detailModel.Detail!;
        clock.Advance(TimeSpan.FromSeconds(5));
        if (source == "background") await FDriverLifecycleTestSupport.RefreshBackground(main);
        else await main.RefreshCommand.ExecuteAsync(null);
        Assert.Same(previouslyBound, detailModel.Detail);
        Assert.True(previouslyBound.HasOrderDetails);
        Assert.True(previouslyBound.HasCustomerDetails);
        Assert.Equal(1, detailQueries);
        clock.Advance(TimeSpan.FromSeconds(5));
        await Until(() => !previouslyBound.HasCustomerDetails);
        AssertPrivateCleared(previouslyBound);
        Assert.Equal("4,180원", previouslyBound.Settlement.Display.GrossText);
        Assert.Contains("열람 기간이 지났습니다", detailModel.AccessNotice);
        detailModel.Deactivate();
        await main.StopMonitoringAsync();
    }

    [Fact]
    public async Task PauseImmediatelyClearsPrivateFields_ResumeQueriesServerAgain()
    {
        var (model, api, _, _) = Ready();
        var count = 0;
        api.CompletedDetail = (_, _) => Task.FromResult(Detail(++count == 1 ? "Allowed" : "Expired"));
        await model.ActivateAsync();
        var beforePause = model.Detail!;
        model.Pause();
        AssertPrivateCleared(beforePause);
        Assert.Equal("4,180원", beforePause.Settlement.Display.GrossText);
        Assert.True(model.HasTarget);
        await model.ResumeAsync();
        Assert.Equal(2, count);
        AssertPrivateCleared(model.Detail!);
        model.Deactivate();
    }

    [Theory]
    [InlineData("deactivate")]
    [InlineData("pause")]
    [InlineData("target")]
    [InlineData("account")]
    public async Task ChangedPageLifetime_CancelsQueryAndRejectsLatePrivateResponse(string action)
    {
        var (model, api, session, _) = Ready();
        CancellationToken requestToken = default;
        var response = new TaskCompletionSource<FoodDeliveryCompletedDeliveryDetailDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        api.CompletedDetail = (_, token) => { requestToken = token; return response.Task; };
        var query = model.ActivateAsync();
        Assert.True(model.IsLoading);
        if (action == "deactivate") model.Deactivate();
        if (action == "pause") model.Pause();
        if (action == "target") model.SetTarget("settlement-2");
        if (action == "account") await session.ApplyAsync(OtherUser());
        Assert.True(requestToken.IsCancellationRequested);
        response.SetResult(Detail());
        await query;
        Assert.Null(model.Detail);
        Assert.False(model.IsLoading);
        model.Deactivate();
    }

    [Fact]
    public async Task ChangedTarget_DiscardsEarlierResponse_EvenWhenTransportIgnoresCancellation()
    {
        var (model, api, _, _) = Ready();
        var oldResponse = new TaskCompletionSource<FoodDeliveryCompletedDeliveryDetailDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        api.CompletedDetail = (id, _) => id == "settlement-1" ? oldResponse.Task : Task.FromResult(Detail(id: "settlement-2"));
        var firstQuery = model.ActivateAsync();
        model.SetTarget("settlement-2", "order-settlement-2", "attempt-settlement-2");
        await model.RefreshAsync();
        oldResponse.SetResult(Detail());
        await firstQuery;
        Assert.Equal("settlement-2", model.Detail!.Settlement.SettlementId);
        Assert.True(model.Detail.HasCustomerDetails);
        model.Deactivate();
    }

    [Theory]
    [InlineData("owner")]
    [InlineData("settlement")]
    [InlineData("order")]
    [InlineData("attempt")]
    public async Task InconsistentSelectedDetail_IsRejectedBeforePrivateProjection(string fault)
    {
        var (model, api, _, _) = Ready();
        var result = Detail();
        if (fault == "owner") result.Settlement.DriverId = "another-driver";
        if (fault == "settlement") result.Settlement.SettlementId = "another-settlement";
        if (fault == "order") result.Settlement.OrderNo = "another-order";
        if (fault == "attempt") result.Settlement.DeliveryAttemptId = "another-attempt";
        api.CompletedDetail = (_, _) => Task.FromResult(result);
        await model.ActivateAsync();
        Assert.Null(model.Detail);
        Assert.Contains("조회하지 못했습니다", model.StatusText);
        model.Deactivate();
    }

    [Theory]
    [InlineData("owner")]
    [InlineData("settlement")]
    public async Task DeepLinkWithoutOptionalIdentity_StillRequiresOwnerAndExactSettlementId(string fault)
    {
        var (model, api, _, _) = Ready();
        model.SetTarget("settlement-1");
        api.CompletedDetail = (_, _) => Task.FromResult(Detail());
        await model.ActivateAsync();
        Assert.True(model.Detail!.HasCustomerDetails);
        api.CompletedDetail = (_, _) =>
        {
            var result = Detail();
            if (fault == "owner") result.Settlement.DriverId = "another-driver";
            else result.Settlement.SettlementId = "another-settlement";
            return Task.FromResult(result);
        };
        await model.RefreshAsync();
        AssertPrivateCleared(model.Detail!);
        Assert.Contains("조회하지 못했습니다", model.StatusText);
        model.Deactivate();
    }

    [Fact]
    public async Task FailedRequery_ClearsOldPrivateFieldsAndAllowsRetry_WithoutRenderingErrorPayload()
    {
        var (model, api, _, _) = Ready();
        api.CompletedDetail = (_, _) => Task.FromResult(Detail());
        await model.ActivateAsync();
        var beforeFailure = model.Detail!;
        api.CompletedDetail = (_, _) => throw new FDriverApiException("private-customer-address", HttpStatusCode.ServiceUnavailable);
        await model.RefreshCommand.ExecuteAsync(null);
        AssertPrivateCleared(beforeFailure);
        Assert.DoesNotContain("private-customer-address", model.StatusText);
        Assert.Equal("4,180원", model.Detail!.Settlement.Display.GrossText);
        api.CompletedDetail = (_, _) => Task.FromResult(Detail());
        await model.RefreshAsync();
        Assert.True(model.Detail!.HasCustomerDetails);
        model.Deactivate();
    }

    [Fact]
    public async Task AuthLoss_ClearsBoundFieldsWithoutWaitingForNextApiRefresh()
    {
        var (model, api, session, _) = Ready();
        api.CompletedDetail = (_, _) => Task.FromResult(Detail());
        await model.ActivateAsync();
        var previouslyBound = model.Detail!;
        await session.ClearAsync();
        AssertPrivateCleared(previouslyBound);
        Assert.Null(model.Detail);
        Assert.True(model.IsAuthenticationRequired);
        model.Deactivate();
    }

    [Fact]
    public async Task SameUserTokenRefresh_DoesNotExtendDeadline_AccountChangeClearsAndCanRequery()
    {
        var (model, api, session, clock) = Ready();
        api.CompletedDetail = (_, _) => Task.FromResult(Detail(expiresIn: TimeSpan.FromSeconds(10)));
        await model.ActivateAsync();
        var previouslyBound = model.Detail!;
        clock.Advance(TimeSpan.FromSeconds(9));
        await session.ApplyAsync(OtherUser() with { UserId = "test-driver" });
        Assert.Same(previouslyBound, model.Detail);
        Assert.True(previouslyBound.HasCustomerDetails);
        clock.Advance(TimeSpan.FromSeconds(1));
        await Until(() => !previouslyBound.HasCustomerDetails);
        AssertPrivateCleared(previouslyBound);
        await session.ApplyAsync(OtherUser());
        Assert.Null(model.Detail);
        Assert.False(model.IsAuthenticationRequired);
        api.CompletedDetail = (_, _) =>
        {
            var result = Detail();
            result.Settlement.DriverId = "another-driver";
            return Task.FromResult(result);
        };
        await model.RefreshAsync();
        Assert.Equal("another-driver", model.Detail!.Settlement.DriverId);
        Assert.True(model.Detail.HasCustomerDetails);
        model.Deactivate();
    }

    [Fact]
    public async Task Deactivation_ReleasesSessionSubscriptionAndIgnoresLaterSessionChanges()
    {
        var (model, api, session, _) = Ready();
        api.CompletedDetail = (_, _) => Task.FromResult(Detail());
        Assert.Equal(0, SessionListenerCount(session));
        await model.ActivateAsync();
        Assert.Equal(1, SessionListenerCount(session));
        await model.ActivateAsync();
        Assert.Equal(1, SessionListenerCount(session));
        model.Deactivate();
        Assert.Equal(0, SessionListenerCount(session));
        var status = model.StatusText;
        await session.ClearAsync();
        Assert.Equal(status, model.StatusText);
        Assert.Null(model.Detail);
        await model.ActivateAsync();
        Assert.True(model.IsAuthenticationRequired);
        Assert.Equal(1, SessionListenerCount(session));
        model.Deactivate();
    }

    [Fact]
    public async Task MissingTarget_DoesNotCallApi_AndDeactivatedModelCannotRefresh()
    {
        var (model, api, _, _) = Ready();
        var calls = 0;
        api.CompletedDetail = (_, _) => { calls++; return Task.FromResult(Detail()); };
        model.SetTarget(" ");
        await model.ActivateAsync();
        Assert.False(model.HasTarget);
        Assert.Equal(0, calls);
        model.Deactivate();
        model.SetTarget("settlement-1");
        await model.RefreshAsync();
        await model.ResumeAsync();
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task AnonymousActivation_DoesNotFetchProtectedTarget()
    {
        var (model, api, session, _) = Ready();
        await session.ClearAsync();
        var calls = 0;
        api.CompletedDetail = (_, _) => { calls++; return Task.FromResult(Detail()); };
        await model.ActivateAsync();
        Assert.Equal(0, calls);
        Assert.True(model.HasTarget);
        Assert.True(model.IsAuthenticationRequired);
        Assert.Null(model.Detail);
        model.Deactivate();
    }

    [Theory]
    [InlineData("Orderer")]
    [InlineData("")]
    public async Task AuthenticatedAccountWithoutDriverRole_DoesNotFetchOrKeepFinancialView(string role)
    {
        var (model, api, session, _) = Ready();
        await session.ApplyAsync(OtherUser() with { UserId = "test-driver", Roles = role.Length == 0 ? [] : [role] });
        var calls = 0;
        api.CompletedDetail = (_, _) => { calls++; return Task.FromResult(Detail()); };
        await model.ActivateAsync();
        await model.RefreshAsync();
        Assert.Equal(0, calls);
        Assert.True(model.IsAuthenticationRequired);
        Assert.Contains("기사 권한", model.StatusText);
        Assert.Null(model.Detail);
        Assert.False(model.HasDetail);
        model.Deactivate();
    }

    [Theory]
    [InlineData("driver")]
    [InlineData("기사")]
    public async Task SupportedDriverRole_AllowsOwnDeliveryDetail(string role)
    {
        var (model, api, session, _) = Ready();
        await session.ApplyAsync(OtherUser() with { UserId = "test-driver", Roles = [role] });
        api.CompletedDetail = (_, _) => Task.FromResult(Detail());
        await model.ActivateAsync();
        Assert.False(model.IsAuthenticationRequired);
        Assert.True(model.HasDetail);
        Assert.True(model.Detail!.HasCustomerDetails);
        model.Deactivate();
    }

    [Fact]
    public async Task SameAccountRoleRevocation_ClearsBoundViewCancelsLateResponse_AndRoleRestorationCanRequery()
    {
        var (model, api, session, _) = Ready();
        api.CompletedDetail = (_, _) => Task.FromResult(Detail());
        await model.ActivateAsync();
        var previouslyBound = model.Detail!;
        CancellationToken requestToken = default;
        var response = new TaskCompletionSource<FoodDeliveryCompletedDeliveryDetailDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        api.CompletedDetail = (_, token) => { requestToken = token; return response.Task; };
        var query = model.RefreshAsync();
        await session.ApplyAsync(OtherUser() with { UserId = "test-driver", Roles = ["Orderer"] });
        Assert.True(requestToken.IsCancellationRequested);
        AssertPrivateCleared(previouslyBound);
        Assert.Null(model.Detail);
        Assert.False(model.HasDetail);
        Assert.True(model.IsAuthenticationRequired);
        Assert.Contains("기사 권한", model.StatusText);
        response.SetResult(Detail());
        await query;
        Assert.Null(model.Detail);
        Assert.False(model.IsLoading);

        await session.ApplyAsync(OtherUser() with { UserId = "test-driver", Roles = ["기사"] });
        Assert.False(model.IsAuthenticationRequired);
        Assert.Null(model.Detail);
        api.CompletedDetail = (_, _) => Task.FromResult(Detail());
        await model.RefreshAsync();
        Assert.True(model.HasDetail);
        Assert.True(model.Detail!.HasCustomerDetails);
        model.Deactivate();
    }

    [Fact]
    public async Task ResponseAfterRoleLoss_IsRejectedEvenWhenSessionChangeNotificationIsMissing()
    {
        var (model, api, session, _) = Ready();
        var response = new TaskCompletionSource<FoodDeliveryCompletedDeliveryDetailDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        api.CompletedDetail = (_, _) => { calls++; return response.Task; };
        var query = model.ActivateAsync();
        // Deliberately bypass the event to exercise the response-time session gate itself.
        typeof(FDriverTestSession).GetField("snapshot", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(session, OtherUser() with { UserId = "test-driver", Roles = ["Orderer"] });
        response.SetResult(Detail());
        await query;
        Assert.Null(model.Detail);
        Assert.False(model.HasDetail);
        await model.RefreshAsync();
        Assert.Equal(1, calls);
        Assert.True(model.IsAuthenticationRequired);
        Assert.Contains("기사 권한", model.StatusText);
        model.Deactivate();
    }

    [Fact]
    public async Task ForbiddenQuery_ClearsPrivateAndFinancialViewWithoutLoggingOutAnotherValidRole()
    {
        var (model, api, session, _) = Ready();
        api.CompletedDetail = (_, _) => Task.FromResult(Detail());
        await model.ActivateAsync();
        var previouslyBound = model.Detail!;
        api.CompletedDetail = (_, _) => throw new FDriverApiException("private forbidden payload", HttpStatusCode.Forbidden);
        await model.RefreshAsync();
        AssertPrivateCleared(previouslyBound);
        Assert.Null(model.Detail);
        Assert.False(model.HasDetail);
        Assert.True(model.IsAuthenticationRequired);
        Assert.Contains("기사 권한", model.StatusText);
        Assert.DoesNotContain("private forbidden payload", model.StatusText);
        Assert.Equal(0, session.ClearCount);
        model.Deactivate();
    }

    [Fact]
    public async Task HasDetail_NotifiesWhenFinancialCardAppearsAndDisappears_AndRemainsTrueAtPrivateExpiry()
    {
        var (model, api, _, clock) = Ready();
        var notifications = new List<string?>();
        model.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);
        Assert.False(model.HasDetail);
        api.CompletedDetail = (_, _) => Task.FromResult(Detail(expiresIn: TimeSpan.FromSeconds(1)));
        await model.ActivateAsync();
        Assert.True(model.HasDetail);
        Assert.Contains(nameof(model.HasDetail), notifications);
        notifications.Clear();
        clock.Advance(TimeSpan.FromSeconds(1));
        await Until(() => !model.Detail!.HasCustomerDetails);
        Assert.True(model.HasDetail);
        Assert.DoesNotContain(nameof(model.HasDetail), notifications);
        model.Deactivate();
        Assert.False(model.HasDetail);
        Assert.Contains(nameof(model.HasDetail), notifications);
    }

    [Fact]
    public async Task NewPageActivation_RejectsPriorLifetimeResponseForSameTarget()
    {
        var (model, api, _, _) = Ready();
        var oldResponse = new TaskCompletionSource<FoodDeliveryCompletedDeliveryDetailDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        api.CompletedDetail = (_, _) => oldResponse.Task;
        var oldActivation = model.ActivateAsync();
        model.Deactivate();
        api.CompletedDetail = (_, _) => Task.FromResult(Detail());
        await model.ActivateAsync();
        var currentView = model.Detail!;
        oldResponse.SetResult(Detail("Expired"));
        await oldActivation;
        Assert.Same(currentView, model.Detail);
        Assert.True(currentView.HasCustomerDetails);
        Assert.False(model.IsLoading);
        model.Deactivate();
    }

    [Fact]
    public async Task UnauthorizedQuery_ClearsSessionAndPriorFinancialView_EvenWhenStorageRemovalFails()
    {
        var (model, api, session, _) = Ready();
        api.CompletedDetail = (_, _) => Task.FromResult(Detail());
        await model.ActivateAsync();
        var previouslyBound = model.Detail!;
        session.ClearFailure = new InvalidOperationException("storage removal failed");
        api.CompletedDetail = (_, _) => throw new FDriverApiException("private unauthorized payload", HttpStatusCode.Unauthorized);
        await model.RefreshAsync();
        AssertPrivateCleared(previouslyBound);
        Assert.Null(model.Detail);
        Assert.True(model.IsAuthenticationRequired);
        Assert.False(model.IsLoading);
        Assert.DoesNotContain("private unauthorized payload", model.StatusText);
        model.Deactivate();
    }

    [Fact]
    public async Task ApiRequestsAuthorizedEncodedSettlementRoute()
    {
        var session = new FDriverTestSession();
        var handler = new FDriverTestHttpHandler((request, _) =>
        {
            Assert.Equal("/api/v1/driver/food-deliveries/settlements/settlement%20one/detail", request.RequestUri!.AbsolutePath);
            Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(Detail()) });
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        var api = new FoodDeliveryDriverApiService(http, session, new(http, session));
        Assert.Equal("Allowed", (await api.GetCompletedDeliveryDetailAsync("settlement one")).DetailAccessStatusCode);
    }

    private static (FDriverCompletedDeliveryDetailPageModel, FDriverTestWorkspaceApi, FDriverTestSession, ManualClock) Ready()
    {
        var session = new FDriverTestSession();
        var api = new FDriverTestWorkspaceApi();
        var clock = new ManualClock();
        var model = new FDriverCompletedDeliveryDetailPageModel(api, session, clock);
        model.SetTarget("settlement-1", "order-settlement-1", "attempt-settlement-1");
        return (model, api, session, clock);
    }

    private static FoodDeliveryOrderSettlementDto Settlement(string id) => new()
    {
        SettlementId = id, OrderNo = "order-" + id, DeliveryAttemptId = "attempt-" + id,
        DriverId = "test-driver", RestaurantName = "음식점 1", GrossAmount = 4180,
        CompletedAtUtc = ServerNow.AddHours(-1), SettlementStatusCode = "AwaitingDeductions",
        PricingBreakdown = new() { EvidenceStatusCode = "Stored", BaseSplitCode = "Split", PickupAmount = 700,
            DropoffAmount = 700, DistanceAmount = 2080, TimeSurchargeAmount = 700, DistanceKm = 2.6m }
    };

    private static FoodDeliveryCompletedDeliveryDetailDto Detail(string status = "Allowed", string id = "settlement-1", TimeSpan? expiresIn = null)
        => new()
        {
            Settlement = Settlement(id), DetailAccessStatusCode = status, ServerNowUtc = ServerNow,
            DetailExpiresAtUtc = ServerNow + (expiresIn ?? TimeSpan.FromMinutes(1)),
            OrderDetails = new() { RestaurantAddress = "픽업 주소 1", TotalOrderAmount = 20000,
                Items = [new() { MenuName = "메뉴 1", Quantity = 2, UnitPrice = 10000 }] },
            CustomerDetails = new() { DisplayName = "수령인 1", ContactPhone = "010-0000-0000",
                Address = "전달 주소 1", DeliveryInstructions = "문 앞에 놓고 벨을 눌러 주세요." }
        };

    private static ClientAuthTokenSnapshot OtherUser() => new("access-other", DateTime.UtcNow.AddMinutes(30),
        "refresh-other", DateTime.UtcNow.AddHours(1), "another-driver", "다른 기사", ["Driver"]);

    private static int SessionListenerCount(FDriverTestSession session)
        => (typeof(FDriverTestSession).GetField("SessionChanged", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(session) as Delegate)?.GetInvocationList().Length ?? 0;

    private static void AssertPrivateCleared(FDriverCompletedDeliveryDetailItem view)
    {
        Assert.False(view.HasOrderDetails);
        Assert.False(view.HasCustomerDetails);
        Assert.Empty(view.RestaurantAddress);
        Assert.Empty(view.TotalOrderAmountText);
        Assert.Empty(view.MenuText);
        Assert.Empty(view.CustomerName);
        Assert.Empty(view.CustomerPhone);
        Assert.Empty(view.CustomerAddress);
        Assert.Empty(view.DeliveryInstructions);
    }

    private static async Task Until(Func<bool> condition)
    {
        for (var i = 0; i < 100 && !condition(); i++) await Task.Delay(5);
        Assert.True(condition());
    }

    private sealed class ManualClock : TimeProvider
    {
        private long _elapsed;
        private TimeSpan _wallClockOffset;
        private readonly List<ManualTimer> _timers = [];
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => _elapsed;
        public override DateTimeOffset GetUtcNow() => new DateTimeOffset(ServerNow) + TimeSpan.FromTicks(_elapsed) + _wallClockOffset;
        public void JumpWallClock(TimeSpan delta) => _wallClockOffset += delta;
        public void Advance(TimeSpan delta)
        {
            _elapsed += delta.Ticks;
            foreach (var timer in _timers.ToArray()) timer.Fire(_elapsed);
        }
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = new ManualTimer(this, callback, state);
            timer.Change(dueTime, period);
            _timers.Add(timer);
            return timer;
        }
        private sealed class ManualTimer(ManualClock clock, TimerCallback callback, object? state) : ITimer
        {
            private long _due;
            private TimeSpan _period;
            private bool _disposed;
            public bool Change(TimeSpan dueTime, TimeSpan period)
            {
                if (_disposed) return false;
                _due = dueTime == Timeout.InfiniteTimeSpan ? long.MaxValue : clock._elapsed + dueTime.Ticks;
                _period = period;
                return true;
            }
            public void Fire(long now)
            {
                if (_disposed || now < _due) return;
                _due = _period == Timeout.InfiniteTimeSpan ? long.MaxValue : now + _period.Ticks;
                callback(state);
            }
            public void Dispose() => _disposed = true;
            public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
        }
    }
}
