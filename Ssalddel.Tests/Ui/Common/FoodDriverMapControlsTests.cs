using System.Net;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using MudBlazor.Services;
using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Contracts.Common.Drivers;
using Ssalddel.Contracts.Common.Workflow;
using Ssalddel.Contracts.Driver.Food;
using Ssalddel.Contracts.Driver.Work;
using Ssalddel.Contracts.Food;
using Ssalddel.Ui.Common.Areas.App.Models;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Food;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.Tests.Ui.Common;

public sealed class FoodDriverMapControlsTests
{
    [Fact]
    public async Task 미응답자동중지는_조회와운행시작으로해제하지않고_수동ON만_해제의사를보낸다()
    {
        var api = new Api { AutomaticallyPaused = true }; var clock = new Clock();
        var adapter = new FoodDriverRoleWorkspaceAdapter(api, new Location(clock), clock);
        var paused = await adapter.LoadAsync(null, default);
        Assert.Contains(paused.EmptyActions!, action => action.Key == FoodDriverRoleWorkspaceAdapter.EnableDispatch);
        Assert.Equal(FoodDriverDispatchReceptionPolicy.AutomaticStopNotice, adapter.DispatchReceptionNotice);
        await adapter.LoadAsync(null, default); Assert.Empty(api.IntentAttempts);
        await adapter.PerformAsync(string.Empty, FoodDriverRoleWorkspaceAdapter.EnableDispatch, Guid.NewGuid(), default);
        Assert.True(Assert.Single(api.IntentAttempts).미응답자동중지해제);
        var resumed = await adapter.LoadAsync(null, default);
        Assert.Null(adapter.DispatchReceptionNotice);
        Assert.Contains(resumed.EmptyActions!, action => action.Key == FoodDriverRoleWorkspaceAdapter.DisableDispatch);

        var startingApi = new Api(onDuty: false, receiving: false) { AutomaticallyPaused = true };
        var starting = new FoodDriverRoleWorkspaceAdapter(startingApi, new Location(clock), clock);
        await starting.LoadAsync(null, default);
        await starting.PerformAsync(string.Empty, FoodDriverRoleWorkspaceAdapter.StartWork, Guid.NewGuid(), default);
        Assert.False(Assert.Single(startingApi.IntentAttempts).미응답자동중지해제);
        var started = await starting.LoadAsync(null, default);
        Assert.Equal(FoodDriverDispatchReceptionPolicy.AutomaticStopNotice, starting.DispatchReceptionNotice);
        Assert.Contains(started.EmptyActions!, action => action.Key == FoodDriverRoleWorkspaceAdapter.EnableDispatch && action.Enabled);
    }

    [Fact]
    public async Task 실제선택카드의_표시확인만전송하고_실패는다음정본에서재시도하며_새차수와계정은구분한다()
    {
        var clock = new Clock(); var api = new Api { FailReceipt = true };
        api.Recommendations = [ReceiptOffer("offer-a", 1, clock), ReceiptOffer("offer-b", 1, clock)];
        var adapter = new FoodDriverRoleWorkspaceAdapter(api, new Location(clock), clock);
        using var model = new RoleWorkspaceViewModel([adapter], new Access(), new RoleWorkspaceState(), clock);
        await model.InitializeAsync("food-driver", null);
        Assert.Empty(api.ReceiptAttempts); // 조회 자체는 화면 표시의 근거가 아닙니다.
        var first = model.SelectedItem!;
        await model.AcknowledgePresentedItemAsync(model.Items.Single(item => item.Id == "offer-b"));
        Assert.Empty(api.ReceiptAttempts);
        await model.AcknowledgePresentedItemAsync(first);
        await model.AcknowledgePresentedItemAsync(first);
        Assert.Equal(("offer-a", 1), Assert.Single(api.ReceiptAttempts));
        api.FailReceipt = false; await model.RefreshAsync();
        await model.AcknowledgePresentedItemAsync(first); Assert.Single(api.ReceiptAttempts); // 이전 DOM 참조
        await model.AcknowledgePresentedItemAsync(model.SelectedItem!);
        Assert.Equal(2, api.ReceiptAttempts.Count);
        await model.RefreshAsync(); await model.AcknowledgePresentedItemAsync(model.SelectedItem!);
        Assert.Equal(2, api.ReceiptAttempts.Count); // 성공한 동일 차수는 중복 전송하지 않습니다.
        api.Recommendations = [ReceiptOffer("offer-a", 2, clock)];
        await model.RefreshAsync(); await model.AcknowledgePresentedItemAsync(model.SelectedItem!);
        Assert.Equal(("offer-a", 2), api.ReceiptAttempts[2]);
        model.Suspend(); await model.AcknowledgePresentedItemAsync(model.SelectedItem!);
        Assert.Equal(3, api.ReceiptAttempts.Count);
        await model.ResumeAsync(); adapter.Clear(); await model.RefreshAsync();
        await model.AcknowledgePresentedItemAsync(model.SelectedItem!);
        Assert.Equal(4, api.ReceiptAttempts.Count); // 계정 초기화는 성공 캐시도 비웁니다.
        api.Recommendations = [ReceiptOffer("offer-a", 0, clock)];
        await model.RefreshAsync(); await model.AcknowledgePresentedItemAsync(model.SelectedItem!);
        api.Recommendations = [ReceiptOffer("offer-a", 3, clock, expired: true)];
        await model.RefreshAsync(); await model.AcknowledgePresentedItemAsync(model.SelectedItem!);
        Assert.Equal(4, api.ReceiptAttempts.Count);
    }

    private static FoodDeliveryDriverOfferDto ReceiptOffer(string id, int round, Clock clock, bool expired = false) => new()
    {
        OfferId = id, RecommendationRound = round, RestaurantName = "음식점",
        ExpiresAtUtc = clock.Now.AddMinutes(expired ? -1 : 5).UtcDateTime,
        AvailableActions = [new() { ActionId = 음식배달가능행동Ids.기사제안수락 }]
    };

    [Theory]
    [InlineData(true, true, false, 1)]
    [InlineData(true, false, true, 1)]
    [InlineData(true, false, false, 0)]
    [InlineData(false, true, false, 0)]
    public async Task 자동위치는_운행과수신또는진행배달에만_권한요청없이_전송한다(bool onDuty, bool receiving, bool active, int expected)
    {
        var api = new Api(onDuty, receiving, active); var location = new Location();
        var snapshot = await new FoodDriverRoleWorkspaceAdapter(api, location).LoadAsync(null, default);
        Assert.Equal(expected, location.AutomaticCalls); Assert.Equal(0, location.ExplicitCalls);
        Assert.Equal(expected, api.LocationPosts);
        Assert.DoesNotContain(snapshot.EmptyActions!, action => action.Key == FoodDriverRoleWorkspaceAdapter.UpdateLocation);
    }

