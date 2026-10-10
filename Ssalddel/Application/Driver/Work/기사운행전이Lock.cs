using Ssalddel.Contracts.Common.Metadata;

using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using 살뜰.Data;

namespace Ssalddel.Application.Driver.Work;

/// <summary>한 프로세스의 전이 순서와 여러 서버의 SQL 행 잠금을 함께 사용합니다.</summary>
[SsalddelCodeMetadata(
    SsalddelCodeFeatureKeys.FoodDriverWork,
    SsalddelCodeLayer.Application,
    "운행 시작·종료·GPS·후속의 기사별 전이 순서를 직렬화한다.",
    Effects = SsalddelCodeEffect.PersistentWrite | SsalddelCodeEffect.StateMutation,
    FlowOrder = 50,
    StepKey = "application.work-transition-lock",
    DependsOnStepKeys = ["infrastructure.shift-mapping"],
    ExecutionStage = SsalddelCodeExecutionStage.Confirm,
    ReadsFrom = SsalddelCodeDataScope.OperationalState,
    WritesTo = SsalddelCodeDataScope.OperationalState,
    Boundary = "프로세스 잠금과 transaction 내부의 SQL 행 잠금을 함께 사용한다. 동일 값을 유지하는 UPDATE도 DB 쓰기이며 잠금 자체가 업무 성공을 확정하지 않는다.")]
public static class 기사운행전이Lock
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Gates = new(StringComparer.Ordinal);

    public static async Task<IDisposable> 진입Async(string driverId, CancellationToken ct)
    {
        var gate = Gates.GetOrAdd(driverId, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        return new Release(gate);
    }

    // 반드시 transaction 안에서 호출. 값은 유지하면서 UPDATE 행 잠금을 잡습니다.
    public static async Task 행잠금Async(SsalddelContext db, string driverId, CancellationToken ct)
    {
        if (db.Database.IsRelational())
            await db.용달기사.Where(x => x.기사Id == driverId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.UpdatedAt, x => x.UpdatedAt), ct);
    }

    private sealed class Release(SemaphoreSlim gate) : IDisposable
    {
        public void Dispose() => gate.Release();
    }
}
