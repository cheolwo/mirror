using System.Net;
using System.Net.Http.Json;
using FDriverApp.PageModels;
using FDriverApp.Services;
using Ssalddel.Contracts.Driver.Notification;

namespace Ssalddel.Tests.Clients;

public sealed class FDriverNotificationSettingsTests
{
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
        Assert.False(alerts.Settings!.배차추천알림사용);
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

    private static 기사알림설정응답 Settings() => new() { DriverId = "test-driver", 소리사용 = true, 진동사용 = true };
    private static FDriverNotificationSettingsPageModel Model(FDriverTestHttpHandler handler, FDriverTestSession session, Alerts alerts)
    {
        var http = new HttpClient(handler) { BaseAddress = new("https://local.invalid/") };
        return new(new FoodDeliveryDriverApiService(http, session, new(http, session)), session, alerts);
    }
    private sealed class Alerts : IFDriverFoodNotificationService
    {
        public 기사알림설정응답? Settings;
        public void ApplySettings(string ownerId, 기사알림설정응답 settings, DateTime at) => Settings = settings;
        public FDriverFoodNotificationReceipt? ReadReceipt() => null;
        public FDriverFoodNotificationTarget? ReadOpenTarget() => null;
        public void WriteReceipt(FDriverFoodNotificationReceipt receipt) { }
        public void SetOpenTarget(FDriverFoodNotificationTarget target) { }
        public void ClearOpenTarget() { }
        public bool Publish(string userId, string offerId, DateTime expiresAtUtc) => false;
        public void Cancel() { }
        public void ClearAccount() { }
    }
}
