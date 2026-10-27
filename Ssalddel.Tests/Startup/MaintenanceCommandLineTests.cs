using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging.Abstractions;
using Ssalddel.Startup;

namespace Ssalddel.Tests.Startup;

public sealed class MaintenanceCommandLineTests
{
    [Fact]
    public async Task 일치하지않는유지관리명령은_인자를검증하거나서비스를준비하지않는다()
    {
        var services = new NoServices();
        Assert.False(await 농수산물품목코드InspectCommandLine.TryRunAsync(
            ["--inspect-agricultural-fisheries-product-codes=true", "--year=1989"], services));
        Assert.False(await YouTube국가동기화CommandLine.TryRunAsync(
            ["--sync-youtube-country"], services, NullLogger.Instance));
        Assert.False(await 관세청Hsk카탈로그ImportCommandLine.TryRunAsync(
            ["--import-kcs-hsk-catalog=true", "--chapters=26"], services, NullLogger.Instance));
        Assert.Equal(0, services.ResolveCount);
    }

    [Theory]
    [InlineData(1990)]
    [InlineData(2100)]
    public void 품목코드조사연도는_허용경계와최초대소문자무관prefix를유지한다(int year)
    {
        Assert.Equal(year, 농수산물품목코드InspectCommandLine.ParseInspectionYear([
            "--yearly=2000", $"--YEAR={year}", "--year=2025"]));
    }

