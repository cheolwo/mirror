using Microsoft.AspNetCore.Components.Forms;

namespace Ssalddel.Ui.Common.Areas.App.ViewModels;

/// <summary>저장된 글의 첨부를 순차 실행하고 부분 실패를 기록한다. 게시·초안은 변경하지 않는다.</summary>
internal static class CommunityComposerAttachmentUploader
{
    public static async Task<CommunityComposerAttachmentUploadResult> UploadAsync(
        IBrowserFile[] selectedFiles,
        Func<IBrowserFile, CancellationToken, Task> upload,
        CancellationToken cancellationToken)
    {
        var uploadedFileCount = 0;
        var failedFileNames = new List<string>();
        for (var fileIndex = 0; fileIndex < selectedFiles.Length; fileIndex++)
        {
            var file = selectedFiles[fileIndex];
            try
            {
                // 호출 때의 입력 조회도 기존 파일별 예외 경계 안에 둔다.
                await upload(file, cancellationToken);
                uploadedFileCount++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                failedFileNames.AddRange(selectedFiles
                    .Skip(fileIndex)
                    .Select(pending => pending.Name));
                break;
            }
            catch (Exception)
            {
                failedFileNames.Add(file.Name);
            }
        }

        return new(uploadedFileCount, failedFileNames);
    }
}

internal sealed record CommunityComposerAttachmentUploadResult(
    int SucceededCount,
    IReadOnlyList<string> FailedFileNames);
