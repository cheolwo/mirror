namespace Ssalddel.Contracts.Common.Community;

public static class CargoNoticeRoutes
{
    public const string Api = "api/v1/community/cargo-notices";
}

public static class CargoNoticeStatus
{
    public const string Draft = "Draft";
    public const string Published = "Published";
    public const string Withdrawn = "Withdrawn";
    public const string Expired = "Expired";
}

/// <summary>실제 차량 자격이나 운송 적합성을 확정하지 않는 정보 공고의 차량 분류입니다.</summary>
public static class CargoNoticeVehicleKinds
{
    public const string Unspecified = "unspecified";
    public const string Motorcycle = "motorcycle";
    public const string Van = "van";
    public const string Truck = "truck";
    public static bool IsSupported(string? value) => value is Unspecified or Motorcycle or Van or Truck;
}

public sealed class CargoNoticeContent
{
    public string Title { get; set; } = string.Empty;
    public string GoodsName { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    public decimal? WeightKg { get; set; }
    public string VehicleKind { get; set; } = CargoNoticeVehicleKinds.Unspecified;
    public string PickupRegionKey { get; set; } = string.Empty;
    public string DropoffRegionKey { get; set; } = string.Empty;
    public DateTimeOffset? DesiredFromUtc { get; set; }
    public DateTimeOffset? DesiredUntilUtc { get; set; }
}

public sealed class CargoNoticeDraftRequest
{
    public Guid ClientRequestId { get; set; }
    public long ExpectedRevision { get; set; }
    public CargoNoticeContent Content { get; set; } = new();
}

public class CargoNoticeMutationRequest
{
    public Guid ClientRequestId { get; set; }
    public long ExpectedRevision { get; set; }
}

public sealed class CargoNoticePublishRequest : CargoNoticeMutationRequest
{
    public long ExpectedContentRevision { get; set; }
    public bool ConsentToPublicPublication { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
}

public sealed class CargoNoticeRegion
{
    public string RegionKey { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
}

/// <summary>공개 허용 필드만 포함합니다. 작성자·운송 의뢰·금융·연락처·상세 위치는 없습니다.</summary>
public sealed class CargoNoticePublicDto
{
    public string NoticeId { get; set; } = string.Empty;
    public long Revision { get; set; }
    public CargoNoticeContent Content { get; set; } = new();
    public string PickupRegionName { get; set; } = string.Empty;
    public string DropoffRegionName { get; set; } = string.Empty;
    public DateTimeOffset PublishedAtUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
}

public sealed class CargoNoticeOwnerDto
{
    public string NoticeId { get; set; } = string.Empty;
    public long Revision { get; set; }
    public long ContentRevision { get; set; }
    public string Status { get; set; } = CargoNoticeStatus.Draft;
    public CargoNoticeContent Content { get; set; } = new();
    public long? ConsentedContentRevision { get; set; }
    public DateTimeOffset? PublishedAtUtc { get; set; }
    public DateTimeOffset? ExpiresAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

public sealed class CargoNoticeListResponse
{
    public IReadOnlyList<CargoNoticePublicDto> Items { get; set; } = [];
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public int TotalCount { get; set; }
}

public sealed class CargoNoticeOwnerListResponse
{
    public IReadOnlyList<CargoNoticeOwnerDto> Items { get; set; } = [];
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public int TotalCount { get; set; }
}

/// <summary>원 요청의 결과 사본입니다. 이후 최신 내용은 내 공고를 별도로 재조회합니다.</summary>
public sealed class CargoNoticeCommandResponse
{
    public Guid ClientRequestId { get; set; }
    public string Operation { get; set; } = string.Empty;
    public DateTimeOffset AppliedAtUtc { get; set; }
    public CargoNoticeOwnerDto Notice { get; set; } = new();
}
