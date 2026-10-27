using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.ViewModels;

namespace Ssalddel.Tests.Ui.Common;

public sealed class CommunityPostComposerAttachmentFlowTests
{
    [Fact]
    public async Task 첨부목록은_게시저장후에고정하고_업로드중목록변경은반영하지않는다()
    {
        await using var harness = new ComposerHarness();
        var allowPostResponse = NewCompletionSource();
        harness.Handler.CreateResponse = async cancellationToken =>
        {
            await allowPostResponse.Task.WaitAsync(cancellationToken);
            return PostResponse();
        };
        harness.Composer.SetFiles([File("before-save.png")]);
        harness.Handler.UploadResponse = (index, _, _) =>
        {
            if (index == 0)
            {
                harness.Composer.SetFiles([File("during-upload.png")]);
            }

            return Task.FromResult(AttachmentResponse());
        };

        var saveTask = harness.Composer.SaveAsync();
        await harness.Handler.CreateStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        harness.Composer.SetFiles([File("after-save-a.png"), File("after-save-b.png")]);
        allowPostResponse.TrySetResult();
        var result = await saveTask;

        Assert.True(result.Succeeded);
        Assert.Equal(1, harness.Handler.CreateCount);
        Assert.Equal(["after-save-a.png", "after-save-b.png"], harness.Handler.Uploads.Select(upload => upload.Name));
        Assert.Equal(2, result.AttachmentUploadAttemptedCount);
        Assert.Equal(2, result.AttachmentUploadSucceededCount);
        Assert.Empty(result.AttachmentUploadFailedFileNames);
    }

    [Fact]
    public async Task 게시저장응답이없으면_선택파일과초안을보존하고_업로드하지않는다()
    {
        await using var harness = new ComposerHarness();
        var selected = File("keep.png");
        harness.Composer.SetFiles([selected]);
        await harness.Composer.SaveLocalDraftAsync();
        var storedDraft = harness.Store.Snapshot;
        harness.Handler.CreateResponse = _ => Task.FromResult(PostResponse(null));

        var result = await harness.Composer.SaveAsync();

        Assert.False(result.Succeeded);
        Assert.Null(result.Post);
        Assert.Equal("글 저장 응답을 확인하지 못했습니다.", result.Message);
        Assert.Empty(harness.Handler.Uploads);
        Assert.Equal(0, harness.Store.ClearCount);
        Assert.Same(storedDraft, harness.Store.Snapshot);
        Assert.NotNull(harness.Composer.LocalDraftSavedAtUtc);
        Assert.Same(selected, Assert.Single(harness.Composer.SelectedFiles));
        Assert.True(harness.Composer.Draft.HasContent);
        Assert.True(harness.Composer.IsOpen);
        Assert.False(harness.Composer.IsSaving);
    }

    [Fact]
    public async Task 업로드마다현재비밀번호원문을읽고_제출결과에는저장전정리값을보존한다()
    {
        await using var harness = new ComposerHarness();
        var allowPostResponse = NewCompletionSource();
        harness.Composer.Draft.Password = "  submitted-secret  ";
        harness.Composer.SetFiles([File("first.png"), File("second.png")]);
        harness.Handler.CreateResponse = async cancellationToken =>
        {
            await allowPostResponse.Task.WaitAsync(cancellationToken);
            return PostResponse();
        };
        harness.Handler.UploadResponse = (index, _, _) =>
        {
            if (index == 0)
            {
                harness.Composer.Draft.Password = " raw-second ";
            }

            return Task.FromResult(AttachmentResponse());
        };

        var saveTask = harness.Composer.SaveAsync();
        await harness.Handler.CreateStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        harness.Composer.Draft.Password = " raw-first ";
        allowPostResponse.TrySetResult();
        var result = await saveTask;

        Assert.True(result.Succeeded);
        Assert.Equal([" raw-first ", " raw-second "], harness.Handler.Uploads.Select(upload => upload.Password));
        Assert.Equal("submitted-secret", result.SubmissionPassword);
        Assert.Equal(2, result.AttachmentUploadSucceededCount);
    }

