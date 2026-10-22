using System.Globalization;
using Ssalddel.Contracts.Common.Inbound;
using Ssalddel.Contracts.Common.Inventory;
using Ssalddel.Contracts.Common.Warehouse;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.WebApp.Services;

/// <summary>
/// 통합 Web의 입고·창고 화면을 서버의 사용자 소유 원장에 연결합니다.
/// 운영 API 실패를 샘플 데이터로 대체하지 않습니다.
/// </summary>
public sealed class WebShipperWarehouseWorkspaceService(
    ISsalddelJsonApiClient client) : IWarehouseWorkspaceService
{
    private const string BasePath = "api/v1/warehouse-operations";

    public Task<창고목록응답?> GetWarehousesAsync(
        CancellationToken cancellationToken = default)
        => client.GetAsync<창고목록응답>(
            $"{BasePath}/warehouses",
            "창고 목록 조회",
            allowNotFound: false,
            cancellationToken);

    public Task<창고요약응답?> CreateWarehouseAsync(
        창고저장요청 payload,
        CancellationToken cancellationToken = default)
        => client.SendAsync<창고저장요청, 창고요약응답>(
            HttpMethod.Post,
            $"{BasePath}/warehouses",
            payload,
            "창고 등록",
            cancellationToken: cancellationToken);

    public Task<입고요청목록응답?> GetInboundsAsync(
        CancellationToken cancellationToken = default)
        => client.GetAsync<입고요청목록응답>(
            $"{BasePath}/inbounds",
            "입고 요청 목록 조회",
            allowNotFound: false,
            cancellationToken);

    public Task<입고요청항목응답?> GetInboundAsync(
        long inboundId,
        CancellationToken cancellationToken = default)
        => client.GetAsync<입고요청항목응답>(
            $"{BasePath}/inbounds/{RequireId(inboundId, nameof(inboundId))}",
            "입고 요청 상세 조회",
            allowNotFound: true,
            cancellationToken);

    public Task<입고요청페이지응답?> QueryInboundsAsync(
        입고요청목록조회요청 request,
        CancellationToken cancellationToken = default)
        => client.GetAsync<입고요청페이지응답>(
            BuildInboundQueryPath(request),
            "입고 요청 조건 조회",
            allowNotFound: false,
            cancellationToken);

    public Task<입고요청항목응답?> CreateInboundAsync(
        입고요청저장요청 payload,
        CancellationToken cancellationToken = default)
        => client.SendAsync<입고요청저장요청, 입고요청항목응답>(
            HttpMethod.Post,
            $"{BasePath}/inbounds",
            payload,
            "입고 요청 등록",
            cancellationToken: cancellationToken);

    public Task<입고요청항목응답?> GetInboundByClientRequestAsync(
        Guid clientRequestId,
        CancellationToken cancellationToken = default)
        => client.GetAsync<입고요청항목응답>(
            $"{BasePath}/inbounds/by-client-request/{RequireRequestId(clientRequestId)}",
            "입고 접수 결과 확인",
            allowNotFound: true,
            cancellationToken);

    public Task<입고상품목록응답?> CompleteInboundAsync(
        long inboundId,
        입고완료요청 payload,
        CancellationToken cancellationToken = default)
        => client.SendAsync<입고완료요청, 입고상품목록응답>(
            HttpMethod.Post,
            $"{BasePath}/inbounds/{RequireId(inboundId, nameof(inboundId))}/complete",
            payload,
            "입고 완료",
            cancellationToken: cancellationToken);

    public Task<재고목록응답?> GetInventoryAsync(
        CancellationToken cancellationToken = default)
        => client.GetAsync<재고목록응답>(
            $"{BasePath}/inventory",
            "화주 재고 목록 조회",
            allowNotFound: false,
            cancellationToken);

    private static string BuildInboundQueryPath(입고요청목록조회요청 request)
    {
        ArgumentNullException.ThrowIfNull(request);

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
            values.Add(
                $"warehouseId={request.WarehouseId.Value.ToString(CultureInfo.InvariantCulture)}");
        }

        return $"{BasePath}/inbounds/query?{string.Join('&', values)}";
    }

    private static void AddQueryValue(
        ICollection<string> values,
        string name,
        string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            values.Add($"{name}={Uri.EscapeDataString(value.Trim())}");
        }
    }

    private static string RequireId(long value, string parameterName)
        => value > 0
            ? value.ToString(CultureInfo.InvariantCulture)
            : throw new ArgumentOutOfRangeException(
                parameterName,
                "원장 ID는 1 이상이어야 합니다.");

    private static string RequireRequestId(Guid value)
        => value != Guid.Empty ? value.ToString("D") : throw new ArgumentOutOfRangeException(nameof(value));
}
