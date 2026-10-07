using Ssalddel.Contracts.Common.Privacy;

namespace Ssalddel.Contracts.Common.Community;

public static class NeighborhoodStorageRoutes
{
    public const string Api = "api/v1/common/neighborhood-storage-spaces";
    public const string MineApi = Api + "/mine";
    public const string MapApi = Api + "/map";
    public const string ListPage = "/community/exchange/spaces";
    public const string WritePage = ListPage + "/new";
    public const string PrivacyNotice = "공개 설명에는 상세 주소·전화번호·출입 정보를 적지 마세요. 인계 정보는 본인과 현재 조건에 동의한 참여자만 확인합니다.";
    public static string DetailPage(string spaceId) => ListPage + "/" + Uri.EscapeDataString(spaceId);
    public static string DetailApi(string spaceId) => Api + "/" + Uri.EscapeDataString(spaceId);
}

public static class NeighborhoodStorageStatus
{
    public const string Draft = "draft";
    public const string Published = "published";
    public const string Paused = "paused";
    public const string Closed = "closed";
}

public static class NeighborhoodStorageReservationStatus
{
    public const string Reserved = "reserved";
    public const string InCustody = "in-custody";
    public const string Returned = "returned";
    public const string Cancelled = "cancelled";
}

public sealed class NeighborhoodStorageSpaceRequest
{
    public Guid RequestId { get; init; }
    public long ExpectedRevision { get; init; }
    public string PublicTitle { get; init; } = string.Empty;
    public string PublicDescription { get; init; } = string.Empty;
    public string PublicNeighborhoodRegionKey { get; init; } = string.Empty;
    public string GoodsKind { get; init; } = string.Empty;
    public decimal CapacityQuantity { get; init; }
    public string CapacityUnit { get; init; } = "상자";
    public DateTimeOffset AvailableFromUtc { get; init; }
    public DateTimeOffset AvailableUntilUtc { get; init; }
    [IsmsPProtectedData(PersonalDataFieldKey.DetailedAddress, "본인 보관 인계 장소 등록")]
    public string PrivateAddress { get; init; } = string.Empty;
    [IsmsPProtectedData(PersonalDataFieldKey.PhoneNumber, "본인 보관 인계 연락처 등록")]
    public string PrivateContact { get; init; } = string.Empty;
    [IsmsPProtectedData(PersonalDataFieldKey.DetailedAddress, "본인 보관 장소 출입·인계 안내 등록")]
    public string PrivateHandoverInstructions { get; init; } = string.Empty;
    public bool PublishOnCreate { get; init; }
}

public sealed class NeighborhoodStorageMutationRequest
{
    public Guid RequestId { get; init; }
    public long ExpectedRevision { get; init; }
}

/// <summary>공개 응답에는 소유자의 내부 ID와 실제 인계 정보가 없습니다.</summary>
public sealed class NeighborhoodStoragePublicDto
{
    public string SpaceId { get; init; } = string.Empty;
    public long Revision { get; init; }
    public long OfferRevision { get; init; }
    public string PublicTitle { get; init; } = string.Empty;
    public string PublicDescription { get; init; } = string.Empty;
    public NeighborhoodPublicRegionDto Region { get; init; } = new();
    public string GoodsKind { get; init; } = string.Empty;
    public decimal CapacityQuantity { get; init; }
    public string CapacityUnit { get; init; } = string.Empty;
    public DateTimeOffset AvailableFromUtc { get; init; }
    public DateTimeOffset AvailableUntilUtc { get; init; }
    public string LocationBoundary { get; init; } = "동네 대표점이며 실제 보관 장소가 아닙니다.";
}

public sealed class NeighborhoodStorageSpaceDto
{
    public string SpaceId { get; init; } = string.Empty;
    public long Revision { get; init; }
    public string Status { get; init; } = NeighborhoodStorageStatus.Draft;
    public NeighborhoodStoragePublicDto Public { get; init; } = new();
    [IsmsPProtectedData(PersonalDataFieldKey.DetailedAddress, "본인 또는 동의한 보관 참여자의 인계 장소 확인")]
    public string PrivateAddress { get; init; } = string.Empty;
    [IsmsPProtectedData(PersonalDataFieldKey.PhoneNumber, "본인 또는 동의한 보관 참여자의 연락처 확인")]
    public string PrivateContact { get; init; } = string.Empty;
    [IsmsPProtectedData(PersonalDataFieldKey.DetailedAddress, "본인 또는 동의한 보관 참여자의 출입·인계 안내 확인")]
    public string PrivateHandoverInstructions { get; init; } = string.Empty;
    public bool IsOwner { get; init; }
    public bool IdempotentReplay { get; init; }
    public string RequestOutcome { get; init; } = "committed";
    public string? RequestFailureCode { get; init; }
    public IReadOnlyList<NeighborhoodStorageReservationDto> Reservations { get; init; } = [];
    public string Notice { get; init; } = NeighborhoodStorageRoutes.PrivacyNotice;
}

public sealed class NeighborhoodStorageListResponse
{
    public IReadOnlyList<NeighborhoodStoragePublicDto> Items { get; init; } = [];
    public int Page { get; init; } = 1;
    public int TotalCount { get; init; }
    public string Notice { get; init; } = NeighborhoodStorageRoutes.PrivacyNotice;
}

public sealed class NeighborhoodStorageMapMarkerDto
{
    public NeighborhoodPublicRegionDto Region { get; init; } = new();
    public int SpaceCount { get; init; }
}

public sealed class NeighborhoodStorageMapResponse
{
    public IReadOnlyList<NeighborhoodStorageMapMarkerDto> Items { get; init; } = [];
    public string LocationBoundary { get; init; } = "보관 공간의 위치는 동네 대표점으로만 표시합니다.";
}

public sealed class NeighborhoodStorageReservationRequest
{
    public Guid RequestId { get; init; }
    public long ExpectedRevision { get; init; }
    public string CollaborationId { get; init; } = string.Empty;
}

public sealed class NeighborhoodStorageReservationActionRequest
{
    public Guid RequestId { get; init; }
    public long ExpectedRevision { get; init; }
    public string Action { get; init; } = string.Empty;
}

public sealed class NeighborhoodStorageReservationDto
{
    public string CollaborationId { get; init; } = string.Empty;
    public long TermsRevision { get; init; }
    public decimal Quantity { get; init; }
    public string Unit { get; init; } = string.Empty;
    public DateTimeOffset FromUtc { get; init; }
    public DateTimeOffset UntilUtc { get; init; }
    public string Status { get; init; } = NeighborhoodStorageReservationStatus.Reserved;
    public bool OwnerIntakeConfirmed { get; init; }
    public bool RequesterIntakeConfirmed { get; init; }
    public bool OwnerReturnConfirmed { get; init; }
    public bool RequesterReturnConfirmed { get; init; }
    public IReadOnlyList<string> AllowedActions { get; init; } = [];
}
