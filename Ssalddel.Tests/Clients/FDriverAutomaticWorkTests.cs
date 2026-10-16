using System.Net;
using System.Text.Json;
using System.Xml.Linq;
using FDriverApp.PageModels;
using FDriverApp.Services;
using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Contracts.Driver.Food;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Food;

namespace Ssalddel.Tests.Clients;

public sealed class FDriverAutomaticWorkTests
{
    [Fact]
    public async Task NoResponsePause_ShowsOffAndResumesOnlyFromManualOnWithoutClearingOtherRestrictions()
    {
        var paused = true; var requests = new List<운영배차수신의사변경요청>();
        var api = Api(); api.WorkStatus = "운행중";
        api.Availability = _ => Task.FromResult(new 운영배차수신상태Dto
        { 수신의사Code = 운영배차수신의사Code.On, 미응답자동중지여부 = paused, 연속미응답횟수 = paused ? 3 : 0,
            실효상태Code = 운영배차실효상태Code.서버일시정지 });
        api.ChangeIntent = (request, _) =>
        {
            requests.Add(request); if (request.미응답자동중지해제) paused = false;
            return api.Availability(default);
        };
        var model = FDriverLifecycleTestSupport.Model(new(), api);
        try
        {
            await model.InitializeAsync(); await FDriverLifecycleTestSupport.RefreshBackground(model);
            Assert.False(model.IsDispatchReceptionOn); Assert.True(model.CanUseDispatchReceptionSwitch);
            Assert.Equal(FoodDriverDispatchReceptionPolicy.AutomaticStopNotice, model.DispatchAutomaticStopNotice);
            Assert.Empty(requests);
            await model.ToggleDispatchIntentCommand.ExecuteAsync(null);
            var request = Assert.Single(requests);
            Assert.Equal(운영배차수신의사Code.On, request.수신의사Code); Assert.True(request.미응답자동중지해제);
            Assert.True(model.IsDispatchReceptionOn); Assert.Null(model.DispatchAutomaticStopNotice);
            Assert.Contains("조건", model.DispatchIntentNotice);
        }
        finally { await model.StopMonitoringAsync(); }
    }

    [Fact]
    public async Task AutomaticStartOn_PreservesNoResponsePauseAndUnlocksExplicitResumeSwitch()
    {
        var intent = 운영배차수신의사Code.Off; var requests = new List<운영배차수신의사변경요청>();
        var api = Api(); api.WorkStatus = "운행종료";
        api.Availability = _ => Task.FromResult(new 운영배차수신상태Dto
        { 수신의사Code = intent, 미응답자동중지여부 = true, 연속미응답횟수 = 3 });
        api.ChangeIntent = (request, _) => { requests.Add(request); intent = request.수신의사Code; return api.Availability(default); };
        var model = FDriverLifecycleTestSupport.Model(new(), api);
        try
        {
            await model.InitializeAsync(); await model.ToggleWorkCommand.ExecuteAsync(null);
            Assert.True(model.IsOnDuty); Assert.False(model.IsDispatchReceptionOn);
            Assert.True(model.CanUseDispatchReceptionSwitch);
            Assert.All(requests, request => Assert.False(request.미응답자동중지해제));
            Assert.Equal(FoodDriverDispatchReceptionPolicy.AutomaticStopNotice, model.DispatchAutomaticStopNotice);
        }
        finally { await model.StopMonitoringAsync(); }
    }

    [Fact]
    public async Task AcknowledgedOldResume_DoesNotRetryForeverAfterAnotherPause_AndNextGestureGetsNewId()
    {
        var paused = true; var attempts = new List<운영배차수신의사변경요청>();
        var api = Api(); api.WorkStatus = "운행중";
        api.Availability = _ => Task.FromResult(new 운영배차수신상태Dto
        { 수신의사Code = 운영배차수신의사Code.On, 미응답자동중지여부 = paused, 연속미응답횟수 = paused ? 3 : 0 });
        api.ChangeIntent = (request, _) =>
        {
            attempts.Add(request);
            if (attempts.Count == 1) throw new FDriverApiException("이미 적용한 뒤 새로운 미응답 중지가 발생한 합성 상황", HttpStatusCode.ServiceUnavailable);
            if (attempts.Count >= 3) paused = false;
            return api.Availability(default);
        };
        var model = FDriverLifecycleTestSupport.Model(new(), api);
        try
        {
            await model.InitializeAsync(); await model.ToggleDispatchIntentCommand.ExecuteAsync(null);
            Assert.False(model.IsDispatchReceptionOn);
            await FDriverLifecycleTestSupport.RefreshBackground(model);
            Assert.Equal(2, attempts.Count); Assert.Equal(attempts[0].클라이언트요청Id, attempts[1].클라이언트요청Id);
            await FDriverLifecycleTestSupport.RefreshBackground(model);
            Assert.Equal(2, attempts.Count); Assert.False(model.IsDispatchReceptionOn);
            await model.ToggleDispatchIntentCommand.ExecuteAsync(null);
            Assert.Equal(3, attempts.Count); Assert.NotEqual(attempts[1].클라이언트요청Id, attempts[2].클라이언트요청Id);
            Assert.All(attempts, request => Assert.True(request.미응답자동중지해제));
            Assert.True(model.IsDispatchReceptionOn);
        }
        finally { await model.StopMonitoringAsync(); }
    }

