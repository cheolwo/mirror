using System.Text.Json;
using FluentResults;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Ssalddel.Application.Driver.Notification;
using Ssalddel.Contracts.Common.Drivers;
using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Contracts.Driver.Notification;
using Ssalddel.Hubs;
using Ssalddel.Services.Dispatch.Participation;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Dispatch.Notification;
using 살뜰.Services.Dispatch.Common;
using 살뜰.Services.Notifications;
using 살뜰.Services.Options;
using 살뜰.Services.Storage.Local;
using 살뜰.도메인.공통;
using 살뜰.도메인.운송;

namespace Ssalddel.Tests.Services.Dispatch.Notification;

public sealed class DriverRecommendationOptInTests
{
    [Fact]
    public void 이전여섯인자와이전JSON은_종류별알림을자동허용하지않는다()
    {
        var legacy = new DriverNotificationSettings(true, false, true, true, false, false);
        Assert.False(legacy.음식배달추천알림사용); Assert.False(legacy.화물운송추천알림사용);
        var oldJson = JsonSerializer.Serialize(new { legacy.배차추천알림사용, legacy.운전중푸시만사용, legacy.소리사용,
            legacy.진동사용, legacy.야간알림제한, legacy.정차후모아보기 });
        var restored = JsonSerializer.Deserialize<DriverNotificationSettings>(oldJson);
        Assert.False(restored!.음식배달추천알림사용); Assert.False(restored.화물운송추천알림사용);
        Assert.False(new 기사알림설정수정요청().음식배달추천알림사용);
        Assert.False(new 기사알림설정응답().화물운송추천알림사용);
        Assert.False(new Ssalddel.Contracts.Driver.Settings.기사알림설정응답().음식배달추천알림사용);
        Assert.False(new Ssalddel.Contracts.Driver.Settings.기사알림설정수정요청().화물운송추천알림사용);
    }

    [Fact]
    public async Task 설정왕복은현재기사와기존여섯필드및종류별선택을보존한다()
    {
        await using var f = await Fixture.CreateAsync(false);
        var useCase = new 기사알림UseCase(f.Tokens, f.Settings, f.Db, NullLogger<기사알림UseCase>.Instance);
        var saved = await useCase.설정수정Async("driver", new()
        {
            배차추천알림사용 = true, 음식배달추천알림사용 = true, 화물운송추천알림사용 = false,
            운전중푸시만사용 = true, 소리사용 = false, 진동사용 = true, 야간알림제한 = true, 정차후모아보기 = true
        });
        Assert.True(saved.IsSuccess);
        var read = (await useCase.설정조회Async("driver")).Value;
        Assert.Equal("driver", read.DriverId); Assert.Equal("driver", f.Settings.WrittenOwner);
        Assert.True(read.음식배달추천알림사용); Assert.False(read.화물운송추천알림사용);
        Assert.True(read.배차추천알림사용); Assert.True(read.운전중푸시만사용); Assert.False(read.소리사용);
        Assert.True(read.진동사용); Assert.True(read.야간알림제한); Assert.True(read.정차후모아보기);
        Assert.True((await useCase.설정수정Async(null, new())).IsFailed);
        Assert.Equal(1, f.Settings.Writes);
    }

    public static IEnumerable<object[]> OptIns()
    {
        foreach (var food in new[] { false, true })
        foreach (var master in new[] { false, true })
        foreach (var foodOptIn in new[] { false, true })
        foreach (var cargoOptIn in new[] { false, true })
            yield return [food, master, foodOptIn, cargoOptIn];
    }

