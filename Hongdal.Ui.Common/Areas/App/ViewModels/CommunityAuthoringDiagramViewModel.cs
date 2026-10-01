using System.Collections.ObjectModel;
using System.Net.Mail;
using CommunityToolkit.Mvvm.ComponentModel;
using Hongdal.Contracts.Common.Community;
using Hongdal.Contracts.Common.Content;
using Hongdal.Contracts.Common.Operations;
using Hongdal.Ui.Common.Areas.App.Services;

namespace Hongdal.Ui.Common.Areas.App.ViewModels;

public sealed class CommunityAuthoringDiagramViewModel : 조립ViewModelBase
{
    private readonly IDiagramOrganizationDirectoryClient _organizationDirectoryClient;
    private string _selectedTemplateKey = CommunityLedgerTemplateKeys.GroupImport;
    private string _newStepTitle = string.Empty;
    private string _newStepDescription = string.Empty;
    private string _selectedOrganizationNodeKey = string.Empty;
    private string _newOrganizationName = string.Empty;
    private string _newOrganizationRoleLabel = string.Empty;
    private string _newOrganizationCountryCode = "ZZ";
    private string _newOrganizationWebsiteUrl = string.Empty;
    private string _newOrganizationPublicBusinessEmail = string.Empty;
    private string _newOrganizationContactSourceUrl = string.Empty;
    private bool _newOrganizationContactSourceReviewed;
    private string _organizationDirectorySearchText = string.Empty;
    private IReadOnlyList<ThirdPartyLogisticsProviderDirectoryItem> _organizationDirectoryItems = [];
    private bool _isSearchingOrganizationDirectory;
    private string? _organizationDirectoryErrorMessage;
    private string? _statusMessage;

    public CommunityAuthoringDiagramViewModel(
        PlatformCommunityDiagramWorkspaceViewModel workspace)
        : this(workspace, NoopDiagramOrganizationDirectoryClient.Instance)
    {
    }

    public CommunityAuthoringDiagramViewModel(
        PlatformCommunityDiagramWorkspaceViewModel workspace,
        IDiagramOrganizationDirectoryClient organizationDirectoryClient)
    {
        _organizationDirectoryClient = organizationDirectoryClient;
        Workspace = 하위ViewModel등록(workspace, 수명소유: true);
        LoadSelectedTemplate();
    }

    public PlatformCommunityDiagramWorkspaceViewModel Workspace { get; }
    public ObservableCollection<CommunityAuthoringDiagramStepViewModel> Steps { get; } = [];
    public ObservableCollection<CommunityAuthoringOrganizationCandidateViewModel> OrganizationCandidates { get; } = [];

    public IReadOnlyList<CommunityLedgerTemplateResponse> Templates { get; } =
        CommunityLedgerTemplateCatalog.All
            .Where(template => !template.IsInternalAggregationTemplate && template.LedgerBlocks.Count > 0)
            .OrderBy(template => template.Category, StringComparer.Ordinal)
            .ThenBy(template => template.DisplayName, StringComparer.Ordinal)
            .ToArray();

    public string SelectedTemplateKey
    {
        get => _selectedTemplateKey;
        set => SetProperty(
            ref _selectedTemplateKey,
            string.IsNullOrWhiteSpace(value) ? CommunityLedgerTemplateKeys.GroupImport : value.Trim());
    }

    public string NewStepTitle
    {
        get => _newStepTitle;
        set => SetProperty(ref _newStepTitle, value ?? string.Empty);
    }

    public string NewStepDescription
    {
        get => _newStepDescription;
        set => SetProperty(ref _newStepDescription, value ?? string.Empty);
    }

    public string SelectedOrganizationNodeKey
    {
        get => _selectedOrganizationNodeKey;
        set => SetProperty(ref _selectedOrganizationNodeKey, value ?? string.Empty);
    }

    public string NewOrganizationName
    {
        get => _newOrganizationName;
        set => SetProperty(ref _newOrganizationName, value ?? string.Empty);
    }

    public string NewOrganizationRoleLabel
    {
        get => _newOrganizationRoleLabel;
        set => SetProperty(ref _newOrganizationRoleLabel, value ?? string.Empty);
    }

