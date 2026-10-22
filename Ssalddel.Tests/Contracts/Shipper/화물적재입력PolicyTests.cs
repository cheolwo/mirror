using System.Globalization;
using System.Text.Json;
using Ssalddel.Contracts.Shipper.Request;

namespace Ssalddel.Tests.Contracts.Shipper;

public sealed class 화물적재입력PolicyTests
{
    private static 화물적재입력 Box => new()
    {
        화물종류 = "검증 상자", 수량 = 2, 길이Mm = 293, 폭Mm = 350, 높이Mm = 200,
        총중량Kg = 12.5m, 바닥회전가능여부 = false, 적층가능여부 = false, 팔레트개수 = 0
    };

    [Fact]
    public void 치수와수량은원정밀도로계산하고_입력부피가작으면보수추정값을사용한다()
    {
        var input = Box with { 입력부피Cbm = 0.040999m };
        var result = 화물적재입력Policy.계산(input);

        Assert.True(result.유효여부);
        Assert.Equal(0.04102m, result.유효부피Cbm);
        Assert.Equal(0.04102m, result.치수추정부피Cbm);
        Assert.Equal(0.20510m, result.비적층바닥면적M2);
        Assert.Equal(0.040999m, result.입력.입력부피Cbm);
        Assert.Equal(12.5m, result.입력.총중량Kg);
        Assert.Equal("ConservativeDimensionsEstimate", result.부피근거Code);
        Assert.Contains(result.경고, x => x.Contains("더 큰", StringComparison.Ordinal));
    }

    [Fact]
    public void 더큰직접입력부피와치수추정부피를별도로보존한다()
    {
        var result = 화물적재입력Policy.계산(Box with { 입력부피Cbm = 0.050001m });

        Assert.Equal(0.050001m, result.유효부피Cbm);
        Assert.Equal(0.04102m, result.치수추정부피Cbm);
        Assert.Equal("UserProvided", result.부피근거Code);
    }

    [Fact]
    public void 누락수량과회전적층은원래null을남기고_기존가정을경고로구분한다()
    {
        var result = 화물적재입력Policy.계산(Box with
        { 수량 = null, 바닥회전가능여부 = null, 적층가능여부 = null, 입력부피Cbm = null });

        Assert.True(result.유효여부);
        Assert.Null(result.입력.수량);
        Assert.Null(result.입력.바닥회전가능여부);
        Assert.Null(result.입력.적층가능여부);
        Assert.Equal(1, result.적용수량);
        Assert.True(result.바닥회전가능여부);
        Assert.True(result.적층가능여부);
        Assert.Equal(0.02051m, result.유효부피Cbm);
        Assert.Equal("DimensionsEstimated", result.부피근거Code);
        Assert.Contains(result.경고, x => x.Contains("1개를 가정", StringComparison.Ordinal));
        Assert.Contains(result.경고, x => x.Contains("회전 가능 여부가 미확인", StringComparison.Ordinal));
        Assert.Contains(result.경고, x => x.Contains("적층 가능 여부가 미확인", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("quantity", 0)]
    [InlineData("quantity", -1)]
    [InlineData("length", 0)]
    [InlineData("width", -1)]
    [InlineData("height", 0)]
    [InlineData("weight", 0)]
    [InlineData("weight", -1)]
    [InlineData("volume", 0)]
    [InlineData("volume", -1)]
    [InlineData("pallet", -1)]
    public void 명시한영또는음수는_기본값으로바꾸지않고오류를반환한다(string field, int value)
    {
        var input = field switch
        {
            "quantity" => Box with { 수량 = value },
            "length" => Box with { 길이Mm = value },
            "width" => Box with { 폭Mm = value },
            "height" => Box with { 높이Mm = value },
            "weight" => Box with { 총중량Kg = value },
            "volume" => Box with { 입력부피Cbm = value },
            _ => Box with { 팔레트개수 = value }
        };

        var result = 화물적재입력Policy.계산(input);

        Assert.False(result.유효여부);
        Assert.NotEmpty(result.오류);
        Assert.Equal(input, result.입력);
    }

    [Fact]
    public void 팔레트영개는기존의팔레트없음의미로유효하다()
    {
        var result = 화물적재입력Policy.계산(Box);
        Assert.True(result.유효여부);
        Assert.Equal(0, result.입력.팔레트개수);
    }

    [Fact]
    public void 일부치수만있으면부피미확인과_비적층면적미확인을구분한다()
    {
        var result = 화물적재입력Policy.계산(Box with { 폭Mm = null });

        Assert.Null(result.유효부피Cbm);
        Assert.Null(result.치수추정부피Cbm);
        Assert.Null(result.비적층바닥면적M2);
        Assert.Equal("Unknown", result.부피근거Code);
        Assert.Contains(result.경고, x => x.Contains("바닥면적", StringComparison.Ordinal));
    }

    [Fact]
    public void 입력지문은문화권과숫자표현에독립적이고_rawnull과명시조건은구분한다()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            var first = 화물적재입력Policy.계산(Box with { 입력부피Cbm = 1.00m });
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            var second = 화물적재입력Policy.계산(Box with { 입력부피Cbm = 1m });
            Assert.Equal(first.입력지문, second.입력지문);
            Assert.NotEqual(first.입력지문, 화물적재입력Policy.계산(Box with { 입력부피Cbm = 1m, 바닥회전가능여부 = null }).입력지문);
            Assert.NotEqual(first.입력지문, 화물적재입력Policy.계산(Box with { 입력부피Cbm = 1m, 요청사항 = "리프트 필요" }).입력지문);
        }
        finally { CultureInfo.CurrentCulture = original; }
    }

    [Fact]
    public void 계약입력변환과json왕복에서명시false와원수량이유지된다()
    {
        var cargo = new CargoDTO
        {
            화물종류 = "검증 상자", 수량 = 2, 길이Mm = 293, 폭Mm = 350, 높이Mm = 200,
            중량Kg = 12.5m, 팔레트개수 = 0, 바닥회전가능여부 = false, 적층가능여부 = false
        };
        var result = 화물적재입력Policy.계산(화물적재입력Policy.입력생성(cargo));
        var restored = JsonSerializer.Deserialize<화물적재입력결과>(JsonSerializer.Serialize(result))!;

        Assert.Equal(Box, restored.입력);
        Assert.False(restored.바닥회전가능여부);
        Assert.False(restored.적층가능여부);
        Assert.Equal(result.유효부피Cbm, restored.유효부피Cbm);
        Assert.Equal(result.입력지문, restored.입력지문);
        Assert.True(restored.유효여부);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("future-unrecognized")]
    [InlineData(화물적재입력Policy.CurrentRevision)]
    public void 저장부피는원입력으로승격하지않고_더큰하한과기존판본을보존한다(string? storedRevision)
    {
        var calculated = 화물적재입력Policy.계산(Box);
        var result = 화물적재입력Policy.저장값적용(calculated, 0.1m, null, storedRevision);

        Assert.Null(result.입력.입력부피Cbm);
        Assert.Equal(0.1m, result.유효부피Cbm);
        Assert.Equal(0.04102m, result.치수추정부피Cbm);
        Assert.Equal(storedRevision ?? string.Empty, result.계산판본);
        Assert.Equal("LegacyUnverified", result.부피근거Code);
        Assert.Contains(result.경고, x => x.Contains("하한", StringComparison.Ordinal));
    }
}
