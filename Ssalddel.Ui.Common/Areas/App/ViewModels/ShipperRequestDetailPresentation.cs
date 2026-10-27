using Ssalddel.Contracts.Shipper.Request;
using Ssalddel.Contracts.Common.Transport;

namespace Ssalddel.Ui.Common.Areas.App.ViewModels;

public enum ShipperRequestDetailMessageTone
{
    Info,
    Success,
    Warning,
    Error
}

public enum ShipperRequestProgressState
{
    Pending,
    Active,
    Completed,
    Attention
}

public sealed class ShipperRequestDetailPageState
{
    public string LookupRequestId { get; set; } = string.Empty;
    public ShipperRequestDetailSnapshot? Request { get; set; }
    public bool IsBusy { get; set; }
    public bool RequiresLogin { get; set; }
    public bool? IsWorkflowEnabled { get; set; } = true;
    public bool Created { get; set; }
    public string StatusMessage { get; set; } = string.Empty;
    public Api작업오류? QueryError { get; set; }
    public ShipperRequestDetailMessageTone StatusTone { get; set; } = ShipperRequestDetailMessageTone.Info;
    public string SourceBoundaryMessage { get; set; }
        = "같은 운송 의뢰 ID의 저장 원장을 다시 조회해 표시합니다.";
}

public sealed record ShipperRequestDetailSnapshot
{
    public string RequestId { get; init; } = string.Empty;
    public string CargoType { get; init; } = string.Empty;
    public string RequestStatus { get; init; } = string.Empty;
    public string PaymentStatus { get; init; } = string.Empty;
    public string SettlementStatus { get; init; } = string.Empty;
    public string DispatchStatus { get; init; } = string.Empty;
    public string TransportStatus { get; init; } = string.Empty;
    public bool OperationalReviewHeld { get; init; }
    public DateTime? TransportLedgerUpdatedAtUtc { get; init; }
    public string AssignedDriverId { get; init; } = string.Empty;
    public string AssignedDriverName { get; init; } = string.Empty;
    public string AssignedDriverVehicle { get; init; } = string.Empty;
    public decimal? DriverLatitude { get; init; }
    public decimal? DriverLongitude { get; init; }
    public DateTime? DriverLocationRecordedAtUtc { get; init; }
    public string DriverLocationStateCode { get; init; } = string.Empty;
    public string TransportMethod { get; init; } = string.Empty;
    public string VehicleType { get; init; } = string.Empty;
    public decimal? 화물중량Kg { get; init; }
    public int? 화물수량 { get; init; }
    public DateTime? 상차시간창시작일시 { get; init; }
    public DateTime? 상차시간창종료일시 { get; init; }
    public DateTime? 하차시간창시작일시 { get; init; }
    public DateTime? 하차시간창종료일시 { get; init; }
    public string PaymentMethod { get; init; } = string.Empty;
    public int? ExpectedPaymentAmount { get; init; }
    /// <summary>화주에게 청구하는 고객 총운임입니다. 기사 보수와 다릅니다.</summary>
    public decimal? FinalFare { get; init; }
    /// <summary>수락 전 제안한 약정액입니다. 수락 후 운송 원장의 동결액을 대신하지 않습니다.</summary>
    public decimal? AgreedDriverFare { get; init; }
    /// <summary>수락 후 운송 원장에 동결된 지급 예정액만 표시합니다. 현재 화주 응답에 없으면 미확인입니다.</summary>
    public decimal? DriverFare { get; init; }
    public DateTime CreatedAt { get; init; }
    public string PickupAddress { get; init; } = string.Empty;
    public string DropoffAddress { get; init; } = string.Empty;
    public string SettlementTiming { get; init; } = string.Empty;
    public string EvidenceMethod { get; init; } = string.Empty;
    public string CollectionOwner { get; init; } = string.Empty;
    public bool TaxInvoiceRequired { get; init; }
    public bool CashReceiptRequired { get; init; }
    public string SettlementMemo { get; init; } = string.Empty;
    public string ReceiptNumber { get; init; } = string.Empty;
    public DateTime? ReceiptRegisteredAt { get; init; }
    public DateTime? OnSiteCollectionConfirmedAt { get; init; }
    public string OnSitePaymentMemo { get; init; } = string.Empty;
    public string CargoDimensions { get; init; } = string.Empty;
    public int? PalletCount { get; init; }
    public bool CanPay { get; init; }

