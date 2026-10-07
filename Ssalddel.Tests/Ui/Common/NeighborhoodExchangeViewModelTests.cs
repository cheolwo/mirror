using System.Net;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.ViewModels;

namespace Ssalddel.Tests.Ui.Common;

public sealed class NeighborhoodExchangeViewModelTests
{
    [Fact]
    public async Task 필터가_바뀐뒤_도착한_이전_목록은_화면을_덮지않는다()
    {
        var old = new TaskCompletionSource<PlatformCommunityPostListResponse>();
        var client = new FakeClient { List = (intent, _) => intent == NeighborhoodExchange.Offer
            ? old.Task : Task.FromResult(new PlatformCommunityPostListResponse { Items = [Post(2, NeighborhoodExchange.Need)], TotalCount = 1 }) };
        using var vm = new NeighborhoodExchangeViewModel(client);
        var first = vm.LoadListAsync(NeighborhoodExchange.Offer);
        await vm.LoadListAsync(NeighborhoodExchange.Need);
        old.SetResult(new() { Items = [Post(1)], TotalCount = 1 }); await first;
        Assert.Equal(2, Assert.Single(vm.Items).Id);
        Assert.Equal(NeighborhoodExchange.Need, vm.IntentFilter);
    }

    [Fact]
    public async Task 전송중_중복클릭을_막고_확인된_등록뒤에만_초안과_비밀번호를_비운다()
    {
        var pending = new TaskCompletionSource<PlatformCommunityPostResponse?>();
        var client = new FakeClient { Publish = _ => pending.Task };
        using var vm = Draft(client);
        var first = vm.PublishAsync();
        Assert.True(vm.IsSending);
        Assert.Null(await vm.PublishAsync()); Assert.Equal(1, client.PublishCalls);
        pending.SetResult(Post(4));
        Assert.Equal(4L, await first); Assert.Equal("", vm.Title); Assert.Equal("", vm.Password);
        Assert.False(vm.IsSending);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task 등록_미확정이나_실패는_초안을_유지하고_자동재시도하지않는다(HttpStatusCode? status)
    {
        var client = new FakeClient { Publish = _ => status.HasValue
            ? Task.FromException<PlatformCommunityPostResponse?>(new HttpRequestException("private diagnostics", null, status))
            : Task.FromResult<PlatformCommunityPostResponse?>(null) };
        using var vm = Draft(client); Assert.Null(await vm.PublishAsync());
        Assert.Equal("동네 도움", vm.Title); Assert.Equal("작성 내용", vm.Body); Assert.Equal("test-password", vm.Password);
        Assert.NotNull(vm.Error); Assert.DoesNotContain("private diagnostics", vm.Error); Assert.Equal(1, client.PublishCalls);
    }

    [Fact]
    public async Task 다른분류_글의_직접주소는_문의와_삭제를_열지않는다()
    {
        var unrelated = Post(1); unrelated.WorkflowTag = "국내 화물 운송";
        var client = new FakeClient { Read = _ => Task.FromResult<PlatformCommunityPostResponse?>(unrelated) };
        using var vm = new NeighborhoodExchangeViewModel(client);
        await vm.LoadDetailAsync(1); Assert.Null(vm.Post); Assert.NotNull(vm.Error);
        await vm.CommentAsync(); Assert.False(await vm.DeleteAsync()); Assert.Equal(0, client.CommentCalls);
    }

    [Fact]
    public async Task 댓글조회_실패를_빈대화로_보이지않고_쓰기하지않는다()
    {
        var client = new FakeClient { Read = _ => Task.FromResult<PlatformCommunityPostResponse?>(Post(1)), FailComments = true };
        using var vm = new NeighborhoodExchangeViewModel(client);
        await vm.LoadDetailAsync(1); Assert.NotNull(vm.Post); Assert.False(vm.CommentsLoaded); Assert.NotNull(vm.Error);
        vm.CommentBody = "문의"; vm.CommentPassword = "pass";
        await vm.CommentAsync(); Assert.Equal(0, client.CommentCalls);
    }

    [Fact]
    public async Task 서버의_삭제권한이_없으면_클라이언트에서_삭제하지않는다()
    {
        var post = Post(1); post.CanDelete = false;
        var client = new FakeClient { Read = _ => Task.FromResult<PlatformCommunityPostResponse?>(post) };
        using var vm = new NeighborhoodExchangeViewModel(client);
        await vm.LoadDetailAsync(1); vm.DeletePassword = "pass";
        Assert.False(await vm.DeleteAsync()); Assert.Equal(0, client.DeleteCalls);
    }

    private static NeighborhoodExchangeViewModel Draft(FakeClient client) => new(client)
    { Title = "동네 도움", Body = "작성 내용", Password = "test-password" };
    [Fact]
    public void 페이지와_DI가_중복으로_종료해도_오류없이_비밀번호를_비운다()
    {
        var vm = Draft(new FakeClient()); vm.SetCommentDeletePassword(1, "pass");
        vm.Dispose(); vm.Dispose(); Assert.Equal("", vm.Password); Assert.Equal("", vm.GetCommentDeletePassword(1));
    }
    private static PlatformCommunityPostResponse Post(long id, string intent = NeighborhoodExchange.Offer) => new()
    { Id = id, WorkflowTag = NeighborhoodExchange.WorkflowTag, RoleTag = intent, Category = PlatformCommunityPostCategories.General, CanDelete = true, DeleteRequiresPassword = true };
    private sealed class FakeClient : INeighborhoodExchangeClient
    {
        public Func<string?, int, Task<PlatformCommunityPostListResponse>> List { get; set; } = (_, _) => Task.FromResult(new PlatformCommunityPostListResponse());
        public Func<long, Task<PlatformCommunityPostResponse?>> Read { get; set; } = _ => Task.FromResult<PlatformCommunityPostResponse?>(null);
        public Func<PlatformCommunityPostCreateRequest, Task<PlatformCommunityPostResponse?>> Publish { get; set; } = _ => Task.FromResult<PlatformCommunityPostResponse?>(Post(1));
        public int PublishCalls; public int CommentCalls; public int DeleteCalls; public bool FailComments;
        public Task<PlatformCommunityPostListResponse> ListAsync(string? intent, int page, CancellationToken ct) => List(intent, page);
        public Task<PlatformCommunityPostResponse?> ReadAsync(long id, CancellationToken ct) => Read(id);
        public Task<PlatformCommunityPostResponse?> PublishAsync(PlatformCommunityPostCreateRequest request, CancellationToken ct) { PublishCalls++; return Publish(request); }
        public Task<IReadOnlyList<PlatformCommunityPostCommentResponse>> CommentsAsync(long id, CancellationToken ct)
            => FailComments ? Task.FromException<IReadOnlyList<PlatformCommunityPostCommentResponse>>(new HttpRequestException()) : Task.FromResult<IReadOnlyList<PlatformCommunityPostCommentResponse>>([]);
        public Task<PlatformCommunityPostCommentResponse?> CommentAsync(long id, PlatformCommunityPostCommentCreateRequest request, CancellationToken ct) { CommentCalls++; return Task.FromResult<PlatformCommunityPostCommentResponse?>(new() { Id = 1, Body = request.Body }); }
        public Task DeleteAsync(long id, string? password, CancellationToken ct) { DeleteCalls++; return Task.CompletedTask; }
        public Task DeleteCommentAsync(long id, long commentId, string password, CancellationToken ct) => Task.CompletedTask;
        public Task ReportCommentAsync(long commentId, CancellationToken ct) => Task.CompletedTask;
    }
}
