using Hongdal.Contracts.Common.Community;
using Hongdal.Contracts.Common.Content;
using Hongdal.Contracts.Common.Operations;
using Hongdal.Ui.Common.Areas.App.Services;
using Hongdal.Ui.Common.Areas.App.ViewModels;

namespace Hongdal.Tests.Ui.Common;

public sealed class CommunityInformationReviewPageViewModelTests
{
    [Fact]
    public async Task Initialize_LoadsSourcesAndCandidatesThroughSelectedFilters()
    {
        var client = new RecordingClient(
            [Source(CommunityInformationSourceKeys.KamisPriceObservations)],
            [KamisCandidate()]);
        using var viewModel = CreateViewModel(client);
        viewModel.CountryCode = "KR";
        viewModel.SearchText = "사과";

        await viewModel.InitializeAsync();

        Assert.Single(viewModel.Sources);
        Assert.Single(viewModel.Candidates);
        Assert.Equal("KR", client.LastQuery?.CountryCode);
        Assert.Equal("사과", client.LastQuery?.SearchText);
        Assert.Equal(100, client.LastQuery?.Take);
    }

    [Fact]
    public void KamisCandidate_PreparesEditableInformationPostDraftWithSourceBoundary()
    {
        using var viewModel = CreateViewModel(new RecordingClient([], []));
        var candidate = KamisCandidate();

        var prepared = viewModel.PrepareDraft(candidate, "운영자 홍길동");

        Assert.True(prepared);
        Assert.True(viewModel.Composer.IsOpen);
        Assert.True(viewModel.Composer.IsSettingsOpen);
        Assert.Equal(CommunityBoardCatalog.Vow.DisplayName, viewModel.Composer.Draft.Category);
        Assert.Equal("운영자 정보 공유", viewModel.Composer.Draft.RoleTag);
        Assert.StartsWith("[공공자료]", viewModel.Composer.Draft.Title);
        Assert.Contains("자료 기준일: 2026-07-17", viewModel.Composer.Draft.Body);
        Assert.Contains("표시 기준: KRW · 10개", viewModel.Composer.Draft.Body);
        Assert.Contains("판매 권고", viewModel.Composer.Draft.Body);
        Assert.Equal(candidate.OriginalUrl, viewModel.Composer.Draft.SharedLinkUrl);
        Assert.Equal(string.Empty, viewModel.Composer.Draft.Password);
    }

    [Fact]
    public async Task ExistingDraft_IsNotReplacedUntilOperatorConfirms()
    {
        using var viewModel = CreateViewModel(new RecordingClient([], []));
        viewModel.Composer.Draft.Title = "작성 중인 글";
        var video = VideoCandidate();

        var prepared = viewModel.PrepareDraft(video, "관리자");

        Assert.False(prepared);
        Assert.True(viewModel.HasDraftConflict);
        Assert.Equal("작성 중인 글", viewModel.Composer.Draft.Title);

        await viewModel.ReplaceDraftAsync("관리자");

        Assert.False(viewModel.HasDraftConflict);
        Assert.Equal(CommunityBoardCatalog.Vow.DisplayName, viewModel.Composer.Draft.Category);
        Assert.StartsWith("[영상 공유]", viewModel.Composer.Draft.Title);
        Assert.Contains("제작자가 작성한 정보", viewModel.Composer.Draft.Body);
    }

    [Fact]
    public void Candidate_CanBeAppendedToCurrentDraftWithoutReplacingExistingText()
    {
        using var viewModel = CreateViewModel(new RecordingClient([], []));
        viewModel.OpenBlankDraft("관리자");
        viewModel.Composer.Draft.Title = "지역 사과를 같이 살펴봅니다";
        viewModel.Composer.Draft.Body = "먼저 적어 둔 생각입니다.";

        var added = viewModel.AppendCandidateToDraft(KamisCandidate(), "관리자");

        Assert.True(added);
        Assert.StartsWith("먼저 적어 둔 생각입니다.", viewModel.Composer.Draft.Body, StringComparison.Ordinal);
        Assert.Contains("참고 자료 · 사과 (후지 · 상품)", viewModel.Composer.Draft.Body, StringComparison.Ordinal);
        Assert.Contains("https://www.kamis.or.kr/service/price/xml.do", viewModel.Composer.Draft.Body, StringComparison.Ordinal);
    }

    [Fact]
    public void SelectedLedger_IsAttachedToAdminDraftAndReturnsToComposer()
    {
        using var viewModel = CreateViewModel(new RecordingClient([], []));
        var ledger = new PlatformCommunityPostLedgerChoiceResponse
        {
            원장Id = "ledger-group-import-17",
            원장템플릿Key = CommunityLedgerTemplateKeys.GroupImport,
            원장템플릿명 = "공동수입 원장",
            제목 = "호주 식재료 공동수입 검토",
            WorkflowTag = "공동수입"
        };
        viewModel.LedgerPicker.ReplaceItems([ledger]);
        viewModel.LedgerPicker.Open(null);
        viewModel.SelectLedger(ledger);

        var attached = viewModel.AttachSelectedLedger();

        Assert.True(attached);
        Assert.Equal(ledger.원장Id, viewModel.Composer.Draft.커뮤니티원장Id);
        Assert.Contains(ledger.제목, viewModel.Composer.Draft.Title, StringComparison.Ordinal);
        Assert.True(viewModel.Composer.IsOpen);
        Assert.False(viewModel.LedgerPicker.IsPickerOpen);
    }

    [Fact]
    public async Task LlmDraft_DoesNotChangeComposerUntilOperatorAppliesIt()
    {
        var aiResponse = new CommunityAuthoringAiDraftResponse(
            true,
            CommunityAuthoringAiDraftStatusCodes.ReadyForReview,
            "검토용 초안을 만들었습니다.",
            new CommunityAuthoringAiPostDraftDto(
                "[서원] 지역 사과를 함께 살펴봅니다",
                "공개 가격 자료와 아직 확인할 조건을 함께 적습니다.\n\n확인한 출처\n- https://example.com/price",
                CommunityBoardCatalog.Vow.DisplayName,
                "공동구매 사전 검토",
                "운영자 서원 기록",
                "https://example.com/price",
                ["https://example.com/price"],
                ["수요 확인"],
                ["최소 수량은 얼마인가요?"]),
            [],
            [],
            true,
            false,
            "fake-model",
            0.01m,
            0.02m,
            20m);
        var client = new RecordingClient([], [], aiDraftResponse: aiResponse);
        using var viewModel = CreateViewModel(client);
        viewModel.AiDraft.Objective = "사과 공동구매를 근거와 함께 검토한다.";

        var generated = await viewModel.GenerateAiDraftAsync();

        Assert.True(generated);
        Assert.False(viewModel.Composer.Draft.HasContent);
        Assert.NotNull(client.LastAiDraftRequest);

        var applied = viewModel.ApplyAiDraftToComposer("관리자");

        Assert.True(applied);
        Assert.True(viewModel.Composer.IsOpen);
        Assert.Equal("[서원] 지역 사과를 함께 살펴봅니다", viewModel.Composer.Draft.Title);
        Assert.Contains("확인한 출처", viewModel.Composer.Draft.Body);
    }

