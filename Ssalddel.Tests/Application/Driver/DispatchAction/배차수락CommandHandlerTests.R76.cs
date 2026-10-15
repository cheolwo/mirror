using Microsoft.EntityFrameworkCore;
using Ssalddel.Application.Driver.DispatchAction;
using 살뜰.도메인.공통;

namespace Ssalddel.Tests.Application.Driver.DispatchAction;

public sealed partial class 배차수락CommandHandlerTests
{
    [Theory]
    [InlineData(null)]
    [InlineData(0d)]
    [InlineData(-1d)]
    [InlineData(0.5)]
    public async Task R76_고객운임이있어도_기사보수가미확정이면_수락과확정을차단한다(double? invalidPayout)
    {
        await using var db = CreateContext();
        var (queue, work) = await SeedUnpaidRequestAsync(db, "운송완료후정산", "후불승인완료");
        work.최종운임 = 42000m;
        var fare = await db.운임구성.SingleAsync(x => x.Id == work.운임구성Id);
        fare.기사지급예정운임 = invalidPayout.HasValue ? (decimal)invalidPayout.Value : null;
        await db.SaveChangesAsync();
        var result = await CreateHandler(db, new StubEligibilityService()).Handle(new("driver-1", queue.의뢰Id), default);
        Assert.True(result.IsFailed);
        Assert.Equal("FreightDriverPayoutAgreementRequired", Assert.Single(result.Errors).Metadata["ErrorCode"]);
        db.ChangeTracker.Clear();
        var current = await db.운송원장.SingleAsync();
        Assert.Null(current.확정기사Id); Assert.Equal(상태값.배차대기상태.대기, current.상태);
        Assert.Equal(42000m, (await db.화주운송의뢰.SingleAsync()).최종운임);
    }

    [Fact]
    public async Task R76_제안후보수가바뀌면_최신합의를확인하기전확정하지않는다()
    {
        await using var db = CreateContext();
        var (queue, _) = await SeedUnpaidRequestAsync(db, "운송완료후정산", "후불승인완료");
        queue.기사지급예정액 = 19000m; await db.SaveChangesAsync();
        var result = await CreateHandler(db, new StubEligibilityService()).Handle(new("driver-1", queue.의뢰Id), default);
        Assert.True(result.IsFailed);
        Assert.Equal("FreightDriverPayoutAgreementChanged", Assert.Single(result.Errors).Metadata["ErrorCode"]);
        Assert.Null(queue.확정기사Id); Assert.Equal(19000m, queue.기사지급예정액);
    }

    [Fact]
    public async Task R76_확정한보수는고객운임과구분해저장하고_재수락으로변경하지않는다()
    {
        await using var db = CreateContext();
        var (queue, work) = await SeedUnpaidRequestAsync(db, "운송완료후정산", "후불승인완료");
        work.최종운임 = 42000m; await db.SaveChangesAsync();
        var handler = CreateHandler(db, new StubEligibilityService());
        Assert.True((await handler.Handle(new("driver-1", queue.의뢰Id), default)).IsSuccess);
        Assert.Equal(18000m, queue.기사지급예정액); Assert.Equal(42000m, queue.운임);
        (await db.운임구성.SingleAsync()).기사지급예정운임 = null; await db.SaveChangesAsync();
        Assert.True((await handler.Handle(new("driver-1", queue.의뢰Id), default)).IsSuccess);
        Assert.Equal(18000m, queue.기사지급예정액);
    }
}
