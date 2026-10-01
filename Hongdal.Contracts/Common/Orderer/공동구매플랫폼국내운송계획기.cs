namespace Hongdal.Contracts.Common.Orderer;

public static class 공동구매국내운송의뢰주체유형코드
{
    public const string 플랫폼 = "Platform";
    public const string 주문자집단 = "OrdererGroup";
}

public static class 공동구매국내운송비용부담주체유형코드
{
    public const string 주문자집단 = "OrdererGroup";
    public const string 플랫폼 = "Platform";
}

public static class 공동구매국내운송정산정책코드
{
    public const string PlatformCollectsOrdererPaymentsAndPaysDriverAfterDropoff = "PlatformCollectsOrdererPaymentsAndPaysDriverAfterDropoff";
    public const string PlatformPaysAndRechargesOrdererGroup = "PlatformPaysAndRechargesOrdererGroup";
    public const string PlatformAbsorbsAsPromotion = "PlatformAbsorbsAsPromotion";
    public const string ManualSettlement = "ManualSettlement";
}

public static class 공동구매국내운송주문자결제수단코드
{
    public const string Card = "Card";
    public const string CashLike = "CashLike";
    public const string BankTransfer = "BankTransfer";
    public const string PlatformCredit = "PlatformCredit";
}

public static class 공동구매국내운송기사지급트리거코드
{
    public const string DropoffCompleted = "DropoffCompleted";
    public const string PickupAndDropoffEvidenceVerified = "PickupAndDropoffEvidenceVerified";
    public const string ManualAdminApproval = "ManualAdminApproval";
}

public static class 공동구매국내운송기사지급계좌정책코드
{
    public const string DriverRegisteredSettlementAccount = "DriverRegisteredSettlementAccount";
}

public static class 공동구매국내운송증빙코드
{
    public const string PickupPhoto = "PickupPhoto";
    public const string DropoffPhoto = "DropoffPhoto";
    public const string DropoffCompletion = "DropoffCompletion";
    public const string Receipt = "Receipt";
    public const string CashReceipt = "CashReceipt";
}

public static class 공동구매국내운송방식코드
{
    public const string Auto = "Auto";
    public const string FCL = "Fcl";
    public const string LCL = "Lcl";
    public const string GeneralCargo = "GeneralCargo";
}

public static class 공동구매국내운송원천의뢰유형코드
{
    public const string ImportCargoTransport = "ImportCargoTransport";
    public const string FclCargoTransport = "FclCargoTransport";
    public const string LclCargoTransport = "LclCargoTransport";
}

public static class 공동구매국내운송도착지유형코드
{
    public const string ThreePlWarehouse = "ThreePlWarehouse";
    public const string DedicatedWarehouse = "DedicatedWarehouse";
    public const string ApartmentComplexDirectDistribution = "ApartmentComplexDirectDistribution";
    public const string OrdererGroupRepresentativeDropoff = "OrdererGroupRepresentativeDropoff";
}

public static class 공동구매공동주택세대배송방식코드
{
    public const string None = "None";
    public const string DriverToDesignatedPickupPoint = "DriverToDesignatedPickupPoint";
    public const string DriverToBuildingEntrance = "DriverToBuildingEntrance";
    public const string DriverToUnitDoor = "DriverToUnitDoor";
}

public static class 공동구매세대배송개인정보방식코드
{
    public const string MaskedUnitTokens = "MaskedUnitTokens";
    public const string FullUnitAddressAfterAssignment = "FullUnitAddressAfterAssignment";
    public const string ManualChecklist = "ManualChecklist";
}

public static class 공동구매세대별분류방식코드
{
    public const string NotRequired = "NotRequired";
    public const string ByBuilding = "ByBuilding";
    public const string ByBuildingAndUnit = "ByBuildingAndUnit";
    public const string ByRouteSequence = "ByRouteSequence";
}

public static class 공동구매세대별분류장소코드
{
    public const string BondedArea = "BondedArea";
    public const string ThreePlWarehouse = "ThreePlWarehouse";
    public const string ApartmentStagingArea = "ApartmentStagingArea";
}

public static class 공동구매세대별분류책임주체코드
{
    public const string OverseasSeller = "OverseasSeller";
    public const string OverseasForwarder = "OverseasForwarder";
    public const string 플랫폼 = "Platform";
    public const string 주문자집단 = "OrdererGroup";
    public const string DomesticOperator = "DomesticOperator";
}

public static class 공동구매단위포장라벨링방식코드
{
    public const string 상품정보스티커 = "ProductInfoSticker";
    public const string 세대별송장라벨 = "UnitInvoiceLabel";
    public const string 단위라벨없음 = "NoUnitLabel";
}

public static class 공동구매상품정보보관위치코드
{
    public const string OverseasSellerSystem = "OverseasSellerSystem";
    public const string OverseasForwarderSystem = "OverseasForwarderSystem";
    public const string PlatformImportLedger = "PlatformImportLedger";
    public const string ManualDocumentArchive = "ManualDocumentArchive";
}

public static class 공동구매세대단위바코드체계코드
{
    public const string OrderNumberBarcode = "OrderNumberBarcode";
    public const string InvoiceNumberBarcode = "InvoiceNumberBarcode";
    public const string PackageIdBarcode = "PackageIdBarcode";
}

public static class 공동구매공동주택분배책임코드
{
    public const string None = "None";
    public const string Driver = "Driver";
    public const string 주문자집단 = "OrdererGroup";
    public const string SeparateWorker = "SeparateWorker";
    public const string PlatformArrangedWorker = "PlatformArrangedWorker";
}

public static class 공동구매국내운송비용옵션상태코드
{
    public const string Selectable = "Selectable";
    public const string NeedsConfirmation = "NeedsConfirmation";
    public const string NotCompatible = "NotCompatible";
}

public static class 공동구매국내운송필요조치코드
{
    public const string ConfirmPlatformShipperProfile = "ConfirmPlatformShipperProfile";
    public const string ConfirmCustomsReleaseOrBondedRelease = "ConfirmCustomsReleaseOrBondedRelease";
    public const string ConfirmBondedAreaPickupAddress = "ConfirmBondedAreaPickupAddress";
    public const string ConfirmThreePlDropoffAddress = "ConfirmThreePlDropoffAddress";
    public const string ConfirmApartmentComplexDropoffAddress = "ConfirmApartmentComplexDropoffAddress";
    public const string ConfirmApartmentUnitDistributionPlan = "ConfirmApartmentUnitDistributionPlan";
    public const string ConfirmRecipientAddressPrivacy = "ConfirmRecipientAddressPrivacy";
    public const string ConfirmCargoSpecification = "ConfirmCargoSpecification";
    public const string ConfirmPlatformEntrustedTransport = "ConfirmPlatformEntrustedTransport";
    public const string ConfirmOrdererPaymentCollection = "ConfirmOrdererPaymentCollection";
    public const string ConfirmDriverSettlementAccount = "ConfirmDriverSettlementAccount";
    public const string ConfirmDriverPayoutPolicy = "ConfirmDriverPayoutPolicy";
    public const string ConfirmColdChainVehicle = "ConfirmColdChainVehicle";
    public const string ConfirmColdChainThreePlFacility = "ConfirmColdChainThreePlFacility";
    public const string ConfirmDistributionResponsibility = "ConfirmDistributionResponsibility";
    public const string ConfirmTransportDecisionRevision = "ConfirmTransportDecisionRevision";
    public const string ConfirmUnitSortationBeforePickup = "ConfirmUnitSortationBeforePickup";
    public const string ConfirmOverseasUnitInvoiceAndLabeling = "ConfirmOverseasUnitInvoiceAndLabeling";
    public const string ConfirmImportedProductInfoRegistration = "ConfirmImportedProductInfoRegistration";
    public const string ConfirmProductInfoStickerStorage = "ConfirmProductInfoStickerStorage";
    public const string ConfirmUnitProductInfoSticker = "ConfirmUnitProductInfoSticker";
    public const string ConfirmUnitBarcodeScanLookup = "ConfirmUnitBarcodeScanLookup";
}