    [Fact]
    public async Task 자동위치전송은_15초로제한하고_계정초기화후_이전위치를재사용하지않는다()
    {
        var api = new Api(); var clock = new Clock(); var location = new Location(clock);
        var adapter = new FoodDriverRoleWorkspaceAdapter(api, location, clock);
        await adapter.LoadAsync(null, default); await adapter.LoadAsync(null, default);
        Assert.Equal(1, api.LocationPosts);
        clock.Now = clock.Now.AddSeconds(15); await adapter.LoadAsync(null, default);
        Assert.Equal(2, api.LocationPosts);
        adapter.Clear(); await adapter.LoadAsync(null, default);
        Assert.Equal(3, api.LocationPosts); Assert.Equal(0, location.ExplicitCalls);
    }

    [Fact]
    public async Task 권한미허용은_업무와수신값을보존하고_명시적위치확인으로만_권한을요청한다()
    {
        var api = new Api(active: true); var location = new Location { AutomaticAllowed = false };
        var adapter = new FoodDriverRoleWorkspaceAdapter(api, location);
        var snapshot = await adapter.LoadAsync(null, default);
        Assert.Single(snapshot.Items); Assert.Contains("위치 확인", snapshot.Message);
        Assert.Contains(snapshot.EmptyActions!, action => action.Key == FoodDriverRoleWorkspaceAdapter.DisableDispatch);
        Assert.Equal("위치 확인", Assert.Single(snapshot.EmptyActions!, action => action.Key == FoodDriverRoleWorkspaceAdapter.UpdateLocation).Label);
        Assert.Equal(0, api.LocationPosts); Assert.Equal(0, location.ExplicitCalls);
        await adapter.PerformAsync(string.Empty, FoodDriverRoleWorkspaceAdapter.UpdateLocation, Guid.NewGuid(), default);
        Assert.Equal(1, location.ExplicitCalls); Assert.Equal(1, api.LocationPosts);
        Assert.DoesNotContain((await adapter.LoadAsync(null, default)).EmptyActions!, action => action.Key == FoodDriverRoleWorkspaceAdapter.UpdateLocation);
    }

