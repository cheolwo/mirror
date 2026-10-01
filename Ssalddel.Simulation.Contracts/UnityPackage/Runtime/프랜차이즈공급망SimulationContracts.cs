using System;

namespace Ssalddel.Simulation.Contracts
{
    public static class 프랜차이즈공급망SimulationContractCodes
    {
        public const string SchemaVersion = "simulation-franchise-supply.v1";
        public const string RuleRevision = "franchise-supply.rules.r1";
        public const string UnitConversionRevision = "ingredient-package-conversion.r1";
        public const string Initialization = "Initialization";
        public const string SourcingAgreement = "SourcingAgreement";
        public const string StoreSupplyOffer = "StoreSupplyOffer";
        public const string StoreOrder = "StoreOrder";
        public const string FoodIngredientSupplier = "FoodIngredientSupplier";
    }

    public sealed class 프랜차이즈본부SimulationInput
    {
        public string HeadquartersStableId { get; set; } = string.Empty;
        public string HeadquartersCode { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
    }

    public sealed class 프랜차이즈매장SimulationInput
    {
        public string StoreStableId { get; set; } = string.Empty;
        public string StoreCode { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string SemanticPlaceStableId { get; set; } = string.Empty;
        public string MembershipStableId { get; set; } = string.Empty;
    }

    public sealed class 프랜차이즈공급자SimulationInput
    {
        public string SupplierStableId { get; set; } = string.Empty;
        public string SupplierKey { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string SupplierKindCode { get; set; } =
            프랜차이즈공급망SimulationContractCodes.FoodIngredientSupplier;
    }

    public sealed class 프랜차이즈공급망초기화PreviewRequest
    {
        public long ExpectedRevision { get; set; }
        public string NetworkStableId { get; set; } = string.Empty;
        public string DataRevision { get; set; } = string.Empty;
        public 프랜차이즈본부SimulationInput Headquarters { get; set; } = new();
        public 프랜차이즈매장SimulationInput[] Stores { get; set; } =
            Array.Empty<프랜차이즈매장SimulationInput>();
        public 프랜차이즈공급자SimulationInput[] Suppliers { get; set; } =
            Array.Empty<프랜차이즈공급자SimulationInput>();
        public string[] SourceStableIds { get; set; } = Array.Empty<string>();
    }

    public sealed class 프랜차이즈조달계약품목SimulationInput
    {
        public string ContractItemStableId { get; set; } = string.Empty;
        public string ProductStableId { get; set; } = string.Empty;
        public string SupplierSku { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public string OrderUnitCode { get; set; } = string.Empty;
        public decimal PackageContentQuantity { get; set; }
        public string PackageContentUnitCode { get; set; } = string.Empty;
        public string ConversionRuleRevision { get; set; } = string.Empty;
        public decimal UnitPrice { get; set; }
        public decimal MinimumOrderQuantity { get; set; }
        public decimal? MaximumOrderQuantity { get; set; }
        public string StorageConditionCode { get; set; } = string.Empty;
        public string[] SourceStableIds { get; set; } = Array.Empty<string>();
    }

    public sealed class 프랜차이즈조달계약PreviewRequest
    {
        public long ExpectedRevision { get; set; }
        public string ContractStableId { get; set; } = string.Empty;
        public string HeadquartersStableId { get; set; } = string.Empty;
        public string SupplierStableId { get; set; } = string.Empty;
        public string ContractNumber { get; set; } = string.Empty;
        public string ContractDocumentVersion { get; set; } = string.Empty;
        public int EffectiveFromWorldTick { get; set; }
        public int? EffectiveUntilWorldTick { get; set; }
        public string CurrencyCode { get; set; } = "KRW";
        public 프랜차이즈조달계약품목SimulationInput[] Items { get; set; } =
            Array.Empty<프랜차이즈조달계약품목SimulationInput>();
        public string[] SourceStableIds { get; set; } = Array.Empty<string>();
    }

    public sealed class 프랜차이즈매장공급안품목SimulationInput
    {
        public string OfferItemStableId { get; set; } = string.Empty;
        public string SourceContractStableId { get; set; } = string.Empty;
        public string SourceContractItemStableId { get; set; } = string.Empty;
        public string StoreSku { get; set; } = string.Empty;
        public decimal UnitPrice { get; set; }
        public decimal MinimumOrderQuantity { get; set; }
        public decimal? MaximumOrderQuantity { get; set; }
        public string[] SourceStableIds { get; set; } = Array.Empty<string>();
    }

