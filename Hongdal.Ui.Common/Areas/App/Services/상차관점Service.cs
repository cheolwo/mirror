using System.Globalization;
using Hongdal.Contracts.Common.VehicleLoading;

namespace Hongdal.Ui.Common.Areas.App.Services;

public interface I상차관점Service
{
    Task<상차관점페이지응답> 목록조회Async(
        string perspectiveCode,
        string? communityLedgerId,
        상차관점목록조회요청 request,
        CancellationToken cancellationToken = default);
}

public sealed class 상차관점Service(IHongdalJsonApiClient client) : I상차관점Service
{
    private const string BasePath = "api/v1/loading-perspectives";

    public async Task<상차관점페이지응답> 목록조회Async(
        string perspectiveCode,
        string? communityLedgerId,
        상차관점목록조회요청 request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var path = perspectiveCode switch
        {
            상차업무관점코드.주문자 => $"{BasePath}/orderer",
            상차업무관점코드.판매자 => $"{BasePath}/seller",
            상차업무관점코드.창고관리자 => $"{BasePath}/warehouse",
            상차업무관점코드.운송담당자 => $"{BasePath}/transport",
            상차업무관점코드.공동원장 when !string.IsNullOrWhiteSpace(communityLedgerId)
                => $"{BasePath}/community-ledgers/{Uri.EscapeDataString(communityLedgerId.Trim())}",
            상차업무관점코드.공동원장 => throw new InvalidOperationException("조회할 공동 원장을 선택해 주세요."),
            _ => throw new InvalidOperationException($"지원하지 않는 상차 관점입니다: {perspectiveCode}")
        };

        var query = new List<string>
        {
            $"page={Math.Max(0, request.Page).ToString(CultureInfo.InvariantCulture)}",
            $"pageSize={Math.Clamp(request.PageSize, 1, 200).ToString(CultureInfo.InvariantCulture)}",
            $"sortDescending={request.SortDescending.ToString().ToLowerInvariant()}"
        };
        Add(query, "search", request.Search);
        Add(query, "status", request.Status);
        Add(query, "sortBy", request.SortBy);
        if (request.WarehouseId is > 0)
        {
            query.Add($"warehouseId={request.WarehouseId.Value.ToString(CultureInfo.InvariantCulture)}");
        }

        return await client.GetAsync<상차관점페이지응답>(
                   $"{path}?{string.Join('&', query)}",
                   "역할별 상차 목록 조회",
                   cancellationToken: cancellationToken)
               ?? new 상차관점페이지응답
               {
                   Page = Math.Max(0, request.Page),
                   PageSize = Math.Clamp(request.PageSize, 1, 200)
               };
    }

    private static void Add(ICollection<string> query, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            query.Add($"{name}={Uri.EscapeDataString(value.Trim())}");
        }
    }
}
