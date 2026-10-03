using System.Text.Json;
using System.Text.Json.Nodes;
using Ssalddel.Application.Food;
using Ssalddel.Contracts.Common.Finance;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.Services.Weather;
using 살뜰.도메인.음식;

namespace Ssalddel.Tests.Application.Driver.Food;

public sealed class FoodDeliverySettlementPricingEvidenceTests
{
    [Fact]
    public void 실제저장형식의동결구성은원액과할증최소보정을풀어표시하며현재요율로계산하지않는다()
    {
        var item = Settlement();
        var before = item.요금계산근거Json;
        var detail = 음식주문기사정산Recorder.ToDto(item).PricingBreakdown;
        Assert.Equal("VerifiedComponents", detail.EvidenceStatusCode);
        Assert.Equal("StoredAcceptedOffer", detail.EvidenceScopeCode);
        Assert.Equal(3000m, detail.BaseAndDistanceAmount);
        Assert.Equal(1400m, detail.BaseAmount);
        Assert.Equal(700m, detail.PickupAmount);
        Assert.Equal(700m, detail.DropoffAmount);
        Assert.Equal(1500m, detail.DistanceAmount);
        Assert.Equal(100m, detail.MinimumAdjustmentAmount);
        Assert.Equal(700m, detail.TimeSurchargeAmount);
        Assert.Equal(500m, detail.WeatherSurchargeAmount);
        Assert.Equal(300m, detail.DemandSurchargeAmount);
        Assert.Equal(1.5m, detail.DistanceKm);
        Assert.Equal("NaverDirections5CarOnlyAvoidanceEstimate", detail.DistanceBasisCode);
        Assert.Equal("Motorcycle", detail.RouteVehicleCode);
        Assert.Equal("traavoidcaronly", detail.RouteOptionCode);
        Assert.Equal("Morning", detail.TimeBandCode);
        Assert.Equal("stored.policy.r1", detail.PricingPolicyRevision);
        Assert.Equal(new DateTime(2026, 10, 3, 0, 0, 0, DateTimeKind.Utc), detail.FrozenAtUtc);
        Assert.Equal(before, item.요금계산근거Json);
        Assert.Equal(4500m, 음식주문기사정산Recorder.ToDto(item).GrossAmount);
    }

    [Fact]
    public void 기본비배분미정은픽업0원전달기본비로오인하지않고확인된총기본비를보존한다()
    {
        var item = Settlement();
        var json = JsonNode.Parse(item.요금계산근거Json)!.AsObject();
        var pricing = json["요금"]!.AsObject();
        pricing["기본지급구분Code"] = "LegacyUnsplit";
        var components = pricing["기본요금구성"]!.AsObject();
        components["PickupFeeKrw"] = 0m;
        components["DropoffFeeKrw"] = 1400m;
        item.요금계산근거Json = json.ToJsonString();
        var detail = 음식주문기사정산Recorder.ToDto(item).PricingBreakdown;
        Assert.Equal("LegacyUnsplit", detail.EvidenceStatusCode);
        Assert.Equal(1400m, detail.BaseAmount);
        Assert.Null(detail.PickupAmount);
        Assert.Null(detail.DropoffAmount);
        Assert.Equal(1500m, detail.DistanceAmount);
    }

    [Fact]
    public void 저장구분이없으면기본구성의금액을임의로픽업전달비에배분하지않는다()
    {
        var item = Settlement();
        var json = JsonNode.Parse(item.요금계산근거Json)!.AsObject();
        json["요금"]!.AsObject().Remove("기본지급구분Code");
        item.요금계산근거Json = json.ToJsonString();
        var detail = 음식주문기사정산Recorder.ToDto(item).PricingBreakdown;
        Assert.Equal("UnclassifiedBaseSplit", detail.EvidenceStatusCode);
        Assert.Equal(1400m, detail.BaseAmount);
        Assert.Null(detail.PickupAmount);
        Assert.Null(detail.DropoffAmount);
    }

    [Theory]
    [InlineData("Malformed")]
    [InlineData("Array")]
    [InlineData("PolicyMismatch")]
    [InlineData("DriverMismatch")]
    [InlineData("GrossMismatch")]
    [InlineData("CoreSumMismatch")]
    [InlineData("ComponentSumMismatch")]
    [InlineData("DistanceMismatch")]
    [InlineData("NegativeComponent")]
    [InlineData("UnknownTimestampZone")]
    [InlineData("HiddenStorePromotion")]
    [InlineData("Overflow")]
    public void 잘못된근거는금액0이나현재정책으로보완하지않고미확인구성으로반환한다(string scenario)
    {
        var item = Settlement();
        var json = JsonNode.Parse(item.요금계산근거Json)!.AsObject();
        var pricing = json["요금"]!.AsObject();
        var components = pricing["기본요금구성"]!.AsObject();
        switch (scenario)
        {
            case "PolicyMismatch": json["정책판본"] = "changed.policy"; break;
            case "DriverMismatch": json["기사Id"] = "different-driver"; break;
            case "GrossMismatch": pricing["기사지급예정액"] = 4501m; break;
            case "CoreSumMismatch": pricing["시간대할증액"] = 701m; break;
            case "ComponentSumMismatch": components["DistanceFeeKrw"] = 1501m; break;
            case "DistanceMismatch": json["산정거리Km"] = 2m; break;
            case "NegativeComponent": components["PickupFeeKrw"] = -1m; break;
            case "UnknownTimestampZone": json["판정시각Utc"] = "2026-10-03T00:00:00"; break;
            case "HiddenStorePromotion": components["StorePromotionKrw"] = 500m; break;
            case "Overflow": pricing["기본거리지급액"] = decimal.MaxValue; break;
        }
        item.요금계산근거Json = scenario switch { "Malformed" => "{", "Array" => "[]", _ => json.ToJsonString() };
        var result = 음식주문기사정산Recorder.ToDto(item);
        Assert.Equal(4500m, result.GrossAmount);
        Assert.Equal("InvalidEvidence", result.PricingBreakdown.EvidenceStatusCode);
        Assert.Null(result.PricingBreakdown.PickupAmount);
        Assert.Null(result.PricingBreakdown.DropoffAmount);
        Assert.Null(result.PricingBreakdown.DistanceAmount);
        Assert.Null(result.PricingBreakdown.TimeSurchargeAmount);
    }

