namespace 살뜰.Infrastructure.BackgroundJobs.FoodDeliveryDispatch;

/// <summary>음식 배달 대기열 후속 작업의 주기와 처리 한도입니다.</summary>
public sealed class 음식배달배차배치작업Options
{
    public const string SectionName = "FoodDeliveryDispatchJobs";

    public int 큐스캔주기초 { get; set; } = 30;
    public int 추천만료정리주기초 { get; set; } = 30;
    public int 알림발송주기초 { get; set; } = 15;
    public int 처리배치크기 { get; set; } = 100;
    public int 후보재탐색간격초 { get; set; } = 30;
}
