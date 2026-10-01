using System;
using System.Collections.Generic;
using System.Linq;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.WorkflowRules.Contracts;

namespace Ssalddel.Unity.Data.WorldProjection
{
    /// <summary>생활관찰표현Session의 E3 표현 정책에서 사용하는 관찰 단위이며 공간 H 단계가 아닙니다.</summary>
    public enum 생활관찰단계
    {
        District,
        Neighborhood,
        Street
    }

    /// <summary>
    /// 해석기가 승인한 전체 배치 결과 중 한 지역 음식 업무 사본과 표현 수명만 메모리에 보관합니다.
    /// 삭제·만료한 ID의 고수위 이력을 쌓지 않습니다. 외부 응답의 순서·삭제 판본 권위는
    /// 기존 Interpreter에 남으며 이 정책에 원본 응답이나 늦은 과거 계획을 직접 넣지 않습니다.
    /// </summary>
    [SsalddelEvidenceResponsibility(
        SsalddelEvidenceStage.E3,
        "한 지역 음식 상태 사본의 복제·집계와 거리·동·구 표현 수명을 순수 C#으로 관리한다.",
        Boundary = "서버·Simulation 권위, HTTP, 저장, Unity 객체 생성 또는 실제 화면 검증 책임이 아니다.")]
    public sealed class 생활관찰표현Session
    {
        private readonly string 지역고유식별자;
        private readonly double 안정대기초;
        private readonly double 해제유예초;
        private readonly int 최대객체수;
        private Dictionary<string, OperationalWorldPlacementInstruction> 현재사본 =
            new Dictionary<string, OperationalWorldPlacementInstruction>(StringComparer.Ordinal);
        private double? 마지막관찰초;
        private DateTime? 마지막기준시각;
        private double 요청시작초;
        private double? 비표시시작초;
        private string 자료진단 = string.Empty;
        private string 시계진단 = string.Empty;

        public 생활관찰표현Session(
            string areaStableId,
            double settleSeconds = .5,
            double releaseSeconds = 5,
            int maxObjects = 128)
        {
            if (string.IsNullOrWhiteSpace(areaStableId))
                throw new ArgumentException("AreaStableIdRequired", nameof(areaStableId));
            if (!유한비음수(settleSeconds))
                throw new ArgumentOutOfRangeException(nameof(settleSeconds));
            if (!유한비음수(releaseSeconds))
                throw new ArgumentOutOfRangeException(nameof(releaseSeconds));
            if (maxObjects <= 0) throw new ArgumentOutOfRangeException(nameof(maxObjects));
            지역고유식별자 = areaStableId;
            안정대기초 = settleSeconds;
            해제유예초 = releaseSeconds;
            최대객체수 = maxObjects;
        }

        public 생활관찰단계 CurrentStage { get; private set; } = 생활관찰단계.Neighborhood;
        public 생활관찰단계 RequestedStage { get; private set; } = 생활관찰단계.Neighborhood;
        public bool DetailVisible => CurrentStage == 생활관찰단계.Street && DetailRetained;
        public bool DetailRetained { get; private set; }
        public long RequestGeneration { get; private set; }
        public string DiagnosticCode => string.IsNullOrEmpty(시계진단) ? 자료진단 : 시계진단;

        // 반환 배열과 각 항목 모두 복제하므로 소비자의 수정이 다음 관찰이나 집계에 반영되지 않습니다.
        public IReadOnlyList<OperationalWorldPlacementInstruction> CurrentItems => 현재사본.Values
            .OrderBy(item => item.ObjectStableId, StringComparer.Ordinal)
            .Select(복제)
            .ToArray();

        public int WorkCount => 현재사본.Values
            .Select(item => (item.SourceKindCode, item.ScenarioRunStableId, item.WorkStableId))
            .Distinct()
            .Count();

