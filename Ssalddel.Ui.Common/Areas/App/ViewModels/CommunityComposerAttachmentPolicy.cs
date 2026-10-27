using Microsoft.AspNetCore.Components.Forms;

namespace Ssalddel.Ui.Common.Areas.App.ViewModels;

/// <summary>게시 방식·초안 수명과 별개인 첨부 미디어의 선택 한도.</summary>
internal static class CommunityComposerAttachmentPolicy
{
    private const long MaxImageUploadBytes = 5 * 1024 * 1024;
    private const long MaxVideoUploadBytes = 15 * 1024 * 1024;
    private const int MaxUploadFileCount = 5;
    private static readonly HashSet<string> AllowedUploadContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/webp",
        "image/gif",
        "video/mp4",
        "video/webm"
    };

    public static CommunityComposerAttachmentSelection Select(IEnumerable<IBrowserFile> files)
    {
        var candidates = files.ToArray();
        var accepted = candidates
            .Where(file => file.Size <= MaxUploadBytes(file.ContentType)
                           && AllowedUploadContentTypes.Contains(file.ContentType))
            .Take(MaxUploadFileCount)
            .ToArray();
        return new(accepted, candidates.Length - accepted.Length);
    }

    public static long MaxUploadBytes(string contentType)
        => contentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase)
            ? MaxVideoUploadBytes
            : MaxImageUploadBytes;
}

internal sealed record CommunityComposerAttachmentSelection(IBrowserFile[] AcceptedFiles, int RejectedCount);
