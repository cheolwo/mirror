using System.Text.Json.Serialization;
using Ssalddel.Contracts.Common.Versioning;

namespace Ssalddel.Contracts.Common.Workflow;

/// <summary>현재 업무 단계의 실패 맥락입니다. 생명주기 표시 순서가 실행·복구 권한을 부여하지는 않습니다.</summary>
public sealed record 업무실패맥락Dto(
    [property: JsonPropertyName("operatingSystemId")] string OperatingSystemId,
    [property: JsonPropertyName("stageId")] string StageId,
    [property: JsonPropertyName("outcomeCode")] string OutcomeCode)
{
    /// <summary>알려진 OS와 해당 OS의 단계, 명시된 결과만 허용합니다.</summary>
    public bool 유효한가()
        => OperatingSystemLifecycleCatalog.TryGet(OperatingSystemId, out var lifecycle)
           && lifecycle.Stages.Any(stage => string.Equals(stage.StageId, StageId, StringComparison.Ordinal))
           && 업무처리결과Codes.알려진결과인가(OutcomeCode);
}

public static class 업무처리결과Codes
{
    public const string 조회 = "NotApplicable";
    public const string 미적용 = "NotApplied";
    public const string 적용 = "Applied";
    public const string 미확정 = "Unknown";

    public static bool 알려진결과인가(string? value)
        => value is 조회 or 미적용 or 적용 or 미확정;
}
