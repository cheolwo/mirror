using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Completion;

namespace FDriverApp.PageModels;

/// <summary>현재 화면에서 확인한 결과만 표시합니다. 알림 확인은 업무 명령이 아닙니다.</summary>
public sealed class FDriverCompletionFeedbackState : ObservableObject
{
    private readonly WorkflowCompletionFeedback feedback = new();
    public FDriverCompletionFeedbackState()
    {
        DismissCommand = new RelayCommand(Dismiss);
        feedback.Changed += NotifyState;
    }
    public List<string> Modes { get; } = ["단계마다 알림창", "간단히 표시", "최종 전달만 알림창"];
    public int ModeIndex
    {
        get => (int)feedback.Mode;
        set
        {
            if (value < 0 || value >= Modes.Count || ModeIndex == value) return;
            feedback.Mode = (WorkflowCompletionDisplayMode)value;
            OnPropertyChanged();
        }
    }
    public WorkflowCompletionNotice? Current => feedback.Current;
    public IReadOnlyList<WorkflowCompletionNotice> History => feedback.History;
    public bool HasNotice => Current is not null;
    public bool IsDialog => HasNotice && feedback.Mode != WorkflowCompletionDisplayMode.Brief;
    public bool IsBrief => HasNotice && feedback.Mode == WorkflowCompletionDisplayMode.Brief;
    public string Title => Current?.Title ?? string.Empty;
    public string Message => Current?.Message ?? string.Empty;
    public string NextStep => Current?.NextStep ?? string.Empty;
    public string IconPath => Current?.IconPath ?? WorkflowCompletionVisuals.FoodIconPath;
    public string AccentColor => Current?.AccentColor ?? "#2563EB";
    public bool HasHistory => History.Count > 0;
    public string RecentResultsText => string.Join(Environment.NewLine, History.Reverse().Select(x => x.Title));
    public IRelayCommand DismissCommand { get; }
    public bool Record(WorkflowCompletionNotice notice) => feedback.Record(notice);
    public void Dismiss() => feedback.Dismiss();
    public void Clear() => feedback.Clear();
    private void NotifyState()
    {
        foreach (var property in new[] { nameof(Current), nameof(HasNotice), nameof(IsDialog), nameof(IsBrief),
            nameof(Title), nameof(Message), nameof(NextStep), nameof(IconPath), nameof(AccentColor),
            nameof(History), nameof(HasHistory), nameof(RecentResultsText) })
            OnPropertyChanged(property);
    }
}
