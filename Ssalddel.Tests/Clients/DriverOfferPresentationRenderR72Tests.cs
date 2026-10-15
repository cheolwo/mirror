using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Ssalddel.Contracts.Common.Drivers;
using Ssalddel.Contracts.Driver.Settings;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.DriverOffers;

namespace Ssalddel.Tests.Clients;

/// <summary>실제 공유 Razor 출력입니다. 독립 HTML 미리보기는 native 앱 입력·배치 증거가 아닙니다.</summary>
public sealed class DriverOfferPresentationRenderR72Tests
{
    [Theory]
    [InlineData("food", 1, "음식 배달", "#2563EB")]
    [InlineData("goods", 1, "소형 물품·퀵", "#DC2626")]
    [InlineData("warehouse", 1, "창고 작업", "#64748B")]
    [InlineData("combined", 2, "복합 작업", "#2563EB")]
    [InlineData("unknown", 1, "업무 유형 확인 필요", "#64748B")]
    public async Task 업무유형은_막대와단색아이콘과명칭으로함께표시한다(string key, int bars, string label, string color)
    {
        var html = await RenderBadgeAsync(Visual(key));
        Assert.Contains(label, html); Assert.Contains(color, html); Assert.Contains("<svg", html); Assert.Contains("aria-label", html);
        Assert.Equal(bars, Regex.Matches(html, "background-color:").Count);
        if (key == "combined") Assert.Contains("#DC2626", html);
        Assert.DoesNotContain("linear-gradient", html); Assert.DoesNotContain("script", html);
    }

    [Fact]
    public async Task 거리비화면은_산정거리와단가와서버금액을_주행거리와구별한다()
    {
        var html = await RenderPriceAsync(DriverOfferPresentationR72Tests.Price());
        Assert.Contains("산정 직선거리 2.34 km", html); Assert.Contains("거리비 2,340원", html);
        Assert.Contains("거리 단가 1,000원/km", html); Assert.Contains("제안 보수 4,340원", html);
        Assert.Contains("주행거리·픽업 접근거리와 별도", html); Assert.Contains("거리비 원 단위 반올림", html);
        Assert.Contains("산정 근거", html); Assert.DoesNotContain("ProportionalAwayFromZero", html);
    }

    [Fact]
    public async Task 거리비근거미확인은_0원이아닌미정으로표시한다()
    {
        var html = await RenderPriceAsync(null);
        Assert.Contains("산정 거리 미정", html); Assert.Contains("거리비 미정", html);
        Assert.DoesNotContain("0원", html); Assert.DoesNotContain("제안 보수", html);
    }

    [Fact]
    public async Task 정책원문은_HTML명령으로실행되지않게인코딩한다()
    {
        var html = await RenderRawAsync<DriverOfferPricingSummary>(new()
        { [nameof(DriverOfferPricingSummary.Pricing)] = DriverOfferPresentationR72Tests.Price() with { PolicyRevision = "<script>alert(1)</script>" } });
        Assert.DoesNotContain("<script>", html); Assert.Contains("&lt;script&gt;", html);
    }

