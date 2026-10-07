namespace 살뜰.Services.Dispatch.Notification
{
    public interface I배차추천알림Service
    {
        Task 추천알림요청생성Async(long 배차대기Id, string 의뢰Id, string 기사Id, int 추천라운드, CancellationToken cancellationToken = default);
        Task<int> 대기알림발송Async(int take = 100, CancellationToken cancellationToken = default);
        Task<int> 업무유형별대기알림발송Async(int 배차업무유형, int take = 100, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("업무 유형별 배차 추천 알림 발송 구현이 필요합니다.");
    }
}
