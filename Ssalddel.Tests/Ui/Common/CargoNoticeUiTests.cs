using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Cargo;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.Tests.Ui.Common;

public sealed class CargoNoticeUiTests
{
    private static readonly string NoticeId = "cargo-notice:" + new string('a', 64);

    [Fact]
    public async Task 실제_공고ID는_소유자조회와_명령_경로를_그대로_왕복한다()
    {
        var api = new Api(); var client = new CargoNoticeClient(api); var id = Guid.NewGuid();
        await client.OwnerAsync(NoticeId);
        await client.UpdateAsync(NoticeId, new() { ClientRequestId = id });
        await client.PublishAsync(NoticeId, new() { ClientRequestId = id });
        await client.WithdrawAsync(NoticeId, new() { ClientRequestId = id });
        await client.ResultAsync(id, NoticeId);
        Assert.Equal(5, api.Paths.Count);
        Assert.All(api.Paths, path => Assert.Contains(NoticeId, path));
        Assert.All(api.Paths, path => Assert.StartsWith(CargoNoticeRoutes.Api + "/", path));
    }

    [Theory]
    [InlineData("../../requests")]
    [InlineData("cargo-notice:abc")]
    [InlineData("0123456789abcdef01234567")]
    public async Task 임의_경로는_공고_API로_전달하지않는다(string id)
    {
        var api = new Api(); var client = new CargoNoticeClient(api);
        await Assert.ThrowsAsync<ArgumentException>(() => client.OwnerAsync(id));
        Assert.Empty(api.Paths);
    }

    [Fact]
    public async Task 결과404와_응답유실은_새_요청이나_자동POST를_만들지않는다()
    {
        var client = new Client { Send = _ => Task.FromResult<CargoNoticeCommandResponse?>(null) };
        var identity = new Identity(); var store = new VolatileWorkflowRecoveryStore();
        using (var model = Model(client, identity, store))
        {
            await model.LoadAsync(); Fill(model); await model.SaveAsync();
            Assert.True(model.HasPending); Assert.False(model.CanEdit); Assert.Equal(1, client.SendCount);
            await model.ResolvePendingAsync(); Assert.True(model.HasPending); Assert.Equal(1, client.SendCount);
        }
        using var restored = Model(client, identity, store);
        await restored.LoadAsync();
        Assert.True(restored.HasPending); Assert.True(restored.CanRetryPending); Assert.Equal(1, client.SendCount);
    }

    [Fact]
    public async Task 명시적_재전송도_기존_원요청ID와_payload를_유지한다()
    {
        var client = new Client();
        client.Send = request => Task.FromResult<CargoNoticeCommandResponse?>(client.SendCount == 1 ? null : Receipt(request));
        using var model = Model(client); await model.LoadAsync(); Fill(model); await model.SaveAsync();
        await model.RetryPendingAsync();
        Assert.Equal(2, client.RequestIds.Count); Assert.Equal(client.RequestIds[0], client.RequestIds[1]);
        Assert.False(model.HasPending); Assert.NotNull(model.Current);
    }

    [Fact]
    public async Task 복구_영수증은_과거_사본이며_최신_서버_공고를_덮지않는다()
    {
        var client = new Client { Send = _ => Task.FromResult<CargoNoticeCommandResponse?>(null) };
        var store = new VolatileWorkflowRecoveryStore(); var identity = new Identity();
        using (var original = Model(client, identity, store))
        { await original.LoadAsync(); Fill(original); await original.SaveAsync(); }
        var old = Receipt(client.RequestIds[0]); old.Notice.Content.Title = "과거 저장";
        client.Result = (_, _) => Task.FromResult<CargoNoticeCommandResponse?>(old);
        client.Current.Content.Title = "다른 화면의 최신 내용"; client.Current.Revision = 9;
        using var reopened = Model(client, identity, store); await reopened.LoadAsync();
        Assert.Equal(9, reopened.Current!.Revision); Assert.Equal("다른 화면의 최신 내용", reopened.Form.Title);
        Assert.False(reopened.HasPending); Assert.Equal(1, client.SendCount);
    }

