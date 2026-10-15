using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Shipper.Request;
using Ssalddel.Services.Operations;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.도메인.운송;
using 살뜰.도메인.화주;

namespace Ssalddel.Tests.Services.Operations;

public sealed class CargoIncidentSettlementR76Tests
{
    [Theory]
    [InlineData("입금대기", false)]
    [InlineData("입금대기", true)]
    [InlineData("청구대기", false)]
    [InlineData("청구대기", true)]
    [InlineData("현장수금예정", false)]
    [InlineData("현장수금예정", true)]
    [InlineData("입금확인완료", false)]
    [InlineData("입금확인완료", true)]
    public async Task 복수사건_종료순서와무관하게_보류직전금융사실만복구한다(string original, bool reverse)
    {
        await using var db = Context();
        var work = new 화주운송의뢰 { 의뢰Id = "r76", 화주Id = "shipper", 정산상태 = original };
        db.Add(work); await db.SaveChangesAsync();
        var service = new 비정상운송사건Service(db);
        var at = DateTime.UtcNow.AddMinutes(-2);
        var a = (await service.접수Async(new(76, "r76", "하차", "수량불일치", true, at))).사건!;
        await db.SaveChangesAsync();
        var b = (await service.접수Async(new(76, "r76", "하차", "화물훼손", true, at.AddSeconds(1)))).사건!;
        await db.SaveChangesAsync();
        Assert.Equal(original, a.보류전정산상태Code); Assert.Equal(original, b.보류전정산상태Code);
        foreach (var row in reverse ? new[] { b, a } : new[] { a, b })
        {
            await service.검토Async(new(row.사건StableId, Guid.NewGuid(), row.Revision,
                비정상운송해결결과Codes.사건종료, null, null, false, "확인 근거에 따른 종료", "admin"));
            await db.SaveChangesAsync();
            if (row == (reverse ? b : a)) Assert.Equal("비정상운송검토보류", work.정산상태);
        }
        db.ChangeTracker.Clear();
        Assert.Equal(original, (await db.화주운송의뢰.SingleAsync()).정산상태);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 과거빈원상태는_동일보류근거로만복구하고_없으면입금확정하지않는다(bool hasEvidence)
    {
        await using var db = Context();
        var at = DateTime.UtcNow.AddMinutes(-2);
        db.Add(new 화주운송의뢰 { 의뢰Id = "r76", 화주Id = "shipper", 정산상태 = "비정상운송검토보류" });
        var a = new 비정상운송사건 { 사건StableId = "a", 운송의뢰Id = "r76", 상태Code = 비정상운송사건상태Codes.종료,
            보류전정산상태Code = hasEvidence ? "입금대기" : "", 최근신고시각Utc = at, UpdatedAt = DateTime.UtcNow };
        var b = new 비정상운송사건 { 사건StableId = "b", 운송의뢰Id = "r76", 상태Code = 비정상운송사건상태Codes.운영검토대기,
            정산보류적용여부 = true };
        b.최근신고시각Utc = at.AddSeconds(1); b.Revision = 1;
        db.AddRange(a, b); await db.SaveChangesAsync();
        await new 비정상운송사건Service(db).검토Async(new("b", Guid.NewGuid(), 1,
            비정상운송해결결과Codes.사건종료, null, null, false, "과거 기록 대조", "admin"));
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        Assert.Equal(hasEvidence ? "입금대기" : "비정상운송검토보류", (await db.화주운송의뢰.SingleAsync()).정산상태);
        var alerts = await new Ssalddel.Services.OperationsAlerts.CargoOperationsAlertSource(db, TimeProvider.System).ReadCurrentAsync("b", default);
        Assert.Equal(!hasEvidence, alerts.Single(x => x.ProblemCode == "CollectionEvidenceReviewRequired").Active);
    }

    [Fact]
    public async Task 종료된옛사건을재신고해도_이전입금근거를새미수납보류로가져오지않는다()
    {
        await using var db = Context();
        var work = new 화주운송의뢰 { 의뢰Id = "r76", 화주Id = "shipper", 정산상태 = "입금확인완료" };
        db.Add(work); await db.SaveChangesAsync(); var service = new 비정상운송사건Service(db);
        var at = DateTime.UtcNow.AddMinutes(-10);
        var a = (await service.접수Async(new(76, "r76", "하차", "수량불일치", true, at))).사건!;
        await db.SaveChangesAsync();
        await service.검토Async(new(a.사건StableId, Guid.NewGuid(), a.Revision, 비정상운송해결결과Codes.사건종료, null, null, false, "옛 보류 종료", "admin"));
        await db.SaveChangesAsync();
        work.정산상태 = "입금대기"; await db.SaveChangesAsync();
        var b = (await service.접수Async(new(76, "r76", "하차", "화물훼손", true, at.AddMinutes(5)))).사건!;
        await db.SaveChangesAsync();
        await service.접수Async(new(76, "r76", "하차", "수량불일치", true, at.AddMinutes(6))); await db.SaveChangesAsync();
        Assert.Equal("입금대기", a.보류전정산상태Code);
        foreach (var row in new[] { b, a })
        {
            await service.검토Async(new(row.사건StableId, Guid.NewGuid(), row.Revision, 비정상운송해결결과Codes.사건종료, null, null, false, "현재 보류 종료", "admin"));
            await db.SaveChangesAsync();
        }
        Assert.Equal("입금대기", work.정산상태);
    }

    private static SsalddelContext Context() => new(new DbContextOptionsBuilder<SsalddelContext>()
        .UseInMemoryDatabase("incident-r76-" + Guid.NewGuid()).Options, new Encryption());
    private sealed class Encryption : IPersonalDataEncryptionService
    { public string? Protect(string? value) => value; public string? Unprotect(string? value) => value; }
}
