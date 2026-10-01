namespace Hongdal.Contracts.Shipper.ImportFood;

public sealed class 해외제조업소조회화면응답
{
    public 수입식품공식자료조회메타데이터 조회메타데이터 { get; set; } = new();
    public string 결과코드 { get; set; } = string.Empty;
    public string 결과메시지 { get; set; } = string.Empty;
    public int 페이지번호 { get; set; }
    public int 한페이지결과수 { get; set; }
    public int 전체결과수 { get; set; }
    public List<해외제조업소조회화면항목> 항목목록 { get; set; } = [];
}

public sealed class 해외제조업소조회화면항목
{
    public string 제조업소코드 { get; set; } = string.Empty;
    public string 제조업소명 { get; set; } = string.Empty;
    public string 제조업소주소 { get; set; } = string.Empty;
    public string? 국가명 { get; set; }
    public string? 지역명 { get; set; }
    public string? 식품구분명 { get; set; }
    public string? 영업구분명 { get; set; }
    public bool 식품안전관리인증여부 { get; set; }
    public string? 인증명 { get; set; }
    public string? 인증기관명 { get; set; }
    public string? 인증일 { get; set; }
    public string? 인증만료일 { get; set; }
    public bool 주의필요여부 { get; set; }
    public string? 주의사유 { get; set; }
    public string? 취소중단명 { get; set; }
    public string? 수입중단번호 { get; set; }
}

public sealed class 수입식품공식자료조회메타데이터
{
    public string 제공기관 { get; set; } = "식품의약품안전처";

    public string 데이터셋키 { get; set; } = string.Empty;

    public string 공식문서Url { get; set; } = string.Empty;

    public DateTimeOffset 조회시각Utc { get; set; }

    public bool 실시간재확인필요여부 { get; set; } = true;
}
