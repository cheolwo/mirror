using System.Reflection;
using System.Text.Json;
using Ssalddel.Application.Admin.Operating;
using Ssalddel.Contracts.Common.Dispatch;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Dispatch.Coordination;
using 살뜰.Services.Dispatch.Common;
using 살뜰.Services.Dispatch.Notification;
using 살뜰.Services.Dispatch.Queue;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.Services.Storage.Local;
using 살뜰.Services.Weather;
using 살뜰.도메인.공통;
using 살뜰.도메인.배차;
using 살뜰.도메인.음식;
using 살뜰.도메인.운송;

namespace Ssalddel.Tests.Services.Dispatch.Queue;

public sealed class 배차엔진판단감사Tests
{
    [Fact]
    public void 감사메타데이터는_식별자와판단결과를남기고_기사개인정보는제거한다()
    {
        const string driverId = "DRIVER-PRIVATE-001";
        var queue = CreateQueue();
        queue.원본의뢰유형 = 살뜰.Services.Dispatch.Engine.운송의뢰배차원천유형.살뜰마트포장완료주문;
        var selection = 배차추천후보선정결과.선정됨(new 배차추천후보(
            driverId,
            91.25m,
            $"{driverId} / rider@example.com / 010-1234-5678 추천")) with
        {
            감사Context = new 배차엔진판단감사Context(
                "correlation-001",
                Ssalddel.Contracts.Common.Versioning.OperatingSystemIds.SsalddelMartUrbanLogistics,
                Ssalddel.Contracts.Common.Versioning.EngineFamilyIds.TransportRequestDispatch,
                Ssalddel.Contracts.Common.Versioning.EngineImplementationIds.FoodDeliveryDispatch)
        };

        var auditEvent = 배차엔진판단감사이벤트Factory.생성(
            queue,
            selection,
            배차엔진후속전환.추천시작,
            "추천시작됨",
            new DateTime(2026, 7, 17, 1, 2, 3, DateTimeKind.Utc));

        Assert.Equal(운송이벤트유형.배차엔진판단감사, auditEvent.이벤트타입);
        Assert.DoesNotContain(driverId, auditEvent.메타데이터, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("rider@example.com", auditEvent.메타데이터, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("010-1234-5678", auditEvent.메타데이터, StringComparison.OrdinalIgnoreCase);

        using var document = JsonDocument.Parse(auditEvent.메타데이터);
        var metadata = document.RootElement;
        Assert.Equal(1, metadata.GetProperty("SchemaVersion").GetInt32());
        Assert.Equal("correlation-001", metadata.GetProperty("CorrelationId").GetString());
        Assert.Equal(
            Ssalddel.Contracts.Common.Versioning.OperatingSystemIds.SsalddelMartUrbanLogistics,
            metadata.GetProperty("OperatingSystemId").GetString());
        Assert.Equal(
            Ssalddel.Contracts.Common.Versioning.EngineFamilyIds.TransportRequestDispatch,
            metadata.GetProperty("EngineFamilyId").GetString());
        Assert.Equal(
            Ssalddel.Contracts.Common.Versioning.EngineImplementationIds.FoodDeliveryDispatch,
            metadata.GetProperty("EngineImplementationId").GetString());
        Assert.Equal(nameof(배차추천후보선정상태.선정됨), metadata.GetProperty("ResultStatus").GetString());
        Assert.Equal(91.25m, metadata.GetProperty("CandidateScore").GetDecimal());
        Assert.Contains("[REDACTED]", metadata.GetProperty("CandidateReason").GetString());
        Assert.Equal(배차엔진후속전환.추천시작, metadata.GetProperty("FollowUpTransition").GetString());
        Assert.Equal("추천시작됨", metadata.GetProperty("TransitionResultCode").GetString());
        Assert.False(metadata.TryGetProperty("DriverId", out _));
    }

    [Fact]
    public void 필수감사식별자가_비어있으면_이벤트생성을거부한다()
    {
        var selection = 배차추천후보선정결과.적격후보없음("후보 없음") with
        {
            감사Context = new 배차엔진판단감사Context(
                string.Empty,
                Ssalddel.Contracts.Common.Versioning.OperatingSystemIds.DomesticCargoTransport,
                Ssalddel.Contracts.Common.Versioning.EngineFamilyIds.TransportRequestDispatch,
                배차엔진감사식별자.미등록구현)
        };

        Assert.Throws<ArgumentException>(() => 배차엔진판단감사이벤트Factory.생성(
            CreateQueue(),
            selection,
            배차엔진후속전환.보류,
            "배차구성오류",
            DateTime.UtcNow));
    }

    [Fact]
    public async Task 후보없음_상태전환과_감사이벤트는_같은SaveChanges에포함된다()
    {
        await using var db = new CapturingSsalddelContext(CreateOptions());
        var queue = CreateQueue();
        queue.배차큐단계 = 상태값.배차큐단계.배차추천;
        queue.배차노출상태 = 상태값.배차노출상태.추천대기;
        db.Attach(queue);

        var selection = 배차추천후보선정결과.적격후보없음("현재 조건의 후보 없음") with
        {
            감사Context = new 배차엔진판단감사Context(
                "correlation-atomic",
                Ssalddel.Contracts.Common.Versioning.OperatingSystemIds.DomesticCargoTransport,
                Ssalddel.Contracts.Common.Versioning.EngineFamilyIds.TransportRequestDispatch,
                Ssalddel.Contracts.Common.Versioning.EngineImplementationIds.CargoYongdalDispatch)
        };
        var service = new 배차대기원장전환Service(
            db,
            Options.Create(new 배차큐정책Options { 최대추천라운드 = 5 }),
            new StubCandidateSelectionService(selection),
            null!,
            null!,
            null!);

        var method = typeof(배차대기원장전환Service).GetMethod(
            "추천거절후다음후보로진행Async",
            BindingFlags.Instance | BindingFlags.NonPublic);
        var task = Assert.IsAssignableFrom<Task<배차대기원장전환결과>>(
            method!.Invoke(service, [queue, null, CancellationToken.None]));
        var result = await task;

        Assert.True(result.전환여부);
        Assert.Equal(상태값.배차노출상태.공개중, queue.배차노출상태);
        var batch = Assert.Single(db.SaveBatches);
        Assert.Contains(batch, item => item.EntityType == typeof(운송원장) && item.State == EntityState.Modified);
        Assert.Contains(batch, item => item.EntityType == typeof(운송이벤트) && item.State == EntityState.Added);
    }

    [Fact]
    public async Task 음식배달_후보없음은_공개배차가아닌_재탐색대기로전환한다()
    {
        await using var db = new CapturingSsalddelContext(CreateOptions());
        var queue = CreateQueue();
        queue.배차업무유형 = 상태값.배차업무유형.음식배달;
        queue.원본의뢰유형 = 살뜰.Services.Dispatch.Engine.운송의뢰배차원천유형.음식점주문;
        queue.배차큐단계 = 상태값.배차큐단계.배차추천;
        queue.배차노출상태 = 상태값.배차노출상태.추천대기;
        queue.추천라운드 = 5;
        db.Attach(queue);

        var selection = 배차추천후보선정결과.적격후보없음("현재 조건의 음식배달 기사 없음") with
        {
            감사Context = new 배차엔진판단감사Context(
                "correlation-food-retry",
                Ssalddel.Contracts.Common.Versioning.OperatingSystemIds.FoodDelivery,
                Ssalddel.Contracts.Common.Versioning.EngineFamilyIds.TransportRequestDispatch,
                Ssalddel.Contracts.Common.Versioning.EngineImplementationIds.FoodDeliveryDispatch)
        };
        var service = new 배차대기원장전환Service(
            db,
            Options.Create(new 배차큐정책Options { 최대추천라운드 = 5 }),
            new StubCandidateSelectionService(selection),
            null!,
            null!,
            null!);

        var method = typeof(배차대기원장전환Service).GetMethod(
            "추천거절후다음후보로진행Async",
            BindingFlags.Instance | BindingFlags.NonPublic);
        var task = Assert.IsAssignableFrom<Task<배차대기원장전환결과>>(
            method!.Invoke(service, [queue, null, CancellationToken.None]));
        var result = await task;

        Assert.True(result.전환여부);
        Assert.Equal(배차대기원장전환결과코드.음식배달후보재탐색대기, result.결과코드);
        Assert.Equal(상태값.배차큐단계.배차추천, queue.배차큐단계);
        Assert.Equal(상태값.배차노출상태.추천후보없음, queue.배차노출상태);
        Assert.Null(queue.공개전환시각);
        var batch = Assert.Single(db.SaveBatches);
        Assert.Contains(batch, item => item.EntityType == typeof(운송원장) && item.State == EntityState.Modified);
        Assert.Contains(batch, item => item.EntityType == typeof(운송이벤트) && item.State == EntityState.Added);
    }

    [Fact]
    public async Task 후보선정_추천시작과_감사이벤트는_같은SaveChanges에포함된다()
    {
        await using var db = new CapturingSsalddelContext(CreateOptions());
        var queue = CreateQueue();
        queue.배차큐단계 = 상태값.배차큐단계.배차추천;
        queue.배차노출상태 = 상태값.배차노출상태.추천대기;
        db.Attach(queue);

        var selection = 배차추천후보선정결과.선정됨(
            new 배차추천후보("DRIVER-ATOMIC", 87m, "근접 기사")) with
        {
            감사Context = new 배차엔진판단감사Context(
                "correlation-recommendation",
                Ssalddel.Contracts.Common.Versioning.OperatingSystemIds.DomesticCargoTransport,
                Ssalddel.Contracts.Common.Versioning.EngineFamilyIds.TransportRequestDispatch,
                Ssalddel.Contracts.Common.Versioning.EngineImplementationIds.CargoYongdalDispatch)
        };
        var service = new 배차대기원장전환Service(
            db,
            Options.Create(new 배차큐정책Options
            {
                최대추천라운드 = 5,
                추천유지시간초 = 30
            }),
            new StubCandidateSelectionService(selection),
            new NoOpRecommendationNotificationService(),
            new NoOpDriverStateService(),
            null!);

        var method = typeof(배차대기원장전환Service).GetMethod(
            "추천거절후다음후보로진행Async",
            BindingFlags.Instance | BindingFlags.NonPublic);
        var task = Assert.IsAssignableFrom<Task<배차대기원장전환결과>>(
            method!.Invoke(service, [queue, null, CancellationToken.None]));
        var result = await task;

        Assert.True(result.전환여부);
        Assert.Equal(상태값.배차노출상태.추천중, queue.배차노출상태);
        var batch = Assert.Single(db.SaveBatches);
        Assert.Contains(batch, item => item.EntityType == typeof(운송원장) && item.State == EntityState.Modified);
        Assert.Contains(batch, item => item.EntityType == typeof(운송이벤트) && item.State == EntityState.Added);
        Assert.Contains(batch, item => item.EntityType == typeof(운영배차활동사건) && item.State == EntityState.Added);
    }

    [Fact]
    public async Task 음식배달_근처기사가_선정되면_알림전_픽업지기상과_지급액을_원장에_동결한다()
    {
        await using var db = new CapturingSsalddelContext(CreateOptions());
        var queue = CreateQueue();
        queue.배차업무유형 = 상태값.배차업무유형.음식배달;
        queue.원본의뢰유형 = 살뜰.Services.Dispatch.Engine.운송의뢰배차원천유형.음식점주문;
        queue.배차큐단계 = 상태값.배차큐단계.배차추천;
        queue.배차노출상태 = 상태값.배차노출상태.추천대기;
        db.Attach(queue);
        var selection = 배차추천후보선정결과.선정됨(
            new 배차추천후보("FOOD-DRIVER", 95m, "반경 내 기사")) with
        {
            감사Context = new 배차엔진판단감사Context(
                "correlation-food-pricing",
                Ssalddel.Contracts.Common.Versioning.OperatingSystemIds.FoodDelivery,
                Ssalddel.Contracts.Common.Versioning.EngineFamilyIds.TransportRequestDispatch,
                Ssalddel.Contracts.Common.Versioning.EngineImplementationIds.FoodDeliveryDispatch)
        };
        var pricing = new StubFoodPricingService();
        var service = new 배차대기원장전환Service(
            db,
            Options.Create(new 배차큐정책Options { 추천유지시간초 = 30 }),
            new StubCandidateSelectionService(selection),
            new NoOpRecommendationNotificationService(),
            new NoOpDriverStateService(),
            null!,
            pricing);

        var method = typeof(배차대기원장전환Service).GetMethod(
            "추천거절후다음후보로진행Async",
            BindingFlags.Instance | BindingFlags.NonPublic);
        var task = Assert.IsAssignableFrom<Task<배차대기원장전환결과>>(
            method!.Invoke(service, [queue, null, CancellationToken.None]));

        await task;

        Assert.Equal(1, pricing.CallCount);
        Assert.Equal(3500m, queue.기사지급예정액);
        Assert.Equal(1000m, queue.기사기상할증액);
        Assert.True(queue.기사기상할증적용여부);
        Assert.Equal("Available", queue.픽업지기상자료상태);
        Assert.Equal("food-pricing-test.r1", queue.기사제안요금정책판본);
    }

    [Fact]
    public async Task 기사거절은_선택한사유와_유효제안책임을_활동원장에남긴다()
    {
        await using var db = new SsalddelContext(
            new DbContextOptionsBuilder<SsalddelContext>()
                .UseInMemoryDatabase($"dispatch-reject-{Guid.NewGuid():N}")
                .Options,
            new DummyPersonalDataEncryptionService());
        var queue = CreateQueue();
        queue.배차업무유형 = 상태값.배차업무유형.음식배달;
        queue.원본의뢰유형 = 살뜰.Services.Dispatch.Engine.운송의뢰배차원천유형.음식점주문;
        queue.배차큐단계 = 상태값.배차큐단계.배차추천;
        queue.배차노출상태 = 상태값.배차노출상태.추천중;
        queue.현재추천대상기사Id = "DRIVER-REJECT";
        queue.추천라운드 = 2;
        queue.추천시작시각 = DateTime.UtcNow.AddSeconds(-5);
        queue.추천만료시각 = DateTime.UtcNow.AddSeconds(25);
        db.운송원장.Add(queue);
        await db.SaveChangesAsync();

        var noCandidate = 배차추천후보선정결과.적격후보없음("다음 후보 없음") with
        {
            감사Context = new 배차엔진판단감사Context(
                "correlation-reject",
                Ssalddel.Contracts.Common.Versioning.OperatingSystemIds.FoodDelivery,
                Ssalddel.Contracts.Common.Versioning.EngineFamilyIds.TransportRequestDispatch,
                Ssalddel.Contracts.Common.Versioning.EngineImplementationIds.FoodDeliveryDispatch)
        };
        var service = new 배차대기원장전환Service(
            db,
            Options.Create(new 배차큐정책Options { 최대추천라운드 = 5 }),
            new StubCandidateSelectionService(noCandidate),
            null!,
            null!,
            null!);

        await service.추천거절처리Async(
            queue.의뢰Id,
            "DRIVER-REJECT",
            운영배차거절사유Code.시간제약);

        var activity = Assert.Single(await db.운영배차활동사건.ToListAsync());
        Assert.Equal(운영배차사건유형Code.거절, activity.사건유형Code);
        Assert.Equal(운영배차제안유효성Code.유효, activity.제안유효성Code);
        Assert.Equal(운영배차책임Code.기사, activity.책임Code);
        Assert.Equal(운영배차거절사유Code.시간제약, activity.사유Code);
        Assert.Equal("DRIVER-REJECT", activity.주체Id);
    }

    [Fact]
    public void 저장된_감사이벤트는_수정할수없다()
    {
        using var db = new SsalddelContext(CreateOptions(), new DummyPersonalDataEncryptionService());
        var auditEvent = CreatePersistedAuditEvent();
        db.Attach(auditEvent);
        auditEvent.메타데이터 = "변조";

        var exception = Assert.Throws<InvalidOperationException>(() => db.SaveChanges());

        Assert.Contains("수정하거나 삭제할 수 없습니다", exception.Message);
    }

    [Fact]
    public async Task 저장된_감사이벤트는_삭제할수없다()
    {
        await using var db = new SsalddelContext(CreateOptions(), new DummyPersonalDataEncryptionService());
        var auditEvent = CreatePersistedAuditEvent();
        db.Attach(auditEvent);
        db.Remove(auditEvent);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => db.SaveChangesAsync());

        Assert.Contains("수정하거나 삭제할 수 없습니다", exception.Message);
    }

    [Fact]
    public async Task 관리자이벤트API경로는_감사이벤트생성을허용하지않는다()
    {
        var handler = new 운송이벤트생성CommandHandler(null!);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(
            new 운송이벤트생성Command(
                "REQUEST-AUDIT-1",
                운송이벤트유형.배차엔진판단감사,
                DateTime.UtcNow,
                "{}"),
            CancellationToken.None));

        Assert.Contains("배차 실행 경로에서만 생성", exception.Message);
    }

