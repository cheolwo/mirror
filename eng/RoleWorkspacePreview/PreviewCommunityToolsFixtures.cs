using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.WebUtilities;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.Privacy;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.ViewModels;

namespace RoleWorkspacePreview;

// 실제 공유 지도/독립 화면을 읽기 전용 예시 포트로 조립합니다. 기존 역할 예시는 교체하지 않습니다.
public static class PreviewCommunityToolsRegistration
{
    public static IServiceCollection AddCommunityToolsPreview(this IServiceCollection services)
    {
        services.AddScoped<PreviewCommunityToolsFixture>();
        services.AddScoped<NeighborhoodMapWorkspaceSession>();
        services.AddScoped<INeighborhoodExchangeMapClient>(sp => sp.GetRequiredService<PreviewCommunityToolsFixture>());
        services.AddScoped<INeighborhoodMapPreferenceStore>(sp => sp.GetRequiredService<PreviewCommunityToolsFixture>());
        services.AddTransient(sp => new NeighborhoodExchangeMapViewModel(sp.GetRequiredService<PreviewCommunityToolsFixture>(),
            sp.GetRequiredService<PreviewCommunityToolsFixture>(), sp.GetRequiredService<PreviewCommunityToolsFixture>(),
            session: sp.GetRequiredService<NeighborhoodMapWorkspaceSession>()));
        services.AddScoped<INeighborhoodExchangeClient>(sp => sp.GetRequiredService<PreviewCommunityToolsFixture>());
        services.AddTransient<NeighborhoodExchangeViewModel>();
        services.AddScoped<NeighborhoodCollaborationDraftSession>();
        services.AddScoped<NeighborhoodStorageDraftSession>();
        services.AddTransient(sp => new NeighborhoodCollaborationQueryViewModel(sp.GetRequiredService<PreviewCommunityToolsFixture>(),
            sp.GetRequiredService<NeighborhoodCollaborationDraftSession>(), sp.GetRequiredService<PreviewCommunityToolsFixture>()));
        services.AddTransient(sp => new NeighborhoodStorageQueryViewModel(sp.GetRequiredService<PreviewCommunityToolsFixture>(),
            sp.GetRequiredService<PreviewCommunityToolsFixture>(), sp.GetRequiredService<NeighborhoodStorageDraftSession>(), sp.GetRequiredService<PreviewCommunityToolsFixture>()));
        services.AddTransient(sp => new NeighborhoodStorageAuthoringViewModel(sp.GetRequiredService<PreviewCommunityToolsFixture>(),
            sp.GetRequiredService<PreviewCommunityToolsFixture>(), sp.GetRequiredService<NeighborhoodStorageDraftSession>(), sp.GetRequiredService<PreviewCommunityToolsFixture>()));
        services.AddTransient(sp => new NeighborhoodDeliveryAuthoringViewModel(sp.GetRequiredService<PreviewCommunityToolsFixture>(),
            sp.GetRequiredService<PreviewCommunityToolsFixture>(), sp.GetRequiredService<PreviewCommunityToolsFixture>()));
        services.AddTransient(sp => new NeighborhoodDeliveryQueryViewModel(
            new Uri(sp.GetRequiredService<NavigationManager>().Uri).AbsolutePath.StartsWith("/community/", StringComparison.Ordinal)
                ? sp.GetRequiredService<PreviewCommunityToolsFixture>() : sp.GetRequiredService<INeighborhoodDeliveryClient>(),
            new Uri(sp.GetRequiredService<NavigationManager>().Uri).AbsolutePath.StartsWith("/community/", StringComparison.Ordinal)
                ? sp.GetRequiredService<PreviewCommunityToolsFixture>() : sp.GetRequiredService<ISsalddel현재사용자Context>()));
        return services;
    }
}