    public string NewOrganizationCountryCode
    {
        get => _newOrganizationCountryCode;
        set => SetProperty(ref _newOrganizationCountryCode, value ?? string.Empty);
    }

    public string NewOrganizationWebsiteUrl
    {
        get => _newOrganizationWebsiteUrl;
        set => SetProperty(ref _newOrganizationWebsiteUrl, value ?? string.Empty);
    }

    public string NewOrganizationPublicBusinessEmail
    {
        get => _newOrganizationPublicBusinessEmail;
        set => SetProperty(ref _newOrganizationPublicBusinessEmail, value ?? string.Empty);
    }

    public string NewOrganizationContactSourceUrl
    {
        get => _newOrganizationContactSourceUrl;
        set => SetProperty(ref _newOrganizationContactSourceUrl, value ?? string.Empty);
    }

    public bool NewOrganizationContactSourceReviewed
    {
        get => _newOrganizationContactSourceReviewed;
        set => SetProperty(ref _newOrganizationContactSourceReviewed, value);
    }

    public string OrganizationDirectorySearchText
    {
        get => _organizationDirectorySearchText;
        set => SetProperty(ref _organizationDirectorySearchText, value ?? string.Empty);
    }

    public IReadOnlyList<ThirdPartyLogisticsProviderDirectoryItem> OrganizationDirectoryItems
    {
        get => _organizationDirectoryItems;
        private set => SetProperty(ref _organizationDirectoryItems, value);
    }

    public bool IsSearchingOrganizationDirectory
    {
        get => _isSearchingOrganizationDirectory;
        private set => SetProperty(ref _isSearchingOrganizationDirectory, value);
    }

    public string? OrganizationDirectoryErrorMessage
    {
        get => _organizationDirectoryErrorMessage;
        private set => SetProperty(ref _organizationDirectoryErrorMessage, value);
    }

    public string? StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public bool HasSteps => Steps.Count > 0;
    public bool HasOrganizationCandidates => OrganizationCandidates.Count > 0;
    public int EdgeCount => Math.Max(0, Steps.Count - 1);
    public string OutreachReadinessLabel
        => OrganizationCandidates.Count == 0
            ? "업체 후보 수집 중"
            : OrganizationCandidates.All(candidate =>
                candidate.ContactSourceReviewed
                && candidate.PublicBusinessEmail.Trim().Length > 0
                && candidate.ContactSourceUrl.Trim().Length > 0)
                ? "수동 이메일 초안 준비 가능"
                : "공개 연락처 검토 필요";

    public async Task SearchOrganizationDirectoryAsync(
        CancellationToken cancellationToken = default)
    {
        if (IsSearchingOrganizationDirectory)
        {
            return;
        }

        IsSearchingOrganizationDirectory = true;
        OrganizationDirectoryErrorMessage = null;
        try
        {
            var response = await _organizationDirectoryClient.SearchThirdPartyLogisticsAsync(
                OrganizationDirectorySearchText,
                12,
                cancellationToken);
            OrganizationDirectoryItems = response.Items;
            if (!response.Success)
            {
                OrganizationDirectoryErrorMessage = response.ErrorMessage
                    ?? "업체 디렉터리를 조회하지 못했습니다.";
                return;
            }

            StatusMessage = OrganizationDirectoryItems.Count == 0
                ? "검색 조건에 맞는 3PL 조사 후보가 없습니다."
                : $"3PL 조사 후보 {OrganizationDirectoryItems.Count:N0}곳을 확인했습니다.";
        }
        catch (Exception exception)
        {
            OrganizationDirectoryItems = [];
            OrganizationDirectoryErrorMessage = $"업체 디렉터리 조회 중 문제가 발생했습니다: {exception.Message}";
        }
        finally
        {
            IsSearchingOrganizationDirectory = false;
        }
    }

