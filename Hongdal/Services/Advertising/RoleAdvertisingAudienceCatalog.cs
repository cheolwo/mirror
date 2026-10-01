using Hongdal.Contracts.Common.Advertising;

namespace Hongdal.Services.Advertising;

public interface IRoleAdvertisingAudienceCatalog
{
    IReadOnlyList<RoleAdvertisingRoleProfile> GetAll();
    RoleAdvertisingRoleProfile? Find(string roleCode);
}

public sealed class RoleAdvertisingAudienceCatalog : IRoleAdvertisingAudienceCatalog
{
    private static readonly IReadOnlyList<RoleAdvertisingRoleProfile> Profiles =
    [
        new(
            RoleAdvertisingAudienceRoleCodes.CommunityMember,
            "커뮤니티 참여자",
            RoleAdvertisingObjectiveCodes.CommunityJoin,
            "관심 게시판을 둘러보고 첫 공개 게시글 또는 참여 의사를 남기는 페이지",
            "가입 후 첫 공개 참여",
            true,
            [RoleAdvertisingProviderCodes.Meta, RoleAdvertisingProviderCodes.GoogleAds, RoleAdvertisingProviderCodes.NaverSearchAds],
            ["지역 커뮤니티", "공동구매", "생산자 직거래"],
            [],
            []),
        new(
            RoleAdvertisingAudienceRoleCodes.GroupPurchaseBuyer,
            "공동구매 수요자",
            RoleAdvertisingObjectiveCodes.QualifiedLead,
            "구매 희망 품목과 지역을 확인하고 구매 의향을 남기는 페이지",
            "유효 구매 의향 등록",
            true,
            [RoleAdvertisingProviderCodes.Meta, RoleAdvertisingProviderCodes.GoogleAds, RoleAdvertisingProviderCodes.NaverSearchAds],
            ["지역 공동구매", "먹거리 공동구매", "산지 직송"],
            [],
            []),
        new(
            RoleAdvertisingAudienceRoleCodes.GroupPurchaseRepresentative,
            "공동구매 대표",
            RoleAdvertisingObjectiveCodes.QualifiedLead,
            "모집 조건과 운영 범위를 확인하고 대표 참여 의사를 남기는 페이지",
            "검증 가능한 대표 참여 신청",
            true,
            [RoleAdvertisingProviderCodes.Meta, RoleAdvertisingProviderCodes.GoogleAds, RoleAdvertisingProviderCodes.NaverSearchAds],
            ["공동구매 모집", "아파트 공동구매", "지역 공동주문"],
            ["Community Services"],
            ["Community Management"]),
        new(
            RoleAdvertisingAudienceRoleCodes.ProducerSupplier,
            "생산자·공급자",
            RoleAdvertisingObjectiveCodes.SupplyProposal,
            "공급 가능 품목과 조건을 공개하고 공급 제안을 남기는 페이지",
            "검증 가능한 공급 제안",
            true,
            [RoleAdvertisingProviderCodes.GoogleAds, RoleAdvertisingProviderCodes.NaverSearchAds, RoleAdvertisingProviderCodes.LinkedIn, RoleAdvertisingProviderCodes.Meta],
            ["농산물 판로", "식품 공급업체", "공동구매 납품"],
            ["Food Production", "Farming", "Wholesale"],
            ["Sales", "Operations", "Business Development"]),
        new(
            RoleAdvertisingAudienceRoleCodes.Shipper,
            "화주",
            RoleAdvertisingObjectiveCodes.QualifiedLead,
            "운송이 필요한 사람이 공개 조건을 게시하는 미래 역할 페이지",
            "검증 가능한 운송 필요 게시",
            false,
            [RoleAdvertisingProviderCodes.GoogleAds, RoleAdvertisingProviderCodes.LinkedIn, RoleAdvertisingProviderCodes.NaverSearchAds],
            ["기업 화물 운송", "물류 운송 문의"],
            ["Logistics and Supply Chain"],
            ["Operations", "Supply Chain"]),
        new(
            RoleAdvertisingAudienceRoleCodes.WarehouseOperator,
            "창고 운영자",
            RoleAdvertisingObjectiveCodes.RoleApplication,
            "보관 가능 조건과 서비스 지역을 공개하는 미래 역할 페이지",
            "검증 가능한 창고 운영자 신청",
            false,
            [RoleAdvertisingProviderCodes.LinkedIn, RoleAdvertisingProviderCodes.GoogleAds, RoleAdvertisingProviderCodes.NaverSearchAds],
            ["창고 운영", "3PL 물류"],
            ["Warehousing", "Logistics and Supply Chain"],
            ["Operations", "Supply Chain"]),
        new(
            RoleAdvertisingAudienceRoleCodes.CargoDriver,
            "화물·용달 기사",
            RoleAdvertisingObjectiveCodes.RoleApplication,
            "운행 가능 지역과 시간을 공개하는 미래 역할 페이지",
            "검증 가능한 기사 역할 신청",
            false,
            [RoleAdvertisingProviderCodes.Meta, RoleAdvertisingProviderCodes.GoogleAds, RoleAdvertisingProviderCodes.NaverSearchAds],
            ["화물 기사", "용달 기사", "운송 기사"],
            [],
            []),
        new(
            RoleAdvertisingAudienceRoleCodes.FoodDeliveryDriver,
            "음식 배달 기사",
            RoleAdvertisingObjectiveCodes.RoleApplication,
            "배달 가능 지역과 시간을 공개하는 미래 역할 페이지",
            "검증 가능한 배달 기사 역할 신청",
            false,
            [RoleAdvertisingProviderCodes.Meta, RoleAdvertisingProviderCodes.GoogleAds, RoleAdvertisingProviderCodes.NaverSearchAds],
            ["음식 배달 기사", "배달 파트너"],
            [],
            [])
    ];

    public IReadOnlyList<RoleAdvertisingRoleProfile> GetAll() => Profiles;

    public RoleAdvertisingRoleProfile? Find(string roleCode)
        => Profiles.FirstOrDefault(x => string.Equals(x.RoleCode, roleCode?.Trim(), StringComparison.OrdinalIgnoreCase));
}
