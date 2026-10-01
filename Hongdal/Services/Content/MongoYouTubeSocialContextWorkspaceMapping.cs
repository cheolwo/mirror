using System.Globalization;
using System.Net.Mail;
using Hongdal.Contracts.Common.Community;
using Hongdal.Contracts.Common.Content;

namespace Hongdal.Services.Content;

public sealed partial class MongoYouTubeSocialContextWorkspaceStore
{
    private static List<YouTubeSocialContextSourceGroupDocument> BuildSourceGroups(
        YouTubeSocialContextResearchResponse research)
    {
        var descriptors = research.Sources.ToDictionary(
            source => source.SourceKey,
            StringComparer.OrdinalIgnoreCase);
        var sourceKeys = research.Sources.Select(source => source.SourceKey)
            .Concat(research.Items.Select(item => item.SourceKey))
            .Distinct(StringComparer.OrdinalIgnoreCase);
        return sourceKeys
            .Select(sourceKey =>
            {
                var items = research.Items
                    .Where(item => string.Equals(item.SourceKey, sourceKey, StringComparison.OrdinalIgnoreCase))
                    .Select(ToDocument)
                    .ToList();
                var descriptor = descriptors.GetValueOrDefault(sourceKey)
                                 ?? CreateFallbackSource(sourceKey, items.FirstOrDefault()?.Provider);
                return new YouTubeSocialContextSourceGroupDocument
                {
                    Source = ToDocument(descriptor),
                    Items = items
                };
            })
            .ToList();
    }

    private static SocialMediaResearchSourceDto CreateFallbackSource(
        string sourceKey,
        string? provider)
        => new(
            sourceKey,
            Normalize(provider, "외부 SNS", 120),
            Normalize(provider, sourceKey, 120),
            string.Empty,
            true,
            true,
            false);

    private static YouTubeSocialContextWorkspaceDraftDocument ToDraftDocument(
        YouTubeSocialContextPostDraftDto draft,
        string sharedLinkUrl,
        DateTime now,
        bool isManuallyEdited)
        => new()
        {
            Title = Normalize(draft.Title, "YouTube 함께 보기", 160),
            Body = Normalize(draft.Body, string.Empty, 4_000),
            SharedLinkUrl = NormalizeUrl(sharedLinkUrl),
            WorkflowTag = Normalize(draft.CollectiveAction.WorkflowTag, string.Empty, 100),
            CollectiveAction = ToDocument(draft.CollectiveAction),
            IsManuallyEdited = isManuallyEdited,
            UpdatedAtUtc = now
        };

    private static YouTubeSocialContextWorkspaceDto ToDto(
        YouTubeSocialContextWorkspaceDocument document)
        => new(
            document.Id,
            document.Revision,
            document.Status,
            ToDto(document.Video),
            document.SearchTerms.ToArray(),
            document.AdjacentTopics.ToArray(),
            document.SourceTargets.Select(ToDto).ToArray(),
            document.TakePerSource,
            document.SocialContextSources.Select(ToDto).ToArray(),
            document.Failures.Select(ToDto).ToArray(),
            ToDto(document.Draft),
            document.PublishedPostId,
            document.PublicationLinks.Select(ToDto).ToArray(),
            EnsureUtc(document.LastResearchedAtUtc),
            EnsureUtc(document.CreatedAtUtc),
            EnsureUtc(document.UpdatedAtUtc),
            document.UpdatedByDisplayName)
        {
            ImportJourney = ToDto(document.ImportJourney ?? new YouTubeImportJourneyDraftDocument())
        };

    private static YouTubeSocialContextWorkspaceSummaryDto ToSummaryDto(
        YouTubeSocialContextWorkspaceDocument document)
        => new(
            document.Id,
            document.Revision,
            document.Status,
            document.Video.VideoId,
            document.Video.Title,
            document.Video.ChannelName,
            document.SocialContextSources.Sum(group => group.Items.Count),
            document.PublishedPostId,
            EnsureUtc(document.UpdatedAtUtc))
        {
            ImportJourneyNodeCount = document.ImportJourney?.Nodes.Count ?? 0,
            OrganizationCandidateCount = document.ImportJourney?.OrganizationCandidates.Count ?? 0,
            OutreachReadinessCode = Normalize(
                document.ImportJourney?.OutreachReadinessCode,
                YouTubeImportOutreachReadinessCodes.Collecting,
                100)
        };

