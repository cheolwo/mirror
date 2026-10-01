using System.Text.Json;
using Hongdal.Services.AgriculturalFisheries.Information;

namespace Hongdal.Tests.Services.AgriculturalFisheries;

public sealed class KamisJsonReaderTests
{
    [Fact]
    public void ReadString_대소문자와숫자필드를지원한다()
    {
        using var document = JsonDocument.Parse(
            """{"ITEM_NAME":"감자","price":1234.5}""");

        Assert.Equal("감자", KamisJsonReader.ReadString(document.RootElement, "item_name"));
        Assert.Equal("1234.5", KamisJsonReader.ReadString(document.RootElement, "PRICE"));
    }

    [Fact]
    public void ReadDataObject_배열형식의첫객체를반환한다()
    {
        using var document = JsonDocument.Parse(
            """{"data":[{"error_code":"000"}]}""");

        var result = KamisJsonReader.ReadDataObject(document.RootElement, "01", "100");

        Assert.Equal("000", result.GetProperty("error_code").GetString());
    }

    [Fact]
    public void ReadDataObject_data가없으면명확한오류를반환한다()
    {
        using var document = JsonDocument.Parse("{}");

        var exception = Assert.Throws<InvalidOperationException>(() =>
            KamisJsonReader.ReadDataObject(document.RootElement, "01", "100"));

        Assert.Contains("data 항목", exception.Message, StringComparison.Ordinal);
    }
}
