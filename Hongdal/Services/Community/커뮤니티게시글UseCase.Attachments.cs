using Hongdal.Contracts.Common.Community;
using FluentResults;

namespace Hongdal.Services.Community;

public sealed partial class 커뮤니티게시글UseCase
{
    public async Task<Result<PlatformCommunityPostAttachmentResponse>> 첨부업로드Async(
        long id,
        커뮤니티게시글첨부업로드Command? command,
        CancellationToken cancellationToken)
        => await _attachmentUseCase.첨부업로드Async(id, command, cancellationToken);
}
