using System.Net;
using System.Net.Http.Json;
using Hongdal.Contracts.Common.Community;
using Hongdal.Ui.Common.Areas.App.Services;
using Hongdal.Ui.Common.Areas.App.ViewModels;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;

namespace Hongdal.Tests.Ui.Common;

public sealed class CommunityPostComposerViewModelTests
{
    [Fact]
    public void 글초안은_필수값과활동국가를검증한다()
    {
        var draft = new CommunityPostComposerDraftViewModel();

        Assert.NotNull(draft.Validate());

        draft.Nickname = "테스터";
        draft.Password = "secret";
        draft.Category = "자유";
        draft.WorkflowTag = "커뮤니티 신뢰";
        draft.RoleTag = "플랫폼 구성원";
        draft.Title = "공동수입 참여자를 찾습니다";
        draft.Body = "조건을 먼저 함께 확인하고 싶습니다.";
        draft.IsAuthorDisplayCountryPublic = true;
        draft.AuthorDisplayCountryCode = "K";

        Assert.Contains("ISO 알파-2", draft.Validate());

        draft.AuthorDisplayCountryCode = "KR";
        draft.AuthorDisplayCountryName = "대한민국";
        Assert.Null(draft.Validate());
    }

    [Fact]
    public void 글초안은_서버와같은비밀번호링크통화규칙을미리검증한다()
    {
        var draft = ValidDraft();

        draft.Password = "123";
        Assert.Contains("4자 이상", draft.Validate());

        draft.Password = "secret";
        draft.SharedLinkUrl = "javascript:alert(1)";
        Assert.Contains("http", draft.Validate());

        draft.SharedLinkUrl = "https://example.com/source";
        draft.IsSalesPost = true;
        draft.SalesProductTitle = "공동구매 사과";
        draft.SalesAvailableQuantity = 10;
        draft.SalesQuantityUnit = "상자";
        draft.SalesUnitPrice = 20_000;
        draft.SalesCurrencyCode = "원";
        Assert.Contains("영문 세 자리", draft.Validate());

        draft.SalesCurrencyCode = "KRW";
        Assert.Null(draft.Validate());
    }

    [Fact]
    public async Task 임시저장은_비밀번호를제외하고_다시열때복원한다()
    {
        var store = new InMemoryDraftStore();
        using var first = CreateComposer(store);
        first.Configure("shipper", "화주");
        first.Draft.Nickname = "테스터";
        first.Draft.Password = "저장하면안됨";
        first.Draft.Title = "입고 예정 확인";
        first.Draft.Body = "업체별 입고 예정품을 확인합니다.";

        await first.SaveLocalDraftAsync();

        Assert.NotNull(store.Snapshot);
        Assert.DoesNotContain("저장하면안됨", System.Text.Json.JsonSerializer.Serialize(store.Snapshot));

        using var restored = CreateComposer(store);
        restored.Configure("shipper", "화주");
        await restored.LoadLocalDraftAsync();
        restored.Open();

        Assert.Equal("입고 예정 확인", restored.Draft.Title);
        Assert.Equal(string.Empty, restored.Draft.Password);
        Assert.Equal(CommunityComposerMessageKind.Info, restored.StatusKind);
    }

