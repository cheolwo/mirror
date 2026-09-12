namespace Ssalddel.Contracts.Admin.Progress;

public sealed class 비정상운송사건검토요청
{
    public Guid 클라이언트요청Id { get; set; }
    public long 예상Revision { get; set; }
    public string 결정Code { get; set; } = string.Empty;
    public int? 정상확인수량 { get; set; }
    public int? 영향수량 { get; set; }
    public bool 보험적용가능성검토요청 { get; set; }
    public string 검토사유 { get; set; } = string.Empty;
}

public sealed class 비정상운송사건운영응답
{
    public string 사건StableId { get; set; } = string.Empty;
    public long 운송Id { get; set; }
    public string 운송의뢰Id { get; set; } = string.Empty;
    public string 사건유형Code { get; set; } = string.Empty;
    public string 원본예외Code { get; set; } = string.Empty;
    public string 단계Code { get; set; } = string.Empty;
    public string 상태Code { get; set; } = string.Empty;
    public string 현재담당Code { get; set; } = string.Empty;
    public bool 정산보류적용여부 { get; set; }
    public int? 전체수량 { get; set; }
    public int? 정상확인수량 { get; set; }
    public int? 영향수량 { get; set; }
    public string 업무통제상태Code { get; set; } = string.Empty;
    public string 보류범위Code { get; set; } = string.Empty;
    public bool 현장진행불가 { get; set; }
    public string 보험검토상태Code { get; set; } = string.Empty;
    public string 해결결과Code { get; set; } = string.Empty;
    public bool 증빙참조있음 { get; set; }
    public DateTime 최초신고시각Utc { get; set; }
    public DateTime 최근신고시각Utc { get; set; }
    public DateTime? 최근검토시각Utc { get; set; }
    public long Revision { get; set; }
    public bool 멱등재시도여부 { get; set; }
}
