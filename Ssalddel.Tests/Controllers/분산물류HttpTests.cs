using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.FileProviders;
using SkiaSharp;
using Ssalddel.Services.Storage;
using 살뜰.Services.Options;
using Ssalddel.Application.Warehouse;
using Ssalddel.Contracts.Common.Warehouse;
using Ssalddel.Controllers.Common;
using Ssalddel.Tests.Application.Warehouse;
using 살뜰.Services.Versioning;
using 살뜰.도메인.창고;

namespace Ssalddel.Tests.Controllers;

// 실제 Kestrel 인증/권한/JSON 경계와 격리 SQLite 업무 서비스를 소비한다.
// 인증 헤더·계정·경로는 명시적인 시험 자료다. 운영 로그인·외부 DB·실운행의 증거가 아니다.
public sealed class 분산물류HttpTests
{
    private const string Base = "/" + DistributedFulfillmentCodes.BaseRoute;

    [Fact]
    public async Task 공간재고_HTTP는_인증과관계를검사하고_페이지와역할별최소정보만반환한다()
    {
        await using var host = await Host.Create();
        var sites = await host.Data.Sites(); await host.Data.Stock(sites);
        var endpoint = Base + "/sites/" + sites[0].SiteId + "/inventory";
        using var anonymous = await host.Client.GetAsync(endpoint);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        host.Actor(DistributedTestFixture.Operator);
        using var response = await host.Client.GetAsync(endpoint + "?take=1");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); NoStore(response);
        var first = (await response.Content.ReadFromJsonAsync<분산공간재고PageDto>())!;
        Assert.Equal(sites[0].SiteId, first.Site.SiteId); Assert.True(first.HasMore);
        var item = Assert.Single(first.Items); Assert.Equal(5, item.ReceivedQuantity);
        Assert.Equal(0, item.RemainingExpectedQuantity); Assert.Equal(5, item.AvailableQuantity);
        using var nextResponse = await host.Client.GetAsync(endpoint + "?skip=1&take=1");
        var next = (await nextResponse.Content.ReadFromJsonAsync<분산공간재고PageDto>())!;
        Assert.False(next.HasMore); Assert.NotEqual(item.LotId, Assert.Single(next.Items).LotId);

