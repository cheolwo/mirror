using Hongdal.Contracts.Common.Sales;

namespace 홍달.Services.Sales;

public interface ISalesChannelService
{
    Task<판매채널계정목록응답> GetAccountsAsync(CancellationToken cancellationToken);
    Task<판매채널계정항목응답> CreateAccountAsync(판매채널계정저장요청 request, CancellationToken cancellationToken);
    Task<판매채널계정항목응답> UpdateAccountAsync(long accountId, 판매채널계정저장요청 request, CancellationToken cancellationToken);
    Task DeleteAccountAsync(long accountId, CancellationToken cancellationToken);
    Task<판매상품목록응답> GetProductsAsync(CancellationToken cancellationToken);
    Task<판매상품항목응답> CreateProductAsync(판매상품저장요청 request, CancellationToken cancellationToken);
    Task<판매상품항목응답> UpdateProductAsync(long productId, 판매상품저장요청 request, CancellationToken cancellationToken);
    Task DeleteProductAsync(long productId, CancellationToken cancellationToken);
    Task<판매상품목록응답> SeedSampleProductsAsync(판매상품샘플시드요청 request, CancellationToken cancellationToken);
    Task<채널출품목록응답> GetListingsAsync(CancellationToken cancellationToken);
    Task<채널출품항목응답> CreateListingAsync(채널출품저장요청 request, CancellationToken cancellationToken);
    Task<채널출품항목응답> UpdateListingAsync(long listingId, 채널출품저장요청 request, CancellationToken cancellationToken);
    Task DeleteListingAsync(long listingId, CancellationToken cancellationToken);
}
