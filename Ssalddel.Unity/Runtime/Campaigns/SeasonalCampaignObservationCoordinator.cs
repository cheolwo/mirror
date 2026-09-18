using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Unity.Application;
using Ssalddel.Unity.Data.Campaigns;

namespace Ssalddel.Unity.Campaigns
{
    /// <summary>
    /// 기존 자료원 해석 결과의 판본과 실패 상태를 조합해 운영자 화면 모델을 만듭니다.
    /// 각 자료원의 payload와 권위는 기존 Interpreter가 유지하며 이 조정자는 전역
    /// revision을 만들거나 Campaign 구간을 전이하지 않습니다.
    /// </summary>
    [SsalddelEvidenceResponsibility(
        SsalddelEvidenceStage.E4,
        "절기 Campaign과 디오라마·운영·카드·NPC 자료원의 판본·지연 상태를 한 화면 문맥으로 조합한다.",
        Boundary = "읽기 전용 화면 모델 조합이며 원천 payload 변경·업무 완료·Campaign Confirm을 수행하지 않는다.")]
    public sealed class SeasonalCampaignObservationCoordinator
    {
        private readonly object gate = new object();
        private readonly Dictionary<string, SeasonalCampaignSourceDiagnostic>
            diagnostics = new Dictionary<string,
                SeasonalCampaignSourceDiagnostic>(StringComparer.Ordinal);
        private readonly SelectionStateStore? selectionState;
        private string sessionStableId = string.Empty;
        private string areaStableId = string.Empty;
        private SeasonalCampaignScreenModel screenModel =
            new SeasonalCampaignScreenModel();

        public SeasonalCampaignObservationCoordinator(
            SelectionStateStore? selectionStateStore = null)
        {
            selectionState = selectionStateStore;
        }

        public SeasonalCampaignObservationResult Compose(
            string sessionStableId,
            string areaStableId,
            Simulation절기운영CampaignApplyResult campaign,
            IEnumerable<SeasonalCampaignObservationSourceUpdate>? sourceUpdates,
            DateTime observedAtUtc)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            var session = Require(sessionStableId,
                "SeasonalCampaignObservationSessionRequired");
            var area = Require(areaStableId,
                "SeasonalCampaignObservationAreaRequired");
            if (observedAtUtc == default)
                throw new ArgumentException(
                    "SeasonalCampaignObservationTimeRequired",
                    nameof(observedAtUtc));
            var updates = (sourceUpdates ?? Array.Empty<
                SeasonalCampaignObservationSourceUpdate>()).ToArray();
            if (updates.Any(value => value == null)
                || updates.GroupBy(value => value.SourceCode,
                        StringComparer.Ordinal).Any(group => group.Count() > 1)
                || updates.Any(value => string.Equals(value.SourceCode,
                    SeasonalCampaignObservationSourceCodes.Campaign,
                    StringComparison.Ordinal)))
                return Rejected("SeasonalCampaignObservationSourceSetInvalid");

            lock (gate)
            {
                EnsureContext(session, area);
                if (campaign.State != null
                    && (!string.IsNullOrWhiteSpace(campaign.SessionStableId)
                        && !string.Equals(campaign.SessionStableId, session,
                            StringComparison.Ordinal)
                        || !string.IsNullOrWhiteSpace(campaign.AreaStableId)
                        && !string.Equals(campaign.AreaStableId, area,
                            StringComparison.Ordinal)))
                    return Rejected(
                        "SeasonalCampaignObservationContextMismatch");

                var campaignUpdate = CampaignUpdate(campaign, observedAtUtc);
                ApplySourceUpdate(campaignUpdate);
                foreach (var update in updates.OrderBy(value =>
                             value.SourceCode, StringComparer.Ordinal))
                    ApplySourceUpdate(update);

                screenModel = BuildScreenModel(session, area, campaign);
                return Snapshot(true, string.Empty);
            }
        }

        public SeasonalCampaignObservationResult Current()
        {
            lock (gate) return Snapshot(!string.IsNullOrWhiteSpace(
                sessionStableId), string.Empty);
        }

        public void Clear()
        {
            lock (gate) ClearCore();
        }

        private void EnsureContext(string session, string area)
        {
            if (string.IsNullOrWhiteSpace(sessionStableId))
            {
                sessionStableId = session;
                areaStableId = area;
                return;
            }
            if (string.Equals(sessionStableId, session, StringComparison.Ordinal)
                && string.Equals(areaStableId, area, StringComparison.Ordinal))
                return;
            ClearCore();
            sessionStableId = session;
            areaStableId = area;
        }

        private void ClearCore()
        {
            sessionStableId = string.Empty;
            areaStableId = string.Empty;
            diagnostics.Clear();
            screenModel = new SeasonalCampaignScreenModel();
            selectionState?.Clear();
        }

