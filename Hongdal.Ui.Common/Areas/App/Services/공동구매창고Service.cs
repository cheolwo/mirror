using System.Globalization;
using Hongdal.Contracts.Common.Inbound;
using Hongdal.Contracts.Common.Inventory;
using Hongdal.Contracts.Common.Warehouse;
using Hongdal.Contracts.Shipper.Request;

namespace Hongdal.Ui.Common.Areas.App.Services;

/// <summary>
/// 업무 종류와 무관하게 창고 기준정보와 입출고 작업을 다루는 API 경계입니다.
/// </summary>
public interface I입출고작업Service
{
    Task<IReadOnlyList<창고요약응답>> 창고목록조회Async(CancellationToken cancellationToken = default);

    Task<창고요약응답?> 창고생성Async(
        창고저장요청 request,
        CancellationToken cancellationToken = default);

    Task<창고요약응답?> 창고수정Async(
        long warehouseId,
        창고저장요청 request,
        CancellationToken cancellationToken = default);

    Task 창고삭제Async(long warehouseId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<창고사용자항목응답>> 창고사용자목록조회Async(
        long warehouseId,
        CancellationToken cancellationToken = default);

    Task<창고사용자항목응답?> 창고사용자추가Async(
        long warehouseId,
        창고사용자저장요청 request,
        CancellationToken cancellationToken = default);

    Task<창고사용자항목응답?> 창고사용자수정Async(
        long warehouseId,
        long warehouseUserId,
        창고사용자저장요청 request,
        CancellationToken cancellationToken = default);

    Task 창고사용자삭제Async(
        long warehouseId,
        long warehouseUserId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<입고요청항목응답>> 입고목록조회Async(CancellationToken cancellationToken = default);

    async Task<입고요청페이지응답> 입고목록조회Async(
        입고요청목록조회요청 request,
        CancellationToken cancellationToken = default)
        => 입고요청목록Query.Apply(
            await 입고목록조회Async(cancellationToken),
            request);

    Task<입고요청페이지응답> 입고예정관점목록조회Async(
        string perspectiveCode,
        string? communityLedgerId,
        입고요청목록조회요청 request,
        CancellationToken cancellationToken = default);

    Task<출고예정페이지응답> 출고예정관점목록조회Async(
        string perspectiveCode,
        string? communityLedgerId,
        출고예정목록조회요청 request,
        CancellationToken cancellationToken = default);

    Task<입고요청항목응답?> 입고요청생성Async(
        입고요청저장요청 request,
        CancellationToken cancellationToken = default);

    Task<입고요청항목응답?> 입고요청수정Async(
        long inboundId,
        입고요청저장요청 request,
        CancellationToken cancellationToken = default);

    Task 입고요청취소Async(long inboundId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<입고상품항목응답>> 입고완료Async(
        long inboundId,
        입고완료요청 request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<재고항목응답>> 재고목록조회Async(CancellationToken cancellationToken = default);

    Task<창고작업결과응답?> 입고검수Async(
        long inboundItemId,
        입고검수요청 request,
        CancellationToken cancellationToken = default);

    Task<창고작업결과응답?> 적재위치배정Async(
        long inboundItemId,
        적재위치배정요청 request,
        CancellationToken cancellationToken = default);

    Task<창고작업결과응답?> 포장작업Async(
        long inboundItemId,
        포장작업요청 request,
        CancellationToken cancellationToken = default);

    Task<화주운송의뢰응답?> 운송인계Async(
        재고운송의뢰생성요청 request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 기존 공동구매 화면과의 호환성을 위한 이름입니다.
/// 공동구매 전용 동작은 ViewModel 계층에서 추가하고 API 작업은 공통 계약을 사용합니다.
/// </summary>
public interface I공동구매창고Service : I입출고작업Service
{
}

public class 입출고작업Service(IHongdalJsonApiClient client) : I입출고작업Service
{
    private const string BasePath = "api/v1/warehouse-operations";
    private const string PerspectiveBasePath = "api/v1/warehouse-perspectives";

    public async Task<IReadOnlyList<창고요약응답>> 창고목록조회Async(
        CancellationToken cancellationToken = default)
        => (await client.GetAsync<창고목록응답>(
                $"{BasePath}/warehouses",
                "창고 목록 조회",
                cancellationToken: cancellationToken))?.Items
            ?? [];

    public Task<창고요약응답?> 창고생성Async(
        창고저장요청 request,
        CancellationToken cancellationToken = default)
        => client.SendAsync<창고저장요청, 창고요약응답>(
            HttpMethod.Post,
            $"{BasePath}/warehouses",
            request,
            "창고 생성",
            cancellationToken: cancellationToken);

    public Task<창고요약응답?> 창고수정Async(
        long warehouseId,
        창고저장요청 request,
        CancellationToken cancellationToken = default)
        => client.SendAsync<창고저장요청, 창고요약응답>(
            HttpMethod.Put,
            $"{BasePath}/warehouses/{warehouseId}",
            request,
            "창고 수정",
            cancellationToken: cancellationToken);

    public Task 창고삭제Async(long warehouseId, CancellationToken cancellationToken = default)
        => client.SendAsync(
            HttpMethod.Delete,
            $"{BasePath}/warehouses/{warehouseId}",
            "창고 삭제",
            cancellationToken);

    public async Task<IReadOnlyList<창고사용자항목응답>> 창고사용자목록조회Async(
        long warehouseId,
        CancellationToken cancellationToken = default)
        => (await client.GetAsync<창고사용자목록응답>(
                $"{BasePath}/warehouses/{warehouseId}/users",
                "창고 사용자 목록 조회",
                cancellationToken: cancellationToken))?.Items
            ?? [];

    public Task<창고사용자항목응답?> 창고사용자추가Async(
        long warehouseId,
        창고사용자저장요청 request,
        CancellationToken cancellationToken = default)
        => client.SendAsync<창고사용자저장요청, 창고사용자항목응답>(
            HttpMethod.Post,
            $"{BasePath}/warehouses/{warehouseId}/users",
            request,
            "창고 사용자 추가",
            cancellationToken: cancellationToken);

    public Task<창고사용자항목응답?> 창고사용자수정Async(
        long warehouseId,
        long warehouseUserId,
        창고사용자저장요청 request,
        CancellationToken cancellationToken = default)
        => client.SendAsync<창고사용자저장요청, 창고사용자항목응답>(
            HttpMethod.Put,
            $"{BasePath}/warehouses/{warehouseId}/users/{warehouseUserId}",
            request,
            "창고 사용자 수정",
            cancellationToken: cancellationToken);

    public Task 창고사용자삭제Async(
        long warehouseId,
        long warehouseUserId,
        CancellationToken cancellationToken = default)
        => client.SendAsync(
            HttpMethod.Delete,
            $"{BasePath}/warehouses/{warehouseId}/users/{warehouseUserId}",
            "창고 사용자 삭제",
            cancellationToken);

    public async Task<IReadOnlyList<입고요청항목응답>> 입고목록조회Async(
        CancellationToken cancellationToken = default)
        => (await client.GetAsync<입고요청목록응답>(
                $"{BasePath}/inbounds",
                "입고 요청 목록 조회",
                cancellationToken: cancellationToken))?.Items
            ?? [];

    public async Task<입고요청페이지응답> 입고목록조회Async(
        입고요청목록조회요청 request,
        CancellationToken cancellationToken = default)
        => await client.GetAsync<입고요청페이지응답>(
               BuildInboundQueryPath(request),
               "입고 요청 서버 목록 조회",
               cancellationToken: cancellationToken)
           ?? new 입고요청페이지응답
           {
               Page = Math.Max(0, request.Page),
               PageSize = Math.Clamp(request.PageSize, 1, 200)
           };

    public async Task<입고요청페이지응답> 입고예정관점목록조회Async(
        string perspectiveCode,
        string? communityLedgerId,
        입고요청목록조회요청 request,
        CancellationToken cancellationToken = default)
    {
        var queryPath = perspectiveCode switch
        {
            창고업무관점코드.주문자 => $"{PerspectiveBasePath}/inbounds/expected/orderer",
            창고업무관점코드.판매자 => $"{PerspectiveBasePath}/inbounds/expected/seller",
            창고업무관점코드.창고관리자 => $"{BasePath}/inbounds/query",
            창고업무관점코드.운송담당자 => $"{PerspectiveBasePath}/inbounds/expected/transport",
            창고업무관점코드.공동원장 when !string.IsNullOrWhiteSpace(communityLedgerId)
                => $"{PerspectiveBasePath}/inbounds/expected/community-ledgers/{Uri.EscapeDataString(communityLedgerId.Trim())}",
            창고업무관점코드.공동원장 => throw new ArgumentException("공동 원장 ID가 필요합니다.", nameof(communityLedgerId)),
            _ => throw new ArgumentOutOfRangeException(nameof(perspectiveCode), perspectiveCode, "지원하지 않는 입고 예정 관점입니다.")
        };

        return await client.GetAsync<입고요청페이지응답>(
                   BuildInboundQueryPath(request, queryPath),
                   "역할별 입고 예정 목록 조회",
                   cancellationToken: cancellationToken)
               ?? new 입고요청페이지응답
               {
                   Page = Math.Max(0, request.Page),
                   PageSize = Math.Clamp(request.PageSize, 1, 200)
               };
    }

    public async Task<출고예정페이지응답> 출고예정관점목록조회Async(
        string perspectiveCode,
        string? communityLedgerId,
        출고예정목록조회요청 request,
        CancellationToken cancellationToken = default)
    {
        var queryPath = perspectiveCode switch
        {
            창고업무관점코드.주문자 => $"{PerspectiveBasePath}/outbounds/expected/orderer",
            창고업무관점코드.판매자 => $"{PerspectiveBasePath}/outbounds/expected/seller",
            창고업무관점코드.창고관리자 => $"{PerspectiveBasePath}/outbounds/expected/warehouse",
            창고업무관점코드.운송담당자 => $"{PerspectiveBasePath}/outbounds/expected/transport",
            창고업무관점코드.공동원장 when !string.IsNullOrWhiteSpace(communityLedgerId)
                => $"{PerspectiveBasePath}/outbounds/expected/community-ledgers/{Uri.EscapeDataString(communityLedgerId.Trim())}",
            창고업무관점코드.공동원장 => throw new ArgumentException("공동 원장 ID가 필요합니다.", nameof(communityLedgerId)),
            _ => throw new ArgumentOutOfRangeException(nameof(perspectiveCode), perspectiveCode, "지원하지 않는 출고 예정 관점입니다.")
        };

        return await client.GetAsync<출고예정페이지응답>(
                   BuildOutboundQueryPath(request, queryPath),
                   "역할별 출고 예정 목록 조회",
                   cancellationToken: cancellationToken)
               ?? new 출고예정페이지응답
               {
                   Page = Math.Max(0, request.Page),
                   PageSize = Math.Clamp(request.PageSize, 1, 200)
               };
    }

    public Task<입고요청항목응답?> 입고요청생성Async(
        입고요청저장요청 request,
        CancellationToken cancellationToken = default)
        => client.SendAsync<입고요청저장요청, 입고요청항목응답>(
            HttpMethod.Post,
            $"{BasePath}/inbounds",
            request,
            "입고 요청 생성",
            cancellationToken: cancellationToken);

    public Task<입고요청항목응답?> 입고요청수정Async(
        long inboundId,
        입고요청저장요청 request,
        CancellationToken cancellationToken = default)
        => client.SendAsync<입고요청저장요청, 입고요청항목응답>(
            HttpMethod.Put,
            $"{BasePath}/inbounds/{inboundId}",
            request,
            "입고 요청 수정",
            cancellationToken: cancellationToken);

    public Task 입고요청취소Async(long inboundId, CancellationToken cancellationToken = default)
        => client.SendAsync(
            HttpMethod.Delete,
            $"{BasePath}/inbounds/{inboundId}",
            "입고 요청 취소",
            cancellationToken);

    public async Task<IReadOnlyList<입고상품항목응답>> 입고완료Async(
        long inboundId,
        입고완료요청 request,
        CancellationToken cancellationToken = default)
        => (await client.SendAsync<입고완료요청, 입고상품목록응답>(
                HttpMethod.Post,
                $"{BasePath}/inbounds/{inboundId}/complete",
                request,
                "입고 완료",
                cancellationToken: cancellationToken))?.Items
            ?? [];

    public async Task<IReadOnlyList<재고항목응답>> 재고목록조회Async(
        CancellationToken cancellationToken = default)
        => (await client.GetAsync<재고목록응답>(
                $"{BasePath}/inventory",
                "재고 목록 조회",
                cancellationToken: cancellationToken))?.Items
            ?? [];

    public Task<창고작업결과응답?> 입고검수Async(
        long inboundItemId,
        입고검수요청 request,
        CancellationToken cancellationToken = default)
        => client.SendAsync<입고검수요청, 창고작업결과응답>(
            HttpMethod.Post,
            $"{BasePath}/inventory/{inboundItemId}/inspect",
            request,
            "입고 검수",
            cancellationToken: cancellationToken);

    public Task<창고작업결과응답?> 적재위치배정Async(
        long inboundItemId,
        적재위치배정요청 request,
        CancellationToken cancellationToken = default)
        => client.SendAsync<적재위치배정요청, 창고작업결과응답>(
            HttpMethod.Post,
            $"{BasePath}/inventory/{inboundItemId}/put-away",
            request,
            "적재 위치 배정",
            cancellationToken: cancellationToken);

    public Task<창고작업결과응답?> 포장작업Async(
        long inboundItemId,
        포장작업요청 request,
        CancellationToken cancellationToken = default)
        => client.SendAsync<포장작업요청, 창고작업결과응답>(
            HttpMethod.Post,
            $"{BasePath}/inventory/{inboundItemId}/pack",
            request,
            "출고 포장",
            cancellationToken: cancellationToken);

    public Task<화주운송의뢰응답?> 운송인계Async(
        재고운송의뢰생성요청 request,
        CancellationToken cancellationToken = default)
        => client.SendAsync<재고운송의뢰생성요청, 화주운송의뢰응답>(
            HttpMethod.Post,
            $"{BasePath}/inventory/reconsignment",
            request,
            "출고 운송 인계",
            cancellationToken: cancellationToken);

    private static string BuildInboundQueryPath(
        입고요청목록조회요청 request,
        string? queryPath = null)
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
            values.Add($"warehouseId={request.WarehouseId.Value.ToString(CultureInfo.InvariantCulture)}");
        }

        return $"{queryPath ?? $"{BasePath}/inbounds/query"}?{string.Join('&', values)}";
    }

    private static string BuildOutboundQueryPath(
        출고예정목록조회요청 request,
        string queryPath)
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
        if (request.WarehouseId is > 0)
        {
            values.Add($"warehouseId={request.WarehouseId.Value.ToString(CultureInfo.InvariantCulture)}");
        }

        return $"{queryPath}?{string.Join('&', values)}";
    }

    private static void AddQueryValue(ICollection<string> values, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            values.Add($"{name}={Uri.EscapeDataString(value.Trim())}");
        }
    }
}

/// <summary>기존 공동구매 화면이 공통 입출고 API 구현을 사용하도록 유지하는 호환 형식입니다.</summary>
public sealed class 공동구매창고Service(IHongdalJsonApiClient client)
    : 입출고작업Service(client), I공동구매창고Service;
