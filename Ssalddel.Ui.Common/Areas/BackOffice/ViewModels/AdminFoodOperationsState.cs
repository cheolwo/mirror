using System.Net;
using Ssalddel.Contracts.Admin.Food;
using Ssalddel.Contracts.Food;
using Ssalddel.Ui.Common.Areas.BackOffice.Services;

namespace Ssalddel.Ui.Common.Areas.BackOffice.ViewModels;

public interface IAdminFoodOperationsClient : IFoodOrderInterruptionReviewClient
{
    Task<AdminFoodOrderListDto> ListAsync(string query, int page, CancellationToken cancellationToken = default);
}

public sealed class AdminFoodWorkflowUnavailableException() : HttpRequestException(
    "현재 음식 운영을 사용할 수 없습니다. 다시 확인해 주세요.", null, HttpStatusCode.NotFound);

/// <summary>한 계정의 음식 목록/선택 상세 조회 수명만 소유하며 업무 명령은 실행하지 않습니다.</summary>
public sealed class AdminFoodOperationsState(IAdminFoodOperationsClient client, Func<string> authenticationIdentity) : IDisposable
{
    private CancellationTokenSource lifetime = new();
    private long generation;
    private bool disposed;
    private string identity = string.Empty;

    public AdminFoodOrderListDto? List { get; private set; }
    public 음식주문운영추적응답? Trace { get; private set; }
    public string Query { get; private set; } = string.Empty;
    public int Page { get; private set; } = 1;
    public string OrderNo { get; private set; } = string.Empty;
    public bool IsBusy { get; private set; }
    public bool RequiresRefresh { get; private set; } = true;
    public bool RequiresLogin { get; private set; }
    public bool Unavailable { get; private set; }
    public string? Message { get; private set; }
    public bool AuthenticationCurrent => !disposed && !string.IsNullOrWhiteSpace(identity) && identity == authenticationIdentity();
    public bool CanReview => AuthenticationCurrent && !IsBusy && !RequiresRefresh
        && Trace?.배달시도목록.Any(x => x.상태Code == 음식배달시도상태Code.중단) == true;

    public bool SynchronizeAuthentication()
    {
        var actor = authenticationIdentity();
        if (disposed || actor == identity) return false;
        Invalidate();
        identity = actor;
        List = null; Trace = null;
        RequiresLogin = string.IsNullOrWhiteSpace(actor);
        RequiresRefresh = true; Unavailable = false; Message = null;
        return true;
    }

    public Task RefreshListAsync(string query, int page = 1)
    {
        SynchronizeAuthentication();
        var clean = query.Trim();
        if (clean.Length > 100 || page < 1) { Message = "검색어와 페이지를 확인해 주세요."; return Task.CompletedTask; }
        if (Query != clean || Page != page || !string.IsNullOrEmpty(OrderNo))
        {
            Invalidate(); List = null; Trace = null; Query = clean; Page = page; OrderNo = string.Empty;
        }
        return ReadAsync(token => client.ListAsync(Query, Page, token), value =>
        {
            List = value; RequiresRefresh = false;
            if (value.Items.Count == 0) Message = "해당하는 음식 주문이 없습니다.";
        });
    }

    public Task RefreshTraceAsync(string orderNo)
    {
        SynchronizeAuthentication();
        var clean = orderNo.Trim();
        if (clean.Length == 0) { Message = "주문번호를 확인해 주세요."; return Task.CompletedTask; }
        if (OrderNo != clean) { Invalidate(); List = null; Trace = null; OrderNo = clean; }
        return ReadAsync(token => client.조회Async(OrderNo, token), value =>
        {
            if (value is null || value.주문번호 != OrderNo)
            {
                Trace = null; Message = "해당 음식 주문을 찾지 못했습니다."; return;
            }
            Trace = value; RequiresRefresh = false;
        });
    }

    private async Task ReadAsync<T>(Func<CancellationToken, Task<T>> read, Action<T> apply)
    {
        if (disposed || IsBusy || !AuthenticationCurrent)
        {
            if (!disposed && !AuthenticationCurrent) { List = null; Trace = null; RequiresLogin = true; }
            return;
        }
        IsBusy = true; RequiresRefresh = true; Unavailable = false; Message = null;
        var owner = generation; var token = lifetime.Token;
        try
        {
            var value = await read(token);
            if (IsCurrent(owner)) apply(value);
        }
        catch (Exception) when (!IsCurrent(owner) || token.IsCancellationRequested) { }
        catch (AdminFoodWorkflowUnavailableException)
        {
            List = null; Trace = null; Unavailable = true; Message = "현재 음식 운영을 사용할 수 없습니다. 다시 확인해 주세요.";
        }
        catch (HttpRequestException ex) when (ex.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            List = null; Trace = null; RequiresLogin = true; Message = "운영자 계정을 확인하고 다시 로그인해 주세요.";
        }
        catch
        {
            Message = "최신 음식 주문을 확인하지 못했습니다. 다시 조회해 주세요.";
        }
        finally { if (IsCurrent(owner)) IsBusy = false; }
    }

    private bool IsCurrent(long owner) => !disposed && generation == owner && AuthenticationCurrent;
    private void Invalidate()
    {
        generation++; lifetime.Cancel(); lifetime.Dispose(); lifetime = new(); IsBusy = false;
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true; generation++; lifetime.Cancel(); lifetime.Dispose();
    }

    public static string StageText(string code) => code switch
    {
        음식배달운영생명주기단계Codes.기사확보대기 => "기사 배정 대기",
        음식배달운영생명주기단계Codes.음식점응답대기 => "음식점 응답 대기",
        음식배달운영생명주기단계Codes.조리배차병행 => "조리·픽업 준비",
        음식배달운영생명주기단계Codes.픽업인계 => "픽업",
        음식배달운영생명주기단계Codes.배송 => "전달 중",
        음식배달운영생명주기단계Codes.수령확인대기 => "수령 확인 대기",
        음식배달운영생명주기단계Codes.종료 => "종료",
        _ => "진행 상태 확인 필요"
    };
    public static string ActorText(string code) => code switch
    {
        음식배달운영책임주체Codes.주문자 => "주문자",
        음식배달운영책임주체Codes.음식점 => "음식점",
        음식배달운영책임주체Codes.배차Engine => "기사 배정",
        음식배달운영책임주체Codes.음식배달기사 => "배달 기사",
        음식배달운영책임주체Codes.플랫폼운영자 => "운영자",
        _ => "담당 확인 필요"
    };
    public static string DateText(DateTime? value) => value is null || value == default(DateTime)
        ? "미확인" : DateTime.SpecifyKind(value.Value, DateTimeKind.Utc).AddHours(9).ToString("M/d HH:mm") + " (한국)";
}
