using System;

namespace Ssalddel.Unity.Campaigns
{
    public interface ISeasonalCampaignPresentationTarget
    {
        void ApplySeasonalCampaign(
            SeasonalCampaignScreenModel screenModel,
            SeasonalCampaignSourceDiagnostic[] sourceDiagnostics,
            SeasonalCampaignCompositionRevision[] compositionRevisionSet);
    }

    /// <summary>
    /// Coordinator의 읽기 전용 결과를 기존 World View Socket 구현에 전달할
    /// 최소 Presenter입니다. 버튼 Command나 GameObject 생성 책임은 갖지 않습니다.
    /// </summary>
    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E4,
        "절기 운영 Campaign 화면 모델과 원천별 진단을 View Socket 계약에 전달한다.",
        Boundary = "버튼 Command·GameObject 생성·Scene 배선·업무 완료 판정을 수행하지 않는다.")]
    public sealed class SeasonalCampaignPresenter
    {
        public void Present(SeasonalCampaignObservationResult result,
            ISeasonalCampaignPresentationTarget target)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (result.ScreenModel == null
                || !result.ScreenModel.PresentationOnly)
                throw new InvalidOperationException(
                    "SeasonalCampaignPresentationAuthorityInvalid");
            target.ApplySeasonalCampaign(result.ScreenModel,
                result.SourceDiagnostics ?? Array.Empty<
                    SeasonalCampaignSourceDiagnostic>(),
                result.CompositionRevisionSet ?? Array.Empty<
                    SeasonalCampaignCompositionRevision>());
        }
    }
}
