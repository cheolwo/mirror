namespace Ssalddel.Contracts.Admin.Management;

public sealed class 차량추천기준응답
{
    public string 차량코드 { get; set; } = string.Empty;
    public string 차량명 { get; set; } = string.Empty;
    public string 차급 { get; set; } = string.Empty;
    public string 차체형태 { get; set; } = string.Empty;
    public int 적재함길이Mm { get; set; }
    public int 적재함폭Mm { get; set; }
    public int? 적재함높이Mm { get; set; }
    public int 최대적재중량Kg { get; set; }
    public int? 운영권장중량Kg { get; set; }
    public int? 팔레트적재개수 { get; set; }
    public decimal? 계산CBM { get; set; }
    public decimal? 권장최대CBM { get; set; }
    public int 추천우선순위 { get; set; }
    public bool 추천사용여부 { get; set; }
}

public sealed class 차량추천기준수정요청
{
    public decimal? 권장최대CBM { get; set; }
    public int 추천우선순위 { get; set; }
    public bool 추천사용여부 { get; set; }
    public int? 운영권장중량Kg { get; set; }
    public int? 팔레트적재개수 { get; set; }
    // 실차·적재 장비를 확인한 값. 생략하면 기존 제원을 보존한다.
    public int? 적재함길이Mm { get; set; }
    public int? 적재함폭Mm { get; set; }
    public int? 적재함높이Mm { get; set; }
    public int? 최대적재중량Kg { get; set; }
}

public sealed class 차량추천시뮬레이션요청
{
    public string? 화물종류 { get; set; }
    public int? 화물수량 { get; set; }
    public int? 화물길이Mm { get; set; }
    public int? 화물폭Mm { get; set; }
    public int? 화물높이Mm { get; set; }
    public decimal? 화물중량Kg { get; set; }
    public decimal? 화물부피Cbm { get; set; }
    public int? 팔레트개수 { get; set; }
    public string? 화물온도조건 { get; set; }
    public bool 화물파손주의여부 { get; set; }
}

public sealed class 차량추천시뮬레이션응답
{
    public string 추천차량종류 { get; set; } = string.Empty;
    public decimal? 추정화물부피Cbm { get; set; }
    public IReadOnlyList<string> 추천사유 { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> 경고목록 { get; set; } = Array.Empty<string>();
    public IReadOnlyList<차량추천시뮬레이션후보응답> 후보목록 { get; set; } = Array.Empty<차량추천시뮬레이션후보응답>();
}

public sealed class 차량추천시뮬레이션후보응답
{
    public string 차량코드 { get; set; } = string.Empty;
    public string 차량종류 { get; set; } = string.Empty;
    public int 우선순위 { get; set; }
    public decimal 적재가능중량Kg { get; set; }
    public decimal? 적재가능부피Cbm { get; set; }
    public int? 적재가능팔레트개수 { get; set; }
    public string 설명 { get; set; } = string.Empty;
}