    public sealed class 프랜차이즈매장공급안PreviewRequest
    {
        public long ExpectedRevision { get; set; }
        public string OfferStableId { get; set; } = string.Empty;
        public string HeadquartersStableId { get; set; } = string.Empty;
        public string OfferNumber { get; set; } = string.Empty;
        public string OfferDocumentVersion { get; set; } = string.Empty;
        public int EffectiveFromWorldTick { get; set; }
        public int? EffectiveUntilWorldTick { get; set; }
        public string CurrencyCode { get; set; } = "KRW";
        public 프랜차이즈매장공급안품목SimulationInput[] Items { get; set; } =
            Array.Empty<프랜차이즈매장공급안품목SimulationInput>();
        public string[] SourceStableIds { get; set; } = Array.Empty<string>();
    }

    public sealed class 프랜차이즈매장발주품목SimulationInput
    {
        public string OrderLineStableId { get; set; } = string.Empty;
        public string OfferItemStableId { get; set; } = string.Empty;
        public decimal RequestedOrderUnitQuantity { get; set; }
    }

    public sealed class 프랜차이즈매장발주PreviewRequest
    {
        public long ExpectedRevision { get; set; }
        public string OrderStableId { get; set; } = string.Empty;
        public string StoreMembershipStableId { get; set; } = string.Empty;
        public string OfferStableId { get; set; } = string.Empty;
        public string OrderNumber { get; set; } = string.Empty;
        public string DeliverySiteStableId { get; set; } = string.Empty;
        public int RequestedDeliveryWorldTick { get; set; }
        public 프랜차이즈매장발주품목SimulationInput[] Lines { get; set; } =
            Array.Empty<프랜차이즈매장발주품목SimulationInput>();
        public string[] SourceStableIds { get; set; } = Array.Empty<string>();
    }

    public sealed class 프랜차이즈공급망CommandConfirmRequest<TPreview>
    {
        public string ClientRequestId { get; set; } = string.Empty;
        public string ExpectedPreviewHash { get; set; } = string.Empty;
        public TPreview PreviewRequest { get; set; } = default!;
    }

    public sealed class 프랜차이즈공급망PreviewSnapshot
    {
        public string ActionCode { get; set; } = string.Empty;
        public long ExpectedRevision { get; set; }
        public bool CanConfirm { get; set; }
        public string[] BlockReasonCodes { get; set; } = Array.Empty<string>();
        public string RequestPayloadHashSha256 { get; set; } = string.Empty;
        public string PreviewHash { get; set; } = string.Empty;
    }

    public sealed class 프랜차이즈본부Snapshot
    {
        public string HeadquartersStableId { get; set; } = string.Empty;
        public string HeadquartersCode { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string StatusCode { get; set; } = 프랜차이즈공급망SimulationCodes.활성;
        public long Revision { get; set; }
    }

    public sealed class 프랜차이즈매장Snapshot
    {
        public string StoreStableId { get; set; } = string.Empty;
        public string StoreCode { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string SemanticPlaceStableId { get; set; } = string.Empty;
        public string StatusCode { get; set; } = 프랜차이즈공급망SimulationCodes.활성;
        public long Revision { get; set; }
    }

    public sealed class 프랜차이즈매장소속Snapshot
    {
        public string MembershipStableId { get; set; } = string.Empty;
        public string HeadquartersStableId { get; set; } = string.Empty;
        public string StoreStableId { get; set; } = string.Empty;
        public string StatusCode { get; set; } = 프랜차이즈공급망SimulationCodes.활성;
        public int EffectiveFromWorldTick { get; set; }
        public int? EffectiveUntilWorldTick { get; set; }
        public long Revision { get; set; }
    }

    public sealed class 프랜차이즈공급자Snapshot
    {
        public string SupplierStableId { get; set; } = string.Empty;
        public string SupplierKey { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string SupplierKindCode { get; set; } = string.Empty;
        public string StatusCode { get; set; } = 프랜차이즈공급망SimulationCodes.활성;
        public long Revision { get; set; }
    }

