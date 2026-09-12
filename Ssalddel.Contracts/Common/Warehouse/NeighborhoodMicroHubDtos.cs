namespace Ssalddel.Contracts.Common.Warehouse;

public static class NeighborhoodMicroHubRoutes
{
    public const string Common = "api/v1/common/neighborhood-logistics-hubs";
    public const string Admin = "api/v1/admin/neighborhood-logistics-hubs";
}

public sealed class 생활권물류거점신청Request
{
    public string 신청가원장Id { get; set; } = string.Empty;
    public string 공간StableId { get; set; } = string.Empty;
    public string 관리담당자UserId { get; set; } = string.Empty;
    public string 생활권Key { get; set; } = string.Empty;
    public string 대략위치Label { get; set; } = string.Empty;
    public string 정확위치보호참조 { get; set; } = string.Empty;
    public int 최대동시보관건수 { get; set; }
    public decimal 최대총중량Kg { get; set; }
    public int 최대보관시간분 { get; set; }
    public string 입고가능시간창 { get; set; } = string.Empty;
    public string 수령가능시간창 { get; set; } = string.Empty;
    public decimal 완료건당고정보상 { get; set; }
}

public sealed class 생활권물류거점동의Request
{
    public bool 동의 { get; set; }
    public long ExpectedRevision { get; set; }
}

public sealed class 생활권물류거점검토Request
{
    public bool 현장확인 { get; set; }
    public bool 플랫폼승인 { get; set; }
    public string 사유 { get; set; } = string.Empty;
    public long ExpectedRevision { get; set; }
}

public sealed class 생활권물류거점상태변경Request
{
    public string 상태Code { get; set; } = string.Empty;
    public string 사유 { get; set; } = string.Empty;
    public long ExpectedRevision { get; set; }
}

public sealed class 생활권물류거점예약Request
{
    public string 업무StableId { get; set; } = string.Empty;
    public string 멱등성Key { get; set; } = string.Empty;
    public decimal 중량Kg { get; set; }
    public int 보관시간분 { get; set; }
}

public sealed class 생활권물류거점Response
{
    public Guid Id { get; set; }
    public string StableId { get; set; } = string.Empty;
    public string 신청가원장Id { get; set; } = string.Empty;
    public string 공간StableId { get; set; } = string.Empty;
    public string 생활권Key { get; set; } = string.Empty;
    public string 대략위치Label { get; set; } = string.Empty;
    public string 상태Code { get; set; } = string.Empty;
    public bool 소유자동의 { get; set; }
    public bool 관리주체동의 { get; set; }
    public bool 플랫폼승인 { get; set; }
    public bool 현장확인 { get; set; }
    public int 최대동시보관건수 { get; set; }
    public int 현재예약건수 { get; set; }
    public decimal 최대총중량Kg { get; set; }
    public int 최대보관시간분 { get; set; }
    public string 입고가능시간창 { get; set; } = string.Empty;
    public string 수령가능시간창 { get; set; } = string.Empty;
    public decimal 완료건당고정보상 { get; set; }
    public string 보상통화Code { get; set; } = "KRW";
    public long? 연결창고Id { get; set; }
    public bool 실운영허용 { get; set; }
    public long Revision { get; set; }
    public string 상태사유 { get; set; } = string.Empty;
}

public sealed class 생활권물류거점공개Response
{
    public string StableId { get; set; } = string.Empty;
    public string 생활권Key { get; set; } = string.Empty;
    public string 대략위치Label { get; set; } = string.Empty;
    public string 상태Code { get; set; } = string.Empty;
    public bool 기사인계가능 { get; set; }
    public bool 주문자수령가능 { get; set; }
    public string 가동상태Code { get; set; } = string.Empty;
}

public sealed class 생활권물류거점예약Response
{
    public Guid 예약Id { get; set; }
    public string 업무StableId { get; set; } = string.Empty;
    public string 상태Code { get; set; } = string.Empty;
    public bool 기존예약재사용 { get; set; }
}

public sealed class 생활권물류거점완료Response
{
    public Guid 예약Id { get; set; }
    public string 상태Code { get; set; } = string.Empty;
    public decimal 모의보상금액 { get; set; }
    public bool 실제지급대상 { get; set; }
}
