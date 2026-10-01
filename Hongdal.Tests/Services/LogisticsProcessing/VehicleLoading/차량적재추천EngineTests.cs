using Hongdal.Services.LogisticsProcessing.VehicleLoading;
using 홍달.Services.Dispatch.Recommendation;
using 홍달.도메인.차량;
using 홍달.도메인.화주;

namespace Hongdal.Tests.Services.LogisticsProcessing.VehicleLoading;

public sealed class 차량적재추천EngineTests
{
    private readonly 차량적재추천Engine _engine = new();

    [Fact]
    public void 분석_바닥회전으로적재가능하면_관리우선순위보다알맞은작은차량을추천한다()
    {
        var compact = Vehicle("compact", 1_600, 2_000, 1_200, 1_000, 100, 3.8m);
        var oversized = Vehicle("oversized", 3_000, 2_000, 2_000, 5_000, 1, 12m);
        var requirement = new 차량적재추천요구사항
        {
            총중량Kg = 500,
            총부피Cbm = 1.8m,
            포장목록 =
            [
                new 차량적재포장요구사항
                {
                    항목명 = "회전 가능한 상자",
                    포장길이Mm = 1_800,
                    포장폭Mm = 1_000,
                    포장높이Mm = 1_000,
                    바닥회전가능여부 = true
                }
            ]
        };

        var result = _engine.분석(requirement, [oversized, compact]);

        Assert.Equal("compact", result.추천후보[0].차량.차량코드);
        Assert.True(result.추천후보[0].단일운송가능여부);
    }

    [Fact]
    public void 평가_중량을초과하고분할허용이면_필요운행횟수를올림계산한다()
    {
        var vehicle = Vehicle("one-ton", 3_000, 1_700, 1_700, 1_000, 10, 8m);
        var requirement = new 차량적재추천요구사항
        {
            총중량Kg = 2_100,
            총부피Cbm = 4,
            분할운송허용 = true
        };

        var result = _engine.평가(vehicle, requirement);

        Assert.True(result.하드조건적합여부);
        Assert.False(result.단일운송가능여부);
        Assert.True(result.분할운송추천가능여부);
        Assert.Equal(3, result.권장운행횟수);
        Assert.Contains(result.단일운송불가사유, x => x.Contains("중량 초과", StringComparison.Ordinal));
    }

    [Fact]
    public void 평가_적층불가바닥면적도_운행횟수에반영한다()
    {
        var vehicle = Vehicle("flatbed", 2_000, 2_000, null, 2_000, 10, null);
        var requirement = new 차량적재추천요구사항
        {
            총중량Kg = 500,
            적층불가바닥면적M2 = 7.5m,
            분할운송허용 = true
        };

        var result = _engine.평가(vehicle, requirement);

        Assert.Equal(2, result.권장운행횟수);
        Assert.Equal(187.5m, result.바닥면적사용률Percent);
    }

    [Fact]
    public void 평가_포장치수가일부만있어도_입력된길이는검증한다()
    {
        var vehicle = Vehicle("short-bed", 3_000, 1_700, 1_700, 2_000, 10, 7m);
        var requirement = new 차량적재추천요구사항
        {
            포장목록 =
            [
                new 차량적재포장요구사항
                {
                    항목명 = "길이만 등록된 화물",
                    포장길이Mm = 3_500
                }
            ]
        };

        var result = _engine.평가(vehicle, requirement);

        Assert.False(result.하드조건적합여부);
        Assert.Contains(result.하드부적합사유, x => x.Contains("맞지 않음", StringComparison.Ordinal));
        Assert.Contains(result.검증경고, x => x.Contains("일부", StringComparison.Ordinal));
    }

    [Fact]
    public void 배차적합성_별도요구조건이없어도_의뢰의부피와팔레트를검증한다()
    {
        var vehicle = Vehicle("limited", 3_000, 1_700, 1_700, 2_000, 10, 4m);
        vehicle.팔레트적재개수 = 2;
        var request = new 화주운송의뢰
        {
            의뢰Id = "request-1",
            화물종류 = "공동구매 상자",
            화물중량Kg = 500,
            화물부피Cbm = 5,
            화물팔레트개수 = 3,
            화물온도조건 = "상온"
        };
        var service = new 차량화물적합성Service(_engine);

        var result = service.판정(vehicle, request, null);

        Assert.False(result.적합여부);
        Assert.Contains(result.부적합사유, x => x.Contains("부피 초과", StringComparison.Ordinal));
        Assert.Contains(result.부적합사유, x => x.Contains("팔레트 초과", StringComparison.Ordinal));
    }

    private static 차량제원 Vehicle(
        string code,
        int lengthMm,
        int widthMm,
        int? heightMm,
        int weightKg,
        int priority,
        decimal? allowedCbm)
        => new()
        {
            차량코드 = code,
            차량명 = code,
            차체형태 = "테스트",
            적재함길이Mm = lengthMm,
            적재함폭Mm = widthMm,
            적재함높이Mm = heightMm,
            최대적재중량Kg = weightKg,
            운영권장중량Kg = weightKg,
            권장최대CBM = allowedCbm,
            추천우선순위 = priority,
            추천사용여부 = true
        };
}
