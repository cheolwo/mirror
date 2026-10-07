using System.Net;
using Ssalddel.Contracts.Admin.Food;
using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Contracts.Food;

namespace Ssalddel.Ui.Common.Areas.BackOffice.Services;

/// <summary>한 주문의 사람 검토 입력과 재조회 수명만 소유합니다. 배차·지급을 실행하지 않습니다.</summary>
public sealed class FoodDeliveryInterruptionReviewState(
    IFoodOrderInterruptionReviewClient client,
    Func<string> authenticationIdentity) : IDisposable
{
    private CancellationTokenSource lifetime = new();
    private long generation;
    private bool disposed;
    private string identity = string.Empty;
    private 음식배달중단검토요청? pendingRequest;
    private string pendingAttemptId = string.Empty;

    public string OrderNo { get; private set; } = string.Empty;
    public 음식주문운영추적응답? Trace { get; private set; }
    public string SelectedAttemptId { get; private set; } = string.Empty;
    public 음식배달시도운영응답? SelectedAttempt => Trace?.배달시도목록
        .FirstOrDefault(x => x.시도StableId == SelectedAttemptId);
    public string DecisionCode { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public bool AbuseConfirmed { get; set; }
    public bool Confirmed { get; set; }
    public bool IsBusy { get; private set; }
    public bool RequiresRefresh { get; private set; } = true;
    public bool RequiresLogin { get; private set; }
    public string? Message { get; private set; }
    public bool Saved { get; private set; }
    public bool AuthenticationCurrent => IsAuthenticatedCurrent();
    public bool CanSubmit => !disposed && !IsBusy && !RequiresRefresh && !RequiresLogin
        && IsAuthenticatedCurrent() && SelectedAttempt?.상태Code == 음식배달시도상태Code.중단
        && IsDecision(DecisionCode) && !string.IsNullOrWhiteSpace(Reason) && Reason.Trim().Length <= 1000
        && Confirmed
        && !(SelectedAttempt.책임Code == 운영배차책임Code.보호대상
             && DecisionCode == 음식배달중단검토판정Code.기사책임 && !AbuseConfirmed);

    public bool BindOrder(string orderNo)
    {
        var clean = orderNo.Trim();
        var currentIdentity = authenticationIdentity();
        if (disposed || (OrderNo == clean && identity == currentIdentity)) return false;
        Invalidate();
        OrderNo = clean;
        identity = currentIdentity;
        Trace = null;
        SelectedAttemptId = string.Empty;
        DecisionCode = Reason = string.Empty;
        AbuseConfirmed = Confirmed = Saved = false;
        RequiresRefresh = true;
        RequiresLogin = string.IsNullOrWhiteSpace(identity);
        Message = null;
        pendingRequest = null;
        pendingAttemptId = string.Empty;
        return true;
    }

    public void SelectAttempt(string attemptId)
    {
        if (IsBusy || disposed || !IsAuthenticatedCurrent()) return;
        if (SelectedAttemptId == attemptId) return;
        SelectedAttemptId = Trace?.배달시도목록.Any(x => x.시도StableId == attemptId) == true ? attemptId : string.Empty;
        DecisionCode = Reason = string.Empty;
        AbuseConfirmed = Confirmed = Saved = false;
        Message = null;
        pendingRequest = null;
        pendingAttemptId = string.Empty;
    }

    public async Task RefreshAsync()
    {
        if (disposed || IsBusy || RequiresLogin) return;
        if (!IsAuthenticatedCurrent()) { RequireAuthentication(); return; }
        IsBusy = true;
        var owner = generation;
        var token = lifetime.Token;
        try { await ReadCanonicalAsync(owner, token); }
        finally { if (IsCurrent(owner)) IsBusy = false; }
    }

    public async Task SubmitAsync()
    {
        if (!CanSubmit) return;
        var attempt = SelectedAttempt!;
        if (pendingRequest is null || pendingAttemptId != attempt.시도StableId
            || pendingRequest.판정Code != DecisionCode || pendingRequest.악용확정여부 != AbuseConfirmed
            || pendingRequest.판정사유 != Reason.Trim())
        {
            pendingAttemptId = attempt.시도StableId;
            pendingRequest = new()
            {
                클라이언트요청Id = Guid.NewGuid(), 예상Revision = attempt.Revision,
                판정Code = DecisionCode, 악용확정여부 = AbuseConfirmed, 판정사유 = Reason.Trim()
            };
        }

        IsBusy = true;
        Confirmed = Saved = false;
        RequiresRefresh = true;
        Message = null;
        var owner = generation;
        var token = lifetime.Token;
        var responseConfirmed = false;
        try
        {
            var response = await client.중단검토Async(pendingAttemptId, pendingRequest, token);
            if (!IsCurrent(owner)) return;
            if (response.시도StableId != pendingAttemptId)
                throw new InvalidOperationException("검토 대상과 다른 시도 응답입니다.");
            responseConfirmed = true;
            pendingRequest = null;
            await ReadCanonicalAsync(owner, token);
            if (IsCurrent(owner) && !RequiresRefresh && !RequiresLogin)
            {
                Saved = true;
                Message = "검토를 저장하고 최신 주문을 확인했습니다. 재배차와 지급은 별도 업무입니다.";
            }
        }
        catch (Exception) when (!IsCurrent(owner) || token.IsCancellationRequested) { }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            if (IsCurrent(owner)) RequireAuthentication();
        }
        catch (Exception ex)
        {
            if (!IsCurrent(owner)) return;
            var conflict = ex is HttpRequestException { StatusCode: HttpStatusCode.Conflict };
            if (conflict) pendingRequest = null;
            await ReadCanonicalAsync(owner, token);
            if (IsCurrent(owner) && !RequiresLogin)
                Message = RequiresRefresh
                    ? "검토 결과를 확인할 수 없습니다. 최신 상태 조회가 성공한 뒤에만 다시 검토할 수 있습니다."
                    : conflict
                        ? "다른 요청에서 변경되었거나 현재 판정 조건이 맞지 않습니다. 최신 시도와 근거를 확인하고 다시 선택해 주세요."
                        : "저장 응답을 확인할 수 없습니다. 최신 검토 상태를 확인한 뒤 같은 입력으로 재시도할 수 있습니다.";
        }
        finally
        {
            if (IsCurrent(owner))
            {
                IsBusy = false;
                if (responseConfirmed && RequiresRefresh && !RequiresLogin)
                    Message = "검토 저장 응답은 받았지만 최신 조회가 실패했습니다. 다시 조회해 주세요.";
            }
        }
    }

    private async Task ReadCanonicalAsync(long owner, CancellationToken token)
    {
        RequiresRefresh = true;
        try
        {
            var result = await client.조회Async(OrderNo, token);
            if (!IsCurrent(owner)) return;
            if (result is null || result.주문번호 != OrderNo)
            {
                Trace = null;
                SelectedAttemptId = string.Empty;
                Message = "해당 주문의 최신 시도를 찾지 못했습니다. 주문번호를 다시 확인해 주세요.";
                return;
            }
            var oldAttempt = SelectedAttempt;
            Trace = result;
            if (!Trace.배달시도목록.Any(x => x.시도StableId == SelectedAttemptId)) SelectedAttemptId = string.Empty;
            if (oldAttempt?.Revision != SelectedAttempt?.Revision) Confirmed = false;
            RequiresRefresh = false;
            Message = null;
        }
        catch (Exception) when (!IsCurrent(owner) || token.IsCancellationRequested) { }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            if (IsCurrent(owner)) RequireAuthentication();
        }
        catch
        {
            if (IsCurrent(owner)) Message = "최신 시도를 조회하지 못했습니다. 이전 정보로 검토를 저장할 수 없습니다. 다시 조회해 주세요.";
        }
    }

    private bool IsAuthenticatedCurrent()
        => !string.IsNullOrWhiteSpace(identity) && identity == authenticationIdentity();

    private bool IsCurrent(long owner) => !disposed && owner == generation && IsAuthenticatedCurrent();

    private void RequireAuthentication()
    {
        RequiresLogin = true;
        RequiresRefresh = true;
        Trace = null;
        SelectedAttemptId = string.Empty;
        Message = "로그인이 만료되었거나 계정이 변경되었습니다. 다시 로그인한 뒤 최신 주문을 조회해 주세요.";
    }

    private static bool IsDecision(string code) => code is 음식배달중단검토판정Code.보호
        or 음식배달중단검토판정Code.기사책임 or 음식배달중단검토판정Code.음식점책임 or 음식배달중단검토판정Code.플랫폼책임;

    private void Invalidate()
    {
        generation++;
        lifetime.Cancel();
        lifetime.Dispose();
        lifetime = new();
        IsBusy = false;
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        generation++;
        lifetime.Cancel();
        lifetime.Dispose();
    }
}
