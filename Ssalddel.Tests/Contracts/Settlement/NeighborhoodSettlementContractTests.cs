using System.Text.Json;
using Ssalddel.Contracts.Common.Settlement;

namespace Ssalddel.Tests.Contracts.Settlement;

public sealed class NeighborhoodSettlementContractTests
{
    [Fact]
    public void 정산약정JSON왕복에서미확인금액과명시한무상금액을구별하고배송비포함과상품송금을보존한다()
    {
        var terms = new NeighborhoodSettlementAgreementRequest
        {
            SourceKind = NeighborhoodSettlementCodes.DistributedLot, SourceId = "source-lot", ActivityKind = NeighborhoodSettlementCodes.StoredGoods,
            GoodsPaymentRoute = NeighborhoodSettlementCodes.SellerCollect, GoodsOwnerUserId = "goods-owner", SellerUserId = "seller",
            Handling = NeighborhoodSettlementCodes.Chilled, Perishable = true, ExpiresAtUtc = DateTime.UtcNow.AddDays(2),
            Lines = [new() { LineKey = "unknown-storage", Kind = NeighborhoodSettlementCodes.Storage, AmountKrw = null },
                new() { LineKey = "free-space", Kind = NeighborhoodSettlementCodes.Storage, Billing = NeighborhoodSettlementCodes.Free, AmountKrw = 0 },
                new() { LineKey = "included-picking", Kind = NeighborhoodSettlementCodes.Picking, AmountKrw = 500, IncludedInDelivery = true },
                new() { LineKey = "owner-remittance", Kind = NeighborhoodSettlementCodes.OwnerRemittance, AmountKrw = 1234.56m, PayerUserId = "seller", PayeeUserId = "goods-owner" },
                new() { LineKey = "shared-delivery", Kind = NeighborhoodSettlementCodes.Delivery, AmountKrw = 4000m,
                    DeliveryCostShares = [new() { PayerUserId = "buyer", AmountKrw = 2000m },
                        new() { PayerUserId = "seller", AmountKrw = null }] }]
        };
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var roundTrip = JsonSerializer.Deserialize<NeighborhoodSettlementAgreementRequest>(JsonSerializer.Serialize(terms, options), options)!;
        Assert.Null(roundTrip.Lines[0].AmountKrw); Assert.Null(roundTrip.Lines[0].Quantity); Assert.Null(roundTrip.Lines[0].UnitPriceKrw);
        Assert.Equal(0, roundTrip.Lines[1].AmountKrw); Assert.Equal(NeighborhoodSettlementCodes.Free, roundTrip.Lines[1].Billing);
        Assert.True(roundTrip.Lines[2].IncludedInDelivery); Assert.Equal(1234.56m, roundTrip.Lines[3].AmountKrw);
        Assert.Equal("seller", roundTrip.Lines[3].PayerUserId); Assert.Equal("goods-owner", roundTrip.Lines[3].PayeeUserId);
        Assert.Equal(NeighborhoodSettlementCodes.Chilled, roundTrip.Handling); Assert.Equal(terms.ExpiresAtUtc, roundTrip.ExpiresAtUtc);
        Assert.Equal(4000m, roundTrip.Lines[4].AmountKrw);
        Assert.Equal(2, roundTrip.Lines[4].DeliveryCostShares.Count);
        Assert.Equal("buyer", roundTrip.Lines[4].DeliveryCostShares[0].PayerUserId);
        Assert.Equal(2000m, roundTrip.Lines[4].DeliveryCostShares[0].AmountKrw);
        Assert.Equal("seller", roundTrip.Lines[4].DeliveryCostShares[1].PayerUserId);
        Assert.Null(roundTrip.Lines[4].DeliveryCostShares[1].AmountKrw);
    }
}
