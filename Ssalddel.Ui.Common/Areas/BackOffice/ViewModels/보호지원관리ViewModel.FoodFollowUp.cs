using Ssalddel.Contracts.Admin.Food;
using Ssalddel.Contracts.Common.PrivacySupport;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.Ui.Common.Areas.BackOffice.ViewModels;

public sealed partial class 보호지원관리ViewModel
{
    private FoodSupportFollowUpRequest? _foodPending;
    private string? _foodPendingOrder;
    private bool _foodReadVerified;
    public FoodSupportFollowUpContext? FoodFollowUp { get; private set; }
    public bool HasFoodFollowUpPending => _foodPending is not null;
    public bool FoodFollowUpConfirmed { get; set; }
    public string FoodFollowUpAction { get; set; } = FoodSupportFollowUpActions.OperatorReview;
    public bool CanLoadFoodFollowUp => IsAuthenticated && !IsBusy && !HasPending && Detail is
        { Kind: 보호지원종류Codes.분쟁, SourceKind: 보호지원출처Codes.음식주문, RetentionPending: false }
        && Detail.StatusCode is "closed" or "result-prepared" && Detail.ResultNotifiedAtUtc.HasValue;
    public bool CanSubmitFoodFollowUp => CanLoadFoodFollowUp && FoodFollowUp is { ResultNoticeVerified: true } row
        && row.SupportCaseId == Detail!.CaseId && row.OrderNo == Detail.SourceId && row.CaseRevision == Detail.Revision
        && FoodFollowUpConfirmed && (FoodFollowUpAction == FoodSupportFollowUpActions.OperatorReview && row.CanRecordOperatorReview
            || FoodFollowUpAction == FoodSupportFollowUpActions.RetryDispatch && row.CanRetryDispatch);
    public bool CanRetryFoodFollowUp => IsAuthenticated && !IsBusy && HasFoodFollowUpPending && _foodReadVerified
        && FoodFollowUp is { Receipt: null } row && CanSendFood(row, _foodPending!);
    public bool CanReviewChangedFoodFollowUp => IsAuthenticated && !IsBusy && HasFoodFollowUpPending && _foodReadVerified
        && FoodFollowUp is { Receipt: null } row && !CanSendFood(row, _foodPending!);

    public async Task ReviewChangedFoodFollowUpAsync()
    {
        SynchronizeOwner();
        if (!CanReviewChangedFoodFollowUp) return;
        var caseId = _caseId;
        ClearFoodFollowUp();
        await LoadAsync(caseId);
    }

    public async Task LoadFoodFollowUpAsync()
    {
        SynchronizeOwner();
        if (!CanLoadFoodFollowUp) return;
        var orderNo = Detail!.SourceId; var caseId = Detail.CaseId;
        var op = Begin(); IsBusy = true; Error = Notice = null; FoodFollowUpConfirmed = false; Changed();
        try
        {
            var row = await client.음식후속조회Async(orderNo, caseId, null, _operation.Token);
            if (!Current(op)) return;
            ValidateFoodContext(row, orderNo, caseId);
            FoodFollowUp = row; FoodFollowUpAction = row.CanRetryDispatch ? FoodSupportFollowUpActions.RetryDispatch : FoodSupportFollowUpActions.OperatorReview;
        }
        catch (Exception ex) { if (Current(op)) { FoodFollowUp = null; ApplyError(ex); } }
        finally { if (Current(op)) { IsBusy = false; Changed(); } }
    }

    public async Task SubmitFoodFollowUpAsync()
    {
        SynchronizeOwner();
        if (!CanSubmitFoodFollowUp) return;
        _foodPendingOrder = FoodFollowUp!.OrderNo;
        _foodPending = new() { ClientRequestId = Guid.NewGuid(), SupportCaseId = FoodFollowUp.SupportCaseId,
            ExpectedCaseRevision = FoodFollowUp.CaseRevision, ExpectedOrderRevision = FoodFollowUp.OrderRevision, ActionCode = FoodFollowUpAction };
        await RunFoodFollowUpAsync(send: true);
    }
    public Task CheckFoodFollowUpAsync() => RunFoodFollowUpAsync(send: false);
    public Task RetryFoodFollowUpAsync() => CanRetryFoodFollowUp ? RunFoodFollowUpAsync(send: true) : Task.CompletedTask;

