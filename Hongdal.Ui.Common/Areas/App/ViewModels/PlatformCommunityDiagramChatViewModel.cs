using CommunityToolkit.Mvvm.ComponentModel;
using Hongdal.Contracts.Common.Community;
using Hongdal.Ui.Common.Areas.App.Services;

namespace Hongdal.Ui.Common.Areas.App.ViewModels;

public sealed record CommunityDiagramChatContext(
    string RoomId,
    string RoomName,
    string CurrentLedgerLabel,
    string CommunityId,
    string? LedgerId,
    string DiagramName,
    string LedgerTemplateKey);

public sealed record CommunityDiagramChatMessage(
    string Id,
    string SenderUserId,
    string SenderDisplayName,
    string Message,
    string MessageKind,
    DateTime SentAt,
    bool IsMine,
    bool IsSystem);

public sealed class PlatformCommunityDiagramChatViewModel : ObservableObject, IDisposable
{
    private readonly IDiagramCollaborationClientService _client;
    private readonly List<CommunityDiagramChatMessage> _messages = [];
    private readonly string _localUserId = $"local-{Guid.NewGuid():N}";
    private CommunityDiagramChatContext _context = new(
        "community:cargo-transport:diagram",
        "화물 운송 대화방",
        "현재 원장 선택 전",
        "platform",
        null,
        "화물 운송",
        CommunityLedgerTemplateKeys.CargoTransport);
    private bool _isPanelOpen;
    private bool _isJoining;
    private bool _isHistoryLoading;
    private bool _needsFeedScroll;
    private string _messageInput = string.Empty;
    private string? _notificationMessage;
    private string? _connectedRoomId;
    private string? _historyLoadedRoomId;
    private bool _disposed;

    public PlatformCommunityDiagramChatViewModel(IDiagramCollaborationClientService client)
    {
        _client = client;
        _client.메시지수신 += HandleMessageReceivedAsync;
        _client.상태변경 += HandleStatusChangedAsync;
    }

    public IReadOnlyList<CommunityDiagramChatMessage> Messages => _messages;

    public CommunityDiagramChatContext Context
    {
        get => _context;
        private set => SetProperty(ref _context, value);
    }

    public bool IsPanelOpen
    {
        get => _isPanelOpen;
        set => SetProperty(ref _isPanelOpen, value);
    }

    public bool IsJoining
    {
        get => _isJoining;
        private set
        {
            if (SetProperty(ref _isJoining, value))
            {
                OnPropertyChanged(nameof(ConnectionLabel));
                OnPropertyChanged(nameof(CanSend));
            }
        }
    }

    public bool IsHistoryLoading
    {
        get => _isHistoryLoading;
        private set
        {
            if (SetProperty(ref _isHistoryLoading, value))
            {
                OnPropertyChanged(nameof(CanSend));
            }
        }
    }

    public bool NeedsFeedScroll
    {
        get => _needsFeedScroll;
        private set => SetProperty(ref _needsFeedScroll, value);
    }

    public string MessageInput
    {
        get => _messageInput;
        set
        {
            if (SetProperty(ref _messageInput, value ?? string.Empty))
            {
                OnPropertyChanged(nameof(CanSend));
            }
        }
    }

    public string? NotificationMessage
    {
        get => _notificationMessage;
        private set => SetProperty(ref _notificationMessage, value);
    }

    public string ConnectionLabel
        => IsJoining
            ? "SignalR 연결 중"
            : _client.연결됨
                ? "SignalR 연결됨"
                : "로컬 미리보기";

    public bool CanSend
        => !IsJoining && !IsHistoryLoading && !string.IsNullOrWhiteSpace(MessageInput);

    public void SetContext(CommunityDiagramChatContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (string.IsNullOrWhiteSpace(context.RoomId))
        {
            throw new ArgumentException("대화방 ID가 필요합니다.", nameof(context));
        }

        Context = context;
    }

    public async Task TogglePanelAsync(CancellationToken cancellationToken = default)
    {
        IsPanelOpen = !IsPanelOpen;
        if (!IsPanelOpen)
        {
            return;
        }

        NotificationMessage ??= "대화방을 열었습니다. 메시지 이력과 실시간 연결을 차례로 준비합니다.";
        EnsureDefaultMessages();
        await LoadRoomAsync(cancellationToken);
    }

    public void ClosePanel() => IsPanelOpen = false;

    public void PrepareShare(int nodeCount, int edgeCount)
    {
        IsPanelOpen = true;
        NotificationMessage = $"{nodeCount}개 노드와 {edgeCount}개 연결선이 {Context.RoomName} 공유 payload로 준비되었습니다.";
        AddMessage(new(
            Guid.NewGuid().ToString("N"),
            _localUserId,
            "나",
            $"{nodeCount}개 노드와 {edgeCount}개 연결선을 공유할 준비가 됐습니다.",
            DiagramCollaborationMessageKinds.DiagramNote,
            DateTime.UtcNow,
            true,
            false));
    }

    public async Task LoadRoomAsync(CancellationToken cancellationToken = default)
    {
        await LoadHistoryAsync(cancellationToken);
        await JoinAsync(cancellationToken);
    }

    public async Task SendAsync(CancellationToken cancellationToken = default)
    {
        var message = MessageInput.Trim();
        if (message.Length == 0)
        {
            return;
        }

        MessageInput = string.Empty;
        var sent = await _client.메시지전송Async(new DiagramChatMessageRequest
        {
            RoomId = Context.RoomId,
            Message = message,
            DiagramId = Context.RoomId,
            DiagramName = Context.DiagramName,
            MessageKind = DiagramCollaborationMessageKinds.Text
        }, cancellationToken);

        if (!sent)
        {
            AddMessage(new(
                Guid.NewGuid().ToString("N"),
                _localUserId,
                "나",
                message,
                DiagramCollaborationMessageKinds.Text,
                DateTime.UtcNow,
                true,
                false));
        }
    }

