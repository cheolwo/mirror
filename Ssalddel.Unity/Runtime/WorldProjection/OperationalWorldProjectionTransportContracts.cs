using System;
using System.Threading;
using System.Threading.Tasks;

namespace Ssalddel.Unity.WorldProjection
{
    public static class WorldProjectionSourceModeCodes
    {
        public const string SimulationSession = "SimulationSession";
        public const string OperationalSnapshot = "OperationalSnapshot";
    }

    /// <summary>
    /// 화면 모듈 하나가 사용할 권위 자료원을 명시적으로 고정합니다.
    /// 운영 상태 사본과 Simulation Session 사이의 자동 fallback은 허용하지 않습니다.
    /// </summary>
    public sealed class WorldProjectionSourceSelection
    {
        public WorldProjectionSourceSelection(
            string moduleStableId,
            string sourceModeCode,
            bool allowAutomaticFallback = false)
        {
            if (string.IsNullOrWhiteSpace(moduleStableId))
            {
                throw new ArgumentException(
                    "WorldProjectionModuleStableIdMissing",
                    nameof(moduleStableId));
            }

            if (!string.Equals(
                    sourceModeCode,
                    WorldProjectionSourceModeCodes.SimulationSession,
                    StringComparison.Ordinal)
                && !string.Equals(
                    sourceModeCode,
                    WorldProjectionSourceModeCodes.OperationalSnapshot,
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "WorldProjectionSourceModeInvalid",
                    nameof(sourceModeCode));
            }

            if (allowAutomaticFallback)
            {
                throw new ArgumentException(
                    "WorldProjectionAutomaticFallbackForbidden",
                    nameof(allowAutomaticFallback));
            }

            ModuleStableId = moduleStableId.Trim();
            SourceModeCode = sourceModeCode;
        }

        public string ModuleStableId { get; }

        public string SourceModeCode { get; }

        public bool AllowAutomaticFallback => false;
    }

    /// <summary>
    /// 비밀 정보를 포함하지 않는 운영 서버의 읽기 전용 World Projection 접속 정보입니다.
    /// </summary>
    public sealed class OperationalWorldProjectionEndpoint
    {
        public OperationalWorldProjectionEndpoint(
            string baseUrl,
            int timeoutSeconds = 15)
        {
            if (!Uri.TryCreate(baseUrl?.Trim(), UriKind.Absolute, out var parsed))
            {
                throw new ArgumentException(
                    "OperationalWorldProjectionBaseUrlInvalid",
                    nameof(baseUrl));
            }

            var isHttp = string.Equals(
                    parsed.Scheme,
                    Uri.UriSchemeHttp,
                    StringComparison.OrdinalIgnoreCase)
                || string.Equals(
                    parsed.Scheme,
                    Uri.UriSchemeHttps,
                    StringComparison.OrdinalIgnoreCase);
            if (!isHttp
                || !string.IsNullOrEmpty(parsed.Query)
                || !string.IsNullOrEmpty(parsed.Fragment))
            {
                throw new ArgumentException(
                    "OperationalWorldProjectionBaseUrlInvalid",
                    nameof(baseUrl));
            }

            if (timeoutSeconds <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(timeoutSeconds));
            }

            var builder = new UriBuilder(parsed);
            if (!builder.Path.EndsWith("/", StringComparison.Ordinal))
            {
                builder.Path += "/";
            }

            BaseUri = builder.Uri;
            TimeoutSeconds = timeoutSeconds;
        }

        public Uri BaseUri { get; }

        public int TimeoutSeconds { get; }

        public Uri BuildRequestUri(string relativeRoute)
        {
            if (string.IsNullOrWhiteSpace(relativeRoute)
                || Uri.TryCreate(relativeRoute.Trim(), UriKind.Absolute, out _))
            {
                throw new ArgumentException(
                    "OperationalWorldProjectionRouteInvalid",
                    nameof(relativeRoute));
            }

            return new Uri(BaseUri, relativeRoute.Trim().TrimStart('/'));
        }
    }

    /// <summary>
    /// 접근 토큰은 실행 중 메모리에서만 제공하며 Scene·Prefab·배포 자원에 저장하지 않습니다.
    /// </summary>
    public interface IOperationalRuntimeAccessTokenProvider
    {
        string GetAccessToken();
    }

    /// <summary>
    /// 운영 서버에 대한 GET 전용 전송 경계입니다. Command 전송 메서드를 제공하지 않습니다.
    /// </summary>
    public interface IOperationalWorldProjectionTransport
    {
        Task<string?> GetAsync(
            string relativeRoute,
            bool allowNotFound,
            bool requiresAuthentication,
            CancellationToken cancellationToken = default);
    }

    public sealed class OperationalWorldProjectionException : Exception
    {
        public OperationalWorldProjectionException(
            string failureCode,
            string relativeRoute,
            long? statusCode = null,
            Exception? innerException = null)
            : base(BuildMessage(failureCode, relativeRoute, statusCode), innerException)
        {
            FailureCode = failureCode;
            RelativeRoute = relativeRoute;
            StatusCode = statusCode;
        }

        public string FailureCode { get; }

        public string RelativeRoute { get; }

        public long? StatusCode { get; }

        private static string BuildMessage(
            string failureCode,
            string relativeRoute,
            long? statusCode)
            => failureCode
               + ":"
               + relativeRoute
               + (statusCode.HasValue ? ":" + statusCode.Value : string.Empty);
    }
}
