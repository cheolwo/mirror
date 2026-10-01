namespace Hongdal.Contracts.Shipper.ImportFood;

public sealed class 수입식품한글표시사항조회응답
{
    public 수입식품공식자료조회메타데이터 조회메타데이터 { get; set; } = new();

    public string 결과코드 { get; set; } = string.Empty;

    public string 결과메시지 { get; set; } = string.Empty;

    public int 페이지번호 { get; set; }

    public int 한페이지결과수 { get; set; }

    public int 전체결과수 { get; set; }

    public List<수입식품한글표시사항조회항목> 항목목록 { get; set; } = [];
}

public sealed class 수입식품한글표시사항조회항목
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
