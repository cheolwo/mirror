using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Ssalddel.Unity.OperationalTransport;
using Ssalddel.Unity.UrbanMarket;
using Ssalddel.Unity.WorldProjection;
using UnityEngine;

namespace Ssalddel.Unity.Samples.UrbanMarket
{
    public sealed class UrbanMarketApiOptions
    {
        public string BaseUrl { get; set; } = string.Empty;
        public int TimeoutSeconds { get; set; } = 15;
    }

    public sealed class OperationalUrbanMarketApiClient : I도심마트ApiClient
    {
        private readonly IOperationalWorldProjectionTransport transport;

        public OperationalUrbanMarketApiClient(UrbanMarketApiOptions apiOptions)
        {
            if (apiOptions == null) throw new ArgumentNullException(nameof(apiOptions));
            transport = new UnityWebRequestOperationalWorldProjectionTransport(
                new OperationalWorldProjectionEndpoint(
                    apiOptions.BaseUrl,
                    Math.Max(1, apiOptions.TimeoutSeconds)));
        }

        public async Task<도심마트목록ApiModel> GetAsync(
            CancellationToken cancellationToken = default)
        {
            var json = await transport.GetAsync(
                도심마트ApiRoutes.PublicProducts,
                false,
                false,
                cancellationToken);
            var wire = JsonUtility.FromJson<UrbanMarketListWireModel>(json);
            return wire?.ToApiModel()
                ?? throw new InvalidOperationException("UrbanMarketApiJsonInvalid");
        }
    }

    [Serializable]
    internal sealed class UrbanMarketListWireModel
    {
        public UrbanMarketProductWireModel[] items = Array.Empty<UrbanMarketProductWireModel>();
        public int totalCount;
        public string 재고기준안내 = string.Empty;

        public 도심마트목록ApiModel ToApiModel()
        {
            var values = new 도심마트상품ApiModel[items?.Length ?? 0];
            for (var index = 0; index < values.Length; index++)
            {
                values[index] = items[index].ToApiModel();
            }

            return new 도심마트목록ApiModel
            {
                Items = values,
                TotalCount = totalCount,
                재고기준안내 = 재고기준안내,
            };
        }
    }

    [Serializable]
    internal sealed class UrbanMarketProductWireModel
    {
        public long id;
        public string 상품명 = string.Empty;
        public string 판매단위 = string.Empty;
        public double 판매가;
        public int 판매가능수량;
        public bool 판매가능여부;
        public string 재고기준시각Utc = string.Empty;
        public string 수정일시Utc = string.Empty;

        public 도심마트상품ApiModel ToApiModel()
        {
            return new 도심마트상품ApiModel
            {
                Id = id,
                상품명 = 상품명,
                판매단위 = 판매단위,
                판매가 = Convert.ToDecimal(판매가, CultureInfo.InvariantCulture),
                판매가능수량 = 판매가능수량,
                판매가능여부 = 판매가능여부,
                재고기준시각 = ParseTimestamp(재고기준시각Utc),
                수정시각 = ParseTimestamp(수정일시Utc),
            };
        }

        private static DateTimeOffset ParseTimestamp(string value)
        {
            return DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal,
                out var parsed)
                ? parsed
                : throw new InvalidOperationException("UrbanMarketApiTimestampInvalid");
        }
    }
}
