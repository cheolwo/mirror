using Ssalddel.Contracts.Food;

namespace Ssalddel.Application.Driver.Food;

/// <summary>완료 후 기사 상세 열람 기간입니다. DB 보존·파기 기간이나 법정 보존 기간을 정하지 않습니다.</summary>
public sealed class FoodDeliveryCompletedDetailAccessOptions
{
    public const string SectionName = "FoodDelivery:CompletedDetailAccess";
    // 사용자 확정 기본값: 완료 후 3일. 명시 null/0/음수는 민감 상세 제공을 중지합니다.
    public int? WindowMinutes { get; set; } = 3 * 24 * 60;
}

internal static class FoodDeliveryCompletedDetailAccessPolicy
{
    public static (string StatusCode, DateTime? ExpiresAtUtc) Evaluate(
        DateTime completedAtUtc, DateTime nowUtc, FoodDeliveryCompletedDetailAccessOptions options)
    {
        if (completedAtUtc == default || completedAtUtc.Kind == DateTimeKind.Local || completedAtUtc > nowUtc)
            return (FoodDeliveryCompletedDetailAccessStatusCodes.CompletionEvidenceUnavailable, null);
        if (options.WindowMinutes is not > 0)
            return (FoodDeliveryCompletedDetailAccessStatusCodes.PolicyNotConfigured, null);

        DateTime expiresAtUtc;
        try
        {
            // UTC 저장 열은 DB provider에 따라 Unspecified로 읽힐 수 있으므로 UTC로 명시합니다.
            expiresAtUtc = DateTime.SpecifyKind(completedAtUtc, DateTimeKind.Utc).AddMinutes(options.WindowMinutes.Value);
        }
        catch (ArgumentOutOfRangeException)
        {
            return (FoodDeliveryCompletedDetailAccessStatusCodes.PolicyNotConfigured, null);
        }

        return (nowUtc < expiresAtUtc
            ? FoodDeliveryCompletedDetailAccessStatusCodes.Allowed
            : FoodDeliveryCompletedDetailAccessStatusCodes.Expired, expiresAtUtc);
    }
}