public sealed class 공동구매플랫폼국내운송초안요청
{
    public string PlatformShipperUserId { get; set; } = "platform";
    public string PlatformLegalEntityName { get; set; } = string.Empty;
    public string TransportMode { get; set; } = 공동구매국내운송방식코드.Auto;
    public bool CustomsReleaseReady { get; set; }
    public bool RequireAdminConfirmation { get; set; } = true;
    public string SettlementPolicyCode { get; set; } = 공동구매국내운송정산정책코드.PlatformCollectsOrdererPaymentsAndPaysDriverAfterDropoff;
    public bool PlatformCollectsOrdererPayments { get; set; } = true;
    public bool PlatformHoldsFundsUntilDropoff { get; set; } = true;
    public bool OrdererPaymentCollectionConfirmed { get; set; }
    public IReadOnlyList<string> OrdererPaymentMethodCodes { get; set; } =
    [
        공동구매국내운송주문자결제수단코드.Card,
        공동구매국내운송주문자결제수단코드.CashLike
    ];
    public bool DriverSettlementAccountConfirmed { get; set; }
    public string DriverPayoutTriggerCode { get; set; } = 공동구매국내운송기사지급트리거코드.DropoffCompleted;
    public int DriverPayoutDelayDays { get; set; } = 3;
    public DateTime? DropoffCompletedAtUtc { get; set; }
    public bool RequirePickupEvidence { get; set; } = true;
    public bool RequireDropoffEvidence { get; set; } = true;
    public bool RequireReceiptEvidence { get; set; }
    public bool RequireCashReceipt { get; set; }
    public int? EstimatedFareKrw { get; set; }
    public int? EstimatedThreePlTransportFareKrw { get; set; }
    public int? EstimatedThreePlInboundFeeKrw { get; set; }
    public int? EstimatedThreePlStorageFeeKrw { get; set; }
    public int? EstimatedDedicatedWarehouseTransportFareKrw { get; set; }
    public int? EstimatedDedicatedWarehouseInboundFeeKrw { get; set; }
    public int? EstimatedDedicatedWarehouseStorageFeeKrw { get; set; }
    public int? EstimatedApartmentDirectTransportFareKrw { get; set; }
    public int? EstimatedDriverUnitDistributionFeeKrw { get; set; }
    public int? EstimatedSeparateWorkerDistributionFeeKrw { get; set; }
    public int? EstimatedRepresentativeDropoffTransportFareKrw { get; set; }
    public string DestinationTypeCode { get; set; } = 공동구매국내운송도착지유형코드.ApartmentComplexDirectDistribution;
    public bool TransportDecisionConfirmed { get; set; } = true;
    public bool TransportDecisionLocked { get; set; } = true;
    public bool TransportDecisionRevisionRequested { get; set; }
    public string TransportDecisionRevisionReason { get; set; } = string.Empty;
    public string BondedAreaCode { get; set; } = string.Empty;
    public string BondedAreaName { get; set; } = string.Empty;
    public string PickupRoadAddress { get; set; } = string.Empty;
    public string PickupDetailAddress { get; set; } = string.Empty;
    public decimal? PickupLatitude { get; set; }
    public decimal? PickupLongitude { get; set; }
    public string PickupContactName { get; set; } = string.Empty;
    public string PickupContactPhone { get; set; } = string.Empty;
    public DateTime? PickupWindowStartAtUtc { get; set; }
    public DateTime? PickupWindowEndAtUtc { get; set; }
    public string ThreePlWarehouseName { get; set; } = string.Empty;
    public string DedicatedWarehouseName { get; set; } = string.Empty;
    public bool DedicatedWarehouseColdChainFacilityConfirmed { get; set; }
    public string ApartmentComplexCode { get; set; } = string.Empty;
    public string ApartmentComplexName { get; set; } = string.Empty;
    public bool DriverPerformsApartmentUnitDistribution { get; set; } = true;
    public string ApartmentUnitDistributionModeCode { get; set; } = 공동구매공동주택세대배송방식코드.DriverToUnitDoor;
    public int? ApartmentUnitDeliveryCount { get; set; }
    public bool ApartmentUnitDistributionPlanConfirmed { get; set; }
    public bool UnitSortationBeforePickupRequired { get; set; } = true;
    public bool UnitSortationBeforePickupConfirmed { get; set; }
    public string UnitSortationModeCode { get; set; } = 공동구매세대별분류방식코드.ByBuildingAndUnit;
    public string UnitSortationLocationCode { get; set; } = 공동구매세대별분류장소코드.BondedArea;
    public string UnitSortationResponsiblePartyCode { get; set; } = 공동구매세대별분류책임주체코드.OverseasSeller;
    public bool UnitDemandBreakdownConfirmed { get; set; }
    public bool ImportedProductInfoRegistrationRequired { get; set; } = true;
    public bool ImportedProductInfoRegistered { get; set; }
    public string ProductInfoRegisteredByPartyCode { get; set; } = 공동구매세대별분류책임주체코드.OverseasSeller;
    public string ProductInfoStorageLocationCode { get; set; } = 공동구매상품정보보관위치코드.PlatformImportLedger;
    public bool ProductInfoStorageConfirmed { get; set; }
    public string 단위포장라벨링방식코드 { get; set; } = 공동구매단위포장라벨링방식코드.상품정보스티커;
    public bool UnitProductInfoStickerConfirmed { get; set; }
    public bool ProductInfoStickerBarcodeIncluded { get; set; }
    public bool ProductInfoStickerMatchesImportedProductConfirmed { get; set; }
    public bool UnitInvoiceIssuedConfirmed { get; set; }
    public bool UnitPackageLabelsConfirmed { get; set; }
    public bool UnitBarcodeScanLookupEnabled { get; set; }
    public string UnitBarcodeSchemeCode { get; set; } = 공동구매세대단위바코드체계코드.OrderNumberBarcode;
    public bool UnitBarcodeLookupDataConfirmed { get; set; }
    public bool UnitBarcodeMapsToMaskedRecipientConfirmed { get; set; }
    public bool UnitBarcodeMapsToDemandQuantityConfirmed { get; set; }
    public bool LoadingSequenceConfirmed { get; set; }
    public int? SortedUnitPackageCount { get; set; }
    public bool RecipientAddressPrivacyConfirmed { get; set; }
    public string DistributionPrivacyModeCode { get; set; } = 공동구매세대배송개인정보방식코드.MaskedUnitTokens;
    public string DistributionResponsibilityCode { get; set; } = 공동구매공동주택분배책임코드.Driver;
    public bool DistributionResponsibilityConfirmed { get; set; }
    public string DropoffRoadAddress { get; set; } = string.Empty;
    public string DropoffDetailAddress { get; set; } = string.Empty;
    public decimal? DropoffLatitude { get; set; }
    public decimal? DropoffLongitude { get; set; }
    public string DropoffContactName { get; set; } = string.Empty;
    public string DropoffContactPhone { get; set; } = string.Empty;
    public DateTime? DropoffWindowStartAtUtc { get; set; }
    public DateTime? DropoffWindowEndAtUtc { get; set; }
    public string CargoDescription { get; set; } = string.Empty;
    public int? CargoQuantity { get; set; }
    public decimal? CargoWeightKg { get; set; }
    public decimal? CargoVolumeCbm { get; set; }
    public int? PalletCount { get; set; }
    public string TemperatureCondition { get; set; } = string.Empty;
    public bool RequiresColdChain { get; set; }
    public bool ColdChainVehicleConfirmed { get; set; }
    public bool ThreePlColdChainFacilityConfirmed { get; set; }
    public bool Fragile { get; set; }
    public string AdminMemo { get; set; } = string.Empty;
}

public sealed class 공동구매플랫폼국내운송초안결과
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string PrincipalType { get; set; } = 공동구매국내운송의뢰주체유형코드.플랫폼;
    public string CostOwnerType { get; set; } = 공동구매국내운송비용부담주체유형코드.주문자집단;
    public string SettlementPolicyCode { get; set; } = 공동구매국내운송정산정책코드.PlatformCollectsOrdererPaymentsAndPaysDriverAfterDropoff;
    public string 공동구매Id { get; set; } = string.Empty;
    public string 주문자집단배송권키 { get; set; } = string.Empty;
    public string 주문자집단배송권명 { get; set; } = string.Empty;
    public string 문서관리번호 { get; set; } = string.Empty;
    public string SourceRequestType { get; set; } = 공동구매국내운송원천의뢰유형코드.ImportCargoTransport;
    public int DispatchBusinessTypeCode { get; set; } = 20;
    public bool ReadyForDispatchQueue { get; set; }
    public IReadOnlyList<string> RequiredActionCodes { get; set; } = [];
    public PlatformEntrustedCargoTransportDraftDto CargoTransportDraft { get; set; } = new();
    public PlatformEntrustedDispatchQueueDraftDto DispatchQueueDraft { get; set; } = new();
    public PlatformEntrustedDestinationPlanDto DestinationPlan { get; set; } = new();
    public PlatformEntrustedColdChainPlanDto ColdChainPlan { get; set; } = new();
    public IReadOnlyList<PlatformEntrustedDestinationCostOptionDto> DestinationCostOptions { get; set; } = [];
    public PlatformEntrustedDriverPayoutPlanDto DriverPayoutPlan { get; set; } = new();
}

public sealed class 공동구매국내운송배차대기생성결과
{
    public 공동구매플랫폼국내운송초안결과 운송초안 { get; set; } = new();
    public long 배차대기Id { get; set; }
    public string 의뢰Id { get; set; } = string.Empty;
    public string 원본의뢰유형 { get; set; } = string.Empty;
    public int 배차업무유형 { get; set; }
    public string 상태 { get; set; } = string.Empty;
    public string? 공동구매도착지유형코드 { get; set; }
    public bool? 공동구매기사세대배송여부 { get; set; }
    public int? 공동구매세대배송건수 { get; set; }
    public string? 공동구매분배책임코드 { get; set; }
}

public sealed class PlatformEntrustedCargoTransportDraftDto
{
    public string ClientRequestId { get; set; } = string.Empty;
    public string PlatformShipperUserId { get; set; } = string.Empty;
    public string PlatformLegalEntityName { get; set; } = string.Empty;
    public string 주문자집단배송권키 { get; set; } = string.Empty;
    public string 주문자집단배송권명 { get; set; } = string.Empty;
    public string CargoType { get; set; } = "Imported group purchase cargo";
    public string DestinationTypeCode { get; set; } = 공동구매국내운송도착지유형코드.ThreePlWarehouse;
    public string DestinationName { get; set; } = string.Empty;
    public string CargoDescription { get; set; } = string.Empty;
    public int? CargoQuantity { get; set; }
    public decimal? CargoWeightKg { get; set; }
    public decimal? CargoVolumeCbm { get; set; }
    public int? PalletCount { get; set; }
    public string TemperatureCondition { get; set; } = string.Empty;
    public bool Fragile { get; set; }
    public string PickupRoadAddress { get; set; } = string.Empty;
    public string PickupDetailAddress { get; set; } = string.Empty;
    public decimal? PickupLatitude { get; set; }
    public decimal? PickupLongitude { get; set; }
    public string PickupContactName { get; set; } = string.Empty;
    public string PickupContactPhone { get; set; } = string.Empty;
    public DateTime? PickupWindowStartAtUtc { get; set; }
    public DateTime? PickupWindowEndAtUtc { get; set; }
    public string DropoffRoadAddress { get; set; } = string.Empty;
    public string DropoffDetailAddress { get; set; } = string.Empty;
    public decimal? DropoffLatitude { get; set; }
    public decimal? DropoffLongitude { get; set; }
    public string DropoffContactName { get; set; } = string.Empty;
    public string DropoffContactPhone { get; set; } = string.Empty;
    public DateTime? DropoffWindowStartAtUtc { get; set; }
    public DateTime? DropoffWindowEndAtUtc { get; set; }
    public int? EstimatedFareKrw { get; set; }
    public string PaymentMethodCode { get; set; } = "PlatformSettlement";
    public string SettlementMemo { get; set; } = string.Empty;
}

public sealed class PlatformEntrustedDestinationPlanDto
{
    public string DestinationTypeCode { get; set; } = 공동구매국내운송도착지유형코드.ThreePlWarehouse;
    public string DestinationName { get; set; } = string.Empty;
    public string ThreePlWarehouseName { get; set; } = string.Empty;
    public string DedicatedWarehouseName { get; set; } = string.Empty;
    public string ApartmentComplexCode { get; set; } = string.Empty;
    public string ApartmentComplexName { get; set; } = string.Empty;
    public bool DirectApartmentDistribution { get; set; }
    public bool DriverPerformsApartmentUnitDistribution { get; set; }
    public string ApartmentUnitDistributionModeCode { get; set; } = 공동구매공동주택세대배송방식코드.None;
    public int? ApartmentUnitDeliveryCount { get; set; }
    public bool ApartmentUnitDistributionPlanConfirmed { get; set; }
    public PlatformEntrustedUnitSortationPlanDto UnitSortationPlan { get; set; } = new();
    public bool RecipientAddressPrivacyConfirmed { get; set; }
    public string DistributionPrivacyModeCode { get; set; } = 공동구매세대배송개인정보방식코드.MaskedUnitTokens;
    public string DistributionResponsibilityCode { get; set; } = 공동구매공동주택분배책임코드.None;
    public bool DistributionResponsibilityConfirmed { get; set; }
    public bool TransportDecisionConfirmed { get; set; } = true;
    public bool TransportDecisionLocked { get; set; } = true;
    public bool TransportDecisionRevisionRequested { get; set; }
    public string TransportDecisionRevisionReason { get; set; } = string.Empty;
    public IReadOnlyList<string> RequiredDistributionEvidenceCodes { get; set; } = [];
    public string DestinationMemo { get; set; } = string.Empty;
}