    public static ShipperRequestDetailSnapshot FromContract(화주운송의뢰응답 source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return new ShipperRequestDetailSnapshot
        {
            RequestId = source.의뢰Id,
            CargoType = source.요약?.화물종류 ?? string.Empty,
            RequestStatus = source.의뢰상태,
            PaymentStatus = source.결제상태,
            SettlementStatus = source.정산상태,
            DispatchStatus = source.배차상태,
            TransportStatus = source.운송상태,
            OperationalReviewHeld = source.비정상운송검토보류중,
            TransportLedgerUpdatedAtUtc = source.운송원장갱신일시Utc,
            AssignedDriverId = source.확정기사Id ?? string.Empty,
            AssignedDriverName = source.확정기사명 ?? string.Empty,
            AssignedDriverVehicle = source.확정기사차량 ?? string.Empty,
            DriverLatitude = source.기사최근위도,
            DriverLongitude = source.기사최근경도,
            DriverLocationRecordedAtUtc = source.기사최근위치시각Utc,
            DriverLocationStateCode = source.기사위치상태Code,
            TransportMethod = source.운송방식,
            VehicleType = source.차량종류,
            화물중량Kg = source.화물?.중량Kg,
            화물수량 = source.화물?.수량,
            상차시간창시작일시 = source.픽업?.시간창?.시작일시,
            상차시간창종료일시 = source.픽업?.시간창?.종료일시,
            하차시간창시작일시 = source.하차?.시간창?.시작일시,
            하차시간창종료일시 = source.하차?.시간창?.종료일시,
            PaymentMethod = source.결제수단,
            ExpectedPaymentAmount = source.결제예정금액,
            FinalFare = source.최종운임,
            AgreedDriverFare = source.요금옵션?.기사지급예정운임,
            // 화주 응답의 최종운임과 요금옵션은 운송 원장의 동결 기사액이 아닙니다.
            DriverFare = null,
            CreatedAt = source.생성일시,
            PickupAddress = source.픽업지,
            DropoffAddress = source.하차지,
            SettlementTiming = source.정산시점?.ToString() ?? string.Empty,
            EvidenceMethod = source.증빙방식?.ToString() ?? string.Empty,
            CollectionOwner = source.수납주체?.ToString() ?? string.Empty,
            TaxInvoiceRequired = source.세금계산서필요,
            CashReceiptRequired = source.현금영수증필요,
            SettlementMemo = source.정산메모 ?? string.Empty,
            ReceiptNumber = source.인수증번호 ?? string.Empty,
            ReceiptRegisteredAt = source.인수증등록일시,
            OnSiteCollectionConfirmedAt = source.현장수금확인일시,
            OnSitePaymentMemo = source.현장지급메모 ?? string.Empty,
            CargoDimensions = BuildDimensions(source.화물길이Mm, source.화물폭Mm, source.화물높이Mm),
            PalletCount = source.팔레트개수,
            CanPay = ShipperRequestDetailPresentation.CanPay(source.배차상태, source.결제상태,
                source.정산시점?.ToString(), source.의뢰상태)
        };
    }

    private static string BuildDimensions(int? length, int? width, int? height)
        => length is > 0 && width is > 0 && height is > 0
            ? $"{length:N0} × {width:N0} × {height:N0} mm"
            : string.Empty;
}

public sealed record ShipperRequestTimelineStep(
    string Title,
    string Status,
    string Description,
    string Detail,
    ShipperRequestProgressState State);

