using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Ssalddel.Contracts.Common.Versioning;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Dispatch.Engine;
using 살뜰.Services.Dispatch.Notification;
using 살뜰.Services.Dispatch.Queue;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.Services.Weather;
using 살뜰.도메인.공통;
using 살뜰.도메인.배차;
using 살뜰.도메인.운송;
using 살뜰.도메인.음식;

namespace Ssalddel.Tests.Services.Dispatch.Queue;

public sealed class FoodDemandParticipationDirectRecommendationTests
{
    [Fact]
    public async Task 기능_OFF이면_직접추천의_기존동작을_유지한다()
    {
        await using var fixture = new Fixture(enabled: false, selectedDriver: "another-driver");
        var result = await fixture.Service.추천시작Async(fixture.Queue.의뢰Id, "requested-driver");

        Assert.True(result.전환여부);
        Assert.Equal(0, fixture.Selection.Calls);
        Assert.Equal("requested-driver", fixture.Queue.현재추천대상기사Id);
        Assert.Equal(1, fixture.Pricing.Calls);
        Assert.Equal(1, fixture.Notifications.Calls);
    }

    [Fact]
    public async Task 기능_ON이고_현재_선정기사와_같으면_추천하고_선정감사를_연결한다()
    {
        await using var fixture = new Fixture(enabled: true, selectedDriver: "requested-driver");
        var result = await fixture.Service.추천시작Async(fixture.Queue.의뢰Id, "requested-driver");

        Assert.True(result.전환여부);
        Assert.Equal(1, fixture.Selection.Calls);
        Assert.Equal(1, fixture.Pricing.Calls);
        Assert.Equal(1, fixture.Notifications.Calls);
        fixture.Db.ChangeTracker.Clear();
        var saved = await fixture.Db.운송원장.SingleAsync();
        Assert.Equal("requested-driver", saved.현재추천대상기사Id);
        var audit = Assert.Single(await fixture.Db.운송이벤트.ToListAsync());
        using var metadata = JsonDocument.Parse(audit.메타데이터);
        Assert.Equal("direct-demand-test", metadata.RootElement.GetProperty("CorrelationId").GetString());
        Assert.Equal(배차엔진후속전환.추천시작, metadata.RootElement.GetProperty("FollowUpTransition").GetString());
    }

    [Fact]
    public async Task 현재_선정기사와_다르면_직접지정으로_우회할수없다()
    {
        await using var fixture = new Fixture(enabled: true, selectedDriver: "another-driver");
        var result = await fixture.Service.추천시작Async(fixture.Queue.의뢰Id, "requested-driver");

        Assert.False(result.전환여부);
        Assert.Equal(배차대기원장전환결과코드.추천준비안됨, result.결과코드);
        Assert.Equal(1, fixture.Selection.Calls);
        await AssertUnchangedAsync(fixture);
    }

    [Fact]
    public async Task 현재_적격후보가_없으면_직접추천도_상태변경없이_보류한다()
    {
        await using var fixture = new Fixture(enabled: true, selectedDriver: null);
        var result = await fixture.Service.추천시작Async(fixture.Queue.의뢰Id, "requested-driver");

        Assert.False(result.전환여부);
        Assert.Equal(배차대기원장전환결과코드.추천준비안됨, result.결과코드);
        Assert.Equal(1, fixture.Selection.Calls);
        await AssertUnchangedAsync(fixture);
    }

    [Fact]
    public async Task 유효한_기존추천은_새로운_참여판정으로_취소하지_않는다()
    {
        await using var fixture = new Fixture(enabled: true, selectedDriver: "another-driver");
        fixture.Queue.배차노출상태 = 상태값.배차노출상태.추천중;
        fixture.Queue.현재추천대상기사Id = "existing-driver";
        fixture.Queue.추천라운드 = 4;
        fixture.Queue.추천시작시각 = DateTime.UtcNow.AddSeconds(-10);
        fixture.Queue.추천만료시각 = DateTime.UtcNow.AddMinutes(1);
        await fixture.Db.SaveChangesAsync();
        var expires = fixture.Queue.추천만료시각;

        var result = await fixture.Service.추천시작Async(fixture.Queue.의뢰Id, "requested-driver");

        Assert.False(result.전환여부);
        Assert.Equal(배차대기원장전환결과코드.이미추천중, result.결과코드);
        Assert.Equal(0, fixture.Selection.Calls);
        Assert.Equal(0, fixture.Pricing.Calls);
        Assert.Equal(1, fixture.Notifications.Calls);
        Assert.Equal(("existing-driver", 4), fixture.Notifications.LastRequest);
        fixture.Db.ChangeTracker.Clear();
        var saved = await fixture.Db.운송원장.SingleAsync();
        Assert.Equal("existing-driver", saved.현재추천대상기사Id);
        Assert.Equal(expires, saved.추천만료시각);
        Assert.Equal(4, saved.추천라운드);
    }

