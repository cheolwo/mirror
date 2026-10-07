using System.Net;
using System.Reflection;
using DriverApp.Services;
using DriverApp.ViewModels.Driver.Transport;
using Ssalddel.Client.Infrastructure.Security;
using Ssalddel.Contracts.Driver.Transport;

namespace Ssalddel.Tests.Clients;

/// <summary>실제 내역 VM과 인증 세션의 회귀시험. HTTP·앱 화면·기기 실행 증거는 아닙니다.</summary>
public sealed class CargoTransportHistoryLifetimeTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task 미로그인은_서버내역을조회하지않고로그인상태를표시한다()
    {
        using var fixture = await Fixture.CreateAsync(authenticated: false);

        await fixture.Model.InitializeAsync();

        Assert.True(fixture.Model.로그인필요);
        Assert.False(fixture.Model.불러오는중);
        Assert.Empty(fixture.Model.운송목록);
        Assert.Empty(fixture.Api.Tokens);
    }

    [Fact]
    public async Task 겹친조회는_이전취소를무시한응답이늦어도최신내역을보존한다()
    {
        using var fixture = await Fixture.CreateAsync();
        var firstResponse = Pending();
        var secondResponse = Pending();
        fixture.Api.Query = token => fixture.Api.Tokens.Count == 1 ? firstResponse.Task : secondResponse.Task;
        var first = fixture.Model.RefreshAsync();
        var second = fixture.Model.RefreshAsync();
        Assert.True(fixture.Api.Tokens[0].IsCancellationRequested);
        Assert.True(fixture.Model.불러오는중);

        secondResponse.SetResult([Row(22)]);
        await second.WaitAsync(Timeout);
        Assert.Equal(22, Assert.Single(fixture.Model.운송목록).Id);
        firstResponse.SetResult([Row(11)]);
        await first.WaitAsync(Timeout);

        Assert.Equal(22, Assert.Single(fixture.Model.운송목록).Id);
        Assert.False(fixture.Model.불러오는중);
        Assert.Null(fixture.Model.오류메시지);
    }

    [Fact]
    public async Task 이전조회실패는_새조회로딩이나오류표시를덮어쓰지않는다()
    {
        using var fixture = await Fixture.CreateAsync();
        var firstResponse = Pending();
        var secondResponse = Pending();
        fixture.Api.Query = token => fixture.Api.Tokens.Count == 1 ? firstResponse.Task : secondResponse.Task;
        var first = fixture.Model.RefreshAsync();
        var second = fixture.Model.RefreshAsync();

        firstResponse.SetException(new UnauthorizedAccessException("old session"));
        await first.WaitAsync(Timeout);
        Assert.True(fixture.Model.불러오는중);
        Assert.False(fixture.Model.로그인필요);
        Assert.Null(fixture.Model.오류메시지);
        secondResponse.SetResult([Row(22)]);
        await second.WaitAsync(Timeout);
        Assert.Equal(22, Assert.Single(fixture.Model.운송목록).Id);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task 로그아웃또는계정변경은_이전목록을즉시지우고뒤늦은개인내역을버린다(bool logout)
    {
        using var fixture = await Fixture.CreateAsync();
        await fixture.Model.InitializeAsync();
        Assert.Equal(11, Assert.Single(fixture.Model.운송목록).Id);
        var pending = Pending();
        fixture.Api.Query = _ => pending.Task;
        var loading = fixture.Model.RefreshAsync();

        if (logout) await fixture.Auth.ClearAsync();
        else await fixture.Auth.ApplyAsync(Snapshot("synthetic-driver-two"));

        Assert.True(fixture.Api.Tokens.Last().IsCancellationRequested);
        Assert.Empty(fixture.Model.운송목록);
        Assert.Equal(logout, fixture.Model.로그인필요);
        Assert.False(fixture.Model.불러오는중);
        pending.SetResult([Row(11)]);
        await loading.WaitAsync(Timeout);
        Assert.Empty(fixture.Model.운송목록);

        if (!logout)
        {
            fixture.Api.Query = _ => Task.FromResult<IReadOnlyList<기사운송요약응답>>([Row(22)]);
            await fixture.Model.RefreshAsync();
            Assert.Equal(22, Assert.Single(fixture.Model.운송목록).Id);
            Assert.Null(fixture.Model.오류메시지);
        }
    }

    [Fact]
    public async Task 같은계정토큰갱신은_조회된내역을지우거나중복조회하지않는다()
    {
        using var fixture = await Fixture.CreateAsync();
        await fixture.Model.InitializeAsync();
        var session = fixture.Auth.SessionRevision;

        Assert.True(await fixture.Auth.TryApplyAsync(Snapshot(), fixture.Auth.Version));

        Assert.Equal(session, fixture.Auth.SessionRevision);
        Assert.Equal(11, Assert.Single(fixture.Model.운송목록).Id);
        Assert.Single(fixture.Api.Tokens);
        Assert.Null(fixture.Model.오류메시지);
    }

    [Fact]
    public async Task 페이지이탈은_조회와구독을종료하고늦은응답이화면을다시갱신하지않는다()
    {
        using var fixture = await Fixture.CreateAsync();
        var pending = Pending();
        fixture.Api.Query = _ => pending.Task;
        var notifications = 0;
        fixture.Model.PropertyChanged += (_, _) => notifications++;
        var loading = fixture.Model.RefreshAsync();
        fixture.Model.Dispose();
        var afterDispose = notifications;
        Assert.True(fixture.Api.Tokens[0].IsCancellationRequested);

        await fixture.Auth.ApplyAsync(Snapshot("synthetic-driver-two"));
        pending.SetResult([Row(11)]);
        await loading.WaitAsync(Timeout);
        await fixture.Model.RefreshAsync();

        Assert.Empty(fixture.Model.운송목록);
        Assert.False(fixture.Model.불러오는중);
        Assert.Equal(afterDispose, notifications);
        Assert.Single(fixture.Api.Tokens);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task 인증오류와일시연결실패를구분하고_현재계정의재조회로회복한다(bool unauthorized)
    {
        using var fixture = await Fixture.CreateAsync();
        fixture.Api.Query = _ => Task.FromException<IReadOnlyList<기사운송요약응답>>(
            unauthorized ? new UnauthorizedAccessException("synthetic 401")
                : new HttpRequestException("synthetic 503", null, HttpStatusCode.ServiceUnavailable));

        await fixture.Model.RefreshAsync();

        Assert.True(fixture.Auth.IsAuthenticated);
        Assert.Equal(unauthorized, fixture.Model.로그인필요);
        Assert.False(fixture.Model.불러오는중);
        Assert.Empty(fixture.Model.운송목록);
        if (unauthorized) Assert.Null(fixture.Model.오류메시지);
        else Assert.Contains("다시 시도", fixture.Model.오류메시지);
        fixture.Api.Query = _ => Task.FromResult<IReadOnlyList<기사운송요약응답>>([Row(22)]);
        await fixture.Model.RefreshAsync();
        Assert.Equal(22, Assert.Single(fixture.Model.운송목록).Id);
        Assert.False(fixture.Model.로그인필요);
        Assert.Null(fixture.Model.오류메시지);
    }

    private static TaskCompletionSource<IReadOnlyList<기사운송요약응답>> Pending()
        => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static 기사운송요약응답 Row(long id) => new()
    {
        Id = id, 운송번호 = $"synthetic-cargo-{id}", 상태 = "인수완료", 출발지 = "합성 상차지", 도착지 = "합성 하차지"
    };
    private static ClientAuthTokenSnapshot Snapshot(string userId = "synthetic-driver-one")
        => new("test-only-access", DateTime.UtcNow.AddHours(1), "test-only-refresh",
            DateTime.UtcNow.AddDays(1), userId, "합성 기사", ["Driver"]);

    private sealed class Fixture(AuthSession auth, TransportApiStub api, 기사운송내역PageViewModel model) : IDisposable
    {
        public AuthSession Auth { get; } = auth;
        public TransportApiStub Api { get; } = api;
        public 기사운송내역PageViewModel Model { get; } = model;
        public static async Task<Fixture> CreateAsync(bool authenticated = true)
        {
            var auth = new AuthSession(new TokenStore(authenticated ? Snapshot() : null), new ClientSessionGuard());
            await auth.RestoreAsync();
            var service = DispatchProxy.Create<IDriverTransportApiService, TransportApiStub>();
            return new(auth, (TransportApiStub)(object)service, new(service, auth));
        }
        public void Dispose() => Model.Dispose();
    }

    public class TransportApiStub : DispatchProxy
    {
        public List<CancellationToken> Tokens { get; } = [];
        public Func<CancellationToken, Task<IReadOnlyList<기사운송요약응답>>> Query { get; set; }
            = _ => Task.FromResult<IReadOnlyList<기사운송요약응답>>([Row(11)]);
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name != nameof(IDriverTransportApiService.목록조회Async))
                throw new InvalidOperationException("내역 화면에서 운송 명령을 호출하면 안 됩니다.");
            var token = (CancellationToken)args![0]!;
            Tokens.Add(token);
            return Query(token);
        }
    }

    private sealed class TokenStore(ClientAuthTokenSnapshot? value) : IClientSecureTokenStore
    {
        public Task<ClientAuthTokenSnapshot?> LoadAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(value);
        public Task SaveAsync(ClientAuthTokenSnapshot snapshot, CancellationToken cancellationToken = default)
        { value = snapshot; return Task.CompletedTask; }
        public Task ClearAsync(CancellationToken cancellationToken = default)
        { value = null; return Task.CompletedTask; }
    }
}
