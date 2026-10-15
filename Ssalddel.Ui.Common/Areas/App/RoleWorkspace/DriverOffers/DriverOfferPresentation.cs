using System.Globalization;
using Ssalddel.Contracts.Common.Drivers;
using Ssalddel.Contracts.Common.Transport;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Contracts.Driver.Settings;

namespace Ssalddel.Ui.Common.Areas.App.RoleWorkspace.DriverOffers;

public sealed record DriverOfferVisual(string Label, string ActivityText, string PrimaryColor, string? SecondaryColor = null)
{
    public bool IsCombined => SecondaryColor is not null;
}

public sealed record DriverOfferPricingVisual(bool HasEvidence, string DistanceText, string DistanceFeeText,
    string RateText, string TotalText, string ComponentsText, string PolicyText);

/// <summary>서버가 이 제안에 연결한 업무·실제 작업·지급 근거만 표시합니다. 선호나 선행 bool로 작업을 만들지 않습니다.</summary>
[SsalddelCodeMetadata(SsalddelCodeFeatureKeys.OperationalDispatchCore, SsalddelCodeLayer.ViewModel,
    "서버의 배차 업무·배정 작업·지급 근거를 색·명칭·거리비 요약으로 변환",
    StepKey = "ui.driver-offer-presentation", FlowOrder = 206,
    ExecutionStage = SsalddelCodeExecutionStage.Presentation,
    ReadsFrom = SsalddelCodeDataScope.ClientPresentation, Effects = SsalddelCodeEffect.None,
    Boundary = "순수 표시이며 배차·작업·거리·금액을 확정하거나 기존 지도 경로색을 바꾸지 않는다.")]
public static class DriverOfferPresentation
{
    public const string FoodColor = "#2563EB";
    public const string CargoColor = "#DC2626";
    public const string WarehouseColor = "#64748B";
    public const string WorkIconPath = "M3 7 L12 2 L21 7 L21 17 L12 22 L3 17 Z M3 7 L12 12 L21 7 M12 12 L12 22";
    private static readonly CultureInfo Korean = CultureInfo.GetCultureInfo("ko-KR");
    private static readonly (string Code, string Label)[] Activities =
    [
        (DriverOfferActivityCodes.Pickup, "픽업"), (DriverOfferActivityCodes.Pick, "피킹"),
        (DriverOfferActivityCodes.Pack, "포장"), (DriverOfferActivityCodes.OutboundHandover, "출고 인계"),
        (DriverOfferActivityCodes.Deliver, "배송")
    ];

    public static DriverOfferVisual Resolve(string? workKind, IReadOnlyList<string>? assignedActivities)
    {
        var assigned = (assignedActivities ?? []).ToHashSet(StringComparer.Ordinal);
        var known = Activities.Where(x => assigned.Contains(x.Code)).ToArray();
        var unknown = assigned.Except(Activities.Select(x => x.Code), StringComparer.Ordinal).Any();
        var activityText = known.Length == 0 ? "작업 범위 확인 필요" : string.Join(" → ", known.Select(x => x.Label));
        if (unknown) activityText += " · 추가 작업 확인 필요";
        var warehouse = assigned.Contains(DriverOfferActivityCodes.Pick) || assigned.Contains(DriverOfferActivityCodes.Pack)
            || assigned.Contains(DriverOfferActivityCodes.OutboundHandover);
        var delivery = assigned.Contains(DriverOfferActivityCodes.Deliver);
        if (warehouse && !delivery && !assigned.Contains(DriverOfferActivityCodes.Pickup) && !unknown)
            return new("창고 작업", activityText, WarehouseColor);
        var (label, color) = workKind switch
        {
            DriverWorkKinds.FoodDelivery or 운송실행상세유형코드.음식점음식배달 => ("음식 배달", FoodColor),
            DriverWorkKinds.QuickDelivery or 운송실행상세유형코드.마트라스트마일배송 => ("소형 물품·퀵", CargoColor),
            DriverWorkKinds.CargoTransport or 운송실행상세유형코드.일반화물운송 or 운송실행상세유형코드.창고출고화물운송
                or 운송실행상세유형코드.같이주문국내배송 or 운송실행상세유형코드.같이수입국내인계 => ("화물 운송", CargoColor),
            _ => ("업무 유형 확인 필요", WarehouseColor)
        };
        return color != WarehouseColor && warehouse && delivery && !unknown
            ? new(label + " · 복합 작업", activityText, FoodColor, CargoColor)
            : new(label, activityText, color);
    }

    public static DriverOfferVisual Resolve(DriverWorkOfferDto offer) => Resolve(offer.WorkType, offer.AssignedActivities);

    public static DriverOfferPricingVisual Pricing(DriverOfferPricingDto? price)
    {
        if (price is null || string.IsNullOrWhiteSpace(price.PolicyRevision) || price.DistanceBasisCode is null or "" or "Unknown"
            || price.RawDistanceMeters < 0 || price.RatePerKmKrw < 0 || price.IncludedDistanceMeters < 0
            || price.DistanceFeeKrw < 0 || price.TotalPayoutKrw < 0 || price.BaseFeeKrw is < 0
            || price.MinimumPayoutKrw is < 0 || price.WorkFeeKrw is < 0 || price.SurchargeKrw is < 0)
            return new(false, "산정 거리 미정", "거리비 미정", "거리 단가 미정", "제안 보수 미정", "지급 구성 미정", "산정 근거 미정");
        var distanceLabel = price.DistanceBasisCode is "StraightLine" or "StraightLineHaversine" ? "산정 직선거리" : "산정 거리";
        var components = new List<string>
        {
            "기본비 " + (price.BaseFeeKrw is { } basic ? Money(basic) : "미정"),
            "작업비 " + (price.WorkFeeKrw is { } work ? Money(work) : "미정"),
            "할증 " + (price.SurchargeKrw is { } surcharge ? Money(surcharge) : "미정")
        };
        if (price.MinimumPayoutKrw is { } minimum) components.Add("최소 보수 " + Money(minimum));
        return new(true, $"{distanceLabel} {(price.RawDistanceMeters / 1000m).ToString("0.######", Korean)} km",
            "거리비 " + Money(price.DistanceFeeKrw), "거리 단가 " + Money(price.RatePerKmKrw) + "/km"
                + (price.IncludedDistanceMeters > 0 ? " · 포함 거리 " + (price.IncludedDistanceMeters / 1000m).ToString("0.######", Korean) + " km" : ""),
            "제안 보수 " + Money(price.TotalPayoutKrw), string.Join(" · ", components),
            $"{price.PolicyRevision} · {price.DistanceBasisCode}");
    }

    private static string Money(decimal value) => value.ToString("N0", Korean) + "원";
}
