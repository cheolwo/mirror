using Hongdal.Contracts.Common.Community;
using Hongdal.Contracts.Common.Content;
using Hongdal.Ui.Common.Areas.App.Services;

namespace Hongdal.Ui.Common.Areas.App.ViewModels;

public sealed partial class CommunityInformationReviewPageViewModel
{
    public void OpenBlankDraft(string defaultNickname)
    {
        if (!Composer.Draft.HasContent)
        {
            MutualBenefit.Reset();
            EvidenceChart.Reset();
            Composer.Draft.Nickname = ResolveWritingNickname(defaultNickname);
            Composer.Draft.Category = CommunityBoardCatalog.Vow.DisplayName;
            Composer.Draft.WorkflowTag = "출처 기반 정보 공유";
            Composer.Draft.RoleTag = "운영자 정보 공유";
        }

        Composer.Open();
        StatusMessage = "글을 쓰면서 오른쪽 도구에서 자료와 다이어그램을 바로 추가할 수 있습니다.";
    }

    public void OpenVowDraft(string defaultNickname)
    {
        if (!Composer.Draft.HasContent)
        {
            var templateDraft = VowJourneyTemplate.BuildDraft(VowVersion.Selected);
            MutualBenefit.Reset();
            EvidenceChart.Reset();
            Composer.Draft.Nickname = ResolveWritingNickname(defaultNickname);
            Composer.Draft.Category = CommunityBoardCatalog.Vow.DisplayName;
            Composer.Draft.WorkflowTag = VowVersion.Selected.WorkflowTag;
            Composer.Draft.RoleTag = "운영자 서원 기록";
            Composer.Draft.Title = templateDraft.Title;
            Composer.Draft.Body = templateDraft.Body;
        }

        Composer.Open();
        Composer.OpenSettings();
        ActiveTool = CommunityAuthoringTool.VowJourneyTemplate;
        StatusMessage = $"{VowVersion.Selected.DisplayName} · {VowJourneyTemplate.Selected.DisplayName} 서원 초안을 열었습니다.";
    }

    public void SelectVowVersion(string code)
    {
        VowVersion.SelectedCode = code;
        StatusMessage = Composer.Draft.HasContent
            ? $"{VowVersion.Selected.DisplayName}을(를) 다음 서원 초안의 목표로 선택했습니다. 현재 작성 중인 글은 바꾸지 않습니다."
            : $"{VowVersion.Selected.DisplayName}을(를) 서원 목표 버전으로 선택했습니다.";
    }

    public void SelectVowJourneyTemplate(string key)
    {
        VowJourneyTemplate.SelectedKey = key;
        ActiveTool = CommunityAuthoringTool.VowJourneyTemplate;
        StatusMessage = Composer.Draft.HasContent
            ? $"{VowJourneyTemplate.Selected.DisplayName} 틀을 선택했습니다. 현재 글은 유지되며 명시적으로 추가할 때만 본문 뒤에 붙습니다."
            : $"{VowJourneyTemplate.Selected.DisplayName} 틀을 다음 서원 초안에 적용합니다.";
    }

    public bool ApplyVowJourneyTemplate(string defaultNickname)
    {
        if (!Composer.Draft.HasContent)
        {
            OpenVowDraft(defaultNickname);
            return true;
        }

        var template = VowJourneyTemplate.Selected;
        var templateDraft = VowJourneyTemplate.BuildDraft(VowVersion.Selected);
        if (!AppendBodySection(
                $"서원 여정 틀 · {template.DisplayName}",
                templateDraft.Body,
                allowTruncate: false))
        {
            return false;
        }

        EnsureComposerIdentity(defaultNickname);
        Composer.Open();
        ActiveTool = CommunityAuthoringTool.VowJourneyTemplate;
        Composer.SetStatus(
            $"{template.DisplayName} 틀을 현재 본문 뒤에 추가했습니다.",
            CommunityComposerMessageKind.Success);
        StatusMessage = $"기존 내용을 유지하고 {template.DisplayName} 틀을 본문에 추가했습니다.";
        return true;
    }

    public void SelectWritingPersona(string key)
    {
        WritingPersona.Select(key);
        Composer.Draft.Nickname = WritingPersona.Selected.Nickname;
        StatusMessage = $"{WritingPersona.Selected.Nickname} 필명으로 글을 쓸 준비를 했습니다.";
    }

    public void SelectNextWritingPersona()
    {
        WritingPersona.SelectNext();
        Composer.Draft.Nickname = WritingPersona.Selected.Nickname;
        StatusMessage = $"{WritingPersona.Selected.Nickname} 필명으로 바꿔습니다.";
    }

    public bool PrepareDraft(
        CommunityInformationCandidateDto candidate,
        string defaultNickname,
        bool replaceExisting = false)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        SelectCandidate(candidate);
        if (!replaceExisting
            && (Composer.Draft.HasContent || Composer.LocalDraftSavedAtUtc.HasValue))
        {
            PendingDraftCandidate = candidate;
            StatusMessage = "작성 중이거나 임시 저장된 초안이 있습니다.";
            return false;
        }

