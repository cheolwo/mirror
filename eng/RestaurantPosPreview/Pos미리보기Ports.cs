using RestaurantDeskApp.Models.Restaurant;
using RestaurantDeskApp.Services;
using Ssalddel.Contracts.Food;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace RestaurantDeskApp;

public sealed class Pos미리보기Desk(Pos미리보기자료 data) : I음식점주문DeskService
{
    private readonly 음식점전표DraftFactory slips = new();
    private static Task Delay(CancellationToken token) => Task.Delay(120, token);
    private void EnsureRead()
    { if (data.ReadFailure) throw new HttpRequestException("화면 검증용 조회 실패"); }

    public async Task<IReadOnlyList<음식점주문DeskItem>> 주문목록조회Async(음식점주문복구출처 복구출처 = 음식점주문복구출처.서버재조회, CancellationToken cancellationToken = default)
    { await Delay(cancellationToken); EnsureRead(); return data.목록(); }
    public async Task<음식점주문DeskItem?> 주문조회Async(string 주문번호, 음식점주문복구출처 복구출처 = 음식점주문복구출처.서버재조회, CancellationToken cancellationToken = default)
    { await Delay(cancellationToken); EnsureRead(); return data.조회(주문번호); }
    public Task<음식점주문DeskItem> 주문알림수신Async(음식점주문수신Payload payload, CancellationToken cancellationToken = default)
        => Task.FromResult(data.조회(payload.주문번호) ?? throw new InvalidOperationException("합성 주문 없음"));
    public Task<음식점주문수락결과> 주문수락후전표준비Async(string 주문번호, CancellationToken cancellationToken = default)
        => 주문수락후전표준비Async(주문번호, 20, cancellationToken);
    public async Task<음식점주문수락결과> 주문수락후전표준비Async(string 주문번호, int 조리예상분, CancellationToken cancellationToken = default)
    {
        await Delay(cancellationToken); EnsureRead();
        var result = data.Mutate(주문번호, 음식배달가능행동Ids.음식점주문수락, order =>
        {
            order.상태 = 음식주문상태코드.주문확인; order.미확인 = false;
            order.선택조리예상분 = 음식점조리시간정책.Clamp(조리예상분);
            order.수락시각 = DateTimeOffset.UtcNow; order.배차상태 = 음식주문배차상태코드.배차대기;
            order.배차요청시각Utc = DateTime.UtcNow;
            order.상세주문!.음식점수락시각Utc = DateTime.UtcNow;
            order.상세주문.조리예상분 = order.선택조리예상분;
        });
        return Result(result);
    }
    public async Task<음식점주문DeskItem?> 주문거절Async(string 주문번호, string 사유, CancellationToken cancellationToken = default)
    {
        await Delay(cancellationToken); EnsureRead();
        return data.Mutate(주문번호, 음식배달가능행동Ids.음식점주문거절, order =>
        { order.상태 = 음식주문상태코드.거절; order.미확인 = false; order.최근메시지 = 사유; });
    }
    public async Task<음식점주문DeskItem?> 조리시작Async(string 주문번호, int 조리예상분, CancellationToken cancellationToken = default)
    {
        await Delay(cancellationToken); EnsureRead();
        return data.Mutate(주문번호, 음식배달가능행동Ids.음식점조리시작, order =>
        {
            order.상태 = 음식주문상태코드.조리중;
            order.선택조리예상분 = 음식점조리시간정책.Clamp(조리예상분);
            order.상세주문!.조리시작시각Utc = DateTime.UtcNow;
            order.상세주문.조리예상분 = order.선택조리예상분;
            order.상세주문.조리예상완료시각Utc = DateTime.UtcNow.AddMinutes(order.선택조리예상분.Value);
        });
    }
    public async Task<음식점주문DeskItem?> 조리시간변경Async(string 주문번호, int 조리예상분, CancellationToken cancellationToken = default)
    {
        await Delay(cancellationToken); EnsureRead();
        return data.Mutate(주문번호, 음식배달가능행동Ids.음식점조리시간변경, order =>
        { order.선택조리예상분 = 음식점조리시간정책.Clamp(조리예상분); order.상세주문!.조리예상분 = order.선택조리예상분; });
    }
    public async Task<음식점주문DeskItem?> 픽업준비완료Async(string 주문번호, CancellationToken cancellationToken = default)
    {
        await Delay(cancellationToken); EnsureRead();
        return data.Mutate(주문번호, 음식배달가능행동Ids.음식점픽업준비완료, order =>
        { order.상태 = 음식주문상태코드.픽업대기; order.상세주문!.픽업준비시각Utc = DateTime.UtcNow; });
    }
    public Task 전표출력완료Async(string 주문번호, CancellationToken cancellationToken = default)
    { cancellationToken.ThrowIfCancellationRequested(); data.PrintRequested(주문번호); return Task.CompletedTask; }
    public Task 전표출력요청기록Async(string 주문번호, CancellationToken cancellationToken = default)
        => 전표출력완료Async(주문번호, cancellationToken);
    public async Task<음식점주문수락결과> 전표준비Async(string 주문번호, CancellationToken cancellationToken = default)
    { await Delay(cancellationToken); EnsureRead(); return Result(data.조회(주문번호)); }
    private 음식점주문수락결과 Result(음식점주문DeskItem? order)
        => new() { 성공 = order is not null, 메시지 = "화면 검토용 전표 준비", 주문 = order, 상세주문 = order?.상세주문,
            전표Draft = order?.상세주문 is { } detail ? slips.Create주문전표Draft(detail) : null };
}