public sealed record ShipperRequestProofItem(
    string Title,
    string Status,
    string Description,
    string Detail,
    ShipperRequestProgressState State);

public sealed record ShipperRequestPaymentReceiptPresentation(
    string ReceiptId,
    int Amount,
    string PaymentMethod,
    string SettlementMode,
    string PaymentStatus,
    DateTimeOffset ApprovedAt,
    string? PayerMemo);

public sealed record ShipperRequestNextAction(
    string Title, string Reason, string ResponsibleParty, string Guidance,
    ShipperRequestProgressState State, ShipperRequestDetailScreenKind RelatedScreen);

public static class ShipperRequestDetailPresentation
{
    public static readonly TimeSpan DriverLocationFreshnessWindow = TimeSpan.FromMinutes(10);

    // 조회한 원장의 표시만 분류합니다. 이 결과는 승인 권한이나 배차 실행 가능 여부가 아닙니다.
    public static ShipperRequestNextAction ResolveNextAction(ShipperRequestDetailSnapshot item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (IsCancelled(item))
            return new("의뢰 취소 상태", "환불·정산은 별도 확인이 필요합니다.",
                "화주·처리 담당자", "결제 상태를 확인해 주세요.", ShipperRequestProgressState.Attention, ShipperRequestDetailScreenKind.Payment);
        if (NeedsReview(item))
            return new("진행 확인·처리 대기", "보류·예외 상태입니다.",
                "처리 담당자", "검토 결과를 기다려 주세요.", ShipperRequestProgressState.Attention, ShipperRequestDetailScreenKind.Timeline);
        if (IsSimulationRecord(item))
            return new("모의 수행 기록·정산 제외", "실제 결제·후불 승인·정산 대상이 아닙니다.",
                "수행 확인 담당자", "진행 이력을 확인해 주세요.", ShipperRequestProgressState.Pending, ShipperRequestDetailScreenKind.Timeline);
        if (item.SettlementStatus == "정산완료")
            return new("정산 완료 기록", "실제 지급은 별도 확인이 필요합니다.",
                "정산 담당자", "지급 내역을 확인해 주세요.", ShipperRequestProgressState.Completed, ShipperRequestDetailScreenKind.Proofs);
        if (ResolveDropoffState(item) == ShipperRequestProgressState.Completed)
            return new("운송 완료", "결제·정산·기사 입금은 별도 확인이 필요합니다.",
                "화주·정산 담당자", "인수 증빙을 확인해 주세요.", ShipperRequestProgressState.Completed, ShipperRequestDetailScreenKind.Proofs);
        if (ResolvePickupState(item) == ShipperRequestProgressState.Completed)
            return new(TransportStage(item) == "하차지도착" ? "하차 확인 진행" : "운송 중",
                "하차·인수 전입니다.",
                "기사·수령 담당자", "전달·인수 결과를 확인해 주세요.", ShipperRequestProgressState.Active, ShipperRequestDetailScreenKind.Timeline);
        if (ResolveAcceptedState(item) == ShipperRequestProgressState.Completed)
            return new("기사 배정 완료", "상차 전입니다.",
                "기사·상차 담당자", "상차 장소와 담당자 준비 상태를 확인해 주세요.", ShipperRequestProgressState.Active, ShipperRequestDetailScreenKind.Timeline);
        if (!string.IsNullOrWhiteSpace(TransportStage(item)) && !IsKnownBeforeAcceptance(item))
            return UnconfirmedNextAction();

        if (item.PaymentStatus == "결제대기")
        {
            if (item.SettlementTiming == "선결제")
                return new("접수 준비 · 결제 대기", "결제 후 배차 접수",
                    "화주", "결제를 완료해 주세요.", ShipperRequestProgressState.Active, ShipperRequestDetailScreenKind.Payment);
            if (item.SettlementTiming is ("운송완료후정산" or "월말정산") && item.SettlementStatus != "후불승인완료")
                return new("후불 승인 확인 필요", "후불 승인 미확인",
                    "후불 승인 담당자", "승인 조건을 확인해 주세요.", ShipperRequestProgressState.Active, ShipperRequestDetailScreenKind.Payment);
            if (item.SettlementTiming == "현장지급" && item.SettlementStatus is not ("현장수금예정" or "현장수금완료"))
                return new("현장 지급 조건 확인 필요", "지급 조건 미확인 · 수금·입금과 별도",
                    "화주·현장 지급 담당자", "합의한 지급 조건을 확인해 주세요.", ShipperRequestProgressState.Active, ShipperRequestDetailScreenKind.Payment);
        }

        if (DispatchConditionsRecorded(item))
        {
            if (item.DispatchStatus is "대기" or "배차대기" or "매칭중" or "추천중" or "추천")
                return new("기사 배차 대기", "기사 수락 전",
                    "배차 담당·기사", "배차 결과를 기다려 주세요.", ShipperRequestProgressState.Active, ShipperRequestDetailScreenKind.Timeline);
            return new("배차 접수 확인 필요", "지급 조건 확인됨 · 배차 미확인",
                "배차 담당자", "배차 상태를 확인해 주세요.", ShipperRequestProgressState.Pending, ShipperRequestDetailScreenKind.Timeline);
        }
        return UnconfirmedNextAction();
    }