    public sealed class 프랜차이즈조달계약Snapshot
    {
        public string ContractStableId { get; set; } = string.Empty;
        public string HeadquartersStableId { get; set; } = string.Empty;
        public string SupplierStableId { get; set; } = string.Empty;
        public string ContractNumber { get; set; } = string.Empty;
        public string ContractDocumentVersion { get; set; } = string.Empty;
        public string StatusCode { get; set; } = 프랜차이즈공급망SimulationCodes.계약활성;
        public int EffectiveFromWorldTick { get; set; }
        public int? EffectiveUntilWorldTick { get; set; }
        public string CurrencyCode { get; set; } = string.Empty;
        public long Revision { get; set; }
    }

    public sealed class 프랜차이즈조달계약품목Snapshot
    {
        public string ContractItemStableId { get; set; } = string.Empty;
        public string HeadquartersStableId { get; set; } = string.Empty;
        public string ContractStableId { get; set; } = string.Empty;
        public string ProductStableId { get; set; } = string.Empty;
        public string SupplierSku { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public string OrderUnitCode { get; set; } = string.Empty;
        public decimal PackageContentQuantity { get; set; }
        public string PackageContentUnitCode { get; set; } = string.Empty;
        public string ConversionRuleRevision { get; set; } = string.Empty;
        public decimal UnitPrice { get; set; }
        public decimal MinimumOrderQuantity { get; set; }
        public decimal? MaximumOrderQuantity { get; set; }
        public string StorageConditionCode { get; set; } = string.Empty;
        public string StatusCode { get; set; } = 프랜차이즈공급망SimulationCodes.활성;
        public long Revision { get; set; }
        public string[] SourceStableIds { get; set; } = Array.Empty<string>();
    }

    public sealed class 프랜차이즈매장공급안Snapshot
    {
        public string OfferStableId { get; set; } = string.Empty;
        public string HeadquartersStableId { get; set; } = string.Empty;
        public string OfferNumber { get; set; } = string.Empty;
        public string OfferDocumentVersion { get; set; } = string.Empty;
        public string CommercialFlowModelCode { get; set; } =
            프랜차이즈공급망SimulationCodes.본부재판매;
        public string FulfillmentModelCode { get; set; } =
            프랜차이즈공급망SimulationCodes.본부자체배송;
        public string StatusCode { get; set; } = 프랜차이즈공급망SimulationCodes.계약활성;
        public int EffectiveFromWorldTick { get; set; }
        public int? EffectiveUntilWorldTick { get; set; }
        public string CurrencyCode { get; set; } = string.Empty;
        public long Revision { get; set; }
    }

    public sealed class 프랜차이즈매장공급안품목Snapshot
    {
        public string OfferItemStableId { get; set; } = string.Empty;
        public string HeadquartersStableId { get; set; } = string.Empty;
        public string OfferStableId { get; set; } = string.Empty;
        public string SourceContractStableId { get; set; } = string.Empty;
        public string SourceContractItemStableId { get; set; } = string.Empty;
        public string ProductStableId { get; set; } = string.Empty;
        public string StoreSku { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public string OrderUnitCode { get; set; } = string.Empty;
        public decimal PackageContentQuantity { get; set; }
        public string PackageContentUnitCode { get; set; } = string.Empty;
        public string ConversionRuleRevision { get; set; } = string.Empty;
        public decimal UnitPrice { get; set; }
        public decimal MinimumOrderQuantity { get; set; }
        public decimal? MaximumOrderQuantity { get; set; }
        public string StorageConditionCode { get; set; } = string.Empty;
        public string StatusCode { get; set; } = 프랜차이즈공급망SimulationCodes.활성;
        public long Revision { get; set; }
        public string[] SourceStableIds { get; set; } = Array.Empty<string>();
    }

    public sealed class 프랜차이즈매장발주Snapshot
    {
        public string OrderStableId { get; set; } = string.Empty;
        public string HeadquartersStableId { get; set; } = string.Empty;
        public string StoreMembershipStableId { get; set; } = string.Empty;
        public string StoreStableId { get; set; } = string.Empty;
        public string OfferStableId { get; set; } = string.Empty;
        public string ClientRequestStableId { get; set; } = string.Empty;
        public string RequestPayloadHashSha256 { get; set; } = string.Empty;
        public string OrderNumber { get; set; } = string.Empty;
        public string OfferDocumentVersionSnapshot { get; set; } = string.Empty;
        public string SellerHeadquartersStableIdSnapshot { get; set; } = string.Empty;
        public string CommercialFlowModelCodeSnapshot { get; set; } = string.Empty;
        public string FulfillmentModelCodeSnapshot { get; set; } = string.Empty;
        public string DeliverySiteStableIdSnapshot { get; set; } = string.Empty;
        public string CurrencyCodeSnapshot { get; set; } = string.Empty;
        public string StatusCode { get; set; } = 프랜차이즈공급망SimulationCodes.발주제출;
        public int RequestedDeliveryWorldTick { get; set; }
        public int CreatedWorldTick { get; set; }
        public int? SubmittedWorldTick { get; set; }
        public decimal TotalAmountSnapshot { get; set; }
        public long Revision { get; set; }
    }