        private static SeasonalCampaignObservationSourceUpdate CampaignUpdate(
            Simulation절기운영CampaignApplyResult campaign,
            DateTime observedAtUtc)
            => new SeasonalCampaignObservationSourceUpdate
            {
                SourceCode = SeasonalCampaignObservationSourceCodes.Campaign,
                Revision = campaign.State?.CampaignRevision.ToString(
                    CultureInfo.InvariantCulture) ?? string.Empty,
                RevisionOrdinal = campaign.State?.CampaignRevision ?? 0,
                SuccessfulAtUtc = observedAtUtc,
                Succeeded = campaign.Accepted && campaign.State != null,
                ErrorCode = campaign.ErrorCode,
            };

        private void ApplySourceUpdate(
            SeasonalCampaignObservationSourceUpdate update)
        {
            var sourceCode = update.SourceCode?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(sourceCode))
            {
                diagnostics["Unknown"] = new SeasonalCampaignSourceDiagnostic
                {
                    SourceCode = "Unknown",
                    FreshnessCode =
                        SeasonalCampaignSourceFreshnessCodes.Missing,
                    ErrorCode = "SeasonalCampaignObservationSourceRequired",
                };
                return;
            }
            diagnostics.TryGetValue(sourceCode, out var existing);
            if (!update.Succeeded)
            {
                diagnostics[sourceCode] = new SeasonalCampaignSourceDiagnostic
                {
                    SourceCode = sourceCode,
                    LastSuccessfulRevision = existing?
                        .LastSuccessfulRevision ?? string.Empty,
                    LastSuccessfulRevisionOrdinal = existing?
                        .LastSuccessfulRevisionOrdinal ?? 0,
                    LastSuccessfulAtUtc = existing?
                        .LastSuccessfulAtUtc ?? default,
                    FreshnessCode = existing == null
                        ? SeasonalCampaignSourceFreshnessCodes.Missing
                        : SeasonalCampaignSourceFreshnessCodes.RefreshDelayed,
                    ErrorCode = string.IsNullOrWhiteSpace(update.ErrorCode)
                        ? "SeasonalCampaignObservationRefreshFailed"
                        : update.ErrorCode.Trim(),
                };
                return;
            }
            if (string.IsNullOrWhiteSpace(update.Revision)
                || update.RevisionOrdinal < 0
                || update.SuccessfulAtUtc == default)
            {
                ApplySourceUpdate(new SeasonalCampaignObservationSourceUpdate
                {
                    SourceCode = sourceCode,
                    ErrorCode = "SeasonalCampaignObservationRevisionInvalid",
                });
                return;
            }
            if (existing != null
                && update.RevisionOrdinal < existing
                    .LastSuccessfulRevisionOrdinal)
            {
                ApplySourceUpdate(new SeasonalCampaignObservationSourceUpdate
                {
                    SourceCode = sourceCode,
                    ErrorCode = "SeasonalCampaignObservationRevisionStale",
                });
                return;
            }
            if (existing != null
                && update.RevisionOrdinal == existing
                    .LastSuccessfulRevisionOrdinal
                && !string.Equals(update.Revision,
                    existing.LastSuccessfulRevision, StringComparison.Ordinal))
            {
                ApplySourceUpdate(new SeasonalCampaignObservationSourceUpdate
                {
                    SourceCode = sourceCode,
                    ErrorCode = "SeasonalCampaignObservationRevisionConflict",
                });
                return;
            }
            diagnostics[sourceCode] = new SeasonalCampaignSourceDiagnostic
            {
                SourceCode = sourceCode,
                LastSuccessfulRevision = update.Revision.Trim(),
                LastSuccessfulRevisionOrdinal = update.RevisionOrdinal,
                LastSuccessfulAtUtc = update.SuccessfulAtUtc.ToUniversalTime(),
                FreshnessCode = SeasonalCampaignSourceFreshnessCodes.Fresh,
            };
        }

        private SeasonalCampaignScreenModel BuildScreenModel(string session,
            string area, Simulation절기운영CampaignApplyResult campaign)
        {
            diagnostics.TryGetValue(
                SeasonalCampaignObservationSourceCodes.Campaign,
                out var campaignDiagnostic);
            var state = campaign.State;
            var campaignFresh = campaignDiagnostic != null
                && string.Equals(campaignDiagnostic.FreshnessCode,
                    SeasonalCampaignSourceFreshnessCodes.Fresh,
                    StringComparison.Ordinal);
            var requiredSources = RequiredObservationSources(state);
            var delayedRequiredSources = requiredSources.Where(source =>
            {
                diagnostics.TryGetValue(source, out var diagnostic);
                return diagnostic == null || !string.Equals(
                    diagnostic.FreshnessCode,
                    SeasonalCampaignSourceFreshnessCodes.Fresh,
                    StringComparison.Ordinal);
            }).ToArray();
            var actionsDisabled = state != null
                && (!campaignFresh || delayedRequiredSources.Length > 0);
            var disabledReasons = !campaignFresh && state != null
                ? new[] { "CampaignRefreshDelayed" }
                : delayedRequiredSources.Select(source =>
                    "RequiredSourceUnavailable:" + source).ToArray();
            return new SeasonalCampaignScreenModel
            {
                SessionStableId = session,
                AreaStableId = area,
                HasCampaign = state != null,
                CampaignStableId = state?.CampaignStableId ?? string.Empty,
                CampaignStateCode = state?.StateCode ?? string.Empty,
                CurrentPhaseCode = state?.CurrentPhaseCode ?? string.Empty,
                CampaignRevision = state?.CampaignRevision ?? 0,
                AvailableActions = campaignFresh && !actionsDisabled
                    ? (state?.AvailableActions ?? Array.Empty<string>())
                        .ToArray()
                    : Array.Empty<string>(),
                RequiredSourceCodes = requiredSources,
                DelayedRequiredSourceCodes = delayedRequiredSources,
                ActionsDisabled = actionsDisabled,
                DisabledActionReasons = disabledReasons,
                PresentationOnly = true,
            };
        }

        private static string[] RequiredObservationSources(
            Simulation절기운영CampaignStateSnapshot? state)
        {
            if (state == null) return Array.Empty<string>();
            var phase = (state.Phases ?? Array.Empty<
                    Simulation절기운영CampaignPhaseDefinition>())
                .FirstOrDefault(value => string.Equals(value.PhaseCode,
                    state.CurrentPhaseCode, StringComparison.Ordinal));
            return (phase?.RequiredSourceCodes ?? Array.Empty<string>())
                .Select(MapObservationSource)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
        }

        private static string MapObservationSource(string sourceCode)
            => sourceCode switch
            {
                Simulation절기운영CampaignSourceCodes
                    .AdministrativeDongDiorama =>
                    SeasonalCampaignObservationSourceCodes.Diorama,
                Simulation절기운영CampaignSourceCodes.OperationsScene =>
                    SeasonalCampaignObservationSourceCodes.Operations,
                Simulation절기운영CampaignSourceCodes.CardWorkspace =>
                    SeasonalCampaignObservationSourceCodes.Cards,
                Simulation절기운영CampaignSourceCodes.NpcObservation =>
                    SeasonalCampaignObservationSourceCodes.Npc,
                _ => "Unmapped:" + (sourceCode?.Trim() ?? string.Empty),
            };

        private SeasonalCampaignObservationResult Rejected(string errorCode)
        {
            lock (gate) return Snapshot(false, errorCode);
        }

        private SeasonalCampaignObservationResult Snapshot(bool accepted,
            string errorCode)
            => new SeasonalCampaignObservationResult
            {
                Accepted = accepted,
                ErrorCode = errorCode,
                ScreenModel = Clone(screenModel),
                SourceDiagnostics = diagnostics.Values
                    .OrderBy(value => value.SourceCode, StringComparer.Ordinal)
                    .Select(Clone).ToArray(),
                CompositionRevisionSet = diagnostics.Values
                    .Where(value => !string.IsNullOrWhiteSpace(
                        value.LastSuccessfulRevision))
                    .OrderBy(value => value.SourceCode, StringComparer.Ordinal)
                    .Select(value => new SeasonalCampaignCompositionRevision
                    {
                        SourceCode = value.SourceCode,
                        Revision = value.LastSuccessfulRevision,
                        RevisionOrdinal = value
                            .LastSuccessfulRevisionOrdinal,
                    }).ToArray(),
            };

        private static string Require(string value, string errorCode)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException(errorCode);
            return value.Trim();
        }

        private static SeasonalCampaignScreenModel Clone(
            SeasonalCampaignScreenModel source)
            => new SeasonalCampaignScreenModel
            {
                SessionStableId = source.SessionStableId,
                AreaStableId = source.AreaStableId,
                HasCampaign = source.HasCampaign,
                CampaignStableId = source.CampaignStableId,
                CampaignStateCode = source.CampaignStateCode,
                CurrentPhaseCode = source.CurrentPhaseCode,
                CampaignRevision = source.CampaignRevision,
                AvailableActions = source.AvailableActions.ToArray(),
                RequiredSourceCodes = source.RequiredSourceCodes.ToArray(),
                DelayedRequiredSourceCodes = source
                    .DelayedRequiredSourceCodes.ToArray(),
                ActionsDisabled = source.ActionsDisabled,
                DisabledActionReasons = source.DisabledActionReasons.ToArray(),
                PresentationOnly = source.PresentationOnly,
            };

        private static SeasonalCampaignSourceDiagnostic Clone(
            SeasonalCampaignSourceDiagnostic source)
            => new SeasonalCampaignSourceDiagnostic
            {
                SourceCode = source.SourceCode,
                LastSuccessfulRevision = source.LastSuccessfulRevision,
                LastSuccessfulRevisionOrdinal =
                    source.LastSuccessfulRevisionOrdinal,
                LastSuccessfulAtUtc = source.LastSuccessfulAtUtc,
                FreshnessCode = source.FreshnessCode,
                ErrorCode = source.ErrorCode,
            };
    }
}
