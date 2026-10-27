using Ssalddel.Application.Driver.Recommendation;
using Ssalddel.Contracts.Common.Drivers;
using Ssalddel.Hubs;
using 살뜰.도메인.운송;

namespace 살뜰.Services.Dispatch.Recommendation;

internal static class 운송콜제안Projector
{
    internal static DispatchRecommendationDto CreateCore(운송원장 item, string workKind,
        IReadOnlyList<string> assignedActivities, DriverOfferPricingDto? pricing, string cargoKind)
        => new()
        {
            WorkKind = workKind,
            AssignedActivities = assignedActivities,
            Pricing = pricing,
            의뢰Id = item.의뢰Id,
            화물종류 = cargoKind,
            픽업지 = item.픽업_도로명주소,
            하차지 = item.하차_도로명주소,
            픽업_위도 = item.픽업_위도,
            픽업_경도 = item.픽업_경도,
            하차_위도 = item.하차_위도,
            하차_경도 = item.하차_경도
        };

    internal static void ApplyClassificationAndPrivacy(DispatchRecommendationDto recommendation,
        운송원장 item, string? clientRequestId)
    {
        DispatchRecommendationRequestTypeClassifier.ApplyTo(
            recommendation,
            item.원본의뢰유형,
            item.공동구매도착지유형코드,
            item.공동구매기사세대배송여부,
            item.공동구매세대배송건수);
        if (생활배송기사정보공개Policy.생활배송인가(clientRequestId))
            생활배송기사정보공개Policy.추천정보가림(recommendation);
    }
}
