#if UNITY_5_3_OR_NEWER
using System;
using System.Threading;
using System.Threading.Tasks;
using Ssalddel.Unity.WorldProjection;
using UnityEngine.Networking;

namespace Ssalddel.Unity.OperationalTransport
{
    /// <summary>
    /// 운영 서버의 승인된 World Projection을 읽기만 하는 Unity 전송 구현입니다.
    /// </summary>
    public sealed class UnityWebRequestOperationalWorldProjectionTransport
        : IOperationalWorldProjectionTransport
    {
        private readonly OperationalWorldProjectionEndpoint endpoint;
        private readonly IOperationalRuntimeAccessTokenProvider? tokenProvider;

        public UnityWebRequestOperationalWorldProjectionTransport(
            OperationalWorldProjectionEndpoint endpoint,
            IOperationalRuntimeAccessTokenProvider? tokenProvider = null)
        {
            this.endpoint = endpoint
                ?? throw new ArgumentNullException(nameof(endpoint));
            this.tokenProvider = tokenProvider;
        }

        public async Task<string?> GetAsync(
            string relativeRoute,
            bool allowNotFound,
            bool requiresAuthentication,
            CancellationToken cancellationToken = default)
        {
            var token = tokenProvider?.GetAccessToken();
            if (requiresAuthentication && string.IsNullOrWhiteSpace(token))
            {
                throw new OperationalWorldProjectionException(
                    "OperationalAccessTokenMissing",
                    relativeRoute);
            }

            var requestUri = endpoint.BuildRequestUri(relativeRoute);
            using (var request = UnityWebRequest.Get(requestUri.AbsoluteUri))
            using (cancellationToken.Register(request.Abort))
            {
                request.timeout = endpoint.TimeoutSeconds;
                request.SetRequestHeader("Accept", "application/json");
                if (requiresAuthentication)
                {
                    request.SetRequestHeader(
                        "Authorization",
                        "Bearer " + token!.Trim());
                }

                var operation = request.SendWebRequest();
                while (!operation.isDone)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await Task.Yield();
                }

                cancellationToken.ThrowIfCancellationRequested();
                if (allowNotFound && request.responseCode == 404)
                {
                    return null;
                }

                if (request.result != UnityWebRequest.Result.Success)
                {
                    throw new OperationalWorldProjectionException(
                        FailureCode(request.responseCode),
                        relativeRoute,
                        request.responseCode);
                }

                var json = request.downloadHandler.text;
                if (string.IsNullOrWhiteSpace(json))
                {
                    throw new OperationalWorldProjectionException(
                        "OperationalApiResponseEmpty",
                        relativeRoute,
                        request.responseCode);
                }

                return json;
            }
        }

        private static string FailureCode(long statusCode)
        {
            if (statusCode == 401)
            {
                return "OperationalApiUnauthorized";
            }

            if (statusCode == 403)
            {
                return "OperationalApiForbidden";
            }

            if (statusCode == 404)
            {
                return "OperationalApiNotFound";
            }

            return statusCode <= 0
                ? "OperationalApiTransportFailed"
                : "OperationalApiRequestFailed";
        }
    }
}
#endif
