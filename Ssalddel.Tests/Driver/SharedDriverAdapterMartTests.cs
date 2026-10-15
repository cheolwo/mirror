using Microsoft.EntityFrameworkCore;
using Ssalddel.Tests.Driver;
using 살뜰.도메인.공통;
using 살뜰.도메인.마트;

namespace Ssalddel.Tests.Services.Operations;

public sealed partial class 마트기사업무ServiceTests
{
    [Fact]
    public async Task 마트수락은공통수락거부를전달하고시도와영수증을만들지않는다()
    {
        await using var f = await Fixture.CreateAsync();
        var admission = new SharedDriverAdapterAdmissionProbe(f.Db) { RequireTransaction = true };
        var result = await f.Service(sharedAdmission: admission).수락Async(Driver, Ref, await f.AcceptRequestAsync());
        Assert.True(result.IsFailed);
        Assert.Equal("SharedDriverWorkProbeDenied", result.Errors.Single().Metadata["ErrorCode"]);
        Assert.Null((await f.Db.운송원장.SingleAsync()).확정기사Id);
        Assert.Empty(await f.Db.Set<마트배송시도>().ToListAsync());
        Assert.Empty(await f.Db.Set<마트기사요청영수증>().ToListAsync());
        Assert.Equal("Exit", admission.Calls.Last());
    }

    [Fact]
    public async Task 마트완료후수락영수증재생은신규제안설정과무관하게유지한다()
    {
        await using var f = await Fixture.CreateAsync();
        var request = await f.AcceptRequestAsync();
        var admission = new SharedDriverAdapterAdmissionProbe(f.Db) { Deny = false, RequireTransaction = true };
        var service = f.Service(sharedAdmission: admission);
        Assert.True((await service.수락Async(Driver, Ref, request)).IsSuccess);
        var queue = await f.Db.운송원장.SingleAsync();
        queue.상태 = 상태값.배차상태.인수완료; queue.배차큐단계 = 상태값.배차큐단계.종료;
        (await f.Db.Set<마트배송시도>().SingleAsync()).상태Code = "Delivered";
        await f.Db.SaveChangesAsync();
        admission.Deny = true; admission.Calls.Clear();

        var replay = await service.수락Async(Driver, Ref, request);
        Assert.True(replay.IsSuccess);
        Assert.True(replay.Value.IsReplay);
        Assert.Equal("Delivered", replay.Value.StateCode);
        Assert.DoesNotContain(admission.Calls, x => x.StartsWith("Check:", StringComparison.Ordinal));
        Assert.Single(await f.Db.Set<마트배송시도>().ToListAsync());
        Assert.Single(await f.Db.Set<마트기사요청영수증>().ToListAsync());
    }

    [Fact]
    public async Task 마트신규제안은숨겨도이미수락한업무는계속조회한다()
    {
        await using var f = await Fixture.CreateAsync();
        var admission = new SharedDriverAdapterAdmissionProbe(f.Db) { RequireTransaction = true };
        var service = f.Service(sharedAdmission: admission);
        Assert.Empty((await service.조회Async(Driver)).Value.Items);
        admission.Deny = false;
        Assert.True((await service.수락Async(Driver, Ref, await f.AcceptRequestAsync())).IsSuccess);
        admission.Deny = true;
        Assert.Single((await service.조회Async(Driver)).Value.Items);
    }
}
