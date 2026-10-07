using Ssalddel.Client.Infrastructure.Security;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace OrdererApp.Services;

/// <summary>주문자 앱의 저장 세션 복원·갱신과 공용 인증 계약 연결만 담당합니다.</summary>
public sealed class OrdererSessionService(
    ClientAuthSession session,
    OrdererAuthApiService authApi,
    IFoodOrderPendingSubmissionStore pendingStore) : I주문자앱인증Service
{
    public async Task<주문자앱인증결과> 복원Async(
        CancellationToken cancellationToken = default)
    {
        var state = await session.RestoreAsync(cancellationToken);
        if (state == ClientAuthSessionRestoreState.RefreshRequired)
        {
            var refresh = await authApi.갱신Async(
                session.UserId ?? string.Empty,
                session.RefreshToken ?? string.Empty,
                cancellationToken);
            if (!refresh.성공)
            {
                // HTTP 경계가 원래 revision만 조건부 정리합니다. 늦은 실패로 새 계정을 지우지 않습니다.
                return new 주문자앱인증결과(CurrentSession(), refresh.오류메시지);
            }
        }

        return new 주문자앱인증결과(CurrentSession());
    }

    public async Task<주문자앱인증결과> 로그인Async(
        string userNameOrEmail,
        string password,
        CancellationToken cancellationToken = default)
    {
        var result = await authApi.로그인Async(userNameOrEmail, password, cancellationToken);
        return result.성공
            ? new 주문자앱인증결과(CurrentSession())
            : new 주문자앱인증결과(CurrentSession(), result.오류메시지);
    }

    public async Task 로그아웃Async(CancellationToken cancellationToken = default)
    {
        var (owner, revision) = session.CaptureIdentity();
        await session.TryClearAsync(revision, cancellationToken);
        var pending = await pendingStore.LoadAsync(cancellationToken);
        if (pending is not null && string.Equals(pending.OwnerId, owner, StringComparison.Ordinal))
            await pendingStore.ClearAsync(pending.Request.클라이언트요청Id, cancellationToken);
    }

    public Task 세션만료Async(CancellationToken cancellationToken = default) => session.ClearAsync(cancellationToken);

    private 주문자앱세션상태 CurrentSession()
        => session.IsAuthenticated
            ? new 주문자앱세션상태(true, session.UserId, session.UserName)
            : 주문자앱세션상태.익명;
}
