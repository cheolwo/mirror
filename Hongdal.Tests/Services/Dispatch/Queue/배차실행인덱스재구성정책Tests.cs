using 홍달.도메인.공통;
using 홍달.도메인.배차;
using 홍달.Services.Dispatch.Queue;

namespace Hongdal.Tests.Services.Dispatch.Queue;

public sealed class 배차실행인덱스재구성정책Tests
{
    [Theory]
    [InlineData(상태값.배차업무유형.용달운송)]
    [InlineData(상태값.배차업무유형.음식배달)]
    public void 미처리운송의뢰인가_지원하는배차업무의_대기상태면_true(int 배차업무유형)
    {
        var now = new DateTime(2026, 7, 9, 3, 0, 0, DateTimeKind.Utc);
        var queue = CreateQueue(배차업무유형: 배차업무유형);

        Assert.True(배차실행인덱스재구성정책.미처리운송의뢰인가(queue, now));
    }

    [Fact]
    public void 미처리운송의뢰인가_지원하지않는배차업무면_false()
    {
        var now = new DateTime(2026, 7, 9, 3, 0, 0, DateTimeKind.Utc);
        var queue = CreateQueue(배차업무유형: 999);

        Assert.False(배차실행인덱스재구성정책.미처리운송의뢰인가(queue, now));
    }

    [Theory]
    [InlineData(상태값.배차업무유형.용달운송, 상태값.배차대기상태.확정, 상태값.배차큐단계.확정)]
    [InlineData(상태값.배차업무유형.용달운송, 상태값.배차대기상태.대기, 상태값.배차큐단계.종료)]
    [InlineData(상태값.배차업무유형.음식배달, 상태값.배차대기상태.확정, 상태값.배차큐단계.확정)]
    [InlineData(상태값.배차업무유형.음식배달, 상태값.배차대기상태.대기, 상태값.배차큐단계.종료)]
    public void 미처리운송의뢰인가_확정이나_종료는_false(int 배차업무유형, string 상태, int 배차큐단계)
    {
        var now = new DateTime(2026, 7, 9, 3, 0, 0, DateTimeKind.Utc);
        var queue = CreateQueue(배차업무유형: 배차업무유형);
        queue.상태 = 상태;
        queue.배차큐단계 = 배차큐단계;

        Assert.False(배차실행인덱스재구성정책.미처리운송의뢰인가(queue, now));
    }

    [Theory]
    [InlineData(상태값.배차업무유형.용달운송)]
    [InlineData(상태값.배차업무유형.음식배달)]
    public void 미처리운송의뢰인가_추천중이고_만료전이면_false(int 배차업무유형)
    {
        var now = new DateTime(2026, 7, 9, 3, 0, 0, DateTimeKind.Utc);
        var queue = CreateQueue(배차업무유형: 배차업무유형);
        queue.배차노출상태 = 상태값.배차노출상태.추천중;
        queue.현재추천대상기사Id = "DRV-1";
        queue.추천만료시각 = now.AddSeconds(30);

        Assert.True(배차실행인덱스재구성정책.유효한추천중잠금인가(queue, now));
        Assert.False(배차실행인덱스재구성정책.미처리운송의뢰인가(queue, now));
    }

    [Theory]
    [InlineData(상태값.배차업무유형.용달운송)]
    [InlineData(상태값.배차업무유형.음식배달)]
    public void 미처리운송의뢰인가_추천중이어도_만료됐으면_true(int 배차업무유형)
    {
        var now = new DateTime(2026, 7, 9, 3, 0, 0, DateTimeKind.Utc);
        var queue = CreateQueue(배차업무유형: 배차업무유형);
        queue.배차노출상태 = 상태값.배차노출상태.추천중;
        queue.현재추천대상기사Id = "DRV-1";
        queue.추천만료시각 = now.AddSeconds(-1);

        Assert.False(배차실행인덱스재구성정책.유효한추천중잠금인가(queue, now));
        Assert.True(배차실행인덱스재구성정책.미처리운송의뢰인가(queue, now));
    }

    [Fact]
    public void 미처리운송의뢰쿼리_정책과_동일하게_대상을_거른다()
    {
        var now = new DateTime(2026, 7, 9, 3, 0, 0, DateTimeKind.Utc);
        var activeCargo = CreateQueue("REQ-ACTIVE-CARGO");
        var activeFood = CreateQueue("REQ-ACTIVE-FOOD", 상태값.배차업무유형.음식배달);
        var unsupported = CreateQueue("REQ-UNSUPPORTED", 999);
        var locked = CreateQueue("REQ-LOCKED", 상태값.배차업무유형.음식배달);
        locked.배차노출상태 = 상태값.배차노출상태.추천중;
        locked.현재추천대상기사Id = "DRV-1";
        locked.추천만료시각 = now.AddMinutes(1);
        var confirmed = CreateQueue("REQ-CONFIRMED");
        confirmed.배차큐단계 = 상태값.배차큐단계.확정;

        var result = new[] { activeCargo, activeFood, unsupported, locked, confirmed }
            .AsQueryable()
            .미처리운송의뢰쿼리(now)
            .Select(x => x.의뢰Id)
            .ToArray();

        Assert.Equal(["REQ-ACTIVE-CARGO", "REQ-ACTIVE-FOOD"], result);
    }

    private static 운송원장 CreateQueue(
        string requestId = "REQ-1",
        int 배차업무유형 = 상태값.배차업무유형.용달운송)
        => new()
        {
            의뢰Id = requestId,
            배차업무유형 = 배차업무유형,
            상태 = 상태값.배차대기상태.대기,
            배차큐단계 = 상태값.배차큐단계.계획배차,
            배차노출상태 = 상태값.배차노출상태.계획대기,
            CreatedAt = new DateTime(2026, 7, 9, 0, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2026, 7, 9, 0, 0, 0, DateTimeKind.Utc)
        };
}
