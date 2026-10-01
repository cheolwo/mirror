using Hongdal.Contracts.Common.Community;

namespace Hongdal.Ui.Common.Areas.App.Services;

public interface I개별주문관점Service
{
    Task<개별주문관점페이지응답> 목록조회Async(
        string perspectiveCode,
        string? communityLedgerId,
        개별주문관점목록조회요청 request,
        CancellationToken cancellationToken = default);
}

public sealed class 개별주문관점Service(IHongdalJsonApiClient client) : I개별주문관점Service
{
    private const string BasePath = "api/v1/order-perspectives/individual-orders";

    public async Task<개별주문관점페이지응답> 목록조회Async(
        string perspectiveCode,
        string? communityLedgerId,
        개별주문관점목록조회요청 request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var path = perspectiveCode switch
        {
            개별주문관점코드.주문자 => $"{BasePath}/orderer",
            개별주문관점코드.판매자 => $"{BasePath}/seller",
            개별주문관점코드.창고관리자 => $"{BasePath}/warehouse",
            개별주문관점코드.운송담당자 => $"{BasePath}/transport",
            개별주문관점코드.공동원장 when !string.IsNullOrWhiteSpace(communityLedgerId)
                => $"{BasePath}/community-ledgers/{Uri.EscapeDataString(communityLedgerId.Trim())}",
            개별주문관점코드.공동원장 => throw new InvalidOperationException("조회할 공동 원장을 선택해 주세요."),
            _ => throw new InvalidOperationException($"지원하지 않는 개별 주문 관점입니다: {perspectiveCode}")
        };

        var query = new List<string>
        {
            $"page={Math.Max(0, request.Page)}",
            $"pageSize={Math.Clamp(request.PageSize, 1, 100)}",
            $"sortDescending={request.SortDescending.ToString().ToLowerInvariant()}"
        };
        Add(query, "search", request.Search);
        Add(query, "status", request.Status);
        Add(query, "sortBy", request.SortBy);

        return await client.GetAsync<개별주문관점페이지응답>(
                   $"{path}?{string.Join("&", query)}",
                   "역할별 개별 주문 목록 조회",
                   cancellationToken: cancellationToken)
               ?? new 개별주문관점페이지응답
               {
                   Page = Math.Max(0, request.Page),
                   PageSize = Math.Clamp(request.PageSize, 1, 100)
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
