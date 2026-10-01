using Hongdal.Contracts.Common.Community;
using Hongdal.Ui.Common.Areas.App.Services;
using Hongdal.Ui.Common.Areas.App.ViewModels;

namespace Hongdal.Tests.Ui.Common;

public sealed class PlatformCommunityDiagramWorkspaceViewModelTests
{
    [Fact]
    public async Task Chat_LoadsHistoryBeforeJoiningRoom()
    {
        var client = new FakeDiagramCollaborationClient
        {
            History =
            [
                new DiagramChatMessageResponse
                {
                    MessageId = "message-1",
                    SenderUserId = "participant-1",
                    SenderDisplayName = "참여자",
                    Message = "흐름을 확인했습니다.",
                    SentAtUtc = DateTime.UtcNow
                }
            ]
        };
        using var viewModel = new PlatformCommunityDiagramChatViewModel(client);
        viewModel.SetContext(CreateContext("room-1"));

        await viewModel.TogglePanelAsync();

        Assert.True(viewModel.IsPanelOpen);
        Assert.Equal(["history:room-1", "join:room-1"], client.Calls);
        Assert.Single(viewModel.Messages);
        Assert.Equal("흐름을 확인했습니다.", viewModel.Messages[0].Message);
        Assert.Equal("SignalR 연결됨", viewModel.ConnectionLabel);
    }

    [Fact]
    public async Task Chat_AddsLocalMessageWhenRealtimeSendIsUnavailable()
    {
        var client = new FakeDiagramCollaborationClient { SendResult = false };
        using var viewModel = new PlatformCommunityDiagramChatViewModel(client);
        viewModel.SetContext(CreateContext("room-local"));
        viewModel.MessageInput = "  같이 확인해 주세요.  ";

        await viewModel.SendAsync();

        Assert.Equal(string.Empty, viewModel.MessageInput);
        var message = Assert.Single(viewModel.Messages);
        Assert.True(message.IsMine);
        Assert.Equal("같이 확인해 주세요.", message.Message);
        Assert.True(viewModel.NeedsFeedScroll is false);
    }

    [Fact]
    public void Canvas_ResetClearsEditingSessionState()
    {
        var viewModel = new PlatformCommunityDiagramCanvasViewModel
        {
            SelectedNodeTitle = "입고",
            ConnectionStartNodeTitle = "입고",
            SelectedEdgeId = "입고->검수",
            ActiveHandleDrag = new("입고", DiagramConnectionHandleKind.Right),
            DragPointer = new(120, 80),
            HandleDragMoved = true,
            SuppressNextHandleClick = true,
            NewConnectionLabel = "검수 요청",
            ConnectionMessage = "연결됨",
            IsEdgeOptionDockCollapsed = false
        };
        viewModel.NodeOrder.AddRange(["입고", "검수"]);
        viewModel.StackOrder.Synchronize(viewModel.NodeOrder);
        viewModel.CustomEdges.Add(new(
            "입고->검수",
            "입고",
            "검수",
            "검수 요청",
            true));
        viewModel.EdgeLabels["입고->검수"] = "검수 요청";
        viewModel.EdgeStyles["입고->검수"] = DiagramEdgeStyleKind.Elbow;

        viewModel.Reset();

        Assert.Empty(viewModel.NodeOrder);
        Assert.Empty(viewModel.StackOrder.NodeTitles);
        Assert.Empty(viewModel.CustomEdges);
        Assert.Empty(viewModel.EdgeLabels);
        Assert.Empty(viewModel.EdgeStyles);
        Assert.Null(viewModel.SelectedNodeTitle);
        Assert.Null(viewModel.ActiveHandleDrag);
        Assert.Equal("다음 단계", viewModel.NewConnectionLabel);
        Assert.True(viewModel.IsEdgeOptionDockCollapsed);
    }

    [Fact]
    public void Canvas_DoesNotHideLockedLayer()
    {
        var viewModel = new PlatformCommunityDiagramCanvasViewModel();

        viewModel.ToggleLayer("structure", isLocked: true);
        viewModel.ToggleLayer("evidence", isLocked: false);

        Assert.True(viewModel.IsLayerVisible("structure", isLocked: true));
        Assert.False(viewModel.IsLayerVisible("evidence", isLocked: false));
    }

