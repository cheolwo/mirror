using Microsoft.Extensions.Logging.Abstractions;
using Ssalddel.Startup;

namespace Ssalddel.Tests.Startup;

public sealed class PriceCollectionCommandLineTests
{
    [Theory]
    [InlineData("--collect-bls-average-retail-prices=true")]
    [InlineData("--collect-international-agricultural-prices=true")]
    [InlineData("--year-from=2042")]
    public async Task 명령이아니면_다른인자나서비스를검사하지않는다(string argument)
    {
        var services = new ScopeProbe();
        Assert.False(await Bls평균소매가격수집CommandLine.TryRunAsync([argument], services, NullLogger.Instance));
        Assert.False(await 국제농수산가격수집CommandLine.TryRunAsync([argument], services, NullLogger.Instance));
        Assert.Equal(0, services.ResolveCount);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("+2042")]
    [InlineData(" 2042 ")]
    [InlineData("-1")]
    [InlineData("2147483648")]
    [InlineData("2042.0")]
    [InlineData("invalid")]
    public void 숫자형식실패는_최초인자의기존기본값을사용한다(string value)
    {
        var before = DateTime.UtcNow.Year;
        var bls = Bls평균소매가격수집CommandLine.ParseArguments([$"--year-from={value}", "--year-from=2042", $"--year-to={value}", "--year-to=2045"]);
        Assert.InRange(bls.YearFrom, before, DateTime.UtcNow.Year);
        Assert.Equal(bls.YearFrom, bls.YearTo);
        var international = 국제농수산가격수집CommandLine.ParseArguments(["--source=source", $"--year-from={value}", "--year-from=2042", $"--year-to={value}", "--year-to=2045"]);
        Assert.Equal(0, international.YearFrom);
        Assert.Equal(0, international.YearTo);
    }

    [Theory]
    [InlineData("0000", 0)]
    [InlineData("0042", 42)]
    [InlineData("2042", 2042)]
    [InlineData("2147483647", int.MaxValue)]
    public void 대소문자무관_최초옵션과허용정수를보존한다(string value, int expected)
    {
        var arguments = new[] { "--SOURCE=  Provider-X  ", "--source=ignored", $"--YEAR-FROM={value}", "--year-from=2020", "--YEAR-TO=2045", "--year-to=2050" };
        var bls = Bls평균소매가격수집CommandLine.ParseArguments(arguments);
        var international = 국제농수산가격수집CommandLine.ParseArguments(arguments);
        Assert.Equal(expected, bls.YearFrom); Assert.Equal(2045, bls.YearTo);
        Assert.Equal(expected, international.YearFrom); Assert.Equal(2045, international.YearTo);
        Assert.Equal("Provider-X", international.SourceKey);
    }

    [Theory]
    [InlineData("--source=")]
    [InlineData("--SOURCE=  ")]
    [InlineData("--source-other=x")]
    public async Task 국제가격의source누락은_뒤옵션으로대체하거나DB를준비하지않는다(string argument)
    {
        var services = new ScopeProbe();
        var args = argument.Contains("other", StringComparison.Ordinal)
            ? new[] { "--COLLECT-INTERNATIONAL-AGRICULTURAL-PRICES", argument }
            : new[] { "--COLLECT-INTERNATIONAL-AGRICULTURAL-PRICES", argument, "--source=later" };
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => 국제농수산가격수집CommandLine.TryRunAsync(args, services, NullLogger.Instance));
        Assert.Equal("--collect-international-agricultural-prices에는 --source가 필요합니다.", error.Message);
        Assert.Equal(0, services.ResolveCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 유효한명령만_scope준비를시작하고_실패를감추지않는다(bool international)
    {
        var services = new ScopeProbe();
        var marker = await Assert.ThrowsAsync<ScopeProbeException>(() => international
            ? 국제농수산가격수집CommandLine.TryRunAsync(["--COLLECT-INTERNATIONAL-AGRICULTURAL-PRICES", "--source=x"], services, NullLogger.Instance)
            : Bls평균소매가격수집CommandLine.TryRunAsync(["--COLLECT-BLS-AVERAGE-RETAIL-PRICES"], services, NullLogger.Instance));
        Assert.Same(services.Failure, marker);
        Assert.Equal(1, services.ResolveCount);
    }

    private sealed class ScopeProbeException : Exception { }
    private sealed class ScopeProbe : IServiceProvider
    {
        internal int ResolveCount { get; private set; }
        internal ScopeProbeException Failure { get; } = new();
        public object? GetService(Type serviceType) { ResolveCount++; throw Failure; }
    }
}
