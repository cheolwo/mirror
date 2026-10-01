using System.Globalization;
using Microsoft.Extensions.Options;

namespace 홍달.Services.External.Mfds;

public sealed class 수입식품한글표시사항조회Service : I수입식품한글표시사항조회Service
{
    private readonly HttpClient _httpClient;
    private readonly 수입식품한글표시사항조회Options _옵션;

    public 수입식품한글표시사항조회Service(
        HttpClient httpClient,
        IOptions<수입식품한글표시사항조회Options> 옵션)
    {
        _httpClient = httpClient;
        _옵션 = 옵션.Value;

        if (string.IsNullOrWhiteSpace(_옵션.ServiceKey))
        {
            throw new InvalidOperationException(
                "수입식품한글표시사항조회:ServiceKey 또는 PublicData:DataGoKrServiceKey 설정이 필요합니다.");
        }
    }

    public async Task<수입식품한글표시사항조회응답DTO> 조회Async(
        수입식품한글표시사항조회요청DTO 요청,
        CancellationToken 취소토큰 = default)
    {
        ArgumentNullException.ThrowIfNull(요청);

        var 페이지번호 = Math.Max(1, 요청.페이지번호);
        var 한페이지결과수 = Math.Clamp(요청.한페이지결과수, 1, 100);
        var 데이터형식 = Mfds공공데이터요청Builder.데이터형식정리(요청.데이터형식, _옵션.DefaultType);
        var 요청주소 = Mfds공공데이터요청Builder.요청주소생성(
            _옵션.Path,
            new Dictionary<string, string?>
            {
                ["serviceKey"] = _옵션.ServiceKey,
                ["pageNo"] = 페이지번호.ToString(CultureInfo.InvariantCulture),
                ["numOfRows"] = 한페이지결과수.ToString(CultureInfo.InvariantCulture),
                ["type"] = 데이터형식,
                ["dclPrductSeCdNm"] = 요청.제품구분,
                ["bsnOfcName"] = 요청.수입업체명,
                ["prductKoreanNm"] = 요청.한글제품명,
                ["prductNm"] = 요청.영문제품명,
                ["ovsmnfstNm"] = 요청.해외제조업소명,
                ["itmNm"] = 요청.품목명,
                ["xportNtncdNm"] = 요청.수출국명,
                ["mnfNtncdNm"] = 요청.제조국명,
                ["korlabel"] = 요청.한글표시사항검색어,
                ["irdntNm"] = 요청.원재료명,
                ["expirdeBeginDtmStart"] = 요청.유통기한시작일자검색시작,
                ["expirdeBeginDtmEnd"] = 요청.유통기한시작일자검색종료,
                ["expirdeEndDtmStart"] = 요청.유통기한종료일자검색시작,
                ["expirdeEndDtmEnd"] = 요청.유통기한종료일자검색종료,
                ["procsDtmStart"] = 요청.처리일자검색시작,
                ["procsDtmEnd"] = 요청.처리일자검색종료
            });

        using var 응답 = await _httpClient.GetAsync(요청주소, 취소토큰);
        응답.EnsureSuccessStatusCode();

        var 본문텍스트 = await 응답.Content.ReadAsStringAsync(취소토큰);
        var 파싱결과 = Mfds공공데이터목록Parser.파싱(본문텍스트, 데이터형식, 항목변환);

        return new 수입식품한글표시사항조회응답DTO
        {
            결과코드 = 파싱결과.결과코드,
            결과메시지 = 파싱결과.결과메시지,
            페이지번호 = 파싱결과.페이지번호,
            한페이지결과수 = 파싱결과.한페이지결과수,
            전체결과수 = 파싱결과.전체결과수,
            항목목록 = 파싱결과.항목목록
        };
    }

    private static 수입식품한글표시사항조회항목DTO 항목변환(Mfds공공데이터항목 항목)
        => new()
        {
            제품구분 = 항목.문자열("DCL_PRDUCT_SE_CD_NM"),
            수입업체명 = 항목.문자열("BSN_OFC_NAME"),
            한글제품명 = 항목.문자열("PRDUCT_KOREAN_NM"),
            영문제품명 = 항목.문자열("PRDUCT_NM"),
            유통기한 = 항목.문자열("EXPIRDE_DTM"),
            처리일자 = 항목.문자열("PROCS_DTM"),
            해외제조업소명 = 항목.문자열("OVSMNFST_NM"),
            품목명 = 항목.문자열("ITM_NM"),
            수출국명 = 항목.문자열("XPORT_NTNCD_NM"),
            제조국명 = 항목.문자열("MNF_NTNCD_NM"),
            한글표시사항 = 항목.문자열("KORLABEL"),
            원재료명 = 항목.문자열("IRDNT_NM"),
            유통기한시작일자 = 항목.문자열("EXPIRDE_BEGIN_DTM"),
            유통기한종료일자 = 항목.문자열("EXPIRDE_END_DTM")
        };
}
