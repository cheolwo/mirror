namespace Ssalddel.Ui.Common.Areas.App.Services;

public sealed record 주문자앱세션상태(
    bool 로그인됨,
    string? UserId,
    string? UserName)
{
    public static 주문자앱세션상태 익명 { get; } = new(false, null, null);

    public string 사용자표시
        => string.IsNullOrWhiteSpace(UserName) ? "로그인 사용자" : UserName.Trim();
}

public sealed record 주문자앱인증결과(
    주문자앱세션상태 세션,
    string? 오류메시지 = null)
{
    public bool 성공 => string.IsNullOrWhiteSpace(오류메시지);
}

/// <summary>공용 주문 화면이 플랫폼별 토큰 저장소와 로그인 HTTP 구현을 알지 않도록 분리합니다.</summary>
public interface I주문자앱인증Service
{
    Task<주문자앱인증결과> 복원Async(CancellationToken cancellationToken = default);

    Task<주문자앱인증결과> 로그인Async(
        string userNameOrEmail,
        string password,
        CancellationToken cancellationToken = default);

    Task 로그아웃Async(CancellationToken cancellationToken = default);

    Task 세션만료Async(CancellationToken cancellationToken = default) => 로그아웃Async(cancellationToken);
}

internal sealed class 미구성주문자앱인증Service : I주문자앱인증Service
{
    private const string Message = "이 클라이언트에는 주문자 로그인 저장소가 연결되지 않았습니다.";

    public Task<주문자앱인증결과> 복원Async(CancellationToken cancellationToken = default)
        => Task.FromResult(new 주문자앱인증결과(주문자앱세션상태.익명, Message));

    public Task<주문자앱인증결과> 로그인Async(
        string userNameOrEmail,
        string password,
        CancellationToken cancellationToken = default)
        => Task.FromResult(new 주문자앱인증결과(주문자앱세션상태.익명, Message));

    public Task 로그아웃Async(CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