    [Fact]
    public async Task 요청한경우에만_실제Razor출력과공통CSS를고정로컬미리보기로내보낸다()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("SSALDDEL_R72_EXPORT_UI"), "true", StringComparison.OrdinalIgnoreCase)) return;
        var root = FindRoot();
        var output = Path.Combine(root, "artifacts", "local", "straight-line-driver-pricing-r72", "ui-preview");
        Directory.CreateDirectory(output);
        var source = Path.Combine(root, "Ssalddel.Ui.Common", "Areas", "App", "RoleWorkspace", "DriverOffers");
        var css = File.ReadAllText(Path.Combine(source, "DriverOfferBadge.razor.css")) + Environment.NewLine
            + File.ReadAllText(Path.Combine(source, "DriverOfferPricingSummary.razor.css"));
        File.WriteAllText(Path.Combine(output, "offer.css"), css);
        var pieces = new List<string>();
        foreach (var key in new[] { "food", "goods", "warehouse", "combined", "unknown" })
        {
            var body = await RenderRawAsync<DriverOfferBadge>(new() { [nameof(DriverOfferBadge.Visual)] = Visual(key) });
            body += await RenderRawAsync<DriverOfferPricingSummary>(new()
            { [nameof(DriverOfferPricingSummary.Pricing)] = key is "warehouse" or "unknown" ? null : DriverOfferPresentationR72Tests.Price() });
            pieces.Add("<article>" + body + "</article>");
            File.WriteAllText(Path.Combine(output, key + ".html"), Page("<article>" + body + "</article>"));
        }
        File.WriteAllText(Path.Combine(output, "index.html"), Page(string.Join(Environment.NewLine, pieces)));
        File.WriteAllText(Path.Combine(output, "render-evidence.json"), JsonSerializer.Serialize(new
        {
            evidence = "actual-shared-razor-static-render",
            samples = new[] { "food", "goods", "warehouse", "combined", "unknown" },
            nativeAppRendered = false, deviceTested = false, createdAtUtc = DateTime.UtcNow
        }, new JsonSerializerOptions { WriteIndented = true }));
        Assert.True(File.Exists(Path.Combine(output, "index.html")));
    }

    private static string Page(string body) => "<!doctype html><html lang=\"ko\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">"
        + "<title>추천 업무 표시 검토</title><link rel=\"stylesheet\" href=\"offer.css\"><style>body{margin:0;background:#F5F7FA;font-family:Arial,sans-serif}"
        + "main{max-width:420px;margin:0 auto;padding:16px}article{background:#fff;border:1px solid #DCE5E9;border-radius:16px;padding:16px;margin-bottom:16px}</style></head><body><main>"
        + body + "</main></body></html>";
    private static string FindRoot()
    {
        for (var dir = new DirectoryInfo(Directory.GetCurrentDirectory()); dir is not null; dir = dir.Parent)
            if (Directory.Exists(Path.Combine(dir.FullName, "Ssalddel.Ui.Common"))) return dir.FullName;
        throw new DirectoryNotFoundException("공통 UI 소스 루트를 확인하지 못했습니다.");
    }
    private static DriverOfferVisual Visual(string key) => key switch
    {
        "food" => DriverOfferPresentation.Resolve(DriverWorkKinds.FoodDelivery, [DriverOfferActivityCodes.Pickup, DriverOfferActivityCodes.Deliver]),
        "goods" => DriverOfferPresentation.Resolve(DriverWorkKinds.QuickDelivery, [DriverOfferActivityCodes.Pickup, DriverOfferActivityCodes.Deliver]),
        "warehouse" => DriverOfferPresentation.Resolve(DriverWorkKinds.CargoTransport, [DriverOfferActivityCodes.Pick, DriverOfferActivityCodes.Pack]),
        "combined" => DriverOfferPresentation.Resolve(DriverWorkKinds.QuickDelivery, [DriverOfferActivityCodes.Pick, DriverOfferActivityCodes.Pack, DriverOfferActivityCodes.Deliver]),
        _ => DriverOfferPresentation.Resolve(null, [])
    };
    private static async Task<string> RenderBadgeAsync(DriverOfferVisual visual)
        => WebUtility.HtmlDecode(await RenderRawAsync<DriverOfferBadge>(new() { [nameof(DriverOfferBadge.Visual)] = visual }));
    private static async Task<string> RenderPriceAsync(DriverOfferPricingDto? price)
        => WebUtility.HtmlDecode(await RenderRawAsync<DriverOfferPricingSummary>(new() { [nameof(DriverOfferPricingSummary.Pricing)] = price }));
    private static async Task<string> RenderRawAsync<T>(Dictionary<string, object?> parameters) where T : IComponent
    {
        var services = new ServiceCollection(); services.AddLogging();
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () =>
            (await renderer.RenderComponentAsync<T>(ParameterView.FromDictionary(parameters))).ToHtmlString());
    }
}
