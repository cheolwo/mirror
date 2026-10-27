using FDriverApp.PageModels;
using FDriverApp.Services;
using DriverApp.Models.Driver;
using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Contracts.Common.Drivers;
using Ssalddel.Contracts.Common.Transport;
using Ssalddel.Contracts.Common.Workflow;
using Ssalddel.Contracts.Driver.Food;
using Ssalddel.Contracts.Driver.Settings;
using Ssalddel.Contracts.Food;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.DriverOffers;

namespace Ssalddel.Tests.Clients;

public sealed class DriverOfferPresentationR72Tests
{
    [Fact]
    public void 음식과화물과피킹은_색외에도서로다른그림으로구별한다()
    {
        var food = DriverOfferPresentation.Resolve(DriverWorkKinds.FoodDelivery, [DriverOfferActivityCodes.Pickup, DriverOfferActivityCodes.Deliver]);
        var cargo = DriverOfferPresentation.Resolve(DriverWorkKinds.CargoTransport, [DriverOfferActivityCodes.Pickup, DriverOfferActivityCodes.Deliver]);
        var picking = DriverOfferPresentation.Resolve(DriverWorkKinds.CargoTransport, [DriverOfferActivityCodes.Pick]);
        Assert.Equal(DriverOfferPresentation.FoodIconPath, food.IconPath);
        Assert.Equal(DriverOfferPresentation.CargoIconPath, cargo.IconPath);
        Assert.Equal(DriverOfferPresentation.PickingIconPath, picking.IconPath);
        Assert.NotEqual(food.IconPath, cargo.IconPath);
        Assert.NotEqual(cargo.IconPath, picking.IconPath);
        Assert.DoesNotContain("포장", picking.ActivityText);
    }

    [Fact]
    public void 알수없는업무는그림으로종류를단정하지않는다()
    {
        var unknown = DriverOfferPresentation.Resolve("future-kind", [DriverOfferActivityCodes.Deliver]);
        Assert.Equal(DriverOfferPresentation.WorkIconPath, unknown.IconPath);
        Assert.Equal(DriverOfferPresentation.WarehouseColor, unknown.PrimaryColor);
        Assert.Contains("확인 필요", unknown.Label);
    }

    [Theory]
    [InlineData(DriverWorkKinds.FoodDelivery, "#2563EB", "음식 배달")]
    [InlineData(DriverWorkKinds.QuickDelivery, "#DC2626", "소형 물품·퀵")]
    [InlineData(DriverWorkKinds.CargoTransport, "#DC2626", "화물 운송")]
    [InlineData(운송실행상세유형코드.창고출고화물운송, "#DC2626", "화물 운송")]
    public void 기존명시업무종류는_작업목록없어도단색이며_복합을추정하지않는다(string kind, string color, string label)
    {
        var value = DriverOfferPresentation.Resolve(kind, []);
        Assert.Equal(color, value.PrimaryColor); Assert.Equal(label, value.Label); Assert.False(value.IsCombined);
        Assert.Equal("작업 범위 확인 필요", value.ActivityText);
    }

    [Theory]
    [InlineData(DriverOfferActivityCodes.Pick)]
    [InlineData(DriverOfferActivityCodes.Pack)]
    [InlineData(DriverOfferActivityCodes.OutboundHandover)]
    public void 실제창고작업과배송이함께할당된경우만_파랑빨강을같이표시한다(string activity)
    {
        var value = DriverOfferPresentation.Resolve(DriverWorkKinds.QuickDelivery, [activity, DriverOfferActivityCodes.Deliver]);
        Assert.True(value.IsCombined); Assert.Equal("#2563EB", value.PrimaryColor); Assert.Equal("#DC2626", value.SecondaryColor);
        Assert.Contains("배송", value.ActivityText); Assert.Contains("복합 작업", value.Label);
    }

    [Fact]
    public void 단순픽업배송과창고선행필요는_복합작업으로승격하지않는다()
    {
        var offer = Offer() with
        {
            ExecutionProfile = new() { 실행유형코드 = 운송실행유형코드.음식배달, 창고선행작업필요 = true },
            AssignedActivities = [DriverOfferActivityCodes.Pickup, DriverOfferActivityCodes.Deliver]
        };
        var value = DriverOfferPresentation.Resolve(offer);
        Assert.False(value.IsCombined); Assert.Equal("#2563EB", value.PrimaryColor); Assert.Equal("픽업 → 배송", value.ActivityText);
        Assert.DoesNotContain("피킹", value.ActivityText); Assert.DoesNotContain("포장", value.ActivityText);
    }

