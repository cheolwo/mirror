using Ssalddel.Contracts.Driver.Recommendation;
using Ssalddel.Contracts.Driver.Transport;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.DriverOffers;

namespace Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Cargo;

/// <summary>고객 금액을 기사 보수로 대체하지 않고 서버의 명시된 지급 근거만 표시합니다.</summary>
public static class CargoDriverPayoutPresentation
{
    public static decimal? OfferAmount(기사배차추천항목응답? source)
    {
        if (source is null) return null;
        if (source.Pricing is not null)
        {
            var price = DriverOfferPresentation.Pricing(source.Pricing);
            return price.HasEvidence && Valid(source.Pricing.TotalPayoutKrw)
                && (!source.기사지급예정운임.HasValue || source.기사지급예정운임 == source.Pricing.TotalPayoutKrw)
                ? source.Pricing.TotalPayoutKrw : null;
        }
        return Valid(source.기사지급예정운임) && source.기사지급예정운임근거 == "운임구성.기사지급예정운임"
            ? source.기사지급예정운임 : null;
    }

    public static decimal? FrozenAmount(기사운송요약응답 source)
        => Valid(source.기사지급예정액) && source.기사지급예정액근거 == "운송원장.기사지급예정액"
            ? source.기사지급예정액 : null;

    public static string Money(decimal? amount)
        => amount.HasValue ? CargoWorkspacePresentation.Money(amount) : "미확인";

    private static bool Valid(decimal? amount)
        => amount is > 0m && decimal.Truncate(amount.Value) == amount.Value;
}
