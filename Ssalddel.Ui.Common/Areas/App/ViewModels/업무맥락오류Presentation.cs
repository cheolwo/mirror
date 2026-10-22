using Ssalddel.Contracts.Common.Versioning;
using Ssalddel.Contracts.Common.Workflow;

namespace Ssalddel.Ui.Common.Areas.App.ViewModels;

/// <summary>업무 실패의 표시만 소유합니다. 원장 판정·명령 재전송·권한 부여를 수행하지 않습니다.</summary>
public static class 업무맥락오류Presentation
{
    public static 업무오류안내Presentation 생성(Api작업오류 error, 업무실패맥락Dto? fallback = null,
        bool outcomeUnknown = false)
    {
        ArgumentNullException.ThrowIfNull(error);
        var context = error.WorkflowContext?.유효한가() == true ? error.WorkflowContext
            : fallback?.유효한가() == true ? fallback : null;
        // GET에는 실행 단계가 없으므로 같은 OS의 화면 목적(상차·하차·인수 등)으로 표시만 구체화합니다.
        // 명령의 단계·결과는 화면의 fallback으로 덮지 않습니다.
        if (context?.OutcomeCode == 업무처리결과Codes.조회 && fallback?.유효한가() == true
            && context.OperatingSystemId == fallback.OperatingSystemId)
            context = context with { StageId = fallback.StageId };
        var label = 단계명(context);
        var outcome = outcomeUnknown ? 업무처리결과Codes.미확정 : context?.OutcomeCode;
        var key = $"{context?.OperatingSystemId}:{context?.StageId}:{outcome}:{error.코드}";
        var cargoCompletion = context?.OperatingSystemId == OperatingSystemIds.DomesticCargoTransport
            && context.StageId is OperatingSystemLifecycleStageIds.CargoPickup or OperatingSystemLifecycleStageIds.CargoDropoff;
        if (cargoCompletion && error.코드 == "CargoCompletionPreparationIncomplete" && outcome == 업무처리결과Codes.미적용)
            return new(key, $"{label} 준비 확인 필요", "완료 요청을 보내지 않았습니다. 사진과 확인 항목을 점검한 뒤 다시 진행해 주세요.")
            { 재조회허용 = false };
        if (cargoCompletion && error.코드 == "CargoCompletionRecoveryUnavailable")
            return new(key, $"{label} 요청 확인 필요", "이전 요청의 확인 기록을 읽거나 저장하지 못했습니다. 같은 업무를 다시 확인해 주세요.", "다시 확인")
            { 복귀허용 = false, 재조회허용 = true };
        if (cargoCompletion && error.코드 == "CargoCompletionRecoveryCleanupPending" && outcome == 업무처리결과Codes.적용)
            return new(key, $"{label} 완료 · 확인 기록 정리 필요", "완료는 확인됐습니다. 다시 완료하지 말고 확인 기록 정리를 다시 진행해 주세요.", "다시 확인")
            { 복귀허용 = false, 재조회허용 = true };
        // Unknown은 404·로그인·도메인 사유보다 먼저 결과 확인으로 안내합니다.
        if (outcome == 업무처리결과Codes.미확정)
            return new(key, $"{label} 결과 확인 필요", "처리 결과를 아직 확인하지 못했습니다. 다시 제출하지 말고 같은 업무의 결과를 확인해 주세요.", "결과 확인")
            { 복귀허용 = false, 재조회허용 = true };
        if (outcome == 업무처리결과Codes.적용)
            return new(key, context?.StageId == OperatingSystemLifecycleStageIds.WarehouseReceiving
                    ? "입고 완료 · 내역 확인 필요"
                    : context?.StageId == OperatingSystemLifecycleStageIds.WarehouseInboundPlan
                        ? "입고 신청 완료 · 내역 확인 필요" : $"{label} 등록 완료 · 내역 확인 필요",
                context?.StageId == OperatingSystemLifecycleStageIds.WarehouseReceiving
                    ? "입고 완료 처리는 끝났습니다. 다시 완료하지 말고 처리된 내역을 확인해 주세요."
                    : "등록은 완료됐습니다. 다시 제출하지 말고 등록된 내역을 확인해 주세요.", "내역 확인")
            { 재조회허용 = true };

        var knownCargoReason = error.코드.StartsWith("Cargo", StringComparison.Ordinal)
            || error.코드 is "MartDriverWorkflowRequired" or "DistributedCargoCustodyRequired";
        var matchingCargoReason = context?.OperatingSystemId == OperatingSystemIds.DomesticCargoTransport
            && 사유단계일치(error.코드, context.StageId);
        var notice = (matchingCargoReason ? error.코드 : null) switch
        {
            "CargoTransportHeld" => new 업무오류안내Presentation(key, "운송 보류", "운송이 보류되어 있습니다. 처리 상태를 확인해 주세요.", "처리 상태 확인"),
            "CargoTransportStateChanged" => new(key, "운송 상태가 변경됐습니다", "현재 운송을 다시 확인한 뒤 다음 업무를 진행해 주세요.", "운송 상태 확인"),
            "CargoPickupPhotoRequired" => new(key, "상차 사진 필요", "상차 사진을 등록한 뒤 완료해 주세요.") { 재조회허용 = false },
            "CargoDropoffPhotoRequired" => new(key, "하차 사진 필요", "하차 사진을 등록한 뒤 완료해 주세요.") { 재조회허용 = false },
            "CargoReceiptEvidenceRequired" => new(key, "인수 증빙 확인 필요", "필요한 인수증·서명 항목을 확인해 주세요.") { 재조회허용 = false },
            "CargoPhotoStorageUnavailable" => new(key, "증빙 저장소 확인 필요", "사진 보관 상태를 확인한 뒤 다시 진행해 주세요.") { 재조회허용 = false },
            "CargoPhotoBindingMismatch" or "CargoPhotoReceiptInvalid" or "CargoPhotoUploadReceiptRequired" or "CargoPhotoIntegrityMismatch"
                => new(key, $"{label} 사진 확인 필요", "해당 운송에 사용할 사진을 확인해 주세요.") { 재조회허용 = false },
            "CargoPhotoStorageVerificationPending" => new(key, $"{label} 사진 확인 중", "사진 확인이 끝난 뒤 다시 진행해 주세요.") { 재조회허용 = false },
            "CargoWarehouseHandoffPending" => new(key, "창고 인계 확인 필요", "창고의 인계 처리가 끝나면 운송 상태를 다시 확인해 주세요.", "인계 상태 확인"),
            "MartDriverWorkflowRequired" => new(key, "피킹·배송 업무에서 진행해 주세요", "해당 업무의 피킹·인계 절차를 확인해 주세요.") { 재조회허용 = false },
            "DistributedCargoCustodyRequired" => new(key, "화물 인계 확인 필요", "피킹·인계 절차를 확인한 뒤 진행해 주세요.") { 재조회허용 = false },
            _ when error.Http상태코드 == 404 || error.코드 is "CargoTransportNotFound" or "ShipperRequestNotFound" or "request-not-found"
                => new(key, $"{label} 대상을 찾지 못했습니다", "업무 목록에서 대상을 다시 선택해 주세요."),
            _ when error.Http상태코드 is 401 or 403
                => new(key, $"{label} 접근 확인 필요", "업무를 맡은 계정과 접근 권한을 확인해 주세요.") { 재조회허용 = false },
            _ => new(key, $"{label} 정보 확인 필요", $"{label} 정보를 불러오지 못했습니다. 같은 업무를 다시 확인해 주세요.", $"{label} 확인")
        };
        if (knownCargoReason && !matchingCargoReason)
            notice = notice with { 재조회허용 = false };
        // 서버가 명시한 행동은 화면의 기존 GET 콜백과 교집합으로만 사용합니다.
        if (context is not null && context.OutcomeCode != 업무처리결과Codes.조회
            && !업무복구행동목록.포함(error.복구가능행동, 업무복구행동Ids.상태전체재조회))
            notice = notice with { 재조회허용 = false };
        return notice;
    }