    public bool AttachOrganizationDirectoryItem(
        ThirdPartyLogisticsProviderDirectoryItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        var nodeKey = SelectedOrganizationNodeKey.Trim();
        if (!Steps.Any(step => string.Equals(step.Key, nodeKey, StringComparison.OrdinalIgnoreCase)))
        {
            StatusMessage = "업체가 참여할 다이어그램 단계를 선택해 주세요.";
            return false;
        }

        if (OrganizationCandidates.Any(candidate =>
                string.Equals(candidate.DiagramNodeKey, nodeKey, StringComparison.OrdinalIgnoreCase)
                && string.Equals(candidate.SourceReferenceKey, item.ProviderKey, StringComparison.OrdinalIgnoreCase)))
        {
            StatusMessage = $"'{item.DisplayName}'은(는) 이미 선택한 단계에 연결되어 있습니다.";
            return false;
        }

        var evidence = item.Evidence.FirstOrDefault();
        OrganizationCandidates.Add(new CommunityAuthoringOrganizationCandidateViewModel(
            $"organization-{Guid.NewGuid():N}",
            nodeKey,
            item.DisplayName,
            ResolveDirectoryRoleLabel(item),
            item.MarketCode,
            item.OfficialWebsiteUrl,
            string.Empty,
            evidence?.SourceUrl ?? item.OfficialWebsiteUrl,
            false,
            DiagramOrganizationSourceKindCodes.ThirdPartyLogisticsDirectory,
            item.ProviderKey,
            item.DirectoryStatusCode,
            item.PlatformRelationshipStatusCode,
            item.CompanySourceVerificationStatusCode,
            item.RegulatoryVerificationStatusCode,
            item.IsPlatformPartner,
            item.CanBeSelectedForOperations,
            item.CapabilityCodes));
        NotifyOrganizationCandidatesChanged();
        StatusMessage = $"'{item.DisplayName}'을(를) 선택한 단계의 조사 후보로 연결했습니다. 제휴·면허·시설 역량은 별도 확인이 필요합니다.";
        return true;
    }

