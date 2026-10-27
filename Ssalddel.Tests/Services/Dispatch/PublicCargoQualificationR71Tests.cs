using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Driver.Profile;
using Ssalddel.Hubs;
using Ssalddel.Services.Dispatch.Compliance;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Dispatch.Queue;
using 살뜰.Services.Dispatch.Request;
using 살뜰.Services.Storage.Local;
using 살뜰.도메인.공통;
using 살뜰.도메인.운송;
using 살뜰.도메인.화주;

namespace Ssalddel.Tests.Services.Dispatch;

public sealed class PublicCargoQualificationR71Tests
{
    [Theory]
    [InlineData("public")]
    [InlineData("national")]
    public async Task 검증연결이_없으면_공개나전국콜을_노출하지않는다(string scope)
    {
        await using var db = Context();
        await Seed(db, complete: true);
        Assert.Empty(await Read(db, scope, null));
    }

    [Theory]
    [InlineData("public")]
    [InlineData("national")]
    public async Task 정지되거나_미승인된기사는_조회시작부터_차단한다(string scope)
    {
        await using var db = Context();
        await Seed(db, complete: true);
        var guard = new Guard(_ => false);
        Assert.Empty(await Read(db, scope, guard));
        Assert.Equal(1, guard.Reads);
    }

    [Theory]
    [InlineData("public")]
    [InlineData("national")]
    public async Task 레거시화물정보누락과_누락원장은_공개나전국콜에서도_제외한다(string scope)
    {
        await using var db = Context();
        await Seed(db, complete: false);
        db.운송원장.Add(Queue("orphan"));
        await db.SaveChangesAsync();
        Assert.Empty(await Read(db, scope, new Guard(_ => true)));
    }

    [Theory]
    [InlineData("public")]
    [InlineData("national")]
    public async Task 유효기사와_완전한화물정보는_기존콜을_조회할수있다(string scope)
    {
        await using var db = Context();
        await Seed(db, complete: true);
        var guard = new Guard(_ => true);
        Assert.Equal("request", Assert.Single(await Read(db, scope, guard)).의뢰Id);
        Assert.Equal(2, guard.Reads);
    }

    [Theory]
    [InlineData("public")]
    [InlineData("national")]
    public async Task 조회중_정지되면_반환직전_추천을_제거한다(string scope)
    {
        await using var db = Context();
        await Seed(db, complete: true);
        var guard = new Guard(call => call == 1);
        Assert.Empty(await Read(db, scope, guard));
        Assert.Equal(2, guard.Reads);
    }

    [Fact]
    public async Task 전국화물콜은_음식배달원장을_포함하지않는다()
    {
        await using var db = Context();
        await Seed(db, complete: true);
        var food = Queue("food"); food.배차업무유형 = 상태값.배차업무유형.음식배달;
        db.운송원장.Add(food);
        await db.SaveChangesAsync();
        Assert.Equal("request", Assert.Single(await Read(db, "national", new Guard(_ => true))).의뢰Id);
    }

    private static Task<IReadOnlyList<DispatchRecommendationDto>> Read(SsalddelContext db, string scope, I신규화물업무자격Guard? guard)
        => scope == "public"
            ? new 공개배차Service(db, 신규업무자격: guard).GetPublicDispatchesAsync("driver")
            : new NationalDispatchRequestService(db, new Rejections(), 신규업무자격: guard).GetNationwideRequestsAsync("driver");

    private static SsalddelContext Context() => new(new DbContextOptionsBuilder<SsalddelContext>()
        .UseInMemoryDatabase("public-cargo-qualification-" + Guid.NewGuid().ToString("N")).Options, new Encryption());

    private static async Task Seed(SsalddelContext db, bool complete)
    {
        db.화주운송의뢰.Add(new 화주운송의뢰
        {
            의뢰Id = "request", 화물종류 = "상자", 화물중량Kg = complete ? 2m : null,
            화물길이Mm = complete ? 100 : null, 화물폭Mm = complete ? 200 : null,
            화물높이Mm = complete ? 300 : null, 픽업_도로명주소 = "상차지역", 하차_도로명주소 = "하차지역"
        });
        db.운송원장.Add(Queue("request"));
        await db.SaveChangesAsync();
    }

    private static 운송원장 Queue(string id) => new()
    {
        의뢰Id = id, 운송번호 = id, 배차업무유형 = 상태값.배차업무유형.용달운송,
        상태 = 상태값.배차대기상태.대기, 배차큐단계 = 상태값.배차큐단계.공개배차,
        배차노출상태 = 상태값.배차노출상태.공개중,
        픽업_도로명주소 = "상차지역", 하차_도로명주소 = "하차지역"
    };

    private sealed class Guard(Func<int, bool> allows) : I신규화물업무자격Guard
    {
        public int Reads { get; private set; }
        public Task<운송자격평가결과> 평가Async(string driverId, CancellationToken ct = default)
        {
            var allowed = allows(++Reads);
            return Task.FromResult(new 운송자격평가결과(allowed, allowed ? "Qualification.Valid" : "Qualification.Suspended",
                "명시적 시험 결과", allowed ? 운송자격Codes.Valid : 운송자격Codes.Suspended));
        }
    }

    private sealed class Encryption : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
    private sealed class Rejections : IDriverRejectedRequestStore
    {
        public Task RejectAsync(string driverId, string requestId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> IsRejectedAsync(string driverId, string requestId, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<IReadOnlySet<string>> GetRejectedRequestIdsAsync(string driverId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlySet<string>>(new HashSet<string>());
        public Task<IReadOnlySet<string>> GetRejectedDriverIdsAsync(string requestId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlySet<string>>(new HashSet<string>());
    }
}
