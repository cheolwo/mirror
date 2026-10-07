using Ssalddel.Contracts.Food;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;

namespace RoleWorkspacePreview;

// 외부 요청을 보내지 않는 화면 검토용 예시입니다. 실제 원장 검증의 대체가 아닙니다.
public sealed class PreviewActionApi : IRoleWorkspaceApi
{
    private string scenario = "after-read-failure";
    public event Action? Changed;
    public int Writes { get; private set; }
    public int Reads { get; private set; }
    public void Reset(string value) { scenario = value; Writes = Reads = 0; }

    public Task<T> GetAsync<T>(string roleKey, string relativePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (roleKey != "orderer" || relativePath != "api/v1/food-orders/order-preview")
            throw new InvalidOperationException("미리보기에서 지원하지 않는 조회입니다.");
        Reads++; Changed?.Invoke();
        if (Writes > 0 && Reads == 2 && scenario == "after-read-failure")
            throw new HttpRequestException("Preview read interruption");
        var changed = Writes > 0 && !(Reads == 2 && scenario == "stale-read");
        var detail = new 주문자음식주문상세응답
        {
            주문 = new() { 주문번호 = "order-preview", 상태 = changed ? 음식주문상태코드.취소 : "주문접수" },
            AvailableActions = changed ? [] : [new() { ActionId = 음식배달가능행동Ids.주문취소, ExpectedRevision = 3 }]
        };
        return Task.FromResult((T)(object)detail);
    }

    public Task<T?> PostAsync<T>(string roleKey, string relativePath, object body, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (roleKey != "orderer" || relativePath != "api/v1/food-orders/order-preview/cancellation")
            throw new InvalidOperationException("미리보기에서 지원하지 않는 요청입니다.");
        var request = body as 주문자음식주문취소요청 ?? throw new InvalidOperationException("취소 요청 형식이 다릅니다.");
        Writes++; Changed?.Invoke();
        return Task.FromResult<T?>((T)(object)new 음식주문응답
        {
            주문번호 = "order-preview", Revision = 4, 상태 = 음식주문상태코드.취소,
            상태이력 = [new() { 클라이언트요청Id = request.클라이언트요청Id, 처리UserId = "preview-owner",
                이전상태 = 음식주문상태코드.주문대기, 다음상태 = 음식주문상태코드.취소, 사유 = request.사유, 전이시각Utc = DateTime.UtcNow }]
        });
    }

    public Task<T?> PutAsync<T>(string roleKey, string relativePath, object body, CancellationToken cancellationToken)
        => throw new NotSupportedException();
    public Task<T?> UploadAsync<T>(string roleKey, string relativePath, HttpContent body, CancellationToken cancellationToken)
        => throw new NotSupportedException();
}
