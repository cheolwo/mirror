using Hongdal.Contracts.Common.Inbound;
using Hongdal.Contracts.Common.Inventory;
using Hongdal.Contracts.Common.Warehouse;

namespace Hongdal.Ui.Common.Areas.App.Services;

public interface IWarehouseWorkspaceService
{
    Task<창고목록응답?> GetWarehousesAsync(CancellationToken cancellationToken = default);

    Task<창고요약응답?> CreateWarehouseAsync(창고저장요청 payload, CancellationToken cancellationToken = default);

    Task<입고요청목록응답?> GetInboundsAsync(CancellationToken cancellationToken = default);

    async Task<입고요청페이지응답?> QueryInboundsAsync(
        입고요청목록조회요청 request,
        CancellationToken cancellationToken = default)
    {
        var response = await GetInboundsAsync(cancellationToken);
        return 입고요청목록Query.Apply(response?.Items ?? [], request);
    }

    Task<입고요청항목응답?> CreateInboundAsync(입고요청저장요청 payload, CancellationToken cancellationToken = default);

    Task<입고상품목록응답?> CompleteInboundAsync(long inboundId, 입고완료요청 payload, CancellationToken cancellationToken = default);

    Task<재고목록응답?> GetInventoryAsync(CancellationToken cancellationToken = default);
}

/// <summary>샘플·오프라인 서비스가 서버 조회 계약을 동일하게 흉내 내는 목록 평가기입니다.</summary>
public static class 입고요청목록Query
{
    public static 입고요청페이지응답 Apply(
        IEnumerable<입고요청항목응답> source,
        입고요청목록조회요청 request)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(request);

        var page = Math.Max(0, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 200);
        var query = source.Where(item => !string.Equals(item.상태, "입고취소", StringComparison.OrdinalIgnoreCase));

        if (request.WarehouseId is > 0)
        {
            query = query.Where(item => item.창고Id == request.WarehouseId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            query = query.Where(item => string.Equals(
                item.상태,
                request.Status.Trim(),
                StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(request.FlowType))
        {
            var flowType = 입고흐름유형코드.Normalize(request.FlowType);
            query = query.Where(item => string.Equals(
                item.입고흐름유형,
                flowType,
                StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(item =>
                item.Id.ToString().Contains(search, StringComparison.OrdinalIgnoreCase)
                || item.공급처코드.Contains(search, StringComparison.OrdinalIgnoreCase)
                || item.공급처명.Contains(search, StringComparison.OrdinalIgnoreCase)
                || item.예정상품명.Contains(search, StringComparison.OrdinalIgnoreCase)
                || item.예정SKU.Contains(search, StringComparison.OrdinalIgnoreCase)
                || item.원주문참조번호.Contains(search, StringComparison.OrdinalIgnoreCase)
                || item.주문참조번호.Contains(search, StringComparison.OrdinalIgnoreCase)
                || item.계약정보.계약번호.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        query = (request.SortBy?.Trim(), request.SortDescending) switch
        {
            (nameof(입고요청항목응답.Id), false) => query.OrderBy(item => item.Id),
            (nameof(입고요청항목응답.Id), true) => query.OrderByDescending(item => item.Id),
            (nameof(입고요청항목응답.창고Id), false) => query.OrderBy(item => item.창고Id).ThenBy(item => item.Id),
            (nameof(입고요청항목응답.창고Id), true) => query.OrderByDescending(item => item.창고Id).ThenByDescending(item => item.Id),
            (nameof(입고요청항목응답.공급처코드), false) => query.OrderBy(item => item.공급처코드).ThenBy(item => item.Id),
            (nameof(입고요청항목응답.공급처코드), true) => query.OrderByDescending(item => item.공급처코드).ThenByDescending(item => item.Id),
            (nameof(입고요청항목응답.공급처명), false) => query.OrderBy(item => item.공급처명).ThenBy(item => item.Id),
            (nameof(입고요청항목응답.공급처명), true) => query.OrderByDescending(item => item.공급처명).ThenByDescending(item => item.Id),
            (nameof(입고요청항목응답.원주문참조번호), false) => query.OrderBy(item => item.원주문참조번호).ThenBy(item => item.Id),
            (nameof(입고요청항목응답.원주문참조번호), true) => query.OrderByDescending(item => item.원주문참조번호).ThenByDescending(item => item.Id),
            (nameof(입고요청항목응답.상태), false) => query.OrderBy(item => item.상태).ThenBy(item => item.Id),
            (nameof(입고요청항목응답.상태), true) => query.OrderByDescending(item => item.상태).ThenByDescending(item => item.Id),
            (nameof(입고요청항목응답.예정도착일), false) => query.OrderBy(item => item.예정도착일).ThenBy(item => item.Id),
            (nameof(입고요청항목응답.예정도착일), true) => query.OrderByDescending(item => item.예정도착일).ThenByDescending(item => item.Id),
            _ => query.OrderByDescending(item => item.Id)
        };

        var items = query.ToArray();
        var skip = (int)Math.Min((long)page * pageSize, int.MaxValue);
        return new 입고요청페이지응답
        {
            Items = items.Skip(skip).Take(pageSize).ToArray(),
            TotalCount = items.Length,
            Page = page,
            PageSize = pageSize
        };
    }
}
