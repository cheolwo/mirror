using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using RestaurantDeskApp.Components.Pages;
using RestaurantDeskApp.Services;
using Ssalddel.Client.Infrastructure.Security;
using Ssalddel.Contracts.Common;
using Ssalddel.Contracts.Food;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.Tests.Clients;

public sealed class RestaurantMenuSessionRecoveryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DefinitiveRejectionHidesBusinessAndAllowsOnlyLoginWithoutDiscardingRequest(bool api401)
    {
        await using var fixture = new Fixture();
        var page = await fixture.MountAsync();
        await fixture.BeginNewAsync(page, "확인 중 메뉴");
        fixture.Client.CreateError = api401
            ? new SsalddelApiException("통제된 인증 만료", 401, "메뉴 저장", "", null)
            : new UnauthorizedAccessException("통제된 인증 만료");

        await fixture.CallAsync(page, "SaveAsync");

        Assert.True(Get<bool>(page, "requiresLogin"));
        Assert.False(Get<bool>(page, "loaded"));
        Assert.Empty(Get<IReadOnlyList<음식점메뉴관리응답>>(page, "menus"));
        Assert.False(fixture.Auth.Session.IsAuthenticated);
        Assert.EndsWith("/login", fixture.Navigation.Uri);
        Assert.Equal(fixture.Client.Requests.Single(), Get<음식점메뉴등록요청>(page, "pendingCreate").클라이언트요청Id);
        Assert.True(fixture.CanLogin(page, "http://localhost/login"));
        Assert.False(fixture.CanLogin(page, "http://localhost/orders"));
        Assert.False(fixture.CanLogin(page, "http://localhost/login-other"));
        Assert.False(fixture.CanLogin(page, "https://external.invalid/login"));
    }

    [Fact]
    public async Task SameOwnerReauthRestoresInputAndSameRequestButDoesNotAutomaticallySubmit()
    {
        await using var fixture = new Fixture();
        var first = await fixture.MountAsync();
        await fixture.BeginNewAsync(first, "응답을 확인할 메뉴");
        fixture.Client.CreateError = new UnauthorizedAccessException("통제된 응답 유실 및 만료");
        await fixture.CallAsync(first, "SaveAsync");
        var requestId = Get<음식점메뉴등록요청>(first, "pendingCreate").클라이언트요청Id;
        first.Dispose();

        await fixture.LoginAsync("owner-a");
        Assert.Equal("/menus", fixture.Drafts.GetReturnRoute("owner-a"));
        fixture.Client.CreateError = null;
        var resumed = await fixture.MountAsync();

        Assert.Equal("응답을 확인할 메뉴", Get<string>(resumed, "name"));
        Assert.Equal(requestId, Get<음식점메뉴등록요청>(resumed, "pendingCreate").클라이언트요청Id);
        Assert.True(Get<bool>(resumed, "editing"));
        Assert.Single(fixture.Client.Requests);
        await fixture.CallAsync(resumed, "SaveAsync");
        Assert.Equal(2, fixture.Client.Requests.Count);
        Assert.Single(fixture.Client.Requests.Distinct());
        Assert.Single(fixture.Client.Items);
        Assert.Null(Get<object?>(resumed, "pendingCreate"));
        Assert.False(Get<bool>(resumed, "editing"));
    }

    [Fact]
    public async Task DifferentOwnerCannotRestoreOrCapturePreviousOwnersDraft()
    {
        await using var fixture = new Fixture();
        var first = await fixture.MountAsync();
        await fixture.BeginNewAsync(first, "이전 가게 메뉴");
        fixture.Client.CreateError = new UnauthorizedAccessException();
        await fixture.CallAsync(first, "SaveAsync");
        var oldGeneration = Get<long>(first, "draftGeneration");
        first.Dispose();

        await fixture.LoginAsync("owner-b");
        Assert.Null(fixture.Drafts.GetReturnRoute("owner-b"));
        var next = await fixture.MountAsync(new MenuClient());
        Assert.Equal("", Get<string>(next, "name"));
        Assert.False(Get<bool>(next, "editing"));
        Assert.Null(Get<object?>(next, "pendingCreate"));
        fixture.Drafts.Capture("owner-a", oldGeneration, new(true, null, 0, 0, "늦은 입력", "", "", 1, false, false, null));
        Assert.Null(fixture.Drafts.Restore("owner-b", Get<long>(next, "draftGeneration")));
    }

    [Fact]
    public async Task ExplicitLogoutClearsSuspendedDraftEvenWhenItsPageWasDisposed()
    {
        await using var fixture = new Fixture();
        var first = await fixture.MountAsync();
        await fixture.BeginNewAsync(first, "폐기할 입력");
        fixture.Client.CreateError = new UnauthorizedAccessException();
        await fixture.CallAsync(first, "SaveAsync");
        first.Dispose();
        await fixture.Auth.LogoutAsync();
        await fixture.LoginAsync("owner-a");

        Assert.Null(fixture.Drafts.GetReturnRoute("owner-a"));
        var resumed = await fixture.MountAsync();
        Assert.False(Get<bool>(resumed, "editing"));
        Assert.Null(Get<object?>(resumed, "pendingCreate"));
        Assert.Equal("", Get<string>(resumed, "name"));
    }

    [Fact]
    public async Task LateCreateSuccessCannotClearNewOwnersUnconfirmedRequest()
    {
        await using var fixture = new Fixture();
        var first = await fixture.MountAsync();
        await fixture.BeginNewAsync(first, "이전 요청");
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var delayed = new TaskCompletionSource<음식점메뉴관리응답>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Client.Create = (_, _) => { started.SetResult(); return delayed.Task; };
        var saving = fixture.CallAsync(first, "SaveAsync");
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await fixture.Auth.LogoutAsync();
        await fixture.LoginAsync("owner-b");
        var other = new MenuClient { CreateError = new HttpRequestException("통제된 응답 유실") };
        var next = await fixture.MountAsync(other);
        await fixture.BeginNewAsync(next, "새 가게 요청");
        await fixture.CallAsync(next, "SaveAsync");
        var pending = Get<음식점메뉴등록요청>(next, "pendingCreate");

        delayed.SetResult(new() { Id = 99, 메뉴명 = "이전 요청" });
        await saving;

        Assert.True(fixture.Auth.Session.IsAuthenticated);
        Assert.Equal("owner-b", fixture.Auth.Session.UserId);
        Assert.Equal(pending.클라이언트요청Id,
            fixture.Drafts.Restore("owner-b", Get<long>(next, "draftGeneration"))!.PendingCreate!.클라이언트요청Id);
        Assert.True(Get<bool>(next, "editing"));
    }

    [Fact]
    public async Task LateUnauthorizedFromOldOwnerCannotInvalidateNewOwnersSession()
    {
        await using var fixture = new Fixture();
        var first = await fixture.MountAsync();
        await fixture.BeginNewAsync(first, "이전 요청");
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var delayed = new TaskCompletionSource<음식점메뉴관리응답>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Client.Create = (_, _) => { started.SetResult(); return delayed.Task; };
        var saving = fixture.CallAsync(first, "SaveAsync");
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await fixture.LoginAsync("owner-b");
        fixture.Drafts.BindOwner("owner-b");
        delayed.SetException(new UnauthorizedAccessException("통제된 이전 계정의 늦은 실패"));
        await saving;

        Assert.True(fixture.Auth.Session.IsAuthenticated);
        Assert.Equal("owner-b", fixture.Auth.Session.UserId);
        Assert.False(Get<bool>(first, "requiresLogin"));
    }

    [Fact]
    public async Task SaveFromPreviousOwnersPageCannotEndNewSessionOrReplaceNewDraft()
    {
        await using var fixture = new Fixture();
        var first = await fixture.MountAsync();
        await fixture.BeginNewAsync(first, "이전 계정 입력");
        await fixture.LoginAsync("owner-b");
        var generation = fixture.Drafts.BindOwner("owner-b");
        var nextRequest = new 음식점메뉴등록요청 { 클라이언트요청Id = Guid.NewGuid(), 메뉴명 = "새 계정 요청" };
        fixture.Drafts.Capture("owner-b", generation, new(true, null, 0, 0, "새 계정 요청", "", "", 1, false, false, nextRequest));
        await fixture.CallAsync(first, "SaveAsync");
        Assert.True(fixture.Auth.Session.IsAuthenticated);
        Assert.Equal("owner-b", fixture.Auth.Session.UserId);
        Assert.Empty(fixture.Client.Requests);
        Assert.Equal("", Get<string>(first, "name"));
        Assert.EndsWith("/orders", fixture.Navigation.Uri);
        Assert.Equal(nextRequest.클라이언트요청Id, fixture.Drafts.Restore("owner-b", generation)!.PendingCreate!.클라이언트요청Id);
    }

    [Fact]
    public async Task ReadFinishingAfterDisposalCannotRestoreBusinessSnapshot()
    {
        await using var fixture = new Fixture();
        var page = await fixture.MountAsync();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var delayed = new TaskCompletionSource<IReadOnlyList<음식점메뉴관리응답>>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Client.Read = _ => { started.SetResult(); return delayed.Task; };
        var reading = fixture.CallAsync(page, "ReloadAsync");
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        page.Dispose();
        delayed.SetResult([new() { Id = 12, 메뉴명 = "늦은 목록" }]);
        await reading;
        Assert.Empty(Get<IReadOnlyList<음식점메뉴관리응답>>(page, "menus"));
    }

    [Fact]
    public async Task NetworkFailureKeepsUnconfirmedRequestAndConfirmedSaveReadFailureDoesNotRecreate()
    {
        await using var fixture = new Fixture();
        var page = await fixture.MountAsync();
        await fixture.BeginNewAsync(page, "응답 유실");
        fixture.Client.CreateError = new HttpRequestException("통제된 응답 유실");
        await fixture.CallAsync(page, "SaveAsync");
        Assert.False(Get<bool>(page, "requiresLogin"));
        Assert.True(fixture.Auth.Session.IsAuthenticated);
        await fixture.CallAsync(page, "CloseEditor");
        Assert.True(Get<bool>(page, "editing"));

        fixture.Client.CreateError = null;
        fixture.Client.ReadError = new UnauthorizedAccessException("통제된 저장 후 목록 인증 실패");
        await fixture.CallAsync(page, "SaveAsync");
        Assert.True(Get<bool>(page, "requiresLogin"));
        await fixture.LoginAsync("owner-a");
        var draft = fixture.Drafts.Restore("owner-a", fixture.Drafts.BindOwner("owner-a"));
        Assert.Null(draft!.PendingCreate);
        Assert.False(draft.Editing);
        Assert.Single(fixture.Client.Items);
        Assert.Single(fixture.Client.Requests.Distinct());
    }

    [Fact]
    public async Task RealClientFinal401SharesSessionEndWithMenuAndPreservesRequest()
    {
        await using var fixture = new Fixture();
        var posts = 0;
        using var http = new HttpClient(new Handler(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/refresh"))
                return new(HttpStatusCode.OK) { Content = JsonContent.Create(new 토큰응답
                { AccessToken = "refreshed-test", AccessTokenExpiresAtUtc = DateTime.UtcNow.AddHours(1),
                    RefreshToken = "refresh-test", RefreshTokenExpiresAtUtc = DateTime.UtcNow.AddDays(1),
                    UserId = "owner-a", UserName = "시험 가게", Roles = ["음식점"] }) };
            if (request.Method == HttpMethod.Get)
                return new(HttpStatusCode.OK) { Content = JsonContent.Create(Array.Empty<음식점메뉴관리응답>()) };
            posts++;
            return new(HttpStatusCode.Unauthorized);
        })) { BaseAddress = new("http://controlled.invalid/") };
        var auth = new RestaurantAuthService(http, fixture.Auth.Session);
        using var drafts = new RestaurantMenuDraftStore(auth);
        var client = new Ssalddel음식주문Client(http, auth, auth.Session);
        var page = await fixture.MountAsync(client, auth, drafts);
        await fixture.BeginNewAsync(page, "실제 Client 계약 검증");
        await fixture.CallAsync(page, "SaveAsync");
        Assert.Equal(2, posts);
        Assert.False(auth.Session.IsAuthenticated);
        Assert.True(Get<bool>(page, "requiresLogin"));
        Assert.NotEqual(Guid.Empty, Get<음식점메뉴등록요청>(page, "pendingCreate").클라이언트요청Id);
        Assert.EndsWith("/login", fixture.Navigation.Uri);
    }

    private static T Get<T>(Menus page, string name)
        => (T)typeof(Menus).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(page)!;

    // 코드뒤 파일의 실제 lifecycle/dispatch를 검증하며 Razor 화면·Android 캡처 증거는 아니다.
