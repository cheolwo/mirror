using CommunityToolkit.Mvvm.ComponentModel;
using Hongdal.Contracts.Common.Community;

namespace Hongdal.Ui.Common.Areas.App.ViewModels;

public sealed class CommunityAuthoringDiagramStepViewModel : ObservableObject
{
    private string _title;
    private string _description;
    private string _groupLabel;
    private string _kind;
    private string _nextLabel;

    public CommunityAuthoringDiagramStepViewModel(
        string key,
        string title,
        string description,
        string groupLabel,
        string kind,
        string nextLabel)
    {
        Key = key;
        _title = title;
        _description = description;
        _groupLabel = groupLabel;
        _kind = kind;
        _nextLabel = nextLabel;
    }

    public string Key { get; }
    public string Title { get => _title; set => SetProperty(ref _title, value ?? string.Empty); }
    public string Description { get => _description; set => SetProperty(ref _description, value ?? string.Empty); }
    public string GroupLabel { get => _groupLabel; set => SetProperty(ref _groupLabel, value ?? string.Empty); }
    public string Kind { get => _kind; set => SetProperty(ref _kind, value ?? string.Empty); }
    public string NextLabel { get => _nextLabel; set => SetProperty(ref _nextLabel, value ?? string.Empty); }
}

public sealed class CommunityAuthoringOrganizationCandidateViewModel : ObservableObject
{
    private string _diagramNodeKey;
    private string _organizationName;
    private string _roleLabel;
    private string _countryCode;
    private string _websiteUrl;
    private string _publicBusinessEmail;
    private string _contactSourceUrl;
    private bool _contactSourceReviewed;
    private string _sourceKindCode;
    private string _sourceReferenceKey;
    private string _directoryStatusCode;
    private string _platformRelationshipStatusCode;
    private string _companySourceVerificationStatusCode;
    private string _regulatoryVerificationStatusCode;

    public CommunityAuthoringOrganizationCandidateViewModel(
        string candidateKey,
        string diagramNodeKey,
        string organizationName,
        string roleLabel,
        string countryCode,
        string websiteUrl,
        string publicBusinessEmail,
        string contactSourceUrl,
        bool contactSourceReviewed,
        string sourceKindCode = DiagramOrganizationSourceKindCodes.ManualResearch,
        string sourceReferenceKey = "",
        string directoryStatusCode = "",
        string platformRelationshipStatusCode = "",
        string companySourceVerificationStatusCode = DiagramOrganizationVerificationStatusCodes.VerificationRequired,
        string regulatoryVerificationStatusCode = "",
        bool isPlatformPartner = false,
        bool canBeSelectedForOperations = false,
        IReadOnlyList<string>? capabilityCodes = null)
    {
        CandidateKey = candidateKey;
        _diagramNodeKey = diagramNodeKey;
        _organizationName = organizationName;
        _roleLabel = roleLabel;
        _countryCode = countryCode;
        _websiteUrl = websiteUrl;
        _publicBusinessEmail = publicBusinessEmail;
        _contactSourceUrl = contactSourceUrl;
        _contactSourceReviewed = contactSourceReviewed;
        _sourceKindCode = sourceKindCode;
        _sourceReferenceKey = sourceReferenceKey;
        _directoryStatusCode = directoryStatusCode;
        _platformRelationshipStatusCode = platformRelationshipStatusCode;
        _companySourceVerificationStatusCode = companySourceVerificationStatusCode;
        _regulatoryVerificationStatusCode = regulatoryVerificationStatusCode;
        IsPlatformPartner = isPlatformPartner;
        CanBeSelectedForOperations = canBeSelectedForOperations;
        CapabilityCodes = capabilityCodes?.ToArray() ?? [];
    }

    public string CandidateKey { get; }
    public string DiagramNodeKey { get => _diagramNodeKey; set => SetProperty(ref _diagramNodeKey, value ?? string.Empty); }
    public string OrganizationName { get => _organizationName; set => SetProperty(ref _organizationName, value ?? string.Empty); }
    public string RoleLabel { get => _roleLabel; set => SetProperty(ref _roleLabel, value ?? string.Empty); }
    public string CountryCode { get => _countryCode; set => SetProperty(ref _countryCode, value ?? string.Empty); }
    public string WebsiteUrl { get => _websiteUrl; set => SetProperty(ref _websiteUrl, value ?? string.Empty); }
    public string PublicBusinessEmail { get => _publicBusinessEmail; set => SetProperty(ref _publicBusinessEmail, value ?? string.Empty); }
    public string ContactSourceUrl { get => _contactSourceUrl; set => SetProperty(ref _contactSourceUrl, value ?? string.Empty); }
    public bool ContactSourceReviewed { get => _contactSourceReviewed; set => SetProperty(ref _contactSourceReviewed, value); }
    public string SourceKindCode { get => _sourceKindCode; set => SetProperty(ref _sourceKindCode, value ?? string.Empty); }
    public string SourceReferenceKey { get => _sourceReferenceKey; set => SetProperty(ref _sourceReferenceKey, value ?? string.Empty); }
    public string DirectoryStatusCode { get => _directoryStatusCode; set => SetProperty(ref _directoryStatusCode, value ?? string.Empty); }
    public string PlatformRelationshipStatusCode { get => _platformRelationshipStatusCode; set => SetProperty(ref _platformRelationshipStatusCode, value ?? string.Empty); }
    public string CompanySourceVerificationStatusCode { get => _companySourceVerificationStatusCode; set => SetProperty(ref _companySourceVerificationStatusCode, value ?? string.Empty); }
    public string RegulatoryVerificationStatusCode { get => _regulatoryVerificationStatusCode; set => SetProperty(ref _regulatoryVerificationStatusCode, value ?? string.Empty); }
    public bool IsPlatformPartner { get; }
    public bool CanBeSelectedForOperations { get; }
    public IReadOnlyList<string> CapabilityCodes { get; }

    public DiagramOrganizationReferenceDto ToDiagramReference()
        => new()
        {
            ReferenceId = CandidateKey,
            OrganizationKey = string.IsNullOrWhiteSpace(SourceReferenceKey)
                ? CandidateKey
                : SourceReferenceKey,
            DisplayName = OrganizationName.Trim(),
            RoleLabel = RoleLabel.Trim(),
            CountryCode = CountryCode.Trim().ToUpperInvariant(),
            OfficialWebsiteUrl = WebsiteUrl.Trim(),
            SourceKindCode = SourceKindCode,
            SourceReferenceUrl = ContactSourceUrl.Trim(),
            DirectoryStatusCode = DirectoryStatusCode,
            PlatformRelationshipStatusCode = PlatformRelationshipStatusCode,
            CompanySourceVerificationStatusCode = CompanySourceVerificationStatusCode,
            RegulatoryVerificationStatusCode = RegulatoryVerificationStatusCode,
            IsPlatformPartner = IsPlatformPartner,
            CanBeSelectedForOperations = CanBeSelectedForOperations,
            CapabilityCodes = CapabilityCodes
        };
}
