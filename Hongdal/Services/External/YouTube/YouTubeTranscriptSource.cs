using Hongdal.Contracts.Common.Content;

namespace Hongdal.Services.External.YouTube;

public interface IYouTubeTranscriptSource
{
    bool IsEnabled { get; }

    Task<YouTubeTranscriptResponse?> GetAsync(
        YouTubeTranscriptRequest request,
        CancellationToken cancellationToken);
}
