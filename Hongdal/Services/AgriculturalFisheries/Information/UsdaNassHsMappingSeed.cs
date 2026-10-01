using System.Security.Cryptography;
using System.Text;
using Hongdal.Domain.AgriculturalFisheries;

namespace Hongdal.Services.AgriculturalFisheries.Information;

public static class UsdaNassHsMappingSeed
{
    private const string SourceUrl =
        "https://www.census.gov/foreign-trade/schedules/b/2026/exp-code.txt";

    public static IReadOnlyList<HsUsdaCommodityMapping> Create()
        =>
        [
            Map("100510", "옥수수 종자", "Corn (maize), seed", "CORN", "SeedScopeReview"),
            Map("100590", "옥수수", "Corn (maize), other than seed", "CORN"),
            Map("100111", "듀럼밀 종자", "Durum wheat, seed", "WHEAT", "SeedScopeReview"),
            Map("100119", "듀럼밀", "Durum wheat, other than seed", "WHEAT"),
            Map("100191", "밀·메슬린 종자", "Wheat and meslin seed, except durum", "WHEAT", "SeedScopeReview"),
            Map("100199", "밀·메슬린", "Wheat and meslin, other than seed and durum", "WHEAT"),
            Map("120110", "대두 종자", "Soybeans, seed", "SOYBEANS", "SeedScopeReview"),
            Map("120190", "대두", "Soybeans, other", "SOYBEANS"),
            Map("100610", "벼·현미 전 단계", "Rice in the husk (paddy or rough)", "RICE"),
            Map("100620", "현미", "Husked (brown) rice", "RICE", "ProcessingScopeReview"),
            Map("100630", "정미", "Semi-milled or wholly milled rice", "RICE", "ProcessingScopeReview"),
            Map("100640", "쇄미", "Broken rice", "RICE", "ProcessingScopeReview"),
            Map("070110", "씨감자", "Potatoes, seed, fresh or chilled", "POTATOES", "SeedScopeReview"),
            Map("070190", "감자", "Potatoes, other, fresh or chilled", "POTATOES"),
            Map("070200", "토마토", "Tomatoes, fresh or chilled", "TOMATOES"),
            Map("070310", "양파·샬롯", "Onions and shallots, fresh or chilled", "ONIONS"),
            Map("080810", "사과", "Apples, fresh", "APPLES"),
            Map("080510", "오렌지", "Oranges, fresh or dried", "ORANGES"),
            Map("080610", "포도", "Grapes, fresh", "GRAPES"),
            Map("081010", "딸기", "Strawberries, fresh", "STRAWBERRIES"),
            Map("520100", "원면", "Cotton, not carded or combed", "COTTON", "ProcessingScopeReview"),
            Map("120230", "땅콩 종자", "Peanut seed", "PEANUTS", "SeedScopeReview"),
            Map("120241", "껍질 있는 땅콩", "Peanuts in shell, other than seed", "PEANUTS"),
            Map("120242", "탈각 땅콩", "Peanuts shelled, whether or not broken", "PEANUTS", "ProcessingScopeReview")
        ];

    private static HsUsdaCommodityMapping Map(
        string hsCode6,
        string productNameKo,
        string hsDescriptionEn,
        string commodity,
        string matchQualityCode = "Candidate")
    {
        var mappingKey = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes($"{hsCode6}|{commodity}")))
            .ToLowerInvariant();

        return new HsUsdaCommodityMapping
        {
            MappingKey = mappingKey,
            HsCode6 = hsCode6,
            ProductNameKo = productNameKo,
            HsDescriptionEn = hsDescriptionEn,
            UsdaCommodityDesc = commodity,
            MatchQualityCode = matchQualityCode,
            ReviewStatusCode = HsUsdaMappingReviewStatusCodes.NeedsReview,
            ReviewNote =
                "HS 무역 품목과 USDA 생산자 수취가격의 범위가 일치하는지 class·utilization·가공 상태를 관세사 또는 운영자가 검토해야 합니다.",
            SourceUrl = SourceUrl,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
    }
}
