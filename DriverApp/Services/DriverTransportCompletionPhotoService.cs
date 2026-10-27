using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Ssalddel.Contracts.Driver.Transport;

namespace DriverApp.Services;

public enum DriverTransportCompletionPhotoKind
{
    Pickup,
    Dropoff
}

public static class DriverTransportCompletionPhotoLimits
{
    public const int MaximumUploadBytes = 5 * 1024 * 1024;
}

public sealed record DriverTransportCompletionPhoto(
    DriverTransportCompletionPhotoKind Kind,
    long TransportId,
    string FileName,
    string ContentType,
    byte[] Bytes,
    DriverTransportPickupReceiptEvidence? ReceiptEvidence = null,
    string? ExistingObjectName = null,
    string? ExistingUrl = null);

public sealed record DriverTransportPickupReceiptEvidence(
    string EvidenceMethod,
    bool Signed,
    bool SignatureOmitted,
    string? SignatureOmissionReason,
    string? RecipientName,
    string? RecipientOrganization,
    string? RecipientSignature,
    string? DriverSignature);

public sealed record DriverTransportCompletionPhotoResult(
    bool Uploaded,
    bool CompletionRecorded,
    string Message,
    string? Url = null,
    string? ObjectName = null,
    DriverApiException? CompletionFailure = null,
    bool CompletionNotSubmitted = false);

public interface IDriverTransportCompletionPhotoService
{
    Task<DriverTransportCompletionPhotoResult> CompleteWithPhotoAsync(
        DriverTransportCompletionPhoto photo,
        CancellationToken cancellationToken = default);
}

public sealed class SampleDriverTransportCompletionPhotoService : IDriverTransportCompletionPhotoService
{
    public Task<DriverTransportCompletionPhotoResult> CompleteWithPhotoAsync(
        DriverTransportCompletionPhoto photo,
        CancellationToken cancellationToken = default)
    {
        var stepName = photo.Kind == DriverTransportCompletionPhotoKind.Pickup ? "상차 완료" : "하차 완료";
        var message = $"{stepName} 사진이 완료 Command에 첨부될 준비가 끝났습니다. 파일: {photo.FileName}";

        return Task.FromResult(new DriverTransportCompletionPhotoResult(
            Uploaded: false,
            CompletionRecorded: false,
            Message: message,
            ObjectName: BuildSampleObjectName(photo),
            CompletionNotSubmitted: true));
    }

    private static string BuildSampleObjectName(DriverTransportCompletionPhoto photo)
    {
        var folder = photo.Kind == DriverTransportCompletionPhotoKind.Pickup
            ? "pickup-complete"
            : "dropoff-complete";

        return $"driver-transports/{photo.TransportId}/{folder}/{photo.FileName}";
    }
}

public sealed class HttpDriverTransportCompletionPhotoService : IDriverTransportCompletionPhotoService
{
    private readonly HttpClient _httpClient;
    private readonly IAuthSession _authSession;
    private readonly AuthApiService _authApiService;
    private readonly IDriverTransportApiService _transportApi;
    private readonly IDriverCargoPhotoRecoveryStore? _recoveryStore;

    public HttpDriverTransportCompletionPhotoService(
        HttpClient httpClient,
        IAuthSession authSession,
        AuthApiService authApiService,
        IDriverTransportApiService transportApi,
        IDriverCargoPhotoRecoveryStore? recoveryStore = null)
    {
        _httpClient = httpClient;
        _authSession = authSession;
        _authApiService = authApiService;
        _transportApi = transportApi;
        _recoveryStore = recoveryStore;
    }