    [Fact]
    public async Task 생성영수증_이후_최신상세조회가_계속실패하면_새_생성을_허용하지않는다()
    {
        var client = new Client { Owner = _ => throw new HttpRequestException("synthetic lost owner response") };
        using var model = Model(client);
        await model.LoadAsync(); Fill(model); await model.SaveAsync();

        Assert.False(model.HasPending); // The successful original receipt is known.
        Assert.True(model.RequiresRefresh); Assert.False(model.CanEdit); Assert.Null(model.Current);
        Assert.Equal(1, client.SendCount);

        await model.LoadAsync(); // Mine succeeds again, but the selected notice detail still fails.
        Assert.True(model.HasLoaded); Assert.NotEmpty(model.Items);
        Assert.True(model.RequiresRefresh); Assert.False(model.CanEdit); Assert.Null(model.Current);
        await model.SaveAsync();
        Assert.Equal(1, client.SendCount); Assert.Single(client.RequestIds);
    }

    [Fact]
    public async Task 계정변경은_비공개_화면을_비우고_늦은_다른계정_응답을_무시한다()
    {
        var client = new Client(); var identity = new Identity();
        using var model = Model(client, identity); await model.LoadAsync();
        var response = new TaskCompletionSource<CargoNoticeOwnerDto?>(TaskCreationOptions.RunContinuationsAsynchronously);
        client.Owner = _ => response.Task; var read = model.SelectAsync(NoticeId);
        identity.Change("other-owner"); response.SetResult(Draft()); await read;
        Assert.Null(model.Current); Assert.Empty(model.Items); Assert.Empty(model.Form.Title); Assert.False(model.HasActiveContext);
    }

    [Fact]
    public async Task 서버전환_신호가_없어도_늦은응답과_명령은_현재문맥에_들어오지않는다()
    {
        var client = new Client(); var identity = new Identity();
        using var model = Model(client, identity); await model.LoadAsync();
        var response = new TaskCompletionSource<CargoNoticeOwnerDto?>(TaskCreationOptions.RunContinuationsAsynchronously);
        client.Owner = _ => response.Task; var read = model.SelectAsync(NoticeId);
        identity.EndpointKey = new string('b', 32); response.SetResult(Draft()); await read;
        Assert.False(model.CanEdit); Assert.False(model.HasActiveContext); Assert.Null(model.Current);
        await model.SaveAsync(); Assert.Equal(0, client.SendCount);
    }

    [Fact]
    public async Task 보호저장이_실패하면_변경_요청을_전송하지않는다()
    {
        var client = new Client(); using var model = Model(client, store: new FailingStore());
        await model.LoadAsync(); Fill(model); await model.SaveAsync();
        Assert.Equal(0, client.SendCount); Assert.NotNull(model.Error);
    }

    [Theory]
    [InlineData("010-1234-5678")]
    [InlineData("서울 면목로 203")]
    [InlineData("a@example.invalid")]
    [InlineData("https://example.invalid")]
    [InlineData("상품\u200b")]
    public async Task 대표_개인정보와_숨은문자는_명령전_차단한다(string text)
    {
        var client = new Client(); using var model = Model(client); await model.LoadAsync(); Fill(model);
        model.Form.Title = text; await model.SaveAsync(); Assert.Equal(0, client.SendCount); Assert.NotNull(model.Error);
    }

    [Fact]
    public async Task 게시에는_저장된_내용과_새_명시동의가_필요하다()
    {
        var client = new Client(); using var model = Model(client); await model.LoadAsync(); await model.SelectAsync(NoticeId);
        Assert.False(model.CanPublish); model.Consent = true; Assert.True(model.CanPublish);
        model.Form.Title = "바뀐 제목"; model.DraftChanged(); Assert.False(model.Consent); Assert.False(model.CanPublish);
        await model.PublishAsync(); Assert.Equal(0, client.SendCount);
    }

