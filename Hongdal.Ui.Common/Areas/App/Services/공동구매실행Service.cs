using Hongdal.Contracts.Common.Community;
using Hongdal.Contracts.Common.Orderer;

namespace Hongdal.Ui.Common.Areas.App.Services;

/// <summary>
/// 공동구매와 무관하게 주문 원장의 조회, 하위 원장 구성과 서명을 제공하는 기본 업무 경계입니다.
/// </summary>
public interface I주문원장Service
{
    Task<주문원장역할별조회공개Dto?> 주문원장보호조회Async(
        string orderLedgerId,
        CancellationToken cancellationToken = default);

    Task<주문원장역할별조회공개Dto?> 주문원장역할조회Async(
        string orderLedgerId,
        string viewCode,
        CancellationToken cancellationToken = default);

    Task<주문원장통합공개Dto?> 하위원장연결Async(
        string orderLedgerId,
        주문하위원장연결ClientRequest request,
        CancellationToken cancellationToken = default);

    Task<주문원장통합공개Dto?> 하위원장분리Async(
        string orderLedgerId,
        string childLedgerId,
        long? expectedRevision = null,
        CancellationToken cancellationToken = default);

    Task<주문원장서명상태공개Dto?> 주문원장서명상태조회Async(
        string orderLedgerId,
        CancellationToken cancellationToken = default);

    Task<주문원장서명상태공개Dto?> 주문원장서명준비Async(
        string orderLedgerId,
        주문원장서명준비ClientRequest request,
        CancellationToken cancellationToken = default);

