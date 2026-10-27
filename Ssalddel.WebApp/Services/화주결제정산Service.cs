using System.Net.Http.Headers;
using System.Net.Http.Json;
using Ssalddel.Client.Infrastructure.Transport;
using Ssalddel.Contracts.Common.Payments;
using Ssalddel.Contracts.Shipper.Payment;
using Ssalddel.Contracts.Shipper.Request;

namespace Ssalddel.WebApp.Services;

public sealed class 화주결제정산Service
{
    private readonly HttpClient _httpClient;
    private readonly WebAuthSessionService _authSession;
    private readonly ITransportRequestLedgerObserver _ledgerObserver;

    public 화주결제정산Service(
        HttpClient httpClient,
        WebAuthSessionService authSession,
        ITransportRequestLedgerObserver ledgerObserver)
    {
        _httpClient = httpClient;
        _authSession = authSession;
        _ledgerObserver = ledgerObserver;
    }

    public async Task<화주운송의뢰응답> 의뢰조회Async(string requestId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(requestId))
        {
            throw new InvalidOperationException("의뢰 ID를 입력해 주세요.");
        }

        using var request = await CreateAuthorizedRequestAsync(
            HttpMethod.Get,
            $"api/v1/shipper/requests/{Uri.EscapeDataString(requestId.Trim())}",
            cancellationToken);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, "운송 의뢰 조회", cancellationToken);

        var item = await response.Content.ReadFromJsonAsync<화주운송의뢰응답>(cancellationToken)
                   ?? throw new InvalidOperationException("운송 의뢰 조회 응답을 읽을 수 없습니다.");
        Observe(item, "Ssalddel.WebApp.ShipperRequest");
        return item;
    }

    public async Task<IReadOnlyList<결제목록응답>> 결제목록조회Async(
        string? requestId,
        string? paymentStatus,
        CancellationToken cancellationToken = default)
    {
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(requestId))
        {
            query.Add($"의뢰Id={Uri.EscapeDataString(requestId.Trim())}");
        }

        if (!string.IsNullOrWhiteSpace(paymentStatus))
        {
            query.Add($"결제상태={Uri.EscapeDataString(paymentStatus.Trim())}");
        }

        query.Add("page=1");
        query.Add("pageSize=20");

        using var request = await CreateAuthorizedRequestAsync(
            HttpMethod.Get,
            $"api/v1/payments?{string.Join('&', query)}",
            cancellationToken);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, "결제 목록 조회", cancellationToken);

        return await response.Content.ReadFromJsonAsync<IReadOnlyList<결제목록응답>>(cancellationToken) ?? [];
    }

    public async Task<토스결제환경응답> 토스결제환경조회Async(CancellationToken cancellationToken = default)
    {
        using var request = await CreateAuthorizedRequestAsync(HttpMethod.Get, "api/v1/payments/toss/config", cancellationToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, "토스 결제 환경 조회", cancellationToken);

        return await response.Content.ReadFromJsonAsync<토스결제환경응답>(cancellationToken)
               ?? throw new InvalidOperationException("토스 결제 환경 조회 응답을 읽을 수 없습니다.");
    }

    public Task<공통결제준비응답> 공통결제준비Async(
        string requestId,
        int amount,
        string? orderName,
        CancellationToken cancellationToken = default)
    {
        return PostJsonAsync<공통결제준비요청, 공통결제준비응답>(
            "api/v1/payments/prepare",
            new 공통결제준비요청
            {
                결제대상유형 = 계약결제대상유형.용달운송의뢰,
                대상Id = requestId.Trim(),
                결제제공자 = 계약결제제공자.TossPayments,
                금액 = amount,
                주문명 = orderName
            },
            "공통 결제 준비",
            requireAuth: true,
            cancellationToken);
    }

    public Task<토스결제준비응답> 토스결제준비Async(
        string requestId,
        int amount,
        CancellationToken cancellationToken = default)
    {
        return PostJsonAsync<토스결제준비요청, 토스결제준비응답>(
            "api/v1/payments/toss/prepare",
            new 토스결제준비요청
            {
                의뢰Id = requestId.Trim(),
                Amount = amount
            },
            "토스 결제 준비",
            requireAuth: true,
            cancellationToken);
    }

    public Task<토스결제승인응답> 토스결제승인Async(
        string paymentKey,
        string orderId,
        int amount,
        CancellationToken cancellationToken = default)
    {
        return PostJsonAsync<토스결제승인요청, 토스결제승인응답>(
            "api/v1/payments/toss/confirm",
            new 토스결제승인요청
            {
                PaymentKey = paymentKey.Trim(),
                OrderId = orderId.Trim(),
                Amount = amount
            },
            "토스 결제 승인",
            requireAuth: true,
            cancellationToken);
    }

    public async Task<string> 인수증등록Async(
        string requestId,
        string receiptNo,
        string? memo,
        CancellationToken cancellationToken = default)
    {
        return await PostForMessageAsync(
            $"api/v1/shipper/requests/{Uri.EscapeDataString(requestId.Trim())}/settlement/receipt",
            new 화주운송의뢰인수증등록요청
            {
                인수증번호 = receiptNo.Trim(),
                등록메모 = memo
            },
            "인수증 등록",
            requestId,
            cancellationToken);
    }

    /// <summary>명시적으로 확인한 계정의 인수증만 한 번 전송합니다. 결과 확정은 같은 의뢰 GET이 담당합니다.</summary>
    public async Task 인수증단일등록Async(string requestId, string receiptNo, string? memo,
        string expectedOwner, string expectedToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(requestId) || string.IsNullOrWhiteSpace(receiptNo))
            throw new InvalidOperationException("의뢰와 인수증 번호를 확인해 주세요.");
        using var request = await CreateAuthorizedRequestAsync(HttpMethod.Post,
            $"api/v1/shipper/requests/{Uri.EscapeDataString(requestId.Trim())}/settlement/receipt", cancellationToken);
        if (_authSession.UserId != expectedOwner || _authSession.AccessToken != expectedToken)
            throw new InvalidOperationException("로그인 정보가 변경되었습니다. 같은 의뢰를 다시 확인해 주세요.");
        request.Content = JsonContent.Create(new 화주운송의뢰인수증등록요청
        {
            인수증번호 = receiptNo.Trim(), 등록메모 = memo
        });
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, "인수증 등록", cancellationToken);
        // POST 응답 본문·observer는 성공 표시의 근거로 삼지 않습니다.
    }

    public async Task<string> 후불승인Async(
        string requestId,
        string? memo,
        CancellationToken cancellationToken = default)
    {
        return await PostForMessageAsync(
            $"api/v1/shipper/requests/{Uri.EscapeDataString(requestId.Trim())}/settlement/postpay/approve",
            new 화주운송의뢰후불승인요청
            {
                승인메모 = memo
            },
            "후불 승인",
            requestId,
            cancellationToken);
    }

    public async Task<string> 현장지급처리Async(
        string requestId,
        string? memo,
        CancellationToken cancellationToken = default)
    {
        return await PostForMessageAsync(
            $"api/v1/shipper/requests/{Uri.EscapeDataString(requestId.Trim())}/settlement/offline",
            new 화주운송의뢰현장지급처리요청
            {
                현장지급메모 = memo
            },
            "현장 지급 처리",
            requestId,
            cancellationToken);
    }

    private async Task<string> PostForMessageAsync<TRequest>(
        string path,
        TRequest payload,
        string actionName,
        string? requestId,
        CancellationToken cancellationToken)
    {
        using var request = await CreateAuthorizedRequestAsync(HttpMethod.Post, path, cancellationToken);
        request.Content = JsonContent.Create(payload);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, actionName, cancellationToken);

        if (!string.IsNullOrWhiteSpace(requestId))
        {
            _ledgerObserver.RequestRefresh(requestId, $"Ssalddel.WebApp.{actionName}");
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return string.IsNullOrWhiteSpace(body) ? $"{actionName} 처리가 완료되었습니다." : body;
    }

    private async Task<TResponse> PostJsonAsync<TRequest, TResponse>(
        string path,
        TRequest payload,
        string actionName,
        bool requireAuth,
        CancellationToken cancellationToken)
    {
        using var request = requireAuth
            ? await CreateAuthorizedRequestAsync(HttpMethod.Post, path, cancellationToken)
            : await CreateOptionalAuthorizedRequestAsync(HttpMethod.Post, path, cancellationToken);

        request.Content = JsonContent.Create(payload);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, actionName, cancellationToken);

        return await response.Content.ReadFromJsonAsync<TResponse>(cancellationToken)
               ?? throw new InvalidOperationException($"{actionName} 응답을 읽을 수 없습니다.");
    }

    private async Task<HttpRequestMessage> CreateAuthorizedRequestAsync(
        HttpMethod method,
        string path,
        CancellationToken cancellationToken)
    {
        await _authSession.RestoreAsync(cancellationToken);
        if (!_authSession.IsLoggedIn || string.IsNullOrWhiteSpace(_authSession.AccessToken))
        {
            throw new InvalidOperationException("이 작업은 서버 인증이 필요합니다. 먼저 웹 로그인에서 로그인해 주세요.");
        }

        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _authSession.AccessToken);
        return request;
    }

    private async Task<HttpRequestMessage> CreateOptionalAuthorizedRequestAsync(
        HttpMethod method,
        string path,
        CancellationToken cancellationToken)
    {
        await _authSession.RestoreAsync(cancellationToken);
        var request = new HttpRequestMessage(method, path);

        if (_authSession.IsLoggedIn && !string.IsNullOrWhiteSpace(_authSession.AccessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _authSession.AccessToken);
        }

        return request;
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, string actionName, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new HttpRequestException(
            string.IsNullOrWhiteSpace(body)
                ? $"{actionName} 실패: HTTP {(int)response.StatusCode}"
                : $"{actionName} 실패: HTTP {(int)response.StatusCode}: {body}",
            inner: null,
            response.StatusCode);
    }

    private void Observe(화주운송의뢰응답 item, string source)
    {
        if (string.IsNullOrWhiteSpace(item.의뢰Id))
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
