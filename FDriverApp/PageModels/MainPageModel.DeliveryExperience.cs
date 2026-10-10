using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ssalddel.Contracts.Food;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Food;

namespace FDriverApp.PageModels;

public sealed partial class MainPageModel
{
    private CancellationTokenSource _experienceSharingLifetime = new();
    private string? _experienceSharingOwner;
    private DeliveryExperienceSharingPreferenceDto? _confirmedExperienceSharing;
    private bool _experienceSnapshotUnavailable;

    [ObservableProperty, NotifyPropertyChangedFor(nameof(CanEditExperienceSharing), nameof(CanLoadExperienceSharing))]
    private bool _experienceSharingBusy;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(CanEditExperienceSharing))]
    private bool _experienceSharingLoaded;
    [ObservableProperty] private bool _shareDeliveryExperience;
    [ObservableProperty] private string _experienceSharingNotice = "공유 설정 확인 전";

    public bool CanEditExperienceSharing => ExperienceSharingLoaded && !ExperienceSharingBusy
        && _confirmedExperienceSharing?.Enabled == true && IsAuthenticated && HasDriverSession();
    public bool CanLoadExperienceSharing => !ExperienceSharingBusy && IsAuthenticated && HasDriverSession();
    private IReadOnlyList<string> CurrentExperienceLines => IsAuthenticated && HasDriverSession() && !_experienceSnapshotUnavailable
        ? DeliveryExperiencePresentation.DriverLines(ActiveDelivery?.ExperienceSummaries, HasDeliveryStage) : [];
    public string PickupExperienceText => HasPickupStage ? CurrentExperienceLines.FirstOrDefault() ?? string.Empty : string.Empty;
    public string DestinationExperienceText => HasDeliveryStage ? CurrentExperienceLines.FirstOrDefault() ?? string.Empty : string.Empty;
    public string DestinationExperienceSecondText => HasDeliveryStage ? CurrentExperienceLines.Skip(1).FirstOrDefault() ?? string.Empty : string.Empty;
    public bool HasPickupExperience => !string.IsNullOrEmpty(PickupExperienceText);
    public bool HasDestinationExperience => !string.IsNullOrEmpty(DestinationExperienceText);
    public bool HasDestinationSecondExperience => !string.IsNullOrEmpty(DestinationExperienceSecondText);

    private void InitializeDeliveryExperience()
    {
        var observedOwner = _authSession.UserId;
        var observedAccess = HasDriverSession();
        _authSession.SessionChanged += (_, _) =>
        {
            var owner = _authSession.UserId;
            var access = HasDriverSession();
            if (observedOwner == owner && observedAccess == access) return;
            observedOwner = owner;
            observedAccess = access;
            _experienceSharingLifetime.Cancel();
            _ = MainThread.InvokeOnMainThreadAsync(ResetDeliveryExperienceState);
        };
    }

    private void ResetDeliveryExperienceState()
    {
        _experienceSharingLifetime.Cancel(); _experienceSharingLifetime.Dispose(); _experienceSharingLifetime = new();
        _experienceSharingOwner = null; _confirmedExperienceSharing = null;
        ExperienceSharingLoaded = ExperienceSharingBusy = ShareDeliveryExperience = false;
        ExperienceSharingNotice = "공유 설정 확인 전";
        _experienceSnapshotUnavailable = true;
        NotifyDeliveryExperience();
    }

    private void NotifyDeliveryExperience()
    {
        OnPropertyChanged(nameof(PickupExperienceText));
        OnPropertyChanged(nameof(DestinationExperienceText));
        OnPropertyChanged(nameof(DestinationExperienceSecondText));
        OnPropertyChanged(nameof(HasPickupExperience));
        OnPropertyChanged(nameof(HasDestinationExperience));
        OnPropertyChanged(nameof(HasDestinationSecondExperience));
        OnPropertyChanged(nameof(CanEditExperienceSharing));
        OnPropertyChanged(nameof(CanLoadExperienceSharing));
    }

    private void SetDeliveryExperienceSnapshotCurrent(bool current)
    {
        _experienceSnapshotUnavailable = !current;
        NotifyDeliveryExperience();
    }

    private bool ExperienceSharingCurrent(string? owner, CancellationToken token)
        => !token.IsCancellationRequested && IsAuthenticated && HasDriverSession()
           && !string.IsNullOrWhiteSpace(owner) && owner == _authSession.UserId;

    [RelayCommand]
    private async Task LoadExperienceSharing()
    {
        if (ExperienceSharingBusy) return;
        var owner = _authSession.UserId;
        var lifetime = _experienceSharingLifetime;
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token, _workspaceCancellation.Token);
        var token = cancellation.Token;
        _experienceSharingOwner = owner; _confirmedExperienceSharing = null;
        ExperienceSharingLoaded = ShareDeliveryExperience = false;
        if (!ExperienceSharingCurrent(owner, token)) { ExperienceSharingNotice = "로그인한 뒤 공유 설정을 확인해 주세요."; return; }
        ExperienceSharingBusy = true; ExperienceSharingNotice = "공유 설정을 확인하고 있습니다.";
        try
        {
            var result = await _api.GetDeliveryExperienceSharingAsync(token);
            if (!ExperienceSharingCurrent(owner, token)) return;
            ApplyExperienceSharing(result);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            if (ReferenceEquals(lifetime, _experienceSharingLifetime))
                ExperienceSharingNotice = "설정 조회가 중단됐습니다. 다시 조회해 주세요.";
        }
        catch
        {
            if (ExperienceSharingCurrent(owner, token))
                ExperienceSharingNotice = "공유 설정을 확인하지 못했습니다. 다시 조회해 주세요.";
        }
        finally { if (ReferenceEquals(lifetime, _experienceSharingLifetime)) ExperienceSharingBusy = false; }
    }

    [RelayCommand]
    private async Task SaveExperienceSharing()
    {
        if (!CanEditExperienceSharing || _confirmedExperienceSharing is null) return;
        var owner = _experienceSharingOwner;
        var lifetime = _experienceSharingLifetime;
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token, _workspaceCancellation.Token);
        var token = cancellation.Token;
        if (!ExperienceSharingCurrent(owner, token)) return;
        var expected = _confirmedExperienceSharing.Revision;
        var request = new DeliveryExperienceSharingPreferenceRequest
        { ExpectedRevision = expected, ShareWithCurrentParticipants = ShareDeliveryExperience };
        ExperienceSharingBusy = true; ExperienceSharingNotice = "공유 설정을 저장하고 있습니다.";
        try
        {
            var written = await _api.UpdateDeliveryExperienceSharingAsync(request, token);
            if (!ExperienceSharingCurrent(owner, token)) return;
            if (written.Revision != expected + 1 || written.ShareWithCurrentParticipants != request.ShareWithCurrentParticipants)
                throw new InvalidDataException("저장한 공유 설정을 확인하지 못했습니다.");
            var confirmed = await _api.GetDeliveryExperienceSharingAsync(token);
            if (!ExperienceSharingCurrent(owner, token)) return;
            if (confirmed.Revision < written.Revision)
                throw new InvalidDataException("공유 설정의 최신 판본을 확인하지 못했습니다.");
            ApplyExperienceSharing(confirmed);
            if (confirmed.Revision > written.Revision)
                ExperienceSharingNotice = "다른 화면에서 변경된 최신 공유 설정을 확인했습니다.";
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            if (ReferenceEquals(lifetime, _experienceSharingLifetime)) InvalidateExperienceSharingResult();
        }
        catch
        {
            if (ExperienceSharingCurrent(owner, token)) InvalidateExperienceSharingResult();
        }
        finally { if (ReferenceEquals(lifetime, _experienceSharingLifetime)) ExperienceSharingBusy = false; }
    }

    private void InvalidateExperienceSharingResult()
    {
        _confirmedExperienceSharing = null;
        ExperienceSharingLoaded = ShareDeliveryExperience = false;
        ExperienceSharingNotice = "저장 결과를 확인하지 못했습니다. 다시 조회한 뒤 확인해 주세요.";
    }

    partial void OnShareDeliveryExperienceChanged(bool value)
    {
        if (ExperienceSharingLoaded && _confirmedExperienceSharing is { } current
            && current.ShareWithCurrentParticipants != value)
            ExperienceSharingNotice = "선택한 공유 설정은 저장하면 적용됩니다.";
    }

    private void ApplyExperienceSharing(DeliveryExperienceSharingPreferenceDto value)
    {
        if (value.Revision < 0) throw new InvalidDataException("공유 설정의 판본이 올바르지 않습니다.");
        _confirmedExperienceSharing = value;
        ExperienceSharingLoaded = true;
        ShareDeliveryExperience = value.ShareWithCurrentParticipants;
        ExperienceSharingNotice = !value.Enabled ? "배달 경험 공유 기능이 아직 제공되지 않습니다."
            : value.ShareWithCurrentParticipants
                ? "현재 배달의 음식점·주문자에게 확인된 완료 건수를 공유합니다."
                : "배달 경험 공유가 꺼져 있습니다. 내 경험은 본인 화면에서 확인합니다.";
    }
}
