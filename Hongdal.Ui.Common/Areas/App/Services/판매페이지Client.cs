using Hongdal.Contracts.Common.Sales;

namespace Hongdal.Ui.Common.Areas.App.Services;

public interface I판매페이지Client
{
    Task<IReadOnlyList<판매페이지초안응답>> 초안목록조회Async(CancellationToken cancellationToken = default);
    Task<판매페이지초안응답?> 초안조회Async(string pageId, CancellationToken cancellationToken = default);
    Task<판매페이지초안응답?> 초안생성Async(판매페이지초안생성요청 request, CancellationToken cancellationToken = default);
    Task<판매페이지초안응답?> 초안수정Async(string pageId, 판매페이지초안수정요청 request, CancellationToken cancellationToken = default);
}

public sealed class 판매페이지Client(IHongdalJsonApiClient client) : I판매페이지Client
{
    private const string BasePath = "api/v1/sales-channels/product-pages/drafts";

    public async Task<IReadOnlyList<판매페이지초안응답>> 초안목록조회Async(
        CancellationToken cancellationToken = default)
        => (await client.GetAsync<판매페이지초안목록응답>(
                BasePath,
                "판매 페이지 초안 목록 조회",
                cancellationToken: cancellationToken))?.Items
           ?? [];

    public Task<판매페이지초안응답?> 초안조회Async(
        string pageId,
        CancellationToken cancellationToken = default)
        => client.GetAsync<판매페이지초안응답>(
            $"{BasePath}/{Uri.EscapeDataString(pageId)}",
            "판매 페이지 초안 조회",
            cancellationToken: cancellationToken);

    public Task<판매페이지초안응답?> 초안생성Async(
        판매페이지초안생성요청 request,
        CancellationToken cancellationToken = default)
        => client.SendAsync<판매페이지초안생성요청, 판매페이지초안응답>(
            HttpMethod.Post,
            BasePath,
            request,
            "판매 페이지 초안 생성",
            cancellationToken: cancellationToken);

    public Task<판매페이지초안응답?> 초안수정Async(
        string pageId,
        판매페이지초안수정요청 request,
        CancellationToken cancellationToken = default)
        => client.SendAsync<판매페이지초안수정요청, 판매페이지초안응답>(
            HttpMethod.Put,
            $"{BasePath}/{Uri.EscapeDataString(pageId)}",
            request,
            "판매 페이지 초안 수정",
            cancellationToken: cancellationToken);
}
