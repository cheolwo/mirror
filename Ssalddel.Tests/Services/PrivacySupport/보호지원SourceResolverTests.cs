using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.PrivacySupport;
using Ssalddel.Contracts.Food;
using Ssalddel.Services.Community;
using Ssalddel.Services.PrivacySupport;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.도메인.음식;
using 살뜰.도메인.화주;

namespace Ssalddel.Tests.Services.PrivacySupport;

public sealed class 보호지원SourceResolverTests
{
    [Fact]
    public async Task 음식주문번호를알아도본인주문또는현재배정된음식점범위가아니면조회되지않는다()
    {
        await using var db = Context();
        db.음식주문.Add(new 음식주문 { 주문번호 = "order", 음식점Id = 7, 주문자UserId = "buyer" });
        db.Roles.Add(new IdentityRole { Id = "restaurant-role", Name = "음식점" });
        db.UserRoles.Add(new() { RoleId = "restaurant-role", UserId = "seller" });
        db.UserClaims.Add(new() { UserId = "seller", ClaimType = 음식점접근ClaimTypes.음식점Id, ClaimValue = "7" });
        db.UserClaims.Add(new() { UserId = "unscoped", ClaimType = 음식점접근ClaimTypes.음식점Id, ClaimValue = "8" });
        await db.SaveChangesAsync();
        var resolver = new 보호지원SourceResolver(db, new Collaboration());
        Assert.NotNull(await resolver.조회Async(보호지원출처Codes.음식주문, "order", "buyer"));
        Assert.NotNull(await resolver.조회Async(보호지원출처Codes.음식주문, "order", "seller"));
        Assert.Null(await resolver.조회Async(보호지원출처Codes.음식주문, "order", "unscoped"));
        Assert.Null(await resolver.조회Async(보호지원출처Codes.음식주문, "guessed", "buyer"));
        db.UserClaims.Remove(await db.UserClaims.SingleAsync(x => x.UserId == "seller"));
        await db.SaveChangesAsync();
        Assert.Null(await resolver.조회Async(보호지원출처Codes.음식주문, "order", "seller"));
    }

    [Fact]
    public async Task 생활배송과일반화물의출처를바꿔쓰거나다른화주ID를쓸수없다()
    {
        await using var db = Context();
        db.화주운송의뢰.Add(new 화주운송의뢰 { 의뢰Id = "neighborhood", 주문자UserId = "owner", 클라이언트요청Id = NeighborhoodDeliveryRoutes.ClientRequestPrefix + "verified" });
        db.화주운송의뢰.Add(new 화주운송의뢰 { 의뢰Id = "cargo", 주문자UserId = "owner", 클라이언트요청Id = "cargo-original" });
        await db.SaveChangesAsync();
        var resolver = new 보호지원SourceResolver(db, new Collaboration());
        Assert.NotNull(await resolver.조회Async(보호지원출처Codes.생활배송, "neighborhood", "owner"));
        Assert.Null(await resolver.조회Async(보호지원출처Codes.화물의뢰, "neighborhood", "owner"));
        Assert.Null(await resolver.조회Async(보호지원출처Codes.생활배송, "cargo", "owner"));
        Assert.Null(await resolver.조회Async(보호지원출처Codes.화물의뢰, "cargo", "other-user"));
        Assert.NotNull(await resolver.조회Async(보호지원출처Codes.화물의뢰, "cargo", "owner"));
    }

    [Fact]
    public async Task 생활협업은기존서버연결조회가확인한양당사자만사용한다()
    {
        await using var db = Context();
        var resolver = new 보호지원SourceResolver(db, new Collaboration());
        var source = await resolver.조회Async(보호지원출처Codes.생활협업, "work", "owner");
        Assert.Equal(new[] { "owner", "requester" }, source!.PartyUserIds);
        Assert.Null(await resolver.조회Async(보호지원출처Codes.생활협업, "work", "outsider"));
        Assert.Null(await resolver.조회Async("unsupported", "work", "owner"));
    }

    private static SsalddelContext Context() => new(new DbContextOptionsBuilder<SsalddelContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options, new Encryption());
    private sealed class Encryption : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
    private sealed class Collaboration : I생활협업연결Query
    {
        public Task<생활협업연결Context?> 조회Async(string stableId, CancellationToken ct = default)
            => Task.FromResult<생활협업연결Context?>(stableId == "work" ? new("work", 1, 1, "owner", "requester", "goods", "accepted", true, new(), null, 1) : null);
    }
}
