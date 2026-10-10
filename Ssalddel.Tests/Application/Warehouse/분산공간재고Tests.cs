using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Application.Warehouse;
using Ssalddel.Contracts.Common.Warehouse;
using 살뜰.Services.Options;
using 살뜰.도메인.창고;
using static Ssalddel.Tests.Application.Warehouse.DistributedTestFixture;

namespace Ssalddel.Tests.Application.Warehouse;

/// <summary>r51의 한 공간 읽기 계약을 기존 격리 SQLite 업무 명령과 대조합니다. 실운영·실물 검증이 아닙니다.</summary>
public sealed class 분산공간재고Tests
{
    [Fact]
    public async Task 입고예정은실재고가아니며사진확인한부분수령만수량에반영된다()
    {
        await using var f = await Create();
        var site = (await f.Sites())[0];
        var plan = await Plan(f, site);
        var before = Assert.Single((await Read(f, Operator, site.SiteId)).Items);
        Assert.Equal(5, before.ExpectedQuantity);
        Assert.Equal(5, before.RemainingExpectedQuantity);
        Assert.Equal(0, before.ReceivedQuantity);
        Assert.Equal(0, before.AvailableQuantity);
        Assert.Equal(0, before.ReservedQuantity);
        Assert.Equal(0, before.DefectiveQuantity);

        await f.EnsureAgreement(site, new("goods-owner-a"), "same-sku");
        var command = await f.Proof(Operator, plan.AggregateId, new DistributedInboundReceiptRequest
        {
            RequestId = Guid.NewGuid(), ExpectedRevision = plan.Revision,
            LineKey = "first", LocationBarcode = site.Locations.Single().Barcode,
            Sku = "same-sku", Quantity = 2, EvidenceReference = "synthetic-actual-count"
        });
        await f.Use(Operator).ReceiveAsync(Operator, plan.AggregateId, command, default);
        await f.Use(Operator).ReceiveAsync(Operator, plan.AggregateId, command, default);
        var after = Assert.Single((await Read(f, Operator, site.SiteId)).Items);
        Assert.Equal(before.LotId, after.LotId);
        Assert.Equal(plan.AggregateId, after.PlanId);
        Assert.True(after.Revision > before.Revision);
        Assert.Equal(5, after.ExpectedQuantity);
        Assert.Equal(2, after.ReceivedQuantity);
        Assert.Equal(3, after.RemainingExpectedQuantity);
        Assert.Equal(2, after.AvailableQuantity);
        Assert.Equal(0, after.ReservedQuantity);
    }

