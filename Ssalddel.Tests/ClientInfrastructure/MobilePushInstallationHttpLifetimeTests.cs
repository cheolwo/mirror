using System.Net;
using FDriverApp.Services;
using Ssalddel.Client.Infrastructure.Notifications;
using Ssalddel.Client.Infrastructure.Security;
using Ssalddel.Tests.Clients;

namespace Ssalddel.Tests.ClientInfrastructure;

public sealed class MobilePushInstallationHttpLifetimeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Put_PreservesPayloadAndKeepsHttpResourcesUntilRegistrationIsSaved(bool foodDriver)
    {
        using var fixture = new Fixture(foodDriver);
        fixture.BeforePersist = registered =>
        {
            Assert.True(registered);
            Assert.False(fixture.Handler.ResponseContent!.Disposed);
            Assert.False(fixture.Handler.RequestContent!.Disposed);
        };

        var result = await fixture.RegisterAsync();

        Assert.Equal("Registered", result.State);
        Assert.Equal(1, fixture.Handler.CallCount);
        Assert.Equal(HttpMethod.Put, fixture.Handler.Method);
        Assert.Equal("https://localhost/api/v1/mobile/push/installations", fixture.Handler.Uri!.AbsoluteUri);
        Assert.Equal("Bearer", fixture.Handler.Scheme);
        Assert.Equal(foodDriver ? "test-access" : "member-access", fixture.Handler.Token);
        Assert.Equal("application/json; charset=utf-8", fixture.Handler.ContentType);
        var installationId = foodDriver
            ? FDriverPushRegistrationService.InstallationForOwner(Fixture.DeviceId, "test-driver")
            : Fixture.DeviceId;
        var expectedPayload = foodDriver
            ? $"{{\"installationId\":\"{installationId}\",\"appKey\":\"FoodDeliveryDriverApp\",\"platform\":\"android\",\"pushToken\":\"push-token\",\"appVersion\":null,\"deviceModel\":null}}"
            : $"{{\"installationId\":\"{installationId}\",\"appKey\":\"ssalddel.shipper\",\"platform\":\"Android\",\"pushToken\":\"push-token\",\"appVersion\":\"1.2.3\",\"deviceModel\":\"test-device\"}}";
        Assert.Equal(expectedPayload, fixture.Handler.Payload);
        Assert.Equal(new[] { "send:PUT", "registered-save", "response-dispose", "request-content-dispose" }, fixture.Events);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Delete_NotFoundIsSuccessful_AndPreservesEachCallersDisposalBoundary(bool foodDriver)
    {
        using var fixture = new Fixture(foodDriver, registered: true);
        fixture.Handler.Status = HttpStatusCode.NotFound;
        fixture.BeforePersist = registered =>
        {
            if (registered) return; // Persist revocation intent before issuing DELETE.
            Assert.Equal(foodDriver, fixture.Handler.ResponseContent!.Disposed);
            Assert.Equal(foodDriver, fixture.Handler.RequestContent!.Disposed);
        };

        var result = await fixture.DeactivateAsync();

        Assert.Equal("Registered", result.State);
        Assert.Equal(1, fixture.Handler.CallCount);
        Assert.Equal(HttpMethod.Delete, fixture.Handler.Method);
        var installationId = foodDriver
            ? FDriverPushRegistrationService.InstallationForOwner(Fixture.DeviceId, "test-driver")
            : Fixture.DeviceId;
        Assert.Equal($"https://localhost/api/v1/mobile/push/installations/{Uri.EscapeDataString(installationId)}",
            fixture.Handler.Uri!.AbsoluteUri);
        Assert.Equal("Bearer", fixture.Handler.Scheme);
        Assert.Equal(foodDriver ? "test-access" : "member-access", fixture.Handler.Token);
        Assert.Null(fixture.Handler.Payload);
        Assert.Equal(foodDriver
            ? new[] { "revoke-save", "send:DELETE", "response-dispose", "request-content-dispose", "revoked-save" }
            : new[] { "revoke-save", "send:DELETE", "revoked-save", "response-dispose", "request-content-dispose" }, fixture.Events);
    }

    [Theory]
    [InlineData(false, "network")]
    [InlineData(true, "network")]
    [InlineData(false, "timeout")]
    [InlineData(true, "timeout")]
    [InlineData(false, "status")]
    [InlineData(true, "status")]
    public async Task Put_TransportFailureIsNotRetriedOrStoredAsRegistration(bool foodDriver, string failure)
    {
        using var fixture = new Fixture(foodDriver);
        fixture.Handler.Failure = failure;
        fixture.Handler.Status = HttpStatusCode.ServiceUnavailable;

        var result = await fixture.RegisterAsync();

        Assert.Equal("Failed", result.State);
        Assert.Equal(1, fixture.Handler.CallCount);
        Assert.True(fixture.Handler.RequestContent!.Disposed);
        Assert.DoesNotContain("registered-save", fixture.Events);
        if (!foodDriver && failure == "timeout")
            Assert.Equal("모바일 Push 설치 등록 시간이 초과되었습니다.", result.Message);
        else if (foodDriver && failure == "timeout")
            Assert.Equal("알림 연결 중 계정이 변경되었습니다. 현재 계정으로 다시 확인하세요.", result.Message);
        else if (!foodDriver && failure == "status")
            Assert.Equal("모바일 Push 설치 등록 실패: HTTP 503", result.Message);
        else
            Assert.Equal(foodDriver
                ? "알림 연결을 확인하지 못했습니다. 앱 복귀와 자동 갱신으로 요청을 확인합니다."
                : "알림 연결을 확인하지 못했습니다. 앱에서 다시 확인해 주세요.", result.Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Put_CallerCancellationPropagatesWithoutRetryOrRegistrationSave(bool foodDriver)
    {
        using var fixture = new Fixture(foodDriver);
        using var cancellation = new CancellationTokenSource();
        fixture.Handler.Cancel = cancellation;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.RegisterAsync(cancellation.Token));

        Assert.Equal(1, fixture.Handler.CallCount);
        Assert.True(fixture.Handler.RequestContent!.Disposed);
        Assert.DoesNotContain("registered-save", fixture.Events);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task MalformedBearer_DoesNotSendOrSaveRegistration_AndRetainsRevocationIntent(bool foodDriver, bool deactivate)
    {
        using var fixture = new Fixture(foodDriver, registered: deactivate);
        await fixture.SetAccessTokenAsync("invalid\r\nAuthorization: injected");
        fixture.BeforePersist = hasRegisteredOwner => Assert.True(hasRegisteredOwner);

        var result = deactivate ? await fixture.DeactivateAsync() : await fixture.RegisterAsync();

        Assert.Equal("Failed", result.State);
        Assert.Equal(0, fixture.Handler.CallCount);
        Assert.Equal(deactivate ? new[] { "revoke-save" } : [], fixture.Events);
        Assert.Equal(foodDriver
            ? "알림 연결을 확인하지 못했습니다. 앱 복귀와 자동 갱신으로 요청을 확인합니다."
            : deactivate
                ? "알림 해제를 확인하지 못했습니다. 같은 계정으로 다시 연결하면 재시도합니다."
                : "알림 연결을 확인하지 못했습니다. 앱에서 다시 확인해 주세요.", result.Message);
    }

    private sealed class Fixture : IDisposable
    {
        public const string DeviceId = "device /?%";
        public List<string> Events { get; } = [];
        public Action<bool>? BeforePersist { get; set; }
        public RecordingHandler Handler { get; }
        private readonly HttpClient _http;
        private readonly SsalddelMobilePushInstallationClient? _member;
        private readonly FDriverPushRegistrationService? _driver;
        private readonly FDriverTestSession? _driverSession;
        private string _memberAccessToken = "member-access";

        public Fixture(bool foodDriver, bool registered = false)
        {
            Handler = new(Events);
            _http = new(Handler) { BaseAddress = new("https://localhost/") };
            if (foodDriver)
            {
                var session = _driverSession = new FDriverTestSession();
                var store = new DriverStore(new(DeviceId, "push-token", registered ? "test-driver" : null), Persist);
                _driver = new(_http, session, new(_http, session), store);
            }
            else
            {
                var store = new MemberStore(new(DeviceId)
                {
                    PushToken = "push-token", RegisteredOwnerId = registered ? "member" : null
                }, Persist);
                _member = new(_http, new Provider(), () => _memberAccessToken, store, () => "member", () => 1);
            }
        }

        private void Persist(bool registered, bool pending)
        {
            BeforePersist?.Invoke(registered);
            Events.Add(pending ? "revoke-save" : registered ? "registered-save" : "revoked-save");
        }

        public Task SetAccessTokenAsync(string accessToken)
        {
            if (_driverSession is not null)
                return _driverSession.ApplyAsync(new ClientAuthTokenSnapshot(accessToken, DateTime.UtcNow.AddMinutes(30),
                    "test-refresh", DateTime.UtcNow.AddHours(1), "test-driver", "검증 기사", ["Driver"]));
            _memberAccessToken = accessToken;
            return Task.CompletedTask;
        }

        public async Task<Result> RegisterAsync(CancellationToken cancellationToken = default)
        {
            if (_member is not null)
            {
                var result = await _member.EnsureRegisteredAsync(cancellationToken);
                return new(result.State.ToString(), result.ErrorMessage);
            }
            var driverResult = await _driver!.EnsureRegisteredAsync(cancellationToken);
            return new(driverResult.State.ToString(), driverResult.Message);
        }

        public async Task<Result> DeactivateAsync()
        {
            if (_member is not null)
            {
                var result = await _member.DeactivateCurrentOwnerAsync();
                return new(result.State.ToString(), result.ErrorMessage);
            }
            var driverResult = await _driver!.DeactivateCurrentOwnerAsync();
            return new(driverResult.State.ToString(), driverResult.Message);
        }

        public void Dispose() => _http.Dispose();
    }

    private sealed record Result(string State, string? Message);

    private sealed class Provider : ISsalddelMobilePushTokenProvider
    {
        public Task<SsalddelMobilePushTokenSnapshot?> GetCurrentAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<SsalddelMobilePushTokenSnapshot?>(new(Fixture.DeviceId, "ssalddel.shipper", "Android",
                "push-token", "1.2.3", "test-device"));
    }

    private sealed class MemberStore(MemberDeliveryPushState state, Action<bool, bool> persist) : IMemberDeliveryPushStateStore
    {
        public Task<MemberDeliveryPushState> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(state);
        public Task UpdateAsync(Func<MemberDeliveryPushState, MemberDeliveryPushState> update, CancellationToken cancellationToken = default)
        {
            var next = update(state);
            persist(next.RegisteredOwnerId is not null, next.RevocationPending);
            state = next;
            return Task.CompletedTask;
        }
    }

    private sealed class DriverStore(FDriverPushDeviceState state, Action<bool, bool> persist) : IFDriverPushDeviceStore
    {
        public bool IsConfigured => true;
        public Task<FDriverPushDeviceState> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(state);
        public Task SaveAsync(FDriverPushDeviceState next, CancellationToken cancellationToken = default)
        {
            persist(next.RegisteredOwnerId is not null, next.RevocationPending);
            state = next;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingHandler(List<string> events) : HttpMessageHandler
    {
        public int CallCount { get; private set; }
        public HttpMethod? Method { get; private set; }
        public Uri? Uri { get; private set; }
        public string? Scheme { get; private set; }
        public string? Token { get; private set; }
        public string? ContentType { get; private set; }
        public string? Payload { get; private set; }
        public HttpStatusCode Status { get; set; } = HttpStatusCode.NoContent;
        public string? Failure { get; set; }
        public CancellationTokenSource? Cancel { get; set; }
        public TrackingContent? RequestContent { get; private set; }
        public TrackingContent? ResponseContent { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            Method = request.Method;
            Uri = request.RequestUri;
            Scheme = request.Headers.Authorization?.Scheme;
            Token = request.Headers.Authorization?.Parameter;
            ContentType = request.Content?.Headers.ContentType?.ToString();
            Payload = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            events.Add($"send:{request.Method}");
            request.Content = RequestContent = new(request.Content ?? new ByteArrayContent([]), () => events.Add("request-content-dispose"));
            if (Cancel is not null)
            {
                Cancel.Cancel();
                cancellationToken.ThrowIfCancellationRequested();
            }
            if (Failure == "network") throw new HttpRequestException("network unavailable");
            if (Failure == "timeout") throw new TaskCanceledException("transport timeout");
            return new(Status)
            {
                Content = ResponseContent = new(new ByteArrayContent([]), () => events.Add("response-dispose"))
            };
        }
    }

    private sealed class TrackingContent(HttpContent inner, Action disposed) : HttpContent
    {
        public bool Disposed { get; private set; }
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => inner.CopyToAsync(stream);
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
        protected override void Dispose(bool disposing)
        {
            if (disposing && !Disposed)
            {
                Disposed = true;
                disposed();
                inner.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
