using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MudBlazor.Services;
using Microsoft.JSInterop;
using Ssalddel.Contracts.Common.Notifications;
using Ssalddel.Contracts.Driver.Notification;
using Ssalddel.Contracts.Driver.Settings;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.DriverNotifications;
using NotificationResponse = Ssalddel.Contracts.Driver.Notification.기사알림설정응답;
using NotificationRequest = Ssalddel.Contracts.Driver.Notification.기사알림설정수정요청;

namespace Ssalddel.Tests.Ui.Common;

public sealed class DriverRecommendationSettingsTests
{
    [Theory]
    [InlineData(DriverWorkKinds.FoodDelivery, "음식 배달", "ic_driver_food")]
    [InlineData(DriverWorkKinds.CargoTransport, "화물 운송", "ic_driver_cargo")]
    public void 추천종류의_그림과업무명을_함께표시한다(string kind, string label, string drawable)
    {
        var visual = DriverRecommendationAlertPresentation.Resolve(kind)!;
        Assert.Contains(label, visual.Title); Assert.Equal(label, visual.Label);
        Assert.Equal(drawable, visual.AndroidIconName); Assert.NotEmpty(visual.IconPath);
        Assert.DoesNotContain("주소", visual.Body);
        Assert.NotEqual(DriverRecommendationAlertPresentation.Resolve(DriverWorkKinds.FoodDelivery)!.IconPath,
            DriverRecommendationAlertPresentation.Resolve(DriverWorkKinds.CargoTransport)!.IconPath);
    }

    [Theory]
    [InlineData(null)] [InlineData("")] [InlineData("unknown")]
    public void 미확인업무를_음식이나화물로추정하지않는다(string? kind)
        => Assert.Null(DriverRecommendationAlertPresentation.Resolve(kind));

    [Fact]
    public async Task 기본OFF와_조회전편집금지()
    {
        using var model = new DriverRecommendationSettingsViewModel(new Client());
        Assert.False(model.CanEdit); Assert.False(model.Enabled); Assert.False(model.FoodEnabled); Assert.False(model.CargoEnabled);
        await model.SaveAsync(); await model.LoadAsync();
        Assert.True(model.CanEdit); Assert.False(model.Enabled); Assert.False(model.FoodEnabled); Assert.False(model.CargoEnabled);
    }

    [Fact]
    public async Task 저장후재조회하며_두종류와기존보조설정을_독립보존한다()
    {
        var client = new Client { Response = new() { DriverId = "driver", 운전중푸시만사용 = true, 정차후모아보기 = true } };
        using var model = new DriverRecommendationSettingsViewModel(client); await model.LoadAsync();
        model.Enabled = true; model.FoodEnabled = true; model.CargoEnabled = false; model.Vibrate = true;
        await model.SaveAsync();
        Assert.Equal(2, client.Reads); var request = Assert.Single(client.Writes);
        Assert.True(request.음식배달추천알림사용); Assert.False(request.화물운송추천알림사용);
        Assert.True(request.운전중푸시만사용); Assert.True(request.정차후모아보기);
        Assert.True(model.FoodEnabled); Assert.False(model.CargoEnabled); Assert.True(model.IsLoaded);
    }

    [Fact]
    public async Task 저장결과불명확하면_다시전송하지않고_재조회가필요하다()
    {
        var client = new Client { FailSave = true };
        using var model = new DriverRecommendationSettingsViewModel(client); await model.LoadAsync();
        model.Enabled = true; model.CargoEnabled = true; await model.SaveAsync(); await model.SaveAsync();
        Assert.Single(client.Writes); Assert.False(model.CanEdit); Assert.Contains("저장 결과", model.Status);
    }

    [Fact]
    public async Task 계정전환은_이전설정과늦은조회응답을폐기한다()
    {
        var pending = new TaskCompletionSource<NotificationResponse?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new Client { Read = _ => pending.Task };
        using var model = new DriverRecommendationSettingsViewModel(client);
        var load = model.LoadAsync();
        client.Change(new("other", 2, true)); pending.SetResult(new() { DriverId = "driver", 배차추천알림사용 = true, 음식배달추천알림사용 = true });
        await load;
        Assert.False(model.IsLoaded); Assert.False(model.Enabled); Assert.False(model.FoodEnabled);
    }

    [Fact]
    public async Task 같은기사라도_다른로그인판본의저장응답은_표시하지않는다()
    {
        var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new Client { Save = _ => pending.Task };
        using var model = new DriverRecommendationSettingsViewModel(client); await model.LoadAsync();
        model.Enabled = model.FoodEnabled = true; var save = model.SaveAsync();
        client.Change(new("driver", 2, true)); pending.SetResult(); await save;
        Assert.False(model.CanEdit); Assert.False(model.Enabled); Assert.Equal(1, client.Reads);
    }