        ApplyCandidateToComposer(candidate, defaultNickname);
        return true;
    }

    public async Task ReplaceDraftAsync(
        string defaultNickname,
        CancellationToken cancellationToken = default)
    {
        if (PendingDraftCandidate is null)
        {
            return;
        }

        await Composer.DiscardDraftAsync(cancellationToken);
        ApplyCandidateToComposer(PendingDraftCandidate, defaultNickname);
        await Composer.SaveLocalDraftSilentlyAsync(cancellationToken);
    }

    public void ContinueExistingDraft(string defaultNickname)
    {
        PendingDraftCandidate = null;
        if (string.IsNullOrWhiteSpace(Composer.Draft.Nickname))
        {
            Composer.Draft.Nickname = ResolveWritingNickname(defaultNickname);
        }

        Composer.Open();
        Composer.OpenSettings();
        StatusMessage = null;
    }

    public async Task ClearDraftAsync(CancellationToken cancellationToken = default)
    {
        PendingDraftCandidate = null;
        var discarded = await Composer.DiscardDraftAsync(cancellationToken);
        MutualBenefit.Reset();
        EvidenceChart.Reset();
        AiDraft.ResetResult();
        ImageGenerator.Reset();
        StatusMessage = discarded
            ? "현재 화면과 browser 임시 저장의 초안을 비웠습니다."
            : "현재 화면은 비웠지만 browser 임시 저장 제거는 다시 확인해야 합니다.";
    }

    public async Task OpenLedgerPickerAsync(CancellationToken cancellationToken = default)
    {
        LedgerPicker.Open(Composer.Draft.커뮤니티원장Id);
        await LedgerPicker.LoadAsync(cancellationToken);
        StatusMessage = LedgerPicker.Items.Count == 0
            ? LedgerPicker.LoadMessage
            : "글에 연결할 내 원장 또는 공개 원장을 선택해 주세요.";
    }

    public void SelectLedger(PlatformCommunityPostLedgerChoiceResponse ledger)
    {
        ArgumentNullException.ThrowIfNull(ledger);
        LedgerPicker.PendingLedgerId = ledger.원장Id;
        StatusMessage = $"'{ledger.제목}' 원장을 선택했습니다. 내용을 확인하거나 글에 첨부할 수 있습니다.";
    }

    public bool AttachSelectedLedger()
    {
        var ledger = LedgerPicker.Items.FirstOrDefault(LedgerPicker.IsPending);
        if (ledger is null)
        {
            StatusMessage = "글에 첨부할 원장을 먼저 선택해 주세요.";
            return false;
        }

        Composer.Draft.커뮤니티원장Id = ledger.원장Id;
        Composer.Draft.WorkflowTag = CommunityWorkClassificationCatalog
            .FindByLedgerTemplate(ledger.원장템플릿Key)?.WorkflowTag
            ?? Composer.Draft.WorkflowTag;
        if (string.IsNullOrWhiteSpace(Composer.Draft.Title))
        {
            Composer.Draft.Title = Limit($"[{ledger.원장템플릿명}] {ledger.제목}", 160);
        }

        if (string.IsNullOrWhiteSpace(Composer.Draft.Body))
        {
            Composer.Draft.Body = Limit(
                $"{ledger.제목} 원장의 진행 상황과 함께 확인할 내용을 기록합니다.{Environment.NewLine}{Environment.NewLine}확인하고 싶은 내용: ",
                4000);
        }

        LedgerPicker.ReturnToCompose();
        Composer.Open();
        Composer.SetStatus(
            $"'{ledger.제목}' 원장을 글에 첨부했습니다.",
            CommunityComposerMessageKind.Success);
        StatusMessage = $"'{ledger.제목}' 원장을 글 초안에 연결했습니다.";
        return true;
    }

    public void ClearAttachedLedger()
    {
        Composer.Draft.커뮤니티원장Id = string.Empty;
        LedgerPicker.PendingLedgerId = null;
        Composer.SetStatus("글에서 원장 첨부를 해제했습니다.", CommunityComposerMessageKind.Info);
        StatusMessage = "글에서 원장 첨부를 해제했습니다.";
    }

    public bool AppendCandidateToDraft(
        CommunityInformationCandidateDto candidate,
        string defaultNickname)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        SelectCandidate(candidate);
        EnsureComposerIdentity(defaultNickname);

        if (Composer.Draft.Body.Contains(candidate.OriginalUrl, StringComparison.OrdinalIgnoreCase))
        {
            Composer.Open();
            Composer.SetStatus(
                "이 자료의 원문 주소가 이미 본문에 들어 있습니다.",
                CommunityComposerMessageKind.Warning);
            return false;
        }

        var added = AppendBodySection(
            $"참고 자료 · {candidate.Title}",
            BuildDraftBody(candidate));
        if (!added)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(Composer.Draft.SharedLinkUrl))
        {
            Composer.Draft.SharedLinkUrl = candidate.OriginalUrl;
        }

        Composer.Open();
        Composer.SetStatus(
            $"'{candidate.Title}' 자료를 현재 글 본문에 추가했습니다.",
            CommunityComposerMessageKind.Success);
        StatusMessage = "선택한 자료를 현재 글에 이어 붙였습니다.";
        return true;
    }

    public bool ApplySocialResearchToDraft(string defaultNickname)
    {
        var result = SocialResearch.Result;
        if (result is null)
        {
            StatusMessage = "먼저 YouTube·SNS 자료 조사를 실행해 주세요.";
            return false;
        }

        var hadContent = Composer.Draft.HasContent;
        EnsureComposerIdentity(defaultNickname);
        if (!hadContent)
        {
            MutualBenefit.Reset();
            EvidenceChart.Reset();
            Composer.Draft.Category = CommunityBoardCatalog.Vow.DisplayName;
            Composer.Draft.WorkflowTag = result.Draft.CollectiveAction.WorkflowTag;
            Composer.Draft.RoleTag = "운영자 정보 공유";
            Composer.Draft.Title = Limit(result.Draft.Title, 160);
            Composer.Draft.Body = Limit(result.Draft.Body, 4000);
            Composer.Draft.SharedLinkUrl = result.Video.OriginalUrl;
        }
        else if (!AppendBodySection("YouTube·SNS 함께 보기", result.Draft.Body))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(Composer.Draft.WorkflowTag)
            || Composer.Draft.WorkflowTag is "출처 기반 정보 공유" or "외부 공개 자료 공유")
        {
            Composer.Draft.WorkflowTag = result.Draft.CollectiveAction.WorkflowTag;
        }

        if (string.IsNullOrWhiteSpace(Composer.Draft.SharedLinkUrl))
        {
            Composer.Draft.SharedLinkUrl = result.Video.OriginalUrl;
        }

        Composer.Open();
        Composer.OpenSettings();
        Composer.SetStatus(
            hadContent
                ? "YouTube·SNS 조사 내용을 현재 글에 추가했습니다. 원문과 수집 한계를 다시 확인해 주세요."
                : "YouTube·SNS 조사 초안을 글쓰기로 옮겼습니다. 원문과 수집 한계를 다시 확인해 주세요.",
            CommunityComposerMessageKind.Success);
        StatusMessage = "YouTube·SNS 자료를 글 초안에 반영했습니다.";
        return true;
    }

    public async Task<bool> ApplySocialResearchToDraftAsync(
        string defaultNickname,
        CancellationToken cancellationToken = default)
    {
        if (!ApplySocialResearchToDraft(defaultNickname))
        {
            return false;
        }

        await SaveSocialWorkspaceDraftAsync(cancellationToken);
        return true;
    }

    public Task<bool> SaveSocialWorkspaceDraftAsync(
        CancellationToken cancellationToken = default)
    {
        var snapshot = Composer.Draft.CreateSnapshot(DateTime.UtcNow);
        return SaveSocialWorkspaceDraftAsync(snapshot, cancellationToken);
    }

    public bool LoadSocialWorkspaceJourney()
    {
        var workspace = SocialResearch.Workspace;
        if (workspace is null)
        {
            StatusMessage = "불러온 YouTube 작업공간이 없습니다.";
            return false;
        }

        if (!Diagram.LoadImportJourney(workspace.ImportJourney))
        {
            StatusMessage = "저장된 여정이 없어 현재 공동수입 기본 흐름을 유지합니다.";
            return false;
        }

        StatusMessage = "YouTube 영상에 연결해 둔 공동수입 여정과 업체 후보를 불러왔습니다.";
        return true;
    }

    public bool ApplyDiagramToDraft(
        string defaultNickname,
        IReadOnlyList<string> boardCategories)
    {
        CommunityComposerDraftTransition transition;
        try
        {
            transition = Diagram.CreateCommunityDraft(
                boardCategories,
                "Hongdal Admin",
                "운영자");
        }
        catch (InvalidOperationException exception)
        {
            StatusMessage = exception.Message;
            return false;
        }

        var hadContent = Composer.Draft.HasContent;
        EnsureComposerIdentity(defaultNickname);
        if (!hadContent)
        {
            MutualBenefit.Reset();
            EvidenceChart.Reset();
            Composer.Draft.Category = CommunityBoardCatalog.Vow.DisplayName;
            Composer.Draft.WorkflowTag = string.IsNullOrWhiteSpace(transition.WorkflowTag)
                ? CommunityLedgerTemplateCatalog.Find(transition.LedgerTemplateKey).WorkflowTag
                : transition.WorkflowTag;
            Composer.Draft.RoleTag = string.IsNullOrWhiteSpace(transition.RoleTag)
                ? "운영자 정보 공유"
                : transition.RoleTag;
            Composer.Draft.Title = Limit(transition.Title, 160);
            Composer.Draft.Body = Limit(transition.Body, 4000);
            Composer.Draft.IsReportBoardPost = transition.IsReportBoardPost;
        }
        else if (!AppendBodySection("함께 살펴볼 업무 흐름", transition.Body))
        {
            return false;
        }

        Composer.Open();
        Composer.OpenSettings();
        Composer.SetStatus(
            hadContent
                ? "빠른 흐름도를 현재 글에 추가했습니다. 게시 전 단계와 연결 설명을 확인해 주세요."
                : transition.StatusMessage,
            CommunityComposerMessageKind.Success);
        StatusMessage = "다이어그램을 글 초안에 반영했습니다.";
        return true;
    }

    public void OpenMutualBenefitTool()
    {
        MutualBenefit.PrepareFromDraft(Composer.Draft.Title);
        ActiveTool = CommunityAuthoringTool.MutualBenefit;
        StatusMessage = "영향을 받는 역할별 기대 편익, 부담과 미확정 조건을 함께 검토합니다.";
    }

    public int ImportDiagramRolesToMutualBenefit()
    {
        MutualBenefit.PrepareFromDraft(Composer.Draft.Title);
        var importedCount = MutualBenefit.ImportRoleSeeds(
            Diagram.OrganizationCandidates.Select(candidate =>
                new CommunityMutualBenefitRoleSeed(
                    candidate.RoleLabel,
                    candidate.OrganizationName)));
        ActiveTool = CommunityAuthoringTool.MutualBenefit;
        StatusMessage = MutualBenefit.StatusMessage;
        return importedCount;
    }

    public bool ApplyMutualBenefitToDraft(string defaultNickname)
    {
        MutualBenefit.PrepareFromDraft(Composer.Draft.Title);
        var assessment = MutualBenefit.Evaluate();
        if (assessment.RoleCount < 2 || string.IsNullOrWhiteSpace(MutualBenefit.SharedPurpose))
        {
            StatusMessage = "공동 목적과 영향을 받는 역할 두 개 이상을 먼저 적어 주세요.";
            return false;
        }

        var hadContent = Composer.Draft.HasContent;
        EnsureComposerIdentity(defaultNickname);
        var section = MutualBenefit.BuildDraftSection();
        if (section.Length > 4000)
        {
            StatusMessage = "상호 이익 검토가 본문 제한을 넘었습니다. 역할 설명을 간결하게 정리해 주세요.";
            Composer.SetStatus(StatusMessage, CommunityComposerMessageKind.Warning);
            return false;
        }

        if (!hadContent)
        {
            Composer.Draft.Category = CommunityBoardCatalog.Vow.DisplayName;
            Composer.Draft.WorkflowTag = "상호 이익 사전 검토";
            Composer.Draft.RoleTag = "운영자 서원 기록";
            Composer.Draft.Title = Limit($"[서원] {MutualBenefit.SharedPurpose.Trim()}", 160);
            Composer.Draft.Body = section;
        }
        else if (!AppendBodySection("상호 이익 사전 검토", section, allowTruncate: false))
        {
            return false;
        }

        Composer.Open();
        Composer.OpenSettings();
        Composer.SetStatus(
            assessment.IsMutualBenefitCandidate
                ? "상호 이익 후보 검토를 글에 추가했습니다. 실제 경제성과 최신 당사자 동의는 별도로 확인해 주세요."
                : "상호 이익 사전 검토를 글에 추가했습니다. 보완점과 미확정 조건을 숨기지 않고 함께 공개합니다.",
            assessment.IsMutualBenefitCandidate
                ? CommunityComposerMessageKind.Success
                : CommunityComposerMessageKind.Warning);
        StatusMessage = $"{MutualBenefit.AssessmentStatusLabel} 결과를 글 초안에 반영했습니다.";
        return true;
    }

    public void OpenEvidenceChartTool()
    {
        EvidenceChart.PrepareFromDraft(Composer.Draft.Title, Composer.Draft.Body);
        ActiveTool = CommunityAuthoringTool.EvidenceChart;
        StatusMessage = "주장과 수치, 출처, 기준일과 한계를 한 묶음으로 확인합니다.";
    }

    public void OpenPeriodStatisticsTool()
    {
        PeriodStatistics.PrepareFilters(SelectedSourceKey, CountryCode, SearchText);
        ActiveTool = CommunityAuthoringTool.PeriodStatistics;
        StatusMessage = "달력에서 고른 기간 안의 수집 자료를 구간별 통계로 확인합니다.";
    }

    public void OpenImageGeneratorTool()
    {
        ImageGenerator.PrepareFromDraft(Composer.Draft.Title, Composer.Draft.Body);
        ActiveTool = CommunityAuthoringTool.ImageGenerator;
        StatusMessage = "현재 글을 연속된 문맥으로 나눈 뒤 Kie.ai 이미지 프롬프트를 검토합니다.";
    }

    public void OpenAiDraftTool()
    {
        AiDraft.PrepareFilters(
            SelectedSourceKey,
            CountryCode,
            SearchText,
            Composer.Draft.Title);
        ActiveTool = CommunityAuthoringTool.AiDraft;
        StatusMessage = "허용한 자료 조회 도구와 현재 편집 문맥으로 검토용 글 초안을 만듭니다.";
    }

    public async Task<bool> GenerateAiDraftAsync(CancellationToken cancellationToken = default)
    {
        YouTubeSocialContextResearchRequest? socialResearchRequest = null;
        if (AiDraft.IncludeYouTubeSocialResearch)
        {
            try
            {
                socialResearchRequest = SocialResearch.CreateResearchRequest();
            }
            catch (ArgumentException exception)
            {
                AiDraft.SetInputError(exception.Message);
                StatusMessage = exception.Message;
                return false;
            }
        }

        var generated = await AiDraft.GenerateAsync(
            BuildAiDraftContextSections(),
            socialResearchRequest,
            cancellationToken);
        StatusMessage = AiDraft.StatusMessage;
        return generated;
    }

    public bool ApplyAiDraftToComposer(string defaultNickname)
    {
        var generatedDraft = AiDraft.Result?.Draft;
        if (!AiDraft.CanApply || generatedDraft is null)
        {
            StatusMessage = "먼저 검토 가능한 LLM 글 초안을 만들어 주세요.";
            return false;
        }

        var hadContent = Composer.Draft.HasContent;
        EnsureComposerIdentity(defaultNickname);
        if (!hadContent)
        {
            MutualBenefit.Reset();
            EvidenceChart.Reset();
            Composer.Draft.Category = generatedDraft.Category;
            Composer.Draft.WorkflowTag = generatedDraft.WorkflowTag;
            Composer.Draft.RoleTag = generatedDraft.RoleTag;
            Composer.Draft.Title = Limit(generatedDraft.Title, 160);
            Composer.Draft.Body = Limit(generatedDraft.Body, 4000);
            Composer.Draft.SharedLinkUrl = generatedDraft.SharedLinkUrl ?? string.Empty;
        }
        else if (!AppendBodySection("LLM 보조 초안", generatedDraft.Body, allowTruncate: false))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(Composer.Draft.SharedLinkUrl))
        {
            Composer.Draft.SharedLinkUrl = generatedDraft.SharedLinkUrl ?? string.Empty;
        }

        Composer.Open();
        Composer.OpenSettings();
        Composer.SetStatus(
            hadContent
                ? "LLM 보조 초안을 현재 본문 뒤에 추가했습니다. 근거와 표현을 다시 확인해 주세요."
                : "LLM 보조 초안을 글쓰기로 옮겼습니다. 게시 전에 근거와 표현을 다시 확인해 주세요.",
            CommunityComposerMessageKind.Success);
        StatusMessage = "LLM 결과를 검토 가능한 글 초안에 반영했습니다. 자동 게시되지는 않습니다.";
        return true;
    }

    public int ImportAiDiagramSteps()
    {
        var suggestedSteps = AiDraft.Result?.Draft?.SuggestedDiagramSteps ?? [];
        var importedCount = 0;
        foreach (var step in suggestedSteps.Take(8))
        {
            if (Diagram.Steps.Any(existing => string.Equals(
                    existing.Title.Trim(),
                    step.Trim(),
                    StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            Diagram.NewStepTitle = step;
            Diagram.NewStepDescription = "LLM 초안에서 제안된 검토 단계입니다. 실제 업무 전환 전에 담당자와 조건을 확인합니다.";
            if (Diagram.AddStep())
            {
                importedCount++;
            }
        }

        ActiveTool = CommunityAuthoringTool.Diagram;
        StatusMessage = importedCount > 0
            ? $"LLM 제안 단계 {importedCount:N0}개를 편집용 다이어그램에 추가했습니다."
            : "다이어그램에 새로 추가할 LLM 제안 단계가 없습니다.";
        return importedCount;
    }

    public bool ImportPeriodStatisticsToEvidenceChart()
    {
        var block = PeriodStatistics.CreateEvidenceBlock();
        if (block is null)
        {
            StatusMessage = "기간 통계를 먼저 만들고 값이 있는 구간을 두 개 이상 확인해 주세요.";
            return false;
        }

        var imported = EvidenceChart.ImportPeriodStatistics(block);
        ActiveTool = CommunityAuthoringTool.EvidenceChart;
        StatusMessage = EvidenceChart.StatusMessage;
        return imported;
    }

    public bool ImportMutualBenefitToEvidenceChart()
    {
        var imported = EvidenceChart.ImportMutualBenefit(MutualBenefit);
        ActiveTool = CommunityAuthoringTool.EvidenceChart;
        StatusMessage = EvidenceChart.StatusMessage;
        return imported;
    }

    public bool ApplyEvidenceChartToDraft(string defaultNickname)
    {
        var hadContent = Composer.Draft.HasContent;
        EnsureComposerIdentity(defaultNickname);
        var result = EvidenceChart.ApplyToDraft(Composer.Draft);
        if (!result.Succeeded)
        {
            StatusMessage = result.Message;
            return false;
        }

        if (!hadContent)
        {
            Composer.Draft.RoleTag = "운영자 서원 기록";
        }

        Composer.Open();
        Composer.OpenSettings();
        Composer.SetStatus(
            $"{result.Message} 게시 시 같은 데이터로 그래프와 요약 통계가 다시 표시됩니다.",
            CommunityComposerMessageKind.Success);
        StatusMessage = result.ReplacedExistingBlock
            ? "기존 그래프와 요약 통계를 새 내용으로 갱신했습니다."
            : "그래프와 요약 통계를 글 초안에 반영했습니다.";
        return true;
    }

    public void HandleComposerSaved(CommunityPostComposerSaveResult result)
    {
        if (!result.Succeeded || result.Post is null)
        {
            return;
        }

        PublishedPostId = result.WasScheduled ? null : result.Post.Id;
        PendingDraftCandidate = null;
        MutualBenefit.Reset();
        EvidenceChart.Reset();
        PeriodStatistics.Reset();
        AiDraft.ResetResult();
        var savedMessage = result.WasScheduled && result.ScheduledPublishAtUtc is DateTime scheduledAtUtc
            ? $"커뮤니티 글 #{result.Post.Id:N0}을 {scheduledAtUtc.ToLocalTime():yyyy-MM-dd HH:mm} 발행으로 예약했습니다."
            : $"커뮤니티 글 #{result.Post.Id:N0}을 등록했습니다.";
        StatusMessage = result.MessageKind == CommunityComposerMessageKind.Warning
            ? $"{savedMessage} {result.Message}"
            : savedMessage;
    }

    public async Task HandleComposerSavedAsync(
        CommunityPostComposerSaveResult result,
        CancellationToken cancellationToken = default)
    {
        HandleComposerSaved(result);
        if (result.WasScheduled)
        {
            await ScheduledPosts.RefreshAsync(cancellationToken);
        }

        if (!result.Succeeded || result.Post is null)
        {
            return;
        }

        string? imageAttachmentMessage = null;
        if (ImageGenerator.HasSelectedImage)
        {
            var attached = await ImageGenerator.AttachSelectedAsync(
                result.Post.Id,
                result.SubmissionPassword,
                Math.Max(0, 5 - result.AttachmentUploadAttemptedCount),
                cancellationToken);
            imageAttachmentMessage = ImageGenerator.StatusMessage
                                     ?? (attached
                                         ? "선택한 생성 이미지도 문맥 순서대로 게시글 사진에 첨부했습니다."
                                         : "일부 생성 이미지 첨부는 재시도가 필요합니다.");
            StatusMessage = $"{StatusMessage} {imageAttachmentMessage}";
        }

        if (result.SubmittedDraft is null || !SocialResearch.HasWorkspace)
        {
            return;
        }

        var draftSaved = await SaveSocialWorkspaceDraftAsync(
            result.SubmittedDraft,
            cancellationToken);
        if (!draftSaved)
        {
            StatusMessage = $"커뮤니티 글 #{result.Post.Id:N0}은 등록됐지만 YouTube 작업공간 초안은 재저장해야 합니다.";
            if (!string.IsNullOrWhiteSpace(imageAttachmentMessage))
            {
                StatusMessage = $"{StatusMessage} {imageAttachmentMessage}";
            }

            return;
        }

        var linked = await SocialResearch.LinkPublicationAsync(
            result.Post.Id,
            cancellationToken);
        StatusMessage = linked
            ? result.WasScheduled
                ? $"예약 글 #{result.Post.Id:N0}과 YouTube 작업공간을 연결했습니다."
                : $"커뮤니티 글 #{result.Post.Id:N0}과 YouTube 작업공간을 연결했습니다."
            : result.WasScheduled
                ? $"예약 글 #{result.Post.Id:N0}은 저장됐지만 YouTube 작업공간 연결은 재시도가 필요합니다."
                : $"커뮤니티 글 #{result.Post.Id:N0}은 등록됐지만 YouTube 작업공간 연결은 재시도가 필요합니다.";
        if (!string.IsNullOrWhiteSpace(imageAttachmentMessage))
        {
            StatusMessage = $"{StatusMessage} {imageAttachmentMessage}";
        }
    }

    public static string BuildDraftBody(CommunityInformationCandidateDto candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        var lines = new List<string>();
        if (!string.IsNullOrWhiteSpace(candidate.Summary))
        {
            lines.Add(candidate.Summary.Trim());
            lines.Add(string.Empty);
        }

        lines.Add($"자료 출처: {candidate.Provider}");
        if (candidate.ReferenceDate.HasValue)
        {
            lines.Add($"자료 기준일: {candidate.ReferenceDate:yyyy-MM-dd}");
        }
        else if (candidate.PublishedAtUtc.HasValue)
        {
            lines.Add($"원 게시일: {candidate.PublishedAtUtc:yyyy-MM-dd}");
        }

        if (!string.IsNullOrWhiteSpace(candidate.CurrencyCode)
            || !string.IsNullOrWhiteSpace(candidate.Unit))
        {
            lines.Add($"표시 기준: {string.Join(" · ", new[] { candidate.CurrencyCode, candidate.Unit }.Where(value => !string.IsNullOrWhiteSpace(value)))}");
        }

        lines.Add($"원문: {candidate.OriginalUrl}");
        lines.Add(string.Empty);
        lines.Add(candidate.SourceNotice.Trim());
        lines.Add($"확인할 점: {candidate.Limitations.Trim()}");
        lines.Add(string.Empty);
        lines.Add("이 자료를 보고 함께 확인하거나 나누고 싶은 생각을 적어 주세요.");
        return Limit(string.Join(Environment.NewLine, lines), 4000);
    }

    private void ApplyCandidateToComposer(
        CommunityInformationCandidateDto candidate,
        string defaultNickname)
    {
        Composer.Reset();
        MutualBenefit.Reset();
        EvidenceChart.Reset();
        Composer.Draft.Nickname = ResolveWritingNickname(defaultNickname);
        Composer.Draft.Category = CommunityBoardCatalog.Vow.DisplayName;
        Composer.Draft.WorkflowTag = ResolveWorkflowTag(candidate);
        Composer.Draft.RoleTag = "운영자 정보 공유";
        Composer.Draft.Title = Limit(BuildDraftTitle(candidate), 160);
        Composer.Draft.Body = BuildDraftBody(candidate);
        Composer.Draft.SharedLinkUrl = candidate.OriginalUrl;
        Composer.Draft.IsAuthorDisplayCountryPublic = false;
        Composer.Open();
        Composer.OpenSettings();
        PendingDraftCandidate = null;
        PublishedPostId = null;
        StatusMessage = "출처 정보를 포함한 글 초안을 만들었습니다.";
    }

    private void EnsureComposerIdentity(string defaultNickname)
    {
        if (string.IsNullOrWhiteSpace(Composer.Draft.Nickname))
        {
            Composer.Draft.Nickname = ResolveWritingNickname(defaultNickname);
        }

        if (string.IsNullOrWhiteSpace(Composer.Draft.RoleTag))
        {
            Composer.Draft.RoleTag = "운영자 정보 공유";
        }
    }

    private Task<bool> SaveSocialWorkspaceDraftAsync(
        CommunityPostComposerSnapshot snapshot,
        CancellationToken cancellationToken)
        => SocialResearch.SaveDraftAsync(
            snapshot.Nickname,
            snapshot.Category,
            snapshot.WorkflowTag,
            snapshot.RoleTag,
            snapshot.Title,
            snapshot.Body,
            snapshot.SharedLinkUrl,
            Diagram.CreateImportJourneyUpdate(),
            cancellationToken);

    private IReadOnlyList<CommunityAuthoringAiContextSectionDto> BuildAiDraftContextSections()
    {
        var sections = new List<CommunityAuthoringAiContextSectionDto>();
        if (Composer.Draft.HasContent)
        {
            sections.Add(new CommunityAuthoringAiContextSectionDto(
                "current-draft",
                "현재 작성 중인 글",
                $"제목: {Composer.Draft.Title}{Environment.NewLine}본문:{Environment.NewLine}{Composer.Draft.Body}"));
        }

        if (Diagram.Steps.Count > 0)
        {
            var diagramLines = Diagram.Steps.Select((step, index) =>
                    $"{index + 1}. {step.Title}: {step.Description}")
                .Concat(Diagram.OrganizationCandidates.Select(candidate =>
                    $"업체 후보: {candidate.OrganizationName} · 역할 {candidate.RoleLabel} · 국가 {candidate.CountryCode}"));
            sections.Add(new CommunityAuthoringAiContextSectionDto(
                "diagram",
                "현재 다이어그램과 업체 후보",
                string.Join(Environment.NewLine, diagramLines)));
        }

        if (!string.IsNullOrWhiteSpace(MutualBenefit.SharedPurpose) || MutualBenefit.Roles.Count > 0)
        {
            var roleLines = MutualBenefit.Roles.Select(role =>
                $"- {role.RoleLabel} · 편익: {role.ExpectedBenefit} · 부담: {role.ContributionOrBurden} · 조건: {role.RiskOrCondition}");
            sections.Add(new CommunityAuthoringAiContextSectionDto(
                "mutual-benefit",
                "상호 이익 사전 검토",
                $"공동 목적: {MutualBenefit.SharedPurpose}{Environment.NewLine}{string.Join(Environment.NewLine, roleLines)}"));
        }

        if (PeriodStatistics.HasResult)
        {
            sections.Add(new CommunityAuthoringAiContextSectionDto(
                "period-statistics",
                "기간 통계",
                string.Join(
                    Environment.NewLine,
                    PeriodStatistics.Buckets.Select(bucket =>
                        $"- {bucket.Label}: 자료 {bucket.RecordCount:N0}건, 수치 {bucket.NumericValueCount:N0}건, 값 {(bucket.Value.HasValue ? bucket.Value.Value.ToString("N2") : "없음")}"))));
        }

        if (SocialResearch.Result is { } socialResult)
        {
            sections.Add(new CommunityAuthoringAiContextSectionDto(
                "youtube-social-workspace",
                "현재 YouTube·SNS 조사 작업공간",
                $"영상: {socialResult.Video.Title}{Environment.NewLine}영상 주소: {socialResult.Video.OriginalUrl}{Environment.NewLine}조사 초안:{Environment.NewLine}{socialResult.Draft.Body}"));
        }

        if (EvidenceChart.HasPreview)
        {
            sections.Add(new CommunityAuthoringAiContextSectionDto(
                "evidence-chart",
                "현재 근거 그래프",
                $"제목: {EvidenceChart.Title}{Environment.NewLine}주장: {EvidenceChart.Claim}{Environment.NewLine}출처: {EvidenceChart.SourceLabel} {EvidenceChart.SourceUrl}{Environment.NewLine}한계: {EvidenceChart.Limitation}"));
        }

        if (sections.Count == 0)
        {
            sections.Add(new CommunityAuthoringAiContextSectionDto(
                "vow-direction",
                "서원 방향",
                $"목표 버전: {VowVersion.Selected.DisplayName}{Environment.NewLine}여정 틀: {VowJourneyTemplate.Selected.DisplayName}"));
        }

        return sections.Take(6).ToArray();
    }

    private bool AppendBodySection(
        string heading,
        string content,
        bool allowTruncate = true)
    {
        var prefix = string.IsNullOrWhiteSpace(Composer.Draft.Body)
            ? string.Empty
            : $"{Environment.NewLine}{Environment.NewLine}---{Environment.NewLine}{Environment.NewLine}";
        var section = $"{heading}{Environment.NewLine}{Environment.NewLine}{content.Trim()}";
        var remaining = 4000 - Composer.Draft.Body.Length - prefix.Length;
        if (remaining <= heading.Length + 2)
        {
            Composer.Open();
            Composer.SetStatus(
                "본문이 4,000자에 가까워 자료를 더 추가할 수 없습니다. 내용을 정리한 뒤 다시 시도해 주세요.",
                CommunityComposerMessageKind.Warning);
            StatusMessage = "본문 길이 제한 때문에 자료를 추가하지 못했습니다.";
            return false;
        }

        if (!allowTruncate && section.Length > remaining)
        {
            Composer.Open();
            Composer.SetStatus(
                "상호 이익 검토의 조건과 경계 문구가 잘리지 않도록 본문을 정리한 뒤 다시 추가해 주세요.",
                CommunityComposerMessageKind.Warning);
            StatusMessage = "본문 길이 제한 때문에 상호 이익 검토를 추가하지 못했습니다.";
            return false;
        }

        Composer.Draft.Body += prefix + (allowTruncate ? Limit(section, remaining) : section);
        return true;
    }

    private static string ResolveWorkflowTag(CommunityInformationCandidateDto candidate)
        => candidate.SourceKey switch
        {
            CommunityInformationSourceKeys.KamisPriceObservations => "농수산물 가격 정보",
            CommunityInformationSourceKeys.FishCooperativeGeneralStatistics => "수산업협동조합 통계 정보",
            CommunityInformationSourceKeys.YouTubeChannelVideos => "외부 공개 자료 공유",
            _ => "출처 기반 정보 공유"
        };

    private static string BuildDraftTitle(CommunityInformationCandidateDto candidate)
        => candidate.SourceType switch
        {
            CommunityInformationSourceTypes.Video => $"[영상 공유] {candidate.Title}",
            CommunityInformationSourceTypes.PublicData => $"[공공자료] {candidate.Title}",
            _ => $"[자료 공유] {candidate.Title}"
        };

    private string ResolveWritingNickname(string fallback)
        => WritingPersona.Selected.Nickname.Length <= 40
            ? WritingPersona.Selected.Nickname
            : NormalizeNickname(fallback);

    private static string NormalizeNickname(string value)
        => string.IsNullOrWhiteSpace(value) ? "홍달 운영자" : Limit(value.Trim(), 40);

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string Limit(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength];
}
