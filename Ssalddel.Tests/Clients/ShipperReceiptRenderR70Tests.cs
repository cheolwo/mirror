using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using MudBlazor.Services;
using Ssalddel.Ui.Common.Areas.App.Components;
using Ssalddel.Ui.Common.Areas.App.Components.Transport;

namespace Ssalddel.Tests.Clients;

/// <summary>실제 공통 패널의 서버 정적 렌더. 기기 클릭·화면 배치·앱 실행 증거와 구분합니다.</summary>
public sealed class ShipperReceiptRenderR70Tests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 미등록패널은_준비상태에맞는버튼과명시확인을표시하고_렌더만으로등록하지않는다(bool canRegister)
    {
        var calls = 0;
        var html = await RenderAsync(new()
        {
            [nameof(ShipperReceiptRegistrationPanel.CanRegister)] = canRegister,
            [nameof(ShipperReceiptRegistrationPanel.RegisterRequested)] = EventCallback.Factory.Create(this, () => calls++)
        });
        Assert.Contains("인수증 번호", html); Assert.Contains("검수 메모 (선택)", html);
        Assert.Contains("수령한 화물과 인수증 내용을 확인", html);
        Assert.DoesNotContain("value=\"ReceiptNumber\"", html);
        Assert.DoesNotContain(">Memo</textarea>", html);
        Assert.DoesNotMatch("<input[^>]*\\schecked(?:\\s|=|>)", html);
        Assert.Equal(!canRegister, Button(html, "인수증 등록").Contains("disabled", StringComparison.Ordinal));
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task 번호와메모는_지정된입력값을표시한다()
    {
        var html = await RenderAsync(new()
        {
            [nameof(ShipperReceiptRegistrationPanel.ReceiptNumber)] = "actual-receipt-a",
            [nameof(ShipperReceiptRegistrationPanel.Memo)] = "actual-memo-a"
        });
        Assert.Contains("value=\"actual-receipt-a\"", html);
        Assert.Contains("actual-memo-a", html);
    }

    [Fact]
    public async Task 결과미확인은_등록버튼대신조회버튼만표시한다()
    {
        var html = await RenderAsync(new()
        {
            [nameof(ShipperReceiptRegistrationPanel.NeedsResultCheck)] = true,
            [nameof(ShipperReceiptRegistrationPanel.InputLocked)] = true,
            [nameof(ShipperReceiptRegistrationPanel.ReceiptNumber)] = "receipt-a"
        });
        Assert.Contains("다시 전송하지 않습니다", html);
        Assert.Contains("등록 결과 확인", Button(html, "등록 결과 확인"));
        Assert.DoesNotContain("인수증 등록", Regex.Replace(html, "<h6\\b[^>]*>.*?</h6>", string.Empty, RegexOptions.Singleline));
        Assert.Contains("disabled", Regex.Match(html, "<input\\b[^>]*value=\"receipt-a\"[^>]*>").Value);
    }

    [Fact]
    public async Task 등록후에는_저장된번호를표시하며_번호입력과등록행동을닫는다()
    {
        var html = await RenderAsync(new()
        {
            [nameof(ShipperReceiptRegistrationPanel.Registered)] = true,
            [nameof(ShipperReceiptRegistrationPanel.RegisteredNumber)] = "recorded-receipt",
            [nameof(ShipperReceiptRegistrationPanel.ReadinessMessage)] = "입금·정산 상태는 별도로 확인합니다."
        });
        Assert.Contains("등록된 번호: recorded-receipt", html);
        Assert.Contains("입금·정산 상태는 별도", html);
        Assert.DoesNotContain("<input", html); Assert.DoesNotContain("<button", html);
    }

    private static string Button(string html, string text)
        => Assert.Single(Regex.Matches(html, "<button\\b[^>]*>(?:(?!</button>).)*</button>", RegexOptions.Singleline)
            .Cast<Match>().Select(match => match.Value).Where(value => value.Contains(text, StringComparison.Ordinal)));

    private static async Task<string> RenderAsync(Dictionary<string, object?> parameters)
    {
        var services = new ServiceCollection(); services.AddLogging(); services.AddMudServices();
        services.AddSingleton<IJSRuntime, NoBrowserJs>(); services.AddSingleton<NavigationManager, NavigationStub>();
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        return WebUtility.HtmlDecode(await renderer.Dispatcher.InvokeAsync(async () =>
        {
            await renderer.RenderComponentAsync<RoleAppProviders>();
            return (await renderer.RenderComponentAsync<ShipperReceiptRegistrationPanel>(ParameterView.FromDictionary(parameters))).ToHtmlString();
        }));
    }
    private sealed class NavigationStub : NavigationManager
    { public NavigationStub() => Initialize("http://localhost/", "http://localhost/shipper/request/request-a/proofs"); protected override void NavigateToCore(string uri, bool forceLoad) { } }
    private sealed class NoBrowserJs : IJSRuntime
    {
        public ValueTask<T> InvokeAsync<T>(string id, object?[]? args) => ValueTask.FromResult(default(T)!);
        public ValueTask<T> InvokeAsync<T>(string id, CancellationToken ct, object?[]? args) => InvokeAsync<T>(id, args);
    }
}
