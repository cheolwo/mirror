using Ssalddel.Contracts.Common.Community;

namespace Ssalddel.Services.Community;

public interface I생활교류공개지역Source
{
    Task<IReadOnlyList<NeighborhoodPublicRegionDto>> 목록Async(CancellationToken cancellationToken = default);
}

/// <summary>
/// 서울특별시 OA-22160의 공개 지역 대표점만 파생한 초기 30동 카탈로그입니다.
/// 2026-10-05 공식 사이트에서 새로 내려받아 KOGL 제1유형·EPSG:5181을 확인했습니다.
/// 파일 실제 수정일 2023-10-31을 판본으로 표시하며 현행 경계나 물품 위치를 뜻하지 않습니다.
/// ADSTRD_CD 뒤 00을 보완해 MOIS 2026-03-01 미말소 행정기관 코드·명칭과 대조했습니다.
/// MOIS ZIP SHA256: 8af8c1f122d67d43518f58b37aea6eea7986f2809062f24e2e03465f21ae7a08.
/// 비공개 디오라마·개인 배송 위치를 읽지 않고 런타임 외부 호출 없이 이 공개 사본을 제공합니다.
/// </summary>
public sealed class Official생활교류공개지역Source : I생활교류공개지역Source
{
    public const string SourceUrl = "https://data.seoul.go.kr/dataList/OA-22160/S/1/datasetView.do";
    public const string SourceVintage = "seoul-oa22160:file-modified:2023-10-31:retrieved:2026-10-05";
    public const string SourceSha256 = "969f7033bd3609a5fd586790f5b2cfedc638d7647ef45c78f9c75e1dabf79f68";

    private static readonly IReadOnlyList<NeighborhoodPublicRegionDto> Regions = Array.AsReadOnly(new[]
    {
        Region("1121574000", "서울특별시 광진구 중곡제1동", 37.562097, 127.077689), // EPSG:5181 206864, 451399
        Region("1121575000", "서울특별시 광진구 중곡제2동", 37.558912, 127.084578), // EPSG:5181 207473, 451046
        Region("1121576000", "서울특별시 광진구 중곡제3동", 37.567915, 127.082019), // EPSG:5181 207246, 452045
        Region("1121577000", "서울특별시 광진구 중곡제4동", 37.563878, 127.094499), // EPSG:5181 208349, 451598
        Region("1123056000", "서울특별시 동대문구 전농제1동", 37.579222, 127.050956), // EPSG:5181 204501, 453298
        Region("1123057000", "서울특별시 동대문구 전농제2동", 37.580695, 127.061123), // EPSG:5181 205399, 453462
        Region("1123060000", "서울특별시 동대문구 답십리제1동", 37.571275, 127.051878), // EPSG:5181 204583, 452416
        Region("1123061000", "서울특별시 동대문구 답십리제2동", 37.570784, 127.061036), // EPSG:5181 205392, 452362
        Region("1123065000", "서울특별시 동대문구 장안제1동", 37.566239, 127.068525), // EPSG:5181 206054, 451858
        Region("1123066000", "서울특별시 동대문구 장안제2동", 37.575643, 127.072077), // EPSG:5181 206367, 452902
        Region("1123072000", "서울특별시 동대문구 휘경제1동", 37.591956, 127.062604), // EPSG:5181 205529, 454712
        Region("1123073000", "서울특별시 동대문구 휘경제2동", 37.586639, 127.065826), // EPSG:5181 205814, 454122
        Region("1123074000", "서울특별시 동대문구 이문제1동", 37.597372, 127.060446), // EPSG:5181 205338, 455313
        Region("1123075000", "서울특별시 동대문구 이문제2동", 37.602847, 127.067517), // EPSG:5181 205962, 455921
        Region("1126052000", "서울특별시 중랑구 면목제2동", 37.589289, 127.077842), // EPSG:5181 206875, 454417
        Region("1126054000", "서울특별시 중랑구 면목제4동", 37.573616, 127.085376), // EPSG:5181 207542, 452678
        Region("1126055000", "서울특별시 중랑구 면목제5동", 37.582522, 127.078876), // EPSG:5181 206967, 453666
        Region("1126056500", "서울특별시 중랑구 면목본동", 37.588317, 127.089378), // EPSG:5181 207894, 454310
        Region("1126057000", "서울특별시 중랑구 면목제7동", 37.577142, 127.092795), // EPSG:5181 208197, 453070
        Region("1126057500", "서울특별시 중랑구 면목제3.8동", 37.584490, 127.098352), // EPSG:5181 208687, 453886
        Region("1126058000", "서울특별시 중랑구 상봉제1동", 37.601724, 127.089440), // EPSG:5181 207898, 455798
        Region("1126059000", "서울특별시 중랑구 상봉제2동", 37.594322, 127.084018), // EPSG:5181 207420, 454976
        Region("1126060000", "서울특별시 중랑구 중화제1동", 37.602593, 127.083076), // EPSG:5181 207336, 455894
        Region("1126061000", "서울특별시 중랑구 중화제2동", 37.599184, 127.075033), // EPSG:5181 206626, 455515
        Region("1126062000", "서울특별시 중랑구 묵제1동", 37.613730, 127.082534), // EPSG:5181 207287, 457130
        Region("1126063000", "서울특별시 중랑구 묵제2동", 37.610825, 127.074625), // EPSG:5181 206589, 456807
        Region("1126065500", "서울특별시 중랑구 망우본동", 37.602132, 127.107446), // EPSG:5181 209488, 455845
        Region("1126066000", "서울특별시 중랑구 망우제3동", 37.591883, 127.102834), // EPSG:5181 209082, 454707
        Region("1126068000", "서울특별시 중랑구 신내1동", 37.616473, 127.110423), // EPSG:5181 209749, 457437
        Region("1126069000", "서울특별시 중랑구 신내2동", 37.610984, 127.091591), // EPSG:5181 208087, 456826
    });

    public Task<IReadOnlyList<NeighborhoodPublicRegionDto>> 목록Async(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Regions);
    }

    private static NeighborhoodPublicRegionDto Region(string code, string name, double latitude, double longitude)
        => new()
        {
            RegionKey = "region:kr:hjd:" + code,
            DisplayName = name,
            ParentRegionKey = "region:kr:hjd:" + code[..5] + "00000",
            Latitude = latitude,
            Longitude = longitude,
            AnchorSourceName = "서울특별시 상권분석서비스(영역-행정동) · 공공누리 제1유형",
            AnchorSourceUrl = SourceUrl,
            AnchorSourceVintage = SourceVintage,
            AnchorSourceSha256 = SourceSha256,
            AnchorLicenseCode = "KOGL-Type1"
        };
}
