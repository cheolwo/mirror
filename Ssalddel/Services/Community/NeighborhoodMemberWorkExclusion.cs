using Microsoft.EntityFrameworkCore;
using Ssalddel.Domain.Community;
using 살뜰.Data;

namespace Ssalddel.Services.Community;

/// <summary>일반 회원과 기존 기사 수락이 동일 사용자별 SQL 행을 먼저 잠근다. 반드시 수락 transaction 안에서 호출한다.</summary>
public static class NeighborhoodMemberWorkExclusion
{
    public static async Task<bool> LockAndHasMemberWorkAsync(SsalddelContext db, string actor, CancellationToken ct)
    {
        if (db.Database.IsRelational())
            await db.Set<NeighborhoodMemberParticipation>().Where(x => x.UserId == actor)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.WorkLockRevision, x => x.WorkLockRevision + 1), ct);
        return await db.Set<NeighborhoodMemberParticipation>().AsNoTracking().AnyAsync(x => x.UserId == actor && x.ActiveRequestId != null, ct);
    }
}