    private async Task RunFoodFollowUpAsync(bool send)
    {
        SynchronizeOwner();
        if (!IsAuthenticated || IsBusy || _foodPending is not { } request || _foodPendingOrder is not { } orderNo) return;
        var op = Begin(); IsBusy = true; Error = Notice = null; _foodReadVerified = false; Changed();
        try
        {
            var row = await client.음식후속조회Async(orderNo, request.SupportCaseId, request.ClientRequestId, _operation.Token);
            if (!Current(op)) return;
            if (ApplyFoodRead(row, orderNo, request)) return;
            if (!send || !CanSendFood(row, request)) return;
            var response = await client.음식후속처리Async(orderNo, request, _operation.Token);
            if (!Current(op)) return;
            ValidateFoodReceipt(response, orderNo, request);
            row = await client.음식후속조회Async(orderNo, request.SupportCaseId, request.ClientRequestId, _operation.Token);
            if (Current(op)) ApplyFoodRead(row, orderNo, request);
        }
        catch (Exception ex)
        {
            if (!Current(op)) return;
            _foodReadVerified = false;
            if (ex is SsalddelApiException { StatusCode: 401 or 403 }) ApplyError(ex);
            else if (ex is SsalddelApiException { StatusCode: 400 or 404 or 409 or 422 })
            { ClearFoodFollowUp(); RequiresRefresh = true; Error = "음식 후속 처리 조건이 변경되었습니다. 사건과 주문을 다시 조회해 주세요."; }
            else Error = "처리 결과가 확인되지 않았습니다. 같은 요청을 보존했으니 먼저 결과를 다시 확인하세요.";
        }
        finally { if (Current(op)) { IsBusy = false; Changed(); } }
    }
    private bool ApplyFoodRead(FoodSupportFollowUpContext row, string orderNo, FoodSupportFollowUpRequest request)
    {
        ValidateFoodContext(row, orderNo, request.SupportCaseId);
        FoodFollowUp = row; _foodReadVerified = true;
        if (row.Receipt is { } receipt)
        {
            ValidateFoodReceipt(receipt, orderNo, request);
            _foodPending = null; _foodPendingOrder = null; FoodFollowUpConfirmed = false;
            Notice = receipt.Message; return true;
        }
        Notice = CanSendFood(row, request) ? "이번 요청의 처리 기록은 아직 없습니다. 확인한 같은 요청으로 다시 진행할 수 있습니다."
            : "이번 요청의 처리 기록이 없고 사건 또는 주문 상태가 바뀌었습니다. 성공으로 처리하지 않았습니다. 최신 사건을 다시 확인하세요.";
        return false;
    }
    private static bool CanSendFood(FoodSupportFollowUpContext row, FoodSupportFollowUpRequest request)
        => row.Receipt is null && row.CaseRevision == request.ExpectedCaseRevision && row.OrderRevision == request.ExpectedOrderRevision
            && row.ResultNoticeVerified && (request.ActionCode == FoodSupportFollowUpActions.RetryDispatch ? row.CanRetryDispatch : row.CanRecordOperatorReview);
    private static void ValidateFoodContext(FoodSupportFollowUpContext row, string orderNo, string caseId)
    { if (row.OrderNo != orderNo || row.SupportCaseId != caseId) throw new InvalidOperationException("요청과 다른 음식 주문 응답입니다."); }
    private static void ValidateFoodReceipt(FoodSupportFollowUpResponse receipt, string orderNo, FoodSupportFollowUpRequest request)
    {
        if (receipt.OrderNo != orderNo || receipt.SupportCaseId != request.SupportCaseId || receipt.ClientRequestId != request.ClientRequestId
            || receipt.SupportCaseRevision != request.ExpectedCaseRevision || receipt.OrderRevision != request.ExpectedOrderRevision + 1
            || receipt.ActionCode != request.ActionCode) throw new InvalidOperationException("음식 후속 처리 기록을 확인할 수 없습니다.");
    }
    private void ClearFoodFollowUp()
    { FoodFollowUp = null; _foodPending = null; _foodPendingOrder = null; _foodReadVerified = false; FoodFollowUpConfirmed = false; }
}