    [Theory, MemberData(nameof(OptIns))]
    public async Task 직접발송은전체허용과해당종류선택이모두있어야_현재앱의DataOnly로전송한다(
        bool food, bool master, bool foodOptIn, bool cargoOptIn)
    {
        await using var f = await Fixture.CreateAsync(food);
        f.Settings.Value = Settings(master, foodOptIn, cargoOptIn);
        var expected = master && (food ? foodOptIn : cargoOptIn);
        Assert.Equal(expected, await f.Direct().SendAsync("driver", f.Recommendations));
        Assert.Equal(expected ? 1 : 0, f.Push.Messages.Count);
        Assert.Equal(expected ? 1 : 0, f.Tokens.Reads);
        if (!expected) return;
        var message = Assert.Single(f.Push.Messages);
        Assert.True(message.DataOnly); Assert.True(message.HighPriority);
        Assert.Equal("", message.Title); Assert.Equal("", message.Body);
        Assert.Equal(food ? 기사앱식별자.FoodDeliveryDriverApp : 기사앱식별자.CargoYongdalDriverApp, message.Data["appKey"]);
        Assert.Equal("driver", message.Data["userId"]);
        Assert.Equal("request", message.Data["requestId"]);
        Assert.Equal("1", message.Data["recommendationRound"]);
        Assert.True(message.Data.ContainsKey("expiresAtUtc"));
        Assert.DoesNotContain("private-address", JsonSerializer.Serialize(message.Data));
        Assert.Equal(food ? "FoodDeliveryRecommendation" : 기사배차추천알림계약.현재유형, message.Data["type"]);
        Assert.Equal(food ? "FoodDelivery" : "CargoTransport", message.Data["workKind"]);
        Assert.All(f.Settings.Owners, owner => Assert.Equal("driver", owner));
    }

