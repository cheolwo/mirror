using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Options;
using 살뜰.Services.Options;

namespace 살뜰.Services.Notifications
{
    public sealed class FirebaseFcmPushService : IFcmPushService
    {
        private const string FirebaseMessagingScope = "https://www.googleapis.com/auth/firebase.messaging";

        private readonly HttpClient _httpClient;
        private readonly PushNotificationsOptions _options;
        private readonly ILogger<FirebaseFcmPushService> _logger;

        public FirebaseFcmPushService(
            HttpClient httpClient,
            IOptions<PushNotificationsOptions> options,
            ILogger<FirebaseFcmPushService> logger)
        {
            _httpClient = httpClient;
            _options = options.Value;
            _logger = logger;
        }

        public async Task<bool> SendToTokenAsync(
            string token,
            string title,
            string body,
            IReadOnlyDictionary<string, string> data,
            CancellationToken cancellationToken = default)
            => await SendAsync(
                new FcmPushMessage(token, title, body, data, null, HighPriority: true),
                cancellationToken).ConfigureAwait(false);

        public async Task<bool> SendAsync(
            FcmPushMessage message,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(message);
            if (string.IsNullOrWhiteSpace(message.Token))
            {
                return false;
            }

            var serviceAccountJsonPath = ResolveServiceAccountJsonPath();
            if (!string.IsNullOrWhiteSpace(serviceAccountJsonPath))
            {
                return await SendHttpV1Async(message, serviceAccountJsonPath, cancellationToken)
                    .ConfigureAwait(false);
            }

            if (!string.IsNullOrWhiteSpace(_options.ServerKey))
            {
                return await SendLegacyAsync(message, cancellationToken).ConfigureAwait(false);
            }

            _logger.LogDebug(
                "Action={Action} Result={Result} Reason={Reason} TraceId={TraceId} OccurredAt={OccurredAt}",
                "FcmSend",
                "Skipped",
                "PushNotifications:ServiceAccountJsonPath or GOOGLE_APPLICATION_CREDENTIALS is not configured",
                System.Diagnostics.Activity.Current?.TraceId.ToString() ?? string.Empty,
                DateTime.UtcNow);

            return false;
        }

        private async Task<bool> SendHttpV1Async(
            FcmPushMessage message,
            string serviceAccountJsonPath,
            CancellationToken cancellationToken)
        {
            if (!File.Exists(serviceAccountJsonPath))
            {
                _logger.LogWarning(
                    "Action={Action} Result={Result} Reason={Reason} TraceId={TraceId} OccurredAt={OccurredAt}",
                    "FcmSend",
                    "Failed",
                    "Firebase service account file was not found",
                    System.Diagnostics.Activity.Current?.TraceId.ToString() ?? string.Empty,
                    DateTime.UtcNow);
                return false;
            }

            var projectId = await ResolveProjectIdAsync(serviceAccountJsonPath, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(projectId))
            {
                _logger.LogWarning(
                    "Action={Action} Result={Result} Reason={Reason} TraceId={TraceId} OccurredAt={OccurredAt}",
                    "FcmSend",
                    "Failed",
                    "Firebase project id is not configured",
                    System.Diagnostics.Activity.Current?.TraceId.ToString() ?? string.Empty,
                    DateTime.UtcNow);
                return false;
            }

            var accessToken = await CreateAccessTokenAsync(serviceAccountJsonPath, cancellationToken).ConfigureAwait(false);
            var json = FcmPushPayloadSerializer.SerializeHttpV1(message);
            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"https://fcm.googleapis.com/v1/projects/{projectId}/messages:send")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            return await SendAndLogAsync(request, cancellationToken).ConfigureAwait(false);
        }

        private async Task<bool> SendLegacyAsync(
            FcmPushMessage message,
            CancellationToken cancellationToken)
        {
            var json = FcmPushPayloadSerializer.SerializeLegacy(message);
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://fcm.googleapis.com/fcm/send")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("key", _options.ServerKey);

            return await SendAndLogAsync(request, cancellationToken).ConfigureAwait(false);
        }

        private async Task<bool> SendAndLogAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                return true;
            }

            _logger.LogWarning(
                "Action={Action} Result={Result} Reason={Reason} StatusCode={StatusCode} TraceId={TraceId} OccurredAt={OccurredAt}",
                "FcmSend",
                "Failed",
                "FCM provider rejected the message",
                response.StatusCode,
                System.Diagnostics.Activity.Current?.TraceId.ToString() ?? string.Empty,
                DateTime.UtcNow);

            return false;
        }

        private string ResolveServiceAccountJsonPath()
        {
            var configuredPath = string.IsNullOrWhiteSpace(_options.ServiceAccountJsonPath)
                ? Environment.GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS")
                : _options.ServiceAccountJsonPath;

            return string.IsNullOrWhiteSpace(configuredPath)
                ? string.Empty
                : Environment.ExpandEnvironmentVariables(configuredPath);
        }

        private async Task<string> ResolveProjectIdAsync(string serviceAccountJsonPath, CancellationToken cancellationToken)
        {
            if (!string.IsNullOrWhiteSpace(_options.ProjectId))
            {
                return _options.ProjectId;
            }

            await using var stream = File.OpenRead(serviceAccountJsonPath);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            return document.RootElement.TryGetProperty("project_id", out var projectIdElement)
                ? projectIdElement.GetString() ?? string.Empty
                : string.Empty;
        }

        private static async Task<string> CreateAccessTokenAsync(
            string serviceAccountJsonPath,
            CancellationToken cancellationToken)
        {
            await using var stream = File.OpenRead(serviceAccountJsonPath);
            var credential = ServiceAccountCredential
                .FromServiceAccountData(stream)
                .ToGoogleCredential()
                .CreateScoped(FirebaseMessagingScope);

            return await ((ITokenAccess)credential)
                .GetAccessTokenForRequestAsync(null, cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