    [Fact]
    public async Task 임시저장은_수정대상과예약발행시각을각각복원한다()
    {
        var editStore = new InMemoryDraftStore();
        using (var editor = CreateComposer(editStore))
        {
            editor.Configure("platform", "운영자", allowScheduledPublication: true);
            editor.BeginEdit(new PlatformCommunityPostResponse
            {
                Id = 71,
                Nickname = "운영자",
                Category = CommunityBoardCatalog.Vow.DisplayName,
                WorkflowTag = "출처 기반 정보 공유",
                RoleTag = "운영자",
                Title = "수정 중인 글",
                Body = "수정한 본문"
            });
            await editor.SaveLocalDraftAsync();
        }

        using (var restoredEditor = CreateComposer(editStore))
        {
            restoredEditor.Configure("platform", "운영자", allowScheduledPublication: true);
            await restoredEditor.LoadLocalDraftAsync();
            restoredEditor.Open();

            Assert.Equal(71, restoredEditor.EditingPostId);
            Assert.Equal("수정 중인 글", restoredEditor.Draft.Title);
            Assert.False(restoredEditor.IsScheduledPublication);
        }

        var scheduleStore = new InMemoryDraftStore();
        var scheduledDate = DateTime.Today.AddDays(3);
        var scheduledTime = new TimeSpan(14, 30, 0);
        using (var scheduler = CreateComposer(scheduleStore))
        {
            scheduler.Configure("platform", "운영자", allowScheduledPublication: true);
            scheduler.Draft.Nickname = "운영자";
            scheduler.Draft.Title = "예약할 글";
            scheduler.Draft.Body = "예약 본문";
            scheduler.ScheduledPublishDateLocal = scheduledDate;
            scheduler.ScheduledPublishTimeLocal = scheduledTime;
            scheduler.IsScheduledPublication = true;
            await scheduler.SaveLocalDraftAsync();
        }

        using var restoredScheduler = CreateComposer(scheduleStore);
        restoredScheduler.Configure("platform", "운영자", allowScheduledPublication: true);
        await restoredScheduler.LoadLocalDraftAsync();
        restoredScheduler.Open();

        Assert.True(restoredScheduler.IsScheduledPublication);
        Assert.Equal(scheduledDate, restoredScheduler.ScheduledPublishDateLocal);
        Assert.Equal(scheduledTime, restoredScheduler.ScheduledPublishTimeLocal);
    }

    [Fact]
    public async Task 초안비우기는_browser임시저장도함께삭제한다()
    {
        var store = new InMemoryDraftStore();
        using var composer = CreateComposer(store);
        composer.Configure("platform", "운영자");
        composer.Draft.Title = "지울 초안";
        await composer.SaveLocalDraftAsync();

        var discarded = await composer.DiscardDraftAsync();

        Assert.True(discarded);
        Assert.Null(store.Snapshot);
        Assert.False(composer.Draft.HasContent);
        Assert.Null(composer.LocalDraftSavedAtUtc);
    }

    [Fact]
    public async Task 초안비우기는_늦게끝난자동저장보다항상나중에반영된다()
    {
        var store = new BlockingDraftStore();
        using var composer = CreateComposer(store);
        composer.Configure("platform", "운영자");
        composer.Draft.Title = "늦게 저장되는 초안";

        var saveTask = composer.SaveLocalDraftSilentlyAsync();
        await store.SaveStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var discardTask = composer.DiscardDraftAsync();

        store.AllowSaveToFinish.TrySetResult();
        await Task.WhenAll(saveTask, discardTask);

        Assert.True(await discardTask);
        Assert.Null(store.Snapshot);
        Assert.Null(composer.LocalDraftSavedAtUtc);
        Assert.False(composer.Draft.HasContent);
    }

    [Fact]
    public void 사진선택은_형식크기개수를검증하고_개별제거한다()
    {
        using var composer = CreateComposer(new InMemoryDraftStore());
        composer.Configure("platform", "운영자");
        var validFiles = Enumerable.Range(1, 6)
            .Select(index => new TestBrowserFile($"valid-{index}.png", "image/png", 1024))
            .Cast<IBrowserFile>()
            .ToList();
        validFiles.Add(new TestBrowserFile("large.jpg", "image/jpeg", 6 * 1024 * 1024));
        validFiles.Add(new TestBrowserFile("document.pdf", "application/pdf", 1024));

        composer.SetFiles(validFiles);

        Assert.Equal(5, composer.SelectedFiles.Count);
        Assert.Equal(CommunityComposerMessageKind.Warning, composer.StatusKind);
        var removed = composer.SelectedFiles[0];
        composer.RemoveFile(removed);
        Assert.Equal(4, composer.SelectedFiles.Count);
        Assert.DoesNotContain(removed, composer.SelectedFiles);
    }

