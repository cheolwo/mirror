namespace 살뜰.도메인.운송;

/// <summary>
/// 화주가 특정 운송 의뢰의 실제 업무 담당자를 지정한 판본별 원장 행입니다.
/// 한 판본의 전체 담당자 집합을 append-only로 보존하며 가장 큰 판본만 현재 권한으로 사용합니다.
/// </summary>
public sealed class 운송업무담당자배정
{
    public Guid Id { get; set; }

    public string 운송의뢰Id { get; set; } = string.Empty;

    public long 배정세트Revision { get; set; }

    public Guid 클라이언트요청Id { get; set; }

    public string 화주Id { get; set; } = string.Empty;

    public string 담당자UserId { get; set; } = string.Empty;

    public string 담당유형Code { get; set; } = string.Empty;

    /// <summary>
    /// 주 담당 행은 "PRIMARY", 보조 담당 행은 null입니다.
    /// 판본별 주 담당 한 명 제약을 DB에서도 지키기 위한 슬롯입니다.
    /// </summary>
    public string? 주담당Slot { get; set; }

    public string 권한CodesJson { get; set; } = "[]";

    public string 지정자UserId { get; set; } = string.Empty;

    public DateTime 지정시각Utc { get; set; }
}
