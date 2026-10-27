using Ssalddel.Application.Shipper.Request;
using Ssalddel.Contracts.Shipper.Request;
using Ssalddel.Services.LogisticsProcessing.VehicleLoading;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.도메인.차량;
using 살뜰.도메인.화물;
using 살뜰.도메인.화주;

namespace Ssalddel.Tests.Application.Shipper.Request;

public sealed class 차량적재입력일관성Tests
{
    [Fact]
    public async Task 소수부피초과는_추천과저장후판정에서같이제외한다()
    {
        var vehicle = Vehicle(293, 350, 200);
        var request = Input() with
        { 수량 = 1, 길이Mm = 293, 폭Mm = 350, 높이Mm = 200, 입력부피Cbm = 0.0208m };
        var (recommended, fit, service) = await Compare(request, vehicle);

        Assert.Empty(recommended.후보목록);
        Assert.False(fit.적합여부);
        Assert.Equal(0.0208m, service.LastRequirement!.총부피Cbm);
        Assert.Equal(recommended.적재조건!.유효부피Cbm, fit.적재조건!.유효부피Cbm);
        Assert.Contains(fit.부적합사유, x => x.Contains("부피 초과", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 명시한바닥회전선택에따라_추천과저장후판정이함께바뀐다(bool rotate)
    {
        var input = Input() with
        { 수량 = 1, 길이Mm = 1_800, 폭Mm = 1_000, 높이Mm = 1_000, 바닥회전가능여부 = rotate };
        var (recommended, fit, _) = await Compare(input, Vehicle(1_600, 2_000, 1_200));

        Assert.Equal(rotate, recommended.후보목록.Count > 0);
        Assert.Equal(rotate, fit.적합여부);
        Assert.Equal(rotate, recommended.적재조건!.바닥회전가능여부);
        Assert.Equal(recommended.적재조건.바닥회전가능여부, fit.적재조건!.바닥회전가능여부);
        Assert.Equal(recommended.적재조건.입력지문, fit.적재조건.입력지문);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 비적층포장수량의바닥면적을_추천과저장후판정에함께반영한다(bool stackable)
    {
        var input = Input() with
        { 수량 = 3, 길이Mm = 1_000, 폭Mm = 1_000, 높이Mm = 500, 적층가능여부 = stackable };
        var (recommended, fit, service) = await Compare(input, Vehicle(2_000, 1_000, 2_000));

        Assert.Equal(stackable, recommended.후보목록.Count > 0);
        Assert.Equal(stackable, fit.적합여부);
        Assert.Equal(stackable ? (decimal?)null : 3m, service.LastRequirement!.적층불가바닥면적M2);
        Assert.Equal(recommended.적재조건!.비적층바닥면적M2, fit.적재조건!.비적층바닥면적M2);
        Assert.Equal(1.5m, fit.적재조건.유효부피Cbm);
    }

    [Theory]
    [InlineData("리프트 필요")]
    [InlineData("측면 상하차 필요")]
    [InlineData("장재물")]
    [InlineData("방수 필요")]
    public async Task 같은취급요청문구는_추천과저장후장비판정에서같은제약이된다(string handling)
    {
        var input = Input() with { 요청사항 = handling };
        var vehicle = Vehicle(2_000, 2_000, 2_000);
        var (blocked, blockedFit, _) = await Compare(input, vehicle);
        Assert.Empty(blocked.후보목록);
        Assert.False(blockedFit.적합여부);

        vehicle.리프트가능 = true;
        vehicle.측면상하차가능 = true;
        vehicle.장재물유리 = true;
        vehicle.비눈보호가능 = true;
        var (allowed, allowedFit, _) = await Compare(input, vehicle);
        Assert.Single(allowed.후보목록);
        Assert.True(allowedFit.적합여부);
    }

    [Fact]
    public async Task 총중량은수량을다시곱하지않고_전문원장의정수올림보다원장소수를우선한다()
    {
        var input = Input() with { 수량 = 5, 총중량Kg = 12.5m };
        var service = new LoadingService(Vehicle(2_000, 2_000, 2_000));
        var recommended = await new 차량추천Service(service).추천Async(ToRequest(input), CancellationToken.None);
        var persisted = ToEntity(input);
        var fit = new 차량화물적합성Service(new()).판정(service.Vehicle, persisted,
            new 화물요구조건 { 화물무게Kg = 13 });

        Assert.Equal(12.5m, service.LastRequirement!.총중량Kg);
        Assert.Equal(12.5m, fit.적재조건!.입력.총중량Kg);
        Assert.Equal(recommended.적재조건!.입력지문, fit.적재조건.입력지문);
        Assert.True(fit.적합여부);
    }

    [Fact]
    public async Task 입력오류는추천후보계산전에거절하고_저장후적합판정에서도차단한다()
    {
        var input = Input() with { 총중량Kg = 0m };
        var (recommended, fit, service) = await Compare(input, Vehicle(2_000, 2_000, 2_000));

        Assert.Empty(recommended.후보목록);
        Assert.False(recommended.적재조건!.유효여부);
        Assert.Equal(0, service.Calls);
        Assert.False(fit.적합여부);
        Assert.Contains(fit.부적합사유, x => x.Contains("총중량", StringComparison.Ordinal));
    }

    [Fact]
    public void 구형저장부피는_직접입력으로승격하지않고_판정하한으로만사용한다()
    {
        var entity = new 화주운송의뢰 { 의뢰Id = "legacy", 화물종류 = "검증 상자", 화물부피Cbm = 0.5m };
        var result = new 차량화물적합성Service(new()).판정(Vehicle(2_000, 2_000, 2_000), entity, null);

        Assert.True(result.적합여부);
        Assert.Null(result.적재조건!.입력.입력부피Cbm);
        Assert.Null(result.적재조건.입력.수량);
        Assert.Null(result.적재조건.입력.바닥회전가능여부);
        Assert.Equal(0.5m, result.적재조건.유효부피Cbm);
        Assert.Equal("LegacyUnverified", result.적재조건.부피근거Code);
        Assert.Equal(string.Empty, result.적재조건.계산판본);
    }

    [Fact]
    public async Task 신규저장의선택문자열과기본온도운송방식은_원입력을승격하지않고동일지문을유지한다()
    {
        var input = Input() with { 설명 = null, 온도조건 = null, 운송방식 = null, 서비스레벨 = "  ", 요청사항 = null };
        var vehicle = Vehicle(2_000, 2_000, 2_000);
        var recommended = await new 차량추천Service(new LoadingService(vehicle))
            .추천Async(ToRequest(input), CancellationToken.None);
        var stored = ToEntity(input);
        stored.화물설명 = string.Empty;
        stored.화물온도조건 = "상온";
        stored.운송방식 = "혼적";
        stored.서비스레벨 = string.Empty;
        stored.요청사항 = string.Empty;

        var fit = new 차량화물적합성Service(new()).판정(vehicle, stored, null);

        Assert.Equal(recommended.적재조건!.입력지문, fit.적재조건!.입력지문);
        Assert.Null(recommended.적재조건.입력.온도조건);
        Assert.Null(recommended.적재조건.입력.운송방식);
        Assert.True(fit.적합여부);
    }

    private static 화물적재입력 Input() => new()
    {
        화물종류 = "검증 상자", 설명 = "", 수량 = 1, 길이Mm = 200, 폭Mm = 200, 높이Mm = 100,
        총중량Kg = 12.5m, 팔레트개수 = 0, 온도조건 = "상온", 바닥회전가능여부 = false,
        적층가능여부 = true, 운송방식 = "혼적", 서비스레벨 = "", 요청사항 = ""
    };

    private static async Task<(차량추천응답 Recommended, 차량화물적합성결과 Fit, LoadingService Service)> Compare(
        화물적재입력 input, 차량제원 vehicle)
    {
        var service = new LoadingService(vehicle);
        var recommended = await new 차량추천Service(service).추천Async(ToRequest(input), CancellationToken.None);
        var fit = new 차량화물적합성Service(new()).판정(vehicle, ToEntity(input), null);
        return (recommended, fit, service);
    }

    private static 차량추천요청 ToRequest(화물적재입력 input) => new()
    {
        화물종류 = input.화물종류, 화물설명 = input.설명, 화물수량 = input.수량,
        화물길이Mm = input.길이Mm, 화물폭Mm = input.폭Mm, 화물높이Mm = input.높이Mm,
        화물중량Kg = input.총중량Kg, 화물부피Cbm = input.입력부피Cbm, 팔레트개수 = input.팔레트개수,
        화물온도조건 = input.온도조건, 화물바닥회전가능여부 = input.바닥회전가능여부,
        화물적층가능여부 = input.적층가능여부, 화물파손주의여부 = input.파손주의여부,
        운송방식 = input.운송방식, 서비스레벨 = input.서비스레벨, 요청사항 = input.요청사항
    };

    private static 화주운송의뢰 ToEntity(화물적재입력 input)
    {
        var calculated = 화물적재입력Policy.계산(input);
        return new()
        {
            의뢰Id = "consistency-test", 화물종류 = input.화물종류!, 화물설명 = input.설명!, 화물수량 = input.수량,
            화물길이Mm = input.길이Mm, 화물폭Mm = input.폭Mm, 화물높이Mm = input.높이Mm,
            화물중량Kg = input.총중량Kg, 화물부피Cbm = calculated.유효부피Cbm,
            화물입력부피Cbm = input.입력부피Cbm, 화물부피근거Code = calculated.부피근거Code,
            화물적재계산판본 = calculated.계산판본, 화물팔레트개수 = input.팔레트개수,
            화물온도조건 = input.온도조건!, 화물바닥회전가능여부 = input.바닥회전가능여부,
            화물적층가능여부 = input.적층가능여부, 화물파손주의여부 = input.파손주의여부,
            운송방식 = input.운송방식!, 서비스레벨 = input.서비스레벨!, 요청사항 = input.요청사항!
        };
    }

    private static 차량제원 Vehicle(int length, int width, int height) => new()
    {
        차량코드 = "measured-test-vehicle", 차량명 = "측정된 검증 차량", 최대적재중량Kg = 1000,
        운영권장중량Kg = 1000, 적재함길이Mm = length, 적재함폭Mm = width, 적재함높이Mm = height,
        추천사용여부 = true
    };

    private sealed class LoadingService(차량제원 vehicle) : I차량적재추천Service
    {
        public 차량제원 Vehicle { get; } = vehicle;
        public int Calls { get; private set; }
        public 차량적재추천요구사항? LastRequirement { get; private set; }
        public Task<차량적재추천분석결과> 추천Async(차량적재추천요구사항 요구사항, CancellationToken cancellationToken = default)
        {
            Calls++;
            LastRequirement = 요구사항;
            return Task.FromResult(new 차량적재추천Engine().분석(요구사항, [Vehicle]));
        }
    }
}