    [Fact]
    public async Task 게시글저장뒤_사진업로드만실패하면_중복게시를막도록성공경고를반환한다()
    {
        var handler = new PostThenAttachmentFailureHandler();
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://localhost/") };
        await using var encryption = new HongdalIsmsPClientEncryptionService(new NoopJsRuntime());
        var protectedClient = new HongdalProtectedApiClient(
            httpClient,
            encryption,
            new EmptyAccessTokenProvider());
        var service = new PlatformCommunityService(httpClient, protectedClient);
        using var composer = new CommunityPostComposerViewModel(service, new InMemoryDraftStore());
        composer.Configure("platform", "운영자");
        ApplyValidDraft(composer.Draft);
        composer.SetFiles([new TestBrowserFile("evidence.png", "image/png", 1024)]);

        var result = await composer.SaveAsync();

        Assert.True(result.Succeeded);
        Assert.Equal(91, result.Post?.Id);
        Assert.Equal(CommunityComposerMessageKind.Warning, result.MessageKind);
        Assert.Equal(1, result.AttachmentUploadAttemptedCount);
        Assert.Equal(0, result.AttachmentUploadSucceededCount);
        Assert.Equal(["evidence.png"], result.AttachmentUploadFailedFileNames);
        Assert.Contains("글 상세에서 저장 여부를 확인", result.Message);
        Assert.Equal(1, handler.CreatePostRequestCount);
    }

    [Fact]
    public async Task 게시글저장뒤_사진업로드가취소되어도_browser초안을삭제한다()
    {
        using var cancellation = new CancellationTokenSource();
        var handler = new PostThenAttachmentCancellationHandler(cancellation);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://localhost/") };
        await using var encryption = new HongdalIsmsPClientEncryptionService(new NoopJsRuntime());
        var service = new PlatformCommunityService(
            httpClient,
            new HongdalProtectedApiClient(httpClient, encryption, new EmptyAccessTokenProvider()));
        var store = new InMemoryDraftStore();
        using var composer = new CommunityPostComposerViewModel(service, store);
        composer.Configure("platform", "운영자");
        ApplyValidDraft(composer.Draft);
        await composer.SaveLocalDraftAsync();
        composer.SetFiles([new TestBrowserFile("evidence.png", "image/png", 1024)]);

        var result = await composer.SaveAsync(cancellation.Token);

        Assert.True(result.Succeeded);
        Assert.Equal(CommunityComposerMessageKind.Warning, result.MessageKind);
        Assert.Equal(["evidence.png"], result.AttachmentUploadFailedFileNames);
        Assert.Null(store.Snapshot);
        Assert.Null(composer.LocalDraftSavedAtUtc);
    }

    [Fact]
    public async Task 게시글저장거절은_서버문제설명을사용자에게전달한다()
    {
        var handler = new PostFailureHandler();
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://localhost/") };
        await using var encryption = new HongdalIsmsPClientEncryptionService(new NoopJsRuntime());
        var service = new PlatformCommunityService(
            httpClient,
            new HongdalProtectedApiClient(httpClient, encryption, new EmptyAccessTokenProvider()));
        using var composer = new CommunityPostComposerViewModel(service, new InMemoryDraftStore());
        composer.Configure("platform", "운영자");
        ApplyValidDraft(composer.Draft);

        var result = await composer.SaveAsync();

        Assert.False(result.Succeeded);
        Assert.Contains("선택한 원장은 이 글에 연결할 수 없습니다", result.Message);
        Assert.True(composer.IsOpen || composer.Draft.HasContent);
    }

    [Fact]
    public void 수정시작은_게시글을초안에복사하고_설정을연다()
    {
        using var composer = CreateComposer(new InMemoryDraftStore());
        composer.Configure("shipper", "화주");

        composer.BeginEdit(new PlatformCommunityPostResponse
        {
            Id = 17,
            Nickname = "작성자",
            Category = "업무 질문",
            WorkflowTag = "창고·커머스 이행",
            RoleTag = "창고 관리자",
            Title = "입고 질문",
            Body = "검수 순서를 알고 싶습니다."
        });

        Assert.Equal(17, composer.EditingPostId);
        Assert.True(composer.IsOpen);
        Assert.True(composer.IsSettingsOpen);
        Assert.Equal("입고 질문", composer.Draft.Title);
        Assert.Equal(string.Empty, composer.Draft.Password);
    }

