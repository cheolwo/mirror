using System.Globalization;
using Microsoft.Extensions.Options;

namespace 홍달.Services.External.Mfds;

public sealed class 수입식품제품조회Service : I수입식품제품조회Service
{
    private readonly HttpClient _httpClient;
    private readonly 수입식품제품조회Options _옵션;

    public 수입식품제품조회Service(
        HttpClient httpClient,
        IOptions<수입식품제품조회Options> 옵션)
    {
        _httpClient = httpClient;
        _옵션 = 옵션.Value;

        if (string.IsNullOrWhiteSpace(_옵션.ServiceKey))
        {
            throw new InvalidOperationException(
                "수입식품제품조회:ServiceKey 또는 PublicData:DataGoKrServiceKey 설정이 필요합니다.");
        }
    }

    public async Task<수입식품제품조회응답DTO> 조회Async(
        수입식품제품조회요청DTO 요청,
        CancellationToken 취소토큰 = default)
    {
        ArgumentNullException.ThrowIfNull(요청);

        var 페이지번호 = 요청.페이지번호 <= 0 ? 1 : 요청.페이지번호;
        var 한페이지결과수 = 요청.한페이지결과수 <= 0 ? 10 : 요청.한페이지결과수;
        var 데이터형식 = Mfds공공데이터요청Builder.데이터형식정리(요청.데이터형식, _옵션.DefaultType);
        var 요청주소 = Mfds공공데이터요청Builder.요청주소생성(
            _옵션.Path,
            new Dictionary<string, string?>
            {
                ["serviceKey"] = _옵션.ServiceKey,
                ["pageNo"] = 페이지번호.ToString(CultureInfo.InvariantCulture),
                ["numOfRows"] = 한페이지결과수.ToString(CultureInfo.InvariantCulture),
                ["type"] = 데이터형식,
                ["DCLR_PRDT_DIVS_NM"] = 요청.신고제품구분명,
                ["MNFT_NATN_NM"] = 요청.제조국가명,
                ["PRDT_NM"] = 요청.제품명,
                ["PRDLST_NM"] = 요청.품목명
            });

        using var 응답 = await _httpClient.GetAsync(요청주소, 취소토큰);
        응답.EnsureSuccessStatusCode();

        var 본문텍스트 = await 응답.Content.ReadAsStringAsync(취소토큰);
        var 파싱결과 = Mfds공공데이터목록Parser.파싱(본문텍스트, 데이터형식, 항목변환);

        return new 수입식품제품조회응답DTO
        {
            헤더 = new 수입식품제품조회헤더DTO
            {
                결과코드 = 파싱결과.결과코드,
                결과메시지 = 파싱결과.결과메시지
            },
            본문 = new 수입식품제품조회본문DTO
            {
                한페이지결과수 = 파싱결과.한페이지결과수,
                페이지번호 = 파싱결과.페이지번호,
                전체결과수 = 파싱결과.전체결과수,
                아이템 = new 수입식품제품조회아이템목록DTO
                {
                    항목 = 파싱결과.항목목록.ToList()
                }
            }
        };
    }

    private static 수입식품제품조회항목DTO 항목변환(Mfds공공데이터항목 항목)
        => new()
        {
            신고제품구분코드 = 항목.문자열("DCLR_PRDT_DIVS_CD"),
            신고제품구분명 = 항목.문자열("DCLR_PRDT_DIVS_NM"),
            제조국가코드 = 항목.문자열("MNFT_NATN_CD"),
            제조국가명 = 항목.문자열("MNFT_NATN_NM"),
            제품명 = 항목.문자열("PRDT_NM"),
            육류품목코드 = 항목.문자열("MEAT_PRDLST_CD"),
            육류품목명 = 항목.문자열("MEAT_PRDLST_NM"),
            품목코드 = 항목.문자열("PRDLST_CD"),
            품목명 = 항목.문자열("PRDLST_NM"),
            수입식품관리번호 = 항목.문자열("IPRT_FOOD_MNG_NO")
        };
}