    public async Task<DriverTransportCompletionPhotoResult> CompleteWithPhotoAsync(
        DriverTransportCompletionPhoto photo,
        CancellationToken cancellationToken = default)
    {
        await _authSession.RestoreAsync(cancellationToken);
        var owner = _authSession.UserId;
        var revision = _authSession.SessionRevision;
        if (!_authSession.IsAuthenticated || string.IsNullOrWhiteSpace(owner))
            throw new UnauthorizedAccessException("로그인한 뒤 현재 운송을 다시 확인해 주세요.");
        if (photo.TransportId <= 0 || !Enum.IsDefined(photo.Kind))
            throw new ArgumentException("완료할 운송과 단계를 확인해 주세요.");
        if (photo.Bytes is not { Length: > 0 and <= DriverTransportCompletionPhotoLimits.MaximumUploadBytes })
            throw new ArgumentException("사진은 5MB 이하로 다시 촬영해 주세요.");
        var photoScope = _recoveryStore as IDriverCargoPhotoRecoveryScope;
        var photoIdentity = photoScope?.Capture(owner, cancellationToken);
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        void EnsureCurrentPhotoSession()
        {
            EnsureCurrentSession(owner, revision, operation.Token);
            if (photoScope is not null && (photoIdentity is null || !photoScope.IsCurrent(photoIdentity)))
                throw new OperationCanceledException("서버가 변경되어 이전 사진 완료 요청을 계속하지 않았습니다.", operation.Token);
        }
        void AuthenticationChanged()
        {
            if (!_authSession.IsAuthenticated || _authSession.SessionRevision != revision || _authSession.UserId != owner)
            {
                try { operation.Cancel(); }
                catch (ObjectDisposedException) { }
            }
        }
        _authSession.Changed += AuthenticationChanged;
        FileUploadResponse? upload = null;
        var completionAttempted = false;
        try
        {
            EnsureCurrentPhotoSession();
            if (_recoveryStore is not null || !string.IsNullOrWhiteSpace(photo.ExistingObjectName))
            {
                var current = await _transportApi.상세조회Async(photo.TransportId, operation.Token);
                EnsureCurrentPhotoSession();
                if (current is null || current.Id != photo.TransportId)
                    throw new InvalidOperationException("현재 계정의 운송을 확인하지 못했습니다.");
                var alreadyComplete = photo.Kind == DriverTransportCompletionPhotoKind.Pickup
                    ? current.상태 is "상차완료" or "운송중" or "하차지도착" or "인수완료"
                    : current.상태 == "인수완료";
                if (alreadyComplete)
                    return new(false, true, "같은 운송에서 해당 단계 완료를 확인했습니다. 현재 운송을 다시 조회해 주세요.");
                if (current.상태 != (photo.Kind == DriverTransportCompletionPhotoKind.Pickup ? "상차지도착" : "하차지도착"))
                    throw new InvalidOperationException("현재 운송 단계에서는 이 완료 요청을 보낼 수 없습니다.");
            }
            if (_recoveryStore is not null)
            {
                if (photoScope is not null) await photoScope.SaveAsync(photoIdentity!, photo, operation.Token);
                else await _recoveryStore.SaveAsync(owner, photo, operation.Token);
            }
            EnsureCurrentPhotoSession();
            upload = await ResolveUploadAsync(photo, operation.Token);
            EnsureCurrentPhotoSession();
            if (string.IsNullOrWhiteSpace(upload.ObjectName))
                throw new InvalidOperationException("사진 업로드 결과를 확인하지 못했습니다.");
            if (_recoveryStore is not null)
            {
                var uploadedPhoto = photo with { ExistingObjectName = upload.ObjectName, ExistingUrl = upload.Url };
                if (photoScope is not null) await photoScope.SaveAsync(photoIdentity!, uploadedPhoto, operation.Token);
                else await _recoveryStore.SaveAsync(owner, uploadedPhoto, operation.Token);
            }
            EnsureCurrentPhotoSession();
            try
            {
                completionAttempted = true;
                await CompleteTransportAsync(photo, upload, operation.Token);
                EnsureCurrentPhotoSession();
            }
            catch (Exception ex) when (IsCompletionRetryableFailure(ex) && !operation.IsCancellationRequested)
            {
                return new DriverTransportCompletionPhotoResult(
                    Uploaded: true,
                    CompletionRecorded: false,
                    Message: "사진 업로드는 확인했지만 완료 결과를 확인하지 못했습니다. 같은 운송의 현재 상태를 먼저 확인해 주세요.",
                    Url: upload.Url,
                    ObjectName: upload.ObjectName,
                    CompletionFailure: ex as DriverApiException);
            }

            var stepName = photo.Kind == DriverTransportCompletionPhotoKind.Pickup ? "상차 완료" : "하차 완료";
            return new DriverTransportCompletionPhotoResult(
                Uploaded: true,
                CompletionRecorded: true,
                Message: $"{stepName} 사진 업로드와 완료 처리가 끝났습니다.",
                Url: upload.Url,
                ObjectName: upload.ObjectName);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception) when (!completionAttempted)
        {
            EnsureCurrentPhotoSession();
            return new DriverTransportCompletionPhotoResult(
                Uploaded: !string.IsNullOrWhiteSpace(upload?.ObjectName), CompletionRecorded: false,
                Message: "완료 요청 전 준비를 마치지 못했습니다. 현재 운송을 확인한 뒤 다시 진행해 주세요.",
                Url: upload?.Url, ObjectName: upload?.ObjectName, CompletionNotSubmitted: true);
        }
        finally
        {
            _authSession.Changed -= AuthenticationChanged;
        }
    }

