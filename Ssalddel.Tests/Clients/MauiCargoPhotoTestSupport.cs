// 실제 기사 사진 ViewModel을 실행하기 위한 MAUI 플랫폼 어댑터입니다.
// 카메라·기기·사진 업로드를 검증하지 않으며 업무 ViewModel은 대체하지 않습니다.
namespace Microsoft.Maui.ApplicationModel
{
    public sealed class FeatureNotSupportedException : Exception { }
    public sealed class PermissionException : Exception { }
}

namespace Microsoft.Maui.Storage
{
    public sealed class FileResult(string fileName, string contentType, byte[] bytes)
    {
        public string FileName { get; } = fileName;
        public string ContentType { get; } = contentType;
        public Task<Stream> OpenReadAsync() => Task.FromResult<Stream>(new MemoryStream(bytes, writable: false));
    }
}

namespace Microsoft.Maui.Media
{
    public sealed class MediaPickerOptions
    {
        public string? Title { get; set; }
    }

    public interface IMediaPicker
    {
        bool IsCaptureSupported { get; }
        Task<Microsoft.Maui.Storage.FileResult?> CapturePhotoAsync(MediaPickerOptions? options = null);
    }

    public static class MediaPicker
    {
        private static readonly AsyncLocal<IMediaPicker?> Current = new();
        private static readonly IMediaPicker Unsupported = new UnsupportedPicker();
        public static IMediaPicker Default => Current.Value ?? Unsupported;

        internal static IDisposable UseForCurrentTest(IMediaPicker adapter)
        {
            var previous = Current.Value;
            Current.Value = adapter;
            return new Scope(previous);
        }

        private sealed class Scope(IMediaPicker? previous) : IDisposable
        {
            public void Dispose() => Current.Value = previous;
        }

        private sealed class UnsupportedPicker : IMediaPicker
        {
            public bool IsCaptureSupported => false;
            public Task<Microsoft.Maui.Storage.FileResult?> CapturePhotoAsync(MediaPickerOptions? options = null)
                => throw new Microsoft.Maui.ApplicationModel.FeatureNotSupportedException();
        }
    }
}

namespace Ssalddel.Tests.Clients
{
    internal static class MauiCargoPhotoTestSupport
    {
        public static IDisposable UseSyntheticCapture()
            => Microsoft.Maui.Media.MediaPicker.UseForCurrentTest(new SyntheticPicker());

        private sealed class SyntheticPicker : Microsoft.Maui.Media.IMediaPicker
        {
            public bool IsCaptureSupported => true;
            public Task<Microsoft.Maui.Storage.FileResult?> CapturePhotoAsync(Microsoft.Maui.Media.MediaPickerOptions? options = null)
                => Task.FromResult<Microsoft.Maui.Storage.FileResult?>(new(
                    "synthetic-cargo-proof.jpg", "image/jpeg", [1, 2, 3, 4]));
        }
    }
}
