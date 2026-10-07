using System.Net;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Ui.Common.Areas.App.Components.Community.Exchange;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.ViewModels;

namespace Ssalddel.Tests.Ui.Common;

public sealed class NeighborhoodMapWorkspacePanelRenderTests
{
    [Fact]
    public async Task 등록패널은_입력폼없이_전용페이지로_같은지도로의복귀를_전달한다()
    {
        var home = NeighborhoodMapNavigation.Href([NeighborhoodMapNavigation.Offer], "map", "seoul-a", "register");
        var html = await RenderAsync<NeighborhoodMapWorkspacePanel>(new()
        {
            [nameof(NeighborhoodMapWorkspacePanel.Kind)] = "register",
            [nameof(NeighborhoodMapWorkspacePanel.Region)] = "seoul-a",
            [nameof(NeighborhoodMapWorkspacePanel.ReturnUrl)] = home
        });
        Assert.DoesNotContain("<form", html); Assert.DoesNotContain("<input", html);
        Assert.Contains("http://localhost/roles/01/community/exchange/write?returnUrl=", html);
        Assert.Contains("http://localhost/roles/01/community/exchange/spaces/new?returnUrl=", html);
        Assert.Contains("http://localhost/roles/01/community/exchange/deliveries/new?returnUrl=", html);
        Assert.Contains(Uri.EscapeDataString(home), html);
    }

    [Fact]
    public async Task 지도글상세는_공개문의를조회하고_비밀번호와새본문입력은_독립상세로_인계한다()
    {
        var home = NeighborhoodMapNavigation.Href(NeighborhoodMapNavigation.DefaultLayers, "map", "seoul-a", "post", "12");
        var html = await RenderAsync<ExchangeDetail>(new()
        {
            [nameof(ExchangeDetail.PostId)] = 12L,
            [nameof(ExchangeDetail.Embedded)] = true,
            [nameof(ExchangeDetail.ReturnUrl)] = home
        });
        Assert.Contains("이웃의 제공 글", html); Assert.Contains("언제 받을 수 있나요", html);
        Assert.DoesNotContain("<form", html); Assert.DoesNotContain("type=\"password\"", html);
        Assert.DoesNotContain("<textarea", html);
        Assert.Contains("공개 문의 남기기·관리", html);
        Assert.Contains("/roles/01/community/exchange/posts/12?returnUrl=", html);
        Assert.Contains("/roles/01/community/exchange/work/new?postId=12", html);
        Assert.Contains("returnUrl=" + Uri.EscapeDataString(home), html);
    }

    [Fact]
    public async Task 직접글상세는_기존문의작성과관리입력을_유지한다()
    {
        var html = await RenderAsync<ExchangeDetail>(new() { [nameof(ExchangeDetail.PostId)] = 12L });
        Assert.Contains("<form", html); Assert.Contains("type=\"password\"", html);
        Assert.Contains("글 삭제", html); Assert.Contains("생활 교류", html);
    }