public sealed class PlatformEntrustedUnitSortationPlanDto
{
    public bool UnitSortationBeforePickupRequired { get; set; }
    public bool UnitSortationBeforePickupConfirmed { get; set; }
    public string UnitSortationModeCode { get; set; } = 공동구매세대별분류방식코드.NotRequired;
    public string UnitSortationLocationCode { get; set; } = 공동구매세대별분류장소코드.BondedArea;
    public string UnitSortationResponsiblePartyCode { get; set; } = 공동구매세대별분류책임주체코드.OverseasSeller;
    public bool UnitDemandBreakdownConfirmed { get; set; }
    public bool ImportedProductInfoRegistrationRequired { get; set; } = true;
    public bool ImportedProductInfoRegistered { get; set; }
    public string ProductInfoRegisteredByPartyCode { get; set; } = 공동구매세대별분류책임주체코드.OverseasSeller;
    public string ProductInfoStorageLocationCode { get; set; } = 공동구매상품정보보관위치코드.PlatformImportLedger;
    public bool ProductInfoStorageConfirmed { get; set; }
    public string 단위포장라벨링방식코드 { get; set; } = 공동구매단위포장라벨링방식코드.상품정보스티커;
    public bool UnitProductInfoStickerConfirmed { get; set; }
    public bool ProductInfoStickerBarcodeIncluded { get; set; }
    public bool ProductInfoStickerMatchesImportedProductConfirmed { get; set; }
    public bool UnitInvoiceIssuedConfirmed { get; set; }
    public bool UnitPackageLabelsConfirmed { get; set; }
    public bool UnitBarcodeScanLookupEnabled { get; set; }
    public string UnitBarcodeSchemeCode { get; set; } = 공동구매세대단위바코드체계코드.OrderNumberBarcode;
    public bool UnitBarcodeLookupDataConfirmed { get; set; }
    public bool UnitBarcodeMapsToMaskedRecipientConfirmed { get; set; }
    public bool UnitBarcodeMapsToDemandQuantityConfirmed { get; set; }
    public bool LoadingSequenceConfirmed { get; set; }
    public int? SortedUnitPackageCount { get; set; }
    public IReadOnlyList<string> RequiredActionCodes { get; set; } = [];
    public string Memo { get; set; } = string.Empty;
}

public sealed class PlatformEntrustedColdChainPlanDto
{
    public string TemperatureCode { get; set; } = 공동구매온도코드.상온;
    public bool RequiresColdChain { get; set; }
    public bool ColdChainVehicleConfirmed { get; set; }
    public bool ThreePlColdChainFacilityConfirmed { get; set; }
    public bool DedicatedWarehouseColdChainFacilityConfirmed { get; set; }
    public bool SelectedDestinationColdChainCompatible { get; set; } = true;
    public IReadOnlyList<string> RequiredActionCodes { get; set; } = [];
    public string Memo { get; set; } = string.Empty;
}

public sealed class PlatformEntrustedDestinationCostOptionDto
{
    public string DestinationTypeCode { get; set; } = 공동구매국내운송도착지유형코드.ThreePlWarehouse;
    public string OptionName { get; set; } = string.Empty;
    public string DistributionResponsibilityCode { get; set; } = 공동구매공동주택분배책임코드.None;
    public bool CompatibleWithTemperature { get; set; } = true;
    public string StatusCode { get; set; } = 공동구매국내운송비용옵션상태코드.Selectable;
    public int? EstimatedTransportFareKrw { get; set; }
    public int? EstimatedInboundFeeKrw { get; set; }
    public int? EstimatedStorageFeeKrw { get; set; }
    public int? EstimatedDistributionFeeKrw { get; set; }
    public int? EstimatedTotalCostKrw { get; set; }
    public decimal? EstimatedCostPerKgKrw { get; set; }
    public IReadOnlyList<string> RequiredActionCodes { get; set; } = [];
    public string Memo { get; set; } = string.Empty;
}

public sealed class PlatformEntrustedDriverPayoutPlanDto
{
    public bool PlatformCollectsOrdererPayments { get; set; } = true;
    public bool PlatformHoldsFundsUntilDropoff { get; set; } = true;
    public IReadOnlyList<string> OrdererPaymentMethodCodes { get; set; } = [];
    public bool OrdererPaymentCollectionConfirmed { get; set; }
    public string DriverPayoutTriggerCode { get; set; } = 공동구매국내운송기사지급트리거코드.DropoffCompleted;
    public int DriverPayoutDelayDays { get; set; } = 3;
    public DateTime? DropoffCompletedAtUtc { get; set; }
    public DateTime? DriverPayoutDueAtUtc { get; set; }
    public string PayoutRecipientType { get; set; } = "Driver";
    public string PayoutAccountPolicyCode { get; set; } = 공동구매국내운송기사지급계좌정책코드.DriverRegisteredSettlementAccount;
    public bool DriverSettlementAccountConfirmed { get; set; }
    public bool RequirePickupEvidence { get; set; } = true;
    public bool RequireDropoffEvidence { get; set; } = true;
    public bool RequireReceiptEvidence { get; set; }
    public bool RequireCashReceipt { get; set; }
    public IReadOnlyList<string> RequiredEvidenceCodes { get; set; } = [];
}

public sealed class PlatformEntrustedDispatchQueueDraftDto
{
    public string RequestId { get; set; } = string.Empty;
    public string PlatformShipperUserId { get; set; } = string.Empty;
    public int DispatchBusinessTypeCode { get; set; } = 20;
    public string SourceRequestType { get; set; } = 공동구매국내운송원천의뢰유형코드.ImportCargoTransport;
    public string SourceRequestId { get; set; } = string.Empty;
    public string DestinationTypeCode { get; set; } = 공동구매국내운송도착지유형코드.ThreePlWarehouse;
    public string DestinationName { get; set; } = string.Empty;
    public bool DriverPerformsApartmentUnitDistribution { get; set; }
    public string ApartmentUnitDistributionModeCode { get; set; } = 공동구매공동주택세대배송방식코드.None;
    public int? ApartmentUnitDeliveryCount { get; set; }
    public string DistributionResponsibilityCode { get; set; } = 공동구매공동주택분배책임코드.None;
    public bool UnitSortationBeforePickupRequired { get; set; }
    public bool UnitSortationBeforePickupConfirmed { get; set; }
    public string UnitSortationModeCode { get; set; } = 공동구매세대별분류방식코드.NotRequired;
    public string UnitSortationLocationCode { get; set; } = 공동구매세대별분류장소코드.BondedArea;
    public string UnitSortationResponsiblePartyCode { get; set; } = 공동구매세대별분류책임주체코드.OverseasSeller;
    public bool UnitDemandBreakdownConfirmed { get; set; }
    public bool ImportedProductInfoRegistrationRequired { get; set; } = true;
    public bool ImportedProductInfoRegistered { get; set; }
    public string ProductInfoRegisteredByPartyCode { get; set; } = 공동구매세대별분류책임주체코드.OverseasSeller;
    public string ProductInfoStorageLocationCode { get; set; } = 공동구매상품정보보관위치코드.PlatformImportLedger;
    public bool ProductInfoStorageConfirmed { get; set; }
    public string 단위포장라벨링방식코드 { get; set; } = 공동구매단위포장라벨링방식코드.상품정보스티커;
    public bool UnitProductInfoStickerConfirmed { get; set; }
    public bool ProductInfoStickerBarcodeIncluded { get; set; }
    public bool ProductInfoStickerMatchesImportedProductConfirmed { get; set; }
    public bool UnitInvoiceIssuedConfirmed { get; set; }
    public bool UnitPackageLabelsConfirmed { get; set; }
    public bool UnitBarcodeScanLookupEnabled { get; set; }
    public string UnitBarcodeSchemeCode { get; set; } = 공동구매세대단위바코드체계코드.OrderNumberBarcode;
    public bool UnitBarcodeLookupDataConfirmed { get; set; }
    public bool UnitBarcodeMapsToMaskedRecipientConfirmed { get; set; }
    public bool UnitBarcodeMapsToDemandQuantityConfirmed { get; set; }
    public bool LoadingSequenceConfirmed { get; set; }
    public int? SortedUnitPackageCount { get; set; }
    public string PickupRoadAddress { get; set; } = string.Empty;
    public string PickupDetailAddress { get; set; } = string.Empty;
    public decimal? PickupLatitude { get; set; }
    public decimal? PickupLongitude { get; set; }
    public string DropoffRoadAddress { get; set; } = string.Empty;
    public string DropoffDetailAddress { get; set; } = string.Empty;
    public decimal? DropoffLatitude { get; set; }
    public decimal? DropoffLongitude { get; set; }
    public string QueueStatusCode { get; set; } = "Waiting";
}

