using CommunityToolkit.Mvvm.ComponentModel;
using Hongdal.Ui.Common.Areas.App.Services;
using MudBlazor;

namespace Hongdal.Ui.Common.Areas.App.ViewModels;

public enum DiagramConnectionHandleKind
{
    Top,
    Right,
    Bottom,
    Left
}

public enum DiagramNodeConnectionRole
{
    Standard,
    WarehouseOutbound,
    WarehouseInbound
}

public enum DiagramEdgeStyleKind
{
    Curve,
    Straight,
    Elbow
}

public sealed record DiagramDragPoint(double X, double Y);

public sealed record DiagramHandleDrag(string NodeTitle, DiagramConnectionHandleKind Handle);

public sealed record 다이어그램레이어정의(
    string Key,
    string Label,
    string Description,
    int DisplayOrder,
    int ConflictPriority,
    string Icon,
    Color Color,
    bool DefaultVisible,
    bool IsLocked = false);

public sealed record 원장블록연결선(
    string Id,
    string FromTitle,
    string ToTitle,
    string Label,
    bool IsCustom,
    DiagramConnectionHandleKind FromHandle = DiagramConnectionHandleKind.Right,
    DiagramConnectionHandleKind ToHandle = DiagramConnectionHandleKind.Left,
    DiagramEdgeStyleKind Style = DiagramEdgeStyleKind.Curve);

public sealed record DiagramEdgeMutationResult(
    원장블록연결선 Edge,
    bool IsNew);

public sealed class PlatformCommunityDiagramCanvasViewModel : ObservableObject
{
    private string? _selectedNodeTitle;
    private string? _connectionStartNodeTitle;
    private string? _connectionMessage;
    private string? _selectedEdgeId;
    private bool _isEdgeOptionDockCollapsed = true;
    private DiagramHandleDrag? _activeHandleDrag;
    private DiagramDragPoint? _dragPointer;
    private bool _handleDragMoved;
    private bool _suppressNextHandleClick;
    private string _newConnectionLabel = "다음 단계";
    private CommunityComposerMessageKind _connectionMessageKind = CommunityComposerMessageKind.Info;

    public List<string> NodeOrder { get; } = [];
    public PlatformDiagramNodeStackOrder StackOrder { get; } = new();
    public List<원장블록연결선> CustomEdges { get; } = [];
    public Dictionary<string, string> EdgeLabels { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, DiagramEdgeStyleKind> EdgeStyles { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> HiddenLayerKeys { get; } = new(StringComparer.OrdinalIgnoreCase);

    public string? SelectedNodeTitle
    {
        get => _selectedNodeTitle;
        set => SetProperty(ref _selectedNodeTitle, value);
    }

    public string? ConnectionStartNodeTitle
    {
        get => _connectionStartNodeTitle;
        set => SetProperty(ref _connectionStartNodeTitle, value);
    }

    public string? ConnectionMessage
    {
        get => _connectionMessage;
        set => SetProperty(ref _connectionMessage, value);
    }

    public string? SelectedEdgeId
    {
        get => _selectedEdgeId;
        set => SetProperty(ref _selectedEdgeId, value);
    }

    public bool IsEdgeOptionDockCollapsed
    {
        get => _isEdgeOptionDockCollapsed;
        set => SetProperty(ref _isEdgeOptionDockCollapsed, value);
    }

    public DiagramHandleDrag? ActiveHandleDrag
    {
        get => _activeHandleDrag;
        set => SetProperty(ref _activeHandleDrag, value);
    }

    public DiagramDragPoint? DragPointer
    {
        get => _dragPointer;
        set => SetProperty(ref _dragPointer, value);
    }

    public bool HandleDragMoved
    {
        get => _handleDragMoved;
        set => SetProperty(ref _handleDragMoved, value);
    }

    public bool SuppressNextHandleClick
    {
        get => _suppressNextHandleClick;
        set => SetProperty(ref _suppressNextHandleClick, value);
    }

    public string NewConnectionLabel
    {
        get => _newConnectionLabel;
        set => SetProperty(ref _newConnectionLabel, value ?? string.Empty);
    }

    public CommunityComposerMessageKind ConnectionMessageKind
    {
        get => _connectionMessageKind;
        set => SetProperty(ref _connectionMessageKind, value);
    }

    public void Reset()
    {
        NodeOrder.Clear();
        StackOrder.Clear();
        ResetConnections();
        OnPropertyChanged(string.Empty);
    }

    public void ResetConnections()
    {
        CustomEdges.Clear();
        EdgeLabels.Clear();
        EdgeStyles.Clear();
        SelectedNodeTitle = null;
        ConnectionStartNodeTitle = null;
        SelectedEdgeId = null;
        ActiveHandleDrag = null;
        DragPointer = null;
        HandleDragMoved = false;
        SuppressNextHandleClick = false;
        NewConnectionLabel = "다음 단계";
        ConnectionMessage = null;
        ConnectionMessageKind = CommunityComposerMessageKind.Info;
        IsEdgeOptionDockCollapsed = true;
        OnPropertyChanged(string.Empty);
    }

    public DiagramEdgeMutationResult AddOrSelectCustomEdge(
        string fromTitle,
        string toTitle,
        DiagramConnectionHandleKind fromHandle,
        DiagramConnectionHandleKind toHandle,
        string? label)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fromTitle);
        ArgumentException.ThrowIfNullOrWhiteSpace(toTitle);

        var existingEdge = CustomEdges.FirstOrDefault(edge =>
            string.Equals(edge.FromTitle, fromTitle, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(edge.ToTitle, toTitle, StringComparison.OrdinalIgnoreCase) &&
            edge.FromHandle == fromHandle &&
            edge.ToHandle == toHandle);

        if (existingEdge is not null)
        {
            SelectedEdgeId = existingEdge.Id;
            return new DiagramEdgeMutationResult(existingEdge, IsNew: false);
        }

        var edge = new 원장블록연결선(
            Guid.NewGuid().ToString("N"),
            fromTitle.Trim(),
            toTitle.Trim(),
            NormalizeEdgeLabel(label),
            IsCustom: true,
            fromHandle,
            toHandle);

        CustomEdges.Add(edge);
        SelectedEdgeId = edge.Id;
        OnPropertyChanged(nameof(CustomEdges));
        return new DiagramEdgeMutationResult(edge, IsNew: true);
    }

    public void BeginConnection(
        string nodeTitle,
        string? defaultLabel,
        string? guidanceMessage = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeTitle);
        ConnectionStartNodeTitle = nodeTitle;
        SelectedEdgeId = null;
        IsEdgeOptionDockCollapsed = true;
        NewConnectionLabel = NormalizeEdgeLabel(defaultLabel);
        ConnectionMessage = guidanceMessage;
        ConnectionMessageKind = CommunityComposerMessageKind.Info;
    }

