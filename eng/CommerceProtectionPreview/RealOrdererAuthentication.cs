using Ssalddel.Ui.Common.Areas.App.Services;

namespace CommerceProtectionPreview;

/// <summary>준비된 합성 계정의 실제 서버 세션을 공유 주문 화면에 연결합니다. 제품 로그인 UI 증거가 아닙니다.</summary>
public sealed class RealOrdererAuthentication(RealApiSession session) : I주문자앱인증Service
{
    public async Task<주문자앱인증결과> 복원Async(CancellationToken cancellationToken = default)
    {
        await session.EnsureInitializedAsync("orderer", cancellationToken);
        return new(new(true, session.AuthenticationOwnerId, "시험 주문자"));
    }
    public Task<주문자앱인증결과> 로그인Async(string name, string password, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("준비된 시험 세션만 사용합니다.");
    public Task 로그아웃Async(CancellationToken cancellationToken = default) => session.SignOutAsync("orderer", cancellationToken);
}