    [Fact]
    public async Task 관리자이벤트API경로는_감사이벤트수정과삭제를차단한다()
    {
        await using var db = new SsalddelContext(CreateOptions(), new DummyPersonalDataEncryptionService());
        var auditEvent = CreatePersistedAuditEvent();
        db.Attach(auditEvent);

        var updateHandler = new 운송이벤트수정CommandHandler(db);
        var updateException = await Assert.ThrowsAsync<InvalidOperationException>(() => updateHandler.Handle(
            new 운송이벤트수정Command(
                auditEvent.Id,
                auditEvent.의뢰Id,
                "OtherEvent",
                DateTime.UtcNow,
                "변조"),
            CancellationToken.None));

        var deleteHandler = new 운송이벤트삭제CommandHandler(db);
        var deleteResult = await deleteHandler.Handle(
            new 운송이벤트삭제Command(auditEvent.Id),
            CancellationToken.None);

        Assert.Contains("수정하거나", updateException.Message);
        Assert.True(deleteResult.IsFailed);
        Assert.Contains(deleteResult.Errors, error => error.Message.Contains("삭제할 수 없습니다", StringComparison.Ordinal));
    }

    private static 운송원장 CreateQueue()
        => new()
        {
            Id = 10,
            의뢰Id = "REQUEST-AUDIT-1",
            상태 = 상태값.배차대기상태.대기,
            배차업무유형 = 상태값.배차업무유형.용달운송,
            원본의뢰유형 = 살뜰.Services.Dispatch.Engine.운송의뢰배차원천유형.화주운송의뢰,
            픽업_위도 = 37.5m,
            픽업_경도 = 127m
        };

