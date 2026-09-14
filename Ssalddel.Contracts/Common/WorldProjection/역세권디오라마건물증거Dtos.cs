namespace Ssalddel.Contracts.Common.WorldProjection;

public static class 역세권디오라마건물증거Routes
{
    public const string Base =
        "api/v1/admin/world/stations/{transitStationStableId}/diorama-building-evidence";
    public const string Manifest = "manifest";
    public const string PresentationBuildingBindings = "presentation-building-bindings";
    public const string PresentationBuildingAddresses = "presentation-building-addresses";
}

public static class 역세권디오라마건물증거SchemaVersions
{
    public const string ManifestV1 = "station-diorama-building-evidence-manifest.v1";
    public const string BindingLedgerV1 = "presentation-building-binding-ledger.v1";
    public const string AddressLedgerV1 = "presentation-building-address-assignment-ledger.v1";
}

public static class 역세권디오라마건물증거KindCodes
{
    public const string RoadAddress = "RoadAddress";
    public const string ParcelIdentifier = "ParcelIdentifier";
    public const string ParcelGeometry = "ParcelGeometry";
}

public static class 역세권디오라마건물증거CollectionStatusCodes
{
    public const string Collected = "Collected";
    public const string NotCollected = "NotCollected";
}

public static class 역세권디오라마건물증거CompletenessCodes
{
    public const string Complete = "Complete";
    public const string Missing = "Missing";
}

public sealed class 역세권디오라마건물증거ManifestDto
{
    public string SchemaVersion { get; set; } = 역세권디오라마건물증거SchemaVersions.ManifestV1;
    public string TransitStationStableId { get; set; } = string.Empty;
    public string RegionStableId { get; set; } = string.Empty;
    public string SourceVintage { get; set; } = string.Empty;
    public DateOnly EvidenceAsOf { get; set; }
    public string BindingLedgerRevision { get; set; } = string.Empty;
    public string BindingLedgerContentSha256 { get; set; } = string.Empty;
    public string AddressLedgerRevision { get; set; } = string.Empty;
    public string AddressLedgerContentSha256 { get; set; } = string.Empty;
    public string ProjectionHashSha256 { get; set; } = string.Empty;
    public 역세권디오라마건물증거집계Dto Summary { get; set; } = new();
    public List<역세권디오라마자료점검Dto> EvidenceChecks { get; set; } = [];
    public 역세권디오라마비권위경계Dto Boundary { get; set; } = new();
}

public sealed class 역세권디오라마건물증거집계Dto
{
    public int PresentationBuildingCount { get; set; }
    public int ReferenceBuildingCount { get; set; }
    public int BindingCount { get; set; }
    public int UnresolvedReferenceBuildingCount { get; set; }
    public int ParcelIdentifierCount { get; set; }
    public int UniqueParcelIdentifierCount { get; set; }
    public int RoadAddressCandidateBuildingCount { get; set; }
    public int UnresolvedAddressBuildingCount { get; set; }
    public int OfficialPromotedCount { get; set; }
}

public sealed class 역세권디오라마자료점검Dto
{
    public string EvidenceKindCode { get; set; } = string.Empty;
    public string CollectionStatusCode { get; set; } = string.Empty;
    public string CompletenessCode { get; set; } = string.Empty;
    public int CoveredBuildingCount { get; set; }
    public bool UseAuthorityGranted { get; set; }
    public string LimitationCode { get; set; } = string.Empty;
}

