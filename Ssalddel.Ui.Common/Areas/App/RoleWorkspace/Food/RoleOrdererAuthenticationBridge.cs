using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Food;

/// <summary>기존 주문 작성 흐름이 통합 홈의 주문자 자격만 소비하도록 연결합니다.</summary>
public sealed class RoleOrdererAuthenticationBridge(IRoleWorkspaceAccess access) : I주문자앱인증Service
{
    public async Task<주문자앱인증결과> 복원Async(CancellationToken cancellationToken = default)
    {
        await access.EnsureInitializedAsync("orderer", cancellationToken);
        return Current();
    }
    public async Task<주문자앱인증결과> 로그인Async(string userNameOrEmail, string password, CancellationToken cancellationToken = default)
    {
        await access.SignInAsync("orderer", userNameOrEmail, password, cancellationToken);
        return Current();
    }
    public Task 로그아웃Async(CancellationToken cancellationToken = default) => access.SignOutAsync("orderer", cancellationToken);
    public Task 세션만료Async(CancellationToken cancellationToken = default) => access.SignOutAsync("orderer", cancellationToken);
    private 주문자앱인증결과 Current()
    {
        var identity = access.GetIdentity("orderer");
        return new(identity.IsAuthenticated && !string.IsNullOrWhiteSpace(identity.OwnerId)
            ? new(true, identity.OwnerId, null) : 주문자앱세션상태.익명);
    }
}
