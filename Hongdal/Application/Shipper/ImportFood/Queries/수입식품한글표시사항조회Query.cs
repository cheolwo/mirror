using Hongdal.Contracts.Shipper.ImportFood;

namespace Hongdal.Application.Shipper.ImportFood;

public sealed record 수입식품한글표시사항조회Query : IRequest<수입식품한글표시사항조회응답>
{
    public int 페이지번호 { get; init; } = 1;

    public int 한페이지결과수 { get; init; } = 10;

    public string 데이터형식 { get; init; } = "xml";

    public string? 제품구분 { get; init; }

    public string? 수입업체명 { get; init; }

    public string? 한글제품명 { get; init; }

    public string? 영문제품명 { get; init; }

    public string? 해외제조업소명 { get; init; }

    public string? 품목명 { get; init; }

    public string? 수출국명 { get; init; }

    public string? 제조국명 { get; init; }

    public string? 한글표시사항검색어 { get; init; }

    public string? 원재료명 { get; init; }

    public string? 유통기한시작일자검색시작 { get; init; }

    public string? 유통기한시작일자검색종료 { get; init; }

    public string? 유통기한종료일자검색시작 { get; init; }

    public string? 유통기한종료일자검색종료 { get; init; }

    public string? 처리일자검색시작 { get; init; }

    public string? 처리일자검색종료 { get; init; }
}
