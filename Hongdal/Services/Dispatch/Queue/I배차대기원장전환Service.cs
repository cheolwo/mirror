using System.Threading;
using System.Threading.Tasks;
using Hongdal.Contracts.Common.Operations;

namespace 홍달.Services.Dispatch.Queue
{
    public interface I배차대기원장전환Service
    {
        Task<배차대기원장전환결과> 계획배차에서추천으로전환Async(string requestId, CancellationToken cancellationToken = default);
        Task<배차대기원장전환결과> 추천대기처리Async(string requestId, CancellationToken cancellationToken = default);
        Task<배차대기원장전환결과> 추천시작Async(string requestId, string driverId, int? timeoutSeconds = null, CancellationToken cancellationToken = default);
        Task<배차대기원장전환결과> 추천거절처리Async(string requestId, string driverId, CancellationToken cancellationToken = default);
        Task<배차대기원장전환결과> 추천만료처리Async(string requestId, CancellationToken cancellationToken = default);
        Task<배차대기원장전환결과> 공개배차로전환Async(string requestId, CancellationToken cancellationToken = default);
        Task<배차대기원장전환결과> 실행주체확정결과동기화Async(
            string requestId,
            DispatchConfirmationBoundaryRequest confirmation,
            CancellationToken cancellationToken = default);
        Task<배차대기원장전환결과> 배차수락취소처리Async(string requestId, string driverId, string? reason = null, CancellationToken cancellationToken = default);
    }

    public sealed record 배차대기원장전환결과(
        bool 전환여부,
        string 결과코드,
        string 메시지,
        string? 의뢰Id,
        string? 기사Id = null)
    {
        public static 배차대기원장전환결과 전환됨(string 의뢰Id, string 결과코드, string 메시지, string? 기사Id = null)
            => new(true, 결과코드, 메시지, 의뢰Id, 기사Id);

        public static 배차대기원장전환결과 전환안됨(string? 의뢰Id, string 결과코드, string 메시지, string? 기사Id = null)
            => new(false, 결과코드, 메시지, 의뢰Id, 기사Id);
    }

    public static class 배차대기원장전환결과코드
    {
        public const string 전환됨 = "전환됨";
        public const string 대상없음 = "대상없음";
        public const string 대기상태아님 = "대기상태아님";
        public const string 단계불일치 = "단계불일치";
        public const string 추천시작됨 = "추천시작됨";
        public const string 이미추천중 = "이미추천중";
        public const string 현재추천기사불일치 = "현재추천기사불일치";
        public const string 만료전 = "만료전";
        public const string 공개배차전환됨 = "공개배차전환됨";
        public const string 확정됨 = "확정됨";
        public const string 후보없음 = "후보없음";
        public const string 추천준비안됨 = "추천준비안됨";
        public const string 후보선정입력오류 = "후보선정입력오류";
        public const string 배차구성오류 = "배차구성오류";
        public const string 수락취소됨 = "수락취소됨";
        public const string 창고선행작업대기 = "창고선행작업대기";
        public const string 실행결정권한없음 = "실행결정권한없음";
        public const string 실행결정불일치 = "실행결정불일치";
    }
}
