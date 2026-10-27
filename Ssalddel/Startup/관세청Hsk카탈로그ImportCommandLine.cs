using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Services.Customs;
using 살뜰.Infrastructure;

namespace Ssalddel.Startup;

/// <summary>관세청 HSK 카탈로그 일회성 가져오기의 옵션과 실행 순서를 담당합니다.</summary>
internal static class 관세청Hsk카탈로그ImportCommandLine
{
    internal const string Command = "--import-kcs-hsk-catalog";

    public static async Task<bool> TryRunAsync(IReadOnlyList<string> arguments, IServiceProvider services, ILogger logger)
    {
        if (!arguments.Any(argument => string.Equals(argument, Command, StringComparison.OrdinalIgnoreCase)))
            return false;

        var request = ParseArguments(arguments);
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SsalddelContext>();
        await db.Database.MigrateAsync();
        var importService = scope.ServiceProvider.GetRequiredService<IKcsHskCatalogImportService>();
        var result = await importService.ImportAsync(request);
        logger.LogInformation(
            "관세청 HSK 식품 카탈로그 처리 완료. Imported={Imported}, VersionId={VersionId}, Entries={Entries}, Added={Added}, Updated={Updated}, Deactivated={Deactivated}, Requests={Requests}, EffectiveFrom={EffectiveFrom:yyyy-MM-dd}",
            result.Imported,
            result.CatalogVersionId,
            result.EntryCount,
            result.AddedCount,
            result.UpdatedCount,
            result.DeactivatedCount,
            result.RequestCount,
            result.EffectiveFrom);
        return true;
    }

    internal static KcsHskCatalogImportRequest ParseArguments(IReadOnlyList<string> arguments)
    {
        var yearArgument = arguments.FirstOrDefault(argument =>
            argument.StartsWith("--year=", StringComparison.OrdinalIgnoreCase));
        var year = int.TryParse(
            yearArgument?["--year=".Length..],
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var parsedYear)
            ? parsedYear
            : DateTime.UtcNow.Year;
        var chaptersArgument = arguments.FirstOrDefault(argument =>
            argument.StartsWith("--chapters=", StringComparison.OrdinalIgnoreCase));
        var chapters = KcsHskFoodChapterSelection.Parse(chaptersArgument?["--chapters=".Length..]);
        var delayArgument = arguments.FirstOrDefault(argument =>
            argument.StartsWith("--request-delay-ms=", StringComparison.OrdinalIgnoreCase));
        var requestDelayMilliseconds = int.TryParse(
            delayArgument?["--request-delay-ms=".Length..],
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var parsedRequestDelayMilliseconds)
            ? parsedRequestDelayMilliseconds
            : 150;
        var force = arguments.Any(argument => string.Equals(
            argument,
            "--force",
            StringComparison.OrdinalIgnoreCase));
        return new KcsHskCatalogImportRequest(year, chapters, requestDelayMilliseconds, force);
    }
}
