using Hongdal.Contracts.Admin.Restaurants;
using Hongdal.Contracts.Common.Participants;
using Hongdal.Contracts.Food;
using Hongdal.Contracts.Restaurants;
using Hongdal.FoodApi.Domain.Orders;

namespace Hongdal.FoodApi.Services;

public sealed class 음식샘플Store
{
    private readonly List<음식점요약응답> _restaurants;
    private readonly Dictionary<long, List<음식점리뷰요약응답>> _reviews;
    private readonly List<음식점리뷰관리항목응답> _moderationItems;
    private readonly List<음식주문> _orders;
    private readonly 음식점리뷰운영정책응답 _policy;

    public 음식샘플Store()
    {
        _restaurants =
        [
            new 음식점요약응답 { Id = 101, 상호명 = "홍달식당 강서점", 카테고리 = "한식", 주소 = "서울 강서구 화곡동", 위도 = 37.5412m, 경도 = 126.8409m, 거리Km = 0.6m, 평균평점 = 4.6m, 리뷰수 = 184, 주문가능여부 = true },
            new 음식점요약응답 { Id = 102, 상호명 = "목동반상", 카테고리 = "백반", 주소 = "서울 양천구 목동", 위도 = 37.5241m, 경도 = 126.8736m, 거리Km = 1.4m, 평균평점 = 4.4m, 리뷰수 = 132, 주문가능여부 = true },
            new 음식점요약응답 { Id = 103, 상호명 = "가양국수", 카테고리 = "분식", 주소 = "서울 강서구 가양동", 위도 = 37.5616m, 경도 = 126.8548m, 거리Km = 1.8m, 평균평점 = 4.1m, 리뷰수 = 67, 주문가능여부 = true, 저평점주의필요 = true }
        ];

        _reviews = new Dictionary<long, List<음식점리뷰요약응답>>
        {
            [103] =
            [
                new 음식점리뷰요약응답 { Id = 7001, 음식점Id = 103, 주문자UserId = "orderer-a", 주문번호 = "FOOD-10001", 별점 = 1, 내용 = "면이 불고 사진과 다르게 나왔습니다.", 사진Urls = ["https://example.com/review-1.jpg"], 사진포함여부 = true, 사장노출허용여부 = false, 관리자검토필요여부 = true, 관리자게시강제여부 = true, 현재노출여부 = true, CreatedAt = DateTime.UtcNow.AddDays(-1), 게시종료일시Utc = DateTime.UtcNow.AddDays(2) }
            ],
            [101] =
            [
                new 음식점리뷰요약응답 { Id = 7002, 음식점Id = 101, 주문자UserId = "orderer-b", 주문번호 = "FOOD-10098", 별점 = 5, 내용 = "포장도 깔끔하고 맛있었습니다.", 사진Urls = [], 사진포함여부 = false, 사장노출허용여부 = true, 관리자검토필요여부 = false, 관리자게시강제여부 = false, 현재노출여부 = true, CreatedAt = DateTime.UtcNow.AddHours(-10) }
            ]
        };

        _moderationItems =
        [
            new 음식점리뷰관리항목응답 { 리뷰Id = 7001, 음식점Id = 103, 음식점명 = "가양국수", 주문자UserId = "orderer-a", 주문번호 = "FOOD-10001", 별점 = 1, 내용 = "면이 불고 사진과 다르게 나왔습니다.", 사진포함여부 = true, 같은음식점기준저평점3회연속여부 = true, 사장노출허용여부 = false, 관리자게시강제여부 = true, 현재노출여부 = true, CreatedAt = DateTime.UtcNow.AddDays(-1), 게시종료일시Utc = DateTime.UtcNow.AddDays(2), 최근조치사유 = "같은 음식점 기준 1~2점 사진 리뷰 3회 연속" }
        ];

        _orders = FoodOrderSampleData.CreateOrders().Select(MapSampleOrder).ToList();

        _policy = new 음식점리뷰운영정책응답
        {
            Id = 1,
            기본저평점게시일수 = 3,
            허용게시일수옵션 = [3, 7],
            UpdatedAt = DateTime.UtcNow.AddHours(-3)
        };
    }