public sealed class 화면건물결속LedgerDto
{
    public string SchemaVersion { get; set; } = string.Empty;
    public string Revision { get; set; } = string.Empty;
    public string StationStableId { get; set; } = string.Empty;
    public string RegionStableId { get; set; } = string.Empty;
    public string PresentationOverlayRevision { get; set; } = string.Empty;
    public string PresentationOverlayFileSha256 { get; set; } = string.Empty;
    public string PresentationOverlayContentSha256 { get; set; } = string.Empty;
    public string ReferenceMapRevision { get; set; } = string.Empty;
    public string ReferenceMapSha256 { get; set; } = string.Empty;
    public string FileSha256 { get; set; } = string.Empty;
    public string NormalizedDataRowsSha256 { get; set; } = string.Empty;
    public List<화면건물주소출처영수증Dto> SourceReceipts { get; set; } = [];
    public 화면건물결속집계Dto Summary { get; set; } = new();
    public List<화면건물결속상태Dto> PresentationBuildings { get; set; } = [];
    public List<참고건물결속상태Dto> ReferenceBuildings { get; set; } = [];
    public List<화면참고건물결속Dto> Bindings { get; set; } = [];
    public 역세권디오라마비권위경계Dto Boundary { get; set; } = new();
    public string ContentSha256 { get; set; } = string.Empty;
    public string ProjectionHashSha256 { get; set; } = string.Empty;
}

public sealed class 화면건물결속집계Dto
{
    public int PresentationBuildingCount { get; set; }
    public int ReferenceBuildingCount { get; set; }
    public int BindingCount { get; set; }
    public int UnresolvedReferenceBuildingCount { get; set; }
    public Dictionary<string, int> PresentationBindingStates { get; set; } = new(StringComparer.Ordinal);
}

public sealed class 화면건물결속상태Dto
{
    public string PresentationBuildingStableId { get; set; } = string.Empty;
    public string SourceFeatureId { get; set; } = string.Empty;
    public string BindingState { get; set; } = string.Empty;
    public List<string> ReferenceBuildingStableIds { get; set; } = [];
    public string BindingMethod { get; set; } = string.Empty;
    public bool Ambiguous { get; set; }
}

public sealed class 참고건물결속상태Dto
{
    public string ReferenceBuildingStableId { get; set; } = string.Empty;
    public string BindingState { get; set; } = string.Empty;
    public string PresentationBuildingStableId { get; set; } = string.Empty;
}

public sealed class 화면참고건물결속Dto
{
    public string PresentationBuildingStableId { get; set; } = string.Empty;
    public string ReferenceBuildingStableId { get; set; } = string.Empty;
    public string BindingMethod { get; set; } = string.Empty;
    public decimal CentroidDistanceMeters { get; set; }
    public decimal IntersectionOverUnion { get; set; }
    public decimal SmallerFootprintCoverage { get; set; }
}

public sealed class 화면건물주소LedgerDto
{
    public string SchemaVersion { get; set; } = string.Empty;
    public string Revision { get; set; } = string.Empty;
    public string StationStableId { get; set; } = string.Empty;
    public string RegionStableId { get; set; } = string.Empty;
    public string SourceVintage { get; set; } = string.Empty;
    public string EvidenceAsOf { get; set; } = string.Empty;
    public string BindingLedgerRevision { get; set; } = string.Empty;
    public string BindingLedgerContentSha256 { get; set; } = string.Empty;
    public List<화면건물주소출처영수증Dto> SourceReceipts { get; set; } = [];
    public 화면건물주소집계Dto Summary { get; set; } = new();
    public List<화면건물주소할당Dto> Assignments { get; set; } = [];
    public 역세권디오라마비권위경계Dto Boundary { get; set; } = new();
    public string ContentSha256 { get; set; } = string.Empty;
    public string FileSha256 { get; set; } = string.Empty;
    public string NormalizedDataRowsSha256 { get; set; } = string.Empty;
    public string ProjectionHashSha256 { get; set; } = string.Empty;
}