public static class 공동구매플랫폼국내운송계획기
{
    public static 공동구매플랫폼국내운송초안결과 계획(
        공동구매커머스이행계획Dto fulfillmentPlan,
        공동구매플랫폼국내운송초안요청 request)
    {
        ArgumentNullException.ThrowIfNull(fulfillmentPlan);
        ArgumentNullException.ThrowIfNull(request);

        var destinationType = NormalizeDestinationType(request.DestinationTypeCode);
        var unitDistributionMode = NormalizeApartmentUnitDistributionMode(
            request.ApartmentUnitDistributionModeCode,
            request.DriverPerformsApartmentUnitDistribution);
        var distributionPrivacyMode = NormalizeDistributionPrivacyMode(request.DistributionPrivacyModeCode);
        var distributionResponsibility = NormalizeDistributionResponsibility(
            request.DistributionResponsibilityCode,
            request.DriverPerformsApartmentUnitDistribution,
            destinationType);
        var temperatureCode = NormalizeTemperatureCode(request.TemperatureCondition);
        var requiresColdChain = ResolveRequiresColdChain(request, temperatureCode);
        var requiredActions = ResolveRequiredActions(request, destinationType, requiresColdChain, distributionResponsibility).ToArray();
        var unitSortationActionCodes = ResolveUnitSortationRequiredActions(request, destinationType, distributionResponsibility).ToArray();
        var sourceRequestType = ResolveSourceRequestType(request.TransportMode);
        var clientRequestId = BuildClientRequestId(fulfillmentPlan, sourceRequestType, destinationType);
        var ready = requiredActions.Length == 0;
        var cargoDescription = string.IsNullOrWhiteSpace(request.CargoDescription)
            ? fulfillmentPlan.상품명
            : request.CargoDescription.Trim();
        var cargoQuantity = request.CargoQuantity ?? fulfillmentPlan.예상입고수량;
        var settlementPolicy = NormalizeSettlementPolicy(request.SettlementPolicyCode);
        var ordererPaymentMethodCodes = NormalizeOrdererPaymentMethods(request.OrdererPaymentMethodCodes).ToArray();
        var driverPayoutTrigger = NormalizeDriverPayoutTrigger(request.DriverPayoutTriggerCode);
        var driverPayoutDelayDays = NormalizeDriverPayoutDelayDays(request.DriverPayoutDelayDays);
        var requiredEvidenceCodes = ResolveRequiredEvidenceCodes(request).ToArray();
        var destinationName = ResolveDestinationName(fulfillmentPlan, request, destinationType);
        var distributionEvidenceCodes = ResolveRequiredDistributionEvidenceCodes(request, destinationType).ToArray();
        var coldChainActionCodes = ResolveColdChainRequiredActions(request, destinationType, requiresColdChain).ToArray();
        var destinationCostOptions = BuildDestinationCostOptions(
            request,
            fulfillmentPlan,
            destinationType,
            requiresColdChain).ToArray();

        return new 공동구매플랫폼국내운송초안결과
        {
            Success = true,
            Message = ready
                ? "Platform-entrusted domestic cargo transport draft is ready for the 1.0 dispatch queue."
                : "Platform-entrusted domestic cargo transport draft requires confirmation before dispatch queue creation.",
            PrincipalType = 공동구매국내운송의뢰주체유형코드.플랫폼,
            CostOwnerType = 공동구매국내운송비용부담주체유형코드.주문자집단,
            SettlementPolicyCode = settlementPolicy,
            공동구매Id = fulfillmentPlan.공동구매Id,
            주문자집단배송권키 = fulfillmentPlan.주문자집단배송권키,
            주문자집단배송권명 = fulfillmentPlan.주문자집단배송권명,
            문서관리번호 = fulfillmentPlan.문서관리번호,
            SourceRequestType = sourceRequestType,
            DispatchBusinessTypeCode = 20,
            ReadyForDispatchQueue = ready,
            RequiredActionCodes = requiredActions,
            CargoTransportDraft = new PlatformEntrustedCargoTransportDraftDto
            {
                ClientRequestId = clientRequestId,
                PlatformShipperUserId = NormalizePlatformUserId(request.PlatformShipperUserId),
                PlatformLegalEntityName = request.PlatformLegalEntityName.Trim(),
                주문자집단배송권키 = fulfillmentPlan.주문자집단배송권키,
                주문자집단배송권명 = fulfillmentPlan.주문자집단배송권명,
                DestinationTypeCode = destinationType,
                DestinationName = destinationName,
                CargoDescription = cargoDescription,
                CargoQuantity = cargoQuantity > 0 ? cargoQuantity : null,
                CargoWeightKg = request.CargoWeightKg,
                CargoVolumeCbm = request.CargoVolumeCbm,
                PalletCount = request.PalletCount,
                TemperatureCondition = request.TemperatureCondition.Trim(),
                Fragile = request.Fragile,
                PickupRoadAddress = request.PickupRoadAddress.Trim(),
                PickupDetailAddress = request.PickupDetailAddress.Trim(),
                PickupLatitude = request.PickupLatitude,
                PickupLongitude = request.PickupLongitude,
                PickupContactName = request.PickupContactName.Trim(),
                PickupContactPhone = request.PickupContactPhone.Trim(),
                PickupWindowStartAtUtc = request.PickupWindowStartAtUtc,
                PickupWindowEndAtUtc = request.PickupWindowEndAtUtc,
                DropoffRoadAddress = request.DropoffRoadAddress.Trim(),
                DropoffDetailAddress = request.DropoffDetailAddress.Trim(),
                DropoffLatitude = request.DropoffLatitude,
                DropoffLongitude = request.DropoffLongitude,
                DropoffContactName = request.DropoffContactName.Trim(),
                DropoffContactPhone = request.DropoffContactPhone.Trim(),
                DropoffWindowStartAtUtc = request.DropoffWindowStartAtUtc,
                DropoffWindowEndAtUtc = request.DropoffWindowEndAtUtc,
                EstimatedFareKrw = request.EstimatedFareKrw,
                PaymentMethodCode = request.PlatformCollectsOrdererPayments ? "PlatformCollectedSettlement" : "PlatformSettlement",
                SettlementMemo = BuildSettlementMemo(
                    fulfillmentPlan,
                    request,
                    settlementPolicy,
                    ordererPaymentMethodCodes,
                    driverPayoutTrigger,
                    driverPayoutDelayDays,
                    destinationType)
            },
            DispatchQueueDraft = new PlatformEntrustedDispatchQueueDraftDto
            {
                RequestId = clientRequestId,
                PlatformShipperUserId = NormalizePlatformUserId(request.PlatformShipperUserId),
                DispatchBusinessTypeCode = 20,
                SourceRequestType = sourceRequestType,
                SourceRequestId = clientRequestId,
                DestinationTypeCode = destinationType,
                DestinationName = destinationName,
                DriverPerformsApartmentUnitDistribution = request.DriverPerformsApartmentUnitDistribution,
                ApartmentUnitDistributionModeCode = unitDistributionMode,
                ApartmentUnitDeliveryCount = request.ApartmentUnitDeliveryCount > 0 ? request.ApartmentUnitDeliveryCount : null,
                DistributionResponsibilityCode = distributionResponsibility,
                UnitSortationBeforePickupRequired = request.UnitSortationBeforePickupRequired,
                UnitSortationBeforePickupConfirmed = request.UnitSortationBeforePickupConfirmed,
                UnitSortationModeCode = NormalizeUnitSortationMode(request.UnitSortationModeCode, request.UnitSortationBeforePickupRequired),
                UnitSortationLocationCode = NormalizeUnitSortationLocation(request.UnitSortationLocationCode),
                UnitSortationResponsiblePartyCode = NormalizeUnitSortationResponsibleParty(request.UnitSortationResponsiblePartyCode),
                UnitDemandBreakdownConfirmed = request.UnitDemandBreakdownConfirmed,
                ImportedProductInfoRegistrationRequired = request.ImportedProductInfoRegistrationRequired,
                ImportedProductInfoRegistered = request.ImportedProductInfoRegistered,
                ProductInfoRegisteredByPartyCode = NormalizeUnitSortationResponsibleParty(request.ProductInfoRegisteredByPartyCode),
                ProductInfoStorageLocationCode = NormalizeProductInfoStorageLocation(request.ProductInfoStorageLocationCode),
                ProductInfoStorageConfirmed = request.ProductInfoStorageConfirmed,
                단위포장라벨링방식코드 = 단위포장라벨링방식정규화(request.단위포장라벨링방식코드),
                UnitProductInfoStickerConfirmed = request.UnitProductInfoStickerConfirmed,
                ProductInfoStickerBarcodeIncluded = request.ProductInfoStickerBarcodeIncluded,
                ProductInfoStickerMatchesImportedProductConfirmed = request.ProductInfoStickerMatchesImportedProductConfirmed,
                UnitInvoiceIssuedConfirmed = request.UnitInvoiceIssuedConfirmed,
                UnitPackageLabelsConfirmed = request.UnitPackageLabelsConfirmed,
                UnitBarcodeScanLookupEnabled = request.UnitBarcodeScanLookupEnabled,
                UnitBarcodeSchemeCode = NormalizeUnitBarcodeScheme(request.UnitBarcodeSchemeCode),
                UnitBarcodeLookupDataConfirmed = request.UnitBarcodeLookupDataConfirmed,
                UnitBarcodeMapsToMaskedRecipientConfirmed = request.UnitBarcodeMapsToMaskedRecipientConfirmed,
                UnitBarcodeMapsToDemandQuantityConfirmed = request.UnitBarcodeMapsToDemandQuantityConfirmed,
                LoadingSequenceConfirmed = request.LoadingSequenceConfirmed,
                SortedUnitPackageCount = request.SortedUnitPackageCount > 0 ? request.SortedUnitPackageCount : null,
                PickupRoadAddress = request.PickupRoadAddress.Trim(),
                PickupDetailAddress = request.PickupDetailAddress.Trim(),
                PickupLatitude = request.PickupLatitude,
                PickupLongitude = request.PickupLongitude,
                DropoffRoadAddress = request.DropoffRoadAddress.Trim(),
                DropoffDetailAddress = request.DropoffDetailAddress.Trim(),
                DropoffLatitude = request.DropoffLatitude,
                DropoffLongitude = request.DropoffLongitude
            },
            DestinationPlan = new PlatformEntrustedDestinationPlanDto
            {
                DestinationTypeCode = destinationType,
                DestinationName = destinationName,
                ThreePlWarehouseName = request.ThreePlWarehouseName.Trim(),
                DedicatedWarehouseName = request.DedicatedWarehouseName.Trim(),
                ApartmentComplexCode = request.ApartmentComplexCode.Trim(),
                ApartmentComplexName = request.ApartmentComplexName.Trim(),
                DirectApartmentDistribution = destinationType == 공동구매국내운송도착지유형코드.ApartmentComplexDirectDistribution,
                DriverPerformsApartmentUnitDistribution = request.DriverPerformsApartmentUnitDistribution,
                ApartmentUnitDistributionModeCode = unitDistributionMode,
                ApartmentUnitDeliveryCount = request.ApartmentUnitDeliveryCount > 0 ? request.ApartmentUnitDeliveryCount : null,
                ApartmentUnitDistributionPlanConfirmed = request.ApartmentUnitDistributionPlanConfirmed,
                UnitSortationPlan = new PlatformEntrustedUnitSortationPlanDto
                {
                    UnitSortationBeforePickupRequired = request.UnitSortationBeforePickupRequired,
                    UnitSortationBeforePickupConfirmed = request.UnitSortationBeforePickupConfirmed,
                    UnitSortationModeCode = NormalizeUnitSortationMode(request.UnitSortationModeCode, request.UnitSortationBeforePickupRequired),
                    UnitSortationLocationCode = NormalizeUnitSortationLocation(request.UnitSortationLocationCode),
                    UnitSortationResponsiblePartyCode = NormalizeUnitSortationResponsibleParty(request.UnitSortationResponsiblePartyCode),
                    UnitDemandBreakdownConfirmed = request.UnitDemandBreakdownConfirmed,
                    ImportedProductInfoRegistrationRequired = request.ImportedProductInfoRegistrationRequired,
                    ImportedProductInfoRegistered = request.ImportedProductInfoRegistered,
                    ProductInfoRegisteredByPartyCode = NormalizeUnitSortationResponsibleParty(request.ProductInfoRegisteredByPartyCode),
                    ProductInfoStorageLocationCode = NormalizeProductInfoStorageLocation(request.ProductInfoStorageLocationCode),
                    ProductInfoStorageConfirmed = request.ProductInfoStorageConfirmed,
                    단위포장라벨링방식코드 = 단위포장라벨링방식정규화(request.단위포장라벨링방식코드),
                    UnitProductInfoStickerConfirmed = request.UnitProductInfoStickerConfirmed,
                    ProductInfoStickerBarcodeIncluded = request.ProductInfoStickerBarcodeIncluded,
                    ProductInfoStickerMatchesImportedProductConfirmed = request.ProductInfoStickerMatchesImportedProductConfirmed,
                    UnitInvoiceIssuedConfirmed = request.UnitInvoiceIssuedConfirmed,
                    UnitPackageLabelsConfirmed = request.UnitPackageLabelsConfirmed,
                    UnitBarcodeScanLookupEnabled = request.UnitBarcodeScanLookupEnabled,
                    UnitBarcodeSchemeCode = NormalizeUnitBarcodeScheme(request.UnitBarcodeSchemeCode),
                    UnitBarcodeLookupDataConfirmed = request.UnitBarcodeLookupDataConfirmed,
                    UnitBarcodeMapsToMaskedRecipientConfirmed = request.UnitBarcodeMapsToMaskedRecipientConfirmed,
                    UnitBarcodeMapsToDemandQuantityConfirmed = request.UnitBarcodeMapsToDemandQuantityConfirmed,
                    LoadingSequenceConfirmed = request.LoadingSequenceConfirmed,
                    SortedUnitPackageCount = request.SortedUnitPackageCount > 0 ? request.SortedUnitPackageCount : null,
                    RequiredActionCodes = unitSortationActionCodes,
                    Memo = BuildUnitSortationMemo(request, destinationType, distributionResponsibility)
                },
                RecipientAddressPrivacyConfirmed = request.RecipientAddressPrivacyConfirmed,
                DistributionPrivacyModeCode = distributionPrivacyMode,
                DistributionResponsibilityCode = distributionResponsibility,
                DistributionResponsibilityConfirmed = request.DistributionResponsibilityConfirmed,
                TransportDecisionConfirmed = request.TransportDecisionConfirmed,
                TransportDecisionLocked = request.TransportDecisionLocked,
                TransportDecisionRevisionRequested = request.TransportDecisionRevisionRequested,
                TransportDecisionRevisionReason = request.TransportDecisionRevisionReason.Trim(),
                RequiredDistributionEvidenceCodes = distributionEvidenceCodes,
                DestinationMemo = BuildDestinationMemo(fulfillmentPlan, request, destinationType, unitDistributionMode)
            },
            ColdChainPlan = new PlatformEntrustedColdChainPlanDto
            {
                TemperatureCode = temperatureCode,
                RequiresColdChain = requiresColdChain,
                ColdChainVehicleConfirmed = request.ColdChainVehicleConfirmed,
                ThreePlColdChainFacilityConfirmed = request.ThreePlColdChainFacilityConfirmed,
                DedicatedWarehouseColdChainFacilityConfirmed = request.DedicatedWarehouseColdChainFacilityConfirmed,
                SelectedDestinationColdChainCompatible = IsDestinationColdChainCompatible(request, destinationType, requiresColdChain),
                RequiredActionCodes = coldChainActionCodes,
                Memo = BuildColdChainMemo(destinationType, requiresColdChain, request)
            },
            DestinationCostOptions = destinationCostOptions,
            DriverPayoutPlan = new PlatformEntrustedDriverPayoutPlanDto
            {
                PlatformCollectsOrdererPayments = request.PlatformCollectsOrdererPayments,
                PlatformHoldsFundsUntilDropoff = request.PlatformHoldsFundsUntilDropoff,
                OrdererPaymentMethodCodes = ordererPaymentMethodCodes,
                OrdererPaymentCollectionConfirmed = request.OrdererPaymentCollectionConfirmed,
                DriverPayoutTriggerCode = driverPayoutTrigger,
                DriverPayoutDelayDays = driverPayoutDelayDays,
                DropoffCompletedAtUtc = request.DropoffCompletedAtUtc,
                DriverPayoutDueAtUtc = request.DropoffCompletedAtUtc?.AddDays(driverPayoutDelayDays),
                PayoutRecipientType = "Driver",
                PayoutAccountPolicyCode = 공동구매국내운송기사지급계좌정책코드.DriverRegisteredSettlementAccount,
                DriverSettlementAccountConfirmed = request.DriverSettlementAccountConfirmed,
                RequirePickupEvidence = request.RequirePickupEvidence,
                RequireDropoffEvidence = request.RequireDropoffEvidence,
                RequireReceiptEvidence = request.RequireReceiptEvidence,
                RequireCashReceipt = request.RequireCashReceipt,
                RequiredEvidenceCodes = requiredEvidenceCodes
            }
        };
    }

