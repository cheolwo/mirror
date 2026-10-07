using Ssalddel.Contracts.Food;
using Ssalddel.Services.Food;
using 살뜰.도메인.음식;

namespace Ssalddel.Tests.Services.Food;

public sealed class 음식주문현재조리PolicyTests
{
    private static readonly DateTime At = new(2026, 10, 4, 1, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void 재조리경계는_일반조리와기존즉시준비를모두무효화한다(bool immediate)
    {
        var order = new 음식주문
        {
            상태이력 =
            [
                Event(1, immediate ? 음식주문상태코드.주문확인 : 음식주문상태코드.조리중,
                    immediate ? "음식점 주문 확인 · 기존 준비 완료" : "음식점 조리 시작"),
                Event(2, 음식주문상태코드.픽업대기, "음식점 픽업 준비 완료"),
                Event(3, 음식주문상태코드.픽업완료, "기사 픽업 완료"),
                Event(4, 음식주문상태코드.조리중, 음식주문현재조리Policy.재조리요청이력사유)
            ]
        };
        var result = 음식주문현재조리Policy.계산(order);
        Assert.Equal(2, result.Round);
        Assert.Null(result.ReadyAtUtc);
        Assert.Equal(At.AddMinutes(4), result.CookingStartedAtUtc);
        Assert.Equal(At.AddMinutes(4), result.RecookingRequestedAtUtc);
        Assert.Equal(4, order.상태이력.Count);
    }

    [Fact]
    public void 여러번재조리하면_마지막차수준비만사용하고_이력을보존한다()
    {
        var order = new 음식주문 { 상태이력 =
        [
            Event(1, 음식주문상태코드.픽업대기, "음식점 픽업 준비 완료"),
            Event(2, 음식주문상태코드.조리중, 음식주문현재조리Policy.재조리요청이력사유),
            Event(3, 음식주문상태코드.픽업대기, "음식점 픽업 준비 완료"),
            Event(4, 음식주문상태코드.조리중, 음식주문현재조리Policy.재조리요청이력사유)
        ] };
        Assert.Null(음식주문현재조리Policy.계산(order).ReadyAtUtc);
        order.상태이력.Add(Event(5, 음식주문상태코드.픽업대기, "음식점 픽업 준비 완료"));
        var current = 음식주문현재조리Policy.계산(order);
        Assert.Equal(3, current.Round);
        Assert.Equal(At.AddMinutes(5), current.ReadyAtUtc);
        Assert.Equal(5, order.상태이력.Count);
    }

    [Fact]
    public void 동일시각사건은_원장ID순서로재조리전후를구분한다()
    {
        var ready = Event(1, 음식주문상태코드.픽업대기, "음식점 픽업 준비 완료");
        var recook = Event(2, 음식주문상태코드.조리중, 음식주문현재조리Policy.재조리요청이력사유);
        ready.전이시각Utc = recook.전이시각Utc = At;
        var order = new 음식주문 { 상태이력 = [recook, ready] };
        Assert.Null(음식주문현재조리Policy.계산(order).ReadyAtUtc);
        var currentReady = Event(3, 음식주문상태코드.픽업대기, "음식점 픽업 준비 완료");
        currentReady.전이시각Utc = At;
        order.상태이력.Insert(0, currentReady);
        Assert.Equal(At, 음식주문현재조리Policy.계산(order).ReadyAtUtc);
    }

    [Fact]
    public void 픽업전재배차는_같은음식의현재차수준비를보존한다()
    {
        var order = new 음식주문 { 상태이력 =
        [
            Event(1, 음식주문상태코드.픽업대기, "음식점 픽업 준비 완료"),
            Event(2, 음식주문상태코드.픽업대기, "픽업 전 배달 중단 · Accident"),
            Event(3, 음식주문상태코드.픽업대기, "F드라이버 배차 수락")
        ] };
        var current = 음식주문현재조리Policy.계산(order);
        Assert.Equal(1, current.Round);
        Assert.Null(current.RecookingRequestedAtUtc);
        Assert.Equal(At.AddMinutes(1), current.ReadyAtUtc);
    }

    [Fact]
    public void DTO의과거시각은_재조리차수에다시붙이지않는다()
    {
        var order = new 음식주문응답
        {
            조리시작시각Utc = At,
            픽업준비시각Utc = At.AddMinutes(1),
            상태이력 = [new() { 다음상태 = 음식주문상태코드.조리중,
                사유 = 음식주문현재조리Policy.재조리요청이력사유, 전이시각Utc = At.AddMinutes(2) }]
        };
        var current = 음식주문현재조리Policy.계산(order);
        Assert.Equal(2, current.Round);
        Assert.Null(current.ReadyAtUtc);
        Assert.Equal(At.AddMinutes(2), current.CookingStartedAtUtc);
        Assert.Equal(At.AddMinutes(1), order.픽업준비시각Utc);
    }

    [Fact]
    public void 첫차수구형응답은_기존시각을재사용한다()
    {
        var current = 음식주문현재조리Policy.계산(new 음식주문응답
        { 조리시작시각Utc = At, 픽업준비시각Utc = At.AddMinutes(1) });
        Assert.Equal(1, current.Round);
        Assert.Equal(At, current.CookingStartedAtUtc);
        Assert.Equal(At.AddMinutes(1), current.ReadyAtUtc);
    }

    private static 음식주문상태이력 Event(int sequence, string status, string reason)
        => new() { Id = sequence, 다음상태 = status, 사유 = reason, 전이시각Utc = At.AddMinutes(sequence) };
}
