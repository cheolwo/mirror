using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Ssalddel.Ui.Common.Areas.App.Services;

public enum 역할앱생명주기단계
{
    시작중,
    활성,
    일시정지,
    재개중,
    종료중
}

/// <summary>
/// MAUI 운영체제 생명주기 신호를 역할 화면이 이해할 수 있는 짧은 상태로 투영합니다.
/// 업무 상태나 서버 연결 성공을 확정하지 않습니다.
/// </summary>
public sealed class 역할앱생명주기State : INotifyPropertyChanged
{
    private readonly object _gate = new();
    private 역할앱생명주기단계 _단계 = 역할앱생명주기단계.시작중;
    private bool _연결가능;
    private string _플랫폼 = "앱";
    private DateTimeOffset _변경시각 = DateTimeOffset.UtcNow;

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action? Changed;

    public 역할앱생명주기단계 단계 { get { lock (_gate) return _단계; } }
    public bool 연결가능 { get { lock (_gate) return _연결가능; } }
    public string 플랫폼 { get { lock (_gate) return _플랫폼; } }
    public DateTimeOffset 변경시각 { get { lock (_gate) return _변경시각; } }

    public string 사용자안내 => 단계 switch
    {
        역할앱생명주기단계.시작중 => "준비 중",
        역할앱생명주기단계.재개중 => "다시 여는 중",
        역할앱생명주기단계.일시정지 => "잠시 멈춤",
        역할앱생명주기단계.종료중 => "종료 중",
        _ when !연결가능 => "네트워크 없음",
        _ => "앱 활성"
    };

    public string 상태Code => 단계 switch
    {
        역할앱생명주기단계.시작중 => "starting",
        역할앱생명주기단계.재개중 => "resuming",
        역할앱생명주기단계.일시정지 => "paused",
        역할앱생명주기단계.종료중 => "stopping",
        _ when !연결가능 => "offline",
        _ => "ready"
    };

    public string 상태색상Hex => 상태Code switch
    {
        "ready" => "#0F9F6E",
        "offline" => "#B45309",
        "paused" => "#64748B",
        "stopping" => "#64748B",
        _ => "#2563EB"
    };

    public void 초기화(string? platform, bool connected)
    {
        lock (_gate)
        {
            _플랫폼 = string.IsNullOrWhiteSpace(platform) ? "앱" : platform.Trim();
            _연결가능 = connected;
            _변경시각 = DateTimeOffset.UtcNow;
        }

        NotifyChanged();
    }

    public void 전환(역할앱생명주기단계 stage)
    {
        lock (_gate)
        {
            if (_단계 == stage)
            {
                return;
            }

            _단계 = stage;
            _변경시각 = DateTimeOffset.UtcNow;
        }

        NotifyChanged();
    }

    public void 연결상태변경(bool connected)
    {
        lock (_gate)
        {
            if (_연결가능 == connected)
            {
                return;
            }

            _연결가능 = connected;
            _변경시각 = DateTimeOffset.UtcNow;
        }

        NotifyChanged();
    }

    /// <summary>
    /// 운영체제가 앱을 재개했다는 짧은 피드백을 준 뒤 전경 상태로 돌아갑니다.
    /// 인증 복원이나 서버 데이터 준비 완료를 의미하지 않습니다.
    /// </summary>
    public async Task 재개표시후활성Async(
        TimeSpan? displayDuration = null,
        CancellationToken cancellationToken = default)
    {
        전환(역할앱생명주기단계.재개중);
        await Task.Delay(displayDuration ?? TimeSpan.FromMilliseconds(450), cancellationToken);

        lock (_gate)
        {
            if (_단계 != 역할앱생명주기단계.재개중)
            {
                return;
            }

            _단계 = 역할앱생명주기단계.활성;
            _변경시각 = DateTimeOffset.UtcNow;
        }

        NotifyChanged();
    }

    private void NotifyChanged()
    {
        OnPropertyChanged(string.Empty);
        Changed?.Invoke();
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
