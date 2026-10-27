using Ssalddel.Services.LogisticsProcessing.VehicleLoading;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.도메인.차량;
using 살뜰.도메인.화주;

namespace Ssalddel.Tests.Services.LogisticsProcessing.VehicleLoading;

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

    [Theory]
    [InlineData(1_500, 1_000, false)]
    [InlineData(800, 800, false)]
    [InlineData(null, 1_000, true)]
    public void 평가_권장중량이물리최대중량을넘어도_더작은한도로판정한다(
        int? recommendedKg, int expectedAllowedKg, bool fits)
    {
        var vehicle = Vehicle("bounded-weight", 2_000, 2_000, 2_000, 1_000, 10, 8m);
        vehicle.운영권장중량Kg = recommendedKg;
        var result = _engine.평가(vehicle, new() { 총중량Kg = fits ? 1_000m : 1_100m });

        Assert.Equal(expectedAllowedKg, result.허용중량Kg);
        Assert.Equal(fits, result.단일운송가능여부);
    }

    [Theory]
    [InlineData("weight")]
    [InlineData("volume")]
    [InlineData("pallet")]
    [InlineData("floor")]
    public void 분석_요구량이있는축의차량기준이없으면_경고를남기고분할후보에서도제외한다(string axis)
    {
        var vehicle = Vehicle("incomplete", 2_000, 2_000, 2_000, 1_000, 10, 8m);
        var requirement = new 차량적재추천요구사항 { 분할운송허용 = true };
        var label = axis switch
        {
            "weight" => "중량",
            "volume" => "부피",
            "pallet" => "팔레트",
            _ => "바닥면적"
        };
        switch (axis)
        {
            case "weight":
                vehicle.최대적재중량Kg = 0;
                requirement.총중량Kg = 100m; // 권장값만 있어도 실차 최대값을 대신하지 않는다.
                break;
            case "volume":
                vehicle.권장최대CBM = null;
                vehicle.적재함높이Mm = null;
                requirement.총부피Cbm = 1m;
                break;
            case "pallet":
                requirement.총팔레트개수 = 1;
                break;
            default:
                vehicle.적재함폭Mm = 0;
                requirement.적층불가바닥면적M2 = 1m;
                break;
        }

        var analysis = _engine.분석(requirement, [vehicle]);
        var result = Assert.Single(analysis.전체평가);

        Assert.Empty(analysis.추천후보);
        Assert.False(result.하드조건적합여부);
        Assert.False(result.분할운송추천가능여부);
        Assert.Contains(result.검증경고, x => x.Contains(label, StringComparison.Ordinal));
        Assert.Contains(result.하드부적합사유, x => x.Contains("검증 불가", StringComparison.Ordinal)
            && x.Contains(label, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("length", 0)]
    [InlineData("width", 0)]
    [InlineData("height", null)]
    [InlineData("height", 0)]
    public void 평가_요구한포장치수의차량축이미확인이면_적합으로판정하지않는다(string axis, int? missingValue)
    {
        var vehicle = Vehicle("incomplete-dimensions", 2_000, 2_000, 2_000, 1_000, 10, 8m);
        var package = new 차량적재포장요구사항 { 항목명 = "확인할 상자" };
        switch (axis)
        {
            case "length": vehicle.적재함길이Mm = missingValue!.Value; package.포장길이Mm = 100; break;
            case "width": vehicle.적재함폭Mm = missingValue!.Value; package.포장폭Mm = 100; break;
            default: vehicle.적재함높이Mm = missingValue; package.포장높이Mm = 100; break;
        }

        var result = _engine.평가(vehicle, new() { 포장목록 = [package] });

        Assert.False(result.단일운송가능여부);
        Assert.Contains(result.하드부적합사유, x => x.Contains("검증 불가", StringComparison.Ordinal));
        Assert.Contains(result.검증경고, x => x.Contains("차량 적재함", StringComparison.Ordinal));
    }

    [Fact]
    public void 평가_생략한요구축은_차량기준이없어도_새로운필수조건으로만들지않는다()
    {
        var vehicle = Vehicle("weight-only", 0, 0, null, 1_000, 10, null);

        var result = _engine.평가(vehicle, new() { 총중량Kg = 500m });

        Assert.True(result.단일운송가능여부);
        Assert.Empty(result.하드부적합사유);
        Assert.Empty(result.검증경고);
    }

    [Fact]
    public void 분석_권장부피가있어도_물리높이가없으면_요구부피를검증한후보로추천하지않는다()
    {
        var vehicle = Vehicle("unverified-physical-volume", 2_000, 2_000, null, 1_000, 10, 8m);

        var analysis = _engine.분석(new() { 총부피Cbm = 1m }, [vehicle]);
        var result = Assert.Single(analysis.전체평가);

        Assert.Null(result.허용부피Cbm);
        Assert.Empty(analysis.추천후보);
        Assert.Contains(result.하드부적합사유, x => x.Contains("검증 불가", StringComparison.Ordinal)
            && x.Contains("부피", StringComparison.Ordinal));
        Assert.Contains(result.검증경고, x => x.Contains("부피", StringComparison.Ordinal));
    }

    [Fact]
    public void 분석_소형적재함의물리부피를반올림해_초과화물을허용하지않는다()
    {
        var vehicle = Vehicle("precise-small-volume", 293, 350, 200, 20, 10, null);

        var analysis = _engine.분석(new() { 총부피Cbm = 0.0208m }, [vehicle]);
        var result = Assert.Single(analysis.전체평가);

        Assert.Equal(0.02051m, result.허용부피Cbm);
        Assert.Empty(analysis.추천후보);
        Assert.Contains(result.단일운송불가사유, x => x.Contains("부피 초과", StringComparison.Ordinal));
    }

    [Fact]
    public void 분석_비적층바닥면적을반올림해_단일운송초과화물을허용하지않는다()
    {
        var vehicle = Vehicle("precise-floor-area", 293, 350, 200, 20, 10, null);

        var analysis = _engine.분석(new() { 적층불가바닥면적M2 = 0.1028m }, [vehicle]);
        var result = Assert.Single(analysis.전체평가);

        Assert.Equal(0.10255m, result.적재함바닥면적M2);
        Assert.Empty(analysis.추천후보);
        Assert.False(result.단일운송가능여부);
        Assert.Contains(result.단일운송불가사유, x => x.Contains("바닥면적", StringComparison.Ordinal));
    }

    [Fact]
    public void 배차적합성_차량제원이없으면_경고승인으로대신할수없는검증불가다()
    {
        var service = new 차량화물적합성Service(_engine);

        var result = service.판정(null, new() { 의뢰Id = "missing-vehicle" }, null);

        Assert.False(result.적합여부);
        Assert.Contains(result.경고, x => x.Contains("제원이 등록되지 않아", StringComparison.Ordinal));
        Assert.Contains(result.부적합사유, x => x.Contains("검증 불가", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void 분석과배차적합성_요구축생략여부와관계없이_비활성제원을제외한다(bool hasDemand)
    {
        var vehicle = Vehicle("disabled-vehicle", 2_000, 2_000, 2_000, 1_000, 10, 8m);
        vehicle.추천사용여부 = false;
        var weight = hasDemand ? 100m : (decimal?)null;

        var analysis = _engine.분석(new() { 총중량Kg = weight }, [vehicle]);
        var result = new 차량화물적합성Service(_engine)
            .판정(vehicle, new() { 의뢰Id = "disabled-test", 화물중량Kg = weight }, null);

        Assert.Empty(analysis.추천후보);
        Assert.Contains(Assert.Single(analysis.전체평가).하드부적합사유,
            x => x.Contains("비활성화", StringComparison.Ordinal));
        Assert.False(result.적합여부);
        Assert.Contains(result.부적합사유, x => x.Contains("비활성화", StringComparison.Ordinal));
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
