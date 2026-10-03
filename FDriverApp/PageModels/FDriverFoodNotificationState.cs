using FDriverApp.Services;

namespace FDriverApp.PageModels;

public sealed class FDriverFoodNotificationState
{
    private readonly IFDriverFoodNotificationService _service;
    private readonly HashSet<string> _seen = new(StringComparer.Ordinal);
    private readonly HashSet<string> _unread = new(StringComparer.Ordinal);
    private string? _userId;
    private string? _publishedOfferId;
    public string? LatestOfferId { get; private set; }
    public int UnreadCount => _unread.Count;

    public FDriverFoodNotificationState(IFDriverFoodNotificationService service) => _service = service;

    public void BindAccount(string? userId)
    {
        userId = string.IsNullOrWhiteSpace(userId) ? null : userId.Trim();
        if (string.Equals(_userId, userId, StringComparison.Ordinal)) return;
        _seen.Clear(); _unread.Clear(); LatestOfferId = null; _publishedOfferId = null;
        _service.Cancel();
        _userId = userId;
        if (userId is null) { _service.ClearAccount(); return; }
        var saved = _service.ReadReceipt();
        if (saved is not null && string.Equals(saved.UserId, userId, StringComparison.Ordinal))
            _seen.UnionWith(saved.SeenOfferIds.TakeLast(100));
        else
        {
            if (_service.ReadOpenTarget() is { } target
                && !string.Equals(target.UserId, userId, StringComparison.Ordinal)) _service.ClearOpenTarget();
            _service.WriteReceipt(new(userId, []));
        }
    }

    public void Reconcile(IReadOnlyList<DeliveryTicketPreview> recommendations, DateTime utcNow)
    {
        if (_userId is null) { Clear(); return; }
        var valid = recommendations.Where(x => x.CanAccept && x.ExpiresAtUtc is { } expiry && expiry > utcNow)
            .GroupBy(x => x.TicketId, StringComparer.Ordinal).Select(x => x.First()).ToArray();
        var ids = valid.Select(x => x.TicketId).ToHashSet(StringComparer.Ordinal);
        _unread.IntersectWith(ids);
        if (LatestOfferId is not null && !ids.Contains(LatestOfferId)) LatestOfferId = null;
        if (_publishedOfferId is not null && !ids.Contains(_publishedOfferId))
        {
            _service.Cancel(); _publishedOfferId = null;
        }
        var incoming = valid.Where(x => _seen.Add(x.TicketId)).ToArray();
        foreach (var offer in incoming) _unread.Add(offer.TicketId);
        if (incoming.Length > 0)
        {
            LatestOfferId = incoming[^1].TicketId;
            // 교체 가능한 OS 알림 하나로 표시하고 같은 제안의 주기 조회에는 소리를 반복하지 않는다.
            if (_service.Publish(_userId, LatestOfferId, incoming[^1].ExpiresAtUtc!.Value))
                _publishedOfferId = LatestOfferId;
        }
        var receiptChanged = incoming.Length > 0;
        if (_seen.Count > 100)
        {
            var retained = valid.Select(x => x.TicketId).Concat(_seen).Distinct(StringComparer.Ordinal).Take(100).ToArray();
            _seen.Clear(); _seen.UnionWith(retained);
            receiptChanged = true;
        }
        if (receiptChanged) _service.WriteReceipt(new(_userId, _seen.ToArray()));
    }

    public void MarkRead()
    {
        _unread.Clear(); LatestOfferId = null; _publishedOfferId = null; _service.Cancel();
    }

    public void Clear()
    {
        _userId = null; _seen.Clear(); _unread.Clear(); LatestOfferId = null; _publishedOfferId = null;
        _service.ClearAccount();
    }
}
