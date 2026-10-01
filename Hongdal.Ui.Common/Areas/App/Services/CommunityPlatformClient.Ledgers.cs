using System.Net.Http.Json;
using Hongdal.Contracts.Common.Community;
using Hongdal.Contracts.Common.Customs;
using Hongdal.Contracts.Common.Orderer;
using Hongdal.Contracts.Common.PublicData;
using Hongdal.Contracts.Common.Versioning;
using Microsoft.AspNetCore.Components.Forms;

namespace Hongdal.Ui.Common.Areas.App.Services;

public partial class CommunityPlatformClient
{
    public async Task<IReadOnlyList<PlatformCommunityPostLedgerChoiceResponse>> GetMyLedgersAsync(
        string? workflowTag = null,
        CancellationToken cancellationToken = default)
    {
        var path = "api/v1/community/posts/my-ledgers";
        if (!string.IsNullOrWhiteSpace(workflowTag))
        {
            path += $"?workflowTag={Uri.EscapeDataString(workflowTag)}";
        }

        using var response = await _protectedApiClient.GetAsync(path, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<IReadOnlyList<PlatformCommunityPostLedgerChoiceResponse>>(
                   cancellationToken: cancellationToken)
               ?? [];
    }

    public async Task<IReadOnlyList<PlatformCommunityPostLedgerChoiceResponse>> GetSharedLedgersAsync(
        string? workflowTag = null,
        CancellationToken cancellationToken = default)
    {
        var path = "api/v1/community/posts/shared-ledgers";
        if (!string.IsNullOrWhiteSpace(workflowTag))
        {
            path += $"?workflowTag={Uri.EscapeDataString(workflowTag)}";
        }

        using var response = await _protectedApiClient.GetAsync(path, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<IReadOnlyList<PlatformCommunityPostLedgerChoiceResponse>>(
                   cancellationToken: cancellationToken)
               ?? [];
    }

    public async Task<PlatformCommunityPostLedgerContextResponse?> GetLedgerContextAsync(
        string ledgerId,
        CancellationToken cancellationToken = default)
    {
        using var response = await _protectedApiClient.GetAsync(
            $"api/v1/community/posts/ledgers/{Uri.EscapeDataString(ledgerId)}/context",
            cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PlatformCommunityPostLedgerContextResponse>(
            cancellationToken: cancellationToken);
    }

    public async Task<커뮤니티원장공개설정Response?> GetLedgerSharingSettingsAsync(
        string ledgerId,
        CancellationToken cancellationToken = default)
    {
        using var response = await _protectedApiClient.GetAsync(
            $"api/v1/community/ledgers/{Uri.EscapeDataString(ledgerId)}/sharing",
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<커뮤니티원장공개설정Response>(cancellationToken: cancellationToken);
    }

    public async Task<커뮤니티원장공개설정Response?> UpdateLedgerSharingSettingsAsync(
        string ledgerId,
        커뮤니티원장공개설정변경Request request,
        CancellationToken cancellationToken = default)
    {
        using var response = await _protectedApiClient.PutAsProtectedJsonAsync(
            $"api/v1/community/ledgers/{Uri.EscapeDataString(ledgerId)}/sharing",
            request,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<커뮤니티원장공개설정Response>(cancellationToken: cancellationToken);
    }

    public async Task<CommunityLedgerRoleAccessSettingsResponse?> GetLedgerRoleAccessSettingsAsync(
        string ledgerId,
        CancellationToken cancellationToken = default)
    {
        using var response = await _protectedApiClient.GetAsync(
            $"api/v1/community/ledgers/{Uri.EscapeDataString(ledgerId)}/role-access",
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CommunityLedgerRoleAccessSettingsResponse>(
            cancellationToken: cancellationToken);
    }

    public async Task<CommunityLedgerRoleAccessSettingsResponse?> UpdateLedgerRoleAccessSettingsAsync(
        string ledgerId,
        CommunityLedgerRoleAccessUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        using var response = await _protectedApiClient.PutAsProtectedJsonAsync(
            $"api/v1/community/ledgers/{Uri.EscapeDataString(ledgerId)}/role-access",
            request,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CommunityLedgerRoleAccessSettingsResponse>(
            cancellationToken: cancellationToken);
    }

    public async Task<CommunityLedgerBlockAssignmentSettingsResponse?> GetLedgerBlockAssignmentsAsync(
        string ledgerId,
        string blockId,
        CancellationToken cancellationToken = default)
    {
        using var response = await _protectedApiClient.GetAsync(
            $"api/v1/community/ledgers/{Uri.EscapeDataString(ledgerId)}/blocks/{Uri.EscapeDataString(blockId)}/assignees",
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CommunityLedgerBlockAssignmentSettingsResponse>(
            cancellationToken: cancellationToken);
    }

    public async Task<CommunityLedgerBlockAssignmentSettingsResponse?> UpdateLedgerBlockAssignmentsAsync(
        string ledgerId,
        string blockId,
        CommunityLedgerBlockAssignmentUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        using var response = await _protectedApiClient.PutAsProtectedJsonAsync(
            $"api/v1/community/ledgers/{Uri.EscapeDataString(ledgerId)}/blocks/{Uri.EscapeDataString(blockId)}/assignees",
            request,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CommunityLedgerBlockAssignmentSettingsResponse>(
            cancellationToken: cancellationToken);
    }

    public async Task<커뮤니티원장재사용Response?> ReuseSharedLedgerAsync(
        string ledgerId,
        string? newTitle = null,
        CancellationToken cancellationToken = default)
    {
        using var response = await _protectedApiClient.PostAsProtectedJsonAsync(
            $"api/v1/community/ledgers/{Uri.EscapeDataString(ledgerId)}/sharing/reuse",
            new 커뮤니티원장재사용Request { 새제목 = newTitle },
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<커뮤니티원장재사용Response>(cancellationToken: cancellationToken);
    }
}
