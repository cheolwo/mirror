using Ssalddel.Ui.Common.Areas.App.ViewModels;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.Ui.Common.Areas.App.Components.Community;

public partial class PlatformCommunityHome
{
    private DiagramStageSurface? diagramStageSurface;

    private DiagramStageSurface DiagramStage
        => diagramStageSurface ??= new(this);

    public sealed class DiagramStageSurface
    {
        private readonly PlatformCommunityHome owner;

        internal DiagramStageSurface(PlatformCommunityHome owner)
        {
            this.owner = owner;
        }

        public PlatformCommunityDiagramCanvasViewModel Canvas => owner.DiagramCanvas;

        public IReadOnlyList<다이어그램레이어정의> Layers => PlatformCommunityDiagramCatalog.All;

        public IReadOnlyList<PlatformDiagramPaletteBlock> FormNodes => DiagramFormPaletteBlocks;

        public IReadOnlyList<원장블록노드> FlowNodes
            => owner.정렬된원장블록노드목록가져오기(owner.선택원장블록흐름도);

        public IReadOnlyList<원장블록연결선> BuildEdges(IReadOnlyList<원장블록노드> nodes)
            => owner.BuildDiagramEdges(nodes);

        public PlatformCommunityDiagramDesktopCanvasPresentation BuildDesktopPresentation(
            IReadOnlyList<원장블록노드> nodes,
            IReadOnlyList<원장블록연결선> edges)
            => owner.BuildDesktopDiagramPresentation(nodes, edges, DiagramModeCanvasMinHeight);

        public IReadOnlyList<PlatformCommunityDiagramMobileStep> BuildMobileSteps(
            IReadOnlyList<원장블록노드> nodes,
            IReadOnlyList<원장블록연결선> edges)
            => owner.BuildMobileDiagramSteps(nodes, edges);

        public 원장블록노드? SelectedNode => owner.선택원장블록노드;

        public string? ConnectionStartNodeTitle => owner.connectionStartNodeTitle;

        public bool CanMoveBackward => owner.CanMove선택원장블록노드(-1);

        public bool CanMoveForward => owner.CanMove선택원장블록노드(1);

        public bool CanBringToFront => owner.CanBringSelectedDiagramNodeToFront();

        public bool CanSendToBack => owner.CanSendSelectedDiagramNodeToBack();

        public bool CanStartConnection
            => SelectedNode is not null && CanStartDiagramConnection(SelectedNode);

        public PlatformCommunityWarehouseProxyViewModel WarehouseProxy => owner.WarehouseProxy;

        public PlatformCommunityDiagramChatViewModel DiagramChat => owner.DiagramChat;

        public CommunityDiagramChatContext ChatContext => owner.BuildDiagramChatContext();

        public void Close() => owner.CloseDiagramSurface();

        public void MoveBackward() => owner.Move선택원장블록노드(-1);

        public void MoveForward() => owner.Move선택원장블록노드(1);

        public void MoveNode(int offset) => owner.Move선택원장블록노드(offset);

        public void BringToFront() => owner.BringSelectedDiagramNodeToFront();

        public void SendToBack() => owner.SendSelectedDiagramNodeToBack();

        public void AddFormNode(PlatformDiagramPaletteBlock block) => owner.AddPaletteBlockToCanvas(block);

        public void StartConnection() => owner.StartDiagramEdgeFromSelectedNode();

        public void Reset() => owner.원장블록흐름도배치초기화();

        public void CancelConnection() => owner.CancelDiagramEdgeCreation();

        public void SelectEdge(원장블록연결선 edge) => owner.SelectDiagramEdge(edge);

        public void SelectNode(원장블록노드 node) => owner.원장블록노드클릭처리(node);

        public Task OpenNodeContextAsync(원장블록노드 node)
            => owner.원장블록노드컨텍스트메뉴처리Async(node);

        public void OpenNodeDetail(원장블록노드 node) => owner.OpenNodeDetailPanel(node);

        public void BeginHandleDrag(PlatformCommunityDiagramHandlePointerStart request)
            => owner.BeginDiagramHandlePointerDrag(request);

        public void CompleteHandlePointerUp(PlatformCommunityDiagramDesktopHandle handle)
            => owner.CompleteDiagramHandlePointerUp(handle);

        public void HandleDesktopHandleClick(PlatformCommunityDiagramDesktopHandle handle)
            => owner.HandleDiagramDesktopHandleClick(handle);

        public void ChangeDragPointer(DiagramDragPoint point)
            => owner.HandleDiagramDragPointerChanged(point);

        public void CompleteHandleDrop(DiagramHandleHit? hit)
            => owner.CompleteDiagramHandleDrop(hit);

        public void ChangeEdgeLabel(string value) => owner.UpdateSelectedDiagramEdgeLabel(value);

        public void ChangeEdgeStyle(DiagramEdgeStyleKind style) => owner.UpdateSelectedDiagramEdgeStyle(style);

        public void DeleteEdge() => owner.DeleteSelectedDiagramEdge();

        public void OpenWarehouseWorkspace(string targetUrl)
            => owner.NavigateToWarehouseProxyWorkspace(targetUrl);

        public void OpenSelectedWork() => owner.NavigateToNodeDetailPage();

        public void PrepareCommunityDraft(
            IReadOnlyList<원장블록노드> nodes,
            IReadOnlyList<원장블록연결선> edges)
            => owner.다이어그램커뮤니티초안준비(nodes, edges);
    }
}
