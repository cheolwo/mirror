using System.Text.Json;
using Microsoft.JSInterop;
using Ssalddel.Client.Infrastructure.Security;
using Ssalddel.Contracts.Food;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.ViewModels;
using Ssalddel.WebApp.Services;

namespace Ssalddel.Tests.Ui.Common;

/// <summary>실제 웹 인증/탭 저장 adapter와 공통 인증 VM의 회귀. 실제 브라우저/기기 저장소 실행은 아닙니다.</summary>
public sealed class FoodOrderAuthenticationCleanupTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);
    private const string AuthKey = "ssalddel.web.auth.v1";
    private const string PendingKey = "ssalddel.food.pending-submission.v1";

    [Theory]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{not-json")]
    [InlineData("{\"OwnerId\":\"synthetic-owner\",\"Request\":null}")]
    [InlineData("{\"OwnerId\":\"synthetic-owner\",\"Request\":{}}")]
    [InlineData("{\"OwnerId\":\"\",\"Request\":{\"클라이언트요청Id\":\"c2d9ba10-bd5d-49d1-967a-fb806c103aaa\",\"상품목록\":[]}}")]
    [InlineData("{\"OwnerId\":\"synthetic-owner\",\"Request\":{\"클라이언트요청Id\":\"c2d9ba10-bd5d-49d1-967a-fb806c103aaa\",\"상품목록\":null}}")]
    [InlineData("{\"OwnerId\":\"synthetic-owner\",\"Request\":{\"클라이언트요청Id\":\"c2d9ba10-bd5d-49d1-967a-fb806c103aaa\",\"상품목록\":[null]}}")]
    public async Task 실제브라우저손상저장값은_빈제출로숨기지않고초기확인실패와입력잠금을유지한다(string json)
    {
        using var fixture = await WebFixture.CreateAsync();
        fixture.Js.Set(PendingKey, json);
        await using var encryption = new SsalddelIsmsPClientEncryptionService(fixture.Js);
        var client = fixture.FoodClient(encryption);
        using var recovery = new FoodOrderSubmissionRecoveryViewModel(fixture.PendingStore, client, client);

        await recovery.계정설정Async("synthetic-owner");

        Assert.False(recovery.초기확인완료);
        Assert.True(recovery.입력잠금);
        Assert.True(recovery.오류발생);
        Assert.NotNull(recovery.오류메시지);
        Assert.Null(recovery.Pending);
        Assert.Null(recovery.접수주문번호);
        Assert.False(recovery.미접수확인됨);
        Assert.False(await recovery.결과확인Async());
        Assert.False(await recovery.동일주문재제출Async());
        await Assert.ThrowsAsync<InvalidOperationException>(() => recovery.등록Async(new()
        {
            클라이언트요청Id = Guid.NewGuid(), 음식점Id = 7,
            상품목록 = [new() { 메뉴Id = 8, 상품명 = "다른 메뉴", 수량 = 1, 단가 = 3000 }]
        }, CancellationToken.None));
        Assert.Equal(json, fixture.Js.Get(PendingKey));
        Assert.Equal(0, fixture.Http.Calls); // 실제 protected/json client의 GET·POST 모두 호출하지 않습니다.
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task 실제웹adapter는_만료시미확정제출을보존하고명시로그아웃에서만제거한다(bool expired)
    {
        using var fixture = await WebFixture.CreateAsync();
        var original = fixture.Js.Get(PendingKey);

        if (expired) await fixture.Authentication.세션만료Async();
        else await fixture.Authentication.로그아웃Async();

        Assert.False(fixture.Session.IsLoggedIn);
        Assert.Null(fixture.Session.AccessToken);
        Assert.Null(fixture.Js.Get(AuthKey));
        Assert.Equal(expired ? original : null, fixture.Js.Get(PendingKey));
        var restored = await fixture.Authentication.복원Async();
        Assert.False(restored.세션.로그인됨);
        Assert.True(restored.성공);
        if (expired)
        {
            var pending = await fixture.PendingStore.LoadAsync();
            Assert.NotNull(pending);
            Assert.Equal(fixture.RequestId, pending.Request.클라이언트요청Id);
            Assert.Equal("synthetic-owner", pending.OwnerId);
            Assert.Equal("기존 선택 메뉴", Assert.Single(pending.Request.상품목록).상품명);
        }
        else Assert.Null(await fixture.PendingStore.LoadAsync());
        Assert.Equal(0, fixture.Http.Calls);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task 토큰저장소삭제가실패해도_공통인증화면은즉시익명화하고미확정제출을보존한다(bool expired)
    {
        using var fixture = await WebFixture.CreateAsync();
        var model = new 주문자앱인증ViewModel(fixture.Authentication);
        Assert.True(await model.복원Async());
        Assert.True(model.로그인됨);
        var original = fixture.Js.Get(PendingKey);
        fixture.Js.BlockRemoveKey = AuthKey;
        var notifications = new List<(bool IsLoggedIn, string? AccessToken)>();
        fixture.Session.Changed += () => notifications.Add((fixture.Session.IsLoggedIn, fixture.Session.AccessToken));

        var ending = expired ? model.세션만료Async() : model.로그아웃Async();
        await fixture.Js.RemoveStarted.Task.WaitAsync(Timeout);

        Assert.False(model.로그인됨);
        Assert.Null(model.세션.UserId);
        Assert.True(model.초기화됨);
        Assert.False(fixture.Session.IsLoggedIn);
        var notification = Assert.Single(notifications);
        Assert.False(notification.IsLoggedIn);
        Assert.Null(notification.AccessToken);
        Assert.Equal(original, fixture.Js.Get(PendingKey));
        fixture.Js.RemoveCompletion.SetException(new IOException("synthetic token storage failure"));
        Assert.False(await ending.WaitAsync(Timeout));

        Assert.False(model.로그인됨);
        Assert.True(model.오류발생);
        Assert.Contains(expired ? "다시 로그인" : "다시 시도", model.오류메시지);
        Assert.Single(notifications);
        Assert.Equal(original, fixture.Js.Get(PendingKey));
        Assert.Equal(0, fixture.Http.Calls);
    }

    [Fact]
    public async Task 미확정제출삭제실패는_로그인을남기지않고정리오류와재시도가능성을유지한다()
    {
        using var fixture = await WebFixture.CreateAsync();
        var model = new 주문자앱인증ViewModel(fixture.Authentication);
        Assert.True(await model.복원Async());
        var original = fixture.Js.Get(PendingKey);
        fixture.Js.BlockRemoveKey = PendingKey;

        var ending = model.로그아웃Async();
        await fixture.Js.RemoveStarted.Task.WaitAsync(Timeout);
        Assert.False(model.로그인됨);
        Assert.Null(fixture.Js.Get(AuthKey));
        fixture.Js.RemoveCompletion.SetException(new IOException("synthetic pending storage failure"));
        Assert.False(await ending.WaitAsync(Timeout));

        Assert.False(model.로그인됨);
        Assert.True(model.오류발생);
        Assert.Contains("정리", model.오류메시지);
        Assert.Equal(original, fixture.Js.Get(PendingKey));
        fixture.Js.BlockRemoveKey = null;
        Assert.True(await model.로그아웃Async());
        Assert.Null(fixture.Js.Get(PendingKey));
        Assert.False(model.오류발생);
        Assert.Equal(0, fixture.Http.Calls);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task 늦은인증복원또는로그인은_로그아웃뒤개인화면과토큰을다시살리지않는다(bool restore)
    {
        var service = new DelayedAuthentication();
        var model = new 주문자앱인증ViewModel(service);
        Assert.True(await model.복원Async());
        Assert.True(model.로그인됨);
        service.DelayNext = true;
        var old = restore ? model.복원Async() : model.로그인Async("synthetic-owner", "synthetic-password");
        await service.AuthenticationStarted.Task.WaitAsync(Timeout);

        var ending = model.로그아웃Async();
        Assert.True(service.AuthenticationToken.IsCancellationRequested);
        Assert.False(model.로그인됨);
        Assert.Null(model.세션.UserId);
        service.AuthenticationCompletion.SetResult(new(new(true, "late-owner", "늦은 인증")));

        Assert.False(await old.WaitAsync(Timeout));
        Assert.True(await ending.WaitAsync(Timeout));
        Assert.False(model.로그인됨);
        Assert.Null(model.세션.UserId);
        Assert.Null(service.StoredUserId);
        Assert.Equal(1, service.Logouts);
        Assert.False(model.오류발생);
    }

    private sealed class WebFixture : IDisposable
    {
        public Guid RequestId { get; } = Guid.NewGuid();
        public StorageJs Js { get; } = new();
        public NeverHttp Http { get; } = new();
        public WebAuthSessionService Session { get; }
        public BrowserFoodOrderPendingSubmissionStore PendingStore { get; }
        public WebOrdererAuthenticationService Authentication { get; }
        private readonly HttpClient httpClient;
        public 주문자음식주문Client FoodClient(SsalddelIsmsPClientEncryptionService encryption)
            => new(new SsalddelJsonApiClient(new SsalddelProtectedApiClient(httpClient, encryption, Session)));
        private WebFixture()
        {
            var snapshot = new ClientAuthTokenSnapshot("synthetic-access", DateTime.UtcNow.AddHours(1),
                "synthetic-refresh", DateTime.UtcNow.AddDays(1), "synthetic-owner", "합성 주문자", ["Orderer"]);
            Js.Set(AuthKey, JsonSerializer.Serialize(snapshot, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
            httpClient = new(Http) { BaseAddress = new("https://test.invalid/") };
            Session = new(httpClient, Js, new ClientSessionGuard());
            PendingStore = new(Js);
            Authentication = new(Session, PendingStore);
        }
        public static async Task<WebFixture> CreateAsync()
        {
            var fixture = new WebFixture();
            await fixture.PendingStore.SaveAsync(new("synthetic-owner", new()
            {
                클라이언트요청Id = fixture.RequestId, 음식점Id = 7,
                상품목록 = [new() { 메뉴Id = 8, 상품명 = "기존 선택 메뉴", 수량 = 2, 단가 = 3000 }]
            }, DateTime.UtcNow));
            var restored = await fixture.Authentication.복원Async();
            Assert.True(restored.성공);
            Assert.True(restored.세션.로그인됨);
            return fixture;
        }
        public void Dispose() => httpClient.Dispose();
    }

    private sealed class StorageJs : IJSRuntime
    {
        private readonly Dictionary<string, string> data = [];
        public string? BlockRemoveKey { get; set; }
        public TaskCompletionSource RemoveStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource RemoveCompletion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public string? Get(string key) => data.GetValueOrDefault(key);
        public void Set(string key, string value) => data[key] = value;
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            => InvokeAsync<TValue>(identifier, CancellationToken.None, args);
        public async ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var key = (string)args![0]!;
            if (identifier is "localStorage.getItem" or "sessionStorage.getItem")
                return (TValue)(object?)Get(key)!;
            if (identifier is "localStorage.setItem" or "sessionStorage.setItem")
                Set(key, (string)args[1]!);
            else if (identifier is "localStorage.removeItem" or "sessionStorage.removeItem")
            {
                if (BlockRemoveKey == key)
                {
                    RemoveStarted.TrySetResult();
                    await RemoveCompletion.Task;
                }
                data.Remove(key);
            }
            else throw new InvalidOperationException($"시험에서 허용하지 않은 JS 호출: {identifier}");
            return default!;
        }
    }
    private sealed class NeverHttp : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            throw new InvalidOperationException("세션 복원·만료·정리 시험에서 HTTP나 음식 주문을 제출하면 안 됩니다.");
        }
    }
    private sealed class DelayedAuthentication : I주문자앱인증Service
    {
        public bool DelayNext { get; set; }
        public string? StoredUserId { get; private set; } = "synthetic-owner";
        public int Logouts { get; private set; }
        public CancellationToken AuthenticationToken { get; private set; }
        public TaskCompletionSource AuthenticationStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<주문자앱인증결과> AuthenticationCompletion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<주문자앱인증결과> 복원Async(CancellationToken cancellationToken = default) => AuthenticateAsync(cancellationToken);
        public Task<주문자앱인증결과> 로그인Async(string userNameOrEmail, string password, CancellationToken cancellationToken = default)
            => AuthenticateAsync(cancellationToken);
        private async Task<주문자앱인증결과> AuthenticateAsync(CancellationToken cancellationToken)
        {
            if (!DelayNext) return new(new(true, StoredUserId, "합성 주문자"));
            AuthenticationToken = cancellationToken;
            AuthenticationStarted.TrySetResult();
            var response = await AuthenticationCompletion.Task; // 저장소/HTTP가 취소를 무시한 늦은 응답입니다.
            StoredUserId = response.세션.UserId;
            return response;
        }
        public Task 로그아웃Async(CancellationToken cancellationToken = default)
        { Logouts++; StoredUserId = null; return Task.CompletedTask; }
    }
}
