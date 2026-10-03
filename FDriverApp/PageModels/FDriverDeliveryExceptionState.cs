using CommunityToolkit.Mvvm.ComponentModel;
using Ssalddel.Contracts.Food;

namespace FDriverApp.PageModels;

/// <summary>현장 중단 입력과 동일 요청 재시도를 소유합니다. 귀책 판정은 서버/운영자 업무입니다.</summary>
public sealed partial class FDriverDeliveryExceptionState : ObservableObject
{
    [ObservableProperty] private bool _isOpen;
    [ObservableProperty] private ActiveDeliveryPreview? _delivery;
    [ObservableProperty] private FDriverInterruptionReason? _selectedReason;
    [ObservableProperty] private string _memo = string.Empty;
    [ObservableProperty] private string _notice = string.Empty;
    [ObservableProperty] private bool _hasPendingRequest;
    private 음식배달중단요청? _pending;

    public IReadOnlyList<FDriverInterruptionReason> Reasons { get; } =
    [
        new("사고", 음식배달중단사유Code.사고),
        new("조리 지연", 음식배달중단사유Code.조리지연),
        new("배터리 부족", 음식배달중단사유Code.배터리부족),
        new("배달 수단 고장", 음식배달중단사유Code.배달수단고장),
        new("위험 기상", 음식배달중단사유Code.위험기상),
        new("개인 긴급·질병", 음식배달중단사유Code.개인긴급),
        new("기타", 음식배달중단사유Code.기타)
    ];

    public bool IsInputUnlocked => !HasPendingRequest;
    public string ActionLabel => HasPendingRequest ? "같은 요청 다시 확인" : "배달 중단 요청";
    public string DeliveryText => Delivery is null ? string.Empty : $"{Delivery.RestaurantName} · {Delivery.OrderSummary}";
    public string PreparationText => Delivery is null ? string.Empty
        : $"가게 도착 {TimeText(Delivery.RestaurantArrivedAtUtc)} · 준비 예정 {TimeText(Delivery.DisplayedPreparationReadyAtUtc)}\n조리 지연 중단 기준 {TimeText(Delivery.PreparationDelayEligibleAtUtc)}";

    public void Open(ActiveDeliveryPreview delivery)
    {
        if (_pending is not null && Delivery?.DeliveryAttemptId != delivery.DeliveryAttemptId) return;
        Delivery = delivery;
        Notice = string.Empty;
        IsOpen = true;
    }

    public void Hide() => IsOpen = false;

    public 음식배달중단요청? Prepare(DateTime utcNow)
    {
        if (_pending is not null) return Copy(_pending);
        if (Delivery is null || string.IsNullOrWhiteSpace(Delivery.DeliveryAttemptId) || SelectedReason is null)
        {
            Notice = "중단 사유를 선택해 주세요.";
            return null;
        }
        if (Memo.Length > 500)
        {
            Notice = "메모는 500자 이내로 입력해 주세요.";
            return null;
        }
        if (SelectedReason.Code == 음식배달중단사유Code.조리지연
            && (!Delivery.RestaurantArrivedAtUtc.HasValue
                || !Delivery.PreparationDelayEligibleAtUtc.HasValue
                || utcNow < Delivery.PreparationDelayEligibleAtUtc.Value))
        {
            Notice = "가게 도착과 서버의 조리 지연 중단 기준 시각을 먼저 확인해 주세요.";
            return null;
        }
        _pending = new()
        {
            클라이언트요청Id = Guid.NewGuid(), 예상시도Revision = Delivery.AttemptRevision,
            사유Code = SelectedReason.Code, 메모 = string.IsNullOrWhiteSpace(Memo) ? null : Memo.Trim()
        };
        HasPendingRequest = true;
        return Copy(_pending);
    }

    public void Reconcile(ActiveDeliveryPreview? current)
    {
        if (Delivery is null) return;
        if (current is null || current.DeliveryAttemptId != Delivery.DeliveryAttemptId)
        {
            Reset();
            return;
        }
        if (current.AttemptRevision != Delivery.AttemptRevision)
        {
            _pending = null;
            HasPendingRequest = false;
            Notice = "배달 상태가 변경되었습니다. 새 상태와 사유를 확인한 뒤 다시 요청해 주세요.";
        }
        Delivery = current;
    }

    public void ConfirmFailure(string message, bool allowEdit)
    {
        if (allowEdit) { _pending = null; HasPendingRequest = false; }
        Notice = message;
    }

    public void Reset()
    {
        IsOpen = false; Delivery = null; SelectedReason = null; Memo = string.Empty;
        _pending = null; HasPendingRequest = false; Notice = string.Empty;
    }

    partial void OnDeliveryChanged(ActiveDeliveryPreview? value)
    {
        OnPropertyChanged(nameof(DeliveryText)); OnPropertyChanged(nameof(PreparationText));
    }
    partial void OnHasPendingRequestChanged(bool value)
    {
        OnPropertyChanged(nameof(IsInputUnlocked)); OnPropertyChanged(nameof(ActionLabel));
    }
    private static 음식배달중단요청 Copy(음식배달중단요청 value) => new()
    { 클라이언트요청Id = value.클라이언트요청Id, 예상시도Revision = value.예상시도Revision, 사유Code = value.사유Code, 메모 = value.메모 };
    private static string TimeText(DateTime? value) => value.HasValue ? value.Value.ToLocalTime().ToString("HH:mm") : "미확인";
}

public sealed record FDriverInterruptionReason(string Label, string Code);
