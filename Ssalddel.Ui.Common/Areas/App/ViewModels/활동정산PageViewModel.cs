using Ssalddel.Contracts.Common.Settlement;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.Ui.Common.Areas.App.ViewModels;

public sealed class 활동정산PageViewModel(I활동정산Client client, 활동정산Session session,
    ISsalddel현재사용자Context user) : NeighborhoodWorkflowViewModel(user)
{
    public IReadOnlyList<NeighborhoodSettlementSummaryDto> Items { get; private set; } = [];
    public NeighborhoodSettlementSummaryDto? Detail { get; private set; }
    public string? SelectedId { get; private set; }
    public bool Loaded { get; private set; }
    public bool ShowWeekly { get; private set; }
    public WorkerWeeklyStatementDto? Weekly { get; private set; }
    public string? ValidationError { get; private set; }
    public bool HasPending => session.Command is not null;
    public string? PendingAgreementId => session.AgreementId;
    public bool CanAct => IsAuthenticated && Loaded && !IsLoading && !IsSending && !HasPending && Error is null;
    public string? CurrentUserId => Owner;
    public bool MyConsentGranted => Detail is { } detail && detail.Agreement.Consents.Any(value => value.UserId == Owner
        && value.TermsVersion == detail.Agreement.TermsVersion && value.Granted);
    public bool AgreementAllows(string action) => CanAct && Detail?.Agreement.AllowedActions.Contains(action, StringComparer.Ordinal) == true;
    public bool EntryAllows(NeighborhoodSettlementEntryDto entry, string action) => CanAct && Detail is { } detail
        && detail.Entries.Contains(entry) && entry.AllowedActions.Contains(action, StringComparer.Ordinal);
    public bool PaymentAllows(NeighborhoodSettlementPaymentDto payment, string action) => CanAct && Detail is { } detail
        && detail.Payments.Contains(payment) && payment.PayeeUserId == Owner && payment.AllowedActions.Contains(action, StringComparer.Ordinal);
    public bool CanAccrueReward => AgreementAllows("accrue-activity-reward") && Detail is
        { ActivityReward: { StatusCode: NeighborhoodActivityRewardCodes.Ready, AlreadyAccrued: false } preview }
        && Detail.Agreement.Terms.ActivityRewardTerms?.PayerUserId == Owner && !string.IsNullOrWhiteSpace(preview.CalculationDigest);

    protected override void ResetForOwner()
    {
        session.Bind(Owner); Items = []; Detail = null; Weekly = null; ShowWeekly = false; SelectedId = null; Loaded = false; ValidationError = null;
    }
    public async Task LoadAsync(string? agreementId = null)
    {
        SynchronizeOwner();
        if (Disposed || IsSending || !CheckOwner()) return;
        SelectedId = agreementId; ShowWeekly = false; Weekly = null;
        var operation = Begin(); IsLoading = true; Loaded = false; Error = null; ValidationError = null; Detail = null; Items = []; Changed();
        try
        {
            if (agreementId is null)
            {
                var items = await client.MineAsync(Token);
                if (!Current(operation)) return;
                if (items.Any(value => !Valid(value)) || items.Select(value => value.Agreement.AgreementId).Distinct(StringComparer.Ordinal).Count() != items.Count)
                    throw new InvalidOperationException("정산 목록 연결 미확인");
                Items = items;
            }
            else
            {
                if (RoleWorkspaceNavigation.StableId(agreementId) is null) throw new InvalidOperationException("정산 식별값 미확인");
                var detail = await client.DetailAsync(agreementId, Token);
                if (!Current(operation)) return;
                if (detail is null) { Error = "본인에게 제공되는 정산 내역을 찾을 수 없습니다."; return; }
                if (!Valid(detail, agreementId)) throw new InvalidOperationException("정산 상세 연결 미확인");
                Detail = detail;
            }
            Loaded = true;
        }
        catch (Exception ex) { if (Current(operation)) ApplyError(ex); }
        finally { if (Current(operation)) { IsLoading = false; Changed(); } }
    }
    public async Task ConsentAsync(bool granted)
    {
        if (!CheckOwner() || Detail is not { } detail || !AgreementAllows(granted ? "consent" : "revoke")) return;
        if (granted == MyConsentGranted) return;
        await ExecuteAsync(detail.Agreement.AgreementId, "Consent", new NeighborhoodSettlementConsentRequest
        { RequestId = Guid.NewGuid(), ExpectedRevision = detail.Agreement.Revision, TermsVersion = detail.Agreement.TermsVersion, Granted = granted });
    }
    public async Task ConfirmAsync(string entryId)
    {
        if (!CheckOwner() || Detail is not { } detail || detail.Entries.FirstOrDefault(value => value.EntryId == entryId) is not { } entry
            || !EntryAllows(entry, "confirm-entry") || entry.ConfirmationUserIds.Contains(Owner!, StringComparer.Ordinal)) return;
        await ExecuteAsync(detail.Agreement.AgreementId, "ConfirmEntry", new NeighborhoodSettlementEntryConfirmRequest
        { RequestId = Guid.NewGuid(), ExpectedRevision = detail.Agreement.Revision, EntryId = entryId });
    }
    public async Task DisputeAsync(string entryId, string reason)
    {
        if (!CheckOwner() || Detail is not { } detail || detail.Entries.FirstOrDefault(value => value.EntryId == entryId) is not { } entry
            || !EntryAllows(entry, "dispute")) return;
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 1000)
        { ValidationError = "이의 사유를 1~1,000자로 입력해 주세요."; Changed(); return; }
        await ExecuteAsync(detail.Agreement.AgreementId, "Dispute", new NeighborhoodSettlementDisputeRequest
        { RequestId = Guid.NewGuid(), ExpectedRevision = detail.Agreement.Revision, EntryId = entryId, Reason = reason.Trim() });
    }
    public async Task ReceiveAsync(string paymentId, decimal? amount, string? reason)
    {
        if (!CheckOwner() || Detail is not { } detail || detail.Payments.FirstOrDefault(value => value.PaymentId == paymentId) is not { } payment
            || !PaymentAllows(payment, "confirm-receipt")) return;
        if (amount is null || amount < 0 || amount <= payment.RecipientConfirmedAmountKrw || amount > payment.AmountKrw
            || decimal.Round(amount.Value, 2) != amount || string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 1000)
        { ValidationError = "기존 확인액보다 크고 지급 신고액을 넘지 않는 누적 수령액과 사유를 입력해 주세요."; Changed(); return; }
        await ExecuteAsync(detail.Agreement.AgreementId, "ConfirmReceipt", new NeighborhoodSettlementPaymentReceiptRequest
        { RequestId = Guid.NewGuid(), ExpectedRevision = detail.Agreement.Revision, PaymentId = paymentId,
            ConfirmedAmountKrw = amount.Value, Reason = reason?.Trim() ?? "" });
    }
    public bool CanReportPayment(NeighborhoodSettlementEntryDto entry) => EntryAllows(entry, "report-payment")
        && (entry.AmountKrw < 0 ? entry.PayeeUserId : entry.PayerUserId) == Owner && entry.ReportablePaymentAmountKrw is > 0;
    public async Task ReportPaymentAsync(string entryId, decimal? amount, string? evidence, bool acknowledged)
    {
        if (!CheckOwner() || Detail is not { } detail || detail.Entries.FirstOrDefault(x => x.EntryId == entryId) is not { } entry
            || !CanReportPayment(entry)) return;
        if (!acknowledged || amount is null or <= 0 || amount > entry.ReportablePaymentAmountKrw
            || decimal.Round(amount.Value, 2) != amount || string.IsNullOrWhiteSpace(evidence) || evidence.Trim().Length > 1000)
        { ValidationError = "실제로 지급한 금액을 미신고 잔액 이내로 입력하고 지급 진술을 확인해 주세요."; Changed(); return; }
        await ExecuteAsync(detail.Agreement.AgreementId, "ReportPayment", new NeighborhoodSettlementPaymentReportRequest
        {
            RequestId = Guid.NewGuid(), ExpectedRevision = detail.Agreement.Revision, AmountKrw = amount.Value,
            Allocations = [new() { EntryId = entryId, AmountKrw = amount.Value }], EvidenceReference = evidence.Trim()
        });
    }
    public async Task LoadWeeklyAsync(DateOnly? completionDateKst = null)
    {
        SynchronizeOwner(); if (Disposed || IsSending || !CheckOwner()) return;
        var operation = Begin(); ShowWeekly = true; IsLoading = true; Loaded = false; Weekly = null;
        Error = null; Notice = null; Detail = null; Items = []; ValidationError = null; Changed();
        try
        {
            var value = await client.WeeklyAsync(completionDateKst, Token);
            if (!Current(operation)) return;
            if (value is null || value.WorkerUserId != Owner || value.PeriodEndUtc - value.PeriodStartUtc != TimeSpan.FromDays(7)
                || value.PeriodBasisCode != "WorkCompletedAt" || value.CompletionWeekStartKst.DayOfWeek != DayOfWeek.Wednesday
                || DateOnly.FromDateTime(value.PeriodStartUtc.AddHours(9)) != value.CompletionWeekStartKst
                || value.IsActualBankVerified || value.ActualBankVerifiedAmountKrw is not null
                || value.Lines.Any(x => x.CompletedAtUtc < value.PeriodStartUtc || x.CompletedAtUtc >= value.PeriodEndUtc || x.IsActualBankVerified)
                || value.Lines.Select(x => x.StableId).Distinct(StringComparer.Ordinal).Count() != value.Lines.Count)
                throw new InvalidOperationException("본인 주간 명세의 원천·기간 연결을 확인하지 못했습니다.");
            Weekly = value; Loaded = true;
        }
        catch (Exception ex) { if (Current(operation)) ApplyError(ex); }
        finally { if (Current(operation)) { IsLoading = false; Changed(); } }
    }
    public async Task AccrueRewardAsync(bool acknowledged)
    {
        if (!CheckOwner() || !CanAccrueReward || !acknowledged || Detail is not { ActivityReward: { } preview } detail) return;
        await ExecuteAsync(detail.Agreement.AgreementId, "AccrueActivityReward", new NeighborhoodSettlementActivityRewardAccrualRequest
        { RequestId = Guid.NewGuid(), ExpectedRevision = detail.Agreement.Revision, ExpectedCalculationDigest = preview.CalculationDigest });
    }
    private async Task ExecuteAsync(string id, string kind, NeighborhoodSettlementCommand command)
    {
        if (!CanAct) return;
        session.Prepare(id, kind, command);
        var operation = Begin(); IsSending = true; Error = null; ValidationError = null; Notice = null; Changed();
        try
        {
            var receipt = command switch
            {
                NeighborhoodSettlementConsentRequest request => await client.ConsentAsync(id, request, Token),
                NeighborhoodSettlementEntryConfirmRequest request => await client.ConfirmAsync(id, request, Token),
                NeighborhoodSettlementDisputeRequest request => await client.DisputeAsync(id, request, Token),
                NeighborhoodSettlementPaymentReceiptRequest request => await client.ReceiveAsync(id, request, Token),
                NeighborhoodSettlementActivityRewardAccrualRequest request => await client.AccrueRewardAsync(id, request, Token),
                NeighborhoodSettlementPaymentReportRequest request => await client.ReportPaymentAsync(id, request, Token),
                _ => null
            };
            if (!Current(operation)) return;
            await CompleteAsync(receipt, operation);
        }
        catch (Exception ex)
        {
            if (Current(operation)) { if (DefinitiveRejection(ex)) session.Clear(); ApplyError(ex, true); }
        }
        finally { if (Current(operation)) { IsSending = false; Changed(); } }
    }
    /// <summary>미확인 POST를 재전송하지 않습니다. 요청 영수증과 같은 약정의 최신 원장만 조회합니다.</summary>
    public async Task RecoverAsync()
    {
        if (IsLoading || IsSending || !CheckOwner() || session.Command is not { } command) return;
        var operation = Begin(); IsSending = true; Error = null; Notice = null; Changed();
        try
        {
            var receipt = await client.ReceiptAsync(command.RequestId, Token);
            if (!Current(operation)) return;
            if (receipt is null) { Notice = "이전 요청의 처리 기록이 아직 확인되지 않았습니다. 새 요청을 보내기 전에 결과를 다시 확인해 주세요."; return; }
            await CompleteAsync(receipt, operation);
        }
        catch (Exception ex) { if (Current(operation)) ApplyError(ex); }
        finally { if (Current(operation)) { IsSending = false; Changed(); } }
    }
    private async Task CompleteAsync(NeighborhoodSettlementReceiptDto? receipt, (long Generation, string? Owner) operation)
    {
        if (receipt is null || session.Command is not { } command || receipt.RequestId != command.RequestId
            || receipt.AgreementId != session.AgreementId || receipt.Kind != session.Kind || receipt.Revision < command.ExpectedRevision)
            throw new InvalidOperationException("정산 요청 결과 연결 미확인");
        var latest = await client.DetailAsync(receipt.AgreementId, Token);
        if (!Current(operation)) return;
        if (latest is null || !Valid(latest, receipt.AgreementId) || latest.Agreement.Revision < receipt.Revision)
            throw new InvalidOperationException("정산 정본 재조회 미확인");
        session.Clear(); SelectedId = receipt.AgreementId; ShowWeekly = false; Weekly = null; Detail = latest; Items = []; Loaded = true;
        Notice = "요청 처리 기록과 현재 정산 내역을 확인했습니다. 지급 신고와 수령 확인은 별도 상태입니다.";
    }
    private static bool Valid(NeighborhoodSettlementSummaryDto value, string? id = null)
        => value.Agreement is { } agreement && RoleWorkspaceNavigation.StableId(agreement.AgreementId) is not null
            && (id is null || agreement.AgreementId == id) && agreement.Revision >= 0
            && value.Entries.All(entry => entry.AgreementId == agreement.AgreementId)
            && value.Payments.All(payment => payment.AgreementId == agreement.AgreementId);
}