    private static async Task<string> RenderAsync<T>(Dictionary<string, object?> parameters) where T : IComponent
    {
        var services = new ServiceCollection(); services.AddLogging();
        services.AddSingleton<NavigationManager, TestNavigation>();
        services.AddSingleton<ISsalddel현재사용자Context, AnonymousUser>();
        services.AddSingleton<INeighborhoodExchangeClient, ExchangeClient>();
        services.AddSingleton<INeighborhoodExchangeMapClient, MapClient>();
        services.AddSingleton<INeighborhoodCollaborationClient, CollaborationClient>();
        services.AddTransient<NeighborhoodExchangeViewModel>();
        services.AddSingleton<NeighborhoodCollaborationDraftSession>();
        services.AddTransient<NeighborhoodCollaborationSourceViewModel>();
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var output = await renderer.RenderComponentAsync<T>(ParameterView.FromDictionary(parameters));
            return WebUtility.HtmlDecode(output.ToHtmlString());
        });
    }

    private sealed class TestNavigation : NavigationManager
    {
        public TestNavigation() => Initialize("http://localhost/roles/01/", "http://localhost/roles/01/community/map");
        protected override void NavigateToCore(string uri, bool forceLoad) { }
    }
    private sealed class AnonymousUser : ISsalddel현재사용자Context { public 현재사용자Snapshot 현재사용자 => 현재사용자Snapshot.익명; }
    private sealed class ExchangeClient : INeighborhoodExchangeClient
    {
        public Task<PlatformCommunityPostResponse?> ReadAsync(long id, CancellationToken ct) => Task.FromResult<PlatformCommunityPostResponse?>(new()
        { Id = id, Title = "이웃의 제공 글", Body = "내일 물건을 제공해요", RoleTag = NeighborhoodExchange.Offer, Category = PlatformCommunityPostCategories.General, WorkflowTag = NeighborhoodExchange.WorkflowTag, CanDelete = true });
        public Task<IReadOnlyList<PlatformCommunityPostCommentResponse>> CommentsAsync(long id, CancellationToken ct) => Task.FromResult<IReadOnlyList<PlatformCommunityPostCommentResponse>>([new() { Id = 30, Body = "언제 받을 수 있나요" }]);
        public Task<PlatformCommunityPostListResponse> ListAsync(string? intent, int page, CancellationToken ct) => Task.FromResult(new PlatformCommunityPostListResponse());
        public Task<PlatformCommunityPostResponse?> PublishAsync(PlatformCommunityPostCreateRequest request, CancellationToken ct) => throw new NotSupportedException();
        public Task<PlatformCommunityPostCommentResponse?> CommentAsync(long id, PlatformCommunityPostCommentCreateRequest request, CancellationToken ct) => throw new NotSupportedException();
        public Task DeleteAsync(long id, string? password, CancellationToken ct) => throw new NotSupportedException();
        public Task DeleteCommentAsync(long id, long commentId, string password, CancellationToken ct) => throw new NotSupportedException();
        public Task ReportCommentAsync(long commentId, CancellationToken ct) => throw new NotSupportedException();
    }
    private sealed class CollaborationClient : INeighborhoodCollaborationClient
    {
        public Task<IReadOnlyList<NeighborhoodCollaborationOpportunityResponse>> OpportunitiesAsync(long postId, CancellationToken ct) => Task.FromResult<IReadOnlyList<NeighborhoodCollaborationOpportunityResponse>>([]);
        public Task<IReadOnlyList<NeighborhoodCollaborationPublicHistoryResponse>> PublicHistoryAsync(long postId, CancellationToken ct) => Task.FromResult<IReadOnlyList<NeighborhoodCollaborationPublicHistoryResponse>>([]);
        public Task<NeighborhoodCollaborationListResponse> MineAsync(string scope, int page, CancellationToken ct) => throw new NotSupportedException();
        public Task<NeighborhoodCollaborationResponse?> ReadAsync(string id, CancellationToken ct) => throw new NotSupportedException();
        public Task<NeighborhoodCollaborationResponse?> CreateAsync(NeighborhoodCollaborationCreateRequest request, CancellationToken ct) => throw new NotSupportedException();
        public Task<NeighborhoodCollaborationResponse?> CommandAsync(string id, NeighborhoodCollaborationCommandRequest request, CancellationToken ct) => throw new NotSupportedException();
        public Task<NeighborhoodCollaborationResponse?> ReceiptAsync(Guid requestId, string? stableId, CancellationToken ct) => throw new NotSupportedException();
    }
    private sealed class MapClient : INeighborhoodExchangeMapClient
    {
        public Task<NeighborhoodExchangeRegionListResponse> RegionsAsync(CancellationToken ct) => Task.FromResult(new NeighborhoodExchangeRegionListResponse());
        public Task<NeighborhoodExchangeMapResponse> MapAsync(string? intent, CancellationToken ct) => throw new NotSupportedException();
        public Task<PlatformCommunityPostListResponse> PostsAsync(string? regionKey, string? intent, int page, CancellationToken ct) => throw new NotSupportedException();
        public Task<NeighborhoodMapDeliveryPage> MineAsync(int page, CancellationToken ct) => throw new NotSupportedException();
        public Task<NeighborhoodDeliveryMapResponse?> DeliveryMapAsync(string requestId, bool includeRoute, CancellationToken ct) => throw new NotSupportedException();
    }
}
