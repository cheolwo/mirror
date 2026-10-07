using Microsoft.EntityFrameworkCore;
using Ssalddel.Application.Driver.DispatchAction;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Services.Community;
using 살뜰.도메인.공통;
namespace Ssalddel.Tests.Application.Driver.DispatchAction;

public sealed partial class NeighborhoodDeliveryAssignmentPersistenceTests
{
    [Fact]
    public async Task 자동추천만료뒤에는_이전제안을수락하거나_공개콜로수락할수없다()
    {
        await using var f = await Fixture.Create(); var queue = await f.Db.운송원장.SingleAsync();
        queue.생활배송배차방식 = NeighborhoodDispatchModes.Automatic; queue.생활배송선택판본 = 1;
        queue.추천만료시각 = DateTime.UtcNow.AddSeconds(-1); await f.Db.SaveChangesAsync();
        Assert.True((await f.Handler.Handle(new 배차수락Command("driver-1", "delivery-1", 3), default)).IsFailed);
        Assert.Null((await f.Db.운송원장.SingleAsync()).확정기사Id);
    }
    [Theory]
    [InlineData(NeighborhoodDispatchModes.Automatic, false)]
    [InlineData(NeighborhoodDispatchModes.PublicCall, true)]
    [InlineData(NeighborhoodDispatchModes.Hybrid, true)]
    public async Task 선택한모드에서만_공개콜을수락하며_연결전큐는_모든수락을차단한다(string mode, bool canAcceptPublic)
    {
        await using var f = await Fixture.Create(); var queue = await f.Db.운송원장.SingleAsync();
        queue.생활배송배차방식 = mode; queue.생활배송선택판본 = 1; queue.생활배송수락준비완료 = true;
        생활배송배차Policy.대기적용(queue);
        if (mode == NeighborhoodDispatchModes.Automatic) { queue.배차큐단계 = 상태값.배차큐단계.배차추천; queue.배차노출상태 = 상태값.배차노출상태.추천중; queue.현재추천대상기사Id = "driver-1"; queue.추천만료시각 = DateTime.UtcNow.AddMinutes(1); }
        Assert.Equal(canAcceptPublic, 배차응답가능정책.공개배차수락가능(queue));
        queue.생활배송수락준비완료 = false; Assert.False(배차응답가능정책.공개배차수락가능(queue)); Assert.False(배차응답가능정책.추천수락가능(queue, "driver-1", DateTime.UtcNow));
        await f.Db.SaveChangesAsync();
        Assert.True((await f.Handler.Handle(new 배차수락Command("driver-1", "delivery-1", 3), default)).IsFailed);
    }
    [Fact]
    public async Task 병행모드의_공개수락은_추천기사이외에도_동일원장한건만확정한다()
    {
        await using var f = await Fixture.Create(); var queue = await f.Db.운송원장.SingleAsync();
        queue.생활배송배차방식 = NeighborhoodDispatchModes.Hybrid; queue.생활배송선택판본 = 1;
        await f.Db.SaveChangesAsync();
        Assert.True((await f.Handler.Handle(new 배차수락Command("driver-1", "delivery-1", 3), default)).IsSuccess);
        Assert.Equal(2, (await f.Db.운송원장.SingleAsync()).생활배송선택판본);
        Assert.True((await f.Handler.Handle(new 배차수락Command("driver-2", "delivery-1", 3), default)).IsFailed);
        Assert.Equal("driver-1", (await f.Db.운송원장.SingleAsync()).확정기사Id);
    }
    [Fact]
    public async Task 배차선택은_본인권한_판본_멱등성을검사하고_수락후변경하지않는다()
    {
        await using var f = await Fixture.Create(); var queue = await f.Db.운송원장.SingleAsync();
        queue.생활배송배차방식 = NeighborhoodDispatchModes.Automatic; queue.생활배송선택판본 = 1; await f.Db.SaveChangesAsync();
        var service = new 생활배송배차선택Service(f.Db, new Owner());
        var command = new NeighborhoodDispatchChoiceRequest { ClientRequestId = Guid.NewGuid(), ExpectedDispatchRevision = 1, DispatchMode = NeighborhoodDispatchModes.PublicCall };
        Assert.True((await service.선택Async("delivery-1", command, default)).IsSuccess);
        Assert.True((await service.선택Async("delivery-1", command, default)).IsSuccess);
        Assert.Equal(2, (await f.Db.운송원장.SingleAsync()).생활배송선택판본);
        command.DispatchMode = NeighborhoodDispatchModes.Hybrid; Assert.True((await service.선택Async("delivery-1", command, default)).IsFailed);
        Assert.True((await f.Handler.Handle(new 배차수락Command("driver-1", "delivery-1", 3), default)).IsFailed);
        Assert.True((await f.Handler.Handle(new 배차수락Command("driver-1", "delivery-1", 4), default)).IsSuccess);
        command = new() { ClientRequestId = Guid.NewGuid(), ExpectedDispatchRevision = 3, DispatchMode = NeighborhoodDispatchModes.Automatic };
        Assert.True((await service.선택Async("delivery-1", command, default)).IsFailed);
    }
    private sealed class Owner : Ssalddel.Application.CommandProcessing.ICurrentUserAccessor { public string UserId => "owner-1"; public string Role => "일반회원"; }
}
