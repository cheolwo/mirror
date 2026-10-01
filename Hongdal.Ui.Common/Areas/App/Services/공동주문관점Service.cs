using Hongdal.Contracts.Common.Community;

namespace Hongdal.Ui.Common.Areas.App.Services;

public interface I공동주문관점Service
{
    Task<공동주문관점페이지응답> 목록조회Async(
        string perspectiveCode,
        string? communityLedgerId,
        공동주문관점목록조회요청 request,
        CancellationToken cancellationToken = default);
}

public sealed class 공동주문관점Service(IHongdalJsonApiClient client) : I공동주문관점Service
{
    private const string BasePath = "api/v1/order-perspectives/group-orders";

    public async Task<공동주문관점페이지응답> 목록조회Async(
        string perspectiveCode,
        string? communityLedgerId,
        공동주문관점목록조회요청 request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var path = perspectiveCode switch
        {
            공동주문관점코드.주문자 => $"{BasePath}/orderer",
            공동주문관점코드.판매자 => $"{BasePath}/seller",
            공동주문관점코드.창고관리자 => $"{BasePath}/warehouse",
            공동주문관점코드.운송담당자 => $"{BasePath}/transport",
            공동주문관점코드.공동원장 when !string.IsNullOrWhiteSpace(communityLedgerId)
                => $"{BasePath}/community-ledgers/{Uri.EscapeDataString(communityLedgerId.Trim())}",
            공동주문관점코드.공동원장 => throw new InvalidOperationException("조회할 공동 원장을 선택해 주세요."),
            _ => throw new InvalidOperationException($"지원하지 않는 공동주문 관점입니다: {perspectiveCode}")
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

        return await client.GetAsync<공동주문관점페이지응답>(
                   $"{path}?{string.Join("&", query)}",
                   "역할별 공동주문 목록 조회",
                   cancellationToken: cancellationToken)
               ?? new 공동주문관점페이지응답
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
