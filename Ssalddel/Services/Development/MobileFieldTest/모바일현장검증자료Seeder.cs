using Microsoft.EntityFrameworkCore;
using 살뜰.Data;
using 살뜰.도메인.음식;

namespace Ssalddel.Services.Development.MobileFieldTest;

/// <summary>
/// 전용 Simulation DB에만 사가정 현장 검증용 합성 음식점과 메뉴를 멱등하게 준비합니다.
/// 실제 업체·가격·영업 여부를 나타내지 않으며 운영 DB에는 사용할 수 없습니다.
/// </summary>
internal static class 모바일현장검증자료Seeder
{
    internal const string FixtureRevision = "sagajeong-mobile-field-test.v1";
    private const string Disclosure = "합성 샘플 · 실제 업체/메뉴/가격 아님 · 내부 현장 검증 전용";

    internal static async Task SeedAsync(
        SsalddelContext db,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        var definitions = Definitions();
        var profileIds = definitions.Select(item => item.Id).ToList();
        var existingProfiles = await db.음식점공개프로필
            .Where(item => profileIds.Contains(item.Id))
            .Include(item => item.메뉴목록)
            .ToDictionaryAsync(item => item.Id, cancellationToken);

        foreach (var definition in definitions)
        {
            if (existingProfiles.TryGetValue(definition.Id, out var existing))
            {
                if (!Matches(existing, definition))
                {
                    throw new InvalidOperationException(
                        $"MobileFieldTestFixtureConflict:{definition.Id}");
                }

                continue;
            }

            db.음식점공개프로필.Add(new 음식점공개프로필
            {
                Id = definition.Id,
                상호명 = definition.Name,
                카테고리 = definition.Category,
                소개 = $"{Disclosure}; revision={FixtureRevision}",
                공개주소 = "서울특별시 중랑구 사가정역 내부 검증권역",
                위도 = definition.Latitude,
                경도 = definition.Longitude,
                최소주문금액 = 0,
                예상조리분 = definition.CookingMinutes,
                공개여부 = true,
                주문가능여부 = true,
                CreatedAtUtc = utcNow,
                UpdatedAtUtc = utcNow,
                메뉴목록 = definition.Menus.Select((menu, index) => new 음식점메뉴
                {
                    Id = definition.Id * 100 + index + 1,
                    메뉴명 = menu.Name,
                    설명 = Disclosure,
                    판매가 = menu.Price,
                    공개여부 = true,
                    품절여부 = false,
                    표시순서 = index + 1,
                    CreatedAtUtc = utcNow,
                    UpdatedAtUtc = utcNow
                }).ToList()
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static bool Matches(음식점공개프로필 existing, RestaurantDefinition expected)
        => string.Equals(existing.상호명, expected.Name, StringComparison.Ordinal)
           && string.Equals(existing.카테고리, expected.Category, StringComparison.Ordinal)
           && existing.소개.Contains(FixtureRevision, StringComparison.Ordinal)
           && existing.메뉴목록.Count == expected.Menus.Count;

    private static RestaurantDefinition[] Definitions()
        =>
        [
            new(101, "사가정 한상 샘플", "한식", 37.580450m, 127.087950m, 12,
                [new("제육 한상", 10_000m), new("두부 김치덮밥", 9_000m), new("계절 나물비빔밥", 9_500m)]),
            new(102, "면목 분식 샘플", "분식", 37.581050m, 127.086750m, 8,
                [new("떡볶이 한 그릇", 5_500m), new("김밥 한 줄", 4_000m), new("어묵 모음", 4_500m)]),
            new(103, "용마 국수 샘플", "면요리", 37.579850m, 127.089100m, 10,
                [new("잔치국수", 7_000m), new("비빔국수", 8_000m), new("만두 한 접시", 6_000m)])
        ];

    private sealed record RestaurantDefinition(
        long Id,
        string Name,
        string Category,
        decimal Latitude,
        decimal Longitude,
        int CookingMinutes,
        IReadOnlyList<MenuDefinition> Menus);

    private sealed record MenuDefinition(string Name, decimal Price);
}