    public sealed class 프랜차이즈매장발주품목Snapshot
    {
        public string OrderLineStableId { get; set; } = string.Empty;
        public string HeadquartersStableId { get; set; } = string.Empty;
        public string OfferStableId { get; set; } = string.Empty;
        public string OrderStableId { get; set; } = string.Empty;
        public string OfferItemStableId { get; set; } = string.Empty;
        public string ProductStableIdSnapshot { get; set; } = string.Empty;
        public string StoreSkuSnapshot { get; set; } = string.Empty;
        public string ItemNameSnapshot { get; set; } = string.Empty;
        public string OrderUnitCodeSnapshot { get; set; } = string.Empty;
        public decimal PackageContentQuantitySnapshot { get; set; }
        public string PackageContentUnitCodeSnapshot { get; set; } = string.Empty;
        public string ConversionRuleRevisionSnapshot { get; set; } = string.Empty;
        public decimal UnitPriceSnapshot { get; set; }
        public string CurrencyCodeSnapshot { get; set; } = string.Empty;
        public decimal RequestedOrderUnitQuantity { get; set; }
        public decimal RequestedContentQuantitySnapshot { get; set; }
        public decimal? AcceptedOrderUnitQuantity { get; set; }
        public decimal LineAmountSnapshot { get; set; }
        public long Revision { get; set; }
    }

    public sealed class 프랜차이즈공급망StateSnapshot
    {
        public string SchemaVersion { get; set; } =
            프랜차이즈공급망SimulationContractCodes.SchemaVersion;
        public string RuleRevision { get; set; } =
            프랜차이즈공급망SimulationContractCodes.RuleRevision;
        public string NetworkStableId { get; set; } = string.Empty;
        public string ScenarioStableId { get; set; } = string.Empty;
        public string DataRevision { get; set; } = string.Empty;
        public long SourceSessionRevision { get; set; }
        public long ProjectionRevision { get; set; }
        public int AsOfWorldTick { get; set; }
        public bool SimulationOnly { get; set; } = true;
        public bool IsOperationalState { get; set; }
        public string[] SourceStableIds { get; set; } = Array.Empty<string>();
        public 프랜차이즈본부Snapshot[] Headquarters { get; set; } =
            Array.Empty<프랜차이즈본부Snapshot>();
        public 프랜차이즈매장Snapshot[] Stores { get; set; } =
            Array.Empty<프랜차이즈매장Snapshot>();
        public 프랜차이즈매장소속Snapshot[] StoreMemberships { get; set; } =
            Array.Empty<프랜차이즈매장소속Snapshot>();
        public 프랜차이즈공급자Snapshot[] Suppliers { get; set; } =
            Array.Empty<프랜차이즈공급자Snapshot>();
        public 프랜차이즈조달계약Snapshot[] SourcingAgreements { get; set; } =
            Array.Empty<프랜차이즈조달계약Snapshot>();
        public 프랜차이즈조달계약품목Snapshot[] SourcingAgreementItems { get; set; } =
            Array.Empty<프랜차이즈조달계약품목Snapshot>();
        public 프랜차이즈매장공급안Snapshot[] StoreSupplyOffers { get; set; } =
            Array.Empty<프랜차이즈매장공급안Snapshot>();
        public 프랜차이즈매장공급안품목Snapshot[] StoreSupplyOfferItems { get; set; } =
            Array.Empty<프랜차이즈매장공급안품목Snapshot>();
        public 프랜차이즈매장발주Snapshot[] StoreOrders { get; set; } =
            Array.Empty<프랜차이즈매장발주Snapshot>();
        public 프랜차이즈매장발주품목Snapshot[] StoreOrderLines { get; set; } =
            Array.Empty<프랜차이즈매장발주품목Snapshot>();
    }
}
