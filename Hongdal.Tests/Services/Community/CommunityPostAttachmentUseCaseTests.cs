using Hongdal.Domain.Community;
using Hongdal.Services.Community;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using 홍달.Data;
using 홍달.Infrastructure.Security;
using 홍달.Services.External.Google;
using 홍달.Services.Options;

namespace Hongdal.Tests.Services.Community;

public sealed class CommunityPostAttachmentUseCaseTests
{
    [Fact]
    public async Task 유효한_이미지는_객체저장소와_게시글_첨부에_한번씩_저장된다()
    {
        await using var db = CreateContext();
        var post = CreatePost("correct-password");
        db.PlatformCommunityPosts.Add(post);
        await db.SaveChangesAsync();
        var storage = new RecordingStorageService();
        var useCase = CreateUseCase(db, storage);
        await using var content = new MemoryStream([1, 2, 3]);

        var result = await useCase.첨부업로드Async(
            post.Id,
            new 커뮤니티게시글첨부업로드Command(
                "correct-password",
                content,
                "sample.png",
                "image/png",
                content.Length),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal($"community/posts/{post.Id}", storage.UploadFolder);
        var attachment = await db.PlatformCommunityPostAttachments.SingleAsync();
        Assert.Equal(post.Id, attachment.PostId);
        Assert.Equal("object/sample.png", attachment.ObjectName);
        Assert.Equal(attachment.Id, result.Value.Id);
    }

    [Fact]
    public async Task 존재하지_않는_게시글과_틀린_비밀번호는_HTTP_오류_의미를_보존한다()
    {
        await using var db = CreateContext();
        var post = CreatePost("correct-password");
        db.PlatformCommunityPosts.Add(post);
        await db.SaveChangesAsync();
        var storage = new RecordingStorageService();
        var useCase = CreateUseCase(db, storage);

        var notFound = await UploadAsync(useCase, post.Id + 1, "correct-password");
        var forbidden = await UploadAsync(useCase, post.Id, "wrong-password");

        Assert.Equal(404, notFound.Errors.Single().Metadata["StatusCode"]);
        Assert.Equal(403, forbidden.Errors.Single().Metadata["StatusCode"]);
        Assert.Equal(0, storage.UploadCount);
    }

    private static 커뮤니티게시글첨부UseCase CreateUseCase(
        HongdalContext db,
        IGoogleCloudStorageService storage)
        => new(
            db,
            storage,
            Options.Create(new CommunityPostStorageOptions()));

    private static async Task<FluentResults.Result<
        Hongdal.Contracts.Common.Community.PlatformCommunityPostAttachmentResponse>> UploadAsync(
        I커뮤니티게시글첨부UseCase useCase,
        long postId,
        string password)
    {
        await using var content = new MemoryStream([1]);
        return await useCase.첨부업로드Async(
            postId,
            new 커뮤니티게시글첨부업로드Command(
                password,
                content,
                "sample.png",
                "image/png",
                content.Length),
            CancellationToken.None);
    }

    private static PlatformCommunityPost CreatePost(string password)
        => new()
        {
            AppKey = "platform",
            Category = "정보",
            WorkflowTag = "커뮤니티 신뢰",
            RoleTag = "구성원",
            Title = "첨부 테스트",
            Body = "이미지 첨부 경계를 검증합니다.",
            OriginalLanguageCode = "ko",
            Nickname = "테스터",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            PublicationStatusCode = PlatformCommunityPostPublicationStatusCodes.Published,
            PublishedAtUtc = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

    private static HongdalContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<HongdalContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new HongdalContext(options, new DummyPersonalDataEncryptionService());
    }

    private sealed class RecordingStorageService : IGoogleCloudStorageService
    {
        public int UploadCount { get; private set; }
        public string? UploadFolder { get; private set; }

        public Task<GoogleCloudStorageUploadResult> UploadAsync(
            Stream stream,
            string originalFileName,
            string? contentType,
            string? folder,
            CancellationToken cancellationToken = default)
        {
            UploadCount++;
            UploadFolder = folder;
            return Task.FromResult(new GoogleCloudStorageUploadResult(
                "test-bucket",
                $"object/{Path.GetFileName(originalFileName)}",
                $"https://storage.test/{Path.GetFileName(originalFileName)}"));
        }

        public Task<byte[]> DownloadAsync(
            string bucketName,
            string objectName,
            CancellationToken cancellationToken = default)
            => Task.FromResult(Array.Empty<byte>());
    }

    private sealed class DummyPersonalDataEncryptionService : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
}