    [Fact]
    public async Task AutomaticTick_RecoversUnknownDispatchIntentWithoutManualRefreshOrIntentChange()
    {
        var availabilityFails = true;
        var reads = 0;
        var writes = 0;
        var api = Api();
        api.Availability = _ =>
        {
            reads++;
            return availabilityFails
                ? throw new FDriverApiException("offline", HttpStatusCode.ServiceUnavailable)
                : Task.FromResult(new 운영배차수신상태Dto
                {
                    수신의사Code = 운영배차수신의사Code.On,
                    실효상태Code = 운영배차실효상태Code.배차가능
                });
        };
        api.ChangeIntent = (request, _) =>
        {
            writes++;
            return Task.FromResult(new 운영배차수신상태Dto { 수신의사Code = request.수신의사Code });
        };
        var model = FDriverLifecycleTestSupport.Model(new(), api);
        try
        {
            await model.InitializeAsync();
            Assert.False(model.DispatchIntentKnown);
            Assert.False(model.CanUseDispatchReceptionSwitch);
            Assert.Contains("자동으로 다시 확인", model.DispatchIntentNotice);
            Assert.DoesNotContain("새로고침", model.DispatchIntentNotice);

            availabilityFails = false;
            await FDriverLifecycleTestSupport.RefreshBackground(model);

            Assert.Equal(2, reads);
            Assert.Equal(0, writes);
            Assert.True(model.DispatchIntentKnown);
            Assert.True(model.IsDispatchReceptionOn);
            Assert.True(model.CanUseDispatchReceptionSwitch);
        }
        finally { await model.StopMonitoringAsync(); }
    }

    [Fact]
    public async Task AutomaticTick_RetriesFailedWorkspaceAndReplacesOnlyItsFailureNotice()
    {
        var api = Api();
        var model = FDriverLifecycleTestSupport.Model(new(), api);
        try
        {
            await model.InitializeAsync();
            api.Workspace = _ => throw new FDriverApiException("offline", HttpStatusCode.ServiceUnavailable);
            await FDriverLifecycleTestSupport.RefreshBackground(model);
            Assert.True(model.HasWorkspaceWarning);
            Assert.Contains("자동으로 다시 확인", model.WorkspaceWarningText);
            Assert.DoesNotContain("새로고침", model.WorkspaceWarningText);

            api.Workspace = _ => Task.FromResult(FDriverTestWorkspaceApi.Data("next"));
            await FDriverLifecycleTestSupport.RefreshBackground(model);

            Assert.False(model.HasWorkspaceWarning);
            Assert.Equal("next", model.SelectedTicket?.TicketId);
            Assert.DoesNotContain("불러오지 못했습니다", model.StatusMessage);
        }
        finally { await model.StopMonitoringAsync(); }
    }

    [Fact]
    public async Task DeviceGps_UpdatesMapWithoutFetchingTheWholeWorkspaceForEveryFix()
    {
        var workspaceReads = 0;
        var api = Api();
        api.Workspace = _ =>
        {
            workspaceReads++;
            return Task.FromResult(new FoodDeliveryDriverWorkspaceDto
                { DriverId = "test-driver", UpdatedAtUtc = DateTime.UtcNow, MaxActiveDeliveries = 3 });
        };
        var gps = new LocationFake();
        var model = FDriverLifecycleTestSupport.Model(new(), api, gps);
        try
        {
            await model.InitializeAsync();
            await model.ResumeFoodMapAsync();
            var before = workspaceReads;
            gps.Emit(new(37.51m, 127.01m, 5, DateTime.UtcNow));
            gps.Emit(new(37.52m, 127.02m, 5, DateTime.UtcNow.AddMilliseconds(1)));

            Assert.True(model.HasCurrentLocation);
            Assert.Equal(37.52d, model.CurrentLocationLatitude);
            Assert.Equal(127.02d, model.CurrentLocationLongitude);
            Assert.Equal(before, workspaceReads);
        }
        finally { await model.StopMonitoringAsync(); }
    }

