namespace Ssalddel.Ui.Common.Areas.App.RoleWorkspace.DistributedFulfillment;

/// <summary>촬영은 플랫폼 책임이며 사진 첨부가 업무 완료를 의미하지 않습니다.</summary>
public interface I업무사진촬영Adapter
{
    bool IsSupported { get; }
    Task<업무촬영사진?> CaptureAsync(string title, CancellationToken ct);
}

public sealed record 업무촬영사진(string FileName, string ContentType, byte[] Bytes)
{
    public string PreviewUrl => $"data:{ContentType};base64,{Convert.ToBase64String(Bytes)}";
}

public sealed class 미지원업무사진촬영Adapter : I업무사진촬영Adapter
{
    public bool IsSupported => false;
    public Task<업무촬영사진?> CaptureAsync(string title, CancellationToken ct)
        => throw new NotSupportedException("이 화면에서는 카메라 촬영을 지원하지 않습니다. 모바일 앱에서 촬영해 주세요.");
}
