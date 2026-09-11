using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Ssalddel.Unity.OperationalTransport;
using Ssalddel.Unity.ResidentialPickup;
using Ssalddel.Unity.WorldProjection;
using UnityEngine;

namespace Ssalddel.Unity.Samples.ResidentialPickup
{
    public sealed class ResidentialPickupApiOptions
    {
        public string BaseUrl { get; set; } = string.Empty;
        public int TimeoutSeconds { get; set; } = 15;
    }

    public sealed class SimulatedResidentialPickupApiClient
        : IResidentialPickupPerspectiveApiClient
    {
        public Task<ResidentialPickupPerspectiveApiModel> GetAsync(
            string requestedRoleCode,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var roleLabel = string.Equals(
                requestedRoleCode,
                ResidentialPickupRoleCodes.Orderer,
                StringComparison.Ordinal)
                ? "내 수령 상품"
                : "내 하차 대상";
            return Task.FromResult(new ResidentialPickupPerspectiveApiModel
            {
                StableId = "role-perspective:residential-pickup."
                    + requestedRoleCode.ToLowerInvariant(),
                Revision = 1,
                AuthorizedRoleCode = requestedRoleCode,
                WorldZoneCode = "residential-pickup",
                ViewerScopeCode = "AuthorizedParty",
                SourceTypeCode = "SimulatedFixture",
                AuthorizationDecisionId = "simulation:residential-pickup",
                GeneratedAt = DateTimeOffset.Parse("2026-08-08T15:00:00+09:00"),
                PickupPoints = new[]
                {
                    new ResidentialPickupPointApiModel
                    {
                        StableId = "residential-pickup:91",
                        CanonicalTaskStableId = "unloading-task:71.91",
                        PickupPointLabel = "공동 수령지",
                        ProductLabel = "감자 20kg",
                        Quantity = 3,
                        StatusCode = ResidentialPickupStatusCodes.Arrived,
                        RoleLabel = roleLabel,
                        CanInspect = true,
                        UpdatedAt = DateTimeOffset.Parse("2026-08-08T15:00:00+09:00"),
                    },
                },
            });
        }
    }

    public sealed class OperationalResidentialPickupApiClient
        : IResidentialPickupPerspectiveApiClient
    {
        private readonly IOperationalWorldProjectionTransport transport;

        public OperationalResidentialPickupApiClient(
            ResidentialPickupApiOptions apiOptions,
            ResidentialPickupSessionTokenProvider sessionTokenProvider)
        {
            if (apiOptions == null) throw new ArgumentNullException(nameof(apiOptions));
            if (sessionTokenProvider == null) throw new ArgumentNullException(nameof(sessionTokenProvider));
            transport = new UnityWebRequestOperationalWorldProjectionTransport(
                new OperationalWorldProjectionEndpoint(
                    apiOptions.BaseUrl,
                    Math.Max(1, apiOptions.TimeoutSeconds)),
                sessionTokenProvider);
        }

        public async Task<ResidentialPickupPerspectiveApiModel> GetAsync(
            string requestedRoleCode,
            CancellationToken cancellationToken = default)
        {
            var route = string.Equals(
                requestedRoleCode,
                ResidentialPickupRoleCodes.Orderer,
                StringComparison.Ordinal)
                ? ResidentialPickupApiRoutes.Orderer
                : string.Equals(
                    requestedRoleCode,
                    ResidentialPickupRoleCodes.Transporter,
                    StringComparison.Ordinal)
                    ? ResidentialPickupApiRoutes.Transporter
                    : throw new InvalidOperationException("ResidentialPickupRoleUnsupported");
            var json = await transport.GetAsync(
                route,
                false,
                true,
                cancellationToken);
            var wire = JsonUtility.FromJson<ResidentialPickupPerspectiveWireModel>(json);
            return wire?.ToApiModel()
                ?? throw new InvalidOperationException("ResidentialPickupApiJsonInvalid");
        }
    }

    [Serializable]
    internal sealed class ResidentialPickupPerspectiveWireModel
    {
        public string stableId = string.Empty;
        public long revision;
        public string authorizedRoleCode = string.Empty;
        public string worldZoneCode = string.Empty;
        public string viewerScopeCode = string.Empty;
        public string sourceTypeCode = string.Empty;
        public string authorizationDecisionId = string.Empty;
        public string generatedAt = string.Empty;
        public ResidentialPickupPointWireModel[] pickupPoints =
            Array.Empty<ResidentialPickupPointWireModel>();

        public ResidentialPickupPerspectiveApiModel ToApiModel()
        {
            var points = new ResidentialPickupPointApiModel[pickupPoints?.Length ?? 0];
            for (var index = 0; index < points.Length; index++)
            {
                points[index] = pickupPoints[index].ToApiModel();
            }

            return new ResidentialPickupPerspectiveApiModel
            {
                StableId = stableId,
                Revision = revision,
                AuthorizedRoleCode = authorizedRoleCode,
                WorldZoneCode = worldZoneCode,
                ViewerScopeCode = viewerScopeCode,
                SourceTypeCode = sourceTypeCode,
                AuthorizationDecisionId = authorizationDecisionId,
                GeneratedAt = Parse(generatedAt),
                PickupPoints = points,
            };
        }

        internal static DateTimeOffset Parse(string value)
        {
            return DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal,
                out var parsed)
                ? parsed
                : throw new InvalidOperationException("ResidentialPickupTimestampInvalid");
        }
    }

    [Serializable]
    internal sealed class ResidentialPickupPointWireModel
    {
        public string stableId = string.Empty;
        public string canonicalTaskStableId = string.Empty;
        public string pickupPointLabel = string.Empty;
        public string productLabel = string.Empty;
        public int quantity;
        public string statusCode = string.Empty;
        public string roleLabel = string.Empty;
        public bool canInspect;
        public string updatedAt = string.Empty;

        public ResidentialPickupPointApiModel ToApiModel()
        {
            return new ResidentialPickupPointApiModel
            {
                StableId = stableId,
                CanonicalTaskStableId = canonicalTaskStableId,
                PickupPointLabel = pickupPointLabel,
                ProductLabel = productLabel,
                Quantity = quantity,
                StatusCode = statusCode,
                RoleLabel = roleLabel,
                CanInspect = canInspect,
                UpdatedAt = ResidentialPickupPerspectiveWireModel.Parse(updatedAt),
            };
        }
    }
}
