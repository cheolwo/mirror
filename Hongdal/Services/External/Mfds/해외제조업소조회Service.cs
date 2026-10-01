using System.Globalization;
using Microsoft.Extensions.Options;

namespace 홍달.Services.External.Mfds;

public sealed class 해외제조업소조회Service : I해외제조업소조회Service
{
    private readonly HttpClient _httpClient;
    private readonly 해외제조업소조회Options _옵션;

    public 해외제조업소조회Service(
        HttpClient httpClient,
        IOptions<해외제조업소조회Options> 옵션)
    {
        _httpClient = httpClient;
        _옵션 = 옵션.Value;

        if (string.IsNullOrWhiteSpace(_옵션.ServiceKey))
        {
            throw new InvalidOperationException(
                "해외제조업소조회:ServiceKey 또는 PublicData:DataGoKrServiceKey 설정이 필요합니다.");
        }
    }

    public async Task<해외제조업소조회응답> 조회Async(
        해외제조업소조회요청 요청,
        CancellationToken 취소토큰 = default)
    {
        ArgumentNullException.ThrowIfNull(요청);

        var 페이지번호 = 요청.페이지번호 <= 0 ? 1 : 요청.페이지번호;
        var 한페이지결과수 = 요청.한페이지결과수 <= 0 ? 10 : 요청.한페이지결과수;
        var 데이터형식 = Mfds공공데이터요청Builder.데이터형식정리(요청.데이터형식, _옵션.DefaultType);
        var 요청주소 = Mfds공공데이터요청Builder.요청주소생성(
            _옵션.Endpoint,
            new Dictionary<string, string?>
            {
                ["serviceKey"] = _옵션.ServiceKey,
                ["pageNo"] = 페이지번호.ToString(CultureInfo.InvariantCulture),
                ["numOfRows"] = 한페이지결과수.ToString(CultureInfo.InvariantCulture),
                ["type"] = 데이터형식,
                ["OCTR_MNFT_BSSH_NM"] = 요청.해외제조업소명,
                ["FOOD_SE_NM"] = 요청.식품구분명,
                ["NATN_NM"] = 요청.국가명
            });

        using var 응답 = await _httpClient.GetAsync(요청주소, 취소토큰);
        응답.EnsureSuccessStatusCode();

        var 본문텍스트 = await 응답.Content.ReadAsStringAsync(취소토큰);
        var 파싱결과 = Mfds공공데이터목록Parser.파싱(본문텍스트, 데이터형식, 항목변환);

        return new 해외제조업소조회응답
        {
            헤더 = new 해외제조업소조회헤더
            {
                결과코드 = 파싱결과.결과코드,
                결과메시지 = 파싱결과.결과메시지
            },
            본문 = new 해외제조업소조회본문
            {
                한페이지결과수 = 파싱결과.한페이지결과수,
                페이지번호 = 파싱결과.페이지번호,
                전체결과수 = 파싱결과.전체결과수,
                아이템 = new 해외제조업소조회아이템목록
                {
                    항목 = 파싱결과.항목목록.ToList()
                }
            }
        };
    }

    private static 해외제조업소조회항목 항목변환(Mfds공공데이터항목 항목)
    {
        var 결과 = new 해외제조업소조회항목
        {
            해외제조업소코드 = 항목.문자열("OCTR_MNFT_BSSH_CD"),
            해외제조업소명 = 항목.문자열("OCTR_MNFT_BSSH_NM"),
            해외제조업소주소 = 항목.문자열("OCTR_MNFT_BSSH_ADDR"),
            영업구분코드 = 항목.문자열("OCTR_MNFT_ENTP_BSN_DIVS_CD"),
            영업구분명 = 항목.문자열("OCTR_MNFT_ENTP_BSN_DIVS_NM"),
            식품구분코드 = 항목.문자열("FOOD_SE_CD"),
            식품구분명 = 항목.문자열("FOOD_SE_NM"),
            시설취소철회일 = 항목.문자열("FCLT_RTRCN_DT"),
            국가코드 = 항목.문자열("NATN_CD"),
            국가명 = 항목.문자열("NATN_NM"),
            지역코드 = 항목.문자열("AREA_CD"),
            지역명 = 항목.문자열("AREA_NM"),
            식품안전관리시스템인증여부 = 항목.문자열("FOOD_SAFE_MNG_SYS_CERT_YN"),
            인증명 = 항목.문자열("CERT_NM"),
            인증기관명 = 항목.문자열("CERT_INST_NM"),
            인증기관인증일 = 항목.문자열("CERT_INST_CERT_DT"),
            인증기관만료일 = 항목.문자열("CERT_INST_EXPRN_DT"),
            단종여부 = 항목.문자열("DSCTN_YN"),
            단종일 = 항목.문자열("DSCTN_DT"),
            취소중단코드 = 항목.문자열("RTRCN_SUSP_CD"),
            취소중단명 = 항목.문자열("RTRCN_SUSP_NM"),
            수동등록구분코드 = 항목.문자열("PASV_REG_DIVS_CD"),
            식품유통시작일 = 항목.문자열("FOOD_SLDT_BGNG_DT"),
            식품유통종료일 = 항목.문자열("FOOD_SLDT_END_DT"),
            수산시작일 = 항목.문자열("MARN_BGNG_DT"),
            수입중단번호 = 항목.문자열("IPRT_SUSP_NO")
        };

        결과.주의필요여부 = 주의필요조건충족(결과);
        결과.주의사유 = 결과.주의필요여부 ? 주의사유생성(결과) : null;
        return 결과;
    }

    private static bool 주의필요조건충족(해외제조업소조회항목 항목)
        => !string.IsNullOrWhiteSpace(항목.시설취소철회일)
            || string.Equals(항목.단종여부?.Trim(), "Y", StringComparison.OrdinalIgnoreCase)
            || !string.IsNullOrWhiteSpace(항목.단종일)
            || !string.IsNullOrWhiteSpace(항목.취소중단코드)
            || !string.IsNullOrWhiteSpace(항목.취소중단명)
            || !string.IsNullOrWhiteSpace(항목.수입중단번호);

    private static string 주의사유생성(해외제조업소조회항목 항목)
    {
        var 사유목록 = new List<string>();

        if (!string.IsNullOrWhiteSpace(항목.시설취소철회일)) 사유목록.Add($"시설 취소/철회일: {항목.시설취소철회일}");
        if (!string.IsNullOrWhiteSpace(항목.단종여부)) 사유목록.Add($"단종 여부: {항목.단종여부}");
        if (!string.IsNullOrWhiteSpace(항목.단종일)) 사유목록.Add($"단종일: {항목.단종일}");
        if (!string.IsNullOrWhiteSpace(항목.취소중단명)) 사유목록.Add($"취소·중단: {항목.취소중단명}");
        if (!string.IsNullOrWhiteSpace(항목.수입중단번호)) 사유목록.Add($"수입중단번호: {항목.수입중단번호}");

        return string.Join("; ", 사유목록);
    }
}
