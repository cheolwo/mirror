namespace Ssalddel.Ui.Common.Areas.App.Services;

public interface ISsalddelAccessTokenProvider
{
    string? AuthenticationOwnerId => null;
    string? AccessToken { get; }
    Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(AccessToken);
    Task<string?> RefreshAccessTokenAsync(string? rejectedToken, CancellationToken cancellationToken = default)
        => Task.FromResult<string?>(null);
    Task<string?> RefreshAccessTokenAsync(string? rejectedToken, string? expectedOwnerId, CancellationToken cancellationToken = default)
        => RefreshAccessTokenAsync(rejectedToken, cancellationToken);
}

internal sealed class EmptySsalddelAccessTokenProvider : ISsalddelAccessTokenProvider
{
    public string? AccessToken => null;
}