    private static YouTubeSocialContextSourceGroupDto ToDto(
        YouTubeSocialContextSourceGroupDocument document)
        => new(ToDto(document.Source), document.Items.Select(ToDto).ToArray());

    private static List<SocialMediaResearchTargetDocument> BuildTargets(
        IEnumerable<SocialMediaResearchTargetDto>? targets)
        => (targets ?? [])
            .Where(target => target is not null && !string.IsNullOrWhiteSpace(target.SourceKey))
            .GroupBy(target => target.SourceKey.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group => new SocialMediaResearchTargetDocument
            {
                SourceKey = Normalize(group.Key, string.Empty, 100),
                StartUrls = group
                    .SelectMany(target => target.StartUrls ?? [])
                    .Select(NormalizeOptionalUrl)
                    .Where(url => url is not null)
                    .Cast<string>()
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(30)
                    .ToList()
            })
            .ToList();

    private static SocialMediaResearchTargetDto ToDto(SocialMediaResearchTargetDocument document)
        => new(document.SourceKey, document.StartUrls.ToArray());

    private static YouTubeSocialContextWorkspaceDraftDto ToDto(
        YouTubeSocialContextWorkspaceDraftDocument document)
        => new(
            document.Nickname,
            document.Category,
            document.WorkflowTag,
            document.RoleTag,
            document.Title,
            document.Body,
            document.SharedLinkUrl,
            ToDto(document.CollectiveAction),
            document.IsManuallyEdited,
            EnsureUtc(document.UpdatedAtUtc));

    private static YouTubeSocialContextPublicationLinkDto ToDto(
        YouTubeSocialContextPublicationLinkDocument document)
        => new(document.PostId, EnsureUtc(document.LinkedAtUtc), document.LinkedByDisplayName);

    private static YouTubeSocialContextVideoDocument ToDocument(YouTubeSocialContextVideoDto dto)
        => new()
        {
            VideoId = NormalizeVideoId(dto.VideoId),
            ChannelName = Normalize(dto.ChannelName, string.Empty, 300),
            Title = Normalize(dto.Title, dto.VideoId, 500),
            Summary = Normalize(dto.Summary, string.Empty, 4_000),
            OriginalUrl = NormalizeUrl(dto.OriginalUrl),
            ThumbnailUrl = NormalizeOptionalUrl(dto.ThumbnailUrl),
            PublishedAtUtc = EnsureUtc(dto.PublishedAtUtc),
            CountryCode = Normalize(dto.CountryCode, "ZZ", 2).ToUpperInvariant(),
            LanguageCode = Normalize(dto.LanguageCode, "und", 20)
        };

    private static YouTubeSocialContextVideoDto ToDto(YouTubeSocialContextVideoDocument document)
        => new(
            document.VideoId,
            document.ChannelName,
            document.Title,
            document.Summary,
            document.OriginalUrl,
            document.ThumbnailUrl,
            EnsureUtc(document.PublishedAtUtc),
            document.CountryCode,
            document.LanguageCode);

    private static SocialMediaResearchSourceDocument ToDocument(SocialMediaResearchSourceDto dto)
        => new()
        {
            SourceKey = Normalize(dto.SourceKey, string.Empty, 100),
            Provider = Normalize(dto.Provider, string.Empty, 160),
            DisplayName = Normalize(dto.DisplayName, dto.SourceKey, 160),
            DocumentationUrl = NormalizeOptionalUrl(dto.DocumentationUrl),
            Enabled = dto.Enabled,
            SupportsKeywordSearch = dto.SupportsKeywordSearch,
            RequiresStartUrl = dto.RequiresStartUrl
        };

    private static SocialMediaResearchSourceDto ToDto(SocialMediaResearchSourceDocument document)
        => new(
            document.SourceKey,
            document.Provider,
            document.DisplayName,
            document.DocumentationUrl ?? string.Empty,
            document.Enabled,
            document.SupportsKeywordSearch,
            document.RequiresStartUrl);

