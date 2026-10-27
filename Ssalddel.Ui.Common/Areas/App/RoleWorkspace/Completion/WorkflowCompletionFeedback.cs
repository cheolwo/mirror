namespace Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Completion;

public enum WorkflowCompletionDisplayMode { EveryStep, Brief, FinalOnly }

/// <summary>이미 확인한 업무 결과의 표시 정보입니다. 명령이나 다음 단계 실행을 포함하지 않습니다.</summary>
public sealed record WorkflowCompletionNotice(string Key, string Title, string Message, string NextStep,
    bool IsFinal, string IconPath, string AccentColor);

/// <summary>현재 화면의 확인 결과만 보관합니다. 새 조회를 완료로 판정하거나 서버 상태를 변경하지 않습니다.</summary>
public sealed class WorkflowCompletionFeedback
{
    private readonly HashSet<string> seen = new(StringComparer.Ordinal);
    private readonly List<WorkflowCompletionNotice> history = [];
    private WorkflowCompletionDisplayMode mode;
    public event Action? Changed;
    public long Revision { get; private set; }
    public WorkflowCompletionNotice? Current { get; private set; }
    public IReadOnlyList<WorkflowCompletionNotice> History => history.AsReadOnly();
    public WorkflowCompletionDisplayMode Mode
    {
        get => mode;
        set
        {
            if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
            if (mode == value) return;
            mode = value; Dismiss();
        }
    }
    public bool Record(WorkflowCompletionNotice notice)
    {
        ArgumentNullException.ThrowIfNull(notice);
        if (string.IsNullOrWhiteSpace(notice.Key) || string.IsNullOrWhiteSpace(notice.Title))
            throw new ArgumentException("확인된 업무 결과의 구분값과 제목이 필요합니다.", nameof(notice));
        if (!seen.Add(notice.Key)) return false;
        history.Add(notice);
        if (history.Count > 10) history.RemoveAt(0);
        Current = mode != WorkflowCompletionDisplayMode.FinalOnly || notice.IsFinal ? notice : null;
        ++Revision; Changed?.Invoke(); return true;
    }
    public void Dismiss() { Current = null; ++Revision; Changed?.Invoke(); }
    public void Clear()
    {
        Current = null; history.Clear(); seen.Clear(); ++Revision; Changed?.Invoke();
    }
}

public static class WorkflowCompletionVisuals
{
    public const string FoodIconPath = "M11 9H9V2H7V9H5V2H3V9C3 11.12 4.66 12.84 6.75 12.97V22H9.25V12.97C11.34 12.84 13 11.12 13 9V2H11V9M16 6V14H18.5V22H21V2C18.24 2 16 4.24 16 6Z";
    public const string CargoIconPath = "M20 8H17V4H3C1.9 4 1 4.9 1 6V17H3C3 18.66 4.34 20 6 20S9 18.66 9 17H15C15 18.66 16.34 20 18 20S21 18.66 21 17H23V12L20 8M6 18.5C5.17 18.5 4.5 17.83 4.5 17S5.17 15.5 6 15.5 7.5 16.17 7.5 17 6.83 18.5 6 18.5M19.5 9.5 21.46 12H17V9.5H19.5M18 18.5C17.17 18.5 16.5 17.83 16.5 17S17.17 15.5 18 15.5 19.5 16.17 19.5 17 18.83 18.5 18 18.5Z";
    public const string PickingIconPath = "M21 8L12 3 3 8V18L12 23 21 18V8M12 5.3 17.6 8.4 12 11.5 6.4 8.4 12 5.3M5 10.1 11 13.4V20.2L5 16.9V10.1M13 20.2V13.4L19 10.1V16.9L13 20.2Z";
}
