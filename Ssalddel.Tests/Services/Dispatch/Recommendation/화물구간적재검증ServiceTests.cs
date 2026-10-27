using Ssalddel.Contracts.Shipper.Request;
using Ssalddel.Services.LogisticsProcessing.VehicleLoading;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.도메인.차량;
using 살뜰.도메인.화물;
using 살뜰.도메인.화주;

namespace Ssalddel.Tests.Services.Dispatch.Recommendation;

public sealed class 화물구간적재검증ServiceTests
{
    private readonly 화물구간적재검증Service service = new(new 차량적재추천Engine());

    [Fact]
    public void 합계가한도를넘어도_먼저하차하고다음상차하면통과한다()
    {
        var jobs = new[] { Job("a", 60), Job("ba", 60) };
        Assert.True(service.평가(Vehicle(), jobs, Stops(("a", "pickup"), ("a", "dropoff"), ("ba", "pickup"), ("ba", "dropoff"))).적합여부);
        Assert.False(service.평가(Vehicle(), jobs, Stops(("a", "pickup"), ("ba", "pickup"), ("a", "dropoff"), ("ba", "dropoff"))).적합여부);
    }

    [Fact]
    public void 처음적재상태부터_운영권장상한을검증한다()
    {
        var vehicle = Vehicle(); vehicle.운영권장중량Kg = 50;
        var result = service.평가(vehicle, [Job("a", 30) with { 이미상차됨 = true }, Job("b", 30) with { 이미상차됨 = true }],
            Stops(("a", "dropoff"), ("b", "dropoff")));
        Assert.False(result.적합여부);
        Assert.Equal("initial", Assert.Single(result.구간목록).단계유형);
    }

    [Fact]
    public void 십진수중량을_전문원장의정수올림으로덮지않는다()
    {
        var vehicle = Vehicle(); vehicle.최대적재중량Kg = 25;
        var first = Job("a", 12.5m) with { 요구조건 = new 화물요구조건 { 화물무게Kg = 13 } };
        var second = Job("b", 12.5m) with { 요구조건 = new 화물요구조건 { 화물무게Kg = 13 } };
        var result = service.평가(vehicle, [first, second], Stops(("a", "pickup"), ("b", "pickup"), ("a", "dropoff"), ("b", "dropoff")));
        Assert.True(result.적합여부);
        Assert.Contains(result.구간목록, x => x.총중량Kg == 25m);
    }

    [Fact]
    public void 비적층합계바닥면적을_부피와별도로검증한다()
    {
        var a = Job("a", 10); var b = Job("b", 10);
        foreach (var item in new[] { a, b })
        { item.의뢰.화물길이Mm = 800; item.의뢰.화물폭Mm = 800; item.의뢰.화물높이Mm = 100; item.의뢰.화물적층가능여부 = false; }
        Assert.False(service.평가(Vehicle(), [a, b], Stops(("a", "pickup"), ("b", "pickup"), ("a", "dropoff"), ("b", "dropoff"))).적합여부);
    }

    [Fact]
    public void 물리CBM을_표시반올림이나운영CBM으로늘리지않는다()
    {
        var vehicle = Vehicle(); vehicle.적재함길이Mm = 293; vehicle.적재함폭Mm = 350; vehicle.적재함높이Mm = 200; vehicle.권장최대CBM = 1m;
        var job = Job("a", 1); job.의뢰.화물입력부피Cbm = job.의뢰.화물부피Cbm = .0208m;
        Assert.False(service.평가(vehicle, [job], Stops(("a", "pickup"), ("a", "dropoff"))).적합여부);
    }

    [Fact]
    public void 누락중복하차선행_문자열접미어혼동을통과시키지않는다()
    {
        var jobs = new[] { Job("a", 10), Job("ba", 10) };
        Assert.False(service.평가(Vehicle(), jobs, Stops(("a", "pickup"), ("a", "dropoff"))).확인됨);
        Assert.False(service.평가(Vehicle(), [jobs[0]], Stops(("a", "dropoff"), ("a", "pickup"))).적합여부);
        Assert.False(service.평가(Vehicle(), [jobs[0]], Stops(("a", "pickup"), ("a", "pickup"), ("a", "dropoff"))).확인됨);
    }

    [Fact]
    public void 입력근거미확인은_적합으로확정하지않는다()
    {
        var job = Job("a", 1); job.의뢰.화물중량Kg = null;
        var result = service.평가(Vehicle(), [job], Stops(("a", "pickup"), ("a", "dropoff")));
        Assert.False(result.확인됨); Assert.False(result.적합여부);
    }

    [Fact]
    public void 합산표현이달라도_개별냉동요구를잃지않는다()
    {
        var job = Job("a", 1); job.의뢰.화물온도조건 = "Frozen";
        Assert.False(service.평가(Vehicle(), [job], Stops(("a", "pickup"), ("a", "dropoff"))).적합여부);
    }

    [Theory]
    [InlineData("독차", "상온")]
    [InlineData("혼적불가", "상온")]
    [InlineData("혼적", "냉장")]
    public void 조합정책은_물리용량검사와별도로_독차와민감을차단한다(string mode, string temperature)
    {
        var a = Job("a", 1); a.의뢰.운송방식 = mode; a.의뢰.화물온도조건 = temperature;
        Assert.Empty(화물구간적재검증Service.혼적차단사유([a]));
        Assert.NotEmpty(화물구간적재검증Service.혼적차단사유([a, Job("b", 1)]));
    }

    private static 화물구간적재작업 Job(string id, decimal weight) => new(new 화주운송의뢰
    {
        의뢰Id = id, 화물종류 = "합성 상자", 화물수량 = 1, 화물중량Kg = weight,
        화물입력부피Cbm = .1m, 화물부피Cbm = .1m, 화물부피근거Code = "UserProvided",
        화물적재계산판본 = 화물적재입력Policy.CurrentRevision, 화물팔레트개수 = 0,
        화물바닥회전가능여부 = true, 화물적층가능여부 = true, 화물온도조건 = "상온"
    });
    private static 차량제원 Vehicle() => new() { 차량코드 = "fixture", 최대적재중량Kg = 100,
        적재함길이Mm = 1000, 적재함폭Mm = 1000, 적재함높이Mm = 1000, 팔레트적재개수 = 2 };
    private static 기사운송일정항목[] Stops(params (string Id, string Stage)[] items) => items.Select((x, i) =>
        new 기사운송일정항목(x.Id, x.Stage, "합성 공개 지점", null, null, null, i, null, false, false)).ToArray();
}
