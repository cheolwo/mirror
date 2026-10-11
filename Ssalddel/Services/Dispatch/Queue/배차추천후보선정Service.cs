using Microsoft.EntityFrameworkCore;
using Ssalddel;
using 살뜰.Services.Dispatch.Engine;
using 살뜰.Services.Dispatch.AI;
using 살뜰.도메인.공통;
using 살뜰.도메인.운송;

namespace 살뜰.Services.Dispatch.Queue
{
    public sealed class 배차추천후보선정Service : I배차추천후보선정Service
    {
        private readonly SsalddelContext _db;
        private readonly I운영체제배차EngineCatalog _engineCatalog;
        private readonly IFoodDispatchCandidateAI? _candidateAI;

        public 배차추천후보선정Service(
            SsalddelContext db,
            I운영체제배차EngineCatalog engineCatalog,
            IFoodDispatchCandidateAI? candidateAI = null)
        {
            _db = db;
            _engineCatalog = engineCatalog ?? throw new ArgumentNullException(nameof(engineCatalog));
            _candidateAI = candidateAI;
        }

        public async Task<배차추천후보선정결과> 다음후보선정Async(string requestId, string? 제외기사Id = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(requestId))
            {
                return 배차추천후보선정결과.잘못된입력("운송 의뢰 ID가 제공되지 않았습니다.");
            }

            var queue = await _db.운송원장.AsNoTracking().FirstOrDefaultAsync(x => x.의뢰Id == requestId, cancellationToken);
            if (queue is null)
            {
                return 배차추천후보선정결과.잘못된입력($"배차대기 원장을 찾을 수 없습니다. RequestId={requestId}");
            }

            if (!Ssalddel.Services.Community.생활배송배차Policy.자동추천가능(queue))
                return 배차추천후보선정결과.준비안됨("생활 배송 합의 연결 대기 또는 공개 콜 전용 의뢰입니다.");
            if (!_engineCatalog.TryResolve(queue, out var enginePlan, out var catalogReason))
            {
                return 배차추천후보선정결과.구성오류(
                    catalogReason) with
                {
                    감사Context = 배차엔진판단감사Context.생성(
                        queue,
                        Ssalddel.Contracts.Common.Versioning.EngineFamilyIds.TransportRequestDispatch,
                        배차엔진감사식별자.미등록구현,
                        제외기사Id)
                };
            }

            var engine = enginePlan.PrimaryEngine;

            운송의뢰배차Engine입력Context? engineContext = null;
            if (enginePlan.운영체제Id == Ssalddel.Contracts.Common.Versioning.OperatingSystemIds.DomesticCargoTransport)
            {
                var transportMethod = await _db.화주운송의뢰
                    .AsNoTracking()
                    .Where(request => request.의뢰Id == queue.의뢰Id)
                    .Select(request => request.운송방식)
                    .SingleOrDefaultAsync(cancellationToken);
                if (transportMethod is null)
                {
                    return 배차추천후보선정결과.잘못된입력(
                        $"화물/용달 배차에 필요한 운송 의뢰를 찾을 수 없습니다. RequestId={queue.의뢰Id}") with
                    {
                        감사Context = 배차엔진판단감사Context.생성(queue, engine, 제외기사Id)
                    };
                }

                engineContext = new 운송의뢰배차Engine입력Context(transportMethod);
            }

            if (!배차실행인덱스재구성정책.미처리운송의뢰인가(queue, DateTime.UtcNow))
            {
                return 배차추천후보선정결과.준비안됨(
                    $"현재 배차대기 원장은 후보 선정 가능한 상태가 아닙니다. RequestId={requestId}") with
                {
                    감사Context = 배차엔진판단감사Context.생성(queue, engine, 제외기사Id)
                };
            }

            var before = 음식배달선정원장판본.생성(queue);
            var selection = await engine.다음후보선정Async(queue, engineContext, 제외기사Id, cancellationToken);
            if (_candidateAI?.Enabled == true && queue.배차업무유형 == 상태값.배차업무유형.음식배달)
            {
                var current = await _db.운송원장.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.의뢰Id == requestId, cancellationToken);
                if (current is null || before != 음식배달선정원장판본.생성(current)
                    || !배차실행인덱스재구성정책.미처리운송의뢰인가(current, DateTime.UtcNow))
                    return 배차추천후보선정결과.준비안됨("FoodDispatchAI.QueueChanged") with
                    {
                        감사Context = 배차엔진판단감사Context.생성(queue, engine, 제외기사Id)
                    };
            }
            return selection with
            {
                감사Context = 배차엔진판단감사Context.생성(queue, engine, 제외기사Id)
            };
        }
    }

    // 추론 대기 전후의 서버 원장 조건을 비교한다. 모델 입력이나 영속 DTO로 사용하지 않는다.
    internal sealed record 음식배달선정원장판본(
        DateTime? RowVersion, DateTime UpdatedAt, int 업무유형, string 상태, int 단계, int 노출,
        int 추천라운드, string? 추천기사, string? 확정기사, string 운송기사, string? 마지막거절기사,
        DateTime? 추천시작, DateTime? 추천만료, string 원본유형, string 원본Id,
        decimal? 픽업위도, decimal? 픽업경도, decimal? 전달위도, decimal? 전달경도,
        string? 생활배송방식, long 생활배송판본, bool 생활배송준비, string? 커뮤니티상태)
    {
        internal static 음식배달선정원장판본 생성(운송원장 queue) => new(
            queue.RowVersion, DB시각정밀도(queue.UpdatedAt), queue.배차업무유형, queue.상태, queue.배차큐단계,
            queue.배차노출상태, queue.추천라운드, queue.현재추천대상기사Id, queue.확정기사Id,
            queue.기사_운송자, queue.마지막거절기사Id, DB시각정밀도(queue.추천시작시각), DB시각정밀도(queue.추천만료시각),
            queue.원본의뢰유형, queue.원본의뢰Id, queue.픽업_위도, queue.픽업_경도,
            queue.하차_위도, queue.하차_경도, queue.생활배송배차방식, queue.생활배송선택판본,
            queue.생활배송수락준비완료, queue.커뮤니티원장상태);

        // MySQL datetime(6)의 저장 정밀도에 맞춘다. DB가 생성한 RowVersion은 원값 그대로 비교한다.
        private static DateTime DB시각정밀도(DateTime value) => value.AddTicks(-(value.Ticks % 10));
        private static DateTime? DB시각정밀도(DateTime? value) => value.HasValue ? DB시각정밀도(value.Value) : null;
    }
}