    [Fact]
    public async Task 미로그인및다른소유자의응답은_정상기본OFF로위장하지않는다()
    {
        var anonymous = new Client(); anonymous.Change(new(null, 1, false));
        using var model = new DriverRecommendationSettingsViewModel(anonymous); await model.LoadAsync();
        Assert.True(model.RequiresLogin); Assert.False(model.IsLoaded); Assert.Equal(0, anonymous.Reads);
        var foreign = new Client { Response = new() { DriverId = "other", 배차추천알림사용 = true } };
        using var other = new DriverRecommendationSettingsViewModel(foreign); await other.LoadAsync();
        Assert.False(other.CanEdit); Assert.Contains("확인하지 못", other.Status);
    }

    [Fact]
    public async Task 최초인증복원의_변경이벤트는_조회자체를취소하지않는다()
    {
        var client = new Client(); client.Initialize = () => client.Change(new("driver", 2, true));
        using var model = new DriverRecommendationSettingsViewModel(client); await model.LoadAsync();
        Assert.True(model.IsLoaded); Assert.Equal(1, client.Reads);
    }

    [Fact]
    public async Task 실제공통폼이_식기트럭과상태안내를함께렌더한다()
    {
        var services = new ServiceCollection().AddLogging().AddMudServices();
        services.AddSingleton<IJSRuntime, NoopJs>();
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        using var model = new DriverRecommendationSettingsViewModel(new Client()); await model.LoadAsync();
        var html = await renderer.Dispatcher.InvokeAsync(async () => (await renderer.RenderComponentAsync<DriverRecommendationSettingsForm>(
            ParameterView.FromDictionary(new Dictionary<string, object?> { ["Model"] = model }))).ToHtmlString());
        html = System.Net.WebUtility.HtmlDecode(html);
        Assert.Contains("음식 배달 추천", html); Assert.Contains("화물 운송 추천", html);
        Assert.Contains(DriverRecommendationAlertPresentation.FoodIconPath, html);
        Assert.Contains(DriverRecommendationAlertPresentation.CargoIconPath, html);
        Assert.Contains("자동으로 수락되지", html); Assert.Contains("전체 추천 알림이 꺼져", html);
        using var unavailable = new DriverRecommendationSettingsViewModel(new Client { Read = _ => throw new IOException() });
        await unavailable.LoadAsync();
        var failureHtml = await renderer.Dispatcher.InvokeAsync(async () => System.Net.WebUtility.HtmlDecode(
            (await renderer.RenderComponentAsync<DriverRecommendationSettingsForm>(ParameterView.FromDictionary(
                new Dictionary<string, object?> { ["Model"] = unavailable }))).ToHtmlString()));
        Assert.Contains("설정을 확인하지 못", failureHtml);
        Assert.DoesNotContain("전체 추천 알림이 꺼져", failureHtml);
    }

    private sealed class NoopJs : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => ValueTask.FromResult(default(TValue)!);
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken ct, object?[]? args) => ValueTask.FromResult(default(TValue)!);
    }

    private sealed class Client : IDriverRecommendationSettingsClient
    {
        public event Action? Changed;
        public RoleWorkspaceIdentity Identity { get; private set; } = new("driver", 1, true);
        public NotificationResponse Response { get; set; } = new() { DriverId = "driver" };
        public Func<CancellationToken, Task<NotificationResponse?>>? Read { get; set; }
        public Func<CancellationToken, Task>? Save { get; set; }
        public Action? Initialize { get; set; }
        public bool FailSave { get; set; }
        public int Reads { get; private set; }
        public List<NotificationRequest> Writes { get; } = [];
        public void Change(RoleWorkspaceIdentity identity) { Identity = identity; Changed?.Invoke(); }
        public Task InitializeAsync(CancellationToken ct) { Initialize?.Invoke(); return Task.CompletedTask; }
        public Task<NotificationResponse?> LoadAsync(CancellationToken ct) { Reads++; return Read?.Invoke(ct) ?? Task.FromResult<NotificationResponse?>(Response); }
        public async Task SaveAsync(NotificationRequest request, CancellationToken ct)
        {
            Writes.Add(request); if (FailSave) throw new IOException();
            if (Save is not null) await Save(ct);
            Response = new() { DriverId = Identity.OwnerId!, 배차추천알림사용 = request.배차추천알림사용,
                음식배달추천알림사용 = request.음식배달추천알림사용, 화물운송추천알림사용 = request.화물운송추천알림사용,
                소리사용 = request.소리사용, 진동사용 = request.진동사용, 야간알림제한 = request.야간알림제한,
                운전중푸시만사용 = request.운전중푸시만사용, 정차후모아보기 = request.정차후모아보기 };
        }
    }
}