    [Theory]
    [InlineData("1989")]
    [InlineData("2101")]
    [InlineData("0000")]
    public async Task 품목코드조사의범위밖연도는_DB준비전에기존오류로거부한다(string year)
    {
        var services = new NoServices();
        var error = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            농수산물품목코드InspectCommandLine.TryRunAsync(
                ["--INSPECT-AGRICULTURAL-FISHERIES-PRODUCT-CODES", $"--year={year}"], services));
        Assert.Equal("inspectionYear", error.ParamName);
        Assert.Contains("조사 연도는 1990년부터 2100년 사이여야 합니다.", error.Message);
        Assert.Equal(0, services.ResolveCount);
    }

    [Theory]
    [InlineData("+2042")]
    [InlineData(" 2042 ")]
    [InlineData("invalid")]
    [InlineData("")]
    public void 품목코드조사의숫자형식실패는_지역현재연도로돌아가며_뒤옵션으로대체하지않는다(string value)
    {
        var before = DateTime.Now.Year;
        var parsed = 농수산물품목코드InspectCommandLine.ParseInspectionYear([$"--year={value}", "--year=2042"]);
        Assert.InRange(parsed, before, DateTime.Now.Year);
    }

    [Theory]
    [InlineData("--sync-youtube-country=", "")]
    [InlineData("--SYNC-YOUTUBE-COUNTRY=jp", "jp")]
    [InlineData("--sync-youtube-country= JP ", " JP ")]
    public void YouTube국가옵션은_원문과최초prefix를유지한다(string argument, string expected)
    {
        if (expected.Length == 0)
        {
            Assert.Throws<InvalidOperationException>(() => YouTube국가동기화CommandLine.ParseCountryCode([argument, "--sync-youtube-country=KR"]));
            return;
        }
        Assert.Equal(expected, YouTube국가동기화CommandLine.ParseCountryCode([argument, "--sync-youtube-country=KR"]));
        Assert.Null(YouTube국가동기화CommandLine.ParseCountryCode(["--sync-youtube-country", "--sync-youtube-country-other=JP"]));
    }

    [Theory]
    [InlineData("--sync-youtube-country=")]
    [InlineData("--sync-youtube-country=  ")]
    public async Task YouTube국가빈값은_DB준비전에기존오류로거부한다(string argument)
    {
        var services = new NoServices();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            YouTube국가동기화CommandLine.TryRunAsync([argument], services, NullLogger.Instance));
        Assert.Equal("--sync-youtube-country에는 국가 코드가 필요합니다. 예: --sync-youtube-country=JP", error.Message);
        Assert.Equal(0, services.ResolveCount);
    }

    [Fact]
    public void HSK옵션기본값은_UTC현재연도와기존식품류및요청간격을사용한다()
    {
        var before = DateTime.UtcNow.Year;
        var request = 관세청Hsk카탈로그ImportCommandLine.ParseArguments([]);
        Assert.InRange(request.Year, before, DateTime.UtcNow.Year);
        Assert.Equal(Enumerable.Range(1, 25), request.Chapters!);
        Assert.Equal(150, request.RequestDelayMilliseconds); Assert.False(request.Force);
    }

    [Fact]
    public void HSK정수옵션은_부호와공백을허용하고_최초값과명시force를유지한다()
    {
        var request = 관세청Hsk카탈로그ImportCommandLine.ParseArguments([
            "--YEAR= +2042 ", "--year=2025", "--CHAPTERS=10,01-03,10", "--chapters=25",
            "--REQUEST-DELAY-MS= +300 ", "--request-delay-ms=999", "--FORCE"]);
        Assert.Equal(2042, request.Year); Assert.Equal([1, 2, 3, 10], request.Chapters!);
        Assert.Equal(300, request.RequestDelayMilliseconds); Assert.True(request.Force);
        Assert.False(관세청Hsk카탈로그ImportCommandLine.ParseArguments(["--force=true"]).Force);
    }

    [Fact]
    public void HSK숫자형식실패와빈chapters는_최초옵션의기본값을유지한다()
    {
        var before = DateTime.UtcNow.Year;
        var request = 관세청Hsk카탈로그ImportCommandLine.ParseArguments([
            "--year=invalid", "--year=2042", "--request-delay-ms=invalid", "--request-delay-ms=300", "--chapters="]);
        Assert.InRange(request.Year, before, DateTime.UtcNow.Year);
        Assert.Equal(150, request.RequestDelayMilliseconds);
        Assert.Equal(Enumerable.Range(1, 25), request.Chapters!);
    }

    [Fact]
    public async Task HSK식품류범위실패는_명령선택후DB준비전에거부한다()
    {
        var services = new NoServices();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => 관세청Hsk카탈로그ImportCommandLine.TryRunAsync(
            ["--IMPORT-KCS-HSK-CATALOG", "--chapters=26"], services, NullLogger.Instance));
        Assert.Equal(0, services.ResolveCount);
    }

    [Fact]
    public void Program은_원래명령우선순위와조기return및일반초기화위치를유지한다()
    {
        var program = Read("Ssalddel", "Program.cs");
        var orderedMarkers = new[]
        {
            "var initializeDatabaseOnly = args.Any", "var app = builder.Build();",
            "대한민국공간공공데이터CommandLine.TryRunAsync", "--preview-common-food-product-reconciliation",
            "농수산물품목코드InspectCommandLine.TryRunAsync", "YouTube국가동기화CommandLine.TryRunAsync",
            "Bls평균소매가격수집CommandLine.TryRunAsync", "국제농수산가격수집CommandLine.TryRunAsync", "UsdaAms공개사업체CommandLine.TryRunAsync",
            "--index-official-food-ingredient-prices", "관세청Hsk카탈로그ImportCommandLine.TryRunAsync",
            "--index-official-food-ingredient-hs-codes", "var officialFoodRecipeSourceKey = args.Any",
            "if (databaseInitializationOptions.RunAtStartup || initializeDatabaseOnly)",
            "DatabaseCompatibilityInitializer.InitializeAsync", "if (initializeDatabaseOnly)", "app.UseRouting();"
        };
        var previous = -1;
        foreach (var marker in orderedMarkers)
        {
            var index = program.IndexOf(marker, StringComparison.Ordinal);
            Assert.True(index > previous, $"기존 명령/초기화 순서를 유지해야 합니다: {marker}");
            previous = index;
        }
        foreach (var commandLine in new[] { "농수산물품목코드InspectCommandLine", "YouTube국가동기화CommandLine", "Bls평균소매가격수집CommandLine", "국제농수산가격수집CommandLine", "관세청Hsk카탈로그ImportCommandLine" })
        {
            Assert.Matches(new Regex($@"if \(await {commandLine}\.TryRunAsync\([^\r\n]*\)\)\s*\{{\s*return;\s*\}}"), program);
        }
        Assert.Contains("databaseInitializationOptions.FailOnError || initializeDatabaseOnly", program);
        Assert.Contains("if (developmentReadOnly)", program); Assert.Contains("if (isTestingEnvironment)", program);
        Assert.DoesNotContain("--inspect-agricultural-fisheries-product-codes", program);
        Assert.DoesNotContain("--sync-youtube-country=", program);
        Assert.DoesNotContain("--import-kcs-hsk-catalog", program);
    }

    [Fact]
    public void 품목조사는migration없이읽고_국가동기화와HSK는기존DB준비후실행한다()
    {
        var inspect = Read("Ssalddel", "Startup", "농수산물품목코드InspectCommandLine.cs");
        Assert.DoesNotContain("MigrateAsync", inspect); Assert.Contains(".AsNoTracking()", inspect);
        Assert.Contains("Console.WriteLine(JsonSerializer.Serialize(", inspect);
        Assert.Contains("new JsonSerializerOptions { WriteIndented = true }", inspect);
        foreach (var (source, execution) in new[]
        {
            (Read("Ssalddel", "Startup", "YouTube국가동기화CommandLine.cs"), "service.국가별동기화Async(countryCode, CancellationToken.None)"),
            (Read("Ssalddel", "Startup", "관세청Hsk카탈로그ImportCommandLine.cs"), "importService.ImportAsync(request)")
        })
        {
            var scope = source.IndexOf("services.CreateAsyncScope()", StringComparison.Ordinal);
            var migrate = source.IndexOf("await db.Database.MigrateAsync();", StringComparison.Ordinal);
            Assert.True(scope >= 0 && migrate > scope);
            Assert.True(source.IndexOf(execution, StringComparison.Ordinal) > migrate);
            Assert.Equal(1, Regex.Matches(source, "MigrateAsync").Count);
        }
    }

    private sealed class NoServices : IServiceProvider
    {
        public int ResolveCount { get; private set; }
        public object? GetService(Type serviceType)
        {
            ResolveCount++;
            throw new InvalidOperationException($"실행하지 않는 명령이 서비스를 요청했습니다: {serviceType.Name}");
        }
    }

    private static string Read(params string[] parts)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Ssalddel.slnx"))) current = current.Parent;
        if (current is null) throw new DirectoryNotFoundException("저장소 루트를 찾을 수 없습니다.");
        return File.ReadAllText(Path.Combine(new[] { current.FullName }.Concat(parts).ToArray()));
    }
}