public sealed class 화면건물주소출처영수증Dto
{
    public string RoleCode { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string Sha256 { get; set; } = string.Empty;
    public string SourceRevision { get; set; } = string.Empty;
    public string? ContentSha256 { get; set; }
}

public sealed class 화면건물주소집계Dto
{
    public int PresentationBuildingCount { get; set; }
    public int ParcelIdentifierCount { get; set; }
    public int UniqueParcelIdentifierCount { get; set; }
    public int LegalDongAndJibunCount { get; set; }
    public int SourceBuildingIdentifierCount { get; set; }
    public int SourceUfidCount { get; set; }
    public 화면건물주소후보집계Dto RawParcelAddressCandidates { get; set; } = new();
    public int ReferenceOfficialAddressCount { get; set; }
    public 화면건물주소교차검증집계Dto CrossSourceReconciliation { get; set; } = new();
    public Dictionary<string, int> ResolutionStates { get; set; } = new(StringComparer.Ordinal);
    public int OfficialPromotedCount { get; set; }
    public int NearestAddressInferenceCount { get; set; }
}

public sealed class 화면건물주소후보집계Dto
{
    public int Single { get; set; }
    public int Multiple { get; set; }
    public int None { get; set; }
}

public sealed class 화면건물주소교차검증집계Dto
{
    public int Agreement { get; set; }
    public int Conflict { get; set; }
    public int ParcelCandidateMissing { get; set; }
}

public sealed class 화면건물주소할당Dto
{
    public string PresentationBuildingStableId { get; set; } = string.Empty;
    public string SourceFeatureId { get; set; } = string.Empty;
    public string ParcelIdentifierPnu { get; set; } = string.Empty;
    public string LegalDongCode { get; set; } = string.Empty;
    public string LegalDongName { get; set; } = string.Empty;
    public string Jibun { get; set; } = string.Empty;
    public string SourceBuildingIdentifier { get; set; } = string.Empty;
    public string SourceUfid { get; set; } = string.Empty;
    public string ReferenceBuildingStableId { get; set; } = string.Empty;
    public string ReferenceAddressResolutionState { get; set; } = string.Empty;
    public List<string> ReferenceOfficialCompositeKeys { get; set; } = [];
    public List<도로명주소후보Dto> ParcelAddressCandidates { get; set; } = [];
    public string ResolutionState { get; set; } = string.Empty;
    public string ReconciliationState { get; set; } = string.Empty;
    public string AssignmentMethod { get; set; } = string.Empty;
    public string UnresolvedReason { get; set; } = string.Empty;
    public string SourceVintage { get; set; } = string.Empty;
    public 화면건물주소원천HashDto SourceHashes { get; set; } = new();
    public bool ObservationPresentationOnly { get; set; }
    public bool DistributionApproved { get; set; }
    public bool DeliveryEligible { get; set; }
    public bool PriceObservationEligible { get; set; }
    public bool BusinessLocationEligible { get; set; }
    public bool UnityApplyAllowed { get; set; }
    public bool TraversalReady { get; set; }
    public bool GameplayReady { get; set; }
}

public sealed class 도로명주소후보Dto
{
    public string AddressStableId { get; set; } = string.Empty;
    public string OfficialCompositeKey { get; set; } = string.Empty;
    public string CanonicalRoadAddress { get; set; } = string.Empty;
    public List<string> OfficialBuildingManagementNumbers { get; set; } = [];
    public List<string> EvidenceMethods { get; set; } = [];
}

public sealed class 화면건물주소원천HashDto
{
    public string AlD010Sha256 { get; set; } = string.Empty;
    public string MoisBuildingDatabaseZipSha256 { get; set; } = string.Empty;
    public string MoisRelatedParcelSha256 { get; set; } = string.Empty;
}

public sealed class 역세권디오라마비권위경계Dto
{
    public bool ObservationPresentationOnly { get; set; }
    public bool DistributionApproved { get; set; }
    public bool DeliveryEligible { get; set; }
    public bool PriceObservationEligible { get; set; }
    public bool BusinessLocationEligible { get; set; }
    public bool UnityApplyAllowed { get; set; }
    public bool TraversalReady { get; set; }
    public bool GameplayReady { get; set; }
    public bool GeometryIncluded { get; set; }
    public bool NearestBindingInferenceAllowed { get; set; }
    public bool ParcelGeometryCollected { get; set; }
    public bool NearestAddressInferenceAllowed { get; set; }
    public bool OfficialBuildingIdentityConfirmed { get; set; }
}