    [Fact]
    public void 운영자글쓰기는_로컬날짜와시간으로_예약발행시각을준비한다()
    {
        using var composer = CreateComposer(new InMemoryDraftStore());
        composer.Configure("platform", "운영자 정보 공유", allowScheduledPublication: true);

        composer.IsScheduledPublication = true;

        Assert.True(composer.AllowScheduledPublication);
        Assert.NotNull(composer.ScheduledPublishDateLocal);
        Assert.NotNull(composer.ScheduledPublishTimeLocal);
        Assert.True(composer.ScheduledPublishAtUtc > DateTime.UtcNow.AddMinutes(1));

        composer.Reset();

        Assert.False(composer.IsScheduledPublication);
        Assert.Null(composer.ScheduledPublishDateLocal);
        Assert.Null(composer.ScheduledPublishTimeLocal);
    }

    [Fact]
    public void 판매글은_본문없이도_상품수량가격결제정보로_요청을만든다()
    {
        var draft = new CommunityPostComposerDraftViewModel
        {
            Nickname = "햇살농원",
            Password = "secret",
            Category = "자유",
            WorkflowTag = "커뮤니티 신뢰",
            RoleTag = "생산자",
            Title = "오늘 수확한 복숭아를 판매합니다",
            IsSalesPost = true,
            SalesProductTitle = "햇복숭아 3kg 한 상자",
            SalesAvailableQuantity = 24,
            SalesQuantityUnit = "상자",
            SalesUnitPrice = 29_000,
            SalesCurrencyCode = "KRW",
            AcceptsDirectCash = true,
            AcceptsTossPayments = true,
            AllowsGroupPurchase = true
        };

        Assert.Null(draft.Validate());

        var request = draft.CreateRequest("shipper");
        var salesOffer = Assert.IsType<PlatformCommunityPostSalesOfferRequest>(request.SalesOffer);
        Assert.Equal("햇복숭아 3kg 한 상자", salesOffer.ProductTitle);
        Assert.Equal(24, salesOffer.AvailableQuantity);
        Assert.Contains(PlatformCommunitySalesPaymentMethodCodes.DirectCash, salesOffer.AcceptedPaymentMethods);
        Assert.Contains(PlatformCommunitySalesPaymentMethodCodes.TossPayments, salesOffer.AcceptedPaymentMethods);
        Assert.True(salesOffer.AllowsGroupPurchase);
        Assert.Equal(PlatformCommunityPostCategories.Sales, request.Category);
        Assert.False(request.IsReportBoardPost);
    }

    [Fact]
    public void 판매정보를_붙이면_판매게시판으로_자동분류되고_다른분류로_바뀌지않는다()
    {
        var draft = new CommunityPostComposerDraftViewModel
        {
            Category = PlatformCommunityPostCategories.ReportDispute,
            IsReportBoardPost = true
        };

        draft.IsSalesPost = true;
        draft.Category = PlatformCommunityPostCategories.General;
        draft.IsReportBoardPost = true;

        Assert.Equal(PlatformCommunityPostCategories.Sales, draft.Category);
        Assert.False(draft.IsReportBoardPost);

        var updateRequest = draft.CreateUpdateRequest();
        Assert.Equal(PlatformCommunityPostCategories.Sales, updateRequest.Category);
        Assert.False(updateRequest.IsReportBoardPost);
    }

    [Fact]
    public void 판매글은_결제방법이없으면_검증에실패한다()
    {
        var draft = new CommunityPostComposerDraftViewModel
        {
            Nickname = "판매자",
            Password = "secret",
            Category = "자유",
            WorkflowTag = "커뮤니티 신뢰",
            RoleTag = "판매자",
            Title = "판매글",
            IsSalesPost = true,
            SalesProductTitle = "상품",
            SalesAvailableQuantity = 1,
            SalesQuantityUnit = "개",
            SalesUnitPrice = 10_000,
            AcceptsDirectCash = false
        };

        Assert.Contains("결제 방법", draft.Validate());
    }

    private static CommunityPostComposerViewModel CreateComposer(
        ICommunityPostComposerDraftStore store)
    {
        var service = new PlatformCommunityService(new HttpClient(), null!);
        return new CommunityPostComposerViewModel(service, store);
    }

    private static CommunityPostComposerDraftViewModel ValidDraft()
    {
        var draft = new CommunityPostComposerDraftViewModel();
        ApplyValidDraft(draft);
        return draft;
    }

