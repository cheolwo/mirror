using System.Text.Json;
using Xunit;
using 살뜰.도메인.기사;
using 살뜰.Services.Dispatch.Coordination;
using 살뜰.Services.Dispatch.Recommendation;

namespace Ssalddel.Tests.Services.Dispatch.Coordination;

public sealed class 국내화물조합평가ComposerTests
{
    public static TheoryData<decimal?, decimal?, decimal> 보정경계 => new()
    {
        { null, null, 70m },
        { -5000m, -90000m, 50m },
        { 0m, -60000m, 50m },
        { 5000m, 3000m, 70m },
        { 100000m, 90000m, 80m },
        { 500000m, 300000m, 80m },
        { 99995m, 89985m, 79.996m }
    };

    [Theory]
    [MemberData(nameof(보정경계))]
    public void 비용과이익보정은_기존상하한을유지하고_복귀가점은한번만더한다(
        decimal? cost, decimal? profit, decimal expected)
    {
        var score = 국내화물조합평가Composer.점수계산(
            new(50m, [], [], "기본", null), 4m,
            new(20m, true, 10m, 7m, "복귀"), cost, profit,
            new(6m, 2m, true, 기사복귀선호코드.복귀우선, "부담"));

        Assert.Equal(expected, score);
    }

    [Fact]
    public void 제외사유추가후_전체필드와문구와배지경고순서를보존한다()
    {
        var input = 자료();
        var score = 국내화물조합평가Composer.점수계산(input.추천평가, input.Aging점수,
            input.권역복귀평가, input.예상총비용, input.예상순이익, input.복귀부담);
        var actual = 국내화물조합평가Composer.조립(input, score,
            ["기존 제외", "DispatchEvidence.ChangedDuringEvaluation", "기존 제외"]);
        var expected = new 운송의뢰기사조합평가(
            "request", "driver", false,
            1.24m, 2.4m, 3.6m, 6m, 7.89m, 1234m, 50000m, 25000m, 25000m,
            true, false, 3, true, 6.5m, 8m, false, true, 2.34m, 3.12m, true, 0m,
            "기본 근거 · 기사대기보정 +4 · 부담 근거 · 복귀 근거",
            ["기본", "권역 복귀 방향", "퇴근복귀부담", "복귀콜선호"],
            ["적합", "차량", "평가"],
            ["기존 제외", "DispatchEvidence.ChangedDuringEvaluation"]);

        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));
        Assert.Equal(new[] { "기본", "기본", "권역 복귀 방향" }, input.추천평가.배지);
        Assert.Null(actual.판정근거);
    }

    [Fact]
    public void 정상후보는_계산중반올림하지않고_결과에서두자리로반올림한다()
    {
        var input = 자료();
        var score = 국내화물조합평가Composer.점수계산(input.추천평가, input.Aging점수,
            input.권역복귀평가, input.예상총비용, input.예상순이익, input.복귀부담);
        var result = 국내화물조합평가Composer.조립(input, score, []);

        Assert.True(result.추천가능여부);
        Assert.Equal(76.21m, result.추천점수);
        Assert.Empty(result.제외사유);
    }

    [Fact]
    public void 제외된음수점수는_0으로올리지않으며_중복사유는먼저나온순서로유지한다()
    {
        var result = 국내화물조합평가Composer.조립(자료(), -15.335m, ["둘", "하나", "둘"]);

        Assert.False(result.추천가능여부);
        Assert.Equal(-15.34m, result.추천점수);
        Assert.Equal(new[] { "둘", "하나" }, result.제외사유);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    public void 미확인수치와일정은_수락건수별기존기본값을유지한다(int acceptedCount, bool insertionAllowed)
    {
        var input = 자료() with
        {
            Aging점수 = 0m, 현재수락운송건수 = acceptedCount,
            상차지거리Km = null, 상차지이동시간분 = null, 화물운송시간분 = null,
            총예상시간분 = null, 총예상거리Km = null, 예상톨비 = null,
            예상운임 = null, 예상총비용 = null, 예상순이익 = null,
            일정평가 = null, 하차후복귀거리Km = null,
            복귀부담 = new(0m, 0m, false, 기사복귀선호코드.복귀우선, " "),
            권역복귀평가 = null,
            추천평가 = new(null, [], [], "기본 근거", null),
            차량적합성 = new(true, [], [])
        };
        var score = 국내화물조합평가Composer.점수계산(input.추천평가, input.Aging점수,
            input.권역복귀평가, input.예상총비용, input.예상순이익, input.복귀부담);
        var result = 국내화물조합평가Composer.조립(input, score, []);

        Assert.Equal(0m, result.추천점수);
        Assert.True(result.추천가능여부);
        Assert.Equal(insertionAllowed, result.일정삽입가능여부);
        Assert.True(result.전체일정완수가능여부);
        Assert.Null(result.최적삽입인덱스);
        Assert.False(result.경로변경이점여부);
        Assert.Null(result.경로변경절감분);
        Assert.Null(result.총추가지연분);
        Assert.Null(result.상차지거리Km);
        Assert.Null(result.상차지이동시간분);
        Assert.Null(result.화물운송시간분);
        Assert.Null(result.총예상시간분);
        Assert.Null(result.총예상거리Km);
        Assert.Null(result.예상톨비);
        Assert.Null(result.예상운임);
        Assert.Null(result.예상총비용);
        Assert.Null(result.예상순이익);
        Assert.Null(result.하차후복귀거리Km);
        Assert.Equal("기본 근거", result.추천사유);
        Assert.Empty(result.배지);
        Assert.Empty(result.경고);
    }

    [Fact]
    public void 가점없는복귀보류도_사유에남기고_수익우선배지와대소문자차이를보존한다()
    {
        var input = 자료() with
        {
            Aging점수 = -1m,
            권역복귀평가 = 기사권역복귀평가.보류("미확인 복귀"),
            복귀부담 = new(5m, 0m, true, 기사복귀선호코드.수익우선, null),
            추천평가 = new(10m, ["badge", "Badge", "badge"], ["warn", "Warn"], "기본 근거", null),
            차량적합성 = new(true, ["warn"], [])
        };
        var result = 국내화물조합평가Composer.조립(input, 4m, []);

        Assert.Equal("기본 근거 · 미확인 복귀", result.추천사유);
        Assert.Equal(new[] { "badge", "Badge", "퇴근복귀부담", "수익우선" }, result.배지);
        Assert.Equal(new[] { "warn", "Warn" }, result.경고);
    }

    private static 국내화물조합평가자료 자료() => new(
        "request", "driver", 4m, 1,
        1.235m, 2.4m, 3.6m, 6m, 7.89m, 1234m, 50000m, 25000m, 25000m,
        new(true, false, 3, true, 6.5m, 99m, 25m, 8m, null, [], [], [], []),
        false, true, 2.345m,
        new(3.125m, 2m, true, 기사복귀선호코드.복귀우선, "부담 근거"),
        new(20m, true, 10m, 7m, "복귀 근거"),
        new(50m, ["기본", "기본", "권역 복귀 방향"], ["적합", "평가", "평가"], "기본 근거", null),
        new(true, ["적합", "차량"], []));
}
