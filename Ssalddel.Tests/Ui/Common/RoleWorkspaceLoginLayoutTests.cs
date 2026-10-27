using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Web.HtmlRendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;

namespace Ssalddel.Tests.Ui.Common;

/// <summary>실제 로그인 컴포넌트의 의미·상태·복귀 계약입니다. CSS 배치와 단말 터치는 별도 검증합니다.</summary>
public sealed class RoleWorkspaceLoginLayoutTests
{
    [Theory]
    [InlineData("shipper")]
    [InlineData("cargo-driver")]
    [InlineData("food-driver")]
    public async Task 입력은명시라벨로연결하고_주행동과취소를입력밖에둔다(string role)
    {
        await using var f = new Fixture();
        var html = await f.Start(role);
        var inputs = Regex.Matches(html, "<input\\b[^>]*>").Select(match => match.Value).ToArray();
        Assert.Equal(2, inputs.Length);
        foreach (var input in inputs)
        {
            var id = Attribute(input, "id");
            Assert.False(string.IsNullOrWhiteSpace(id));
            Assert.Matches("<label\\b[^>]*for=\"" + Regex.Escape(id!) + "\"[^>]*>[^<]+</label>", html);
        }
        Assert.NotEqual(Attribute(inputs[0], "id"), Attribute(inputs[1], "id"));
        Assert.Equal("username", Attribute(inputs[0], "autocomplete"));
        Assert.Equal("current-password", Attribute(inputs[1], "autocomplete"));
        Assert.Equal("password", Attribute(inputs[1], "type"));
        Assert.DoesNotMatch("(?s)<label\\b[^>]*>(?:(?!</label>).)*<button", html);
        Assert.Equal(1, Regex.Matches(html, "<button\\b[^>]*type=\"submit\"").Count);
        Assert.Equal(1, Regex.Matches(html, "<button\\b[^>]*type=\"button\"").Count);
        Assert.Contains("class=\"role-login__actions\"", html);
        Assert.Equal(0, f.Access.Calls);
    }

    [Fact]
    public async Task 처리중에는입력과제출을잠그고_취소는사용할수있다()
    {
        await using var f = new Fixture(); await f.Start("shipper");
        await f.Set("_busy", true);
        var html = await f.Refresh();
        Assert.Contains("aria-busy=\"true\"", html);
        Assert.Equal(2, Regex.Matches(html, "<input\\b[^>]*\\bdisabled(?:[\\s=>])").Count);
        var submit = Regex.Match(html, "<button\\b(?=[^>]*type=\"submit\")[^>]*>").Value;
        var cancel = Regex.Match(html, "<button\\b(?=[^>]*type=\"button\")[^>]*>").Value;
        Assert.Contains("disabled", submit);
        Assert.DoesNotContain("disabled", cancel);
        Assert.Contains("로그인 중", html);
    }

    [Fact]
    public async Task 제출은기존역할인증을한번호출하고_안전한원래업무로복귀한다()
    {
        await using var f = new Fixture();
        await f.Start("cargo-driver", "/workspace/cargo-driver?selected=work-a");
        await f.Set("_name", "test-user"); await f.Set("_password", "test-secret");
        await f.Submit();
        Assert.Equal(1, f.Access.Calls);
        Assert.Equal(("cargo-driver", "test-user", "test-secret"), f.Access.Last);
        Assert.Equal("/workspace/cargo-driver?selected=work-a", Assert.Single(f.Navigation.Paths));
        Assert.Equal("", f.Field<string>("_password"));
        Assert.DoesNotContain("test-secret", await f.Refresh());
    }

    [Fact]
    public async Task 실패는비공개오류를숨기고_아이디와재시도를보존하며비밀번호를지운다()
    {
        await using var f = new Fixture(); await f.Start("shipper");
        f.Access.SignIn = _ => Task.FromException(new InvalidOperationException("private-debug-message"));
        await f.Set("_name", "test-user"); await f.Set("_password", "test-secret"); await f.Submit();
        var html = await f.Refresh();
        Assert.Equal("test-user", f.Field<string>("_name"));
        Assert.Equal("", f.Field<string>("_password"));
        Assert.Contains("role=\"alert\"", html);
        Assert.Contains("로그인하지 못했습니다", html);
        Assert.DoesNotContain("private-debug-message", html);
        Assert.DoesNotContain("test-secret", html);
        Assert.Empty(f.Navigation.Paths);
        f.Access.SignIn = _ => Task.CompletedTask;
        await f.Set("_password", "retry-secret"); await f.Submit();
        Assert.Equal(2, f.Access.Calls);
        Assert.Single(f.Navigation.Paths);
    }