    [Fact]
    public void Canvas_AddOrSelectCustomEdgeDoesNotDuplicateSameHandles()
    {
        var viewModel = new PlatformCommunityDiagramCanvasViewModel();

        var added = viewModel.AddOrSelectCustomEdge(
            "입고",
            "검수",
            DiagramConnectionHandleKind.Right,
            DiagramConnectionHandleKind.Left,
            "  검수 요청  ");
        var selected = viewModel.AddOrSelectCustomEdge(
            "입고",
            "검수",
            DiagramConnectionHandleKind.Right,
            DiagramConnectionHandleKind.Left,
            "다른 라벨");

        Assert.True(added.IsNew);
        Assert.False(selected.IsNew);
        Assert.Same(added.Edge, selected.Edge);
        Assert.Equal("검수 요청", added.Edge.Label);
        Assert.Equal(added.Edge.Id, viewModel.SelectedEdgeId);
        Assert.Single(viewModel.CustomEdges);
    }

    [Fact]
    public void Canvas_EditsAndDeletesSelectedCustomEdge()
    {
        var viewModel = new PlatformCommunityDiagramCanvasViewModel();
        var added = viewModel.AddOrSelectCustomEdge(
            "입고",
            "검수",
            DiagramConnectionHandleKind.Bottom,
            DiagramConnectionHandleKind.Top,
            null);

        Assert.True(viewModel.UpdateSelectedEdgeLabel("  검수 완료  "));
        Assert.True(viewModel.UpdateSelectedEdgeStyle(DiagramEdgeStyleKind.Elbow));

        var edited = Assert.Single(viewModel.CustomEdges);
        Assert.Equal("검수 완료", edited.Label);
        Assert.Equal(DiagramEdgeStyleKind.Elbow, edited.Style);
        Assert.Equal(DiagramEdgeStyleKind.Elbow, viewModel.EdgeStyles[added.Edge.Id]);

        Assert.True(viewModel.DeleteSelectedEdge());
        Assert.Empty(viewModel.CustomEdges);
        Assert.Empty(viewModel.EdgeStyles);
        Assert.Null(viewModel.SelectedEdgeId);
        Assert.True(viewModel.IsEdgeOptionDockCollapsed);
    }

    [Fact]
    public void Canvas_ResetConnectionsPreservesNodeLayoutAndLayerSelection()
    {
        var viewModel = new PlatformCommunityDiagramCanvasViewModel();
        viewModel.NodeOrder.AddRange(["입고", "검수"]);
        viewModel.ToggleLayer("evidence", isLocked: false);
        viewModel.AddOrSelectCustomEdge(
            "입고",
            "검수",
            DiagramConnectionHandleKind.Right,
            DiagramConnectionHandleKind.Left,
            null);

        viewModel.ResetConnections();

        Assert.Equal(["입고", "검수"], viewModel.NodeOrder);
        Assert.False(viewModel.IsLayerVisible("evidence", isLocked: false));
        Assert.Empty(viewModel.CustomEdges);
        Assert.Null(viewModel.SelectedEdgeId);
    }

    [Fact]
    public void Canvas_BeginHandleDragCreatesSingleEditingState()
    {
        var viewModel = new PlatformCommunityDiagramCanvasViewModel
        {
            SelectedEdgeId = "previous-edge",
            IsEdgeOptionDockCollapsed = false
        };

        viewModel.BeginHandleDrag(
            "입고",
            DiagramConnectionHandleKind.Right,
            "  다음 처리  ",
            "출력 연결점을 선택하세요.");

        Assert.Equal("입고", viewModel.SelectedNodeTitle);
        Assert.Equal("입고", viewModel.ConnectionStartNodeTitle);
        Assert.Equal(
            new DiagramHandleDrag("입고", DiagramConnectionHandleKind.Right),
            viewModel.ActiveHandleDrag);
        Assert.Equal("다음 처리", viewModel.NewConnectionLabel);
        Assert.Equal("출력 연결점을 선택하세요.", viewModel.ConnectionMessage);
        Assert.Null(viewModel.SelectedEdgeId);
        Assert.True(viewModel.IsEdgeOptionDockCollapsed);

        viewModel.SuppressNextHandleClick = true;
        Assert.True(viewModel.ConsumeSuppressedHandleClick());
        Assert.False(viewModel.ConsumeSuppressedHandleClick());
    }