    [Fact]
    public void 창고작업만할당되면_중립회색으로표시한다()
    {
        var value = DriverOfferPresentation.Resolve(DriverWorkKinds.CargoTransport, [DriverOfferActivityCodes.Pick, DriverOfferActivityCodes.Pack]);
        Assert.Equal("#64748B", value.PrimaryColor); Assert.False(value.IsCombined); Assert.Equal("창고 작업", value.Label);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("future-kind")]
    public void 미확인업무종류는_앱역할이나활동으로음식화물을추정하지않는다(string? kind)
    {
        var value = DriverOfferPresentation.Resolve(kind, [DriverOfferActivityCodes.Pick, DriverOfferActivityCodes.Deliver]);
        Assert.Equal("#64748B", value.PrimaryColor); Assert.False(value.IsCombined); Assert.Contains("확인 필요", value.Label);
    }

    [Fact]
    public void 미확인추가작업은_완전한복합작업처럼표시하지않고_확인을안내한다()
    {
        var value = DriverOfferPresentation.Resolve(DriverWorkKinds.FoodDelivery, ["FutureStep", DriverOfferActivityCodes.Pick, DriverOfferActivityCodes.Deliver]);
        Assert.False(value.IsCombined); Assert.Contains("추가 작업 확인 필요", value.ActivityText);
    }

    [Fact]
    public void 작업은중복없이일관된순서로표시하며_다건수량을추론하지않는다()
    {
        var value = DriverOfferPresentation.Resolve(DriverWorkKinds.CargoTransport,
            [DriverOfferActivityCodes.Deliver, DriverOfferActivityCodes.Pack, DriverOfferActivityCodes.Pick, DriverOfferActivityCodes.Pick]);
        Assert.Equal("피킹 → 포장 → 배송", value.ActivityText);
    }

    [Fact]
    public void 거리비표시는_서버금액을사용하며_주행거리나UI반올림으로재계산하지않는다()
    {
        var price = Price() with { RawDistanceMeters = 2340.5m, DistanceFeeKrw = 2341m, TotalPayoutKrw = 4341m };
        var value = DriverOfferPresentation.Pricing(price);
        Assert.True(value.HasEvidence); Assert.Equal("산정 직선거리 2.3405 km", value.DistanceText);
        Assert.Equal("거리비 2,341원", value.DistanceFeeText); Assert.Equal("거리 단가 1,000원/km", value.RateText);
        Assert.Equal("제안 보수 4,341원", value.TotalText);
    }

    [Fact]
    public void 근거있는0은표시하고_근거없는금액은0으로채우지않는다()
    {
        var unknown = DriverOfferPresentation.Pricing(null);
        Assert.False(unknown.HasEvidence); Assert.Equal("거리비 미정", unknown.DistanceFeeText); Assert.DoesNotContain("0원", unknown.TotalText);
        var zero = DriverOfferPresentation.Pricing(Price() with { RawDistanceMeters = 0, DistanceFeeKrw = 0, TotalPayoutKrw = 2000 });
        Assert.True(zero.HasEvidence); Assert.Equal("산정 직선거리 0 km", zero.DistanceText); Assert.Equal("거리비 0원", zero.DistanceFeeText);
    }

    [Theory]
    [InlineData("revision")]
    [InlineData("basis")]
    [InlineData("distance")]
    [InlineData("fee")]
    [InlineData("total")]
    public void 잘못되거나누락된지급근거는_확정거리비로표시하지않는다(string invalid)
    {
        var price = invalid switch
        {
            "revision" => Price() with { PolicyRevision = "" },
            "basis" => Price() with { DistanceBasisCode = "Unknown" },
            "distance" => Price() with { RawDistanceMeters = -1 },
            "fee" => Price() with { DistanceFeeKrw = -1 },
            _ => Price() with { TotalPayoutKrw = -1 }
        };
        var value = DriverOfferPresentation.Pricing(price);
        Assert.False(value.HasEvidence); Assert.Equal("거리비 미정", value.DistanceFeeText);
    }

    [Fact]
    public void 확인되지않은기본작업할증은_미정으로남기고거리비와구분한다()
    {
        var value = DriverOfferPresentation.Pricing(Price() with { BaseFeeKrw = null, WorkFeeKrw = null, SurchargeKrw = null });
        Assert.Contains("기본비 미정", value.ComponentsText); Assert.Contains("작업비 미정", value.ComponentsText); Assert.Contains("할증 미정", value.ComponentsText);
        Assert.Equal("거리비 2,340원", value.DistanceFeeText);
    }

