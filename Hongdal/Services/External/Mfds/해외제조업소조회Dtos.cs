namespace 홍달.Services.External.Mfds;

public sealed class 해외제조업소조회요청
{
    public int 페이지번호 { get; set; } = 1;

    public int 한페이지결과수 { get; set; } = 10;

    public string 데이터형식 { get; set; } = "xml";

    public string? 해외제조업소명 { get; set; }

    public string? 식품구분명 { get; set; }

    public string? 국가명 { get; set; }
}

public sealed class 해외제조업소조회응답
{
    public 해외제조업소조회헤더? 헤더 { get; set; }

    public 해외제조업소조회본문? 본문 { get; set; }
}

public sealed class 해외제조업소조회헤더
{
    public string? 결과코드 { get; set; }

    public string? 결과메시지 { get; set; }
}

public sealed class 해외제조업소조회본문
{
    public int 한페이지결과수 { get; set; }

    public int 페이지번호 { get; set; }

    public int 전체결과수 { get; set; }

    public 해외제조업소조회아이템목록? 아이템 { get; set; }
}

public sealed class 해외제조업소조회아이템목록
{
    public List<해외제조업소조회항목> 항목 { get; set; } = [];
}

public sealed class 해외제조업소조회항목
{
    public string? 해외제조업소코드 { get; set; }

    public string? 해외제조업소명 { get; set; }

    public string? 해외제조업소주소 { get; set; }

    public string? 영업구분코드 { get; set; }

    public string? 영업구분명 { get; set; }

    public string? 식품구분코드 { get; set; }

    public string? 식품구분명 { get; set; }

    public string? 시설취소철회일 { get; set; }

    public string? 국가코드 { get; set; }

    public string? 국가명 { get; set; }

    public string? 지역코드 { get; set; }

    public string? 지역명 { get; set; }

    public string? 식품안전관리시스템인증여부 { get; set; }

    public string? 인증명 { get; set; }

    public string? 인증기관명 { get; set; }

    public string? 인증기관인증일 { get; set; }

    public string? 인증기관만료일 { get; set; }

    public string? 단종여부 { get; set; }

    public string? 단종일 { get; set; }

    public string? 취소중단코드 { get; set; }

    public string? 취소중단명 { get; set; }

    public string? 수동등록구분코드 { get; set; }

    public string? 식품유통시작일 { get; set; }

    public string? 식품유통종료일 { get; set; }

    public string? 수산시작일 { get; set; }

    public string? 수입중단번호 { get; set; }

    public bool 주의필요여부 { get; set; }

    public string? 주의사유 { get; set; }
}