    public static string ResolveDriverLocationFreshness(
        ShipperRequestDetailSnapshot item,
        DateTime? nowUtc = null)
    {
        if (item.DriverLocationStateCode == 기사위치상태Codes.Closed
            || IsLocationClosed(item.TransportStatus) || IsLocationClosed(item.RequestStatus)) return "운송 종료 후 비공개";
        if (string.IsNullOrWhiteSpace(item.AssignedDriverId))
        {
            return "배차 전";
        }

        if (item.DriverLocationStateCode == 기사위치상태Codes.Delayed) return "위치 갱신 지연 · 현재 위치 확인 필요";
        if (!string.IsNullOrWhiteSpace(item.DriverLocationStateCode) && item.DriverLocationStateCode != 기사위치상태Codes.Available)
            return "위치 미수신";

        if (!item.DriverLocationRecordedAtUtc.HasValue)
        {
            return ContainsAny(item.TransportStatus, "하차완료", "인수완료", "완료", "취소")
                ? "운송 종료 후 비공개"
                : "위치 미수신";
        }

        var referenceUtc = NormalizeUtc(nowUtc ?? DateTime.UtcNow);
        var recordedAtUtc = NormalizeUtc(item.DriverLocationRecordedAtUtc.Value);
        var age = referenceUtc - recordedAtUtc;
        if (age < TimeSpan.Zero) return "위치 시각 확인 필요";
        if (age < DriverLocationFreshnessWindow)
        {
            return "최신 위치";
        }

        var delayedMinutes = Math.Max(1, (int)Math.Floor(age.TotalMinutes));
        return $"위치 갱신 지연 {delayedMinutes:N0}분";
    }

    public static bool IsDriverLocationStale(
        ShipperRequestDetailSnapshot item,
        DateTime? nowUtc = null)
    {
        if (item.DriverLocationStateCode == 기사위치상태Codes.Delayed) return true;
        if (!item.DriverLocationRecordedAtUtc.HasValue)
        {
            return false;
        }

        var age = NormalizeUtc(nowUtc ?? DateTime.UtcNow) - NormalizeUtc(item.DriverLocationRecordedAtUtc.Value);
        return age < TimeSpan.Zero || age >= DriverLocationFreshnessWindow;
    }

    private static bool IsLocationClosed(string status)
        => status is "완료" or "운송완료" or "인수완료" or "하차완료" or "배송완료" or "취소" or "운송취소" or "의뢰취소";

