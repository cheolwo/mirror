namespace Ssalddel.Contracts.Common.Workflow;

public static class 업무Revision종류Codes
{
    public const string 없음 = "None";
    public const string 음식주문 = "FoodOrder";
    public const string 음식배달시도 = "FoodDeliveryAttempt";
}

/// <summary>
/// 현재 역할별 상태 사본에서 요청할 수 있는 작은 행동 힌트입니다.
/// 실제 권한과 상태 전이는 각 Command가 다시 검증합니다.
/// </summary>
public sealed class 업무가능행동Dto
{
    public string ActionId { get; set; } = string.Empty;
    public string RevisionKindCode { get; set; } = 업무Revision종류Codes.없음;
    public long? ExpectedRevision { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
    public IReadOnlyList<string> EnvironmentRequirementCodes { get; set; } = [];
}

public static class 업무가능행동목록
{
    public static bool 포함(IEnumerable<업무가능행동Dto>? actions, string actionId)
        => actions?.Any(action => string.Equals(action.ActionId, actionId, StringComparison.Ordinal)) == true;

    public static 업무가능행동Dto Clone(업무가능행동Dto source)
        => new()
        {
            ActionId = source.ActionId,
            RevisionKindCode = source.RevisionKindCode,
            ExpectedRevision = source.ExpectedRevision,
            ExpiresAtUtc = source.ExpiresAtUtc,
            EnvironmentRequirementCodes = source.EnvironmentRequirementCodes.ToArray()
        };
}
