using System.Net;
using System.Reflection;
using DriverApp.Services;
using Ssalddel.Client.Infrastructure.Security;
using Ssalddel.Contracts.Driver.Transport;

namespace Ssalddel.Tests.Clients;

public sealed class CargoTransportDetailRecoveryTests
{
    [Theory]
    [InlineData("확정", "상차지 도착")]
    [InlineData("배차확정", "상차지 도착")]
    [InlineData("상차지도착", "상차 완료")]
    [InlineData("상차완료", "하차지 도착")]
    [InlineData("운송중", "하차지 도착")]
    [InlineData("하차지도착", "하차 완료")]
    public async Task 서버의_확정과_상하차단계는_상태를보존하며_같은운송의_다음행동으로연결한다(
        string status, string expectedNextAction)
    {
        var fixture = new Fixture();
        fixture.Api.Detail = (id, _) => Task.FromResult<기사운송상세응답?>(new()
        {
            Id = id, 운송번호 = "synthetic-confirmed-request", 상태 = status,
            출발지 = "합성 상차지", 도착지 = "합성 하차지"
        });

        var result = await fixture.Service.조회Async(42, default);

        Assert.Equal(42, fixture.Api.RequestedId);
        Assert.Equal(42, result!.Id);
        Assert.Equal("synthetic-confirmed-request", result.의뢰Id);
        Assert.Equal(status, result.현재단계);
        Assert.Equal(expectedNextAction, result.다음행동);
    }

    [Fact]
    public async Task 캐시없이_선택운송의_서버상태와_필요증빙을조회한다()
    {
        var fixture = new Fixture();
        fixture.Api.Detail = (id, _) => Task.FromResult<기사운송상세응답?>(new()
        {
            Id = id, 운송번호 = "synthetic-cargo-42", 출발지 = "합성 상차지", 도착지 = "합성 하차지",
            상태 = "상차지도착", 인수증필요 = true, 인수증서명필수 = true,
            수령자명 = "합성 수령자", 수령자연락처 = "test-only-dropoff", 전달요청 = "현장 확인", 운임 = 6500m,
            상차담당자명 = "합성 상차 담당자", 상차연락처 = "test-only-pickup",
            상차시간창시작일시 = new DateTime(2026, 10, 3, 0, 30, 0, DateTimeKind.Utc),
            상차시간창종료일시 = new DateTime(2026, 10, 3, 1, 30, 0, DateTimeKind.Utc),
            하차시간창시작일시 = new DateTime(2026, 10, 3, 3, 0, 0, DateTimeKind.Utc),
            하차시간창종료일시 = new DateTime(2026, 10, 3, 4, 0, 0, DateTimeKind.Utc)
        });
        var result = await fixture.Service.조회Async(42, default);
        Assert.Equal(42, fixture.Api.RequestedId);
        Assert.Equal("상차 완료", result!.다음행동);
        Assert.True(result.인수증필요);
        Assert.True(result.인수증서명필수);
        Assert.Equal("합성 수령자", result.수령자명);
        Assert.Equal("test-only-dropoff", result.수령자연락처);
        Assert.Equal("합성 상차 담당자", result.상차담당자명);
        Assert.Equal("test-only-pickup", result.상차연락처);
        Assert.Equal("10.03 09:30 ~ 10.03 10:30 (한국시간)", result.상차시간창표시);
        Assert.Equal("10.03 12:00 ~ 10.03 13:00 (한국시간)", result.하차시간창표시);
        Assert.Equal(6500m, result.예상수익);
    }

    [Fact]
    public async Task 없는시간창은_예정시각으로만들지않는다()
    {
        var fixture = new Fixture();
        var result = await fixture.Service.조회Async(42, default);
        Assert.Equal("시간 미지정", result!.상차시간창표시);
        Assert.Equal("시간 미지정", result.하차시간창표시);
        Assert.Empty(result.상차연락처);
    }

    [Fact]
    public async Task 없는운송을다른캐시나샘플로대체하지않는다()
    {
        var fixture = new Fixture();
        fixture.Api.Detail = (_, _) => Task.FromResult<기사운송상세응답?>(null);
        Assert.Null(await fixture.Service.조회Async(42, default));
    }

