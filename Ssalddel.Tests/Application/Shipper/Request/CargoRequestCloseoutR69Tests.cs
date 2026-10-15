using Microsoft.EntityFrameworkCore;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Application.Shipper.Request;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.도메인.운송;
using 살뜰.도메인.화주;

namespace Ssalddel.Tests.Application.Shipper.Request;

public sealed class CargoRequestCloseoutR69Tests
{
    [Theory]
    [InlineData("미시작")]
    [InlineData("배차확정")]
    [InlineData("상차완료")]
    [InlineData("인수완료")]
    [InlineData("취소")]
    public async Task 접수원장은단계에관계없이물리삭제하지않고취소로안내한다(string stage)
    {
        await using var db = Context();
        db.Add(new 화주운송의뢰 { 의뢰Id = "request", 주문자UserId = "owner", 화주Id = "owner", 배차상태 = stage });
        db.Add(new 운송원장 { 의뢰Id = "request", 운송번호 = "request", 상태 = stage });
        await db.SaveChangesAsync();
        var result = await new 의뢰삭제CommandHandler(db, new User("owner", "화주")).Handle(new("request"), default);
        Assert.True(result.IsFailed); Assert.Equal("CargoRequestCancellationRequired", result.Errors.Single().Metadata["ErrorCode"]);
        Assert.Equal(409, result.Errors.Single().Metadata["StatusCode"]);
        Assert.Single(db.화주운송의뢰); Assert.Single(db.운송원장);
        Assert.Equal(stage, (await db.화주운송의뢰.SingleAsync()).배차상태);
    }

    [Fact]
    public async Task 타인삭제요청은의뢰존재나연결정보를노출하지않는다()
    {
        await using var db = Context(); db.Add(new 화주운송의뢰 { 의뢰Id = "request", 주문자UserId = "owner", 화주Id = "owner" });
        await db.SaveChangesAsync();
        var result = await new 의뢰삭제CommandHandler(db, new User("another", "화주")).Handle(new("request"), default);
        Assert.True(result.IsFailed); Assert.Empty(result.Errors.Single().Metadata); Assert.Single(db.화주운송의뢰);
    }

    private static SsalddelContext Context() => new(new DbContextOptionsBuilder<SsalddelContext>()
        .UseInMemoryDatabase("cargo-closeout-r69-" + Guid.NewGuid()).Options, new Encryption());
    private sealed record User(string? UserId, string? Role) : ICurrentUserAccessor;
    private sealed class Encryption : IPersonalDataEncryptionService
    { public string? Protect(string? value) => value; public string? Unprotect(string? value) => value; }
}
