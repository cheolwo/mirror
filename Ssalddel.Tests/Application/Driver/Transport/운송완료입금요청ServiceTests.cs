using Microsoft.EntityFrameworkCore;
using Ssalddel.Application.Driver.Transport;
using Ssalddel.Contracts.Shipper.Request;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.도메인.운송;
using 살뜰.도메인.화주;

namespace Ssalddel.Tests.Application.Driver.Transport;

public sealed class 운송완료입금요청ServiceTests
{
    [Fact]
    public async Task 열린비정상운송사건이있으면_완료뒤입금요청을만들지않는다()
    {
        await using var db = CreateContext();
        db.화주운송의뢰.Add(new 화주운송의뢰
        {
            의뢰Id = "request-81",
            화주Id = "shipper-81",
            정산시점 = 정산시점.운송완료후정산.ToString(),
            정산상태 = 운임정산상태.비정상운송검토보류.ToString(),
            결제예정금액 = 65000
        });
        db.비정상운송사건.Add(new 비정상운송사건
        {
            사건StableId = "abnormal-transport:81:CargoDamage",
            운송Id = 81,
            운송의뢰Id = "request-81",
            사건유형Code = 비정상운송사건유형Codes.화물훼손,
            원본예외Code = "화물훼손",
            단계Code = "운행중",
            상태Code = 비정상운송사건상태Codes.운영검토대기,
            최초신고시각Utc = DateTime.UtcNow,
            최근신고시각Utc = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        var service = new 운송완료입금요청Service(db);

        var result = await service.준비Async(new 운송인수완료됨Event(
            81,
            "request-81",
            "driver-81",
            "상차지",
            "하차지",
            "인수완료",
            DateTime.UtcNow,
            "trace-81",
            null));

        Assert.False(result.처리됨);
        Assert.Contains("비정상 운송", result.사유, StringComparison.Ordinal);
        Assert.Empty(db.결제);
        Assert.Equal(
            운임정산상태.비정상운송검토보류.ToString(),
            (await db.화주운송의뢰.SingleAsync()).정산상태);
    }

    private static SsalddelContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<SsalddelContext>()
            .UseInMemoryDatabase($"transport-payment-hold-{Guid.NewGuid():N}")
            .Options;
        return new SsalddelContext(options, new DummyPersonalDataEncryptionService());
    }

    private sealed class DummyPersonalDataEncryptionService : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
}
