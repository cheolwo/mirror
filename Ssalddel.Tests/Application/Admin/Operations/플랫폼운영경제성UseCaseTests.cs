using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ssalddel.ApiMetadata;
using Ssalddel.Application.Admin.Operations;
using Ssalddel.Contracts.Admin.Operations;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Controllers.Admin.Operations;
using Ssalddel.Domain.운영;

namespace Ssalddel.Tests.Application.Admin.Operations;

public sealed class 플랫폼운영경제성UseCaseTests
{
    private readonly 플랫폼운영경제성UseCase _useCase = new(new 플랫폼운영경제성Calculator());

    [Fact]
    public void 주문당기여금과손익분기주문수를_분류된금액으로계산한다()
    {
        var response = _useCase.평가(CreateRequest());

        Assert.Equal(600m, response.주문당기여금);
        Assert.Equal(0.6m, response.기여이익률);
        Assert.Equal(-30_000m, response.영업이익후보);
        Assert.Equal(150L, response.손익분기완료주문수);
        Assert.Equal(50L, response.손익분기추가필요주문수);
        Assert.False(response.손익분기도달);
        Assert.Equal(1_080_000m, response.기말가용현금);
        Assert.Equal(플랫폼운영경제성평가상태Codes.관리Simulation전용, response.상태Code);
        Assert.True(response.회계매핑승인필요);
        Assert.False(response.운영전표쓰기허용);
    }

    [Fact]
    public void 총거래액과통과자금은_플랫폼수익과영업이익에포함하지않는다()
    {
        var request = CreateRequest();
        request.항목 =
        [
            Item("gmv", 플랫폼운영경제성금액분류Codes.총거래액, 1_000m),
            Item("pass", 플랫폼운영경제성금액분류Codes.통과자금, 900m),
            Item("order-revenue", 플랫폼운영경제성금액분류Codes.주문수익후보, 100m),
            Item("recurring-revenue", 플랫폼운영경제성금액분류Codes.반복수익후보, 10m)
        ];

        var response = _useCase.평가(request);

        Assert.Equal(1_000m, response.총거래액);
        Assert.Equal(900m, response.통과자금);
        Assert.Equal(110m, response.플랫폼보유수익후보);
        Assert.Equal(110m, response.영업이익후보);
    }

    [Fact]
    public void 국가와통화는_시나리오를구분하지만자동환산하지않는다()
    {
        var korea = CreateRequest();
        var unitedStates = CreateRequest();
        unitedStates.시나리오StableId = "scenario:us:seattle";
        unitedStates.국가Code = "us";
        unitedStates.관할Code = "US-WA-SEA";
        unitedStates.통화Code = "usd";

        var koreaResponse = _useCase.평가(korea);
        var usResponse = _useCase.평가(unitedStates);

        Assert.Equal(koreaResponse.주문당기여금, usResponse.주문당기여금);
        Assert.Equal(koreaResponse.영업이익후보, usResponse.영업이익후보);
        Assert.Equal("US", usResponse.국가Code);
        Assert.Equal("USD", usResponse.통화Code);
        Assert.NotEqual(koreaResponse.결과Hash, usResponse.결과Hash);
    }

    [Fact]
    public void 본인대리인가정은_현금과영업이익후보를바꾸지않는다()
    {
        var principal = CreateRequest();
        principal.본인대리인가정Code = 플랫폼운영경제성본인대리인가정Codes.본인후보;
        var agent = CreateRequest();
        agent.본인대리인가정Code = 플랫폼운영경제성본인대리인가정Codes.대리인후보;

        var principalResponse = _useCase.평가(principal);
        var agentResponse = _useCase.평가(agent);

        Assert.Equal(principalResponse.영업이익후보, agentResponse.영업이익후보);
        Assert.Equal(principalResponse.기말가용현금, agentResponse.기말가용현금);
        Assert.DoesNotContain(플랫폼운영경제성주의Codes.본인대리인미정, principalResponse.주의사항Codes);
        Assert.NotEqual(principalResponse.결과Hash, agentResponse.결과Hash);
    }

    [Fact]
    public void 기여금이양수가아니면_손익분기주문수를제시하지않는다()
    {
        var request = CreateRequest();
        request.항목 =
        [
            Item("order-revenue", 플랫폼운영경제성금액분류Codes.주문수익후보, 40_000m),
            Item("variable-cost", 플랫폼운영경제성금액분류Codes.변동비용, 40_000m),
            Item("fixed-cost", 플랫폼운영경제성금액분류Codes.고정비용, 10_000m)
        ];

        var response = _useCase.평가(request);

        Assert.Null(response.손익분기완료주문수);
        Assert.Null(response.손익분기추가필요주문수);
        Assert.Contains(플랫폼운영경제성주의Codes.기여금비양수, response.주의사항Codes);
    }

