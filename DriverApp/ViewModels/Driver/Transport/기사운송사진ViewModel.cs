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
    기사운송작업상태ViewModel 상태) : ObservableObject
{
    private string _contentType = "image/jpeg";
    private byte[]? _bytes;
    private string? _uploadedObjectName;
    private string? _uploadedUrl;

    [ObservableProperty]
    public partial bool 촬영중 { get; private set; }

    [ObservableProperty]
    public partial string? 파일명 { get; private set; }

    [ObservableProperty]
    public partial string? 미리보기Url { get; private set; }

    public bool 사진있음 => _bytes is { Length: > 0 };

    [RelayCommand(AllowConcurrentExecutions = false)]
    public async Task 촬영Async()
    {
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

            if (photo is null)
            {
                상태.설정("사진 촬영이 취소되었습니다.", Severity.Info);
                return;
            }

            await using var imageStream = await photo.OpenReadAsync();
            using var memoryStream = new MemoryStream();
            await imageStream.CopyToAsync(memoryStream);

            _bytes = memoryStream.ToArray();
            OnPropertyChanged(nameof(사진있음));
            파일명 = photo.FileName;
            _contentType = string.IsNullOrWhiteSpace(photo.ContentType) ? "image/jpeg" : photo.ContentType;
            미리보기Url = $"data:{_contentType};base64,{Convert.ToBase64String(_bytes)}";
            _uploadedObjectName = null;
            _uploadedUrl = null;
            상태.설정($"{단계명} 완료 사진이 첨부되었습니다. 사진을 확인한 뒤 {단계명} 완료를 눌러 주세요.", Severity.Success);
        }
        catch (FeatureNotSupportedException)
        {
            상태.설정("이 기기에서는 카메라 촬영을 지원하지 않습니다.", Severity.Warning);
        }
        catch (PermissionException)
        {
            상태.설정("카메라 권한이 필요합니다. 앱 권한 설정에서 카메라 접근을 허용해 주세요.", Severity.Error);
        }
        catch (Exception ex)
        {
            상태.설정($"사진 촬영 중 오류가 발생했습니다: {ex.Message}", Severity.Error);
        }
        finally
        {
            촬영중 = false;
        }
    }

    public DriverTransportCompletionPhoto 완료요청생성(
        DriverTransportCompletionPhotoKind kind,
        long transportId,
        DriverTransportPickupReceiptEvidence? receiptEvidence = null)
    {
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

    [RelayCommand]
    public void 제거()
    {
        초기화();
        상태.설정($"첨부된 {단계명} 사진을 제거했습니다.", Severity.Info);
    }

    public void 초기화()
    {
        촬영중 = false;
        파일명 = null;
        _bytes = null;
        OnPropertyChanged(nameof(사진있음));
        미리보기Url = null;
        _contentType = "image/jpeg";
        _uploadedObjectName = null;
        _uploadedUrl = null;
    }
}
