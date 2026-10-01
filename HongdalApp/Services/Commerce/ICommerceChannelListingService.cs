using Hongdal.Contracts.Common.Sales;

namespace HongdalApp.Services.Commerce;

public interface ICommerceChannelListingService
{
    Task<CommerceChannelListingPreparation> PrepareListingAsync(
        판매채널계정항목응답 account,
        판매상품항목응답 product,
        CancellationToken cancellationToken = default);
}
