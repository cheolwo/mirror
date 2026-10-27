using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Common.Drivers;
using Ssalddel.Contracts.Common.Metadata;
using 살뜰.Data;
using 살뜰.도메인.운송;
using 살뜰.도메인.화주;
using 살뜰.도메인.창고;

namespace 살뜰.Services.Dispatch.Recommendation;

[SsalddelCodeMetadata(SsalddelCodeFeatureKeys.FoodDriverWork, SsalddelCodeLayer.Application,
    "저장한 기사 요금 근거와 실제 분산 피킹 책임을 제안 표시로 읽는다.",
    StepKey = "application.driver-offer-evidence", FlowOrder = 53,
    Effects = SsalddelCodeEffect.PersistentRead, ExecutionStage = SsalddelCodeExecutionStage.Projection,
    ReadsFrom = SsalddelCodeDataScope.OperationalState, WritesTo = SsalddelCodeDataScope.None,
    Boundary = "조회만 수행한다. 고객 견적·희망 옵션·창고 선행 bool로 보수나 포장 배정을 추론하지 않는다.")]
public static class 기사제안표시근거Service
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public static IReadOnlyList<string> 운송작업(bool 직접피킹)
        => 직접피킹 ? [DriverOfferActivityCodes.Pick, DriverOfferActivityCodes.Deliver]
            : [DriverOfferActivityCodes.Pickup, DriverOfferActivityCodes.Deliver];

    /// <summary>원의뢰에 결속된 약정 또는 기존 소형 제안만 읽으며 고객 청구액은 사용하지 않습니다.</summary>
    public static (decimal? Amount, string? Source) 화물보수(
        운송원장 queue, 화주운송의뢰? request, 운임구성? fare, DriverOfferPricingDto? pricing)
    {
        if (request is null || request.의뢰Id != queue.의뢰Id) return (null, null);
        if (pricing is not null)
            return 유효한보수(pricing.TotalPayoutKrw) && queue.기사지급예정액 == pricing.TotalPayoutKrw
                ? (pricing.TotalPayoutKrw, "운송원장.기사제안요금계산근거Json") : (null, null);
        // 별도 동결 제안이 있는데 읽을 수 없으면 다른 약정으로 대신 표시하지 않습니다.
        if (!string.IsNullOrWhiteSpace(queue.기사제안요금계산근거Json)
            || !string.IsNullOrWhiteSpace(queue.기사제안요금정책판본)) return (null, null);
        return fare is not null && fare.Id == request.운임구성Id && fare.의뢰Id == request.의뢰Id
            && 유효한보수(fare.기사지급예정운임)
            && (!queue.기사지급예정액.HasValue || queue.기사지급예정액 == fare.기사지급예정운임)
                ? (fare.기사지급예정운임, "운임구성.기사지급예정운임") : (null, null);
    }

    private static bool 유효한보수(decimal? value)
        => value is > 0m && decimal.Truncate(value.Value) == value.Value;

    public static async Task<IReadOnlySet<string>> 직접피킹의뢰Async(SsalddelContext db, IReadOnlyList<string> requestIds,
        CancellationToken ct = default)
    {
        if (requestIds.Count == 0) return new HashSet<string>(StringComparer.Ordinal);
        return (await db.Set<분산배송주문Record>().AsNoTracking()
            .Where(x => Enumerable.Contains(requestIds, x.CargoRequestId) && x.StatusCode != "Cancelled")
            .Select(x => x.CargoRequestId).Distinct().ToArrayAsync(ct)).ToHashSet(StringComparer.Ordinal);
    }

    public static DriverOfferPricingDto? 요금근거(운송원장 queue)
    {
        if (string.IsNullOrWhiteSpace(queue.기사제안요금계산근거Json)) return null;
        try
        {
            using var doc = JsonDocument.Parse(queue.기사제안요금계산근거Json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return null;
            var property = doc.RootElement.EnumerateObject().FirstOrDefault(x => string.Equals(x.Name, "Pricing", StringComparison.OrdinalIgnoreCase));
            if (property.Value.ValueKind != JsonValueKind.Object) return null;
            // decimal 기본값 0을 관측된 0원으로 오인하지 않도록 원문 필수 필드도 확인합니다.
            foreach (var name in new[] { "rawDistanceMeters", "ratePerKmKrw", "includedDistanceMeters", "distanceFeeKrw", "totalPayoutKrw" })
                if (!property.Value.TryGetProperty(name, out var number) || number.ValueKind != JsonValueKind.Number
                    || !number.TryGetDecimal(out _)) return null;
            var value = property.Value.Deserialize<DriverOfferPricingDto>(Json);
            return value is { DistanceBasisCode: "StraightLine", RawDistanceMeters: >= 0 and <= 20100000m,
                RatePerKmKrw: 1000m, IncludedDistanceMeters: 0m, RoundingCode: "ProportionalAwayFromZero",
                DistanceFeeKrw: >= 0, TotalPayoutKrw: >= 0 }
                && value.DistanceFeeKrw == decimal.Round(value.RawDistanceMeters, 0, MidpointRounding.AwayFromZero)
                && !string.IsNullOrWhiteSpace(value.PolicyRevision) ? value : null;
        }
        catch (JsonException) { return null; }
    }
}
