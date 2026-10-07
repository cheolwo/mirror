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
    private readonly I운송원장배달권연결Service _운송원장배달권연결Service;
    private readonly I음식배달권실행공간Store _음식배달권실행공간Store;
    private readonly I국내화물배달권실행공간Store _국내화물배달권실행공간Store;

    public 운송의뢰배차대기Service(
        SsalddelContext db,
        I운송의뢰배차원천분류Service sourceClassifier,
        I운송원장배달권연결Service 운송원장배달권연결Service,
        I음식배달권실행공간Store 음식배달권실행공간Store,
        I국내화물배달권실행공간Store 국내화물배달권실행공간Store)
    {
        _db = db;
        _sourceClassifier = sourceClassifier;
        _운송원장배달권연결Service = 운송원장배달권연결Service;
        _음식배달권실행공간Store = 음식배달권실행공간Store;
        _국내화물배달권실행공간Store = 국내화물배달권실행공간Store;
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
        if (existing is not null)
        {
            ApplyExistingMetadata(existing, target, options, requestId, sourceType, sourceId, shipperId);
            await Upsert배달권실행공간Async(existing, cancellationToken);
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
        await Upsert배달권실행공간Async(entity, cancellationToken);
        return entity;
    }

    private void ApplyExistingMetadata(
        운송원장 existing,
        출고예정운송대상 target,
        운송의뢰배차대기생성옵션? options,
        string requestId,
        string sourceType,
        string? sourceId,
        string? shipperId)
    {
        var changed = false;

        changed |= SetIfEmptyOrDifferent(existing.운송번호, requestId, value => existing.운송번호 = value);
        changed |= SetIfEmptyOrDifferent(existing.의뢰Id, requestId, value => existing.의뢰Id = value);
        changed |= SetIfEmptyOrDifferent(existing.원본의뢰유형, sourceType, value => existing.원본의뢰유형 = value);
        changed |= SetIfEmptyOrDifferent(existing.원본의뢰Id, sourceId, value => existing.원본의뢰Id = value);
        changed |= SetIfEmptyOrDifferent(existing.화주Id, shipperId, value => existing.화주Id = value);

        changed |= SetIfEmpty(existing.픽업_도로명주소, target.상차주소, value => existing.픽업_도로명주소 = value);
        changed |= SetIfEmpty(existing.픽업_상세주소, options?.픽업상세주소, value => existing.픽업_상세주소 = value);
        changed |= SetIfNull(existing.픽업_위도, target.상차위도, value => existing.픽업_위도 = value);
        changed |= SetIfNull(existing.픽업_경도, target.상차경도, value => existing.픽업_경도 = value);
        changed |= SetIfEmpty(existing.하차_도로명주소, target.하차주소, value => existing.하차_도로명주소 = value);
        changed |= SetIfEmpty(existing.하차_상세주소, options?.하차상세주소, value => existing.하차_상세주소 = value);
        changed |= SetIfNull(existing.하차_위도, target.하차위도, value => existing.하차_위도 = value);
        changed |= SetIfNull(existing.하차_경도, target.하차경도, value => existing.하차_경도 = value);

        if (options?.배차업무유형 is int dispatchWorkType && existing.배차업무유형 != dispatchWorkType)
        {
            existing.배차업무유형 = dispatchWorkType;
            changed = true;
        }
        else if (existing.배차업무유형 <= 0)
        {
            var source = _sourceClassifier.분류(existing);
            existing.배차업무유형 = source.배차업무유형;
            changed = true;
        }

        if (changed)
        {
            existing.UpdatedAt = DateTime.UtcNow;
        }
    }

    private async Task Upsert배달권실행공간Async(운송원장 배차대기, CancellationToken cancellationToken)
    {
        var 배달권연결 = await _운송원장배달권연결Service.투영추적Async(
            배차대기,
            cancellationToken);

        if (배차대기.상태 != 상태값.배차대기상태.대기
            || 배차대기.배차큐단계 is 상태값.배차큐단계.확정 or 상태값.배차큐단계.종료)
        {
            await Remove모든실행공간Async(배차대기.의뢰Id, cancellationToken);
            return;
        }

        var 음식배달 = 배차대기.배차업무유형 == 상태값.배차업무유형.음식배달;
        var 실행배달권 = 음식배달
            ? 음식배달권정책.판정(
                CreatePoint(배차대기.픽업_위도, 배차대기.픽업_경도),
                배차대기.픽업_도로명주소)
            : 배달권연결.픽업배달권;
        if (string.Equals(실행배달권.배달권키, "unknown", StringComparison.Ordinal))
        {
            await Remove모든실행공간Async(배차대기.의뢰Id, cancellationToken);
            return;
        }

        if (음식배달)
        {
            await _국내화물배달권실행공간Store.Remove운송의뢰Async(
                배차대기.의뢰Id,
                cancellationToken);
            await _음식배달권실행공간Store.Upsert운송의뢰Async(
                실행배달권.배달권키,
                배차대기.의뢰Id,
                음식배달권정책.인접배달권키조회(실행배달권.배달권키),
                cancellationToken);
        }
        else
        {
            await _음식배달권실행공간Store.Remove운송의뢰Async(
                배차대기.의뢰Id,
                cancellationToken);
            await _국내화물배달권실행공간Store.Upsert운송의뢰Async(
                실행배달권.배달권키,
                배차대기.의뢰Id,
                국내행정구역배달권Catalog.인접배달권키조회(실행배달권.배달권키),
                cancellationToken);
        }
    }

    private async Task Remove모든실행공간Async(string 의뢰Id, CancellationToken cancellationToken)
    {
        await _음식배달권실행공간Store.Remove운송의뢰Async(의뢰Id, cancellationToken);
        await _국내화물배달권실행공간Store.Remove운송의뢰Async(의뢰Id, cancellationToken);
    }

    private static 배차경로좌표? CreatePoint(decimal? latitude, decimal? longitude)
        => latitude.HasValue && longitude.HasValue
            ? new 배차경로좌표(latitude.Value, longitude.Value)
            : null;

    private static string To배차원천유형(string? sourceType)
    {
        return sourceType switch
        {
            출고예정운송대상원천유형.화주운송의뢰 => 운송의뢰배차원천유형.화주운송의뢰,
            출고예정운송대상원천유형.창고출고예정 => 운송의뢰배차원천유형.창고출고연계운송,
            출고예정운송대상원천유형.판매채널주문 => 운송의뢰배차원천유형.판매채널출고,
            출고예정운송대상원천유형.공동주문수입 => 운송의뢰배차원천유형.공동주문국내운송,
            _ when string.IsNullOrWhiteSpace(sourceType) => 운송의뢰배차원천유형.화주운송의뢰,
            _ => sourceType.Trim()
        };
    }

    private static string ResolveSourceType(운송의뢰배차대기생성옵션? options, 출고예정운송대상 target)
        => NormalizeOptional(options?.원본의뢰유형)
           ?? To배차원천유형(target.원천유형);

    private static bool SetIfEmptyOrDifferent(string? current, string? value, Action<string> update)
    {
        var normalized = NormalizeOptional(value);
        if (normalized is null || string.Equals(current, normalized, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        update(normalized);
        return true;
    }

    private static bool SetIfEmpty(string? current, string? value, Action<string> update)
    {
        var normalized = NormalizeOptional(value);
        if (normalized is null || !string.IsNullOrWhiteSpace(current))
        {
            return false;
        }

        update(normalized);
        return true;
    }

    private static bool SetIfNull(decimal? current, decimal? value, Action<decimal> update)
    {
        if (current.HasValue || !value.HasValue)
        {
            return false;
        }

        update(value.Value);
        return true;
    }

    private static string NormalizeRequired(string? primary, string? secondary, string? fallback, string errorMessage)
    {
        var value = NormalizeOptional(primary) ?? NormalizeOptional(secondary) ?? NormalizeOptional(fallback);
        return value ?? throw new InvalidOperationException(errorMessage);
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
