using CommunityToolkit.Mvvm.ComponentModel;
using Ssalddel.Client.Infrastructure.Security;
using WarehouseManagerApp.Services;

namespace WarehouseManagerApp.ViewModels.Warehouse;

public enum 창고인증안내수준
{
    정보,
    성공,
    주의,
    오류
}

/// <summary>창고 앱 로그인 세션 복원·갱신·로그아웃 상태만 관리합니다.</summary>
public sealed class 창고로그인ViewModel : ObservableObject
{
    private readonly ClientAuthSession _session;
    private readonly WarehouseAuthApiService _authApi;
    private readonly WarehouseAccessPolicyService _accessPolicy;
    private bool _처리중;
    private string? _안내메시지;
    private 창고인증안내수준 _안내수준 = 창고인증안내수준.정보;

    public 창고로그인ViewModel(
        ClientAuthSession session,
        WarehouseAuthApiService authApi,
        WarehouseAccessPolicyService accessPolicy)
    {
        _session = session;
        _authApi = authApi;
        _accessPolicy = accessPolicy;
    }

    public bool 처리중
    {
        get => _처리중;
        private set => SetProperty(ref _처리중, value);
    }

    public string? 안내메시지
    {
        get => _안내메시지;
        private set => SetProperty(ref _안내메시지, value);
    }

    public 창고인증안내수준 안내수준
    {
        get => _안내수준;
        private set => SetProperty(ref _안내수준, value);
    }

    public bool 로그인됨 => _session.IsAuthenticated;
    public string? 현재사용자Id => _session.UserId;
    public bool 창고업무접근가능 => _accessPolicy.CanAccessWarehouseOperations(_session);
    public string 현재사용자표시 => _session.UserName ?? "미로그인";

    public async Task 초기화Async(CancellationToken cancellationToken = default)
    {
        if (처리중)
        {
            return;
        }

        처리중 = true;
        안내메시지 = null;
        try
        {
            var restoreState = await _session.RestoreAsync(cancellationToken);
            if (restoreState == ClientAuthSessionRestoreState.RefreshRequired)
            {
                var refreshResult = await _authApi.RefreshAsync(
                    _session.UserId ?? string.Empty,
                    _session.RefreshToken ?? string.Empty,
                    cancellationToken);
                if (!refreshResult.IsSuccess)
                {
                    // 갱신 HTTP 경계가 원 revision만 정리하며 뒤늦은 실패는 새 계정을 지우지 않습니다.
                    안내수준 = 창고인증안내수준.주의;
                    안내메시지 = refreshResult.ErrorMessage;
                }
            }

            인증상태안내적용();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            안내수준 = 창고인증안내수준.오류;
            안내메시지 = "인증 서버 응답 시간이 초과되었습니다.";
        }
        catch (HttpRequestException)
        {
            안내수준 = 창고인증안내수준.오류;
            안내메시지 = "인증 서버에 연결하지 못했습니다. 서버 실행 상태를 확인해 주세요.";
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            안내수준 = 창고인증안내수준.오류;
            안내메시지 = "저장된 로그인 세션을 확인하지 못했습니다. 다시 로그인해 주세요.";
        }
        finally
        {
            처리중 = false;
            세션속성변경알림();
        }
    }

    public async Task<bool> 로그인Async(
        string userNameOrEmail,
        string password,
        CancellationToken cancellationToken = default)
    {
        if (처리중)
        {
            return false;
        }

        처리중 = true;
        안내메시지 = null;
        try
        {
            var result = await _authApi.LoginAsync(userNameOrEmail, password, cancellationToken);
            if (!result.IsSuccess)
            {
                안내수준 = 창고인증안내수준.오류;
                안내메시지 = result.ErrorMessage;
                return false;
            }

            인증상태안내적용();
            return 창고업무접근가능;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            안내수준 = 창고인증안내수준.오류;
            안내메시지 = "로그인 서버 응답 시간이 초과되었습니다.";
            return false;
        }
        catch (HttpRequestException)
        {
            안내수준 = 창고인증안내수준.오류;
            안내메시지 = "인증 서버에 연결하지 못했습니다. 서버 실행 상태를 확인해 주세요.";
            return false;
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            안내수준 = 창고인증안내수준.오류;
            안내메시지 = "로그인 응답을 처리하지 못했습니다. 잠시 후 다시 시도해 주세요.";
            return false;
        }
        finally
        {
            처리중 = false;
            세션속성변경알림();
        }
    }

    public async Task 로그아웃Async(CancellationToken cancellationToken = default)
    {
        if (처리중)
        {
            return;
        }

        처리중 = true;
        try
        {
            await _session.ClearAsync(cancellationToken);
            안내수준 = 창고인증안내수준.정보;
            안내메시지 = "로그아웃했습니다.";
        }
        finally
        {
            처리중 = false;
            세션속성변경알림();
        }
    }

    private void 인증상태안내적용()
    {
        if (!로그인됨)
        {
            안내수준 = 창고인증안내수준.정보;
            안내메시지 ??= "창고 업무 계정으로 로그인해 주세요.";
            return;
        }

        if (!창고업무접근가능)
        {
            안내수준 = 창고인증안내수준.주의;
            안내메시지 = "이 계정에는 창고관리자 역할이 없습니다.";
            return;
        }

        안내수준 = 창고인증안내수준.성공;
        안내메시지 = $"{현재사용자표시} 계정으로 인증되었습니다.";
    }

    private void 세션속성변경알림()
    {
        OnPropertyChanged(nameof(로그인됨));
        OnPropertyChanged(nameof(현재사용자Id));
        OnPropertyChanged(nameof(창고업무접근가능));
        OnPropertyChanged(nameof(현재사용자표시));
    }
}
