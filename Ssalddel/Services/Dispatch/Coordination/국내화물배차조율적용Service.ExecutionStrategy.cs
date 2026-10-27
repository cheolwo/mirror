using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using 살뜰.도메인.공통;
using 살뜰.도메인.운송;

namespace 살뜰.Services.Dispatch.Coordination;

public sealed partial class 국내화물배차조율적용Service
{
    private sealed record 저장한추천(국내화물배차추천잠금 잠금, string 근거Json, decimal? 지급예정액)
    {
        // 같은 SQL transaction에 실제로 참여한 호출자 변경의 마지막 저장 직후 상태다.
        // commit 확인으로 성공이 증명된 경우에만 이 범위의 추적 상태를 수렴시킨다.
        public 추적상태[] 호출자저장상태 { get; init; } = [];

        public bool 일치(운송원장 row)
            => row.의뢰Id == 잠금.의뢰Id && row.현재추천대상기사Id == 잠금.기사Id
                && row.추천라운드 == 잠금.추천라운드 && row.추천시작시각 == 잠금.추천시작시각Utc
                && row.추천만료시각 == 잠금.추천만료시각Utc && row.추천판정근거Json == 근거Json
                && row.기사지급예정액 == 지급예정액 && row.상태 == 상태값.배차대기상태.대기
                && row.배차큐단계 == 상태값.배차큐단계.배차추천 && row.배차노출상태 == 상태값.배차노출상태.추천중;
    }

    private sealed record 추적상태(EntityEntry Entry, EntityState State, PropertyValues Current, PropertyValues Original,
        IReadOnlyDictionary<string, (bool Modified, bool Temporary)> Properties);

    private 추적상태[] 추적상태보관()
        => _db.ChangeTracker.Entries().Select(추적상태보관).ToArray();

    private static 추적상태 추적상태보관(EntityEntry entry)
        => new(entry, entry.State, entry.CurrentValues.Clone(), entry.OriginalValues.Clone(),
            entry.Properties.ToDictionary(p => p.Metadata.Name, p => (p.IsModified, p.IsTemporary)));

    private void 추적상태복원(추적상태[] before, bool 새추적해제 = true)
    {
        if (새추적해제)
        {
            var retained = before.Select(x => x.Entry.Entity).ToHashSet(ReferenceEqualityComparer.Instance);
            foreach (var entry in _db.ChangeTracker.Entries().Where(x => !retained.Contains(x.Entity)).ToArray())
                entry.State = EntityState.Detached;
        }
        foreach (var saved in before)
        {
            // ChangeTracker.Clear는 호출자가 먼저 추적하던 별도 변경까지 버리므로 쓰지 않는다.
            var entry = saved.Entry;
            entry.State = EntityState.Detached;
            entry.CurrentValues.SetValues(saved.Current);
            entry.OriginalValues.SetValues(saved.Original);
            entry.State = saved.State;
            foreach (var property in entry.Properties)
            {
                var flags = saved.Properties[property.Metadata.Name];
                property.IsModified = flags.Modified;
                property.IsTemporary = flags.Temporary;
            }
        }
    }
}
