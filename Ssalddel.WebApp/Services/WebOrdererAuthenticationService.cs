using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.WebApp.Services;

/// <summary>웹 인증 세션을 공용 주문자 인증 계약으로 변환하는 host adapter입니다.</summary>
public sealed class WebOrdererAuthenticationService(
    WebAuthSessionService session,
    IFoodOrderPendingSubmissionStore pendingStore) : I주문자앱인증Service
{
    public async Task<주문자앱인증결과> 복원Async(CancellationToken cancellationToken = default)
    {
        try
        {
            await session.RestoreAsync(cancellationToken);
            return new 주문자앱인증결과(CurrentSession());
        }
        catch (Exception ex)
        {
            return new 주문자앱인증결과(주문자앱세션상태.익명, ex.Message);
        }
    }

    public async Task<주문자앱인증결과> 로그인Async(
        string userNameOrEmail,
        string password,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await session.LoginAsync(userNameOrEmail, password, cancellationToken);
            return new 주문자앱인증결과(CurrentSession());
        }
        catch (Exception ex)
        {
            return new 주문자앱인증결과(주문자앱세션상태.익명, ex.Message);
        }
    }

    public async Task 로그아웃Async(CancellationToken cancellationToken = default)
    {
        await session.ClearAsync(cancellationToken);
        var pending = await pendingStore.LoadAsync(cancellationToken);
        if (pending is not null) await pendingStore.ClearAsync(pending.Request.클라이언트요청Id, cancellationToken);
    }

    public Task 세션만료Async(CancellationToken cancellationToken = default)
        => session.ClearAsync(cancellationToken);

    private 주문자앱세션상태 CurrentSession()
        => session.IsLoggedIn
            ? new 주문자앱세션상태(true, session.UserId, session.UserName)
            : 주문자앱세션상태.익명;
}
