using Hongdal.Contracts.CommandSettings;

namespace DriverApp.Services;

public interface IDriverCommandFeatureSettingsApiService
{
    Task<Command기능설정목록응답?> 목록조회Async(CancellationToken cancellationToken = default);
    Task 수정Async(
        string commandName,
        string featureName,
        Command기능설정수정요청 request,
        CancellationToken cancellationToken = default);
    Task 기본값복원Async(
        string commandName,
        string featureName,
        CancellationToken cancellationToken = default);
}

public sealed class DriverCommandFeatureSettingsApiService : IDriverCommandFeatureSettingsApiService
{
    private const string BasePath = "api/v1/driver/command-feature-settings";
    private readonly IDriverApiClient _client;

    public DriverCommandFeatureSettingsApiService(IDriverApiClient client)
    {
        _client = client;
    }

    public Task<Command기능설정목록응답?> 목록조회Async(CancellationToken cancellationToken = default)
        => _client.GetAsync<Command기능설정목록응답>(
            BasePath,
            "기사 Command 기능 설정 목록 조회",
            cancellationToken);

    public Task 수정Async(
        string commandName,
        string featureName,
        Command기능설정수정요청 request,
        CancellationToken cancellationToken = default)
        => _client.PutAsync(
            BuildPath(commandName, featureName),
            request,
            "기사 Command 기능 설정 수정",
            cancellationToken);

    public Task 기본값복원Async(
        string commandName,
        string featureName,
        CancellationToken cancellationToken = default)
        => _client.DeleteAsync(
            BuildPath(commandName, featureName),
            "기사 Command 기능 설정 기본값 복원",
            cancellationToken);

    private static string BuildPath(string commandName, string featureName)
        => $"{BasePath}/{Uri.EscapeDataString(commandName)}/{Uri.EscapeDataString(featureName)}";
}
