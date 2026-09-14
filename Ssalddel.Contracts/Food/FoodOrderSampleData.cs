using Ssalddel.Contracts.Common.Participants;

namespace Ssalddel.Contracts.Food;

public static class FoodOrderSampleData
{
    public static IReadOnlyList<음식주문응답> CreateOrders()
    {
        return
        [
            new 음식주문응답
            {
                주문번호 = "FOOD-20260701-001",
                음식점Id = 101,
                주문자UserId = "orderer-a",
                수령인정보 = new 음식주문수령인정보Dto
                {
                    수령인명 = "홍길동",
                    연락처 = "010-1234-5678",
                    주소 = "서울 강서구 화곡동 100",
                    상세주소 = "101호",
                    요청사항 = "문 앞에 두고 벨 눌러주세요.",
                    주문자본인수령여부 = true
                },
                상품목록 = [new 음식주문상품Dto { 상품명 = "제육덮밥", 수량 = 2, 단가 = 9500m }],
                총주문금액 = 19000m,
                상태 = 음식주문상태코드.주문대기,
                배차상태 = 음식주문배차상태코드.미요청,
                결제수단 = "카드",
                CreatedAt = DateTime.UtcNow.AddMinutes(-20)
            },
            new 음식주문응답
            {
                주문번호 = "FOOD-20260701-002",
                음식점Id = 102,
                주문자UserId = "orderer-b",
                수령인정보 = new 음식주문수령인정보Dto
                {
                    수령인명 = "김부모",
                    연락처 = "010-4444-1111",
                    주소 = "서울 양천구 목동 200",
                    상세주소 = "1203호",
                    요청사항 = "경비실 전달",
                    주문자본인수령여부 = false
                },
                상품목록 =
                [
                    new 음식주문상품Dto { 상품명 = "돈까스", 수량 = 1, 단가 = 11000m },
                    new 음식주문상품Dto { 상품명 = "우동", 수량 = 1, 단가 = 8000m }
                ],
                총주문금액 = 19000m,
                상태 = 음식주문상태코드.주문대기,
                배차상태 = 음식주문배차상태코드.미요청,
                결제수단 = "간편결제",
                CreatedAt = DateTime.UtcNow.AddMinutes(-8)
            },
            new 음식주문응답
            {
                주문번호 = "FOOD-20260701-003",
                음식점Id = 101,
                주문자UserId = "orderer-c",
                수령인정보 = new 음식주문수령인정보Dto
                {
                    수령인명 = "이배달",
                    연락처 = "010-8888-2222",
                    주소 = "서울 강서구 등촌동 45",
                    상세주소 = "2층",
                    요청사항 = "숟가락 3개 요청",
                    주문자본인수령여부 = true
                },
                상품목록 = [new 음식주문상품Dto { 상품명 = "비빔밥", 수량 = 3, 단가 = 11000m }],
                총주문금액 = 33000m,
                상태 = 음식주문상태코드.주문대기,
                배차상태 = 음식주문배차상태코드.미요청,
                결제수단 = "카드",
                CreatedAt = DateTime.UtcNow.AddMinutes(-18)
            }
        ];
    }

    public static IReadOnlyList<음식점주문수신알림> CreateRestaurantOrderNotifications()
    {
        return CreateOrders()
            .Select(order => new 음식점주문수신알림
            {
                주문번호 = order.주문번호,
                음식점Id = order.음식점Id,
                고객명 = order.수령인정보.수령인명,
                메뉴요약 = BuildMenuSummary(order.상품목록),
                상품목록 = order.상품목록.Select(x => new 음식주문상품Dto
                {
                    상품명 = x.상품명,
                    수량 = x.수량,
                    단가 = x.단가
                }).ToArray(),
                주문금액 = order.총주문금액,
                상태 = order.상태,
                수신시각 = new DateTimeOffset(DateTime.SpecifyKind(order.CreatedAt, DateTimeKind.Utc)),
                제목 = "신규 음식 주문",
                본문 = "실시간 신규 주문 수신"
            })
            .ToArray();
    }

    public static 음식주문응답 Clone(음식주문응답 source)
    {
        return new 음식주문응답
        {
            주문번호 = source.주문번호,
            클라이언트요청Id = source.클라이언트요청Id,
            음식점Id = source.음식점Id,
            음식점명 = source.음식점명,
            음식점주소 = source.음식점주소,
            음식점상세주소 = source.음식점상세주소,
            음식점위도 = source.음식점위도,
            음식점경도 = source.음식점경도,
            주문자UserId = source.주문자UserId,
            수령인정보 = new 음식주문수령인정보Dto
            {
                수령인명 = source.수령인정보.수령인명,
                연락처 = source.수령인정보.연락처,
                주소 = source.수령인정보.주소,
                상세주소 = source.수령인정보.상세주소,
                요청사항 = source.수령인정보.요청사항,
                주문자본인수령여부 = source.수령인정보.주문자본인수령여부
            },
            상품목록 = source.상품목록.Select(x => new 음식주문상품Dto
            {
                메뉴Id = x.메뉴Id,
                상품명 = x.상품명,
                수량 = x.수량,
                단가 = x.단가
            }).ToArray(),
            총주문금액 = source.총주문금액,
            상태 = source.상태,
            배차상태 = source.배차상태,
            배차대기Id = source.배차대기Id,
            결제수단 = source.결제수단,
            음식점수락시각Utc = source.음식점수락시각Utc,
            조리예상완료시각Utc = source.조리예상완료시각Utc,
            배차요청시각Utc = source.배차요청시각Utc,
            수락메모 = source.수락메모,
            커뮤니티원장Id = source.커뮤니티원장Id,
            커뮤니티원장템플릿Key = source.커뮤니티원장템플릿Key,
            커뮤니티원장상태 = source.커뮤니티원장상태,
            커뮤니티원장동기화시각Utc = source.커뮤니티원장동기화시각Utc,
            CreatedAt = source.CreatedAt,
            최근변경시각Utc = source.최근변경시각Utc,
            Revision = source.Revision > 0 ? source.Revision : source.상태이력.Count,
            AvailableActions = source.AvailableActions
                .Select(Ssalddel.Contracts.Common.Workflow.업무가능행동목록.Clone)
                .ToArray(),
            상태이력 = source.상태이력.Select(x => new 음식주문상태전이기록Dto
            {
                클라이언트요청Id = x.클라이언트요청Id,
                처리UserId = x.처리UserId,
                이전상태 = x.이전상태,
                다음상태 = x.다음상태,
                사유 = x.사유,
                전이시각Utc = x.전이시각Utc
            }).ToArray()
        };
    }

    public static string BuildMenuSummary(IEnumerable<음식주문상품Dto> products)
    {
        return string.Join(", ", products.Select(x => $"{x.상품명} {x.수량}"));
    }
}
