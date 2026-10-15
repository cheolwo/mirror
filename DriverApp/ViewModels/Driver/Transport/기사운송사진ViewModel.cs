using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DriverApp.Services;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Media;
using MudBlazor;

namespace DriverApp.ViewModels.Driver.Transport;

public sealed partial class 기사운송사진ViewModel(
    string 단계명,
    string 촬영제목,
    기사운송작업상태ViewModel 상태,
    IDriverCargoPhotoRecoveryStore? recoveryStore = null) : ObservableObject
{
    private string _contentType = "image/jpeg";
    private byte[]? _bytes;
    private string? _uploadedObjectName;
    private string? _uploadedUrl;
    private int _captureGeneration;
    private RecoveryContext? _context;

    [ObservableProperty]
    public partial bool 촬영중 { get; private set; }

    [ObservableProperty]
    public partial string? 파일명 { get; private set; }

    [ObservableProperty]
    public partial string? 미리보기Url { get; private set; }

    [ObservableProperty]
    public partial bool 복구자료폐기가능 { get; private set; }

    public bool 사진있음 => _bytes is { Length: > 0 };

    [RelayCommand(AllowConcurrentExecutions = false)]
    public async Task 촬영Async()
    {
        if (촬영중)
        {
            return;
        }

        var generation = _captureGeneration;
        var context = _context;
        if (recoveryStore is not null && context is null)
        {
            상태.설정("현재 운송과 로그인 정보를 확인한 뒤 사진을 촬영해 주세요.", Severity.Warning);
            return;
        }
        if (!MediaPicker.Default.IsCaptureSupported)
        {
            상태.설정("이 기기에서는 카메라 촬영을 지원하지 않습니다.", Severity.Warning);
            return;
        }

        try
        {
            촬영중 = true;
            상태.설정("카메라를 여는 중입니다.", Severity.Info);

            var photo = await MediaPicker.Default.CapturePhotoAsync(new MediaPickerOptions
            {
                Title = 촬영제목
            });

            if (generation != _captureGeneration)
            {
                return;
            }

            if (photo is null)
            {
                상태.설정("사진 촬영이 취소되었습니다.", Severity.Info);
                return;
            }

            await using var imageStream = await photo.OpenReadAsync();
            if (generation != _captureGeneration)
            {
                return;
            }
            using var memoryStream = new MemoryStream();
            var buffer = new byte[81920];
            int read;
            while ((read = await imageStream.ReadAsync(buffer)) > 0)
            {
                if (memoryStream.Length + read > DriverTransportCompletionPhotoLimits.MaximumUploadBytes)
                    throw new ArgumentOutOfRangeException(nameof(photo), "사진은 5MB 이하로 다시 촬영해 주세요.");
                await memoryStream.WriteAsync(buffer.AsMemory(0, read));
            }
            if (generation != _captureGeneration)
            {
                return;
            }

            var bytes = memoryStream.ToArray();
            var contentType = string.IsNullOrWhiteSpace(photo.ContentType) ? "image/jpeg" : photo.ContentType;
            _bytes = bytes;
            파일명 = photo.FileName;
            _contentType = contentType;
            미리보기Url = $"data:{_contentType};base64,{Convert.ToBase64String(_bytes)}";
            _uploadedObjectName = null;
            _uploadedUrl = null;
            OnPropertyChanged(nameof(사진있음));
            if (recoveryStore is not null && context is not null)
            {
                await recoveryStore.SaveAsync(context.Owner, new(context.Kind, context.TransportId, photo.FileName, contentType, bytes));
                if (generation != _captureGeneration || context != _context) return;
            }
            복구자료폐기가능 = false;
            상태.설정($"{단계명} 완료 사진이 첨부되었습니다. 사진을 확인한 뒤 {단계명} 완료를 눌러 주세요.", Severity.Success);
        }
        catch (FeatureNotSupportedException)
        {
            if (generation == _captureGeneration)
                상태.설정("이 기기에서는 카메라 촬영을 지원하지 않습니다.", Severity.Warning);
        }
        catch (PermissionException)
        {
            if (generation == _captureGeneration)
                상태.설정("카메라 권한이 필요합니다. 앱 권한 설정에서 카메라 접근을 허용해 주세요.", Severity.Error);
        }
        catch (ArgumentOutOfRangeException)
        {
            if (generation == _captureGeneration)
                상태.설정("사진은 5MB 이하로 다시 촬영해 주세요.", Severity.Warning);
        }
        catch (Exception)
        {
            if (generation == _captureGeneration)
            {
                복구자료폐기가능 = recoveryStore is not null && context is not null;
                상태.설정("사진 촬영 또는 보호 보관을 완료하지 못했습니다. 복구 사진을 폐기하거나 다시 시도해 주세요.", Severity.Error);
            }
        }
        finally
        {
            if (generation == _captureGeneration)
                촬영중 = false;
        }
    }

    public DriverTransportCompletionPhoto 완료요청생성(
        DriverTransportCompletionPhotoKind kind,
        long transportId,
        DriverTransportPickupReceiptEvidence? receiptEvidence = null)
    {
        if (recoveryStore is not null && (_context is null || _context.Kind != kind || _context.TransportId != transportId))
            throw new InvalidOperationException("현재 계정과 운송의 사진을 다시 확인해 주세요.");
        if (_bytes is not { Length: > 0 })
        {
            throw new InvalidOperationException($"{단계명} 완료 사진이 없습니다.");
        }

        var prefix = kind == DriverTransportCompletionPhotoKind.Pickup ? "pickup" : "dropoff";
        return new DriverTransportCompletionPhoto(
            kind,
            transportId,
            파일명 ?? $"{prefix}-complete-{transportId}.jpg",
            _contentType,
            _bytes,
            receiptEvidence,
            _uploadedObjectName,
            _uploadedUrl);
    }

    public void 업로드결과반영(DriverTransportCompletionPhotoResult result)
    {
        if (!result.Uploaded)
        {
            return;
        }

        _uploadedObjectName = result.ObjectName;
        _uploadedUrl = result.Url;
    }

    public Task 제거() => 제거Async();

    [RelayCommand]
    public async Task 제거Async()
    {
        var context = _context;
        var generation = _captureGeneration;
        try
        {
            if (recoveryStore is not null && context is not null)
                await recoveryStore.RemoveAsync(context.Owner, context.Kind, context.TransportId);
            if (generation != _captureGeneration || context != _context) return;
            초기화();
            _context = context;
            상태.설정($"첨부된 {단계명} 사진을 제거했습니다.", Severity.Info);
        }
        catch (Exception)
        {
            if (generation == _captureGeneration && context == _context)
                상태.설정("복구 사진을 제거하지 못했습니다. 다시 시도해 주세요.", Severity.Warning);
        }
    }

    public void 복구설정(string? owner, DriverTransportCompletionPhotoKind kind, long transportId, long sessionRevision)
    {
        var next = string.IsNullOrWhiteSpace(owner) || transportId <= 0 ? null : new RecoveryContext(owner, kind, transportId, sessionRevision);
        if (_context == next) return;
        초기화();
        _context = next;
    }

    public async Task 복원Async(CancellationToken cancellationToken = default)
    {
        var context = _context;
        var generation = _captureGeneration;
        if (recoveryStore is null || context is null || 사진있음) return;
        try
        {
            var recovered = await recoveryStore.LoadAsync(context.Owner, context.Kind, context.TransportId, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (generation != _captureGeneration || context != _context || recovered is null) return;
            _bytes = recovered.Bytes;
            _contentType = recovered.ContentType;
            파일명 = recovered.FileName;
            미리보기Url = $"data:{_contentType};base64,{Convert.ToBase64String(_bytes)}";
            _uploadedObjectName = recovered.ExistingObjectName;
            _uploadedUrl = recovered.ExistingUrl;
            복구자료폐기가능 = false;
            OnPropertyChanged(nameof(사진있음));
            상태.설정($"보관된 {단계명} 사진을 불러왔습니다. 현재 화물과 현장 확인을 다시 진행해 주세요.", Severity.Info);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception)
        {
            if (generation == _captureGeneration && context == _context)
            {
                복구자료폐기가능 = true;
                상태.설정("복구 사진을 확인하지 못했습니다. 해당 복구 사진을 폐기한 뒤 다시 촬영하거나 보호 저장소를 확인해 주세요.", Severity.Warning);
            }
        }
    }

    public async Task 완료확인정리Async(CancellationToken cancellationToken = default)
    {
        var context = _context;
        var generation = _captureGeneration;
        try
        {
            if (recoveryStore is not null && context is not null)
                await recoveryStore.RemoveAsync(context.Owner, context.Kind, context.TransportId, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception)
        {
            if (generation == _captureGeneration && context == _context)
                상태.설정("운송 완료는 확인했지만 복구 사진 정리가 남았습니다. 다시 조회해 주세요.", Severity.Warning);
        }
        finally
        {
            if (generation == _captureGeneration && context == _context) 초기화();
        }
    }

    public void 초기화()
    {
        _captureGeneration++;
        _context = null;
        촬영중 = false;
        복구자료폐기가능 = false;
        파일명 = null;
        _bytes = null;
        OnPropertyChanged(nameof(사진있음));
        미리보기Url = null;
        _contentType = "image/jpeg";
        _uploadedObjectName = null;
        _uploadedUrl = null;
    }

    private sealed record RecoveryContext(string Owner, DriverTransportCompletionPhotoKind Kind, long TransportId, long SessionRevision);
}
