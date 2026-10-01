using Ssalddel.Simulation.Contracts;

namespace Ssalddel.Simulation.Persistence;

// 이 행들은 정규화된 읽기 전용 사본이다. 프랜차이즈 공급망 명령은
// Simulation Session Aggregate를 먼저 변경한 뒤 이 사본을 다시 만든다.
public sealed class 프랜차이즈공급망ProjectionEntity
{
    public string SessionStableId { get; set; } = string.Empty;
    public string NetworkStableId { get; set; } = string.Empty;
    public string ScenarioStableId { get; set; } = string.Empty;
    public string SourceKindCode { get; set; } = 프랜차이즈공급망SimulationCodes.검증표본;
    public string ModeCode { get; set; } = SimulationModeCodes.Simulation;
    public string StateAuthorityCode { get; set; } =
        프랜차이즈공급망SimulationCodes.SessionAggregate권위;
    public bool IsSynthetic { get; set; } = true;
    public bool IsOperationalState { get; set; }
    public bool IsReadOnlyProjection { get; set; } = true;
    public string DataRevision { get; set; } = string.Empty;
    public string RuleRevision { get; set; } = string.Empty;
    public string SourceStableIdsJson { get; set; } = "[]";
    public string StatePayloadHashSha256 { get; set; } = string.Empty;
    public long SourceSessionRevision { get; set; }
    public long ProjectionRevision { get; set; }
    public int AsOfWorldTick { get; set; }
    public DateTimeOffset GeneratedAtUtc { get; set; }

    public ICollection<프랜차이즈본부ProjectionEntity> Headquarters { get; set; } = [];
    public ICollection<프랜차이즈매장ProjectionEntity> Stores { get; set; } = [];
    public ICollection<프랜차이즈공급자ProjectionEntity> Suppliers { get; set; } = [];
}

public sealed class 프랜차이즈본부ProjectionEntity
{
    public string SessionStableId { get; set; } = string.Empty;
    public string NetworkStableId { get; set; } = string.Empty;
    public string HeadquartersStableId { get; set; } = string.Empty;
    public string HeadquartersCode { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string StatusCode { get; set; } = 프랜차이즈공급망SimulationCodes.활성;
    public long Revision { get; set; }

    public 프랜차이즈공급망ProjectionEntity Network { get; set; } = null!;
    public ICollection<프랜차이즈매장소속ProjectionEntity> StoreMemberships { get; set; } = [];
    public ICollection<프랜차이즈조달계약ProjectionEntity> SourcingAgreements { get; set; } = [];
    public ICollection<프랜차이즈매장공급안ProjectionEntity> StoreSupplyOffers { get; set; } = [];
}

public sealed class 프랜차이즈매장ProjectionEntity
{
    public string SessionStableId { get; set; } = string.Empty;
    public string NetworkStableId { get; set; } = string.Empty;
    public string StoreStableId { get; set; } = string.Empty;
    public string StoreCode { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string SemanticPlaceStableId { get; set; } = string.Empty;
    public string StatusCode { get; set; } = 프랜차이즈공급망SimulationCodes.활성;
    public long Revision { get; set; }

    public 프랜차이즈공급망ProjectionEntity Network { get; set; } = null!;
    public ICollection<프랜차이즈매장소속ProjectionEntity> Memberships { get; set; } = [];
}

public sealed class 프랜차이즈매장소속ProjectionEntity
{
    public string SessionStableId { get; set; } = string.Empty;
    public string NetworkStableId { get; set; } = string.Empty;
    public string MembershipStableId { get; set; } = string.Empty;
    public string HeadquartersStableId { get; set; } = string.Empty;
    public string StoreStableId { get; set; } = string.Empty;
    public string StatusCode { get; set; } = 프랜차이즈공급망SimulationCodes.활성;
    public int EffectiveFromWorldTick { get; set; }
    public int? EffectiveUntilWorldTick { get; set; }
    public long Revision { get; set; }

