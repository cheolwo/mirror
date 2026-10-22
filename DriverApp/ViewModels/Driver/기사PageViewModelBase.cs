using Ssalddel.Ui.Common.Areas.App.ViewModels;
using DriverApp.Services;
using Ssalddel.Contracts.Common.Workflow;
using Ssalddel.Contracts.Common.Versioning;

namespace DriverApp.ViewModels.Driver;

/// <summary>
/// 04 기사 화면의 페이지 수명과 업무 ViewModel 조립 경계를 통일합니다.
/// </summary>
public abstract class 기사PageViewModelBase : PageViewModelBase
{
    protected override Api작업오류 조회오류변환(Exception exception)
        => 정규화조회오류(exception);

    public static Api작업오류 정규화조회오류(Exception exception)
    {
        if (exception is not DriverApiException driver) return Api작업오류.변환(exception);
        var metadata = driver.RecoveryProblem;
        var status = (int)driver.StatusCode;
        return new Api작업오류(driver.ErrorCode ?? $"http-{status}", driver.Message, status,
            실패분류Code: metadata?.FailureClassCode,
            책임역할Code: metadata?.ResponsibilityRoleCode,
            상태전체재조회필요: metadata?.RequiresStateRefresh ?? false,
            재시도정책Code: metadata?.RetryPolicyCode,
            복구가능행동: metadata?.AvailableRecoveryActions,
            현재Revision: metadata?.CurrentRevision,
            재시도가능시각Utc: metadata?.RetryAfterUtc,
            WorkflowContext: metadata?.WorkflowContext);
    }

    protected static Api작업오류 완료오류변환(Exception exception, string stage)
    {
        var failure = 정규화조회오류(exception);
        return failure.WorkflowContext?.유효한가() == true
               && failure.WorkflowContext.OutcomeCode != 업무처리결과Codes.조회 ? failure
            : failure with { WorkflowContext = new 업무실패맥락Dto(OperatingSystemIds.DomesticCargoTransport,
                stage, 업무처리결과Codes.미확정) };
    }
}