    private static IEnumerable<string> ResolveRequiredActions(
        공동구매플랫폼국내운송초안요청 request,
        string destinationType,
        bool requiresColdChain,
        string distributionResponsibility)
    {
        if (string.IsNullOrWhiteSpace(request.PlatformShipperUserId))
        {
            yield return 공동구매국내운송필요조치코드.ConfirmPlatformShipperProfile;
        }

        if (!request.CustomsReleaseReady)
        {
            yield return 공동구매국내운송필요조치코드.ConfirmCustomsReleaseOrBondedRelease;
        }

        if (string.IsNullOrWhiteSpace(request.PickupRoadAddress))
        {
            yield return 공동구매국내운송필요조치코드.ConfirmBondedAreaPickupAddress;
        }

        if (!request.TransportDecisionConfirmed ||
            (request.TransportDecisionLocked && request.TransportDecisionRevisionRequested))
        {
            yield return 공동구매국내운송필요조치코드.ConfirmTransportDecisionRevision;
        }

        if (string.IsNullOrWhiteSpace(request.DropoffRoadAddress))
        {
            yield return destinationType == 공동구매국내운송도착지유형코드.ApartmentComplexDirectDistribution
                ? 공동구매국내운송필요조치코드.ConfirmApartmentComplexDropoffAddress
                : 공동구매국내운송필요조치코드.ConfirmThreePlDropoffAddress;
        }

        if (destinationType == 공동구매국내운송도착지유형코드.ApartmentComplexDirectDistribution &&
            request.DriverPerformsApartmentUnitDistribution)
        {
            if (!request.ApartmentUnitDistributionPlanConfirmed ||
                request.ApartmentUnitDeliveryCount is null or <= 0 ||
                string.Equals(
                    NormalizeApartmentUnitDistributionMode(request.ApartmentUnitDistributionModeCode, request.DriverPerformsApartmentUnitDistribution),
                    공동구매공동주택세대배송방식코드.None,
                    StringComparison.OrdinalIgnoreCase))
            {
                yield return 공동구매국내운송필요조치코드.ConfirmApartmentUnitDistributionPlan;
            }

            if (!request.RecipientAddressPrivacyConfirmed)
            {
                yield return 공동구매국내운송필요조치코드.ConfirmRecipientAddressPrivacy;
            }
        }

        if (destinationType == 공동구매국내운송도착지유형코드.ApartmentComplexDirectDistribution &&
            !request.DistributionResponsibilityConfirmed)
        {
            yield return 공동구매국내운송필요조치코드.ConfirmDistributionResponsibility;
        }

        foreach (var actionCode in ResolveUnitSortationRequiredActions(request, destinationType, distributionResponsibility))
        {
            yield return actionCode;
        }

        foreach (var actionCode in ResolveColdChainRequiredActions(request, destinationType, requiresColdChain))
        {
            yield return actionCode;
        }

        if (!request.CargoWeightKg.HasValue && !request.CargoVolumeCbm.HasValue && !request.PalletCount.HasValue)
        {
            yield return 공동구매국내운송필요조치코드.ConfirmCargoSpecification;
        }

        if (request.RequireAdminConfirmation)
        {
            yield return 공동구매국내운송필요조치코드.ConfirmPlatformEntrustedTransport;
        }

        if (request.PlatformCollectsOrdererPayments &&
            (!request.OrdererPaymentCollectionConfirmed || request.OrdererPaymentMethodCodes.Count == 0))
        {
            yield return 공동구매국내운송필요조치코드.ConfirmOrdererPaymentCollection;
        }

        if (!request.DriverSettlementAccountConfirmed)
        {
            yield return 공동구매국내운송필요조치코드.ConfirmDriverSettlementAccount;
        }

        if (request.DriverPayoutDelayDays is < 0 or > 30 ||
            string.IsNullOrWhiteSpace(request.DriverPayoutTriggerCode))
        {
            yield return 공동구매국내운송필요조치코드.ConfirmDriverPayoutPolicy;
        }
    }

    private static IEnumerable<string> ResolveUnitSortationRequiredActions(
        공동구매플랫폼국내운송초안요청 request,
        string destinationType,
        string distributionResponsibility)
    {
        if (destinationType != 공동구매국내운송도착지유형코드.ApartmentComplexDirectDistribution ||
            distributionResponsibility != 공동구매공동주택분배책임코드.Driver ||
            !request.DriverPerformsApartmentUnitDistribution ||
            !request.UnitSortationBeforePickupRequired)
        {
            yield break;
        }

        var labelingMode = 단위포장라벨링방식정규화(request.단위포장라벨링방식코드);
        if (request.ImportedProductInfoRegistrationRequired &&
            !request.ImportedProductInfoRegistered)
        {
            yield return 공동구매국내운송필요조치코드.ConfirmImportedProductInfoRegistration;
        }

        if (request.ImportedProductInfoRegistrationRequired &&
            !request.ProductInfoStorageConfirmed)
        {
            yield return 공동구매국내운송필요조치코드.ConfirmProductInfoStickerStorage;
        }

        if (!request.UnitSortationBeforePickupConfirmed ||
            !request.UnitDemandBreakdownConfirmed ||
            !단위포장라벨링확인됨(request, labelingMode) ||
            !request.LoadingSequenceConfirmed ||
            request.SortedUnitPackageCount is null or <= 0)
        {
            yield return 공동구매국내운송필요조치코드.ConfirmUnitSortationBeforePickup;
        }

        var responsibleParty = NormalizeUnitSortationResponsibleParty(request.UnitSortationResponsiblePartyCode);
        if (labelingMode == 공동구매단위포장라벨링방식코드.세대별송장라벨 &&
            responsibleParty is 공동구매세대별분류책임주체코드.OverseasSeller or 공동구매세대별분류책임주체코드.OverseasForwarder &&
            (!request.UnitInvoiceIssuedConfirmed || !request.UnitPackageLabelsConfirmed || !request.UnitDemandBreakdownConfirmed))
        {
            yield return 공동구매국내운송필요조치코드.ConfirmOverseasUnitInvoiceAndLabeling;
        }

        if (labelingMode == 공동구매단위포장라벨링방식코드.상품정보스티커 &&
            (!request.UnitProductInfoStickerConfirmed || !request.ProductInfoStickerMatchesImportedProductConfirmed))
        {
            yield return 공동구매국내운송필요조치코드.ConfirmUnitProductInfoSticker;
        }

        if (request.UnitBarcodeScanLookupEnabled &&
            (!request.UnitBarcodeLookupDataConfirmed ||
             !request.UnitBarcodeMapsToMaskedRecipientConfirmed ||
             !request.UnitBarcodeMapsToDemandQuantityConfirmed))
        {
            yield return 공동구매국내운송필요조치코드.ConfirmUnitBarcodeScanLookup;
        }
    }

