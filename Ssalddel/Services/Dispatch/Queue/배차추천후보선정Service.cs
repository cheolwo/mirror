using Microsoft.EntityFrameworkCore;
using Ssalddel;
using 살뜰.Services.Dispatch.Engine;

namespace 살뜰.Services.Dispatch.Queue
{
    public sealed class 배차추천후보선정Service : I배차추천후보선정Service
    {
        private readonly SsalddelContext _db;
        private readonly I운영체제배차EngineCatalog _engineCatalog;

        public 배차추천후보선정Service(
            SsalddelContext db,
            I운영체제배차EngineCatalog engineCatalog)
        {
            _db = db;
            _engineCatalog = engineCatalog ?? throw new ArgumentNullException(nameof(engineCatalog));
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

            var selection = await engine.다음후보선정Async(queue, engineContext, 제외기사Id, cancellationToken);
            return selection with
            {
                감사Context = 배차엔진판단감사Context.생성(queue, engine, 제외기사Id)
            };
        }
    }
}