    public void LoadSelectedTemplate()
    {
        var template = CommunityLedgerTemplateCatalog.Find(SelectedTemplateKey);
        SelectedTemplateKey = template.Key;
        Workspace.SelectedLedgerTemplateKey = template.Key;
        Steps.Clear();
        OrganizationCandidates.Clear();

        var blocks = template.LedgerBlocks.Take(12).ToArray();
        for (var index = 0; index < blocks.Length; index++)
        {
            var block = blocks[index];
            var nextBlock = index + 1 < blocks.Length ? blocks[index + 1] : null;
            var relation = nextBlock is null
                ? null
                : template.BlockRelations.FirstOrDefault(item =>
                    string.Equals(item.FromBlockCode, block.Code, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(item.ToBlockCode, nextBlock.Code, StringComparison.OrdinalIgnoreCase));
            Steps.Add(new CommunityAuthoringDiagramStepViewModel(
                block.Code,
                block.DisplayName,
                block.Purpose,
                string.IsNullOrWhiteSpace(block.UiSectionHint) ? template.DisplayName : block.UiSectionHint,
                string.IsNullOrWhiteSpace(block.BlockType) ? "work" : block.BlockType,
                relation?.Description ?? "다음 단계"));
        }

        SelectedOrganizationNodeKey = Steps.FirstOrDefault()?.Key ?? string.Empty;
        SynchronizeCanvas();
        StatusMessage = $"{template.DisplayName} 기본 흐름 {Steps.Count:N0}단계를 불러왔습니다.";
    }

    public bool AddStep()
    {
        var title = NewStepTitle.Trim();
        if (title.Length == 0)
        {
            StatusMessage = "추가할 단계 이름을 입력해 주세요.";
            return false;
        }

        if (Steps.Any(step => string.Equals(step.Title.Trim(), title, StringComparison.OrdinalIgnoreCase)))
        {
            StatusMessage = "같은 이름의 단계가 이미 있습니다.";
            return false;
        }

        Steps.Add(new CommunityAuthoringDiagramStepViewModel(
            $"custom-{Guid.NewGuid():N}",
            title,
            NewStepDescription.Trim(),
            "직접 추가",
            "work",
            "다음 단계"));
        SelectedOrganizationNodeKey = Steps[^1].Key;
        NewStepTitle = string.Empty;
        NewStepDescription = string.Empty;
        SynchronizeCanvas();
        StatusMessage = $"'{title}' 단계를 추가했습니다.";
        return true;
    }

    public bool AddOrganizationCandidate()
    {
        var nodeKey = SelectedOrganizationNodeKey.Trim();
        if (!Steps.Any(step => string.Equals(step.Key, nodeKey, StringComparison.OrdinalIgnoreCase)))
        {
            StatusMessage = "업체가 참여할 다이어그램 단계를 선택해 주세요.";
            return false;
        }

        var organizationName = NewOrganizationName.Trim();
        if (organizationName.Length == 0)
        {
            StatusMessage = "업체 또는 기관 이름을 입력해 주세요.";
            return false;
        }

        var countryCode = string.IsNullOrWhiteSpace(NewOrganizationCountryCode)
            ? "ZZ"
            : NewOrganizationCountryCode.Trim().ToUpperInvariant();
        if (countryCode.Length != 2 || countryCode.Any(character => !char.IsLetter(character)))
        {
            StatusMessage = "업체 국가 코드는 ISO 2자리 형식으로 입력해 주세요.";
            return false;
        }

        var websiteUrl = NormalizeHttpUrl(NewOrganizationWebsiteUrl, "업체 웹사이트");
        var contactSourceUrl = NormalizeHttpUrl(NewOrganizationContactSourceUrl, "연락처 근거 주소");
        if (websiteUrl is null || contactSourceUrl is null)
        {
            return false;
        }

        var publicBusinessEmail = NewOrganizationPublicBusinessEmail.Trim();
        if (publicBusinessEmail.Length > 0 && !MailAddress.TryCreate(publicBusinessEmail, out _))
        {
            StatusMessage = "공개 업무 이메일 형식을 확인해 주세요.";
            return false;
        }

        var contactSourceReviewed = NewOrganizationContactSourceReviewed
                                    && publicBusinessEmail.Length > 0
                                    && contactSourceUrl.Length > 0;
        OrganizationCandidates.Add(new CommunityAuthoringOrganizationCandidateViewModel(
            $"organization-{Guid.NewGuid():N}",
            nodeKey,
            organizationName,
            string.IsNullOrWhiteSpace(NewOrganizationRoleLabel)
                ? "협업 후보"
                : NewOrganizationRoleLabel.Trim(),
            countryCode,
            websiteUrl,
            publicBusinessEmail,
            contactSourceUrl,
            contactSourceReviewed,
            companySourceVerificationStatusCode: contactSourceReviewed
                ? DiagramOrganizationVerificationStatusCodes.PublicSourceReviewed
                : DiagramOrganizationVerificationStatusCodes.VerificationRequired));
        NewOrganizationName = string.Empty;
        NewOrganizationRoleLabel = string.Empty;
        NewOrganizationWebsiteUrl = string.Empty;
        NewOrganizationPublicBusinessEmail = string.Empty;
        NewOrganizationContactSourceUrl = string.Empty;
        NewOrganizationContactSourceReviewed = false;
        NotifyOrganizationCandidatesChanged();
        StatusMessage = $"'{organizationName}'을(를) 다이어그램 업체 후보로 연결했습니다.";
        return true;
    }

    public void RemoveOrganizationCandidate(CommunityAuthoringOrganizationCandidateViewModel candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        if (!OrganizationCandidates.Remove(candidate))
        {
            return;
        }

        NotifyOrganizationCandidatesChanged();
        StatusMessage = $"'{candidate.OrganizationName}' 업체 후보를 제거했습니다.";
    }

    public IReadOnlyList<CommunityAuthoringOrganizationCandidateViewModel> GetOrganizationsForStep(
        string nodeKey)
        => OrganizationCandidates
            .Where(candidate => string.Equals(
                candidate.DiagramNodeKey,
                nodeKey,
                StringComparison.OrdinalIgnoreCase))
            .ToArray();

    public void RemoveStep(CommunityAuthoringDiagramStepViewModel step)
    {
        ArgumentNullException.ThrowIfNull(step);
        if (!Steps.Remove(step))
        {
            return;
        }

        foreach (var candidate in OrganizationCandidates
                     .Where(candidate => string.Equals(
                         candidate.DiagramNodeKey,
                         step.Key,
                         StringComparison.OrdinalIgnoreCase))
                     .ToArray())
        {
            OrganizationCandidates.Remove(candidate);
        }

        SelectedOrganizationNodeKey = Steps.FirstOrDefault()?.Key ?? string.Empty;

        SynchronizeCanvas();
        NotifyOrganizationCandidatesChanged();
        StatusMessage = $"'{step.Title}' 단계를 제거했습니다.";
    }

    public void MoveStep(CommunityAuthoringDiagramStepViewModel step, int offset)
    {
        ArgumentNullException.ThrowIfNull(step);
        var currentIndex = Steps.IndexOf(step);
        var nextIndex = currentIndex + offset;
        if (currentIndex < 0 || nextIndex < 0 || nextIndex >= Steps.Count)
        {
            return;
        }

        Steps.Move(currentIndex, nextIndex);
        SynchronizeCanvas();
        StatusMessage = $"'{step.Title}' 단계 순서를 바꿨습니다.";
    }

    public CommunityComposerDraftTransition CreateCommunityDraft(
        IReadOnlyList<string> boardCategories,
        string appName,
        string roleLabel)
    {
        if (Steps.Count == 0)
        {
            throw new InvalidOperationException("글에 넣을 다이어그램 단계가 없습니다.");
        }

        var duplicate = Steps
            .GroupBy(step => step.Title.Trim(), StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => string.IsNullOrWhiteSpace(group.Key) || group.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidOperationException("다이어그램 단계 이름은 비어 있거나 중복될 수 없습니다.");
        }

        var transition = Workspace.CreateCommunityDraft(
            Steps.Select(step => new CommunityDiagramDraftNode(
                    step.Title.Trim(),
                    string.IsNullOrWhiteSpace(step.GroupLabel) ? "업무 단계" : step.GroupLabel.Trim(),
                    step.Description.Trim(),
                    string.IsNullOrWhiteSpace(step.Kind) ? "work" : step.Kind.Trim()))
                .ToArray(),
            Steps.Zip(Steps.Skip(1), (from, to) => new CommunityDiagramDraftEdge(
                    from.Title.Trim(),
                    to.Title.Trim(),
                    string.IsNullOrWhiteSpace(from.NextLabel) ? "다음 단계" : from.NextLabel.Trim()))
                .ToArray(),
            boardCategories,
            appName,
            roleLabel);

        if (OrganizationCandidates.Count == 0)
        {
            return transition;
        }

        var organizationLines = OrganizationCandidates.Select(candidate =>
        {
            var step = Steps.FirstOrDefault(item => string.Equals(
                item.Key,
                candidate.DiagramNodeKey,
                StringComparison.OrdinalIgnoreCase));
            var source = string.IsNullOrWhiteSpace(candidate.ContactSourceUrl)
                ? string.Empty
                : $" · 공개 근거: {candidate.ContactSourceUrl.Trim()}";
            return $"- {step?.Title ?? "연결 단계 미확인"}: {candidate.OrganizationName.Trim()} ({candidate.RoleLabel.Trim()} · {candidate.CountryCode.Trim().ToUpperInvariant()}){source}";
        });
        return transition with
        {
            Body = string.Join(
                Environment.NewLine,
                [
                    transition.Body,
                    string.Empty,
                    "함께 알아차린 업체·기관 후보",
                    string.Empty,
                    .. organizationLines,
                    string.Empty,
                    "※ 위 업체·기관은 아직 협업이 확정되지 않은 후보입니다. 연락 전 공개 출처와 담당 창구를 다시 확인합니다."
                ])
        };
    }

    public DiagramSnapshotDto CreateDiagramSnapshot(
        string? diagramId = null,
        string? ledgerId = null)
        => new()
        {
            DiagramId = string.IsNullOrWhiteSpace(diagramId)
                ? $"authoring-{Guid.NewGuid():N}"
                : diagramId.Trim(),
            DiagramName = $"{CommunityLedgerTemplateCatalog.Find(SelectedTemplateKey).DisplayName} 작성 흐름",
            LedgerId = string.IsNullOrWhiteSpace(ledgerId) ? null : ledgerId.Trim(),
            LedgerTemplateKey = SelectedTemplateKey,
            Nodes = Steps.Select((step, index) => new DiagramNodeDto
            {
                NodeId = step.Key,
                Kind = string.IsNullOrWhiteSpace(step.Kind) ? "work" : step.Kind.Trim(),
                Title = step.Title.Trim(),
                GroupLabel = string.IsNullOrWhiteSpace(step.GroupLabel) ? "업무 단계" : step.GroupLabel.Trim(),
                Description = step.Description.Trim(),
                X = 80,
                Y = 80 + index * 140,
                OrganizationReferences = OrganizationCandidates
                    .Where(candidate => string.Equals(
                        candidate.DiagramNodeKey,
                        step.Key,
                        StringComparison.OrdinalIgnoreCase))
                    .Select(candidate => candidate.ToDiagramReference())
                    .ToArray()
            }).ToArray(),
            Edges = Steps.Zip(Steps.Skip(1), (from, to) => new DiagramEdgeDto
            {
                EdgeId = $"edge-{from.Key}-{to.Key}",
                FromNodeId = from.Key,
                ToNodeId = to.Key,
                Label = string.IsNullOrWhiteSpace(from.NextLabel) ? "다음 단계" : from.NextLabel.Trim()
            }).ToArray()
        };

    public YouTubeImportJourneyDraftUpdateRequest CreateImportJourneyUpdate()
        => new()
        {
            LedgerTemplateKey = SelectedTemplateKey,
            Nodes = Steps.Select(step => new YouTubeImportJourneyNodeDto(
                step.Key,
                step.Title.Trim(),
                step.Description.Trim(),
                string.IsNullOrWhiteSpace(step.GroupLabel) ? "업무 단계" : step.GroupLabel.Trim(),
                string.IsNullOrWhiteSpace(step.Kind) ? "work" : step.Kind.Trim())).ToArray(),
            Edges = Steps.Zip(Steps.Skip(1), (from, to) => new YouTubeImportJourneyEdgeDto(
                from.Key,
                to.Key,
                string.IsNullOrWhiteSpace(from.NextLabel) ? "다음 단계" : from.NextLabel.Trim())).ToArray(),
            OrganizationCandidates = OrganizationCandidates.Select(candidate =>
                new YouTubeImportOrganizationCandidateDto(
                    candidate.CandidateKey,
                    candidate.DiagramNodeKey,
                    candidate.OrganizationName.Trim(),
                    candidate.RoleLabel.Trim(),
                    candidate.CountryCode.Trim().ToUpperInvariant(),
                    candidate.WebsiteUrl.Trim(),
                    candidate.PublicBusinessEmail.Trim(),
                    candidate.ContactSourceUrl.Trim(),
                    candidate.ContactSourceReviewed)
                {
                    SourceKindCode = candidate.SourceKindCode,
                    SourceReferenceKey = candidate.SourceReferenceKey,
                    DirectoryStatusCode = candidate.DirectoryStatusCode,
                    PlatformRelationshipStatusCode = candidate.PlatformRelationshipStatusCode,
                    CompanySourceVerificationStatusCode = candidate.CompanySourceVerificationStatusCode,
                    RegulatoryVerificationStatusCode = candidate.RegulatoryVerificationStatusCode,
                    IsPlatformPartner = candidate.IsPlatformPartner,
                    CanBeSelectedForOperations = candidate.CanBeSelectedForOperations,
                    CapabilityCodes = candidate.CapabilityCodes
                }).ToArray()
        };

    public bool LoadImportJourney(YouTubeImportJourneyDraftDto journey)
    {
        ArgumentNullException.ThrowIfNull(journey);
        if (journey.Nodes.Count == 0)
        {
            return false;
        }

        var templateKey = CommunityLedgerTemplateCatalog.All.Any(template => string.Equals(
            template.Key,
            journey.LedgerTemplateKey,
            StringComparison.OrdinalIgnoreCase))
            ? journey.LedgerTemplateKey
            : CommunityLedgerTemplateKeys.GroupImport;
        SelectedTemplateKey = templateKey;
        Workspace.SelectedLedgerTemplateKey = templateKey;
        Steps.Clear();
        OrganizationCandidates.Clear();

        foreach (var node in journey.Nodes)
        {
            var outgoing = journey.Edges.FirstOrDefault(edge => string.Equals(
                edge.FromNodeKey,
                node.NodeKey,
                StringComparison.OrdinalIgnoreCase));
            Steps.Add(new CommunityAuthoringDiagramStepViewModel(
                node.NodeKey,
                node.Title,
                node.Description,
                node.GroupLabel,
                node.Kind,
                outgoing?.Label ?? "다음 단계"));
        }

        foreach (var candidate in journey.OrganizationCandidates.Where(candidate =>
                     Steps.Any(step => string.Equals(
                         step.Key,
                         candidate.DiagramNodeKey,
                         StringComparison.OrdinalIgnoreCase))))
        {
            OrganizationCandidates.Add(new CommunityAuthoringOrganizationCandidateViewModel(
                candidate.CandidateKey,
                candidate.DiagramNodeKey,
                candidate.OrganizationName,
                candidate.RoleLabel,
                candidate.CountryCode,
                candidate.WebsiteUrl,
                candidate.PublicBusinessEmail,
                candidate.ContactSourceUrl,
                candidate.ContactSourceReviewed,
                candidate.SourceKindCode,
                candidate.SourceReferenceKey,
                candidate.DirectoryStatusCode,
                candidate.PlatformRelationshipStatusCode,
                candidate.CompanySourceVerificationStatusCode,
                candidate.RegulatoryVerificationStatusCode,
                candidate.IsPlatformPartner,
                candidate.CanBeSelectedForOperations,
                candidate.CapabilityCodes));
        }

        SelectedOrganizationNodeKey = Steps[0].Key;
        SynchronizeCanvas();
        NotifyOrganizationCandidatesChanged();
        StatusMessage = $"저장한 공동수입 여정 {Steps.Count:N0}단계와 업체 후보 {OrganizationCandidates.Count:N0}곳을 불러왔습니다.";
        return true;
    }

    public void Reset()
    {
        Steps.Clear();
        OrganizationCandidates.Clear();
        SelectedOrganizationNodeKey = string.Empty;
        Workspace.Canvas.Reset();
        StatusMessage = "빠른 흐름도를 비웠습니다.";
        NotifyDiagramChanged();
        NotifyOrganizationCandidatesChanged();
    }

    private void SynchronizeCanvas()
    {
        Workspace.Canvas.ResetConnections();
        Workspace.Canvas.SynchronizeNodeOrder(Steps.Select(step => step.Title));
        NotifyDiagramChanged();
    }

    private void NotifyDiagramChanged()
    {
        OnPropertyChanged(nameof(Steps));
        OnPropertyChanged(nameof(HasSteps));
        OnPropertyChanged(nameof(EdgeCount));
    }

    private void NotifyOrganizationCandidatesChanged()
    {
        OnPropertyChanged(nameof(OrganizationCandidates));
        OnPropertyChanged(nameof(HasOrganizationCandidates));
        OnPropertyChanged(nameof(OutreachReadinessLabel));
    }

    private string? NormalizeHttpUrl(string value, string label)
    {
        var normalized = value.Trim();
        if (normalized.Length == 0)
        {
            return string.Empty;
        }

        if (Uri.TryCreate(normalized, UriKind.Absolute, out var uri)
            && uri.Scheme is "http" or "https")
        {
            return uri.AbsoluteUri;
        }

        StatusMessage = $"{label}는 http 또는 https 주소로 입력해 주세요.";
        return null;
    }

    private static string ResolveDirectoryRoleLabel(
        ThirdPartyLogisticsProviderDirectoryItem item)
    {
        if (item.CapabilityCodes.Contains(
                ThirdPartyLogisticsProviderCapabilityCodes.CustomsBrokerage,
                StringComparer.OrdinalIgnoreCase))
        {
            return "통관 지원 후보";
        }

        if (item.CapabilityCodes.Contains(
                ThirdPartyLogisticsProviderCapabilityCodes.FreightForwarding,
                StringComparer.OrdinalIgnoreCase))
        {
            return "국제 물류 후보";
        }

        if (item.CapabilityCodes.Any(code => code is
                ThirdPartyLogisticsProviderCapabilityCodes.CustomsControlledWarehousing
                or ThirdPartyLogisticsProviderCapabilityCodes.ForeignTradeZoneOperations
                or ThirdPartyLogisticsProviderCapabilityCodes.PortDrayage
                or ThirdPartyLogisticsProviderCapabilityCodes.Transloading))
        {
            return "수입 물류 후보";
        }

        return "3PL 물류 후보";
    }
}
