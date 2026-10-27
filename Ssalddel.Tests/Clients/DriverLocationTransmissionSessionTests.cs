using DriverApp.Services;
using Ssalddel.Client.Infrastructure.Security;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.Transport;
using Ssalddel.Contracts.Driver.Work;

namespace Ssalddel.Tests.Clients;

public sealed class DriverLocationTransmissionSessionTests
{
    [Fact]
    public async Task 명시적_시작과_서비스활성화_전에는_측정하거나_전송하지_않는다()
    {
        using var f = new Fixture(); var reads = 0;
        Assert.False(f.Session.TryActivate(Guid.Empty));
        Assert.False(await f.Session.SampleAndSendAsync(Guid.Empty, _ => { reads++; return Task.FromResult<DriverGpsObservation?>(f.Fix()); }));
        var id = await f.Prepare();
        Assert.False(f.Session.IsRunning);
        Assert.False(await f.Session.SampleAndSendAsync(id, _ => { reads++; return Task.FromResult<DriverGpsObservation?>(f.Fix()); }));
        Assert.True(f.Session.TryActivate(id)); Assert.False(f.Session.TryActivate(id));
        await f.Session.WaitActivatedAsync(id);
        Assert.True(f.Session.IsRunning); Assert.Equal(0, reads); Assert.Empty(f.Api.Posts);
    }

