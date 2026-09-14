using Ssalddel.Contracts.Common.Metadata;

namespace Ssalddel.Contracts.Common.DeliveryZones;

public static class 배달운영권역Routes
{
    public const string AdminBase = "api/v1/admin/delivery-territories";
}

public static class 배달운영권역SourceScopes
{
    public const string NortheastSeoulRiderR1 = "delivery-territory-source-scope:northeast-seoul-rider.r1";
}

public static class 배달운영권역상태Codes
{
    public const string Draft = "Draft";
}

public static class 배달운영권역행정동상태Codes
{
    public const string Included = "Included";
    public const string Excluded = "Excluded";
}

public static class 행정동운영Module상태Codes
{
    public const string ProfileRegistered = "ProfileRegistered";
    public const string OfficialJurisdictionConfirmed = "OfficialJurisdictionConfirmed";
    public const string WaitingForSpatialProjection = "WaitingForSpatialProjection";
    public const string ProjectionPublished = "ProjectionPublished";
}

public static class 배달운영권역ActionIds
{
    public const string ReplaceAdministrativeDongs = "ReplaceAdministrativeDongs";
}

public sealed class 행정동운영법정동RefDto
{
    public string LegalAreaStableId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;
}

[SsalddelCodeMetadata(
    SsalddelCodeFeatureKeys.PlatformDeliveryZoneLedger,
    SsalddelCodeLayer.Contract,
    "공식 행정동 관할과 별도 Mongo 디오라마 게시 상태를 관리자용 운영 셀로 합성한다.",
    FlowOrder = 10,
    Boundary = "역세권 1km 창, 주문·배차 권위, 추정 경계 또는 미게시 타일을 만들지 않는다.")]
public sealed class 행정동운영ModuleDto
{
    public string SourceScopeStableId { get; set; } = string.Empty;

    public string AdministrativeAreaStableId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public IReadOnlyList<행정동운영법정동RefDto> LegalAreas { get; set; } = [];

    public string JurisdictionSourceId { get; set; } = string.Empty;

    public string JurisdictionDatasetId { get; set; } = string.Empty;

    public string JurisdictionSourceVersion { get; set; } = string.Empty;

    public string JurisdictionDataRevision { get; set; } = string.Empty;

    public DateTimeOffset JurisdictionEvidenceAsOfUtc { get; set; }

    public string ModuleProfileStatusCode { get; set; } = 행정동운영Module상태Codes.ProfileRegistered;

    public string JurisdictionReadinessCode { get; set; } = 행정동운영Module상태Codes.OfficialJurisdictionConfirmed;

    public string DioramaReadinessCode { get; set; } = 행정동운영Module상태Codes.WaitingForSpatialProjection;

    public string? DioramaSourceVintage { get; set; }

    public string? DioramaProjectionHashSha256 { get; set; }

    public bool ObservationPresentationOnly { get; set; } = true;

    public bool DistributionApproved { get; set; }

    public string? AssignedDeliveryTerritoryStableId { get; set; }

    public string? AssignedDeliveryTerritoryDisplayName { get; set; }
}

public sealed class 배달운영권역행정동Dto
{
    public string AdministrativeAreaStableId { get; set; } = string.Empty;

    public string AdministrativeAreaDisplayName { get; set; } = string.Empty;

    public IReadOnlyList<행정동운영법정동RefDto> LegalAreas { get; set; } = [];

    public string MembershipStateCode { get; set; } = 배달운영권역행정동상태Codes.Included;

    public string JurisdictionSourceId { get; set; } = string.Empty;

    public string JurisdictionDataRevision { get; set; } = string.Empty;

    public DateTime IncludedAtUtc { get; set; }

    public DateTime? ExcludedAtUtc { get; set; }
}

public sealed class 배달운영권역Dto
{
    public string DeliveryTerritoryStableId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string SourceScopeStableId { get; set; } = string.Empty;

    public string StatusCode { get; set; } = 배달운영권역상태Codes.Draft;

    public long Revision { get; set; }

    public IReadOnlyList<배달운영권역행정동Dto> AdministrativeDongs { get; set; } = [];

    public IReadOnlyList<string> AvailableActionIds { get; set; } = [];

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}

public sealed class 배달운영권역Draft생성Request
{
    public string DeliveryTerritoryStableId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string SourceScopeStableId { get; set; } = 배달운영권역SourceScopes.NortheastSeoulRiderR1;

    public IReadOnlyList<string> AdministrativeAreaStableIds { get; set; } = [];

    public string ClientRequestId { get; set; } = string.Empty;
}

public sealed class 배달운영권역행정동교체Request
{
    public long ExpectedRevision { get; set; }

    public string SourceScopeStableId { get; set; } = 배달운영권역SourceScopes.NortheastSeoulRiderR1;

    public IReadOnlyList<string> AdministrativeAreaStableIds { get; set; } = [];

    public string ClientRequestId { get; set; } = string.Empty;
}
