using Ssalddel.Contracts.Food;

namespace RestaurantDeskApp.Services;

// 메뉴 입력과 미확정 요청만 앱 메모리에 보존한다. 자격 정보나 주문 목록은 저장하지 않는다.
public sealed record RestaurantMenuDraft(
    bool Editing, long? EditingId, long Revision, int DisplayOrder,
    string Name, string Description, string ImageUrl, decimal Price,
    bool Published, bool SoldOut, 음식점메뉴등록요청? PendingCreate);

public sealed class RestaurantMenuDraftStore : IDisposable
{
    private readonly RestaurantAuthService auth;
    private readonly object gate = new();
    private string? owner;
    private long generation;
    private RestaurantMenuDraft? draft;
    private bool returnAfterLogin;

    public RestaurantMenuDraftStore(RestaurantAuthService auth)
    {
        this.auth = auth;
        auth.SessionEnding += EndSession;
    }

    public long BindOwner(string ownerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        lock (gate)
        {
            if (!string.Equals(owner, ownerId, StringComparison.Ordinal))
            {
                Clear();
                owner = ownerId;
            }
            return generation;
        }
    }

    public bool IsCurrent(string ownerId, long expectedGeneration)
    {
        lock (gate) return generation == expectedGeneration && string.Equals(owner, ownerId, StringComparison.Ordinal);
    }

    public RestaurantMenuDraft? Restore(string ownerId, long expectedGeneration)
    {
        lock (gate)
        {
            if (!IsCurrent(ownerId, expectedGeneration)) return null;
            returnAfterLogin = false;
            return draft;
        }
    }

    public void Capture(string ownerId, long expectedGeneration, RestaurantMenuDraft value)
    {
        lock (gate)
            if (IsCurrent(ownerId, expectedGeneration)) draft = value;
    }

    public string? GetReturnRoute(string ownerId)
    {
        // 다른 계정으로 로그인하면 이전 계정 입력을 폐기하고 기본 업무로 돌아간다.
        lock (gate)
        {
            BindOwner(ownerId);
            return returnAfterLogin ? "/menus" : null;
        }
    }

    private void EndSession(bool explicitLogout)
    {
        lock (gate)
        {
            if (explicitLogout) Clear();
            else if (owner is not null) returnAfterLogin = true;
        }
    }

    public void Clear()
    {
        lock (gate)
        {
            generation++;
            owner = null;
            draft = null;
            returnAfterLogin = false;
        }
    }

    public void Dispose()
    {
        auth.SessionEnding -= EndSession;
        Clear();
    }
}