    [Fact]
    public void WorkspaceAndProfile_ScrollWithoutPullToRefreshAndKeepExplicitLocationRecovery()
    {
        var page = XDocument.Load(RepoFile("FDriverApp/Pages/MainPage.xaml"));
        XNamespace x = "http://schemas.microsoft.com/winfx/2009/xaml";
        Assert.DoesNotContain(page.Descendants(), element => element.Name.LocalName == "RefreshView");
        foreach (var name in new[] { "WorkspaceScroll", "ProfileScroll" })
        {
            var scroll = Assert.Single(page.Descendants(), element => (string?)element.Attribute(x + "Name") == name);
            Assert.Equal("ScrollView", scroll.Name.LocalName);
        }
        var recoveries = page.Descendants().Where(element =>
            (string?)element.Attribute("Command") == "{Binding RetryMapLocationCommand}").ToArray();
        Assert.NotEmpty(recoveries);
        Assert.All(recoveries, recovery => Assert.Contains((string?)recovery.Attribute("IsVisible"),
            new[] { "{Binding HasTaskLocationWarning}", "{Binding IsWaitingLocationPrimaryAction}" }));
    }

    [Fact]
    public async Task ExplicitStart_PersistsIntentBeforeOneStart_AndEnablesReceivingAfterCanonicalDriving()
    {
        var api = Api(); api.WorkStatus = "운행종료";
        var store = new PendingStore();
        var intent = 운영배차수신의사Code.Off;
        var requests = IntentApi(api, () => intent, request => intent = request.수신의사Code);
        api.StartWork = (_, _) =>
        {
            Assert.NotNull(store.Snapshot?.StartReceptionRequestId);
            Assert.Equal(Assert.Single(api.StartRequestIds), store.Snapshot?.StartRequestId);
            Assert.NotEqual(store.Snapshot!.StartRequestId, store.Snapshot.StartReceptionRequestId);
            Assert.Equal(store.Snapshot!.StartReceptionRequestId, store.Snapshot.DispatchIntent?.클라이언트요청Id);
            Assert.Equal(운영배차수신의사Code.On, store.Snapshot.DispatchIntent?.수신의사Code);
            return Task.CompletedTask;
        };
        var model = Model(api, store);
        try
        {
            await model.InitializeAsync();
            await model.ToggleWorkCommand.ExecuteAsync(null);
            Assert.Equal(1, api.StartWorkCalls);
            Assert.Single(requests);
            Assert.True(model.IsOnDuty);
            Assert.True(model.IsDispatchReceptionOn);
            Assert.Null(store.Snapshot);
            await FDriverLifecycleTestSupport.RefreshBackground(model);
            Assert.Equal(1, api.StartWorkCalls);
            Assert.Single(requests);
        }
        finally { await model.StopMonitoringAsync(); }
    }

