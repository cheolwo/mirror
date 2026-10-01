using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Hongdal.Contracts.Common.Drivers;
using Hongdal.Contracts.Driver.Food;
using Hongdal.Contracts.Driver.Work;

namespace FDriverApp.Services;

public interface IFoodDeliveryDriverApiService
{
    Task<FoodDeliveryDriverWorkspaceDto> GetWorkspaceAsync(CancellationToken cancellationToken = default);
    Task<기사운행상태응답?> GetWorkStatusAsync(CancellationToken cancellationToken = default);
    Task StartWorkAsync(string startLocation, CancellationToken cancellationToken = default);
    Task StopWorkAsync(CancellationToken cancellationToken = default);
    Task<기사위치갱신응답?> UpdateLocationAsync(기사위치갱신요청 request, CancellationToken cancellationToken = default);
    Task<FoodDeliveryDriverActionResponse> AcceptAsync(string offerId, CancellationToken cancellationToken = default);
    Task<FoodDeliveryDriverActionResponse> AcceptBundleAsync(IReadOnlyList<string> offerIds, CancellationToken cancellationToken = default);
    Task<FoodDeliveryDriverActionResponse> ConfirmPickupAsync(string offerId, CancellationToken cancellationToken = default);
    Task<FoodDeliveryDriverActionResponse> CompleteAsync(string offerId, CancellationToken cancellationToken = default);
    Task<FoodDeliveryDriverRouteResponseDto> GetRouteAsync(FoodDeliveryDriverRouteRequestDto request, CancellationToken cancellationToken = default);
}

public sealed class FoodDeliveryDriverApiService : IFoodDeliveryDriverApiService
{
    private readonly HttpClient _httpClient;
    private readonly IFDriverAuthSession _session;

    public FoodDeliveryDriverApiService(HttpClient httpClient, IFDriverAuthSession session)
    {
        _httpClient = httpClient;
        _session = session;
    }

    public Task<FoodDeliveryDriverWorkspaceDto> GetWorkspaceAsync(CancellationToken cancellationToken = default)
        => SendAsync<FoodDeliveryDriverWorkspaceDto>(HttpMethod.Get, "api/v1/driver/food-deliveries/workspace", null, cancellationToken);

    public async Task<기사운행상태응답?> GetWorkStatusAsync(CancellationToken cancellationToken = default)
        => await SendAsync<기사운행상태응답>(HttpMethod.Get, "api/v1/driver/work/status", null, cancellationToken);

    public async Task StartWorkAsync(string startLocation, CancellationToken cancellationToken = default)
        => _ = await SendAsync<기사운행시작응답>(
            HttpMethod.Post,
            "api/v1/driver/work/start",
            new 기사운행시작요청
            {
                시작모드 = "immediate",
                시작시각 = DateTime.UtcNow,
                시작위치 = startLocation
            },
            cancellationToken);

    public async Task StopWorkAsync(CancellationToken cancellationToken = default)
        => await SendNoContentAsync(HttpMethod.Post, "api/v1/driver/work/stop", null, cancellationToken);

    public async Task<기사위치갱신응답?> UpdateLocationAsync(
        기사위치갱신요청 request,
        CancellationToken cancellationToken = default)
        => await SendAsync<기사위치갱신응답>(HttpMethod.Post, "api/v1/driver/work/location", request, cancellationToken);

    public Task<FoodDeliveryDriverActionResponse> AcceptAsync(string offerId, CancellationToken cancellationToken = default)
        => SendAsync<FoodDeliveryDriverActionResponse>(
            HttpMethod.Post,
            $"api/v1/driver/food-deliveries/offers/{Uri.EscapeDataString(offerId)}/accept",
            null,
            cancellationToken);

    public Task<FoodDeliveryDriverActionResponse> AcceptBundleAsync(
        IReadOnlyList<string> offerIds,
        CancellationToken cancellationToken = default)
        => SendAsync<FoodDeliveryDriverActionResponse>(
            HttpMethod.Post,
            "api/v1/driver/food-deliveries/bundles/accept",
            new FoodDeliveryBundleAcceptRequest { OfferIds = offerIds },
            cancellationToken);

    public Task<FoodDeliveryDriverActionResponse> ConfirmPickupAsync(string offerId, CancellationToken cancellationToken = default)
        => SendAsync<FoodDeliveryDriverActionResponse>(
            HttpMethod.Post,
            $"api/v1/driver/food-deliveries/offers/{Uri.EscapeDataString(offerId)}/pickup-complete",
            null,
            cancellationToken);

    public Task<FoodDeliveryDriverActionResponse> CompleteAsync(string offerId, CancellationToken cancellationToken = default)
        => SendAsync<FoodDeliveryDriverActionResponse>(
            HttpMethod.Post,
            $"api/v1/driver/food-deliveries/offers/{Uri.EscapeDataString(offerId)}/delivery-complete",
            null,
            cancellationToken);

    public Task<FoodDeliveryDriverRouteResponseDto> GetRouteAsync(
        FoodDeliveryDriverRouteRequestDto request,
        CancellationToken cancellationToken = default)
        => SendAsync<FoodDeliveryDriverRouteResponseDto>(
            HttpMethod.Post,
            "api/v1/driver/food-deliveries/route",
            request,
            cancellationToken);

    private async Task<T> SendAsync<T>(
        HttpMethod method,
        string path,
        object? body,
        CancellationToken cancellationToken)
    {
        using var request = CreateRequest(method, path, body);
        using var response = await SendCoreAsync(request, cancellationToken);
        var result = await response.Content.ReadFromJsonAsync<T>(cancellationToken);
        return result ?? throw new FDriverApiException("서버 응답을 읽을 수 없습니다.", response.StatusCode);
    }

    private async Task SendNoContentAsync(
        HttpMethod method,
        string path,
        object? body,
        CancellationToken cancellationToken)
    {
        using var request = CreateRequest(method, path, body);
        using var response = await SendCoreAsync(request, cancellationToken);
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string path, object? body)
    {
        if (!_session.IsAuthenticated || string.IsNullOrWhiteSpace(_session.AccessToken))
        {
            throw new FDriverApiException("기사 로그인이 필요합니다.", HttpStatusCode.Unauthorized);
        }

        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _session.AccessToken);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return request;
    }

    private async Task<HttpResponseMessage> SendCoreAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new FDriverApiException("살뜰 서비스에 연결할 수 없습니다.", null, ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new FDriverApiException("서버 응답 시간이 초과되었습니다.", null, ex);
        }

        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var message = response.StatusCode == HttpStatusCode.Unauthorized
                ? "로그인이 만료되었습니다. 다시 로그인해 주세요."
                : ReadProblemMessage(content);
            var statusCode = response.StatusCode;
            response.Dispose();
            throw new FDriverApiException(
                string.IsNullOrWhiteSpace(message) ? "서버 요청을 처리하지 못했습니다." : message,
                statusCode);
        }

        return response;
    }

    private static string ReadProblemMessage(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return string.Empty;
        }

        try
        {
            using var document = JsonDocument.Parse(content);
            foreach (var propertyName in new[] { "detail", "message", "title" })
            {
                if (document.RootElement.TryGetProperty(propertyName, out var value)
                    && value.ValueKind == JsonValueKind.String
                    && !string.IsNullOrWhiteSpace(value.GetString()))
                {
                    return value.GetString()!;
                }
            }
        }
        catch (JsonException)
        {
        }

        return content;
    }
}

public sealed class FDriverApiException : Exception
{
    public FDriverApiException(string message, HttpStatusCode? statusCode, Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
    }

    public HttpStatusCode? StatusCode { get; }
}
