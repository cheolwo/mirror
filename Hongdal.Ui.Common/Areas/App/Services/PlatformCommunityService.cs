using System.Net.Http.Json;
using System.Text.Json;
using Hongdal.Contracts.Common.Community;
using Hongdal.Contracts.Common.Customs;
using Hongdal.Contracts.Common.Metadata;
using Hongdal.Contracts.Common.Orderer;
using Hongdal.Contracts.Common.PublicData;
using Hongdal.Contracts.Common.Versioning;
using Microsoft.AspNetCore.Components.Forms;

namespace Hongdal.Ui.Common.Areas.App.Services;

[HongdalCommunityV0Module(
    HongdalCommunityV0ModuleKeys.Ui,
    HongdalModuleKind.ClientFeature,
    "공통 UI의 게시판·게시글·댓글·참여·원장 HTTP 요청을 서버 API에 연결",
    ReleaseStage = HongdalCommunityV0ReleaseStages.IndependentExecution,
    Boundary = "후속 버전 API client가 함께 있어도 0.0 화면은 CommunityTrustWorkflow와 사용자 직접 선택 경계를 따릅니다.")]
public partial class CommunityPlatformClient :
    ICommunityPostClient,
    ICommunityParticipationClient,
    ICommunityLedgerClient,
    ICommunityProcurementClient,
    ICommunityVoteClient
{
    private readonly HttpClient _httpClient;
    private readonly HongdalProtectedApiClient _protectedApiClient;

    public CommunityPlatformClient(
        HttpClient httpClient,
        HongdalProtectedApiClient protectedApiClient)
    {
        _httpClient = httpClient;
        _protectedApiClient = protectedApiClient;
    }

    private static void AddQueryValue(List<string> query, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            query.Add($"{name}={Uri.EscapeDataString(value.Trim())}");
        }
    }

    private static async Task EnsureCommunityWriteSucceededAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var fallback = $"요청을 처리하지 못했습니다. HTTP {(int)response.StatusCode}";
        string? message = null;
        try
        {
            var payload = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!string.IsNullOrWhiteSpace(payload))
            {
                using var document = JsonDocument.Parse(payload);
                var root = document.RootElement;
                message = ReadProblemText(root, "detail")
                          ?? ReadProblemText(root, "message")
                          ?? ReadProblemText(root, "title");
            }
        }
        catch (JsonException)
        {
            // An invalid error payload still returns a stable HTTP fallback below.
        }

        throw new HttpRequestException(message ?? fallback, null, response.StatusCode);
    }

    private static string? ReadProblemText(JsonElement root, string propertyName)
    {
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty(propertyName, out var property)
            || property.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var value = property.GetString()?.Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
