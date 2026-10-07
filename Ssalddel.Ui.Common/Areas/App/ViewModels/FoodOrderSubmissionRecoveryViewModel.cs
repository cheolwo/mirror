using CommunityToolkit.Mvvm.ComponentModel;
using Ssalddel.Contracts.Food;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.Ui.Common.Areas.App.ViewModels;

/// <summary>미확정 제출의 보존과 조회만 소유하며 재제출은 명시적으로 요청해야 합니다.</summary>
public sealed partial class FoodOrderSubmissionRecoveryViewModel(
    IFoodOrderPendingSubmissionStore store,
    I주문자음식주문접수결과Service reader,
    I주문자음식주문쓰기Service writer) : ObservableObject, IDisposable
{
    private string? ownerId;
    private long generation;
    private CancellationTokenSource? operationCancellation;
    private bool disposed;
    [ObservableProperty] public partial bool 처리중 { get; private set; }
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(오류발생))]
    [NotifyPropertyChangedFor(nameof(오류메시지))]
    public partial Api작업오류? 오류 { get; private set; }
    public bool 오류발생 => 오류 is not null;
    public string? 오류메시지 => 오류?.메시지;
    [ObservableProperty] public partial FoodOrderPendingSubmission? Pending { get; private set; }
    [ObservableProperty] public partial bool 초기확인완료 { get; private set; }
    [ObservableProperty] public partial bool 미접수확인됨 { get; private set; }
    [ObservableProperty] public partial string? 접수주문번호 { get; private set; }
    [ObservableProperty] public partial bool 제출중 { get; private set; }
    public bool 입력잠금 => !초기확인완료 || Pending is not null || 처리중 || 제출중;

    public async Task 계정설정Async(string? userId, CancellationToken cancellationToken = default)
    {
        if (disposed) return;
        userId = string.IsNullOrWhiteSpace(userId) ? null : userId.Trim();
        if (ownerId == userId && 초기확인완료) return;
        작업취소();
        ownerId = userId;
        Pending = null;
        접수주문번호 = null;
        미접수확인됨 = false;
        제출중 = false;
        초기확인완료 = false;
        var current = generation;
        await 작업실행Async(async token =>
        {
            // 익명 상태에서 저장된 수령 정보는 읽거나 화면에 노출하지 않습니다.
            if (ownerId is null) { 초기확인완료 = true; return; }
            var snapshot = await store.LoadAsync(token);
            token.ThrowIfCancellationRequested();
            if (current != generation) return;
            if (snapshot is not null && snapshot.OwnerId != ownerId)
            {
                await store.ClearAsync(snapshot.Request.클라이언트요청Id, token);
                snapshot = null;
            }
            token.ThrowIfCancellationRequested();
            if (current != generation) return;
            Pending = snapshot;
            초기확인완료 = true;
        }, "이전 주문 제출을 확인했습니다.", cancellationToken,
            _ => "이전 주문 제출을 확인하지 못했습니다. 다시 확인해 주세요.");
        if (current == generation && Pending is not null) await 결과확인Async(cancellationToken);
    }

    public void 계정숨김()
    {
        작업취소();
        ownerId = null;
        Pending = null;
        접수주문번호 = null;
        미접수확인됨 = false;
        초기확인완료 = false;
        제출중 = false;
    }

    public async Task<음식주문응답> 등록Async(음식주문등록요청 request, CancellationToken cancellationToken)
    {
        if (disposed || 제출중) throw new InvalidOperationException("현재 제출 결과를 먼저 확인해 주세요.");
        if (string.IsNullOrWhiteSpace(ownerId) || !초기확인완료)
            throw new InvalidOperationException("로그인한 계정의 이전 주문 제출을 먼저 확인해 주세요.");
        if (Pending is not null && Pending.Request.클라이언트요청Id != request.클라이언트요청Id)
            throw new InvalidOperationException("이전 주문의 접수 결과를 먼저 확인해 주세요.");
        var snapshot = Pending ?? new FoodOrderPendingSubmission(ownerId, request, DateTime.UtcNow);
        var current = generation;
        // 저장 실패 시 POST하지 않습니다. 서버 응답 유실 뒤에도 동일 요청을 보존합니다.
        제출중 = true;
        try
        {
            await store.SaveAsync(snapshot, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (current != generation) throw new OperationCanceledException(cancellationToken);
            Pending = snapshot;
            음식주문응답 response;
            try { response = await writer.등록Async(snapshot.Request, cancellationToken); }
            catch (SsalddelApiException ex) when (current == generation && ex.StatusCode is 400 or 403 or 404 or 422)
            {
                await store.ClearAsync(snapshot.Request.클라이언트요청Id, cancellationToken);
                if (current == generation) Pending = null;
                throw;
            }
            cancellationToken.ThrowIfCancellationRequested();
            if (current != generation) throw new OperationCanceledException(cancellationToken);
            if (string.IsNullOrWhiteSpace(response.주문번호)) throw new InvalidOperationException("주문 접수 응답을 확인해 주세요.");
            await store.ClearAsync(snapshot.Request.클라이언트요청Id, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (current != generation) throw new OperationCanceledException(cancellationToken);
            Pending = null;
            접수성공(response.주문번호);
            return response;
        }
        finally { if (current == generation) 제출중 = false; }
    }

    public async Task<bool> 결과확인Async(CancellationToken cancellationToken = default)
    {
        if (Pending is not { } pending || ownerId != pending.OwnerId || 처리중 || 제출중) return false;
        var current = generation;
        미접수확인됨 = false;
        return await 작업실행Async(async token =>
        {
            var result = await reader.접수결과Async(pending.Request.클라이언트요청Id, token);
            token.ThrowIfCancellationRequested();
            if (current != generation) throw new OperationCanceledException(token);
            if (result is null) { 미접수확인됨 = true; return; }
            if (string.IsNullOrWhiteSpace(result.주문번호)) throw new InvalidOperationException("주문 접수 응답을 확인해 주세요.");
            await store.ClearAsync(pending.Request.클라이언트요청Id, token);
            token.ThrowIfCancellationRequested();
            if (current != generation) throw new OperationCanceledException(token);
            Pending = null;
            접수성공(result.주문번호);
        }, "주문 접수 결과를 확인했습니다.", cancellationToken,
            _ => "접수 결과를 확인하지 못했습니다. 다시 확인해 주세요.");
    }

    public Task<bool> 동일주문재제출Async(CancellationToken cancellationToken = default)
    {
        if (Pending is not { } pending || !미접수확인됨 || 처리중 || 제출중 || ownerId != pending.OwnerId)
            return Task.FromResult(false);
        return 작업실행Async(async token => { await 등록Async(pending.Request, token); },
            "주문 접수 결과를 확인했습니다.", cancellationToken,
            ex => ex is SsalddelApiException { StatusCode: 400 or 403 or 404 or 422 }
                ? ex.Message : "주문 접수를 확인하지 못했습니다. 같은 주문의 결과를 다시 확인해 주세요.");
    }
    private void 접수성공(string orderNo)
    {
        접수주문번호 = orderNo;
        미접수확인됨 = false;
    }

    public void 거절안내초기화()
    {
        if (Pending is null && !처리중 && !제출중) 오류 = null;
    }

    public void 작업취소()
    {
        generation++;
        operationCancellation?.Cancel();
        operationCancellation = null;
        처리중 = false;
        오류 = null;
    }

    private async Task<bool> 작업실행Async(Func<CancellationToken, Task> action, string successMessage,
        CancellationToken cancellationToken, Func<Exception, string> errorMessage)
    {
        if (disposed || 처리중) return false;
        var current = generation;
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        operationCancellation = operation;
        처리중 = true;
        오류 = null;
        try
        {
            await action(operation.Token);
            operation.Token.ThrowIfCancellationRequested();
            return current == generation && !disposed;
        }
        catch (OperationCanceledException) when (operation.IsCancellationRequested || current != generation) { return false; }
        catch (Exception ex)
        {
            if (current == generation && !disposed) 오류 = Api작업오류.변환(ex) with { 메시지 = errorMessage(ex) };
            return false;
        }
        finally
        {
            if (current == generation && !disposed)
            {
                처리중 = false;
                operationCancellation = null;
            }
        }
    }

    public void Dispose()
    {
        if (disposed) return;
        계정숨김();
        disposed = true;
    }
}
