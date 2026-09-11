using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Ssalddel.Unity.Learning;
using Ssalddel.Unity.OperationalTransport;
using Ssalddel.Unity.WorldProjection;
using UnityEngine;

namespace Ssalddel.Unity.Samples.LearningCards
{
    public sealed class 학습카드ApiOptions
    {
        public string BaseUrl { get; set; } = string.Empty;
        public int TimeoutSeconds { get; set; } = 15;
    }

    public sealed class Operational학습카드PublicationApiClient
        : I학습카드PublicationApiClient
    {
        public const string CatalogRoute = "api/integration/v1/unity-learning-cards";

        private readonly IOperationalWorldProjectionTransport transport;

        public Operational학습카드PublicationApiClient(학습카드ApiOptions apiOptions)
        {
            if (apiOptions == null) throw new ArgumentNullException(nameof(apiOptions));
            transport = new UnityWebRequestOperationalWorldProjectionTransport(
                new OperationalWorldProjectionEndpoint(
                    apiOptions.BaseUrl,
                    Math.Max(1, apiOptions.TimeoutSeconds)));
        }

        public async Task<학습카드PublicationCatalogApiModel> GetCatalogAsync(
            CancellationToken cancellationToken)
        {
            var json = await transport.GetAsync(
                CatalogRoute,
                false,
                false,
                cancellationToken);

            학습카드PublicationCatalogWire wire;
            try
            {
                wire = JsonUtility.FromJson<학습카드PublicationCatalogWire>(json);
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    "LearningCardCatalogJsonInvalid",
                    exception);
            }

            return wire?.ToApiModel()
                ?? throw new InvalidOperationException("LearningCardCatalogJsonInvalid");
        }
    }

#pragma warning disable 0649 // JsonUtility assigns serialized wire fields.
    [Serializable]
    internal sealed class 학습카드PublicationCatalogWire
    {
        public string schemaVersion = string.Empty;
        public 학습카드PublicationWire[] items = Array.Empty<학습카드PublicationWire>();

        public 학습카드PublicationCatalogApiModel ToApiModel()
            => new 학습카드PublicationCatalogApiModel
            {
                SchemaVersion = schemaVersion,
                Items = Array.ConvertAll(
                    items ?? Array.Empty<학습카드PublicationWire>(),
                    item => item?.ToApiModel()
                        ?? throw new InvalidOperationException("LearningCardCatalogItemInvalid")),
            };
    }

    [Serializable]
    internal sealed class 학습카드PublicationWire
    {
        public string schemaVersion = string.Empty;
        public string learningContentStableId = string.Empty;
        public int contentRevision;
        public string arcanaStableId = string.Empty;
        public string title = string.Empty;
        public string keyPhrase = string.Empty;
        public string interpretation = string.Empty;
        public string reflectionPrompt = string.Empty;
        public int reviewStatus;
        public int audioReviewStatus;
        public 학습카드SourceWire hongikAcademySource = new 학습카드SourceWire();
        public 학습카드GeneralMeaningWire generalMeaning;
        public 학습카드ImageWire image = new 학습카드ImageWire();
        public 학습카드EffectWire effect = new 학습카드EffectWire();
        public string editorialReviewStableId = string.Empty;
        public string approvedBy = string.Empty;
        public string publishedAtUtc = string.Empty;
        public string publicationHash = string.Empty;

        public 학습카드PublicationApiModel ToApiModel()
            => new 학습카드PublicationApiModel
            {
                SchemaVersion = schemaVersion,
                LearningContentStableId = learningContentStableId,
                ContentRevision = contentRevision,
                ArcanaStableId = arcanaStableId,
                Title = title,
                KeyPhrase = keyPhrase,
                Interpretation = interpretation,
                ReflectionPrompt = reflectionPrompt,
                ReviewStatus = reviewStatus,
                AudioReviewStatus = audioReviewStatus,
                HongikAcademySource = hongikAcademySource?.ToApiModel()
                    ?? throw new InvalidOperationException("LearningCardSourceJsonInvalid"),
                GeneralMeaning = generalMeaning?.ToApiModel(),
                Image = image?.ToApiModel()
                    ?? throw new InvalidOperationException("LearningCardImageJsonInvalid"),
                Effect = effect?.ToApiModel()
                    ?? throw new InvalidOperationException("LearningCardEffectJsonInvalid"),
                EditorialReviewStableId = editorialReviewStableId,
                ApprovedBy = approvedBy,
                PublishedAtUtc = ParseUtc(publishedAtUtc),
                PublicationHash = publicationHash,
            };

        private static DateTimeOffset ParseUtc(string value)
        {
            if (!DateTimeOffset.TryParse(
                    value,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out var parsed))
                throw new InvalidOperationException("LearningCardPublishedAtJsonInvalid");
            return parsed.ToUniversalTime();
        }
    }

    [Serializable]
    internal sealed class 학습카드SourceWire
    {
        public string youtubeVideoId = string.Empty;
        public long startMilliseconds;
        public long endMilliseconds;
        public string corePassage = string.Empty;
        public string sourceAnalysisId = string.Empty;
        public string[] evidenceSegmentIds = Array.Empty<string>();

        public 학습카드SourceProvenanceApiModel ToApiModel()
            => new 학습카드SourceProvenanceApiModel
            {
                YoutubeVideoId = youtubeVideoId,
                StartMilliseconds = startMilliseconds,
                EndMilliseconds = endMilliseconds,
                CorePassage = corePassage,
                SourceAnalysisId = sourceAnalysisId,
                EvidenceSegmentIds = evidenceSegmentIds ?? Array.Empty<string>(),
            };
    }

    [Serializable]
    internal sealed class 학습카드GeneralMeaningWire
    {
        public string sourceUri = string.Empty;
        public int revision;
        public string summary = string.Empty;
        public int reviewStatus;

        public 학습카드GeneralMeaningApiModel ToApiModel()
            => new 학습카드GeneralMeaningApiModel
            {
                SourceUri = sourceUri,
                Revision = revision,
                Summary = summary,
                ReviewStatus = reviewStatus,
            };
    }

    [Serializable]
    internal sealed class 학습카드ImageWire
    {
        public string containerName = string.Empty;
        public string objectName = string.Empty;
        public string sha256 = string.Empty;
        public string contentType = string.Empty;
        public long byteLength;
        public string sourceUri = string.Empty;
        public string licenseCode = string.Empty;

        public 학습카드ImageBlobApiModel ToApiModel()
            => new 학습카드ImageBlobApiModel
            {
                ContainerName = containerName,
                ObjectName = objectName,
                Sha256 = sha256,
                ContentType = contentType,
                ByteLength = byteLength,
                SourceUri = sourceUri,
                LicenseCode = licenseCode,
            };
    }

    [Serializable]
    internal sealed class 학습카드EffectWire
    {
        public string basisCode = string.Empty;
        public int revision;
        public string targetStatCode = string.Empty;
        public int statDelta;
        public string grantedRuleCode = string.Empty;
        public string rationale = string.Empty;

        public 학습카드EffectApiModel ToApiModel()
            => new 학습카드EffectApiModel
            {
                BasisCode = basisCode,
                Revision = revision,
                TargetStatCode = targetStatCode,
                StatDelta = statDelta,
                GrantedRuleCode = grantedRuleCode,
                Rationale = rationale,
            };
    }
#pragma warning restore 0649
}