    // 서버의 *Utc 필드는 EF 왕복 뒤 Kind가 없어도 UTC다. 로컬 시간으로 다시 해석하지 않는다.
    public static DateTime NormalizeUtc(DateTime value) => value.Kind == DateTimeKind.Unspecified
        ? DateTime.SpecifyKind(value, DateTimeKind.Utc) : value.ToUniversalTime();

    public static string ResolveEtaStatus(ShipperRequestDetailSnapshot item)
        => ResolveDropoffState(item) == ShipperRequestProgressState.Completed
            ? "도착 완료"
            : "미산정";

    public static IReadOnlyList<ShipperRequestTimelineStep> BuildTimeline(ShipperRequestDetailSnapshot item)
        =>
        [
            new(
                "결제",
                Display(item.PaymentStatus),
                string.Empty,
                $"{Display(item.PaymentMethod)} · {Money(item.ExpectedPaymentAmount)}",
                ResolvePaymentState(item)),
            new(
                "배차",
                Display(item.DispatchStatus),
                ResolveNextAction(item).Title,
                $"{Display(item.VehicleType)} · {Display(item.TransportMethod)}",
                ResolveDispatchState(item)),
            new(
                "기사 수락",
                ResolveAcceptedStatus(item),
                string.Empty,
                $"상차지 {Display(item.PickupAddress)}",
                ResolveAcceptedState(item)),
            new(
                "상차",
                ResolvePickupStatus(item),
                string.Empty,
                $"운송 상태 {Display(TransportStage(item))}",
                ResolvePickupState(item)),
            new(
                "하차",
                ResolveDropoffStatus(item),
                string.Empty,
                $"하차지 {Display(item.DropoffAddress)}",
                ResolveDropoffState(item)),
            new(
                "정산",
                Display(item.SettlementStatus),
                string.Empty,
                $"{Display(item.SettlementTiming)} · {ResolveDriverPaymentSummary(item)}",
                ResolveSettlementState(item))
        ];

    public static string ResolveDriverPaymentSummary(ShipperRequestDetailSnapshot item)
    {
        var beforeAcceptance = IsKnownBeforeAcceptance(item) && string.IsNullOrWhiteSpace(item.AssignedDriverId);
        var amount = beforeAcceptance ? item.AgreedDriverFare : item.DriverFare;
        var label = beforeAcceptance ? "기사 약정액" : "기사 지급 예정액";
        return $"{label} {(amount.HasValue ? Money(amount) : "미확인")}";
    }

    public static IReadOnlyList<ShipperRequestProofItem> BuildProofs(ShipperRequestDetailSnapshot item)
        =>
        [
            new(
                "상차 증빙",
                ResolvePickupState(item) == ShipperRequestProgressState.Completed ? "상차 상태 기록됨" : ResolvePickupStatus(item),
                string.Empty,
                string.Empty,
                ResolvePickupState(item)),
            new(
                "하차 증빙",
                ResolveDropoffState(item) == ShipperRequestProgressState.Completed ? "하차 상태 기록됨" : ResolveDropoffStatus(item),
                string.Empty,
                string.Empty,
                ResolveDropoffState(item)),
            new(
                "인수증",
                string.IsNullOrWhiteSpace(item.ReceiptNumber) ? "등록 전" : "번호 등록됨",
                string.Empty,
                string.IsNullOrWhiteSpace(item.ReceiptNumber)
                    ? $"증빙 방식 {Display(item.EvidenceMethod)}"
                    : $"{item.ReceiptNumber} · {DisplayDateTime(item.ReceiptRegisteredAt)}",
                ResolveReceiptState(item)),
            new(
                "세무 증빙 조건",
                ResolveTaxEvidenceStatus(item),
                "발급 요청 조건입니다.",
                $"세금계산서 {YesNo(item.TaxInvoiceRequired)} · 현금영수증 {YesNo(item.CashReceiptRequired)}",
                item.TaxInvoiceRequired || item.CashReceiptRequired
                    ? ShipperRequestProgressState.Active
                    : ShipperRequestProgressState.Pending)
        ];