    private static CommunityInformationCandidateDocument ToDocument(CommunityInformationCandidateDto dto)
        => new()
        {
            CandidateKey = Normalize(dto.CandidateKey, string.Empty, 300),
            SourceKey = Normalize(dto.SourceKey, string.Empty, 100),
            SourceType = Normalize(dto.SourceType, string.Empty, 60),
            Provider = Normalize(dto.Provider, string.Empty, 160),
            Title = Normalize(dto.Title, string.Empty, 500),
            Summary = Normalize(dto.Summary, string.Empty, 4_000),
            OriginalUrl = NormalizeUrl(dto.OriginalUrl),
            ThumbnailUrl = NormalizeOptionalUrl(dto.ThumbnailUrl),
            PublishedAtUtc = dto.PublishedAtUtc.HasValue ? EnsureUtc(dto.PublishedAtUtc.Value) : null,
            ReferenceDate = dto.ReferenceDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            CollectedAtUtc = EnsureUtc(dto.CollectedAtUtc),
            CountryCode = Normalize(dto.CountryCode, "ZZ", 2).ToUpperInvariant(),
            LanguageCode = Normalize(dto.LanguageCode, "und", 20),
            CurrencyCode = NormalizeOptional(dto.CurrencyCode, 12),
            Unit = NormalizeOptional(dto.Unit, 80),
            ReviewState = Normalize(dto.ReviewState, string.Empty, 60),
            TopicTags = NormalizeList(dto.TopicTags, 50, 100),
            SourceNotice = Normalize(dto.SourceNotice, string.Empty, 2_000),
            Limitations = Normalize(dto.Limitations, string.Empty, 2_000)
        };

    private static CommunityInformationCandidateDto ToDto(CommunityInformationCandidateDocument document)
        => new(
            document.CandidateKey,
            document.SourceKey,
            document.SourceType,
            document.Provider,
            document.Title,
            document.Summary,
            document.OriginalUrl,
            document.ThumbnailUrl,
            document.PublishedAtUtc.HasValue ? EnsureUtc(document.PublishedAtUtc.Value) : null,
            DateOnly.TryParseExact(
                document.ReferenceDate,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var referenceDate)
                ? referenceDate
                : null,
            EnsureUtc(document.CollectedAtUtc),
            document.CountryCode,
            document.LanguageCode,
            document.CurrencyCode,
            document.Unit,
            document.ReviewState,
            document.TopicTags.ToArray(),
            document.SourceNotice,
            document.Limitations);

    private static YouTubeSocialContextSourceFailureDocument ToDocument(
        YouTubeSocialContextSourceFailureDto dto)
        => new()
        {
            SourceKey = Normalize(dto.SourceKey, string.Empty, 100),
            Message = Normalize(dto.Message, string.Empty, 2_000)
        };

    private static YouTubeSocialContextSourceFailureDto ToDto(
        YouTubeSocialContextSourceFailureDocument document)
        => new(document.SourceKey, document.Message);

    private static YouTubeSocialContextCollectiveActionDocument ToDocument(
        YouTubeSocialContextCollectiveActionDraftDto dto)
        => new()
        {
            WorkflowTag = Normalize(dto.WorkflowTag, string.Empty, 100),
            PrimaryIntentTypeCode = Normalize(dto.PrimaryIntentTypeCode, string.Empty, 100),
            IntentTypeCodes = NormalizeList(dto.IntentTypeCodes, 20, 100),
            Prompt = Normalize(dto.Prompt, string.Empty, 1_000),
            NonBindingNotice = Normalize(dto.NonBindingNotice, string.Empty, 1_000),
            ParticipationEndpointTemplate = Normalize(dto.ParticipationEndpointTemplate, string.Empty, 500)
        };

    private static YouTubeSocialContextCollectiveActionDraftDto ToDto(
        YouTubeSocialContextCollectiveActionDocument document)
        => new(
            document.WorkflowTag,
            document.PrimaryIntentTypeCode,
            document.IntentTypeCodes.ToArray(),
            document.Prompt,
            document.NonBindingNotice,
            document.ParticipationEndpointTemplate);

