using System.Net;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using MudBlazor.Services;
using Ssalddel.Contracts.Driver.Settings;
using Ssalddel.Ui.Common.Areas.App.Components;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.DriverPreferences;

namespace Ssalddel.Tests.Clients;

/// <summary>공통 Razor의 정적 렌더이며 네이티브 터치·픽셀 배치 검증과 구별합니다.</summary>
public sealed class DriverWorkPreferencesRenderR71Tests
{
    [Fact]
    public async Task 로그인전에는_업무입력과저장버튼없이_로그인복귀만표시한다()
    {
        var client = new DriverWorkPreferencesR71Tests.Client { Identity = new(null, 1, false) };
        using var model = new DriverWorkParticipationViewModel(client);
        var html = await RenderAsync(model);
        Assert.Contains("기사 로그인이 필요", html); Assert.Contains("로그인 화면으로", html);
        Assert.DoesNotContain("업무·적재 설정 저장", html); Assert.DoesNotContain("<input", html); Assert.Equal(0, client.Writes);
    }

    [Fact]
    public async Task 준비완료는_업무_적재_추가배차_세섹션과_단위를표시한다()
    {
        var client = new DriverWorkPreferencesR71Tests.Client(); using var model = new DriverWorkParticipationViewModel(client); await model.LoadAsync();
        var html = await RenderAsync(model);
        foreach (var text in new[] { "받을 업무", "음식 배달", "소형 물품·퀵", "화물 운송", "직접 피킹 가능", "차량·적재 조건", "중량 (kg)", "부피 (L)", "추가 배차", "희망 최대 동시 업무 수", "업무·적재 설정 저장" })
            Assert.Contains(text, html);
        Assert.Contains("희망 차량 종류", html); Assert.DoesNotContain("현재 차량", html);
        Assert.Contains("실제 배차는 등록 차량과 수행 자격", html); Assert.Contains("소수 3자리", html);
        Assert.DoesNotContain("Fixture", html); Assert.DoesNotContain("fixture", html);
        Assert.Equal(0, client.Writes);
    }

    [Fact]
    public async Task 기능OFF와수행자격미확인은_희망보관과실배차를구분한다()
    {
        var client = new DriverWorkPreferencesR71Tests.Client
        { Value = DriverWorkPreferencesR71Tests.Response() with { SharedAdmissionEnabled = false, FoodEligible = false, CargoEligible = false } };
        using var model = new DriverWorkParticipationViewModel(client); await model.LoadAsync(); var html = await RenderAsync(model);
        Assert.Contains("설정은 미리 저장", html); Assert.Contains("업무 선택과 수행 자격은 별도", html);
        Assert.Contains("업무·적재 설정 저장", html);
    }

    [Fact]
    public async Task 저장미확인은_입력과재전송대신_결과확인만표시한다()
    {
        var client = new DriverWorkPreferencesR71Tests.Client { Update = (_, _) => throw new HttpRequestException() };
        using var model = new DriverWorkParticipationViewModel(client); await model.LoadAsync(); await model.SaveAsync();
        var html = await RenderAsync(model);
        Assert.Contains("저장 결과 확인", html); Assert.Contains("다시 전송하지 말고", html);
        Assert.DoesNotContain("업무·적재 설정 저장", html); Assert.DoesNotContain("<input", html); Assert.Equal(1, client.Writes);
    }

    [Fact]
    public async Task 조회실패는_정상빈상태가아니라오류와재확인으로표시한다()
    {
        var client = new DriverWorkPreferencesR71Tests.Client { Get = _ => throw new HttpRequestException() };
        using var model = new DriverWorkParticipationViewModel(client); await model.LoadAsync(); var html = await RenderAsync(model);
        Assert.Contains("설정을 확인하지 못했습니다", html); Assert.Contains("설정 다시 확인", html);
        Assert.DoesNotContain("업무·적재 설정 저장", html);
    }

    [Fact]
    public async Task 저장중에는_다른저장버튼이없고_결과확인버튼도잠근다()
    {
        var client = new DriverWorkPreferencesR71Tests.Client(); var response = new TaskCompletionSource<DriverWorkParticipationResponse>();
        client.Update = (_, _) => response.Task;
        using var model = new DriverWorkParticipationViewModel(client); await model.LoadAsync(); var save = model.SaveAsync();
        var html = await RenderAsync(model);
        Assert.Contains("저장 결과 확인", html); Assert.DoesNotContain("업무·적재 설정 저장", html);
        Assert.Contains("disabled", html); Assert.Contains("설정을 저장하고 있습니다", html);
        client.Value = client.Value with { Settings = client.Written!.Settings with { Revision = 5 } };
        response.SetResult(client.Value); await save;
    }

    private static async Task<string> RenderAsync(DriverWorkParticipationViewModel model)
    {
        var services = new ServiceCollection(); services.AddLogging(); services.AddMudServices();
        services.AddSingleton<IJSRuntime, NoBrowserJs>(); services.AddSingleton<NavigationManager, NavigationStub>();
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        return WebUtility.HtmlDecode(await renderer.Dispatcher.InvokeAsync(async () =>
        {
            await renderer.RenderComponentAsync<RoleAppProviders>();
            return (await renderer.RenderComponentAsync<DriverWorkParticipationForm>(ParameterView.FromDictionary(new Dictionary<string, object?>
            { [nameof(DriverWorkParticipationForm.Model)] = model, [nameof(DriverWorkParticipationForm.LoginHref)] = "/login?returnUrl=%2Fdriver%2Fwork%2Fsettings" }))).ToHtmlString();
        }));
    }
    private sealed class NavigationStub : NavigationManager
    { public NavigationStub() => Initialize("http://localhost/", "http://localhost/driver/work/settings"); protected override void NavigateToCore(string uri, bool forceLoad) { } }
    private sealed class NoBrowserJs : IJSRuntime
    {
        public ValueTask<T> InvokeAsync<T>(string identifier, object?[]? args) => ValueTask.FromResult(default(T)!);
        public ValueTask<T> InvokeAsync<T>(string identifier, CancellationToken token, object?[]? args) => InvokeAsync<T>(identifier, args);
    }
}
