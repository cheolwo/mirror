using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Hongdal.Contracts.Common.Versioning;
using 홍달.Services.Dispatch.Engine;
using 홍달.도메인.공통;
using 홍달.도메인.운송;

namespace 홍달.Services.Dispatch.Queue;

public sealed record 배차엔진판단감사Context(
    string CorrelationId,
    string OperatingSystemId,
    string EngineFamilyId,
    string EngineImplementationId,
    [property: System.Text.Json.Serialization.JsonIgnore]
    string? SensitiveDriverId = null)
{
    public static 배차엔진판단감사Context 생성(
        운송원장 queue,
        I운송의뢰배차엔진 engine,
        string? sensitiveDriverId = null)
    {
        ArgumentNullException.ThrowIfNull(queue);
        ArgumentNullException.ThrowIfNull(engine);

        return 생성(queue, engine.논리엔진코드, engine.엔진코드, sensitiveDriverId);
    }

    public static 배차엔진판단감사Context 생성(
        운송원장 queue,
        string engineFamilyId,
        string engineImplementationId,
        string? sensitiveDriverId = null)
    {
        ArgumentNullException.ThrowIfNull(queue);
        ArgumentException.ThrowIfNullOrWhiteSpace(engineFamilyId);
        ArgumentException.ThrowIfNullOrWhiteSpace(engineImplementationId);

        var traceId = Activity.Current?.TraceId.ToString();
        return new 배차엔진판단감사Context(
            string.IsNullOrWhiteSpace(traceId) ? Guid.NewGuid().ToString("N") : traceId,
            운영체제식별자결정(queue),
            engineFamilyId,
            engineImplementationId,
            sensitiveDriverId);
    }

    private static string 운영체제식별자결정(운송원장 queue)
    {
        if (운송의뢰배차원천유형.Is홍달마트음식주문(queue.원본의뢰유형)
            || string.Equals(
                queue.원본의뢰유형,
                운송의뢰배차원천유형.홍달마트출고,
                StringComparison.OrdinalIgnoreCase))
        {
            return OperatingSystemIds.HongdalMartUrbanLogistics;
        }

        if (queue.배차업무유형 == 상태값.배차업무유형.음식배달
            || 운송의뢰배차원천유형.Is음식배달운송(queue.원본의뢰유형))
        {
            return OperatingSystemIds.FoodDelivery;
        }

        if (운송의뢰배차원천유형.Is수입통관연계운송(queue.원본의뢰유형))
        {
            return OperatingSystemIds.GroupPurchaseImport;
        }

        if (운송의뢰배차원천유형.Is창고출고연계운송(queue.원본의뢰유형)
            && !string.Equals(
                queue.원본의뢰유형,
                운송의뢰배차원천유형.화주운송의뢰,
                StringComparison.OrdinalIgnoreCase)
            && !string.Equals(
                queue.원본의뢰유형,
                운송의뢰배차원천유형.주문자화물주문,
                StringComparison.OrdinalIgnoreCase))
        {
            return OperatingSystemIds.WarehouseCommerceFulfillment;
        }

        return OperatingSystemIds.DomesticCargoTransport;
    }
}

public static class 배차엔진감사식별자
{
    public const string 미등록구현 = "Unregistered";
}

public static class 배차엔진후속전환
{
    public const string 추천시작 = "RecommendationStarted";
    public const string 공개배차전환 = "PublicDispatchStarted";
    public const string 보류 = "Held";
    public const string 전환없음 = "NoTransition";
}

internal static class 배차엔진판단감사이벤트Factory
{
    private const int 최대사유길이 = 500;

    public static 운송이벤트 생성(
        운송원장 queue,
        배차추천후보선정결과 selection,
        string followUpTransition,
        string transitionResultCode,
        DateTime occurredAtUtc)
    {
        ArgumentNullException.ThrowIfNull(queue);
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(selection.감사Context);
        ArgumentException.ThrowIfNullOrWhiteSpace(selection.감사Context.CorrelationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(selection.감사Context.OperatingSystemId);
        ArgumentException.ThrowIfNullOrWhiteSpace(selection.감사Context.EngineFamilyId);
        ArgumentException.ThrowIfNullOrWhiteSpace(selection.감사Context.EngineImplementationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(followUpTransition);
        ArgumentException.ThrowIfNullOrWhiteSpace(transitionResultCode);

        if (selection.감사Context.CorrelationId.Length > 120)
        {
            throw new ArgumentException("배차 판단 CorrelationId는 120자를 초과할 수 없습니다.", nameof(selection));
        }

        if (!OperatingSystemIds.TryNormalize(
                selection.감사Context.OperatingSystemId,
                out var canonicalOperatingSystemId))
        {
            throw new ArgumentException("등록되지 않은 운영체제 식별자는 감사 이벤트에 기록할 수 없습니다.", nameof(selection));
        }

        var sensitiveValues = new[]
        {
            selection.후보?.DriverId,
            selection.감사Context.SensitiveDriverId,
            queue.현재추천대상기사Id,
            queue.마지막거절기사Id,
            queue.확정기사Id
        };
        var metadata = new 배차엔진판단감사Metadata(
            SchemaVersion: 1,
            selection.감사Context.CorrelationId,
            canonicalOperatingSystemId,
            selection.감사Context.EngineFamilyId,
            selection.감사Context.EngineImplementationId,
            ResultStatus: selection.상태값.ToString(),
            CandidateScore: selection.후보?.추천점수,
            CandidateReason: 개인정보제거(selection.사유, sensitiveValues),
            FollowUpTransition: followUpTransition,
            TransitionResultCode: transitionResultCode);

        return new 운송이벤트
        {
            의뢰Id = queue.의뢰Id,
            이벤트타입 = 운송이벤트유형.배차엔진판단감사,
            이벤트시각 = occurredAtUtc,
            메타데이터 = JsonSerializer.Serialize(metadata)
        };
    }

    private static string 개인정보제거(string? reason, IEnumerable<string?> sensitiveValues)
    {
        var sanitized = string.IsNullOrWhiteSpace(reason)
            ? "후보 선정 사유 없음"
            : reason.Trim();

        foreach (var sensitiveValue in sensitiveValues
                     .Where(value => !string.IsNullOrWhiteSpace(value))
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            sanitized = sanitized.Replace(
                sensitiveValue!,
                "[REDACTED]",
                StringComparison.OrdinalIgnoreCase);
        }

        sanitized = Regex.Replace(
            sanitized,
            @"\b[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}\b",
            "[REDACTED]",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        sanitized = Regex.Replace(
            sanitized,
            @"(?<!\d)(?:01[016789]|0\d{1,2})[- ]?\d{3,4}[- ]?\d{4}(?!\d)",
            "[REDACTED]",
            RegexOptions.CultureInvariant);

        return sanitized.Length <= 최대사유길이
            ? sanitized
            : sanitized[..최대사유길이];
    }

    private sealed record 배차엔진판단감사Metadata(
        int SchemaVersion,
        string CorrelationId,
        string OperatingSystemId,
        string EngineFamilyId,
        string EngineImplementationId,
        string ResultStatus,
        decimal? CandidateScore,
        string CandidateReason,
        string FollowUpTransition,
        string TransitionResultCode);
}
