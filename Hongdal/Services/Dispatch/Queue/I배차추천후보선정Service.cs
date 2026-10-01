using System.Threading;
using System.Threading.Tasks;

namespace 홍달.Services.Dispatch.Queue
{
    public interface I배차추천후보선정Service
    {
        Task<배차추천후보선정결과> 다음후보선정Async(string requestId, string? 제외기사Id = null, CancellationToken cancellationToken = default);
    }

    public sealed record 배차추천후보(string DriverId, decimal 추천점수, string 추천사유);

    public enum 배차추천후보선정상태
    {
        선정됨 = 1,
        적격후보없음 = 2,
        준비안됨 = 3,
        잘못된입력 = 4,
        구성오류 = 5
    }

    public sealed record 배차추천후보선정결과
    {
        private 배차추천후보선정결과(
            배차추천후보선정상태 상태,
            배차추천후보? 후보,
            string 사유)
        {
            상태값 = 상태;
            this.후보 = 후보;
            this.사유 = 사유;
        }

        public 배차추천후보선정상태 상태값 { get; }

        public 배차추천후보? 후보 { get; }

        public string 사유 { get; }

        [System.Text.Json.Serialization.JsonIgnore]
        public 배차엔진판단감사Context? 감사Context { get; init; }

        public bool 공개배차전환허용 => 상태값 == 배차추천후보선정상태.적격후보없음;

        public static 배차추천후보선정결과 선정됨(배차추천후보 후보)
            => new(
                배차추천후보선정상태.선정됨,
                후보 ?? throw new ArgumentNullException(nameof(후보)),
                후보.추천사유);

        public static 배차추천후보선정결과 적격후보없음(string 사유)
            => 실패(배차추천후보선정상태.적격후보없음, 사유);

        public static 배차추천후보선정결과 준비안됨(string 사유)
            => 실패(배차추천후보선정상태.준비안됨, 사유);

        public static 배차추천후보선정결과 잘못된입력(string 사유)
            => 실패(배차추천후보선정상태.잘못된입력, 사유);

        public static 배차추천후보선정결과 구성오류(string 사유)
            => 실패(배차추천후보선정상태.구성오류, 사유);

        private static 배차추천후보선정결과 실패(배차추천후보선정상태 상태, string 사유)
            => new(
                상태,
                null,
                string.IsNullOrWhiteSpace(사유) ? "후보 선정 결과 사유가 제공되지 않았습니다." : 사유.Trim());
    }
}
