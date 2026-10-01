namespace Hongdal.Contracts.Common.Warehouse;

public sealed class 창고요약응답
{
    public long Id { get; set; }
    public string 창고명 { get; set; } = string.Empty;
    public string 소유자UserId { get; set; } = string.Empty;
    public string 소유자유형 { get; set; } = string.Empty;
    public string 창고유형 { get; set; } = string.Empty;
    public string 물류대행지분류 { get; set; } = LogisticsProxySiteTypes.DeliveryAgency;
    public string 주소 { get; set; } = string.Empty;
    public string 담당자명 { get; set; } = string.Empty;
    public string 연락처 { get; set; } = string.Empty;
    public decimal? 위도 { get; set; }
    public decimal? 경도 { get; set; }
    public bool 기본창고여부 { get; set; }
    public bool IsActive { get; set; }
    public bool 가상창고여부
        => string.Equals(창고유형, 창고유형코드.가상창고, StringComparison.OrdinalIgnoreCase);
}

public sealed class 창고목록응답
{
    public IReadOnlyList<창고요약응답> Items { get; set; } = [];
}

public sealed class 창고저장요청
{
    public string 창고명 { get; set; } = string.Empty;
    public string 소유자유형 { get; set; } = string.Empty;
    public string 창고유형 { get; set; } = 창고유형코드.가상창고;
    public string 물류대행지분류 { get; set; } = LogisticsProxySiteTypes.DeliveryAgency;
    public string 주소 { get; set; } = string.Empty;
    public string 담당자명 { get; set; } = string.Empty;
    public string 연락처 { get; set; } = string.Empty;
    public decimal? 위도 { get; set; }
    public decimal? 경도 { get; set; }
    public bool 기본창고여부 { get; set; } = true;
}

/// <summary>
/// 실물 보관 시설뿐 아니라 주문자의 자택·수령지를 논리적인 입고 지점으로 다루기 위한 창고 유형입니다.
/// 가상 창고는 재고가 실제 입고 완료되었다는 뜻이 아니라, 주문 물품이 귀속될 수령지를 뜻합니다.
/// </summary>
public static class 창고유형코드
{
    public const string 실제창고 = "실제창고";
    public const string 가상창고 = "가상창고";
    public const string 차량창고 = "차량창고";
    public const string 임시보관소 = "임시보관소";
}

public sealed class 창고사용자항목응답
{
    public long Id { get; set; }
    public long 창고Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string 사용자명 { get; set; } = string.Empty;
    public string 역할명 { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
}

public sealed class 창고사용자목록응답
{
    public IReadOnlyList<창고사용자항목응답> Items { get; set; } = [];
}

public sealed class 창고사용자저장요청
{
    public string UserId { get; set; } = string.Empty;
    public string 역할명 { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
}
