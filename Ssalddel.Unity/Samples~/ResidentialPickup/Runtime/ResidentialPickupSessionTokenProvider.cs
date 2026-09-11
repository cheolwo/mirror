using System;
using Ssalddel.Unity.WorldProjection;
using UnityEngine;

namespace Ssalddel.Unity.Samples.ResidentialPickup
{
    public sealed class ResidentialPickupSessionTokenProvider
        : MonoBehaviour, IOperationalRuntimeAccessTokenProvider
    {
        [NonSerialized]
        private string accessToken = string.Empty;

        public void SetAccessToken(string token)
        {
            accessToken = token?.Trim() ?? string.Empty;
        }

        public string GetAccessToken()
        {
            return accessToken;
        }
    }
}
