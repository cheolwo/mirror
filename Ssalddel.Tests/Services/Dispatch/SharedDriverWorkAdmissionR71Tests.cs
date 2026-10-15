using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Ssalddel.Contracts.Driver.Settings;
using Ssalddel.Services.Dispatch.Participation;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.도메인.기사;
using 살뜰.도메인.공통;
using 살뜰.도메인.운송;
using 살뜰.도메인.창고;
using 살뜰.도메인.화주;

namespace Ssalddel.Tests.Services.Dispatch;

public sealed class SharedDriverWorkAdmissionR71Tests
{
    [Theory]
    [InlineData(DriverWorkKinds.FoodDelivery)]
    [InlineData(DriverWorkKinds.QuickDelivery)]
    [InlineData(DriverWorkKinds.CargoTransport)]
    public void 명시선택하지않은업무를허용하지않는다(string kind)
    {
        Assert.Equal("DriverWork.NotSelected", SharedDriverWorkAdmissionPolicy.Participation(new(), kind, false).ErrorCode);
    }

    [Theory]
    [InlineData(1, 0, 1, true)]
    [InlineData(1, 1, 1, false)]
    [InlineData(3, 2, 1, true)]
    [InlineData(4, 3, 1, true)]
    [InlineData(4, 4, 1, false)]
    public void 기사희망건수와운영상한을함께적용한다(int limit, int active, int added, bool allowed)
    {
        var p = Profile(); p.MaxConcurrentJobs = limit;
        Assert.Equal(allowed, SharedDriverWorkAdmissionPolicy.Evaluate(p,
            Enumerable.Range(0, active).Select(i => Food("active-" + i)).ToArray(),
            Enumerable.Range(0, added).Select(i => Food("new-" + i)).ToArray(), 4).CanAccept);
    }

    [Fact]
    public void 같은원주문이분산과운송에중복되어도한번만센다()
    {
        var p = Profile(); p.MaxConcurrentJobs = 2;
        Assert.True(SharedDriverWorkAdmissionPolicy.Evaluate(p, [Food("a"), Food("a")], [Food("b")], 4).CanAccept);
    }

    [Fact]
    public void 이미수락한건의재처리는설정해제와상한축소로차단하지않는다()
    {
        Assert.True(SharedDriverWorkAdmissionPolicy.Evaluate(null, [Food("a")], [Food("a")], 1).CanAccept);
    }

    [Fact]
    public void 분리동의만으로음식과화물의미확인적재시간을허용하지않는다()
    {
        var p = Profile(); p.FoodCargoSeparationConfirmed = true;
        Assert.Equal("DriverWork.MixedWorkEvidenceIncomplete", SharedDriverWorkAdmissionPolicy.Evaluate(p,
            [Food("a")], [Cargo("b")], 4).ErrorCode);
    }

