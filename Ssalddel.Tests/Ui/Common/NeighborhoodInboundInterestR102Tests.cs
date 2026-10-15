using Ssalddel.Contracts.Common.Community;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.ViewModels;

namespace Ssalddel.Tests.Ui.Common;

public sealed class NeighborhoodInboundInterestR102Tests
{
    [Fact]
    public void 입고희망_진입은_빈초안을_필요글로만_준비하고_저장하지않는다()
    {
        var client = new CapturingClient();
        using var vm = new NeighborhoodExchangeViewModel(client);
        vm.PrepareWritePurpose(NeighborhoodExchangeViewModel.InboundInterestPurpose);
        Assert.True(vm.IsInboundInterest);
        Assert.Equal(NeighborhoodExchange.Need, vm.Intent);
        Assert.Empty(vm.Title);
        Assert.Empty(vm.Body);
        Assert.Null(client.Request);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("unknown")]
    [InlineData("INBOUND-INTEREST")]
    public void 알수없는_진입은_일반교류작성을_유지한다(string? purpose)
    {
        using var vm = new NeighborhoodExchangeViewModel(new CapturingClient());
        vm.PrepareWritePurpose(purpose);
        Assert.False(vm.IsInboundInterest);
        Assert.Equal(NeighborhoodExchange.Offer, vm.Intent);
    }

    [Theory]
    [InlineData("초안 제목", "", "")]
    [InlineData("", "작성 중인 내용", "")]
    [InlineData("", "", "draft-password")]
    public void 기존초안이_있으면_분류와내용을_덮어쓰지않는다(string title, string body, string password)
    {
        using var vm = new NeighborhoodExchangeViewModel(new CapturingClient())
        { Title = title, Body = body, Password = password, Intent = NeighborhoodExchange.Offer };
        vm.PrepareWritePurpose(NeighborhoodExchangeViewModel.InboundInterestPurpose);
        Assert.False(vm.IsInboundInterest);
        Assert.Equal(NeighborhoodExchange.Offer, vm.Intent);
        Assert.Equal(title, vm.Title);
        Assert.Equal(body, vm.Body);
        Assert.Equal(password, vm.Password);
    }

    [Fact]
    public void 같은작성수명에서_query변경은_입고희망초안을_재분류하지않는다()
    {
        using var vm = new NeighborhoodExchangeViewModel(new CapturingClient());
        vm.PrepareWritePurpose(NeighborhoodExchangeViewModel.InboundInterestPurpose);
        vm.Title = "희망 상품";
        vm.Body = "수량은 미정";
        vm.PrepareWritePurpose(null);
        Assert.True(vm.IsInboundInterest);
        Assert.Equal(NeighborhoodExchange.Need, vm.Intent);
        Assert.Equal("희망 상품", vm.Title);
        Assert.Equal("수량은 미정", vm.Body);
    }

    [Fact]
    public async Task 희망상품과수량은_기존필요글본문으로_전달하고_거래조건을_만들지않는다()
    {
        var client = new CapturingClient();
        using var vm = new NeighborhoodExchangeViewModel(client);
        vm.PrepareWritePurpose(NeighborhoodExchangeViewModel.InboundInterestPurpose);
        vm.Title = "생수 입고 희망";
        vm.Body = "상품: 생수 2L\n대략 수량: 6개\n필요 시기: 다음 주, 구매 약속 없음";
        vm.Password = "test-only-password";
        var body = vm.Body;
        Assert.Equal(102L, await vm.PublishAsync());
        var request = Assert.IsType<PlatformCommunityPostCreateRequest>(client.Request);
        Assert.Equal(NeighborhoodExchange.Need, request.RoleTag);
        Assert.Equal(body, request.Body);
        Assert.Null(request.SalesOffer);
        Assert.Null(request.커뮤니티원장Id);
        Assert.False(request.IsInterestGatheringEnabled);
        Assert.Null(request.PublicNeighborhoodRegionKey);
        Assert.Equal(1, client.PublishCalls);
    }

    [Fact]
    public async Task 분류가바뀌거나_응답이없으면_자동으로_다시등록하지않는다()
    {
        var client = new CapturingClient { Uncertain = true };
        using var vm = new NeighborhoodExchangeViewModel(client);
        vm.PrepareWritePurpose(NeighborhoodExchangeViewModel.InboundInterestPurpose);
        vm.Title = "희망 상품"; vm.Body = "수량 미정"; vm.Password = "test-only-password";
        vm.Intent = NeighborhoodExchange.Offer;
        Assert.Null(await vm.PublishAsync());
        Assert.Equal(0, client.PublishCalls);
        vm.Intent = NeighborhoodExchange.Need;
        Assert.Null(await vm.PublishAsync());
        Assert.Equal(1, client.PublishCalls);
        Assert.Equal("희망 상품", vm.Title);
        Assert.Equal("수량 미정", vm.Body);
        Assert.NotNull(vm.Error);
    }

    private sealed class CapturingClient : INeighborhoodExchangeClient
    {
        public PlatformCommunityPostCreateRequest? Request { get; private set; }
        public int PublishCalls { get; private set; }
        public bool Uncertain { get; init; }
        public Task<PlatformCommunityPostResponse?> PublishAsync(PlatformCommunityPostCreateRequest request, CancellationToken ct)
        { Request = request; PublishCalls++; return Task.FromResult<PlatformCommunityPostResponse?>(Uncertain ? null : new() { Id = 102 }); }
        public Task<PlatformCommunityPostListResponse> ListAsync(string? intent, int page, CancellationToken ct) => Task.FromResult(new PlatformCommunityPostListResponse());
        public Task<PlatformCommunityPostResponse?> ReadAsync(long id, CancellationToken ct) => Task.FromResult<PlatformCommunityPostResponse?>(null);
        public Task<IReadOnlyList<PlatformCommunityPostCommentResponse>> CommentsAsync(long id, CancellationToken ct) => Task.FromResult<IReadOnlyList<PlatformCommunityPostCommentResponse>>([]);
        public Task<PlatformCommunityPostCommentResponse?> CommentAsync(long id, PlatformCommunityPostCommentCreateRequest request, CancellationToken ct) => throw new NotSupportedException();
        public Task DeleteAsync(long id, string? password, CancellationToken ct) => throw new NotSupportedException();
        public Task DeleteCommentAsync(long id, long commentId, string password, CancellationToken ct) => throw new NotSupportedException();
        public Task ReportCommentAsync(long commentId, CancellationToken ct) => throw new NotSupportedException();
    }
}
