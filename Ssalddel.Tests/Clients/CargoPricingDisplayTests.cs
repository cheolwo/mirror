using System.Text.Json;
using DriverApp.Models.Driver;
using DriverApp.Models.Driver.Samples;
using DriverApp.Services;
using Ssalddel.Contracts.Driver.Transport;

namespace Ssalddel.Tests.Clients;

public sealed class CargoPricingDisplayTests
{
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData(false, "{}", "운임 미확인", "거리 미확인")]
    [InlineData(true, "{}", "운임 미확인", "거리 미확인")]
    [InlineData(false, "{\"운임\":null,\"예상거리Km\":null}", "운임 미확인", "거리 미확인")]
    [InlineData(true, "{\"운임\":null,\"예상거리Km\":null}", "운임 미확인", "거리 미확인")]
    [InlineData(false, "{\"운임\":0,\"예상거리Km\":0}", "0원", "예상 0.0km")]
    [InlineData(true, "{\"운임\":0,\"예상거리Km\":0}", "0원", "예상 0.0km")]
    [InlineData(false, "{\"운임\":0,\"예상거리Km\":null}", "0원", "거리 미확인")]
    [InlineData(true, "{\"운임\":0,\"예상거리Km\":null}", "0원", "거리 미확인")]
    [InlineData(false, "{\"운임\":null,\"예상거리Km\":0}", "운임 미확인", "예상 0.0km")]
    [InlineData(true, "{\"운임\":null,\"예상거리Km\":0}", "운임 미확인", "예상 0.0km")]
    [InlineData(false, "{\"운임\":6500,\"예상거리Km\":12.5,\"거리계산방식\":\"synthetic-quote-basis\"}", "6,500원", "예상 12.5km")]
    [InlineData(true, "{\"운임\":6500,\"예상거리Km\":12.5,\"거리계산방식\":\"synthetic-quote-basis\"}", "6,500원", "예상 12.5km")]
    public void 기존_JSON의_누락_null_확인된0과양수는_상세와요약에서_구별하고_예상거리로표시한다(
        bool detail, string json, string expectedFare, string expectedDistance)
    {
        기사운송요약응답 source = detail
            ? JsonSerializer.Deserialize<기사운송상세응답>(json, WebJson)!
            : JsonSerializer.Deserialize<기사운송요약응답>(json, WebJson)!;
        source.Id = 42;
        source.운송번호 = "synthetic-priced-request";
        source.상태 = "확정";

        var result = 기사운송표시Mapper.Map(source);

        Assert.Equal(42, result.Id);
        Assert.Equal("synthetic-priced-request", result.의뢰Id);
        Assert.Equal(source.운임, result.예상수익);
        Assert.Equal(source.예상거리Km, result.운송거리Km);
        Assert.Equal(source.거리계산방식 ?? string.Empty, result.거리계산방식);
        Assert.Equal(expectedFare, result.운임표시);
        Assert.Equal(expectedDistance, result.운송거리표시);
        Assert.Equal("확정", result.현재단계);
        Assert.Equal("상차지 도착", result.다음행동);
        if (expectedDistance == "거리 미확인")
            Assert.Null(result.운송거리Km);
        else
            Assert.NotNull(result.운송거리Km);
    }

    [Theory]
    [InlineData("{}", "운임 미확인", "거리 미확인", "거리 미확인", false)]
    [InlineData("{\"예상수익\":0,\"운송거리Km\":0,\"픽업거리Km\":0}", "0원", "0.0km", "예상 0.0km", true)]
    [InlineData("{\"예상수익\":6500,\"운송거리Km\":12.5,\"픽업거리Km\":12.5}", "6,500원", "12.5km", "예상 12.5km", true)]
    public void 추천의_미확인운임은_0원으로표시하거나출력하지않고_실제0은_보존한다(
        string json, string expectedFare, string expectedDistance, string expectedForecast, bool canPrint)
    {
        var request = JsonSerializer.Deserialize<DriverRequestItem>(json, WebJson)!;
        var recommendation = new 추천의뢰표시항목(request, 0m, 1);

        Assert.Equal(expectedFare, request.운임표시);
        Assert.Equal(expectedDistance, request.운송거리표시);
        Assert.Equal(expectedDistance, request.픽업거리표시);
        Assert.Equal(expectedFare, recommendation.예상수익표시);
        Assert.Equal(expectedForecast, recommendation.운송거리표시);
        Assert.Equal(canPrint, request.운송장출력가능);
        Assert.Equal("0.0km", recommendation.상차지까지거리표시); // 확인된 추천 거리 0은 보존합니다.
    }

    [Theory]
    [InlineData(null, "거리 미확인", "거리 미확인")]
    [InlineData(0, "0.0km", "0.0km")]
    [InlineData(5, "5.0km", "+5.0km")]
    public void 추천의_보조거리도_누락과_확인된0을구별한다(
        int? value, string expectedDistance, string expectedDetour)
    {
        var request = new DriverRequestItem
        {
            총공차거리Km = value, 복귀예상거리Km = value,
            지금바로복귀거리Km = value, 복귀우회증가거리Km = value
        };

        Assert.Equal(expectedDistance, request.총공차거리표시);
        Assert.Equal(expectedDistance, request.복귀예상거리표시);
        Assert.Equal(expectedDistance, request.지금바로복귀거리표시);
        Assert.Equal(expectedDetour, request.복귀우회증가거리표시);
    }

    [Fact]
    public void 일부거리만확인된경우_없는값을0으로채우지않는다()
    {
        var request = new DriverRequestItem
        {
            직선거리Km = 0m, 주행거리Km = null,
            복귀지기준추천여부 = true, 복귀예상거리Km = 0m, 복귀우회증가거리Km = null
        };

        Assert.Equal("직선 0.0km / 주행 거리 미확인", request.거리표시);
        Assert.Equal("복귀 0.0km / 우회 거리 미확인", request.복귀표시);
    }
}