    [Fact]
    public async Task 중간첨부실패는다음첨부를계속하고_실패중복수와표시이름을구분한다()
    {
        await using var harness = new ComposerHarness();
        harness.Composer.SetFiles([File("first.png"), File("same.png"), File("SAME.PNG"), File("last.png")]);
        harness.Handler.UploadResponse = (index, _, _) => Task.FromResult(
            index is 1 or 2 ? FailureResponse() : AttachmentResponse());

        var result = await harness.Composer.SaveAsync();

        Assert.True(result.Succeeded);
        Assert.Equal(1, harness.Handler.CreateCount);
        Assert.Equal(["first.png", "same.png", "SAME.PNG", "last.png"], harness.Handler.Uploads.Select(upload => upload.Name));
        Assert.Equal(4, result.AttachmentUploadAttemptedCount);
        Assert.Equal(2, result.AttachmentUploadSucceededCount);
        Assert.Equal(["same.png", "SAME.PNG"], result.AttachmentUploadFailedFileNames);
        Assert.Equal(CommunityComposerMessageKind.Warning, result.MessageKind);
        Assert.Contains("미디어 2개를 첨부하지 못했습니다.", result.Message);
        Assert.EndsWith(": same.png", result.Message);
        Assert.DoesNotContain("SAME.PNG", result.Message);
        Assert.Equal(1, harness.Store.ClearCount);
        Assert.False(harness.Composer.IsOpen);
    }