    public static string ResolveOverallLabel(ShipperRequestDetailSnapshot item)
        => ResolveNextAction(item).Title;

    public static ShipperRequestProgressState ResolveOverallState(ShipperRequestDetailSnapshot item)
        => ResolveNextAction(item).State;

    public static bool CanPay(string? dispatchStatus, string? paymentStatus)
        => ContainsAny(dispatchStatus, "상차완료", "운송중", "하차지도착", "하차완료", "인수완료")
           && !ContainsAny(paymentStatus, "결제완료", "결제확보", "입금확인", "승인완료");

    // 표시 가능 여부만 판단합니다. 실제 실행은 서버 옵션·권한·확정 운임으로 다시 검사합니다.
    public static bool CanPay(string? dispatchStatus, string? paymentStatus, string? settlementTiming, string? requestStatus)
        => settlementTiming == "선결제" && requestStatus == "생성됨" && dispatchStatus == "미시작" && paymentStatus == "결제대기"
           || CanPay(dispatchStatus, paymentStatus);

    public static ShipperRequestProgressState ResolvePaymentState(ShipperRequestDetailSnapshot item)
    {
        if (HasProblem(item.PaymentStatus))
        {
            return ShipperRequestProgressState.Attention;
        }

        if (IsSimulationRecord(item)) return ShipperRequestProgressState.Pending;

        if (item.PaymentStatus is "결제완료" or "결제확보" or "승인완료" or "입금확인")
        {
            return ShipperRequestProgressState.Completed;
        }

        return ContainsAny(item.PaymentStatus, "결제대기", "미결제", "입금대기", "청구", "대기")
            ? ShipperRequestProgressState.Active
            : ShipperRequestProgressState.Pending;
    }

    public static ShipperRequestProgressState ResolveDispatchState(ShipperRequestDetailSnapshot item)
    {
        if (HasProblem(item.DispatchStatus))
        {
            return ShipperRequestProgressState.Attention;
        }

        if (ResolveAcceptedState(item) == ShipperRequestProgressState.Completed)
        {
            return ShipperRequestProgressState.Completed;
        }

        return DispatchConditionsRecorded(item) && item.DispatchStatus is ("대기" or "매칭중" or "배차대기" or "추천중" or "추천")
            ? ShipperRequestProgressState.Active
            : ShipperRequestProgressState.Pending;
    }

    public static ShipperRequestProgressState ResolveAcceptedState(ShipperRequestDetailSnapshot item)
    {
        if (HasProblem(item.DispatchStatus))
        {
            return ShipperRequestProgressState.Attention;
        }

        if (TransportStage(item) is "배차확정" or "확정" or "기사배정" or "수락" or "이동중" or "상차대기" or "상차중" or "상차지도착"
            or "상차완료" or "운송중" or "하차대기" or "하차지도착" or "하차완료" or "인수완료" or "배송완료" or "운송완료")
        {
            return ShipperRequestProgressState.Completed;
        }

        return DispatchConditionsRecorded(item) && item.DispatchStatus is ("추천" or "추천중" or "매칭중")
            ? ShipperRequestProgressState.Active
            : ShipperRequestProgressState.Pending;
    }

    public static ShipperRequestProgressState ResolvePickupState(ShipperRequestDetailSnapshot item)
    {
        if (HasProblem(item.DispatchStatus) || HasProblem(item.RequestStatus))
        {
            return ShipperRequestProgressState.Attention;
        }

        if (TransportStage(item) is "상차완료" or "운송중" or "하차대기" or "하차지도착" or "하차완료" or "인수완료" or "배송완료" or "운송완료")
        {
            return ShipperRequestProgressState.Completed;
        }

        return TransportStage(item) is "상차지도착" or "상차대기" or "상차중" or "이동중" or "배차확정" or "확정" or "기사배정" or "수락"
            ? ShipperRequestProgressState.Active
            : ShipperRequestProgressState.Pending;
    }

