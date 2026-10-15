using Ssalddel.Contracts.Driver.Settings;

namespace 살뜰.도메인.기사;

/// <summary>전문 원장의 사실을 읽은 수락 관문입니다. 업무 상태·자격·실경로 또는 지급을 확정하지 않습니다.</summary>
public static class SharedDriverWorkAdmissionPolicy
{
    public static SharedDriverWorkDecision Participation(DriverWorkParticipationRecord? profile, string kind, bool directPicking)
    {
        if (profile is null) return Block("DriverWork.ProfileRequired", "받을 업무와 수행 여력을 먼저 설정해 주세요.");
        var selected = kind switch
        {
            DriverWorkKinds.FoodDelivery => profile.FoodDelivery,
            DriverWorkKinds.QuickDelivery => profile.QuickDelivery,
            DriverWorkKinds.CargoTransport => profile.CargoTransport,
            _ => false
        };
        if (!selected) return Block("DriverWork.NotSelected", "현재 선택하지 않은 종류의 업무입니다.");
        if (directPicking && !profile.DirectPicking)
            return Block("DriverWork.DirectPickingNotSelected", "직접 피킹 후 배송을 받으려면 직접 피킹을 선택해 주세요.");
        return SharedDriverWorkDecision.Allowed;
    }

    public static SharedDriverWorkDecision Evaluate(DriverWorkParticipationRecord? profile,
        IReadOnlyList<SharedDriverWorkDemand> active, IReadOnlyList<SharedDriverWorkDemand> candidates, int operationalMaximum)
    {
        var current = active.DistinctBy(x => x.RequestId, StringComparer.Ordinal).ToArray();
        var currentIds = current.Select(x => x.RequestId).ToHashSet(StringComparer.Ordinal);
        var added = candidates.DistinctBy(x => x.RequestId, StringComparer.Ordinal).Where(x => !currentIds.Contains(x.RequestId)).ToArray();
        // 이미 확정한 업무의 재조회/복구에 새 희망 조건을 소급 적용하지 않습니다.
        if (added.Length == 0) return SharedDriverWorkDecision.Allowed;
        if (profile is null) return Participation(null, added[0].WorkKind, added[0].RequiresDirectPicking);
        foreach (var demand in added)
        {
            var participation = Participation(profile, demand.WorkKind, demand.RequiresDirectPicking);
            if (!participation.CanAccept) return participation;
        }
        if (current.Any(x => x.ReturnCustodyPending))
            return Block("DriverWork.ReturnCustodyPending", "취소된 물품의 반납 확인을 마친 뒤 새 업무를 받을 수 있습니다.");
        if (current.Length > 0 && !profile.ReceiveAdditionalOffers)
            return Block("DriverWork.AdditionalOffersOff", "추가 배차 수신이 꺼져 있습니다. 현재 업무는 계속 진행할 수 있습니다.");
        if (profile.MaxConcurrentJobs is < 1 or > 10 || operationalMaximum is < 1 or > 10)
            return Block("DriverWork.ConcurrentLimitInvalid", "동시 수행 건수 설정을 다시 확인해 주세요.");
        if (current.Length + added.Length > Math.Min(profile.MaxConcurrentJobs, operationalMaximum))
            return Block("DriverWork.ConcurrentLimitExceeded", "현재 업무와 새 업무가 설정한 동시 수행 건수를 초과합니다.");

        var all = current.Concat(added).ToArray();
        var mixedKinds = all.Select(x => x.WorkKind).Distinct(StringComparer.Ordinal).Count() > 1;
        var mixedExecution = all.Select(x => x.UsesCargoExecution).Distinct().Count() > 1;
        // 음식/포장 마트 업무에는 확인된 중량·부피·전달기한이 없습니다. 선택과 분리 동의만으로 혼적을 인증하지 않습니다.
        // 같은 '퀵' 표시라도 화물 실행과 마트 실행은 독립 원장이므로 혼합 근거가 필요합니다.
        if ((mixedKinds || mixedExecution) && !all.All(x => x.UsesCargoExecution))
        {
            if (!profile.FoodCargoSeparationConfirmed)
                return Block("DriverWork.SeparationRequired", "음식과 물품을 분리해 운반할 수 있는지 확인해 주세요.");
            return Block("DriverWork.MixedWorkEvidenceIncomplete", "함께 수행할 업무의 적재량과 전달 시간 근거가 부족합니다. 현재 업무를 마친 뒤 전환할 수 있습니다.");
        }
        if (all.Any(x => x.WeightKg is < 0 || x.VolumeLitres is < 0))
            return Block("DriverWork.LoadEvidenceInvalid", "물품의 중량과 부피 근거를 다시 확인해 주세요.");
        // 같은 음식 업무의 기존 상한·묶음 검사는 전문 서비스가 유지합니다. 모르는 물품 크기를 0으로 합산하지 않습니다.
        if (all.All(x => x.WorkKind == DriverWorkKinds.FoodDelivery)) return SharedDriverWorkDecision.Allowed;
        if (profile.CapacityKg is { } kg)
        {
            if (kg <= 0 || all.Any(x => !x.WeightKg.HasValue))
                return Block("DriverWork.LoadEvidenceIncomplete", "설정한 중량과 비교할 물품 중량이 아직 확인되지 않았습니다.");
            if (all.Sum(x => x.WeightKg!.Value) > kg)
                return Block("DriverWork.WeightCapacityExceeded", "현재 약속한 물품과 새 물품의 중량이 설정한 한도를 초과합니다.");
        }
        if (profile.CapacityLitres is { } litres)
        {
            if (litres <= 0 || all.Any(x => !x.VolumeLitres.HasValue))
                return Block("DriverWork.LoadEvidenceIncomplete", "설정한 부피와 비교할 물품 부피가 아직 확인되지 않았습니다.");
            if (all.Sum(x => x.VolumeLitres!.Value) > litres)
                return Block("DriverWork.VolumeCapacityExceeded", "현재 약속한 물품과 새 물품의 부피가 설정한 한도를 초과합니다.");
        }
        return SharedDriverWorkDecision.Allowed;
    }

    private static SharedDriverWorkDecision Block(string code, string message) => new(false, code, message);
}

public sealed record SharedDriverWorkDemand(string RequestId, string WorkKind, bool RequiresDirectPicking,
    bool UsesCargoExecution, decimal? WeightKg, decimal? VolumeLitres, bool ReturnCustodyPending = false);
public sealed record SharedDriverWorkDecision(bool CanAccept, string? ErrorCode, string? Message)
{
    public static SharedDriverWorkDecision Allowed { get; } = new(true, null, null);
}