    [Theory]
    [InlineData("anonymous")]
    [InlineData("owner")]
    [InlineData("stopped")]
    [InlineData("shift")]
    [InlineData("execution")]
    [InlineData("unknown")]
    public async Task 로그인과_본인_화물근무정본이_맞아야_시작한다(string invalid)
    {
        using var f = new Fixture();
        switch (invalid)
        {
            case "anonymous": f.Auth.Login(null); break;
            case "owner": f.Api.Status!.DriverId = "other-driver"; break;
            case "stopped": f.Api.Status!.Status = "대기"; break;
            case "shift": f.Api.Status!.ShiftId = null; break;
            case "execution": f.Api.Status!.운송실행유형 = 운송실행유형코드.음식배달; break;
            case "unknown": f.Api.Status = null; break;
        }
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Prepare());
        Assert.False(f.Session.IsRunning); Assert.Empty(f.Api.Posts);
    }

    [Fact]
    public async Task 시작정본을_기다리는_동안_로그인이_바뀌면_늦은_시작을_막는다()
    {
        using var f = new Fixture();
        f.Api.BeforeStatus = () => { f.Auth.Login("other-driver"); return Task.CompletedTask; };
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Prepare());
        Assert.False(f.Session.IsRunning); Assert.Empty(f.Api.Posts);
    }

    [Fact]
    public async Task 아직_권한이_발급되기전_중지해도_늦은_시작은_취소된다()
    {
        using var f = new Fixture();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Api.BeforeStatus = () => { entered.SetResult(); return release.Task; };
        var preparation = f.Prepare(); await entered.Task;
        f.Session.Stop(notice: "시험 중지"); release.SetResult();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => preparation);
        Assert.False(f.Session.IsRunning); Assert.Empty(f.Api.Posts);
        f.Api.BeforeStatus = null;
        Assert.True(f.Session.TryActivate(await f.Prepare()));
    }

    [Fact]
    public async Task 측정시각을_그대로_전송하고_서버권장주기를_따른다()
    {
        using var f = new Fixture(); var id = await f.Start();
        f.Api.Interval = 45; var measurement = f.Fix() with { MeasuredAtUtc = f.Clock.Now.AddSeconds(-2) };
        Assert.True(await f.Session.SampleAndSendAsync(id, _ => Task.FromResult<DriverGpsObservation?>(measurement)));
        var post = Assert.Single(f.Api.Posts);
        Assert.Equal(measurement.MeasuredAtUtc.UtcDateTime, post.기록시각);
        Assert.Equal(37.57m, post.위도); Assert.Equal(127.03m, post.경도);
        Assert.Equal(7m, post.정확도_m); Assert.Equal("운행중", post.운행상태);
        Assert.Equal("test-cargo", post.AppKey); Assert.Equal(45, f.Session.NextIntervalSeconds(id));
        Assert.Equal(3, f.Api.StatusReads); Assert.Null(f.Session.Notice);
    }

    [Theory]
    [InlineData("stale")]
    [InlineData("future")]
    [InlineData("nan-lat")]
    [InlineData("infinite-lon")]
    [InlineData("latitude")]
    [InlineData("longitude")]
    [InlineData("zero")]
    [InlineData("negative-accuracy")]
    [InlineData("nan-accuracy")]
    [InlineData("huge-accuracy")]
    [InlineData("mock")]
    [InlineData("null")]
    public async Task 신선하지_않거나_유효하지_않은_측정은_전송하지_않는다(string invalid)
    {
        using var f = new Fixture(); var id = await f.Start();
        DriverGpsObservation? fix = invalid switch
        {
            "stale" => f.Fix() with { MeasuredAtUtc = f.Clock.Now.AddSeconds(-31) },
            "future" => f.Fix() with { MeasuredAtUtc = f.Clock.Now.AddTicks(1) },
            "nan-lat" => f.Fix() with { Latitude = double.NaN },
            "infinite-lon" => f.Fix() with { Longitude = double.PositiveInfinity },
            "latitude" => f.Fix() with { Latitude = 91 },
            "longitude" => f.Fix() with { Longitude = -181 },
            "zero" => f.Fix() with { Latitude = 0, Longitude = 0 },
            "negative-accuracy" => f.Fix() with { Accuracy = -1 },
            "nan-accuracy" => f.Fix() with { Accuracy = double.NaN },
            "huge-accuracy" => f.Fix() with { Accuracy = double.MaxValue },
            "mock" => f.Fix() with { IsMock = true },
            _ => null
        };
        Assert.False(await f.Session.SampleAndSendAsync(id, _ => Task.FromResult(fix)));
        Assert.Empty(f.Api.Posts); Assert.True(f.Session.IsRunning); Assert.NotNull(f.Session.Notice);
        Assert.Equal(60, f.Session.NextIntervalSeconds(id));
    }

    [Theory]
    [InlineData("logout")]
    [InlineData("other-account")]
    [InlineData("same-account-new-session")]
    [InlineData("server")]
    [InlineData("stopped")]
    [InlineData("shift")]
    [InlineData("execution")]
    public async Task 측정_중_계정_서버_근무가_바뀌면_위치POST를_보내지_않는다(string change)
    {
        using var f = new Fixture(); var id = await f.Start();
        Assert.False(await f.Session.SampleAndSendAsync(id, _ =>
        {
            switch (change)
            {
                case "logout": f.Auth.Login(null); break;
                case "other-account": f.Auth.Login("other-driver"); break;
                case "same-account-new-session": f.Auth.Login("driver-gps-test"); break;
                case "server": f.Endpoint = new("https://second.test/"); break;
                case "stopped": f.Api.Status!.Status = "대기"; break;
                case "shift": f.Api.Status!.ShiftId = 2; break;
                case "execution": f.Api.Status!.운송실행유형 = 운송실행유형코드.음식배달; break;
            }
            return Task.FromResult<DriverGpsObservation?>(f.Fix());
        }));
        Assert.Empty(f.Api.Posts); Assert.False(f.Session.IsRunning); Assert.NotNull(f.Session.Notice);
        Assert.True(f.Session.Lifetime(id).IsCancellationRequested);
    }

    [Fact]
    public async Task 같은_로그인세션의_토큰갱신은_운행을_중지하지_않는다()
    {
        using var f = new Fixture(); var id = await f.Start();
        f.Auth.Refresh(); Assert.True(f.Session.IsRunning);
        Assert.True(await f.Session.SampleAndSendAsync(id, _ => Task.FromResult<DriverGpsObservation?>(f.Fix())));
        Assert.Single(f.Api.Posts);
    }

    [Fact]
    public async Task 정본_재조회중_위치가_오래되면_새측정을_기다린다()
    {
        using var f = new Fixture(); var id = await f.Start();
        var measured = f.Fix();
        f.Api.BeforeStatus = () => { if (f.Api.StatusReads == 3) f.Clock.Now = f.Clock.Now.AddSeconds(31); return Task.CompletedTask; };
        Assert.False(await f.Session.SampleAndSendAsync(id, _ => Task.FromResult<DriverGpsObservation?>(measured)));
        Assert.Empty(f.Api.Posts); Assert.True(f.Session.IsRunning);
    }

    [Fact]
    public async Task 센서_시간초과는_재시도하지만_권한철회는_중지한다()
    {
        using var f = new Fixture(); var id = await f.Start();
        Assert.False(await f.Session.SampleAndSendAsync(id, _ => Task.FromException<DriverGpsObservation?>(new OperationCanceledException())));
        Assert.True(f.Session.IsRunning); Assert.Equal(60, f.Session.NextIntervalSeconds(id));
        Assert.Contains("시간", f.Session.Notice);
        Assert.False(await f.Session.SampleAndSendAsync(id, _ => Task.FromException<DriverGpsObservation?>(new DriverLocationPermissionRevokedException())));
        Assert.False(f.Session.IsRunning); Assert.Contains("권한", f.Session.Notice); Assert.Empty(f.Api.Posts);
    }

    [Fact]
    public async Task 서버응답이_미확인이면_재시도하고_다른소유자면_중지한다()
    {
        using var f = new Fixture(); var id = await f.Start();
        f.Api.NullResponse = true;
        Assert.False(await f.Session.SampleAndSendAsync(id, _ => Task.FromResult<DriverGpsObservation?>(f.Fix())));
        Assert.True(f.Session.IsRunning); Assert.Equal(60, f.Session.NextIntervalSeconds(id));
        f.Api.NullResponse = false; f.Api.ResponseOwner = "other-driver";
        Assert.False(await f.Session.SampleAndSendAsync(id, _ => Task.FromResult<DriverGpsObservation?>(f.Fix())));
        Assert.False(f.Session.IsRunning); Assert.Equal(2, f.Api.Posts.Count);
    }

    [Theory]
    [InlineData(1, 30)]
    [InlineData(900, 900)]
    [InlineData(1000, 900)]
    [InlineData(0, 30)]
    public async Task 권장주기와_실패대기는_정해진_범위를_넘지_않는다(int interval, int expected)
    {
        using var f = new Fixture(); var id = await f.Start(); f.Api.Interval = interval;
        Assert.True(await f.Session.SampleAndSendAsync(id, _ => Task.FromResult<DriverGpsObservation?>(f.Fix())));
        Assert.Equal(expected, f.Session.NextIntervalSeconds(id));
        for (var i = 0; i < 6; i++)
            await f.Session.SampleAndSendAsync(id, _ => Task.FromResult<DriverGpsObservation?>(null));
        Assert.InRange(f.Session.NextIntervalSeconds(id), expected, 900);
        Assert.True(await f.Session.SampleAndSendAsync(id, _ => Task.FromResult<DriverGpsObservation?>(f.Fix())));
        Assert.Equal(expected, f.Session.NextIntervalSeconds(id));
    }

    [Fact]
    public async Task 늦게끝난_이전측정은_새송신과_겹치거나_새권한을_취소하지_않는다()
    {
        using var f = new Fixture(); var oldId = await f.Start();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var oldTask = f.Session.SampleAndSendAsync(oldId, async _ => { entered.SetResult(); await release.Task; return f.Fix(); });
        await entered.Task;
        var newId = await f.Start(); var newRead = false;
        var newTask = f.Session.SampleAndSendAsync(newId, _ => { newRead = true; return Task.FromResult<DriverGpsObservation?>(f.Fix()); });
        Assert.False(newRead); Assert.False(newTask.IsCompleted);
        f.Session.Stop(oldId); Assert.True(f.Session.IsCurrent(newId));
        release.SetResult(); Assert.False(await oldTask); Assert.True(await newTask);
        Assert.True(newRead); Assert.Single(f.Api.Posts); Assert.True(f.Session.IsRunning);
    }

    [Fact]
    public async Task 중지는_진행중측정을_취소하고_실제종료를_기다릴수_있다()
    {
        using var f = new Fixture(); var id = await f.Start();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sample = f.Session.SampleAndSendAsync(id, async _ => { entered.SetResult(); await release.Task; return f.Fix(); });
        await entered.Task; var lifetime = f.Session.Lifetime(id);
        f.Session.Stop(id, "시험 중지"); var drain = f.Session.DrainAsync();
        Assert.True(lifetime.IsCancellationRequested); Assert.False(f.Session.IsRunning); Assert.False(drain.IsCompleted);
        release.SetResult(); Assert.False(await sample); await drain;
        Assert.Empty(f.Api.Posts); Assert.Equal("시험 중지", f.Session.Notice);
    }

    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 10, 24, 3, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private sealed class Fixture : IDisposable
    {
        public Clock Clock { get; } = new();
        public Auth Auth { get; } = new();
        public Api Api { get; } = new();
        public Uri Endpoint { get; set; } = new("https://gps-fixture.test/");
        public DriverLocationTransmissionSession Session { get; }
        public Fixture() => Session = new(Auth, Api, () => Endpoint, Clock);
        public DriverGpsObservation Fix() => new(37.57, 127.03, 7, Clock.Now);
        public Task<Guid> Prepare() => Session.PrepareAsync(new("test-cargo"));
        public async Task<Guid> Start() { var id = await Prepare(); Assert.True(Session.TryActivate(id)); return id; }
        public void Dispose() => Session.Dispose();
    }
    private sealed class Auth : IAuthSession
    {
        public string? AccessToken => UserId is null ? null : "synthetic-token";
        public string? RefreshToken => null;
        public DateTime AccessTokenExpiresAtUtc => DateTime.MaxValue;
        public DateTime RefreshTokenExpiresAtUtc => DateTime.MaxValue;
        public string? UserId { get; private set; } = "driver-gps-test";
        public string? UserName => UserId;
        public IReadOnlyList<string> Roles => [];
        public bool IsAuthenticated => UserId is not null;
        public long Version { get; private set; }
        public long SessionRevision { get; private set; }
        public event Action? Changed;
        public void Login(string? owner) { UserId = owner; Version++; SessionRevision++; Changed?.Invoke(); }
        public void Refresh() { Version++; Changed?.Invoke(); }
        public Task RestoreAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ApplyAsync(ClientAuthTokenSnapshot snapshot, CancellationToken cancellationToken = default) { Login(snapshot.UserId); return Task.CompletedTask; }
        public Task ClearAsync(CancellationToken cancellationToken = default) { Login(null); return Task.CompletedTask; }
        public Task<bool> TryApplyAsync(ClientAuthTokenSnapshot snapshot, long expectedVersion, CancellationToken cancellationToken = default, bool startsNewSession = false)
        { if (Version != expectedVersion) return Task.FromResult(false); Login(snapshot.UserId); return Task.FromResult(true); }
        public Task<bool> TryClearAsync(long expectedVersion, CancellationToken cancellationToken = default)
        { if (Version != expectedVersion) return Task.FromResult(false); Login(null); return Task.FromResult(true); }
    }
    private sealed class Api : IDriverWorkApiService
    {
        public 기사운행상태응답? Status { get; set; } = new()
        { DriverId = "driver-gps-test", ShiftId = 1, Status = "운행중", 운송실행유형 = 운송실행유형코드.화물운송 };
        public int StatusReads { get; private set; }
        public Func<Task>? BeforeStatus { get; set; }
        public List<기사위치갱신요청> Posts { get; } = [];
        public int Interval { get; set; } = 30;
        public bool NullResponse { get; set; }
        public string ResponseOwner { get; set; } = "driver-gps-test";
        public async Task<기사운행상태응답?> 운행상태조회Async(CancellationToken cancellationToken = default)
        { StatusReads++; if (BeforeStatus is not null) await BeforeStatus(); return Status; }
        public Task<기사위치갱신응답?> 위치갱신Async(기사위치갱신요청 request, CancellationToken cancellationToken = default)
        {
            Posts.Add(request);
            return Task.FromResult<기사위치갱신응답?>(NullResponse ? null : new()
            { DriverId = ResponseOwner, Status = "운행중", 권장위치전송간격초 = Interval });
        }
        public Task<기사현재근무응답?> 현재근무조회Async(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<기사근무요약응답>> 근무목록조회Async(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<기사근무요약응답?> 근무상세조회Async(long shiftId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<기사근무요약응답?> 기사별근무상세조회Async(string driverId, long shiftId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<기사운행시작응답?> 운행시작Async(기사운행시작요청 request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task 운행종료Async(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<CommunityDriverInquiryResponse>> 커뮤니티의뢰목록Async(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<CommunityDriverInquiryResponse?> 커뮤니티의뢰답변Async(Guid inquiryId, CommunityDriverInquiryDecisionRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
