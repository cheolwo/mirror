using Ssalddel.Application.Admin.Food;
using Ssalddel.Contracts.Admin.Food;
using Ssalddel.Contracts.Food;

namespace Ssalddel.Tests.Application.Admin.Food;

public sealed class 음식배달운영생명주기조화ProjectorTests
{
    [Theory]
    [InlineData(
        음식주문상태코드.주문대기,
        음식배달운영생명주기단계Codes.음식점응답대기,
        음식배달운영책임주체Codes.음식점)]
    [InlineData(
        음식주문상태코드.조리중,
        음식배달운영생명주기단계Codes.조리배차병행,
        음식배달운영책임주체Codes.배차Engine)]
    [InlineData(
        음식주문상태코드.기사배정,
        음식배달운영생명주기단계Codes.픽업인계,
        음식배달운영책임주체Codes.음식배달기사)]
    [InlineData(
        음식주문상태코드.픽업완료,
        음식배달운영생명주기단계Codes.배송,
        음식배달운영책임주체Codes.음식배달기사)]
    [InlineData(
        음식주문상태코드.전달완료,
        음식배달운영생명주기단계Codes.수령확인대기,
        음식배달운영책임주체Codes.주문자)]
    public void 정상단계는_같은주문원장에서_현재책임역할을정렬한다(
        string orderState,
        string expectedStage,
        string expectedRole)
    {
        var result = 음식배달운영생명주기조화Projector.판정(
            new 음식배달운영생명주기조화입력(orderState));

        Assert.Equal(expectedStage, result.현재단계Code);
        Assert.Equal(음식배달운영주의상태Codes.정상, result.주의상태Code);
        Assert.Contains(expectedRole, result.현재책임주체Codes);
        Assert.True(result.정상경로조화여부);
        Assert.False(result.자동회복대상여부);
        Assert.False(result.운영자확인필요여부);
    }

    [Theory]
    [InlineData(음식주문상태코드.수령확인)]
    [InlineData(음식주문상태코드.거절)]
    [InlineData(음식주문상태코드.취소)]
    public void 종료상태는_새책임역할을만들지않는다(string orderState)
    {
        var result = 음식배달운영생명주기조화Projector.판정(
            new 음식배달운영생명주기조화입력(orderState));

        Assert.Equal(음식배달운영생명주기단계Codes.종료, result.현재단계Code);
        Assert.Empty(result.현재책임주체Codes);
        Assert.True(result.정상경로조화여부);
    }

    [Fact]
    public void 추천만료와배달중단은_관리자수동배차가아닌_배차Engine자동회복대상이다()
    {
        var result = 음식배달운영생명주기조화Projector.판정(
            new 음식배달운영생명주기조화입력(
                음식주문상태코드.조리중,
                기사추천만료여부: true,
                최근배달시도중단여부: true));

        Assert.Equal(음식배달운영주의상태Codes.자동회복중, result.주의상태Code);
        Assert.True(result.자동회복대상여부);
        Assert.False(result.운영자확인필요여부);
        Assert.Contains(음식배달운영책임주체Codes.배차Engine, result.현재책임주체Codes);
        Assert.DoesNotContain(음식배달운영책임주체Codes.플랫폼운영자, result.현재책임주체Codes);
        Assert.Empty(result.예외Codes);
        Assert.Equal(
            [
                음식배달운영자동회복Codes.기사추천만료,
                음식배달운영자동회복Codes.최근배달시도중단
            ],
            result.자동회복Codes);
    }

    [Theory]
    [InlineData(
        음식배달운영지연상태Codes.주의,
        음식배달운영주의상태Codes.주의,
        false)]
    [InlineData(
        음식배달운영지연상태Codes.운영자확인필요,
        음식배달운영주의상태Codes.운영자확인필요,
        true)]
    public void 음식점준비지연은_하나의업무지연으로만표시한다(
        string delayState,
        string expectedAttentionState,
        bool expectedOperatorReview)
    {
        var result = 음식배달운영생명주기조화Projector.판정(
            new 음식배달운영생명주기조화입력(
                음식주문상태코드.조리중,
                음식점준비지연상태Code: delayState,
                음식점준비초과분: expectedOperatorReview ? 10 : 5));

        Assert.Equal(expectedAttentionState, result.주의상태Code);
        Assert.Equal(
            [음식배달운영업무지연Codes.음식점준비지연],
            result.업무지연Codes);
        Assert.Equal(expectedOperatorReview, result.운영자확인필요여부);
        Assert.Equal(
            expectedOperatorReview ? 1 : 0,
            result.예외Codes.Count);
        Assert.Contains(음식배달운영책임주체Codes.음식점, result.현재책임주체Codes);
    }

    [Fact]
    public void 배달진행지연은_조리지연과다른업무지연코드를사용한다()
    {
        var result = 음식배달운영생명주기조화Projector.판정(
            new 음식배달운영생명주기조화입력(
                음식주문상태코드.픽업완료,
                배달진행지연상태Code: 음식배달운영지연상태Codes.운영자확인필요,
                배달진행초과분: 10));

        Assert.Equal(
            [음식배달운영업무지연Codes.배달진행지연],
            result.업무지연Codes);
        Assert.Equal(
            [음식배달운영업무지연Codes.배달진행지연],
            result.예외Codes);
        Assert.Contains(음식배달운영책임주체Codes.음식배달기사, result.현재책임주체Codes);
    }

    [Fact]
    public void 원장누락이나Outbox실패는_플랫폼운영자확인대상이다()
    {
        var result = 음식배달운영생명주기조화Projector.판정(
            new 음식배달운영생명주기조화입력(
                음식주문상태코드.조리중,
                배차원장누락여부: true,
                공동원장동기화확인필요여부: true));

        Assert.Equal(음식배달운영주의상태Codes.운영자확인필요, result.주의상태Code);
        Assert.False(result.자동회복대상여부);
        Assert.True(result.운영자확인필요여부);
        Assert.False(result.정상경로조화여부);
        Assert.Contains(음식배달운영책임주체Codes.플랫폼운영자, result.현재책임주체Codes);
        Assert.Contains(음식배달운영기술이상Codes.배차원장누락, result.기술이상Codes);
        Assert.Contains(음식배달운영기술이상Codes.공동원장동기화확인필요, result.기술이상Codes);
    }
}
