using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Media;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.DistributedFulfillment;

namespace Ssalddel.Client.Infrastructure.Native;

/// <summary>MAUI 호스트가 연결해 쓰는 공통 촬영 어댑터. 기존 화물 완료 API를 호출하지 않습니다.</summary>
public sealed class Maui업무사진촬영Adapter : I업무사진촬영Adapter
{
    public bool IsSupported => MediaPicker.Default.IsCaptureSupported;
    public async Task<업무촬영사진?> CaptureAsync(string title, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!IsSupported) throw new NotSupportedException("이 기기에서는 카메라 촬영을 지원하지 않습니다.");
        try
        {
            var photo = await MainThread.InvokeOnMainThreadAsync(() => MediaPicker.Default.CapturePhotoAsync(new MediaPickerOptions
                { Title = title, MaximumWidth = 2048, MaximumHeight = 2048, CompressionQuality = 80, PreserveMetaData = false, RotateImage = true }));
            ct.ThrowIfCancellationRequested();
            if (photo is null) return null;
            await using var input = await photo.OpenReadAsync();
            using var output = new MemoryStream();
            var buffer = new byte[64 * 1024];
            int count;
            while ((count = await input.ReadAsync(buffer, ct)) > 0)
            {
                if (output.Length + count > 5 * 1024 * 1024) throw new InvalidOperationException("사진은 한 장당 5MB 이하로 촬영해 주세요.");
                await output.WriteAsync(buffer.AsMemory(0, count), ct);
            }
            var bytes = output.ToArray();
            var type = bytes.Length >= 8 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })
                ? "image/png" : bytes.Length >= 3 && bytes[0] == 255 && bytes[1] == 216 && bytes[2] == 255 ? "image/jpeg" : null;
            if (type is null) throw new InvalidOperationException("JPEG 또는 PNG 사진만 첨부할 수 있습니다.");
            return new(type == "image/png" ? "work-photo.png" : "work-photo.jpg", type, bytes);
        }
        catch (PermissionException) { throw new InvalidOperationException("카메라 권한이 필요합니다. 앱 권한 설정에서 카메라를 허용해 주세요."); }
        catch (FeatureNotSupportedException) { throw new NotSupportedException("이 기기에서는 카메라 촬영을 지원하지 않습니다."); }
    }
}
