using Hongdal.Contracts.Common.Sales;
using System.Text.Json.Nodes;

namespace HongdalApp.Services.Commerce;

public interface IProductListingPayloadBuilder
{
    string ChannelKey { get; }

    JsonNode BuildPayloadDraft(판매채널계정항목응답 account, 판매상품항목응답 product);
}
