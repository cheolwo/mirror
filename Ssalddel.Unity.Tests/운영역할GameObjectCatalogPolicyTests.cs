using Ssalddel.Simulation.Contracts;
using Ssalddel.Unity.WorldProjection;

namespace Ssalddel.Unity.Tests;

[Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
    Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E3,
    "운영 역할 객체 대장의 비식별·비권위·Scene 생성 관문을 자동 검증한다.",
    Boundary = "Catalog 후보와 실제 Prefab·Scene·Play Mode 증거를 구분한다.")]
public sealed class 운영역할GameObjectCatalogPolicyTests
{
    [Fact]
    public void 안전한_후보는_표현준비만_가능하고_SceneReady전에는_생성할수없다()
    {
        var candidate = ValidCandidate();
        var catalog = ValidCatalog(candidate);

        var errors = 운영역할GameObjectCatalogPolicy.Validate(catalog);

        Assert.Empty(errors);
        Assert.True(운영역할GameObjectCatalogPolicy.CanPrepareRepresentation(candidate));
        Assert.False(운영역할GameObjectCatalogPolicy.CanInstantiateInScene(candidate));
    }

    [Fact]
    public void 운영권위나_개인정보가_있는_후보는_거부한다()
    {
        var candidate = ValidCandidate();
        candidate.IsExecutionAuthority = true;
        candidate.ContainsPrivateData = true;

        var errors = 운영역할GameObjectCatalogPolicy.Validate(ValidCatalog(candidate));

        Assert.Contains(errors, error => error.StartsWith(
            "OperationalRoleObjectAuthorityOrPrivacyLeak:",
            StringComparison.Ordinal));
        Assert.False(운영역할GameObjectCatalogPolicy.CanPrepareRepresentation(candidate));
    }

    [Fact]
    public void Prefab준비없이_SceneReady인_후보는_거부한다()
    {
        var candidate = ValidCandidate();
        candidate.SceneReady = true;

        var errors = 운영역할GameObjectCatalogPolicy.Validate(ValidCatalog(candidate));

        Assert.Contains(errors, error => error.StartsWith(
            "OperationalRoleObjectSceneReadyWithoutPrefab:",
            StringComparison.Ordinal));
        Assert.False(운영역할GameObjectCatalogPolicy.CanInstantiateInScene(candidate));
    }

    [Fact]
    public void Unity표현제외_역할은_생성할수없다()
    {
        var candidate = ValidCandidate();
        candidate.RepresentationDecisionCode = OperationalRoleObjectRepresentationCodes.NoUnityRepresentation;
        candidate.SpawnModeCode = OperationalRoleObjectSpawnModeCodes.NotSpawnable;
        candidate.IdentityPolicyCode = OperationalRoleObjectIdentityPolicyCodes.NoIdentity;
        candidate.PresentationStateCodes = Array.Empty<string>();
        candidate.VisualKey = string.Empty;

        var errors = 운영역할GameObjectCatalogPolicy.Validate(ValidCatalog(candidate));

        Assert.Empty(errors);
        Assert.False(운영역할GameObjectCatalogPolicy.CanPrepareRepresentation(candidate));
        Assert.False(운영역할GameObjectCatalogPolicy.CanInstantiateInScene(candidate));
    }

    private static OperationalUnityTransferObjectCatalog ValidCatalog(OperationalRoleObjectCandidate candidate)
        => new()
        {
            SchemaVersion = OperationalRoleObjectCatalogSchemas.V3,
            Revision = "operations-unity-transfer.r3",
            RoleObjectCandidates = new[] { candidate }
        };

    private static OperationalRoleObjectCandidate ValidCandidate()
        => new()
        {
            ObjectArchetypeId = "operational-object:actor:food-courier.v1",
            DisplayNameKo = "음식 배달 기사",
            ObjectKindCode = OperationalRoleObjectKindCodes.Actor,
            SourceActorCodes = new[] { "FoodDeliveryDriver" },
            OperatingSystemIds = new[] { "FoodDeliveryOS" },
            WorkflowCodes = new[] { "FoodDelivery" },
            ObservationRoleCode = "FoodCourier",
            VisualKey = "visual:actor:food-courier",
            RepresentationDecisionCode = OperationalRoleObjectRepresentationCodes.Candidate,
            SpawnModeCode = OperationalRoleObjectSpawnModeCodes.SimulationAnalog,
            IdentityPolicyCode = OperationalRoleObjectIdentityPolicyCodes.PseudonymousStableId,
            IsExecutionAuthority = false,
            ContainsPrivateData = false,
            PrefabReady = false,
            SceneReady = false,
            Boundary = "가상 역할만 표현"
        };
}
