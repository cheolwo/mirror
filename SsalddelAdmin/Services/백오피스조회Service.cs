using System.Net.Http.Headers;
using System.Net.Http.Json;
using Ssalddel.Client.Infrastructure.Transport;
using Ssalddel.Contracts.Admin.Transport;
using Ssalddel.Contracts.CommonContents;
using SharedShipperRequest = Ssalddel.Contracts.Shipper.Request.화주운송의뢰응답;

namespace SsalddelAdmin.Services;

public sealed partial class 백오피스조회Service : I백오피스Service
{
    private readonly HttpClient _httpClient;
    private readonly 관리자인증세션Service _session;
    private readonly ITransportRequestLedgerObserver _ledgerObserver;

    public 백오피스조회Service(
        HttpClient httpClient,
        관리자인증세션Service session,
        ITransportRequestLedgerObserver ledgerObserver)
    {
        _httpClient = httpClient;
        _session = session;
        _ledgerObserver = ledgerObserver;
    }

    public async Task<관리자대시보드요약응답> 대시보드조회Async(CancellationToken cancellationToken = default)
    {
        return await 서버단건조회Async<관리자대시보드요약응답>(
            "api/v1/admin/dashboard",
            cancellationToken);
    }

    public async Task<IReadOnlyList<화주운송의뢰응답>> 의뢰목록조회Async(string? 결제상태 = null, string? 배차상태 = null, CancellationToken cancellationToken = default)
    {
        var query = BuildQuery(
            "api/v1/shipper/requests",
            ("paymentStatus", 결제상태),
            ("dispatchStatus", 배차상태),
            ("pageSize", "200"));

        var source = await 서버목록조회Async<SharedShipperRequest>(
            query,
            cancellationToken);
        var items = source.Select(AdminShipperRequestProjection.ToView).ToArray();
        foreach (var item in items)
        {
            Observe(item, "SsalddelAdmin.RequestList");
        }

        return items;
    }

    public async Task<IReadOnlyList<공개화물요약응답>> 공개화물요약조회Async(CancellationToken cancellationToken = default)
    {
        return await 서버목록조회Async<공개화물요약응답>(
            "api/v1/shipper/requests/public?pageSize=200",
            cancellationToken);
    }

    public async Task<화주운송의뢰응답?> 의뢰상세조회Async(string requestId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(requestId))
        {
            return null;
        }

