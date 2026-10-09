using System.Globalization;
using Microsoft.Extensions.Options;
using Ssalddel.Contracts.Common.Commerce;

namespace Ssalddel.Services.Commerce;

/// <summary>새 건별 의뢰의 준비 기본값. 기존 거래·배차 자격이나 계약을 변경하지 않는다.</summary>
public sealed class NeighborhoodTaskContractOptions
{
    public const string SectionName = "NeighborhoodTaskContracts";
    public string DefaultContractModel { get; set; } = NeighborhoodTaskContractModels.PerTaskFreelance;
    public NeighborhoodMinorParticipationPreparationOptions MinorParticipation { get; set; } = new();
}

/// <summary>향후 검토할 정책 후보만 보관한다. 이 판본에서는 Enabled를 켤 수 없다.</summary>
public sealed class NeighborhoodMinorParticipationPreparationOptions
{
    public bool Enabled { get; set; }
    public string PolicyRevision { get; set; } = string.Empty;
    public string LegalBasisReference { get; set; } = string.Empty;
    public string LegalReviewReference { get; set; } = string.Empty;
    public DateTimeOffset? EffectiveFromUtc { get; set; }
    public DateTimeOffset? EffectiveUntilUtc { get; set; }
    public int? MinimumAgeYears { get; set; }
    public string[] PermittedWorkKinds { get; set; } = [];
    public string[] PermittedLocalTimeWindows { get; set; } = [];
}

public sealed class NeighborhoodTaskContractOptionsValidator : IValidateOptions<NeighborhoodTaskContractOptions>
{
    private static readonly HashSet<string> CandidateWorkKinds = new(StringComparer.Ordinal)
        { "FoodDelivery", "CargoDelivery", "Inbound", "Picking", "DeliveryAssistance" };

    public ValidateOptionsResult Validate(string? name, NeighborhoodTaskContractOptions options)
    {
        var failures = new List<string>();
        if (options.DefaultContractModel != NeighborhoodTaskContractModels.PerTaskFreelance)
            failures.Add("새 건별 의뢰의 준비 기본값은 PerTaskFreelance여야 합니다. 기존 고용계약을 재분류하지 않습니다.");
        if (options.MinorParticipation is not { } minor)
            return ValidateOptionsResult.Fail("미성년 참여 준비 설정이 필요합니다. 현재 참여 허용 설정은 아닙니다.");
        if (minor.Enabled)
            failures.Add("미성년 참여는 이 판본에서 활성화할 수 없습니다. 법령 시행·검토와 신원·대리인 확인 및 업무 경로 검증을 거친 후속 구현이 필요합니다.");
        if (minor.MinimumAgeYears is < 0 or > 18)
            failures.Add("향후 미성년 정책 후보의 나이는 0~18 범위이거나 미확정이어야 합니다. 범위 안의 값도 참여 허가가 아닙니다.");
        if (minor.EffectiveFromUtc is { Offset: var fromOffset } && fromOffset != TimeSpan.Zero
            || minor.EffectiveUntilUtc is { Offset: var untilOffset } && untilOffset != TimeSpan.Zero)
            failures.Add("향후 정책 후보의 적용 시각은 UTC로 기록해야 합니다.");
        if (minor.EffectiveFromUtc is { } from && minor.EffectiveUntilUtc is { } until && until <= from)
            failures.Add("향후 정책 후보의 종료 시각은 시작 시각보다 뒤여야 합니다.");
        if (new[] { minor.PolicyRevision, minor.LegalBasisReference, minor.LegalReviewReference }
            .Any(reference => reference is null || reference.Length > 512))
            failures.Add("후보 판본·법적 근거·검토 참조는 512자 이내여야 합니다. 개인정보 원문을 넣지 않습니다.");
        if (minor.PermittedWorkKinds is null || minor.PermittedWorkKinds.Length > CandidateWorkKinds.Count
            || minor.PermittedWorkKinds.Any(kind => !CandidateWorkKinds.Contains(kind))
            || minor.PermittedWorkKinds.Distinct(StringComparer.Ordinal).Count() != minor.PermittedWorkKinds.Length)
            failures.Add("향후 업무 후보는 정의된 종류를 중복 없이 사용해야 합니다. 현재 허용 업무 목록이 아닙니다.");
        if (minor.PermittedLocalTimeWindows is null || minor.PermittedLocalTimeWindows.Length > 24
            || minor.PermittedLocalTimeWindows.Any(window => !IsTimeWindow(window)))
            failures.Add("향후 시간대 후보는 HH:mm-HH:mm 형식이어야 합니다. 현재 작업 가능 시간을 변경하지 않습니다.");
        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static bool IsTimeWindow(string? value)
        => value is { Length: 11 } && value[5] == '-'
            && TimeOnly.TryParseExact(value[..5], "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var start)
            && TimeOnly.TryParseExact(value[6..], "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var end)
            && start != end;
}
