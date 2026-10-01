using Hongdal.Contracts.Common.Sales;

namespace Hongdal.Ui.Common.Areas.App.Services;

/// <summary>판매채널 계정을 연결하고 조회하는 기본 업무 경계입니다.</summary>
public interface I판매채널계정Service
{
    Task<IReadOnlyList<판매채널계정항목응답>> 계정목록조회Async(
        CancellationToken cancellationToken = default);

    Task<판매채널계정항목응답?> 계정생성Async(
        판매채널계정저장요청 request,
        CancellationToken cancellationToken = default);

    Task<판매채널계정항목응답?> 계정수정Async(
        long accountId,
        판매채널계정저장요청 request,
        CancellationToken cancellationToken = default);

    Task 계정삭제Async(long accountId, CancellationToken cancellationToken = default);
}

/// <summary>입고상품을 판매 가능한 상품으로 등록하는 기본 업무 경계입니다.</summary>
public interface I상품등록Service
{
    Task<IReadOnlyList<판매상품항목응답>> 상품목록조회Async(
        CancellationToken cancellationToken = default);

    Task<판매상품항목응답?> 상품생성Async(
        판매상품저장요청 request,
        CancellationToken cancellationToken = default);

    Task<판매상품항목응답?> 상품수정Async(
        long productId,
        판매상품저장요청 request,
        CancellationToken cancellationToken = default);

    Task 상품삭제Async(long productId, CancellationToken cancellationToken = default);
}

/// <summary>등록한 판매상품을 판매채널에 출품하는 기본 업무 경계입니다.</summary>
public interface I채널출품Service
{
    Task<IReadOnlyList<채널출품항목응답>> 출품목록조회Async(
        CancellationToken cancellationToken = default);

    Task<채널출품항목응답?> 출품생성Async(
        채널출품저장요청 request,
        CancellationToken cancellationToken = default);

    Task<채널출품항목응답?> 출품수정Async(
        long listingId,
        채널출품저장요청 request,
        CancellationToken cancellationToken = default);

    Task 출품삭제Async(long listingId, CancellationToken cancellationToken = default);
}

/// <summary>
/// 판매의 기본 업무 경계를 한 API 클라이언트로 제공하는 조합 계약입니다.
/// 특정 화면은 필요한 작은 업무 계약만 주입받을 수 있습니다.
/// </summary>
public interface I판매채널Client : I판매채널계정Service, I상품등록Service, I채널출품Service
{
}

public sealed class 판매채널Client(IHongdalJsonApiClient client) : I판매채널Client
{
    private const string BasePath = "api/v1/sales-channels";

    public async Task<IReadOnlyList<판매채널계정항목응답>> 계정목록조회Async(
        CancellationToken cancellationToken = default)
        => (await client.GetAsync<판매채널계정목록응답>(
                $"{BasePath}/accounts",
                "판매채널 계정 목록 조회",
                cancellationToken: cancellationToken))?.Items
           ?? [];

    public Task<판매채널계정항목응답?> 계정생성Async(
        판매채널계정저장요청 request,
        CancellationToken cancellationToken = default)
        => client.SendAsync<판매채널계정저장요청, 판매채널계정항목응답>(
            HttpMethod.Post,
            $"{BasePath}/accounts",
            request,
            "판매채널 계정 생성",
            cancellationToken: cancellationToken);

    public Task<판매채널계정항목응답?> 계정수정Async(
        long accountId,
        판매채널계정저장요청 request,
        CancellationToken cancellationToken = default)
        => client.SendAsync<판매채널계정저장요청, 판매채널계정항목응답>(
            HttpMethod.Put,
            $"{BasePath}/accounts/{accountId}",
            request,
            "판매채널 계정 수정",
            cancellationToken: cancellationToken);

    public Task 계정삭제Async(long accountId, CancellationToken cancellationToken = default)
        => client.SendAsync(
            HttpMethod.Delete,
            $"{BasePath}/accounts/{accountId}",
            "판매채널 계정 삭제",
            cancellationToken);

    public async Task<IReadOnlyList<판매상품항목응답>> 상품목록조회Async(
        CancellationToken cancellationToken = default)
        => (await client.GetAsync<판매상품목록응답>(
                $"{BasePath}/products",
                "판매상품 목록 조회",
                cancellationToken: cancellationToken))?.Items
           ?? [];

    public Task<판매상품항목응답?> 상품생성Async(
        판매상품저장요청 request,
        CancellationToken cancellationToken = default)
        => client.SendAsync<판매상품저장요청, 판매상품항목응답>(
            HttpMethod.Post,
            $"{BasePath}/products",
            request,
            "판매상품 생성",
            cancellationToken: cancellationToken);

    public Task<판매상품항목응답?> 상품수정Async(
        long productId,
        판매상품저장요청 request,
        CancellationToken cancellationToken = default)
        => client.SendAsync<판매상품저장요청, 판매상품항목응답>(
            HttpMethod.Put,
            $"{BasePath}/products/{productId}",
            request,
            "판매상품 수정",
            cancellationToken: cancellationToken);

    public Task 상품삭제Async(long productId, CancellationToken cancellationToken = default)
        => client.SendAsync(
            HttpMethod.Delete,
            $"{BasePath}/products/{productId}",
            "판매상품 삭제",
            cancellationToken);

    public async Task<IReadOnlyList<채널출품항목응답>> 출품목록조회Async(
        CancellationToken cancellationToken = default)
        => (await client.GetAsync<채널출품목록응답>(
                $"{BasePath}/listings",
                "판매채널 출품 목록 조회",
                cancellationToken: cancellationToken))?.Items
           ?? [];

    public Task<채널출품항목응답?> 출품생성Async(
        채널출품저장요청 request,
        CancellationToken cancellationToken = default)
        => client.SendAsync<채널출품저장요청, 채널출품항목응답>(
            HttpMethod.Post,
            $"{BasePath}/listings",
            request,
            "판매채널 출품 생성",
            cancellationToken: cancellationToken);

    public Task<채널출품항목응답?> 출품수정Async(
        long listingId,
        채널출품저장요청 request,
        CancellationToken cancellationToken = default)
        => client.SendAsync<채널출품저장요청, 채널출품항목응답>(
            HttpMethod.Put,
            $"{BasePath}/listings/{listingId}",
            request,
            "판매채널 출품 수정",
            cancellationToken: cancellationToken);

    public Task 출품삭제Async(long listingId, CancellationToken cancellationToken = default)
        => client.SendAsync(
            HttpMethod.Delete,
            $"{BasePath}/listings/{listingId}",
            "판매채널 출품 삭제",
            cancellationToken);
}
