namespace 홍달.Services.External.Mfds;

public sealed class 수입식품한글표시사항조회요청DTO
{
    public int 페이지번호 { get; set; } = 1;

    public int 한페이지결과수 { get; set; } = 10;

    public string 데이터형식 { get; set; } = "xml";

    public string? 제품구분 { get; set; }

    public string? 수입업체명 { get; set; }

    public string? 한글제품명 { get; set; }

    public string? 영문제품명 { get; set; }

    public string? 해외제조업소명 { get; set; }

    public string? 품목명 { get; set; }

    public string? 수출국명 { get; set; }

    public string? 제조국명 { get; set; }

    public string? 한글표시사항검색어 { get; set; }

    public string? 원재료명 { get; set; }

    public string? 유통기한시작일자검색시작 { get; set; }

    public string? 유통기한시작일자검색종료 { get; set; }

    public string? 유통기한종료일자검색시작 { get; set; }

    public string? 유통기한종료일자검색종료 { get; set; }

    public string? 처리일자검색시작 { get; set; }

    public string? 처리일자검색종료 { get; set; }
}

public sealed class 수입식품한글표시사항조회응답DTO
{
    public string? 결과코드 { get; set; }

    public string? 결과메시지 { get; set; }

    public int 페이지번호 { get; set; }

    public int 한페이지결과수 { get; set; }

    public int 전체결과수 { get; set; }

    public IReadOnlyList<수입식품한글표시사항조회항목DTO> 항목목록 { get; set; } = [];
}

public sealed class 수입식품한글표시사항조회항목DTO
{
    public string? 제품구분 { get; set; }

    public string? 수입업체명 { get; set; }

    public string? 한글제품명 { get; set; }

    public string? 영문제품명 { get; set; }

    public string? 유통기한 { get; set; }

    public string? 처리일자 { get; set; }

    public string? 해외제조업소명 { get; set; }

    public string? 품목명 { get; set; }

    public string? 수출국명 { get; set; }

    public string? 제조국명 { get; set; }

    public string? 한글표시사항 { get; set; }

    public string? 원재료명 { get; set; }

    public string? 유통기한시작일자 { get; set; }

    public string? 유통기한종료일자 { get; set; }
}
