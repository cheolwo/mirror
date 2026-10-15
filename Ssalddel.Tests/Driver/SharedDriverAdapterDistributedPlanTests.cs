using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Ssalddel.Application.Warehouse;
using Ssalddel.Contracts.Common.Warehouse;
using Ssalddel.Services.Dispatch.Participation;
using Ssalddel.Tests.Application.Warehouse;
using 살뜰.도메인.기사;
using static Ssalddel.Tests.Application.Warehouse.DistributedTestFixture;

namespace Ssalddel.Tests.Driver;

public sealed class SharedDriverAdapterDistributedPlanTests
{
    private static DistributedActor DriverActor => DistributedTestFixture.Driver;
    [Fact]
    public async Task 직접피킹미선택기사는수동분산계획의새대상으로지정하지않는다()
    {
        await using var f = await Create();
        var wave = await f.Round(accept: false);
        f.Db.Set<DriverWorkParticipationRecord>().Add(new()
        {
            UserId = DriverActor.UserId, Revision = 1, CargoTransport = true, DirectPicking = false, MaxConcurrentJobs = 4
        });
        await f.Db.SaveChangesAsync();
        var admission = new SharedDriverWorkAdmissionService(f.Db, Options.Create(new SharedDriverWorkOptions { Enabled = true }));
        var error = await Assert.ThrowsAsync<DistributedFulfillmentException>(() =>
            f.Use(Operator, sharedAdmission: admission).PlanAsync(Operator, wave.WaveId,
                new() { RequestId = Guid.NewGuid(), ExpectedRevision = wave.Revision, DriverId = DriverActor.UserId }, default));
        Assert.Equal("DriverWork.DirectPickingNotSelected", error.Code);
        var current = await f.Use(Operator).WaveAsync(Operator, wave.WaveId, "warehouse", default);
        Assert.Equal(wave.Revision, current.Revision);
        Assert.All(await f.Db.운송원장.AsNoTracking().ToArrayAsync(), x => Assert.Null(x.확정기사Id));
    }

    [Fact]
    public async Task 공통기능OFF이면분산계획의기존기사선택절차를유지한다()
    {
        await using var f = await Create();
        var wave = await f.Round(accept: false);
        var admission = new SharedDriverWorkAdmissionService(f.Db, Options.Create(new SharedDriverWorkOptions { Enabled = false }));
        var result = await f.Use(Operator, sharedAdmission: admission).PlanAsync(Operator, wave.WaveId,
            new() { RequestId = Guid.NewGuid(), ExpectedRevision = wave.Revision, DriverId = DriverActor.UserId }, default);
        Assert.Equal("Planned", result.StatusCode);
        Assert.Equal(wave.Revision + 1, result.Revision);
    }

    [Fact]
    public async Task 분산계획은전체두건이기사상한한건을넘으면부분수락전에일괄보류한다()
    {
        await using var f = await Create();
        var wave = await f.Round(accept: false);
        Assert.Equal(2, wave.Orders.Count);
        f.Db.Set<DriverWorkParticipationRecord>().Add(new()
        {
            UserId = DriverActor.UserId, Revision = 1, CargoTransport = true, DirectPicking = true, MaxConcurrentJobs = 1
        });
        await f.Db.SaveChangesAsync();
        var admission = new SharedDriverWorkAdmissionService(f.Db, Options.Create(new SharedDriverWorkOptions { Enabled = true }));
        var error = await Assert.ThrowsAsync<DistributedFulfillmentException>(() =>
            f.Use(Operator, sharedAdmission: admission).PlanAsync(Operator, wave.WaveId,
                new() { RequestId = Guid.NewGuid(), ExpectedRevision = wave.Revision, DriverId = DriverActor.UserId }, default));
        Assert.Equal("DriverWork.ConcurrentLimitExceeded", error.Code);
        var current = await f.Use(Operator).WaveAsync(Operator, wave.WaveId, "warehouse", default);
        Assert.Equal(wave.Revision, current.Revision);
        Assert.Equal(wave.StatusCode, current.StatusCode);
        Assert.All(await f.Db.운송원장.AsNoTracking().ToArrayAsync(), x => Assert.Null(x.확정기사Id));
    }

    [Fact]
    public async Task 기존분산계획도수락전에전체상한을검사해첫건만확정되는것을막는다()
    {
        await using var f = await Create();
        // 공통 보호가 꺼진 기존 절차로 두 건 계획이 만들어진 상황을 재현합니다.
        var wave = await f.Round(accept: false);
        Assert.Equal(2, wave.Orders.Count);
        f.Db.Set<DriverWorkParticipationRecord>().Add(new()
        {
            UserId = DriverActor.UserId, Revision = 1, CargoTransport = true, DirectPicking = true, MaxConcurrentJobs = 1
        });
        await f.Db.SaveChangesAsync();
        f.RefreshLocation();
        var admission = new SharedDriverWorkAdmissionService(f.Db, Options.Create(new SharedDriverWorkOptions { Enabled = true }));
        var error = await Assert.ThrowsAsync<DistributedFulfillmentException>(() =>
            f.Use(DriverActor, sharedAdmission: admission).AcceptAsync(DriverActor, wave.WaveId,
                new() { RequestId = Guid.NewGuid(), ExpectedRevision = wave.Revision }, default));
        Assert.Equal("DriverWork.ConcurrentLimitExceeded", error.Code);
        var current = await f.Use(Operator).WaveAsync(Operator, wave.WaveId, "warehouse", default);
        Assert.Equal(wave.Revision, current.Revision);
        Assert.Equal(wave.StatusCode, current.StatusCode);
        Assert.All(await f.Db.운송원장.AsNoTracking().ToArrayAsync(), x => Assert.Null(x.확정기사Id));
    }

    [Fact]
    public async Task 분산계획영수증재생은이후직접피킹선택철회로막지않는다()
    {
        await using var f = await Create();
        var wave = await f.Round(accept: false);
        f.Db.Set<DriverWorkParticipationRecord>().Add(new()
        {
            UserId = DriverActor.UserId, Revision = 1, CargoTransport = true, DirectPicking = true, MaxConcurrentJobs = 4
        });
        await f.Db.SaveChangesAsync();
        var admission = new SharedDriverWorkAdmissionService(f.Db, Options.Create(new SharedDriverWorkOptions { Enabled = true }));
        var service = f.Use(Operator, sharedAdmission: admission);
        var request = new DistributedRoundPlanRequest
            { RequestId = Guid.NewGuid(), ExpectedRevision = wave.Revision, DriverId = DriverActor.UserId };
        var first = await service.PlanAsync(Operator, wave.WaveId, request, default);
        var profile = await f.Db.Set<DriverWorkParticipationRecord>().SingleAsync();
        profile.DirectPicking = false; await f.Db.SaveChangesAsync();
        var replay = await service.PlanAsync(Operator, wave.WaveId, request, default);
        Assert.True(replay.Replayed);
        Assert.Equal(first.Revision, replay.Revision);
    }
}