    [Fact]
    public void 퀵과화물의희망업무가달라도같은화물전문검사를유지해알려진용량으로통과한다()
    {
        var p = Profile(); p.CapacityKg = 10; p.CapacityLitres = 20;
        Assert.True(SharedDriverWorkAdmissionPolicy.Evaluate(p, [Cargo("a", DriverWorkKinds.QuickDelivery)], [Cargo("b")], 4).CanAccept);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void 같은퀵표시라도생활화물과마트실행의혼합은적재한도미설정으로우회하지못한다(bool cargoFirst)
    {
        var p = Profile(); p.FoodCargoSeparationConfirmed = true;
        var cargo = Cargo("cargo", DriverWorkKinds.QuickDelivery);
        var mart = new SharedDriverWorkDemand("mart", DriverWorkKinds.QuickDelivery, false, false, null, null);
        Assert.Equal("DriverWork.MixedWorkEvidenceIncomplete", SharedDriverWorkAdmissionPolicy.Evaluate(p,
            cargoFirst ? [cargo] : [mart], cargoFirst ? [mart] : [cargo], 4).ErrorCode);
    }

    [Fact]
    public void 알수없는중량을영으로바꾸지않는다()
    {
        var p = Profile(); p.CapacityKg = 10;
        Assert.Equal("DriverWork.LoadEvidenceIncomplete", SharedDriverWorkAdmissionPolicy.Evaluate(p,
            [], [Cargo("a") with { WeightKg = null }], 4).ErrorCode);
    }

    [Fact]
    public void 전달전미래픽업물품도중량여력을점유한다()
    {
        var p = Profile(); p.CapacityKg = 3;
        Assert.Equal("DriverWork.WeightCapacityExceeded", SharedDriverWorkAdmissionPolicy.Evaluate(p,
            [Cargo("a")], [Cargo("b")], 4).ErrorCode);
    }

    [Fact]
    public void 반납책임이남으면취소된큐를새여유로인식하지않는다()
    {
        Assert.Equal("DriverWork.ReturnCustodyPending", SharedDriverWorkAdmissionPolicy.Evaluate(Profile(),
            [Cargo("a") with { ReturnCustodyPending = true }], [Cargo("b")], 4).ErrorCode);
    }

    [Fact]
    public void 추가수신해제는첫배차를막지않고진행중추가배차만막는다()
    {
        var p = Profile(); p.ReceiveAdditionalOffers = false;
        Assert.True(SharedDriverWorkAdmissionPolicy.Evaluate(p, [], [Food("a")], 4).CanAccept);
        Assert.Equal("DriverWork.AdditionalOffersOff", SharedDriverWorkAdmissionPolicy.Evaluate(p, [Food("a")], [Food("b")], 4).ErrorCode);
    }

    [Fact]
    public async Task 기능이꺼져있으면새테이블이없는저장소에서도기존흐름을유지한다()
    {
        await using var db = new SsalddelContext(new DbContextOptionsBuilder<SsalddelContext>().UseSqlite("Data Source=:memory:").Options, new Encryption());
        var service = new SharedDriverWorkAdmissionService(db, Options.Create(new SharedDriverWorkOptions()));
        using var lease = await service.EnterAsync("driver");
        await service.LockAsync("driver");
        Assert.True((await service.CheckAsync("driver", ["not-created"])).IsSuccess);
        Assert.True((await service.CheckParticipationAsync("driver", "unknown", true)).IsSuccess);
        Assert.True((await service.CheckRecommendationAsync("driver", Queue("not-created", false))).IsSuccess);
    }

    [Fact]
    public async Task 새수락은SQLtransaction없이검사완료로표시하지않는다()
    {
        await using var f = await Fixture.Create();
        Assert.Equal("DriverWork.AcceptanceTransactionRequired", Code(await f.Service().CheckAsync("driver", ["a"])));
    }

    [Fact]
    public async Task 프로필없음과직접피킹미선택을서버원장으로검사한다()
    {
        await using var f = await Fixture.Create();
        f.Db.운송원장.Add(Queue("a", false));
        f.Db.Set<분산배송WaveRecord>().Add(new() { WaveId = "wave-a", RoundId = "round-a", DriverId = "driver" });
        f.Db.Set<분산배송주문Record>().Add(new() { OrderId = "order-a", WaveId = "wave-a", CargoRequestId = "a" });
        await f.Db.SaveChangesAsync();
        await using (var tx = await f.Db.Database.BeginTransactionAsync())
        { Assert.Equal("DriverWork.ProfileRequired", Code(await f.Service().CheckAsync("driver", ["a"]))); await tx.RollbackAsync(); }
        var p = Profile(); p.DirectPicking = false; f.Db.Add(p); await f.Db.SaveChangesAsync();
        Assert.Equal("DriverWork.DirectPickingNotSelected", Code(await f.Service().CheckRecommendationAsync("driver", Queue("a", false))));
        await using (var tx = await f.Db.Database.BeginTransactionAsync())
        { Assert.Equal("DriverWork.DirectPickingNotSelected", Code(await f.Service().CheckAsync("driver", ["a"]))); }
    }

    [Fact]
    public async Task 음식과마트와화물큐의공통건수상한을검사한다()
    {
        await using var f = await Fixture.Create();
        var p = Profile(); p.MaxConcurrentJobs = 1; f.Db.Add(p);
        f.Db.운송원장.AddRange(Queue("active", true), Queue("new", false));
        await f.Db.SaveChangesAsync();
        await using var tx = await f.Db.Database.BeginTransactionAsync();
        Assert.Equal("DriverWork.ConcurrentLimitExceeded", Code(await f.Service().CheckAsync("driver", ["new"])));
    }

    [Fact]
    public async Task SQL용량변환과설정판본보존을확인한다()
    {
        await using var f = await Fixture.Create();
        var p = Profile(); p.CapacityLitres = 9; f.Db.Add(p);
        f.Db.운송원장.Add(Queue("a", false)); f.Db.화주운송의뢰.Add(new() { 의뢰Id = "a", 화물중량Kg = 2, 화물부피Cbm = .01m });
        await f.Db.SaveChangesAsync();
        await using var tx = await f.Db.Database.BeginTransactionAsync();
        Assert.Equal("DriverWork.VolumeCapacityExceeded", Code(await f.Service().CheckAsync("driver", ["a"])));
        await tx.CommitAsync(); f.Db.ChangeTracker.Clear();
        var current = await f.Db.Set<DriverWorkParticipationRecord>().SingleAsync();
        Assert.Equal(1, current.Revision); Assert.Equal(1, current.WorkLockRevision);
    }

    [Fact]
    public async Task 종료된분산운송에도남은반납수량이있으면거부한다()
    {
        await using var f = await Fixture.Create(); f.Db.Add(Profile());
        var held = Queue("held", true); held.배차큐단계 = 상태값.배차큐단계.종료; held.상태 = "인수완료";
        f.Db.운송원장.AddRange(held, Queue("new", false));
        f.Db.Set<분산거점Record>().Add(new() { SiteId = "site", WarehouseId = 9 });
        f.Db.Set<분산적재위치Record>().Add(new() { LocationId = "location", SiteId = "site", Barcode = "barcode" });
        f.Db.Set<분산입고계획Record>().Add(new() { PlanId = "plan" });
        f.Db.Set<분산재고LotRecord>().Add(new() { LotId = "lot", PlanId = "plan", LocationId = "location", SiteId = "site" });
        f.Db.Set<분산배송WaveRecord>().Add(new() { WaveId = "w", RoundId = "round", DriverId = "driver", StatusCode = "ReturnRequired" });
        f.Db.Set<분산배송주문Record>().Add(new() { OrderId = "o", WaveId = "w", CargoRequestId = "held", StatusCode = "ReturnRequired" });
        f.Db.Set<분산재고예약Record>().Add(new() { ReservationId = "r", OrderId = "o", WaveId = "w", LotId = "lot", PickedQuantity = 2, ReturnedQuantity = 1 });
        await f.Db.SaveChangesAsync();
        Assert.Equal("DriverWork.ReturnCustodyPending", Code(await f.Service().CheckRecommendationAsync("driver", Queue("new", false))));
        await using var tx = await f.Db.Database.BeginTransactionAsync();
        Assert.Equal("DriverWork.ReturnCustodyPending", Code(await f.Service().CheckAsync("driver", ["new"])));
    }

    [Fact]
    public async Task 종료된이전배달은새로운수락건수를소비하지않는다()
    {
        await using var f = await Fixture.Create(); var p = Profile(); p.MaxConcurrentJobs = 1; f.Db.Add(p);
        var previous = Queue("old", true); previous.배차큐단계 = 상태값.배차큐단계.종료;
        f.Db.운송원장.AddRange(previous, Queue("new", false)); await f.Db.SaveChangesAsync();
        await using var tx = await f.Db.Database.BeginTransactionAsync();
        Assert.True((await f.Service().CheckAsync("driver", ["new"])).IsSuccess);
    }

    [Fact]
    public async Task 추천조회에서도추가수신해제와활성상한을반영한다()
    {
        await using var f = await Fixture.Create(); var p = Profile(); p.ReceiveAdditionalOffers = false;
        f.Db.Add(p); f.Db.운송원장.Add(Queue("active", true)); await f.Db.SaveChangesAsync();
        Assert.Equal("DriverWork.AdditionalOffersOff", Code(await f.Service().CheckParticipationAsync("driver", DriverWorkKinds.CargoTransport, false)));
        p.ReceiveAdditionalOffers = true; p.MaxConcurrentJobs = 1; await f.Db.SaveChangesAsync();
        Assert.Equal("DriverWork.ConcurrentLimitExceeded", Code(await f.Service().CheckParticipationAsync("driver", DriverWorkKinds.CargoTransport, false)));
    }

    [Fact]
    public async Task 음식수행중근거없는화물혼합제안은조회부터보류하고잠금행을갱신하지않는다()
    {
        await using var f = await Fixture.Create(); var p = Profile(); p.FoodCargoSeparationConfirmed = true;
        f.Db.Add(p); var current = Queue("food", true); current.배차업무유형 = 상태값.배차업무유형.음식배달;
        f.Db.운송원장.AddRange(current, Queue("cargo", false)); await f.Db.SaveChangesAsync();
        Assert.Equal("DriverWork.MixedWorkEvidenceIncomplete", Code(await f.Service().CheckRecommendationAsync("driver", Queue("cargo", false))));
        Assert.Equal(0, (await f.Db.Set<DriverWorkParticipationRecord>().AsNoTracking().SingleAsync()).WorkLockRevision);
        Assert.Equal(1, await f.Db.운송원장.CountAsync(x => x.확정기사Id == "driver"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task 생활화물퀵과마트퀵의양방향혼합은추천과SQL수락모두보류한다(bool cargoFirst)
    {
        await using var f = await Fixture.Create(); var p = Profile(); p.FoodCargoSeparationConfirmed = true; f.Db.Add(p);
        운송원장 Quick(string id, bool active, bool cargo)
        {
            var q = Queue(id, active);
            if (cargo) q.생활배송배차방식 = "automatic";
            else { q.배차업무유형 = 상태값.배차업무유형.음식배달; q.원본의뢰유형 = "SsalddelMartPackedOrder"; }
            return q;
        }
        var current = Quick("active", true, cargoFirst); var proposed = Quick("candidate", false, !cargoFirst);
        Assert.Equal(DriverWorkKinds.QuickDelivery, SharedDriverWorkAdmissionService.ResolveWorkKind(current));
        Assert.Equal(DriverWorkKinds.QuickDelivery, SharedDriverWorkAdmissionService.ResolveWorkKind(proposed));
        f.Db.운송원장.AddRange(current, proposed); await f.Db.SaveChangesAsync();
        Assert.Equal("DriverWork.MixedWorkEvidenceIncomplete", Code(await f.Service().CheckRecommendationAsync("driver", proposed)));
        Assert.Equal(0, (await f.Db.Set<DriverWorkParticipationRecord>().AsNoTracking().SingleAsync()).WorkLockRevision);
        await using (var tx = await f.Db.Database.BeginTransactionAsync())
        {
            Assert.Equal("DriverWork.MixedWorkEvidenceIncomplete", Code(await f.Service().CheckAsync("driver", ["candidate"])));
            await tx.RollbackAsync();
        }
        f.Db.ChangeTracker.Clear(); Assert.Equal(1, await f.Db.운송원장.CountAsync(x => x.확정기사Id == "driver"));
        Assert.Null((await f.Db.운송원장.SingleAsync(x => x.의뢰Id == "candidate")).확정기사Id);
    }

    [Fact]
    public async Task 이미수락한건은프로필없는기존기사도정본재처리가가능하다()
    {
        await using var f = await Fixture.Create(); f.Db.운송원장.Add(Queue("a", true)); await f.Db.SaveChangesAsync();
        await using var tx = await f.Db.Database.BeginTransactionAsync();
        Assert.True((await f.Service().CheckAsync("driver", ["a"])).IsSuccess);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    public void 잘못된운영상한을배차허용으로보정하지않는다(int limit)
    {
        Assert.Equal("DriverWork.ConcurrentLimitInvalid", SharedDriverWorkAdmissionPolicy.Evaluate(Profile(), [], [Food("a")], limit).ErrorCode);
    }

    [Theory]
    [InlineData(false, DriverWorkKinds.FoodDelivery)]
    [InlineData(true, DriverWorkKinds.QuickDelivery)]
    public void 음식과마트완성포장품의종류는원천으로구분한다(bool mart, string expected)
    {
        var q = Queue("a", false); q.배차업무유형 = 상태값.배차업무유형.음식배달;
        q.원본의뢰유형 = mart ? "SsalddelMartPackedOrder" : "FoodOrder";
        Assert.Equal(expected, SharedDriverWorkAdmissionService.ResolveWorkKind(q));
    }

    [Fact]
    public async Task 같은기사두요청은마지막한자리여유를중복수락하지못한다()
    {
        await using var f = await Fixture.Create(); var p = Profile(); p.MaxConcurrentJobs = 1; f.Db.Add(p);
        var a = Queue("a", false); a.배차업무유형 = 상태값.배차업무유형.음식배달;
        var b = Queue("b", false); b.배차업무유형 = 상태값.배차업무유형.음식배달;
        f.Db.운송원장.AddRange(a, b); await f.Db.SaveChangesAsync();
        async Task<bool> Accept(string id)
        {
            await using var db = f.OpenDb(); var service = f.Service(db);
            using var lease = await service.EnterAsync("driver");
            await using var tx = await db.Database.BeginTransactionAsync();
            var result = await service.CheckAsync("driver", [id]);
            if (result.IsFailed) { await tx.RollbackAsync(); return false; }
            var queue = await db.운송원장.SingleAsync(x => x.의뢰Id == id);
            queue.확정기사Id = "driver"; queue.기사_운송자 = "driver"; queue.배차큐단계 = 상태값.배차큐단계.확정;
            await db.SaveChangesAsync(); await tx.CommitAsync(); return true;
        }
        var results = await Task.WhenAll(Task.Run(() => Accept("a")), Task.Run(() => Accept("b")));
        Assert.Single(results.Where(x => x));
        f.Db.ChangeTracker.Clear(); Assert.Equal(1, await f.Db.운송원장.CountAsync(x => x.확정기사Id == "driver"));
    }

    private static string? Code(FluentResults.Result r) => r.IsFailed ? r.Errors[0].Metadata["ErrorCode"]?.ToString() : null;
    private static SharedDriverWorkDemand Food(string id) => new(id, DriverWorkKinds.FoodDelivery, false, false, null, null);
    private static SharedDriverWorkDemand Cargo(string id, string kind = DriverWorkKinds.CargoTransport) => new(id, kind, false, true, 2, 10);
    private static DriverWorkParticipationRecord Profile() => new() { UserId = "driver", Revision = 1, FoodDelivery = true,
        QuickDelivery = true, CargoTransport = true, DirectPicking = true, VehicleKind = DriverVehicleKinds.Motorcycle, MaxConcurrentJobs = 4 };
    private static 운송원장 Queue(string id, bool active) => new() { 의뢰Id = id, 운송번호 = id,
        상태 = active ? "확정" : "배차대기", 배차큐단계 = active ? 상태값.배차큐단계.확정 : 상태값.배차큐단계.배차추천,
        확정기사Id = active ? "driver" : null, 기사_운송자 = active ? "driver" : "", 배차업무유형 = 상태값.배차업무유형.용달운송 };
    private sealed class Encryption : IPersonalDataEncryptionService { public string? Protect(string? v) => v; public string? Unprotect(string? v) => v; }
    private sealed class Fixture(SqliteConnection keeper, string connectionString, SsalddelContext db) : IAsyncDisposable
    {
        public SsalddelContext Db { get; } = db;
        public SsalddelContext OpenDb() => new(new DbContextOptionsBuilder<SsalddelContext>().UseSqlite(connectionString).Options, new Encryption());
        public SharedDriverWorkAdmissionService Service(SsalddelContext? context = null) => new(context ?? Db, Options.Create(new SharedDriverWorkOptions { Enabled = true }));
        public static async Task<Fixture> Create()
        {
            var connectionString = "Data Source=shared-driver-" + Guid.NewGuid().ToString("N") + ";Mode=Memory;Cache=Shared";
            var keeper = new SqliteConnection(connectionString); await keeper.OpenAsync();
            var db = new SsalddelContext(new DbContextOptionsBuilder<SsalddelContext>().UseSqlite(connectionString).Options, new Encryption());
            await db.Database.EnsureCreatedAsync(); return new(keeper, connectionString, db);
        }
        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await keeper.DisposeAsync(); }
    }
}