    [Theory]
    [InlineData(true, false)] [InlineData(false, false)]
    [InlineData(true, true)] [InlineData(false, true)]
    public async Task 생활물품Quick은화물선택을사용하되_기존전문앱과유형은보존한다(bool foodQueue, bool outbox)
    {
        await using var f = await Fixture.CreateAsync(foodQueue, relational: outbox);
        if (foodQueue) f.Queue.원본의뢰유형 = "SsalddelMartPackedOrder";
        else f.Queue.생활배송배차방식 = "automatic";
        await f.Db.SaveChangesAsync();
        f.Settings.Value = Settings(true, false, true);
        if (outbox)
        {
            var service = f.Outbox();
            await service.추천알림요청생성Async(1, "request", "driver", 1);
            await service.업무유형별대기알림발송Async(f.Queue.배차업무유형);
        }
        else Assert.True(await f.Direct().SendAsync("driver", f.Recommendations));
        var message = Assert.Single(f.Push.Messages);
        Assert.Equal("QuickDelivery", message.Data["workKind"]);
        Assert.Equal(foodQueue ? 기사앱식별자.FoodDeliveryDriverApp : 기사앱식별자.CargoYongdalDriverApp, message.Data["appKey"]);
        Assert.Equal(foodQueue ? "FoodDeliveryRecommendation" : 기사배차추천알림계약.현재유형, message.Data["type"]);

        f.Settings.Value = Settings(true, true, false);
        Assert.False(await f.Direct().SendAsync("driver", f.Recommendations));
        Assert.Single(f.Push.Messages);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task 기존수신서비스가있으면_토큰조회중Off변경도발송직전에재확인한다(bool food)
    {
        await using var f = await Fixture.CreateAsync(food);
        f.Settings.Value = Settings(true, true, true);
        var receiving = new Receiving();
        f.Tokens.BeforeRead = () => { receiving.On = false; return Task.CompletedTask; };
        Assert.False(await f.Direct(availability: receiving).SendAsync("driver", f.Recommendations));
        Assert.Empty(f.Push.Messages); Assert.True(receiving.Reads >= 2);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task 토큰조회중종류선택을철회하면_양직접발송경로에서전송하지않는다(bool food)
    {
        await using var f = await Fixture.CreateAsync(food);
        f.Settings.Value = Settings(true, true, true);
        f.Tokens.BeforeRead = () => { f.Settings.Value = Settings(true, !food, food); return Task.CompletedTask; };
        Assert.False(await f.Direct().SendAsync("driver", f.Recommendations));
        Assert.Equal(1, f.Tokens.Reads); Assert.Empty(f.Push.Messages);
        Assert.True(f.Settings.Reads >= 2);
    }

    [Theory]
    [InlineData("expired")] [InlineData("round")] [InlineData("recipient")] [InlineData("accepted")]
    public async Task 토큰조회중화물추천이바뀌면_옛라운드나다른기사에게발송하지않는다(string change)
    {
        await using var f = await Fixture.CreateAsync(false);
        f.Settings.Value = Settings(true, false, true);
        f.Tokens.BeforeRead = async () =>
        {
            switch (change)
            {
                case "expired": f.Queue.추천만료시각 = f.Clock.Now.AddSeconds(-1); break;
                case "round": f.Queue.추천라운드++; break;
                case "recipient": f.Queue.현재추천대상기사Id = "other-driver"; break;
                case "accepted": f.Queue.배차노출상태 = 상태값.배차노출상태.확정; break;
            }
            await f.Db.SaveChangesAsync();
        };
        Assert.False(await f.Direct().SendAsync("driver", f.Recommendations));
        Assert.Empty(f.Push.Messages);
    }

    [Theory]
    [InlineData("missing-store")] [InlineData("failed-read")] [InlineData("missing-setting")] [InlineData("other-recipient")]
    public async Task 직접발송은미확인설정과현재추천부재를_허용기본값으로대체하지않는다(string reason)
    {
        await using var f = await Fixture.CreateAsync(false);
        f.Settings.Value = Settings(true, true, true);
        if (reason == "failed-read") f.Settings.Throw = true;
        if (reason == "missing-setting") f.Settings.Value = null;
        if (reason == "other-recipient") { f.Queue.현재추천대상기사Id = "other"; await f.Db.SaveChangesAsync(); }
        Assert.False(await f.Direct(includeSettings: reason != "missing-store").SendAsync("driver", f.Recommendations));
        Assert.Equal(0, f.Tokens.Reads); Assert.Empty(f.Push.Messages);
    }

    [Theory]
    [InlineData(false, false, true)] [InlineData(true, false, false)] [InlineData(true, true, true)]
    public async Task 공통업무선택관문은활성일때만읽고_설정이나배차를쓰지않는다(bool enabled, bool allowed, bool expected)
    {
        await using var f = await Fixture.CreateAsync(false);
        f.Settings.Value = Settings(true, true, true);
        var shared = new Shared { Enabled = enabled, Allowed = allowed };
        Assert.Equal(expected, await f.Direct(shared: shared).SendAsync("driver", f.Recommendations));
        Assert.Equal(enabled, shared.Reads > 0);
        Assert.Equal(enabled, shared.Enabled);
        Assert.Equal(0, f.Settings.Writes);
        Assert.Equal(상태값.배차큐단계.배차추천, f.Queue.배차큐단계);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Outbox도토큰조회전후설정을확인하고_철회된알림을자동표시하지않는다(bool food)
    {
        await using var f = await Fixture.CreateAsync(food, relational: true);
        f.Settings.Value = Settings(true, true, true);
        var service = f.Outbox();
        await service.추천알림요청생성Async(1, "request", "driver", 1);
        f.Tokens.BeforeRead = () => { f.Settings.Value = Settings(false, true, true); return Task.CompletedTask; };
        Assert.Equal(1, await service.업무유형별대기알림발송Async(food ? 상태값.배차업무유형.음식배달 : 상태값.배차업무유형.용달운송));
        Assert.Empty(f.Push.Messages); Assert.Equal(1, f.Tokens.Reads);
        Assert.Equal("Suppressed", (await f.Db.배차추천알림Outbox.AsNoTracking().SingleAsync()).발송상태);
        Assert.Equal(상태값.배차노출상태.추천중, (await f.Db.운송원장.AsNoTracking().SingleAsync()).배차노출상태);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task 허용된Outbox는업무별현재추천데이터만보내며_OS알림본문을전송하지않는다(bool food)
    {
        await using var f = await Fixture.CreateAsync(food, relational: true);
        f.Settings.Value = Settings(true, food, !food);
        var service = f.Outbox();
        await service.추천알림요청생성Async(1, "request", "driver", 1);
        await service.업무유형별대기알림발송Async(food ? 상태값.배차업무유형.음식배달 : 상태값.배차업무유형.용달운송);
        var message = Assert.Single(f.Push.Messages);
        Assert.True(message.DataOnly); Assert.Equal("", message.Title); Assert.Equal("", message.Body);
        Assert.Equal("driver", message.Data["userId"]);
        Assert.Equal(food ? 기사앱식별자.FoodDeliveryDriverApp : 기사앱식별자.CargoYongdalDriverApp, message.Data["appKey"]);
        Assert.Equal("request", message.Data[food ? "offerId" : "requestId"]);
        Assert.Equal("Succeeded", (await f.Db.배차추천알림Outbox.AsNoTracking().SingleAsync()).발송상태);
    }

    private static DriverNotificationSettings Settings(bool master, bool food, bool cargo)
        => new(master, false, true, true, false, false, food, cargo);

    private sealed class Fixture(SqliteConnection? connection, SsalddelContext db, 운송원장 queue) : IAsyncDisposable
    {
        public SsalddelContext Db { get; } = db;
        public 운송원장 Queue { get; } = queue;
        public Clock Clock { get; } = new();
        public Store Settings { get; } = new();
        public Tokens Tokens { get; } = new();
        public Push Push { get; } = new();
        public PushState PushState { get; } = new();
        public IReadOnlyList<DispatchRecommendationDto> Recommendations => [new() { 의뢰Id = "request", 픽업지 = "private-address", 하차지 = "private-address" }];
        public static async Task<Fixture> CreateAsync(bool food, bool relational = false)
        {
            SqliteConnection? connection = null;
            var options = new DbContextOptionsBuilder<SsalddelContext>();
            if (relational) { connection = new("Data Source=:memory:"); await connection.OpenAsync(); options.UseSqlite(connection); }
            else options.UseInMemoryDatabase("notification-opt-in-" + Guid.NewGuid());
            var db = new SsalddelContext(options.Options, new Encryption());
            if (relational) await db.Database.EnsureCreatedAsync();
            var queue = new 운송원장 { Id = 1, 운송번호 = "opt-in", 의뢰Id = "request", 배차업무유형 = food ? 상태값.배차업무유형.음식배달 : 상태값.배차업무유형.용달운송,
                상태 = 상태값.배차대기상태.대기, 배차큐단계 = 상태값.배차큐단계.배차추천, 배차노출상태 = 상태값.배차노출상태.추천중,
                현재추천대상기사Id = "driver", 추천라운드 = 1, 추천만료시각 = DriverRecommendationOptInTests.Clock.Start.AddMinutes(5) };
            db.운송원장.Add(queue); await db.SaveChangesAsync();
            return new(connection, db, queue);
        }
        public FcmDriverRecommendationPushService Direct(bool includeSettings = true, Shared? shared = null, Receiving? availability = null)
            => new(Tokens, PushState, Push, Options.Create(new PushNotificationsOptions()), NullLogger<FcmDriverRecommendationPushService>.Instance,
                Db, includeSettings ? Settings : null, shared, Clock, availability);
        public 배차추천알림Service Outbox() => new(Db, Tokens, Push, NullLogger<배차추천알림Service>.Instance, Clock,
            Ssalddel.Tests.Support.Simulation운송자격Guard.Instance, Settings);
        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); if (connection is not null) await connection.DisposeAsync(); }
    }
    private sealed class Clock : TimeProvider
    {
        public static readonly DateTime Start = new(2026, 10, 23, 3, 0, 0, DateTimeKind.Utc);
        public DateTime Now { get; set; } = Start;
        public override DateTimeOffset GetUtcNow() => new(Now);
    }
    private sealed class Store : IDriverNotificationSettingsStore
    {
        public DriverNotificationSettings? Value = Settings(false, false, false);
        public bool Throw;
        public int Reads, Writes;
        public string? WrittenOwner;
        public List<string> Owners { get; } = [];
        public Task<DriverNotificationSettings> GetAsync(string driverId, CancellationToken cancellationToken = default)
        { Reads++; Owners.Add(driverId); return Throw ? Task.FromException<DriverNotificationSettings>(new IOException("private")) : Task.FromResult(Value!); }
        public Task SetAsync(string driverId, DriverNotificationSettings settings, CancellationToken cancellationToken = default)
        { Writes++; WrittenOwner = driverId; Value = settings; return Task.CompletedTask; }
    }
    private sealed class Tokens : IDriverPushTokenStore
    {
        public int Reads;
        public Func<Task>? BeforeRead;
        public Task SetAsync(string driverId, string pushToken, CancellationToken cancellationToken = default) => throw new InvalidOperationException("Unexpected token write");
        public Task ClearAsync(string driverId, CancellationToken cancellationToken = default) => throw new InvalidOperationException("Unexpected token clear");
        public Task<string?> GetAsync(string driverId, CancellationToken cancellationToken = default) => throw new InvalidOperationException("App key is required");
        public async Task<string?> GetForAppAsync(string driverId, string appKey, CancellationToken cancellationToken = default)
        { Assert.Equal("driver", driverId); Assert.Contains(appKey, new[] { 기사앱식별자.FoodDeliveryDriverApp, 기사앱식별자.CargoYongdalDriverApp }); Reads++; if (BeforeRead is not null) await BeforeRead(); return "token"; }
    }
    private sealed class PushState : IDriverRecommendationPushStateStore
    {
        public Task<bool> HasChangedAsync(string driverId, IReadOnlyList<string> ids, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<string?> GetSignatureAsync(string driverId, CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
    }
    private sealed class Push : IFcmPushService
    {
        public List<FcmPushMessage> Messages { get; } = [];
        public Task<bool> SendAsync(FcmPushMessage message, CancellationToken cancellationToken = default) { Messages.Add(message); return Task.FromResult(true); }
        public Task<bool> SendToTokenAsync(string token, string title, string body, IReadOnlyDictionary<string, string> data, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Automatic OS notification payload is forbidden");
    }
    private sealed class Shared : ISharedDriverWorkAdmissionService
    {
        public bool Enabled { get; set; }
        public bool Allowed { get; set; }
        public int Reads;
        public Task<Result> CheckRecommendationAsync(string driverId, 운송원장 queue, CancellationToken ct = default)
        { Reads++; return Task.FromResult(Allowed ? Result.Ok() : Result.Fail("Not selected")); }
        public Task<IDisposable> EnterAsync(string driverId, CancellationToken ct = default) => throw new InvalidOperationException("No admission write");
        public Task LockAsync(string driverId, CancellationToken ct = default) => throw new InvalidOperationException("No admission write");
        public Task<Result> CheckAsync(string driverId, IReadOnlyList<string> ids, CancellationToken ct = default) => throw new InvalidOperationException("No acceptance");
        public Task<Result> CheckParticipationAsync(string driverId, string kind, bool picking, CancellationToken ct = default) => throw new InvalidOperationException("Use current recommendation scope");
    }
    private sealed class Receiving : I운영배차공통UseCase
    {
        public bool On = true;
        public int Reads;
        public Task<운영배차수신상태Dto> 수신상태조회Async(string driverId, CancellationToken cancellationToken = default)
        {
            Assert.Equal("driver", driverId); Reads++;
            return Task.FromResult(new 운영배차수신상태Dto { 주체Id = driverId,
                수신의사Code = On ? 운영배차수신의사Code.On : 운영배차수신의사Code.Off,
                수신의사변경시각Utc = new DateTimeOffset(Clock.Start), 실효상태Code = 운영배차실효상태Code.배차가능 });
        }
        public Task<운영배차수신상태Dto> 기사의사변경Async(string id, 운영배차수신의사변경요청 request, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("No receiving write");
        public Task<운영배차수신상태Dto> 서버실효상태변경Async(string id, Guid requestId, string state, string reason, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("No receiving write");
        public Task<운영배차단기지표Dto> 단기지표조회Async(string id, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("No metrics read");
        public Task<bool> 활동사건기록Async(운영배차활동사건Dto value, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("No activity write");
    }
    private sealed class Encryption : IPersonalDataEncryptionService
    { public string? Protect(string? value) => value; public string? Unprotect(string? value) => value; }
}
