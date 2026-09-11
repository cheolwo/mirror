using 살뜰.도메인.운영;

namespace Ssalddel.Tests.Domain.Operations;

public sealed class 살뜰마트라스트마일배차PolicyTests
{
    private static readonly DateTime ConfirmedAt = new(2026, 9, 11, 1, 0, 0, DateTimeKind.Utc);
    private readonly 살뜰마트라스트마일배차Policy _policy = new();

    [Fact]
    public void 한가하고시간여유가있으면_최대세건묶음전략을선택한다()
    {
        var result = _policy.Evaluate(Input(
            asOf: ConfirmedAt.AddMinutes(10),
            readyAt: ConfirmedAt.AddMinutes(12),
            pendingOrders: 2,
            availableCouriers: 4,
            bundleCandidates: 3));

        Assert.Equal(살뜰마트라스트마일배차전략Codes.묶음효율, result.전략Code);
        Assert.Equal(3, result.최대묶음주문수);
        Assert.True(result.후보등록);
        Assert.False(result.지금기사제안);
        Assert.Equal(ConfirmedAt.AddMinutes(40), result.내부목표시각Utc);
        Assert.Equal(ConfirmedAt.AddMinutes(50), result.고객약속상한시각Utc);
    }

    [Fact]
    public void 주문이밀리고준비가임박하면_피크전략으로조기제안한다()
    {
        var result = _policy.Evaluate(Input(
            asOf: ConfirmedAt.AddMinutes(12),
            readyAt: ConfirmedAt.AddMinutes(18),
            pendingOrders: 8,
            availableCouriers: 5,
            bundleCandidates: 1));

        Assert.Equal(살뜰마트라스트마일배차전략Codes.피크즉시, result.전략Code);
        Assert.True(result.후보등록);
        Assert.True(result.지금기사제안);
    }

    [Fact]
    public void 고객약속이임박하면_묶지않고오래된주문회복을선택한다()
    {
        var result = _policy.Evaluate(Input(
            asOf: ConfirmedAt.AddMinutes(43),
            readyAt: ConfirmedAt.AddMinutes(43),
            pendingOrders: 2,
            availableCouriers: 4,
            bundleCandidates: 3,
            stage: 살뜰마트라스트마일준비단계Codes.픽업준비완료));

        Assert.Equal(살뜰마트라스트마일배차전략Codes.오래된주문회복, result.전략Code);
        Assert.Equal(1, result.최대묶음주문수);
        Assert.True(result.지금기사제안);
        Assert.Equal(7, result.고객약속잔여분);
    }

    [Fact]
    public void 배차가능기사가없으면_기사부족전략을선택한다()
    {
        var result = _policy.Evaluate(Input(
            asOf: ConfirmedAt.AddMinutes(10),
            readyAt: ConfirmedAt.AddMinutes(15),
            pendingOrders: 3,
            availableCouriers: 0,
            bundleCandidates: 1));

        Assert.Equal(살뜰마트라스트마일배차전략Codes.기사부족, result.전략Code);
        Assert.True(result.지금기사제안);
        Assert.Contains(살뜰마트라스트마일배차사유Codes.기사공급부족, result.사유Codes);
    }

    [Fact]
    public void 준비예정이내부목표보다늦으면_준비지연보호를선택한다()
    {
        var result = _policy.Evaluate(Input(
            asOf: ConfirmedAt.AddMinutes(5),
            readyAt: ConfirmedAt.AddMinutes(45),
            pendingOrders: 1,
            availableCouriers: 5,
            bundleCandidates: 1));

        Assert.Equal(살뜰마트라스트마일배차전략Codes.준비지연보호, result.전략Code);
        Assert.False(result.지금기사제안);
        Assert.Contains(살뜰마트라스트마일배차사유Codes.내부목표이후준비예정, result.사유Codes);
    }

    [Fact]
    public void 수락된기사배정은_새전략이나기사제안을만들지않는다()
    {
        var result = _policy.Evaluate(Input(
            asOf: ConfirmedAt.AddMinutes(45),
            readyAt: ConfirmedAt.AddMinutes(45),
            pendingOrders: 20,
            availableCouriers: 0,
            bundleCandidates: 4,
            acceptedAssignment: true,
            stage: 살뜰마트라스트마일준비단계Codes.픽업준비완료));

        Assert.Equal(살뜰마트라스트마일배차전략Codes.수락배정유지, result.전략Code);
        Assert.False(result.후보등록);
        Assert.False(result.지금기사제안);
        Assert.False(result.전략변경허용);
    }

    [Fact]
    public void 같은입력은_같은판정을반환한다()
    {
        var input = Input(
            asOf: ConfirmedAt.AddMinutes(20),
            readyAt: ConfirmedAt.AddMinutes(24),
            pendingOrders: 4,
            availableCouriers: 2,
            bundleCandidates: 1);

        var first = _policy.Evaluate(input);
        var second = _policy.Evaluate(input);

        Assert.Equal(first with { 사유Codes = [] }, second with { 사유Codes = [] });
        Assert.Equal(first.사유Codes, second.사유Codes);
    }

    private static 살뜰마트라스트마일배차입력 Input(
        DateTime asOf,
        DateTime? readyAt,
        int pendingOrders,
        int? availableCouriers,
        int bundleCandidates,
        bool acceptedAssignment = false,
        string stage = 살뜰마트라스트마일준비단계Codes.피킹시작)
        => new(
            ConfirmedAt,
            asOf,
            stage,
            readyAt,
            pendingOrders,
            availableCouriers,
            bundleCandidates,
            acceptedAssignment);
}
