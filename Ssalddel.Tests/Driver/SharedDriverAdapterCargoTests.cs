using Microsoft.EntityFrameworkCore;
using Ssalddel.Application.Driver.DispatchAction;
using Ssalddel.Contracts.Driver.Settings;
using Ssalddel.Tests.Driver;
using 살뜰.Services.Dispatch.Queue;
using 살뜰.도메인.공통;
using 살뜰.도메인.운송;

namespace Ssalddel.Tests.Application.Driver.DispatchAction;

public sealed partial class 배차수락CommandHandlerTests
{
    [Fact]
    public async Task 공통수락거부는화물원장과본인수락상태를변경하지않는다()
    {
        await using var db = CreateContext();
        var (queue, request) = await SeedUnpaidRequestAsync(db, "운송완료후정산", "후불승인완료");
        var admission = new SharedDriverAdapterAdmissionProbe(db);
        var result = await CreateHandler(db, new StubEligibilityService(), admission)
            .Handle(new 배차수락Command("driver-1", queue.의뢰Id), CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal("SharedDriverWorkProbeDenied", result.Errors.Single().Metadata["ErrorCode"]);
        Assert.Equal(new[] { queue.의뢰Id }, admission.CandidateIds);
        Assert.Null(queue.확정기사Id);
        Assert.Equal(상태값.배차대기상태.대기, queue.상태);
        Assert.Equal(상태값.배차상태.매칭중, request.배차상태);
        Assert.Equal("Enter:driver-1", admission.Calls.First());
        Assert.Equal("Exit", admission.Calls.Last());
    }

    [Fact]
    public async Task 이미수락한화물은신규참여거부후에도원래수락결과를복구한다()
    {
        await using var db = CreateContext();
        var (queue, request) = await SeedUnpaidRequestAsync(db, "운송완료후정산", "후불승인완료");
        queue.상태 = 상태값.배차대기상태.확정;
        queue.배차큐단계 = 상태값.배차큐단계.확정;
        queue.기사_운송자 = queue.확정기사Id = "driver-1";
        request.배차상태 = 상태값.배차상태.배차확정;
        await db.SaveChangesAsync();
        var admission = new SharedDriverAdapterAdmissionProbe(db);
        var result = await CreateHandler(db, new StubEligibilityService(), admission)
            .Handle(new 배차수락Command("driver-1", queue.의뢰Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.DoesNotContain(admission.Calls, x => x.StartsWith("Check:", StringComparison.Ordinal));
        Assert.Equal("driver-1", queue.확정기사Id);
        Assert.Equal("Exit", admission.Calls.Last());
    }

    [Fact]
    public async Task 공개콜은선택한소형물품또는화물종류만노출한다()
    {
        await using var db = CreateContext();
        foreach (var quick in new[] { true, false })
            db.운송원장.Add(new 운송원장
            {
                의뢰Id = quick ? "quick" : "cargo", 상태 = 상태값.배차대기상태.대기,
                배차큐단계 = 상태값.배차큐단계.공개배차, 배차노출상태 = 상태값.배차노출상태.공개중,
                생활배송배차방식 = quick ? Ssalddel.Contracts.Common.Community.NeighborhoodDispatchModes.PublicCall : null
            });
        await db.SaveChangesAsync();
        var admission = new SharedDriverAdapterAdmissionProbe(db)
        {
            AllowsWorkKind = kind => kind == DriverWorkKinds.QuickDelivery
        };
        var offers = await new 공개배차Service(db, admission, 신규업무자격: Ssalddel.Tests.Support.Simulation운송자격Guard.Instance).GetPublicDispatchesAsync("driver-1");
        Assert.Equal("quick", Assert.Single(offers).의뢰Id);
        admission.AllowsWorkKind = kind => kind == DriverWorkKinds.CargoTransport;
        offers = await new 공개배차Service(db, admission, 신규업무자격: Ssalddel.Tests.Support.Simulation운송자격Guard.Instance).GetPublicDispatchesAsync("driver-1");
        Assert.Equal("cargo", Assert.Single(offers).의뢰Id);
    }
}
