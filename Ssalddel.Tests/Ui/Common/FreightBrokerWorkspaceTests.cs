using System.Net;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Cargo;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.Tests.Ui.Common;

public sealed class FreightBrokerWorkspaceTests
{
    [Fact]
    public async Task 본인조회는_정확한화물분류와_인증API만_사용한다()
    {
        var api = new JsonApi { Read = (_, _) => Task.FromResult<object?>(new[] { Row() }) };
        var client = new FreightBrokerWorkspaceClient(api);
        var rows = await client.MineAsync();
        Assert.Single(rows);
        Assert.Equal("cargo:own", rows[0].Id);
        Assert.Equal("api/v1/community/posts/my-ledgers?workflowTag=" + Uri.EscapeDataString("국내 화물 운송"), Assert.Single(api.Paths));
        Assert.DoesNotContain(api.Paths, path => path.Contains("shared-ledgers") || path.Contains("admin"));
        Assert.Equal(0, api.Writes);
    }

    [Theory]
    [InlineData("", true, true)]
    [InlineData("창고 입출고", true, true)]
    [InlineData("국내 화물 운송", false, true)]
    [InlineData("국내 화물 운송", true, false)]
    public async Task 미상필터의_전체조회나_타인원장_다른업무_응답은_전체보류한다(string tag, bool own, bool cargo)
    {
        var wrong = Row("cargo:other"); wrong.WorkflowTag = tag; wrong.내접근원장여부 = own;
        wrong.원장템플릿Key = cargo ? CommunityLedgerTemplateKeys.CargoTransport : CommunityLedgerTemplateKeys.WarehouseInbound;
        var api = new JsonApi { Read = (_, _) => Task.FromResult<object?>(new[] { Row(), wrong }) };
        await Assert.ThrowsAsync<InvalidDataException>(() => new FreightBrokerWorkspaceClient(api).MineAsync());
    }

    [Fact]
    public async Task 빈응답이나_중복ID를_정상목록으로_바꾸지_않는다()
    {
        var api = new JsonApi();
        await Assert.ThrowsAsync<InvalidDataException>(() => new FreightBrokerWorkspaceClient(api).MineAsync());
        api.Read = (_, _) => Task.FromResult<object?>(new[] { Row(), Row() });
        await Assert.ThrowsAsync<InvalidDataException>(() => new FreightBrokerWorkspaceClient(api).MineAsync());
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task 익명공개요약이나_다른역할한정상세를_본인업무상세로_승격하지_않는다(bool detailed, bool roleScoped)
    {
        var detail = Context(); detail.상세조회가능여부 = detailed; detail.역할범위조회여부 = roleScoped;
        var api = new JsonApi { Read = (_, _) => Task.FromResult<object?>(detail) };
        var error = await Assert.ThrowsAsync<SsalddelApiException>(() => new FreightBrokerWorkspaceClient(api).DetailAsync("cargo:own"));
        Assert.Equal(403, error.StatusCode);
    }

    [Fact]
    public async Task 상세는_응답ID와분류를_확인하고_원시주소연락처_실행명령을_복제하지_않는다()
    {
        var detail = Context();
        detail.블록목록 = [new() { 제목 = "조건 확인", 상태 = "검토 중", 항목 = new Dictionary<string, string> { ["contact"] = "private-contact-never-copy" } }];
        detail.가능한행동목록 = ["admin-action-never-copy"];
        var api = new JsonApi { Read = (_, _) => Task.FromResult<object?>(detail) };
        var result = await new FreightBrokerWorkspaceClient(api).DetailAsync("cargo:own");
        Assert.Equal("조건 확인", Assert.Single(result!.Blocks).Title);
        var json = System.Text.Json.JsonSerializer.Serialize(result);
        Assert.DoesNotContain("private-contact-never-copy", json);
        Assert.DoesNotContain("admin-action-never-copy", json);
        detail.원장Id = "cargo:other";
        await Assert.ThrowsAsync<InvalidDataException>(() => new FreightBrokerWorkspaceClient(api).DetailAsync("cargo:own"));
    }

    [Fact]
    public async Task 로그인전에는_본인정보API를_호출하지_않는다()
    {
        var identity = new Identity(); identity.Switch(null);
        var client = new Client(); using var model = new FreightBrokerWorkspaceViewModel(client, identity, identity);
        await model.LoadAsync();
        Assert.True(model.RequiresLogin); Assert.Empty(model.Items); Assert.Equal(0, client.MineCalls);
    }

    [Fact]
    public async Task 목록에없는ID는_직접상세조회하거나_이전상세를유지하지_않는다()
    {
        var identity = new Identity(); var client = new Client();
        using var model = new FreightBrokerWorkspaceViewModel(client, identity, identity);
        await model.LoadAsync(); await model.SelectAsync("cargo:own"); Assert.NotNull(model.Detail);
        await model.SelectAsync("cargo:other");
        Assert.Null(model.Detail); Assert.Null(model.SelectedId); Assert.NotNull(model.DetailMessage); Assert.Equal(1, client.DetailCalls);
    }

    [Fact]
    public async Task 계정변경중_늦은목록응답은_폐기하고_새계정조회만_표시한다()
    {
        var gate = new TaskCompletionSource<IReadOnlyList<FreightBrokerLedgerItem>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var identity = new Identity(); var client = new Client { Mine = _ => gate.Task };
        using var model = new FreightBrokerWorkspaceViewModel(client, identity, identity);
        var pending = model.LoadAsync(); identity.Switch("other");
        gate.SetResult([new("cargo:private-old", "old-user-private", "검토", "조건", "참여자")]); await pending;
        Assert.Empty(model.Items); Assert.False(model.HasLoaded); Assert.Null(model.Detail);
        client.Mine = _ => Task.FromResult<IReadOnlyList<FreightBrokerLedgerItem>>([new("cargo:new", "새 계정", "검토", "조건", "생성자")]);
        await model.LoadAsync(); Assert.Equal("cargo:new", Assert.Single(model.Items).Id);
    }

    [Fact]
    public async Task 서버나_동일계정세션이_바뀐_늦은상세응답을_버린다()
    {
        var gate = new TaskCompletionSource<FreightBrokerLedgerDetail?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var identity = new Identity(); var client = new Client { Detail = (_, _) => gate.Task };
        using var model = new FreightBrokerWorkspaceViewModel(client, identity, identity);
        await model.LoadAsync(); var pending = model.SelectAsync("cargo:own");
        identity.Endpoint = new('b', 32);
        gate.SetResult(new("cargo:own", "stale-detail-never-show", "검토", "조건", true, [])); await pending;
        Assert.Empty(model.Items); Assert.Null(model.Detail);
        identity.Switch("owner"); Assert.Null(model.Detail); Assert.Empty(model.Items);
    }

    [Theory]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(500)]
    public async Task 실패는_정상빈목록이나_서버원문으로_표시하지_않는다(int status)
    {
        var identity = new Identity(); var client = new Client { Mine = _ => throw new SsalddelApiException("secret-server-message", status, "private", "secret-response", null) };
        using var model = new FreightBrokerWorkspaceViewModel(client, identity, identity);
        await model.LoadAsync();
        Assert.False(model.HasLoaded); Assert.Empty(model.Items); Assert.NotNull(model.Error);
        Assert.DoesNotContain("secret", model.Error!); Assert.Equal(status == 401, model.RequiresLogin); Assert.Equal(status == 403, model.AccessDenied);
    }