    public static ShipperRequestProgressState ResolveDropoffState(ShipperRequestDetailSnapshot item)
    {
        if (HasProblem(item.DispatchStatus) || HasProblem(item.RequestStatus))
        {
            return ShipperRequestProgressState.Attention;
        }

        if (TransportStage(item) is "하차완료" or "배송완료" or "인수완료" or "운송완료")
        {
            return ShipperRequestProgressState.Completed;
        }

        return TransportStage(item) is "운송중" or "하차지도착" or "하차대기" or "상차완료"
            ? ShipperRequestProgressState.Active
            : ShipperRequestProgressState.Pending;
    }

    public static ShipperRequestProgressState ResolveSettlementState(ShipperRequestDetailSnapshot item)
    {
        if (item.OperationalReviewHeld || item.SettlementStatus == "미수발생"
            || HasProblem(item.SettlementStatus) || HasProblem(item.PaymentStatus))
        {
            return ShipperRequestProgressState.Attention;
        }

        if (IsSimulationRecord(item)) return ShipperRequestProgressState.Pending;

        if (item.SettlementStatus == "정산완료")
        {
            return ShipperRequestProgressState.Completed;
        }

        return item.SettlementStatus is "정산대기" or "입금대기" or "청구대기" or "입금요청" or "결제대기"
            or "후불승인대기" or "후불승인완료" or "인수증대기" or "인수증등록완료" or "입금확인완료" or "현장수금예정" or "현장수금완료" or "결제완료"
               || ResolveDropoffState(item) == ShipperRequestProgressState.Completed
            ? ShipperRequestProgressState.Active
            : ShipperRequestProgressState.Pending;
    }

    public static string Display(string? value, string fallback = "-")
        => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    public static string Money(decimal? amount)
        => amount.HasValue ? $"{amount.Value:N0}원" : "-";

    private static ShipperRequestProgressState ResolveReceiptState(ShipperRequestDetailSnapshot item)
    {
        if (!string.IsNullOrWhiteSpace(item.ReceiptNumber))
        {
            return ShipperRequestProgressState.Completed;
        }

        return ResolveDropoffState(item) == ShipperRequestProgressState.Completed
            ? ShipperRequestProgressState.Active
            : ShipperRequestProgressState.Pending;
    }

    private static string ResolveAcceptedStatus(ShipperRequestDetailSnapshot item)
        => ResolveAcceptedState(item) switch
        {
            ShipperRequestProgressState.Completed => "기사 수락 완료",
            ShipperRequestProgressState.Active => "기사 응답 대기",
            ShipperRequestProgressState.Attention => "수락 흐름 확인 필요",
            _ => IsKnownBeforeAcceptance(item) ? "수락 전" : "수락 상태 미확인"
        };

    private static string ResolvePickupStatus(ShipperRequestDetailSnapshot item)
        => ResolvePickupState(item) switch
        {
            ShipperRequestProgressState.Completed => "상차 완료",
            ShipperRequestProgressState.Active => TransportStage(item) == "상차지도착" ? "상차지 도착" : "상차 준비",
            ShipperRequestProgressState.Attention => "상차 흐름 확인 필요",
            _ => IsKnownBeforeAcceptance(item) ? "상차 전" : "상차 상태 미확인"
        };

    private static string ResolveDropoffStatus(ShipperRequestDetailSnapshot item)
        => ResolveDropoffState(item) switch
        {
            ShipperRequestProgressState.Completed => "하차 완료",
            ShipperRequestProgressState.Active => TransportStage(item) == "하차지도착" ? "하차지 도착" : "하차 진행",
            ShipperRequestProgressState.Attention => "하차 흐름 확인 필요",
            _ => IsKnownBeforeAcceptance(item) || ResolvePickupState(item) == ShipperRequestProgressState.Active ? "하차 전" : "하차 상태 미확인"
        };

