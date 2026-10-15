using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using MudBlazor.Services;
using Ssalddel.Contracts.Common.Privacy;
using Ssalddel.Tests.UiCommon;
using Ssalddel.Ui.Common.Areas.App.Components;
using Ssalddel.Ui.Common.Areas.App.Components.Transport;

namespace Ssalddel.Tests.Clients;

/// <summary>제품 공통 동의 화면의 정적 렌더이며 실제 클릭·기기 화면 확인을 대신하지 않습니다.</summary>
public sealed class ShipperPrivacyConsentRenderTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 동의안내는_정책원문과명시체크를표시하고_렌더만으로기록하지않는다(bool recorded)
    {
        var callbacks = 0;
        var html = await RenderAsync<ShipperPrivacyConsentPanel>(new()
        {
            [nameof(ShipperPrivacyConsentPanel.Recorded)] = recorded,
            [nameof(ShipperPrivacyConsentPanel.RecordRequested)] = EventCallback.Factory.Create(this, () => callbacks++),
        });
        var notice = 신청개인정보동의정책.For(신청개인정보업무Codes.운송대행);
        Assert.Contains(notice.수집이용목적, html);
        Assert.Contains(notice.보유이용기간, html);
        Assert.Contains(notice.동의거부안내, html);
        Assert.Contains(notice.제3자제공안내, html);
        Assert.Contains(notice.국외이전안내, html);
        Assert.Contains(신청개인정보동의정책.현재버전, html);
        Assert.Contains("만 14세 이상이며 본인 명의", html);
        Assert.DoesNotMatch("<input[^>]*\\schecked(?:\\s|=|>)", html);
        Assert.Equal(0, callbacks);
        if (recorded) Assert.Contains("개인정보 동의 기록을 확인했습니다", html);
        else Assert.Contains("disabled", Button(html, "동의 기록 확인"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 검토화면은_동의조건이준비되어야등록버튼을활성화한다(bool consentReady)
    {
        var callbacks = 0;
        var html = await RenderAsync<ShipperRequestReviewScreen>(new()
        {
            [nameof(ShipperRequestReviewScreen.ViewModel)] = ShipperRequestContactWindowTests.CompleteState(),
            [nameof(ShipperRequestReviewScreen.NavigationContext)] = new Ssalddel.Contracts.Shipper.Request.ShipperRequestNavigationContext(),
            [nameof(ShipperRequestReviewScreen.ShowNavigation)] = false,
            [nameof(ShipperRequestReviewScreen.RegistrationEnabled)] = true,
            [nameof(ShipperRequestReviewScreen.SubmissionPrerequisiteSatisfied)] = consentReady,
            [nameof(ShipperRequestReviewScreen.SubmitRequested)] = EventCallback.Factory.Create(this, () => callbacks++),
            [nameof(ShipperRequestReviewScreen.BeforeSubmission)] = (RenderFragment)(builder => builder.AddMarkupContent(0, "<section data-testid='consent-slot'>본인 동의 확인</section>"))
        });
        Assert.Contains("consent-slot", html);
        var button = Button(html, "운송 의뢰 등록");
        Assert.Equal(!consentReady, button.Contains("disabled", StringComparison.Ordinal));
        Assert.Equal(0, callbacks);
    }

    private static string Button(string html, string text)
    {
        var buttons = Regex.Matches(html, "<button\\b[^>]*>(?:(?!</button>).)*</button>", RegexOptions.Singleline);
        return Assert.Single(buttons.Cast<Match>().Select(match => match.Value).Where(value => value.Contains(text, StringComparison.Ordinal)));
    }

    private static async Task<string> RenderAsync<T>(Dictionary<string, object?> parameters) where T : IComponent
    {
        var services = new ServiceCollection(); services.AddLogging(); services.AddMudServices();
        services.AddSingleton<IJSRuntime, NoBrowserJs>();
        services.AddSingleton<NavigationManager, NavigationStub>();
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        return WebUtility.HtmlDecode(await renderer.Dispatcher.InvokeAsync(async () =>
        {
            await renderer.RenderComponentAsync<RoleAppProviders>();
            return (await renderer.RenderComponentAsync<T>(ParameterView.FromDictionary(parameters))).ToHtmlString();
        }));
    }
    private sealed class NavigationStub : NavigationManager
    {
        public NavigationStub() => Initialize("http://localhost/", "http://localhost/shipper/request/review");
        protected override void NavigateToCore(string uri, bool forceLoad) { }
    }
    private sealed class NoBrowserJs : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => ValueTask.FromResult(default(TValue)!);
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => InvokeAsync<TValue>(identifier, args);
    }
}