    [Fact]
    public async Task 모바일화면은_실제공고링크와_로그인동작을_제공하고_가짜업체금융을_추가하지_않는다()
    {
        var identity = new Identity(); identity.Switch(null);
        using var model = new FreightBrokerWorkspaceViewModel(new Client(), identity, identity);
        var services = new ServiceCollection(); services.AddLogging(); services.AddSingleton(model); services.AddSingleton<IRoleWorkspaceAccess>(new Access());
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        var html = await renderer.Dispatcher.InvokeAsync(async () => WebUtility.HtmlDecode(
            (await renderer.RenderComponentAsync<FreightBrokerWorkspaceScreen>(ParameterView.Empty)).ToHtmlString()));
        Assert.Contains("운송주선업무", html); Assert.Contains("내 공고 작성·관리", html);
        Assert.Contains("/shipper/cargo-notices", html); Assert.Contains("/community/cargo-notices", html);
        Assert.Contains("/workspace-login/freight-broker", html);
        Assert.DoesNotContain("배차 확정", html); Assert.DoesNotContain("수수료 지급", html); Assert.DoesNotContain("/admin", html);
    }

    [Fact]
    public async Task 최초진입은_저장된주계정을_복원한뒤_본인업무를_조회한다()
    {
        var identity = new Identity(); identity.Switch(null); var client = new Client();
        using var model = new FreightBrokerWorkspaceViewModel(client, identity, identity);
        var access = new Access { Initialize = _ => { identity.Switch("owner"); return Task.CompletedTask; } };
        var html = await RenderAsync(model, access);
        Assert.Equal(RoleWorkspaceCatalog.FreightBroker, Assert.Single(access.InitializedRoles));
        Assert.Equal(1, client.MineCalls); Assert.Contains("행사 물품", html);
        Assert.DoesNotContain("로그인하면 본인이", html);
    }

    [Fact]
    public async Task 로그인복원실패는_익명이나_빈원장으로_감추지_않고_재확인한다()
    {
        var identity = new Identity(); var client = new Client();
        using var model = new FreightBrokerWorkspaceViewModel(client, identity, identity);
        var access = new Access { Initialize = _ => throw new InvalidOperationException("private-auth-failure") };
        var html = await RenderAsync(model, access);
        Assert.Equal(0, client.MineCalls); Assert.Contains("저장된 로그인 상태를 확인하지 못했습니다", html);
        Assert.Contains("로그인 상태 다시 확인", html); Assert.DoesNotContain("private-auth-failure", html);
        Assert.DoesNotContain("현재 조회할 본인 화물 협업 원장이 없습니다", html);
    }