    private static YouTubeImportJourneyDraftDocument ToDocument(
        YouTubeImportJourneyDraftUpdateRequest request,
        DateTime now)
    {
        var templateKey = Normalize(
            request.LedgerTemplateKey,
            CommunityLedgerTemplateKeys.GroupImport,
            100);
        if (!CommunityLedgerTemplateCatalog.All.Any(template =>
                string.Equals(template.Key, templateKey, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException("지원하지 않는 원장 여정입니다.", nameof(request.LedgerTemplateKey));
        }

        var nodes = (request.Nodes ?? [])
            .Where(node => node is not null)
            .Take(40)
            .Select(node => new YouTubeImportJourneyNodeDocument
            {
                NodeKey = NormalizeRequired(node.NodeKey, nameof(node.NodeKey), 120),
                Title = NormalizeRequired(node.Title, nameof(node.Title), 160),
                Description = Normalize(node.Description, string.Empty, 1_000),
                GroupLabel = Normalize(node.GroupLabel, "업무 단계", 120),
                Kind = Normalize(node.Kind, "work", 80)
            })
            .ToList();
        if (nodes.Select(node => node.NodeKey).Distinct(StringComparer.OrdinalIgnoreCase).Count() != nodes.Count)
        {
            throw new ArgumentException("다이어그램 단계 키는 중복될 수 없습니다.", nameof(request.Nodes));
        }

        var nodeKeys = nodes.Select(node => node.NodeKey).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var edges = (request.Edges ?? [])
            .Where(edge => edge is not null)
            .Take(80)
            .Select(edge => new YouTubeImportJourneyEdgeDocument
            {
                FromNodeKey = NormalizeRequired(edge.FromNodeKey, nameof(edge.FromNodeKey), 120),
                ToNodeKey = NormalizeRequired(edge.ToNodeKey, nameof(edge.ToNodeKey), 120),
                Label = Normalize(edge.Label, "다음 단계", 200)
            })
            .ToList();
        if (edges.Any(edge => !nodeKeys.Contains(edge.FromNodeKey) || !nodeKeys.Contains(edge.ToNodeKey)))
        {
            throw new ArgumentException("다이어그램 연결은 저장된 단계를 가리켜야 합니다.", nameof(request.Edges));
        }

        var organizations = (request.OrganizationCandidates ?? [])
            .Where(candidate => candidate is not null)
            .Take(40)
            .Select(candidate => ToDocument(candidate, nodeKeys))
            .ToList();
        if (organizations.Select(candidate => candidate.CandidateKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count() != organizations.Count)
        {
            throw new ArgumentException("업체 후보 키는 중복될 수 없습니다.", nameof(request.OrganizationCandidates));
        }

        return new YouTubeImportJourneyDraftDocument
        {
            LedgerTemplateKey = templateKey,
            Nodes = nodes,
            Edges = edges,
            OrganizationCandidates = organizations,
            OutreachReadinessCode = ResolveOutreachReadiness(organizations),
            UpdatedAtUtc = now
        };
    }

    private static YouTubeImportOrganizationCandidateDocument ToDocument(
        YouTubeImportOrganizationCandidateDto candidate,
        IReadOnlySet<string> nodeKeys)
    {
        var nodeKey = NormalizeRequired(candidate.DiagramNodeKey, nameof(candidate.DiagramNodeKey), 120);
        if (!nodeKeys.Contains(nodeKey))
        {
            throw new ArgumentException("업체 후보는 저장된 다이어그램 단계에 연결되어야 합니다.", nameof(candidate.DiagramNodeKey));
        }

        var countryCode = Normalize(candidate.CountryCode, "ZZ", 2).ToUpperInvariant();
        if (countryCode.Length != 2 || countryCode.Any(character => !char.IsLetter(character)))
        {
            throw new ArgumentException("업체 국가 코드는 ISO 2자리 형식이어야 합니다.", nameof(candidate.CountryCode));
        }

        var publicBusinessEmail = Normalize(candidate.PublicBusinessEmail, string.Empty, 320);
        if (publicBusinessEmail.Length > 0 && !MailAddress.TryCreate(publicBusinessEmail, out _))
        {
            throw new ArgumentException("공개 업무 이메일 형식을 확인해 주세요.", nameof(candidate.PublicBusinessEmail));
        }

        var contactSourceUrl = NormalizeOptionalUrl(candidate.ContactSourceUrl) ?? string.Empty;
        return new YouTubeImportOrganizationCandidateDocument
        {
            CandidateKey = NormalizeRequired(candidate.CandidateKey, nameof(candidate.CandidateKey), 120),
            DiagramNodeKey = nodeKey,
            OrganizationName = NormalizeRequired(candidate.OrganizationName, nameof(candidate.OrganizationName), 200),
            RoleLabel = Normalize(candidate.RoleLabel, "협업 후보", 160),
            CountryCode = countryCode,
            WebsiteUrl = NormalizeOptionalUrl(candidate.WebsiteUrl) ?? string.Empty,
            PublicBusinessEmail = publicBusinessEmail,
            ContactSourceUrl = contactSourceUrl,
            ContactSourceReviewed = candidate.ContactSourceReviewed
                                    && publicBusinessEmail.Length > 0
                                    && contactSourceUrl.Length > 0,
            SourceKindCode = Normalize(
                candidate.SourceKindCode,
                DiagramOrganizationSourceKindCodes.ManualResearch,
                100),
            SourceReferenceKey = Normalize(candidate.SourceReferenceKey, string.Empty, 160),
            DirectoryStatusCode = Normalize(candidate.DirectoryStatusCode, string.Empty, 100),
            PlatformRelationshipStatusCode = Normalize(
                candidate.PlatformRelationshipStatusCode,
                string.Empty,
                100),
            CompanySourceVerificationStatusCode = Normalize(
                candidate.CompanySourceVerificationStatusCode,
                DiagramOrganizationVerificationStatusCodes.VerificationRequired,
                100),
            RegulatoryVerificationStatusCode = Normalize(
                candidate.RegulatoryVerificationStatusCode,
                string.Empty,
                100),
            IsPlatformPartner = candidate.IsPlatformPartner,
            CanBeSelectedForOperations = candidate.CanBeSelectedForOperations,
            CapabilityCodes = (candidate.CapabilityCodes ?? [])
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .Select(code => code.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(30)
                .ToList()
        };
    }

    private static string ResolveOutreachReadiness(
        IReadOnlyCollection<YouTubeImportOrganizationCandidateDocument> organizations)
        => organizations.Count == 0
            ? YouTubeImportOutreachReadinessCodes.Collecting
            : organizations.All(candidate => candidate.ContactSourceReviewed)
                ? YouTubeImportOutreachReadinessCodes.ReadyForManualDraft
                : YouTubeImportOutreachReadinessCodes.ContactReviewRequired;

    private static YouTubeImportJourneyDraftDto ToDto(
        YouTubeImportJourneyDraftDocument document)
        => new(
            document.LedgerTemplateKey,
            document.Nodes.Select(node => new YouTubeImportJourneyNodeDto(
                node.NodeKey,
                node.Title,
                node.Description,
                node.GroupLabel,
                node.Kind)).ToArray(),
            document.Edges.Select(edge => new YouTubeImportJourneyEdgeDto(
                edge.FromNodeKey,
                edge.ToNodeKey,
                edge.Label)).ToArray(),
            document.OrganizationCandidates.Select(candidate => new YouTubeImportOrganizationCandidateDto(
                candidate.CandidateKey,
                candidate.DiagramNodeKey,
                candidate.OrganizationName,
                candidate.RoleLabel,
                candidate.CountryCode,
                candidate.WebsiteUrl,
                candidate.PublicBusinessEmail,
                candidate.ContactSourceUrl,
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
                CapabilityCodes = candidate.CapabilityCodes.ToArray()
            }).ToArray(),
            Normalize(
                document.OutreachReadinessCode,
                YouTubeImportOutreachReadinessCodes.Collecting,
                100),
            EnsureUtc(document.UpdatedAtUtc));

    private static string CreateWorkspaceId(string videoId) => $"youtube-{videoId}";

    private static string NormalizeVideoId(string? value)
    {
        var normalized = NormalizeRequired(value, nameof(value), 100);
        if (normalized.Any(character => !char.IsLetterOrDigit(character) && character is not '-' and not '_'))
        {
            throw new ArgumentException("YouTube 영상 ID 형식을 확인해 주세요.", nameof(value));
        }

        return normalized;
    }

    private static string NormalizeUrl(string? value)
        => NormalizeOptionalUrl(value)
           ?? (string.IsNullOrWhiteSpace(value)
               ? string.Empty
               : throw new ArgumentException("http 또는 https 주소만 저장할 수 있습니다.", nameof(value)));

    private static string? NormalizeOptionalUrl(string? value)
    {
        var normalized = NormalizeOptional(value, 2_000);
        if (normalized is null)
        {
            return null;
        }

        return Uri.TryCreate(normalized, UriKind.Absolute, out var uri)
               && uri.Scheme is "http" or "https"
            ? uri.AbsoluteUri
            : null;
    }

    private static List<string> NormalizeList(
        IEnumerable<string>? values,
        int maxCount,
        int maxLength)
        => (values ?? [])
            .Select(value => NormalizeOptional(value, maxLength))
            .Where(value => value is not null)
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(maxCount)
            .ToList();

    private static string NormalizeRequired(string? value, string parameterName, int maxLength)
        => NormalizeOptional(value, maxLength)
           ?? throw new ArgumentException($"{parameterName} 값이 필요합니다.", parameterName);

    private static string Normalize(string? value, string fallback, int maxLength)
        => NormalizeOptional(value, maxLength) ?? fallback;

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }

    private static DateTime EnsureUtc(DateTime value)
        => value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
}
