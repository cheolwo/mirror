using System.Net;
using Microsoft.AspNetCore.Components.Server.Circuits;
using RestaurantDeskApp.Services;
using Ssalddel.Client.Infrastructure.Security;
using Ssalddel.Contracts.Food;

namespace RestaurantDeskApp;

// 제품 실행 모드를 추가하는 것이 아니라 로컬 검증 도구의 자료 연결을 선택한다.
public sealed record 메뉴미리보기Options(bool Api연결, Uri? Endpoint)
{
    public static 메뉴미리보기Options FromArguments(string[] args)
    {
        if (!args.Contains("--api", StringComparer.Ordinal)) return new(false, null);
        var endpoint = ParseEndpoint(Environment.GetEnvironmentVariable("FOOD_OBSERVER_BASE_URL"));
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("FOOD_OBSERVER_ACCOUNT_PASSWORD")))
            throw new InvalidOperationException("격리 API 연결에는 FOOD_OBSERVER_ACCOUNT_PASSWORD 환경 변수가 필요합니다. 값은 출력하지 않습니다.");
        return new(true, endpoint);
    }

    public static Uri ParseEndpoint(string? value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttp
            || !uri.IsLoopback
            || uri.Host is not ("127.0.0.1" or "localhost" or "[::1]")
            || uri.Port is < 1024 or > 49151 or 5387
            || uri.AbsolutePath != "/" || uri.Query.Length != 0 || uri.Fragment.Length != 0
            || uri.UserInfo.Length != 0)
            throw new InvalidOperationException("FOOD_OBSERVER_BASE_URL은 전용 food-observer의 loopback HTTP 원점이어야 합니다. 경로·자격 증명·query·fragment는 허용하지 않습니다.");
        return uri;
    }
}

// Blazor Circuit scope마다 별도 세션을 소유한다. 실제 앱 Client를 그대로 사용하며 합성 대체를 하지 않는다.
public sealed class 메뉴Api연결 : I음식점메뉴ApiClient, IAsyncDisposable
{
    private readonly HttpClient _http;
    private readonly ClientAuthSession _session;
    private readonly RestaurantAuthService _auth;
    private readonly Ssalddel음식주문Client _client;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _stateGate = new();
    private CancellationTokenSource _connection = new();
    private readonly CancellationTokenSource _lifetime = new();
    private int _disposed;
    private bool _healthChecked;

    public RestaurantAuthService AuthService => _auth;

    public 메뉴Api연결(메뉴미리보기Options options) : this(options, new HttpClientHandler
    {
        AllowAutoRedirect = false,
        UseCookies = false,
        UseProxy = false
    }) { }

    // 시험에서만 모의 전송을 주입한다. 운영 연결의 외부 리다이렉트·프록시·쿠키는 사용하지 않는다.
    internal 메뉴Api연결(메뉴미리보기Options options, HttpMessageHandler handler)
    {
        if (!options.Api연결) throw new InvalidOperationException("명시적인 API 연결 선택이 필요합니다.");
        var endpoint = 메뉴미리보기Options.ParseEndpoint(options.Endpoint?.AbsoluteUri);
        _http = new HttpClient(handler) { BaseAddress = endpoint, Timeout = TimeSpan.FromSeconds(20) };
        _session = new ClientAuthSession(new 메뉴검증TokenStore(), new ClientSessionGuard());
        _auth = new RestaurantAuthService(_http, _session);
        _client = new Ssalddel음식주문Client(_http, _auth, _session);
    }

    internal bool HasSession => _session.AccessToken is not null || _session.RefreshToken is not null;

    public Task<IReadOnlyList<음식점메뉴관리응답>> 목록Async(CancellationToken cancellationToken = default)
        => ExecuteAsync(ct => _client.목록Async(ct), cancellationToken);

    public Task<음식점메뉴관리응답> 등록Async(음식점메뉴등록요청 request, CancellationToken cancellationToken = default)
        => ExecuteAsync(ct => _client.등록Async(request, ct), cancellationToken);

    public Task<음식점메뉴관리응답> 수정Async(long menuId, 음식점메뉴수정요청 request, CancellationToken cancellationToken = default)
        => ExecuteAsync(ct => _client.수정Async(menuId, request, ct), cancellationToken);

    private async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _connection.Token, _lifetime.Token);
            var token = linked.Token;
            if (!_healthChecked)
            {
                using var health = await _http.GetAsync("verification/health", token);
                if (health.StatusCode != HttpStatusCode.NoContent)
                    throw new InvalidOperationException("전용 food-observer의 준비 상태를 확인하지 못했습니다. 합성 자료로 대체하지 않습니다.");
                _healthChecked = true;
            }
            if (!_session.IsAuthenticated)
            {
                var password = Environment.GetEnvironmentVariable("FOOD_OBSERVER_ACCOUNT_PASSWORD");
                if (string.IsNullOrWhiteSpace(password))
                    throw new UnauthorizedAccessException("격리 음식점 검증 계정 환경 설정이 필요합니다.");
                var result = await _auth.LoginAsync("food-observer-restaurant", password, token);
                if (!result.IsSuccess)
                    throw new UnauthorizedAccessException("격리 음식점 검증 계정 인증을 확인하지 못했습니다.");
            }
            return await action(token);
        }
        finally { _gate.Release(); }
    }

    public async Task ClearAsync()
    {
        lock (_stateGate)
        {
            if (Volatile.Read(ref _disposed) != 0) return;
            _connection.Cancel();
        }
        await _gate.WaitAsync();
        try
        {
            await _session.ClearAsync();
            _healthChecked = false;
            lock (_stateGate)
            {
                if (Volatile.Read(ref _disposed) != 0) return;
                _connection.Dispose();
                _connection = new CancellationTokenSource();
            }
        }
        finally { _gate.Release(); }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _lifetime.Cancel();
        await _gate.WaitAsync();
        try
        {
            await _session.ClearAsync();
            _http.Dispose();
            lock (_stateGate) { _connection.Dispose(); }
            _lifetime.Dispose();
        }
        finally { _gate.Release(); }
    }

    private sealed class 메뉴검증TokenStore : IClientSecureTokenStore
    {
        private ClientAuthTokenSnapshot? _value;
        public Task<ClientAuthTokenSnapshot?> LoadAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_value);
        public Task SaveAsync(ClientAuthTokenSnapshot snapshot, CancellationToken cancellationToken = default)
        { _value = snapshot; return Task.CompletedTask; }
        public Task ClearAsync(CancellationToken cancellationToken = default)
        { _value = null; return Task.CompletedTask; }
    }
}

public sealed class 메뉴ApiCircuitHandler(메뉴Api연결 connection) : CircuitHandler
{
    public override Task OnConnectionDownAsync(Circuit circuit, CancellationToken cancellationToken)
        => connection.ClearAsync();
    public override Task OnCircuitClosedAsync(Circuit circuit, CancellationToken cancellationToken)
        => connection.ClearAsync();
}