    [Fact]
    public void 화물표시모델은_원래주행거리를보존하고_거리비근거를별도로표시한다()
    {
        var request = new DriverRequestItem { WorkKind = DriverWorkKinds.QuickDelivery, AssignedActivities = [DriverOfferActivityCodes.Pick, DriverOfferActivityCodes.Deliver],
            Pricing = Price(), 주행거리Km = 7.8m, 직선거리Km = 0.8m };
        Assert.Equal(7.8m, request.주행거리Km); Assert.Equal(0.8m, request.직선거리Km);
        Assert.Equal("#DC2626", request.OfferVisual.SecondaryColor);
        Assert.Equal("산정 직선거리 2.34 km", request.OfferPricing.DistanceText);
    }

    [Fact]
    public void 음식제안projection도_주행거리와실제작업_요금근거를독립적으로보존한다()
    {
        var source = FoodOffer("food", DriverWorkKinds.FoodDelivery); source.DistanceKm = 7.8m;
        source.AssignedActivities = [DriverOfferActivityCodes.Pick, DriverOfferActivityCodes.Deliver]; source.Pricing = Price();
        var preview = DeliveryTicketPreview.From(source);
        Assert.Equal(7.8, preview.DistanceKm); Assert.Equal("산정 직선거리 2.34 km", preview.PricingDistanceText); Assert.True(preview.HasCombinedWork);
        var common = preview.ToDriverWorkOffer(new());
        Assert.Equal(source.AssignedActivities, common.AssignedActivities); Assert.Same(source.Pricing, common.Pricing); Assert.Equal(7.8, common.DistanceKm);
    }

    [Fact]
    public async Task 최신배너는_여러추천의색을합치지않고_최신한건만표시한다()
    {
        var offers = new[] { FoodOffer("food", DriverWorkKinds.FoodDelivery), FoodOffer("goods", DriverWorkKinds.QuickDelivery) };
        var api = new FDriverTestWorkspaceApi
        {
            Workspace = _ => Task.FromResult(new FoodDeliveryDriverWorkspaceDto { DriverId = "test-driver", Recommendations = offers, MaxActiveDeliveries = 3 }),
            Availability = _ => Task.FromResult(new 운영배차수신상태Dto { 수신의사Code = 운영배차수신의사Code.On })
        };
        var session = new FDriverTestSession();
        var model = FDriverLifecycleTestSupport.Model(session, api);
        try
        {
            await model.InitializeAsync();
            Assert.Contains("2건", model.NewRecommendationNotice); Assert.Equal("#DC2626", model.NewRecommendationPrimaryColor);
            Assert.False(model.NewRecommendationHasCombinedWork); Assert.Contains("소형 물품·퀵", model.NewRecommendationWorkLabel);
            await session.ClearAsync();
            Assert.False(model.HasNewRecommendations); Assert.Equal("#64748B", model.NewRecommendationPrimaryColor);
        }
        finally { await model.StopMonitoringAsync(); }
    }

    internal static DriverOfferPricingDto Price() => new()
    {
        DistanceBasisCode = "StraightLine", RawDistanceMeters = 2340m, RatePerKmKrw = 1000m,
        IncludedDistanceMeters = 0m, RoundingCode = "ProportionalAwayFromZero", DistanceFeeKrw = 2340m,
        BaseFeeKrw = 2000m, WorkFeeKrw = 0m, SurchargeKrw = 0m, TotalPayoutKrw = 4340m, PolicyRevision = "straight-line.r72"
    };
    private static DriverWorkOfferDto Offer() => new("offer-a", "food", "food", DriverWorkKinds.FoodDelivery, "배달 요청", "",
        new("픽업지", "픽업 장소", 37, 127), new("전달지", "전달 장소", 37, 127), 4340, 7.8, "배달 요청");
    private static FoodDeliveryDriverOfferDto FoodOffer(string id, string kind) => new()
    {
        OfferId = id, WorkKind = kind, RestaurantName = "픽업 장소", ExpiresAtUtc = DateTime.UtcNow.AddMinutes(2),
        AssignedActivities = [DriverOfferActivityCodes.Pickup, DriverOfferActivityCodes.Deliver],
        ExecutionProfile = new() { 실행유형코드 = 운송실행유형코드.음식배달 },
        AvailableActions = [new 업무가능행동Dto { ActionId = 음식배달가능행동Ids.기사제안수락 }]
    };
}
