namespace 살뜰.Services.Dispatch.Queue;

public interface I음식배달기사추천기록Service
{
    Task 추천기록Async(
        string driverId,
        DateTime 추천시각Utc,
        CancellationToken cancellationToken = default);
}