    [Fact]
    public async Task 공개목록_이전페이지_응답은_새페이지를_덮지않는다()
    {
        var client = new Client(); var first = new TaskCompletionSource<CargoNoticeListResponse?>(TaskCreationOptions.RunContinuationsAsynchronously);
        client.Public = page => page == 1 ? first.Task : Task.FromResult<CargoNoticeListResponse?>(new() { Page = 2 });
        using var model = new CargoNoticeListViewModel(client); var old = model.LoadAsync(); await model.LoadAsync(2);
        first.SetResult(new() { Page = 1 }); await old; Assert.Equal(2, model.Page);
    }

    [Fact]
    public async Task 공개화면은_공개조건만_보이고_내부식별자와_수락버튼을_보이지않는다()
    {
        var client = new Client(); var services = new ServiceCollection(); services.AddLogging();
        services.AddTransient(_ => new CargoNoticeListViewModel(client));
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        var html = await renderer.Dispatcher.InvokeAsync(async () => (await renderer.RenderComponentAsync<CargoNoticeListScreen>(ParameterView.Empty)).ToHtmlString());
        html = WebUtility.HtmlDecode(html);
        Assert.Contains("화물 정보 공고", html); Assert.Contains("행사 물품", html);
        Assert.DoesNotContain(NoticeId, html); Assert.DoesNotContain("수락", html); Assert.DoesNotContain("개정", html);
        Assert.Contains(CargoNoticeNavigation.Mine, html);
    }

    [Theory]
    [InlineData("shipper", "/shipper/cargo-notices", "/shipper/cargo-notices")]
    [InlineData("cargo-driver", "/shipper/cargo-notices", null)]
    [InlineData("shipper", "/shipper/cargo-notices/private", null)]
    [InlineData("shipper", "//evil.invalid/shipper/cargo-notices", null)]
    public void 로그인복귀는_새_내공고_정확경로만_추가한다(string role, string path, string? expected)
        => Assert.Equal(expected, CargoPublicBrowseNavigation.LoginReturn(role, path));

    private static CargoNoticeManageViewModel Model(Client client, Identity? identity = null, IWorkflowRecoveryStore? store = null)
    { identity ??= new Identity(); return new(client, identity, identity, store ?? new VolatileWorkflowRecoveryStore(), new()); }
    private static void Fill(CargoNoticeManageViewModel model)
    { model.Form.Title = "행사 물품"; model.Form.GoodsName = "상자"; model.Form.Quantity = "2"; model.Form.PickupRegionKey = "jn"; model.Form.DropoffRegionKey = "ddm"; }
    private static CargoNoticeOwnerDto Draft() => new() { NoticeId = NoticeId, Revision = 1, ContentRevision = 1,
        Content = new() { Title = "행사 물품", GoodsName = "상자", Quantity = 2, PickupRegionKey = "jn", DropoffRegionKey = "ddm" }, UpdatedAtUtc = DateTimeOffset.UtcNow };
    private static CargoNoticeCommandResponse Receipt(Guid request) => new() { ClientRequestId = request, Operation = "create", Notice = Draft(), AppliedAtUtc = DateTimeOffset.UtcNow };

