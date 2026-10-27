using Ssalddel.Contracts.Common.Warehouse;
using 살뜰.Services.Dispatch.Engine;
using 살뜰.도메인.운송;

namespace 살뜰.Services.Dispatch.Queue;

/// <summary>기존 큐의 보충·덮어쓰기·판본 시각 의미를 유지한다. DB 저장이나 실행 인덱스 갱신은 하지 않는다.</summary>
internal sealed class 운송의뢰배차메타데이터Updater(I운송의뢰배차원천분류Service sourceClassifier)
{
    internal void ApplyExistingMetadata(
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
            var source = sourceClassifier.분류(existing);
            existing.배차업무유형 = source.배차업무유형;
            changed = true;
        }

        if (changed)
        {
            existing.UpdatedAt = DateTime.UtcNow;
        }
    }

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

    internal static string ResolveSourceType(운송의뢰배차대기생성옵션? options, 출고예정운송대상 target)
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

    internal static string NormalizeRequired(string? primary, string? secondary, string? fallback, string errorMessage)
    {
        var value = NormalizeOptional(primary) ?? NormalizeOptional(secondary) ?? NormalizeOptional(fallback);
        return value ?? throw new InvalidOperationException(errorMessage);
    }

    internal static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
