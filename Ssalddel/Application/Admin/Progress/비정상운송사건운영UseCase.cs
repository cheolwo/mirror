using Microsoft.EntityFrameworkCore;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Contracts.Admin.Progress;
using Ssalddel.Services.Operations;
using 살뜰.Data;
using 살뜰.도메인.운송;
using AdminIncidentReviewRequest = Ssalddel.Contracts.Admin.Progress.비정상운송사건검토요청;

namespace Ssalddel.Application.Admin.Progress;

public interface I비정상운송사건운영UseCase
{
    Task<IReadOnlyList<비정상운송사건운영응답>> 목록조회Async(
        string? 상태Code,
        CancellationToken cancellationToken = default);

    Task<비정상운송사건운영응답?> 검토Async(
        string 사건StableId,
        AdminIncidentReviewRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class 비정상운송사건운영UseCase(
    SsalddelContext db,
    I비정상운송사건Service incidentService,
    ICurrentUserAccessor currentUser) : I비정상운송사건운영UseCase
{
    public async Task<IReadOnlyList<비정상운송사건운영응답>> 목록조회Async(
        string? 상태Code,
        CancellationToken cancellationToken = default)
    {
        var query = db.비정상운송사건.AsNoTracking();
        var status = 상태Code?.Trim();
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(x => x.상태Code == status);

        return await query
            .OrderByDescending(x => x.최근신고시각Utc)
            .ThenBy(x => x.사건StableId)
            .Select(x => ToResponse(x, false))
            .ToListAsync(cancellationToken);
    }

    public async Task<비정상운송사건운영응답?> 검토Async(
        string 사건StableId,
        AdminIncidentReviewRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var reviewerId = currentUser.UserId?.Trim();
        if (string.IsNullOrWhiteSpace(reviewerId))
            throw new InvalidOperationException("검토 운영자 식별자를 확인할 수 없습니다.");

        var result = await incidentService.검토Async(
            new Ssalddel.Services.Operations.비정상운송사건검토요청(
                사건StableId,
                request.클라이언트요청Id,
                request.예상Revision,
                request.결정Code,
                request.정상확인수량,
                request.영향수량,
                request.보험적용가능성검토요청,
                request.검토사유,
                reviewerId),
            cancellationToken);
        if (!result.찾음 || result.사건 is null)
            return null;

        await db.SaveChangesAsync(cancellationToken);
        return ToResponse(result.사건, result.멱등재시도);
    }

    private static 비정상운송사건운영응답 ToResponse(
        비정상운송사건 entity,
        bool idempotentReplay)
        => new()
        {
            사건StableId = entity.사건StableId,
            운송Id = entity.운송Id,
            운송의뢰Id = entity.운송의뢰Id,
            사건유형Code = entity.사건유형Code,
            원본예외Code = entity.원본예외Code,
            단계Code = entity.단계Code,
            상태Code = entity.상태Code,
            현재담당Code = entity.현재담당Code,
            정산보류적용여부 = entity.정산보류적용여부,
            전체수량 = entity.전체수량,
            정상확인수량 = entity.정상확인수량,
            영향수량 = entity.영향수량,
            업무통제상태Code = entity.업무통제상태Code,
            보류범위Code = entity.보류범위Code,
            현장진행불가 = entity.현장진행불가,
            보험검토상태Code = entity.보험검토상태Code,
            해결결과Code = entity.해결결과Code,
            증빙참조있음 = entity.증빙참조있음,
            최초신고시각Utc = entity.최초신고시각Utc,
            최근신고시각Utc = entity.최근신고시각Utc,
            최근검토시각Utc = entity.최근검토시각Utc,
            Revision = entity.Revision,
            멱등재시도여부 = idempotentReplay
        };
}
