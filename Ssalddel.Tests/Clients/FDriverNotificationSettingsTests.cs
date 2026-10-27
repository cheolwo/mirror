using System.Net;
using System.Net.Http.Json;
using FDriverApp.PageModels;
using FDriverApp.Services;
using Ssalddel.Client.Infrastructure.Security;
using Ssalddel.Contracts.Driver.Notification;

namespace Ssalddel.Tests.Clients;

public sealed class FDriverNotificationSettingsTests
{
    [Fact]
    public async Task 같은계정의설정왕복은_저장전토글을보존하고_서버나알림서비스에미리적용하지않는다()
    {
        var session = new FDriverTestSession(); var alerts = new Alerts(); var reads = 0; var writes = 0;
        var handler = new FDriverTestHttpHandler((request, _) =>
        {
            if (request.Method == HttpMethod.Put) writes++; else reads++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(Settings()) });
        });
        var model = Model(handler, session, alerts);
        await model.ActivateAsync();
        model.Enabled = true; model.Sound = false; model.Vibrate = false; model.QuietAtNight = true;

        model.Deactivate();
        await model.ActivateAsync();

        Assert.True(model.Enabled); Assert.False(model.Sound); Assert.False(model.Vibrate); Assert.True(model.QuietAtNight);
        Assert.True(model.CanEdit); Assert.Contains("저장 전 변경", model.Status);
        Assert.Equal(1, reads); Assert.Equal(0, writes);
        Assert.False(alerts.Settings!.배차추천알림사용); Assert.True(alerts.Settings.소리사용);
        model.Deactivate();
    }

    [Fact]
    public async Task 명시적으로다시조회하면_미저장변경대신정본을적용한다()
    {
        var session = new FDriverTestSession(); var alerts = new Alerts(); var reads = 0;
        var handler = new FDriverTestHttpHandler((_, _) =>
        {
            reads++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(Settings()) });
        });
        var model = Model(handler, session, alerts);
        await model.ActivateAsync(); model.Enabled = true; model.Sound = false; model.QuietAtNight = true;

        await model.LoadCommand.ExecuteAsync(null);

        Assert.Equal(2, reads); Assert.True(model.CanEdit);
        Assert.False(model.Enabled); Assert.True(model.Sound); Assert.False(model.QuietAtNight);
        Assert.DoesNotContain("저장 전 변경", model.Status);
        model.Deactivate();
    }

    [Fact]
    public async Task 비활성동안계정이변경되면_이전초안을즉시비우고새계정정본만적용한다()
    {
        var session = new FDriverTestSession(); var alerts = new Alerts(); var reads = 0;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reply = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new FDriverTestHttpHandler((_, _) =>
        {
            if (++reads == 1) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(Settings()) });
            entered.TrySetResult(); return reply.Task;
        });
        var model = Model(handler, session, alerts);
        await model.ActivateAsync(); model.Enabled = true; model.QuietAtNight = true;
        model.Deactivate();
        await session.ApplyAsync(new ClientAuthTokenSnapshot("other-access", DateTime.UtcNow.AddMinutes(30), "other-refresh",
            DateTime.UtcNow.AddHours(1), "other-driver", "다른 기사", ["Driver"]));

        var load = model.ActivateAsync(); await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(model.Enabled); Assert.False(model.Sound); Assert.False(model.Vibrate); Assert.False(model.QuietAtNight);
        Assert.False(model.FoodEnabled); Assert.False(model.CargoEnabled);
        Assert.False(model.CanEdit);
        var other = Settings(); other.DriverId = "other-driver";
        reply.SetResult(new(HttpStatusCode.OK) { Content = JsonContent.Create(other) });
        await load;

        Assert.True(model.CanEdit); Assert.Equal("other-driver", alerts.Settings!.DriverId); Assert.Equal(2, reads);
        model.Deactivate();
    }

    [Fact]
    public async Task 저장중이탈한뒤복귀는_미확정초안을확정처럼보존하지않고_조회만재실행한다()
    {
        var session = new FDriverTestSession(); var alerts = new Alerts(); var reads = 0; var writes = 0;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new FDriverTestHttpHandler(async (request, token) =>
        {
            if (request.Method == HttpMethod.Put)
            {
                writes++; entered.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
            }
            else reads++;
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(Settings()) };
        });
        var model = Model(handler, session, alerts);
        await model.ActivateAsync(); model.Enabled = true;
        var save = model.SaveCommand.ExecuteAsync(null); await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        model.Deactivate(); await save;
        Assert.False(model.HasLoaded);

        await model.ActivateAsync();

        Assert.Equal(1, writes); Assert.Equal(2, reads); Assert.True(model.CanEdit); Assert.False(model.Enabled);
        Assert.DoesNotContain("저장 전 변경", model.Status);
        model.Deactivate();
    }

    [Fact]
    public async Task Save_RequeriesServerAndPreservesUnchangedDrivingOptions()
    {
        var session = new FDriverTestSession(); var alerts = new Alerts();
        var current = Settings(); current.운전중푸시만사용 = current.정차후모아보기 = true;
        기사알림설정수정요청? written = null; var reads = 0;
        var handler = new FDriverTestHttpHandler(async (request, token) =>
        {
            if (request.Method == HttpMethod.Put)
            {
                written = await request.Content!.ReadFromJsonAsync<기사알림설정수정요청>(token);
                current.배차추천알림사용 = written!.배차추천알림사용;
                current.소리사용 = written.소리사용;
            }
            else reads++;
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(current) };
        });
        var model = Model(handler, session, alerts);
        await model.ActivateAsync(); Assert.False(model.Enabled); // Existing server default is preserved.
        model.Enabled = true; model.Sound = false; await model.SaveCommand.ExecuteAsync(null);
        Assert.True(written!.운전중푸시만사용); Assert.True(written.정차후모아보기);
        Assert.Equal(2, reads); Assert.True(model.HasLoaded); Assert.False(model.Sound);
        Assert.True(alerts.Settings!.배차추천알림사용); Assert.False(alerts.Settings.소리사용);
        model.Deactivate();
    }

    [Fact]
    public async Task SaveAcknowledgedButConfirmationFails_RequiresReadBeforeAnotherWrite()
    {
        var session = new FDriverTestSession(); var alerts = new Alerts(); var reads = 0; var writes = 0;
        var handler = new FDriverTestHttpHandler((request, _) =>
        {
            if (request.Method == HttpMethod.Put) writes++;
            else if (++reads > 1) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(Settings()) });
        });
        var model = Model(handler, session, alerts); await model.ActivateAsync();
        model.Enabled = true; await model.SaveCommand.ExecuteAsync(null);
        Assert.False(model.HasLoaded); Assert.Contains("다시 조회", model.Status);
        await model.SaveCommand.ExecuteAsync(null); Assert.Equal(1, writes);
        Assert.Null(alerts.Settings);
        model.Deactivate();
    }

    [Fact]
    public async Task LogoutDuringDelayedRead_DoesNotApplyOldAccountSettings()
    {
        var session = new FDriverTestSession(); var alerts = new Alerts();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reply = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new FDriverTestHttpHandler((_, _) => { entered.SetResult(); return reply.Task; });
        var model = Model(handler, session, alerts); var load = model.ActivateAsync(); await entered.Task;
        await session.ClearAsync(); reply.SetResult(new(HttpStatusCode.OK) { Content = JsonContent.Create(Settings()) });
        await load; Assert.False(model.HasLoaded); Assert.False(model.Enabled); Assert.Null(alerts.Settings);
        model.Deactivate();
    }

    [Fact]
    public async Task ForeignOwnerSettings_DoNotEnableSaveOrNotifications()
    {
        var session = new FDriverTestSession(); var alerts = new Alerts(); var foreign = Settings(); foreign.DriverId = "other";
        var handler = new FDriverTestHttpHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(foreign) }));
        var model = Model(handler, session, alerts); await model.ActivateAsync();
        Assert.False(model.CanEdit); Assert.Null(alerts.Settings); model.Deactivate();
    }

    [Fact]
    public async Task 업무별알림만바꾼초안도_화면왕복에보존하고_저장전에는적용하지않는다()
    {
        var session = new FDriverTestSession(); var alerts = new Alerts(); var reads = 0;
        var handler = new FDriverTestHttpHandler((_, _) =>
        {
            reads++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(Settings()) });
        });
        var model = Model(handler, session, alerts); await model.ActivateAsync();
        model.FoodEnabled = false; model.CargoEnabled = true;

        model.Deactivate(); await model.ActivateAsync();

        Assert.False(model.FoodEnabled); Assert.True(model.CargoEnabled); Assert.False(model.Enabled);
        Assert.Equal(1, reads); Assert.Contains("저장 전 변경", model.Status);
        Assert.True(alerts.Settings!.음식배달추천알림사용); Assert.False(alerts.Settings.화물운송추천알림사용);
        model.Deactivate();
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task 업무별허용은서로독립적으로저장하고_기존여섯설정과서버재확인을유지한다(bool food, bool cargo)
    {
        var session = new FDriverTestSession(); var alerts = new Alerts(); var current = Settings(); var reads = 0;
        current.배차추천알림사용 = current.운전중푸시만사용 = current.정차후모아보기 = true;
        current.야간알림제한 = true;
        기사알림설정수정요청? written = null;
        var handler = new FDriverTestHttpHandler(async (request, token) =>
        {
            if (request.Method == HttpMethod.Put)
            {
                written = await request.Content!.ReadFromJsonAsync<기사알림설정수정요청>(token);
                current.음식배달추천알림사용 = written!.음식배달추천알림사용;
                current.화물운송추천알림사용 = written.화물운송추천알림사용;
            }
            else reads++;
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(current) };
        });
        var model = Model(handler, session, alerts); await model.ActivateAsync();
        model.FoodEnabled = food; model.CargoEnabled = cargo; await model.SaveCommand.ExecuteAsync(null);

        Assert.NotNull(written); Assert.Equal(food, written.음식배달추천알림사용); Assert.Equal(cargo, written.화물운송추천알림사용);
        Assert.True(written.배차추천알림사용); Assert.True(written.운전중푸시만사용); Assert.True(written.소리사용);
        Assert.True(written.진동사용); Assert.True(written.야간알림제한); Assert.True(written.정차후모아보기);
        Assert.Equal(2, reads); Assert.Equal(food, model.FoodEnabled); Assert.Equal(cargo, model.CargoEnabled);
        Assert.Equal(food, alerts.Settings!.음식배달추천알림사용); Assert.Equal(cargo, alerts.Settings.화물운송추천알림사용);
        Assert.True(model.CanEdit); model.Deactivate();
    }

    [Fact]
    public async Task 이전서버응답에업무별설정이없으면_기본꺼짐으로표시한다()
    {
        var handler = new FDriverTestHttpHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"driverId\":\"test-driver\",\"배차추천알림사용\":true}", System.Text.Encoding.UTF8, "application/json")
        }));
        var model = Model(handler, new FDriverTestSession(), new Alerts()); await model.ActivateAsync();

        Assert.True(model.Enabled); Assert.False(model.FoodEnabled); Assert.False(model.CargoEnabled); Assert.True(model.CanEdit);
        model.Deactivate();
    }

    [Fact]
    public async Task 같은계정이라도로그인판본이바뀌면_업무별설정을즉시비우고늦은응답을폐기한다()
    {
        var session = new RevisionSession(); var alerts = new Alerts(); var reads = 0;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reply = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var current = Settings(); current.배차추천알림사용 = current.화물운송추천알림사용 = true;
        var handler = new FDriverTestHttpHandler((_, _) =>
        {
            if (++reads == 1) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(current) });
            entered.TrySetResult(); return reply.Task;
        });
        var model = Model(handler, session, alerts); await model.ActivateAsync();
        Assert.True(model.FoodEnabled); Assert.True(model.CargoEnabled);
        var reload = model.LoadCommand.ExecuteAsync(null); await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await session.ApplyAsync(new ClientAuthTokenSnapshot("renewed-access", DateTime.UtcNow.AddMinutes(30), "renewed-refresh",
            DateTime.UtcNow.AddHours(1), "test-driver", "같은 기사", ["Driver"]));
        Assert.False(model.FoodEnabled); Assert.False(model.CargoEnabled); Assert.False(model.HasLoaded);
        reply.SetResult(new(HttpStatusCode.OK) { Content = JsonContent.Create(current) }); await reload;

        Assert.False(model.FoodEnabled); Assert.False(model.CargoEnabled); Assert.False(model.CanEdit);
        Assert.Equal(1, alerts.ApplyCount); model.Deactivate();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task 저장결과나재조회가불명확하면_이전허용과겹친Main조회로알림을복원하지않는다(bool writeFails)
    {
        var session = new FDriverTestSession(); var alerts = new Alerts(); var reads = 0; var writes = 0;
        var initial = Settings(); initial.배차추천알림사용 = true;
        var confirmedOff = Settings(); confirmedOff.배차추천알림사용 = false;
        var handler = new FDriverTestHttpHandler((request, _) =>
        {
            if (request.Method == HttpMethod.Put)
            {
                Assert.Equal(1, alerts.BeginCount); Assert.Null(alerts.Settings);
                writes++;
                return Task.FromResult(new HttpResponseMessage(writeFails ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK)
                    { Content = JsonContent.Create(confirmedOff) });
            }
            reads++;
            if (reads == 2 && !writeFails) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(reads == 1 ? initial : confirmedOff) });
        });
        var model = Model(handler, session, alerts); await model.ActivateAsync();
        Assert.True(alerts.Publish("test-driver", "before", DateTime.UtcNow.AddMinutes(1)));
        var oldMainReadStartedAt = DateTime.UtcNow.AddSeconds(-1);
        model.Enabled = false; await model.SaveCommand.ExecuteAsync(null);

        Assert.Equal(1, alerts.BeginCount); Assert.True(alerts.CancelCount > 0); Assert.Null(alerts.Settings);
        Assert.True(FDriverNotificationPublicationGate.HasPendingSettingsChange(alerts.Fence, "test-driver"));
        alerts.ApplySettings("test-driver", initial, oldMainReadStartedAt);
        Assert.Null(alerts.Settings); Assert.False(alerts.Publish("test-driver", "late", DateTime.UtcNow.AddMinutes(1)));
        await model.SaveCommand.ExecuteAsync(null); Assert.Equal(1, writes);

        await model.LoadCommand.ExecuteAsync(null);

        Assert.True(model.CanEdit); Assert.False(model.Enabled);
        Assert.False(FDriverNotificationPublicationGate.HasPendingSettingsChange(alerts.Fence, "test-driver"));
        alerts.ApplySettings("test-driver", initial, oldMainReadStartedAt);
        Assert.False(alerts.Settings!.배차추천알림사용);
        Assert.False(alerts.Publish("test-driver", "still-old", DateTime.UtcNow.AddMinutes(1)));
        model.Deactivate();
    }

    private static 기사알림설정응답 Settings() => new() { DriverId = "test-driver", 음식배달추천알림사용 = true, 소리사용 = true, 진동사용 = true };
    private static FDriverNotificationSettingsPageModel Model(FDriverTestHttpHandler handler, IFDriverAuthSession session, Alerts alerts)
    {
        var http = new HttpClient(handler) { BaseAddress = new("https://local.invalid/") };
        return new(new FoodDeliveryDriverApiService(http, session, new(http, session)), session, alerts);
    }
    private sealed class Alerts : IFDriverFoodNotificationService
    {
        public 기사알림설정응답? Settings;
        public int ApplyCount;
        public int BeginCount;
        public int CancelCount;
        public FDriverNotificationSettingsFence? Fence;
        public void ApplySettings(string ownerId, 기사알림설정응답 settings, DateTime at)
        {
            if (settings.DriverId != ownerId || !FDriverNotificationPublicationGate.CanApplySettings(Fence, ownerId, at, false)) return;
            Settings = settings; ApplyCount++;
        }
        public void BeginSettingsChange(string ownerId, DateTime at)
        { Fence = new(ownerId, at); Settings = null; BeginCount++; Cancel(); }
        public void ConfirmSettings(string ownerId, 기사알림설정응답 settings, DateTime at)
        {
            if (settings.DriverId != ownerId || !FDriverNotificationPublicationGate.CanApplySettings(Fence, ownerId, at, true)) return;
            if (Fence?.OwnerId == ownerId && Fence.ConfirmedReadAtUtc is null) Fence = Fence with { ConfirmedReadAtUtc = at };
            Settings = settings; ApplyCount++;
        }
        public FDriverFoodNotificationReceipt? ReadReceipt() => null;
        public FDriverFoodNotificationTarget? ReadOpenTarget() => null;
        public void WriteReceipt(FDriverFoodNotificationReceipt receipt) { }
        public void SetOpenTarget(FDriverFoodNotificationTarget target) { }
        public void ClearOpenTarget() { }
        public bool Publish(string userId, string offerId, DateTime expiresAtUtc)
            => Settings?.DriverId == userId && !FDriverNotificationPublicationGate.HasPendingSettingsChange(Fence, userId)
                && FDriverNotificationPublicationGate.AllowsFoodAlert(Settings);
        public void Cancel() => CancelCount++;
        public void ClearAccount() { }
    }

    private sealed class RevisionSession : IFDriverAuthSession
    {
        private readonly FDriverTestSession _inner = new();
        public event EventHandler? SessionChanged { add => _inner.SessionChanged += value; remove => _inner.SessionChanged -= value; }
        public long SessionRevision { get; private set; }
        public string? AccessToken => _inner.AccessToken;
        public DateTime AccessTokenExpiresAtUtc => _inner.AccessTokenExpiresAtUtc;
        public string? RefreshToken => _inner.RefreshToken;
        public DateTime RefreshTokenExpiresAtUtc => _inner.RefreshTokenExpiresAtUtc;
        public string? UserId => _inner.UserId;
        public string? UserName => _inner.UserName;
        public IReadOnlyList<string> Roles => _inner.Roles;
        public bool IsAuthenticated => _inner.IsAuthenticated;
        public ClientAuthSessionRestoreState CurrentState => _inner.CurrentState;
        public Task<ClientAuthSessionRestoreState> RestoreAsync(CancellationToken cancellationToken = default) => _inner.RestoreAsync(cancellationToken);
        public Task ApplyAsync(ClientAuthTokenSnapshot snapshot, CancellationToken cancellationToken = default)
        { SessionRevision++; return _inner.ApplyAsync(snapshot, cancellationToken); }
        public Task ClearAsync(CancellationToken cancellationToken = default)
        { SessionRevision++; return _inner.ClearAsync(cancellationToken); }
    }
}
