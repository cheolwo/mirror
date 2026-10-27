using static 살뜰.Services.Dispatch.Queue.운송의뢰배차메타데이터Updater;
using Ssalddel.Contracts.Common.Warehouse;
using Microsoft.EntityFrameworkCore;
using 살뜰.도메인.공통;
using 살뜰.도메인.배차;
using 살뜰.Services.DeliveryZones;
using 살뜰.Services.Dispatch.Coordination;
using 살뜰.Services.Dispatch.Engine;

namespace 살뜰.Services.Dispatch.Queue;

public sealed class 운송의뢰배차대기생성옵션
{
    public string? 의뢰Id { get; init; }

    public string? 화주Id { get; init; }

    public int? 배차업무유형 { get; init; }

    public string? 원본의뢰유형 { get; init; }

    public string? 원본의뢰Id { get; init; }

    public string? 픽업상세주소 { get; init; }

    public string? 하차상세주소 { get; init; }

    public string? 상태 { get; init; }

    public string? 공동구매도착지유형코드 { get; init; }

    public bool? 공동구매기사세대배송여부 { get; init; }

    public string? 공동구매세대배송방식코드 { get; init; }

    public int? 공동구매세대배송건수 { get; init; }

    public string? 공동구매분배책임코드 { get; init; }
}

public interface I운송의뢰배차대기Service
{
    Task<운송원장> 생성또는조회Async(
        출고예정운송대상 target,
        운송의뢰배차대기생성옵션? options = null,
        CancellationToken cancellationToken = default);
}

public sealed class 운송의뢰배차대기Service : I운송의뢰배차대기Service
{
    private readonly SsalddelContext _db;
    private readonly I운송의뢰배차원천분류Service _sourceClassifier;
    private readonly 운송의뢰배차메타데이터Updater _metadataUpdater;
    private readonly 배차대기실행공간Projector _executionProjector;

    public 운송의뢰배차대기Service(
        SsalddelContext db,
        I운송의뢰배차원천분류Service sourceClassifier,
        I운송원장배달권연결Service 운송원장배달권연결Service,
        I음식배달권실행공간Store 음식배달권실행공간Store,
        I국내화물배달권실행공간Store 국내화물배달권실행공간Store)
    {
        _db = db;
        _sourceClassifier = sourceClassifier;
        _metadataUpdater = new(sourceClassifier);
        _executionProjector = new(운송원장배달권연결Service, 음식배달권실행공간Store, 국내화물배달권실행공간Store);
    }

    public async Task<운송원장> 생성또는조회Async(
        출고예정운송대상 target,
        운송의뢰배차대기생성옵션? options = null,
        CancellationToken cancellationToken = default)
    {
        var requestId = NormalizeRequired(options?.의뢰Id, target.운송의뢰Id, target.원천참조번호, "운송 의뢰 ID가 필요합니다.");
        var sourceType = ResolveSourceType(options, target);
        var sourceId = NormalizeOptional(options?.원본의뢰Id) ?? NormalizeOptional(target.원천참조번호);
        var shipperId = NormalizeOptional(options?.화주Id) ?? NormalizeOptional(target.판매자UserId);
        var existing = await _db.운송원장.SingleOrDefaultAsync(x => x.의뢰Id == requestId, cancellationToken);
        마트원천결속검증(existing, requestId, sourceType, sourceId ?? requestId, shipperId, options?.배차업무유형);
        if (existing is not null)
        {
            _metadataUpdater.ApplyExistingMetadata(existing, target, options, requestId, sourceType, sourceId, shipperId);
            await _executionProjector.UpsertAsync(existing, cancellationToken);
            return existing;
        }

        var now = DateTime.UtcNow;

        var entity = new 운송원장
        {
            운송번호 = requestId,
            의뢰Id = requestId,
            화주Id = shipperId ?? throw new InvalidOperationException("배차대기 화주 ID가 필요합니다."),
            원본의뢰유형 = sourceType,
            원본의뢰Id = sourceId ?? requestId,
            공동구매도착지유형코드 = NormalizeOptional(options?.공동구매도착지유형코드),
            공동구매기사세대배송여부 = options?.공동구매기사세대배송여부,
            공동구매세대배송방식코드 = NormalizeOptional(options?.공동구매세대배송방식코드),
            공동구매세대배송건수 = options?.공동구매세대배송건수 is > 0 ? options.공동구매세대배송건수 : null,
            공동구매분배책임코드 = NormalizeOptional(options?.공동구매분배책임코드),
            픽업_도로명주소 = target.상차주소,
            픽업_상세주소 = NormalizeOptional(options?.픽업상세주소) ?? string.Empty,
            픽업_위도 = target.상차위도,
            픽업_경도 = target.상차경도,
            하차_도로명주소 = target.하차주소,
            하차_상세주소 = NormalizeOptional(options?.하차상세주소) ?? string.Empty,
            하차_위도 = target.하차위도,
            하차_경도 = target.하차경도,
            상태 = NormalizeOptional(options?.상태) ?? 상태값.배차대기상태.대기,
            배차큐단계 = 상태값.배차큐단계.계획배차,
            배차노출상태 = 상태값.배차노출상태.계획대기,
            CreatedAt = now,
            UpdatedAt = now
        };

        var sourceMemo = await _db.화주운송의뢰.AsNoTracking().Where(x => x.의뢰Id == requestId)
            .Select(x => x.정산메모).SingleOrDefaultAsync(cancellationToken);
        Ssalddel.Services.Community.생활배송배차Policy.큐초기화(entity, sourceMemo);
        var source = _sourceClassifier.분류(entity);
        entity.배차업무유형 = options?.배차업무유형 ?? source.배차업무유형;

        _db.운송원장.Add(entity);
        await _executionProjector.UpsertAsync(entity, cancellationToken);
        return entity;
    }

    // 마트 원천은 음식 배차 큐를 쓰더라도 음식주문으로 바꾸거나 다른 화주의 의뢰를 재사용하지 않는다.
    // 확정 기사 조기 반환도 같은 검증을 거쳐야 하므로 마트 인계 Service와 공유한다.
    internal static void 마트원천결속검증(
        운송원장? existing,
        string requestId,
        string sourceType,
        string sourceId,
        string? shipperId,
        int? workType)
    {
        var incomingMart = string.Equals(sourceType, 운송의뢰배차원천유형.살뜰마트포장완료주문, StringComparison.OrdinalIgnoreCase);
        var existingMart = string.Equals(existing?.원본의뢰유형, 운송의뢰배차원천유형.살뜰마트포장완료주문, StringComparison.OrdinalIgnoreCase);
        if (!incomingMart && !existingMart)
            return;

        if (!incomingMart || workType is { } requestedType && requestedType != 상태값.배차업무유형.음식배달
            || string.IsNullOrWhiteSpace(shipperId)
            || existing is not null && (!existingMart
                || !string.Equals(existing.의뢰Id, requestId, StringComparison.Ordinal)
                || !string.Equals(existing.원본의뢰Id, sourceId, StringComparison.Ordinal)
                || !string.Equals(existing.화주Id, shipperId, StringComparison.Ordinal)
                || existing.배차업무유형 != 상태값.배차업무유형.음식배달))
            throw new InvalidOperationException("마트 배차 원천 결속이 일치하지 않아 기존 업무를 변경하지 않았습니다.");
    }

}