        host.Actor(DistributedTestFixture.SellerA);
        using var sellerResponse = await host.Client.GetAsync(endpoint + "?roleKey=shipper");
        Assert.Equal(HttpStatusCode.OK, sellerResponse.StatusCode); NoStore(sellerResponse);
        var seller = (await sellerResponse.Content.ReadFromJsonAsync<분산공간재고PageDto>())!;
        Assert.Equal(50, seller.Take); Assert.Equal("선택 가능한 적재 위치", Assert.Single(seller.Items).LocationLabel);
        var json = await sellerResponse.Content.ReadAsStringAsync();
        foreach (var privateField in new[] { "barcode", "address", "latitude", "longitude", "pickupInstructions", "ownerUserId", "sellerUserId", "evidence", "sourceReference" })
            Assert.DoesNotContain(privateField, json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(sites[0].Locations[0].Barcode, json); Assert.DoesNotContain("합성 출입 안내", json);

        host.Actor(DistributedTestFixture.CustomerB);
        using var forbidden = await host.Client.GetAsync(endpoint);
        Assert.Equal(HttpStatusCode.NotFound, forbidden.StatusCode); Assert.Equal("DistributedSiteNotFound", await Code(forbidden));
        using var tampered = await host.Client.GetAsync(endpoint + "?roleKey=shipper");
        Assert.Equal(HttpStatusCode.NotFound, tampered.StatusCode);
        host.Actor(DistributedTestFixture.Operator);
        using var invalid = await host.Client.GetAsync(endpoint + "?take=101");
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode); Assert.Equal("DistributedInventoryPageInvalid", await Code(invalid));
        using var driverScope = await host.Client.GetAsync(endpoint + "?roleKey=driver");
        Assert.Equal(HttpStatusCode.BadRequest, driverScope.StatusCode);
    }

    [Fact]
    public async Task 공간재고_HTTP도_기능비활성상태에서는_등록자료를공개하지않는다()
    {
        await using var host = await Host.Create(enabled: false); host.Actor(DistributedTestFixture.Operator);
        using var response = await host.Client.GetAsync(Base + "/sites/unavailable/inventory");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("DistributedFulfillmentDisabled", await Code(response));
    }

    [Fact]
    public async Task 비공개사진_HTTP저장만으로입고되지않고_정확한완료명령만수량과사진을함께반영한다()
    {
        await using var host = await Host.Create(actualPhotos: true);
        var site = (await host.Data.Sites())[0]; await host.Data.EnsureAgreement(site, new("goods-owner-a"), "same-sku");
        var plan = await host.Data.Use(DistributedTestFixture.SellerA).InboundAsync(DistributedTestFixture.SellerA,
            new() { RequestId = Guid.NewGuid(), SourceReference = "photo-http-fixture", Lines = [new() { LineKey = "http-line", SourceInboundId = 101,
                SiteId = site.SiteId, LocationId = site.Locations[0].LocationId, Sku = "same-sku", ProductName = "물품", LotCode = "http-lot", ExpectedQuantity = 4 }] }, default);
        var work = new DistributedInboundReceiptBatchRequest { RequestId = Guid.NewGuid(), ExpectedRevision = plan.Revision,
            LocationBarcode = site.Locations[0].Barcode, Lines = [new() { LineKey = "http-line", Sku = "same-sku", Quantity = 2 }] };
        using var bitmap = new SKBitmap(16, 16); bitmap.Erase(SKColors.SeaGreen);
        using var image = SKImage.FromBitmap(bitmap); using var data = image.Encode(SKEncodedImageFormat.Jpeg, 80);
        var upload = new DistributedPhotoUploadRequest { UploadRequestId = Guid.NewGuid(), ContentType = "image/jpeg", ImageBase64 = Convert.ToBase64String(data.ToArray()),
            Work = new() { Kind = "Receive", AggregateId = plan.AggregateId, Receive = work } };
        using var anonymous = await host.Client.PostAsJsonAsync(Base + "/evidence", upload); Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        host.Actor(DistributedTestFixture.Operator);
        using var response = await host.Client.PostAsJsonAsync(Base + "/evidence", upload);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); NoStore(response);
        var photo = (await response.Content.ReadFromJsonAsync<DistributedPhotoEvidenceDto>())!; Assert.Equal("Uploaded", photo.StatusCode);
        var rawJson = await response.Content.ReadAsStringAsync(); Assert.DoesNotContain("distributed-proof/", rawJson); Assert.DoesNotContain("url", rawJson, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, (await host.Data.Db.Set<분산재고LotRecord>().AsNoTracking().SingleAsync()).ReceivedQuantity);
        using var content = await host.Client.GetAsync(Base + "/evidence/" + photo.EvidenceId + "/content");
        Assert.Equal(HttpStatusCode.OK, content.StatusCode); Assert.Equal("image/jpeg", content.Content.Headers.ContentType!.MediaType); NoStore(content);
        host.Actor(DistributedTestFixture.CustomerB);
        using var forbidden = await host.Client.GetAsync(Base + "/evidence/" + photo.EvidenceId + "/content"); Assert.Equal(HttpStatusCode.NotFound, forbidden.StatusCode);
        host.Actor(DistributedTestFixture.Operator); work.PhotoEvidenceIds = [photo.EvidenceId];
        var endpoint = Base + "/inbound-plans/" + plan.AggregateId + "/receipt-batches";
        using var completed = await host.Client.PostAsJsonAsync(endpoint, work); Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
        using var replay = await host.Client.PostAsJsonAsync(endpoint, work); Assert.True((await replay.Content.ReadFromJsonAsync<DistributedCommandResponse>())!.Replayed);
        Assert.Equal(2, (await host.Data.Db.Set<분산재고LotRecord>().AsNoTracking().SingleAsync()).ReceivedQuantity);
        Assert.Equal("Bound", (await host.Data.Db.Set<분산사진증빙Record>().AsNoTracking().SingleAsync()).StatusCode);
    }

    [Fact]
    public async Task 익명_입력과조회는_차단하고_기사역할은_인증역할로_확인한다()
    {
        await using var host = await Host.Create();
        using var get = await host.Client.GetAsync(Base + "/workspace");
        Assert.Equal(HttpStatusCode.Unauthorized, get.StatusCode);
        using var post = await host.Client.PostAsJsonAsync(Base + "/sites", NewSite());
        Assert.Equal(HttpStatusCode.Unauthorized, post.StatusCode);
        host.Actor(DistributedTestFixture.Operator);
        using var driver = await host.Client.GetAsync(Base + "/workspace?roleKey=driver");
        Assert.Equal(HttpStatusCode.Forbidden, driver.StatusCode);
        Assert.Equal("DistributedDriverRoleRequired", await Code(driver));
        using var unknownRole = await host.Client.GetAsync(Base + "/workspace?roleKey=admin");
        Assert.Equal(HttpStatusCode.BadRequest, unknownRole.StatusCode);
        Assert.Empty(await host.Data.Db.Set<분산거점Record>().ToArrayAsync());
    }

    [Fact]
    public async Task HTTP_등록_재실행은_같은영수증이며_변경payload와_다른사람은_거부한다()
    {
        await using var host = await Host.Create(); host.Actor(DistributedTestFixture.Operator);
        var input = NewSite();
        using var createdResponse = await host.Client.PostAsJsonAsync(Base + "/sites", input);
        Assert.Equal(HttpStatusCode.OK, createdResponse.StatusCode); NoStore(createdResponse);
        var created = (await createdResponse.Content.ReadFromJsonAsync<DistributedCommandResponse>())!;
        Assert.Equal(1, created.Revision); Assert.False(created.Replayed);
        using var replayResponse = await host.Client.PostAsJsonAsync(Base + "/sites", input);
        var replay = (await replayResponse.Content.ReadFromJsonAsync<DistributedCommandResponse>())!;
        Assert.True(replay.Replayed); Assert.Equal(created.AggregateId, replay.AggregateId);
        input.PublicLabel = "변경된 별칭";
        using var changed = await host.Client.PostAsJsonAsync(Base + "/sites", input);
        Assert.Equal(HttpStatusCode.Conflict, changed.StatusCode);
        Assert.Equal("DistributedRequestContentConflict", await Code(changed));
        host.Actor(DistributedTestFixture.SellerB);
        using var otherReceipt = await host.Client.GetAsync(Base + "/receipts/" + input.RequestId);
        Assert.Equal(HttpStatusCode.NotFound, otherReceipt.StatusCode);
        using var forbidden = await host.Client.PostAsJsonAsync(Base + "/sites/" + created.AggregateId + "/consents",
            new DistributedConsentRequest { RequestId = Guid.NewGuid(), ExpectedRevision = 1, ConsentKind = "Owner", Granted = true, EvidenceReference = "fixture" });
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        var persisted = Assert.Single(await host.Data.Db.Set<분산거점Record>().AsNoTracking().ToArrayAsync());
        Assert.Equal("동쪽 보관 공간", persisted.PublicLabel); Assert.False(persisted.OwnerConsented);
        Assert.Single(await host.Data.Db.Set<분산물류CommandReceipt>().AsNoTracking().ToArrayAsync());
    }

    [Fact]
    public async Task 최신판본과_양측동의만_위치등록을_열고_다른계정의_거점은_보이지_않는다()
    {
        await using var host = await Host.Create(); host.Actor(DistributedTestFixture.Operator);
        using var createdResponse = await host.Client.PostAsJsonAsync(Base + "/sites", NewSite());
        var created = (await createdResponse.Content.ReadFromJsonAsync<DistributedCommandResponse>())!;
        using var blocked = await host.Client.PostAsJsonAsync(Base + "/sites/" + created.AggregateId + "/locations",
            new DistributedLocationCreateRequest { RequestId = Guid.NewGuid(), ExpectedRevision = 1, LocationLabel = "선반" });
        Assert.Equal("DistributedConsentRequired", await Code(blocked));
        long revision = 1;
        foreach (var kind in new[] { "Owner", "Manager" })
        {
            using var result = await host.Client.PostAsJsonAsync(Base + "/sites/" + created.AggregateId + "/consents",
                new DistributedConsentRequest { RequestId = Guid.NewGuid(), ExpectedRevision = revision, ConsentKind = kind, Granted = true, EvidenceReference = "fixture-explicit-consent" });
            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
            revision = (await result.Content.ReadFromJsonAsync<DistributedCommandResponse>())!.Revision;
        }
        using var stale = await host.Client.PostAsJsonAsync(Base + "/sites/" + created.AggregateId + "/locations",
            new DistributedLocationCreateRequest { RequestId = Guid.NewGuid(), ExpectedRevision = 1, LocationLabel = "선반" });
        Assert.Equal("DistributedRevisionConflict", await Code(stale));
        using var saved = await host.Client.PostAsJsonAsync(Base + "/sites/" + created.AggregateId + "/locations",
            new DistributedLocationCreateRequest { RequestId = Guid.NewGuid(), ExpectedRevision = revision, LocationLabel = "선반" });
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        using var own = await host.Client.GetAsync(Base + "/workspace?roleKey=warehouse"); NoStore(own);
        var workspace = (await own.Content.ReadFromJsonAsync<DistributedWorkspaceDto>())!;
        var site = Assert.Single(workspace.Sites); Assert.Single(site.Locations); Assert.StartsWith("DLOC-", site.Locations[0].Barcode);
        Assert.Equal("지하", site.Floor);
        host.Actor(DistributedTestFixture.SellerB);
        using var other = await host.Client.GetAsync(Base + "/workspace?roleKey=warehouse");
        Assert.Empty((await other.Content.ReadFromJsonAsync<DistributedWorkspaceDto>())!.Sites);
        Assert.Single(await host.Data.Db.Set<분산적재위치Record>().AsNoTracking().ToArrayAsync());
    }

    [Fact]
    public async Task 현재배정기사만_상세정보를_보고_주문자와_만료기사에게는_제공하지_않는다()
    {
        await using var host = await Host.Create(); var round = await host.Data.Round();
        host.Actor(DistributedTestFixture.Driver);
        using var assigned = await host.Client.GetAsync(Base + "/waves/" + round.WaveId + "?roleKey=driver");
        Assert.Equal(HttpStatusCode.OK, assigned.StatusCode); NoStore(assigned);
        var wave = (await assigned.Content.ReadFromJsonAsync<DistributedWaveDto>())!;
        Assert.Contains(wave.Stops, stop => stop.Address is not null); Assert.NotEmpty(wave.Reservations);
        host.Actor(DistributedTestFixture.CustomerA);
        using var customer = await host.Client.GetAsync(Base + "/waves/" + round.WaveId + "?roleKey=orderer");
        var mine = (await customer.Content.ReadFromJsonAsync<DistributedWaveDto>())!;
        Assert.Single(mine.Orders); Assert.All(mine.Orders, order => Assert.True(order.IsCurrentCustomer));
        Assert.Empty(mine.Reservations); Assert.Null(mine.DriverId);
        Assert.All(mine.Stops, stop => { Assert.Null(stop.Address); Assert.Null(stop.Latitude); Assert.Null(stop.Longitude); });
        host.Actor(new("other-driver", true));
        using var unrelated = await host.Client.GetAsync(Base + "/waves/" + round.WaveId + "?roleKey=driver");
        Assert.Equal(HttpStatusCode.NotFound, unrelated.StatusCode);
        host.Data.Clock.Now = round.DeliveryEndUtc.AddSeconds(1); host.Actor(DistributedTestFixture.Driver);
        using var expired = await host.Client.GetAsync(Base + "/workspace?roleKey=driver");
        var redacted = (await expired.Content.ReadFromJsonAsync<DistributedWorkspaceDto>())!;
        Assert.Empty(redacted.Sites); Assert.Empty(redacted.Lots);
        Assert.All(redacted.Waves.SelectMany(value => value.Stops), stop => Assert.Null(stop.Address));
        Assert.Empty(Assert.Single(redacted.Waves).Reservations); Assert.Empty(Assert.Single(redacted.Waves).AllowedActions);
    }

    [Fact]
    public async Task 공개주소후보를_위조하거나_요청번호가_없으면_저장하지_않는다()
    {
        await using var host = await Host.Create(); host.Actor(DistributedTestFixture.Operator);
        var forged = NewSite(); forged.PublicCandidateRef = "osm-map:unverified#osm:way:1"; forged.PlaceId = "osm:way:1";
        using var invalid = await host.Client.PostAsJsonAsync(Base + "/sites", forged);
        Assert.Equal("DistributedBuildingCandidateMismatch", await Code(invalid));
        var missing = NewSite(); missing.RequestId = Guid.Empty;
        using var noRequest = await host.Client.PostAsJsonAsync(Base + "/sites", missing);
        Assert.Equal(HttpStatusCode.BadRequest, noRequest.StatusCode);
        Assert.Equal("DistributedRequestIdRequired", await Code(noRequest));
        using var nullBody = await host.Client.PostAsJsonAsync<DistributedSiteCreateRequest?>(Base + "/sites", null);
        Assert.Equal(HttpStatusCode.BadRequest, nullBody.StatusCode);
        Assert.Empty(await host.Data.Db.Set<분산거점Record>().ToArrayAsync());
    }

    [Fact]
    public async Task 기능을_비활성화하면_인증돼도_조회와_명령을_숨긴다()
    {
        await using var host = await Host.Create(enabled: false); host.Actor(DistributedTestFixture.Operator);
        using var read = await host.Client.GetAsync(Base + "/workspace");
        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode); Assert.Equal("DistributedFulfillmentDisabled", await Code(read));
        using var write = await host.Client.PostAsJsonAsync(Base + "/sites", NewSite());
        Assert.Equal(HttpStatusCode.NotFound, write.StatusCode);
        Assert.Empty(await host.Data.Db.Set<분산물류CommandReceipt>().ToArrayAsync());
    }

    [Fact]
    public async Task 요청번호없는_수락은_기존화물배차도_변경하지_않는다()
    {
        await using var host = await Host.Create(); var round = await host.Data.Round(accept: false);
        var before = await host.Data.Db.운송원장.AsNoTracking().OrderBy(row => row.Id)
            .Select(row => new { row.Id, row.기사_운송자, row.확정기사Id, row.상태 }).ToArrayAsync();
        host.Actor(DistributedTestFixture.Driver);
        using var result = await host.Client.PostAsJsonAsync(Base + "/waves/" + round.WaveId + "/accept",
            new DistributedRoundAcceptRequest { RequestId = Guid.Empty, ExpectedRevision = round.Revision });
        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode); Assert.Equal("DistributedRequestIdRequired", await Code(result));
        var after = await host.Data.Db.운송원장.AsNoTracking().OrderBy(row => row.Id)
            .Select(row => new { row.Id, row.기사_운송자, row.확정기사Id, row.상태 }).ToArrayAsync();
        Assert.Equal(before, after);
        Assert.Equal(round.Revision, (await host.Data.Use(DistributedTestFixture.Operator).WaveAsync(DistributedTestFixture.Operator, round.WaveId, "warehouse", default)).Revision);
    }

    private static DistributedSiteCreateRequest NewSite() => new() { RequestId = Guid.NewGuid(), WarehouseId = 11,
        PlaceId = "warehouse:11", PublicLabel = "동쪽 보관 공간", ManagerUserId = DistributedTestFixture.Operator.UserId,
        Floor = "지하", Subspace = "선반 구역", PickupInstructions = "합성 출입 안내", SpaceConfirmed = true, PickupPointConfirmed = true };
    [Fact]
    public async Task 같은HTTP계약으로_다거점_피킹전달_다음정차와_고객수령확인까지_연결된다()
    {
        await using var host = await Host.Create(); var round = await host.Data.Round(accept: false);
        host.Actor(DistributedTestFixture.Driver);
        using var accepted = await host.Client.PostAsJsonAsync(Base + "/waves/" + round.WaveId + "/accept",
            new DistributedRoundAcceptRequest { RequestId = Guid.NewGuid(), ExpectedRevision = round.Revision,
                AcknowledgedWarningCodes = [살뜰.Services.Dispatch.Recommendation.화물배차수락경고코드.차량주의사항] });
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        Assert.Equal("Accepted", (await accepted.Content.ReadFromJsonAsync<DistributedCommandResponse>())!.StatusCode);
        var transitions = 0;
        while (true)
        {
            using var query = await host.Client.GetAsync(Base + "/workspace?roleKey=driver");
            Assert.Equal(HttpStatusCode.OK, query.StatusCode);
            var workspace = (await query.Content.ReadFromJsonAsync<DistributedWorkspaceDto>())!; var wave = Assert.Single(workspace.Waves);
            if (wave.StatusCode == "Completed") { Assert.Null(wave.CurrentStopId); Assert.Empty(wave.Reservations); break; }
            Assert.True(++transitions < 12, "한 순회의 현재 정차와 수량만 앞으로 진행해야 합니다.");
            var stop = Assert.Single(wave.Stops, value => value.StopId == wave.CurrentStopId);
            host.Data.Clock.Now = stop.ExpectedArrivalUtc; host.Data.RefreshLocation();
            if (stop.Kind == "Pickup")
            {
                var line = wave.Reservations.First(value => value.SiteId == stop.SiteId && stop.OrderIds.Contains(value.OrderId)
                    && value.PickedQuantity + value.ReleasedQuantity < value.Quantity);
                var location = workspace.Sites.Single(value => value.SiteId == line.SiteId).Locations.Single(value => value.LocationId == line.LocationId);
                var pickRequest = await host.Data.Proof(DistributedTestFixture.Driver, wave.WaveId,
                    new DistributedPickRequest { RequestId = Guid.NewGuid(), ExpectedRevision = wave.Revision, StopId = stop.StopId,
                        ReservationId = line.ReservationId, LocationBarcode = location.Barcode, OwnerUserId = line.OwnerUserId,
                        Sku = line.Sku, Quantity = line.Quantity - line.PickedQuantity, EvidenceReference = "fixture-http-scan" });
                using var picked = await host.Client.PostAsJsonAsync(Base + "/waves/" + wave.WaveId + "/picks", pickRequest);
                Assert.Equal(HttpStatusCode.OK, picked.StatusCode);
            }
            else
            {
                var order = wave.Orders.First(value => stop.OrderIds.Contains(value.OrderId) && value.StatusCode != "Delivered");
                var deliveryRequest = await host.Data.Proof(DistributedTestFixture.Driver, wave.WaveId,
                    new DistributedDeliveryRequest { RequestId = Guid.NewGuid(), ExpectedRevision = wave.Revision, StopId = stop.StopId,
                        OrderId = order.OrderId, EvidenceReference = "fixture-http-handover" });
                using var delivered = await host.Client.PostAsJsonAsync(Base + "/waves/" + wave.WaveId + "/deliveries", deliveryRequest);
                Assert.Equal(HttpStatusCode.OK, delivered.StatusCode);
            }
        }
        foreach (var customer in new[] { DistributedTestFixture.CustomerA, DistributedTestFixture.CustomerB })
        {
            host.Actor(customer);
            using var query = await host.Client.GetAsync(Base + "/waves/" + round.WaveId + "?roleKey=orderer");
            var wave = (await query.Content.ReadFromJsonAsync<DistributedWaveDto>())!; var own = Assert.Single(wave.Orders);
            Assert.False(own.CustomerAcknowledged); Assert.Contains("acknowledge", own.AllowedActions);
            using var result = await host.Client.PostAsJsonAsync(Base + "/waves/" + wave.WaveId + "/acknowledgements",
                new DistributedAcknowledgementRequest { RequestId = Guid.NewGuid(), ExpectedRevision = wave.Revision,
                    OrderId = own.OrderId, EvidenceReference = "fixture-http-customer-confirmation" });
            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
            using var after = await host.Client.GetAsync(Base + "/waves/" + round.WaveId + "?roleKey=orderer");
            Assert.True(Assert.Single((await after.Content.ReadFromJsonAsync<DistributedWaveDto>())!.Orders).CustomerAcknowledged);
        }
        host.Data.Db.ChangeTracker.Clear();
        Assert.All(await host.Data.Db.운송원장.AsNoTracking().ToArrayAsync(), value => Assert.Equal("인수완료", value.상태));
        Assert.All(await host.Data.Db.화주운송의뢰.AsNoTracking().ToArrayAsync(), value => Assert.Equal("결제대기", value.결제상태));
        Assert.All(await host.Data.Db.Set<분산재고예약Record>().AsNoTracking().ToArrayAsync(), value => Assert.Equal(value.Quantity, value.DeliveredQuantity));
    }

    private static async Task<string?> Code(HttpResponseMessage response)
    { using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync()); return document.RootElement.GetProperty("errorCode").GetString(); }
    private static void NoStore(HttpResponseMessage response)
    { Assert.NotNull(response.Headers.CacheControl); Assert.True(response.Headers.CacheControl!.NoStore); }

    [Fact]
    public async Task 창고가없는_일반계정도_본인공간을_처음등록하며_직원권한과_동의를_자동생성하지_않는다()
    {
        await using var host = await Host.Create(); var actor = new DistributedActor("new-owner"); host.Actor(actor);
        using var before = await host.Client.GetAsync(Base + "/workspace?roleKey=warehouse");
        var initial = (await before.Content.ReadFromJsonAsync<DistributedWorkspaceDto>())!;
        Assert.Empty(initial.WarehouseCandidates); Assert.Contains("create-site", initial.AllowedActions);
        var input = new DistributedSiteCreateRequest { RequestId = Guid.NewGuid(), PublicLabel = "새 보관 공간", ManagerUserId = actor.UserId,
            Floor = "1층", Subspace = "확인한 구역", PickupInstructions = "합성 피킹 안내", SpaceConfirmed = true, PickupPointConfirmed = true,
            NewWarehouse = new DistributedNewWarehouseRequest { Name = "본인 시험 공간", ConfirmedRoadAddress = "서울특별시 중랑구 합성로 123",
                Latitude = 37.58m, Longitude = 127.08m, AddressConfirmed = true, CoordinatesConfirmed = true } };
        using var result = await host.Client.PostAsJsonAsync(Base + "/sites", input);
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        var created = (await result.Content.ReadFromJsonAsync<DistributedCommandResponse>())!;
        using var replayResponse = await host.Client.PostAsJsonAsync(Base + "/sites", input);
        Assert.True((await replayResponse.Content.ReadFromJsonAsync<DistributedCommandResponse>())!.Replayed);
        host.Data.Db.ChangeTracker.Clear();
        var site = await host.Data.Db.Set<분산거점Record>().AsNoTracking().SingleAsync(value => value.SiteId == created.AggregateId);
        var warehouse = Assert.Single(await host.Data.Db.창고.AsNoTracking().Where(value => value.소유자UserId == actor.UserId).ToArrayAsync());
        Assert.Equal(warehouse.Id, site.WarehouseId); Assert.Equal(actor.UserId, site.ManagerUserId);
        Assert.Equal("warehouse:" + warehouse.Id, site.PlaceId); Assert.False(site.OwnerConsented); Assert.False(site.ManagerConsented);
        Assert.Equal(input.NewWarehouse.ConfirmedRoadAddress, warehouse.주소);
        Assert.Empty(await host.Data.Db.창고사용자.AsNoTracking().Where(value => value.UserId == actor.UserId).ToArrayAsync());
    }

    [Fact]
    public async Task 실제_배차수락_주의사항을_HTTP로_다시읽고_명시확인한_코드로만_수락한다()
    {
        await using var host = await Host.Create(); var wave = await host.Data.Round(accept: false);
        var cargoId = wave.Orders[0].CargoRequestId;
        // 경로·수량은 유지하며 개별 포장 치수가 일부만 확인된 실제 기존 적격성 경고를 유도한다.
        await host.Data.Db.화주운송의뢰.Where(value => value.의뢰Id == cargoId)
            .ExecuteUpdateAsync(set => set.SetProperty(value => value.화물길이Mm, 100));
        host.Actor(DistributedTestFixture.Driver);
        var first = new DistributedRoundAcceptRequest { RequestId = Guid.NewGuid(), ExpectedRevision = wave.Revision };
        using var pendingResponse = await host.Client.PostAsJsonAsync(Base + "/waves/" + wave.WaveId + "/accept", first);
        Assert.Equal(HttpStatusCode.OK, pendingResponse.StatusCode);
        Assert.Equal("AcceptancePending", (await pendingResponse.Content.ReadFromJsonAsync<DistributedCommandResponse>())!.StatusCode);
        using var read = await host.Client.GetAsync(Base + "/waves/" + wave.WaveId + "?roleKey=driver"); NoStore(read);
        var pending = (await read.Content.ReadFromJsonAsync<DistributedWaveDto>())!;
        var warning = Assert.Single(pending.AcceptanceWarnings);
        Assert.Equal(cargoId, warning.CargoRequestId); Assert.Equal("FreightVehicleAdvisory", warning.Code);
        Assert.Contains("치수", warning.Message); Assert.Null(pending.CurrentStopId);
        host.Actor(DistributedTestFixture.CustomerA);
        using var customerRead = await host.Client.GetAsync(Base + "/waves/" + wave.WaveId + "?roleKey=orderer");
        Assert.Empty((await customerRead.Content.ReadFromJsonAsync<DistributedWaveDto>())!.AcceptanceWarnings);
        host.Actor(DistributedTestFixture.Driver);
        using var acceptedResponse = await host.Client.PostAsJsonAsync(Base + "/waves/" + wave.WaveId + "/accept",
            new DistributedRoundAcceptRequest { RequestId = Guid.NewGuid(), ExpectedRevision = pending.Revision, AcknowledgedWarningCodes = [warning.Code] });
        Assert.Equal(HttpStatusCode.OK, acceptedResponse.StatusCode);
        Assert.Equal("Accepted", (await acceptedResponse.Content.ReadFromJsonAsync<DistributedCommandResponse>())!.StatusCode);
        host.Data.Db.ChangeTracker.Clear();
        Assert.All(await host.Data.Db.운송원장.AsNoTracking().ToArrayAsync(), value => Assert.Equal(DistributedTestFixture.Driver.UserId, value.확정기사Id));
    }

    private sealed class Host(DistributedTestFixture data, WebApplication app, HttpClient client) : IAsyncDisposable
    {
        public DistributedTestFixture Data { get; } = data;
        public HttpClient Client { get; } = client;
        public void Actor(DistributedActor actor)
        {
            Client.DefaultRequestHeaders.Remove("X-Fixture-Actor"); Client.DefaultRequestHeaders.Remove("X-Fixture-Role");
            Client.DefaultRequestHeaders.Add("X-Fixture-Actor", actor.UserId);
            if (actor.IsDriver) Client.DefaultRequestHeaders.Add("X-Fixture-Role", "driver");
            if (actor.IsAdmin) Client.DefaultRequestHeaders.Add("X-Fixture-Role", "admin");
        }
        public static async Task<Host> Create(bool enabled = true, bool actualPhotos = false)
        {
            var data = await DistributedTestFixture.Create();
            var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions { EnvironmentName = "Testing",
                ApplicationName = typeof(DistributedFulfillmentController).Assembly.GetName().Name, ContentRootPath = AppContext.BaseDirectory });
            builder.WebHost.UseKestrel(options => options.Listen(IPAddress.Loopback, 0));
            builder.Services.AddLogging(); builder.Services.AddRouting(); builder.Services.AddHttpContextAccessor();
            var proof = new DistributedPhotoEvidenceService(data.Db, new DevelopmentLocalStorageService(new PhotoEnvironment(), new HttpContextAccessor()),
                new SsalddelExecutionModePolicy(Options.Create(new SsalddelExecutionOptions { Mode = SsalddelExecutionMode.Simulation })),
                Options.Create(new DistributedFulfillmentOptions { Enabled = enabled, Profile = DistributedFulfillmentCodes.Profile }), data.Clock);
            builder.Services.AddSingleton(proof);
            builder.Services.AddSingleton<IVersionFeatureFlagService, Flags>();
            builder.Services.AddControllers().ConfigureApplicationPartManager(manager =>
            { manager.ApplicationParts.Clear(); manager.FeatureProviders.Clear(); manager.FeatureProviders.Add(new OnlyController()); }).AddControllersAsServices();
            builder.Services.AddScoped<IDistributedFulfillmentUseCase>(services =>
            {
                var user = services.GetRequiredService<IHttpContextAccessor>().HttpContext!.User;
                return data.Use(new(user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "", user.IsInRole("기사"), user.IsInRole("서버관리자")), enabled,
                    binder: actualPhotos ? proof : null);
            });
            builder.Services.AddAuthentication("Fixture").AddScheme<AuthenticationSchemeOptions, FixtureAuthentication>("Fixture", _ => { });
            builder.Services.AddAuthorization(); var app = builder.Build();
            app.UseRouting(); app.UseAuthentication(); app.UseAuthorization(); app.MapControllers();
            try
            {
                using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20)); await app.StartAsync(deadline.Token);
                var addresses = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!;
                var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseProxy = false })
                { BaseAddress = new Uri(Assert.Single(addresses.Addresses)), Timeout = TimeSpan.FromSeconds(20) };
                return new(data, app, client);
            }
            catch { await app.DisposeAsync(); await data.DisposeAsync(); throw; }
        }
        public async ValueTask DisposeAsync()
        {
            Client.Dispose(); using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await app.StopAsync(deadline.Token); await app.DisposeAsync(); await Data.DisposeAsync();
        }
    }
    private sealed class OnlyController : IApplicationFeatureProvider<ControllerFeature>
    { public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature)
        { feature.Controllers.Add(typeof(DistributedFulfillmentController).GetTypeInfo()); feature.Controllers.Add(typeof(DistributedPhotoEvidenceController).GetTypeInfo()); } }
    private sealed class PhotoEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "Ssalddel.Tests";
        public string ContentRootPath { get; set; } = Path.GetFullPath(Path.Combine("artifacts", "local", "distributed-proof-r37-20261008", "root", "http-photo", Guid.NewGuid().ToString("N")));
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
    private sealed class Flags : IVersionFeatureFlagService
    { public bool IsEnabled(string key) => true; public IReadOnlyDictionary<string, bool> GetAll() => new Dictionary<string, bool>(); }
    public sealed class FixtureAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var actor = Request.Headers["X-Fixture-Actor"].ToString();
            if (string.IsNullOrWhiteSpace(actor)) return Task.FromResult(AuthenticateResult.NoResult());
            var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, actor) };
            foreach (var role in Request.Headers["X-Fixture-Role"].SelectMany(value => value!.Split(',')))
                claims.Add(new(ClaimTypes.Role, role switch { "driver" => "기사", "admin" => "서버관리자", _ => "fixture-unprivileged" }));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name)), Scheme.Name)));
        }
        protected override Task HandleChallengeAsync(AuthenticationProperties properties) { Response.StatusCode = 401; return Task.CompletedTask; }
    }
}
