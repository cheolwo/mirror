using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Mart;
using Ssalddel.Services.OperationsAlerts;
using Ssalddel.Tests.Application.Mart;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.도메인.마트;
using 살뜰.도메인.창고;

namespace Ssalddel.Tests.Services.OperationsAlerts;

public sealed class WarehouseOperationsAlertR76Tests
{
    [Fact]
    public async Task 공간철회잔여재고는_일반공간과구분해관찰하고_원장종료뒤에만알림을해소한다()
    {
        await using var db = new SsalddelContext(new DbContextOptionsBuilder<SsalddelContext>()
            .UseInMemoryDatabase("site-alert-r76-" + Guid.NewGuid()).Options, new Encryption());
        db.AddRange(new 분산거점Record { SiteId = "a", StatusCode = "Active", PrivateDetailsJson = "private-address" },
            new 분산거점Record { SiteId = "b", StatusCode = "RevocationPendingCustody", OwnerUserId = "private-owner" });
        await db.SaveChangesAsync(); var source = new DistributedSiteOperationsAlertSource(db);
        var first = Assert.Single(await source.ReadChangesAsync(DateTime.UnixEpoch, "", 1, default));
        Assert.Equal("a", first.SourceId); Assert.False(first.Active);
        var second = Assert.Single(await source.ReadChangesAsync(first.ChangedAtUtc, first.SourceId, 1, default));
        Assert.Equal("b", second.SourceId); Assert.True(second.Active);
        Assert.DoesNotContain("private", System.Text.Json.JsonSerializer.Serialize(second));
        (await db.Set<분산거점Record>().SingleAsync(x => x.SiteId == "b")).StatusCode = "Revoked";
        await db.SaveChangesAsync(); Assert.False(Assert.Single(await source.ReadCurrentAsync("b", default)).Active);
    }

    [Fact]
    public async Task 마트실제전달뒤문제보류를알리고_원천연결유실을해결로오판하지않는다()
    {
        await using var f = await MartReceiptTestFixture.CreateAsync();
        await f.CompleteWarehouseAsync(); await f.DeliverAsync(701);
        var input = await f.ConfirmationRequestAsync(701);
        Assert.True((await f.CustomerUseCase().문제신고Async(701, new 마트수령문제신고요청
        { ClientRequestId = Guid.NewGuid(), ExpectedAttemptStableId = input.ExpectedAttemptStableId, ExpectedRevision = input.ExpectedRevision, ReasonCode = "Missing" })).IsSuccess);
        var attempt = await f.Db.Set<마트배송시도>().SingleAsync();
        var source = new MartReceiptOperationsAlertSource(f.Db, f.Clock);
        var held = Assert.Single(await source.ReadCurrentAsync(attempt.시도StableId, default));
        Assert.True(held.Active); Assert.Equal("MartReceiptHold", held.ProblemCode);
        Assert.DoesNotContain(MartReceiptTestFixture.Buyer, System.Text.Json.JsonSerializer.Serialize(held));
        (await f.Db.마트주문.SingleAsync(x => x.Id == 701)).주문참조번호 = "changed"; await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => source.ReadCurrentAsync(attempt.시도StableId, default));
    }
    private sealed class Encryption : IPersonalDataEncryptionService
    { public string? Protect(string? value) => value; public string? Unprotect(string? value) => value; }
}
