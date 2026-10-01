using FluentResults;
using Hongdal.Contracts.Common.Community;
using Hongdal.Contracts.Common.Metadata;
using Hongdal.Domain.Community;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using 홍달.Data;
using 홍달.Services.External.Google;
using 홍달.Services.Options;

namespace Hongdal.Services.Community;

[HongdalCommunityV0Module(
    HongdalCommunityV0ModuleKeys.Content,
    HongdalModuleKind.Application,
    "게시글 이미지 첨부의 비밀번호·개수·크기·형식 검증과 객체 저장소 업로드를 처리",
    ReleaseStage = HongdalCommunityV0ReleaseStages.Persistence,
    Boundary = "이미지 첨부만 처리하며 게시글 발행, 댓글 참여 또는 운영자 심의를 변경하지 않습니다.")]
public sealed class 커뮤니티게시글첨부UseCase : I커뮤니티게시글첨부UseCase
{
    private readonly HongdalContext _db;
    private readonly IGoogleCloudStorageService _storageService;
    private readonly CommunityPostStorageOptions _storageOptions;

    public 커뮤니티게시글첨부UseCase(
        HongdalContext db,
        IGoogleCloudStorageService storageService,
        IOptions<CommunityPostStorageOptions> storageOptions)
    {
        _db = db;
        _storageService = storageService;
        _storageOptions = storageOptions.Value;
    }

    public async Task<Result<PlatformCommunityPostAttachmentResponse>> 첨부업로드Async(
        long id,
        커뮤니티게시글첨부업로드Command? command,
        CancellationToken cancellationToken)
    {
        if (command is null || command.Length <= 0)
        {
            return BadRequest("업로드할 이미지 파일을 선택해야 합니다.");
        }

        if (string.IsNullOrWhiteSpace(command.Password))
        {
            return BadRequest("게시글 비밀번호를 입력해야 합니다.");
        }

        var entity = await _db.PlatformCommunityPosts
            .IgnoreQueryFilters()
            .Include(post => post.Attachments)
            .FirstOrDefaultAsync(post => post.Id == id && !post.IsDeleted, cancellationToken);
        if (entity is null)
        {
            return NotFound("게시글을 찾을 수 없습니다.");
        }

        if (entity.PublicationStatusCode is PlatformCommunityPostPublicationStatusCodes.Cancelled
            or PlatformCommunityPostPublicationStatusCodes.Failed)
        {
            return BadRequest("취소되거나 실패한 예약 게시글에는 첨부할 수 없습니다.");
        }

        if (!BCrypt.Net.BCrypt.Verify(command.Password.Trim(), entity.PasswordHash))
        {
            return Forbidden("게시글 비밀번호가 일치하지 않습니다.");
        }

        if (entity.Attachments.Count >= _storageOptions.MaxAttachmentsPerPost)
        {
            return BadRequest($"게시글당 이미지는 최대 {_storageOptions.MaxAttachmentsPerPost}개까지 업로드할 수 있습니다.");
        }

        if (command.Length > _storageOptions.MaxImageBytes)
        {
            return BadRequest($"이미지 크기는 최대 {_storageOptions.MaxImageBytes / 1024 / 1024}MB까지 허용됩니다.");
        }

        if (!_storageOptions.AllowedContentTypes.Contains(command.ContentType, StringComparer.OrdinalIgnoreCase))
        {
            return BadRequest("허용되지 않은 이미지 형식입니다.");
        }

        var folder = $"{_storageOptions.Folder.Trim().Trim('/')}/{entity.Id}";
        var uploadResult = await _storageService.UploadAsync(
            command.FileStream,
            command.FileName,
            command.ContentType,
            folder,
            cancellationToken);
        var now = DateTime.UtcNow;
        var attachment = new PlatformCommunityPostAttachment
        {
            PostId = entity.Id,
            BucketName = uploadResult.BucketName,
            ObjectName = uploadResult.ObjectName,
            Url = uploadResult.PublicUrl,
            OriginalFileName = Path.GetFileName(command.FileName),
            ContentType = command.ContentType,
            FileSizeBytes = command.Length,
            UploadedAtUtc = now
        };

        _db.PlatformCommunityPostAttachments.Add(attachment);
        entity.UpdatedAtUtc = now;
        await _db.SaveChangesAsync(cancellationToken);

        return Result.Ok(ToResponse(attachment));
    }

    private static PlatformCommunityPostAttachmentResponse ToResponse(
        PlatformCommunityPostAttachment attachment)
        => new()
        {
            Id = attachment.Id,
            Url = attachment.Url,
            BucketName = attachment.BucketName,
            ObjectName = attachment.ObjectName,
            OriginalFileName = attachment.OriginalFileName,
            ContentType = attachment.ContentType,
            FileSizeBytes = attachment.FileSizeBytes,
            CommentCount = attachment.CommentCount,
            UploadedAtUtc = attachment.UploadedAtUtc,
            RecentComments = []
        };

    private static Result<PlatformCommunityPostAttachmentResponse> BadRequest(string message)
        => Result.Fail<PlatformCommunityPostAttachmentResponse>(message);

    private static Result<PlatformCommunityPostAttachmentResponse> NotFound(string message)
        => Failure(message, StatusCodes.Status404NotFound);

    private static Result<PlatformCommunityPostAttachmentResponse> Forbidden(string message)
        => Failure(message, StatusCodes.Status403Forbidden);

    private static Result<PlatformCommunityPostAttachmentResponse> Failure(
        string message,
        int statusCode)
        => Result.Fail<PlatformCommunityPostAttachmentResponse>(
            new Error(message).WithMetadata("StatusCode", statusCode));
}