    [Theory]
    [InlineData("/workspace/shipper?selected=a", "/workspace/shipper?selected=a")]
    [InlineData("https://outside.invalid/work", "/workspace/shipper")]
    public async Task 취소는인증요청없이_기존안전복귀와비밀번호제거를유지한다(string requested, string expected)
    {
        await using var f = new Fixture(); await f.Start("shipper", requested);
        await f.Set("_password", "test-secret"); await f.Cancel();
        Assert.Equal(expected, Assert.Single(f.Navigation.Paths));
        Assert.Equal("", f.Field<string>("_password"));
        Assert.Equal(0, f.Access.Calls);
    }

    private static string? Attribute(string element, string name)
    {
        var match = Regex.Match(element, "\\b" + Regex.Escape(name) + "=\"([^\"]*)\"");
        return match.Success ? match.Groups[1].Value : null;
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly ServiceProvider provider;
        private readonly HtmlRenderer renderer;
        private HtmlRootComponent output;
        private RoleWorkspaceLogin login = null!;
        public Access Access { get; } = new();
        public Navigation Navigation { get; } = new();
        public Fixture()
        {
            var services = new ServiceCollection(); services.AddLogging();
            services.AddSingleton<IRoleWorkspaceAccess>(Access);
            services.AddSingleton<NavigationManager>(Navigation);
            provider = services.BuildServiceProvider();
            renderer = new(provider, provider.GetRequiredService<ILoggerFactory>());
        }
        public Task<string> Start(string role, string? path = null) => renderer.Dispatcher.InvokeAsync(async () =>
        {
            output = await renderer.RenderComponentAsync<Host>(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(Host.Role)] = role, [nameof(Host.Path)] = path,
                [nameof(Host.Capture)] = (Action<RoleWorkspaceLogin>)(component => login = component)
            }));
            return WebUtility.HtmlDecode(output.ToHtmlString());
        });
        public Task Set(string field, object value) => renderer.Dispatcher.InvokeAsync(() => typeof(RoleWorkspaceLogin)
            .GetField(field, BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(login, value));
        public T? Field<T>(string name) => (T?)typeof(RoleWorkspaceLogin).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(login);
        public Task Submit() => renderer.Dispatcher.InvokeAsync(() => (Task)typeof(RoleWorkspaceLogin).GetMethod("SignIn", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(login, null)!);
        public Task Cancel() => renderer.Dispatcher.InvokeAsync(() => typeof(RoleWorkspaceLogin).GetMethod("Cancel", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(login, null));
        public Task<string> Refresh() => renderer.Dispatcher.InvokeAsync(() =>
        {
            typeof(ComponentBase).GetMethod("StateHasChanged", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(login, null);
            return WebUtility.HtmlDecode(output.ToHtmlString());
        });
        public async ValueTask DisposeAsync() { await renderer.DisposeAsync(); await provider.DisposeAsync(); }
    }

    private sealed class Host : ComponentBase
    {
        [Parameter] public string Role { get; set; } = "shipper";
        [Parameter] public string? Path { get; set; }
        [Parameter] public Action<RoleWorkspaceLogin>? Capture { get; set; }
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenComponent<RoleWorkspaceLogin>(0);
            builder.AddAttribute(1, nameof(RoleWorkspaceLogin.RoleKey), Role);
            builder.AddAttribute(2, nameof(RoleWorkspaceLogin.ReturnUrl), Path);
            builder.AddComponentReferenceCapture(3, instance => Capture?.Invoke((RoleWorkspaceLogin)instance));
            builder.CloseComponent();
        }
    }

    private sealed class Access : IRoleWorkspaceAccess
    {
        public event Action? Changed { add { } remove { } }
        public int Calls;
        public (string Role, string Name, string Password) Last;
        public Func<CancellationToken, Task> SignIn = _ => Task.CompletedTask;
        public RoleWorkspaceIdentity GetIdentity(string roleKey) => new(null, 0, false);
        public Task EnsureInitializedAsync(string roleKey, CancellationToken ct = default) => Task.CompletedTask;
        public Task SignInAsync(string roleKey, string name, string password, CancellationToken ct = default)
        { Calls++; Last = (roleKey, name, password); return SignIn(ct); }
        public Task SignOutAsync(string roleKey, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class Navigation : NavigationManager
    {
        public List<string> Paths { get; } = [];
        public Navigation() => Initialize("http://localhost/", "http://localhost/workspace-login/shipper");
        protected override void NavigateToCore(string uri, bool forceLoad) => Paths.Add(uri);
    }
}