    public void BeginHandleDrag(
        string nodeTitle,
        DiagramConnectionHandleKind handle,
        string? defaultLabel,
        string? guidanceMessage = null)
    {
        BeginConnection(nodeTitle, defaultLabel, guidanceMessage);
        SelectedNodeTitle = nodeTitle;
        ActiveHandleDrag = new DiagramHandleDrag(nodeTitle, handle);
        HandleDragMoved = false;
    }

    public bool ConsumeSuppressedHandleClick()
    {
        if (!SuppressNextHandleClick)
        {
            return false;
        }

        SuppressNextHandleClick = false;
        return true;
    }

    public IReadOnlyList<string> SynchronizeNodeOrder(IEnumerable<string> nodeTitles)
    {
        ArgumentNullException.ThrowIfNull(nodeTitles);
        var sourceTitles = nodeTitles.ToList();
        var sourceTitleSet = sourceTitles.ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (NodeOrder.Count == 0 ||
            NodeOrder.Any(title => !sourceTitleSet.Contains(title)) ||
            sourceTitles.Any(title => !NodeOrder.Contains(title, StringComparer.OrdinalIgnoreCase)))
        {
            NodeOrder.Clear();
            NodeOrder.AddRange(sourceTitles);
            OnPropertyChanged(nameof(NodeOrder));
        }

        StackOrder.Synchronize(sourceTitles);
        return NodeOrder;
    }

    public bool CanMoveSelectedNode(int offset)
    {
        if (string.IsNullOrWhiteSpace(SelectedNodeTitle))
        {
            return false;
        }

        var currentIndex = NodeOrder.FindIndex(title =>
            string.Equals(title, SelectedNodeTitle, StringComparison.OrdinalIgnoreCase));
        var nextIndex = currentIndex + offset;
        return currentIndex >= 0 && nextIndex >= 0 && nextIndex < NodeOrder.Count;
    }

