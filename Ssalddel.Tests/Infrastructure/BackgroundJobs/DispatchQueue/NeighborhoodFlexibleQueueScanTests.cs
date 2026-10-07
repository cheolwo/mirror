using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Common.Community;
using 살뜰.Services.Dispatch.Queue;
using 살뜰.도메인.공통;
namespace Ssalddel.Tests.Infrastructure.BackgroundJobs.DispatchQueue;
public sealed partial class CargoDispatchJobDomainIsolationTests
{
    [Theory]
    [InlineData(NeighborhoodDispatchModes.Automatic)]
    [InlineData(NeighborhoodDispatchModes.Hybrid)]
    public async Task 자동추천부재는_간격후재탐색하고_미연결큐와자동공개전환은제외한다(string mode)
    {
        await using var fixture = await TestDatabase.CreateAsync(); var db = fixture.Context;
        var old = DateTime.UtcNow.AddHours(-2);
        var retry = Queue(1, "automatic-retry", 상태값.배차업무유형.용달운송, old, 상태값.배차큐단계.배차추천, 상태값.배차노출상태.추천후보없음);
        retry.생활배송배차방식 = mode;
        var recent = Queue(2, "automatic-recent", 상태값.배차업무유형.용달운송, old, 상태값.배차큐단계.배차추천, 상태값.배차노출상태.추천후보없음);
        recent.생활배송배차방식 = mode; recent.UpdatedAt = DateTime.UtcNow;
        var held = Queue(3, "held", 상태값.배차업무유형.용달운송, old); held.생활배송배차방식 = NeighborhoodDispatchModes.Hybrid; held.생활배송수락준비완료 = false;
        db.운송원장.AddRange(retry, recent, held); await db.SaveChangesAsync();
        var transition = new RecordingTransition();
        await Scan(db, transition.Service, new 배차큐정책Options { 당일미배정공개전환분 = 1, 음식배달후보재탐색간격초 = 30 }).Execute(Context());
        Assert.Equal(new[] { (nameof(I배차대기원장전환Service.추천대기처리Async), "automatic-retry") }, transition.Calls);
    }
}
