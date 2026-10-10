using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Ssalddel.Contracts.Mart;

namespace FDriverApp.Services;

public enum 마트배송행동 { Accept, Reject, Pickup, Deliver }

/// <summary>복구용 식별자·판본만 보관합니다. 전표 원문·주소·토큰은 포함하지 않습니다.</summary>
public sealed record 살뜰마트배송미확인요청(string OwnerId, Guid RequestId, string OfferId, 마트배송행동 Action,
    long Revision, long LabelRevision, int RecommendationRound, DateTime QueueUpdatedAtUtc,
    bool ByNumber = false, string? CodeHash = null, bool ServerRejected = false);

public interface I살뜰마트배송PendingStore
{
    Task<살뜰마트배송미확인요청?> LoadAsync(CancellationToken token = default);
    Task SaveAsync(살뜰마트배송미확인요청 request, CancellationToken token = default);
    Task ClearAsync(string ownerId, CancellationToken token = default);
}

public interface I살뜰마트배송ApiClient
{
    Task<마트기사업무목록Dto> ListAsync(CancellationToken token = default);
    Task<마트기사업무항목Dto> GetAsync(string offerId, CancellationToken token = default);
    Task<마트기사업무항목Dto> ResultAsync(Guid requestId, CancellationToken token = default);
    Task<마트기사업무항목Dto> AcceptAsync(string offerId, 마트기사수락요청 request, CancellationToken token = default);
    Task<마트기사업무항목Dto> RejectAsync(string offerId, 마트기사거절요청 request, CancellationToken token = default);
    Task<마트기사업무항목Dto> PickupAsync(string offerId, 마트기사픽업요청 request, CancellationToken token = default);
    Task<마트기사업무항목Dto> DeliverAsync(string offerId, 마트기사전달요청 request, CancellationToken token = default);
}

