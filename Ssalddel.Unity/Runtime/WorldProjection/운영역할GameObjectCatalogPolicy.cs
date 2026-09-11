using System;
using System.Collections.Generic;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Simulation.Contracts;

namespace Ssalddel.Unity.WorldProjection
{
    /// <summary>
    /// 운영 역할 기반 객체 원형 대장을 Unity 표현 준비와 실제 Scene 생성 허용으로 구분한다.
    /// 이 정책은 GameObject를 생성하지 않으며 운영 또는 Simulation 상태의 실행 권위를 갖지 않는다.
    /// </summary>
    [SsalddelEvidenceResponsibility(
        SsalddelEvidenceStage.E2,
        "운영 역할 객체 원형의 비식별·비권위 조건과 Scene 준비 관문을 판정한다.",
        Boundary = "대장 판정은 실제 Prefab·Scene 배선·Play Mode·Game View 증거가 아니다.")]
    public static class 운영역할GameObjectCatalogPolicy
    {
        public static IReadOnlyList<string> Validate(OperationalUnityTransferObjectCatalog catalog)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));

            var errors = new List<string>();
            if (!string.Equals(
                    catalog.SchemaVersion,
                    OperationalRoleObjectCatalogSchemas.V3,
                    StringComparison.Ordinal))
            {
                errors.Add("OperationalRoleObjectCatalogSchemaUnsupported");
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            var candidates = catalog.RoleObjectCandidates ?? Array.Empty<OperationalRoleObjectCandidate>();
            foreach (var candidate in candidates)
            {
                if (candidate == null || string.IsNullOrWhiteSpace(candidate.ObjectArchetypeId))
                {
                    errors.Add("OperationalRoleObjectIdentityMissing");
                    continue;
                }

                if (!ids.Add(candidate.ObjectArchetypeId))
                {
                    errors.Add("OperationalRoleObjectIdentityDuplicate:" + candidate.ObjectArchetypeId);
                }

                if (candidate.IsExecutionAuthority || candidate.ContainsPrivateData)
                {
                    errors.Add("OperationalRoleObjectAuthorityOrPrivacyLeak:" + candidate.ObjectArchetypeId);
                }

                if (!IsObjectKind(candidate.ObjectKindCode)
                    || !IsIdentityPolicy(candidate.IdentityPolicyCode)
                    || string.IsNullOrWhiteSpace(candidate.DisplayNameKo))
                {
                    errors.Add("OperationalRoleObjectShapeInvalid:" + candidate.ObjectArchetypeId);
                }

                if (string.Equals(
                        candidate.RepresentationDecisionCode,
                        OperationalRoleObjectRepresentationCodes.Candidate,
                        StringComparison.Ordinal))
                {
                    if (string.IsNullOrWhiteSpace(candidate.VisualKey)
                        || string.Equals(
                            candidate.IdentityPolicyCode,
                            OperationalRoleObjectIdentityPolicyCodes.NoIdentity,
                            StringComparison.Ordinal)
                        || string.Equals(
                            candidate.SpawnModeCode,
                            OperationalRoleObjectSpawnModeCodes.NotSpawnable,
                            StringComparison.Ordinal))
                    {
                        errors.Add("OperationalRoleObjectCandidateNotRepresentable:" + candidate.ObjectArchetypeId);
                    }
                }
                else if (!string.Equals(
                             candidate.RepresentationDecisionCode,
                             OperationalRoleObjectRepresentationCodes.NoUnityRepresentation,
                             StringComparison.Ordinal)
                         || !string.Equals(
                             candidate.SpawnModeCode,
                             OperationalRoleObjectSpawnModeCodes.NotSpawnable,
                             StringComparison.Ordinal))
                {
                    errors.Add("OperationalRoleObjectRepresentationDecisionInvalid:" + candidate.ObjectArchetypeId);
                }

                if (string.Equals(
                        candidate.RepresentationDecisionCode,
                        OperationalRoleObjectRepresentationCodes.NoUnityRepresentation,
                        StringComparison.Ordinal)
                    && (!string.Equals(
                            candidate.IdentityPolicyCode,
                            OperationalRoleObjectIdentityPolicyCodes.NoIdentity,
                            StringComparison.Ordinal)
                        || !string.IsNullOrWhiteSpace(candidate.VisualKey)
                        || (candidate.PresentationStateCodes?.Length ?? 0) > 0
                        || candidate.PrefabReady
                        || candidate.SceneReady))
                {
                    errors.Add("OperationalRoleObjectExcludedButReady:" + candidate.ObjectArchetypeId);
                }

                if (candidate.SceneReady && !candidate.PrefabReady)
                {
                    errors.Add("OperationalRoleObjectSceneReadyWithoutPrefab:" + candidate.ObjectArchetypeId);
                }
            }

            return errors;
        }

        public static bool CanPrepareRepresentation(OperationalRoleObjectCandidate candidate)
            => candidate != null
               && !candidate.IsExecutionAuthority
               && !candidate.ContainsPrivateData
               && string.Equals(
                   candidate.RepresentationDecisionCode,
                   OperationalRoleObjectRepresentationCodes.Candidate,
                   StringComparison.Ordinal)
               && !string.IsNullOrWhiteSpace(candidate.VisualKey)
               && !string.Equals(
                   candidate.SpawnModeCode,
                   OperationalRoleObjectSpawnModeCodes.NotSpawnable,
                   StringComparison.Ordinal);

        public static bool CanInstantiateInScene(OperationalRoleObjectCandidate candidate)
            => CanPrepareRepresentation(candidate)
               && candidate.PrefabReady
               && candidate.SceneReady;

        private static bool IsObjectKind(string value)
            => string.Equals(value, OperationalRoleObjectKindCodes.Actor, StringComparison.Ordinal)
               || string.Equals(value, OperationalRoleObjectKindCodes.Facility, StringComparison.Ordinal)
               || string.Equals(value, OperationalRoleObjectKindCodes.Vehicle, StringComparison.Ordinal)
               || string.Equals(value, OperationalRoleObjectKindCodes.WorkObject, StringComparison.Ordinal);

        private static bool IsIdentityPolicy(string value)
            => string.Equals(value, OperationalRoleObjectIdentityPolicyCodes.PseudonymousStableId, StringComparison.Ordinal)
               || string.Equals(value, OperationalRoleObjectIdentityPolicyCodes.AggregateStableId, StringComparison.Ordinal)
               || string.Equals(value, OperationalRoleObjectIdentityPolicyCodes.NoIdentity, StringComparison.Ordinal);
    }
}