        /// <summary>전체 현재 계획을 교체합니다. 잘못된 계획은 이전 상태를 보존하며 TTL은 Advance/Expire에서 정리합니다.</summary>
        public bool Apply(OperationalWorldPlacementPlan plan, DateTime utcNow)
        {
            if (!기준시각검사(utcNow)) return false;
            if (plan == null || !plan.Accepted) return 자료거부("ApplyResultRejected");
            if (plan.Instructions == null) return 자료거부("PlacementInstructionsRequired");

            var 다음사본 = new Dictionary<string, OperationalWorldPlacementInstruction>(StringComparer.Ordinal);
            var 입력식별자 = new HashSet<string>(StringComparer.Ordinal);
            var 낮은판본존재 = false;
            foreach (var item in plan.Instructions)
            {
                if (item == null) return 자료거부("PlacementInstructionRequired");
                if (!string.Equals(item.OperatingSystemId, OperationalWorldOperatingSystemIds.FoodDelivery, StringComparison.Ordinal)
                    || !string.Equals(item.AreaStableId, 지역고유식별자, StringComparison.Ordinal)) continue;

                var 진단 = 항목검사(item);
                if (!string.IsNullOrEmpty(진단)) return 자료거부(진단);
                if (!입력식별자.Add(item.ObjectStableId)) return 자료거부("DuplicateObjectStableId");
                // 해석된 한 지역 입력에도 같은 상한을 적용해 만료·오류 ID 검사 메모리를 제한합니다.
                if (입력식별자.Count > 최대객체수) return 자료거부("ObjectBudgetExceeded");

                현재사본.TryGetValue(item.ObjectStableId, out var 현재);
                if (현재 != null && item.Revision < 현재.Revision)
                {
                    낮은판본존재 = true;
                    // 과거 입력으로 표시 임대를 연장하거나 이미 만료된 사본을 되살리지 않습니다.
                    if (현재.ExpiresAtUtc > utcNow) 다음사본.Add(item.ObjectStableId, 현재);
                }
                else if (item.ExpiresAtUtc > utcNow)
                {
                    if (현재 != null && item.Revision == 현재.Revision && !동일내용(현재, item))
                        return 자료거부("RevisionConflict");
                    다음사본.Add(item.ObjectStableId, 복제(item));
                }

            }

            현재사본 = 다음사본;
            마지막기준시각 = utcNow;
            시계진단 = string.Empty;
            자료진단 = 낮은판본존재 ? "LowerRevision" : string.Empty;
            return true;
        }

        public void Clear()
        {
            현재사본.Clear();
            CurrentStage = RequestedStage = 생활관찰단계.Neighborhood;
            DetailRetained = false;
            RequestGeneration = 0;
            마지막관찰초 = null;
            마지막기준시각 = null;
            요청시작초 = 0;
            비표시시작초 = null;
            자료진단 = 시계진단 = string.Empty;
        }

        public void Expire(DateTime utcNow)
        {
            if (!기준시각검사(utcNow)) return;
            만료제거(utcNow);
            마지막기준시각 = utcNow;
            시계진단 = string.Empty;
        }

        /// <summary>같은 대기 요청의 반복은 안정 시간을 다시 시작하거나 세대를 늘리지 않습니다.</summary>
        public bool Request(생활관찰단계 stage, double monotonicSeconds)
        {
            if (stage != 생활관찰단계.District && stage != 생활관찰단계.Neighborhood && stage != 생활관찰단계.Street)
                return 시계거부("ObservationStageUnsupported");
            if (!관찰시계검사(monotonicSeconds)) return false;
            마지막관찰초 = monotonicSeconds;
            시계진단 = string.Empty;
            if (RequestedStage == stage) return true;
            RequestedStage = stage;
            요청시작초 = monotonicSeconds;
            // 현실적인 실행 동안 넘칠 수 없지만 wrap으로 과거 세대처럼 보이는 동작도 막습니다.
            if (RequestGeneration < long.MaxValue) RequestGeneration++;
            return true;
        }

        /// <summary>단조 시계는 표현 전환에만, UTC 시계는 승인 사본의 TTL에만 사용합니다.</summary>
        public bool Advance(double monotonicSeconds, DateTime utcNow)
        {
            if (!관찰시계검사(monotonicSeconds) || !기준시각검사(utcNow)) return false;
            마지막관찰초 = monotonicSeconds;
            마지막기준시각 = utcNow;
            시계진단 = string.Empty;
            만료제거(utcNow);

            if (CurrentStage != RequestedStage && monotonicSeconds - 요청시작초 >= 안정대기초)
            {
                var 거리이탈 = CurrentStage == 생활관찰단계.Street;
                CurrentStage = RequestedStage;
                if (CurrentStage == 생활관찰단계.Street)
                {
                    DetailRetained = true;
                    비표시시작초 = null;
                }
                else if (거리이탈)
                {
                    비표시시작초 = monotonicSeconds;
                }
            }

            if (CurrentStage != 생활관찰단계.Street && 비표시시작초.HasValue
                && monotonicSeconds - 비표시시작초.Value >= 해제유예초)
            {
                DetailRetained = false;
                비표시시작초 = null;
            }
            return true;
        }

