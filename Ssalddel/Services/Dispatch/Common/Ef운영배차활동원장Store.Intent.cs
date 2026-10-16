using System.Collections.Concurrent;
using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Ssalddel.Contracts.Common.Dispatch;
using 살뜰.도메인.배차;

namespace 살뜰.Services.Dispatch.Common;

public sealed partial class Ef운영배차활동원장Store
{
    // 비관계형 시험 저장소만의 보호입니다. 운영 RDB는 같은 기사 인덱스 범위의 Serializable 잠금을 사용합니다.
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> NonRelationalIntentGates = new(StringComparer.Ordinal);

    public async ValueTask<bool> 수신의사추가Async(운영배차활동사건Dto 사건, bool 최초On조건부적용,
        string? 예상수신의사판본Token, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(사건);
        if (사건.사건유형Code != 운영배차사건유형Code.수신의사변경
            || 사건.주체역할Code != 운영배차주체역할Code.기사)
            throw new ArgumentException("기사 수신 의사 사건만 전용 쓰기 경계를 사용할 수 있습니다.", nameof(사건));
        var subject = 정리(사건.주체Id, nameof(사건));
        if (_db.Database.IsRelational() && _db.Database.CurrentTransaction is { } ambient
            && ambient.GetDbTransaction().IsolationLevel != IsolationLevel.Serializable)
            throw new InvalidOperationException("수신 의사 변경에는 Serializable 쓰기 경계가 필요합니다.");
        var gate = _db.Database.IsRelational() ? null : NonRelationalIntentGates.GetOrAdd(subject, static _ => new(1, 1));
        if (gate is not null) await gate.WaitAsync(cancellationToken);
        try
        {
            return _db.Database.CurrentTransaction is not null
                ? await WriteAsync()
                : await _db.Database.CreateExecutionStrategy().ExecuteAsync(WriteAsync);

            async Task<bool> WriteAsync()
            {
                // 첫 의사 사건이 없어도 범위 읽기와 INSERT가 같은 transaction에 있습니다.
                // SharedDriverWork의 선택적 설정 행이나 프로세스 잠금에 운영 정합성을 의존하지 않습니다.
                await using var transaction = _db.Database.IsRelational() && _db.Database.CurrentTransaction is null
                    ? await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken) : null;
                운영배차활동사건? added = null;
                try
                {
                    var existing = await 사건조회Async(사건.사건StableId, cancellationToken);
                    if (existing is not null) { 동일요청확인(existing, 사건); return false; }
                    var intents = await _db.운영배차활동사건.AsNoTracking()
                        .Where(x => x.주체Id == subject && x.사건유형Code == 운영배차사건유형Code.수신의사변경
                            && x.주체역할Code == 운영배차주체역할Code.기사)
                        .ToArrayAsync(cancellationToken);
                    if (최초On조건부적용 && !string.Equals(예상수신의사판본Token,
                            운영배차수신의사판본.계산(intents.Select(변환)), StringComparison.Ordinal))
                        return false;
                    added = 변환(사건);
                    // 발생 시각을 미래로 미루지 않고 기존 기록 시각으로 직렬 쓰기 순서를 보존합니다.
                    // MySQL datetime(6)의 정밀도에서 같은 시각·작은 StableId의 최신 OFF도 가려지지 않습니다.
                    if (intents.Length > 0)
                    {
                        var nextRecordedAt = intents.Max(x => x.기록시각Utc).AddTicks(10);
                        if (added.기록시각Utc < nextRecordedAt) added.기록시각Utc = nextRecordedAt;
                    }
                    _db.운영배차활동사건.Add(added);
                    await _db.SaveChangesAsync(cancellationToken);
                    if (transaction is not null) await transaction.CommitAsync(cancellationToken);
                    return true;
                }
                catch (DbUpdateException)
                {
                    await CleanupAsync();
                    // unique 경합과 commit 응답 유실은 동일 요청의 정본이 있을 때만 멱등 성공입니다.
                    var existing = await 사건조회Async(사건.사건StableId, cancellationToken);
                    if (existing is null) throw;
                    동일요청확인(existing, 사건);
                    return false;
                }
                catch
                {
                    await CleanupAsync();
                    throw;
                }

                async Task CleanupAsync()
                {
                    if (transaction is not null)
                    {
                        try { await transaction.RollbackAsync(CancellationToken.None); } catch { }
                        try { await transaction.DisposeAsync(); } catch { }
                    }
                    // 저장 성공 뒤 commit 결과가 불명확한 경우도 재시도는 정본부터 읽습니다.
                    if (added is not null) _db.Entry(added).State = EntityState.Detached;
                }
            }
        }
        finally { gate?.Release(); }
    }
}