    private static async Task<string> RenderAsync(FreightBrokerWorkspaceViewModel model, IRoleWorkspaceAccess access)
    {
        var services = new ServiceCollection(); services.AddLogging(); services.AddSingleton(model); services.AddSingleton(access);
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () => WebUtility.HtmlDecode(
            (await renderer.RenderComponentAsync<FreightBrokerWorkspaceScreen>(ParameterView.Empty)).ToHtmlString()));
    }

    private static PlatformCommunityPostLedgerChoiceResponse Row(string id = "cargo:own") => new()
    { 원장Id = id, 원장템플릿Key = CommunityLedgerTemplateKeys.CargoTransport, WorkflowTag = FreightBrokerWorkspaceClient.WorkflowTag,
        내접근원장여부 = true, 제목 = "행사 물품", 상태 = "검토", 현재단계 = "조건", 참여역할 = "생성자" };
    private static PlatformCommunityPostLedgerContextResponse Context() => new()
    { 원장Id = "cargo:own", 원장템플릿Key = CommunityLedgerTemplateKeys.CargoTransport, 업무분류Code = "cargo-transport",
        상세조회가능여부 = true, 기능활성화여부 = true, 제목 = "행사 물품", 상태 = "검토", 현재단계 = "조건" };

    private sealed class Identity : ISsalddel현재사용자Context, ISsalddel현재사용자변경알림, IWorkflowRecoveryContext
    {
        private string? owner = "owner"; private long revision = 1;
        public string Endpoint { get; set; } = new('a', 32);
        public 현재사용자Snapshot 현재사용자 => new(owner, "합성 사용자", []);
        public event Action? Changed;
        public WorkflowRecoverySnapshot Capture() => new(owner ?? "", revision, Endpoint);
        public bool IsCurrent(WorkflowRecoverySnapshot snapshot) => snapshot == Capture();
        public void Switch(string? next) { owner = next; revision++; Changed?.Invoke(); }
    }
    private sealed class Client : IFreightBrokerWorkspaceClient
    {
        public int MineCalls { get; private set; } public int DetailCalls { get; private set; }
        public Func<CancellationToken, Task<IReadOnlyList<FreightBrokerLedgerItem>>> Mine { get; set; }
            = _ => Task.FromResult<IReadOnlyList<FreightBrokerLedgerItem>>([new("cargo:own", "행사 물품", "검토", "조건", "생성자")]);
        public Func<string, CancellationToken, Task<FreightBrokerLedgerDetail?>> Detail { get; set; }
            = (id, _) => Task.FromResult<FreightBrokerLedgerDetail?>(new(id, "행사 물품", "검토", "조건", true, []));
        public Task<IReadOnlyList<FreightBrokerLedgerItem>> MineAsync(CancellationToken cancellationToken = default) { MineCalls++; return Mine(cancellationToken); }
        public Task<FreightBrokerLedgerDetail?> DetailAsync(string ledgerId, CancellationToken cancellationToken = default) { DetailCalls++; return Detail(ledgerId, cancellationToken); }
    }
    private sealed class Access : IRoleWorkspaceAccess
    {
        public event Action? Changed { add { } remove { } }
        public List<string> InitializedRoles { get; } = [];
        public Func<CancellationToken, Task> Initialize { get; set; } = _ => Task.CompletedTask;
        public RoleWorkspaceIdentity GetIdentity(string roleKey) => new(null, 0, false);
        public Task EnsureInitializedAsync(string roleKey, CancellationToken cancellationToken = default)
        { InitializedRoles.Add(roleKey); return Initialize(cancellationToken); }
        public Task SignInAsync(string roleKey, string name, string password, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SignOutAsync(string roleKey, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
    private sealed class JsonApi : ISsalddelJsonApiClient
    {
        public List<string> Paths { get; } = []; public int Writes { get; private set; }
        public Func<Type, string, Task<object?>> Read { get; set; } = (_, _) => Task.FromResult<object?>(null);
        public async Task<TResponse?> GetAsync<TResponse>(string path, string operationName, bool allowNotFound = true, CancellationToken cancellationToken = default)
        { Paths.Add(path); return (TResponse?)await Read(typeof(TResponse), path); }
        public Task<TResponse?> SendAsync<TResponse>(HttpMethod method, string path, string operationName, bool allowNotFound = false, CancellationToken cancellationToken = default)
        { Writes++; throw new InvalidOperationException("No write expected"); }
        public Task<TResponse?> SendAsync<TRequest, TResponse>(HttpMethod method, string path, TRequest request, string operationName, bool allowNotFound = false, CancellationToken cancellationToken = default)
        { Writes++; throw new InvalidOperationException("No write expected"); }
        public Task SendAsync(HttpMethod method, string path, string operationName, CancellationToken cancellationToken = default)
        { Writes++; throw new InvalidOperationException("No write expected"); }
        public Task SendAsync<TRequest>(HttpMethod method, string path, TRequest request, string operationName, CancellationToken cancellationToken = default)
        { Writes++; throw new InvalidOperationException("No write expected"); }
    }
}
