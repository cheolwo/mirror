using System.Net;
using Ssalddel.Contracts.Admin.Food;

namespace SsalddelAdmin.Services;

/// <summary>선택 주문의 추적 조회와 조회 실패 상태만 소유합니다.</summary>
internal sealed class 음식주문운영추적ReadSession(
    Func<string, CancellationToken, Task<음식주문운영추적응답?>> read)
{
    public string OrderNo { get; private set; } = string.Empty;
    public 음식주문운영추적응답? Trace { get; private set; }
    public bool IsLoading { get; private set; }
    public bool NotFound { get; private set; }
    public string? ErrorMessage { get; private set; }
    public bool RequiresLogin { get; private set; }

    public void ChangeOrderNo(string value)
    {
        OrderNo = value;
        Trace = null;
        ErrorMessage = null;
        NotFound = false;
    }

    public void RequireLogin()
    {
        RequiresLogin = true;
        Trace = null;
    }

    public void InvalidatePending() => IsLoading = false;

    public async Task LoadAsync(
        string requestedOrderNo,
        Action<음식주문운영추적응답?> onLoaded,
        Func<bool> isCurrent,
        Func<bool> ownsPendingState,
        CancellationToken cancellationToken)
    {
        RequiresLogin = false;
        IsLoading = true;
        NotFound = false;
        ErrorMessage = null;
        Trace = null;
        try
        {
            var result = await read(requestedOrderNo, cancellationToken);
            if (!isCurrent()) return;
            if (result is not null && result.주문번호 != requestedOrderNo)
                throw new InvalidOperationException("조회한 주문번호가 요청과 다릅니다.");
            Trace = result;
            NotFound = Trace is null;
            onLoaded(Trace);
        }
        catch (Exception) when (!isCurrent() || cancellationToken.IsCancellationRequested) { }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            RequireLogin();
            ErrorMessage = "로그인이 만료되었습니다. 다시 로그인해 주세요.";
        }
        catch (ArgumentException ex)
        {
            ErrorMessage = ex.Message;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"운영 추적 정보를 불러오지 못했습니다: {ex.Message}";
        }
        finally
        {
            if (ownsPendingState()) IsLoading = false;
        }
    }
}
