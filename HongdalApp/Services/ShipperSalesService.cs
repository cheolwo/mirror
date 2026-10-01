using Hongdal.Contracts.Common.Sales;
using HongdalApp.Services.Application;
using HongdalApp.Services.Commerce;
using HongdalApp.Services.Commerce.Listings.Commands;

namespace HongdalApp.Services;

public sealed class ShipperSalesService : IShipperSalesService
{
    private readonly InMemoryShipperStore _store;
    private readonly ICommerceChannelCatalog _channelCatalog;
    private readonly IAppCommandHandler<CreateChannelListingCommand, 채널출품항목응답?> _createListingHandler;

    public ShipperSalesService(
        InMemoryShipperStore store,
        ICommerceChannelCatalog channelCatalog,
        IAppCommandHandler<CreateChannelListingCommand, 채널출품항목응답?> createListingHandler)
    {
        _store = store;
        _channelCatalog = channelCatalog;
        _createListingHandler = createListingHandler;
    }

    public Task<IReadOnlyList<CommerceChannelDescriptor>> GetSupportedChannelsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_channelCatalog.GetSupportedChannels());
    }

    public Task<판매채널계정목록응답?> GetAccountsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<판매채널계정목록응답?>(new 판매채널계정목록응답 { Items = _store.GetAccounts() });
    }

    public Task<판매채널계정항목응답?> CreateAccountAsync(판매채널계정저장요청 payload, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var created = _store.CreateAccount(payload);
        return Task.FromResult<판매채널계정항목응답?>(created);
    }

    public Task<판매채널계정항목응답?> UpdateAccountAsync(
        long accountId,
        판매채널계정저장요청 payload,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<판매채널계정항목응답?>(_store.UpdateAccount(accountId, payload));
    }

    public Task DeleteAccountAsync(long accountId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _store.DeleteAccount(accountId);
        return Task.CompletedTask;
    }

    public Task<판매상품목록응답?> GetProductsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<판매상품목록응답?>(new 판매상품목록응답 { Items = _store.GetProducts() });
    }

    public Task<판매상품항목응답?> CreateProductAsync(판매상품저장요청 payload, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var created = _store.CreateProduct(payload);
        return Task.FromResult<판매상품항목응답?>(created);
    }

    public Task<판매상품항목응답?> UpdateProductAsync(
        long productId,
        판매상품저장요청 payload,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<판매상품항목응답?>(_store.UpdateProduct(productId, payload));
    }

    public Task DeleteProductAsync(long productId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _store.DeleteProduct(productId);
        return Task.CompletedTask;
    }

    public Task<채널출품목록응답?> GetListingsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<채널출품목록응답?>(new 채널출품목록응답 { Items = _store.GetListings() });
    }

    public async Task<채널출품항목응답?> CreateListingAsync(채널출품저장요청 payload, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await _createListingHandler.HandleAsync(new CreateChannelListingCommand(payload), cancellationToken);
    }

    public Task<채널출품항목응답?> UpdateListingAsync(
        long listingId,
        채널출품저장요청 payload,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<채널출품항목응답?>(_store.UpdateListing(listingId, payload));
    }

    public Task DeleteListingAsync(long listingId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _store.DeleteListing(listingId);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<판매채널계정항목응답>> 계정목록조회Async(
        CancellationToken cancellationToken = default)
        => (await GetAccountsAsync(cancellationToken))?.Items ?? [];

    public Task<판매채널계정항목응답?> 계정생성Async(
        판매채널계정저장요청 request,
        CancellationToken cancellationToken = default)
        => CreateAccountAsync(request, cancellationToken);

    public Task<판매채널계정항목응답?> 계정수정Async(
        long accountId,
        판매채널계정저장요청 request,
        CancellationToken cancellationToken = default)
        => UpdateAccountAsync(accountId, request, cancellationToken);

    public Task 계정삭제Async(long accountId, CancellationToken cancellationToken = default)
        => DeleteAccountAsync(accountId, cancellationToken);

    public async Task<IReadOnlyList<판매상품항목응답>> 상품목록조회Async(
        CancellationToken cancellationToken = default)
        => (await GetProductsAsync(cancellationToken))?.Items ?? [];

    public Task<판매상품항목응답?> 상품생성Async(
        판매상품저장요청 request,
        CancellationToken cancellationToken = default)
        => CreateProductAsync(request, cancellationToken);

    public Task<판매상품항목응답?> 상품수정Async(
        long productId,
        판매상품저장요청 request,
        CancellationToken cancellationToken = default)
        => UpdateProductAsync(productId, request, cancellationToken);

    public Task 상품삭제Async(long productId, CancellationToken cancellationToken = default)
        => DeleteProductAsync(productId, cancellationToken);

    public async Task<IReadOnlyList<채널출품항목응답>> 출품목록조회Async(
        CancellationToken cancellationToken = default)
        => (await GetListingsAsync(cancellationToken))?.Items ?? [];

    public Task<채널출품항목응답?> 출품생성Async(
        채널출품저장요청 request,
        CancellationToken cancellationToken = default)
        => CreateListingAsync(request, cancellationToken);

    public Task<채널출품항목응답?> 출품수정Async(
        long listingId,
        채널출품저장요청 request,
        CancellationToken cancellationToken = default)
        => UpdateListingAsync(listingId, request, cancellationToken);

    public Task 출품삭제Async(long listingId, CancellationToken cancellationToken = default)
        => DeleteListingAsync(listingId, cancellationToken);
}