    [Fact]
    public void 항목순서가달라도_결과Hash는같다()
    {
        var first = CreateRequest();
        var second = CreateRequest();
        second.항목 = first.항목.Reverse().ToArray();

        Assert.Equal(_useCase.평가(first).결과Hash, _useCase.평가(second).결과Hash);
    }

    [Fact]
    public void 중복StableId와알수없는분류Code를_거절한다()
    {
        var duplicate = CreateRequest();
        duplicate.항목 =
        [
            Item("same", 플랫폼운영경제성금액분류Codes.주문수익후보, 1m),
            Item("same", 플랫폼운영경제성금액분류Codes.고정비용, 1m)
        ];
        var unknown = CreateRequest();
        unknown.항목 = [Item("unknown", "Other", 1m)];

        Assert.Throws<ArgumentException>(() => _useCase.평가(duplicate));
        Assert.Throws<ArgumentException>(() => _useCase.평가(unknown));
    }

    [Fact]
    public void Api와Metadata는_관리자전용순수Preview경계를표시한다()
    {
        var controller = typeof(플랫폼운영경제성Controller);

        Assert.Equal(
            "api/v1/admin/operations/economics",
            controller.GetCustomAttribute<RouteAttribute>()?.Template);
        Assert.Equal("서버관리자전용", controller.GetCustomAttribute<AuthorizeAttribute>()?.Policy);
        Assert.Equal(
            SsalddelProductVersion.V3_5,
            controller.GetCustomAttribute<SsalddelApiVersionAttribute>()?.Version);
        Assert.Equal(
            SsalddelApiGrowthTrack.PlatformOperations,
            controller.GetCustomAttribute<SsalddelApiGrowthTrackAttribute>()?.Track);
        Assert.Equal(
            "evaluate",
            controller.GetMethod(nameof(플랫폼운영경제성Controller.평가))
                ?.GetCustomAttribute<HttpPostAttribute>()
                ?.Template);

        var metadata = SsalddelCodeMetadataReader.ReadFeature(
            SsalddelCodeFeatureKeys.PlatformOperatingEconomics,
            typeof(플랫폼운영경제성평가요청Dto).Assembly,
            typeof(플랫폼운영경제성Calculator).Assembly,
            controller.Assembly);

        Assert.Contains(metadata, item => item.ComponentType == typeof(플랫폼운영경제성평가요청Dto));
        Assert.Contains(metadata, item => item.ComponentType == typeof(플랫폼운영경제성Calculator));
        Assert.Contains(metadata, item => item.ComponentType == typeof(플랫폼운영경제성UseCase));
        Assert.Contains(metadata, item => item.ComponentType == controller);
        Assert.All(metadata, item =>
        {
            Assert.Equal(SsalddelCodeEffect.None, item.Effects);
            Assert.False(string.IsNullOrWhiteSpace(item.Boundary));
        });
    }

    private static 플랫폼운영경제성평가요청Dto CreateRequest()
        => new()
        {
            시나리오StableId = "scenario:kr:sagajeong",
            국가Code = "kr",
            관할Code = "KR-11-260",
            통화Code = "krw",
            기간시작일 = new DateOnly(2026, 9, 1),
            기간종료일 = new DateOnly(2026, 9, 7),
            완료주문수 = 100,
            기초가용현금 = 1_000_000m,
            본인대리인가정Code = 플랫폼운영경제성본인대리인가정Codes.미정,
            입력Revision = "input-r1",
            항목 =
            [
                Item("gmv", 플랫폼운영경제성금액분류Codes.총거래액, 1_000_000m),
                Item("order-revenue", 플랫폼운영경제성금액분류Codes.주문수익후보, 100_000m),
                Item("subscription", 플랫폼운영경제성금액분류Codes.반복수익후보, 10_000m),
                Item("pass-through", 플랫폼운영경제성금액분류Codes.통과자금, 900_000m),
                Item("courier-and-pg", 플랫폼운영경제성금액분류Codes.변동비용, 40_000m),
                Item("office", 플랫폼운영경제성금액분류Codes.고정비용, 100_000m),
                Item("pg-receipt", 플랫폼운영경제성금액분류Codes.현금유입, 1_000_000m),
                Item("merchant-driver-payment", 플랫폼운영경제성금액분류Codes.현금유출, 920_000m)
            ]
        };

    private static 플랫폼운영경제성금액항목Dto Item(string id, string classification, decimal amount)
        => new()
        {
            StableId = id,
            분류Code = classification,
            금액 = amount,
            근거Revision = "evidence-r1"
        };
}