    public void MarkFeedScrolled() => NeedsFeedScroll = false;

    private async Task JoinAsync(CancellationToken cancellationToken)
    {
        if (IsJoining ||
            _client.연결됨 && string.Equals(_connectedRoomId, Context.RoomId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        IsJoining = true;
        try
        {
            var connected = await _client.방입장Async(new DiagramRoomJoinRequest
            {
                RoomId = Context.RoomId,
                CommunityId = Context.CommunityId,
                LedgerId = Context.LedgerId,
                DiagramId = Context.RoomId,
                DiagramName = Context.DiagramName,
                LedgerTemplateKey = Context.LedgerTemplateKey,
                WorkContext = new DiagramWorkContextDto
                {
                    WorkType = Context.LedgerTemplateKey,
                    WorkLabel = Context.DiagramName,
                    AppKey = Context.CommunityId,
                    PrimaryRoute = "/community",
                    PrimaryActionLabel = "다이어그램 보기",
                    Parameters = new Dictionary<string, string>
                    {
                        ["ledgerTemplateKey"] = Context.LedgerTemplateKey,
                        ["roomId"] = Context.RoomId
                    }
                }
            }, cancellationToken);
            _connectedRoomId = connected ? Context.RoomId : null;
            OnPropertyChanged(nameof(ConnectionLabel));
        }
        finally
        {
            IsJoining = false;
        }
    }

    private async Task LoadHistoryAsync(CancellationToken cancellationToken)
    {
        var roomId = Context.RoomId;
        if (string.Equals(_historyLoadedRoomId, roomId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        IsHistoryLoading = true;
        NotificationMessage = "저장된 대화 이력을 먼저 불러오고 있습니다.";
        try
        {
            var messages = await _client.메시지목록조회Async(roomId, 40, cancellationToken);
            if (messages.Count > 0)
            {
                _messages.Clear();
                foreach (var message in messages.OrderBy(item => item.SentAtUtc == default ? DateTime.MinValue : item.SentAtUtc))
                {
                    AddMessage(ToDisplayMessage(message));
                }

                NotificationMessage = $"최근 대화 {messages.Count}개를 먼저 불러왔습니다.";
            }
            else
            {
                NotificationMessage = "저장된 대화가 아직 없어 로컬 미리보기 메시지를 유지합니다.";
            }

            _historyLoadedRoomId = roomId;
        }
        finally
        {
            IsHistoryLoading = false;
        }
    }

    private Task HandleMessageReceivedAsync(DiagramChatMessageResponse message)
    {
        AddMessage(ToDisplayMessage(message));
        return Task.CompletedTask;
    }

    private Task HandleStatusChangedAsync(string message)
    {
        NotificationMessage = message;
        OnPropertyChanged(nameof(ConnectionLabel));
        return Task.CompletedTask;
    }

    private CommunityDiagramChatMessage ToDisplayMessage(DiagramChatMessageResponse message)
    {
        var senderUserId = string.IsNullOrWhiteSpace(message.SenderUserId)
            ? Guid.NewGuid().ToString("N")
            : message.SenderUserId.Trim();
        var currentUserId = _client.현재사용자Id;
        var isMine = !string.IsNullOrWhiteSpace(currentUserId) &&
                     string.Equals(senderUserId, currentUserId, StringComparison.OrdinalIgnoreCase);

        return new(
            string.IsNullOrWhiteSpace(message.MessageId) ? Guid.NewGuid().ToString("N") : message.MessageId,
            senderUserId,
            string.IsNullOrWhiteSpace(message.SenderDisplayName) ? "익명 참여자" : message.SenderDisplayName,
            message.Message,
            message.MessageKind,
            message.SentAtUtc == default ? DateTime.UtcNow : message.SentAtUtc,
            isMine,
            string.Equals(message.MessageKind, DiagramCollaborationMessageKinds.System, StringComparison.OrdinalIgnoreCase));
    }

    private void EnsureDefaultMessages()
    {
        if (_messages.Count > 0)
        {
            return;
        }

        AddMessage(new(
            "sample-partner-1",
            "sample-partner-user",
            "익명 참여자",
            "다이어그램을 보면서 상차와 하차 증빙 위치를 같이 확인할게요.",
            DiagramCollaborationMessageKinds.Text,
            DateTime.UtcNow.AddMinutes(-2),
            false,
            false));
        AddMessage(new(
            "sample-me-1",
            _localUserId,
            "나",
            "좋아요. 저는 결제/정산 노드까지 이어지는 흐름을 보고 있습니다.",
            DiagramCollaborationMessageKinds.Text,
            DateTime.UtcNow.AddMinutes(-1),
            true,
            false));
    }

    private void AddMessage(CommunityDiagramChatMessage message)
    {
        if (_messages.Any(item => string.Equals(item.Id, message.Id, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        _messages.Add(message);
        if (_messages.Count > 80)
        {
            _messages.RemoveRange(0, _messages.Count - 80);
        }

        NeedsFeedScroll = IsPanelOpen;
        OnPropertyChanged(nameof(Messages));
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _client.메시지수신 -= HandleMessageReceivedAsync;
        _client.상태변경 -= HandleStatusChangedAsync;
        _ = _client.연결해제Async();
        GC.SuppressFinalize(this);
    }
}
