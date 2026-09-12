using Microsoft.EntityFrameworkCore;
using Ssalddel.Application.Admin.Progress;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Contracts.Shipper.Request;
using Ssalddel.Services.Operations;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.도메인.운송;
using 살뜰.도메인.화주;

namespace Ssalddel.Tests.Application.Admin.Progress;

public sealed class 비정상운송사건운영UseCaseTests
{
    [Fact]
    public async Task 운영자는_목록에서사건을조회하고_부분인수결정을할수있다()
    {
        await using var db = CreateContext();
        db.화주운송의뢰.Add(new 화주운송의뢰
        {
            의뢰Id = "request-71",
            화주Id = "shipper-71",
            화물수량 = 10,
            정산상태 = 운임정산상태.입금확인완료.ToString()
        });
        var service = new 비정상운송사건Service(db);
        var reported = await service.접수Async(
            new 비정상운송사건접수요청(
                71,
                "request-71",
                "하차",
                "화물훼손",
                true,
                DateTime.UtcNow,
                8,
                2));
        await db.SaveChangesAsync();
        var useCase = new 비정상운송사건운영UseCase(db, service, new CurrentUserStub());

        var pending = await useCase.목록조회Async(비정상운송사건상태Codes.운영검토대기);
        var item = Assert.Single(pending);
        Assert.Equal(8, item.정상확인수량);
        Assert.Equal(2, item.영향수량);

        var reviewed = await useCase.검토Async(
            reported.사건!.사건StableId,
            new Ssalddel.Contracts.Admin.Progress.비정상운송사건검토요청
            {
                클라이언트요청Id = Guid.NewGuid(),
                예상Revision = item.Revision,
                결정Code = 비정상운송해결결과Codes.정상분인수영향분보류,
                정상확인수량 = 8,
                영향수량 = 2,
                보험적용가능성검토요청 = true,
                검토사유 = "정상 수량을 인수하고 파손 수량만 보험 검토로 보냅니다."
            });

        Assert.NotNull(reviewed);
        Assert.Equal(비정상운송사건상태Codes.조치결정, reviewed!.상태Code);
        Assert.Equal(비정상운송보류범위Codes.영향수량, reviewed.보류범위Code);
        Assert.False(reviewed.정산보류적용여부);
        Assert.Equal("admin-1", (await db.비정상운송사건.SingleAsync()).최근검토자UserId);
    }

    [Fact]
    public void 운영응답은_검토자식별자와_검토사유를공개하지않는다()
    {
        var properties = typeof(Ssalddel.Contracts.Admin.Progress.비정상운송사건운영응답)
            .GetProperties()
            .Select(x => x.Name)
            .ToArray();

        Assert.DoesNotContain("최근검토자UserId", properties);
        Assert.DoesNotContain("최근검토사유", properties);
    }

    private static SsalddelContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<SsalddelContext>()
            .UseInMemoryDatabase($"abnormal-transport-admin-{Guid.NewGuid():N}")
            .Options;
        return new SsalddelContext(options, new DummyPersonalDataEncryptionService());
    }

    private sealed class CurrentUserStub : ICurrentUserAccessor
    {
        public string? UserId => "admin-1";
        public string? Role => "Admin";
    }

    private sealed class DummyPersonalDataEncryptionService : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
}
