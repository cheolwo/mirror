using Microsoft.EntityFrameworkCore;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.Warehouse;
using Ssalddel.Services.Community;
using 살뜰.Data;
using 살뜰.도메인.창고;
using 살뜰.Services.Options;

namespace Ssalddel.Services.LogisticsProcessing.Warehouse;

public interface I생활권물류거점Service
{
    Task<생활권물류거점Response> 신청Async(생활권물류거점신청Request request, CancellationToken cancellationToken);
    Task<IReadOnlyList<생활권물류거점Response>> 내신청조회Async(CancellationToken cancellationToken);
    Task<IReadOnlyList<생활권물류거점공개Response>> 공개조회Async(string? 생활권Key, CancellationToken cancellationToken);
    Task<생활권물류거점Response> 소유자동의Async(Guid id, 생활권물류거점동의Request request, CancellationToken cancellationToken);
    Task<생활권물류거점Response> 관리주체동의Async(Guid id, 생활권물류거점동의Request request, CancellationToken cancellationToken);
    Task<생활권물류거점Response> 관리자검토Async(Guid id, 생활권물류거점검토Request request, string actorUserId, CancellationToken cancellationToken);
    Task<생활권물류거점Response> 상태변경Async(Guid id, 생활권물류거점상태변경Request request, string actorUserId, CancellationToken cancellationToken);
    Task<생활권물류거점예약Response> 예약Async(Guid id, 생활권물류거점예약Request request, CancellationToken cancellationToken);
    Task<생활권물류거점완료Response> 인계완료Async(Guid id, Guid reservationId, string idempotencyKey, CancellationToken cancellationToken);
}