#pragma warning disable BL0006
    private sealed class Fixture : IAsyncDisposable
    {
        public MenuClient Client { get; } = new();
        public Navigation Navigation { get; } = new();
        public RestaurantAuthService Auth { get; }
        public RestaurantMenuDraftStore Drafts { get; }
        private readonly HttpClient http = new(new Handler(_ => throw new InvalidOperationException("No auth HTTP expected")))
        { BaseAddress = new("http://controlled.invalid/") };
        private readonly TestRenderer renderer = new();
        private readonly List<Menus> pages = [];

        public Fixture()
        {
            Auth = new(http, new(new MemoryStore(), new ClientSessionGuard()));
            Drafts = new(Auth);
        }
        public Task LoginAsync(string owner)
            => Auth.Session.ApplyAsync(new("test-access", DateTime.UtcNow.AddHours(1), "test-refresh", DateTime.UtcNow.AddDays(1), owner, "시험 가게", ["음식점"]));

        public async Task<Menus> MountAsync(I음식점메뉴ApiClient? client = null, RestaurantAuthService? auth = null, RestaurantMenuDraftStore? drafts = null)
        {
            if (!Auth.Session.IsAuthenticated) await LoginAsync("owner-a");
            var page = new Menus { MenuClient = client ?? Client, AuthService = auth ?? Auth, Drafts = drafts ?? Drafts, Navigation = Navigation };
            pages.Add(page);
            await renderer.Dispatcher.InvokeAsync(async () => await renderer.MountAsync(renderer.Add(page)));
            return page;
        }
        public Task CallAsync(Menus page, string method) => renderer.Dispatcher.InvokeAsync(async () =>
        {
            var result = typeof(Menus).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(page, null);
            if (result is Task task) await task;
        });
        public async Task BeginNewAsync(Menus page, string name)
        {
            await CallAsync(page, "NewMenu");
            typeof(Menus).GetField("name", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(page, name);
            typeof(Menus).GetField("price", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(page, 7500m);
        }
        public bool CanLogin(Menus page, string target)
            => (bool)typeof(Menus).GetMethod("CanNavigateToLogin", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(page, [target])!;
        public async ValueTask DisposeAsync()
        {
            foreach (var page in pages) page.Dispose();
            await renderer.DisposeAsync();
            Drafts.Dispose();
            http.Dispose();
        }
    }
    private sealed class TestRenderer() : Renderer(new ServiceCollection().BuildServiceProvider(), NullLoggerFactory.Instance)
    {
        public override Dispatcher Dispatcher { get; } = Dispatcher.CreateDefault();
        public int Add(IComponent component) => AssignRootComponentId(component);
        public Task MountAsync(int id) => RenderRootComponentAsync(id, ParameterView.Empty);
        protected override Task UpdateDisplayAsync(in RenderBatch renderBatch) => Task.CompletedTask;
        protected override void HandleException(Exception exception) => throw exception;
    }
#pragma warning restore BL0006
    private sealed class Navigation : NavigationManager
    {
        public Navigation() => Initialize("http://localhost/", "http://localhost/menus");
        protected override void NavigateToCore(string uri, bool forceLoad) => Uri = ToAbsoluteUri(uri).ToString();
        protected override void NavigateToCore(string uri, NavigationOptions options) => Uri = ToAbsoluteUri(uri).ToString();
    }
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(send(request));
    }
    private sealed class MemoryStore : IClientSecureTokenStore
    {
        private ClientAuthTokenSnapshot? value;
        public Task<ClientAuthTokenSnapshot?> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(value);
        public Task SaveAsync(ClientAuthTokenSnapshot snapshot, CancellationToken cancellationToken = default)
        { value = snapshot; return Task.CompletedTask; }
        public Task ClearAsync(CancellationToken cancellationToken = default)
        { value = null; return Task.CompletedTask; }
    }
    private sealed class MenuClient : I음식점메뉴ApiClient
    {
        public Dictionary<Guid, 음식점메뉴관리응답> Items { get; } = [];
        public List<Guid> Requests { get; } = [];
        public Exception? CreateError { get; set; }
        public Exception? ReadError { get; set; }
        public Func<음식점메뉴등록요청, CancellationToken, Task<음식점메뉴관리응답>>? Create { get; set; }
        public Func<CancellationToken, Task<IReadOnlyList<음식점메뉴관리응답>>>? Read { get; set; }
        public Task<IReadOnlyList<음식점메뉴관리응답>> 목록Async(CancellationToken cancellationToken = default)
            => Read is not null ? Read(cancellationToken) : ReadError is not null
                ? Task.FromException<IReadOnlyList<음식점메뉴관리응답>>(ReadError)
                : Task.FromResult<IReadOnlyList<음식점메뉴관리응답>>(Items.Values.ToArray());
        public Task<음식점메뉴관리응답> 등록Async(음식점메뉴등록요청 request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request.클라이언트요청Id);
            if (Create is not null) return Create(request, cancellationToken);
            if (!Items.TryGetValue(request.클라이언트요청Id, out var item))
                Items.Add(request.클라이언트요청Id, item = new() { Id = Items.Count + 1, 메뉴명 = request.메뉴명, 판매가 = request.판매가, Revision = 1 });
            return CreateError is null ? Task.FromResult(item) : Task.FromException<음식점메뉴관리응답>(CreateError);
        }
        public Task<음식점메뉴관리응답> 수정Async(long menuId, 음식점메뉴수정요청 request, CancellationToken cancellationToken = default)
            => Task.FromResult(new 음식점메뉴관리응답 { Id = menuId, 메뉴명 = request.메뉴명, Revision = request.예상Revision + 1 });
    }
}