    [Theory]
    [InlineData(400)]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(409)]
    public async Task DefinitiveStartRejection_ClearsDurableStartAndOnIntent_AndPreservesExistingOff(int status)
    {
        var api = Api(); api.WorkStatus = "운행종료";
        var intent = 운영배차수신의사Code.Off;
        var requests = IntentApi(api, () => intent, request => intent = request.수신의사Code);
        api.StartWork = (_, _) =>
        {
            api.WorkStatus = "운행중"; // Another app/request started first and explicitly kept reception OFF.
            throw new FDriverApiException("운행 시작 거절", (HttpStatusCode)status);
        };
        var store = new PendingStore(); var model = Model(api, store);
        try
        {
            await model.InitializeAsync();
            await model.ToggleWorkCommand.ExecuteAsync(null);
            Assert.Null(store.Snapshot);
            Assert.Contains(status == 401 ? "로그인" : "운행 시작 거절", model.StatusMessage);
            await FDriverLifecycleTestSupport.RefreshBackground(model);
            Assert.Equal(1, api.StartWorkCalls); Assert.Empty(requests);
            Assert.Equal(운영배차수신의사Code.Off, intent); Assert.False(model.IsDispatchReceptionOn);
            Assert.Null(store.Snapshot);
            if (status == 401) Assert.False(model.IsAuthenticated);
        }
        finally { await model.StopMonitoringAsync(); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LostStartResponse_UsesCanonicalStatusAndNeverAutomaticallyStartsAgain(bool applied)
    {
        var api = Api(); api.WorkStatus = "운행종료";
        var intent = 운영배차수신의사Code.Off;
        var requests = IntentApi(api, () => intent, request => intent = request.수신의사Code);
        api.StartWork = (_, _) =>
        {
            api.WorkStatus = applied ? "운행중" : "운행종료";
            throw new FDriverApiException("lost", null);
        };
        var store = new PendingStore(); var model = Model(api, store);
        try
        {
            await model.InitializeAsync();
            await model.ToggleWorkCommand.ExecuteAsync(null);
            await FDriverLifecycleTestSupport.RefreshBackground(model);
            Assert.Equal(1, api.StartWorkCalls);
            Assert.Equal(applied ? 1 : 0, requests.Count);
            Assert.Equal(applied, model.IsOnDuty);
            Assert.Equal(applied, model.IsDispatchReceptionOn);
            Assert.Null(store.Snapshot);
            if (!applied) Assert.True(model.CanToggleWork);
        }
        finally { await model.StopMonitoringAsync(); }
    }

    [Theory]
    [InlineData("different", false)]
    [InlineData("different", true)]
    [InlineData("missing", false)]
    [InlineData("missing", true)]
    [InlineData("cargo", false)]
    [InlineData("cargo", true)]
    public async Task StartWithoutMatchingFoodShift_NeverEnablesAnotherShiftsReception(string mismatch, bool responseLost)
    {
        var api = Api(); api.WorkStatus = "운행종료";
        var requests = IntentApi(api, () => 운영배차수신의사Code.Off, _ => { });
        api.StartWork = (_, _) =>
        {
            if (mismatch == "different") api.CanonicalStartRequestId = Guid.NewGuid();
            if (mismatch == "missing") api.CanonicalStartRequestId = null;
            if (mismatch == "cargo") api.CanonicalExecutionType = "Cargo";
            if (responseLost) throw new FDriverApiException("lost", null);
            return Task.CompletedTask;
        };
        var store = new PendingStore(); var model = Model(api, store);
        try
        {
            await model.InitializeAsync(); await model.ToggleWorkCommand.ExecuteAsync(null);
            await FDriverLifecycleTestSupport.RefreshBackground(model);
            Assert.Equal(FoodDriverWorkRecoveryPolicy.StartOwnershipNotice, model.StatusMessage);
            Assert.Equal(1, api.StartWorkCalls); Assert.Empty(requests); Assert.Null(store.Snapshot);
            Assert.True(model.IsOnDuty); Assert.False(model.IsDispatchReceptionOn);
        }
        finally { await model.StopMonitoringAsync(); }
    }

    [Fact]
    public async Task LegacyPendingStartWithoutRequestId_IsClearedWithoutAutomaticallyEnablingReception()
    {
        var api = Api(); var store = new PendingStore(); var id = Guid.NewGuid();
        await store.SaveAsync(new("test-driver", null, new() { 클라이언트요청Id = id,
            수신의사Code = 운영배차수신의사Code.On }, null, id));
        var requests = IntentApi(api, () => 운영배차수신의사Code.Off, _ => { });
        var model = Model(api, store);
        try
        {
            await model.InitializeAsync(); await FDriverLifecycleTestSupport.RefreshBackground(model);
            Assert.Empty(requests); Assert.Equal(0, api.StartWorkCalls); Assert.Null(store.Snapshot);
            Assert.Equal(FoodDriverWorkRecoveryPolicy.StartOwnershipNotice, model.StatusMessage);
        }
        finally { await model.StopMonitoringAsync(); }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task LostOnResponse_RecoversWithOriginalRequestIdAndNoRepeatedStart(bool applied, bool restart)
    {
        var api = Api(); api.WorkStatus = "운행종료";
        var intent = 운영배차수신의사Code.Off;
        var requests = new List<운영배차수신의사변경요청>();
        api.Availability = _ => Task.FromResult(new 운영배차수신상태Dto
            { 수신의사Code = intent, 실효상태Code = 운영배차실효상태Code.배차가능 });
        api.ChangeIntent = (request, _) =>
        {
            requests.Add(request);
            if (requests.Count > 1 || applied) intent = request.수신의사Code;
            if (requests.Count == 1) throw new FDriverApiException("lost", null);
            return Task.FromResult(new 운영배차수신상태Dto { 수신의사Code = intent });
        };
        var store = new PendingStore(); var model = Model(api, store);
        try
        {
            await model.InitializeAsync();
            await model.ToggleWorkCommand.ExecuteAsync(null);
            Assert.Single(requests);
            if (!applied)
            {
                Assert.NotNull(store.Snapshot?.StartReceptionRequestId);
                Assert.False(model.CanUseDispatchReceptionSwitch);
            }
            if (restart)
            {
                await model.StopMonitoringAsync();
                model = Model(api, store);
                await model.InitializeAsync();
                Assert.Single(requests);
            }
            await FDriverLifecycleTestSupport.RefreshBackground(model);
            Assert.Equal(1, api.StartWorkCalls);
            Assert.Equal(applied ? 1 : 2, requests.Count);
            Assert.All(requests, request => Assert.Equal(requests[0].클라이언트요청Id, request.클라이언트요청Id));
            Assert.True(model.IsDispatchReceptionOn);
            Assert.Null(store.Snapshot);
        }
        finally { await model.StopMonitoringAsync(); }
    }

    [Fact]
    public async Task ExplicitOff_RemainsOffDuringAutomaticWorkspaceTicks()
    {
        var api = Api(); api.WorkStatus = "운행종료";
        var intent = 운영배차수신의사Code.Off;
        var requests = IntentApi(api, () => intent, request => intent = request.수신의사Code);
        var model = Model(api, new());
        try
        {
            await model.InitializeAsync();
            await model.ToggleWorkCommand.ExecuteAsync(null);
            await model.ToggleDispatchIntentCommand.ExecuteAsync(null);
            await FDriverLifecycleTestSupport.RefreshBackground(model);
            await FDriverLifecycleTestSupport.RefreshBackground(model);
            Assert.True(model.IsOnDuty);
            Assert.False(model.ReceivesNewDispatches);
            Assert.Equal(new[] { 운영배차수신의사Code.On, 운영배차수신의사Code.Off }, requests.Select(x => x.수신의사Code));
        }
        finally { await model.StopMonitoringAsync(); }
    }

    [Fact]
    public async Task UnconfirmedOff_IsNotOverwrittenByAStartIntent()
    {
        var api = Api(); api.WorkStatus = "운행종료";
        api.Availability = _ => Task.FromResult(new 운영배차수신상태Dto { 수신의사Code = 운영배차수신의사Code.On });
        var requests = new List<운영배차수신의사변경요청>();
        api.ChangeIntent = (request, _) =>
        {
            requests.Add(request);
            throw new FDriverApiException("lost", null);
        };
        var store = new PendingStore(); var model = Model(api, store);
        try
        {
            await model.InitializeAsync();
            await model.ToggleDispatchIntentCommand.ExecuteAsync(null);
            await model.ToggleWorkCommand.ExecuteAsync(null);
            Assert.Equal(0, api.StartWorkCalls);
            Assert.Equal(2, requests.Count);
            Assert.All(requests, x => Assert.Equal(운영배차수신의사Code.Off, x.수신의사Code));
            Assert.Equal(requests[0].클라이언트요청Id, requests[1].클라이언트요청Id);
            Assert.Null(store.Snapshot?.StartReceptionRequestId);
        }
        finally { await model.StopMonitoringAsync(); }
    }

    [Fact]
    public async Task PositionUploadFailure_DoesNotPreventCanonicalWorkspaceOrIntentRecovery()
    {
        var api = Api();
        var gps = new LocationFake(); var model = FDriverLifecycleTestSupport.Model(new(), api, gps);
        try
        {
            await model.InitializeAsync();
            await model.ResumeFoodMapAsync();
            api.LocationUpdate = (_, _) => throw new FDriverApiException("location offline", HttpStatusCode.ServiceUnavailable);
            typeof(MainPageModel).GetField("_lastLocationSentAtUtc", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .SetValue(model, null);
            api.Workspace = _ => Task.FromResult(FDriverTestWorkspaceApi.Data("new-automatic"));
            await FDriverLifecycleTestSupport.RefreshBackground(model);
            Assert.Equal("new-automatic", model.SelectedTicket?.TicketId);
            Assert.True(model.DispatchIntentKnown);
            Assert.Contains("위치 전송 지연", model.LocationSyncText);
            Assert.True(model.IsAuthenticated);
        }
        finally { await model.StopMonitoringAsync(); }
    }

    [Fact]
    public void DurableStartIntent_IsOptionalForOldJson_AndRoundTripsItsExistingRequestId()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var legacy = JsonSerializer.Deserialize<FDriverPendingOperationsSnapshot>(
            "{\"ownerId\":\"test-driver\",\"arrival\":null,\"dispatchIntent\":null,\"interruption\":null}", options);
        Assert.NotNull(legacy);
        Assert.Null(legacy.StartReceptionRequestId);
        Assert.Null(legacy.StartRequestId);
        var id = Guid.NewGuid();
        var startId = Guid.NewGuid();
        var current = new FDriverPendingOperationsSnapshot("test-driver", null,
            new() { 클라이언트요청Id = id, 수신의사Code = 운영배차수신의사Code.On }, null, id, startId);
        var restored = JsonSerializer.Deserialize<FDriverPendingOperationsSnapshot>(JsonSerializer.Serialize(current, options), options)!;
        Assert.Equal(id, restored.StartReceptionRequestId);
        Assert.Equal(id, restored.DispatchIntent?.클라이언트요청Id);
        Assert.Equal(startId, restored.StartRequestId);
    }

    [Fact]
    public async Task LostStartAndStatusResponses_BlockAnotherStartUntilAutomaticCanonicalConfirmation()
    {
        var api = Api(); api.WorkStatus = "운행종료";
        var intent = 운영배차수신의사Code.Off;
        var requests = IntentApi(api, () => intent, request => intent = request.수신의사Code);
        api.StartWork = (_, _) =>
        {
            api.WorkStatusRead = _ => throw new FDriverApiException("status offline", HttpStatusCode.ServiceUnavailable);
            throw new FDriverApiException("start lost", null);
        };
        var store = new PendingStore(); var model = Model(api, store);
        try
        {
            await model.InitializeAsync();
            await model.ToggleWorkCommand.ExecuteAsync(null);
            await FDriverLifecycleTestSupport.RefreshBackground(model);
            Assert.False(model.CanToggleWork);
            await model.ToggleWorkCommand.ExecuteAsync(null);
            Assert.Equal(1, api.StartWorkCalls);
            Assert.Empty(requests);
            Assert.NotNull(store.Snapshot?.StartReceptionRequestId);
            api.WorkStatusRead = null;
            await FDriverLifecycleTestSupport.RefreshBackground(model);
            Assert.True(model.IsOnDuty);
            Assert.True(model.IsDispatchReceptionOn);
            Assert.Single(requests);
            Assert.Equal(1, api.StartWorkCalls);
        }
        finally { await model.StopMonitoringAsync(); }
    }

    [Theory]
    [InlineData("확인중")]
    [InlineData("FutureWorkStatus")]
    public async Task UnknownNonemptyWorkStatus_KeepsStartLockedUntilKnownCanonicalState(string unknown)
    {
        var api = Api(); api.WorkStatus = "운행종료";
        var intent = 운영배차수신의사Code.Off;
        var requests = IntentApi(api, () => intent, request => intent = request.수신의사Code);
        api.StartWork = (_, _) =>
        {
            api.WorkStatus = unknown;
            throw new FDriverApiException("start lost", null);
        };
        var store = new PendingStore(); var model = Model(api, store);
        try
        {
            await model.InitializeAsync();
            await model.ToggleWorkCommand.ExecuteAsync(null);
            await FDriverLifecycleTestSupport.RefreshBackground(model);
            Assert.False(model.CanToggleWork);
            Assert.NotNull(store.Snapshot?.StartReceptionRequestId);
            await model.ToggleWorkCommand.ExecuteAsync(null);
            Assert.Equal(1, api.StartWorkCalls);
            Assert.Empty(requests);

            api.WorkStatus = "대기"; // Actual canonical stopped code from the server domain.
            await FDriverLifecycleTestSupport.RefreshBackground(model);
            Assert.True(model.CanToggleWork);
            Assert.False(model.IsOnDuty);
            Assert.Null(store.Snapshot);
            Assert.Equal(1, api.StartWorkCalls);
            Assert.Empty(requests);
        }
        finally { await model.StopMonitoringAsync(); }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("확인중")]
    public async Task InitialUnknownWorkStatus_LocksWorkAndReceptionUntilAnAutomaticCanonicalRead(string? unknown)
    {
        var api = Api();
        api.WorkStatusRead = _ => Task.FromResult<Ssalddel.Contracts.Driver.Work.기사운행상태응답?>(unknown is null
            ? null : new() { DriverId = "test-driver", Status = unknown });
        var intent = 운영배차수신의사Code.Off;
        var requests = IntentApi(api, () => intent, request => intent = request.수신의사Code);
        var model = Model(api, new());
        try
        {
            await model.InitializeAsync();
            Assert.True(model.DispatchIntentKnown);
            Assert.False(model.CanToggleWork);
            Assert.False(model.CanUseDispatchReceptionSwitch);
            await model.ToggleWorkCommand.ExecuteAsync(null);
            await model.ToggleDispatchIntentCommand.ExecuteAsync(null);
            await FDriverLifecycleTestSupport.RefreshBackground(model);
            Assert.Equal(0, api.StartWorkCalls);
            Assert.Empty(requests);
            Assert.False(model.CanToggleWork);

            api.WorkStatusRead = null;
            api.WorkStatus = "대기";
            await FDriverLifecycleTestSupport.RefreshBackground(model);
            Assert.True(model.CanToggleWork);
            Assert.True(model.CanUseDispatchReceptionSwitch);
            Assert.False(model.IsOnDuty);
            Assert.Equal(0, api.StartWorkCalls);
            Assert.Empty(requests);
        }
        finally { await model.StopMonitoringAsync(); }
    }

    [Fact]
    public async Task LocationUpload503AfterStart_DoesNotBlockCanonicalWorkspaceOrRepeatStartAndOn()
    {
        var api = Api(); api.WorkStatus = "운행종료";
        var intent = 운영배차수신의사Code.Off;
        var requests = IntentApi(api, () => intent, request => intent = request.수신의사Code);
        api.LocationUpdate = (_, _) => throw new FDriverApiException("location offline", HttpStatusCode.ServiceUnavailable);
        api.Workspace = _ => Task.FromResult(FDriverTestWorkspaceApi.Data(api.StartWorkCalls == 0 ? "before" : "after"));
        var model = Model(api, new(), location: new LocationFake());
        try
        {
            await model.InitializeAsync();
            await model.ToggleWorkCommand.ExecuteAsync(null);
            Assert.True(model.IsOnDuty);
            Assert.True(model.IsDispatchReceptionOn);
            Assert.Equal("after", model.SelectedTicket?.TicketId);
            Assert.Contains("위치 전송 지연", model.LocationSyncText);
            await FDriverLifecycleTestSupport.RefreshBackground(model);
            Assert.Equal(1, api.StartWorkCalls);
            Assert.Single(requests);
            Assert.Equal("after", model.SelectedTicket?.TicketId);
        }
        finally { await model.StopMonitoringAsync(); }
    }

    [Fact]
    public async Task PendingIntentStorageFailure_PreventsStartAndReceivingWrites()
    {
        var api = Api(); api.WorkStatus = "운행종료";
        var intent = 운영배차수신의사Code.Off;
        var requests = IntentApi(api, () => intent, request => intent = request.수신의사Code);
        var store = new PendingStore(); var model = Model(api, store);
        try
        {
            await model.InitializeAsync();
            store.SaveFailure = new IOException("storage unavailable");
            await model.ToggleWorkCommand.ExecuteAsync(null);
            Assert.Equal(0, api.StartWorkCalls);
            Assert.Empty(requests);
            Assert.False(model.CanToggleWork);
            Assert.Contains("안전하게 저장", model.StatusMessage);
            Assert.DoesNotContain("새로고침", model.StatusMessage);
        }
        finally { await model.StopMonitoringAsync(); }
    }

    [Fact]
    public async Task AccountChangeDuringStart_NeverEnablesReceivingForTheNextAccount()
    {
        var api = Api(); api.WorkStatus = "운행종료";
        var intent = 운영배차수신의사Code.Off;
        var requests = IntentApi(api, () => intent, request => intent = request.수신의사Code);
        var late = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        api.StartWork = (_, _) => late.Task;
        var session = new FDriverTestSession(); var model = Model(api, new(), session);
        try
        {
            await model.InitializeAsync();
            var start = model.ToggleWorkCommand.ExecuteAsync(null);
            Assert.Equal(1, api.StartWorkCalls);
            await session.ApplyAsync(new Ssalddel.Client.Infrastructure.Security.ClientAuthTokenSnapshot(
                "next-access", DateTime.UtcNow.AddMinutes(30), "next-refresh", DateTime.UtcNow.AddHours(1),
                "next-driver", "다른 기사", ["Driver"]));
            late.SetResult();
            await start;
            Assert.Empty(requests);
            Assert.False(model.IsDispatchReceptionOn);
        }
        finally { late.TrySetResult(); await model.StopMonitoringAsync(); }
    }

    private static MainPageModel Model(FDriverTestWorkspaceApi api, PendingStore store, FDriverTestSession? suppliedSession = null,
        IFDriverLocationService? location = null)
    {
        var session = suppliedSession ?? new FDriverTestSession();
        var http = new HttpClient(new FDriverTestHttpHandler((_, _) => Task.FromResult(FDriverTestHttpHandler.TokenResponse())))
            { BaseAddress = new("http://localhost/") };
        return new MainPageModel(new(), session, new(http, session), api, location ?? new FDriverTestLocationService(), new(), pendingOperationStore: store);
    }

    private static List<운영배차수신의사변경요청> IntentApi(FDriverTestWorkspaceApi api, Func<string> get, Action<운영배차수신의사변경요청> apply)
    {
        var requests = new List<운영배차수신의사변경요청>();
        api.Availability = _ => Task.FromResult(new 운영배차수신상태Dto
            { 수신의사Code = get(), 실효상태Code = 운영배차실효상태Code.배차가능 });
        api.ChangeIntent = (request, _) =>
        {
            requests.Add(request); apply(request);
            return Task.FromResult(new 운영배차수신상태Dto { 수신의사Code = get() });
        };
        return requests;
    }

    private sealed class PendingStore : IFDriverPendingOperationStore
    {
        private string? json;
        public Exception? SaveFailure { get; set; }
        public FDriverPendingOperationsSnapshot? Snapshot => json is null ? null : JsonSerializer.Deserialize<FDriverPendingOperationsSnapshot>(json);
        public Task<FDriverPendingOperationsSnapshot?> LoadAsync(CancellationToken cancellationToken = default)
        { cancellationToken.ThrowIfCancellationRequested(); return Task.FromResult(Snapshot); }
        public Task SaveAsync(FDriverPendingOperationsSnapshot snapshot, CancellationToken cancellationToken = default)
        { cancellationToken.ThrowIfCancellationRequested(); if (SaveFailure is not null) throw SaveFailure; json = JsonSerializer.Serialize(snapshot); return Task.CompletedTask; }
        public Task ClearAsync(string ownerId, CancellationToken cancellationToken = default)
        { cancellationToken.ThrowIfCancellationRequested(); if (Snapshot?.OwnerId == ownerId) json = null; return Task.CompletedTask; }
    }

    private static FDriverTestWorkspaceApi Api() => new()
    {
        WorkStatus = "운행중",
        Workspace = _ => Task.FromResult(new FoodDeliveryDriverWorkspaceDto
            { DriverId = "test-driver", UpdatedAtUtc = DateTime.UtcNow, MaxActiveDeliveries = 3 }),
        Availability = _ => Task.FromResult(new 운영배차수신상태Dto
            { 수신의사Code = 운영배차수신의사Code.On, 실효상태Code = 운영배차실효상태Code.배차가능 })
    };

    private static string RepoFile(string relativePath)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "Ssalddel.v3.5.slnx")))
                return Path.Combine(directory.FullName, relativePath.Replace('/', Path.DirectorySeparatorChar));
        throw new InvalidOperationException("Repository root was not found for food driver UI checks.");
    }

    private sealed class LocationFake : IFDriverLocationService
    {
        public bool IsListening { get; private set; }
        public FDriverLocationStatus Status => FDriverLocationStatus.Ready;
        public FDriverLocationSnapshot? LatestLocation { get; private set; } = new(37.5m, 127m, 5, DateTime.UtcNow);
        public event EventHandler<FDriverLocationSnapshot>? LocationChanged;
        public event EventHandler<string>? ListeningFailed { add { } remove { } }
        public Task<bool> StartListeningAsync(CancellationToken cancellationToken = default, bool requestPermission = false)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IsListening = true;
            return Task.FromResult(true);
        }
        public void StopListening() => IsListening = false;
        public Task<FDriverLocationSnapshot?> GetCurrentAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(LatestLocation);
        public void Emit(FDriverLocationSnapshot fix)
        {
            LatestLocation = fix;
            LocationChanged?.Invoke(this, fix);
        }
    }
}