public sealed class 생활권물류거점Service(
    SsalddelContext db,
    ICurrentUserAccessor currentUser,
    ISsalddelExecutionModePolicy executionMode,
    I커뮤니티원장저장소 communityLedgerStore) : I생활권물류거점Service
{
    public async Task<생활권물류거점Response> 신청Async(
        생활권물류거점신청Request request,
        CancellationToken cancellationToken)
    {
        ValidateApplication(request);
        var userId = RequireUser();
        await RequireApplicationLedgerAsync(request.신청가원장Id, userId, cancellationToken);
        var now = DateTime.UtcNow;
        var id = Guid.NewGuid();
        var entity = new 생활권물류거점
        {
            Id = id,
            StableId = $"neighborhood-logistics-hub:{id:N}",
            신청가원장Id = request.신청가원장Id.Trim(),
            신청자UserId = userId,
            관리담당자UserId = request.관리담당자UserId.Trim(),
            공간StableId = request.공간StableId.Trim(),
            공간유형Code = 생활권물류거점공간유형Codes.독립비주거공간,
            생활권Key = request.생활권Key.Trim(),
            대략위치Label = request.대략위치Label.Trim(),
            정확위치보호참조 = request.정확위치보호참조.Trim(),
            최대동시보관건수 = request.최대동시보관건수,
            최대총중량Kg = request.최대총중량Kg,
            최대보관시간분 = request.최대보관시간분,
            입고가능시간창 = request.입고가능시간창.Trim(),
            수령가능시간창 = request.수령가능시간창.Trim(),
            완료건당고정보상 = request.완료건당고정보상,
            상태Code = 생활권물류거점상태Codes.Candidate,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            StatusChangedAtUtc = now
        };

        db.생활권물류거점.Add(entity);
        await SaveAsync(cancellationToken);
        return ToResponse(entity);
    }

    public async Task<IReadOnlyList<생활권물류거점Response>> 내신청조회Async(CancellationToken cancellationToken)
    {
        var userId = RequireUser();
        return await db.생활권물류거점.AsNoTracking()
            .Where(x => x.신청자UserId == userId || x.관리담당자UserId == userId)
            .OrderByDescending(x => x.UpdatedAtUtc)
            .Select(x => ToResponse(x))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<생활권물류거점공개Response>> 공개조회Async(
        string? 생활권Key,
        CancellationToken cancellationToken)
    {
        var query = db.생활권물류거점.AsNoTracking()
            .Where(x => x.상태Code == 생활권물류거점상태Codes.Pilot || x.상태Code == 생활권물류거점상태Codes.Active);
        if (!string.IsNullOrWhiteSpace(생활권Key))
        {
            var normalized = 생활권Key.Trim();
            query = query.Where(x => x.생활권Key == normalized);
        }

        return await query.OrderBy(x => x.생활권Key).ThenBy(x => x.StableId)
            .Select(x => new 생활권물류거점공개Response
            {
                StableId = x.StableId,
                생활권Key = x.생활권Key,
                대략위치Label = x.대략위치Label,
                상태Code = x.상태Code,
                기사인계가능 = x.기사인계가능,
                주문자수령가능 = x.주문자수령가능,
                가동상태Code = x.현재예약건수 >= x.최대동시보관건수 ? "CapacityFull" : "Available"
            })
            .ToArrayAsync(cancellationToken);
    }

    public Task<생활권물류거점Response> 소유자동의Async(
        Guid id,
        생활권물류거점동의Request request,
        CancellationToken cancellationToken)
        => ChangeConsentAsync(id, request, owner: true, cancellationToken);

    public Task<생활권물류거점Response> 관리주체동의Async(
        Guid id,
        생활권물류거점동의Request request,
        CancellationToken cancellationToken)
        => ChangeConsentAsync(id, request, owner: false, cancellationToken);

    public async Task<생활권물류거점Response> 관리자검토Async(
        Guid id,
        생활권물류거점검토Request request,
        string actorUserId,
        CancellationToken cancellationToken)
    {
        var entity = await RequireHubAsync(id, cancellationToken);
        EnsureRevision(entity, request.ExpectedRevision);
        var now = DateTime.UtcNow;
        entity.현장확인시각Utc = request.현장확인 ? now : null;
        entity.플랫폼승인 = request.플랫폼승인;
        entity.플랫폼승인시각Utc = request.플랫폼승인 ? now : null;
        entity.상태사유 = NormalizeReason(request.사유);
        entity.UpdatedAtUtc = now;
        entity.Revision++;
        await SaveAsync(cancellationToken);
        return ToResponse(entity);
    }

    public async Task<생활권물류거점Response> 상태변경Async(
        Guid id,
        생활권물류거점상태변경Request request,
        string actorUserId,
        CancellationToken cancellationToken)
    {
        var entity = await RequireHubAsync(id, cancellationToken);
        EnsureRevision(entity, request.ExpectedRevision);
        var target = request.상태Code?.Trim() ?? string.Empty;
        if (!생활권물류거점상태Codes.All.Contains(target)
            || !생활권물류거점Policy.CanTransition(entity.상태Code, target))
            throw new InvalidOperationException($"{entity.상태Code}에서 {target}(으)로 전환할 수 없습니다.");

        if (target is 생활권물류거점상태Codes.Pilot or 생활권물류거점상태Codes.Active)
        {
            var readinessError = 생활권물류거점Policy.GetReadinessError(entity);
            if (readinessError is not null) throw new InvalidOperationException(readinessError);
            if (executionMode.IsOperational)
                throw new InvalidOperationException("실제 보관 운영은 운영·법률·정산 관문이 열리기 전 활성화할 수 없습니다.");
        }

        if (target == 생활권물류거점상태Codes.Pilot && !entity.연결창고Id.HasValue)
        {
            var warehouse = new 창고
            {
                소유자UserId = entity.신청자UserId,
                소유자유형 = 창고소유자유형.운영자,
                창고유형 = 창고유형.임시보관소,
                창고명 = $"{entity.대략위치Label} 생활권 인계 거점",
                주소 = entity.정확위치보호참조,
                담당자명 = entity.관리담당자UserId,
                기본창고여부 = false,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            db.창고.Add(warehouse);
            await db.SaveChangesAsync(cancellationToken);
            entity.연결창고Id = warehouse.Id;
        }

        entity.상태Code = target;
        entity.상태사유 = NormalizeReason(request.사유);
        entity.실운영허용 = false;
        entity.StatusChangedAtUtc = DateTime.UtcNow;
        entity.UpdatedAtUtc = entity.StatusChangedAtUtc;
        entity.Revision++;
        await SaveAsync(cancellationToken);
        return ToResponse(entity);
    }

    public async Task<생활권물류거점예약Response> 예약Async(
        Guid id,
        생활권물류거점예약Request request,
        CancellationToken cancellationToken)
    {
        ValidateReservation(request);
        var previous = await db.생활권물류거점용량예약.AsNoTracking()
            .SingleOrDefaultAsync(x => x.거점Id == id && x.멱등성Key == request.멱등성Key, cancellationToken);
        if (previous is not null)
            return ToReservationResponse(previous, true);

        var entity = await RequireHubAsync(id, cancellationToken);
        var reservedWeight = await db.생활권물류거점용량예약
            .Where(x => x.거점Id == id && x.상태Code == 생활권물류거점예약상태Codes.Reserved)
            .SumAsync(x => x.중량Kg, cancellationToken);
        if (!생활권물류거점Policy.CanReserve(entity, request.중량Kg, request.보관시간분)
            || reservedWeight + request.중량Kg > entity.최대총중량Kg)
            throw new InvalidOperationException("거점 상태 또는 남은 용량이 요청 조건을 충족하지 않습니다.");

        var now = DateTime.UtcNow;
        var reservation = new 생활권물류거점용량예약
        {
            거점Id = id,
            업무StableId = request.업무StableId.Trim(),
            멱등성Key = request.멱등성Key.Trim(),
            중량Kg = request.중량Kg,
            보관시간분 = request.보관시간분,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        db.생활권물류거점용량예약.Add(reservation);
        entity.현재예약건수++;
        entity.UpdatedAtUtc = now;
        entity.Revision++;
        await SaveAsync(cancellationToken);
        return ToReservationResponse(reservation, false);
    }

    public async Task<생활권물류거점완료Response> 인계완료Async(
        Guid id,
        Guid reservationId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey)) throw new InvalidOperationException("완료 멱등성 키가 필요합니다.");
        var entity = await RequireHubAsync(id, cancellationToken);
        var reservation = await db.생활권물류거점용량예약
            .SingleOrDefaultAsync(x => x.Id == reservationId && x.거점Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("거점 용량 예약을 찾을 수 없습니다.");
        var reward = await db.생활권물류거점보상기록.AsNoTracking()
            .SingleOrDefaultAsync(x => x.거점Id == id && x.멱등성Key == idempotencyKey, cancellationToken);
        if (reward is not null)
            return new 생활권물류거점완료Response { 예약Id = reservation.Id, 상태Code = reservation.상태Code, 모의보상금액 = reward.금액, 실제지급대상 = reward.실제지급대상 };
        if (reservation.상태Code != 생활권물류거점예약상태Codes.Reserved)
            throw new InvalidOperationException("예약 상태에서만 인계를 완료할 수 있습니다.");

        reservation.상태Code = 생활권물류거점예약상태Codes.Completed;
        reservation.UpdatedAtUtc = DateTime.UtcNow;
        entity.현재예약건수 = Math.Max(0, entity.현재예약건수 - 1);
        entity.UpdatedAtUtc = reservation.UpdatedAtUtc;
        entity.Revision++;
        reward = new 생활권물류거점보상기록
        {
            거점Id = id,
            업무StableId = reservation.업무StableId,
            멱등성Key = idempotencyKey.Trim(),
            금액 = entity.완료건당고정보상,
            통화Code = entity.보상통화Code,
            실제지급대상 = false,
            실행모드Code = executionMode.Mode.ToString(),
            CreatedAtUtc = DateTime.UtcNow
        };
        db.생활권물류거점보상기록.Add(reward);
        await SaveAsync(cancellationToken);
        return new 생활권물류거점완료Response { 예약Id = reservation.Id, 상태Code = reservation.상태Code, 모의보상금액 = reward.금액, 실제지급대상 = false };
    }

    private async Task<생활권물류거점Response> ChangeConsentAsync(
        Guid id,
        생활권물류거점동의Request request,
        bool owner,
        CancellationToken cancellationToken)
    {
        var userId = RequireUser();
        var entity = await RequireHubAsync(id, cancellationToken);
        EnsureRevision(entity, request.ExpectedRevision);
        if (owner && entity.신청자UserId != userId) throw new UnauthorizedAccessException("신청자만 소유자 동의를 변경할 수 있습니다.");
        if (!owner && entity.관리담당자UserId != userId) throw new UnauthorizedAccessException("지정된 관리담당자만 관리주체 동의를 변경할 수 있습니다.");
        var now = DateTime.UtcNow;
        if (owner)
        {
            entity.소유자동의 = request.동의;
            entity.소유자동의시각Utc = request.동의 ? now : entity.소유자동의시각Utc;
            entity.소유자동의철회시각Utc = request.동의 ? null : now;
        }
        else
        {
            entity.관리주체동의 = request.동의;
            entity.관리주체동의시각Utc = request.동의 ? now : entity.관리주체동의시각Utc;
            entity.관리주체동의철회시각Utc = request.동의 ? null : now;
        }

        if (!request.동의 && entity.상태Code is 생활권물류거점상태Codes.Pilot or 생활권물류거점상태Codes.Active)
        {
            entity.상태Code = 생활권물류거점상태Codes.Paused;
            entity.상태사유 = "ConsentWithdrawn";
            entity.StatusChangedAtUtc = now;
        }
        entity.UpdatedAtUtc = now;
        entity.Revision++;
        await SaveAsync(cancellationToken);
        return ToResponse(entity);
    }

    private async Task<생활권물류거점> RequireHubAsync(Guid id, CancellationToken cancellationToken)
        => await db.생활권물류거점.SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
           ?? throw new KeyNotFoundException("생활권 물류 거점을 찾을 수 없습니다.");

    private async Task RequireApplicationLedgerAsync(
        string ledgerId,
        string applicantUserId,
        CancellationToken cancellationToken)
    {
        var ledger = await communityLedgerStore.원장조회Async(ledgerId.Trim(), cancellationToken)
            ?? throw new InvalidOperationException("연결할 Mongo 지도 신청 가원장을 찾을 수 없습니다.");
        if (!string.Equals(ledger.원장템플릿Key, CommunityLedgerTemplateKeys.WarehouseInbound, StringComparison.Ordinal))
            throw new InvalidOperationException("생활권 물류 거점은 물류대행 지도 신청 가원장과만 연결할 수 있습니다.");

        var applicantCanUseLedger = string.Equals(ledger.생성자UserId, applicantUserId, StringComparison.Ordinal)
            || ledger.참여자목록.Any(participant => string.Equals(participant.UserId, applicantUserId, StringComparison.Ordinal));
        if (!applicantCanUseLedger)
            throw new UnauthorizedAccessException("신청자가 참여한 지도 신청 가원장만 연결할 수 있습니다.");
    }

    private string RequireUser()
        => string.IsNullOrWhiteSpace(currentUser.UserId)
            ? throw new UnauthorizedAccessException("인증된 사용자가 필요합니다.")
            : currentUser.UserId.Trim();

    private static void EnsureRevision(생활권물류거점 entity, long expected)
    {
        if (entity.Revision != expected) throw new 생활권물류거점ConcurrencyException($"거점 정보가 변경되었습니다. 현재 revision은 {entity.Revision}입니다.");
    }

    private static void ValidateApplication(생활권물류거점신청Request request)
    {
        if (string.IsNullOrWhiteSpace(request.신청가원장Id) || request.신청가원장Id.Length > 100) throw new InvalidOperationException("Mongo 지도 신청 가원장 ID가 필요합니다.");
        if (string.IsNullOrWhiteSpace(request.공간StableId) || request.공간StableId.Length > 160) throw new InvalidOperationException("공간 고유 식별자가 필요합니다.");
        if (string.IsNullOrWhiteSpace(request.관리담당자UserId) || request.관리담당자UserId.Length > 450) throw new InvalidOperationException("관리담당자가 필요합니다.");
        if (string.IsNullOrWhiteSpace(request.생활권Key) || request.생활권Key.Length > 120) throw new InvalidOperationException("생활권 키가 필요합니다.");
        if (string.IsNullOrWhiteSpace(request.대략위치Label) || request.대략위치Label.Length > 160) throw new InvalidOperationException("공개 가능한 대략 위치가 필요합니다.");
        if (string.IsNullOrWhiteSpace(request.정확위치보호참조) || request.정확위치보호참조.Length > 200) throw new InvalidOperationException("정확 위치의 보호된 참조가 필요합니다.");
        if (request.최대동시보관건수 is <= 0 or > 1000 || request.최대총중량Kg is <= 0 or > 100000 || request.최대보관시간분 is <= 0 or > 10080) throw new InvalidOperationException("보관 용량 또는 최대 보관시간이 올바르지 않습니다.");
        if (request.완료건당고정보상 is < 0 or > 1000000) throw new InvalidOperationException("완료 건당 고정 보상 범위가 올바르지 않습니다.");
    }

    private static void ValidateReservation(생활권물류거점예약Request request)
    {
        if (string.IsNullOrWhiteSpace(request.업무StableId) || request.업무StableId.Length > 160) throw new InvalidOperationException("업무 고유 식별자가 필요합니다.");
        if (string.IsNullOrWhiteSpace(request.멱등성Key) || request.멱등성Key.Length > 100) throw new InvalidOperationException("예약 멱등성 키가 필요합니다.");
        if (request.중량Kg <= 0 || request.보관시간분 <= 0) throw new InvalidOperationException("예약 중량과 보관시간은 0보다 커야 합니다.");
    }

    private static string NormalizeReason(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim()[..Math.Min(value.Trim().Length, 500)];

    private static 생활권물류거점Response ToResponse(생활권물류거점 x) => new()
    {
        Id = x.Id, StableId = x.StableId, 신청가원장Id = x.신청가원장Id, 공간StableId = x.공간StableId,
        생활권Key = x.생활권Key, 대략위치Label = x.대략위치Label, 상태Code = x.상태Code,
        소유자동의 = x.소유자동의, 관리주체동의 = x.관리주체동의, 플랫폼승인 = x.플랫폼승인,
        현장확인 = x.현장확인시각Utc.HasValue, 최대동시보관건수 = x.최대동시보관건수,
        현재예약건수 = x.현재예약건수, 최대총중량Kg = x.최대총중량Kg, 최대보관시간분 = x.최대보관시간분,
        입고가능시간창 = x.입고가능시간창, 수령가능시간창 = x.수령가능시간창,
        완료건당고정보상 = x.완료건당고정보상, 보상통화Code = x.보상통화Code,
        연결창고Id = x.연결창고Id, 실운영허용 = x.실운영허용, Revision = x.Revision, 상태사유 = x.상태사유
    };

    private static 생활권물류거점예약Response ToReservationResponse(생활권물류거점용량예약 x, bool reused)
        => new() { 예약Id = x.Id, 업무StableId = x.업무StableId, 상태Code = x.상태Code, 기존예약재사용 = reused };

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException ex) { throw new 생활권물류거점ConcurrencyException("거점 정보가 다른 요청에서 먼저 변경되었습니다.", ex); }
    }
}

public sealed class 생활권물류거점ConcurrencyException : Exception
{
    public 생활권물류거점ConcurrencyException(string message, Exception? inner = null) : base(message, inner) { }
}
