namespace Ssalddel.Contracts.Admin.Restaurants;

public sealed class 음식점리뷰관리목록응답
{
    public IReadOnlyList<음식점리뷰관리항목응답> Items { get; set; } = [];
}

public sealed class 음식점리뷰관리항목응답
{
    public long 리뷰Id { get; set; }
    public long 음식점Id { get; set; }
    public string 음식점명 { get; set; } = string.Empty;
    public string 주문자UserId { get; set; } = string.Empty;
    public string? 주문번호 { get; set; }
    public int 별점 { get; set; }
    public string 내용 { get; set; } = string.Empty;
    public bool 사진포함여부 { get; set; }
    public bool 같은음식점기준저평점3회연속여부 { get; set; }
    public bool 사장노출허용여부 { get; set; }
    public bool 관리자검토필요여부 { get; set; }
    public bool 관리자게시강제여부 { get; set; }
    public bool 현재노출여부 { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? 게시종료일시Utc { get; set; }
    public string? 최근조치사유 { get; set; }
}

public sealed class 음식점리뷰관리조치요청
{
    public bool 관리자게시강제여부 { get; set; }
    public DateTime? 게시종료일시Utc { get; set; }
    public string 조치사유 { get; set; } = string.Empty;
    public string 관리자UserId { get; set; } = string.Empty;
}

public sealed class 음식점리뷰운영정책응답
{
    public long Id { get; set; }
    public int 기본저평점게시일수 { get; set; }
    public IReadOnlyList<int> 허용게시일수옵션 { get; set; } = [3, 7];
    public DateTime UpdatedAt { get; set; }
}

public sealed class 음식점리뷰운영정책수정요청
{
    public int 기본저평점게시일수 { get; set; }
    public string 수정자UserId { get; set; } = string.Empty;
}

public sealed class 음식배달요금정책응답
{
    public decimal BaseFee { get; set; } = 3000m;
    public int IncludedDistanceMeters { get; set; } = 1000;
    public int DistanceUnitMeters { get; set; } = 100;
    public decimal DistanceUnitFee { get; set; } = 120m;
    public decimal MinimumFee { get; set; } = 3000m;
    public decimal DriverBasePayout { get; set; } = 2500m;
    public decimal DriverDistanceUnitPayout { get; set; } = 90m;
    public decimal DriverMinimumPayout { get; set; } = 2500m;
    public bool DriverWeatherSurchargeEnabled { get; set; } = true;
    public decimal DriverWeatherSurcharge { get; set; } = 1000m;
    public string DriverWeatherSurchargePolicyRevision { get; set; } = "food-weather-surcharge.r1";
    public DateTime UpdatedAtUtc { get; set; }
    public string UpdatedByUserId { get; set; } = string.Empty;
}
