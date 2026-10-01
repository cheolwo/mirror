using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Ssalddel.Client.Infrastructure.Security;
using Ssalddel.Contracts.Food;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace RestaurantDeskApp.Services;

public sealed class Ssalddel음식주문Client(
    HttpClient httpClient,
    RestaurantAuthService authService,
    ClientAuthSession authSession) : I음식주문ApiClient, I음식점메뉴ApiClient
{
    private const string BasePath = "api/v1/food-orders";

    public async Task<IReadOnlyList<음식점메뉴관리응답>> 목록Async(CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(HttpMethod.Get, "api/v1/restaurant/menus", null, cancellationToken);
        await EnsureSuccessAsync(response, "메뉴 목록 조회", cancellationToken);
        return await response.Content.ReadFromJsonAsync<List<음식점메뉴관리응답>>(cancellationToken)
            ?? throw new InvalidOperationException("메뉴 목록 응답이 비어 있습니다.");
    }

    public Task<음식점메뉴관리응답> 등록Async(음식점메뉴등록요청 request, CancellationToken cancellationToken = default)
        => SaveMenuAsync(HttpMethod.Post, "api/v1/restaurant/menus", () => JsonContent.Create(request), cancellationToken);

    public Task<음식점메뉴관리응답> 수정Async(long menuId, 음식점메뉴수정요청 request, CancellationToken cancellationToken = default)
        => SaveMenuAsync(HttpMethod.Put, $"api/v1/restaurant/menus/{menuId}", () => JsonContent.Create(request), cancellationToken);

    private async Task<음식점메뉴관리응답> SaveMenuAsync(HttpMethod method, string path, Func<HttpContent?> contentFactory, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(method, path, contentFactory, cancellationToken);
        if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Forbidden or HttpStatusCode.NotFound or HttpStatusCode.Conflict)
            throw new 메뉴저장거절Exception();
        await EnsureSuccessAsync(response, "메뉴 저장", cancellationToken);
        return await response.Content.ReadFromJsonAsync<음식점메뉴관리응답>(cancellationToken)
            ?? throw new InvalidOperationException("메뉴 저장 응답이 비어 있습니다. 목록을 다시 확인해 주세요.");
    }

    public async Task<음식점주문수신함응답> 주문목록조회Async(
        음식점주문수신함조회요청 request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        using var response = await SendAsync(
            HttpMethod.Get,
            BuildInboxPath(request),
            contentFactory: null,
            cancellationToken);
        await EnsureSuccessAsync(response, "음식 주문 목록 조회", cancellationToken);
        return await response.Content.ReadFromJsonAsync<음식점주문수신함응답>(cancellationToken)
            ?? new 음식점주문수신함응답
            {
                Page = Math.Max(1, request.Page),
                PageSize = Math.Clamp(request.PageSize, 1, 100)
            };
    }

    public async Task<음식주문응답?> 주문상세조회Async(
        string 주문번호,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(주문번호))
        {
            return null;
        }

        using var response = await SendAsync(
            HttpMethod.Get,
            $"{BasePath}/restaurant/inbox/{Uri.EscapeDataString(주문번호.Trim())}",
            contentFactory: null,
            cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await EnsureSuccessAsync(response, "음식 주문 상세 조회", cancellationToken);
        return await response.Content.ReadFromJsonAsync<음식주문응답>(cancellationToken);
    }

    public async Task<음식주문응답?> 음식점수락Async(
        string 주문번호,
        음식점주문수락요청 request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(주문번호);
        ArgumentNullException.ThrowIfNull(request);

        using var response = await SendAsync(
            HttpMethod.Post,
            $"{BasePath}/{Uri.EscapeDataString(주문번호.Trim())}/restaurant-acceptance",
            () => JsonContent.Create(request),
            cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await EnsureSuccessAsync(response, "음식점 주문 수락", cancellationToken);
        return await response.Content.ReadFromJsonAsync<음식주문응답>(cancellationToken);
    }

    public async Task<음식주문응답?> 음식점진행변경Async(
        string 주문번호,
        음식점주문진행변경요청 request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(주문번호);
        ArgumentNullException.ThrowIfNull(request);

        using var response = await SendAsync(
            HttpMethod.Post,
            $"{BasePath}/{Uri.EscapeDataString(주문번호.Trim())}/restaurant-progress",
            () => JsonContent.Create(request),
            cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await EnsureSuccessAsync(response, "음식점 주문 진행 변경", cancellationToken);
        return await response.Content.ReadFromJsonAsync<음식주문응답>(cancellationToken);
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string path,
        Func<HttpContent?>? contentFactory,
        CancellationToken cancellationToken)
    {
        var auth = await authService.EnsureAccessTokenAsync(
            cancellationToken: cancellationToken);
        if (!auth.IsSuccess)
        {
            throw new UnauthorizedAccessException(auth.ErrorMessage);
        }

        var response = await SendOnceAsync(
            method,
            path,
            contentFactory,
            cancellationToken);
        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            return response;
        }

        response.Dispose();
        auth = await authService.EnsureAccessTokenAsync(
            forceRefresh: true,
            cancellationToken: cancellationToken);
        if (!auth.IsSuccess)
        {
            throw new UnauthorizedAccessException(auth.ErrorMessage);
        }

        return await SendOnceAsync(
            method,
            path,
            contentFactory,
            cancellationToken);
    }

    private async Task<HttpResponseMessage> SendOnceAsync(
        HttpMethod method,
        string path,
        Func<HttpContent?>? contentFactory,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, path)
        {
            Content = contentFactory?.Invoke()
        };
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            authSession.AccessToken);
        return await httpClient.SendAsync(request, cancellationToken);
    }

    private static string BuildInboxPath(음식점주문수신함조회요청 request)
    {
        var query = new List<string>
        {
            $"처리상태={Uri.EscapeDataString(음식점주문수신함처리상태코드.Normalize(request.처리상태))}",
            $"Page={Math.Max(1, request.Page)}",
            $"PageSize={Math.Clamp(request.PageSize, 1, 100)}"
        };
        if (request.UpdatedAfterUtc is { } updatedAfterUtc)
        {
            query.Add($"UpdatedAfterUtc={Uri.EscapeDataString(updatedAfterUtc.ToUniversalTime().ToString("O"))}");
        }

        return $"{BasePath}/restaurant/inbox?{string.Join("&", query)}";
    }

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        string operation,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var problem = SsalddelApiProblemParser.Parse(body);
        var statusCode = (int)response.StatusCode;
        var detail = problem.Message ?? (string.IsNullOrWhiteSpace(body) ? null : body);
        throw new SsalddelApiException(
            $"{operation} API 실패: HTTP {statusCode}"
            + (string.IsNullOrWhiteSpace(detail) ? string.Empty : $" · {detail}"),
            statusCode,
            operation,
            body,
            problem.TraceId,
            problem.FieldErrors,
            problem.FailureClassCode,
            problem.ResponsibilityRoleCode,
            problem.RequiresStateRefresh,
            problem.RetryPolicyCode,
            problem.AvailableRecoveryActions,
            problem.CurrentRevision,
            problem.RetryAfterUtc,
            problem.ErrorCode);
    }
}