    [Fact]
    public void 과거총액전용JSON은유효원액을보존하되구성누락을0으로채우지않는다()
    {
        var item = Settlement();
        item.요금계산근거Json = JsonSerializer.Serialize(new
        {
            요금 = new { 기사지급예정액 = 4500m }, 정책판본 = "stored.policy.r1", 기사Id = "driver-1"
        });
        var dto = 음식주문기사정산Recorder.ToDto(item);
        Assert.Equal(4500m, dto.GrossAmount);
        Assert.Equal("MissingComponents", dto.PricingBreakdown.EvidenceStatusCode);
        Assert.Null(dto.PricingBreakdown.BaseAndDistanceAmount);
        Assert.Null(dto.PricingBreakdown.DistanceAmount);
        Assert.Null(dto.PricingBreakdown.TimeSurchargeAmount);
    }

    [Fact]
    public void 분해자료가없어도저장된기본거리총액과명시할증은보존하고세부구성을추정하지않는다()
    {
        var item = Settlement();
        var json = JsonNode.Parse(item.요금계산근거Json)!.AsObject();
        json["요금"]!.AsObject().Remove("기본요금구성");
        item.요금계산근거Json = json.ToJsonString();
        var detail = 음식주문기사정산Recorder.ToDto(item).PricingBreakdown;
        Assert.Equal("MissingComponents", detail.EvidenceStatusCode);
        Assert.Equal(3000m, detail.BaseAndDistanceAmount);
        Assert.Equal(700m, detail.TimeSurchargeAmount);
        Assert.Null(detail.PickupAmount);
        Assert.Null(detail.DropoffAmount);
        Assert.Null(detail.DistanceAmount);
    }

    [Fact]
    public void 근거없는세전대금과원본개인메타데이터는상세표시로승격하거나공개하지않는다()
    {
        var item = Settlement();
        var json = JsonNode.Parse(item.요금계산근거Json)!.AsObject();
        json["internalRecipientAddress"] = "private-synthetic-address";
        json["apiSecret"] = "private-synthetic-secret";
        item.요금계산근거Json = json.ToJsonString();
        var serialized = JsonSerializer.Serialize(음식주문기사정산Recorder.ToDto(item).PricingBreakdown);
        Assert.DoesNotContain("private-synthetic-address", serialized);
        Assert.DoesNotContain("private-synthetic-secret", serialized);
        item.세전대금 = null;
        var missing = 음식주문기사정산Recorder.ToDto(item).PricingBreakdown;
        Assert.Equal("MissingEvidence", missing.EvidenceStatusCode);
        Assert.Null(missing.BaseAndDistanceAmount);
        Assert.Null(missing.TimeSurchargeAmount);
    }

    private static 음식주문기사정산 Settlement()
    {
        var components = new FoodDeliveryPayoutReviewLine("offer-1", new DateOnly(2026, 10, 3), 1.5m,
            "RouteEstimate", "ExistingOperatingPolicy", null, 700m, 700m, 1500m, 0m, 0m, 0m, 100m, 3000m);
        var decision = new 음식배달기사제안요금산정결과(
            new 음식배달기사제안요금판정(3000m, 500m, 300m, 4500m, true, true)
            {
                시간대할증액 = 700m, 기본요금구성 = components, 기본지급구분Code = "PickupDropoffSplit"
            },
            픽업지기상관측결과.사용불가("Synthetic"), "stored.policy.r1",
            new DateTime(2026, 10, 3, 0, 0, 0, DateTimeKind.Utc))
        {
            기사Id = "driver-1", 산정거리Km = 1.5m, 거리근거Code = "NaverDirections5CarOnlyAvoidanceEstimate",
            경로차량Code = "Motorcycle", 경로옵션Code = "traavoidcaronly", 시간대Code = "Morning"
        };
        return new 음식주문기사정산
        {
            기사Id = "driver-1", 세전대금 = 4500m, 요금정책판본 = decision.정책판본,
            요금계산근거Json = JsonSerializer.Serialize(decision)
        };
    }
}
