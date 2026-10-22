using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Ssalddel.Application.Driver.Transport;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Notifications;
using 살뜰.도메인.운송;
using 살뜰.도메인.화주;

namespace Ssalddel.Tests.Application.Driver.Transport;

public sealed class 운송상태화주알림EventHandlerTests
{
    [Fact]
    public async Task Push수신자는_업무화주Id보다_인증주문자UserId를우선한다()
    {
        await using var db = CreateContext();
        db.화주운송의뢰.Add(new 화주운송의뢰
        {
            의뢰Id = "request-1",
            주문자UserId = "login-user-1",
            화주Id = "shipper-business-1",
            화물종류 = "식자재",
            픽업_도로명주소 = "서울시 강남구",
            하차_도로명주소 = "서울시 송파구"
        });
        db.운송원장.Add(new 운송원장
        {
            Id = 11,
            운송번호 = "request-1",
            의뢰Id = "request-1",
            확정기사Id = "driver-1",
            기사_운송자 = "driver-1",
            상태 = "상차지도착"
        });
        await db.SaveChangesAsync();

        var handler = new 운송상태화주알림EventHandler(
            db,
            NullLogger<운송상태화주알림EventHandler>.Instance);

        await handler.Handle(
            new 운송상차지도착됨Event(
                "driver-1",
                11,
                "배차확정",
                "상차지도착",
                DateTime.UtcNow,
                "trace-1"),
            CancellationToken.None);

        var outbox = Assert.Single(await db.Command알림Outbox.ToListAsync());
        var payload = Command알림Payload.Parse(outbox.PayloadJson);
        Assert.Equal("login-user-1", payload.TargetUserId);
        Assert.Equal("request-1", payload.RequestId);
    }

    private static SsalddelContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<SsalddelContext>()
            .UseInMemoryDatabase($"transport-shipper-push-{Guid.NewGuid():N}")
            .Options;
        return new SsalddelContext(options, new DummyPersonalDataEncryptionService());
    }

    private sealed class DummyPersonalDataEncryptionService : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
}