    private void EnsureCurrentSession(string owner, long revision, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_authSession.IsAuthenticated || _authSession.UserId != owner || _authSession.SessionRevision != revision)
            throw new OperationCanceledException("로그인 정보가 변경되었습니다.", cancellationToken);
    }

    private async Task<FileUploadResponse> ResolveUploadAsync(
        DriverTransportCompletionPhoto photo,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(photo.ExistingObjectName))
        {
            return new FileUploadResponse
            {
                ObjectName = photo.ExistingObjectName,
                Url = photo.ExistingUrl ?? string.Empty
            };
        }

        return await UploadPhotoAsync(photo, cancellationToken);
    }

    private async Task<FileUploadResponse> UploadPhotoAsync(
        DriverTransportCompletionPhoto photo,
        CancellationToken cancellationToken)
    {
        var authenticationError = await _authApiService.EnsureAccessTokenAsync(
            cancellationToken: cancellationToken);
        if (authenticationError is not null)
        {
            throw new UnauthorizedAccessException(authenticationError);
        }

        var response = await UploadPhotoOnceAsync(photo, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            response.Dispose();
            authenticationError = await _authApiService.EnsureAccessTokenAsync(
                forceRefresh: true,
                cancellationToken: cancellationToken);
            if (authenticationError is not null)
            {
                throw new UnauthorizedAccessException(authenticationError);
            }

            response = await UploadPhotoOnceAsync(photo, cancellationToken);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(await BuildFailureMessageAsync(response, "파일 업로드", cancellationToken));
            }

            var upload = await response.Content.ReadFromJsonAsync<FileUploadResponse>(cancellationToken: cancellationToken);
            return upload ?? throw new InvalidOperationException("파일 업로드 응답을 읽지 못했습니다.");
        }
    }

    private async Task<HttpResponseMessage> UploadPhotoOnceAsync(
        DriverTransportCompletionPhoto photo,
        CancellationToken cancellationToken)
    {
        using var content = new MultipartFormDataContent();
        using var fileContent = new ByteArrayContent(photo.Bytes);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(photo.ContentType);

        content.Add(fileContent, "file", photo.FileName);
        content.Add(new StringContent(ResolveCommandName(photo.Kind)), "commandName");
        content.Add(new StringContent(photo.TransportId.ToString(CultureInfo.InvariantCulture)), "referenceId");

        using var request = new HttpRequestMessage(HttpMethod.Post, "api/v1/files/upload")
        {
            Content = content
        };
        ApplyAuthorization(request);

        return await _httpClient.SendAsync(request, cancellationToken);
    }

    private async Task CompleteTransportAsync(
        DriverTransportCompletionPhoto photo,
        FileUploadResponse upload,
        CancellationToken cancellationToken)
    {
        if (photo.Kind == DriverTransportCompletionPhotoKind.Pickup)
        {
            var result = await _transportApi.상차완료Async(photo.TransportId, new 기사운송상차완료요청
            {
                상차사진ObjectName = upload.ObjectName,
                상차사진Url = upload.Url,
                인수증증빙방식 = photo.ReceiptEvidence?.EvidenceMethod,
                인수자명 = photo.ReceiptEvidence?.RecipientName,
                인수자소속 = photo.ReceiptEvidence?.RecipientOrganization,
                인수자서명 = photo.ReceiptEvidence?.RecipientSignature,
                기사서명 = photo.ReceiptEvidence?.DriverSignature,
                인수증확인완료 = photo.ReceiptEvidence?.Signed == true,
                인수증서명생략확인 = photo.ReceiptEvidence?.SignatureOmitted == true,
                인수증서명생략사유 = photo.ReceiptEvidence?.SignatureOmissionReason
            }, cancellationToken);
            if (result is null || result.Id != photo.TransportId || result.상태 != "상차완료")
                throw new InvalidOperationException("요청한 운송의 상차 완료 결과를 확인하지 못했습니다.");
            return;
        }

        var dropoffResult = await _transportApi.하차완료Async(photo.TransportId, new 기사운송하차완료요청
        {
            하차사진ObjectName = upload.ObjectName,
            하차사진Url = upload.Url
        }, cancellationToken);
        if (dropoffResult is null || dropoffResult.Id != photo.TransportId || dropoffResult.상태 != "인수완료")
            throw new InvalidOperationException("요청한 운송의 하차 완료 결과를 확인하지 못했습니다.");
    }

    private void ApplyAuthorization(HttpRequestMessage request)
    {
        if (!string.IsNullOrWhiteSpace(_authSession.AccessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _authSession.AccessToken);
        }
    }

    private static string ResolveCommandName(DriverTransportCompletionPhotoKind kind)
    {
        return kind == DriverTransportCompletionPhotoKind.Pickup
            ? "TransportPickupComplete"
            : "TransportDropoffComplete";
    }

    private static bool IsCompletionRetryableFailure(Exception exception)
        => exception is HttpRequestException or TaskCanceledException or InvalidOperationException;

    private static async Task<string> BuildFailureMessageAsync(
        HttpResponseMessage response,
        string operationName,
        CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return string.IsNullOrWhiteSpace(body)
            ? $"{operationName} API 실패: HTTP {(int)response.StatusCode}"
            : $"{operationName} API 실패: HTTP {(int)response.StatusCode}: {body}";
    }

    private sealed class FileUploadResponse
    {
        public string BucketName { get; set; } = string.Empty;
        public string ObjectName { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
    }
}
