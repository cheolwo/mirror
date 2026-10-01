using Hongdal.Contracts.Shipper.ImportFood;

namespace Hongdal.Application.Shipper.ImportFood;

public sealed class 수입식품한글표시사항조회QueryHandler
    : IRequestHandler<수입식품한글표시사항조회Query, 수입식품한글표시사항조회응답>
{
    private readonly I수입식품한글표시사항조회Service _service;

    public 수입식품한글표시사항조회QueryHandler(I수입식품한글표시사항조회Service service)
    {
        _service = service;
    }

    public async Task<수입식품한글표시사항조회응답> Handle(
        수입식품한글표시사항조회Query request,
        CancellationToken cancellationToken)
    {
        var 응답 = await _service.조회Async(new 수입식품한글표시사항조회요청DTO
        {
            페이지번호 = request.페이지번호,
            한페이지결과수 = request.한페이지결과수,
            데이터형식 = request.데이터형식,
            제품구분 = request.제품구분,
            수입업체명 = request.수입업체명,
            한글제품명 = request.한글제품명,
            영문제품명 = request.영문제품명,
            해외제조업소명 = request.해외제조업소명,
            품목명 = request.품목명,
            수출국명 = request.수출국명,
            제조국명 = request.제조국명,
            한글표시사항검색어 = request.한글표시사항검색어,
            원재료명 = request.원재료명,
            유통기한시작일자검색시작 = request.유통기한시작일자검색시작,
            유통기한시작일자검색종료 = request.유통기한시작일자검색종료,
            유통기한종료일자검색시작 = request.유통기한종료일자검색시작,
            유통기한종료일자검색종료 = request.유통기한종료일자검색종료,
            처리일자검색시작 = request.처리일자검색시작,
            처리일자검색종료 = request.처리일자검색종료
        }, cancellationToken);

        return new 수입식품한글표시사항조회응답
        {
            조회메타데이터 = new 수입식품공식자료조회메타데이터
            {
                데이터셋키 = "mfds-imported-food-korean-label",
                공식문서Url = "https://www.data.go.kr/data/15110214/openapi.do",
                조회시각Utc = DateTimeOffset.UtcNow
            },
            결과코드 = 응답.결과코드 ?? string.Empty,
            결과메시지 = 응답.결과메시지 ?? string.Empty,
            페이지번호 = 응답.페이지번호,
            한페이지결과수 = 응답.한페이지결과수,
            전체결과수 = 응답.전체결과수,
            항목목록 = 응답.항목목록.Select(항목 => new 수입식품한글표시사항조회항목
            {
                제품구분 = 항목.제품구분,
                수입업체명 = 항목.수입업체명,
                한글제품명 = 항목.한글제품명,
                영문제품명 = 항목.영문제품명,
                유통기한 = 항목.유통기한,
                처리일자 = 항목.처리일자,
                해외제조업소명 = 항목.해외제조업소명,
                품목명 = 항목.품목명,
                수출국명 = 항목.수출국명,
                제조국명 = 항목.제조국명,
                한글표시사항 = 항목.한글표시사항,
                원재료명 = 항목.원재료명,
                유통기한시작일자 = 항목.유통기한시작일자,
                유통기한종료일자 = 항목.유통기한종료일자
            }).ToList()
        };
    }
}
