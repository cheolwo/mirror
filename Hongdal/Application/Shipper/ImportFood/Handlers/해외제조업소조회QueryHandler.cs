using Hongdal.Contracts.Shipper.ImportFood;

namespace Hongdal.Application.Shipper.ImportFood;

public sealed class 해외제조업소조회QueryHandler : IRequestHandler<해외제조업소조회Query, 해외제조업소조회화면응답>
{
    private readonly I해외제조업소조회Service _service;

    public 해외제조업소조회QueryHandler(I해외제조업소조회Service service)
    {
        _service = service;
    }

    public async Task<해외제조업소조회화면응답> Handle(해외제조업소조회Query request, CancellationToken cancellationToken)
    {
        var 응답 = await _service.조회Async(new 해외제조업소조회요청
        {
            페이지번호 = request.페이지번호,
            한페이지결과수 = request.한페이지결과수,
            데이터형식 = request.데이터형식,
            해외제조업소명 = request.해외제조업소명,
            식품구분명 = request.식품구분명,
            국가명 = request.국가명
        }, cancellationToken);

        var 결과목록 = 응답.본문?.아이템?.항목 ?? [];

        return new 해외제조업소조회화면응답
        {
            조회메타데이터 = new 수입식품공식자료조회메타데이터
            {
                데이터셋키 = "mfds-imported-food-overseas-manufacturer",
                공식문서Url = "https://www.data.go.kr/data/15073967/openapi.do",
                조회시각Utc = DateTimeOffset.UtcNow
            },
            결과코드 = 응답.헤더?.결과코드 ?? string.Empty,
            결과메시지 = 응답.헤더?.결과메시지 ?? string.Empty,
            페이지번호 = 응답.본문?.페이지번호 ?? request.페이지번호,
            한페이지결과수 = 응답.본문?.한페이지결과수 ?? request.한페이지결과수,
            전체결과수 = 응답.본문?.전체결과수 ?? 0,
            항목목록 = 결과목록.Select(항목 => new 해외제조업소조회화면항목
            {
                제조업소코드 = 항목.해외제조업소코드 ?? string.Empty,
                제조업소명 = 항목.해외제조업소명 ?? string.Empty,
                제조업소주소 = 항목.해외제조업소주소 ?? string.Empty,
                국가명 = 항목.국가명,
                지역명 = 항목.지역명,
                식품구분명 = 항목.식품구분명,
                영업구분명 = 항목.영업구분명,
                식품안전관리인증여부 = string.Equals(항목.식품안전관리시스템인증여부, "Y", StringComparison.OrdinalIgnoreCase),
                인증명 = 항목.인증명,
                인증기관명 = 항목.인증기관명,
                인증일 = 항목.인증기관인증일,
                인증만료일 = 항목.인증기관만료일,
                주의필요여부 = 항목.주의필요여부,
                주의사유 = 항목.주의사유,
                취소중단명 = 항목.취소중단명,
                수입중단번호 = 항목.수입중단번호
            }).ToList()
        };
    }
}
