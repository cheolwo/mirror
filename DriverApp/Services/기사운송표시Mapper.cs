using DriverApp.Models.Driver.Samples;
using Ssalddel.Contracts.Driver.Transport;
using DriverApp.ViewModels.Driver.Transport;

namespace DriverApp.Services;

internal static class 기사운송표시Mapper
{
    public static 기사운송샘플항목 Map(기사운송요약응답 source)
        => new(source.Id, source.운송번호, "서버 운송", source.출발지, source.도착지,
            source is 기사운송상세응답 { 개인정보제공보류: false } pickup ? pickup.픽업위도 : null,
            source is 기사운송상세응답 { 개인정보제공보류: false } pickupLongitude ? pickupLongitude.픽업경도 : null,
            source is 기사운송상세응답 { 개인정보제공보류: false } dropoff ? dropoff.하차위도 : null,
            source is 기사운송상세응답 { 개인정보제공보류: false } dropoffLongitude ? dropoffLongitude.하차경도 : null,
            string.IsNullOrWhiteSpace(source.상태) ? "진행중" : source.상태,
            기사국내시각표시.한국시간(source.출발_픽업 ?? source.도착 ?? source.UpdatedAt).DateTime,
            source.예상거리Km, source.운임, source.인수증필요, source.인수증서명필수,
            string.IsNullOrWhiteSpace(source.결제방식) ? "서버 정산" : source.결제방식,
            source.상태 switch
            {
                "배차확정" or "확정" or "매칭중" => "상차지 도착",
                "상차지도착" => "상차 완료",
                "상차완료" or "운송중" => "하차지 도착",
                "하차지도착" => "하차 완료",
                "인수완료" or "하차완료" => "운송 완료",
                _ => "상태 갱신"
            })
        {
            거리계산방식 = source.거리계산방식 ?? string.Empty,
            수령자명 = source.수령자명,
            수령자연락처 = source.수령자연락처,
            상차담당자명 = source.상차담당자명,
            상차연락처 = source.상차연락처,
            상차시간창시작일시 = source.상차시간창시작일시,
            상차시간창종료일시 = source.상차시간창종료일시,
            하차시간창시작일시 = source.하차시간창시작일시,
            하차시간창종료일시 = source.하차시간창종료일시,
            전달요청 = source.전달요청
        };
}
