namespace RestaurantDeskApp.Services;

public interface I음식점조리시간설정Service
{
    음식점조리시간설정Snapshot 현재조회();
    Task 저장Async(int 음식점기본조리분, IReadOnlyDictionary<string, int> 상품별기본조리분,
        CancellationToken cancellationToken = default);
}

public sealed record 음식점조리시간설정Snapshot(
    int 음식점기본조리분, IReadOnlyDictionary<string, int> 상품별기본조리분);
