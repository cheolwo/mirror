using Ssalddel.Contracts.Common.Privacy;

namespace Ssalddel.Contracts.Admin.Progress;

public sealed class 운송진행응답
{
    public long Id { get; set; }
    public string 운송번호 { get; set; } = string.Empty;
    public string 상태 { get; set; } = string.Empty;
    /// <summary>원의뢰에 입력된 운송 방식입니다. 표시 명칭과 별개로 원문을 보존합니다.</summary>
    public string? 운송방식 { get; set; }
    public string? 차량종류 { get; set; }
    public decimal? 화물중량Kg { get; set; }
    public int? 화물수량 { get; set; }
    /// <summary>원의뢰의 UTC 상차 시간창입니다. null은 시간 미확인입니다.</summary>
    public DateTime? 상차시간창시작일시 { get; set; }
    public DateTime? 상차시간창종료일시 { get; set; }
    /// <summary>원의뢰의 UTC 하차 시간창입니다. null은 시간 미확인입니다.</summary>
    public DateTime? 하차시간창시작일시 { get; set; }
    public DateTime? 하차시간창종료일시 { get; set; }
    public DateTime? 출발_픽업 { get; set; }
    public DateTime? 도착 { get; set; }
    public string 기사_운송자 { get; set; } = string.Empty;
    /// <summary>현재 명시 배정과 근무 이후의 유효 GPS만 제공합니다. 지연·종료 상태의 좌표는 null입니다.</summary>
    [IsmsPProtectedData(PersonalDataFieldKey.LocationCoordinate, "진행 중인 현재 배정 운송 확인", ProtectionNote = "서버 관리자 전용이며 종료·오래된 좌표는 비공개")]
    public decimal? 기사최근위도 { get; set; }
    [IsmsPProtectedData(PersonalDataFieldKey.LocationCoordinate, "진행 중인 현재 배정 운송 확인", ProtectionNote = "서버 관리자 전용이며 종료·오래된 좌표는 비공개")]
    public decimal? 기사최근경도 { get; set; }
    public DateTime? 기사최근위치시각Utc { get; set; }
    public string 기사위치상태Code { get; set; } = string.Empty;
    public string 출발지 { get; set; } = string.Empty;
    public string 도착지 { get; set; } = string.Empty;
    public decimal? 운임 { get; set; }
    public bool 예외신고됨 { get; set; }
    public string 최근예외단계 { get; set; } = string.Empty;
    public string 최근예외코드 { get; set; } = string.Empty;
    public string 최근예외메시지 { get; set; } = string.Empty;
    public bool 관리자확인필요 { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class 운송이벤트로그응답
{
    public long Id { get; set; }
    public string 의뢰Id { get; set; } = string.Empty;
    public string 이벤트타입 { get; set; } = string.Empty;
    public DateTime 이벤트시각 { get; set; }
    public string 메타데이터 { get; set; } = string.Empty;
}

public sealed class 운송원장이벤트응답
{
    public string 의뢰Id { get; set; } = string.Empty;
    public string 의뢰상태 { get; set; } = string.Empty;
    public string 결제상태 { get; set; } = string.Empty;
    public string 배차상태 { get; set; } = string.Empty;
    public string 정산상태 { get; set; } = string.Empty;
    public DateTime? 의뢰UpdatedAt { get; set; }
    public long? 운송Id { get; set; }
    public string 운송상태 { get; set; } = string.Empty;
    public DateTime? 운송UpdatedAt { get; set; }
    public string Mongo원장Id { get; set; } = string.Empty;
    public bool Mongo원장존재 { get; set; }
    public string Mongo원장상태 { get; set; } = string.Empty;
    public string Mongo원장현재단계Key { get; set; } = string.Empty;
    public string Mongo원장대상OsCode { get; set; } = string.Empty;
    public DateTime? Mongo원장UpdatedAtUtc { get; set; }
    public int Mongo원장블록수 { get; set; }
    public bool Rdb운송실행투영존재 { get; set; }
    public string 원장동기화메시지 { get; set; } = string.Empty;
    public DateTime 마지막변경시각 { get; set; }
    public IReadOnlyList<운송원장이벤트항목응답> 이벤트목록 { get; set; } = [];
}

public sealed class 운송원장이벤트항목응답
{
    public long Id { get; set; }
    public string 이벤트타입 { get; set; } = string.Empty;
    public DateTime 이벤트시각 { get; set; }
    public string 메타데이터 { get; set; } = string.Empty;
}
