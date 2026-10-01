namespace 홍달.Services.External.Mfds;

public sealed class 수입식품제품조회요청DTO
{
    public int 페이지번호 { get; set; } = 1;

    public int 한페이지결과수 { get; set; } = 10;

    public string 데이터형식 { get; set; } = "xml";

    public string? 신고제품구분명 { get; set; }

    public string? 제조국가명 { get; set; }

    public string? 제품명 { get; set; }

    public string? 품목명 { get; set; }
}

public sealed class 수입식품제품조회응답DTO
{
    public 수입식품제품조회헤더DTO? 헤더 { get; set; }

    public 수입식품제품조회본문DTO? 본문 { get; set; }
}

public sealed class 수입식품제품조회헤더DTO
{
    public string? 결과코드 { get; set; }

    public string? 결과메시지 { get; set; }
}

public sealed class 수입식품제품조회본문DTO
{
    public int 한페이지결과수 { get; set; }

    public int 페이지번호 { get; set; }

    public int 전체결과수 { get; set; }

    public 수입식품제품조회아이템목록DTO? 아이템 { get; set; }
}

public sealed class 수입식품제품조회아이템목록DTO
{
    public List<수입식품제품조회항목DTO> 항목 { get; set; } = [];
}

public sealed class 수입식품제품조회항목DTO
{
    public string? 신고제품구분코드 { get; set; }

    public string? 신고제품구분명 { get; set; }

    public string? 제조국가코드 { get; set; }

    public string? 제조국가명 { get; set; }

    public string? 제품명 { get; set; }

    public string? 육류품목코드 { get; set; }

    public string? 육류품목명 { get; set; }

    public string? 품목코드 { get; set; }

    public string? 품목명 { get; set; }

    public string? 수입식품관리번호 { get; set; }
}