    [Fact]
    public async Task 실수령예약불량은원천필드대로분리하고조회가추가예약을만들지않는다()
    {
        await using var f = await Create();
        var sites = await f.Sites();
        var lots = await f.Stock(sites);
        var selected = lots.Single(x => x.SiteId == sites[0].SiteId && x.SellerUserId == SellerA.UserId);
        var wave = await f.Use(Operator).CreateWaveAsync(Operator, new()
        {
            RequestId = Guid.NewGuid(), Label = "합성 재고 확인",
            CutoffUtc = f.Clock.Now.AddMinutes(1), PickupStartUtc = f.Clock.Now.AddMinutes(2),
            PickupEndUtc = f.Clock.Now.AddHours(2), DeliveryStartUtc = f.Clock.Now.AddMinutes(2),
            DeliveryEndUtc = f.Clock.Now.AddHours(3)
        }, default);
        await f.Cargo("inventory-read-only-cargo", SellerA, CustomerA, 2);
        await f.Use(SellerA).OrderAsync(SellerA, wave.AggregateId, new()
        {
            RequestId = Guid.NewGuid(), ExpectedRevision = wave.Revision,
            CargoRequestId = "inventory-read-only-cargo", OrderReference = "합성 주문",
            Lines = [new() { LotId = selected.LotId, Quantity = 2 }]
        }, default);
        // 기존 원천에 이미 기록된 불량을 조회만 합니다. 새 검수 명령이나 불량 판정을 만들지 않습니다.
        await f.Db.입고상품.Where(x => x.Id == selected.InboundItemId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.가용수량, 2).SetProperty(x => x.불량수량, 1));
        var count = await f.Db.Set<분산재고예약Record>().CountAsync();
        var row = (await Read(f, Operator, sites[0].SiteId)).Items.Single(x => x.LotId == selected.LotId);
        Assert.Equal(5, row.ReceivedQuantity);
        Assert.Equal(0, row.RemainingExpectedQuantity);
        Assert.Equal(2, row.AvailableQuantity);
        Assert.Equal(2, row.ReservedQuantity);
        Assert.Equal(1, row.DefectiveQuantity);
        Assert.Equal(count, await f.Db.Set<분산재고예약Record>().CountAsync());
    }

    [Fact]
    public async Task 물품소유자와대리판매자는본인Lot만보고관리위치이름은보지못한다()
    {
        await using var f = await Create();
        var sites = await f.Sites();
        var lots = await f.Stock(sites);
        var site = sites[0];
        var mine = lots.Single(x => x.SiteId == site.SiteId && x.SellerUserId == SellerA.UserId);
        foreach (var actor in new[] { SellerA, new DistributedActor("goods-owner-a") })
        {
            var page = await f.Use(actor).공간재고Async(actor, site.SiteId, "shipper", 0, 50, default);
            var row = Assert.Single(page.Items);
            Assert.Equal(mine.LotId, row.LotId);
            Assert.Equal("선택 가능한 적재 위치", row.LocationLabel);
            Assert.Equal("shipper", page.RoleKey);
        }
        var other = Assert.Single((await f.Use(SellerB).공간재고Async(SellerB, site.SiteId, "shipper", 0, 50, default)).Items);
        Assert.NotEqual(mine.LotId, other.LotId);
        Assert.Equal(2, (await Read(f, Operator, site.SiteId)).Items.Count);
    }

    [Fact]
    public async Task 고유식별자순서로페이지를나누고빈공간과마지막페이지를구별한다()
    {
        await using var f = await Create();
        var sites = await f.Sites();
        var empty = await Read(f, Operator, sites[0].SiteId);
        Assert.Empty(empty.Items);
        Assert.False(empty.HasMore);
        await f.Stock(sites);
        var first = await f.Use(Operator).공간재고Async(Operator, sites[0].SiteId, "warehouse", 0, 1, default);
        var second = await f.Use(Operator).공간재고Async(Operator, sites[0].SiteId, "warehouse", 1, 1, default);
        var end = await f.Use(Operator).공간재고Async(Operator, sites[0].SiteId, "warehouse", 2, 1, default);
        Assert.True(first.HasMore);
        Assert.False(second.HasMore);
        Assert.Empty(end.Items);
        Assert.False(end.HasMore);
        Assert.True(StringComparer.Ordinal.Compare(Assert.Single(first.Items).LotId, Assert.Single(second.Items).LotId) < 0);
        Assert.Equal(1, first.Take);
        Assert.Equal(1, second.Skip);
        Assert.True(first.IsSimulation);
        Assert.Equal(DistributedFulfillmentCodes.Profile, first.Profile);
    }

    [Fact]
    public async Task 담당자와기존관리자는조회하고무관계정과없는공간은같은404를받는다()
    {
        await using var f = await Create();
        var site = (await f.Sites())[0];
        var manager = new DistributedActor("synthetic-site-manager");
        await f.Db.Set<분산거점Record>().Where(x => x.SiteId == site.SiteId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.ManagerUserId, manager.UserId));
        Assert.Equal(site.SiteId, (await Read(f, manager, site.SiteId)).Site.SiteId);
        var admin = new DistributedActor("synthetic-existing-admin", IsAdmin: true);
        Assert.Equal(site.SiteId, (await Read(f, admin, site.SiteId)).Site.SiteId);
        foreach (var actor in new[] { SellerA, DistributedTestFixture.Driver, new DistributedActor("unrelated") })
        {
            await Error("DistributedSiteNotFound", 404, () => Read(f, actor, site.SiteId));
            await Error("DistributedSiteNotFound", 404, () => Read(f, actor, "site-missing"));
        }
        await Error("DistributedSiteNotFound", 404,
            () => f.Use(SellerA).공간재고Async(SellerA, site.SiteId, "shipper", 0, 50, default));
    }

    [Theory]
    [InlineData("driver", 0, 50, "DistributedRoleScopeInvalid")]
    [InlineData("operator", 0, 50, "DistributedRoleScopeInvalid")]
    [InlineData("warehouse", -1, 50, "DistributedInventoryPageInvalid")]
    [InlineData("warehouse", 0, 0, "DistributedInventoryPageInvalid")]
    [InlineData("warehouse", 0, 101, "DistributedInventoryPageInvalid")]
    public async Task 역할과페이지경계오류는구조화된400으로거부한다(string role, int skip, int take, string code)
    {
        await using var f = await Create();
        await Error(code, 400, () => f.Use(Operator).공간재고Async(Operator, "site-any", role, skip, take, default));
    }

    [Fact]
    public async Task 철회된공간의잔존재고를숨기지않고원장상태를바꾸지않는다()
    {
        await using var f = await Create();
        var sites = await f.Sites();
        await f.Stock(sites);
        var site = (await f.Use(Operator).WorkspaceAsync(Operator, "warehouse", default)).Sites.Single(x => x.SiteId == sites[0].SiteId);
        await f.Use(Operator).ConsentAsync(Operator, site.SiteId, new()
        {
            RequestId = Guid.NewGuid(), ExpectedRevision = site.Revision,
            ConsentKind = "Owner", Granted = false, EvidenceReference = "synthetic-revocation"
        }, default);
        f.Db.ChangeTracker.Clear();
        var before = await Snapshot(f);
        var page = await Read(f, Operator, site.SiteId);
        Assert.Equal("RevocationPendingCustody", page.Site.StatusCode);
        Assert.Equal(10, page.Items.Sum(x => x.AvailableQuantity));
        await f.Use(SellerA).공간재고Async(SellerA, site.SiteId, "shipper", 0, 50, default);
        Assert.Equal(before, await Snapshot(f));
        Assert.Empty(f.Publisher.Events);
    }

    [Fact]
    public async Task 재고응답에는주소출입바코드좌표당사자ID와운송묶음이없다()
    {
        await using var f = await Create();
        var sites = await f.Sites();
        await f.Stock(sites);
        var page = await Read(f, Operator, sites[0].SiteId);
        // 분산 입고는 입력 행의 임의 ProductName 대신 원 입고요청의 상품명을 사용합니다.
        Assert.All(page.Items, item => Assert.Contains(item.ProductLabel, new[] { "A 기업 물품", "B 기업 물품" }));
        var json = JsonSerializer.Serialize(page, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        foreach (var key in new[] { "address", "latitude", "longitude", "barcode", "pickupInstructions", "ownerUserId", "sellerUserId", "managerUserId", "waveId", "cargoRequestId", "photoEvidenceIds" })
            Assert.DoesNotContain("\"" + key + "\"", json, StringComparison.OrdinalIgnoreCase);
        foreach (var privateValue in new[] { "합성 출입 안내", "합성 공개도로 가", "goods-owner-a", SellerA.UserId, SellerB.UserId })
            Assert.DoesNotContain(privateValue, json, StringComparison.Ordinal);
        Assert.DoesNotContain(sites[0].Locations.Single().Barcode, json, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("부산", 35.18, 129.07)]
    [InlineData("대전", 36.35, 127.38)]
    public async Task 서울지역후보없이다른지역공간을등록하고빈재고를조회한다(string region, double latitude, double longitude)
    {
        await using var f = await Create();
        var actor = new DistributedActor("synthetic-space-owner");
        var created = await f.Use(actor).SiteAsync(actor, new()
        {
            RequestId = Guid.NewGuid(), PublicLabel = region + " 합성 생활 공간",
            Floor = "지상", Subspace = "합성 구역", PickupInstructions = "합성 현장 확인 안내",
            SpaceConfirmed = true, PickupPointConfirmed = true,
            NewWarehouse = new()
            {
                Name = region + " 합성 보관 공간", ConfirmedRoadAddress = region + " 합성도로 1",
                Latitude = (decimal)latitude, Longitude = (decimal)longitude,
                AddressConfirmed = true, CoordinatesConfirmed = true
            }
        }, default);
        var page = await Read(f, actor, created.AggregateId);
        Assert.StartsWith("site-", page.Site.SiteId);
        Assert.Equal(region + " 합성 생활 공간", page.Site.PublicLabel);
        Assert.Equal("PendingConsent", page.Site.StatusCode);
        Assert.Empty(page.Items);
        Assert.Empty(await f.Db.Set<분산배송WaveRecord>().ToArrayAsync());
    }

    [Theory]
    [InlineData(false, SsalddelExecutionMode.Simulation, DistributedFulfillmentCodes.Profile)]
    [InlineData(true, SsalddelExecutionMode.Operational, DistributedFulfillmentCodes.Profile)]
    [InlineData(true, SsalddelExecutionMode.Simulation, "wrong-profile")]
    public async Task 기본비활성과Operational및다른profile에서는조회도닫힌다(bool enabled, SsalddelExecutionMode mode, string profile)
    {
        await using var f = await Create();
        Assert.False(new DistributedFulfillmentOptions().Enabled);
        await Error("DistributedFulfillmentDisabled", 404,
            () => f.Use(Operator, enabled, mode, profile).공간재고Async(Operator, "site-any", "warehouse", 0, 50, default));
    }

    [Theory]
    [InlineData("stock")]
    [InlineData("location")]
    public async Task 선택Lot의원천연결결손을정상0이나빈페이지로숨기지않는다(string source)
    {
        await using var f = await Create();
        var sites = await f.Sites();
        var lots = await f.Stock(sites);
        var selected = lots.First(x => x.SiteId == sites[0].SiteId);
        if (source == "stock")
            await f.Db.입고상품.Where(x => x.Id == selected.InboundItemId).ExecuteDeleteAsync();
        else
            await f.Db.Set<분산재고LotRecord>().Where(x => x.LotId == selected.LotId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.LocationId, sites[1].Locations.Single().LocationId));
        await Error("DistributedInventorySourceUnavailable", 503, () => Read(f, Operator, sites[0].SiteId));
        var unaffected = await Read(f, Operator, sites[1].SiteId);
        Assert.Equal(2, unaffected.Items.Count);
    }

    [Theory]
    [InlineData("owner")]
    [InlineData("seller")]
    [InlineData("sku")]
    [InlineData("warehouse")]
    [InlineData("barcode")]
    public async Task 원천계보가다르면다른물품의수량을정상재고로반환하지않는다(string field)
    {
        await using var f = await Create();
        var sites = await f.Sites();
        var lots = await f.Stock(sites);
        var selected = lots.First(x => x.SiteId == sites[0].SiteId);
        var stock = f.Db.입고상품.Where(x => x.Id == selected.InboundItemId);
        switch (field)
        {
            case "owner": await stock.ExecuteUpdateAsync(s => s.SetProperty(x => x.소유자UserId, "different-owner")); break;
            case "seller": await stock.ExecuteUpdateAsync(s => s.SetProperty(x => x.판매자UserId, "different-seller")); break;
            case "sku": await stock.ExecuteUpdateAsync(s => s.SetProperty(x => x.SKU, "different-sku")); break;
            case "warehouse": await stock.ExecuteUpdateAsync(s => s.SetProperty(x => x.창고Id, sites[1].WarehouseId)); break;
            case "barcode": await stock.ExecuteUpdateAsync(s => s.SetProperty(x => x.보관위치, sites[1].Locations.Single().Barcode)); break;
            default: throw new ArgumentOutOfRangeException(nameof(field));
        }
        await Error("DistributedInventorySourceUnavailable", 503, () => Read(f, Operator, sites[0].SiteId));
        Assert.Equal(2, (await Read(f, Operator, sites[1].SiteId)).Items.Count);
    }

    [Fact]
    public async Task 취소된조회는상태나명령영수증을만들지않는다()
    {
        await using var f = await Create();
        var site = (await f.Sites())[0];
        var before = await Snapshot(f);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            f.Use(Operator).공간재고Async(Operator, site.SiteId, "warehouse", 0, 50, cancellation.Token));
        Assert.Equal(before, await Snapshot(f));
    }

    private static Task<분산공간재고PageDto> Read(DistributedTestFixture f, DistributedActor actor, string siteId)
        => f.Use(actor).공간재고Async(actor, siteId, "warehouse", 0, 50, default);

    private static Task<DistributedCommandResponse> Plan(DistributedTestFixture f, DistributedSiteDto site)
        => f.Use(SellerA).InboundAsync(SellerA, new()
        {
            RequestId = Guid.NewGuid(), SourceReference = "synthetic-inventory-plan",
            Lines = [new() { LineKey = "first", SourceInboundId = 101, SiteId = site.SiteId,
                LocationId = site.Locations.Single().LocationId, Sku = "same-sku", ProductName = "합성 물품",
                LotCode = "synthetic-lot", ExpectedQuantity = 5 }]
        }, default);

    private static async Task Error(string code, int status, Func<Task> action)
    {
        var error = await Assert.ThrowsAsync<DistributedFulfillmentException>(action);
        Assert.Equal(code, error.Code);
        Assert.Equal(status, error.Status);
    }

    private static async Task<string> Snapshot(DistributedTestFixture f)
        => JsonSerializer.Serialize(new
        {
            Sites = await f.Db.Set<분산거점Record>().AsNoTracking().OrderBy(x => x.SiteId).ToArrayAsync(),
            Lots = await f.Db.Set<분산재고LotRecord>().AsNoTracking().OrderBy(x => x.LotId).ToArrayAsync(),
            Stocks = await f.Db.입고상품.AsNoTracking().OrderBy(x => x.Id).ToArrayAsync(),
            Reservations = await f.Db.Set<분산재고예약Record>().AsNoTracking().OrderBy(x => x.ReservationId).ToArrayAsync(),
            Receipts = await f.Db.Set<분산물류CommandReceipt>().AsNoTracking().OrderBy(x => x.ActorUserId).ThenBy(x => x.RequestId).ToArrayAsync(),
            HistoryCount = await f.Db.재고이력.CountAsync()
        });
}