    private static IEnumerable<string> ResolveColdChainRequiredActions(
        공동구매플랫폼국내운송초안요청 request,
        string destinationType,
        bool requiresColdChain)
    {
        if (!requiresColdChain)
        {
            yield break;
        }

        if (!request.ColdChainVehicleConfirmed)
        {
            yield return 공동구매국내운송필요조치코드.ConfirmColdChainVehicle;
        }

        if (destinationType == 공동구매국내운송도착지유형코드.ThreePlWarehouse &&
            !request.ThreePlColdChainFacilityConfirmed)
        {
            yield return 공동구매국내운송필요조치코드.ConfirmColdChainThreePlFacility;
        }

        if (destinationType == 공동구매국내운송도착지유형코드.DedicatedWarehouse &&
            !request.DedicatedWarehouseColdChainFacilityConfirmed)
        {
            yield return 공동구매국내운송필요조치코드.ConfirmColdChainThreePlFacility;
        }
    }

    private static IEnumerable<PlatformEntrustedDestinationCostOptionDto> BuildDestinationCostOptions(
        공동구매플랫폼국내운송초안요청 request,
        공동구매커머스이행계획Dto fulfillmentPlan,
        string selectedDestinationType,
        bool requiresColdChain)
    {
        yield return BuildCostOption(
            request,
            fulfillmentPlan,
            공동구매국내운송도착지유형코드.ThreePlWarehouse,
            "3PL warehouse inbound",
            공동구매공동주택분배책임코드.None,
            ResolveRouteFare(
                request.EstimatedThreePlTransportFareKrw,
                selectedDestinationType,
                공동구매국내운송도착지유형코드.ThreePlWarehouse,
                request.EstimatedFareKrw),
            request.EstimatedThreePlInboundFeeKrw,
            request.EstimatedThreePlStorageFeeKrw,
            null,
            requiresColdChain);

        yield return BuildCostOption(
            request,
            fulfillmentPlan,
            공동구매국내운송도착지유형코드.DedicatedWarehouse,
            "Dedicated warehouse inbound",
            공동구매공동주택분배책임코드.None,
            ResolveRouteFare(
                request.EstimatedDedicatedWarehouseTransportFareKrw,
                selectedDestinationType,
                공동구매국내운송도착지유형코드.DedicatedWarehouse,
                request.EstimatedFareKrw),
            request.EstimatedDedicatedWarehouseInboundFeeKrw,
            request.EstimatedDedicatedWarehouseStorageFeeKrw,
            null,
            requiresColdChain);

        yield return BuildCostOption(
            request,
            fulfillmentPlan,
            공동구매국내운송도착지유형코드.ApartmentComplexDirectDistribution,
            "Apartment direct dropoff; distribution by orderer group or separate worker",
            공동구매공동주택분배책임코드.SeparateWorker,
            ResolveRouteFare(
                request.EstimatedApartmentDirectTransportFareKrw,
                selectedDestinationType,
                공동구매국내운송도착지유형코드.ApartmentComplexDirectDistribution,
                request.EstimatedFareKrw),
            null,
            null,
            request.EstimatedSeparateWorkerDistributionFeeKrw,
            requiresColdChain);

        yield return BuildCostOption(
            request,
            fulfillmentPlan,
            공동구매국내운송도착지유형코드.ApartmentComplexDirectDistribution,
            "Apartment direct; driver performs unit distribution",
            공동구매공동주택분배책임코드.Driver,
            ResolveRouteFare(
                request.EstimatedApartmentDirectTransportFareKrw,
                selectedDestinationType,
                공동구매국내운송도착지유형코드.ApartmentComplexDirectDistribution,
                request.EstimatedFareKrw),
            null,
            null,
            request.EstimatedDriverUnitDistributionFeeKrw,
            requiresColdChain);

        yield return BuildCostOption(
            request,
            fulfillmentPlan,
            공동구매국내운송도착지유형코드.OrdererGroupRepresentativeDropoff,
            "Orderer group representative dropoff",
            공동구매공동주택분배책임코드.주문자집단,
            ResolveRouteFare(
                request.EstimatedRepresentativeDropoffTransportFareKrw,
                selectedDestinationType,
                공동구매국내운송도착지유형코드.OrdererGroupRepresentativeDropoff,
                request.EstimatedFareKrw),
            null,
            null,
            null,
            requiresColdChain);
    }

    private static PlatformEntrustedDestinationCostOptionDto BuildCostOption(
        공동구매플랫폼국내운송초안요청 request,
        공동구매커머스이행계획Dto fulfillmentPlan,
        string destinationType,
        string optionName,
        string distributionResponsibility,
        int? transportFare,
        int? inboundFee,
        int? storageFee,
        int? distributionFee,
        bool requiresColdChain)
    {
        var requiredActions = ResolveCostOptionRequiredActions(request, destinationType, distributionResponsibility, requiresColdChain).ToArray();
        var total = SumKnownCosts(transportFare, inboundFee, storageFee, distributionFee);
        var compatibleWithTemperature = requiredActions.All(action =>
            action != 공동구매국내운송필요조치코드.ConfirmColdChainVehicle &&
            action != 공동구매국내운송필요조치코드.ConfirmColdChainThreePlFacility);

        return new PlatformEntrustedDestinationCostOptionDto
        {
            DestinationTypeCode = destinationType,
            OptionName = optionName,
            DistributionResponsibilityCode = distributionResponsibility,
            CompatibleWithTemperature = compatibleWithTemperature,
            StatusCode = requiredActions.Length == 0
                ? 공동구매국내운송비용옵션상태코드.Selectable
                : 공동구매국내운송비용옵션상태코드.NeedsConfirmation,
            EstimatedTransportFareKrw = transportFare,
            EstimatedInboundFeeKrw = inboundFee,
            EstimatedStorageFeeKrw = storageFee,
            EstimatedDistributionFeeKrw = distributionFee,
            EstimatedTotalCostKrw = total,
            EstimatedCostPerKgKrw = ToCostPerKg(total, request.CargoWeightKg),
            RequiredActionCodes = requiredActions,
            Memo = BuildCostOptionMemo(fulfillmentPlan, destinationType, distributionResponsibility, requiresColdChain)
        };
    }

    private static IEnumerable<string> ResolveCostOptionRequiredActions(
        공동구매플랫폼국내운송초안요청 request,
        string destinationType,
        string distributionResponsibility,
        bool requiresColdChain)
    {
        foreach (var action in ResolveColdChainRequiredActions(request, destinationType, requiresColdChain))
        {
            yield return action;
        }

        if (destinationType == 공동구매국내운송도착지유형코드.ApartmentComplexDirectDistribution &&
            distributionResponsibility != 공동구매공동주택분배책임코드.주문자집단 &&
            !request.DistributionResponsibilityConfirmed)
        {
            yield return 공동구매국내운송필요조치코드.ConfirmDistributionResponsibility;
        }
    }

    private static int? ResolveRouteFare(
        int? routeSpecificFare,
        string selectedDestinationType,
        string optionDestinationType,
        int? selectedEstimatedFare)
    {
        if (routeSpecificFare.HasValue)
        {
            return routeSpecificFare;
        }

        return selectedDestinationType == optionDestinationType ? selectedEstimatedFare : null;
    }

    private static int? SumKnownCosts(params int?[] costs)
    {
        var knownCosts = costs.Where(cost => cost.HasValue).Select(cost => Math.Max(0, cost!.Value)).ToArray();
        return knownCosts.Length == 0 ? null : knownCosts.Sum();
    }

    private static decimal? ToCostPerKg(int? totalCost, decimal? cargoWeightKg)
    {
        if (!totalCost.HasValue || !cargoWeightKg.HasValue || cargoWeightKg.Value <= 0m)
        {
            return null;
        }

        return decimal.Round(totalCost.Value / cargoWeightKg.Value, 0, MidpointRounding.AwayFromZero);
    }

    private static string ResolveSourceRequestType(string? transportMode)
    {
        if (string.Equals(transportMode, 공동구매국내운송방식코드.FCL, StringComparison.OrdinalIgnoreCase))
        {
            return 공동구매국내운송원천의뢰유형코드.FclCargoTransport;
        }

        if (string.Equals(transportMode, 공동구매국내운송방식코드.LCL, StringComparison.OrdinalIgnoreCase))
        {
            return 공동구매국내운송원천의뢰유형코드.LclCargoTransport;
        }

        return 공동구매국내운송원천의뢰유형코드.ImportCargoTransport;
    }

    private static string NormalizeDestinationType(string? value)
    {
        if (string.Equals(value, 공동구매국내운송도착지유형코드.ApartmentComplexDirectDistribution, StringComparison.OrdinalIgnoreCase))
        {
            return 공동구매국내운송도착지유형코드.ApartmentComplexDirectDistribution;
        }

        if (string.Equals(value, 공동구매국내운송도착지유형코드.OrdererGroupRepresentativeDropoff, StringComparison.OrdinalIgnoreCase))
        {
            return 공동구매국내운송도착지유형코드.OrdererGroupRepresentativeDropoff;
        }

        if (string.Equals(value, 공동구매국내운송도착지유형코드.DedicatedWarehouse, StringComparison.OrdinalIgnoreCase))
        {
            return 공동구매국내운송도착지유형코드.DedicatedWarehouse;
        }

        return 공동구매국내운송도착지유형코드.ThreePlWarehouse;
    }

    private static string NormalizeApartmentUnitDistributionMode(string? value, bool driverPerformsApartmentUnitDistribution)
    {
        if (!driverPerformsApartmentUnitDistribution)
        {
            return 공동구매공동주택세대배송방식코드.None;
        }

        if (string.Equals(value, 공동구매공동주택세대배송방식코드.DriverToUnitDoor, StringComparison.OrdinalIgnoreCase))
        {
            return 공동구매공동주택세대배송방식코드.DriverToUnitDoor;
        }

        if (string.Equals(value, 공동구매공동주택세대배송방식코드.DriverToBuildingEntrance, StringComparison.OrdinalIgnoreCase))
        {
            return 공동구매공동주택세대배송방식코드.DriverToBuildingEntrance;
        }

        if (string.Equals(value, 공동구매공동주택세대배송방식코드.DriverToDesignatedPickupPoint, StringComparison.OrdinalIgnoreCase))
        {
            return 공동구매공동주택세대배송방식코드.DriverToDesignatedPickupPoint;
        }

        return 공동구매공동주택세대배송방식코드.DriverToBuildingEntrance;
    }

    private static string NormalizeDistributionPrivacyMode(string? value)
    {
        if (string.Equals(value, 공동구매세대배송개인정보방식코드.FullUnitAddressAfterAssignment, StringComparison.OrdinalIgnoreCase))
        {
            return 공동구매세대배송개인정보방식코드.FullUnitAddressAfterAssignment;
        }

        if (string.Equals(value, 공동구매세대배송개인정보방식코드.ManualChecklist, StringComparison.OrdinalIgnoreCase))
        {
            return 공동구매세대배송개인정보방식코드.ManualChecklist;
        }

        return 공동구매세대배송개인정보방식코드.MaskedUnitTokens;
    }