    private sealed class Identity : ISsalddel현재사용자Context, ISsalddel현재사용자변경알림, IWorkflowRecoveryContext
    {
        private string owner = "owner"; private long revision = 1;
        public string EndpointKey { get; set; } = new('a', 32);
        public 현재사용자Snapshot 현재사용자 => new(owner, "합성 작성자", []);
        public event Action? Changed;
        public WorkflowRecoverySnapshot Capture() => new(owner, revision, EndpointKey);
        public bool IsCurrent(WorkflowRecoverySnapshot snapshot) => snapshot == Capture();
        public void Change(string next) { owner = next; revision++; Changed?.Invoke(); }
    }
    private sealed class Client : ICargoNoticeClient
    {
        public CargoNoticeOwnerDto Current { get; } = Draft();
        public int SendCount { get; private set; }
        public List<Guid> RequestIds { get; } = [];
        public Func<Guid, Task<CargoNoticeCommandResponse?>> Send { get; set; } = id => Task.FromResult<CargoNoticeCommandResponse?>(Receipt(id));
        public Func<string, Task<CargoNoticeOwnerDto?>>? Owner { get; set; }
        public Func<Guid, string?, Task<CargoNoticeCommandResponse?>> Result { get; set; } = (_, _) => throw new SsalddelApiException("private", 404, "", "", null, errorCode: "CargoNotice.NotFound");
        public Func<int, Task<CargoNoticeListResponse?>>? Public { get; set; }
        public Task<CargoNoticeListResponse?> ListAsync(int page, CancellationToken ct = default) => Public?.Invoke(page) ?? Task.FromResult<CargoNoticeListResponse?>(new()
        { Items = [new() { NoticeId = NoticeId, Content = Current.Content, PickupRegionName = "중랑구", DropoffRegionName = "동대문구" }] });
        public Task<IReadOnlyList<CargoNoticeRegion>?> RegionsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<CargoNoticeRegion>?>([new() { RegionKey = "jn", DisplayName = "중랑구" }, new() { RegionKey = "ddm", DisplayName = "동대문구" }]);
        public Task<CargoNoticeOwnerListResponse?> MineAsync(int page, CancellationToken ct = default) => Task.FromResult<CargoNoticeOwnerListResponse?>(new() { Items = [Current], Page = page });
        public Task<CargoNoticeOwnerDto?> OwnerAsync(string noticeId, CancellationToken ct = default) => Owner?.Invoke(noticeId) ?? Task.FromResult<CargoNoticeOwnerDto?>(Current);
        public Task<CargoNoticeCommandResponse?> ResultAsync(Guid requestId, string? noticeId, CancellationToken ct = default) => Result(requestId, noticeId);
        private Task<CargoNoticeCommandResponse?> Write(Guid request) { SendCount++; RequestIds.Add(request); return Send(request); }
        public Task<CargoNoticeCommandResponse?> CreateAsync(CargoNoticeDraftRequest request, CancellationToken ct = default) => Write(request.ClientRequestId);
        public Task<CargoNoticeCommandResponse?> UpdateAsync(string noticeId, CargoNoticeDraftRequest request, CancellationToken ct = default) => Write(request.ClientRequestId);
        public Task<CargoNoticeCommandResponse?> PublishAsync(string noticeId, CargoNoticePublishRequest request, CancellationToken ct = default) => Write(request.ClientRequestId);
        public Task<CargoNoticeCommandResponse?> WithdrawAsync(string noticeId, CargoNoticeMutationRequest request, CancellationToken ct = default) => Write(request.ClientRequestId);
    }
    private sealed class FailingStore : IWorkflowRecoveryStore
    {
        public Task<WorkflowRecoveryRecord?> LoadAsync(string owner, string scope, CancellationToken ct) => Task.FromResult<WorkflowRecoveryRecord?>(null);
        public Task SaveAsync(WorkflowRecoveryRecord record, CancellationToken ct) => throw new IOException("private storage path");
        public Task RemoveAsync(string owner, string scope, Guid request, CancellationToken ct) => throw new NotSupportedException();
    }
    private sealed class Api : ISsalddelJsonApiClient
    {
        public List<string> Paths { get; } = [];
        public Task<T?> GetAsync<T>(string path, string operation, bool allowNotFound = true, CancellationToken ct = default) { Paths.Add(path); return Task.FromResult<T?>(default); }
        public Task<T?> SendAsync<T>(HttpMethod method, string path, string operation, bool allowNotFound = false, CancellationToken ct = default) { Paths.Add(path); return Task.FromResult<T?>(default); }
        public Task<TResponse?> SendAsync<TRequest, TResponse>(HttpMethod method, string path, TRequest request, string operation, bool allowNotFound = false, CancellationToken ct = default) { Paths.Add(path); return Task.FromResult<TResponse?>(default); }
        public Task SendAsync(HttpMethod method, string path, string operation, CancellationToken ct = default) => throw new NotSupportedException();
        public Task SendAsync<T>(HttpMethod method, string path, T request, string operation, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