    [Fact]
    public void Canvas_SynchronizesAndMovesSelectedNodeOrder()
    {
        var viewModel = new PlatformCommunityDiagramCanvasViewModel();
        viewModel.SynchronizeNodeOrder(["입고", "검수", "적재"]);
        viewModel.SelectedNodeTitle = "검수";

        Assert.True(viewModel.CanMoveSelectedNode(-1));
        Assert.True(viewModel.MoveSelectedNode(-1));
        Assert.Equal(["검수", "입고", "적재"], viewModel.NodeOrder);

        viewModel.SynchronizeNodeOrder(["입고", "출고"]);

        Assert.Equal(["입고", "출고"], viewModel.NodeOrder);
    }

    [Fact]
    public void Canvas_ChangesSelectedNodeStackOrder()
    {
        var viewModel = new PlatformCommunityDiagramCanvasViewModel
        {
            SelectedNodeTitle = "입고"
        };
        viewModel.SynchronizeStackOrder(["입고", "검수", "적재"]);

        Assert.True(viewModel.CanBringSelectedNodeToFront());
        Assert.True(viewModel.BringSelectedNodeToFront());
        Assert.Equal(2, viewModel.GetNodeLayerIndex("입고"));
        Assert.True(viewModel.CanSendSelectedNodeToBack());

        Assert.True(viewModel.SendSelectedNodeToBack());
        Assert.Equal(0, viewModel.GetNodeLayerIndex("입고"));
    }

    private static CommunityDiagramChatContext CreateContext(string roomId)
        => new(
            roomId,
            "입고 원장 대화방",
            "봄 상품 입고",
            "warehouse",
            "ledger-1",
            "창고 입고",
            CommunityLedgerTemplateKeys.WarehouseInbound);

    private sealed class FakeDiagramCollaborationClient : IDiagramCollaborationClientService
    {
        public event Func<DiagramChatMessageResponse, Task>? 메시지수신;
        public event Func<DiagramLedgerChangedResponse, Task>? 원장변경수신;
        public event Func<string, Task>? 상태변경;

        public List<string> Calls { get; } = [];
        public IReadOnlyList<DiagramChatMessageResponse> History { get; init; } = [];
        public bool SendResult { get; init; }
        public string 연결상태 => 연결됨 ? "Connected" : "Disconnected";
        public string? 현재사용자Id => "current-user";
        public string 현재사용자표시명 => "나";
        public bool 연결됨 { get; private set; }

        public Task<bool> 방입장Async(
            DiagramRoomJoinRequest request,
            CancellationToken cancellationToken = default)
        {
            Calls.Add($"join:{request.RoomId}");
            연결됨 = true;
            return Task.FromResult(true);
        }

        public Task<IReadOnlyList<DiagramChatMessageResponse>> 메시지목록조회Async(
            string roomId,
            int limit = 80,
            CancellationToken cancellationToken = default)
        {
            Calls.Add($"history:{roomId}");
            return Task.FromResult(History);
        }

        public Task<bool> 메시지전송Async(
            DiagramChatMessageRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(SendResult);

        public Task 연결해제Async(CancellationToken cancellationToken = default)
        {
            연결됨 = false;
            return Task.CompletedTask;
        }

        public Task PublishMessageAsync(DiagramChatMessageResponse message)
            => 메시지수신?.Invoke(message) ?? Task.CompletedTask;

        public Task PublishStatusAsync(string message)
            => 상태변경?.Invoke(message) ?? Task.CompletedTask;

        public Task PublishLedgerChangedAsync(DiagramLedgerChangedResponse message)
            => 원장변경수신?.Invoke(message) ?? Task.CompletedTask;
    }
}
