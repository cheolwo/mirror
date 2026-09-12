using System;
using System.Collections.Generic;

namespace Ssalddel.WorkflowRules.Contracts
{
    public static class 운영업무완료증명정책
    {
        public const string SchemaVersion = "operational-work-completion-proof.v1";
    }

    public sealed class 운영업무완료단계
    {
        public int Sequence { get; set; }
        public string StageCode { get; set; } = string.Empty;
        public DateTime OccurredAtUtc { get; set; }
    }

    public sealed class 운영업무완료증명
    {
        public string SchemaVersion { get; set; } = 운영업무완료증명정책.SchemaVersion;
        public string OperatingSystemId { get; set; } = string.Empty;
        public string WorkStableId { get; set; } = string.Empty;
        public long Revision { get; set; }
        public string OutcomeCode { get; set; } = string.Empty;
        public DateTime CompletedAtUtc { get; set; }
        public 운영업무완료단계[] Stages { get; set; } = Array.Empty<운영업무완료단계>();
    }

    public sealed class 운영업무완료프로필
    {
        public string OperatingSystemId { get; set; } = string.Empty;
        public string OutcomeCode { get; set; } = string.Empty;
        public string[] RequiredStageCodes { get; set; } = Array.Empty<string>();
    }

    /// <summary>
    /// DB, HTTP, Unity에 의존하지 않는 완료 증명 검사기입니다. 각 OS Adapter가 원장의 사실을
    /// 이 계약으로 옮기고, 모든 필수 단계가 순서대로 끝난 경우에만 표현 계층으로 넘깁니다.
    /// </summary>
    public static class 운영업무완료증명Validator
    {
        public static string[] Validate(운영업무완료증명 proof, 운영업무완료프로필 profile)
        {
            if (proof == null) throw new ArgumentNullException(nameof(proof));
            if (profile == null) throw new ArgumentNullException(nameof(profile));

            var errors = new List<string>();
            if (!string.Equals(proof.SchemaVersion, 운영업무완료증명정책.SchemaVersion, StringComparison.Ordinal))
                errors.Add("SchemaVersionUnsupported");
            if (!string.Equals(proof.OperatingSystemId, profile.OperatingSystemId, StringComparison.Ordinal))
                errors.Add("OperatingSystemMismatch");
            if (!string.Equals(proof.OutcomeCode, profile.OutcomeCode, StringComparison.Ordinal))
                errors.Add("OutcomeMismatch");
            if (string.IsNullOrWhiteSpace(proof.WorkStableId)) errors.Add("WorkStableIdRequired");
            if (proof.Revision < 1) errors.Add("RevisionInvalid");
            if (proof.CompletedAtUtc == default) errors.Add("CompletedAtRequired");

            var stages = proof.Stages ?? Array.Empty<운영업무완료단계>();
            var required = profile.RequiredStageCodes ?? Array.Empty<string>();
            if (stages.Length != required.Length)
            {
                errors.Add("RequiredStageCountMismatch");
                return errors.ToArray();
            }

            DateTime previous = default;
            for (var index = 0; index < required.Length; index++)
            {
                var stage = stages[index];
                if (stage.Sequence != index + 1) errors.Add("StageSequenceInvalid:" + required[index]);
                if (!string.Equals(stage.StageCode, required[index], StringComparison.Ordinal))
                    errors.Add("StageCodeMismatch:" + required[index]);
                if (stage.OccurredAtUtc == default) errors.Add("StageOccurredAtRequired:" + required[index]);
                if (previous != default && stage.OccurredAtUtc < previous)
                    errors.Add("StageOrderInvalid:" + required[index]);
                previous = stage.OccurredAtUtc;
            }

            if (stages.Length > 0 && proof.CompletedAtUtc < stages[stages.Length - 1].OccurredAtUtc)
                errors.Add("CompletionPrecedesFinalStage");
            return errors.ToArray();
        }
    }

    public static class OperationalWorldScenePolicy
    {
        public const string SchemaVersion = "operational-world-scene.v1";
        public const string OnlineEphemeral = "OnlineEphemeral";
        public const int RefreshAfterSeconds = 30;
    }

    public static class OperationalWorldSceneRoutes
    {
        public const string AreaSnapshot = "api/v1/world/areas/{areaStableId}/scene-snapshots";
    }

    public static class OperationalWorldSceneItemKinds
    {
        public const string CompletedLifecycle = "CompletedLifecycle";
        public const string WarehouseTask = "WarehouseTask";
        public const string WarehouseActor = "WarehouseActor";
        public const string CargoHandoff = "CargoHandoff";
    }

    /// <summary>
    /// 운영 지역 장면에서 Unity 관찰 모듈을 선택할 때 사용하는 안정 운영체제 식별자입니다.
    /// 서버 상태 권위를 부여하지 않으며 wire 값의 오탈자를 막는 공유 계약입니다.
    /// </summary>
    public static class OperationalWorldOperatingSystemIds
    {
        public const string FoodDelivery = "FoodDeliveryOS";
        public const string WarehouseCommerceFulfillment = "WarehouseCommerceFulfillmentOS";
        public const string DomesticCargoTransport = "DomesticCargoTransportOS";
        public const string SsalddelMartUrbanLogistics = "SsalddelMartUrbanLogisticsOS";
    }

    public sealed class OperationalWorldSceneItem
    {
        public string SnapshotStableId { get; set; } = string.Empty;
        public string AreaStableId { get; set; } = string.Empty;
        public string OperatingSystemId { get; set; } = string.Empty;
        public string ItemKind { get; set; } = string.Empty;
        public string RoleCode { get; set; } = string.Empty;
        public string ActivityCode { get; set; } = string.Empty;
        public long Revision { get; set; }
        public DateTime OccurredAtUtc { get; set; }
        public DateTime PublishedAtUtc { get; set; }
        public DateTime ExpiresAtUtc { get; set; }
        public bool IsTombstone { get; set; }
        public string DataPolicyCode { get; set; } = OperationalWorldScenePolicy.OnlineEphemeral;
        public bool LocalStorageAllowed { get; set; }
        public bool ReplayAllowed { get; set; }
        public string RepresentationDataJson { get; set; } = "{}";
    }

    public sealed class OperationalWorldSceneSourceFailure
    {
        public string SourceCode { get; set; } = string.Empty;
        public string ErrorCode { get; set; } = string.Empty;
        public bool Retryable { get; set; }
    }

    public sealed class OperationalWorldSceneResponse
    {
        public string SchemaVersion { get; set; } = OperationalWorldScenePolicy.SchemaVersion;
        public string AreaStableId { get; set; } = string.Empty;
        public long Cursor { get; set; }
        public bool IsFullSnapshot { get; set; } = true;
        public DateTime AsOfUtc { get; set; }
        public int RefreshAfterSeconds { get; set; } = OperationalWorldScenePolicy.RefreshAfterSeconds;
        public OperationalWorldSceneItem[] Items { get; set; } = Array.Empty<OperationalWorldSceneItem>();
        public OperationalWorldSceneSourceFailure[] SourceFailures { get; set; } = Array.Empty<OperationalWorldSceneSourceFailure>();
    }
}
