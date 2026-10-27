using System.Collections.Concurrent;
using System.Net;
using System.Reflection;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Contracts.Food;
using Ssalddel.Controllers.Driver.Food;
using Ssalddel.Controllers.Driver.Notification07;
using Ssalddel.Controllers.Driver.Settings05;
using Ssalddel.Controllers.Food;
using Ssalddel.Security;
using Ssalddel.Services.Food;
using 살뜰.Infrastructure.Security;
using 살뜰.Data;
using 살뜰.Services.DeliveryZones;
using 살뜰.Services.Dispatch.Coordination;
using 살뜰.Services.Dispatch.Notification;
using 살뜰.Services.Notifications;
using 살뜰.Services.Storage.Local;
using 살뜰.Services.Transport;
using 살뜰.Services.Versioning;
using 살뜰.도메인.운송;
using 살뜰.도메인.공통;
using Ssalddel.Controllers;

namespace Ssalddel.Tests.Services.Dispatch.AI;

public sealed partial class FoodDispatchWorkflowScenarioFixture
{
    private sealed class Encryption : IPersonalDataEncryptionService
    { public string? Protect(string? value) => value; public string? Unprotect(string? value) => value; }

    private sealed class Flags : IVersionFeatureFlagService
    { public bool IsEnabled(string featureKey) => true; public IReadOnlyDictionary<string, bool> GetAll() => new Dictionary<string, bool>(); }

