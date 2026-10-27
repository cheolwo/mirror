using DriverApp.Models.Driver;

namespace DriverApp.Services;

/// <summary>전경의 정본 조회 수명만 소유합니다. 게시 이력과 설정 검증은 기존 알림 서비스가 공유합니다.</summary>
public sealed class DriverForegroundRecommendationSignal(IAuthSession session,
    IDriverCargoRecommendationAlertService alerts) : IDisposable
{
    private static readonly TimeSpan PublishTimeout = TimeSpan.FromSeconds(10);
    private readonly object _gate = new();
    private RefreshTicket? _current;
    private bool _active;

    public sealed class RefreshTicket
    {
        internal RefreshTicket(string? ownerId, long revision)
        { OwnerId = ownerId; Revision = revision; }
        internal string? OwnerId { get; set; }
        internal long Revision { get; set; }
        internal bool Started { get; set; }
        internal CancellationTokenSource Lifetime { get; } = new();
    }

    public void Resume()
    {
        lock (_gate)
        {
            if (_active) return;
            _active = true;
            session.Changed += OnSessionChanged;
        }
    }

    public RefreshTicket? BeginRefresh()
    {
        lock (_gate)
        {
            InvalidateCore();
            if (!_active) return null;
            return _current = new(session.IsAuthenticated ? session.UserId : null, session.SessionRevision);
        }
    }

    public async Task<bool> TryPublishAsync(RefreshTicket? ticket, DriverRequestItem? request)
    {
        string owner;
        long revision;
        CancellationToken token;
        lock (_gate)
        {
            var currentOwner = session.UserId;
            if (!_active || ticket is null || !ReferenceEquals(ticket, _current) || ticket.Started
                || ticket.Lifetime.IsCancellationRequested || request is null || !session.IsAuthenticated
                || string.IsNullOrWhiteSpace(currentOwner)) return false;
            // 첫 정본 조회 중 세션 복구가 끝난 경우에만 현재 계정에 결속합니다.
            if (ticket.OwnerId is null) { ticket.OwnerId = currentOwner; ticket.Revision = session.SessionRevision; }
            if (ticket.OwnerId != currentOwner || ticket.Revision != session.SessionRevision)
                return false;
            owner = currentOwner;
            revision = ticket.Revision;
            ticket.Started = true;
            token = ticket.Lifetime.Token;
        }

        using var bounded = CancellationTokenSource.CreateLinkedTokenSource(token);
        bounded.CancelAfter(PublishTimeout);
        try
        {
            return await alerts.TryPublishAsync(owner, revision, request, bounded.Token).WaitAsync(bounded.Token);
        }
        catch (Exception)
        {
            // 알림의 지연·취소·실패를 정본 업무 지도 조회 실패로 바꾸지 않습니다.
            return false;
        }
    }

    public void Invalidate()
    {
        lock (_gate) InvalidateCore();
    }

    public void Pause()
    {
        lock (_gate)
        {
            if (_active) session.Changed -= OnSessionChanged;
            _active = false;
            InvalidateCore();
        }
    }

    private void OnSessionChanged()
    {
        lock (_gate)
        {
            if (_current?.OwnerId is not { } owner) return;
            if (session.IsAuthenticated && session.UserId == owner && session.SessionRevision == _current.Revision) return;
            InvalidateCore();
        }
    }

    private void InvalidateCore()
    {
        if (_current is not { } current) return;
        _current = null;
        current.Lifetime.Cancel();
        current.Lifetime.Dispose();
        // 전역 Cancel은 호출하지 않습니다. 다른 Push 경로에서 새로 게시한 알림을 지우면 안 됩니다.
    }

    public void Dispose() => Pause();
}
