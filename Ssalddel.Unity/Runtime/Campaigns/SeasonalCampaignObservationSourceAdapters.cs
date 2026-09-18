using System;
using System.Globalization;
using System.Linq;
using Ssalddel.Unity.Cards;
using Ssalddel.Unity.Data.WorldProjection;
using Ssalddel.Unity.Observation;

namespace Ssalddel.Unity.Campaigns
{
    /// <summary>
    /// 기존 Interpreter 결과를 Coordinator의 판본·진단 입력으로만 변환합니다.
    /// 기존 payload를 복제하거나 새 권위 상태를 만들지 않습니다.
    /// </summary>
    public static class SeasonalCampaignObservationSourceAdapters
    {
        public static SeasonalCampaignObservationSourceUpdate FromDiorama(
            AdministrativeDongDioramaApplyResult result,
            long revisionOrdinal,
            DateTime observedAtUtc)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            return new SeasonalCampaignObservationSourceUpdate
            {
                SourceCode = SeasonalCampaignObservationSourceCodes.Diorama,
                Revision = result.Manifest?.ProjectionHashSha256
                    ?? string.Empty,
                RevisionOrdinal = revisionOrdinal,
                SuccessfulAtUtc = observedAtUtc,
                Succeeded = result.Accepted,
                ErrorCode = result.ErrorCode,
            };
        }

        public static SeasonalCampaignObservationSourceUpdate FromOperations(
            OperationalWorldSceneApplyResult result,
            DateTime observedAtUtc)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            return new SeasonalCampaignObservationSourceUpdate
            {
                SourceCode = SeasonalCampaignObservationSourceCodes.Operations,
                Revision = result.Cursor.ToString(
                    CultureInfo.InvariantCulture),
                RevisionOrdinal = result.Cursor,
                SuccessfulAtUtc = observedAtUtc,
                Succeeded = result.Accepted,
                ErrorCode = result.ErrorCode,
            };
        }

        public static SeasonalCampaignObservationSourceUpdate FromCards(
            CardWorkspaceSnapshot snapshot,
            DateTime observedAtUtc)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (!snapshot.PresentationOnly || snapshot.SourceRevisions == null
                || snapshot.SourceRevisions.Length == 0
                || snapshot.SourceRevisions.Any(value => value == null
                    || string.IsNullOrWhiteSpace(value.FamilyCode)
                    || value.SourceRevision < 0))
                return Failed(SeasonalCampaignObservationSourceCodes.Cards,
                    "CardWorkspaceRevisionUnavailable");

            var revisions = snapshot.SourceRevisions
                .OrderBy(value => value.FamilyCode, StringComparer.Ordinal)
                .ToArray();
            return new SeasonalCampaignObservationSourceUpdate
            {
                SourceCode = SeasonalCampaignObservationSourceCodes.Cards,
                Revision = string.Join("|", revisions.Select(value =>
                    value.FamilyCode + ":" + value.SourceRevision.ToString(
                        CultureInfo.InvariantCulture))),
                RevisionOrdinal = revisions.Max(value => value.SourceRevision),
                SuccessfulAtUtc = observedAtUtc,
                Succeeded = true,
            };
        }

        public static SeasonalCampaignObservationSourceUpdate FromNpc(
            동네관찰ScreenModel screenModel,
            DateTime observedAtUtc)
        {
            if (screenModel == null)
                throw new ArgumentNullException(nameof(screenModel));
            if (screenModel.Revision < 0)
                return Failed(SeasonalCampaignObservationSourceCodes.Npc,
                    "NpcObservationRevisionUnavailable");

            return new SeasonalCampaignObservationSourceUpdate
            {
                SourceCode = SeasonalCampaignObservationSourceCodes.Npc,
                Revision = screenModel.Revision.ToString(
                    CultureInfo.InvariantCulture),
                RevisionOrdinal = screenModel.Revision,
                SuccessfulAtUtc = observedAtUtc,
                Succeeded = true,
            };
        }

        private static SeasonalCampaignObservationSourceUpdate Failed(
            string sourceCode, string errorCode)
            => new SeasonalCampaignObservationSourceUpdate
            {
                SourceCode = sourceCode,
                Succeeded = false,
                ErrorCode = errorCode,
            };
    }
}
