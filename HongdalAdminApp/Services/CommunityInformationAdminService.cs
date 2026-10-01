using System.Net.Http.Headers;
using System.Net.Http.Json;
using Hongdal.Contracts.Common.Community;
using Hongdal.Contracts.Common.Content;
using Hongdal.Contracts.Common.Metadata;
using Hongdal.Ui.Common.Areas.App.Services;

namespace HongdalAdminApp.Services;

[HongdalCodeMetadata(
    HongdalCodeFeatureKeys.CommunityAuthoringImage,
    HongdalCodeLayer.ClientAdapter,
    "관리자 App의 이미지 전용 client port를 Hongdal 관리자 HTTP API에 연결",
    ContractType = typeof(ICommunityAuthoringImageClient),
    FlowOrder = 21,
    Effects = HongdalCodeEffect.NetworkCall | HongdalCodeEffect.MayIncurExternalCost,
    Boundary = "관리자 인증 토큰만 서버에 전달하고 Kie.ai 자격 증명은 다루지 않습니다.")]
public sealed class CommunityInformationAdminService : ICommunityInformationReviewClient
{
    private const string BasePath = "api/v1/admin/content/information";
    private readonly HttpClient _httpClient;
    private readonly AdminAuthSession _session;

    public CommunityInformationAdminService(
        HttpClient httpClient,
        AdminAuthSession session)
    {
        _httpClient = httpClient;
        _session = session;
    }

