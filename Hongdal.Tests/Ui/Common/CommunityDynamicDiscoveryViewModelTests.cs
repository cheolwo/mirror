using Hongdal.Contracts.Common.Community;
using Hongdal.Ui.Common.Areas.App.ViewModels;

namespace Hongdal.Tests.Ui.Common;

public sealed class CommunityDynamicDiscoveryViewModelTests
{
    [Fact]
    public async Task 주제목록ViewModel은_네업무영역과_여덟세부주제를조립한다()
    {
        var client = new FakeClient();
        var viewModel = new CommunityDynamicTopicDirectoryViewModel(client);

        await viewModel.LoadAsync();

        Assert.Equal(4, viewModel.Domains.Count);
        Assert.Equal(8, viewModel.Domains.Sum(domain => domain.Topics.Count));
        Assert.Equal("창고", viewModel.Domains[0].DisplayName);
        Assert.Equal("입고", viewModel.Domains[0].Topics[0].DisplayName);
        Assert.False(viewModel.IsLoading);
        Assert.Null(viewModel.ErrorMessage);
    }

    [Fact]
    public async Task 세부주제ViewModel은_선택한주제의피드만조회한다()
    {
        var client = new FakeClient();
        var viewModel = new CommunityDynamicTopicFeedViewModel(client);

        await viewModel.LoadAsync(CommunityDynamicTopicCodes.TransportLoading);

        Assert.Equal(CommunityDynamicTopicCodes.TransportLoading, client.LastTopicKey);
        Assert.Equal(CommunityDynamicTopicCodes.TransportLoading, viewModel.Feed?.TopicKey);
    }

    [Fact]
    public async Task 게시글문맥조회는_위치일시사용동의와_7킬로미터를_API요청으로조립한다()
    {
        var client = new FakeClient();
        var viewModel = new CommunityDynamicDiscoveryViewModel(client);

        await viewModel.LoadPostContextAsync(42, 37.5m, 127m, true);

        Assert.Equal(42, client.LastPostId);
        Assert.NotNull(client.LastRequest);
        Assert.Equal(7m, client.LastRequest.RadiusKm);
        Assert.True(client.LastRequest.ConfirmTransientLocationUse);
        Assert.NotNull(viewModel.Context);
        Assert.False(viewModel.IsLoading);
        Assert.Null(viewModel.ErrorMessage);
    }

    [Fact]
    public async Task 동적주제피드는_음식과화물코드를그대로_API에전달한다()
    {
        var client = new FakeClient();
        var viewModel = new CommunityDynamicDiscoveryViewModel(client);

        await viewModel.LoadTopicFeedAsync(CommunityDynamicTopicCodes.Cargo);

        Assert.Equal(CommunityDynamicTopicCodes.Cargo, client.LastTopicKey);
        Assert.Equal(CommunityDynamicTopicCodes.Cargo, viewModel.Feed?.TopicKey);
    }

    private sealed class FakeClient : ICommunityDynamicDiscoveryClient
    {
        public long LastPostId { get; private set; }
        public string? LastTopicKey { get; private set; }
        public CommunityPostContextDiscoveryRequest? LastRequest { get; private set; }

        public Task<CommunityDynamicTopicCatalogResponse> GetTopicCatalogAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult(new CommunityDynamicTopicCatalogResponse
            {
                Domains =
                [
                    Domain(CommunityDynamicTopicDomainCodes.Warehouse, "창고", "입고", "출고"),
                    Domain(CommunityDynamicTopicDomainCodes.Order, "주문", "개별주문", "공동주문"),
                    Domain(CommunityDynamicTopicDomainCodes.Sales, "판매", "음식", "화물"),
                    Domain(CommunityDynamicTopicDomainCodes.Transport, "운송", "상차", "하차")
                ]
            });

        public Task<CommunityPostContextDiscoveryResponse> DiscoverAsync(
            long postId,
            CommunityPostContextDiscoveryRequest request,
            CancellationToken cancellationToken = default)
        {
            LastPostId = postId;
            LastRequest = request;
            return Task.FromResult(new CommunityPostContextDiscoveryResponse { PostId = postId });
        }

        public Task<CommunityDynamicTopicFeedResponse?> GetFeedAsync(
            string topicKey,
            int page = 1,
            int pageSize = 20,
            CancellationToken cancellationToken = default)
        {
            LastTopicKey = topicKey;
            return Task.FromResult<CommunityDynamicTopicFeedResponse?>(new()
            {
                TopicKey = topicKey,
                DisplayName = topicKey
            });
        }

        private static CommunityDynamicTopicDomainResponse Domain(
            string domainKey,
            string displayName,
            string firstTopic,
            string secondTopic)
            => new()
            {
                DomainKey = domainKey,
                DisplayName = displayName,
                Topics =
                [
                    new CommunityDynamicTopicResponse { DisplayName = firstTopic },
                    new CommunityDynamicTopicResponse { DisplayName = secondTopic }
                ]
            };
    }
}
