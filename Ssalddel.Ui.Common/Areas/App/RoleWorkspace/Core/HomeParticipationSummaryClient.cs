using System.Net.Http.Json;
using Ssalddel.Contracts.Common.Home;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;

public interface IHomeParticipationSummaryClient
{
    Task<HomeParticipationSummaryResponse?> LoadAsync(CancellationToken cancellationToken = default);
}

/// <summary>공개 누적 집계만 읽으며 역할 자격이나 관리자 원본 응답을 사용하지 않습니다.</summary>
public sealed class HomeParticipationSummaryClient(IHttpClientFactory clients) : IHomeParticipationSummaryClient
{
    public async Task<HomeParticipationSummaryResponse?> LoadAsync(CancellationToken cancellationToken = default)
    {
        var client = clients.CreateClient(SsalddelHttpClientNames.OperationalApi);
        var summary = await client.GetFromJsonAsync<HomeParticipationSummaryResponse>(
            "api/v1/common/home/participation-summary", cancellationToken)
            ?? throw new InvalidDataException("HomeParticipationSummaryEmpty");
        if (summary.PeriodCode != "all-time"
            || summary.StatusCode != "recorded"
            || summary.ExecutionMode is not ("Simulation" or "Operational")
            || summary.AsOfUtc == default
            || summary.CompletedFreightTransportCount < 0
            || summary.FreightShipperAccountCount < 0
            || summary.FreightDriverAccountCount < 0
            || summary.FreightShipperAccountCount > summary.CompletedFreightTransportCount
            || summary.FreightDriverAccountCount > summary.CompletedFreightTransportCount)
            throw new InvalidDataException("HomeParticipationSummaryInvalid");
        return summary;
    }
}
