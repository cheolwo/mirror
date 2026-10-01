using Hongdal.Contracts.Common.Orderer;

namespace Hongdal.Ui.Common.Areas.App.Services;

public interface I공동수입선적통관Client
{
    Task<공동구매해외선적공개Dto?> 공개조회Async(
        string documentManagementNumber,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<공동구매해외선적추적Dto>?> 관리자목록Async(
        공동구매해외선적추적조회조건 condition,
        CancellationToken cancellationToken = default);

    Task<공동구매해외선적추적Dto?> 관리자저장Async(
        공동구매해외선적추적저장요청 request,
        CancellationToken cancellationToken = default);

    Task<공동구매해외선적추적Dto?> 관리자이벤트추가Async(
        string documentManagementNumber,
        공동구매해외선적추적이벤트추가요청 request,
        CancellationToken cancellationToken = default);

    Task<공동구매해외선적통관동기화결과?> 관리자통관동기화Async(
        공동구매해외선적통관동기화요청 request,
        CancellationToken cancellationToken = default);
}

public sealed class 공동수입선적통관Client(IHongdalJsonApiClient client) : I공동수입선적통관Client
{
    private const string PublicBasePath = "api/v1/orderer/group-purchase-overseas-shipments";
    private const string AdminBasePath = "api/v1/admin/orderer/group-purchase-overseas-shipments";

    public Task<공동구매해외선적공개Dto?> 공개조회Async(
        string documentManagementNumber,
        CancellationToken cancellationToken = default)
        => client.GetAsync<공동구매해외선적공개Dto>(
            $"{PublicBasePath}/lookup?documentManagementNumber={Encode(documentManagementNumber)}",
            "공동수입 선적 공개 조회",
            cancellationToken: cancellationToken);

    public Task<IReadOnlyList<공동구매해외선적추적Dto>?> 관리자목록Async(
        공동구매해외선적추적조회조건 condition,
        CancellationToken cancellationToken = default)
        => client.GetAsync<IReadOnlyList<공동구매해외선적추적Dto>>(
            Query(AdminBasePath,
                ("groupPurchaseId", condition.공동구매Id),
                ("ordererGroupScopeKey", condition.주문자집단배송권키),
                ("documentManagementNumber", condition.문서관리번호),
                ("transportDocumentNumber", condition.운송문서번호),
                ("currentStatusCode", condition.현재상태코드)),
            "공동수입 선적 관리자 목록 조회",
            allowNotFound: false,
            cancellationToken);

    public Task<공동구매해외선적추적Dto?> 관리자저장Async(
        공동구매해외선적추적저장요청 request,
        CancellationToken cancellationToken = default)
        => client.SendAsync<공동구매해외선적추적저장요청, 공동구매해외선적추적Dto>(
            HttpMethod.Post,
            AdminBasePath,
            request,
            "공동수입 해외 선적 저장",
            cancellationToken: cancellationToken);

    public Task<공동구매해외선적추적Dto?> 관리자이벤트추가Async(
        string documentManagementNumber,
        공동구매해외선적추적이벤트추가요청 request,
        CancellationToken cancellationToken = default)
        => client.SendAsync<공동구매해외선적추적이벤트추가요청, 공동구매해외선적추적Dto>(
            HttpMethod.Post,
            $"{AdminBasePath}/events?documentManagementNumber={Encode(documentManagementNumber)}",
            request,
            "공동수입 선적 이벤트 추가",
            cancellationToken: cancellationToken);

    public Task<공동구매해외선적통관동기화결과?> 관리자통관동기화Async(
        공동구매해외선적통관동기화요청 request,
        CancellationToken cancellationToken = default)
        => client.SendAsync<공동구매해외선적통관동기화요청, 공동구매해외선적통관동기화결과>(
            HttpMethod.Post,
            $"{AdminBasePath}/customs-sync",
            request,
            "공동수입 통관 상태 동기화",
            cancellationToken: cancellationToken);

    private static string Query(string path, params (string Key, string? Value)[] values)
    {
        var query = values
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Value))
            .Select(pair => $"{pair.Key}={Encode(pair.Value!)}")
            .ToArray();
        return query.Length == 0 ? path : $"{path}?{string.Join('&', query)}";
    }

    private static string Encode(string value) => Uri.EscapeDataString(value.Trim());
}
