using CommunityToolkit.Mvvm.ComponentModel;

namespace FDriverApp.PageModels;

public enum FDriverWorkspaceSection
{
    Delivery,
    Settlement,
    Profile
}

/// <summary>Local view context only. Never changes a delivery, authentication or server work state.</summary>
public sealed class FDriverWorkspaceNavigationState : ObservableObject
{
    private FDriverWorkspaceSection _selected;
    private readonly Dictionary<FDriverWorkspaceSection, double> _scrollPositions = [];

    public FDriverWorkspaceSection Selected => _selected;
    public bool IsDelivery => Selected == FDriverWorkspaceSection.Delivery;
    public bool IsSettlement => Selected == FDriverWorkspaceSection.Settlement;
    public bool IsProfile => Selected == FDriverWorkspaceSection.Profile;
    public bool IsAuxiliary => !IsDelivery;
    public string Title => Selected switch
    {
        FDriverWorkspaceSection.Settlement => "정산",
        FDriverWorkspaceSection.Profile => "내 정보",
        _ => "배달"
    };

    public void Select(FDriverWorkspaceSection section)
    {
        if (!Enum.IsDefined(section))
            throw new ArgumentOutOfRangeException(nameof(section));
        if (!SetProperty(ref _selected, section, nameof(Selected)))
            return;

        OnPropertyChanged(nameof(IsDelivery));
        OnPropertyChanged(nameof(IsSettlement));
        OnPropertyChanged(nameof(IsProfile));
        OnPropertyChanged(nameof(IsAuxiliary));
        OnPropertyChanged(nameof(Title));
    }

    public bool TryApplyFocus(string? focus)
    {
        FDriverWorkspaceSection? section = focus?.Trim().ToLowerInvariant() switch
        {
            "settlement" => FDriverWorkspaceSection.Settlement,
            "profile" => FDriverWorkspaceSection.Profile,
            "dispatch" or "bundle" or "delivery" or "workspace" or "restaurant" or "customer" or "route"
                => FDriverWorkspaceSection.Delivery,
            _ => null
        };
        if (section is null)
            return false;
        Select(section.Value);
        return true;
    }

    public void RememberScroll(FDriverWorkspaceSection section, double scrollY)
        => _scrollPositions[section] = double.IsFinite(scrollY) ? Math.Max(0d, scrollY) : 0d;

    public double GetScrollY(FDriverWorkspaceSection section)
        => _scrollPositions.GetValueOrDefault(section);

    public bool TryReturnToDelivery()
    {
        if (IsDelivery)
            return false;
        Select(FDriverWorkspaceSection.Delivery);
        return true;
    }

    public void Reset()
    {
        _scrollPositions.Clear();
        Select(FDriverWorkspaceSection.Delivery);
    }
}
