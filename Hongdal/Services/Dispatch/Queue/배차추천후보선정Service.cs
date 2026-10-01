using Microsoft.EntityFrameworkCore;
using Hongdal;
using 홍달.Services.Dispatch.Engine;

namespace 홍달.Services.Dispatch.Queue
{
    public sealed class 배차추천후보선정Service : I배차추천후보선정Service
    {
        private readonly HongdalContext _db;
        private readonly IReadOnlyDictionary<int, I운송의뢰배차엔진> _engines;

        public 배차추천후보선정Service(
            HongdalContext db,
            IEnumerable<I운송의뢰배차엔진> engines)
        {
            _db = db;
            ArgumentNullException.ThrowIfNull(engines);

            var engineGroups = engines
                .GroupBy(x => x.배차업무유형)
                .ToArray();
            var duplicateEngineTypes = engineGroups
                .Where(x => x.Count() > 1)
                .Select(x => x.Key)
                .OrderBy(x => x)
                .ToArray();
            if (duplicateEngineTypes.Length > 0)
            {
                throw new InvalidOperationException(
                    $"동일 배차업무유형의 배차 엔진이 중복 등록되었습니다. Types={string.Join(',', duplicateEngineTypes)}");
            }

            _engines = engineGroups.ToDictionary(x => x.Key, x => x.Single());
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

            if (!_engines.TryGetValue(queue.배차업무유형, out var engine))
            {
                return 배차추천후보선정결과.구성오류(
                    $"배차업무유형에 대응하는 엔진이 등록되지 않았습니다. Type={queue.배차업무유형}") with
                {
                    감사Context = 배차엔진판단감사Context.생성(
                        queue,
                        Hongdal.Contracts.Common.Versioning.EngineFamilyIds.TransportRequestDispatch,
                        배차엔진감사식별자.미등록구현,
                        제외기사Id)
                };
            }

            if (!배차실행인덱스재구성정책.미처리운송의뢰인가(queue, DateTime.UtcNow))
            {
                return 배차추천후보선정결과.준비안됨(
                    $"현재 배차대기 원장은 후보 선정 가능한 상태가 아닙니다. RequestId={requestId}") with
                {
                    감사Context = 배차엔진판단감사Context.생성(queue, engine, 제외기사Id)
                };
            }

            var selection = await engine.다음후보선정Async(queue, 제외기사Id, cancellationToken);
            return selection with
            {
                감사Context = 배차엔진판단감사Context.생성(queue, engine, 제외기사Id)
            };
        }
    }
}
