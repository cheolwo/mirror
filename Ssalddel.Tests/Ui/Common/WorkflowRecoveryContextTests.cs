using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.Tests.Ui.Common;

public sealed class WorkflowRecoveryContextTests
{
    [Theory]
    [InlineData("https://EXAMPLE.test:443/api", "https://example.test/api/")]
    [InlineData("http://EXAMPLE.test:80/", "http://example.test")]
    [InlineData("https://example.test/api///", "https://example.test/api/")]
    [InlineData("https://[2001:DB8::1]:443/api", "https://[2001:db8::1]/api/")]
    public void 동일서버의대소문자기본포트끝슬래시는같은복구범위다(string first, string second)
    {
        var firstValue = Native(first).Capture(); var secondValue = Native(second).Capture();
        Assert.Equal(firstValue.EndpointKey, secondValue.EndpointKey);
        Assert.Equal(firstValue.Scope("warehouse.inbound.v1"), secondValue.Scope("warehouse.inbound.v1"));
    }

    [Theory]
    [InlineData("https://example.test/api/", "https://example.test/other/")]
    [InlineData("https://example.test/Api/", "https://example.test/api/")]
    [InlineData("https://example.test/api/", "https://example.test:8443/api/")]
    [InlineData("http://example.test/api/", "https://example.test/api/")]
    [InlineData("https://one.test/api/", "https://two.test/api/")]
    public void 다른서버포트경로나스킴은같은계정과업무번호라도범위가다르다(string first, string second)
        => Assert.NotEqual(Native(first).Capture().Scope("cargo.pickup.9"), Native(second).Capture().Scope("cargo.pickup.9"));

    [Theory]
    [InlineData("https://user:password@example.test/api/")]
    [InlineData("https://example.test/api/?key=secret")]
    [InlineData("https://example.test/api/#secret")]
    [InlineData("file:///private/path")]
    [InlineData("relative/path")]
    public void 자격정보쿼리조각이나비API주소를복구키로만들지않는다(string value)
    {
        var context = new WorkflowRecoveryContext(() => new("account-1", 3, true),
            () => new Uri(value, UriKind.RelativeOrAbsolute));
        Assert.Throws<InvalidDataException>(() => context.Capture());
        Assert.False(context.IsCurrent(new("account-1", 3, new string('a', 32))));
    }

    [Fact]
    public void 미인증또는서버주소가없으면보호기록을만들수없다()
    {
        Assert.Throws<InvalidOperationException>(() => new WorkflowRecoveryContext(
            () => new(null, 0, false), () => new Uri("https://example.test/")).Capture());
        Assert.Throws<InvalidDataException>(() => new WorkflowRecoveryContext(
            () => new("account-1", 1, true), () => null).Capture());
    }

    [Fact]
    public void 실제세션판본은같은계정재로그인과서버변경뒤오래된IO를차단한다()
    {
        var identity = new RoleWorkspaceIdentity("account-1", 4, true);
        var endpoint = new Uri("https://example.test/api/");
        var context = new WorkflowRecoveryContext(() => identity, () => endpoint);
        var old = context.Capture(); Assert.True(context.IsCurrent(old));
        identity = identity with { Revision = 5 };
        Assert.False(context.IsCurrent(old));
        var fresh = context.Capture();
        endpoint = new Uri("https://example.test/other/");
        Assert.False(context.IsCurrent(fresh));
        identity = identity with { OwnerId = "another-account" };
        Assert.False(context.IsCurrent(context.Capture() with { Owner = "account-1" }));
    }

    [Fact]
    public void Capture중세션이나서버가바뀌면일관되지않은스냅샷을반환하지않는다()
    {
        var reads = 0;
        var context = new WorkflowRecoveryContext(() => new("account-1", 3, true),
            () => new Uri(++reads == 1 ? "https://one.test/api/" : "https://two.test/api/"));
        Assert.Throws<InvalidOperationException>(() => context.Capture());
    }

    [Fact]
    public void Web토큰변경과관측된로그아웃은이전판본을무효화하되토큰은스냅샷에넣지않는다()
    {
        var user = new User(); var tokens = new Tokens(); using var http = new HttpClient { BaseAddress = new Uri("https://example.test/api/") };
        var context = new WorkflowRecoveryContext(user, tokens, http);
        var old = context.Capture(); Assert.True(context.IsCurrent(old));
        tokens.AccessToken = "private-token-b";
        var renewed = context.Capture(); Assert.True(renewed.SessionRevision > old.SessionRevision);
        Assert.False(context.IsCurrent(old));
        tokens.AccessToken = null;
        Assert.False(context.IsCurrent(renewed));
        tokens.AccessToken = "private-token-b";
        Assert.False(context.IsCurrent(renewed));
        var json = JsonSerializer.Serialize(context.Capture());
        Assert.DoesNotContain("private-token", json); Assert.DoesNotContain("example.test", json);
        Assert.DoesNotContain("https:", json); Assert.DoesNotContain("/api/", json);
    }

    [Fact]
    public void 사용자문맥과토큰소유자가다르면인증된복구문맥으로받지않는다()
    {
        var tokens = new Tokens { AuthenticationOwnerId = "other" };
        using var http = new HttpClient { BaseAddress = new Uri("https://example.test/") };
        Assert.Throws<InvalidOperationException>(() => new WorkflowRecoveryContext(new User(), tokens, http).Capture());
    }

    [Theory]
    [InlineData("")]
    [InlineData("cargo/9")]
    [InlineData("https://example.test/")]
    [InlineData("cargo\n9")]
    public void 논리범위에는주소나경로와제어문자를저장하지않는다(string value)
        => Assert.Throws<InvalidDataException>(() => Native("https://example.test/").Capture().Scope(value));

    [Fact]
    public void 복구범위는기존저장계약120자이내이며다른업무범위를합치지않는다()
    {
        var snapshot = Native("https://example.test/").Capture();
        Assert.Equal(120, snapshot.Scope(new string('a', 80)).Length);
        Assert.Throws<InvalidDataException>(() => snapshot.Scope(new string('a', 81)));
        Assert.NotEqual(snapshot.Scope("cargo.pickup.9"), snapshot.Scope("cargo.dropoff.9"));
    }

    [Fact]
    public void 공통등록은Scoped이며전문호스트의실제세션Adapter를덮지않는다()
    {
        var services = new ServiceCollection(); services.AddSsalddelUiCommonAppServices();
        var descriptor = Assert.Single(services, entry => entry.ServiceType == typeof(IWorkflowRecoveryContext));
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
        Assert.Equal(typeof(WorkflowRecoveryContext), descriptor.ImplementationType);
        var custom = Native("https://example.test/");
        var overrides = new ServiceCollection(); overrides.AddScoped<IWorkflowRecoveryContext>(_ => custom);
        overrides.AddSsalddelUiCommonAppServices();
        using var provider = overrides.BuildServiceProvider();
        using var scope = provider.CreateScope();
        Assert.Same(custom, scope.ServiceProvider.GetRequiredService<IWorkflowRecoveryContext>());
    }

    private static WorkflowRecoveryContext Native(string endpoint)
        => new(() => new("account-1", 3, true), () => new Uri(endpoint));
    private sealed class User : ISsalddel현재사용자Context
    { public 현재사용자Snapshot 현재사용자 => new("account-1", null, []); }
    private sealed class Tokens : ISsalddelAccessTokenProvider
    {
        public string? AuthenticationOwnerId { get; set; } = "account-1";
        public string? AccessToken { get; set; } = "private-token-a";
    }
}
