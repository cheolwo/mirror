using Ssalddel.Contracts.Common.Settlement;

namespace Ssalddel.Application.Settlement;

public sealed partial class NeighborhoodSettlementUseCase
{
    private static bool Role(NeighborhoodSettlementSourceSnapshot source, string user, string role)
        => source.Participants.Any(x => x.UserId == user && x.Roles.Contains(role, StringComparer.Ordinal));
    private static bool Money(decimal amount) => amount >= 0 && amount <= 1_000_000_000_000m && decimal.Round(amount, 2) == amount;
    private static void OptionalMoney(decimal? amount)
        => Require(amount is null || Money(amount.Value), "NeighborhoodSettlementAmountInvalid", 400);
    private static bool Quantity(decimal quantity) => quantity > 0 && quantity <= 1_000_000_000m && decimal.Round(quantity, 6) == quantity;
    private static void Utc(DateTime? value)
        => Require(value is null || value.Value.Kind == DateTimeKind.Utc, "NeighborhoodSettlementUtcRequired", 400);
    private static decimal? TermTotal(NeighborhoodSettlementTermLine line)
        => line.Billing switch
        {
            NeighborhoodSettlementCodes.Free => 0,
            NeighborhoodSettlementCodes.PerUnit => line.UnitPriceKrw is { } rate && line.Quantity is { } units ? decimal.Round(rate * units, 2) : null,
            _ => line.AmountKrw
        };
    private void ValidateTerms(NeighborhoodSettlementAgreementRequest terms, NeighborhoodSettlementSourceSnapshot source)
    {
        Require(source.SourceKind == terms.SourceKind && source.SourceId == terms.SourceId
            && !string.IsNullOrWhiteSpace(source.SourceVersion) && !string.IsNullOrWhiteSpace(source.SourceDigest),
            "NeighborhoodSettlementSourceEvidenceInvalid");
        Require(source.AllowedActivityKinds.Contains(terms.ActivityKind, StringComparer.Ordinal), "NeighborhoodSettlementActivityUnsupported", 400);
        var expected = terms.SourceKind switch
        {
            NeighborhoodSettlementCodes.FoodOrder => NeighborhoodSettlementCodes.PreparedFood,
            NeighborhoodSettlementCodes.CargoRequest => NeighborhoodSettlementCodes.TransportOnly,
            NeighborhoodSettlementCodes.DistributedLot or NeighborhoodSettlementCodes.DistributedOrder => NeighborhoodSettlementCodes.StoredGoods,
            _ => ""
        };
        Require(expected.Length > 0 && terms.ActivityKind == expected, "NeighborhoodSettlementActivityUnsupported", 400);
        Require(terms.GoodsPaymentRoute is NeighborhoodSettlementCodes.OwnerDirect or NeighborhoodSettlementCodes.SellerCollect,
            "NeighborhoodSettlementGoodsPaymentRouteInvalid", 400);
        // 취급 조건은 공급자의 선언이며 이 검사가 실제 사업·식품 취급 적합성을 인증하지 않습니다.
        Require(terms.Handling is NeighborhoodSettlementCodes.Ambient or NeighborhoodSettlementCodes.Chilled or NeighborhoodSettlementCodes.Frozen,
            "NeighborhoodSettlementHandlingInvalid", 400);
        Utc(terms.ExpiresAtUtc); Require(terms.ExpiresAtUtc is null || terms.ExpiresAtUtc > Now, "NeighborhoodSettlementTermsExpired");
        Require(terms.Lines is { Count: > 0 and <= 64 }, "NeighborhoodSettlementLinesInvalid", 400);
        Require(terms.Lines.Select(x => x.LineKey).Distinct(StringComparer.Ordinal).Count() == terms.Lines.Count, "NeighborhoodSettlementDuplicateLineKey", 400);
        if (!string.IsNullOrWhiteSpace(terms.GoodsOwnerUserId)) Require(Role(source, terms.GoodsOwnerUserId, "Owner"), "NeighborhoodSettlementOwnerMismatch", 403);
        if (!string.IsNullOrWhiteSpace(terms.SellerUserId)) Require(Role(source, terms.SellerUserId, "Seller"), "NeighborhoodSettlementSellerMismatch", 403);
        foreach (var line in terms.Lines)
        {
            Text(line.LineKey, 64); OptionalMoney(line.AmountKrw); OptionalMoney(line.UnitPriceKrw);
            Require(line.DeliveryCostShares is { Count: <= 16 }, "NeighborhoodSettlementDeliverySharesInvalid", 400);
            if (line.DeliveryCostShares.Count > 0)
            {
                Require(line.Kind == NeighborhoodSettlementCodes.Delivery && !string.IsNullOrWhiteSpace(source.CanonicalDeliveryOrigin),
                    "NeighborhoodSettlementDeliverySharesMustReferenceOriginal", 400);
                Require(line.DeliveryCostShares.Select(x => x.PayerUserId).Distinct(StringComparer.Ordinal).Count() == line.DeliveryCostShares.Count,
                    "NeighborhoodSettlementDeliverySharePayerDuplicate", 400);
                foreach (var share in line.DeliveryCostShares)
                {
                    Text(share.PayerUserId); OptionalMoney(share.AmountKrw);
                    Require(new[] { "Buyer", "Owner", "Seller" }.Any(role => Role(source, share.PayerUserId, role)),
                        "NeighborhoodSettlementDeliverySharePartyMismatch", 403);
                }
                var knownShares = line.DeliveryCostShares.Sum(x => x.AmountKrw ?? 0m);
                Require((line.AmountKrw is null || knownShares <= line.AmountKrw) && (source.FrozenDriverFareKrw is null || knownShares <= source.FrozenDriverFareKrw),
                    "NeighborhoodSettlementDeliveryShareTotalMismatch");
                if (line.DeliveryCostShares.All(x => x.AmountKrw.HasValue))
                    Require((line.AmountKrw is null || knownShares == line.AmountKrw) && (source.FrozenDriverFareKrw is null || knownShares == source.FrozenDriverFareKrw),
                        "NeighborhoodSettlementDeliveryShareTotalMismatch");
            }
            Require(line.Quantity is null || Quantity(line.Quantity.Value), "NeighborhoodSettlementQuantityInvalid", 400);
            Utc(line.PeriodStartUtc); Utc(line.PeriodEndUtc);
            Require(line.Kind is NeighborhoodSettlementCodes.ProductSale or NeighborhoodSettlementCodes.OwnerRemittance or NeighborhoodSettlementCodes.SalesAgency
                or NeighborhoodSettlementCodes.Storage or NeighborhoodSettlementCodes.Inbound or NeighborhoodSettlementCodes.Picking or NeighborhoodSettlementCodes.Delivery,
                "NeighborhoodSettlementLineKindInvalid", 400);
            Require(terms.ActivityKind != NeighborhoodSettlementCodes.TransportOnly || line.Kind == NeighborhoodSettlementCodes.Delivery,
                "NeighborhoodSettlementTransportLineUnsupported", 400);
            Require(terms.ActivityKind != NeighborhoodSettlementCodes.PreparedFood || line.Kind is NeighborhoodSettlementCodes.ProductSale
                or NeighborhoodSettlementCodes.OwnerRemittance or NeighborhoodSettlementCodes.SalesAgency or NeighborhoodSettlementCodes.Delivery,
                "NeighborhoodSettlementPreparedFoodLineUnsupported", 400);
            Require(line.Billing is NeighborhoodSettlementCodes.Fixed or NeighborhoodSettlementCodes.Free or NeighborhoodSettlementCodes.PerUnit or NeighborhoodSettlementCodes.StoragePeriod,
                "NeighborhoodSettlementBillingInvalid", 400);
            if (line.Billing == NeighborhoodSettlementCodes.PerUnit)
            {
                Require(line.Quantity.HasValue && line.AmountKrw is null && line.PeriodStartUtc is null && line.PeriodEndUtc is null,
                    "NeighborhoodSettlementPerUnitTermsInvalid", 400);
            }
            else if (line.Billing == NeighborhoodSettlementCodes.StoragePeriod)
            {
                Require(line.Kind == NeighborhoodSettlementCodes.Storage && line.PeriodStartUtc.HasValue && line.PeriodEndUtc.HasValue
                    && line.PeriodStartUtc < line.PeriodEndUtc && line.UnitPriceKrw is null,
                    "NeighborhoodSettlementStoragePeriodRequired", 400);
            }
            else Require(line.UnitPriceKrw is null && line.PeriodStartUtc is null && line.PeriodEndUtc is null,
                "NeighborhoodSettlementBillingTermsInvalid", 400);
            if (line.Billing == NeighborhoodSettlementCodes.Free)
                Require(line.AmountKrw == 0, "NeighborhoodSettlementExplicitFreeRequired", 400);
            if (TermTotal(line) is { } total) Require(Money(total), "NeighborhoodSettlementAmountInvalid", 400);
            if (line.IncludedInDelivery) Require(line.Kind is NeighborhoodSettlementCodes.Inbound or NeighborhoodSettlementCodes.Picking or NeighborhoodSettlementCodes.Delivery,
                "NeighborhoodSettlementIncludedWorkInvalid", 400);
            if (line.IncludedInDelivery && line.Kind != NeighborhoodSettlementCodes.Delivery)
            {
                Require(line.Billing == NeighborhoodSettlementCodes.Free && line.AmountKrw == 0,
                    "NeighborhoodSettlementIncludedWorkMustBeFree", 400);
                Require(terms.Lines.Any(x => x.Kind == NeighborhoodSettlementCodes.Delivery && x.LineKey == line.OriginalLineKey),
                    "NeighborhoodSettlementIncludedDeliveryReferenceRequired", 400);
            }
            var unresolvedParty = string.IsNullOrWhiteSpace(line.PayerUserId) || string.IsNullOrWhiteSpace(line.PayeeUserId) || string.IsNullOrWhiteSpace(line.BeneficiaryRole);
            if (unresolvedParty)
            {
                Require(line.AmountKrw is null && line.UnitPriceKrw is null && line.Billing != NeighborhoodSettlementCodes.Free,
                    "NeighborhoodSettlementUnmappedAmountForbidden", 400);
                continue;
            }
            Text(line.PayerUserId); Text(line.PayeeUserId);
            Require(Party(source, line.PayerUserId) && Role(source, line.PayeeUserId, line.BeneficiaryRole), "NeighborhoodSettlementLinePartyMismatch", 403);
            var payeeRole = line.Kind switch
            {
                NeighborhoodSettlementCodes.ProductSale => terms.GoodsPaymentRoute == NeighborhoodSettlementCodes.OwnerDirect ? "Owner" : "Seller",
                NeighborhoodSettlementCodes.OwnerRemittance => "Owner", NeighborhoodSettlementCodes.SalesAgency => "Seller",
                NeighborhoodSettlementCodes.Storage => "SpaceOwner", NeighborhoodSettlementCodes.Inbound => "Receiver",
                NeighborhoodSettlementCodes.Picking => "Picker", NeighborhoodSettlementCodes.Delivery => "Driver", _ => ""
            };
            Require(line.BeneficiaryRole == payeeRole, "NeighborhoodSettlementBeneficiaryMismatch", 403);
            var payerRoles = line.Kind switch
            {
                NeighborhoodSettlementCodes.ProductSale => new[] { "Buyer" }, NeighborhoodSettlementCodes.OwnerRemittance => ["Seller"],
                NeighborhoodSettlementCodes.SalesAgency => ["Owner"], _ => ["Buyer", "Owner", "Seller"]
            };
            Require(payerRoles.Any(role => Role(source, line.PayerUserId, role)), "NeighborhoodSettlementPayerMismatch", 403);
            if (line.Kind == NeighborhoodSettlementCodes.ProductSale)
            {
                var payee = terms.GoodsPaymentRoute == NeighborhoodSettlementCodes.OwnerDirect ? terms.GoodsOwnerUserId : terms.SellerUserId;
                Require(line.PayeeUserId == payee, "NeighborhoodSettlementGoodsPaymentPartyMismatch", 403);
                if (source.HistoricalSaleTotalKrw is { } price && TermTotal(line) is { } amount)
                    Require(amount == price, "NeighborhoodSettlementHistoricalSaleMismatch");
            }
            if (line.Kind == NeighborhoodSettlementCodes.Delivery && source.CanonicalDeliveryOrigin is not null)
            {
                Require(line.Billing is NeighborhoodSettlementCodes.Fixed or NeighborhoodSettlementCodes.Free, "NeighborhoodSettlementDeliveryMustReferenceOriginal");
                if (source.FrozenDriverFareKrw is { } fare && line.AmountKrw is { } amount)
                    Require(amount == fare, "NeighborhoodSettlementFrozenDeliveryFareMismatch");
            }
        }
        var sale = terms.Lines.Where(x => x.Kind == NeighborhoodSettlementCodes.ProductSale).ToArray();
        Require(sale.Length <= 1, "NeighborhoodSettlementSaleDuplicate", 400);
        var remittances = terms.Lines.Where(x => x.Kind == NeighborhoodSettlementCodes.OwnerRemittance).ToArray();
        if (terms.GoodsPaymentRoute == NeighborhoodSettlementCodes.SellerCollect && sale.Length == 1)
        {
            Require(remittances.Length == 1, "NeighborhoodSettlementOwnerRemittanceRequired", 400);
            var remittance = remittances[0];
            Require(remittance.PayerUserId == terms.SellerUserId && remittance.PayeeUserId == terms.GoodsOwnerUserId
                && remittance.OriginalLineKey == sale[0].LineKey && TermTotal(remittance) == TermTotal(sale[0])
                && remittance.Billing == sale[0].Billing && remittance.Quantity == sale[0].Quantity,
                "NeighborhoodSettlementOwnerRemittanceGrossMismatch", 400);
        }
        else Require(remittances.Length == 0, "NeighborhoodSettlementOwnerRemittanceUnsupported", 400);
    }
    private static bool Resolved(NeighborhoodSettlementTermLine line, NeighborhoodSettlementSourceSnapshot source)
    {
        if (string.IsNullOrWhiteSpace(line.PayerUserId) || string.IsNullOrWhiteSpace(line.PayeeUserId) || string.IsNullOrWhiteSpace(line.BeneficiaryRole)) return false;
        if (line.Kind is NeighborhoodSettlementCodes.ProductSale or NeighborhoodSettlementCodes.OwnerRemittance && source.HistoricalSaleTotalKrw is null) return false;
        if (line.Kind == NeighborhoodSettlementCodes.Delivery && source.CanonicalDeliveryOrigin is not null && source.FrozenDriverFareKrw is null) return false;
        if (line.DeliveryCostShares.Any(x => x.AmountKrw is null)) return false;
        return TermTotal(line).HasValue;
    }
}
