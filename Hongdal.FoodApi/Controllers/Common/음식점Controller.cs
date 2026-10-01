using Hongdal.Contracts.Restaurants;
using Microsoft.AspNetCore.Mvc;
using Hongdal.FoodApi.Services;

namespace Hongdal.FoodApi.Controllers.Common;

[ApiController]
[Route("api/v1/restaurants")]
public sealed class 음식점Controller : ControllerBase
{
    private readonly 음식샘플Store _store;

    public 음식점Controller(음식샘플Store store)
    {
        _store = store;
    }

    [HttpGet("nearby")]
    public ActionResult<음식점목록응답> 가까운조회(
        [FromQuery] decimal? latitude,
        [FromQuery] decimal? longitude,
        [FromQuery] decimal radiusKm = 7m,
        [FromQuery] int limit = 20)
    {
        if (latitude.HasValue != longitude.HasValue)
        {
            return ValidationProblem("latitude와 longitude는 함께 제공해야 합니다.");
        }

        if (latitude is < -90m or > 90m || longitude is < -180m or > 180m)
        {
            return ValidationProblem("위도 또는 경도의 범위가 올바르지 않습니다.");
        }

        return Ok(_store.GetNearbyRestaurants(
            latitude,
            longitude,
            Math.Clamp(radiusKm, 0.1m, 7m),
            Math.Clamp(limit, 1, 50)));
    }

    [HttpGet("popular")]
    public ActionResult<음식점목록응답> 인기조회()
    {
        return Ok(_store.GetPopularRestaurants());
    }

    [HttpGet("{restaurantId:long}/reviews")]
    public ActionResult<음식점리뷰목록응답> 리뷰조회(long restaurantId)
    {
        return Ok(_store.GetReviews(restaurantId));
    }

    [HttpPost("{restaurantId:long}/reviews")]
    public ActionResult<음식점리뷰요약응답> 리뷰등록(long restaurantId, [FromBody] 음식점리뷰등록요청 request)
    {
        return Ok(_store.AddReview(restaurantId, request));
    }
}
