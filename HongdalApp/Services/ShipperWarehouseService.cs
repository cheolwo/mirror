using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Hongdal.Contracts.Common.Inbound;
using Hongdal.Contracts.Common.Inventory;
using Hongdal.Contracts.Common.Warehouse;
using Hongdal.Contracts.Shipper.Request;

namespace HongdalApp.Services;

public sealed class ShipperWarehouseService : IShipperWarehouseWorkflowService
{
    private readonly HttpClient _httpClient;
    private readonly IAuthSession _authSession;

    public ShipperWarehouseService(HttpClient httpClient, IAuthSession authSession)
    {
        _httpClient = httpClient;
        _authSession = authSession;
    }

    public Task<창고목록응답?> GetWarehousesAsync(CancellationToken cancellationToken = default)
        => GetAuthorizedJsonAsync<창고목록응답>("api/v1/warehouse-operations/warehouses", cancellationToken);

    public Task<창고요약응답?> CreateWarehouseAsync(창고저장요청 payload, CancellationToken cancellationToken = default)
        => PostAuthorizedJsonAsync<창고저장요청, 창고요약응답>("api/v1/warehouse-operations/warehouses", payload, cancellationToken);

    public Task<입고요청목록응답?> GetInboundsAsync(CancellationToken cancellationToken = default)
        => GetAuthorizedJsonAsync<입고요청목록응답>("api/v1/warehouse-operations/inbounds", cancellationToken);

    public Task<입고요청페이지응답?> QueryInboundsAsync(
        입고요청목록조회요청 request,
        CancellationToken cancellationToken = default)
        => GetAuthorizedJsonAsync<입고요청페이지응답>(BuildInboundQueryPath(request), cancellationToken);

    public Task<입고요청항목응답?> CreateInboundAsync(입고요청저장요청 payload, CancellationToken cancellationToken = default)
        => PostAuthorizedJsonAsync<입고요청저장요청, 입고요청항목응답>("api/v1/warehouse-operations/inbounds", payload, cancellationToken);

    public Task<입고상품목록응답?> CompleteInboundAsync(long inboundId, 입고완료요청 payload, CancellationToken cancellationToken = default)
        => PostAuthorizedJsonAsync<입고완료요청, 입고상품목록응답>($"api/v1/warehouse-operations/inbounds/{inboundId}/complete", payload, cancellationToken);

    public Task<재고목록응답?> GetInventoryAsync(CancellationToken cancellationToken = default)
        => GetAuthorizedJsonAsync<재고목록응답>("api/v1/warehouse-operations/inventory", cancellationToken);

    public Task<화주운송의뢰응답?> CreateReconsignmentAsync(재고운송의뢰생성요청 payload, CancellationToken cancellationToken = default)
        => PostAuthorizedJsonAsync<재고운송의뢰생성요청, 화주운송의뢰응답>("api/v1/warehouse-operations/inventory/reconsignment", payload, cancellationToken);

    private async Task<T?> GetAuthorizedJsonAsync<T>(string path, CancellationToken cancellationToken)
    {
        using var request = CreateAuthorizedRequest(HttpMethod.Get, path);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(await BuildFailureMessageAsync(response, path, cancellationToken));
        }

        return await response.Content.ReadFromJsonAsync<T>(cancellationToken);
    }

    private async Task<TResponse?> PostAuthorizedJsonAsync<TRequest, TResponse>(string path, TRequest payload, CancellationToken cancellationToken)
    {
        using var request = CreateAuthorizedRequest(HttpMethod.Post, path);
        request.Content = JsonContent.Create(payload);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(await BuildFailureMessageAsync(response, path, cancellationToken));
        }

        return await response.Content.ReadFromJsonAsync<TResponse>(cancellationToken);
    }

    private HttpRequestMessage CreateAuthorizedRequest(HttpMethod method, string path)
    {
        if (string.IsNullOrWhiteSpace(_authSession.AccessToken))
        {
            throw new InvalidOperationException("서버 인증 정보가 없어 창고 API를 호출할 수 없습니다.");
        }

        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _authSession.AccessToken);
        return request;
    }

    private static string BuildInboundQueryPath(입고요청목록조회요청 request)
    {
        var values = new List<string>
        {
            $"page={Math.Max(0, request.Page).ToString(CultureInfo.InvariantCulture)}",
            $"pageSize={Math.Clamp(request.PageSize, 1, 200).ToString(CultureInfo.InvariantCulture)}",
            $"sortDescending={request.SortDescending.ToString().ToLowerInvariant()}"
        };
        AddQueryValue(values, "search", request.Search);
        AddQueryValue(values, "sortBy", request.SortBy);
        AddQueryValue(values, "status", request.Status);
        AddQueryValue(values, "flowType", request.FlowType);
        if (request.WarehouseId is > 0)
        {
            values.Add($"warehouseId={request.WarehouseId.Value.ToString(CultureInfo.InvariantCulture)}");
        }

        return $"api/v1/warehouse-operations/inbounds/query?{string.Join('&', values)}";
    }

    private static void AddQueryValue(ICollection<string> values, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            values.Add($"{name}={Uri.EscapeDataString(value.Trim())}");
        }
    }

    private static async Task<string> BuildFailureMessageAsync(HttpResponseMessage response, string path, CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(body))
        {
            return $"창고 서버 API 요청에 실패했습니다. path={path}, HTTP {(int)response.StatusCode}";
        }

        return $"창고 서버 API 요청에 실패했습니다. path={path}, HTTP {(int)response.StatusCode}: {body}";
    }
}
