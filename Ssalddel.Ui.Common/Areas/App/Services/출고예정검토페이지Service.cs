using System.Globalization;
using Ssalddel.Contracts.Common.Inventory;

namespace Ssalddel.Ui.Common.Areas.App.Services;

public interface I출고예정검토페이지Service
{
    Task<출고예정검토목록페이지응답> 목록조회Async(
        출고예정검토목록조회요청 request,
        CancellationToken cancellationToken = default);

    Task<출고예정검토상세응답?> 상세조회Async(
        long outboundPlanId,
        CancellationToken cancellationToken = default);

    Task<출고운송인계완료응답> 인계완료Async(
        long outboundPlanId,
        출고운송인계완료요청 request,
        CancellationToken cancellationToken = default);
}

public sealed class 출고예정검토페이지Service(ISsalddelJsonApiClient client) : I출고예정검토페이지Service
{
    private const string BasePath = "api/v1/warehouse-operations/outbound-plan-reviews";

    public async Task<출고예정검토목록페이지응답> 목록조회Async(
        출고예정검토목록조회요청 request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var page = Math.Max(0, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 50);
        var query = new List<string>
        {
            $"status={Uri.EscapeDataString(출고예정검토조회상태코드.Normalize(request.Status))}",
            $"page={page.ToString(CultureInfo.InvariantCulture)}",
            $"pageSize={pageSize.ToString(CultureInfo.InvariantCulture)}"
        };
        if (request.WarehouseId is > 0)
            query.Add($"warehouseId={request.WarehouseId.Value.ToString(CultureInfo.InvariantCulture)}");
        if (request.PeriodStartUtc is { } start)
            query.Add($"periodStartUtc={Uri.EscapeDataString(start.ToString("O", CultureInfo.InvariantCulture))}");
        if (request.PeriodEndUtc is { } end)
            query.Add($"periodEndUtc={Uri.EscapeDataString(end.ToString("O", CultureInfo.InvariantCulture))}");
        if (request.PeriodStartUtc.HasValue && request.PeriodEndUtc.HasValue && request.PeriodStage is { Length: > 0 } stage)
        {
            query.Add($"periodStage={Uri.EscapeDataString(stage)}");
            query.Add($"batchPage={Math.Max(0, request.BatchPage).ToString(CultureInfo.InvariantCulture)}");
            query.Add($"batchPageSize={Math.Clamp(request.BatchPageSize, 1, 50).ToString(CultureInfo.InvariantCulture)}");
            query.Add($"memberPage={Math.Max(0, request.MemberPage).ToString(CultureInfo.InvariantCulture)}");
            query.Add($"memberPageSize={Math.Clamp(request.MemberPageSize, 1, 50).ToString(CultureInfo.InvariantCulture)}");
            if (!string.IsNullOrWhiteSpace(request.BatchKey)) query.Add($"batchKey={Uri.EscapeDataString(request.BatchKey)}");
        }
        if (!string.IsNullOrWhiteSpace(request.Search))
            query.Add($"search={Uri.EscapeDataString(request.Search.Trim())}");

        return await client.GetAsync<출고예정검토목록페이지응답>(
                   $"{BasePath}?{string.Join("&", query)}",
                   "출고예정 운송 전 검토 목록 조회",
                   allowNotFound: false,
                   cancellationToken: cancellationToken)
               ?? new 출고예정검토목록페이지응답 { Page = page, PageSize = pageSize };
    }

    public Task<출고예정검토상세응답?> 상세조회Async(
        long outboundPlanId,
        CancellationToken cancellationToken = default)
        => client.GetAsync<출고예정검토상세응답>(
            $"{BasePath}/{outboundPlanId.ToString(CultureInfo.InvariantCulture)}",
            "출고예정 운송 전 검토 상세 조회",
            cancellationToken: cancellationToken);

    public async Task<출고운송인계완료응답> 인계완료Async(
        long outboundPlanId,
        출고운송인계완료요청 request,
        CancellationToken cancellationToken = default)
        => await client.SendAsync<출고운송인계완료요청, 출고운송인계완료응답>(
               HttpMethod.Post,
               $"{BasePath}/{outboundPlanId.ToString(CultureInfo.InvariantCulture)}/handoff-complete",
               request,
               "출고 운송 인계 완료",
               cancellationToken: cancellationToken)
           ?? throw new InvalidOperationException("출고 운송 인계 완료 응답이 비어 있습니다.");
}