    public 음식점목록응답 GetNearbyRestaurants(
        decimal? latitude = null,
        decimal? longitude = null,
        decimal radiusKm = 7m,
        int limit = 20)
    {
        if (!latitude.HasValue || !longitude.HasValue)
        {
            return new 음식점목록응답
            {
                Items = _restaurants
                    .OrderBy(x => x.거리Km ?? decimal.MaxValue)
                    .Take(Math.Clamp(limit, 1, 50))
                    .ToArray()
            };
        }

        var appliedRadius = Math.Clamp(radiusKm, 0.1m, 7m);
        var items = _restaurants
            .Select(restaurant => new
            {
                Restaurant = restaurant,
                DistanceKm = DistanceKm(latitude.Value, longitude.Value, restaurant.위도, restaurant.경도)
            })
            .Where(item => item.DistanceKm <= appliedRadius)
            .OrderBy(item => item.DistanceKm)
            .Take(Math.Clamp(limit, 1, 50))
            .Select(item => CopyWithDistance(item.Restaurant, item.DistanceKm))
            .ToArray();
        return new 음식점목록응답 { Items = items };
    }

    public 음식점목록응답 GetPopularRestaurants() => new()
    {
        Items = _restaurants.OrderByDescending(x => x.평균평점).ThenByDescending(x => x.리뷰수).ToArray()
    };

