using Hongdal.Contracts.Common.Sales;

namespace HongdalApp.Services.Commerce;

public sealed class CommerceChannelListingService : ICommerceChannelListingService
{
    private readonly ICommerceChannelCatalog _catalog;
    private readonly IEnumerable<IProductListingPayloadBuilder> _payloadBuilders;

    public CommerceChannelListingService(
        ICommerceChannelCatalog catalog,
        IEnumerable<IProductListingPayloadBuilder> payloadBuilders)
    {
        _catalog = catalog;
        _payloadBuilders = payloadBuilders;
    }

    public Task<CommerceChannelListingPreparation> PrepareListingAsync(
        판매채널계정항목응답 account,
        판매상품항목응답 product,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var channel = _catalog.FindByChannelType(account.채널종류);
        if (channel is null)
        {
            return Task.FromResult(new CommerceChannelListingPreparation(
                new CommerceChannelDescriptor(account.채널종류, account.채널종류, "Unknown", false, false, false, "미지원"),
                null,
                SalesStatusCodes.SyncManual,
                "지원하지 않는 판매채널입니다."));
        }

        var builder = _payloadBuilders.FirstOrDefault(x => string.Equals(x.ChannelKey, channel.ChannelKey, StringComparison.OrdinalIgnoreCase));
        if (builder is null)
        {
            return Task.FromResult(new CommerceChannelListingPreparation(
                channel,
                null,
                SalesStatusCodes.SyncPending,
                $"{channel.DisplayName} 상품 payload 매퍼가 아직 연결되지 않았습니다."));
        }

        var payload = builder.BuildPayloadDraft(account, product);
        return Task.FromResult(new CommerceChannelListingPreparation(
            channel,
            payload,
            SalesStatusCodes.SyncReady,
            $"{channel.DisplayName} 상품 API payload 초안을 준비했습니다."));
    }
}