    [Fact]
    public async Task 요청과다른ID를표시하지않는다()
    {
        var fixture = new Fixture();
        fixture.Api.Detail = (_, _) => Task.FromResult<기사운송상세응답?>(new() { Id = 99 });
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.조회Async(42, default));
    }

    [Fact]
    public async Task 서버장애를정상빈운송으로숨기지않는다()
    {
        var fixture = new Fixture();
        fixture.Api.Detail = (_, _) => throw new HttpRequestException("temporary", null, HttpStatusCode.ServiceUnavailable);
        var error = await Assert.ThrowsAsync<HttpRequestException>(() => fixture.Service.조회Async(42, default));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, error.StatusCode);
        Assert.True(fixture.Auth.IsAuthenticated);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task 로그아웃또는같은사용자재로그인뒤응답을버린다(bool relogin)
    {
        var fixture = new Fixture();
        var pending = new TaskCompletionSource<기사운송상세응답?>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Api.Detail = (_, _) => pending.Task;
        var load = fixture.Service.조회Async(42, default);
        await fixture.Auth.ClearAsync();
        if (relogin) await fixture.Auth.ApplyAsync(Snapshot("new-session"));
        pending.SetResult(new() { Id = 42 });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => load);
    }

    [Fact]
    public async Task 같은인증세션의정상토큰갱신은응답을허용한다()
    {
        var fixture = new Fixture();
        fixture.Api.Detail = async (id, _) =>
        {
            await fixture.Auth.TryApplyAsync(Snapshot("refreshed"), fixture.Auth.Version);
            return new() { Id = id };
        };
        Assert.Equal(42, (await fixture.Service.조회Async(42, default))!.Id);
    }

    [Fact]
    public async Task 호출자가취소하면API가취소를무시해도표시하지않는다()
    {
        var fixture = new Fixture();
        using var cancellation = new CancellationTokenSource();
        fixture.Api.Detail = (id, _) =>
        {
            cancellation.Cancel();
            return Task.FromResult<기사운송상세응답?>(new() { Id = id });
        };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Service.조회Async(42, cancellation.Token));
    }

    [Fact]
    public async Task 인증없으면보호상세API를호출하지않는다()
    {
        var fixture = new Fixture();
        await fixture.Auth.ClearAsync();
        var error = await Assert.ThrowsAsync<HttpRequestException>(() => fixture.Service.조회Async(42, default));
        Assert.Equal(HttpStatusCode.Unauthorized, error.StatusCode);
        Assert.Null(fixture.Api.RequestedId);
    }

    private static ClientAuthTokenSnapshot Snapshot(string accessToken)
        => new(accessToken, DateTime.UtcNow.AddHours(1), "test-only-refresh", DateTime.UtcNow.AddDays(1),
            "synthetic-cargo-driver", "합성 기사", []);

    private sealed class Fixture
    {
        public TestAuth Auth { get; } = new();
        public TransportApiStub Api { get; }
        public 기사운송상세조회Service Service { get; }
        public Fixture()
        {
            var proxy = DispatchProxy.Create<IDriverTransportApiService, TransportApiStub>();
            Api = (TransportApiStub)(object)proxy;
            Service = new(proxy, Auth);
        }
    }

    public class TransportApiStub : DispatchProxy
    {
        public long? RequestedId { get; private set; }
        public Func<long, CancellationToken, Task<기사운송상세응답?>> Detail { get; set; }
            = (id, _) => Task.FromResult<기사운송상세응답?>(new() { Id = id });
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name != nameof(IDriverTransportApiService.상세조회Async)) throw new NotSupportedException();
            RequestedId = (long)args![0]!;
            return Detail(RequestedId.Value, (CancellationToken)args[1]!);
        }
    }

    private sealed class TestAuth : IAuthSession
    {
        public string? AccessToken { get; private set; } = "test-only";
        public string? RefreshToken => "test-only-refresh";
        public DateTime AccessTokenExpiresAtUtc => DateTime.UtcNow.AddHours(1);
        public DateTime RefreshTokenExpiresAtUtc => DateTime.UtcNow.AddDays(1);
        public string? UserId { get; private set; } = "synthetic-cargo-driver";
        public string? UserName => UserId;
        public IReadOnlyList<string> Roles => [];
        public bool IsAuthenticated => UserId is not null;
        public long Version { get; private set; }
        public long SessionRevision { get; private set; }
        public event Action? Changed;
        public Task RestoreAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ApplyAsync(ClientAuthTokenSnapshot snapshot, CancellationToken cancellationToken = default)
        {
            UserId = snapshot.UserId; AccessToken = snapshot.AccessToken; Version++; SessionRevision++; Changed?.Invoke(); return Task.CompletedTask;
        }
        public Task ClearAsync(CancellationToken cancellationToken = default)
        {
            UserId = null; AccessToken = null; Version++; SessionRevision++; Changed?.Invoke(); return Task.CompletedTask;
        }
        public async Task<bool> TryClearAsync(long expectedVersion, CancellationToken cancellationToken = default)
        { if (Version != expectedVersion) return false; await ClearAsync(cancellationToken); return true; }
        public async Task<bool> TryApplyAsync(ClientAuthTokenSnapshot snapshot, long expectedVersion, CancellationToken cancellationToken = default, bool startsNewSession = false)
        {
            if (Version != expectedVersion) return false;
            if (startsNewSession || UserId != snapshot.UserId) SessionRevision++;
            UserId = snapshot.UserId; AccessToken = snapshot.AccessToken; Version++; Changed?.Invoke();
            await Task.CompletedTask;
            return true;
        }
    }
}
