namespace RestaurantDeskApp.Services;

// 인증 복귀용 주문 번호와 계정만 메모리에 보존한다. 입력/토큰/업무 요청은 저장하지 않는다.
public sealed class RestaurantOrderReturnContext
{
    private readonly object gate = new();
    private string? owner;
    private string? orderNo;
    private bool returnAfterLogin;

    public void TrackOrder(string? ownerId, string? requestedOrderNo)
    {
        lock (gate)
        {
            owner = Clean(ownerId);
            orderNo = Clean(requestedOrderNo);
            returnAfterLogin = false;
        }
    }

    public void SuspendForLogin(string? ownerId, string? requestedOrderNo)
    {
        lock (gate)
        {
            var requestedOwner = Clean(ownerId);
            if (requestedOwner is not null && owner is not null
                && !string.Equals(owner, requestedOwner, StringComparison.Ordinal))
                return;
            owner ??= requestedOwner;
            orderNo = Clean(requestedOrderNo);
            returnAfterLogin = orderNo is not null;
        }
    }

    public void EndSession(bool explicitLogout, string? endingOwner)
    {
        lock (gate)
        {
            if (explicitLogout) { Clear(); return; }
            var currentOwner = Clean(endingOwner);
            if (currentOwner is not null && owner is not null
                && !string.Equals(owner, currentOwner, StringComparison.Ordinal))
                return;
            owner ??= currentOwner;
            returnAfterLogin = orderNo is not null;
        }
    }

    public string? ConsumeReturnRoute(string? signedInOwner)
    {
        lock (gate)
        {
            var currentOwner = Clean(signedInOwner);
            if (currentOwner is null) return null;
            var route = returnAfterLogin && orderNo is not null
                && (owner is null || string.Equals(owner, currentOwner, StringComparison.Ordinal))
                    ? $"/orders/{Uri.EscapeDataString(orderNo)}" : null;
            Clear();
            return route;
        }
    }

    public void Clear()
    {
        lock (gate)
        {
            owner = null;
            orderNo = null;
            returnAfterLogin = false;
        }
    }

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