        var source = await 서버단건조회Async<SharedShipperRequest>(
            $"api/v1/shipper/requests/{Uri.EscapeDataString(requestId.Trim())}",
            cancellationToken);
        var item = AdminShipperRequestProjection.ToView(source);
        Observe(item, "SsalddelAdmin.RequestDetail");
        return item;
    }

    public async Task 의뢰취소환불처리Async(
        string requestId,
        string 확인의뢰Id,
        string 사유,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(requestId))
        {
            throw new ArgumentException("의뢰 ID가 필요합니다.", nameof(requestId));
        }

        ApplyAuthorizationHeader();

        var response = await _httpClient.PostAsJsonAsync(
            $"api/v1/shipper/requests/{Uri.EscapeDataString(requestId.Trim())}/admin/cancel-refund",
            new 관리자운송의뢰취소환불요청
            {
                확인의뢰Id = 확인의뢰Id,
                사유 = 사유
            },
            cancellationToken);

        response.EnsureSuccessStatusCode();
        var source = await response.Content.ReadFromJsonAsync<SharedShipperRequest>(cancellationToken: cancellationToken);
        if (source is not null)
        {
            var item = AdminShipperRequestProjection.ToView(source);
            Observe(item, "SsalddelAdmin.RequestCancelRefund");
            _ledgerObserver.RequestRefresh(item.의뢰Id, "SsalddelAdmin.RequestCancelRefund");
        }
    }

    public async Task<IReadOnlyList<결제목록응답>> 결제목록조회Async(string? 결제상태 = null, string? 의뢰Id = null, CancellationToken cancellationToken = default)
    {
        var query = BuildQuery("api/v1/payments", ("결제상태", 결제상태), ("의뢰Id", 의뢰Id));
        return await 서버목록조회Async<결제목록응답>(
            query,
            cancellationToken);
    }

    public async Task<토스결제환경응답> 토스결제환경조회Async(CancellationToken cancellationToken = default)
    {
        return await 서버단건조회Async<토스결제환경응답>(
            "api/v1/payments/toss/config",
            cancellationToken);
    }

    public async Task<IReadOnlyList<배차대기응답>> 배차대기목록조회Async(CancellationToken cancellationToken = default)
    {
        return await 서버목록조회Async<배차대기응답>(
            "api/v1/dispatch/wait",
            cancellationToken);
    }

    public async Task<배차대기응답?> 배차대기상태변경Async(long id, string status, CancellationToken cancellationToken = default)
    {
        ApplyAuthorizationHeader();

        var current = await _httpClient.GetFromJsonAsync<배차대기응답>($"api/v1/dispatch/wait/{id}", cancellationToken);
        if (current is null)
        {
            return null;
        }

        current.상태 = status;
        var response = await _httpClient.PutAsJsonAsync($"api/v1/dispatch/wait/{id}", current, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<배차대기응답>(cancellationToken: cancellationToken);
    }

    public async Task 배차대기삭제Async(long id, CancellationToken cancellationToken = default)
    {
        ApplyAuthorizationHeader();

        var response = await _httpClient.DeleteAsync($"api/v1/dispatch/wait/{id}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<IReadOnlyList<기사목록응답>> 기사목록조회Async(string? 운행상태 = null, string? 활동지역검색어 = null, CancellationToken cancellationToken = default)
    {
        var query = BuildQuery(
            "api/v1/admin/drivers",
            ("운행상태", 운행상태),
            ("활동지역검색어", 활동지역검색어));

        return await 서버목록조회Async<기사목록응답>(
            query,
            cancellationToken);
    }

    public async Task<기사상세응답?> 기사상세조회Async(string driverId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(driverId))
        {
            return null;
        }

        return await 서버단건조회Async<기사상세응답>(
            $"api/v1/admin/drivers/{Uri.EscapeDataString(driverId.Trim())}",
            cancellationToken);
    }

    public async Task<IReadOnlyList<기사배차내역응답>> 기사배차내역조회Async(string driverId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(driverId))
        {
            return [];
        }

        return await 서버목록조회Async<기사배차내역응답>(
            $"api/v1/admin/drivers/{Uri.EscapeDataString(driverId.Trim())}/dispatches",
            cancellationToken);
    }

    public async Task<IReadOnlyList<기사월정산관리응답>> 기사월정산목록조회Async(int? year = null, int? month = null, string? driverId = null, CancellationToken cancellationToken = default)
    {
        var query = BuildQuery(
            "api/v1/admin/driver-settlements",
            ("year", year?.ToString()),
            ("month", month?.ToString()),
            ("driverId", driverId));

        return await 서버목록조회Async<기사월정산관리응답>(
            query,
            cancellationToken);
    }

    public async Task<IReadOnlyList<운송진행응답>> 운송진행목록조회Async(string? 상태 = null, CancellationToken cancellationToken = default)
    {
        var query = BuildQuery("api/v1/admin/transports", ("상태", 상태));
        return await 운송관제목록조회Async<운송진행응답>(
            query,
            cancellationToken);
    }

    public async Task<IReadOnlyList<운송이벤트로그응답>> 운송이벤트조회Async(string? requestId = null, CancellationToken cancellationToken = default)
    {
        var query = BuildQuery("api/v1/admin/transports/events", ("requestId", requestId));
        return await 운송관제목록조회Async<운송이벤트로그응답>(
            query,
            cancellationToken);
    }

    public async Task<IReadOnlyList<운송진행응답>> 운송진행목록조회Async(string? 상태, string? businessCode, CancellationToken cancellationToken = default)
    {
        if (businessCode is null) return await 운송진행목록조회Async(상태, cancellationToken);
        var business = businessCode.Trim().ToLowerInvariant();
        if (business is not (Ssalddel.Contracts.Admin.BusinessPackages.BusinessPackageCatalog.FreightDelivery
            or Ssalddel.Contracts.Admin.BusinessPackages.BusinessPackageCatalog.FoodDelivery))
            throw new ArgumentException("운송 업무 구분을 확인해 주세요.", nameof(businessCode));
        return await 운송관제목록조회Async<운송진행응답>(
            BuildQuery("api/v1/admin/transports", ("상태", 상태), ("business", business)), cancellationToken);
    }

    private async Task<IReadOnlyList<TItem>> 운송관제목록조회Async<TItem>(string query, CancellationToken cancellationToken)
        where TItem : class
    {
        var items = await 서버단건조회Async<List<TItem>>(query, cancellationToken);
        if (items.Any(item => item is null))
            throw new InvalidDataException("운송 조회 목록의 항목을 확인하지 못했습니다.");
        return items;
    }

    public async Task<IReadOnlyList<업체관리응답>> 업체목록조회Async(string? 상태 = null, CancellationToken cancellationToken = default)
    {
        var query = BuildQuery("api/v1/admin/partners/companies", ("상태", 상태));
        return await 서버목록조회Async<업체관리응답>(
            query,
            cancellationToken);
    }

    public async Task<IReadOnlyList<화주관리응답>> 화주목록조회Async(CancellationToken cancellationToken = default)
    {
        return await 서버목록조회Async<화주관리응답>(
            "api/v1/admin/partners/shippers",
            cancellationToken);
    }

    public async Task<관리자연락처검색응답> 연락처뒤8자리검색Async(string phoneLast8, CancellationToken cancellationToken = default)
    {
        var query = BuildQuery("api/v1/admin/contact-search", ("phoneLast8", phoneLast8));
        return await 서버단건조회Async<관리자연락처검색응답>(query, cancellationToken);
    }

    private void ApplyAuthorizationHeader()
    {
        _httpClient.DefaultRequestHeaders.Authorization = null;

        if (string.IsNullOrWhiteSpace(_session.AccessToken))
        {
            throw new InvalidOperationException("로그인이 필요합니다.");
        }

        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _session.AccessToken);
    }

    private async Task<TValue> 서버단건조회Async<TValue>(
        string requestUri,
        CancellationToken cancellationToken)
        where TValue : class
    {
        ApplyAuthorizationHeader();
        return await _httpClient.GetFromJsonAsync<TValue>(requestUri, cancellationToken)
            ?? throw new InvalidOperationException($"서버 API 응답 본문이 비어 있습니다. path={requestUri}");
    }

    private async Task<IReadOnlyList<TItem>> 서버목록조회Async<TItem>(
        string requestUri,
        CancellationToken cancellationToken)
    {
        ApplyAuthorizationHeader();
        return await _httpClient.GetFromJsonAsync<List<TItem>>(requestUri, cancellationToken)
               ?? [];
    }

    private static string BuildQuery(string path, params (string Key, string? Value)[] parameters)
    {
        var args = parameters
            .Where(x => !string.IsNullOrWhiteSpace(x.Value))
            .Select(x => $"{Uri.EscapeDataString(x.Key)}={Uri.EscapeDataString(x.Value!.Trim())}")
            .ToArray();

        return args.Length == 0
            ? path
            : $"{path}?{string.Join("&", args)}";
    }

    private void Observe(화주운송의뢰응답? item, string source)
    {
        if (item is null || string.IsNullOrWhiteSpace(item.의뢰Id))
        {
            return;
        }

        _ledgerObserver.Observe(
            new TransportRequestLedgerSnapshot(
                item.의뢰Id,
                item.의뢰상태,
                item.결제상태,
                item.배차상태,
                item.정산상태,
                DateTimeOffset.UtcNow,
                source),
            source);
    }
}
