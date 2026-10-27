using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.Tests.Ui.Common;

public sealed class 업무전화번호PolicyTests
{
    [Theory]
    [InlineData("1234567", "1234567")]
    [InlineData("123456789012345", "123456789012345")]
    [InlineData("+123456789012345", "+123456789012345")]
    [InlineData(" 123-4567 ", "1234567")]
    [InlineData("+12 345-6789", "+123456789")]
    public void 숫자와_선행국가코드는_허용하고_표시용공백과하이픈만_제거한다(string raw, string expected)
    {
        Assert.True(업무전화번호Policy.TryNormalize(raw, out var number));
        Assert.Equal(expected, number);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" - ")]
    [InlineData("123456")]
    [InlineData("1234567890123456")]
    [InlineData("+")]
    [InlineData("+123456")]
    [InlineData("++1234567")]
    [InlineData("123+4567")]
    [InlineData("+123+4567")]
    public void 숫자길이와_플러스위치가_모호하면_거절한다(string? raw)
        => Reject(raw);

    [Theory]
    [InlineData("１２３４５６７")]
    [InlineData("١٢٣٤٥٦٧")]
    [InlineData("123\u00a04567")]
    [InlineData("123\t4567")]
    [InlineData("123\r\n4567")]
    [InlineData("123\04567")]
    public void 유니코드숫자와_공백이외제어문자는_숫자로_조용히변환하지않는다(string raw)
        => Reject(raw);

    [Theory]
    [InlineData("*1234567#")]
    [InlineData("1234567;1")]
    [InlineData("1234567,1")]
    [InlineData("%2B1234567")]
    [InlineData("tel:1234567")]
    [InlineData("javascript:1234567")]
    [InlineData("1234567?x=1")]
    [InlineData("1234567&x=1")]
    [InlineData("1234567/1")]
    [InlineData("(123)4567")]
    [InlineData("1234567ext1")]
    public void URI_내선_USSD와_다른문자를_포함하면_거절한다(string raw)
        => Reject(raw);

    [Fact]
    public void 원문길이한도를_형식기호제거전에_검사한다()
    {
        Assert.True(업무전화번호Policy.TryNormalize(new string(' ', 41) + "1234567", out var number));
        Assert.Equal("1234567", number);
        Reject(new string(' ', 42) + "1234567");
    }

    [Fact]
    public async Task Web기본값은_전화앱을열었다고_보고하지않고_잘못된번호도_구분한다()
    {
        I업무전화앱Adapter adapter = new 미지원업무전화앱Adapter();
        Assert.False(adapter.IsSupported);
        Assert.Equal(업무전화앱결과.NotSupported, await adapter.OpenAsync("1234567"));
        Assert.Equal(업무전화앱결과.InvalidNumber, await adapter.OpenAsync("tel:1234567"));
    }

    [Fact]
    public async Task 취소된요청은_지원여부결과로_성공처리하지않는다()
    {
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        I업무전화앱Adapter adapter = new 미지원업무전화앱Adapter();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => adapter.OpenAsync("1234567", cancellation.Token));
    }

    private static void Reject(string? raw)
    {
        Assert.False(업무전화번호Policy.TryNormalize(raw, out var number));
        Assert.Equal("", number);
    }
}
