using Hongdal.Contracts.Common.Sales;
using Hongdal.Ui.Common.Areas.App.Services;
using HongdalApp.Services.Commerce;

namespace HongdalApp.Services;

public interface IShipperSalesService : I판매채널계정Service, I상품등록Service, I채널출품Service
{
    Task<IReadOnlyList<CommerceChannelDescriptor>> GetSupportedChannelsAsync(CancellationToken cancellationToken = default);

    Task<판매채널계정목록응답?> GetAccountsAsync(CancellationToken cancellationToken = default);

    Task<판매채널계정항목응답?> CreateAccountAsync(판매채널계정저장요청 payload, CancellationToken cancellationToken = default);

    Task<판매상품목록응답?> GetProductsAsync(CancellationToken cancellationToken = default);

    Task<판매상품항목응답?> CreateProductAsync(판매상품저장요청 payload, CancellationToken cancellationToken = default);

    Task<채널출품목록응답?> GetListingsAsync(CancellationToken cancellationToken = default);

    Task<채널출품항목응답?> CreateListingAsync(채널출품저장요청 payload, CancellationToken cancellationToken = default);
}
