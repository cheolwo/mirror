using System.Globalization;
using System.Net.Http.Json;
using Ssalddel.Contracts.Common.Home;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;

public interface IHomeParticipationDetailsClient
{
    Task<HomeParticipationDetailsResponse> LoadAsync(int page, CancellationToken cancellationToken = default);
}

/// <summary>공개 월별 집계만 읽습니다. 역할 자격이나 개별 운송 원장을 전달하지 않습니다.</summary>
public sealed class HomeParticipationDetailsClient(IHttpClientFactory clients) : IHomeParticipationDetailsClient
{
    public async Task<HomeParticipationDetailsResponse> LoadAsync(int page, CancellationToken cancellationToken = default)
    {
        if (page is < 1 or > 10000) throw new ArgumentOutOfRangeException(nameof(page));
        var client = clients.CreateClient(SsalddelHttpClientNames.OperationalApi);
        var response = await client.GetFromJsonAsync<HomeParticipationDetailsResponse>(
            "api/v1/common/home/participation-details?page=" + page.ToString(CultureInfo.InvariantCulture), cancellationToken)
            ?? throw new InvalidDataException("HomeParticipationDetailsEmpty");
        var summary = response.Summary;
        if (summary is null || summary.PeriodCode != "all-time" || summary.StatusCode != "recorded"
            || summary.ExecutionMode is not ("Simulation" or "Operational") || summary.AsOfUtc == default
            || summary.CompletedFreightTransportCount is not >= 0
            || summary.FreightShipperAccountCount is not >= 0 || summary.FreightDriverAccountCount is not >= 0
            || summary.FreightShipperAccountCount > summary.CompletedFreightTransportCount
            || summary.FreightDriverAccountCount > summary.CompletedFreightTransportCount
            || response.Page != page || response.PageSize != 12 || response.MonthlyPeriodBasisCode != "arrival-month-kst"
            || response.MonthlyItems is null || response.MonthlyItems.Count > 12
            || response.HasMore && response.MonthlyItems.Count != 12)
            throw new InvalidDataException("HomeParticipationDetailsInvalid");
        string? previous = null;
        long completed = 0;
        foreach (var item in response.MonthlyItems)
        {
            if (item is null || !DateTime.TryParseExact(item.MonthCode, "yyyy-MM", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out _) || previous is not null && string.CompareOrdinal(previous, item.MonthCode) <= 0
                || item.CompletedFreightTransportCount <= 0 || item.FreightShipperAccountCount < 0 || item.FreightDriverAccountCount < 0
                || item.FreightShipperAccountCount > summary.FreightShipperAccountCount
                || item.FreightDriverAccountCount > summary.FreightDriverAccountCount
                || item.FreightShipperAccountCount > item.CompletedFreightTransportCount
                || item.FreightDriverAccountCount > item.CompletedFreightTransportCount)
                throw new InvalidDataException("HomeParticipationMonthInvalid");
            previous = item.MonthCode;
            completed += item.CompletedFreightTransportCount;
        }
        if (completed > summary.CompletedFreightTransportCount
            || page == 1 && !response.HasMore && completed != summary.CompletedFreightTransportCount)
            throw new InvalidDataException("HomeParticipationMonthExceedsTotal");
        return response;
    }
}