internal sealed class PreviewCommunityToolsFixture(NavigationManager navigation) :
    ISsalddel현재사용자Context, INeighborhoodExchangeMapClient, INeighborhoodMapPreferenceStore,
    INeighborhoodExchangeClient, INeighborhoodCollaborationClient, INeighborhoodStorageClient, INeighborhoodDeliveryClient
{
    private string? State => QueryHelpers.ParseQuery(new Uri(navigation.Uri).Query).GetValueOrDefault("state");
    public 현재사용자Snapshot 현재사용자 => State == "anonymous" ? 현재사용자Snapshot.익명 : new("preview-owner", "화면 검토", []);
    private static NeighborhoodPublicRegionDto Region => new() { RegionKey = "seoul-a", DisplayName = "동네 1", CountryCode = "KR", Latitude = 37.580, Longitude = 127.087 };
    public Task<NeighborhoodExchangeRegionListResponse> RegionsAsync(CancellationToken ct)
        => State == "error" ? Task.FromException<NeighborhoodExchangeRegionListResponse>(new InvalidOperationException("preview read failure")) : Task.FromResult(new NeighborhoodExchangeRegionListResponse { Items = [Region] });
    public Task<NeighborhoodExchangeMapResponse> MapAsync(string? intent, CancellationToken ct)
        => Task.FromResult(new NeighborhoodExchangeMapResponse { Items = [new() { Region = Region, OfferCount = 1 }] });
    public Task<PlatformCommunityPostListResponse> PostsAsync(string? region, string? intent, int page, CancellationToken ct)
        => Task.FromResult(new PlatformCommunityPostListResponse { Items = [], TotalCount = 0 });
    Task<NeighborhoodMapDeliveryPage> INeighborhoodExchangeMapClient.MineAsync(int page, CancellationToken ct)
        => Task.FromResult(new NeighborhoodMapDeliveryPage([], false));
    public Task<NeighborhoodDeliveryMapResponse?> DeliveryMapAsync(string id, bool route, CancellationToken ct)
        => Task.FromResult<NeighborhoodDeliveryMapResponse?>(null);
    public Task<NeighborhoodMapPreferences?> LoadAsync(string? owner, CancellationToken ct = default) => Task.FromResult<NeighborhoodMapPreferences?>(null);
    public Task SaveAsync(string? owner, NeighborhoodMapPreferences preference, CancellationToken ct = default) => Task.CompletedTask;
    public Task<PlatformCommunityPostListResponse> ListAsync(string? intent, int page, CancellationToken ct) => PostsAsync(null, intent, page, ct);
    public Task<PlatformCommunityPostResponse?> ReadAsync(long id, CancellationToken ct) => Task.FromResult<PlatformCommunityPostResponse?>(null);
    public Task<IReadOnlyList<PlatformCommunityPostCommentResponse>> CommentsAsync(long id, CancellationToken ct) => Task.FromResult<IReadOnlyList<PlatformCommunityPostCommentResponse>>([]);
    public Task<PlatformCommunityPostResponse?> PublishAsync(PlatformCommunityPostCreateRequest request, CancellationToken ct) => Block<PlatformCommunityPostResponse?>();
    public Task<PlatformCommunityPostCommentResponse?> CommentAsync(long id, PlatformCommunityPostCommentCreateRequest request, CancellationToken ct) => Block<PlatformCommunityPostCommentResponse?>();
    public Task DeleteAsync(long id, string? password, CancellationToken ct) => Block<object>();
    public Task DeleteCommentAsync(long id, long commentId, string password, CancellationToken ct) => Block<object>();
    public Task ReportCommentAsync(long id, CancellationToken ct) => Block<object>();
    public Task<NeighborhoodCollaborationListResponse> MineAsync(string scope, int page, CancellationToken ct) => Task.FromResult(new NeighborhoodCollaborationListResponse { Items = [] });
    Task<NeighborhoodCollaborationResponse?> INeighborhoodCollaborationClient.ReadAsync(string id, CancellationToken ct)
        => State == "work-error" ? Task.FromException<NeighborhoodCollaborationResponse?>(new InvalidOperationException("preview read failure")) : Task.FromResult<NeighborhoodCollaborationResponse?>(new()
        {
            StableId = id, SourceTitle = "물품 나눔 1", Kind = NeighborhoodCollaborationKinds.Goods,
            StatusCode = NeighborhoodCollaborationStates.InProgress, IsRequester = true, Revision = 1,
            OwnerAgreed = true, RequesterAgreed = true, AllowedActions = [NeighborhoodCollaborationActions.ProposeCompletion, NeighborhoodCollaborationActions.Cancel],
            Terms = new() { Summary = "물품 나눔 1", Quantity = 1, Unit = "개", FromUtc = DateTime.UtcNow, UntilUtc = DateTime.UtcNow.AddDays(1) }
        });
    public Task<NeighborhoodCollaborationResponse?> CreateAsync(NeighborhoodCollaborationCreateRequest request, CancellationToken ct) => Block<NeighborhoodCollaborationResponse?>();
    public Task<NeighborhoodCollaborationResponse?> CommandAsync(string id, NeighborhoodCollaborationCommandRequest request, CancellationToken ct) => Block<NeighborhoodCollaborationResponse?>();
    public Task<NeighborhoodCollaborationResponse?> ReceiptAsync(Guid request, string? id, CancellationToken ct) => Block<NeighborhoodCollaborationResponse?>();
    public Task<IReadOnlyList<NeighborhoodCollaborationOpportunityResponse>> OpportunitiesAsync(long id, CancellationToken ct) => Task.FromResult<IReadOnlyList<NeighborhoodCollaborationOpportunityResponse>>([]);
    public Task<IReadOnlyList<NeighborhoodCollaborationPublicHistoryResponse>> PublicHistoryAsync(long id, CancellationToken ct) => Task.FromResult<IReadOnlyList<NeighborhoodCollaborationPublicHistoryResponse>>([]);
    Task<NeighborhoodStorageListResponse> INeighborhoodStorageClient.ListAsync(string? region, int page, CancellationToken ct) => Task.FromResult(new NeighborhoodStorageListResponse { Items = [] });
    Task<IReadOnlyList<NeighborhoodStorageSpaceDto>> INeighborhoodStorageClient.MineAsync(int page, CancellationToken ct) => Task.FromResult<IReadOnlyList<NeighborhoodStorageSpaceDto>>([]);
    public Task<NeighborhoodStoragePublicDto?> PublicAsync(string id, CancellationToken ct) => Task.FromResult<NeighborhoodStoragePublicDto?>(null);
    public Task<NeighborhoodStorageSpaceDto?> PrivateAsync(string id, string? work, CancellationToken ct) => Task.FromResult<NeighborhoodStorageSpaceDto?>(null);
    public Task<NeighborhoodStorageSpaceDto?> SaveAsync(string? id, NeighborhoodStorageSpaceRequest request, CancellationToken ct) => Block<NeighborhoodStorageSpaceDto?>();
    public Task<NeighborhoodStorageSpaceDto?> StateAsync(string id, string action, NeighborhoodStorageMutationRequest request, CancellationToken ct) => Block<NeighborhoodStorageSpaceDto?>();
    public Task<NeighborhoodStorageSpaceDto?> ReserveAsync(string id, NeighborhoodStorageReservationRequest request, CancellationToken ct) => Block<NeighborhoodStorageSpaceDto?>();
    public Task<NeighborhoodStorageSpaceDto?> HandoverAsync(string id, string work, NeighborhoodStorageReservationActionRequest request, CancellationToken ct) => Block<NeighborhoodStorageSpaceDto?>();
    public Task<NeighborhoodStorageSpaceDto?> ReceiptAsync(Guid request, string? id, CancellationToken ct, string? work = null) => Block<NeighborhoodStorageSpaceDto?>();
    Task<NeighborhoodDeliveryResponse?> INeighborhoodDeliveryClient.ReadAsync(string id, CancellationToken ct) => Task.FromResult<NeighborhoodDeliveryResponse?>(null);
    Task<IReadOnlyList<NeighborhoodDeliveryResponse>> INeighborhoodDeliveryClient.MineAsync(int page, CancellationToken ct) => Task.FromResult<IReadOnlyList<NeighborhoodDeliveryResponse>>([]);
    public Task<신청개인정보동의증적Response?> ConsentAsync(신청개인정보동의기록Request request, CancellationToken ct) => Block<신청개인정보동의증적Response?>();
    public Task<NeighborhoodDeliveryQuoteResponse?> QuoteAsync(NeighborhoodDeliveryRequest request, CancellationToken ct) => Block<NeighborhoodDeliveryQuoteResponse?>();
    public Task<NeighborhoodDeliveryResponse?> CreateAsync(NeighborhoodDeliveryRequest request, CancellationToken ct) => Block<NeighborhoodDeliveryResponse?>();
    private static Task<T> Block<T>() => Task.FromException<T>(new InvalidOperationException("미리보기에서는 저장할 수 없습니다."));
}