    private static bool 사유단계일치(string reason, string stage)
        => reason switch
        {
            "CargoPickupPhotoRequired" or "CargoReceiptEvidenceRequired" or "CargoWarehouseHandoffPending"
                => stage == OperatingSystemLifecycleStageIds.CargoPickup,
            "CargoDropoffPhotoRequired" => stage == OperatingSystemLifecycleStageIds.CargoDropoff,
            "CargoPhotoStorageUnavailable" or "CargoPhotoBindingMismatch" or "CargoPhotoReceiptInvalid"
                or "CargoPhotoUploadReceiptRequired" or "CargoPhotoIntegrityMismatch" or "CargoPhotoStorageVerificationPending"
                or "MartDriverWorkflowRequired" or "DistributedCargoCustodyRequired" or "CargoTransportStateChanged"
                => stage is OperatingSystemLifecycleStageIds.CargoPickup or OperatingSystemLifecycleStageIds.CargoDropoff,
            "CargoTransportHeld" => stage == OperatingSystemLifecycleStageIds.CargoInterruptionRecovery,
            "CargoTransportNotFound" => true,
            _ => false
        };

    private static string 단계명(업무실패맥락Dto? context)
        => context?.StageId switch
        {
            OperatingSystemLifecycleStageIds.CargoPickup => "상차",
            OperatingSystemLifecycleStageIds.CargoDropoff => "하차",
            OperatingSystemLifecycleStageIds.CargoDispatch => "배차",
            OperatingSystemLifecycleStageIds.CargoEvidenceSettlement => "운송 정산",
            OperatingSystemLifecycleStageIds.CargoInterruptionRecovery => "운송 문제",
            OperatingSystemLifecycleStageIds.CargoTransport => "운송",
            OperatingSystemLifecycleStageIds.ShipperDeliveryAcceptance => "인수",
            OperatingSystemLifecycleStageIds.ShipperSettlementRecovery => "정산",
            OperatingSystemLifecycleStageIds.ShipperProgressChange => "운송 의뢰",
            OperatingSystemLifecycleStageIds.ShipperRequestCommitment => "운송 접수",
            OperatingSystemLifecycleStageIds.WarehouseInboundPlan or OperatingSystemLifecycleStageIds.WarehouseReceiving => "입고",
            OperatingSystemLifecycleStageIds.WarehousePicking => "피킹",
            OperatingSystemLifecycleStageIds.WarehousePacking => "포장",
            _ => "업무"
        };
}
