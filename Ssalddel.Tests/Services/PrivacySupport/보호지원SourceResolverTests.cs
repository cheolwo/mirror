using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Ssalddel.Application.Warehouse;
using Ssalddel.Contracts.Common.Warehouse;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.PrivacySupport;
using Ssalddel.Contracts.Food;
using Ssalddel.Services.Community;
using Ssalddel.Services.PrivacySupport;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.도메인.음식;
using 살뜰.도메인.화주;
using 살뜰.도메인.창고;
using 살뜰.도메인.운송;
using 살뜰.Services.Options;

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

    [Fact]
    public async Task 분산입고문제는단일Lot당사자만포함하며같은입고계획의다른소유자와거점을노출하지않는다()
    {
        await using var db = Context(); await SeedDistributed(db);
        var resolver = DistributedResolver(db);
        var source = await resolver.조회Async(보호지원출처Codes.분산입고, "lot-a", "goods-a");
        Assert.Equal(new[] { "building-a", "goods-a", "manager-a", "seller-a" }, source!.PartyUserIds.Order(StringComparer.Ordinal));
        Assert.NotNull(await resolver.조회Async(보호지원출처Codes.분산입고, "lot-a", "manager-a"));
        Assert.Null(await resolver.조회Async(보호지원출처Codes.분산입고, "lot-a", "goods-b"));
        Assert.Null(await resolver.조회Async(보호지원출처Codes.분산입고, "lot-a", "manager-b"));
        Assert.Null(await resolver.조회Async(보호지원출처Codes.분산입고, "plan", "seller-a"));
    }

    [Fact]
    public async Task 분산배송문제는단일주문고객과관련재고거점기사만포함하며같은순회의다른고객을차단한다()
    {
        await using var db = Context(); await SeedDistributed(db);
        var resolver = DistributedResolver(db);
        var source = await resolver.조회Async(보호지원출처Codes.분산배송, "order-a", "customer-a");
        Assert.Equal(new[] { "building-a", "customer-a", "driver", "goods-a", "manager-a", "operator", "seller-a" }, source!.PartyUserIds.Order(StringComparer.Ordinal));
        Assert.NotNull(await resolver.조회Async(보호지원출처Codes.분산배송, "order-a", "driver"));
        Assert.Null(await resolver.조회Async(보호지원출처Codes.분산배송, "order-a", "customer-b"));
        Assert.Null(await resolver.조회Async(보호지원출처Codes.분산배송, "order-a", "seller-b"));
        Assert.Null(await resolver.조회Async(보호지원출처Codes.분산배송, "wave", "operator"));
        Assert.Null(await resolver.조회Async(보호지원출처Codes.분산입고, "order-a", "customer-a"));
        var transport = await db.운송원장.SingleAsync(x => x.의뢰Id == "cargo-a");
        transport.확정기사Id = "different-driver"; await db.SaveChangesAsync();
        Assert.Null(await resolver.조회Async(보호지원출처Codes.분산배송, "order-a", "driver"));
        Assert.Null(await resolver.조회Async(보호지원출처Codes.분산배송, "order-a", "different-driver"));
    }

    [Theory]
    [InlineData(false, "distributed-fulfillment-r36", SsalddelExecutionMode.Simulation)]
    [InlineData(true, "other-profile", SsalddelExecutionMode.Simulation)]
    [InlineData(true, "distributed-fulfillment-r36", SsalddelExecutionMode.Operational)]
    public async Task 분산문제접수는정확한활성Simulation설정없이원장권한을확대하지않는다(bool enabled, string profile, SsalddelExecutionMode mode)
    {
        await using var db = Context(); await SeedDistributed(db);
        var resolver = DistributedResolver(db, enabled, profile, mode);
        Assert.Null(await resolver.조회Async(보호지원출처Codes.분산입고, "lot-a", "goods-a"));
        Assert.Null(await resolver.조회Async(보호지원출처Codes.분산배송, "order-a", "customer-a"));
        Assert.Null(await new 보호지원SourceResolver(db, new Collaboration()).조회Async(보호지원출처Codes.분산입고, "lot-a", "goods-a"));
    }

    private static 보호지원SourceResolver DistributedResolver(SsalddelContext db, bool enabled = true,
        string profile = DistributedFulfillmentCodes.Profile, SsalddelExecutionMode mode = SsalddelExecutionMode.Simulation)
        => new(db, new Collaboration(), Options.Create(new DistributedFulfillmentOptions { Enabled = enabled, Profile = profile }),
            new SsalddelExecutionModePolicy(Options.Create(new SsalddelExecutionOptions { Mode = mode })));
    private static async Task SeedDistributed(SsalddelContext db)
    {
        db.AddRange(new 분산거점Record { SiteId = "site-a", WarehouseId = 1, OwnerUserId = "building-a", ManagerUserId = "manager-a" },
            new 분산거점Record { SiteId = "site-b", WarehouseId = 2, OwnerUserId = "building-b", ManagerUserId = "manager-b" },
            new 분산입고계획Record { PlanId = "plan", CreatorUserId = "seller-a" },
            new 분산재고LotRecord { LotId = "lot-a", PlanId = "plan", SiteId = "site-a", LocationId = "location-a", OwnerUserId = "goods-a", SellerUserId = "seller-a" },
            new 분산재고LotRecord { LotId = "lot-b", PlanId = "plan", SiteId = "site-b", LocationId = "location-b", OwnerUserId = "goods-b", SellerUserId = "seller-b" },
            new 분산배송WaveRecord { WaveId = "wave", CreatorUserId = "operator", DriverId = "driver" },
            new 분산배송주문Record { OrderId = "order-a", WaveId = "wave", CustomerUserId = "customer-a", SellerUserId = "seller-a", CargoRequestId = "cargo-a" },
            new 분산배송주문Record { OrderId = "order-b", WaveId = "wave", CustomerUserId = "customer-b", SellerUserId = "seller-b", CargoRequestId = "cargo-b" },
            new 분산재고예약Record { ReservationId = "reservation-a", WaveId = "wave", OrderId = "order-a", LotId = "lot-a" },
            new 분산재고예약Record { ReservationId = "reservation-b", WaveId = "wave", OrderId = "order-b", LotId = "lot-b" },
            new 운송원장 { Id = 1, 의뢰Id = "cargo-a", 확정기사Id = "driver", 기사_운송자 = "driver" },
            new 운송원장 { Id = 2, 의뢰Id = "cargo-b", 확정기사Id = "driver", 기사_운송자 = "driver" });
        await db.SaveChangesAsync();
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
