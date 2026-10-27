using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using MudBlazor.Services;
using Ssalddel.Contracts.Common.Drivers;
using Ssalddel.Contracts.Driver.Recommendation;
using Ssalddel.Contracts.Shipper.Request;
using Ssalddel.Ui.Common.Areas.App.Components;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Cargo;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;

namespace Ssalddel.Tests.Ui.Common;

public sealed class CargoOfferPayoutReadinessTests
{
    [Theory]
    [InlineData("confirmed", "4,200원")]
    [InlineData("missing", "미확인")]
    [InlineData("unknown-distance", "미확인")]
    [InlineData("negative", "미확인")]
    [InlineData("missing-policy", "미확인")]
    [InlineData("cargo-agreement", "6,200원")]
    [InlineData("cargo-source-missing", "미확인")]
    [InlineData("cargo-conflict", "미확인")]
    [InlineData("cargo-fraction", "미확인")]
    public async Task 기사보수는_서버제안근거만표시하고_고객금액이나0원으로대체하지않는다(string scenario, string expected)
    {
        var price = new DriverOfferPricingDto
        {
            DistanceBasisCode = "StraightLine", RawDistanceMeters = 2200, RatePerKmKrw = 1000,
            DistanceFeeKrw = 2200, BaseFeeKrw = 2000, TotalPayoutKrw = 4200, PolicyRevision = "quick-test.r1"
        };
        price = scenario switch
        {
            "missing" => null,
            "unknown-distance" => price with { DistanceBasisCode = "Unknown" },
            "negative" => price with { TotalPayoutKrw = -1 },
            "missing-policy" => price with { PolicyRevision = "" },
            "cargo-agreement" or "cargo-source-missing" or "cargo-fraction" => null,
            _ => price
        };
        var amount = scenario.StartsWith("cargo-", StringComparison.Ordinal) ? scenario == "cargo-fraction" ? 6200.5m : 6200m : (decimal?)null;
        var api = new Api(price, amount, scenario == "cargo-source-missing" ? null : "운임구성.기사지급예정운임");
        var services = new ServiceCollection();
        services.AddLogging(); services.AddMudServices();
        services.AddSingleton<IJSRuntime, Js>();
        services.AddSingleton<NavigationManager, Navigation>();
        services.AddSingleton<IRoleWorkspaceAccess, Access>();
        services.AddSingleton(new CargoDriverWorkspaceApiClient(api));
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            await renderer.RenderComponentAsync<RoleAppProviders>();
            var page = await renderer.RenderComponentAsync<CargoOfferActionScreen>(ParameterView.FromDictionary(
                new Dictionary<string, object?> { [nameof(CargoOfferActionScreen.ItemId)] = "quick-1" }));
            return WebUtility.HtmlDecode(page.ToHtmlString());
        });
        var displayed = Regex.Match(html, "data-testid=\"cargo-offer-driver-pay\"[^>]*>(.*?)</dd>", RegexOptions.Singleline);
        Assert.True(displayed.Success);
        Assert.Equal(expected, displayed.Groups[1].Value);
        Assert.Contains("의뢰 결제 예정액", html);
        Assert.Contains("18,000원", html);
        Assert.DoesNotContain("18,000원", displayed.Groups[1].Value);
        Assert.NotEqual("0원", displayed.Groups[1].Value);
        Assert.Contains("disabled", html); // 기존 명시 확인 전 참여 관문을 유지한다.
        Assert.Equal(2, api.Reads);
        Assert.Equal(0, api.Writes);
        if (scenario is "confirmed" or "cargo-agreement") Assert.Contains("입금 완료 금액은 아닙니다", html);
        else Assert.Contains("산정 근거를 확인하지 못했습니다", html);
    }

    private sealed class Api(DriverOfferPricingDto? pricing, decimal? driverAmount, string? amountSource) : IRoleWorkspaceApi
    {
        public int Reads { get; private set; }
        public int Writes { get; private set; }
        public Task<T> GetAsync<T>(string roleKey, string path, CancellationToken cancellationToken)
        {
            Reads++;
            object result = path.EndsWith("/recommendations", StringComparison.Ordinal)
                ? (IReadOnlyList<기사배차추천항목응답>)[new()
                    { 의뢰Id = "quick-1", 차량적합여부 = true, Pricing = pricing,
                        기사지급예정운임 = driverAmount, 기사지급예정운임근거 = amountSource }]
                : new 기사운송의뢰상세응답
                    { 의뢰Id = "quick-1", 화물종류 = "서류 상자", 픽업지 = "픽업 장소", 하차지 = "전달 장소", 결제예정금액 = 18000, 차량적합여부 = true, 배차상태 = "대기" };
            return Task.FromResult((T)result);
        }
        public Task<T?> PostAsync<T>(string roleKey, string path, object body, CancellationToken cancellationToken)
        { Writes++; throw new InvalidOperationException("화면 조회는 수락 명령을 전송하지 않습니다."); }
        public Task<T?> PutAsync<T>(string roleKey, string path, object body, CancellationToken cancellationToken)
            => PostAsync<T>(roleKey, path, body, cancellationToken);
        public Task<T?> UploadAsync<T>(string roleKey, string path, HttpContent body, CancellationToken cancellationToken)
            => PostAsync<T>(roleKey, path, body, cancellationToken);
    }
    private sealed class Access : IRoleWorkspaceAccess
    {
        public event Action? Changed { add { } remove { } }
        public RoleWorkspaceIdentity GetIdentity(string roleKey) => new("quick-driver", 1, true);
        public Task EnsureInitializedAsync(string roleKey, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SignInAsync(string roleKey, string name, string password, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SignOutAsync(string roleKey, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
    private sealed class Js : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => ValueTask.FromResult(default(TValue)!);
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
            => InvokeAsync<TValue>(identifier, args);
    }
    private sealed class Navigation : NavigationManager
    {
        public Navigation() => Initialize("http://localhost/", "http://localhost/workspace/cargo-driver/offers/quick-1");
        protected override void NavigateToCore(string uri, bool forceLoad) { }
    }
}
