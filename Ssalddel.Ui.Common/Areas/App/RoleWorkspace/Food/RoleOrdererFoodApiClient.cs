using System.Globalization;
using Ssalddel.Contracts.Food;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Food;

/// <summary>기존 주문 작성·접수 복구 서비스를 주문자 보호 API에 연결합니다.</summary>
public sealed class RoleOrdererFoodApiClient(IRoleWorkspaceApi api) : I주문자음식주문쓰기Service,
    I주문자음식주문접수결과Service, I주문자음식주문읽기Service, I주문자음식주문수령확인Service, I주문자음식주문취소Service
{
    public Task<음식주문응답> 등록Async(음식주문등록요청 request, CancellationToken cancellationToken = default)
        => SendAsync("api/v1/food-orders", request, "음식 주문 등록", cancellationToken);

    public async Task<음식주문접수결과응답?> 접수결과Async(Guid requestId, CancellationToken cancellationToken = default)
    {
        if (requestId == Guid.Empty) throw new ArgumentException("제출 요청을 확인해 주세요.", nameof(requestId));
        try { return await api.GetAsync<음식주문접수결과응답>("orderer", $"api/v1/food-orders/client-requests/{requestId:D}", cancellationToken); }
        catch (RoleWorkspaceAccessException ex) when (ex.StatusCode == 404) { return null; }
        catch (RoleWorkspaceAccessException ex) { throw Translate(ex, "음식 주문 접수 결과 조회"); }
    }

    public async Task<주문자음식주문목록응답> 목록Async(주문자음식주문목록조회요청 request, CancellationToken cancellationToken = default)
    {
        var query = new List<string>
        {
            "page=" + Math.Max(1, request.Page).ToString(CultureInfo.InvariantCulture),
            "pageSize=" + Math.Clamp(request.PageSize, 1, 50).ToString(CultureInfo.InvariantCulture)
        };
        if (!string.IsNullOrWhiteSpace(request.검색어)) query.Add("검색어=" + Uri.EscapeDataString(request.검색어.Trim()));
        if (!string.IsNullOrWhiteSpace(request.상태)) query.Add("상태=" + Uri.EscapeDataString(request.상태.Trim()));
        try { return await api.GetAsync<주문자음식주문목록응답>("orderer", "api/v1/food-orders?" + string.Join('&', query), cancellationToken); }
        catch (RoleWorkspaceAccessException ex) { throw Translate(ex, "내 음식 주문 목록 조회"); }
    }

    public async Task<주문자음식주문상세응답?> 상세Async(string orderNo, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(orderNo);
        try { return await api.GetAsync<주문자음식주문상세응답>("orderer", "api/v1/food-orders/" + Uri.EscapeDataString(orderNo.Trim()), cancellationToken); }
        catch (RoleWorkspaceAccessException ex) when (ex.StatusCode == 404) { return null; }
        catch (RoleWorkspaceAccessException ex) { throw Translate(ex, "내 음식 주문 상세 조회"); }
    }

    public Task<음식주문응답> 수령확인Async(string orderNo, 주문자음식주문수령확인요청 request, CancellationToken cancellationToken = default)
        => SendAsync("api/v1/food-orders/" + Uri.EscapeDataString(orderNo.Trim()) + "/receipt-confirmation", request, "음식 주문 수령 확인", cancellationToken);
    public Task<음식주문응답> 취소Async(string orderNo, 주문자음식주문취소요청 request, CancellationToken cancellationToken = default)
        => SendAsync("api/v1/food-orders/" + Uri.EscapeDataString(orderNo.Trim()) + "/cancellation", request, "음식 주문 취소", cancellationToken);

    private async Task<음식주문응답> SendAsync(string path, object body, string operation, CancellationToken token)
    {
        try { return await api.PostAsync<음식주문응답>("orderer", path, body, token) ?? throw new InvalidOperationException("음식 주문 응답이 비어 있습니다."); }
        catch (RoleWorkspaceAccessException ex) { throw Translate(ex, operation); }
    }
    private static SsalddelApiException Translate(RoleWorkspaceAccessException ex, string operation)
        => new(ex.Message, ex.StatusCode, operation, ex.ResponseBody ?? string.Empty, null, errorCode: ex.ErrorCode);
}