    private static string NormalizeUnitSortationMode(string? value, bool unitSortationRequired)
    {
        if (!unitSortationRequired)
        {
            return 공동구매세대별분류방식코드.NotRequired;
        }

        if (string.Equals(value, 공동구매세대별분류방식코드.ByRouteSequence, StringComparison.OrdinalIgnoreCase))
        {
            return 공동구매세대별분류방식코드.ByRouteSequence;
        }

        if (string.Equals(value, 공동구매세대별분류방식코드.ByBuilding, StringComparison.OrdinalIgnoreCase))
        {
            return 공동구매세대별분류방식코드.ByBuilding;
        }

        return 공동구매세대별분류방식코드.ByBuildingAndUnit;
    }

    private static string NormalizeUnitSortationLocation(string? value)
    {
        if (string.Equals(value, 공동구매세대별분류장소코드.ThreePlWarehouse, StringComparison.OrdinalIgnoreCase))
        {
            return 공동구매세대별분류장소코드.ThreePlWarehouse;
        }

        if (string.Equals(value, 공동구매세대별분류장소코드.ApartmentStagingArea, StringComparison.OrdinalIgnoreCase))
        {
            return 공동구매세대별분류장소코드.ApartmentStagingArea;
        }

        return 공동구매세대별분류장소코드.BondedArea;
    }

    private static string NormalizeUnitSortationResponsibleParty(string? value)
    {
        if (string.Equals(value, 공동구매세대별분류책임주체코드.OverseasForwarder, StringComparison.OrdinalIgnoreCase))
        {
            return 공동구매세대별분류책임주체코드.OverseasForwarder;
        }

        if (string.Equals(value, 공동구매세대별분류책임주체코드.플랫폼, StringComparison.OrdinalIgnoreCase))
        {
            return 공동구매세대별분류책임주체코드.플랫폼;
        }

        if (string.Equals(value, 공동구매세대별분류책임주체코드.주문자집단, StringComparison.OrdinalIgnoreCase))
        {
            return 공동구매세대별분류책임주체코드.주문자집단;
        }

        if (string.Equals(value, 공동구매세대별분류책임주체코드.DomesticOperator, StringComparison.OrdinalIgnoreCase))
        {
            return 공동구매세대별분류책임주체코드.DomesticOperator;
        }

        return 공동구매세대별분류책임주체코드.OverseasSeller;
    }

    private static string 단위포장라벨링방식정규화(string? value)
    {
        if (string.Equals(value, 공동구매단위포장라벨링방식코드.세대별송장라벨, StringComparison.OrdinalIgnoreCase))
        {
            return 공동구매단위포장라벨링방식코드.세대별송장라벨;
        }

        if (string.Equals(value, 공동구매단위포장라벨링방식코드.단위라벨없음, StringComparison.OrdinalIgnoreCase))
        {
            return 공동구매단위포장라벨링방식코드.단위라벨없음;
        }

        return 공동구매단위포장라벨링방식코드.상품정보스티커;
    }

    private static bool 단위포장라벨링확인됨(
        공동구매플랫폼국내운송초안요청 request,
        string labelingMode)
        => labelingMode switch
        {
            공동구매단위포장라벨링방식코드.세대별송장라벨 =>
                request.UnitInvoiceIssuedConfirmed && request.UnitPackageLabelsConfirmed,
            공동구매단위포장라벨링방식코드.단위라벨없음 => true,
            _ => request.UnitProductInfoStickerConfirmed &&
                 request.ProductInfoStickerMatchesImportedProductConfirmed
        };

    private static string NormalizeProductInfoStorageLocation(string? value)
    {
        if (string.Equals(value, 공동구매상품정보보관위치코드.OverseasSellerSystem, StringComparison.OrdinalIgnoreCase))
        {
            return 공동구매상품정보보관위치코드.OverseasSellerSystem;
        }

        if (string.Equals(value, 공동구매상품정보보관위치코드.OverseasForwarderSystem, StringComparison.OrdinalIgnoreCase))
        {
            return 공동구매상품정보보관위치코드.OverseasForwarderSystem;
        }

        if (string.Equals(value, 공동구매상품정보보관위치코드.ManualDocumentArchive, StringComparison.OrdinalIgnoreCase))
        {
            return 공동구매상품정보보관위치코드.ManualDocumentArchive;
        }

        return 공동구매상품정보보관위치코드.PlatformImportLedger;
    }

    private static string NormalizeUnitBarcodeScheme(string? value)
    {
        if (string.Equals(value, 공동구매세대단위바코드체계코드.InvoiceNumberBarcode, StringComparison.OrdinalIgnoreCase))
        {
            return 공동구매세대단위바코드체계코드.InvoiceNumberBarcode;
        }

        if (string.Equals(value, 공동구매세대단위바코드체계코드.PackageIdBarcode, StringComparison.OrdinalIgnoreCase))
        {
            return 공동구매세대단위바코드체계코드.PackageIdBarcode;
        }

        return 공동구매세대단위바코드체계코드.OrderNumberBarcode;
    }

    private static string NormalizeDistributionResponsibility(
        string? value,
        bool driverPerformsApartmentUnitDistribution,
        string destinationType)
    {
        if (destinationType != 공동구매국내운송도착지유형코드.ApartmentComplexDirectDistribution)
        {
            return 공동구매공동주택분배책임코드.None;
        }

        if (driverPerformsApartmentUnitDistribution ||
            string.Equals(value, 공동구매공동주택분배책임코드.Driver, StringComparison.OrdinalIgnoreCase))
        {
            return 공동구매공동주택분배책임코드.Driver;
        }

        if (string.Equals(value, 공동구매공동주택분배책임코드.SeparateWorker, StringComparison.OrdinalIgnoreCase))
        {
            return 공동구매공동주택분배책임코드.SeparateWorker;
        }

        if (string.Equals(value, 공동구매공동주택분배책임코드.PlatformArrangedWorker, StringComparison.OrdinalIgnoreCase))
        {
            return 공동구매공동주택분배책임코드.PlatformArrangedWorker;
        }

        return 공동구매공동주택분배책임코드.주문자집단;
    }