    public bool MoveSelectedNode(int offset)
    {
        if (string.IsNullOrWhiteSpace(SelectedNodeTitle))
        {
            SelectedNodeTitle = NodeOrder.FirstOrDefault();
            return false;
        }

        var currentIndex = NodeOrder.FindIndex(title =>
            string.Equals(title, SelectedNodeTitle, StringComparison.OrdinalIgnoreCase));
        var nextIndex = currentIndex + offset;
        if (currentIndex < 0 || nextIndex < 0 || nextIndex >= NodeOrder.Count)
        {
            return false;
        }

        (NodeOrder[currentIndex], NodeOrder[nextIndex]) =
            (NodeOrder[nextIndex], NodeOrder[currentIndex]);
        OnPropertyChanged(nameof(NodeOrder));
        return true;
    }

    public void SynchronizeStackOrder(IEnumerable<string> nodeTitles)
    {
        ArgumentNullException.ThrowIfNull(nodeTitles);
        StackOrder.Synchronize(nodeTitles);
    }

    public bool CanBringSelectedNodeToFront()
        => StackOrder.CanMoveToFront(SelectedNodeTitle);

    public bool CanSendSelectedNodeToBack()
        => StackOrder.CanMoveToBack(SelectedNodeTitle);

    public bool BringSelectedNodeToFront()
        => StackOrder.MoveToFront(SelectedNodeTitle);

    public bool SendSelectedNodeToBack()
        => StackOrder.MoveToBack(SelectedNodeTitle);

    public int GetNodeLayerIndex(string nodeTitle)
        => StackOrder.GetLayerIndex(nodeTitle);

    public void SelectEdge(string edgeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(edgeId);
        SelectedEdgeId = edgeId;
        ConnectionStartNodeTitle = null;
        IsEdgeOptionDockCollapsed = false;
    }

    public void ClearEdgeSelection()
    {
        SelectedEdgeId = null;
        IsEdgeOptionDockCollapsed = true;
    }

    public bool UpdateSelectedEdgeLabel(string? value)
    {
        if (string.IsNullOrWhiteSpace(SelectedEdgeId))
        {
            return false;
        }

        var nextLabel = NormalizeEdgeLabel(value);
        if (SelectedEdgeId.StartsWith("default:", StringComparison.OrdinalIgnoreCase))
        {
            EdgeLabels[SelectedEdgeId] = nextLabel;
            OnPropertyChanged(nameof(EdgeLabels));
            return true;
        }

        var index = CustomEdges.FindIndex(edge =>
            string.Equals(edge.Id, SelectedEdgeId, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            return false;
        }

        CustomEdges[index] = CustomEdges[index] with { Label = nextLabel };
        OnPropertyChanged(nameof(CustomEdges));
        return true;
    }

    public bool UpdateSelectedEdgeStyle(DiagramEdgeStyleKind style)
    {
        if (string.IsNullOrWhiteSpace(SelectedEdgeId))
        {
            return false;
        }

        EdgeStyles[SelectedEdgeId] = style;
        var index = CustomEdges.FindIndex(edge =>
            string.Equals(edge.Id, SelectedEdgeId, StringComparison.OrdinalIgnoreCase));
        if (index >= 0)
        {
            CustomEdges[index] = CustomEdges[index] with { Style = style };
            OnPropertyChanged(nameof(CustomEdges));
        }

        OnPropertyChanged(nameof(EdgeStyles));
        return true;
    }

    public bool DeleteSelectedEdge()
    {
        if (string.IsNullOrWhiteSpace(SelectedEdgeId))
        {
            return false;
        }

        var edgeId = SelectedEdgeId;
        CustomEdges.RemoveAll(edge =>
            string.Equals(edge.Id, edgeId, StringComparison.OrdinalIgnoreCase));
        EdgeLabels.Remove(edgeId);
        EdgeStyles.Remove(edgeId);
        ClearEdgeSelection();
        OnPropertyChanged(nameof(CustomEdges));
        OnPropertyChanged(nameof(EdgeLabels));
        OnPropertyChanged(nameof(EdgeStyles));
        return true;
    }

    public void CancelHandleDrag()
    {
        ActiveHandleDrag = null;
        DragPointer = null;
        HandleDragMoved = false;
        ConnectionStartNodeTitle = null;
    }

    public bool IsLayerVisible(string key, bool isLocked)
        => isLocked || !HiddenLayerKeys.Contains(key);

    public void ToggleLayer(string key, bool isLocked)
    {
        if (isLocked || string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        if (!HiddenLayerKeys.Add(key))
        {
            HiddenLayerKeys.Remove(key);
        }

        OnPropertyChanged(nameof(HiddenLayerKeys));
    }

    private static string NormalizeEdgeLabel(string? value)
        => string.IsNullOrWhiteSpace(value) ? "다음 단계" : value.Trim();
}
