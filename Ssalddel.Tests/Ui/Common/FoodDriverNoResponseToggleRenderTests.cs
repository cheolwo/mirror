using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Food;

namespace Ssalddel.Tests.Ui.Common;

public sealed class FoodDriverNoResponseToggleRenderTests
{
    [Theory]
    [InlineData("paused", false, false, "false", false, "미응답으로 중지")]
    [InlineData("resumed", true, false, "true", false, "켜짐")]
    [InlineData("changing", false, true, "false", true, "변경 중")]
    public async Task 실제토글은_자동중지_재개_변경중의_서버상태와이유를표시한다(
        string example, bool receiving, bool busy, string expectedChecked, bool expectedDisabled, string expectedStatus)
    {
        var notice = receiving ? null : FoodDriverDispatchReceptionPolicy.AutomaticStopNotice;
        var rendered = await RenderAsync(new()
        {
            [nameof(FoodDriverDispatchToggle.Action)] = new RoleWorkspaceAction(
                receiving ? FoodDriverRoleWorkspaceAdapter.DisableDispatch : FoodDriverRoleWorkspaceAdapter.EnableDispatch,
                "신규 배차 받기"),
            [nameof(FoodDriverDispatchToggle.IsStateKnown)] = true,
            [nameof(FoodDriverDispatchToggle.IsBusy)] = busy,
            [nameof(FoodDriverDispatchToggle.AutomaticStopNotice)] = notice
        });
        SaveStandalone(example, expectedStatus, rendered);
        var html = WebUtility.HtmlDecode(rendered);
        var button = Regex.Match(html, "<button\\b[^>]*>", RegexOptions.CultureInvariant).Value;
        Assert.NotEmpty(button);
        Assert.Contains("role=\"switch\"", button);
        Assert.Contains("aria-label=\"신규 배차 받기\"", button);
        Assert.Contains($"aria-checked=\"{expectedChecked}\"", button);
        Assert.Equal(expectedDisabled, Regex.IsMatch(button, "\\sdisabled(?:[\\s=>])", RegexOptions.CultureInvariant));
        Assert.Matches("<small\\b[^>]*>" + Regex.Escape(expectedStatus) + "</small>", html);
        Assert.Matches("\\sb-[a-z0-9]+(?:=\"\"|(?=[\\s>]))", rendered);
        if (notice is not null)
        {
            Assert.Contains(notice, html);
            Assert.Contains("food-driver-dispatch-toggle__notice", html);
        }
        else
        {
            Assert.DoesNotContain(FoodDriverDispatchReceptionPolicy.AutomaticStopNotice, html);
            Assert.DoesNotContain("food-driver-dispatch-toggle__notice", html);
        }
    }

    private static async Task<string> RenderAsync(Dictionary<string, object?> parameters)
    {
        var services = new ServiceCollection().AddLogging();
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () =>
            (await renderer.RenderComponentAsync<FoodDriverDispatchToggle>(ParameterView.FromDictionary(parameters))).ToHtmlString());
    }

    private static void SaveStandalone(string example, string status, string rendered)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !Directory.Exists(Path.Combine(root.FullName, "Ssalddel.Ui.Common"))) root = root.Parent;
        Assert.NotNull(root);
        var ui = Path.Combine(root!.FullName, "Ssalddel.Ui.Common");
        var foundation = File.ReadAllText(Path.Combine(ui, "wwwroot", "Areas", "App", "css", "role-app-foundation.css"));
        var componentCss = File.ReadAllText(Path.Combine(ui, "Areas", "App", "RoleWorkspace", "Core", "FoodDriverDispatchToggle.razor.css"));
        var folder = Path.Combine(root.FullName, "artifacts", "local", "validation", "20261016-driver-no-response-r62", "ui");
        Directory.CreateDirectory(folder);
        // 실제 Razor HTML의 scope 속성과 원본 CSS를 보존합니다. 전체 지도 화면이나 기기 실행 증거가 아닙니다.
        var document = "<!doctype html><html lang=\"ko\"><head><meta charset=\"utf-8\">"
            + "<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">"
            + "<title>신규 배차 받기 · " + WebUtility.HtmlEncode(status) + " · 컴포넌트 배치 확인</title>"
            + "<style>" + foundation + "\n" + componentCss
            + "\nbody{margin:0;padding:20px;font-family:Arial,sans-serif;background:#f5f7fa;color:#172b36}"
            + ".component-preview{max-width:390px;margin:0 auto}.component-preview h1{font-size:18px;line-height:1.5}"
            + ".component-preview p{font-size:13px;line-height:1.6;color:#586b78}.component-preview__fixture{padding:16px 0}"
            + "</style></head><body><main class=\"component-preview\"><h1>신규 배차 받기 · " + WebUtility.HtmlEncode(status) + "</h1>"
            + "<p>실제 토글 컴포넌트의 로컬 배치 확인입니다. 전체 지도 화면·기기 실행·서버 배차 결과를 나타내지 않습니다.</p>"
            + "<section class=\"component-preview__fixture\" aria-label=\"실제 토글 컴포넌트\">" + rendered
            + "</section></main></body></html>";
        File.WriteAllText(Path.Combine(folder, "food-driver-dispatch-toggle-" + example + ".html"), document, new UTF8Encoding(false));
    }
}
