using System.Net.Http.Json;
using Hongdal.Contracts.Food;
using Hongdal.Ui.Common.Areas.App.Services;

namespace HongdalApp.Services;

public sealed class 배차주소ApiService(HongdalProtectedApiClient protectedApiClient)
{
    public async Task<배차주소저장결과> 저장Async(배차주소저장요청 request, CancellationToken cancellationToken = default)
    {
        var response = await protectedApiClient.PostAsProtectedJsonAsync("api/v1/food-orders/dispatch/address-form", request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"배차주소 저장 실패: {(int)response.StatusCode} {error}");
        }

        var payload = await response.Content.ReadFromJsonAsync<배차주소저장응답>(cancellationToken: cancellationToken);
        return new 배차주소저장결과
        {
            메시지 = payload?.메시지 ?? "저장 완료",
            상차지위도 = payload?.상차지위도,
            상차지경도 = payload?.상차지경도,
            하차지위도 = payload?.하차지위도,
            하차지경도 = payload?.하차지경도
        };
    }

    public sealed class 배차주소저장결과
    {
        public string 메시지 { get; set; } = string.Empty;
        public double? 상차지위도 { get; set; }
        public double? 상차지경도 { get; set; }
        public double? 하차지위도 { get; set; }
        public double? 하차지경도 { get; set; }
    }

}
