using System.Net;
using System.Xml.Linq;
using FDriverApp.PageModels;
using FDriverApp.Services;
using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Contracts.Common.Drivers;
using Ssalddel.Contracts.Driver.Food;
using Ssalddel.Contracts.Driver.Work;
using Ssalddel.Contracts.Food;

namespace Ssalddel.Tests.Clients;

public sealed class FDriverWaitingPresentationTests
{
    [Theory]
    [InlineData(false, true, false, true, false, false)]
    [InlineData(false, true, true, true, false, false)]
    [InlineData(true, false, false, false, true, false)]
    [InlineData(true, true, false, false, false, true)]
    [InlineData(true, true, true, false, false, false)]
    public async Task EmptyWorkspace_OffersOnlyTheNextNecessaryWaitingAction(bool onDuty, bool receives, bool hasLocation,
        bool start, bool enableDispatch, bool retryLocation)
    {
        var model = FDriverLifecycleTestSupport.Model(new(), Api(onDuty, receives));
        await model.InitializeAsync();
        model.HasCurrentLocation = hasLocation;
        Assert.True(model.IsWaitingForDelivery);
        Assert.Equal(start, model.IsStartWorkPrimaryAction);
        Assert.Equal(enableDispatch, model.IsEnableDispatchPrimaryAction);
        Assert.Equal(retryLocation, model.IsWaitingLocationPrimaryAction);
        Assert.Equal(start || enableDispatch || retryLocation, model.HasDeliveryFooter);
        Assert.False(model.HasSelectedRecommendationSummary);
        Assert.False(model.HasDeliveryDetails);
        Assert.False(model.IsDeliveryDetailsVisible);
        Assert.False(model.HasTaskLocationWarning);
        Assert.False(model.HasTaskDispatchNotice);
        Assert.False(model.IsWorkControlsExpanded);
        Assert.False(model.HasDeliveryStatusNotice);
        model.ToggleDeliveryDetailsCommand.Execute(null);
        Assert.False(model.IsDeliveryDetailsExpanded);
        Assert.False(model.IsFoodCardExpanded);
        Assert.Equal(onDuty ? "배차 대기" : "운행 시작 전", model.WaitingTitle);
    }

    [Fact]
    public void AnonymousWorkspace_HasNoWaitingWorkflowOrAction()
    {
        var model = FDriverLifecycleTestSupport.Model(new(), new());
        Assert.False(model.IsAuthenticated);
        Assert.False(model.IsWaitingForDelivery);
        Assert.False(model.IsStartWorkPrimaryAction);
        Assert.False(model.IsEnableDispatchPrimaryAction);
        Assert.False(model.IsWaitingLocationPrimaryAction);
        Assert.False(model.HasDeliveryFooter);
    }

    [Fact]
    public async Task StartWithoutLocation_UsesExistingWorkCommandThenShowsTheLocationStep()
    {
        var model = FDriverLifecycleTestSupport.Model(new(), Api(onDuty: false, receives: true));
        await model.InitializeAsync();
        Assert.True(model.CanToggleWork);
        Assert.True(model.IsStartWorkPrimaryAction);
        await model.ToggleWorkCommand.ExecuteAsync(null);
        Assert.True(model.IsOnDuty);
        Assert.False(model.IsStartWorkPrimaryAction);
        Assert.True(model.IsWaitingLocationPrimaryAction);
        Assert.Contains("위치 권한과 GPS", model.WaitingNotice);
        Assert.False(model.HasDeliveryStatusNotice);
    }

    [Fact]
    public async Task BusyStart_KeepsActionIdentityAndDisablesTheExistingCommand()
    {
        var model = FDriverLifecycleTestSupport.Model(new(), Api(onDuty: false, receives: true));
        await model.InitializeAsync();
        model.IsBusy = true;
        Assert.True(model.IsStartWorkPrimaryAction);
        Assert.True(model.HasDeliveryFooter);
        Assert.False(model.CanToggleWork);
        await model.ToggleWorkCommand.ExecuteAsync(null);
        Assert.False(model.IsOnDuty);
    }

    [Fact]
    public async Task NewDeviceLocation_NotifiesWaitingFooterAndRemovesLocationRetry()
    {
        var model = FDriverLifecycleTestSupport.Model(new(), Api(onDuty: true, receives: true));
        await model.InitializeAsync();
        Assert.True(model.IsWaitingLocationPrimaryAction);
        var changed = new List<string?>();
        model.PropertyChanged += (_, args) => changed.Add(args.PropertyName);
        model.HasCurrentLocation = true;
        Assert.False(model.IsWaitingLocationPrimaryAction);
        Assert.False(model.HasDeliveryFooter);
        Assert.Contains(nameof(MainPageModel.IsWaitingLocationPrimaryAction), changed);
        Assert.Contains(nameof(MainPageModel.HasDeliveryFooter), changed);
        Assert.Contains(nameof(MainPageModel.WaitingNotice), changed);
    }

