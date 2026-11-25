using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Ssalddel.Contracts.Common.Dispatch;

namespace Ssalddel.Ui.Common.Areas.App.Services;

/// <summary>역할 호스트의 기존 인증을 사용하며 사용자 의견 POST를 자동 재전송하지 않습니다.</summary>
public abstract class AuthenticatedCargoDispatchExplanationClient(HttpClient http) : ICargoDispatchExplanationClient, IDisposable
{
    public abstract CargoDispatchExplanationIdentity Identity { get; }
    protected abstract string? AccessToken { get; }
    public abstract Task RestoreAsync(CancellationToken cancellationToken = default);
    protected abstract Task EnsureAuthorizedAsync(CancellationToken ct);
    public event Action? SessionChanged;
    protected void NotifySessionChanged() => SessionChanged?.Invoke();

    public async Task<CargoDispatchExplanationResponse> ReadAsync(string requestId, CancellationToken cancellationToken = default)
        => await SendAsync<CargoDispatchExplanationResponse>(HttpMethod.Get, Path(requestId), null, false, cancellationToken)
            ?? throw new InvalidDataException("저장된 배차 설명 응답이 없습니다.");
    public async Task<CargoDispatchFeedbackReceipt> SubmitAsync(string requestId, CargoDispatchFeedbackRequest request, CancellationToken cancellationToken = default)
        => await SendAsync<CargoDispatchFeedbackReceipt>(HttpMethod.Post, Path(requestId) + "/feedback", request, false, cancellationToken)
            ?? throw new InvalidDataException("의견 접수 응답이 없습니다.");
    public Task<CargoDispatchFeedbackReceipt?> ReadFeedbackAsync(string requestId, string evidenceId, Guid clientRequestId, CancellationToken cancellationToken = default)
        => SendAsync<CargoDispatchFeedbackReceipt>(HttpMethod.Get,
            Path(requestId) + "/feedback/" + clientRequestId.ToString("D") + "?evidenceId=" + Uri.EscapeDataString(evidenceId), null, true, cancellationToken);

    private async Task<T?> SendAsync<T>(HttpMethod method, string path, object? body, bool allowMissing, CancellationToken ct)
    {
        var expected = Identity;
        var expectedServer = http.BaseAddress;
        if (!expected.IsAuthenticated || string.IsNullOrWhiteSpace(expected.UserId)) throw new InvalidOperationException("로그인이 필요합니다.");
        await EnsureAuthorizedAsync(ct);
        if (Identity != expected || http.BaseAddress != expectedServer) throw new OperationCanceledException("로그인 또는 서버 정보가 변경되었습니다.");
        var token = AccessToken;
        if (string.IsNullOrWhiteSpace(token)) throw new InvalidOperationException("로그인이 필요합니다.");
        // HttpClient의 기본 서버가 바뀌어도 이미 보존한 의견이 다른 서버로 향하지 않는다.
        using var message = new HttpRequestMessage(method,
            expectedServer is null ? new Uri(path, UriKind.Relative) : new Uri(expectedServer, path));
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null) message.Content = JsonContent.Create(body, body.GetType());
        if (Identity != expected || http.BaseAddress != expectedServer) throw new OperationCanceledException("로그인 또는 서버 정보가 변경되었습니다.");
        using var response = await http.SendAsync(message, ct);
        if (Identity != expected || http.BaseAddress != expectedServer) throw new OperationCanceledException("로그인 또는 서버 정보가 변경되었습니다.");
        if (allowMissing && response.StatusCode == HttpStatusCode.NotFound) return default;
        if (!response.IsSuccessStatusCode) throw new HttpRequestException("배차 설명·의견 요청을 확인하지 못했습니다.", null, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken: ct);
    }
    private static string Path(string requestId)
    {
        if (string.IsNullOrWhiteSpace(requestId)) throw new ArgumentException("의뢰 번호가 필요합니다.", nameof(requestId));
        return ICargoDispatchExplanationClient.Endpoint + "/" + Uri.EscapeDataString(requestId.Trim());
    }
    public virtual void Dispose() => SessionChanged = null;
}
