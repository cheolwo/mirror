using Hongdal.Contracts.Common.Content;
using Hongdal.Services.Content;
using Microsoft.Extensions.Options;
using Quartz;
using 홍달.Services.Options;

namespace Hongdal.Infrastructure.BackgroundJobs.Content;

[DisallowConcurrentExecution]
public sealed class YouTube채널동기화Job : IJob
{
    private readonly IYouTube채널감시Service _service;
    private readonly ILogger<YouTube채널동기화Job> _logger;
    private readonly IReadOnlyList<string> _countryCodes;

    public YouTube채널동기화Job(
        IYouTube채널감시Service service,
        ILogger<YouTube채널동기화Job> logger,
        IOptions<YouTubeOptions> options)
    {
        _service = service;
        _logger = logger;
        _countryCodes = (options.Value.CountryCollectionCodes ?? [])
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(YouTube채널수집국가코드.정규화)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    public async Task Execute(IJobExecutionContext context)
    {
        if (_countryCodes.Count == 0)
        {
            var allResult = await _service.동기화Async(null, context.CancellationToken);
            LogResult("ALL", allResult);
            return;
        }

        foreach (var countryCode in _countryCodes)
        {
            var countryResult = await _service.국가별동기화Async(
                countryCode,
                context.CancellationToken);
            LogResult(countryResult.국가코드, countryResult.동기화결과);
        }
    }

    private void LogResult(string countryCode, YouTube채널동기화결과Dto result)
    {
        _logger.LogInformation(
            "Action={Action} CountryCode={CountryCode} Executed={Executed} ChannelCount={ChannelCount} ReceivedVideoCount={ReceivedVideoCount} AddedVideoCount={AddedVideoCount} NewUploadCount={NewUploadCount}",
            "YouTubeCountryChannelsSynced",
            countryCode,
            result.실행됨,
            result.처리채널수,
            result.수신영상수,
            result.추가영상수,
            result.신규업로드수);
    }
}
