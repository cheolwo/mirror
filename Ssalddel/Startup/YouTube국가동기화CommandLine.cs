using Microsoft.EntityFrameworkCore;
using Ssalddel.Services.Content;
using 살뜰.Infrastructure;

namespace Ssalddel.Startup;

/// <summary>명시한 국가의 YouTube 채널 동기화 명령과 해당 명령의 DB 준비를 담당합니다.</summary>
internal static class YouTube국가동기화CommandLine
{
    public static async Task<bool> TryRunAsync(IReadOnlyList<string> arguments, IServiceProvider services, ILogger logger)
    {
        var countryCode = ParseCountryCode(arguments);
        if (countryCode is null) return false;

        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SsalddelContext>();
        await db.Database.MigrateAsync();
        var service = scope.ServiceProvider.GetRequiredService<IYouTube채널감시Service>();
        var result = await service.국가별동기화Async(countryCode, CancellationToken.None);
        logger.LogInformation(
            "YouTube 국가별 일회성 동기화 완료. CountryCode={CountryCode}, Executed={Executed}, Channels={Channels}, Received={Received}, Added={Added}, NewUploads={NewUploads}, Message={Message}",
            result.국가코드,
            result.동기화결과.실행됨,
            result.동기화결과.처리채널수,
            result.동기화결과.수신영상수,
            result.동기화결과.추가영상수,
            result.동기화결과.신규업로드수,
            result.동기화결과.메시지);
        return true;
    }

    internal static string? ParseCountryCode(IReadOnlyList<string> arguments)
    {
        var youTubeCountrySyncArgument = arguments.FirstOrDefault(argument =>
            argument.StartsWith("--sync-youtube-country=", StringComparison.OrdinalIgnoreCase));
        if (youTubeCountrySyncArgument is null) return null;

        var countryCode = youTubeCountrySyncArgument["--sync-youtube-country=".Length..];
        if (string.IsNullOrWhiteSpace(countryCode))
        {
            throw new InvalidOperationException(
                "--sync-youtube-country에는 국가 코드가 필요합니다. 예: --sync-youtube-country=JP");
        }
        return countryCode;
    }
}
