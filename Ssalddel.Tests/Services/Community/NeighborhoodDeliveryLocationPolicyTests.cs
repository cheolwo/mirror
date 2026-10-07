using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Services.Community;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.도메인.공통;
using 살뜰.도메인.기사;
using 살뜰.도메인.운송;
using 살뜰.도메인.화주;

namespace Ssalddel.Tests.Services.Community;

public sealed class NeighborhoodDeliveryLocationPolicyTests
{
    private static readonly DateTime Now = new(2026, 10, 5, 3, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(-600, -600, true)]
    [InlineData(-601, -1, false)]
    [InlineData(-1, -601, false)]
    [InlineData(1, -1, false)]
    [InlineData(-1, 1, false)]
    public void 측정과수신이_모두10분이내이어야한다(int measuredAgeSeconds, int receivedAgeSeconds, bool expected)
    {
        var location = Location(Now.AddSeconds(measuredAgeSeconds), Now.AddSeconds(receivedAgeSeconds));
        Assert.Equal(expected, NeighborhoodDeliveryLocationPolicy.IsRecent(location, "driver-1", Now.AddMinutes(-20), Now));
    }

    [Theory]
    [InlineData("measured")]
    [InlineData("received")]
    [InlineData("other-driver")]
    [InlineData("invalid-coordinate")]
    [InlineData("invalid-accuracy")]
    public void 배정전기록이나다른기사나잘못된측정은_거부한다(string invalid)
    {
        var location = Location(Now.AddSeconds(-30), Now.AddSeconds(-20));
        switch (invalid)
        {
            case "measured": location.기록시각 = Now.AddMinutes(-2); break;
            case "received": location.CreatedAt = Now.AddMinutes(-2); break;
            case "other-driver": location.기사Id = "driver-2"; break;
            case "invalid-coordinate": location.위도 = 999m; break;
            case "invalid-accuracy": location.정확도_m = -1; break;
        }
        Assert.False(NeighborhoodDeliveryLocationPolicy.IsRecent(location, "driver-1", Now.AddMinutes(-1), Now));
    }

    [Theory]
    [InlineData("driver")]
    [InlineData("round")]
    [InlineData("request")]
    [InlineData("corrupt")]
    [InlineData("future")]
    [InlineData("closed")]
    public void 현재기사와차수에맞지않거나손상된배정근거는_과거근거로대체하지않는다(string invalid)
    {
        var (request, queue) = Assignment();
        var evidence = NeighborhoodDeliveryLocationPolicy.CreateAssignmentEvent(request, queue, Now.AddMinutes(-1))!;
        switch (invalid)
        {
            case "driver": queue.확정기사Id = "driver-2"; break;
            case "round": ++queue.추천라운드; break;
            case "request": evidence.의뢰Id = "other-request"; break;
            case "corrupt": evidence.메타데이터 = "{broken"; break;
            case "future": evidence = NeighborhoodDeliveryLocationPolicy.CreateAssignmentEvent(request, queue, Now.AddSeconds(1))!; break;
            case "closed": queue.상태 = 상태값.배차상태.하차완료; break;
        }
        Assert.Null(NeighborhoodDeliveryLocationPolicy.CurrentAssignmentAt(request, queue, evidence, Now));
    }

    [Fact]
    public async Task 최신배정이손상되면_같은기사의과거근거로돌아가지않는다()
    {
        await using var db = Context();
        var (request, queue) = Assignment();
        db.화주운송의뢰.Add(request); db.운송원장.Add(queue);
        db.운송이벤트.Add(NeighborhoodDeliveryLocationPolicy.CreateAssignmentEvent(request, queue, Now.AddMinutes(-2))!);
        await db.SaveChangesAsync();
        db.운송이벤트.Add(new() { 의뢰Id = request.의뢰Id, 이벤트타입 = NeighborhoodDeliveryLocationPolicy.AssignmentEventType, 메타데이터 = "{broken", 이벤트시각 = Now.AddMinutes(-1) });
        db.기사위치기록.Add(Location(Now.AddSeconds(-30), Now.AddSeconds(-20)));
        await db.SaveChangesAsync();
        var result = await NeighborhoodDeliveryLocationPolicy.ReadAsync(db, request, queue, Now);
        Assert.Equal(NeighborhoodDeliveryMapStates.AssignmentEvidenceMissing, result.StateCode);
        Assert.Null(result.Location);
    }

    [Fact]
    public async Task 재배정근거뒤의좌표만표시하고_미래측정값이정상값을덮지않는다()
    {
        await using var db = Context();
        var (request, queue) = Assignment();
        db.화주운송의뢰.Add(request); db.운송원장.Add(queue);
        db.운송이벤트.Add(NeighborhoodDeliveryLocationPolicy.CreateAssignmentEvent(request, queue, Now.AddMinutes(-2))!);
        var old = Location(Now.AddMinutes(-3), Now.AddMinutes(-3)); old.위도 = 36m;
        var current = Location(Now.AddSeconds(-30), Now.AddSeconds(-20));
        var future = Location(Now.AddHours(1), Now.AddSeconds(-10)); future.위도 = 38m;
        db.기사위치기록.AddRange(old, current, future);
        await db.SaveChangesAsync();
        var result = await NeighborhoodDeliveryLocationPolicy.ReadAsync(db, request, queue, Now);
        Assert.Equal(37.5m, result.Location!.위도);
    }

    [Fact]
    public void 기존화물에는생활배송배정이벤트를추가하지않는다()
    {
        var (request, queue) = Assignment();
        request.클라이언트요청Id = "legacy:1";
        Assert.Null(NeighborhoodDeliveryLocationPolicy.CreateAssignmentEvent(request, queue, Now));
    }

    private static (화주운송의뢰, 운송원장) Assignment()
        => (new()
        {
            의뢰Id = "delivery-1", 주문자UserId = "owner-1", 화주Id = "owner-1",
            클라이언트요청Id = NeighborhoodDeliveryRoutes.ClientRequestPrefix + "fixture", 상태 = 상태값.의뢰상태.생성됨,
            배차상태 = 상태값.배차상태.배차확정
        }, new()
        {
            의뢰Id = "delivery-1", 확정기사Id = "driver-1", 추천라운드 = 3, 배차업무유형 = 상태값.배차업무유형.용달운송,
            배차큐단계 = 상태값.배차큐단계.확정, 상태 = 상태값.배차상태.운송중
        });
    private static 기사위치기록 Location(DateTime measured, DateTime received)
        => new() { 기사Id = "driver-1", 위도 = 37.5m, 경도 = 127m, 기록시각 = measured, CreatedAt = received, UpdatedAt = received };
    private static SsalddelContext Context() => new(new DbContextOptionsBuilder<SsalddelContext>()
        .UseInMemoryDatabase("neighborhood-gps-" + Guid.NewGuid()).Options, new Encryption());
    private sealed class Encryption : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
}
