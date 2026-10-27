using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Common.AgriculturalFisheries;
using Ssalddel.Infrastructure.Persistence.AgriculturalFisheries;
using Ssalddel.Services.AgriculturalFisheries.Information;

namespace Ssalddel.Startup;

/// <summary>가격 수집 명령의 인자 해석과 일회성 실행을 담당합니다.</summary>
internal static class 국제농수산가격수집CommandLine
{
    public static async Task<bool> TryRunAsync(IReadOnlyList<string> arguments, IServiceProvider services, ILogger logger)
    {
        if (!arguments.Any(argument =>
                string.Equals(
                    argument,
                    "--collect-international-agricultural-prices",
                    StringComparison.OrdinalIgnoreCase))) return false;
        var request = ParseArguments(arguments);

        await using var scope = services.CreateAsyncScope();
        var archiveDb = scope.ServiceProvider
            .GetRequiredService<AgriculturalFisheriesDbContext>();
        await archiveDb.Database.MigrateAsync();
        var archiveService =
            scope.ServiceProvider.GetRequiredService<I국제농수산가격ArchiveService>();
        var archiveResult = await archiveService.CollectAsync(
            request);
        logger.LogInformation(
            "국제 농수산 가격 DB 저장 완료. Source={SourceKey}, Range={YearFrom}-{YearTo}, RunId={RunId}, Fetched={Fetched}, Inserted={Inserted}, Updated={Updated}, Existing={Existing}, LatestReferenceDate={LatestReferenceDate}",
            archiveResult.SourceKey,
            archiveResult.YearFrom,
            archiveResult.YearTo,
            archiveResult.CollectionRunId,
            archiveResult.FetchedCount,
            archiveResult.InsertedCount,
            archiveResult.UpdatedCount,
            archiveResult.ExistingCount,
            archiveResult.LatestReferenceDate);
        return true;
    }

    internal static 국제농수산가격수집요청 ParseArguments(IReadOnlyList<string> arguments)
    {
        var sourceArgument = arguments.FirstOrDefault(argument =>
            argument.StartsWith("--source=", StringComparison.OrdinalIgnoreCase));
        var sourceKey = sourceArgument?["--source=".Length..]?.Trim();
        if (string.IsNullOrWhiteSpace(sourceKey))
        {
            throw new InvalidOperationException(
                "--collect-international-agricultural-prices에는 --source가 필요합니다.");
        }

        var yearFromArgument = arguments.FirstOrDefault(argument =>
            argument.StartsWith("--year-from=", StringComparison.OrdinalIgnoreCase));
        var yearFrom = int.TryParse(
            yearFromArgument?["--year-from=".Length..],
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out var parsedYearFrom)
            ? parsedYearFrom
            : 0;
        var yearToArgument = arguments.FirstOrDefault(argument =>
            argument.StartsWith("--year-to=", StringComparison.OrdinalIgnoreCase));
        var yearTo = int.TryParse(
            yearToArgument?["--year-to=".Length..],
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out var parsedYearTo)
            ? parsedYearTo
            : yearFrom;

        return new 국제농수산가격수집요청
        {
            SourceKey = sourceKey,
            YearFrom = yearFrom,
            YearTo = yearTo
        };
    }
}
