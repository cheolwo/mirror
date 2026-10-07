using CommunityToolkit.Mvvm.ComponentModel;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.Ui.Common.Areas.App.ViewModels;

/// <summary>업무 종류와 무관하게 주문자 앱 세션의 복원·로그인·로그아웃 표시 상태만 관리합니다.</summary>
public sealed partial class 주문자앱인증ViewModel(
    I주문자앱인증Service service) : 업무작업ViewModelBase
{
    private long _세션세대;
    private Task<bool>? _인증작업;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(로그인됨))]
    [NotifyPropertyChangedFor(nameof(현재사용자표시))]
    public partial 주문자앱세션상태 세션 { get; private set; } = 주문자앱세션상태.익명;

    [ObservableProperty]
    public partial bool 초기화됨 { get; private set; }

    public bool 로그인됨 => 세션.로그인됨;
    public string 현재사용자표시 => 세션.사용자표시;

    public Task<bool> 복원Async(CancellationToken cancellationToken = default)
        => 인증실행Async(
            token => service.복원Async(token),
            "주문자 로그인 세션을 확인했습니다.",
            cancellationToken,
            markInitialized: true);

    public Task<bool> 로그인Async(
        string userNameOrEmail,
        string password,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userNameOrEmail) || string.IsNullOrWhiteSpace(password))
        {
            return Task.FromResult(유효성실패("아이디와 비밀번호를 입력해 주세요."));
        }

        return 인증실행Async(
            token => service.로그인Async(userNameOrEmail.Trim(), password, token),
            "주문자 계정으로 로그인했습니다.",
            cancellationToken,
            markInitialized: true);
    }

    public Task<bool> 로그아웃Async(CancellationToken cancellationToken = default)
        => 인증종료Async(false, cancellationToken);

    public Task<bool> 세션만료Async(CancellationToken cancellationToken = default)
        => 인증종료Async(true, cancellationToken);

    private async Task<bool> 인증종료Async(bool expired, CancellationToken cancellationToken)
    {
        var generation = ++_세션세대;
        작업취소();
        // 저장소 삭제가 실패하거나 늦어도 개인 화면은 즉시 숨깁니다.
        세션 = 주문자앱세션상태.익명;
        초기화됨 = true;
        if (_인증작업 is { } pending) await pending;
        if (generation != _세션세대) return false;
        return await 작업실행Async(
            token => expired ? service.세션만료Async(token) : service.로그아웃Async(token),
            expired ? "다시 로그인해 주세요." : "로그아웃했습니다.",
            cancellationToken,
            _ => expired ? "로그인 상태를 정리하지 못했습니다. 다시 로그인해 주세요."
                : "로그아웃 저장 정보를 정리하지 못했습니다. 다시 시도해 주세요.");
    }

    private Task<bool> 인증실행Async(
        Func<CancellationToken, Task<주문자앱인증결과>> action,
        string successMessage,
        CancellationToken cancellationToken,
        bool markInitialized)
    {
        var generation = ++_세션세대;
        return _인증작업 = 작업실행Async(
            async token =>
            {
                var result = await action(token);
                token.ThrowIfCancellationRequested();
                if (generation != _세션세대) return;
                if (!result.성공)
                {
                    throw new InvalidOperationException(
                        result.오류메시지 ?? "주문자 로그인 상태를 확인하지 못했습니다.");
                }

                세션 = result.세션;
                if (markInitialized)
                {
                    초기화됨 = true;
                }
            },
            successMessage,
            cancellationToken,
            ex => string.IsNullOrWhiteSpace(ex.Message)
                ? "주문자 로그인 상태를 확인하지 못했습니다."
                : ex.Message);
    }
}