public sealed class Pos미리보기Realtime : I음식점주문SignalRClientService
{
    public event Func<음식점주문수신알림, Task>? 주문수신;
    public event Func<음식점주문상태변경알림, Task>? 주문상태변경;
    public event Func<음식점실시간연결상태변경, Task>? 상태변경;
    public event Func<Task>? 재연결후재조회요청;
    public 음식점실시간연결상태 연결상태 { get; private set; } = 음식점실시간연결상태.연결대기;
    public async Task 연결Async(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (연결상태 == 음식점실시간연결상태.연결됨) return;
        연결상태 = 음식점실시간연결상태.연결됨;
        if (상태변경 is not { } handlers) return;
        var signal = new 음식점실시간연결상태변경(연결상태, "합성 수신 연결");
        foreach (Func<음식점실시간연결상태변경, Task> callback in handlers.GetInvocationList()) await callback(signal);
    }
    public Task 연결해제Async() { 연결상태 = 음식점실시간연결상태.연결끊김; return Task.CompletedTask; }
    public async Task EmitNewAsync(음식점주문DeskItem order)
    {
        if (주문수신 is not { } handlers) return;
        var signal = new 음식점주문수신알림 { 주문번호 = order.주문번호, 음식점Id = order.음식점Id,
            고객명 = order.고객명, 메뉴요약 = order.메뉴요약, 상품목록 = order.상품목록,
            주문금액 = order.주문금액, 상태 = order.상태 };
        foreach (Func<음식점주문수신알림, Task> callback in handlers.GetInvocationList()) await callback(signal);
    }
    public async Task EmitStateAsync(음식점주문DeskItem order)
    {
        if (주문상태변경 is not { } handlers) return;
        var signal = new 음식점주문상태변경알림 { 주문번호 = order.주문번호, 음식점Id = order.음식점Id,
            주문상태 = order.상태, 배차상태 = order.배차상태, 변경사유 = "합성 상태 변경" };
        foreach (Func<음식점주문상태변경알림, Task> callback in handlers.GetInvocationList()) await callback(signal);
    }
    public async Task EmitRefreshAsync()
    {
        if (재연결후재조회요청 is not { } handlers) return;
        foreach (Func<Task> callback in handlers.GetInvocationList()) await callback();
    }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

public sealed class Pos미리보기Api(Pos미리보기자료 data) : I음식주문ApiClient
{
    public Task<음식주문응답?> 주문상세조회Async(string 주문번호, CancellationToken cancellationToken = default)
    { cancellationToken.ThrowIfCancellationRequested(); return Task.FromResult(data.조회(주문번호)?.상세주문); }
    public Task<음식점주문수신함응답> 주문목록조회Async(음식점주문수신함조회요청 request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("합성 POS는 Desk port로만 조회합니다.");
    public Task<음식주문응답?> 음식점수락Async(string 주문번호, 음식점주문수락요청 request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("합성 POS는 Desk port로만 처리합니다.");
    public Task<음식주문응답?> 음식점진행변경Async(string 주문번호, 음식점주문진행변경요청 request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("합성 POS는 Desk port로만 처리합니다.");
}

public sealed class Pos미리보기무음 : I주문알림Service
{ public Task 신규주문알림재생Async(CancellationToken cancellationToken = default) => Task.CompletedTask; }

public sealed class Pos미리보기프린터(Pos미리보기자료 data) : IRestaurantReceiptPrinter
{
    public bool UsesNativeDialog => true;
    public Task RequestPrintDialogAsync(string title, string html, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (data.PrintFailure) throw new InvalidOperationException("합성 인쇄 요청 실패");
        data.PrintRequests++;
        return Task.CompletedTask;
    }
}

// 이번 화면 검토 밖의 설정을 펼쳐도 DI 오류나 실제 HTTP 요청을 만들지 않는다.
public sealed class Pos미리보기미연결Api : ISsalddelJsonApiClient
{
    private static InvalidOperationException OutsideScope()
        => new("이 합성 검토에서는 주문 처리만 연결했습니다. 설정 API는 연결하지 않았습니다.");
    public Task<TResponse?> GetAsync<TResponse>(string path, string operationName, bool allowNotFound = true, CancellationToken cancellationToken = default)
        => Task.FromException<TResponse?>(OutsideScope());
    public Task<TResponse?> SendAsync<TResponse>(HttpMethod method, string path, string operationName, bool allowNotFound = false, CancellationToken cancellationToken = default)
        => Task.FromException<TResponse?>(OutsideScope());
    public Task<TResponse?> SendAsync<TRequest, TResponse>(HttpMethod method, string path, TRequest request, string operationName, bool allowNotFound = false, CancellationToken cancellationToken = default)
        => Task.FromException<TResponse?>(OutsideScope());
    public Task SendAsync(HttpMethod method, string path, string operationName, CancellationToken cancellationToken = default)
        => Task.FromException(OutsideScope());
    public Task SendAsync<TRequest>(HttpMethod method, string path, TRequest request, string operationName, CancellationToken cancellationToken = default)
        => Task.FromException(OutsideScope());
}
