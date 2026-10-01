using Hongdal.Contracts.Common.AgriculturalFisheries;

namespace Hongdal.Services.AgriculturalFisheries.Information;

public interface IAgriculturalFisheriesInformationService
{
    AgriculturalFisheriesInformationOverviewResponse GetOverview();

    AgriculturalFisheriesItemSearchResponse SearchItems(
        string? query,
        string? categoryCode,
        int page,
        int pageSize);

    AgriculturalFisheriesItemResponse? FindItem(string? hsCode);

    Task<AgriculturalFisheriesDomesticPriceResponse> GetDomesticPriceAsync(
        AgriculturalFisheriesDomesticPriceRequest request,
        CancellationToken cancellationToken = default);
}