    [Fact]
    public async Task 자동선정으로_전달된_결과는_다시_후보선정하지_않는다()
    {
        await using var fixture = new Fixture(enabled: true, selectedDriver: "selected-driver");
        var result = await fixture.Service.추천대기처리Async(fixture.Queue.의뢰Id);

        Assert.True(result.전환여부);
        Assert.Equal(1, fixture.Selection.Calls);
        Assert.Equal("selected-driver", fixture.Queue.현재추천대상기사Id);
        Assert.Equal(1, fixture.Pricing.Calls);
    }

    private static async Task AssertUnchangedAsync(Fixture fixture)
    {
        Assert.Equal(0, fixture.Pricing.Calls);
        Assert.Equal(0, fixture.Notifications.Calls);
        Assert.Equal(0, fixture.FoodRecord.Calls);
        Assert.Empty(await fixture.Db.운영배차활동사건.ToListAsync());
        Assert.Empty(await fixture.Db.운송이벤트.ToListAsync());
        fixture.Db.ChangeTracker.Clear();
        var saved = await fixture.Db.운송원장.SingleAsync();
        Assert.Equal(상태값.배차노출상태.추천대기, saved.배차노출상태);
        Assert.Null(saved.현재추천대상기사Id);
        Assert.Null(saved.추천시작시각);
        Assert.Null(saved.추천만료시각);
        Assert.Equal(0, saved.추천라운드);
        Assert.Null(saved.기사제안요금계산근거Json);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public SsalddelContext Db { get; }
        public 운송원장 Queue { get; }
        public SelectionPort Selection { get; }
        public PricingPort Pricing { get; } = new();
        public NotificationPort Notifications { get; } = new();
        public FoodRecordPort FoodRecord { get; } = new();
        public 배차대기원장전환Service Service { get; }

        public Fixture(bool enabled, string? selectedDriver)
        {
            Db = new(new DbContextOptionsBuilder<SsalddelContext>()
                .UseInMemoryDatabase($"food-demand-direct-{Guid.NewGuid():N}").Options, new PersonalDataPort());
            Queue = new()
            {
                Id = 10, 의뢰Id = "food-demand-direct-1", 상태 = 상태값.배차대기상태.대기,
                배차업무유형 = 상태값.배차업무유형.음식배달,
                원본의뢰유형 = 운송의뢰배차원천유형.음식점주문,
                배차큐단계 = 상태값.배차큐단계.배차추천, 배차노출상태 = 상태값.배차노출상태.추천대기,
                픽업_위도 = 37.5m, 픽업_경도 = 127m
            };
            Db.운송원장.Add(Queue);
            Db.SaveChanges();
            Selection = new(selectedDriver is null
                ? 배차추천후보선정결과.적격후보없음("DemandCapacityWaiting")
                : 배차추천후보선정결과.선정됨(new(selectedDriver, 90m, "DemandParticipationSelected"))
                    with { 감사Context = new("direct-demand-test", OperatingSystemIds.FoodDelivery,
                        "food-dispatch", "food-demand-test") });
            Service = new(Db, Options.Create(new 배차큐정책Options()), Selection, Notifications,
                null!, new 음식배달배차흐름Resolver(), Pricing, FoodRecord,
                Options.Create(new 음식배달수요참여Options { Enabled = enabled }));
        }

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }

    private sealed class SelectionPort(배차추천후보선정결과 result) : I배차추천후보선정Service
    {
        public int Calls { get; private set; }
        public Task<배차추천후보선정결과> 다음후보선정Async(string requestId, string? 제외기사Id = null, CancellationToken cancellationToken = default)
        { Calls++; return Task.FromResult(result); }
    }

    private sealed class NotificationPort : I배차추천알림Service
    {
        public int Calls { get; private set; }
        public (string Driver, int Round)? LastRequest { get; private set; }
        public Task 추천알림요청생성Async(long id, string request, string driver, int round, CancellationToken ct = default)
        { Calls++; LastRequest = (driver, round); return Task.CompletedTask; }
        public Task<int> 대기알림발송Async(int take = 100, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class FoodRecordPort : I음식배달기사추천기록Service
    {
        public int Calls { get; private set; }
        public Task 추천기록Async(string driver, DateTime now, CancellationToken ct = default)
        { Calls++; return Task.CompletedTask; }
    }

    private sealed class PricingPort : I음식배달기사제안요금Service
    {
        public int Calls { get; private set; }
        public Task<음식배달기사제안요금산정결과> 산정Async(운송원장 queue, CancellationToken ct = default)
        {
            Calls++;
            return Task.FromResult(new 음식배달기사제안요금산정결과(
                new 음식배달기사제안요금판정(2500m, 1000m, 500m, 4000m, true, true),
                new 픽업지기상관측결과(true, true, 픽업지기상자료상태Code.Available,
                    "1", "1.0", DateTime.UnixEpoch, 픽업지기상관측결과.공식자료출처, new string('a', 64)),
                "food-demand-test.r1", DateTime.UtcNow));
        }
    }

    private sealed class PersonalDataPort : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
}
