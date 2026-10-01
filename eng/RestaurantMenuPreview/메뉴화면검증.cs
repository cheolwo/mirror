using System.Reflection;
using System.Net;
using System.Net.Http.Json;
using Ssalddel.Contracts.Common;
using RestaurantDeskApp.Components.Pages;
using RestaurantDeskApp.Services;
using Ssalddel.Contracts.Food;

namespace RestaurantDeskApp;

// 실제 Razor component의 처리 메서드 검증. 브라우저 클릭/운영 DB 검증으로 보고하지 않는다.
public static class 메뉴화면검증
{
    public static async Task RunAsync()
    {
        var client = new FaultClient();
        var page = new Menus();
        typeof(Menus).GetProperty("MenuClient", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(page, client);
        void Set(string key, object value) => typeof(Menus).GetField(key, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(page, value);
        object? Get(string key) => typeof(Menus).GetField(key, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(page);
        async Task Call(string key) => await (Task)typeof(Menus).GetMethod(key, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(page, null)!;
        void Assert(bool value, string reason) { if (!value) throw new InvalidOperationException(reason); }
        await Call("ReloadAsync");
        Assert((bool)Get("loaded")!, "initial load");
        typeof(Menus).GetMethod("NewMenu", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(page, null);
        Assert((bool)Get("editing")!, "new menu opens editor");
        Set("name", ""); await Call("SaveAsync"); Assert(client.Writes == 0, "blank menu must not write");
        Set("name", "검증 전용 메뉴"); Set("price", 7500m); Set("imageUrl", "http://invalid.test/image");
        await Call("SaveAsync"); Assert(client.Writes == 0, "non HTTPS must not write");
        Set("imageUrl", ""); client.LoseAck = true;
        await Call("SaveAsync"); var pending = (음식점메뉴등록요청)Get("pendingCreate")!;
        Assert(pending is not null && client.Items.Count == 3, "ack lost keeps request");
        typeof(Menus).GetMethod("CloseEditor", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(page, null);
        Assert((bool)Get("editing")!, "uncertain create cannot discard pending request");
        await Call("SaveAsync"); Assert(client.Items.Count == 3 && Get("pendingCreate") is null && client.Ids.Distinct().Count() == 1, "same request retry");
        Assert(!(bool)Get("editing")!, "confirmed save returns to list");
        Set("name", "거절 검증"); client.Reject = true;
        await Call("SaveAsync"); Assert(Get("pendingCreate") is null && client.Items.Count == 3, "definite rejection unlocks editing");
        client.Reject = false; Set("name", "목록 갱신 실패 검증"); client.FailReadAfterSave = true;
        await Call("SaveAsync"); Assert(client.Items.Count == 4 && Get("pendingCreate") is null && ((string)Get("message")!).Contains("저장은 완료"), "save success separate from read failure");
        Console.WriteLine("PASS: 10 menu component behavior checks (in-memory client, no DB or UI clicks)");
    }

    public static async Task Api안전검증Async()
    {
        var checks = 0;
        void Assert(bool value, string reason)
        { if (!value) throw new InvalidOperationException(reason); checks++; }
        async Task Fails<T>(Func<Task> action, string reason) where T : Exception
        {
            try { await action(); }
            catch (T) { checks++; return; }
            throw new InvalidOperationException(reason);
        }
        Assert(!메뉴미리보기Options.FromArguments([]).Api연결, "default must stay synthetic");
        foreach (var value in new string?[]
        {
            null, "", "https://example.invalid/", "http://example.invalid:5321/",
            "http://127.0.0.1:5321/api/", "http://127.0.0.1:5321/?secret=x",
            "http://127.0.0.1:5321/#x", "http://user:secret@127.0.0.1:5321/",
            "http://127.0.0.1:5387/", "http://127.0.0.1:80/", "http://127.0.0.2:5321/"
        })
            await Fails<InvalidOperationException>(() => Task.FromResult(메뉴미리보기Options.ParseEndpoint(value)), "unsafe endpoint accepted");
        foreach (var value in new[] { "http://127.0.0.1:5321/", "http://localhost:5321/", "http://[::1]:5321/" })
            Assert(메뉴미리보기Options.ParseEndpoint(value).IsLoopback, "loopback endpoint rejected");

        var priorPassword = Environment.GetEnvironmentVariable("FOOD_OBSERVER_ACCOUNT_PASSWORD");
        var priorEndpoint = Environment.GetEnvironmentVariable("FOOD_OBSERVER_BASE_URL");
        try
        {
            Environment.SetEnvironmentVariable("FOOD_OBSERVER_BASE_URL", "http://127.0.0.1:5321/");
            Environment.SetEnvironmentVariable("FOOD_OBSERVER_ACCOUNT_PASSWORD", null);
            await Fails<InvalidOperationException>(() => Task.FromResult(메뉴미리보기Options.FromArguments(["--api"])), "missing credential accepted");
            // 합성 시험 비밀번호. 실제 계정/HTTP에 사용하지 않고 이 프로세스에서만 임시 설정한다.
            Environment.SetEnvironmentVariable("FOOD_OBSERVER_ACCOUNT_PASSWORD", "synthetic-transport-test-only");
            var options = 메뉴미리보기Options.FromArguments(["--api"]);
            Assert(options.Api연결, "explicit API selection ignored");

            var handler = new ApiGuardHandler();
            await using var first = new 메뉴Api연결(options, handler);
            var list = await first.목록Async();
            Assert(list.Count == 1 && list[0].Id == 41 && handler.LoginCount == 1 && handler.MenuCount == 1 && handler.HealthCount == 1,
                "linked auth/client GET not used");
            Assert(first.HasSession, "memory session missing");
            using var otherHandler = new ApiGuardHandler();
            await using var second = new 메뉴Api연결(options, otherHandler);
            Assert(!second.HasSession, "circuits share a session");
            await first.ClearAsync();
            Assert(!first.HasSession, "disconnect did not clear session");
            await first.목록Async();
            Assert(handler.LoginCount == 2 && handler.HealthCount == 2, "reconnect failed to authenticate afresh");
            await first.DisposeAsync();
            Assert(!first.HasSession, "dispose retained session");
            await Fails<ObjectDisposedException>(() => first.목록Async(), "disposed client accepted work");

            var unavailable = new ApiGuardHandler { HealthStatus = HttpStatusCode.ServiceUnavailable };
            await using var blocked = new 메뉴Api연결(options, unavailable);
            await Fails<InvalidOperationException>(() => blocked.목록Async(), "failed health silently fell back");
            Assert(unavailable.LoginCount == 0 && unavailable.MenuCount == 0 && !blocked.HasSession, "failed health contacted auth/menu");
            var unauthorized = new ApiGuardHandler { LoginStatus = HttpStatusCode.Unauthorized };
            await using var refused = new 메뉴Api연결(options, unauthorized);
            await Fails<UnauthorizedAccessException>(() => refused.목록Async(), "failed auth silently fell back");
            Assert(unauthorized.MenuCount == 0 && !refused.HasSession, "failed auth reached menu");

            var conflictHandler = new ApiGuardHandler { SaveStatus = HttpStatusCode.Conflict };
            await using var conflict = new 메뉴Api연결(options, conflictHandler);
            await Fails<메뉴저장거절Exception>(() => conflict.등록Async(new 음식점메뉴등록요청
            {
                클라이언트요청Id = Guid.NewGuid(), 메뉴명 = "합성 충돌", 판매가 = 7000
            }), "business rejection not preserved");
            Assert(conflictHandler.LastMethod == HttpMethod.Post, "POST not forwarded");
            await Fails<메뉴저장거절Exception>(() => conflict.수정Async(41, new 음식점메뉴수정요청
            { 예상Revision = 1, 메뉴명 = "합성 충돌", 판매가 = 7000 }), "PUT rejection not preserved");
            Assert(conflictHandler.LastMethod == HttpMethod.Put, "PUT not forwarded");

            var slowHandler = new ApiGuardHandler { HoldMenu = true };
            await using var slow = new 메뉴Api연결(options, slowHandler);
            var pending = slow.목록Async();
            await slowHandler.MenuEntered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            await slow.ClearAsync();
            await Fails<OperationCanceledException>(() => pending, "disconnect left request running");
            Assert(!slow.HasSession, "disconnect after pending request retained session");
        }
        finally
        {
            Environment.SetEnvironmentVariable("FOOD_OBSERVER_ACCOUNT_PASSWORD", priorPassword);
            Environment.SetEnvironmentVariable("FOOD_OBSERVER_BASE_URL", priorEndpoint);
        }
        Console.WriteLine($"PASS: {checks} API preview safety checks (mock HTTP only, no server/DB/browser)");
    }

    private sealed class ApiGuardHandler : HttpMessageHandler
    {
        public HttpStatusCode HealthStatus = HttpStatusCode.NoContent, LoginStatus = HttpStatusCode.OK, SaveStatus = HttpStatusCode.OK;
        public int HealthCount, LoginCount, MenuCount;
        public HttpMethod? LastMethod;
        public bool HoldMenu;
        public TaskCompletionSource MenuEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri?.AbsolutePath == "/verification/health")
            { HealthCount++; return new(HealthStatus); }
            if (request.RequestUri?.AbsolutePath == "/api/v1/auth/login")
            {
                LoginCount++;
                var login = await request.Content!.ReadFromJsonAsync<로그인요청>(cancellationToken);
                if (login?.UserNameOrEmail != "food-observer-restaurant" || login.Password != "synthetic-transport-test-only")
                    throw new InvalidOperationException("Only the fixed synthetic account is allowed");
                return new(LoginStatus) { Content = JsonContent.Create(new 토큰응답
                {
                    AccessToken = "synthetic-access", RefreshToken = "synthetic-refresh",
                    AccessTokenExpiresAtUtc = DateTime.UtcNow.AddHours(1), RefreshTokenExpiresAtUtc = DateTime.UtcNow.AddHours(2),
                    UserId = "synthetic-user", UserName = "synthetic-user", Roles = ["음식점"]
                }) };
            }
            if (request.RequestUri?.AbsolutePath.StartsWith("/api/v1/restaurant/menus", StringComparison.Ordinal) == true)
            {
                if (request.Headers.Authorization?.Scheme != "Bearer" || request.Headers.Authorization.Parameter != "synthetic-access")
                    throw new InvalidOperationException("Product client authorization missing");
                MenuCount++; LastMethod = request.Method;
                MenuEntered.TrySetResult();
                if (HoldMenu) await Task.Delay(Timeout.Infinite, cancellationToken);
                if (request.Method != HttpMethod.Get) return new(SaveStatus) { Content = JsonContent.Create(new 음식점메뉴관리응답 { Id = 41, 메뉴명 = "합성 메뉴" }) };
                return new(HttpStatusCode.OK) { Content = JsonContent.Create(new[] { new 음식점메뉴관리응답 { Id = 41, 메뉴명 = "합성 메뉴" } }) };
            }
            throw new InvalidOperationException("Unexpected endpoint in API preview test");
        }
    }

    private sealed class FaultClient : I음식점메뉴ApiClient
    {
        private readonly 메뉴미리보기Client inner = new();
        public IReadOnlyList<음식점메뉴관리응답> Items => inner.목록Async().Result;
        public List<Guid> Ids { get; } = [];
        public int Writes;
        public bool LoseAck, Reject, FailReadAfterSave;
        private bool failRead;
        public Task<IReadOnlyList<음식점메뉴관리응답>> 목록Async(CancellationToken cancellationToken = default)
            => failRead ? throw new HttpRequestException("synthetic read failure") : inner.목록Async(cancellationToken);
        public async Task<음식점메뉴관리응답> 등록Async(음식점메뉴등록요청 request, CancellationToken cancellationToken = default)
        {
            Writes++; Ids.Add(request.클라이언트요청Id);
            if (Reject) throw new 메뉴저장거절Exception();
            var result = await inner.등록Async(request, cancellationToken);
            if (LoseAck) { LoseAck = false; throw new HttpRequestException("synthetic ack loss"); }
            if (FailReadAfterSave) failRead = true;
            return result;
        }
        public Task<음식점메뉴관리응답> 수정Async(long id, 음식점메뉴수정요청 request, CancellationToken cancellationToken = default)
            => inner.수정Async(id, request, cancellationToken);
    }
}
