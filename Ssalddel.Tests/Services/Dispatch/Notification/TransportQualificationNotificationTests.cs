using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Ssalddel.Contracts.Common.Drivers;
using Ssalddel.Contracts.Driver.Profile;
using Ssalddel.Services.Dispatch.Compliance;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Dispatch.Notification;
using 살뜰.Services.Notifications;
using 살뜰.Services.Storage.Local;
using 살뜰.도메인.공통;
using 살뜰.도메인.설정;
using 살뜰.도메인.운송;

namespace Ssalddel.Tests.Services.Dispatch.Notification;

public sealed class TransportQualificationNotificationTests
{
    [Theory]
    [InlineData(운송자격Codes.NotSubmitted)]
    [InlineData(운송자격Codes.Pending)]
    [InlineData(운송자격Codes.Suspended)]
    public async Task 자격미확인기사에게는_새화물추천알림을생성하지않는다(string status)
    {
        await using var db = Context(); await SeedQueueAsync(db);
        var guard = new Guard { Status = status }; var tokens = new Tokens(); var push = new Push();
        await Service(db, guard, tokens, push).추천알림요청생성Async(1, "cargo-request", "driver", 1);
        Assert.Empty(await db.배차추천알림Outbox.ToListAsync());
        Assert.Equal(0, tokens.Reads); Assert.Equal(0, push.Sends);
        Assert.True(guard.Reads > 0);
    }

    [Fact]
    public async Task 이미생성된화물알림도_기사자격정지후에는_취소하고전송하지않는다()
    {
        await using var db = Context(); await SeedQueueAsync(db);
        var guard = new Guard(); var tokens = new Tokens(); var push = new Push();
        var service = Service(db, guard, tokens, push);
        await service.추천알림요청생성Async(1, "cargo-request", "driver", 1);
        Assert.Single(await db.배차추천알림Outbox.ToListAsync());
        guard.Status = 운송자격Codes.Suspended;
        await service.대기알림발송Async();
        Assert.Equal("Cancelled", (await db.배차추천알림Outbox.SingleAsync()).발송상태);
        Assert.Equal(0, tokens.Reads); Assert.Equal(0, push.Sends);
    }

    [Fact]
    public async Task 푸시토큰조회중자격이정지되면_발송직전재검사로취소한다()
    {
        await using var db = Context(); await SeedQueueAsync(db);
        var guard = new Guard(); var tokens = new Tokens { BeforeRead = () => guard.Status = 운송자격Codes.Suspended };
        var push = new Push(); var service = Service(db, guard, tokens, push);
        await service.추천알림요청생성Async(1, "cargo-request", "driver", 1);
        await service.대기알림발송Async();
        Assert.Equal(1, tokens.Reads); Assert.Equal(0, push.Sends);
        Assert.Equal("Cancelled", (await db.배차추천알림Outbox.SingleAsync()).발송상태);
        Assert.True(guard.Reads >= 3);
    }

    private static 배차추천알림Service Service(SsalddelContext db, Guard guard, Tokens tokens, Push push)
        => new(db, tokens, push, NullLogger<배차추천알림Service>.Instance, 신규업무자격: guard,
            notificationSettings: new Ssalddel.Tests.Support.ExplicitOptInNotificationSettingsStore());
    private static SsalddelContext Context() => new(new DbContextOptionsBuilder<SsalddelContext>()
        .UseInMemoryDatabase("qualification-push-" + Guid.NewGuid()).Options, new Encryption());
    private static async Task SeedQueueAsync(SsalddelContext db)
    {
        db.운송원장.Add(new 운송원장 { Id = 1, 운송번호 = "qualification-cargo", 의뢰Id = "cargo-request",
            배차업무유형 = 상태값.배차업무유형.용달운송, 상태 = 상태값.배차대기상태.대기,
            배차큐단계 = 상태값.배차큐단계.배차추천, 배차노출상태 = 상태값.배차노출상태.추천중,
            현재추천대상기사Id = "driver", 추천라운드 = 1, 추천만료시각 = DateTime.UtcNow.AddMinutes(5) });
        await db.SaveChangesAsync();
    }
    private sealed class Guard : I신규화물업무자격Guard
    {
        // 이 묶음은 알림 생성·발송 사이의 자격 변경만 검증한다. 화물 입력 완결성은 별도 정책 시험이 소유한다.
        public bool RequiresCargoEvidence => false;
        public string Status = 운송자격Codes.Valid;
        public int Reads;
        public Task<운송자격평가결과> 평가Async(string driverId, CancellationToken ct = default)
        { Reads++; Assert.Equal("driver", driverId); return Task.FromResult(new 운송자격평가결과(Status == 운송자격Codes.Valid,
            "Qualification." + Status, "자료 확인 상태", Status)); }
    }
    private sealed class Tokens : IDriverPushTokenStore
    {
        public Action? BeforeRead;
        public int Reads;
        public Task SetAsync(string driverId, string token, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ClearAsync(string driverId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<string?> GetAsync(string driverId, CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
        public Task<string?> GetForAppAsync(string driverId, string appKey, CancellationToken cancellationToken = default)
        { Assert.Equal(기사앱식별자.CargoYongdalDriverApp, appKey); Reads++; BeforeRead?.Invoke(); return Task.FromResult<string?>("token"); }
    }
    private sealed class Push : IFcmPushService
    {
        public int Sends;
        public Task<bool> SendAsync(FcmPushMessage message, CancellationToken cancellationToken = default)
        { Assert.True(message.DataOnly); Sends++; return Task.FromResult(true); }
        public Task<bool> SendToTokenAsync(string token, string title, string body, IReadOnlyDictionary<string, string> data,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("OS 자동 표시를 우회하는 DataOnly 경로만 사용합니다.");
    }
    private sealed class Encryption : IPersonalDataEncryptionService
    { public string? Protect(string? value) => value; public string? Unprotect(string? value) => value; }
}
