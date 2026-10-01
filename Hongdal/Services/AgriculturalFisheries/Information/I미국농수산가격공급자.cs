using Hongdal.Contracts.Common.AgriculturalFisheries;

namespace Hongdal.Services.AgriculturalFisheries.Information;

public interface I미국농수산가격공급자
{
    string SourceKey { get; }

    string ProviderName { get; }

    string DocumentationUrl { get; }

    bool IsConfigured { get; }

    Task<미국농수산가격조회응답> 조회Async(
        미국농수산가격조회요청 request,
        CancellationToken cancellationToken = default);
}