    [Fact]
    public async Task 자동위치_호출자취소는_복구안내로삼키거나_서버전송하지않는다()
    {
        var api = new Api(); var location = new Location { AutomaticAllowed = false, IgnoreCancellation = true }; using var cancellation = new CancellationTokenSource();
        location.BeforeAutomatic = cancellation.Cancel;
        var adapter = new FoodDriverRoleWorkspaceAdapter(api, location);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => adapter.LoadAsync(null, cancellation.Token));
        Assert.Equal(0, api.LocationPosts); Assert.Equal(0, location.ExplicitCalls);
    }

    [Fact]
    public async Task 이전계정의_늦은위치예외는_새계정의확인위치와안내를_지우지않는다()
    {
        var api = new Api(active: true); var location = new Location();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var late = new TaskCompletionSource<RoleWorkspaceLocation?>(TaskCreationOptions.RunContinuationsAsynchronously);
        location.AutomaticRead = _ => { started.SetResult(); return late.Task; };
        var adapter = new FoodDriverRoleWorkspaceAdapter(api, location); using var previous = new CancellationTokenSource();
        var oldRead = adapter.LoadAsync(null, previous.Token); await started.Task;
        previous.Cancel(); adapter.Clear(); location.AutomaticRead = null;
        var current = await adapter.LoadAsync(null, default);
        Assert.Contains(Assert.Single(current.Items).Markers!, marker => marker.Kind == NeighborhoodMapMarkerKinds.Driver);
        late.SetException(new InvalidOperationException("이전 계정의 GPS 조회가 늦게 실패했습니다."));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => oldRead);
        var preserved = await adapter.LoadAsync(null, default);
        Assert.Contains(Assert.Single(preserved.Items).Markers!, marker => marker.Kind == NeighborhoodMapMarkerKinds.Driver);
        Assert.DoesNotContain(preserved.EmptyActions!, action => action.Key == FoodDriverRoleWorkspaceAdapter.UpdateLocation);
        Assert.DoesNotContain("위치 확인에서", preserved.Message ?? string.Empty);
        Assert.Equal(1, api.LocationPosts); Assert.Equal(2, location.AutomaticCalls);
    }

    [Fact]
    public async Task 진행배달이있으면_내정보종료행동과직접명령을_모두차단한다()
    {
        var api = new Api(active: true); var adapter = new FoodDriverRoleWorkspaceAdapter(api, new Location());
        var snapshot = await adapter.LoadAsync(null, default);
        var stop = Assert.Single(snapshot.EmptyActions!, action => action.Key == FoodDriverRoleWorkspaceAdapter.StopWork);
        Assert.False(stop.Enabled); Assert.NotNull(stop.DisabledReason);
        await Assert.ThrowsAsync<InvalidOperationException>(() => adapter.PerformAsync(string.Empty, stop.Key, Guid.NewGuid(), default));
        Assert.Equal(0, api.StopPosts);
    }

    [Fact]
    public async Task 토글은_기존수신의사API로요청하고_서버재조회값만_다음상태로표시한다()
    {
        var api = new Api(); var adapter = new FoodDriverRoleWorkspaceAdapter(api, new Location());
        var before = await adapter.LoadAsync(null, default);
        var toggle = Assert.Single(before.EmptyActions!, action => action.Key == FoodDriverRoleWorkspaceAdapter.DisableDispatch);
        Assert.False(toggle.RequiresConfirmation);
        await adapter.PerformAsync(string.Empty, toggle.Key, Guid.NewGuid(), default);
        Assert.Equal(운영배차수신의사Code.Off, Assert.Single(api.IntentWrites).수신의사Code);
        Assert.Contains((await adapter.LoadAsync(null, default)).EmptyActions!, action => action.Key == FoodDriverRoleWorkspaceAdapter.EnableDispatch);
        api.FailIntent = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => adapter.PerformAsync(string.Empty, FoodDriverRoleWorkspaceAdapter.EnableDispatch, Guid.NewGuid(), default));
        Assert.Contains((await adapter.LoadAsync(null, default)).EmptyActions!, action => action.Key == FoodDriverRoleWorkspaceAdapter.EnableDispatch);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task 수신응답유실은_자동조회전재명령을막고_자동정본확인후_원요청ID를정리한다(bool applied)
    {
        var api = new Api { FailIntent = !applied, ApplyThenFailIntent = applied }; var state = new RoleWorkspaceState(); var clock = new Clock();
        using var model = new RoleWorkspaceViewModel([new FoodDriverRoleWorkspaceAdapter(api, new Location(clock), clock)], new Access(), state, clock);
        await model.InitializeAsync("food-driver", null);
        var original = state.RequestId("food-driver", string.Empty, FoodDriverRoleWorkspaceAdapter.DisableDispatch);
        await model.PerformAsync(string.Empty, FoodDriverRoleWorkspaceAdapter.DisableDispatch);
        Assert.True(model.FoodDispatchStateUnconfirmed); Assert.Contains("자동으로 확인", model.CommandError);
        Assert.Equal(original, Assert.Single(api.IntentAttempts).클라이언트요청Id);
        await model.PerformAsync(string.Empty, FoodDriverRoleWorkspaceAdapter.DisableDispatch);
        await model.PerformAsync(string.Empty, FoodDriverRoleWorkspaceAdapter.EnableDispatch);
        Assert.Single(api.IntentAttempts);
        await model.TickAsync(); Assert.True(model.FoodDispatchStateUnconfirmed); Assert.Single(api.IntentAttempts);
        api.FailAvailability = true; clock.Now = clock.Now.AddSeconds(15); await model.TickAsync();
        Assert.True(model.FoodDispatchStateUnconfirmed); Assert.NotNull(model.CommandError);
        api.FailAvailability = false; clock.Now = clock.Now.AddSeconds(15); await model.TickAsync();
        Assert.False(model.FoodDispatchStateUnconfirmed); Assert.Null(model.CommandError); Assert.Null(model.Error); Assert.Null(model.RefreshError);
        Assert.Contains(model.EmptyActions, action => action.Key == (applied ? FoodDriverRoleWorkspaceAdapter.EnableDispatch : FoodDriverRoleWorkspaceAdapter.DisableDispatch));
        var next = state.RequestId("food-driver", string.Empty, FoodDriverRoleWorkspaceAdapter.DisableDispatch);
        if (applied) Assert.NotEqual(original, next);
        else
        {
            Assert.Equal(original, next); api.FailIntent = false;
            await model.PerformAsync(string.Empty, FoodDriverRoleWorkspaceAdapter.DisableDispatch);
            Assert.Equal(original, api.IntentAttempts[1].클라이언트요청Id);
            Assert.Contains(model.EmptyActions, action => action.Key == FoodDriverRoleWorkspaceAdapter.EnableDispatch);
        }
    }

    [Fact]
    public async Task 지도GPS와새배달은_수동새로고침없이_전경주기로갱신하고_연결실패후에도자동복구한다()
    {
        var api = new Api(); var clock = new Clock(); var location = new Location(clock);
        using var model = new RoleWorkspaceViewModel([new FoodDriverRoleWorkspaceAdapter(api, location, clock)], new Access(), new RoleWorkspaceState(), clock);
        await model.InitializeAsync("food-driver", null);
        Assert.Empty(model.Items); Assert.Equal(1, api.LocationPosts);
        api.Active = true; location.Latitude = 37.561;
        clock.Now = clock.Now.AddSeconds(15); await model.TickAsync();
        var current = Assert.Single(model.Items);
        Assert.True(current.IsCurrent);
        Assert.Equal(37.561, Assert.Single(current.Markers!, marker => marker.Kind == NeighborhoodMapMarkerKinds.Driver).Latitude);
        Assert.Equal(2, api.LocationPosts); Assert.Equal(0, location.ExplicitCalls);
        api.FailAvailability = true; clock.Now = clock.Now.AddSeconds(15); await model.TickAsync();
        Assert.NotNull(model.RefreshError); Assert.Single(model.Items); Assert.Equal(2, api.LocationPosts);
        api.FailAvailability = false; location.Latitude = 37.562;
        clock.Now = clock.Now.AddSeconds(15); await model.TickAsync();
        Assert.Null(model.RefreshError); Assert.Null(model.Error);
        Assert.Equal(37.562, Assert.Single(Assert.Single(model.Items).Markers!, marker => marker.Kind == NeighborhoodMapMarkerKinds.Driver).Latitude);
        Assert.Equal(3, api.LocationPosts); Assert.Equal(0, location.ExplicitCalls);
    }

    [Fact]
    public async Task 운행시작한번으로_수신ON을연결하고_그뒤명시OFF는자동으로뒤집지않는다()
    {
        var api = new Api(onDuty: false, receiving: false); var adapter = new FoodDriverRoleWorkspaceAdapter(api, new Location());
        var before = await adapter.LoadAsync(null, default); var requestId = Guid.NewGuid();
        Assert.False(Assert.Single(before.EmptyActions!, action => action.Key == FoodDriverRoleWorkspaceAdapter.StartWork).RequiresConfirmation);
        await adapter.PerformAsync(string.Empty, FoodDriverRoleWorkspaceAdapter.StartWork, requestId, default);
        Assert.Equal(1, api.StartPosts); Assert.True(adapter.StartReceptionPending);
        var enabled = Assert.Single(api.IntentAttempts); Assert.Equal(requestId, Assert.Single(api.StartRequests).ClientRequestId);
        Assert.NotEqual(requestId, enabled.클라이언트요청Id); Assert.Equal(운영배차수신의사Code.On, enabled.수신의사Code);
        var started = await adapter.LoadAsync(null, default);
        Assert.False(adapter.StartReceptionPending); Assert.True(adapter.StartReceptionReady);
        Assert.True(Assert.Single(started.EmptyActions!, action => action.Key == FoodDriverRoleWorkspaceAdapter.StopWork).RequiresConfirmation);
        Assert.Contains(started.EmptyActions!, action => action.Key == FoodDriverRoleWorkspaceAdapter.DisableDispatch && action.Enabled);
        await adapter.PerformAsync(string.Empty, FoodDriverRoleWorkspaceAdapter.DisableDispatch, Guid.NewGuid(), default);
        var stoppedReception = await adapter.LoadAsync(null, default); await adapter.LoadAsync(null, default);
        Assert.Contains(stoppedReception.EmptyActions!, action => action.Key == FoodDriverRoleWorkspaceAdapter.EnableDispatch && action.Enabled);
        Assert.Equal(1, api.StartPosts); Assert.Equal(2, api.IntentAttempts.Count);
        Assert.Equal(운영배차수신의사Code.Off, api.IntentAttempts[1].수신의사Code);
    }

    [Theory]
    [InlineData(400)]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(409)]
    public async Task 운행시작확정거절은_pending을해제하고_다른요청의기존OFF를ON으로바꾸지않는다(int status)
    {
        var api = new Api(onDuty: false, receiving: false) { StartFailureStatus = status };
        var adapter = new FoodDriverRoleWorkspaceAdapter(api, new Location());
        await adapter.LoadAsync(null, default);
        var error = await Assert.ThrowsAsync<RoleWorkspaceAccessException>(() => adapter.PerformAsync(string.Empty,
            FoodDriverRoleWorkspaceAdapter.StartWork, Guid.NewGuid(), default));
        Assert.Equal(status, error.StatusCode); Assert.False(adapter.StartReceptionPending);
        await adapter.LoadAsync(null, default); await adapter.LoadAsync(null, default);
        Assert.Equal(1, api.StartPosts); Assert.Empty(api.IntentAttempts);
        Assert.False(adapter.StartReceptionPending); Assert.False(adapter.StartReceptionReady);
    }

    [Fact]
    public async Task 운행시작409오류는_미확정으로표시하지않고_정본자동조회뒤에도_거절안내를유지한다()
    {
        var api = new Api(onDuty: false, receiving: false) { StartFailureStatus = 409 }; var clock = new Clock();
        using var model = new RoleWorkspaceViewModel([new FoodDriverRoleWorkspaceAdapter(api, new Location(clock), clock)],
            new Access(), new RoleWorkspaceState(), clock);
        await model.InitializeAsync("food-driver", null); await model.PerformAsync(string.Empty, FoodDriverRoleWorkspaceAdapter.StartWork);
        Assert.False(model.FoodStartStateUnconfirmed); Assert.Equal("운행 시작 거절", model.CommandError);
        clock.Now = clock.Now.AddSeconds(15); await model.TickAsync();
        Assert.Equal("운행 시작 거절", model.CommandError); Assert.Empty(api.IntentAttempts); Assert.Equal(1, api.StartPosts);
    }

    [Theory]
    [InlineData("different", false)]
    [InlineData("different", true)]
    [InlineData("missing", false)]
    [InlineData("missing", true)]
    [InlineData("cargo", false)]
    [InlineData("cargo", true)]
    public async Task 시작성공과응답유실모두_내음식운행정본과일치하지않으면_자동ON을하지않는다(string mismatch, bool responseLost)
    {
        var api = new Api(onDuty: false, receiving: false) { ApplyThenFailStart = responseLost };
        if (mismatch == "different") api.StartIdentityOverride = Guid.NewGuid();
        if (mismatch == "missing") api.OmitStartIdentity = true;
        if (mismatch == "cargo") api.ExecutionType = "Cargo";
        var clock = new Clock(); using var model = new RoleWorkspaceViewModel(
            [new FoodDriverRoleWorkspaceAdapter(api, new Location(clock), clock)], new Access(), new RoleWorkspaceState(), clock);
        await model.InitializeAsync("food-driver", null); await model.PerformAsync(string.Empty, FoodDriverRoleWorkspaceAdapter.StartWork);
        clock.Now = clock.Now.AddSeconds(15); await model.TickAsync();
        Assert.False(model.FoodStartStateUnconfirmed); Assert.Equal(FoodDriverWorkRecoveryPolicy.StartOwnershipNotice, model.CommandError);
        Assert.Empty(api.IntentAttempts); Assert.Equal(1, api.StartPosts);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task 운행시작응답유실은_시작을재전송하지않고_운행정본이확인된경우에만_수신ON을연결한다(bool applied)
    {
        var api = new Api(onDuty: false, receiving: false) { FailStart = !applied, ApplyThenFailStart = applied }; var clock = new Clock();
        var adapter = new FoodDriverRoleWorkspaceAdapter(api, new Location(clock), clock); var state = new RoleWorkspaceState();
        using var model = new RoleWorkspaceViewModel([adapter], new Access(), state, clock);
        await model.InitializeAsync("food-driver", null);
        var original = state.RequestId("food-driver", string.Empty, FoodDriverRoleWorkspaceAdapter.StartWork);
        await model.PerformAsync(string.Empty, FoodDriverRoleWorkspaceAdapter.StartWork);
        Assert.True(model.FoodStartStateUnconfirmed); Assert.Contains("자동으로 확인", model.CommandError);
        await model.PerformAsync(string.Empty, FoodDriverRoleWorkspaceAdapter.StartWork);
        await model.PerformAsync(string.Empty, FoodDriverRoleWorkspaceAdapter.EnableDispatch);
        Assert.Equal(1, api.StartPosts); Assert.Empty(api.IntentAttempts);
        clock.Now = clock.Now.AddSeconds(15); await model.TickAsync();
        Assert.False(model.FoodStartStateUnconfirmed); Assert.Null(model.CommandError); Assert.Null(model.RefreshError);
        Assert.Equal(1, api.StartPosts); Assert.Equal(applied ? 1 : 0, api.IntentAttempts.Count);
        if (applied)
        {
            Assert.Equal(original, Assert.Single(api.StartRequests).ClientRequestId);
            Assert.NotEqual(original, Assert.Single(api.IntentAttempts).클라이언트요청Id); Assert.True(adapter.StartReceptionReady);
            Assert.Contains(model.EmptyActions, action => action.Key == FoodDriverRoleWorkspaceAdapter.DisableDispatch);
        }
        else Assert.Contains(model.EmptyActions, action => action.Key == FoodDriverRoleWorkspaceAdapter.StartWork && action.Enabled);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task 운행시작뒤수신ON응답유실은_같은요청ID로자동복구하며_운행시작을중복하지않는다(bool applied)
    {
        var api = new Api(onDuty: false, receiving: false) { FailIntent = !applied, ApplyThenFailIntent = applied }; var clock = new Clock();
        var state = new RoleWorkspaceState(); using var model = new RoleWorkspaceViewModel([new FoodDriverRoleWorkspaceAdapter(api, new Location(clock), clock)], new Access(), state, clock);
        await model.InitializeAsync("food-driver", null);
        var original = state.RequestId("food-driver", string.Empty, FoodDriverRoleWorkspaceAdapter.StartWork);
        await model.PerformAsync(string.Empty, FoodDriverRoleWorkspaceAdapter.StartWork);
        Assert.True(model.FoodStartStateUnconfirmed); Assert.Equal(original, Assert.Single(api.StartRequests).ClientRequestId);
        var receptionRequestId = Assert.Single(api.IntentAttempts).클라이언트요청Id;
        Assert.NotEqual(original, receptionRequestId);
        api.FailIntent = api.ApplyThenFailIntent = false;
        clock.Now = clock.Now.AddSeconds(15); await model.TickAsync();
        Assert.False(model.FoodStartStateUnconfirmed); Assert.Null(model.CommandError); Assert.Null(model.RefreshError);
        Assert.Contains(model.EmptyActions, action => action.Key == FoodDriverRoleWorkspaceAdapter.DisableDispatch && action.Enabled);
        Assert.Equal(1, api.StartPosts); Assert.Equal(applied ? 1 : 2, api.IntentAttempts.Count);
        Assert.All(api.IntentAttempts, request => Assert.Equal(receptionRequestId, request.클라이언트요청Id));
    }

    [Fact]
    public async Task 계정초기화는_이전운행시작의수신ON재시도를_새계정으로전달하지않는다()
    {
        var api = new Api(onDuty: false, receiving: false) { ApplyThenFailStart = true };
        var adapter = new FoodDriverRoleWorkspaceAdapter(api, new Location()); await adapter.LoadAsync(null, default);
        await Assert.ThrowsAsync<InvalidOperationException>(() => adapter.PerformAsync(string.Empty, FoodDriverRoleWorkspaceAdapter.StartWork, Guid.NewGuid(), default));
        Assert.True(adapter.StartReceptionPending); adapter.Clear(); await adapter.LoadAsync(null, default);
        Assert.False(adapter.StartReceptionPending); Assert.Empty(api.IntentAttempts);
    }

    [Fact]
    public async Task 운행시작뒤알수없는운행상태는_시작재전송이나수신ON을실행하지않고_자동확인을유지한다()
    {
        var api = new Api(onDuty: false, receiving: false) { ApplyThenFailStart = true }; var clock = new Clock();
        using var model = new RoleWorkspaceViewModel([new FoodDriverRoleWorkspaceAdapter(api, new Location(clock), clock)], new Access(), new RoleWorkspaceState(), clock);
        await model.InitializeAsync("food-driver", null); await model.PerformAsync(string.Empty, FoodDriverRoleWorkspaceAdapter.StartWork);
        api.StatusOverride = ""; clock.Now = clock.Now.AddSeconds(15); await model.TickAsync();
        Assert.True(model.FoodStartStateUnconfirmed); Assert.Equal(1, api.StartPosts); Assert.Empty(api.IntentAttempts);
        Assert.All(model.EmptyActions.Where(action => action.Key != FoodDriverRoleWorkspaceAdapter.UpdateLocation), action => Assert.False(action.Enabled));
        api.StatusOverride = null; clock.Now = clock.Now.AddSeconds(15); await model.TickAsync();
        Assert.False(model.FoodStartStateUnconfirmed); Assert.Equal(1, api.StartPosts); Assert.Single(api.IntentAttempts);
    }

    [Fact]
    public async Task 같은계정의화면숨김과복귀는_미확정시작의수신ON요청ID를보존하고_시작을중복하지않는다()
    {
        var api = new Api(onDuty: false, receiving: false) { FailIntent = true }; var clock = new Clock(); var state = new RoleWorkspaceState();
        using var model = new RoleWorkspaceViewModel([new FoodDriverRoleWorkspaceAdapter(api, new Location(clock), clock)], new Access(), state, clock);
        await model.InitializeAsync("food-driver", null);
        var original = state.RequestId("food-driver", string.Empty, FoodDriverRoleWorkspaceAdapter.StartWork);
        await model.PerformAsync(string.Empty, FoodDriverRoleWorkspaceAdapter.StartWork);
        var receptionRequestId = Assert.Single(api.IntentAttempts).클라이언트요청Id;
        Assert.Equal(original, Assert.Single(api.StartRequests).ClientRequestId); Assert.NotEqual(original, receptionRequestId); model.Suspend();
        Assert.True(model.FoodStartStateUnconfirmed); Assert.Empty(model.Items);
        clock.Now = clock.Now.AddSeconds(15); await model.TickAsync(); Assert.Single(api.IntentAttempts);
        api.FailIntent = false; await model.ResumeAsync();
        Assert.False(model.FoodStartStateUnconfirmed); Assert.Null(model.Error); Assert.Equal(1, api.StartPosts);
        Assert.Equal(2, api.IntentAttempts.Count); Assert.All(api.IntentAttempts, request => Assert.Equal(receptionRequestId, request.클라이언트요청Id));
        Assert.Contains(model.EmptyActions, action => action.Key == FoodDriverRoleWorkspaceAdapter.DisableDispatch && action.Enabled);
    }

    [Fact]
    public async Task 숨긴화면의늦은운행시작응답은_ON을실행하지않고_복귀한정본으로만연결한다()
    {
        var api = new Api(onDuty: false, receiving: false) { StartResponseGate = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        var state = new RoleWorkspaceState(); using var model = new RoleWorkspaceViewModel([new FoodDriverRoleWorkspaceAdapter(api, new Location())], new Access(), state);
        await model.InitializeAsync("food-driver", null);
        var original = state.RequestId("food-driver", string.Empty, FoodDriverRoleWorkspaceAdapter.StartWork);
        var start = model.PerformAsync(string.Empty, FoodDriverRoleWorkspaceAdapter.StartWork);
        Assert.True(model.FoodStartStateUnconfirmed); Assert.Equal(1, api.StartPosts);
        model.Suspend(); api.StartResponseGate.SetResult(); await start;
        Assert.True(model.FoodStartStateUnconfirmed); Assert.Empty(api.IntentAttempts);
        await model.ResumeAsync();
        Assert.False(model.FoodStartStateUnconfirmed); Assert.Equal(1, api.StartPosts);
        Assert.Equal(original, Assert.Single(api.StartRequests).ClientRequestId);
        Assert.NotEqual(original, Assert.Single(api.IntentAttempts).클라이언트요청Id);
    }

    [Theory]
    [InlineData("")]
    [InlineData("알수없는상태")]
    public async Task 초기미확인운행상태는_종료로간주하지않고_운행과수신을잠근뒤_자동정본조회로복구한다(string status)
    {
        var api = new Api(onDuty: false, receiving: false, active: true) { StatusOverride = status }; var clock = new Clock();
        var adapter = new FoodDriverRoleWorkspaceAdapter(api, new Location(clock), clock);
        using var model = new RoleWorkspaceViewModel([adapter], new Access(), new RoleWorkspaceState(), clock);
        await model.InitializeAsync("food-driver", null);
        Assert.Single(model.Items); Assert.Contains("운행 상태를 자동으로 확인", model.Message);
        Assert.All(model.EmptyActions, action => Assert.False(action.Enabled));
        await Assert.ThrowsAsync<InvalidOperationException>(() => adapter.PerformAsync(string.Empty, FoodDriverRoleWorkspaceAdapter.StartWork, Guid.NewGuid(), default));
        Assert.Equal(0, api.StartPosts); Assert.Empty(api.IntentAttempts);
        api.StatusOverride = null; clock.Now = clock.Now.AddSeconds(15); await model.TickAsync();
        Assert.Contains(model.EmptyActions, action => action.Key == FoodDriverRoleWorkspaceAdapter.StartWork && action.Enabled);
        Assert.Contains(model.EmptyActions, action => action.Key == FoodDriverRoleWorkspaceAdapter.EnableDispatch && action.Enabled);
        Assert.Equal(0, api.StartPosts); Assert.Empty(api.IntentAttempts);
    }

    [Fact]
    public async Task 시작명령뒤정본조회가일시실패해도_시작미확정을보존하고_다음자동조회로복구한다()
    {
        var api = new Api(onDuty: false, receiving: false) { BreakWorkspaceAfterIntent = true }; var clock = new Clock();
        using var model = new RoleWorkspaceViewModel([new FoodDriverRoleWorkspaceAdapter(api, new Location(clock), clock)], new Access(), new RoleWorkspaceState(), clock);
        await model.InitializeAsync("food-driver", null); await model.PerformAsync(string.Empty, FoodDriverRoleWorkspaceAdapter.StartWork);
        Assert.True(model.FoodStartStateUnconfirmed); Assert.NotNull(model.Error); Assert.Empty(model.Items);
        await model.PerformAsync(string.Empty, FoodDriverRoleWorkspaceAdapter.StartWork); Assert.Equal(1, api.StartPosts);
        api.FailWorkspace = false; clock.Now = clock.Now.AddSeconds(15); await model.TickAsync();
        Assert.False(model.FoodStartStateUnconfirmed); Assert.Null(model.Error); Assert.Null(model.RefreshError);
        Assert.Equal(1, api.StartPosts); Assert.Single(api.IntentAttempts);
        Assert.Contains(model.EmptyActions, action => action.Key == FoodDriverRoleWorkspaceAdapter.DisableDispatch && action.Enabled);
    }

    [Theory]
    [InlineData("account")]
    [InlineData("role")]
    [InlineData("logout")]
    [InlineData("dispose")]
    public async Task 계정역할로그아웃종료는_미확정운행시작을_다음문맥으로전달하지않는다(string reset)
    {
        var api = new Api(onDuty: false, receiving: false) { FailIntent = true }; var access = new Access();
        var adapter = new FoodDriverRoleWorkspaceAdapter(api, new Location());
        using var model = new RoleWorkspaceViewModel([adapter, new OtherAdapter("orderer")], access, new RoleWorkspaceState());
        await model.InitializeAsync("food-driver", null); await model.PerformAsync(string.Empty, FoodDriverRoleWorkspaceAdapter.StartWork);
        Assert.True(adapter.StartReceptionPending);
        if (reset == "account") access.ChangeOwner("owner-b");
        else if (reset == "role") await model.InitializeAsync("orderer", null);
        else if (reset == "logout") await model.SignOutAsync();
        else model.Dispose();
        Assert.False(adapter.StartReceptionPending); Assert.Single(api.IntentAttempts); Assert.Equal(1, api.StartPosts);
    }

    [Theory]
    [InlineData(500)]
    [InlineData(503)]
    public async Task 자동GPS전송의일시서버오류는_업무카드를막지않고_위치복구만안내한다(int status)
    {
        var api = new Api(active: true) { LocationFailureStatus = status }; var clock = new Clock();
        using var model = new RoleWorkspaceViewModel([new FoodDriverRoleWorkspaceAdapter(api, new Location(clock), clock)], new Access(), new RoleWorkspaceState(), clock);
        await model.InitializeAsync("food-driver", null);
        Assert.Single(model.Items); Assert.Null(model.Error); Assert.Null(model.RefreshError);
        Assert.Contains(model.EmptyActions, action => action.Key == FoodDriverRoleWorkspaceAdapter.UpdateLocation);
        Assert.Contains("위치 확인", model.Message);
        api.LocationFailureStatus = null; clock.Now = clock.Now.AddSeconds(15); await model.TickAsync();
        Assert.Single(model.Items); Assert.Null(model.Error); Assert.Null(model.RefreshError);
        Assert.DoesNotContain(model.EmptyActions, action => action.Key == FoodDriverRoleWorkspaceAdapter.UpdateLocation);
    }

    [Theory]
    [InlineData(401)]
    [InlineData(403)]
    public async Task 자동GPS전송의인증권한오류는_기존업무와위치정보를차단한다(int status)
    {
        var api = new Api(active: true) { LocationFailureStatus = status };
        using var model = new RoleWorkspaceViewModel([new FoodDriverRoleWorkspaceAdapter(api, new Location())], new Access(), new RoleWorkspaceState());
        await model.InitializeAsync("food-driver", null);
        Assert.Empty(model.Items); Assert.Empty(model.EmptyActions); Assert.NotNull(model.Error);
        Assert.Equal(status == 401, model.RequiresLogin); Assert.Equal(status == 403, model.AccessDenied);
    }

    [Fact]
    public async Task 수신과무관한명령오류는_정본재조회로_지우지않는다()
    {
        var api = new Api { FailStop = true };
        using var model = new RoleWorkspaceViewModel([new FoodDriverRoleWorkspaceAdapter(api, new Location())], new Access(), new RoleWorkspaceState());
        await model.InitializeAsync("food-driver", null); await model.PerformAsync(string.Empty, FoodDriverRoleWorkspaceAdapter.StopWork);
        var error = model.CommandError; Assert.NotNull(error); Assert.False(model.FoodDispatchStateUnconfirmed);
        await model.RefreshAsync(); Assert.Equal(error, model.CommandError);
    }

    [Fact]
    public async Task 미확정수신의_정본복구도_그뒤발생한별도명령오류를_지우지않는다()
    {
        var api = new Api { FailIntent = true, FailStop = true };
        using var model = new RoleWorkspaceViewModel([new FoodDriverRoleWorkspaceAdapter(api, new Location())], new Access(), new RoleWorkspaceState());
        await model.InitializeAsync("food-driver", null); await model.PerformAsync(string.Empty, FoodDriverRoleWorkspaceAdapter.DisableDispatch);
        Assert.True(model.FoodDispatchStateUnconfirmed);
        await model.PerformAsync(string.Empty, FoodDriverRoleWorkspaceAdapter.StopWork);
        var error = model.CommandError; Assert.NotNull(error); Assert.DoesNotContain("신규 배차", error);
        await model.RefreshAsync(); Assert.False(model.FoodDispatchStateUnconfirmed); Assert.Equal(error, model.CommandError);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 음식기사_토글은지도좌상단또는목록상단이고_운행종료는내정보맨아래에만_있다(bool listOnly)
    {
        var html = await RenderAsync(new Api(), new Location(), listOnly);
        Assert.Contains("role=\"switch\"", html); Assert.Contains("aria-checked=\"true\"", html);
        Assert.Contains(listOnly ? "role-map-workspace__driver-list-control" : "role-map-workspace__driver-map-control", html);
        Assert.DoesNotContain("현재 위치 갱신", html); Assert.DoesNotContain("역할 업무 설정", html);
        Assert.DoesNotContain("새로고침", html); Assert.DoesNotContain("role-map-workspace__refresh", html);
        var profile = html.IndexOf("role-map-workspace__profile", StringComparison.Ordinal);
        Assert.True(profile >= 0); Assert.DoesNotContain("운행 종료", html[..profile]);
        Assert.Contains("내 정보", html[profile..]);
        Assert.True(html.IndexOf("운행 종료", profile, StringComparison.Ordinal) > html.IndexOf("로그아웃", profile, StringComparison.Ordinal));
    }

    [Fact]
    public async Task 운행시작미확정은_자동확인안내와함께_시작버튼과수신토글을비활성화한다()
    {
        var api = new Api(onDuty: false, receiving: false) { ApplyThenFailStart = true };
        using var model = new RoleWorkspaceViewModel([new FoodDriverRoleWorkspaceAdapter(api, new Location())], new Access(), new RoleWorkspaceState());
        await model.InitializeAsync("food-driver", null); await model.PerformAsync(string.Empty, FoodDriverRoleWorkspaceAdapter.StartWork);
        var html = await RenderInitializedAsync(model);
        Assert.Contains("운행 시작 결과와 신규 배차 수신 상태를 자동으로 확인", html);
        Assert.DoesNotContain("새로고침", html); Assert.Contains("확인 필요", html);
        var buttons = System.Text.RegularExpressions.Regex.Matches(html, "<button(?<attributes>[^>]*)>(?<body>.*?)</button>", System.Text.RegularExpressions.RegexOptions.Singleline);
        var start = Assert.Single(buttons.Cast<System.Text.RegularExpressions.Match>(), button => button.Groups["body"].Value.Contains("운행 시작", StringComparison.Ordinal));
        Assert.Contains("disabled", start.Groups["attributes"].Value);
    }

    [Theory]
    [InlineData("orderer")]
    [InlineData("restaurant")]
    [InlineData("cargo-driver")]
    public async Task 다른역할의_정상새로고침행동은_유지한다(string roleKey)
    {
        using var model = new RoleWorkspaceViewModel([new OtherAdapter(roleKey)], new Access(), new RoleWorkspaceState());
        await model.InitializeAsync(roleKey, null);
        Assert.Contains("role-map-workspace__refresh", await RenderInitializedAsync(model));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void 기사자동확인옵션은_정상행동권한을유지하고_음식점기본만료안내를변경하지않는다(bool expired)
    {
        업무가능행동Dto[] actions = [new() { ActionId = "action-a", ExpiresAtUtc = DateTime.UtcNow.AddMinutes(expired ? -1 : 1) }];
        var driver = Assert.IsType<RoleWorkspaceAction>(FoodWorkspacePresentation.Available(actions, "action-a", "음식점 도착", true, automaticRefresh: true));
        var restaurant = Assert.IsType<RoleWorkspaceAction>(FoodWorkspacePresentation.Available(actions, "action-a", "조리 시작", true));
        Assert.Equal(!expired, driver.Enabled); Assert.Equal(driver.Enabled, restaurant.Enabled);
        Assert.True(driver.IsPrimary); Assert.True(restaurant.IsPrimary);
        if (expired)
        {
            Assert.Contains("자동으로 확인", driver.DisabledReason); Assert.DoesNotContain("새로고침", driver.DisabledReason);
            Assert.Contains("새로고침", restaurant.DisabledReason);
        }
        else
        {
            Assert.Null(driver.DisabledReason); Assert.Null(restaurant.DisabledReason);
            Assert.Same(actions[0], FoodWorkspacePresentation.Require(actions, "action-a", automaticRefresh: true));
            Assert.Same(actions[0], FoodWorkspacePresentation.Require(actions, "action-a"));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void 불가하거나만료된행동은_기사자동확인과다른역할기본안내를구별하고_실행을동일하게차단한다(bool expired)
    {
        업무가능행동Dto[] actions = expired ? [new() { ActionId = "action-a", ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1) }] : [];
        var driver = Assert.Throws<InvalidOperationException>(() => FoodWorkspacePresentation.Require(actions, "action-a", automaticRefresh: true));
        var restaurant = Assert.Throws<InvalidOperationException>(() => FoodWorkspacePresentation.Require(actions, "action-a"));
        Assert.Contains("자동으로 확인", driver.Message); Assert.DoesNotContain("새로고침", driver.Message);
        Assert.Contains("새로고침", restaurant.Message);
    }

    [Theory]
    [InlineData(FoodDriverStage.PickupTravel)]
    [InlineData(FoodDriverStage.PickupWaiting)]
    public void 기사수행단계의_도착과픽업가능여부는_수동새로고침대신_자동확인으로안내한다(FoodDriverStage stage)
    {
        var guide = FoodDriverStagePresentation.PickupGuide(new() { DeliveryAttemptId = "attempt-a", CurrentPickupReadyAtUtc = DateTime.UtcNow }, stage);
        Assert.Contains("자동으로 확인", guide); Assert.DoesNotContain("새로고침", guide);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 현재수행화면의_정상과만료행동에도_수동새로고침안내가없다(bool expired)
    {
        var api = new Api(active: true)
        {
            ActiveActions = [new() { ActionId = 음식배달가능행동Ids.기사가게도착, ExpiresAtUtc = DateTime.UtcNow.AddMinutes(expired ? -1 : 1) }]
        };
        var html = await RenderAsync(api, new Location(), false);
        Assert.Contains("가게 도착", html); Assert.DoesNotContain("새로고침", html);
        if (expired) Assert.Contains("자동으로 확인", html);
    }

    [Theory]
    [InlineData(true, false, "true", false)]
    [InlineData(false, false, "false", false)]
    [InlineData(true, true, "false", true)]
    public async Task 토글_미확인과처리중은_서버값을낙관적으로켜지않는다(bool receiving, bool unknown, string expected, bool disabled)
    {
        var services = new ServiceCollection(); services.AddLogging();
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        var html = WebUtility.HtmlDecode(await renderer.Dispatcher.InvokeAsync(async () => (await renderer.RenderComponentAsync<FoodDriverDispatchToggle>(ParameterView.FromDictionary(new Dictionary<string, object?>
        {
            [nameof(FoodDriverDispatchToggle.Action)] = new RoleWorkspaceAction(receiving ? FoodDriverRoleWorkspaceAdapter.DisableDispatch : FoodDriverRoleWorkspaceAdapter.EnableDispatch, "신규 배차 받기"),
            [nameof(FoodDriverDispatchToggle.IsStateKnown)] = !unknown
        }))).ToHtmlString()));
        Assert.Contains($"aria-checked=\"{expected}\"", html); Assert.Equal(disabled, html.Contains(" disabled", StringComparison.Ordinal));
        if (unknown) Assert.Contains("확인 필요", html);
    }

    private static async Task<string> RenderAsync(Api api, Location location, bool listOnly)
    {
        using var model = new RoleWorkspaceViewModel([new FoodDriverRoleWorkspaceAdapter(api, location)], new Access(), new RoleWorkspaceState());
        await model.InitializeAsync("food-driver", null); model.SetListOnly(listOnly);
        return await RenderInitializedAsync(model);
    }

    private static async Task<string> RenderInitializedAsync(RoleWorkspaceViewModel model)
    {
        var services = new ServiceCollection(); services.AddLogging(); services.AddMudServices(); services.AddSingleton(model);
        services.AddSingleton<NavigationManager, Navigation>(); services.AddSingleton<INeighborhoodMapHost, MapHost>(); services.AddSingleton<IJSRuntime, StaticJs>();
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        return WebUtility.HtmlDecode(await renderer.Dispatcher.InvokeAsync(async () => (await renderer.RenderComponentAsync<RoleMapWorkspace>(ParameterView.FromDictionary(new Dictionary<string, object?>
        { [nameof(RoleMapWorkspace.RoleKey)] = model.Role!.Key }))).ToHtmlString()));
    }

    private sealed class Clock : TimeProvider { public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow; public override DateTimeOffset GetUtcNow() => Now; }
    private sealed class Location(Clock? clock = null) : IRoleWorkspaceLocationProvider
    {
        public int AutomaticCalls, ExplicitCalls; public bool AutomaticAllowed = true, IgnoreCancellation; public Action? BeforeAutomatic;
        public double Latitude { get; set; } = 37.56;
        public Func<CancellationToken, Task<RoleWorkspaceLocation?>>? AutomaticRead;
        public Task<RoleWorkspaceLocation?> GetCurrentAsync(CancellationToken token) { ExplicitCalls++; return Task.FromResult<RoleWorkspaceLocation?>(new(37.56, 127.07, 5, clock?.GetUtcNow() ?? DateTimeOffset.UtcNow)); }
        public Task<RoleWorkspaceLocation?> GetCurrentWithoutPermissionPromptAsync(CancellationToken token)
        { AutomaticCalls++; if (AutomaticRead is not null) return AutomaticRead(token); BeforeAutomatic?.Invoke(); if (!IgnoreCancellation) token.ThrowIfCancellationRequested(); return Task.FromResult<RoleWorkspaceLocation?>(AutomaticAllowed ? new(Latitude, 127.07, 5, clock?.GetUtcNow() ?? DateTimeOffset.UtcNow) : null); }
    }
    private sealed class Api(bool onDuty = true, bool receiving = true, bool active = false) : IRoleWorkspaceApi
    {
        public int LocationPosts, StopPosts, StartPosts; public bool FailIntent, ApplyThenFailIntent, FailAvailability, FailStop, FailStart, ApplyThenFailStart, FailWorkspace, BreakWorkspaceAfterIntent;
        public bool AutomaticallyPaused, FailReceipt;
        public List<FoodDeliveryDriverOfferDto> Recommendations { get; set; } = [];
        public List<(string OfferId, int Round)> ReceiptAttempts { get; } = [];
        public bool Active { get; set; } = active;
        public string? StatusOverride { get; set; }
        public int? LocationFailureStatus { get; set; }
        public int? StartFailureStatus { get; set; }
        public Guid? StartIdentityOverride { get; set; }
        public bool OmitStartIdentity { get; set; }
        public string? ExecutionType { get; set; } = "FoodDelivery";
        public TaskCompletionSource? StartResponseGate { get; set; }
        public List<업무가능행동Dto> ActiveActions { get; set; } = [];
        public List<운영배차수신의사변경요청> IntentWrites { get; } = [];
        public List<운영배차수신의사변경요청> IntentAttempts { get; } = [];
        public List<기사운행시작요청> StartRequests { get; } = [];
        public Task<T> GetAsync<T>(string roleKey, string path, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (FailAvailability && path.EndsWith("availability", StringComparison.Ordinal)) throw new InvalidOperationException("수신 상태 조회 실패");
            if (FailWorkspace && path.EndsWith("workspace/current", StringComparison.Ordinal)) throw new InvalidOperationException("업무 상태 조회 실패");
            object result = path.EndsWith("work/status", StringComparison.Ordinal) ? new 기사운행상태응답 { Status = StatusOverride ?? (onDuty ? "운행중" : "운행종료"),
                WorkStartClientRequestId = OmitStartIdentity ? null : StartIdentityOverride ?? StartRequests.LastOrDefault()?.ClientRequestId,
                운송실행유형 = ExecutionType }
                : path.EndsWith("availability", StringComparison.Ordinal) ? new 운영배차수신상태Dto { 수신의사Code = receiving ? 운영배차수신의사Code.On : 운영배차수신의사Code.Off,
                    미응답자동중지여부 = AutomaticallyPaused, 연속미응답횟수 = AutomaticallyPaused ? 3 : 0 }
                : new FoodDeliveryDriverCurrentWorkDto { Recommendations = Recommendations,
                    ActiveDeliveries = Active ? [new() { OfferId = "offer-a", RestaurantName = "음식점", DeliveryAttemptId = "attempt-a", AvailableActions = ActiveActions }] : [] };
            return Task.FromResult((T)result);
        }
        public async Task<T?> PostAsync<T>(string roleKey, string path, object body, CancellationToken token)
        {
            token.ThrowIfCancellationRequested(); object? result = null;
            if (path.EndsWith("work/location", StringComparison.Ordinal))
            {
                LocationPosts++;
                if (LocationFailureStatus is { } failure) throw new RoleWorkspaceAccessException(failure, "위치 전송 실패");
                var value = (기사위치갱신요청)body; result = new 기사위치갱신응답 { 현재위도 = value.위도, 현재경도 = value.경도 };
            }
            if (path.EndsWith("work/stop", StringComparison.Ordinal)) { if (FailStop) throw new InvalidOperationException("종료 처리 실패"); StopPosts++; }
            if (path.EndsWith("work/start", StringComparison.Ordinal))
            {
                StartPosts++; StartRequests.Add((기사운행시작요청)body);
                if (StartFailureStatus is { } rejected)
                {
                    onDuty = true; // Another app/request already started; its reception remains OFF.
                    throw new RoleWorkspaceAccessException(rejected, "운행 시작 거절");
                }
                if (FailStart) throw new InvalidOperationException("시작 처리 실패"); onDuty = true;
                if (StartResponseGate is { } gate) await gate.Task;
                if (ApplyThenFailStart) throw new InvalidOperationException("운행이 시작됐으나 응답이 유실되었습니다.");
                result = new 기사운행시작응답 { Status = "운행중" };
            }
            return (T?)result;
        }
        public Task<T?> PutAsync<T>(string roleKey, string path, object body, CancellationToken token)
        {
            Assert.EndsWith("operational-dispatch/availability/intent", path); token.ThrowIfCancellationRequested();
            var request = Assert.IsType<운영배차수신의사변경요청>(body); IntentAttempts.Add(request);
            if (FailIntent) throw new InvalidOperationException("수신 변경 실패");
            IntentWrites.Add(request); receiving = request.수신의사Code == 운영배차수신의사Code.On;
            if (receiving && request.미응답자동중지해제) AutomaticallyPaused = false;
            if (BreakWorkspaceAfterIntent) FailWorkspace = true;
            if (ApplyThenFailIntent) throw new InvalidOperationException("수신은 변경됐으나 응답이 유실되었습니다.");
            return Task.FromResult((T?)(object)new 운영배차수신상태Dto { 수신의사Code = request.수신의사Code });
        }
        public Task AcknowledgeOfferPresentationAsync(string offerId, int recommendationRound, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested(); ReceiptAttempts.Add((offerId, recommendationRound));
            return FailReceipt ? Task.FromException(new InvalidOperationException("표시 확인 연결 실패")) : Task.CompletedTask;
        }
        public Task<T?> UploadAsync<T>(string roleKey, string path, HttpContent body, CancellationToken token) => throw new NotSupportedException();
    }
    private sealed class Access : IRoleWorkspaceAccess
    {
        private RoleWorkspaceIdentity identity = new("owner-a", 1, true);
        public event Action? Changed;
        public RoleWorkspaceIdentity GetIdentity(string roleKey) => identity;
        public void ChangeOwner(string owner) { identity = new(owner, identity.Revision + 1, true); Changed?.Invoke(); }
        public Task EnsureInitializedAsync(string roleKey, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SignInAsync(string roleKey, string name, string password, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SignOutAsync(string roleKey, CancellationToken cancellationToken = default)
        { identity = new(null, identity.Revision + 1, false); Changed?.Invoke(); return Task.CompletedTask; }
    }
    private sealed class OtherAdapter(string roleKey) : IRoleWorkspaceAdapter
    {
        public string RoleKey => roleKey;
        public Task<RoleWorkspaceSnapshot> LoadAsync(string? selectedId, CancellationToken cancellationToken) => Task.FromResult(new RoleWorkspaceSnapshot(roleKey, []));
        public Task PerformAsync(string itemId, string actionKey, Guid requestId, CancellationToken cancellationToken) => Task.CompletedTask;
        public void Clear() { }
    }
    private sealed class Navigation : NavigationManager { public Navigation() => Initialize("http://localhost/", "http://localhost/workspace/food-driver"); protected override void NavigateToCore(string uri, bool forceLoad) { } }
    private sealed class MapHost : INeighborhoodMapHost
    { public Task<NeighborhoodMapHostStatus> RenderAsync(string id, NeighborhoodMapRenderState state, Func<string, Task> selected, CancellationToken cancellationToken = default) => Task.FromResult(new NeighborhoodMapHostStatus("ready")); public Task HideAsync(CancellationToken cancellationToken = default) => Task.CompletedTask; }
    private sealed class StaticJs : IJSRuntime
    { public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => throw new NotSupportedException(); public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => InvokeAsync<TValue>(identifier, args); }
}
