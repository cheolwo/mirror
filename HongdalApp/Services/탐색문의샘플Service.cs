using Hongdal.Contracts.Common.Exploration;

namespace HongdalApp.Services;

public sealed class 탐색문의샘플Service : IShipperExplorationInquiryService
{
    public IReadOnlyList<탐색문의목록항목응답> 목록() =>
    [
        new()
        {
            탐색캠페인Id = 3001,
            탐색명 = "수도권 오전 회차 물량 탐색",
            개시자UserId = "driver-local-sample",
            개시자명 = "김기사",
            개시자역할 = 탐색캠페인개시자역할값.기사,
            운행예정일 = DateTime.Today.AddDays(1).AddHours(8),
            출발권역 = "경기 남부",
            희망도착권역 = "서울 서북권",
            차량종류 = "1톤 카고",
            대상상태 = 탐색캠페인대상상태값.발송됨,
            발송일시 = DateTime.UtcNow.AddMinutes(-40)
        },
        new()
        {
            탐색캠페인Id = 3002,
            탐색명 = "인천항 오후 냉장 회차 탐색",
            개시자UserId = "driver-2",
            개시자명 = "박기사",
            개시자역할 = 탐색캠페인개시자역할값.기사,
            운행예정일 = DateTime.Today.AddDays(2).AddHours(13),
            출발권역 = "인천항",
            희망도착권역 = "경기 북부",
            차량종류 = "1톤 냉장",
            대상상태 = 탐색캠페인대상상태값.있음응답,
            발송일시 = DateTime.UtcNow.AddHours(-3)
        },
        new()
        {
            탐색캠페인Id = 3004,
            탐색명 = "화주 요청 반응 대기 샘플",
            개시자UserId = "driver-3",
            개시자명 = "이기사",
            개시자역할 = 탐색캠페인개시자역할값.기사,
            운행예정일 = DateTime.Today.AddDays(3).AddHours(10),
            출발권역 = "시흥",
            희망도착권역 = "수원",
            차량종류 = "1톤 윙바디",
            대상상태 = 탐색캠페인대상상태값.미정응답,
            발송일시 = DateTime.UtcNow.AddHours(-9)
        }
    ];

    public 탐색문의상세응답 상세(long campaignId)
    {
        var item = 목록().First(x => x.탐색캠페인Id == campaignId);
        return new 탐색문의상세응답
        {
            탐색캠페인Id = item.탐색캠페인Id,
            탐색명 = item.탐색명,
            개시자UserId = item.개시자UserId,
            개시자명 = item.개시자명,
            개시자역할 = item.개시자역할,
            운행예정일 = item.운행예정일,
            출발권역 = item.출발권역,
            희망도착권역 = item.희망도착권역,
            차량종류 = item.차량종류,
            대상상태 = item.대상상태,
            발송일시 = item.발송일시,
            발송메시지 = "내일 오전 회차에 맞는 물량이 있으면 우선 연락 부탁드립니다.",
            메모 = "샘플 문의함 상세입니다.",
            최대적재중량Kg = 1000,
            최대적재부피Cbm = 5.5m,
            기존응답유형 = item.대상상태 switch
            {
                var x when x == 탐색캠페인대상상태값.있음응답 => 운행문의응답유형.있음,
                var x when x == 탐색캠페인대상상태값.없음응답 => 운행문의응답유형.없음,
                var x when x == 탐색캠페인대상상태값.미정응답 => 운행문의응답유형.미정,
                _ => null
            },
            기존응답일시 = item.대상상태 == 탐색캠페인대상상태값.발송됨 ? null : DateTime.UtcNow.AddMinutes(-20)
        };
    }
}
