using Ssalddel.Unity.WorldProjection;
using UnityEngine;

namespace Ssalddel.Unity.Samples.Farm
{
    public sealed class FarmSessionTokenProvider
        : MonoBehaviour, IOperationalRuntimeAccessTokenProvider
    {
        private string accessToken = string.Empty;

        public void SetAccessToken(string value)
        {
            accessToken = value?.Trim() ?? string.Empty;
        }

        public string GetAccessToken() => accessToken;
    }
}