    public async Task<IReadOnlyList<CommunityInformationSourceDto>> GetSourcesAsync(
        CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Get, $"{BasePath}/sources");
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<IReadOnlyList<CommunityInformationSourceDto>>(
                   cancellationToken: cancellationToken)
               ?? [];
    }

    public async Task<CommunityInformationCollectionResponse> GetCandidatesAsync(
        CommunityInformationCollectionQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        using var request = CreateRequest(HttpMethod.Get, BuildCandidatePath(query));
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CommunityInformationCollectionResponse>(
                   cancellationToken: cancellationToken)
               ?? new CommunityInformationCollectionResponse(DateTime.UtcNow, [], [], []);
    }

    public async Task<CommunityAuthoringAiDraftResponse> GenerateAiDraftAsync(
        CommunityAuthoringAiDraftRequest draftRequest,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draftRequest);
        using var request = CreateRequest(HttpMethod.Post, $"{BasePath}/authoring/ai-drafts");
        request.Content = JsonContent.Create(draftRequest);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CommunityAuthoringAiDraftResponse>(
                   cancellationToken: cancellationToken)
               ?? throw new InvalidOperationException("LLM 글 초안 응답이 비어 있습니다.");
    }

    public async Task<CommunityAuthoringImagePromptPlanResponse> PlanAuthoringImagePromptsAsync(
        CommunityAuthoringImagePromptPlanRequest planRequest,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(planRequest);
        using var request = CreateRequest(HttpMethod.Post, $"{BasePath}/authoring/images/prompt-plan");
        request.Content = JsonContent.Create(planRequest);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CommunityAuthoringImagePromptPlanResponse>(
                   cancellationToken: cancellationToken)
               ?? throw new InvalidOperationException("이미지 문맥 계획 응답이 비어 있습니다.");
    }

    public async Task<CommunityAuthoringImageTaskResponse> GenerateAuthoringImageAsync(
        CommunityAuthoringImageGenerateRequest imageRequest,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(imageRequest);
        using var request = CreateRequest(HttpMethod.Post, $"{BasePath}/authoring/images");
        request.Content = JsonContent.Create(imageRequest);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<CommunityAuthoringImageTaskResponse>(
                         cancellationToken: cancellationToken)
                     ?? throw new InvalidOperationException("이미지 생성 응답이 비어 있습니다.");
        return ResolveImageUrl(result);
    }

    public async Task<CommunityAuthoringImageTaskResponse?> GetAuthoringImageAsync(
        string jobCode,
        bool refreshProvider = true,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(
            HttpMethod.Get,
            $"{BasePath}/authoring/images/{Uri.EscapeDataString(jobCode)}?refreshProvider={refreshProvider.ToString().ToLowerInvariant()}");
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<CommunityAuthoringImageTaskResponse>(
            cancellationToken: cancellationToken);
        return result is null ? null : ResolveImageUrl(result);
    }

    public async Task<PlatformCommunityPostAttachmentResponse> AttachAuthoringImageAsync(
        string jobCode,
        long postId,
        string password,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(
            HttpMethod.Post,
            $"{BasePath}/authoring/images/{Uri.EscapeDataString(jobCode)}/post-attachments/{postId}");
        request.Content = JsonContent.Create(new CommunityAuthoringGeneratedImageAttachRequest
        {
            Password = password
        });
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PlatformCommunityPostAttachmentResponse>(
                   cancellationToken: cancellationToken)
               ?? throw new InvalidOperationException("생성 이미지 첨부 응답이 비어 있습니다.");
    }

    public async Task<IReadOnlyList<SocialMediaResearchSourceDto>> GetSocialMediaSourcesAsync(
        CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Get, $"{BasePath}/social-media/sources");
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<IReadOnlyList<SocialMediaResearchSourceDto>>(
                   cancellationToken: cancellationToken)
               ?? [];
    }

    public async Task<YouTubeSocialContextResearchResponse> ResearchYouTubeSocialContextAsync(
        YouTubeSocialContextResearchRequest researchRequest,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(researchRequest);
        using var request = CreateRequest(HttpMethod.Post, $"{BasePath}/youtube-social-context/draft");
        request.Content = JsonContent.Create(researchRequest);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<YouTubeSocialContextResearchResponse>(
                   cancellationToken: cancellationToken)
               ?? throw new InvalidOperationException("YouTube·SNS 조사 응답이 비어 있습니다.");
    }

    public async Task<YouTubeSocialContextWorkspaceDto?> GetYouTubeSocialContextWorkspaceByVideoAsync(
        string videoId,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(
            HttpMethod.Get,
            $"{BasePath}/youtube-social-context/workspaces/by-video/{Uri.EscapeDataString(videoId)}");
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<YouTubeSocialContextWorkspaceDto>(
                   cancellationToken: cancellationToken)
               ?? throw new InvalidOperationException("YouTube 글쓰기 작업공간 응답이 비어 있습니다.");
    }

    public async Task<IReadOnlyList<YouTubeSocialContextWorkspaceSummaryDto>> GetYouTubeSocialContextWorkspacesAsync(
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(
            HttpMethod.Get,
            $"{BasePath}/youtube-social-context/workspaces?take={Math.Clamp(take, 1, 100)}");
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<IReadOnlyList<YouTubeSocialContextWorkspaceSummaryDto>>(
                   cancellationToken: cancellationToken)
               ?? [];
    }

    public async Task<YouTubeSocialContextWorkspaceDto> SaveYouTubeSocialContextWorkspaceDraftAsync(
        string workspaceId,
        YouTubeSocialContextWorkspaceDraftUpdateRequest draftRequest,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(
            HttpMethod.Put,
            $"{BasePath}/youtube-social-context/workspaces/{Uri.EscapeDataString(workspaceId)}/draft");
        request.Content = JsonContent.Create(draftRequest);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<YouTubeSocialContextWorkspaceDto>(
                   cancellationToken: cancellationToken)
               ?? throw new InvalidOperationException("YouTube 글 초안 저장 응답이 비어 있습니다.");
    }

    public async Task<YouTubeSocialContextWorkspaceDto> LinkYouTubeSocialContextPublicationAsync(
        string workspaceId,
        YouTubeSocialContextPublicationLinkRequest publicationRequest,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(
            HttpMethod.Post,
            $"{BasePath}/youtube-social-context/workspaces/{Uri.EscapeDataString(workspaceId)}/publication-links");
        request.Content = JsonContent.Create(publicationRequest);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<YouTubeSocialContextWorkspaceDto>(
                   cancellationToken: cancellationToken)
               ?? throw new InvalidOperationException("YouTube 작업공간 게시글 연결 응답이 비어 있습니다.");
    }

    internal static string BuildCandidatePath(CommunityInformationCollectionQuery query)
    {
        var parameters = new List<string>();
        Add(parameters, "sourceKey", query.SourceKey);
        Add(parameters, "countryCode", query.CountryCode);
        Add(parameters, "reviewState", query.ReviewState);
        Add(parameters, "searchText", query.SearchText);
        Add(parameters, "startDate", query.StartDate?.ToString("yyyy-MM-dd"));
        Add(parameters, "endDate", query.EndDate?.ToString("yyyy-MM-dd"));
        parameters.Add($"take={Math.Clamp(query.Take, 1, 100)}");
        return $"{BasePath}/candidates?{string.Join("&", parameters)}";
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string path)
    {
        if (!_session.IsServerAdmin || string.IsNullOrWhiteSpace(_session.AccessToken))
        {
            throw new UnauthorizedAccessException("서버관리자 로그인이 필요합니다.");
        }

        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _session.AccessToken);
        return request;
    }

    private CommunityAuthoringImageTaskResponse ResolveImageUrl(CommunityAuthoringImageTaskResponse response)
    {
        if (string.IsNullOrWhiteSpace(response.ImageUrl)
            || Uri.TryCreate(response.ImageUrl, UriKind.Absolute, out _)
            || _httpClient.BaseAddress is null)
        {
            return response;
        }

        return response with
        {
            ImageUrl = new Uri(_httpClient.BaseAddress, response.ImageUrl).ToString()
        };
    }

    private static void Add(ICollection<string> parameters, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            parameters.Add($"{key}={Uri.EscapeDataString(value.Trim())}");
        }
    }
}
