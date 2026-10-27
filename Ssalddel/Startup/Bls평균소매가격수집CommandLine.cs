using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Common.AgriculturalFisheries;
using Ssalddel.Infrastructure.Persistence.AgriculturalFisheries;
using Ssalddel.Services.AgriculturalFisheries.Information;

namespace Ssalddel.Startup;

/// <summary>가격 수집 명령의 인자 해석과 일회성 실행을 담당합니다.</summary>
internal static class Bls평균소매가격수집CommandLine
{
    public static async Task<bool> TryRunAsync(IReadOnlyList<string> arguments, IServiceProvider services, ILogger logger)
    {
        if (!arguments.Any(argument =>
                string.Equals(
                    argument,
                    "--collect-bls-average-retail-prices",
                    StringComparison.OrdinalIgnoreCase))) return false;
        var request = ParseArguments(arguments);

        await using var scope = services.CreateAsyncScope();
        var archiveDb = scope.ServiceProvider.GetRequiredService<AgriculturalFisheriesDbContext>();
        await archiveDb.Database.MigrateAsync();
        var archiveService =
            scope.ServiceProvider.GetRequiredService<IBls평균소매가격ArchiveService>();
        var archiveResult = await archiveService.CollectAsync(
            request);
        logger.LogInformation(
            "BLS 미국 평균 소매가격 DB 저장 완료. Range={YearFrom}-{YearTo}, Series={Series}, RunId={RunId}, Fetched={Fetched}, Inserted={Inserted}, Updated={Updated}, Existing={Existing}, LatestReferenceMonth={LatestReferenceMonth}",
            archiveResult.YearFrom,
            archiveResult.YearTo,
            archiveResult.RequestedSeriesCount,
            archiveResult.CollectionRunId,
            archiveResult.FetchedCount,
            archiveResult.InsertedCount,
            archiveResult.UpdatedCount,
            archiveResult.ExistingCount,
            archiveResult.LatestReferenceMonth);
        return true;
    }

    internal static Bls평균소매가격수집요청 ParseArguments(IReadOnlyList<string> arguments)
    {
        var yearFromArgument = arguments.FirstOrDefault(argument =>
            argument.StartsWith("--year-from=", StringComparison.OrdinalIgnoreCase));
        var yearFrom = int.TryParse(
            yearFromArgument?["--year-from=".Length..],
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out var parsedYearFrom)
            ? parsedYearFrom
            : DateTime.UtcNow.Year;
        var yearToArgument = arguments.FirstOrDefault(argument =>
            argument.StartsWith("--year-to=", StringComparison.OrdinalIgnoreCase));
        var yearTo = int.TryParse(
            yearToArgument?["--year-to=".Length..],
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out var parsedYearTo)
            ? parsedYearTo
            : yearFrom;

        return new Bls평균소매가격수집요청
        {
            YearFrom = yearFrom,
            YearTo = yearTo
        };
    }
}