    private static decimal DistanceKm(decimal latitude1, decimal longitude1, decimal latitude2, decimal longitude2)
    {
        const double earthRadiusKm = 6371.0088d;
        static double Radians(decimal degree) => (double)degree * Math.PI / 180d;

        var lat1 = Radians(latitude1);
        var lat2 = Radians(latitude2);
        var deltaLat = lat2 - lat1;
        var deltaLon = Radians(longitude2 - longitude1);
        var a = Math.Pow(Math.Sin(deltaLat / 2d), 2d)
                + Math.Cos(lat1) * Math.Cos(lat2) * Math.Pow(Math.Sin(deltaLon / 2d), 2d);
        var distance = earthRadiusKm * 2d * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1d - a));
        return Math.Round((decimal)distance, 2, MidpointRounding.AwayFromZero);
    }

    private static 음식점요약응답 CopyWithDistance(음식점요약응답 source, decimal distanceKm)
        => new()
        {
            Id = source.Id,
            상호명 = source.상호명,
            카테고리 = source.카테고리,
            주소 = source.주소,
            대표이미지Url = source.대표이미지Url,
            위도 = source.위도,
            경도 = source.경도,
            거리Km = distanceKm,
            평균평점 = source.평균평점,
            리뷰수 = source.리뷰수,
            주문가능여부 = source.주문가능여부,
            저평점주의필요 = source.저평점주의필요
        };

    public 음식점리뷰목록응답 GetReviews(long restaurantId) => new()
    {
        음식점Id = restaurantId,
        Items = _reviews.TryGetValue(restaurantId, out var items) ? items.OrderByDescending(x => x.CreatedAt).ToArray() : []
    };

    public 음식점리뷰요약응답 AddReview(long restaurantId, 음식점리뷰등록요청 request)
    {
        var item = new 음식점리뷰요약응답
        {
            Id = (_reviews.SelectMany(x => x.Value).MaxBy(x => x.Id)?.Id ?? 7000) + 1,
            음식점Id = restaurantId,
            주문자UserId = request.주문자UserId,
            주문번호 = request.주문번호,
            별점 = request.별점,
            내용 = request.내용,
            사진Urls = request.사진Urls,
            사진포함여부 = request.사진Urls.Count > 0,
            사장노출허용여부 = true,
            관리자검토필요여부 = request.별점 is 1 or 2 && request.사진Urls.Count > 0,
            관리자게시강제여부 = false,
            현재노출여부 = true,
            CreatedAt = DateTime.UtcNow
        };

        if (!_reviews.TryGetValue(restaurantId, out var list))
        {
            list = [];
            _reviews[restaurantId] = list;
        }

        list.Add(item);
        return item;
    }

    public 음식점리뷰관리목록응답 GetModerationItems() => new()
    {
        Items = _moderationItems.OrderByDescending(x => x.CreatedAt).ToArray()
    };

    public 음식점리뷰운영정책응답 GetPolicy() => _policy;

    public 음식주문목록응답 GetOrders() => new()
    {
        Items = _orders.OrderByDescending(x => x.CreatedAt).Select(MapOrder).ToArray()
    };

    public 음식주문응답 AddOrder(음식주문등록요청 request)
    {
        var order = new 음식주문
        {
            주문번호 = $"FOOD-{DateTime.UtcNow:yyyyMMddHHmmss}",
            음식점Id = request.음식점Id,
            주문자UserId = request.주문자UserId,
            수령인정보 = new 음식주문수령인정보
            {
                수령인명 = request.수령인정보.수령인명,
                연락처 = request.수령인정보.연락처,
                주소 = request.수령인정보.주소,
                상세주소 = request.수령인정보.상세주소,
                요청사항 = request.수령인정보.요청사항,
                주문자본인수령여부 = request.수령인정보.주문자본인수령여부
            },
            상품목록 = request.상품목록.Select(x => new 음식주문상품
            {
                상품명 = x.상품명,
                수량 = x.수량,
                단가 = x.단가
            }).ToList(),
            총주문금액 = request.상품목록.Sum(x => x.단가 * x.수량),
            상태 = "주문접수",
            결제수단 = request.결제수단,
            CreatedAt = DateTime.UtcNow
        };

        _orders.Add(order);
        return MapOrder(order);
    }

    public 음식주문응답? 배차대기전환(string 주문번호)
    {
        var order = _orders.FirstOrDefault(x => string.Equals(x.주문번호, 주문번호, StringComparison.Ordinal));
        if (order is null)
        {
            return null;
        }

        order.상태 = "배차대기";
        return MapOrder(order);
    }

    public 음식점요약응답? 음식점조회(long 음식점Id)
    {
        return _restaurants.FirstOrDefault(x => x.Id == 음식점Id);
    }

    private static 음식주문 MapSampleOrder(음식주문응답 order)
    {
        return new 음식주문
        {
            주문번호 = order.주문번호,
            음식점Id = order.음식점Id,
            주문자UserId = order.주문자UserId,
            수령인정보 = new 음식주문수령인정보
            {
                수령인명 = order.수령인정보.수령인명,
                연락처 = order.수령인정보.연락처,
                주소 = order.수령인정보.주소,
                상세주소 = order.수령인정보.상세주소,
                요청사항 = order.수령인정보.요청사항,
                주문자본인수령여부 = order.수령인정보.주문자본인수령여부
            },
            상품목록 = order.상품목록.Select(x => new 음식주문상품
            {
                상품명 = x.상품명,
                수량 = x.수량,
                단가 = x.단가
            }).ToList(),
            총주문금액 = order.총주문금액,
            상태 = order.상태,
            결제수단 = order.결제수단,
            CreatedAt = order.CreatedAt
        };
    }

    private static 음식주문응답 MapOrder(음식주문 order)
    {
        return new 음식주문응답
        {
            주문번호 = order.주문번호,
            음식점Id = order.음식점Id,
            주문자UserId = order.주문자UserId,
            수령인정보 = new 음식주문수령인정보Dto
            {
                수령인명 = order.수령인정보.수령인명,
                연락처 = order.수령인정보.연락처,
                주소 = order.수령인정보.주소,
                상세주소 = order.수령인정보.상세주소,
                요청사항 = order.수령인정보.요청사항,
                주문자본인수령여부 = order.수령인정보.주문자본인수령여부
            },
            상품목록 = order.상품목록.Select(x => new 음식주문상품Dto
            {
                상품명 = x.상품명,
                수량 = x.수량,
                단가 = x.단가
            }).ToArray(),
            총주문금액 = order.총주문금액,
            상태 = order.상태,
            결제수단 = order.결제수단,
            CreatedAt = order.CreatedAt
        };
    }
}