    private static 운송이벤트 CreatePersistedAuditEvent()
        => new()
        {
            Id = 100,
            의뢰Id = "REQUEST-AUDIT-1",
            이벤트타입 = 운송이벤트유형.배차엔진판단감사,
            메타데이터 = "{}"
        };

    private static DbContextOptions<SsalddelContext> CreateOptions()
        => new DbContextOptionsBuilder<SsalddelContext>()
            .UseMySql(
                "Server=localhost;Database=ssalddel_audit_test;User=root;Password=test;",
                new MySqlServerVersion(new Version(8, 4, 0)))
            .Options;

    private sealed class StubCandidateSelectionService(배차추천후보선정결과 result)
        : I배차추천후보선정Service
    {
        public Task<배차추천후보선정결과> 다음후보선정Async(
            string requestId,
            string? 제외기사Id = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(result);
    }

    private sealed class CapturingSsalddelContext(DbContextOptions<SsalddelContext> options)
        : SsalddelContext(options, new DummyPersonalDataEncryptionService())
    {
        public List<IReadOnlyList<(Type EntityType, EntityState State)>> SaveBatches { get; } = [];

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            => CaptureSave();

        public override Task<int> SaveChangesAsync(
            bool acceptAllChangesOnSuccess,
            CancellationToken cancellationToken = default)
            => CaptureSave();

        private Task<int> CaptureSave()
        {
            ChangeTracker.DetectChanges();
            var entries = ChangeTracker.Entries()
                .Where(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
                .Select(entry => (entry.Entity.GetType(), entry.State))
                .ToArray();
            SaveBatches.Add(entries);
            ChangeTracker.AcceptAllChanges();
            return Task.FromResult(entries.Length);
        }
    }

    private sealed class NoOpRecommendationNotificationService : I배차추천알림Service
    {
        public Task 추천알림요청생성Async(
            long 배차대기Id,
            string 의뢰Id,
            string 기사Id,
            int 추천라운드,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<int> 대기알림발송Async(
            int take = 100,
            CancellationToken cancellationToken = default)
            => Task.FromResult(0);
    }

    private sealed class StubFoodPricingService : I음식배달기사제안요금Service
    {
        public int CallCount { get; private set; }

        public Task<음식배달기사제안요금산정결과> 산정Async(
            운송원장 queue,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(new 음식배달기사제안요금산정결과(
                new 음식배달기사제안요금판정(2500m, 1000m, 3500m, true),
                new 픽업지기상관측결과(
                    true,
                    true,
                    픽업지기상자료상태Code.Available,
                    "1",
                    "1.0",
                    new DateTime(2026, 9, 11, 3, 0, 0, DateTimeKind.Utc),
                    픽업지기상관측결과.공식자료출처,
                    new string('a', 64)),
                "food-pricing-test.r1",
                new DateTime(2026, 9, 11, 3, 1, 0, DateTimeKind.Utc)));
        }
    }

    private sealed class NoOpDriverStateService : I국내화물운송기사상태Service
    {
        public Task<국내화물운송기사상태Snapshot> 운행시작Async(
            string driverId,
            long shiftId,
            DateTime startedAtUtc,
            string startMode,
            string startLocation,
            string? returnDestination,
            string? 복귀콜선호 = null,
            string? appKey = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<국내화물운송기사상태Snapshot> 위치갱신Async(
            DriverLocationSnapshot location,
            long? shiftId = null,
            decimal? 상차접근허용반경Km = null,
            string? appKey = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<국내화물운송기사상태Snapshot?> 추천기록Async(
            string driverId,
            DateTime 추천시각Utc,
            CancellationToken cancellationToken = default)
            => Task.FromResult<국내화물운송기사상태Snapshot?>(null);

        public Task<국내화물운송기사상태Snapshot?> 후보없음기록Async(
            string driverId,
            DateTime 기준시각Utc,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task 운행종료Async(
            string driverId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class DummyPersonalDataEncryptionService : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;

        public string? Unprotect(string? value) => value;
    }
}