/// <summary>기존 기사 인증과 HTTP 연결을 사용하되 음식 주문 경로로 대체하지 않습니다.</summary>
public sealed class 살뜰마트배송ApiClient(HttpClient http, IFDriverAuthSession auth, FDriverAuthApiService authentication)
    : I살뜰마트배송ApiClient
{
    private const string Root = "api/v1/driver/mart-deliveries";
    public async Task<마트기사업무목록Dto> ListAsync(CancellationToken token = default)
    {
        try { return await SendAsync<마트기사업무목록Dto>(HttpMethod.Get, Root, null, token); }
        catch (FDriverApiException error) when (error.StatusCode == HttpStatusCode.NotFound
            && error.Data["errorCode"] is string code && string.Equals(code, "FeatureDisabled", StringComparison.Ordinal))
        {
            // 공통 기능 필터의 명확한 비활성 응답만 안내로 표시합니다. 일반 오류는 숨기지 않습니다.
            return new 마트기사업무목록Dto { Enabled = false, Notice = "살뜰마트 기사 업무가 비활성화되어 있습니다." };
        }
    }
    public Task<마트기사업무항목Dto> GetAsync(string offerId, CancellationToken token = default)
        => SendAsync<마트기사업무항목Dto>(HttpMethod.Get, Path(offerId), null, token);
    public Task<마트기사업무항목Dto> ResultAsync(Guid requestId, CancellationToken token = default)
        => requestId == Guid.Empty ? throw new ArgumentException("요청 번호를 확인해 주세요.", nameof(requestId))
            : SendAsync<마트기사업무항목Dto>(HttpMethod.Get, $"{Root}/requests/{requestId:D}", null, token);
    public Task<마트기사업무항목Dto> AcceptAsync(string offerId, 마트기사수락요청 request, CancellationToken token = default)
        => SendAsync<마트기사업무항목Dto>(HttpMethod.Post, Path(offerId) + "/accept", request, token);
    public Task<마트기사업무항목Dto> RejectAsync(string offerId, 마트기사거절요청 request, CancellationToken token = default)
        => SendAsync<마트기사업무항목Dto>(HttpMethod.Post, Path(offerId) + "/reject", request, token);
    public Task<마트기사업무항목Dto> PickupAsync(string offerId, 마트기사픽업요청 request, CancellationToken token = default)
        => SendAsync<마트기사업무항목Dto>(HttpMethod.Post, Path(offerId) + "/pickup", request, token);
    public Task<마트기사업무항목Dto> DeliverAsync(string offerId, 마트기사전달요청 request, CancellationToken token = default)
        => SendAsync<마트기사업무항목Dto>(HttpMethod.Post, Path(offerId) + "/deliver", request, token);

    private static string Path(string offerId) => string.IsNullOrWhiteSpace(offerId)
        ? throw new ArgumentException("배송 제안을 확인해 주세요.", nameof(offerId))
        : Root + "/" + Uri.EscapeDataString(offerId);

    private async Task<T> SendAsync<T>(HttpMethod method, string path, object? body, CancellationToken token)
    {
        var owner = auth.UserId;
        var result = await authentication.EnsureAccessTokenResultAsync(cancellationToken: token);
        if (owner != auth.UserId || !auth.IsAuthenticated)
            throw new OperationCanceledException("기사 계정이 변경되었습니다.", token);
        if (!result.IsSuccess) throw new FDriverApiException("기사 로그인을 확인해 주세요.", result.StatusCode);

        var revision = auth.SessionRevision;
        var accessToken = auth.AccessToken;
        using var first = await SendOnceAsync(method, path, body, token);
        // 인증 갱신 뒤 GET만 재조회합니다. 업무 POST는 응답이 불명확해도 자동 반복하지 않습니다.
        if (first.StatusCode == HttpStatusCode.Unauthorized && method == HttpMethod.Get)
        {
            result = await authentication.EnsureAccessTokenResultAsync(forceRefresh: true, cancellationToken: token);
            if (owner != auth.UserId || !auth.IsAuthenticated)
                throw new OperationCanceledException("기사 계정이 변경되었습니다.", token);
            if (!result.IsSuccess) throw new FDriverApiException("기사 로그인을 확인해 주세요.", result.StatusCode);
            revision = auth.SessionRevision;
            accessToken = auth.AccessToken;
            using var refreshed = await SendOnceAsync(method, path, body, token);
            return await ReadCheckedAsync<T>(refreshed, owner, revision, accessToken, token);
        }
        return await ReadCheckedAsync<T>(first, owner, revision, accessToken, token);
    }

    private async Task<T> ReadCheckedAsync<T>(HttpResponseMessage response, string? owner, long revision, string? accessToken, CancellationToken token)
    {
        try { return await ReadAsync<T>(response, token); }
        finally
        {
            token.ThrowIfCancellationRequested();
            if (owner != auth.UserId || revision != auth.SessionRevision || accessToken != auth.AccessToken)
                throw new OperationCanceledException("기사 계정이 변경되어 이전 응답을 적용하지 않습니다.", token);
        }
    }

    public static bool IsConfirmedCommandRejection(FDriverApiException error)
        => (error.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.NotFound or HttpStatusCode.Conflict
            or HttpStatusCode.UnprocessableEntity or HttpStatusCode.TooManyRequests)
            && error.Data["commandRejected"] is true && error.Data["errorCode"] is string { Length: > 0 };

    private async Task<HttpResponseMessage> SendOnceAsync(HttpMethod method, string path, object? body, CancellationToken token)
    {
        var owner = auth.UserId;
        var revision = auth.SessionRevision;
        var accessToken = auth.AccessToken;
        if (!auth.IsAuthenticated || string.IsNullOrWhiteSpace(owner) || string.IsNullOrWhiteSpace(accessToken))
            throw new FDriverApiException("기사 로그인이 필요합니다.", HttpStatusCode.Unauthorized);
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        if (body is not null) request.Content = JsonContent.Create(body);
        try
        {
            var response = await http.SendAsync(request, token);
            if (token.IsCancellationRequested || owner != auth.UserId || revision != auth.SessionRevision || accessToken != auth.AccessToken)
            {
                response.Dispose();
                token.ThrowIfCancellationRequested();
                throw new OperationCanceledException("기사 계정이 변경되어 이전 응답을 적용하지 않습니다.", token);
            }
            return response;
        }
        catch (HttpRequestException ex) { throw new FDriverApiException("서버 연결을 확인하지 못했습니다.", null, ex); }
        catch (TaskCanceledException ex) when (!token.IsCancellationRequested)
        { throw new FDriverApiException("서버 응답을 확인하지 못했습니다.", null, ex); }
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response, CancellationToken token)
    {
        if (!response.IsSuccessStatusCode)
        {
            // 원문 오류에는 업무 식별자나 전표 비밀이 섞일 수 있어 화면·로그로 전달하지 않습니다.
            var message = response.StatusCode switch
            {
                HttpStatusCode.Unauthorized => "기사 로그인을 확인해 주세요.",
                HttpStatusCode.Forbidden => "이 배송을 처리할 권한이 없습니다.",
                HttpStatusCode.NotFound => "요청 기록을 찾지 못했습니다.",
                HttpStatusCode.Conflict => "현재 배송 상태를 다시 확인해 주세요.",
                HttpStatusCode.ServiceUnavailable => "요청 결과를 아직 확인할 수 없습니다.",
                _ => "요청을 확인하지 못했습니다. 현재 상태를 다시 조회해 주세요."
            };
            var error = new FDriverApiException(message, response.StatusCode);
            try
            {
                using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
                if (document.RootElement.TryGetProperty("errorCode", out var value)
                    && value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 and <= 128 } code
                    && code.All(x => char.IsAsciiLetterOrDigit(x) || x is '-' or '_' or '.'))
                    error.Data["errorCode"] = code;
                if (document.RootElement.TryGetProperty("commandRejected", out var rejected) && rejected.ValueKind == JsonValueKind.True)
                    error.Data["commandRejected"] = true;
            }
            catch (JsonException) { }
            throw error;
        }
        try
        {
            return await response.Content.ReadFromJsonAsync<T>(token)
                ?? throw new FDriverApiException("서버 응답을 읽지 못했습니다.", response.StatusCode);
        }
        catch (JsonException ex) { throw new FDriverApiException("서버 응답을 읽지 못했습니다.", response.StatusCode, ex); }
    }
}