    [Fact]
    public void Diagram_AppliesTemplateFlowToEmptyDraft()
    {
        using var viewModel = CreateViewModel(new RecordingClient([], []));

        var applied = viewModel.ApplyDiagramToDraft(
            "관리자",
            CommunityBoardCatalog.PublicBoards.Select(board => board.DisplayName).ToArray());

        Assert.True(applied);
        Assert.True(viewModel.Composer.IsOpen);
        Assert.Equal(CommunityBoardCatalog.Vow.DisplayName, viewModel.Composer.Draft.Category);
        Assert.NotEmpty(viewModel.Composer.Draft.Title);
        Assert.Contains("다이어그램에서 시작한 커뮤니티 글입니다", viewModel.Composer.Draft.Body, StringComparison.Ordinal);
        Assert.NotEmpty(viewModel.Composer.Draft.WorkflowTag);
    }

    [Fact]
    public void Diagram_UsesGroupImportJourneyAndKeepsBusinessEmailOutOfPublicDraft()
    {
        using var viewModel = CreateViewModel(new RecordingClient([], []));
        var diagram = viewModel.Diagram;
        var linkedStep = diagram.Steps.First();
        diagram.SelectedOrganizationNodeKey = linkedStep.Key;
        diagram.NewOrganizationName = "Example Export Co.";
        diagram.NewOrganizationRoleLabel = "해외 공급자";
        diagram.NewOrganizationCountryCode = "CN";
        diagram.NewOrganizationWebsiteUrl = "https://example.com";
        diagram.NewOrganizationPublicBusinessEmail = "trade@example.com";
        diagram.NewOrganizationContactSourceUrl = "https://example.com/contact";
        diagram.NewOrganizationContactSourceReviewed = true;

        var added = diagram.AddOrganizationCandidate();
        var snapshot = diagram.CreateImportJourneyUpdate();
        var postDraft = diagram.CreateCommunityDraft(
            CommunityBoardCatalog.PublicBoards.Select(board => board.DisplayName).ToArray(),
            "Hongdal Admin",
            "운영자");

        Assert.True(added);
        Assert.Equal(CommunityLedgerTemplateKeys.GroupImport, diagram.SelectedTemplateKey);
        Assert.Equal(CommunityLedgerTemplateKeys.GroupImport, snapshot.LedgerTemplateKey);
        var organization = Assert.Single(snapshot.OrganizationCandidates);
        Assert.Equal(linkedStep.Key, organization.DiagramNodeKey);
        Assert.True(organization.ContactSourceReviewed);
        Assert.Contains("Example Export Co.", postDraft.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("trade@example.com", postDraft.Body, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("수동 이메일 초안 준비 가능", diagram.OutreachReadinessLabel);
    }

    [Fact]
    public void MutualBenefitReview_UsesDraftPurposeAndAppendsNonBindingAssessment()
    {
        using var viewModel = CreateViewModel(new RecordingClient([], []));
        viewModel.OpenVowDraft("관리자");
        viewModel.OpenMutualBenefitTool();
        foreach (var role in viewModel.MutualBenefit.Roles)
        {
            role.ParticipantReviewed = true;
        }

        var applied = viewModel.ApplyMutualBenefitToDraft("관리자");

        Assert.True(applied);
        Assert.Equal(CommunityAuthoringTool.MutualBenefit, viewModel.ActiveTool);
        Assert.Equal(viewModel.Composer.Draft.Title.Trim(), viewModel.MutualBenefit.SharedPurpose);
        Assert.Equal(
            CommunityMutualBenefitAssessmentStatusCodes.MutualBenefitCandidate,
            viewModel.MutualBenefit.Assessment?.StatusCode);
        Assert.Contains("상호 이익 사전 검토", viewModel.Composer.Draft.Body, StringComparison.Ordinal);
        Assert.Contains("거래 실행을 대신하지 않습니다", viewModel.Composer.Draft.Body, StringComparison.Ordinal);
        Assert.Contains("공동조달 경제성 계획", viewModel.Composer.Draft.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("공개용 순편익 추정", viewModel.Composer.Draft.Body, StringComparison.Ordinal);
    }

    [Fact]
    public void MutualBenefitReview_ImportsOrganizationRolesFromDiagramWithoutOperationalSelection()
    {
        using var viewModel = CreateViewModel(new RecordingClient([], []));
        var diagram = viewModel.Diagram;
        diagram.SelectedOrganizationNodeKey = diagram.Steps.First().Key;
        diagram.NewOrganizationName = "Example Logistics";
        diagram.NewOrganizationRoleLabel = "수입 물류 후보";
        diagram.NewOrganizationCountryCode = "US";

        Assert.True(diagram.AddOrganizationCandidate());

        var imported = viewModel.ImportDiagramRolesToMutualBenefit();

        Assert.Equal(1, imported);
        var role = Assert.Single(
            viewModel.MutualBenefit.Roles,
            item => item.RoleLabel == "수입 물류 후보");
        Assert.Equal("Example Logistics", role.ParticipantLabel);
        Assert.False(role.ParticipantReviewed);
        Assert.Equal(CommunityAuthoringTool.MutualBenefit, viewModel.ActiveTool);
    }

    [Fact]
    public void EvidenceChart_ImportsWinWinAmountsAndPersistsReadableChartBlockInDraft()
    {
        using var viewModel = CreateViewModel(new RecordingClient([], []));
        viewModel.OpenVowDraft("관리자");
        for (var index = 0; index < viewModel.MutualBenefit.Roles.Count; index++)
        {
            var role = viewModel.MutualBenefit.Roles[index];
            role.ExpectedBenefitAmount = 20_000m + index * 5_000m;
            role.ExpectedBurdenAmount = 10_000m + index * 2_000m;
        }

        var imported = viewModel.ImportMutualBenefitToEvidenceChart();
        var applied = viewModel.ApplyEvidenceChartToDraft("관리자");

        Assert.True(imported);
        Assert.True(applied);
        Assert.Equal(CommunityAuthoringTool.EvidenceChart, viewModel.ActiveTool);
        Assert.NotNull(viewModel.EvidenceChart.Statistics);
        Assert.Equal(3, viewModel.EvidenceChart.Preview?.Points.Count);
        var block = Assert.Single(
            CommunityEvidenceChartTextCodec.DecodeAll(viewModel.Composer.Draft.Body));
        Assert.Equal("역할별 순편익 추정", block.Title);
        Assert.Equal("KRW", block.Unit);
        Assert.All(block.Points, point => Assert.True(point.Value > 0m));
        Assert.DoesNotContain(
            CommunityEvidenceChartTextCodec.StartMarker,
            CommunityEvidenceChartTextCodec.StripBlocks(viewModel.Composer.Draft.Body),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task PeriodStatistics_AggregatesSelectedCalendarRangeAndImportsEvidenceChart()
    {
        var source = Source(CommunityInformationSourceKeys.KamisPriceObservations);
        var client = new RecordingClient(
            [source],
            [
                KamisCandidate(new DateOnly(2026, 7, 1), 20_000m),
                KamisCandidate(new DateOnly(2026, 7, 5), 25_000m),
                KamisCandidate(new DateOnly(2026, 7, 12), 30_000m),
                KamisCandidate(new DateOnly(2026, 6, 30), 99_000m)
            ]);
        using var viewModel = CreateViewModel(client);
        viewModel.PeriodStatistics.SetAvailableSources([source]);
        viewModel.PeriodStatistics.SetDateRange(
            new DateTime(2026, 7, 1),
            new DateTime(2026, 7, 12));
        viewModel.PeriodStatistics.SourceKey = CommunityInformationSourceKeys.KamisPriceObservations;
        viewModel.PeriodStatistics.MetricCode = CommunityPeriodStatisticsMetricCodes.NumericAverage;

        var generated = await viewModel.PeriodStatistics.GenerateAsync();
        var imported = viewModel.ImportPeriodStatisticsToEvidenceChart();

        Assert.True(generated);
        Assert.True(imported);
        Assert.Equal(new DateOnly(2026, 7, 1), client.LastQuery?.StartDate);
        Assert.Equal(new DateOnly(2026, 7, 12), client.LastQuery?.EndDate);
        Assert.Equal(3, viewModel.PeriodStatistics.RecordCount);
        Assert.Equal(3, viewModel.PeriodStatistics.NumericValueCount);
        Assert.Equal(25_000m, viewModel.PeriodStatistics.Statistics?.Average);
        Assert.Equal(CommunityAuthoringTool.EvidenceChart, viewModel.ActiveTool);
        Assert.Equal("KRW/10개", viewModel.EvidenceChart.Unit);
        Assert.Equal("2026-07-01 ~ 2026-07-12", viewModel.EvidenceChart.ReferenceDate);
        Assert.Equal(3, viewModel.EvidenceChart.Preview?.Points.Count);
    }

    [Fact]
    public async Task PeriodStatistics_DoesNotAverageDifferentPriceSeries()
    {
        var source = Source(CommunityInformationSourceKeys.KamisPriceObservations);
        var client = new RecordingClient(
            [source],
            [
                KamisCandidate(new DateOnly(2026, 7, 1), 20_000m, "retail|apple|fuji|premium|10ea"),
                KamisCandidate(new DateOnly(2026, 7, 2), 22_000m, "retail|apple|fuji|premium|10ea"),
                KamisCandidate(new DateOnly(2026, 7, 1), 12_000m, "wholesale|apple|fuji|premium|10ea"),
                KamisCandidate(new DateOnly(2026, 7, 2), 14_000m, "wholesale|apple|fuji|premium|10ea")
            ]);
        using var viewModel = CreateViewModel(client);
        viewModel.PeriodStatistics.SetAvailableSources([source]);
        viewModel.PeriodStatistics.SetDateRange(
            new DateTime(2026, 7, 1),
            new DateTime(2026, 7, 2));
        viewModel.PeriodStatistics.SourceKey = CommunityInformationSourceKeys.KamisPriceObservations;
        viewModel.PeriodStatistics.MetricCode = CommunityPeriodStatisticsMetricCodes.NumericAverage;

        var generated = await viewModel.PeriodStatistics.GenerateAsync();

        Assert.False(generated);
        Assert.Null(viewModel.PeriodStatistics.Preview);
        Assert.Equal(2, viewModel.PeriodStatistics.AvailableSeries.Count);
        Assert.Contains("수치 계열", viewModel.PeriodStatistics.StatusMessage, StringComparison.Ordinal);

        viewModel.PeriodStatistics.MetricSeriesSelectionKey =
            viewModel.PeriodStatistics.AvailableSeries[0].SelectionKey;
        var selectedSeriesGenerated = await viewModel.PeriodStatistics.GenerateAsync();

        Assert.True(selectedSeriesGenerated);
        Assert.Equal(2, viewModel.PeriodStatistics.RecordCount);
        Assert.Equal(2, viewModel.PeriodStatistics.NumericValueCount);
        Assert.True(viewModel.PeriodStatistics.Statistics?.Average is 13_000m or 21_000m);
        Assert.Equal(2, viewModel.PeriodStatistics.Preview?.Points.Count);
    }

    [Fact]
    public async Task PeriodStatistics_FishCooperativeMonthlyRangeBuildsEmployeeCountGraph()
    {
        var source = Source(CommunityInformationSourceKeys.FishCooperativeGeneralStatistics);
        var client = new RecordingClient(
            [source],
            [
                FishCooperativeCandidate(new DateOnly(2026, 5, 1), 105m),
                FishCooperativeCandidate(new DateOnly(2026, 6, 1), 106m),
                FishCooperativeCandidate(new DateOnly(2026, 7, 1), 107m)
            ]);
        using var viewModel = CreateViewModel(client);
        viewModel.PeriodStatistics.SetAvailableSources([source]);
        viewModel.PeriodStatistics.SetDateRange(
            new DateTime(2026, 5, 15),
            new DateTime(2026, 7, 10));
        viewModel.PeriodStatistics.SelectSource(
            CommunityInformationSourceKeys.FishCooperativeGeneralStatistics);

        var generated = await viewModel.PeriodStatistics.GenerateAsync();
        var imported = viewModel.ImportPeriodStatisticsToEvidenceChart();

        Assert.True(generated);
        Assert.True(imported);
        Assert.Equal("KR", viewModel.PeriodStatistics.CountryCode);
        Assert.Equal(
            CommunityPeriodStatisticsMetricCodes.NumericAverage,
            viewModel.PeriodStatistics.MetricCode);
        Assert.Equal(3, viewModel.PeriodStatistics.Buckets.Count);
        Assert.Equal(106m, viewModel.PeriodStatistics.Statistics?.Average);
        Assert.Equal("명", viewModel.EvidenceChart.Unit);
        Assert.Equal("기간별 총임직원 평균", viewModel.EvidenceChart.Title);
        Assert.Equal(3, viewModel.EvidenceChart.Preview?.Points.Count);
    }

    [Fact]
    public async Task Diagram_CanAttachDirectoryProviderToSelectedNodeWithoutContactEmail()
    {
        var provider = new ThirdPartyLogisticsProviderDirectoryItem
        {
            MarketCode = OperatingMarketCodes.UnitedStates,
            ProviderKey = "example-3pl",
            DisplayName = "Example 3PL",
            OfficialWebsiteUrl = "https://example.com/",
            DirectoryStatusCode = ThirdPartyLogisticsProviderDirectoryStatusCodes.ResearchCandidate,
            PlatformRelationshipStatusCode = ThirdPartyLogisticsProviderRelationshipStatusCodes.NoPlatformRelationship,
            CompanySourceVerificationStatusCode = ThirdPartyLogisticsProviderVerificationStatusCodes.OfficialCompanySourceReviewed,
            RegulatoryVerificationStatusCode = ThirdPartyLogisticsProviderVerificationStatusCodes.RegulatoryStatusNotVerified,
            CapabilityCodes = [ThirdPartyLogisticsProviderCapabilityCodes.WarehousingAndDistribution],
            Evidence =
            [
                new ThirdPartyLogisticsProviderEvidence
                {
                    EvidenceTypeCode = ThirdPartyLogisticsProviderEvidenceTypeCodes.OfficialProviderServicePage,
                    SourceTitle = "Example services",
                    SourceUrl = "https://example.com/services",
                    ReviewedOn = new DateOnly(2026, 7, 19)
                }
            ]
        };
        using var diagram = new CommunityAuthoringDiagramViewModel(
            new PlatformCommunityDiagramWorkspaceViewModel(),
            new RecordingOrganizationDirectoryClient(provider));
        var node = diagram.Steps.First();
        diagram.SelectedOrganizationNodeKey = node.Key;

        await diagram.SearchOrganizationDirectoryAsync();
        var attached = diagram.AttachOrganizationDirectoryItem(
            Assert.Single(diagram.OrganizationDirectoryItems));
        var savedJourney = diagram.CreateImportJourneyUpdate();
        var snapshot = diagram.CreateDiagramSnapshot("diagram-1");

        Assert.True(attached);
        var candidate = Assert.Single(savedJourney.OrganizationCandidates);
        Assert.Equal("example-3pl", candidate.SourceReferenceKey);
        Assert.Empty(candidate.PublicBusinessEmail);
        Assert.False(candidate.CanBeSelectedForOperations);
        var organization = Assert.Single(
            snapshot.Nodes.Single(item => item.NodeId == node.Key).OrganizationReferences);
        Assert.Equal("Example 3PL", organization.DisplayName);
        Assert.Equal(
            DiagramOrganizationSourceKindCodes.ThirdPartyLogisticsDirectory,
            organization.SourceKindCode);
        Assert.Equal(
            ThirdPartyLogisticsProviderVerificationStatusCodes.RegulatoryStatusNotVerified,
            organization.RegulatoryVerificationStatusCode);
    }

    [Fact]
    public async Task SavedYouTubeWorkspace_RestoresAndResavesImportJourneyWithOrganizations()
    {
        var response = SocialResearchResponse();
        var journey = ImportJourney();
        var workspace = SocialWorkspace(response) with { ImportJourney = journey };
        var client = new RecordingClient([], [], response.Sources, response, workspace);
        using var viewModel = CreateViewModel(client);
        await viewModel.SocialResearch.InitializeAsync();
        viewModel.SocialResearch.VideoReference = response.Video.OriginalUrl;

        Assert.True(await viewModel.SocialResearch.LoadSavedWorkspaceAsync());
        Assert.True(viewModel.LoadSocialWorkspaceJourney());
        Assert.Equal(journey.Nodes.Count, viewModel.Diagram.Steps.Count);
        Assert.Single(viewModel.Diagram.OrganizationCandidates);

        viewModel.OpenVowDraft("관리자");
        Assert.True(await viewModel.SaveSocialWorkspaceDraftAsync());
        Assert.NotNull(client.LastWorkspaceDraftRequest?.ImportJourney);
        Assert.Equal(
            CommunityLedgerTemplateKeys.GroupImport,
            client.LastWorkspaceDraftRequest!.ImportJourney!.LedgerTemplateKey);
        Assert.Single(client.LastWorkspaceDraftRequest.ImportJourney.OrganizationCandidates);
    }

    [Fact]
    public void VowDraft_UsesTransparentOperatorPersonaAndNonBindingBoundary()
    {
        using var viewModel = CreateViewModel(new RecordingClient([], []));

        viewModel.OpenVowDraft("실명 관리자");
        var firstNickname = viewModel.Composer.Draft.Nickname;
        viewModel.SelectNextWritingPersona();

        Assert.All(
            viewModel.WritingPersona.Personas,
            persona => Assert.EndsWith("· 운영자", persona.Nickname, StringComparison.Ordinal));
        Assert.NotEqual(firstNickname, viewModel.Composer.Draft.Nickname);
        Assert.Equal(CommunityBoardCatalog.Vow.DisplayName, viewModel.Composer.Draft.Category);
        Assert.StartsWith("[서원]", viewModel.Composer.Draft.Title, StringComparison.Ordinal);
        Assert.Contains("함께할 사람과 업체", viewModel.Composer.Draft.Body, StringComparison.Ordinal);
        Assert.Contains("다이어그램과 원장 연결", viewModel.Composer.Draft.Body, StringComparison.Ordinal);
        Assert.Contains("주문·계약·결제·배차·보관·정산", viewModel.Composer.Draft.Body, StringComparison.Ordinal);
        Assert.Equal(CommunityAuthoringTool.VowJourneyTemplate, viewModel.ActiveTool);
    }

    [Fact]
    public void VowDraft_UsesSelectedRoadmapVersionWithoutEnablingItsOperations()
    {
        using var viewModel = CreateViewModel(new RecordingClient([], []));
        viewModel.SelectVowVersion("2.0");

        viewModel.OpenVowDraft("관리자");

        Assert.Equal("홍달 2.0 · 국제 물류·통관", viewModel.Composer.Draft.WorkflowTag);
        Assert.StartsWith("[서원][홍달 2.0]", viewModel.Composer.Draft.Title, StringComparison.Ordinal);
        Assert.Contains("HS 코드", viewModel.Composer.Draft.Body, StringComparison.Ordinal);
        Assert.Contains("자격 있는 관세사", viewModel.Composer.Draft.Body, StringComparison.Ordinal);
        Assert.Contains("실행 기능의 활성화", viewModel.Composer.Draft.Body, StringComparison.Ordinal);
    }

    [Fact]
    public void VowVersionCatalog_CoversCurrentRoadmapAndOpenEndedFuture()
    {
        Assert.Equal(
            ["0.0", "1.0", "1.5", "2.0", "2.5", "3.0", "3.5", "future"],
            CommunityVowVersionCatalog.All.Select(version => version.Code));
        Assert.True(CommunityVowVersionCatalog.Current.IsCurrentFocus);
        Assert.True(CommunityVowVersionCatalog.Find("future").IsFutureExploration);
        Assert.Equal(
            CommunityVowVersionCatalog.All.Count,
            CommunityVowVersionCatalog.All.Select(version => version.WorkflowTag).Distinct().Count());
    }

    [Fact]
    public void VowJourneyTemplate_SourceDraftSeparatesFactsFromInterpretation()
    {
        using var viewModel = CreateViewModel(new RecordingClient([], []));
        viewModel.SelectVowVersion("2.0");
        viewModel.SelectVowJourneyTemplate(CommunityVowJourneyTemplateCatalog.SourceKey);

        viewModel.OpenVowDraft("관리자");

        Assert.StartsWith("[서원][홍달 2.0] 이 자료에서 시작한 여정", viewModel.Composer.Draft.Title, StringComparison.Ordinal);
        Assert.Contains("자료에서 확인한 사실", viewModel.Composer.Draft.Body, StringComparison.Ordinal);
        Assert.Contains("사실과 구분한 나의 해석", viewModel.Composer.Draft.Body, StringComparison.Ordinal);
        Assert.Contains("가격·통계 근거", viewModel.Composer.Draft.Body, StringComparison.Ordinal);
        Assert.Contains("가원장에 남길 참여 의사와 질문", viewModel.Composer.Draft.Body, StringComparison.Ordinal);
        Assert.Equal("홍달 2.0 · 국제 물류·통관", viewModel.Composer.Draft.WorkflowTag);
        Assert.Equal(CommunityBoardCatalog.Vow.DisplayName, viewModel.Composer.Draft.Category);
    }

    [Fact]
    public void VowJourneyTemplate_AppendsWithoutReplacingExistingDraft()
    {
        using var viewModel = CreateViewModel(new RecordingClient([], []));
        viewModel.OpenBlankDraft("관리자");
        viewModel.Composer.Draft.Title = "이미 작성 중인 제목";
        viewModel.Composer.Draft.Body = "이미 정리한 내용";
        viewModel.SelectVowJourneyTemplate(CommunityVowJourneyTemplateCatalog.PartnersKey);

        var applied = viewModel.ApplyVowJourneyTemplate("관리자");

        Assert.True(applied);
        Assert.Equal("이미 작성 중인 제목", viewModel.Composer.Draft.Title);
        Assert.StartsWith("이미 정리한 내용", viewModel.Composer.Draft.Body, StringComparison.Ordinal);
        Assert.Contains("서원 여정 틀 · 함께할 사람 찾기", viewModel.Composer.Draft.Body, StringComparison.Ordinal);
        Assert.Contains("아직 비어 있는 역할", viewModel.Composer.Draft.Body, StringComparison.Ordinal);
        Assert.Contains("플랫폼이 대신 결정하거나 중개하지 않을 범위", viewModel.Composer.Draft.Body, StringComparison.Ordinal);
    }

    [Fact]
    public void VowJourneyTemplateCatalog_ProvidesDistinctWritingStartingPoints()
    {
        Assert.Equal(
            [
                CommunityVowJourneyTemplateCatalog.JourneyKey,
                CommunityVowJourneyTemplateCatalog.SourceKey,
                CommunityVowJourneyTemplateCatalog.PartnersKey
            ],
            CommunityVowJourneyTemplateCatalog.All.Select(template => template.Key));
        Assert.All(
            CommunityVowJourneyTemplateCatalog.All,
            template => Assert.Contains("운영 경계", template.SectionNames));
    }

    [Fact]
    public void VowJourneyTemplate_AllVersionCombinationsFitComposerLimitsAndKeepBoundary()
    {
        var viewModel = new CommunityVowJourneyTemplateViewModel();

        foreach (var template in CommunityVowJourneyTemplateCatalog.All)
        {
            viewModel.SelectedKey = template.Key;
            foreach (var version in CommunityVowVersionCatalog.All)
            {
                var draft = viewModel.BuildDraft(version);

                Assert.InRange(draft.Title.Length, 1, 160);
                Assert.InRange(draft.Body.Length, 1, 4000);
                Assert.Contains(version.Focus, draft.Body, StringComparison.Ordinal);
                Assert.Contains(version.OperationalBoundary, draft.Body, StringComparison.Ordinal);
                Assert.Contains("비구속적 서원", draft.Body, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public async Task SocialResearch_UsesPastedYouTubeUrlAndAppliesReturnedDraft()
    {
        var response = SocialResearchResponse();
        var client = new RecordingClient(
            [],
            [],
            [new("reddit-public-posts", "Reddit", "Reddit 공개 글", "https://example.com", true, true, false)],
            response);
        using var viewModel = CreateViewModel(client);
        await viewModel.SocialResearch.InitializeAsync();
        viewModel.SocialResearch.VideoReference = "https://www.youtube.com/watch?v=video-1";
        viewModel.SocialResearch.SearchTermsText = "공동구매, 지역 식재료";

        var researched = await viewModel.SocialResearch.ResearchAsync();
        var applied = viewModel.ApplySocialResearchToDraft("관리자");

        Assert.True(researched);
        Assert.True(applied);
        Assert.Equal("video-1", client.LastResearchRequest?.VideoId);
        Assert.Equal("공동구매", viewModel.Composer.Draft.WorkflowTag);
        Assert.Equal(CommunityBoardCatalog.Vow.DisplayName, viewModel.Composer.Draft.Category);
        Assert.Equal(response.Video.OriginalUrl, viewModel.Composer.Draft.SharedLinkUrl);
        Assert.Contains("SNS에서 함께 본 내용", viewModel.Composer.Draft.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SocialResearch_LoadsSavedYouTubeRootAndNestedSocialSources()
    {
        var response = SocialResearchResponse();
        var workspace = SocialWorkspace(response);
        var client = new RecordingClient(
            [],
            [],
            response.Sources,
            response,
            workspace);
        using var viewModel = CreateViewModel(client);
        await viewModel.SocialResearch.InitializeAsync();
        viewModel.SocialResearch.VideoReference = response.Video.OriginalUrl;

        var loaded = await viewModel.SocialResearch.LoadSavedWorkspaceAsync();

        Assert.True(loaded);
        Assert.True(viewModel.SocialResearch.HasWorkspace);
        Assert.Equal(workspace.WorkspaceId, viewModel.SocialResearch.WorkspaceId);
        Assert.Equal(workspace.Revision, viewModel.SocialResearch.WorkspaceRevision);
        Assert.Single(viewModel.SocialResearch.Result!.Items);
        Assert.Equal(
            "https://www.reddit.com/r/localfood/",
            Assert.Single(viewModel.SocialResearch.Sources).StartUrls.Single());
    }

    [Fact]
    public async Task HandleComposerSavedAsync_AttachesSelectedGeneratedImagesInContextOrder()
    {
        var plan = new CommunityAuthoringImagePromptPlanResponse(
            "지역 식재료 공동구매",
            2,
            [
                new CommunityAuthoringImagePromptSegmentDto(
                    "context-01",
                    1,
                    "수요 확인",
                    "이웃이 원하는 품목과 수량을 확인합니다.",
                    "첫 번째 문맥 이미지 프롬프트입니다.",
                    CommunityAuthoringImageAspectRatios.Landscape,
                    true),
                new CommunityAuthoringImagePromptSegmentDto(
                    "context-02",
                    2,
                    "공급 조건",
                    "공급자와 가격 및 물류 조건을 비교합니다.",
                    "두 번째 문맥 이미지 프롬프트입니다.",
                    CommunityAuthoringImageAspectRatios.Square,
                    true)
            ],
            "test-v1",
            "테스트 계획");
        var firstImage = CompletedImage(
            "image-job-41",
            plan.Segments[0].Prompt,
            plan.Segments[0].AspectRatio);
        var secondImage = CompletedImage(
            "image-job-42",
            plan.Segments[1].Prompt,
            plan.Segments[1].AspectRatio);
        var client = new RecordingClient(
            [],
            [],
            authoringImagePromptPlan: plan,
            authoringImageResponses: [firstImage, secondImage]);
        using var viewModel = CreateViewModel(client);

        Assert.True(await viewModel.ImageGenerator.PlanAsync(plan.ArticleTitle, "두 개 문맥 본문"));
        Assert.True(await viewModel.ImageGenerator.GenerateSelectedAsync());
        Assert.All(viewModel.ImageGenerator.Items, item => Assert.True(viewModel.ImageGenerator.TogglePostSelection(item)));

        await viewModel.HandleComposerSavedAsync(
            new CommunityPostComposerSaveResult(
                true,
                false,
                new PlatformCommunityPostResponse { Id = 42 },
                "게시글을 등록했습니다.")
            {
                SubmissionPassword = "draft-password"
            });

        Assert.Equal(2, client.AuthoringImageRequests.Count);
        Assert.Equal(plan.Segments[0].Prompt, client.AuthoringImageRequests[0].Prompt);
        Assert.Equal(plan.Segments[1].Prompt, client.AuthoringImageRequests[1].Prompt);
        Assert.Collection(
            client.ImageAttachments,
            attachment =>
            {
                Assert.Equal("image-job-41", attachment.JobCode);
                Assert.Equal(42, attachment.PostId);
                Assert.Equal("draft-password", attachment.Password);
            },
            attachment => Assert.Equal("image-job-42", attachment.JobCode));
        Assert.False(viewModel.ImageGenerator.HasSelectedImage);
        Assert.Contains("문맥 순서대로 게시글 사진에 첨부했습니다", viewModel.StatusMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ImageGenerator_PreservesEditedPlanUntilExplicitlyCleared()
    {
        var plan = new CommunityAuthoringImagePromptPlanResponse(
            "새 글 제목",
            1,
            [
                new CommunityAuthoringImagePromptSegmentDto(
                    "context-01",
                    1,
                    "도입",
                    "새 글 본문",
                    "자동으로 만든 이미지 프롬프트입니다.",
                    CommunityAuthoringImageAspectRatios.Landscape,
                    true)
            ],
            "test-v1",
            "테스트 계획");
        var client = new RecordingClient([], [], authoringImagePromptPlan: plan);
        var viewModel = new CommunityAuthoringImageGeneratorViewModel(client);

        Assert.True(await viewModel.PlanAsync("새 글 제목", "새 글 본문"));
        viewModel.Items[0].Prompt = "직접 다듬은 이미지 프롬프트입니다.";

        viewModel.PrepareFromDraft("바뀐 글 제목", "바뀐 글 본문");
        Assert.Equal("직접 다듬은 이미지 프롬프트입니다.", viewModel.Items[0].Prompt);
        Assert.Equal(CommunityComposerMessageKind.Warning, viewModel.StatusKind);

        viewModel.PrepareFromDraft("바뀐 글 제목", "바뀐 글 본문", overwrite: true);
        Assert.Empty(viewModel.Items);
    }

    private static CommunityAuthoringImageTaskResponse CompletedImage(
        string jobCode,
        string prompt,
        string aspectRatio)
        => new(
            jobCode,
            CommunityAuthoringImageTaskStatusCodes.Completed,
            "이미지 생성을 완료했습니다.",
            prompt,
            aspectRatio,
            "gpt-image-2-text-to-image",
            $"https://cdn.example.com/{jobCode}.png",
            true,
            true,
            100,
            new DateTime(2026, 7, 19, 1, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 7, 19, 1, 1, 0, DateTimeKind.Utc),
            "AI 생성 이미지");

    private static CommunityInformationReviewPageViewModel CreateViewModel(
        ICommunityInformationReviewClient client)
    {
        var communityService = new PlatformCommunityService(new HttpClient(), null!);
        var composer = new CommunityPostComposerViewModel(
            communityService,
            new InMemoryDraftStore());
        var socialResearch = new CommunityAuthoringSocialResearchViewModel(client);
        var diagram = new CommunityAuthoringDiagramViewModel(new PlatformCommunityDiagramWorkspaceViewModel());
        return new CommunityInformationReviewPageViewModel(
            client,
            composer,
            new CommunityScheduledPostListViewModel(communityService),
            new PlatformCommunityLedgerPickerViewModel(communityService),
            socialResearch,
            diagram,
            new CommunityAuthoringMutualBenefitViewModel(),
            new CommunityAuthoringEvidenceChartViewModel(),
            new CommunityAuthoringPeriodStatisticsViewModel(client),
            new CommunityAuthoringAiDraftViewModel(client),
            new CommunityAuthoringImageGeneratorViewModel(client),
            new CommunityOperatorWritingPersonaViewModel(),
            new CommunityVowVersionViewModel(),
            new CommunityVowJourneyTemplateViewModel());
    }

    private static CommunityInformationSourceDto Source(string sourceKey)
        => new(
            sourceKey,
            CommunityInformationSourceTypes.PublicData,
            "provider",
            "source",
            CommunityInformationCollectionModes.ScheduledArchive,
            "daily",
            "review",
            "https://example.com/docs",
            true);

    private static CommunityInformationCandidateDto KamisCandidate(
        DateOnly? referenceDate = null,
        decimal numericValue = 25_000m,
        string metricSeriesKey = "retail|apple|fuji|premium|10ea")
        => new(
            $"kamis:apple:{referenceDate?.ToString("yyyyMMdd") ?? "default"}",
            CommunityInformationSourceKeys.KamisPriceObservations,
            CommunityInformationSourceTypes.PublicData,
            "KAMIS 농산물 유통정보",
            "사과 (후지 · 상품)",
            $"소매 · 과일류 · {numericValue:N0}원/10개",
            "https://www.kamis.or.kr/service/price/xml.do",
            null,
            null,
            referenceDate ?? new DateOnly(2026, 7, 17),
            new DateTime(2026, 7, 18, 1, 0, 0, DateTimeKind.Utc),
            "KR",
            "ko",
            "KRW",
            "10개",
            CommunityInformationReviewStates.OfficialObservation,
            ["농수산물", "과일류"],
            "KAMIS Open API에서 수집한 관측값입니다.",
            "전체 시장 평균이나 판매 권고가 아닙니다.",
            numericValue,
            "가격",
            metricSeriesKey);

    private static CommunityInformationCandidateDto FishCooperativeCandidate(
        DateOnly referenceMonth,
        decimal employeeCount)
        => new(
            $"fish-coop:{referenceMonth:yyyyMM}:001:TOTAL",
            CommunityInformationSourceKeys.FishCooperativeGeneralStatistics,
            CommunityInformationSourceTypes.PublicData,
            "금융위원회 금융통계",
            "통영수산업협동조합 · 총임직원",
            $"{referenceMonth:yyyy년 M월} · {employeeCount:N0}명",
            "https://www.data.go.kr/data/15061340/openapi.do",
            null,
            null,
            referenceMonth,
            new DateTime(2026, 7, 19, 1, 0, 0, DateTimeKind.Utc),
            "KR",
            "ko",
            null,
            "명",
            CommunityInformationReviewStates.OfficialObservation,
            ["수산업협동조합", "수협", "총임직원"],
            "금융위원회 금융통계 수산업협동조합 일반현황 관측값입니다.",
            "같은 조합과 같은 임직원 구분만 시계열로 비교해야 합니다.",
            employeeCount,
            "총임직원",
            "fish-coop|001|TOTAL|employee-count",
            new DateOnly(
                referenceMonth.Year,
                referenceMonth.Month,
                DateTime.DaysInMonth(referenceMonth.Year, referenceMonth.Month)));

    private static CommunityInformationCandidateDto VideoCandidate()
        => new(
            "youtube:food",
            CommunityInformationSourceKeys.YouTubeChannelVideos,
            CommunityInformationSourceTypes.Video,
            "음식 채널",
            "새로운 사과 요리",
            "사과를 활용한 공개 영상입니다.",
            "https://www.youtube.com/watch?v=food",
            "https://i.ytimg.com/vi/food/hqdefault.jpg",
            new DateTime(2026, 7, 18, 1, 0, 0, DateTimeKind.Utc),
            new DateOnly(2026, 7, 18),
            new DateTime(2026, 7, 18, 2, 0, 0, DateTimeKind.Utc),
            "KR",
            "ko",
            null,
            null,
            CommunityInformationReviewStates.PendingReview,
            ["음식", "CookingIngredient"],
            "YouTube 공개 메타데이터입니다.",
            "제목과 설명은 영상 제작자가 작성한 정보입니다.");

    private static YouTubeSocialContextResearchResponse SocialResearchResponse()
    {
        var video = new YouTubeSocialContextVideoDto(
            "video-1",
            "Food channel",
            "지역 식재료 영상",
            "영상 요약",
            "https://www.youtube.com/watch?v=video-1",
            null,
            new DateTime(2026, 7, 18, 1, 0, 0, DateTimeKind.Utc),
            "US",
            "en");
        var source = new SocialMediaResearchSourceDto(
            "reddit-public-posts",
            "Reddit",
            "Reddit 공개 글",
            "https://developers.reddit.com/",
            true,
            true,
            false);
        var item = new CommunityInformationCandidateDto(
            "reddit:local-food",
            source.SourceKey,
            CommunityInformationSourceTypes.SocialMedia,
            source.Provider,
            "Local food discussion",
            "Public discussion summary",
            "https://www.reddit.com/r/localfood/comments/1",
            null,
            new DateTime(2026, 7, 18, 1, 10, 0, DateTimeKind.Utc),
            null,
            new DateTime(2026, 7, 18, 1, 20, 0, DateTimeKind.Utc),
            "US",
            "en",
            null,
            null,
            CommunityInformationReviewStates.PendingReview,
            ["지역 식재료"],
            "Reddit public post",
            "Editorial review required");
        return new YouTubeSocialContextResearchResponse(
            new DateTime(2026, 7, 18, 2, 0, 0, DateTimeKind.Utc),
            video,
            ["공동구매"],
            ["지역 식재료"],
            [source],
            [item],
            [],
            new YouTubeSocialContextPostDraftDto(
                "[함께 보기] 지역 식재료 영상",
                "SNS에서 함께 본 내용",
                new YouTubeSocialContextCollectiveActionDraftDto(
                    "공동구매",
                    CommunityCollectiveIntentTypeCodes.GroupPurchase,
                    [CommunityCollectiveIntentTypeCodes.GroupPurchase],
                    "함께하기",
                    "비구속적 관심 표시",
                    "/api/v1/community/posts/{postId}/opportunities")));
    }

    private static YouTubeSocialContextWorkspaceDto SocialWorkspace(
        YouTubeSocialContextResearchResponse response)
    {
        var now = response.GeneratedAtUtc;
        return new YouTubeSocialContextWorkspaceDto(
            "youtube-video-1",
            3,
            YouTubeSocialContextWorkspaceStatusCodes.DraftEdited,
            response.Video,
            response.SearchTerms,
            response.AdjacentTopics,
            [new SocialMediaResearchTargetDto("reddit-public-posts", ["https://www.reddit.com/r/localfood/"])],
            8,
            [new YouTubeSocialContextSourceGroupDto(response.Sources[0], response.Items)],
            response.Failures,
            new YouTubeSocialContextWorkspaceDraftDto(
                "홍달 운영자",
                CommunityBoardCatalog.InformationPrices.DisplayName,
                response.Draft.CollectiveAction.WorkflowTag,
                "운영자 정보 공유",
                response.Draft.Title,
                response.Draft.Body,
                response.Video.OriginalUrl,
                response.Draft.CollectiveAction,
                true,
                now),
            null,
            [],
            now,
            now,
            now,
            "홍달 운영자");
    }

    private static YouTubeImportJourneyDraftDto ImportJourney()
        => new(
            CommunityLedgerTemplateKeys.GroupImport,
            [
                new YouTubeImportJourneyNodeDto(
                    "supplier-order",
                    "해외 공급자 발주",
                    "공급자와 발주 조건을 확인합니다.",
                    "해외 발주",
                    "work"),
                new YouTubeImportJourneyNodeDto(
                    "customs",
                    "수입 통관",
                    "통관 조건을 확인합니다.",
                    "통관",
                    "work")
            ],
            [new YouTubeImportJourneyEdgeDto("supplier-order", "customs", "선적 뒤 통관")],
            [
                new YouTubeImportOrganizationCandidateDto(
                    "organization-example",
                    "supplier-order",
                    "Example Export Co.",
                    "해외 공급자",
                    "CN",
                    "https://example.com/",
                    "trade@example.com",
                    "https://example.com/contact",
                    true)
            ],
            YouTubeImportOutreachReadinessCodes.ReadyForManualDraft,
            new DateTime(2026, 7, 18, 3, 0, 0, DateTimeKind.Utc));

    private sealed class RecordingClient(
        IReadOnlyList<CommunityInformationSourceDto> sources,
        IReadOnlyList<CommunityInformationCandidateDto> candidates,
        IReadOnlyList<SocialMediaResearchSourceDto>? socialSources = null,
        YouTubeSocialContextResearchResponse? socialResearchResponse = null,
        YouTubeSocialContextWorkspaceDto? socialWorkspace = null,
        CommunityAuthoringAiDraftResponse? aiDraftResponse = null,
        CommunityAuthoringImageTaskResponse? authoringImageResponse = null,
        CommunityAuthoringImagePromptPlanResponse? authoringImagePromptPlan = null,
        IReadOnlyList<CommunityAuthoringImageTaskResponse>? authoringImageResponses = null) : ICommunityInformationReviewClient
    {
        private readonly Queue<CommunityAuthoringImageTaskResponse> _authoringImageResponses = new(
            authoringImageResponses
            ?? (authoringImageResponse is null ? [] : [authoringImageResponse]));

        public CommunityInformationCollectionQuery? LastQuery { get; private set; }
        public CommunityAuthoringAiDraftRequest? LastAiDraftRequest { get; private set; }
        public YouTubeSocialContextResearchRequest? LastResearchRequest { get; private set; }
        public YouTubeSocialContextWorkspaceDraftUpdateRequest? LastWorkspaceDraftRequest { get; private set; }
        public CommunityAuthoringImagePromptPlanRequest? LastImagePromptPlanRequest { get; private set; }
        public List<CommunityAuthoringImageGenerateRequest> AuthoringImageRequests { get; } = [];
        public List<(string JobCode, long PostId, string Password)> ImageAttachments { get; } = [];

        public Task<IReadOnlyList<CommunityInformationSourceDto>> GetSourcesAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult(sources);

        public Task<CommunityInformationCollectionResponse> GetCandidatesAsync(
            CommunityInformationCollectionQuery query,
            CancellationToken cancellationToken = default)
        {
            LastQuery = query;
            return Task.FromResult(new CommunityInformationCollectionResponse(
                DateTime.UtcNow,
                sources,
                candidates,
                []));
        }

        public Task<CommunityAuthoringAiDraftResponse> GenerateAiDraftAsync(
            CommunityAuthoringAiDraftRequest request,
            CancellationToken cancellationToken = default)
        {
            LastAiDraftRequest = request;
            return Task.FromResult(
                aiDraftResponse
                ?? new CommunityAuthoringAiDraftResponse(
                    false,
                    CommunityAuthoringAiDraftStatusCodes.LlmBlocked,
                    "테스트에서 LLM을 호출하지 않습니다.",
                    null,
                    [],
                    [],
                    true,
                    false,
                    null,
                    0m,
                    0m,
                    0m));
        }

        public Task<CommunityAuthoringImageTaskResponse> GenerateAuthoringImageAsync(
            CommunityAuthoringImageGenerateRequest request,
            CancellationToken cancellationToken = default)
        {
            AuthoringImageRequests.Add(request);
            return Task.FromResult(_authoringImageResponses.Count > 0
                ? _authoringImageResponses.Dequeue()
                : new CommunityAuthoringImageTaskResponse(
                "test-image-job",
                CommunityAuthoringImageTaskStatusCodes.Queued,
                "대기 중",
                request.Prompt,
                request.AspectRatio,
                "gpt-image-2-text-to-image",
                null,
                false,
                false,
                null,
                DateTime.UtcNow,
                null,
                "AI 생성 이미지"));
        }

        public Task<CommunityAuthoringImagePromptPlanResponse> PlanAuthoringImagePromptsAsync(
            CommunityAuthoringImagePromptPlanRequest request,
            CancellationToken cancellationToken = default)
        {
            LastImagePromptPlanRequest = request;
            return Task.FromResult(
                authoringImagePromptPlan
                ?? new CommunityAuthoringImagePromptPlanResponse(
                    string.IsNullOrWhiteSpace(request.Title) ? "테스트 글" : request.Title,
                    1,
                    [
                        new CommunityAuthoringImagePromptSegmentDto(
                            "context-01",
                            1,
                            "도입",
                            request.Body,
                            $"{request.Title} {request.Body}".Trim(),
                            request.AspectRatio,
                            true)
                    ],
                    "test-v1",
                    "테스트 문맥 계획"));
        }

        public Task<CommunityAuthoringImageTaskResponse?> GetAuthoringImageAsync(
            string jobCode,
            bool refreshProvider = true,
            CancellationToken cancellationToken = default)
            => Task.FromResult(authoringImageResponse);

        public Task<PlatformCommunityPostAttachmentResponse> AttachAuthoringImageAsync(
            string jobCode,
            long postId,
            string password,
            CancellationToken cancellationToken = default)
        {
            ImageAttachments.Add((jobCode, postId, password));
            return Task.FromResult(new PlatformCommunityPostAttachmentResponse());
        }

        public Task<IReadOnlyList<SocialMediaResearchSourceDto>> GetSocialMediaSourcesAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<SocialMediaResearchSourceDto>>(socialSources ?? []);

        public Task<YouTubeSocialContextResearchResponse> ResearchYouTubeSocialContextAsync(
            YouTubeSocialContextResearchRequest request,
            CancellationToken cancellationToken = default)
        {
            LastResearchRequest = request;
            return Task.FromResult(
                socialResearchResponse
                ?? throw new InvalidOperationException("SNS 조사 응답이 준비되지 않았습니다."));
        }

        public Task<YouTubeSocialContextWorkspaceDto?> GetYouTubeSocialContextWorkspaceByVideoAsync(
            string videoId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(socialWorkspace);

        public Task<IReadOnlyList<YouTubeSocialContextWorkspaceSummaryDto>> GetYouTubeSocialContextWorkspacesAsync(
            int take = 50,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<YouTubeSocialContextWorkspaceSummaryDto>>([]);

        public Task<YouTubeSocialContextWorkspaceDto> SaveYouTubeSocialContextWorkspaceDraftAsync(
            string workspaceId,
            YouTubeSocialContextWorkspaceDraftUpdateRequest request,
            CancellationToken cancellationToken = default)
        {
            LastWorkspaceDraftRequest = request;
            return Task.FromResult(
                socialWorkspace
                ?? throw new InvalidOperationException("SNS 작업공간이 준비되지 않았습니다."));
        }

        public Task<YouTubeSocialContextWorkspaceDto> LinkYouTubeSocialContextPublicationAsync(
            string workspaceId,
            YouTubeSocialContextPublicationLinkRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(
                socialWorkspace
                ?? throw new InvalidOperationException("SNS 작업공간이 준비되지 않았습니다."));
    }

    private sealed class InMemoryDraftStore : ICommunityPostComposerDraftStore
    {
        public Task<CommunityPostComposerSnapshot?> LoadAsync(
            string appKey,
            CancellationToken cancellationToken = default)
            => Task.FromResult<CommunityPostComposerSnapshot?>(null);

        public Task SaveAsync(
            string appKey,
            CommunityPostComposerSnapshot snapshot,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task ClearAsync(
            string appKey,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class RecordingOrganizationDirectoryClient(
        params ThirdPartyLogisticsProviderDirectoryItem[] providers)
        : IDiagramOrganizationDirectoryClient
    {
        public Task<ThirdPartyLogisticsProviderDirectoryResponse> SearchThirdPartyLogisticsAsync(
            string? searchText,
            int pageSize = 12,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new ThirdPartyLogisticsProviderDirectoryResponse
            {
                Success = true,
                MarketCode = OperatingMarketCodes.UnitedStates,
                Page = 1,
                PageSize = pageSize,
                TotalCount = providers.Length,
                Items = providers
            });
    }
}