    private sealed class CountingModelHandler(HttpClient inner) : HttpMessageHandler
    {
        private int _calls;
        public int Calls => _calls;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _calls);
            using var forwarded = new HttpRequestMessage(request.Method, request.RequestUri) { Content = request.Content };
            return await inner.SendAsync(forwarded, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }
    }

    private sealed class MutableDriverStates : I국내화물운송기사상태Store
    {
        private readonly ConcurrentDictionary<string, 국내화물운송기사상태Snapshot> _items = new(StringComparer.Ordinal);
        public Task UpsertAsync(국내화물운송기사상태Snapshot snapshot, CancellationToken ct = default)
        { ct.ThrowIfCancellationRequested(); _items[snapshot.DriverId] = snapshot; return Task.CompletedTask; }
        public Task<국내화물운송기사상태Snapshot?> GetAsync(string id, CancellationToken ct = default)
            => Task.FromResult(_items.GetValueOrDefault(id));
        public Task<IReadOnlyList<국내화물운송기사상태Snapshot>> 활성기사조회Async(int take, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<국내화물운송기사상태Snapshot>>(_items.Values.OrderBy(x => x.DriverId, StringComparer.Ordinal).Take(take).ToArray());
        public Task<IReadOnlyList<국내화물운송기사상태Snapshot>> 위치반경조회Async(decimal latitude, decimal longitude, decimal radiusKm,
            int take, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<국내화물운송기사상태Snapshot>>(_items.Values
                .Where(x => x.Latitude.HasValue && x.Longitude.HasValue
                    && Math.Abs(x.Latitude.Value - latitude) * 111m + Math.Abs(x.Longitude.Value - longitude) * 88m <= radiusKm)
                .OrderBy(x => x.DriverId, StringComparer.Ordinal).Take(take).ToArray());
        public Task RemoveAsync(string id, CancellationToken ct = default)
        { _items.TryRemove(id, out _); return Task.CompletedTask; }
    }

    private sealed class MemoryTokens : IDriverPushTokenStore
    {
        private readonly ConcurrentDictionary<string, string> _tokens = new(StringComparer.Ordinal);
        public Task SetAsync(string id, string token, CancellationToken ct = default) { _tokens[id] = token; return Task.CompletedTask; }
        public Task<string?> GetAsync(string id, CancellationToken ct = default) => Task.FromResult(_tokens.GetValueOrDefault(id));
        public Task ClearAsync(string id, CancellationToken ct = default) { _tokens.TryRemove(id, out _); return Task.CompletedTask; }
    }

    private sealed class MemoryNotificationSettings : IDriverNotificationSettingsStore
    {
        private readonly ConcurrentDictionary<string, DriverNotificationSettings> _settings = new(StringComparer.Ordinal);
        public Task<DriverNotificationSettings> GetAsync(string id, CancellationToken ct = default)
            => Task.FromResult(_settings.GetValueOrDefault(id) ?? new(true, true, true, true, false, false, true, true));
        public Task SetAsync(string id, DriverNotificationSettings value, CancellationToken ct = default)
        { _settings[id] = value; return Task.CompletedTask; }
    }

    private sealed class RecordingPush : IFcmPushService
    {
        public List<FcmPushMessage> Messages { get; } = [];
        public bool Succeeded { get; set; } = true;
        public Task<bool> SendAsync(FcmPushMessage message, CancellationToken ct = default)
        { ct.ThrowIfCancellationRequested(); Messages.Add(message); return Task.FromResult(Succeeded); }
        public Task<bool> SendToTokenAsync(string token, string title, string body, IReadOnlyDictionary<string, string> data, CancellationToken ct = default)
            => throw new InvalidOperationException("Food pushes must use data-only SendAsync.");
    }

    private sealed class NotificationCreationBoundary(FoodDispatchWorkflowScenarioFixture fixture, I배차추천알림Service actual) : I배차추천알림Service
    {
        public Task 추천알림요청생성Async(long queue, string request, string driver, int round, CancellationToken ct = default)
            => fixture.ThrowNotificationCreation ? throw new IOException("SyntheticOutboxCreationFailure")
                : actual.추천알림요청생성Async(queue, request, driver, round, ct);
        public Task<int> 대기알림발송Async(int take = 100, CancellationToken ct = default)
            => throw new InvalidOperationException("Use the food business scoped drain.");
    }

    private sealed class SyntheticAreaProjection : I운송원장배달권연결Service
    {
        public Task<운송원장배달권연결결과> 투영추적Async(운송원장 queue, CancellationToken ct = default)
            => Task.FromResult(new 운송원장배달권연결결과(
                국내화물배달권정책.판정(new(queue.픽업_위도!.Value, queue.픽업_경도!.Value), queue.픽업_도로명주소),
                국내화물배달권정책.판정(new(queue.하차_위도!.Value, queue.하차_경도!.Value), queue.하차_도로명주소)));
    }

    private sealed class SyntheticTransportRealtime : ITransportRequestLedgerRealtimeService
    { public Task PublishAsync(string requestId, string eventType, CancellationToken ct = default) => Task.CompletedTask; }

    private sealed class SyntheticGeocoder(decimal dropoffLatitude, decimal dropoffLongitude) : IKakao좌표변환Service
    {
        public Task<Kakao주소정보?> 주소정보조회Async(string address, CancellationToken ct = default)
            => Task.FromResult<Kakao주소정보?>(new(address, address, "합성시", "합성구", "합성동", dropoffLatitude, dropoffLongitude));
        public Task<(double 위도, double 경도)?> 도로명주소좌표변환Async(string address, CancellationToken ct = default)
            => Task.FromResult<(double 위도, double 경도)?>(((double)dropoffLatitude, (double)dropoffLongitude));
        public Task<Kakao지역정보?> 좌표지역정보조회Async(decimal latitude, decimal longitude, CancellationToken ct = default)
            => Task.FromResult<Kakao지역정보?>(new("합성시", "합성구", "합성동", "합성도로"));
    }

    private sealed class HttpActor(IHttpContextAccessor context) : ICurrentUserAccessor
    {
        public string? UserId => context.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
        public string? Role => context.HttpContext?.User.FindFirstValue(ClaimTypes.Role);
    }

    private sealed class Controllers : IApplicationFeatureProvider<ControllerFeature>
    {
        public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature)
        {
            foreach (var controller in new[] { typeof(음식주문Controller), typeof(음식배달기사업무Controller),
                         typeof(기사운영배차공통Controller), typeof(기사알림Controller) })
                feature.Controllers.Add(controller.GetTypeInfo());
        }
    }

    public sealed class FixtureAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var value = Request.Headers.Authorization.ToString();
            if (!value.StartsWith("Bearer ", StringComparison.Ordinal)) return Task.FromResult(AuthenticateResult.NoResult());
            var token = value[7..];
            var claims = new List<Claim>();
            if (token.StartsWith("fixture-driver:scenario-", StringComparison.Ordinal)
                || token == "fixture-driver:other-fixture-driver")
            { claims.Add(new(ClaimTypes.NameIdentifier, token[15..])); claims.Add(new(ClaimTypes.Role, 역할명.기사)); }
            else if (token == "fixture-orderer") claims.Add(new(ClaimTypes.NameIdentifier, OrdererId));
            else if (token == "fixture-restaurant")
            {
                claims.Add(new(ClaimTypes.NameIdentifier, RestaurantUserId)); claims.Add(new("fixture-role", "restaurant"));
                claims.Add(new(음식점접근ClaimTypes.음식점Id, RestaurantId.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            }
            else return Task.FromResult(AuthenticateResult.Fail("Unknown synthetic fixture token."));
            return Task.FromResult(AuthenticateResult.Success(new(new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name)), Scheme.Name)));
        }
        protected override Task HandleChallengeAsync(AuthenticationProperties properties) { Response.StatusCode = 401; return Task.CompletedTask; }
        protected override Task HandleForbiddenAsync(AuthenticationProperties properties) { Response.StatusCode = 403; return Task.CompletedTask; }
    }
}
