using CommunityToolkit.Mvvm.ComponentModel;
using Hongdal.Contracts.Common.Community;
using Hongdal.Ui.Common.Areas.App.Services;

namespace Hongdal.Ui.Common.Areas.App.ViewModels;

public sealed class PlatformCommunityCommentForm : ObservableObject
{
    private string _nickname = string.Empty;
    private string _password = string.Empty;
    private string _body = string.Empty;

    public string Nickname { get => _nickname; set => SetProperty(ref _nickname, value ?? string.Empty); }
    public string Password { get => _password; set => SetProperty(ref _password, value ?? string.Empty); }
    public string Body { get => _body; set => SetProperty(ref _body, value ?? string.Empty); }
    public bool IsValid => !string.IsNullOrWhiteSpace(Nickname)
                           && !string.IsNullOrWhiteSpace(Password)
                           && !string.IsNullOrWhiteSpace(Body);
}

/// <summary>
/// 게시글 댓글, 관심 역할과 가원장 참여 상태를 게시글별로 관리합니다.
/// </summary>
public sealed class PlatformCommunityPostEngagementViewModel(
    PlatformCommunityService communityService,
    CommunityPostJourneyCollectionViewModel? journeys = null) : ObservableObject
{
    private string? _selectedSeedPostTitle;

    public string RecommendationSessionKey { get; } = Guid.NewGuid().ToString("N");
    public Dictionary<long, PlatformCommunityCommentForm> CommentForms { get; } = [];
    public Dictionary<long, PlatformCommunityCommentForm> AttachmentCommentForms { get; } = [];
    public Dictionary<long, CommunityPostOpportunityListResponse> Opportunities { get; } = [];
    public CommunityPostJourneyCollectionViewModel Journeys { get; } = journeys ?? new();
    public Dictionary<long, HashSet<string>> SelectedParticipationOptionIds { get; } = [];
    public HashSet<long> PendingPostParticipationIds { get; } = [];
    public HashSet<long> ExpandedCommentPostIds { get; } = [];

    public string? SelectedSeedPostTitle
    {
        get => _selectedSeedPostTitle;
        set => SetProperty(ref _selectedSeedPostTitle, value);
    }

    public PlatformCommunityCommentForm GetCommentForm(long postId)
        => GetOrCreateForm(CommentForms, postId);

    public PlatformCommunityCommentForm GetAttachmentCommentForm(long attachmentId)
        => GetOrCreateForm(AttachmentCommentForms, attachmentId);

    public HashSet<string> GetParticipationOptionSelection(long postId)
    {
        if (!SelectedParticipationOptionIds.TryGetValue(postId, out var selected))
        {
            selected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            SelectedParticipationOptionIds[postId] = selected;
        }

        return selected;
    }

    public bool IsParticipationPending(long postId)
        => PendingPostParticipationIds.Contains(postId);

    public bool IsParticipationRoleSelected(long postId, string optionId)
        => !string.IsNullOrWhiteSpace(optionId)
           && SelectedParticipationOptionIds.TryGetValue(postId, out var selected)
           && selected.Contains(optionId);

    public bool IsCommentsExpanded(long postId)
        => ExpandedCommentPostIds.Contains(postId);

    public void ToggleComments(long postId)
    {
        if (!ExpandedCommentPostIds.Add(postId))
        {
            ExpandedCommentPostIds.Remove(postId);
        }

        OnPropertyChanged(nameof(ExpandedCommentPostIds));
    }

    public void SetOpportunity(long postId, CommunityPostOpportunityListResponse? opportunity)
    {
        if (opportunity is null)
        {
            Opportunities.Remove(postId);
        }
        else
        {
            Opportunities[postId] = opportunity;
        }

        Journeys.Set(postId, opportunity?.Journey);

        OnPropertyChanged(nameof(Opportunities));
    }

    public async Task RefreshOpportunityAsync(
        long postId,
        CancellationToken cancellationToken = default)
    {
        var result = await communityService.GetPostOpportunitiesAsync(
            postId,
            cancellationToken: cancellationToken);
        SetOpportunity(postId, result);
    }

    public async Task<PlatformCommunityCommandResult> StartParticipationAsync(
        long postId,
        CancellationToken cancellationToken = default)
    {
        if (!PendingPostParticipationIds.Add(postId))
        {
            return new(false);
        }

        try
        {
            await communityService.StartPostParticipationAsync(
                postId,
                new StartCommunityPostParticipationRequest
                {
                    DisplayLanguageCode = CommunityDisplayLanguageCodes.Korean,
                    ConfirmExplicitStart = true,
                    ConfirmNonBindingParticipation = true
                },
                cancellationToken);
            await RefreshOpportunityAsync(postId, cancellationToken);
            return new(
                true,
                "게시글에서 비구속적 관심 모집을 시작했습니다.",
                CommunityComposerMessageKind.Success);
        }
        catch (HttpRequestException ex) when (
            ex.StatusCode is System.Net.HttpStatusCode.Unauthorized
                or System.Net.HttpStatusCode.Forbidden)
        {
            return new(
                false,
                "관심 모집을 시작하려면 먼저 로그인해 주세요.",
                CommunityComposerMessageKind.Warning);
        }
        catch (Exception ex)
        {
            return new(
                false,
                $"관심 모집을 시작하지 못했습니다: {ex.Message}",
                CommunityComposerMessageKind.Error);
        }
        finally
        {
            PendingPostParticipationIds.Remove(postId);
        }
    }

    public async Task<PlatformCommunityCommandResult> ToggleParticipationRoleAsync(
        long postId,
        CommunityPostParticipationRoleOptionResponse role,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(role.OptionId)
            || !Opportunities.TryGetValue(postId, out var opportunity)
            || opportunity.Participation.InterestVoteId is not Guid voteId
            || !PendingPostParticipationIds.Add(postId))
        {
            return new(false);
        }

        var selected = GetParticipationOptionSelection(postId);
        var wasSelected = selected.Contains(role.OptionId);
        if (wasSelected && selected.Count == 1)
        {
            PendingPostParticipationIds.Remove(postId);
            return new(
                false,
                "관심 역할은 하나 이상 남겨야 합니다.",
                CommunityComposerMessageKind.Info);
        }

        if (wasSelected)
        {
            selected.Remove(role.OptionId);
        }
        else
        {
            selected.Add(role.OptionId);
        }

        try
        {
            var displayName = CommentForms.TryGetValue(postId, out var commentForm)
                              && !string.IsNullOrWhiteSpace(commentForm.Nickname)
                ? commentForm.Nickname.Trim()
                : "관심 참여자";
            await communityService.CastCommunityVoteAsync(
                voteId,
                new CommunityVoteCastRequest
                {
                    VoterKey = $"community-interest:{RecommendationSessionKey}",
                    VoterDisplayName = displayName,
                    OptionIds = selected.ToArray()
                },
                cancellationToken);
            await RefreshOpportunityAsync(postId, cancellationToken);
            return new(
                true,
                "가능한 참여 역할을 반영했습니다.",
                CommunityComposerMessageKind.Success);
        }
        catch (Exception ex)
        {
            if (wasSelected)
            {
                selected.Add(role.OptionId);
            }
            else
            {
                selected.Remove(role.OptionId);
            }

            return new(
                false,
                $"관심 역할을 반영하지 못했습니다: {ex.Message}",
                CommunityComposerMessageKind.Error);
        }
        finally
        {
            PendingPostParticipationIds.Remove(postId);
        }
    }

    public async Task<PlatformCommunityCommandResult> PromoteParticipationAsync(
        long postId,
        PromoteCommunityPostParticipationRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await communityService.PromotePostParticipationAsync(
                postId,
                request,
                cancellationToken);
            await RefreshOpportunityAsync(postId, cancellationToken);
            return new(
                true,
                result?.ReusedExistingProvisionalLedger == true
                    ? "이미 만들어진 가원장을 다시 확인했습니다."
                    : "모인 관심을 비구속적 가원장으로 기록하고 참여자 알림을 요청했습니다.",
                CommunityComposerMessageKind.Success,
                RefreshPosts: true);
        }
        catch (HttpRequestException ex) when (
            ex.StatusCode is System.Net.HttpStatusCode.Unauthorized
                or System.Net.HttpStatusCode.Forbidden)
        {
            return new(
                false,
                "로그인한 게시글 작성자만 가원장을 만들 수 있습니다.",
                CommunityComposerMessageKind.Warning);
        }
        catch (Exception ex)
        {
            return new(
                false,
                $"가원장을 만들지 못했습니다: {ex.Message}",
                CommunityComposerMessageKind.Error);
        }
    }

    public async Task<PlatformCommunityCommandResult> JoinProfessionalRoleAsync(
        long postId,
        string provisionalLedgerId,
        string roleCode,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await communityService.JoinPostProfessionalRoleAsync(
                postId,
                new JoinCommunityPostProfessionalRequest
                {
                    ProvisionalLedgerId = provisionalLedgerId,
                    ProfessionalRoleCode = roleCode,
                    DisplayLanguageCode = CommunityDisplayLanguageCodes.Korean,
                    ConfirmProfessionalCapacity = true,
                    ConfirmVoluntaryNonBindingParticipation = true,
                    ConfirmParticipantNotification = true
                },
                cancellationToken);
            await RefreshOpportunityAsync(postId, cancellationToken);
            return new(
                true,
                result?.ReusedExistingParticipation == true
                    ? "이미 참여 중인 역할을 다시 확인했습니다."
                    : "거래 참여팀 역할 참여를 기록하고 기존 참여자 알림을 요청했습니다.",
                CommunityComposerMessageKind.Success,
                RefreshPosts: true);
        }
        catch (HttpRequestException ex) when (
            ex.StatusCode is System.Net.HttpStatusCode.Unauthorized
                or System.Net.HttpStatusCode.Forbidden)
        {
            return new(
                false,
                "현재 계정의 플랫폼 프로필에서 이 역할을 확인할 수 없습니다.",
                CommunityComposerMessageKind.Warning);
        }
        catch (Exception ex)
        {
            return new(
                false,
                $"역할 참여를 기록하지 못했습니다: {ex.Message}",
                CommunityComposerMessageKind.Error);
        }
    }

    public async Task<PlatformCommunityCommandResult> JoinPartyRoleAsync(
        long postId,
        string provisionalLedgerId,
        string roleCode,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await communityService.JoinPostPartyRoleAsync(
                postId,
                new JoinCommunityPostPartyRoleRequest
                {
                    ProvisionalLedgerId = provisionalLedgerId,
                    PartyRoleCode = roleCode,
                    DisplayLanguageCode = CommunityDisplayLanguageCodes.Korean,
                    ConfirmRoleCapacity = true,
                    ConfirmVoluntaryNonBindingParticipation = true,
                    ConfirmParticipantNotification = true
                },
                cancellationToken);
            await RefreshOpportunityAsync(postId, cancellationToken);
            return new(
                true,
                result?.ReusedExistingParticipation == true
                    ? "이미 수락한 거래 역할을 다시 확인했습니다."
                    : "비구속적 거래 역할 수락을 기록하고 기존 참여자 알림을 요청했습니다.",
                CommunityComposerMessageKind.Success,
                RefreshPosts: true);
        }
        catch (HttpRequestException ex) when (
            ex.StatusCode is System.Net.HttpStatusCode.Unauthorized
                or System.Net.HttpStatusCode.Forbidden)
        {
            return new(
                false,
                "거래 역할을 수락하려면 로그인해야 합니다.",
                CommunityComposerMessageKind.Warning);
        }
        catch (Exception ex)
        {
            return new(
                false,
                $"거래 역할 수락을 기록하지 못했습니다: {ex.Message}",
                CommunityComposerMessageKind.Error);
        }
    }

    public async Task<PlatformCommunityCommandResult> RecommendAsync(
        long postId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await communityService.RecommendAsync(postId, RecommendationSessionKey, cancellationToken);
            return new(true, RefreshPosts: true);
        }
        catch (Exception ex)
        {
            return new(
                false,
                $"추천 처리에 실패했습니다: {ex.Message}",
                CommunityComposerMessageKind.Error);
        }
    }

    public async Task<PlatformCommunityCommandResult> SetOperatorPinAsync(
        long postId,
        bool isPinned,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await communityService.SetOperatorPinAsync(postId, isPinned, cancellationToken);
            return new(true, RefreshPosts: true);
        }
        catch (Exception ex)
        {
            return new(
                false,
                $"상단 고정 처리에 실패했습니다: {ex.Message}",
                CommunityComposerMessageKind.Error);
        }
    }

    public async Task<PlatformCommunityCommandResult> SaveCommentAsync(
        long postId,
        CancellationToken cancellationToken = default)
    {
        var form = GetCommentForm(postId);
        if (!form.IsValid)
        {
            return new(
                false,
                "댓글 닉네임, 비밀번호, 내용을 입력하세요.",
                CommunityComposerMessageKind.Warning);
        }

        try
        {
            await communityService.CreateCommentAsync(
                postId,
                new PlatformCommunityPostCommentCreateRequest
                {
                    Nickname = form.Nickname,
                    Password = form.Password,
                    Body = form.Body
                },
                cancellationToken);
            CommentForms.Remove(postId);
            return new(true, RefreshPosts: true);
        }
        catch (Exception ex)
        {
            return new(
                false,
                $"댓글 등록에 실패했습니다: {ex.Message}",
                CommunityComposerMessageKind.Error);
        }
    }

    public async Task<PlatformCommunityCommandResult> DeleteCommentAsync(
        long postId,
        long commentId,
        CancellationToken cancellationToken = default)
    {
        var form = GetCommentForm(postId);
        if (string.IsNullOrWhiteSpace(form.Password))
        {
            return new(
                false,
                "Enter the comment password before deleting.",
                CommunityComposerMessageKind.Warning);
        }

        try
        {
            await communityService.DeleteCommentAsync(postId, commentId, form.Password, cancellationToken);
            return new(true, RefreshPosts: true);
        }
        catch (Exception ex)
        {
            return new(
                false,
                $"Comment delete failed: {ex.Message}",
                CommunityComposerMessageKind.Error);
        }
    }

    public async Task<PlatformCommunityCommandResult> ReportCommentAsync(
        long commentId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await communityService.ReportCommentAsync(commentId, cancellationToken);
            return new(true, "Comment report received.", CommunityComposerMessageKind.Info);
        }
        catch (Exception ex)
        {
            return new(
                false,
                $"Comment report failed: {ex.Message}",
                CommunityComposerMessageKind.Error);
        }
    }

    public async Task<PlatformCommunityCommandResult> SaveAttachmentCommentAsync(
        long attachmentId,
        CancellationToken cancellationToken = default)
    {
        var form = GetAttachmentCommentForm(attachmentId);
        if (!form.IsValid)
        {
            return new(
                false,
                "Image comment nickname, password, and body are required.",
                CommunityComposerMessageKind.Warning);
        }

        try
        {
            await communityService.CreateAttachmentCommentAsync(
                attachmentId,
                new PlatformCommunityPostAttachmentCommentCreateRequest
                {
                    Nickname = form.Nickname,
                    Password = form.Password,
                    Body = form.Body
                },
                cancellationToken);
            AttachmentCommentForms.Remove(attachmentId);
            return new(true, RefreshPosts: true);
        }
        catch (Exception ex)
        {
            return new(
                false,
                $"Image comment failed: {ex.Message}",
                CommunityComposerMessageKind.Error);
        }
    }

    public async Task<PlatformCommunityCommandResult> DeleteAttachmentCommentAsync(
        long attachmentId,
        long commentId,
        CancellationToken cancellationToken = default)
    {
        var form = GetAttachmentCommentForm(attachmentId);
        if (string.IsNullOrWhiteSpace(form.Password))
        {
            return new(
                false,
                "Enter the image comment password before deleting.",
                CommunityComposerMessageKind.Warning);
        }

        try
        {
            await communityService.DeleteAttachmentCommentAsync(
                attachmentId,
                commentId,
                form.Password,
                cancellationToken);
            return new(true, RefreshPosts: true);
        }
        catch (Exception ex)
        {
            return new(
                false,
                $"Image comment delete failed: {ex.Message}",
                CommunityComposerMessageKind.Error);
        }
    }

    public async Task<PlatformCommunityCommandResult> ReportAttachmentCommentAsync(
        long commentId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await communityService.ReportAttachmentCommentAsync(commentId, cancellationToken);
            return new(true, "Image comment report received.", CommunityComposerMessageKind.Info);
        }
        catch (Exception ex)
        {
            return new(
                false,
                $"Image comment report failed: {ex.Message}",
                CommunityComposerMessageKind.Error);
        }
    }

    private static PlatformCommunityCommentForm GetOrCreateForm(
        IDictionary<long, PlatformCommunityCommentForm> forms,
        long key)
    {
        if (!forms.TryGetValue(key, out var form))
        {
            form = new PlatformCommunityCommentForm();
            forms[key] = form;
        }

        return form;
    }
}