        private void 만료제거(DateTime utcNow)
        {
            foreach (var id in 현재사본.Values.Where(item => item.ExpiresAtUtc <= utcNow)
                         .Select(item => item.ObjectStableId).ToArray()) 현재사본.Remove(id);
        }

        private bool 관찰시계검사(double seconds)
        {
            if (!유한비음수(seconds)) return 시계거부("MonotonicClockInvalid");
            if (마지막관찰초.HasValue && seconds < 마지막관찰초.Value)
                return 시계거부("MonotonicClockReversed");
            return true;
        }

        private bool 기준시각검사(DateTime utcNow)
        {
            if (utcNow.Kind != DateTimeKind.Utc) return 시계거부("UtcClockRequired");
            if (마지막기준시각.HasValue && utcNow < 마지막기준시각.Value)
                return 시계거부("UtcClockReversed");
            return true;
        }

        private static bool 유한비음수(double value)
            => !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0;

        private bool 시계거부(string code)
        {
            시계진단 = code;
            return false;
        }

        private bool 자료거부(string code)
        {
            시계진단 = string.Empty;
            자료진단 = code;
            return false;
        }

        private static string 항목검사(OperationalWorldPlacementInstruction item)
        {
            if (string.IsNullOrWhiteSpace(item.ObjectStableId)) return "ObjectStableIdRequired";
            if (string.IsNullOrWhiteSpace(item.WorkStableId)) return "WorkStableIdRequired";
            if (item.Revision < 0) return "RevisionInvalid";
            if (item.ExpiresAtUtc.Kind != DateTimeKind.Utc) return "ExpiresAtUtcRequired";
            if (item.SourceKindCode != OperationalWorldSceneSourceKinds.VerificationSample
                && item.SourceKindCode != OperationalWorldSceneSourceKinds.OperationalProjection)
                return "SourceKindUnsupported";
            if (item.SourceKindCode == OperationalWorldSceneSourceKinds.VerificationSample
                && string.IsNullOrWhiteSpace(item.ScenarioRunStableId)) return "ScenarioRunStableIdRequired";
            return string.Empty;
        }

        private static bool 동일내용(OperationalWorldPlacementInstruction a, OperationalWorldPlacementInstruction b)
            => a.AreaStableId == b.AreaStableId && a.OperatingSystemId == b.OperatingSystemId
               && a.AnchorKey == b.AnchorKey && a.VisualKey == b.VisualKey && a.ActivityCode == b.ActivityCode
               && a.WorkStableId == b.WorkStableId && a.LifecycleStageCode == b.LifecycleStageCode
               && a.AttentionStateCode == b.AttentionStateCode && a.ObjectKindCode == b.ObjectKindCode
               && a.SemanticPlaceStableId == b.SemanticPlaceStableId && a.SourceKindCode == b.SourceKindCode
               && a.ScenarioRunStableId == b.ScenarioRunStableId;

        private static OperationalWorldPlacementInstruction 복제(OperationalWorldPlacementInstruction item)
            => new OperationalWorldPlacementInstruction
            {
                ObjectStableId = item.ObjectStableId,
                AreaStableId = item.AreaStableId,
                OperatingSystemId = item.OperatingSystemId,
                AnchorKey = item.AnchorKey,
                VisualKey = item.VisualKey,
                ActivityCode = item.ActivityCode,
                WorkStableId = item.WorkStableId,
                LifecycleStageCode = item.LifecycleStageCode,
                AttentionStateCode = item.AttentionStateCode,
                ObjectKindCode = item.ObjectKindCode,
                SemanticPlaceStableId = item.SemanticPlaceStableId,
                SourceKindCode = item.SourceKindCode,
                ScenarioRunStableId = item.ScenarioRunStableId,
                Revision = item.Revision,
                ExpiresAtUtc = item.ExpiresAtUtc
            };
    }
}
