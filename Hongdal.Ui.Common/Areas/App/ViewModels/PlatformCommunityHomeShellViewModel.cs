using CommunityToolkit.Mvvm.ComponentModel;
using Hongdal.Contracts.Common.Community;
using Hongdal.Ui.Common.Areas.App.Services;

namespace Hongdal.Ui.Common.Areas.App.ViewModels;

/// <summary>
/// 플랫폼 커뮤니티 홈의 화면 전환과 공통 메시지 상태를 소유합니다.
/// DOM 포인터와 JS interop 상태는 Razor 컴포넌트에 남깁니다.
/// </summary>
public sealed class PlatformCommunityHomeShellViewModel : ObservableObject
{
    private bool _isLoading = true;
    private bool _isWorkMode;
    private bool _isCompactHomeSummary;
    private bool _isBaguaNavigatorOpen;
    private string? _statusMessage;
    private CommunityComposerMessageKind _statusKind = CommunityComposerMessageKind.Info;

    public bool IsLoading { get => _isLoading; set => SetProperty(ref _isLoading, value); }
    public bool IsWorkMode { get => _isWorkMode; set => SetProperty(ref _isWorkMode, value); }
    public bool IsCompactHomeSummary { get => _isCompactHomeSummary; set => SetProperty(ref _isCompactHomeSummary, value); }
    public bool IsBaguaNavigatorOpen { get => _isBaguaNavigatorOpen; set => SetProperty(ref _isBaguaNavigatorOpen, value); }
    public string? StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }
    public CommunityComposerMessageKind StatusKind { get => _statusKind; set => SetProperty(ref _statusKind, value); }

    public void SetStatus(string message, CommunityComposerMessageKind kind)
    {
        StatusKind = kind;
        StatusMessage = message;
    }

    public void ClearStatus()
        => StatusMessage = null;
}
