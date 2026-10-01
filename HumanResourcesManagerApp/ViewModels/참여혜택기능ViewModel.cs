using Hongdal.Contracts.Common.Hr;
using Hongdal.Ui.Common.Areas.App.Services;
using Hongdal.Ui.Common.Areas.App.ViewModels;

namespace HumanResourcesManagerApp.ViewModels;

public sealed class 참여혜택기능ViewModel : 인사업무ViewModelBase
{
    private const string BasePath = "api/v1/admin/hr-participation-benefits";

    public 참여혜택기능ViewModel(IHongdalJsonApiClient api)
        : base("participation-benefit", "참여 혜택", "참여에서 발생한 혜택을 조회하고 전환합니다.")
    {
        목록조회 = 하위ViewModel등록(new Api작업ViewModel<참여혜택조회조건, HrParticipationBenefitRecordListResponse?>(
            (condition, cancellationToken) => api.GetAsync<HrParticipationBenefitRecordListResponse>(
                인사Api경로.Query(BasePath,
                    ("userId", condition.UserId),
                    ("sourceType", condition.원천유형)),
                "참여 혜택 목록 조회",
                cancellationToken: cancellationToken)));
        전환 = 하위ViewModel등록(new Api작업ViewModel<HrParticipationBenefitTransferRequest, HrParticipationBenefitRecordResponse?>(
            (request, cancellationToken) => api.SendAsync<HrParticipationBenefitTransferRequest, HrParticipationBenefitRecordResponse>(
                HttpMethod.Post, $"{BasePath}/transfer", request, "참여 혜택 전환", cancellationToken: cancellationToken)));
    }

    public Api작업ViewModel<참여혜택조회조건, HrParticipationBenefitRecordListResponse?> 목록조회 { get; }
    public Api작업ViewModel<HrParticipationBenefitTransferRequest, HrParticipationBenefitRecordResponse?> 전환 { get; }
}

public sealed record 참여혜택조회조건(string? UserId, string? 원천유형);