    private static string ResolveTaxEvidenceStatus(ShipperRequestDetailSnapshot item)
    {
        if (item.TaxInvoiceRequired && item.CashReceiptRequired)
        {
            return "세금계산서·현금영수증 요청";
        }

        if (item.TaxInvoiceRequired)
        {
            return "세금계산서 요청";
        }

        return item.CashReceiptRequired ? "현금영수증 요청" : "추가 발급 요청 없음";
    }

    private static string DisplayDateTime(DateTime? value)
        => value.HasValue ? value.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm") : "등록 시각 없음";

    private static string YesNo(bool value) => value ? "필요" : "불필요";

    private static bool HasProblem(string? value)
        => ContainsAny(value, "실패", "오류", "취소", "거절", "만료", "보류", "불일치", "부재", "분쟁");

    private static bool ContainsAny(string? value, params string[] tokens)
        => !string.IsNullOrWhiteSpace(value)
           && tokens.Any(token => value.Contains(token, StringComparison.OrdinalIgnoreCase));

    private static string TransportStage(ShipperRequestDetailSnapshot item)
        => !string.IsNullOrWhiteSpace(item.TransportStatus) ? item.TransportStatus
            : !string.IsNullOrWhiteSpace(item.DispatchStatus) ? item.DispatchStatus : item.RequestStatus;

    private static bool DispatchConditionsRecorded(ShipperRequestDetailSnapshot item)
    {
        // 표시용 조건 대조이며 실행 정책을 대체하지 않습니다. 알 수 없는 저장값은 확인된 조건으로 취급하지 않습니다.
        if (item.RequestStatus != "생성됨" || NeedsReview(item)
            || !TryReadStoredEnum<정산시점>(item.SettlementTiming, out var timing)
            || !TryReadStoredEnum<운임정산상태>(item.SettlementStatus, out var settlement))
            return false;
        return item.PaymentStatus == "결제완료" || item.PaymentStatus == "결제대기" && (timing switch
        {
            정산시점.현장지급 => settlement == 운임정산상태.현장수금예정,
            정산시점.운송완료후정산 or 정산시점.월말정산 => settlement == 운임정산상태.후불승인완료,
            _ => false
        });
    }

    private static bool TryReadStoredEnum<TEnum>(string value, out TEnum parsed) where TEnum : struct, Enum
        => Enum.TryParse(value, ignoreCase: false, out parsed) && Enum.IsDefined(parsed)
            && string.Equals(value, parsed.ToString(), StringComparison.Ordinal);

    private static bool IsKnownBeforeAcceptance(ShipperRequestDetailSnapshot item)
        => TransportStage(item) is "생성됨" or "미시작" or "대기" or "배차대기" or "매칭중" or "추천중" or "추천";

    private static bool IsSimulationRecord(ShipperRequestDetailSnapshot item)
        => item.SettlementStatus == "모의 수행·정산 제외";

    private static ShipperRequestNextAction UnconfirmedNextAction()
        => new("진행 상태 확인 필요", "다음 진행 조건 미확인",
            "처리 담당자", "새로고침하거나 담당자에게 문의해 주세요.", ShipperRequestProgressState.Pending, ShipperRequestDetailScreenKind.Payment);

    private static bool IsCancelled(ShipperRequestDetailSnapshot item)
        => new[] { item.RequestStatus, item.DispatchStatus, item.TransportStatus, item.SettlementStatus }
            .Any(status => status is "취소" or "의뢰취소" or "운송취소" or "정산취소");

    private static bool NeedsReview(ShipperRequestDetailSnapshot item)
        => item.OperationalReviewHeld || item.SettlementStatus == "미수발생"
            || new[] { item.RequestStatus, item.DispatchStatus, item.TransportStatus, item.SettlementStatus, item.PaymentStatus }.Any(HasProblem);
}