    [Fact]
    public async Task 중간취소는남은파일도실패로남기고_취소되지않는초안삭제후화면을정리한다()
    {
        await using var harness = new ComposerHarness();
        using var cancellation = new CancellationTokenSource();
        var allowDraftClear = NewCompletionSource();
        await harness.Composer.SaveLocalDraftAsync();
        harness.Composer.SetFiles([File("first.png"), File("second.png"), File("third.png"), File("fourth.png")]);
        harness.Events.Clear();
        harness.Handler.UploadResponse = (index, _, cancellationToken) =>
        {
            if (index == 1)
            {
                cancellation.Cancel();
                return Task.FromCanceled<HttpResponseMessage>(cancellationToken);
            }

            return Task.FromResult(AttachmentResponse());
        };
        ClearObservation? atClear = null;
        harness.Store.BeforeClear = async cancellationToken =>
        {
            atClear = new(
                cancellationToken.CanBeCanceled,
                harness.Composer.IsSaving,
                harness.Composer.IsOpen,
                harness.Composer.Draft.HasContent,
                harness.Composer.SelectedFiles.Count,
                harness.Composer.LocalDraftSavedAtUtc);
            await allowDraftClear.Task;
        };
        harness.Composer.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(harness.Composer.SelectedFiles))
            {
                harness.Events.Add($"files:{harness.Composer.SelectedFiles.Count}");
            }
            else if (args.PropertyName == nameof(harness.Composer.IsOpen) && !harness.Composer.IsOpen)
            {
                harness.Events.Add("closed");
            }
            else if (args.PropertyName == nameof(harness.Composer.StatusMessage) && harness.Composer.StatusMessage is not null)
            {
                harness.Events.Add("status");
            }
            else if (args.PropertyName == nameof(harness.Composer.IsSaving) && !harness.Composer.IsSaving)
            {
                harness.Events.Add("saving-finished");
            }
        };

        var saveTask = harness.Composer.SaveAsync(cancellation.Token);
        await harness.Store.ClearStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var beforeClearCompleted = harness.Events.ToArray();
        allowDraftClear.TrySetResult();
        var result = await saveTask;

        Assert.Equal(new ClearObservation(false, true, true, true, 4, null), atClear);
        Assert.Equal(["create", "upload:first.png", "upload:second.png", "draft-clear"], beforeClearCompleted);
        Assert.Equal(
            ["create", "upload:first.png", "upload:second.png", "draft-clear", "files:0", "closed", "status", "saving-finished"],
            harness.Events);
        Assert.True(result.Succeeded);
        Assert.Equal(CommunityComposerMessageKind.Warning, result.MessageKind);
        Assert.Equal(4, result.AttachmentUploadAttemptedCount);
        Assert.Equal(1, result.AttachmentUploadSucceededCount);
        Assert.Equal(["second.png", "third.png", "fourth.png"], result.AttachmentUploadFailedFileNames);
        Assert.Equal(2, harness.Handler.Uploads.Count);
        Assert.Equal(1, harness.Handler.CreateCount);
        Assert.Null(harness.Store.Snapshot);
        Assert.Null(harness.Composer.LocalDraftSavedAtUtc);
        Assert.False(harness.Composer.Draft.HasContent);
        Assert.Empty(harness.Composer.SelectedFiles);
        Assert.False(harness.Composer.IsOpen);
        Assert.False(harness.Composer.IsSaving);
        Assert.Equal(result.Message, harness.Composer.StatusMessage);
    }

    [Fact]
    public async Task 첨부선택은형식대소문자와정확한용량경계를지키고_유효파일순서대로다섯개를고른다()
    {
        await using var harness = new ComposerHarness();
        harness.Composer.SetFiles(
        [
            File("unsupported.pdf", "application/pdf"),
            File("image-boundary.JPG", "IMAGE/JPEG", 5 * 1024 * 1024),
            File("image-too-large.png", "image/png", 5 * 1024 * 1024 + 1),
            File("video-boundary.MP4", "VIDEO/MP4", 15 * 1024 * 1024),
            File("video-too-large.webm", "video/webm", 15 * 1024 * 1024 + 1),
            File("third.gif", "image/gif"),
            File("fourth.webp", "image/webp"),
            File("fifth.png"),
            File("sixth.jpg", "image/jpeg")
        ]);

        Assert.Equal(
            ["image-boundary.JPG", "video-boundary.MP4", "third.gif", "fourth.webp", "fifth.png"],
            harness.Composer.SelectedFiles.Select(file => file.Name));
        Assert.Equal(CommunityComposerMessageKind.Warning, harness.Composer.StatusKind);
        Assert.StartsWith("미디어 5개를 선택했습니다. 4개는", harness.Composer.StatusMessage);
    }

    private static TaskCompletionSource NewCompletionSource()
        => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static TestBrowserFile File(string name, string contentType = "image/png", long size = 1)
        => new(name, contentType, size);

    private static HttpResponseMessage PostResponse()
        => PostResponse(new PlatformCommunityPostResponse { Id = 91, Title = "첨부 흐름 확인" });

    private static HttpResponseMessage PostResponse(PlatformCommunityPostResponse? post)
        => new(HttpStatusCode.OK) { Content = JsonContent.Create(post) };

    private static HttpResponseMessage AttachmentResponse()
        => new(HttpStatusCode.OK) { Content = JsonContent.Create<PlatformCommunityPostAttachmentResponse?>(null) };

    private static HttpResponseMessage FailureResponse()
        => new(HttpStatusCode.InternalServerError) { Content = JsonContent.Create(new { detail = "첨부 실패" }) };

    private sealed record UploadObservation(string Name, string Password);

    private sealed record ClearObservation(
        bool CanBeCanceled,
        bool IsSaving,
        bool IsOpen,
        bool HasContent,
        int SelectedFileCount,
        DateTime? LocalDraftSavedAtUtc);

    private sealed class ComposerHarness : IAsyncDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly SsalddelIsmsPClientEncryptionService _encryption;

        public ComposerHarness()
        {
            Handler = new RecordingHandler(Events);
            Store = new RecordingDraftStore(Events);
            _httpClient = new HttpClient(Handler) { BaseAddress = new Uri("https://localhost/") };
            _encryption = new SsalddelIsmsPClientEncryptionService(new NoopJsRuntime());
            var protectedClient = new SsalddelProtectedApiClient(_httpClient, _encryption, new EmptyAccessTokenProvider());
            Composer = new CommunityPostComposerViewModel(new PlatformCommunityService(_httpClient, protectedClient), Store);
            Composer.Configure("platform", "운영자");
            Composer.Draft.Nickname = "작성자";
            Composer.Draft.Password = "secret";
            Composer.Draft.Category = CommunityBoardCatalog.Vow.DisplayName;
            Composer.Draft.WorkflowTag = "출처 기반 정보 공유";
            Composer.Draft.RoleTag = "운영자 정보 공유";
            Composer.Draft.Title = "첨부 흐름 확인";
            Composer.Draft.Body = "본문과 첨부 저장 결과를 확인합니다.";
            Composer.Open();
        }

        public List<string> Events { get; } = [];
        public RecordingHandler Handler { get; }
        public RecordingDraftStore Store { get; }
        public CommunityPostComposerViewModel Composer { get; }

        public async ValueTask DisposeAsync()
        {
            Composer.Dispose();
            _httpClient.Dispose();
            await _encryption.DisposeAsync();
        }
    }

    private sealed class RecordingHandler(List<string> events) : HttpMessageHandler
    {
        public TaskCompletionSource CreateStarted { get; } = NewCompletionSource();
        public int CreateCount { get; private set; }
        public List<UploadObservation> Uploads { get; } = [];
        public Func<CancellationToken, Task<HttpResponseMessage>>? CreateResponse { get; set; }
        public Func<int, UploadObservation, CancellationToken, Task<HttpResponseMessage>>? UploadResponse { get; set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Post && request.RequestUri?.AbsolutePath == "/api/v1/community/posts")
            {
                CreateCount++;
                events.Add("create");
                CreateStarted.TrySetResult();
                return CreateResponse is null ? PostResponse() : await CreateResponse(cancellationToken);
            }

            if (request.Method == HttpMethod.Post && request.RequestUri?.AbsolutePath == "/api/v1/community/posts/91/attachments")
            {
                var parts = (MultipartFormDataContent)request.Content!;
                var passwordPart = parts.Single(part => part.Headers.ContentDisposition?.Name?.Trim('"') == "Password");
                var filePart = parts.Single(part => part.Headers.ContentDisposition?.Name?.Trim('"') == "File");
                var upload = new UploadObservation(
                    filePart.Headers.ContentDisposition!.FileName!.Trim('"'),
                    await passwordPart.ReadAsStringAsync(cancellationToken));
                var index = Uploads.Count;
                Uploads.Add(upload);
                events.Add($"upload:{upload.Name}");
                return UploadResponse is null ? AttachmentResponse() : await UploadResponse(index, upload, cancellationToken);
            }

            return new(HttpStatusCode.NotFound);
        }
    }

    private sealed class RecordingDraftStore(List<string> events) : ICommunityPostComposerDraftStore
    {
        public CommunityPostComposerSnapshot? Snapshot { get; private set; }
        public TaskCompletionSource ClearStarted { get; } = NewCompletionSource();
        public int ClearCount { get; private set; }
        public Func<CancellationToken, Task>? BeforeClear { get; set; }

        public Task<CommunityPostComposerSnapshot?> LoadAsync(string appKey, CancellationToken cancellationToken = default)
            => Task.FromResult(Snapshot);

        public Task SaveAsync(string appKey, CommunityPostComposerSnapshot snapshot, CancellationToken cancellationToken = default)
        {
            Snapshot = snapshot;
            return Task.CompletedTask;
        }

        public async Task ClearAsync(string appKey, CancellationToken cancellationToken = default)
        {
            ClearCount++;
            events.Add("draft-clear");
            var beforeClear = BeforeClear?.Invoke(cancellationToken) ?? Task.CompletedTask;
            ClearStarted.TrySetResult();
            await beforeClear;
            Snapshot = null;
        }
    }

    private sealed class TestBrowserFile(string name, string contentType, long size) : IBrowserFile
    {
        public string Name { get; } = name;
        public DateTimeOffset LastModified { get; } = DateTimeOffset.UnixEpoch;
        public long Size { get; } = size;
        public string ContentType { get; } = contentType;

        public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default)
        {
            if (Size > maxAllowedSize)
            {
                throw new IOException("파일 크기 제한을 넘었습니다.");
            }

            return new MemoryStream(new byte[checked((int)Size)]);
        }
    }

    private sealed class EmptyAccessTokenProvider : ISsalddelAccessTokenProvider
    {
        public string? AccessToken => null;
    }

    private sealed class NoopJsRuntime : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            => throw new NotSupportedException();

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
            => throw new NotSupportedException();
    }
}
