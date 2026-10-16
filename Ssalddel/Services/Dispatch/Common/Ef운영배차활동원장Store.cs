using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Common.Dispatch;
using 살뜰.Data;
using 살뜰.도메인.배차;

namespace 살뜰.Services.Dispatch.Common;

public sealed partial class Ef운영배차활동원장Store : I운영배차활동원장Store
{
    private readonly SsalddelContext _db;

    public Ef운영배차활동원장Store(SsalddelContext db)
    {
        _db = db;
    }

    public async ValueTask<운영배차활동사건Dto?> 사건조회Async(
        string 사건StableId,
        CancellationToken cancellationToken = default)
    {
        var id = 정리(사건StableId, nameof(사건StableId));
        var entity = await _db.운영배차활동사건
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.사건StableId == id, cancellationToken);
        return entity is null ? null : 변환(entity);
    }

    public async ValueTask<bool> 사건추가Async(
        운영배차활동사건Dto 사건,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(사건);
        if (사건.사건유형Code == 운영배차사건유형Code.수신의사변경)
            return await 수신의사추가Async(사건, false, null, cancellationToken);
        var existing = await 사건조회Async(사건.사건StableId, cancellationToken);
        if (existing is not null)
        {
            동일요청확인(existing, 사건);
            return false;
        }

        var entity = 변환(사건);
        _db.운영배차활동사건.Add(entity);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException)
        {
            _db.Entry(entity).State = EntityState.Detached;
            existing = await 사건조회Async(사건.사건StableId, cancellationToken);
            if (existing is null)
            {
                throw;
            }

            동일요청확인(existing, 사건);
            return false;
        }
    }

    public async ValueTask<IReadOnlyList<운영배차활동사건Dto>> 기간조회Async(
        string 주체Id,
        DateTimeOffset 시작시각UtcInclusive,
        DateTimeOffset 종료시각UtcExclusive,
        CancellationToken cancellationToken = default)
    {
        var subjectId = 정리(주체Id, nameof(주체Id));
        var from = 시작시각UtcInclusive.UtcDateTime;
        var until = 종료시각UtcExclusive.UtcDateTime;
        if (from >= until)
        {
            throw new ArgumentException("운영 배차 원장 조회 종료 시각은 시작 시각보다 뒤여야 합니다.");
        }

        var entities = await _db.운영배차활동사건
            .AsNoTracking()
            .Where(x => x.주체Id == subjectId && x.발생시각Utc >= from && x.발생시각Utc < until)
            .OrderBy(x => x.발생시각Utc)
            .ThenBy(x => x.사건StableId)
            .ToArrayAsync(cancellationToken);
        return entities.Select(변환).ToArray();
    }

    private static 운영배차활동사건 변환(운영배차활동사건Dto dto)
        => new()
        {
            사건StableId = 정리(dto.사건StableId, nameof(dto.사건StableId)),
            발생시각Utc = dto.발생시각Utc.UtcDateTime,
            운영시장시간대Id = dto.운영시장시간대Id,
            ShiftId = dto.ShiftId,
            제안Id = dto.제안Id,
            주문Id = dto.주문Id,
            업무시도Id = dto.업무시도Id,
            주체Id = 정리(dto.주체Id, nameof(dto.주체Id)),
            주체역할Code = dto.주체역할Code,
            사건유형Code = dto.사건유형Code,
            제안유효성Code = dto.제안유효성Code,
            책임Code = dto.책임Code,
            사유Code = dto.사유Code,
            상태값Code = dto.상태값Code,
            지표단위수 = dto.지표단위수,
            기록시각Utc = DateTime.UtcNow
        };

    private static 운영배차활동사건Dto 변환(운영배차활동사건 entity)
        => new()
        {
            사건StableId = entity.사건StableId,
            발생시각Utc = new DateTimeOffset(DateTime.SpecifyKind(entity.발생시각Utc, DateTimeKind.Utc)),
            원장기록시각Utc = entity.기록시각Utc == default ? null
                : new DateTimeOffset(DateTime.SpecifyKind(entity.기록시각Utc, DateTimeKind.Utc)),
            운영시장시간대Id = entity.운영시장시간대Id,
            ShiftId = entity.ShiftId,
            제안Id = entity.제안Id,
            주문Id = entity.주문Id,
            업무시도Id = entity.업무시도Id,
            주체Id = entity.주체Id,
            주체역할Code = entity.주체역할Code,
            사건유형Code = entity.사건유형Code,
            제안유효성Code = entity.제안유효성Code,
            책임Code = entity.책임Code,
            사유Code = entity.사유Code,
            상태값Code = entity.상태값Code,
            지표단위수 = entity.지표단위수
        };

    private static void 동일요청확인(운영배차활동사건Dto existing, 운영배차활동사건Dto requested)
    {
        var same = existing.운영시장시간대Id == requested.운영시장시간대Id
                   && existing.ShiftId == requested.ShiftId
                   && existing.제안Id == requested.제안Id
                   && existing.주문Id == requested.주문Id
                   && existing.업무시도Id == requested.업무시도Id
                   && existing.주체Id == requested.주체Id
                   && existing.주체역할Code == requested.주체역할Code
                   && existing.사건유형Code == requested.사건유형Code
                   && existing.제안유효성Code == requested.제안유효성Code
                   && existing.책임Code == requested.책임Code
                   && existing.사유Code == requested.사유Code
                   && existing.상태값Code == requested.상태값Code
                   && existing.지표단위수 == requested.지표단위수;
        if (!same)
        {
            if (requested.사건유형Code == 운영배차사건유형Code.수신의사변경)
                throw new 운영배차수신요청충돌Exception();
            throw new InvalidOperationException($"같은 사건 Stable ID '{requested.사건StableId}'에 다른 요청 내용이 있습니다.");
        }
    }

    private static string 정리(string value, string parameterName)
    {
        var clean = value?.Trim();
        return string.IsNullOrWhiteSpace(clean)
            ? throw new ArgumentException("값을 비워 둘 수 없습니다.", parameterName)
            : clean;
    }
}