    private static void ApplyValidDraft(CommunityPostComposerDraftViewModel draft)
    {
        draft.Nickname = "운영자";
        draft.Password = "secret";
        draft.Category = CommunityBoardCatalog.Vow.DisplayName;
        draft.WorkflowTag = "출처 기반 정보 공유";
        draft.RoleTag = "운영자 정보 공유";
        draft.Title = "공동구매 자료를 함께 확인합니다";
        draft.Body = "공개 자료의 출처와 한계를 함께 확인합니다.";
    }

    private sealed class InMemoryDraftStore : ICommunityPostComposerDraftStore
    {
        public CommunityPostComposerSnapshot? Snapshot { get; private set; }

        public Task<CommunityPostComposerSnapshot?> LoadAsync(
            string appKey,
            CancellationToken cancellationToken = default)
            => Task.FromResult(Snapshot);

        public Task SaveAsync(
            string appKey,
            CommunityPostComposerSnapshot snapshot,
            CancellationToken cancellationToken = default)
        {
            Snapshot = snapshot;
            return Task.CompletedTask;
        }

        public Task ClearAsync(
            string appKey,
            CancellationToken cancellationToken = default)
        {
            Snapshot = null;
            return Task.CompletedTask;
        }
    }

    private sealed class BlockingDraftStore : ICommunityPostComposerDraftStore
    {
        public TaskCompletionSource SaveStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource AllowSaveToFinish { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public CommunityPostComposerSnapshot? Snapshot { get; private set; }

        public Task<CommunityPostComposerSnapshot?> LoadAsync(
            string appKey,
            CancellationToken cancellationToken = default)
            => Task.FromResult(Snapshot);

        public async Task SaveAsync(
            string appKey,
            CommunityPostComposerSnapshot snapshot,
            CancellationToken cancellationToken = default)
        {
            SaveStarted.TrySetResult();
            await AllowSaveToFinish.Task.WaitAsync(cancellationToken);
            Snapshot = snapshot;
        }

        public Task ClearAsync(
            string appKey,
            CancellationToken cancellationToken = default)
        {
            Snapshot = null;
            return Task.CompletedTask;
        }
    }

    private sealed class TestBrowserFile(
        string name,
        string contentType,
        long size) : IBrowserFile
    {
        public string Name { get; } = name;
        public DateTimeOffset LastModified { get; } = DateTimeOffset.UtcNow;
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

    private sealed class PostThenAttachmentFailureHandler : HttpMessageHandler
    {
        public int CreatePostRequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Post
                && request.RequestUri?.AbsolutePath == "/api/v1/community/posts")
            {
                CreatePostRequestCount++;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new PlatformCommunityPostResponse
                    {
                        Id = 91,
                        Title = "공동구매 자료를 함께 확인합니다"
                    }),
                    RequestMessage = request
                });
            }

            if (request.Method == HttpMethod.Post
                && request.RequestUri?.AbsolutePath == "/api/v1/community/posts/91/attachments")
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
                {
                    Content = JsonContent.Create(new { detail = "테스트 첨부 실패" }),
                    RequestMessage = request
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                RequestMessage = request
            });
        }
    }

    private sealed class PostThenAttachmentCancellationHandler(
        CancellationTokenSource cancellation) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Post
                && request.RequestUri?.AbsolutePath == "/api/v1/community/posts")
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new PlatformCommunityPostResponse
                    {
                        Id = 92,
                        Title = "공동구매 자료를 함께 확인합니다"
                    }),
                    RequestMessage = request
                });
            }

            if (request.Method == HttpMethod.Post
                && request.RequestUri?.AbsolutePath == "/api/v1/community/posts/92/attachments")
            {
                cancellation.Cancel();
                return Task.FromCanceled<HttpResponseMessage>(cancellation.Token);
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                RequestMessage = request
            });
        }
    }

    private sealed class PostFailureHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = JsonContent.Create(new
                {
                    title = "요청 오류",
                    detail = "선택한 원장은 이 글에 연결할 수 없습니다."
                }),
                RequestMessage = request
            });
    }

    private sealed class EmptyAccessTokenProvider : IHongdalAccessTokenProvider
    {
        public string? AccessToken => null;
    }

    private sealed class NoopJsRuntime : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            => throw new NotSupportedException();

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier,
            CancellationToken cancellationToken,
            object?[]? args)
            => throw new NotSupportedException();
    }
}
