using Hongdal.Contracts.Common.AgriculturalFisheries;

namespace Hongdal.Services.AgriculturalFisheries.Information;

public interface I호주농수산식품가격조회Service
{
    호주농수산식품가격Catalog응답 GetCatalog();

    Task<호주농수산식품가격조회응답> 조회Async(
        호주농수산식품가격조회요청 request,
        CancellationToken cancellationToken = default);
}