    private static string NormalizeTemperatureCode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return 공동구매온도코드.상온;
        }

        var normalized = value.Trim();
        if (string.Equals(normalized, 공동구매온도코드.냉동, StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("frozen", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("냉동", StringComparison.OrdinalIgnoreCase))
        {
            return 공동구매온도코드.냉동;
        }

        if (string.Equals(normalized, 공동구매온도코드.냉장, StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("chilled", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("refrigerated", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("냉장", StringComparison.OrdinalIgnoreCase))
        {
            return 공동구매온도코드.냉장;
        }

        return 공동구매온도코드.상온;
    }

    private static bool ResolveRequiresColdChain(공동구매플랫폼국내운송초안요청 request, string temperatureCode)
        => request.RequiresColdChain ||
            temperatureCode is 공동구매온도코드.냉장 or 공동구매온도코드.냉동;

    private static string NormalizeSettlementPolicy(string? value)
    {
        if (string.Equals(value, 공동구매국내운송정산정책코드.PlatformCollectsOrdererPaymentsAndPaysDriverAfterDropoff, StringComparison.OrdinalIgnoreCase))
        {
            return 공동구매국내운송정산정책코드.PlatformCollectsOrdererPaymentsAndPaysDriverAfterDropoff;
        }

        if (string.Equals(value, 공동구매국내운송정산정책코드.PlatformPaysAndRechargesOrdererGroup, StringComparison.OrdinalIgnoreCase))
        {
            return 공동구매국내운송정산정책코드.PlatformPaysAndRechargesOrdererGroup;
        }

        if (string.Equals(value, 공동구매국내운송정산정책코드.PlatformAbsorbsAsPromotion, StringComparison.OrdinalIgnoreCase))
        {
            return 공동구매국내운송정산정책코드.PlatformAbsorbsAsPromotion;
        }

        if (string.Equals(value, 공동구매국내운송정산정책코드.ManualSettlement, StringComparison.OrdinalIgnoreCase))
        {
            return 공동구매국내운송정산정책코드.ManualSettlement;
        }

        return 공동구매국내운송정산정책코드.PlatformCollectsOrdererPaymentsAndPaysDriverAfterDropoff;
    }

    private static IEnumerable<string> NormalizeOrdererPaymentMethods(IReadOnlyList<string>? values)
    {
        var source = values is { Count: > 0 }
            ? values
            :
            [
                공동구매국내운송주문자결제수단코드.Card,
                공동구매국내운송주문자결제수단코드.CashLike
            ];

        return source
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(NormalizeOrdererPaymentMethod)
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static string NormalizeOrdererPaymentMethod(string value)
    {
        if (string.Equals(value, 공동구매국내운송주문자결제수단코드.Card, StringComparison.OrdinalIgnoreCase))
        {
            return 공동구매국내운송주문자결제수단코드.Card;
        }

        if (string.Equals(value, 공동구매국내운송주문자결제수단코드.CashLike, StringComparison.OrdinalIgnoreCase))
        {
            return 공동구매국내운송주문자결제수단코드.CashLike;
        }

        if (string.Equals(value, 공동구매국내운송주문자결제수단코드.BankTransfer, StringComparison.OrdinalIgnoreCase))
        {
            return 공동구매국내운송주문자결제수단코드.BankTransfer;
        }

        if (string.Equals(value, 공동구매국내운송주문자결제수단코드.PlatformCredit, StringComparison.OrdinalIgnoreCase))
        {
            return 공동구매국내운송주문자결제수단코드.PlatformCredit;
        }

        return value.Trim();
    }

    private static string NormalizeDriverPayoutTrigger(string? value)
    {
        if (string.Equals(value, 공동구매국내운송기사지급트리거코드.PickupAndDropoffEvidenceVerified, StringComparison.OrdinalIgnoreCase))
        {
            return 공동구매국내운송기사지급트리거코드.PickupAndDropoffEvidenceVerified;
        }

        if (string.Equals(value, 공동구매국내운송기사지급트리거코드.ManualAdminApproval, StringComparison.OrdinalIgnoreCase))
        {
            return 공동구매국내운송기사지급트리거코드.ManualAdminApproval;
        }

        return 공동구매국내운송기사지급트리거코드.DropoffCompleted;
    }

    private static int NormalizeDriverPayoutDelayDays(int value)
        => Math.Clamp(value, 0, 30);

    private static IEnumerable<string> ResolveRequiredEvidenceCodes(공동구매플랫폼국내운송초안요청 request)
    {
        if (request.RequirePickupEvidence)
        {
            yield return 공동구매국내운송증빙코드.PickupPhoto;
        }

        if (request.RequireDropoffEvidence)
        {
            yield return 공동구매국내운송증빙코드.DropoffPhoto;
        }

        yield return 공동구매국내운송증빙코드.DropoffCompletion;

        if (request.RequireReceiptEvidence)
        {
            yield return 공동구매국내운송증빙코드.Receipt;
        }

        if (request.RequireCashReceipt)
        {
            yield return 공동구매국내운송증빙코드.CashReceipt;
        }
    }

    private static IEnumerable<string> ResolveRequiredDistributionEvidenceCodes(
        공동구매플랫폼국내운송초안요청 request,
        string destinationType)
    {
        if (destinationType != 공동구매국내운송도착지유형코드.ApartmentComplexDirectDistribution ||
            !request.DriverPerformsApartmentUnitDistribution)
        {
            yield break;
        }

        yield return "UnitSortationManifest";
        var labelingMode = 단위포장라벨링방식정규화(request.단위포장라벨링방식코드);
        if (labelingMode == 공동구매단위포장라벨링방식코드.세대별송장라벨)
        {
            yield return "UnitInvoiceLabels";
        }
        else if (labelingMode == 공동구매단위포장라벨링방식코드.상품정보스티커)
        {
            yield return "UnitProductInfoStickers";
        }

        if (request.UnitBarcodeScanLookupEnabled)
        {
            yield return "UnitBarcodeScanLookup";
        }

        yield return "LoadingSequenceManifest";
        yield return "ApartmentUnitDistributionChecklist";
        yield return "ApartmentUnitDeliveryCompletion";
    }

    private static string ResolveDestinationName(
        공동구매커머스이행계획Dto fulfillmentPlan,
        공동구매플랫폼국내운송초안요청 request,
        string destinationType)
    {
        if (destinationType == 공동구매국내운송도착지유형코드.ApartmentComplexDirectDistribution)
        {
            if (!string.IsNullOrWhiteSpace(request.ApartmentComplexName))
            {
                return request.ApartmentComplexName.Trim();
            }

            return fulfillmentPlan.주문자집단배송권명;
        }

        if (destinationType == 공동구매국내운송도착지유형코드.DedicatedWarehouse
            && !string.IsNullOrWhiteSpace(request.DedicatedWarehouseName))
        {
            return request.DedicatedWarehouseName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.ThreePlWarehouseName))
        {
            return request.ThreePlWarehouseName.Trim();
        }

        return string.IsNullOrWhiteSpace(request.DropoffRoadAddress)
            ? fulfillmentPlan.주문자집단배송권명
            : request.DropoffRoadAddress.Trim();
    }

    private static string NormalizePlatformUserId(string? value)
        => string.IsNullOrWhiteSpace(value) ? "platform" : value.Trim();

    private static string BuildClientRequestId(
        공동구매커머스이행계획Dto fulfillmentPlan,
        string sourceRequestType,
        string destinationType)
        => $"GP-IMPORT-DOMESTIC-{sourceRequestType}-{destinationType}-{fulfillmentPlan.계획Id}".ToUpperInvariant();

    private static string BuildSettlementMemo(
        공동구매커머스이행계획Dto fulfillmentPlan,
        공동구매플랫폼국내운송초안요청 request,
        string settlementPolicy,
        IReadOnlyList<string> ordererPaymentMethodCodes,
        string driverPayoutTriggerCode,
        int driverPayoutDelayDays,
        string destinationType)
    {
        var fare = request.EstimatedFareKrw.HasValue ? $"{request.EstimatedFareKrw.Value:N0} KRW" : "fare TBD";
        var routeLabel = destinationType switch
        {
            공동구매국내운송도착지유형코드.ApartmentComplexDirectDistribution
                => "bonded-area-to-apartment-direct-distribution",
            공동구매국내운송도착지유형코드.DedicatedWarehouse
                => "bonded-area-to-dedicated-warehouse",
            _ => "bonded-area-to-3PL"
        };

        if (string.Equals(settlementPolicy, 공동구매국내운송정산정책코드.PlatformCollectsOrdererPaymentsAndPaysDriverAfterDropoff, StringComparison.OrdinalIgnoreCase))
        {
            var paymentMethods = ordererPaymentMethodCodes.Count > 0
                ? string.Join(", ", ordererPaymentMethodCodes)
                : "payment method TBD";

            return $"Platform acts as shipper for {routeLabel} transport. Cost owner: orderer group {fulfillmentPlan.주문자집단배송권키}. Platform collects orderer payments ({paymentMethods}), holds funds until {driverPayoutTriggerCode}, then pays the driver to the registered settlement account after {driverPayoutDelayDays} day(s). Estimated fare: {fare}.";
        }

        return $"Platform acts as shipper for {routeLabel} transport. Cost owner: orderer group {fulfillmentPlan.주문자집단배송권키}. Policy: {settlementPolicy}. Estimated fare: {fare}.";
    }

    private static bool IsDestinationColdChainCompatible(
        공동구매플랫폼국내운송초안요청 request,
        string destinationType,
        bool requiresColdChain)
    {
        if (!requiresColdChain)
        {
            return true;
        }

        return request.ColdChainVehicleConfirmed
               && (destinationType != 공동구매국내운송도착지유형코드.ThreePlWarehouse
                   || request.ThreePlColdChainFacilityConfirmed)
               && (destinationType != 공동구매국내운송도착지유형코드.DedicatedWarehouse
                   || request.DedicatedWarehouseColdChainFacilityConfirmed);
    }

    private static string BuildColdChainMemo(
        string destinationType,
        bool requiresColdChain,
        공동구매플랫폼국내운송초안요청 request)
    {
        if (!requiresColdChain)
        {
            return "Ambient cargo. Cold-chain vehicle or 3PL cold storage confirmation is not required.";
        }

        if (destinationType == 공동구매국내운송도착지유형코드.ThreePlWarehouse)
        {
            return request.ThreePlColdChainFacilityConfirmed
                ? "Cold-chain cargo. Cold-chain vehicle and 3PL refrigerated/frozen facility must remain confirmed before dispatch."
                : "Cold-chain cargo. Select only a 3PL warehouse with refrigerated/frozen storage capability.";
        }

        if (destinationType == 공동구매국내운송도착지유형코드.DedicatedWarehouse)
        {
            return request.DedicatedWarehouseColdChainFacilityConfirmed
                ? "Cold-chain cargo. The dedicated warehouse refrigerated/frozen facility is confirmed."
                : "Cold-chain cargo. Confirm the dedicated warehouse refrigerated/frozen facility before dispatch.";
        }

        return "Cold-chain cargo. The delegated driver route must use a confirmed refrigerated/frozen-capable vehicle through apartment or representative dropoff.";
    }

    private static string BuildCostOptionMemo(
        공동구매커머스이행계획Dto fulfillmentPlan,
        string destinationType,
        string distributionResponsibility,
        bool requiresColdChain)
    {
        var coldChainMemo = requiresColdChain ? " Cold-chain confirmations are required." : string.Empty;
        return destinationType switch
        {
            공동구매국내운송도착지유형코드.ThreePlWarehouse
                => $"Inbound to 3PL for group purchase {fulfillmentPlan.공동구매Id}; useful for storage, sales-channel listing, and later outbound batch.{coldChainMemo}",
            공동구매국내운송도착지유형코드.DedicatedWarehouse
                => $"Inbound to the selected dedicated warehouse for group purchase {fulfillmentPlan.공동구매Id}; ownership and operating responsibility stay explicit.{coldChainMemo}",
            공동구매국내운송도착지유형코드.ApartmentComplexDirectDistribution
                => $"Direct apartment route with distribution responsibility: {distributionResponsibility}.{coldChainMemo}",
            공동구매국내운송도착지유형코드.OrdererGroupRepresentativeDropoff
                => $"Dropoff to orderer group representative point; internal distribution is outside driver scope.{coldChainMemo}",
            _ => $"Domestic route option for group purchase {fulfillmentPlan.공동구매Id}.{coldChainMemo}"
        };
    }

    private static string BuildUnitSortationMemo(
        공동구매플랫폼국내운송초안요청 request,
        string destinationType,
        string distributionResponsibility)
    {
        if (destinationType != 공동구매국내운송도착지유형코드.ApartmentComplexDirectDistribution ||
            distributionResponsibility != 공동구매공동주택분배책임코드.Driver ||
            !request.DriverPerformsApartmentUnitDistribution)
        {
            return "Driver unit delivery is not in scope, so unit-level pre-sortation is not required for dispatch.";
        }

        var responsibleParty = NormalizeUnitSortationResponsibleParty(request.UnitSortationResponsiblePartyCode);
        var sortationMode = NormalizeUnitSortationMode(request.UnitSortationModeCode, request.UnitSortationBeforePickupRequired);
        var location = NormalizeUnitSortationLocation(request.UnitSortationLocationCode);
        var labelingMode = 단위포장라벨링방식정규화(request.단위포장라벨링방식코드);
        var productInfoRegisteredBy = NormalizeUnitSortationResponsibleParty(request.ProductInfoRegisteredByPartyCode);
        var productInfoStorage = NormalizeProductInfoStorageLocation(request.ProductInfoStorageLocationCode);
        var barcodeScheme = NormalizeUnitBarcodeScheme(request.UnitBarcodeSchemeCode);
        var packageCount = request.SortedUnitPackageCount > 0 ? request.SortedUnitPackageCount.Value.ToString("N0") : "TBD";
        var barcodeMemo = request.UnitBarcodeScanLookupEnabled
            ? $" Barcode scheme: {barcodeScheme}; scans must resolve to masked recipient, unit, demand quantity, and delivery sequence."
            : " Barcode scan lookup is optional and currently not required.";

        return $"Driver unit-door delivery requires pre-sortation before pickup. Responsible party: {responsibleParty}. Mode: {sortationMode}. Location: {location}. Sorted unit packages: {packageCount}. Labeling mode: {labelingMode}. Product info registered by: {productInfoRegisteredBy}. Product info storage: {productInfoStorage}.{barcodeMemo}";
    }

    private static string BuildDestinationMemo(
        공동구매커머스이행계획Dto fulfillmentPlan,
        공동구매플랫폼국내운송초안요청 request,
        string destinationType,
        string unitDistributionMode)
    {
        if (destinationType == 공동구매국내운송도착지유형코드.ApartmentComplexDirectDistribution)
        {
            var distributionScope = request.DriverPerformsApartmentUnitDistribution
                ? $"driver unit distribution mode: {unitDistributionMode}, unit deliveries: {(request.ApartmentUnitDeliveryCount > 0 ? request.ApartmentUnitDeliveryCount.Value.ToString("N0") : "TBD")}"
                : "driver completes apartment complex dropoff only";

            return $"Direct apartment route for orderer group {fulfillmentPlan.주문자집단배송권키}; {distributionScope}.";
        }

        if (destinationType == 공동구매국내운송도착지유형코드.DedicatedWarehouse)
        {
            return $"Dedicated warehouse route for orderer group {fulfillmentPlan.주문자집단배송권키}.";
        }

        return $"3PL warehouse route for orderer group {fulfillmentPlan.주문자집단배송권키}.";
    }
}
