using System.Reflection;
using System.Text.Json;
using Hongdal.Application.Admin.Operating;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;
using 홍달.Data;
using 홍달.Infrastructure.Security;
using 홍달.Services.Dispatch.Coordination;
using 홍달.Services.Dispatch.Notification;
using 홍달.Services.Dispatch.Queue;
using 홍달.Services.Storage.Local;
using 홍달.도메인.공통;
using 홍달.도메인.운송;

namespace Hongdal.Tests.Services.Dispatch.Queue;

public sealed class 배차엔진판단감사Tests
{
    [Fact]
    public void 감사메타데이터는_식별자와판단결과를남기고_기사개인정보는제거한다()
    {
        const string driverId = "DRIVER-PRIVATE-001";
        var queue = CreateQueue();
        queue.원본의뢰유형 = 홍달.Services.Dispatch.Engine.운송의뢰배차원천유형.홍달마트포장완료주문;
        var selection = 배차추천후보선정결과.선정됨(new 배차추천후보(
            driverId,
            91.25m,
            $"{driverId} / rider@example.com / 010-1234-5678 추천")) with
        {
            감사Context = new 배차엔진판단감사Context(
                "correlation-001",
                Hongdal.Contracts.Common.Versioning.OperatingSystemIds.HongdalMartUrbanLogistics,
                Hongdal.Contracts.Common.Versioning.EngineFamilyIds.TransportRequestDispatch,
                Hongdal.Contracts.Common.Versioning.EngineImplementationIds.FoodDeliveryDispatch)
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
            Hongdal.Contracts.Common.Versioning.OperatingSystemIds.HongdalMartUrbanLogistics,
            metadata.GetProperty("OperatingSystemId").GetString());
        Assert.Equal(
            Hongdal.Contracts.Common.Versioning.EngineFamilyIds.TransportRequestDispatch,
            metadata.GetProperty("EngineFamilyId").GetString());
        Assert.Equal(
            Hongdal.Contracts.Common.Versioning.EngineImplementationIds.FoodDeliveryDispatch,
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
                Hongdal.Contracts.Common.Versioning.OperatingSystemIds.DomesticCargoTransport,
                Hongdal.Contracts.Common.Versioning.EngineFamilyIds.TransportRequestDispatch,
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
        await using var db = new CapturingHongdalContext(CreateOptions());
        var queue = CreateQueue();
        queue.배차큐단계 = 상태값.배차큐단계.배차추천;
        queue.배차노출상태 = 상태값.배차노출상태.추천대기;
        db.Attach(queue);

        var selection = 배차추천후보선정결과.적격후보없음("현재 조건의 후보 없음") with
        {
            감사Context = new 배차엔진판단감사Context(
                "correlation-atomic",
                Hongdal.Contracts.Common.Versioning.OperatingSystemIds.DomesticCargoTransport,
                Hongdal.Contracts.Common.Versioning.EngineFamilyIds.TransportRequestDispatch,
                Hongdal.Contracts.Common.Versioning.EngineImplementationIds.CargoYongdalDispatch)
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
    public async Task 후보선정_추천시작과_감사이벤트는_같은SaveChanges에포함된다()
    {
        await using var db = new CapturingHongdalContext(CreateOptions());
        var queue = CreateQueue();
        queue.배차큐단계 = 상태값.배차큐단계.배차추천;
        queue.배차노출상태 = 상태값.배차노출상태.추천대기;
        db.Attach(queue);

        var selection = 배차추천후보선정결과.선정됨(
            new 배차추천후보("DRIVER-ATOMIC", 87m, "근접 기사")) with
        {
            감사Context = new 배차엔진판단감사Context(
                "correlation-recommendation",
                Hongdal.Contracts.Common.Versioning.OperatingSystemIds.DomesticCargoTransport,
                Hongdal.Contracts.Common.Versioning.EngineFamilyIds.TransportRequestDispatch,
                Hongdal.Contracts.Common.Versioning.EngineImplementationIds.CargoYongdalDispatch)
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
    }

    [Fact]
    public void 저장된_감사이벤트는_수정할수없다()
    {
        using var db = new HongdalContext(CreateOptions(), new DummyPersonalDataEncryptionService());
        var auditEvent = CreatePersistedAuditEvent();
        db.Attach(auditEvent);
        auditEvent.메타데이터 = "변조";

        var exception = Assert.Throws<InvalidOperationException>(() => db.SaveChanges());

        Assert.Contains("수정하거나 삭제할 수 없습니다", exception.Message);
    }

    [Fact]
    public async Task 저장된_감사이벤트는_삭제할수없다()
    {
        await using var db = new HongdalContext(CreateOptions(), new DummyPersonalDataEncryptionService());
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
        await using var db = new HongdalContext(CreateOptions(), new DummyPersonalDataEncryptionService());
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
            원본의뢰유형 = 홍달.Services.Dispatch.Engine.운송의뢰배차원천유형.화주운송의뢰,
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

    private static DbContextOptions<HongdalContext> CreateOptions()
        => new DbContextOptionsBuilder<HongdalContext>()
            .UseMySql(
                "Server=localhost;Database=hongdal_audit_test;User=root;Password=test;",
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

    private sealed class CapturingHongdalContext(DbContextOptions<HongdalContext> options)
        : HongdalContext(options, new DummyPersonalDataEncryptionService())
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
