using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Hongdal.Contracts.Common.Warehouse;
using Hongdal.Ui.Common.Areas.App.Models;
using Hongdal.Ui.Common.Areas.App.ViewModels;

namespace WarehouseManagerApp.ViewModels.Warehouse;

/// <summary>
/// 창고 화면 사이에서 유지해야 하는 선택 창고와 현재 작업 세션만 공유합니다.
/// 목록, 입력 초안과 화면별 오류 상태는 각 Page ViewModel이 소유합니다.
/// </summary>
public sealed class 창고작업세션상태ViewModel : ObservableObject, IDisposable
{
    private readonly 입출고화면상태ViewModel _입출고상태;
    private long? _관찰중인창고Id;
    private string _운영ProfileCode = 창고운영ProfileCodes.일반입출고;
    private WarehouseWorkOperatorVerificationResult? _작업자확인결과;
    private string? _현재ProcessCode;
    private string? _현재작업대Barcode;
    private string? _현재작업Key;

    public 창고작업세션상태ViewModel(입출고화면상태ViewModel 입출고상태)
    {
        _입출고상태 = 입출고상태;
        _관찰중인창고Id = 입출고상태.선택된창고?.Id;
        _입출고상태.PropertyChanged += 입출고상태변경;
    }

    public 창고요약응답? 선택된창고 => _입출고상태.선택된창고;

    public string 운영ProfileCode
    {
        get => _운영ProfileCode;
        private set
        {
            if (SetProperty(ref _운영ProfileCode, value))
            {
                OnPropertyChanged(nameof(운영Profile));
            }
        }
    }

    public 창고운영ProfileDefinition 운영Profile
        => 창고운영ProfileCatalog.조회(운영ProfileCode);

    public WarehouseWorkOperatorVerificationResult? 작업자확인결과
    {
        get => _작업자확인결과;
        private set => SetProperty(ref _작업자확인결과, value);
    }

    public string? 현재ProcessCode
    {
        get => _현재ProcessCode;
        private set => SetProperty(ref _현재ProcessCode, value);
    }

    public string? 현재작업대Barcode
    {
        get => _현재작업대Barcode;
        private set => SetProperty(ref _현재작업대Barcode, value);
    }

    public string? 현재작업Key
    {
        get => _현재작업Key;
        private set => SetProperty(ref _현재작업Key, value);
    }

    public bool 활성작업있음
        => 작업자확인결과?.IsAllowed == true
           && !string.IsNullOrWhiteSpace(현재ProcessCode);

    public bool 창고선택(long warehouseId, string 운영ProfileCode)
    {
        운영Profile검증(운영ProfileCode);
        if (!_입출고상태.창고선택(warehouseId))
        {
            return false;
        }

        this.운영ProfileCode = 창고운영ProfileCodes.정규화(운영ProfileCode);
        작업세션초기화();
        return true;
    }

    public void 운영Profile설정(string profileCode)
    {
        운영Profile검증(profileCode);
        운영ProfileCode = 창고운영ProfileCodes.정규화(profileCode);
        작업세션초기화();
    }

    public void 작업시작(
        string processCode,
        WarehouseWorkOperatorVerificationResult verification)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(processCode);
        ArgumentNullException.ThrowIfNull(verification);
        if (!verification.IsAllowed)
        {
            throw new InvalidOperationException("확인되지 않은 작업자로는 창고 작업을 시작할 수 없습니다.");
        }

        현재ProcessCode = processCode.Trim();
        작업자확인결과 = verification;
        현재작업대Barcode = null;
        현재작업Key = null;
        OnPropertyChanged(nameof(활성작업있음));
    }

    public void 작업대확인(string workbenchBarcode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workbenchBarcode);
        if (!활성작업있음)
        {
            throw new InvalidOperationException("작업자 확인 후 작업대를 선택해야 합니다.");
        }

        현재작업대Barcode = workbenchBarcode.Trim().ToUpperInvariant();
    }

    public void 작업선택(string workKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workKey);
        현재작업Key = workKey.Trim();
    }

    public void 작업세션초기화()
    {
        작업자확인결과 = null;
        현재ProcessCode = null;
        현재작업대Barcode = null;
        현재작업Key = null;
        OnPropertyChanged(nameof(활성작업있음));
    }

    public void Dispose()
    {
        _입출고상태.PropertyChanged -= 입출고상태변경;
        GC.SuppressFinalize(this);
    }

    private void 입출고상태변경(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not null
            && e.PropertyName != nameof(입출고화면상태ViewModel.선택된창고))
        {
            return;
        }

        var warehouseId = _입출고상태.선택된창고?.Id;
        if (_관찰중인창고Id != warehouseId)
        {
            _관찰중인창고Id = warehouseId;
            운영ProfileCode = 창고운영ProfileCodes.일반입출고;
            작업세션초기화();
        }

        OnPropertyChanged(nameof(선택된창고));
    }

    private static void 운영Profile검증(string profileCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileCode);
        if (!창고운영ProfileCodes.지원함(profileCode))
        {
            throw new ArgumentOutOfRangeException(
                nameof(profileCode),
                profileCode,
                "지원하지 않는 창고 운영 프로필입니다.");
        }
    }
}