    [Fact]
    public async Task UnknownDispatchIntent_DoesNotInventAnEnableOrLocationAction()
    {
        var api = Api(onDuty: true, receives: true);
        api.Availability = _ => throw new FDriverApiException("connection failed", HttpStatusCode.ServiceUnavailable);
        var model = FDriverLifecycleTestSupport.Model(new(), api);
        await model.InitializeAsync();
        Assert.False(model.DispatchIntentKnown);
        Assert.False(model.IsEnableDispatchPrimaryAction);
        Assert.False(model.IsWaitingLocationPrimaryAction);
        Assert.Contains("확인하지 못했습니다", model.WaitingNotice);
    }

    [Theory]
    [InlineData(운영배차실효상태Code.서버일시정지, "일시 중지")]
    [InlineData(운영배차실효상태Code.조건부적합, "조건을 충족하지 못했습니다")]
    [InlineData(운영배차실효상태Code.연결확인불가, "연결 상태를 확인할 수 없습니다")]
    public async Task ServerRestrictedOnIntent_RemainsRestrictedInTheEmptyWaitingCard(string effectiveState, string notice)
    {
        var api = Api(onDuty: true, receives: true);
        api.Availability = _ => Task.FromResult(new 운영배차수신상태Dto
        {
            수신의사Code = 운영배차수신의사Code.On, 실효상태Code = effectiveState,
            실효사유Code = "ServerRecordedRestriction", 실효상태변경시각Utc = DateTimeOffset.UtcNow
        });
        var model = FDriverLifecycleTestSupport.Model(new(), api);
        await model.InitializeAsync();
        model.HasCurrentLocation = true;
        Assert.True(model.ReceivesNewDispatches);
        Assert.True(model.HasServerDispatchRestriction);
        Assert.Contains(notice, model.WaitingNotice);
        Assert.DoesNotContain("새로운 배달 요청을 기다리고", model.WaitingNotice);
        Assert.False(model.HasSelectedRecommendationSummary);
        Assert.False(model.HasDeliveryFooter);
        Assert.False(model.IsWaitingLocationPrimaryAction);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InitialUnrecordedEligibility_IsDistinctFromServerRestrictionAndEligible(bool hasLocation)
    {
        var api = Api(onDuty: true, receives: true);
        api.Availability = _ => Task.FromResult(new 운영배차수신상태Dto
        {
            수신의사Code = 운영배차수신의사Code.On, 실효상태Code = 운영배차실효상태Code.조건부적합,
            실효사유Code = "NoEffectiveEligibilityRecorded", 실효상태변경시각Utc = null
        });
        var model = FDriverLifecycleTestSupport.Model(new(), api);
        await model.InitializeAsync();
        model.HasCurrentLocation = hasLocation;
        Assert.False(model.HasServerDispatchRestriction);
        Assert.Equal(!hasLocation, model.IsWaitingLocationPrimaryAction);
        Assert.Contains(hasLocation ? "가능 여부를 확인" : "위치 권한과 GPS", model.WaitingNotice);
        Assert.DoesNotContain("새로운 배달 요청을 기다리고", model.WaitingNotice);
    }

    [Fact]
    public async Task NewEligibleServerRead_ClearsPreviousRestrictionAndNotifiesTheWaitingNotice()
    {
        var api = Api(onDuty: true, receives: true);
        api.Availability = _ => Task.FromResult(new 운영배차수신상태Dto
        {
            수신의사Code = 운영배차수신의사Code.On, 실효상태Code = 운영배차실효상태Code.서버일시정지
        });
        var model = FDriverLifecycleTestSupport.Model(new(), api, new FDriverFixedTestLocationService());
        await model.InitializeAsync();
        Assert.True(model.HasCurrentLocation);
        Assert.True(model.HasServerDispatchRestriction);
        var changed = new List<string?>();
        model.PropertyChanged += (_, args) => changed.Add(args.PropertyName);
        api.Availability = _ => Task.FromResult(new 운영배차수신상태Dto
        {
            수신의사Code = 운영배차수신의사Code.On, 실효상태Code = 운영배차실효상태Code.배차가능,
            실효상태변경시각Utc = DateTimeOffset.UtcNow
        });
        await FDriverLifecycleTestSupport.RefreshBackground(model);
        Assert.False(model.HasServerDispatchRestriction);
        Assert.Contains("새로운 배달 요청을 기다리고", model.WaitingNotice);
        Assert.Contains(nameof(MainPageModel.WaitingNotice), changed);
    }

    [Fact]
    public async Task WaitingCommandFailure_RemainsVisibleInsteadOfBeingReplacedByRoutineGuidance()
    {
        var model = FDriverLifecycleTestSupport.Model(new(), Api(onDuty: false, receives: true));
        await model.InitializeAsync();
        Assert.False(model.HasDeliveryStatusNotice);
        var changed = new List<string?>();
        model.PropertyChanged += (_, args) => changed.Add(args.PropertyName);
        model.StatusMessage = "서버에 연결하지 못했습니다. 다시 시도해 주세요.";
        Assert.True(model.HasDeliveryStatusNotice);
        Assert.Contains(nameof(MainPageModel.HasDeliveryStatusNotice), changed);
        Assert.True(model.IsStartWorkPrimaryAction);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void ActiveDelivery_RetainsStagePermissionAndFeeWithoutAnyWaitingAction(int stage)
    {
        var model = FDriverLifecycleTestSupport.Model(new(), new());
        model.IsAuthenticated = true;
        var actionId = stage switch
        {
            0 => 음식배달가능행동Ids.기사가게도착,
            1 => 음식배달가능행동Ids.기사픽업확인,
            _ => 음식배달가능행동Ids.기사전달완료
        };
        model.ActiveDelivery = ActiveDeliveryPreview.From(new FoodDeliveryDriverActiveDeliveryDto
        {
            OfferId = "active", DeliveryAttemptId = "attempt", DriverPayout = 4720,
            WorkStatus = stage == 2 ? DriverWorkOfferStatus.MovingToDropoff : DriverWorkOfferStatus.MovingToPickup,
            RestaurantArrivedAtUtc = stage == 1 ? DateTime.UtcNow : null,
            AvailableActions = [new() { ActionId = actionId }],
            Pickup = new() { Address = "음식점" }, Dropoff = new() { Address = "전달지" }
        });
        Assert.False(model.IsWaitingForDelivery);
        Assert.False(model.IsStartWorkPrimaryAction);
        Assert.False(model.IsEnableDispatchPrimaryAction);
        Assert.False(model.IsWaitingLocationPrimaryAction);
        Assert.Equal(stage == 0, model.CanRecordArrival);
        Assert.Equal(stage == 1, model.CanConfirmPickup);
        Assert.Equal(stage == 2, model.CanCompleteDelivery);
        Assert.Equal(4720, model.ActiveDelivery.DriverPayout);
    }

    [Fact]
    public void WaitingActions_AreFixedOutsideTheScrollableBodyAndDetailsRequireRealContent()
    {
        var page = XDocument.Load(RepoFile("FDriverApp/Pages/MainPage.xaml"));
        XNamespace x = "http://schemas.microsoft.com/winfx/2009/xaml";
        var footer = Assert.Single(page.Descendants(), element => (string?)element.Attribute(x + "Name") == "DeliveryPrimaryFooter");
        foreach (var label in new[] { "운행 시작", "신규 배차 받기", "위치 다시 확인" })
        {
            var action = Assert.Single(footer.Elements(), element => (string?)element.Attribute("Text") == label);
            Assert.DoesNotContain(action.Ancestors(), element => element.Name.LocalName == "ScrollView");
        }
        var body = Assert.Single(page.Descendants(), element => (string?)element.Attribute(x + "Name") == "DeliveryCardBodyContent");
        var details = Assert.Single(body.Elements(), element => (string?)element.Attribute("Command") == "{Binding ToggleDeliveryDetailsCommand}");
        Assert.Equal("{Binding HasDeliveryDetails}", (string?)details.Attribute("IsVisible"));
        var target = Assert.Single(page.Descendants(), element =>
            (string?)element.Attribute("Text") == "{Binding CurrentDeliveryTargetText}"
            && (string?)element.Attribute("IsVisible") == "{Binding HasSelectedRecommendationSummary}");
        Assert.Equal("Label", target.Name.LocalName);
    }

    private static FDriverTestWorkspaceApi Api(bool onDuty, bool receives) => new()
    {
        WorkStatus = onDuty ? "운행중" : "운행종료",
        Workspace = _ => Task.FromResult(new FoodDeliveryDriverWorkspaceDto
        {
            DriverId = "test-driver", UpdatedAtUtc = DateTime.UtcNow, MaxActiveDeliveries = 3,
            DispatchAutomationEnabled = true
        }),
        Availability = _ => Task.FromResult(new 운영배차수신상태Dto
        {
            수신의사Code = receives ? 운영배차수신의사Code.On : 운영배차수신의사Code.Off,
            실효상태Code = 운영배차실효상태Code.배차가능
        })
    };

    private static string RepoFile(string relativePath)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "Ssalddel.v3.5.slnx")))
                return Path.Combine(directory.FullName, relativePath.Replace('/', Path.DirectorySeparatorChar));
        throw new InvalidOperationException("Repository root was not found for food driver UI checks.");
    }
}