    Task<주문원장서명상태공개Dto?> 주문원장서명등록Async(
        string orderLedgerId,
        주문원장서명등록ClientRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 공동구매 확정 이후 자동집단, 기본 주문원장 업무와 커머스 이행을 연결하는 API 경계입니다.
/// </summary>
public interface I공동구매실행Service : I주문원장Service
{
    Task<IReadOnlyList<공동구매자동집단응답>> 자동집단목록조회Async(
        공동구매자동집단조회조건 condition,
        CancellationToken cancellationToken = default);

    Task<공동구매자동집단응답?> 자동수요등록Async(
        공동구매자동수요등록Command request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<공동구매커머스이행계획공개Dto>> 공동구매별커머스이행조회Async(
        string groupPurchaseId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<공동구매커머스이행계획공개Dto>> 문서번호로커머스이행조회Async(
        string documentManagementNumber,
        CancellationToken cancellationToken = default);
}

public sealed class 공동구매실행Service(IHongdalJsonApiClient client) : I공동구매실행Service
{
    private const string AutoGroupsPath = "api/v1/orderer/group-purchase-auto-groups";
    private const string OrderLedgersPath = "api/v1/community/order-ledgers";
    private const string CommercePath = "api/v1/orderer/group-purchase-commerce-fulfillment-plans";

    public async Task<IReadOnlyList<공동구매자동집단응답>> 자동집단목록조회Async(
        공동구매자동집단조회조건 condition,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(condition);
        var query = new List<string>();
        AddQuery(query, "productKey", condition.상품키);
        AddQuery(query, "deliveryScopeKey", condition.배송권키);
        AddQuery(query, "currentStatus", condition.현재상태);
        var path = query.Count == 0 ? AutoGroupsPath : $"{AutoGroupsPath}?{string.Join("&", query)}";

        return await client.GetAsync<IReadOnlyList<공동구매자동집단응답>>(
                   path,
                   "공동구매 자동집단 목록 조회",
                   cancellationToken: cancellationToken)
               ?? [];
    }

    public Task<공동구매자동집단응답?> 자동수요등록Async(
        공동구매자동수요등록Command request,
        CancellationToken cancellationToken = default)
        => client.SendAsync<공동구매자동수요등록Command, 공동구매자동집단응답>(
            HttpMethod.Post,
            $"{AutoGroupsPath}/demands",
            request,
            "공동구매 자동집단 수요 등록",
            cancellationToken: cancellationToken);

    public Task<주문원장역할별조회공개Dto?> 주문원장보호조회Async(
        string orderLedgerId,
        CancellationToken cancellationToken = default)
        => client.GetAsync<주문원장역할별조회공개Dto>(
            $"{OrderLedgersPath}/{Segment(orderLedgerId)}",
            "주문 원장 주문자 보호형 조회",
            cancellationToken: cancellationToken);

    public Task<주문원장역할별조회공개Dto?> 주문원장역할조회Async(
        string orderLedgerId,
        string viewCode,
        CancellationToken cancellationToken = default)
        => client.GetAsync<주문원장역할별조회공개Dto>(
            $"{OrderLedgersPath}/{Segment(orderLedgerId)}/views/{Segment(viewCode)}",
            "주문 원장 역할별 조회",
            cancellationToken: cancellationToken);

    public Task<주문원장통합공개Dto?> 하위원장연결Async(
        string orderLedgerId,
        주문하위원장연결ClientRequest request,
        CancellationToken cancellationToken = default)
        => client.SendAsync<주문하위원장연결ClientRequest, 주문원장통합공개Dto>(
            HttpMethod.Post,
            $"{OrderLedgersPath}/{Segment(orderLedgerId)}/children",
            request,
            "주문 하위 원장 연결",
            cancellationToken: cancellationToken);

    public Task<주문원장통합공개Dto?> 하위원장분리Async(
        string orderLedgerId,
        string childLedgerId,
        long? expectedRevision = null,
        CancellationToken cancellationToken = default)
    {
        var path = $"{OrderLedgersPath}/{Segment(orderLedgerId)}/children/{Segment(childLedgerId)}";
        if (expectedRevision is not null)
        {
            path += $"?{Uri.EscapeDataString("기대Revision")}={expectedRevision.Value}";
        }

        return client.SendAsync<주문원장통합공개Dto>(
            HttpMethod.Delete,
            path,
            "주문 하위 원장 분리",
            cancellationToken: cancellationToken);
    }

    public Task<주문원장서명상태공개Dto?> 주문원장서명상태조회Async(
        string orderLedgerId,
        CancellationToken cancellationToken = default)
        => client.GetAsync<주문원장서명상태공개Dto>(
            $"{OrderLedgersPath}/{Segment(orderLedgerId)}/signature",
            "주문 원장 서명 상태 조회",
            cancellationToken: cancellationToken);

    public Task<주문원장서명상태공개Dto?> 주문원장서명준비Async(
        string orderLedgerId,
        주문원장서명준비ClientRequest request,
        CancellationToken cancellationToken = default)
        => client.SendAsync<주문원장서명준비ClientRequest, 주문원장서명상태공개Dto>(
            HttpMethod.Post,
            $"{OrderLedgersPath}/{Segment(orderLedgerId)}/signature-request",
            request,
            "주문 원장 서명 요청 준비",
            cancellationToken: cancellationToken);

    public Task<주문원장서명상태공개Dto?> 주문원장서명등록Async(
        string orderLedgerId,
        주문원장서명등록ClientRequest request,
        CancellationToken cancellationToken = default)
        => client.SendAsync<주문원장서명등록ClientRequest, 주문원장서명상태공개Dto>(
            HttpMethod.Post,
            $"{OrderLedgersPath}/{Segment(orderLedgerId)}/signatures",
            request,
            "주문 원장 서명 등록",
            cancellationToken: cancellationToken);

    public async Task<IReadOnlyList<공동구매커머스이행계획공개Dto>> 공동구매별커머스이행조회Async(
        string groupPurchaseId,
        CancellationToken cancellationToken = default)
        => await client.GetAsync<IReadOnlyList<공동구매커머스이행계획공개Dto>>(
               $"{CommercePath}/by-group-purchase/{Segment(groupPurchaseId)}",
               "공동구매별 커머스 이행 조회",
               cancellationToken: cancellationToken)
           ?? [];

    public async Task<IReadOnlyList<공동구매커머스이행계획공개Dto>> 문서번호로커머스이행조회Async(
        string documentManagementNumber,
        CancellationToken cancellationToken = default)
        => await client.GetAsync<IReadOnlyList<공동구매커머스이행계획공개Dto>>(
               $"{CommercePath}/lookup?documentManagementNumber={Uri.EscapeDataString(documentManagementNumber.Trim())}",
               "문서관리번호 커머스 이행 조회",
               allowNotFound: true,
               cancellationToken: cancellationToken)
           ?? [];

    private static string Segment(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return Uri.EscapeDataString(value.Trim());
    }

    private static void AddQuery(ICollection<string> query, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            query.Add($"{name}={Uri.EscapeDataString(value.Trim())}");
        }
    }
}