    public 프랜차이즈본부ProjectionEntity Headquarters { get; set; } = null!;
    public 프랜차이즈매장ProjectionEntity Store { get; set; } = null!;
    public ICollection<프랜차이즈매장발주ProjectionEntity> Orders { get; set; } = [];
}

public sealed class 프랜차이즈공급자ProjectionEntity
{
    public string SessionStableId { get; set; } = string.Empty;
    public string NetworkStableId { get; set; } = string.Empty;
    public string SupplierStableId { get; set; } = string.Empty;
    public string SupplierKey { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string SupplierKindCode { get; set; } = string.Empty;
    public string StatusCode { get; set; } = 프랜차이즈공급망SimulationCodes.활성;
    public long Revision { get; set; }

    public 프랜차이즈공급망ProjectionEntity Network { get; set; } = null!;
    public ICollection<프랜차이즈조달계약ProjectionEntity> SourcingAgreements { get; set; } = [];
}

public sealed class 프랜차이즈조달계약ProjectionEntity
{
    public string SessionStableId { get; set; } = string.Empty;
    public string NetworkStableId { get; set; } = string.Empty;
    public string ContractStableId { get; set; } = string.Empty;
    public string HeadquartersStableId { get; set; } = string.Empty;
    public string SupplierStableId { get; set; } = string.Empty;
    public string ContractNumber { get; set; } = string.Empty;
    public string ContractDocumentVersion { get; set; } = string.Empty;
    public string StatusCode { get; set; } = 프랜차이즈공급망SimulationCodes.계약초안;
    public int EffectiveFromWorldTick { get; set; }
    public int? EffectiveUntilWorldTick { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public long Revision { get; set; }

    public 프랜차이즈본부ProjectionEntity Headquarters { get; set; } = null!;
    public 프랜차이즈공급자ProjectionEntity Supplier { get; set; } = null!;
    public ICollection<프랜차이즈조달계약품목ProjectionEntity> Items { get; set; } = [];
}

public sealed class 프랜차이즈조달계약품목ProjectionEntity
{
    public string SessionStableId { get; set; } = string.Empty;
    public string NetworkStableId { get; set; } = string.Empty;
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
    public string SourceStableIdsJson { get; set; } = "[]";

    public 프랜차이즈조달계약ProjectionEntity Contract { get; set; } = null!;
    public ICollection<프랜차이즈매장공급안품목ProjectionEntity> StoreOfferItems { get; set; } = [];
}

public sealed class 프랜차이즈매장공급안ProjectionEntity
{
    public string SessionStableId { get; set; } = string.Empty;
    public string NetworkStableId { get; set; } = string.Empty;
    public string OfferStableId { get; set; } = string.Empty;
    public string HeadquartersStableId { get; set; } = string.Empty;
    public string OfferNumber { get; set; } = string.Empty;
    public string OfferDocumentVersion { get; set; } = string.Empty;
    public string CommercialFlowModelCode { get; set; } =
        프랜차이즈공급망SimulationCodes.본부재판매;
    public string FulfillmentModelCode { get; set; } =
        프랜차이즈공급망SimulationCodes.본부자체배송;
    public string StatusCode { get; set; } = 프랜차이즈공급망SimulationCodes.계약초안;
    public int EffectiveFromWorldTick { get; set; }
    public int? EffectiveUntilWorldTick { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public long Revision { get; set; }

    public 프랜차이즈본부ProjectionEntity Headquarters { get; set; } = null!;
    public ICollection<프랜차이즈매장공급안품목ProjectionEntity> Items { get; set; } = [];
    public ICollection<프랜차이즈매장발주ProjectionEntity> Orders { get; set; } = [];
}

public sealed class 프랜차이즈매장공급안품목ProjectionEntity
{
    public string SessionStableId { get; set; } = string.Empty;
    public string NetworkStableId { get; set; } = string.Empty;
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
    public string SourceStableIdsJson { get; set; } = "[]";

    public 프랜차이즈매장공급안ProjectionEntity Offer { get; set; } = null!;
    public 프랜차이즈조달계약품목ProjectionEntity SourceContractItem { get; set; } = null!;
    public ICollection<프랜차이즈매장발주품목ProjectionEntity> OrderLines { get; set; } = [];
}

public sealed class 프랜차이즈매장발주ProjectionEntity
{
    public string SessionStableId { get; set; } = string.Empty;
    public string NetworkStableId { get; set; } = string.Empty;
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
    public string StatusCode { get; set; } = 프랜차이즈공급망SimulationCodes.발주초안;
    public int RequestedDeliveryWorldTick { get; set; }
    public int CreatedWorldTick { get; set; }
    public int? SubmittedWorldTick { get; set; }
    public decimal TotalAmountSnapshot { get; set; }
    public long Revision { get; set; }

    public 프랜차이즈매장소속ProjectionEntity StoreMembership { get; set; } = null!;
    public 프랜차이즈매장공급안ProjectionEntity Offer { get; set; } = null!;
    public ICollection<프랜차이즈매장발주품목ProjectionEntity> Lines { get; set; } = [];
}

public sealed class 프랜차이즈매장발주품목ProjectionEntity
{
    public string SessionStableId { get; set; } = string.Empty;
    public string NetworkStableId { get; set; } = string.Empty;
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

    public 프랜차이즈매장발주ProjectionEntity Order { get; set; } = null!;
    public 프랜차이즈매장공급안품목ProjectionEntity OfferItem { get; set; } = null!;
}
