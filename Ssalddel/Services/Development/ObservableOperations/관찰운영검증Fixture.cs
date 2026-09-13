using System.Globalization;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using 살뜰.Data;
using 살뜰.도메인.음식;

namespace Ssalddel.Services.Development.ObservableOperations;

/// <summary>
/// 로컬 공공자료 directory에서 상호·주소를 복제하지 않고 위치 안정 ID와 업종만 골라
/// 비공개 샘플 음식점/메뉴 및 그 결속 증거를 만듭니다.
/// </summary>
internal static class 관찰운영검증FixtureFactory
{
    internal const string FixtureRevision = "sagajeong-restaurant-fixture.v2";
    internal const string DeterministicSeed = "sagajeong-sample-menu-seed-20260913-r1";
    internal const string SourceKindCode = "SyntheticFixture";
    internal const string EnvironmentCode = "DevelopmentSimulation";
    internal const string BindingPurposeCode = "LocationPresentationAnchorOnly";
    internal const string AffiliationCode = "NoBusinessAffiliation";
    internal const string DisclosureCode = "SampleMenuNotActualOffering";
    internal const string ReviewStatusCode = "PendingHumanReview";
    internal const string GeneratorVersion = "observable-operations-fixture.v2";
    private const string SampleProfileDescription =
        "샘플 메뉴 · 실제 판매 메뉴 아님 · 해당 업체와 제휴 관계 없음 · 로컬 검토용/비배포";
    private const string SampleMenuDescription =
        "SyntheticValue=true; 실제 판매 메뉴·가격이 아닌 로컬 생명주기 검증값";

    private const long ReservedIdBase = 8_000_000_000_000_000_000L;
    private const ulong ReservedIdRange = 1_000_000_000_000_000_000UL;
    private const double MaximumDistanceFromStation = 250d;

    internal static async Task<관찰운영검증FixtureSeed?> ReadAsync(
        string path,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;

        await using var stream = File.OpenRead(path);
        var inputHash = Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken)).ToLowerInvariant();
        stream.Position = 0;
        var directory = await JsonSerializer.DeserializeAsync<RestaurantDirectory>(stream,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, cancellationToken)
            ?? throw new InvalidOperationException("ObservableOperationsRestaurantFixtureInputInvalid");
        Require(string.Equals(directory.SchemaVersion, "sagajeong-restaurant-directory.v1", StringComparison.Ordinal),
            "SchemaVersion");
        Require(!string.IsNullOrWhiteSpace(directory.DataRevision), "DataRevision");
        Require(string.Equals(directory.ProjectionKind, "DerivedProjectionFromStoredObservation", StringComparison.Ordinal),
            "ProjectionKind");
        Require(string.Equals(directory.Scope.Kind, "StationCenteredUnitySquare", StringComparison.Ordinal), "ScopeKind");
        Require(string.Equals(directory.Scope.WorldRegionStableId,
            "world-region:kr:seoul:jungnang:sagajeong.r1", StringComparison.Ordinal), "WorldRegion");
        Require(string.Equals(directory.Scope.LegalAreaStableId,
            "region:kr:bjd:1126010100", StringComparison.Ordinal), "LegalArea");
        Require(string.Equals(directory.Scope.CoordinateSpace,
            "SagajeongReference.UnityXZ", StringComparison.Ordinal), "CoordinateSpace");
        Require(directory.Scope.CenterX == 550d && directory.Scope.CenterZ == 8d
                && directory.Scope.MinX == 50d && directory.Scope.MaxX == 1050d
                && directory.Scope.MinZ == -492d && directory.Scope.MaxZ == 508d,
            "ScopeBounds");
        Require(string.Equals(directory.Readiness.DisplayReviewStatus, ReviewStatusCode, StringComparison.Ordinal),
            "DisplayReviewStatus");
        Require(string.Equals(directory.Readiness.OrderParticipationStatus, "Disabled", StringComparison.Ordinal),
            "OrderParticipationStatus");
        Require(!directory.Readiness.PublicDisplayEnabled
                && !directory.Readiness.DistributionApproved
                && !directory.Readiness.OrderScenarioEligible,
            "ReadinessGates");

        var candidates = (directory.Restaurants ?? [])
            .Where(item => IsEligible(item, directory.DataRevision))
            .Select(item => new Candidate(item, Distance(item)))
            .OrderBy(item => item.Distance)
            .ThenBy(item => item.Source.DirectoryStableId, StringComparer.Ordinal)
            .ToArray();
        var selected = candidates
            .GroupBy(item => item.Source.SourceIndustry.Trim(), StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(item => item.Distance)
            .ThenBy(item => item.Source.SourceIndustry, StringComparer.Ordinal)
            .Take(12)
            .ToList();
        if (selected.Count < 12)
        {
            var selectedIds = selected.Select(item => item.Source.DirectoryStableId).ToHashSet(StringComparer.Ordinal);
            selected.AddRange(candidates.Where(item => !selectedIds.Contains(item.Source.DirectoryStableId))
                .Take(12 - selected.Count));
        }
        if (selected.Count != 12)
            throw new InvalidOperationException("ObservableOperationsRestaurantFixtureCandidatesInsufficient");

        var restaurants = selected.Select((item, index) => CreateRestaurant(item.Source, inputHash, index + 1)).ToArray();
        var fixtureHash = FixtureHash(inputHash, directory.DataRevision, restaurants);
        var pack = new 관찰운영검증Fixture묶음Record
        {
            FixturePackStableId = "fixture-pack:sagajeong-restaurants:" + fixtureHash[..16],
            FixtureRevision = FixtureRevision,
            FixtureHashSha256 = fixtureHash,
            InputHashSha256 = inputHash,
            SourceRevision = directory.DataRevision,
            DeterministicSeed = DeterministicSeed,
            SourceKindCode = SourceKindCode,
            EnvironmentCode = EnvironmentCode,
            DistributionApproved = false,
            OperationalEffectsAllowed = false,
            GeneratorVersion = GeneratorVersion,
            CreatedAtUtc = utcNow,
            ExpiresAtUtc = utcNow.AddDays(30)
        };
        return new 관찰운영검증FixtureSeed(pack, restaurants);
    }

    internal static async Task PersistAsync(
        관찰운영검증FixtureSeed seed,
        SsalddelContext applicationDb,
        관찰운영검증DbContext verificationDb,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var recomputedFixtureHash = FixtureHash(
            seed.Pack.InputHashSha256,
            seed.Pack.SourceRevision,
            seed.Restaurants);
        if (!string.Equals(seed.Pack.FixtureHashSha256, recomputedFixtureHash, StringComparison.Ordinal)
            || !PackSafetyFieldsMatch(seed.Pack, seed.Pack, utcNow))
            throw new InvalidOperationException("ObservableOperationsRestaurantFixturePackInvalid");

        var profileIds = seed.Restaurants.Select(item => item.ProfileId).ToList();
        var existingProfiles = await applicationDb.음식점공개프로필
            .Where(item => profileIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, cancellationToken);
        foreach (var definition in seed.Restaurants)
        {
            if (existingProfiles.TryGetValue(definition.ProfileId, out var profile)
                && !Matches(profile, definition))
                throw new InvalidOperationException("ObservableOperationsRestaurantFixtureProfileIdConflict");
        }

        var menuIds = seed.Restaurants.SelectMany(item => item.Menus).Select(item => item.MenuId).ToList();
        var existingMenus = await applicationDb.음식점메뉴
            .Where(item => menuIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, cancellationToken);
        foreach (var restaurant in seed.Restaurants)
        {
            foreach (var definition in restaurant.Menus)
            {
                if (existingMenus.TryGetValue(definition.MenuId, out var menu)
                    && !Matches(menu, restaurant.ProfileId, definition))
                    throw new InvalidOperationException("ObservableOperationsRestaurantFixtureMenuIdConflict");
            }
        }

        var existingPack = await verificationDb.FixturePacks
            .SingleOrDefaultAsync(item => item.FixturePackStableId == seed.Pack.FixturePackStableId, cancellationToken);
        if (existingPack is not null && !PackSafetyFieldsMatch(existingPack, seed.Pack, utcNow))
            throw new InvalidOperationException("ObservableOperationsRestaurantFixturePackConflict");

        var bindings = CreateBindings(seed).ToArray();
        var existingBindings = await verificationDb.FixtureBindings.Where(item =>
                item.FixturePackStableId == seed.Pack.FixturePackStableId)
            .ToDictionaryAsync(item => item.FixtureObjectStableId, StringComparer.Ordinal, cancellationToken);
        var expectedObjectIds = bindings.Select(item => item.FixtureObjectStableId)
            .ToHashSet(StringComparer.Ordinal);
        var bindingSetMatches = existingBindings.Count == expectedObjectIds.Count
            && existingBindings.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(expectedObjectIds);
        if ((existingPack is not null && !bindingSetMatches)
            || (existingPack is null && existingBindings.Count > 0))
            throw new InvalidOperationException("ObservableOperationsRestaurantFixtureBindingSetConflict");
        foreach (var binding in bindings)
        {
            if (existingBindings.TryGetValue(binding.FixtureObjectStableId, out var existing)
                && !BindingSafetyFieldsMatch(existing, binding))
                throw new InvalidOperationException("ObservableOperationsRestaurantFixtureBindingConflict");
        }

        // 두 DbContext를 아우르는 원자성을 가장하지 않습니다. 대신 기존 행의 충돌 검사를 모두 끝낸 뒤
        // 새 행만 쓰므로 알려진 profile/menu/pack/binding 충돌은 앱 DB 부분 저장을 만들지 않습니다.
        foreach (var definition in seed.Restaurants.Where(item => !existingProfiles.ContainsKey(item.ProfileId)))
        {
            applicationDb.음식점공개프로필.Add(new 음식점공개프로필
            {
                Id = definition.ProfileId,
                업체Id = null,
                상호명 = definition.SampleDisplayName,
                카테고리 = definition.CategoryCode,
                소개 = SampleProfileDescription,
                공개주소 = string.Empty,
                위도 = 0,
                경도 = 0,
                대표이미지Url = null,
                최소주문금액 = 0,
                예상조리분 = definition.ExpectedCookingMinutes,
                공개여부 = false,
                주문가능여부 = false,
                CreatedAtUtc = utcNow,
                UpdatedAtUtc = utcNow
            });
        }
        foreach (var restaurant in seed.Restaurants)
        {
            foreach (var definition in restaurant.Menus.Where(item => !existingMenus.ContainsKey(item.MenuId)))
            {
                applicationDb.음식점메뉴.Add(new 음식점메뉴
                {
                    Id = definition.MenuId,
                    음식점공개프로필Id = restaurant.ProfileId,
                    메뉴명 = definition.Name,
                    설명 = SampleMenuDescription,
                    판매가 = definition.Price,
                    대표이미지Url = null,
                    공개여부 = false,
                    품절여부 = false,
                    표시순서 = definition.DisplayOrder,
                    CreatedAtUtc = utcNow,
                    UpdatedAtUtc = utcNow
                });
            }
        }
        await applicationDb.SaveChangesAsync(cancellationToken);

        if (existingPack is null) verificationDb.FixturePacks.Add(seed.Pack);
        foreach (var binding in bindings.Where(item => !existingBindings.ContainsKey(item.FixtureObjectStableId)))
            verificationDb.FixtureBindings.Add(binding);
        await verificationDb.SaveChangesAsync(cancellationToken);
    }

    private static IEnumerable<관찰운영검증Fixture결속Record> CreateBindings(관찰운영검증FixtureSeed seed)
    {
        foreach (var restaurant in seed.Restaurants)
        {
            yield return Binding(seed.Pack.FixturePackStableId, restaurant,
                restaurant.ProfileStableId, "RestaurantProfile", null);
            foreach (var menu in restaurant.Menus)
                yield return Binding(seed.Pack.FixturePackStableId, restaurant,
                    menu.MenuStableId, "RestaurantMenu", menu.MenuId);
        }
    }

    private static 관찰운영검증Fixture결속Record Binding(
        string packStableId,
        관찰운영검증RestaurantFixture restaurant,
        string fixtureObjectStableId,
        string objectKindCode,
        long? menuId)
    {
        var binding = new 관찰운영검증Fixture결속Record
        {
            FixturePackStableId = packStableId,
            FixtureObjectStableId = fixtureObjectStableId,
            ObjectKindCode = objectKindCode,
            RestaurantProfileId = restaurant.ProfileId,
            MenuId = menuId,
            PublicBusinessObservationStableId = restaurant.PublicBusinessObservationStableId,
            LocationAnchorStableId = restaurant.LocationAnchorStableId,
            BuildingStableId = restaurant.BuildingStableId,
            SemanticPlaceStableId = restaurant.SemanticPlaceStableId,
            BindingPurposeCode = BindingPurposeCode,
            AffiliationCode = AffiliationCode,
            DisplayDisclosureCode = DisclosureCode,
            ScenarioOrderAllowed = true,
            ActualOrderAllowed = false,
            DistributionApproved = false,
            ReviewStatusCode = ReviewStatusCode,
            Revision = 1
        };
        binding.BindingHashSha256 = BindingHash(binding);
        return binding;
    }

    private static 관찰운영검증RestaurantFixture CreateRestaurant(
        RestaurantDirectoryItem source,
        string inputHash,
        int sequence)
    {
        var category = source.SourceIndustry.Trim();
        var template = MenuTemplate(category);
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(
            string.Join('|', DeterministicSeed, source.DirectoryStableId, category)));
        var count = 2 + digest[0] % 2;
        var start = digest[1] % template.Length;
        var profileStableId = $"sample-restaurant:sagajeong:v2:{inputHash[..12]}:{Sha256(source.DirectoryStableId)[..12]}";
        var profileId = StableNumericId("profile|" + GeneratorVersion + "|" + inputHash + "|" + source.DirectoryStableId);
        var menus = Enumerable.Range(0, count).Select(index =>
        {
            var name = template[(start + index) % template.Length];
            var price = 6_000m + ((digest[(index + 2) % digest.Length] % 9) * 1_000m);
            var menuStableId = $"{profileStableId}:menu:{index + 1:00}";
            return new 관찰운영검증MenuFixture(
                StableNumericId("menu|" + menuStableId),
                menuStableId,
                "[샘플] " + name,
                price,
                index + 1);
        }).ToArray();
        return new 관찰운영검증RestaurantFixture(
            profileId,
            profileStableId,
            $"[샘플] 사가정 음식점 {sequence:00}",
            category,
            10 + digest[5] % 16,
            source.ParentStableId,
            source.DirectoryStableId,
            source.BuildingCandidateStableId,
            $"synthetic-place:sagajeong-restaurant-anchor-{sequence:00}",
            menus);
    }

    private static string[] MenuTemplate(string category)
    {
        if (ContainsAny(category, "카페", "커피", "빵", "도넛", "빙수", "아이스크림"))
            return ["음료 A", "구움과자 B", "디저트 C"];
        if (ContainsAny(category, "김밥", "분식", "만두"))
            return ["분식 A", "김밥 B", "만두 C"];
        if (ContainsAny(category, "닭", "치킨", "오리", "고기", "구이"))
            return ["구이 A", "덮밥 B", "곁들임 C"];
        if (ContainsAny(category, "국", "탕", "찌개"))
            return ["국밥 A", "찌개 B", "만두국 C"];
        if (ContainsAny(category, "면", "국수"))
            return ["면요리 A", "비빔면 B", "만두 C"];
        if (ContainsAny(category, "피자"))
            return ["피자 A", "파스타 B", "샐러드 C"];
        if (ContainsAny(category, "중국", "마라", "훠궈"))
            return ["볶음밥 A", "면요리 B", "만두 C"];
        return ["덮밥 A", "국물요리 B", "곁들임 C"];
    }

    private static bool IsEligible(RestaurantDirectoryItem item, string dataRevision)
        => !string.IsNullOrWhiteSpace(item.DirectoryStableId)
           && !string.IsNullOrWhiteSpace(item.ParentStableId)
           && !string.IsNullOrWhiteSpace(item.SourceIndustry)
           && !string.IsNullOrWhiteSpace(item.BuildingCandidateStableId)
           && string.Equals(item.DisplayReviewStatus, ReviewStatusCode, StringComparison.Ordinal)
           && string.Equals(item.OrderParticipationStatus, "Disabled", StringComparison.Ordinal)
           && !item.DistributionApproved
           && !item.OrderScenarioEligible
           && string.Equals(item.DataRevision, dataRevision, StringComparison.Ordinal)
           && string.Equals(item.ProjectionKind, "DerivedProjectionFromStoredObservation", StringComparison.Ordinal)
           && !ContainsAny(item.SourceIndustry, "유흥", "주점", "호프", "바")
           && Distance(item) <= MaximumDistanceFromStation;

    private static double Distance(RestaurantDirectoryItem item)
        => Math.Sqrt(Math.Pow(item.UnityX - 550d, 2) + Math.Pow(item.UnityZ - 8d, 2));

    private static bool ContainsAny(string value, params string[] candidates)
        => candidates.Any(candidate => value.Contains(candidate, StringComparison.OrdinalIgnoreCase));

    private static string FixtureHash(
        string inputHash,
        string sourceRevision,
        IReadOnlyList<관찰운영검증RestaurantFixture> restaurants)
    {
        var fixtureRows = restaurants.SelectMany(item =>
        {
            var restaurant = string.Join('|',
                item.ProfileId.ToString(CultureInfo.InvariantCulture),
                item.ProfileStableId,
                item.SampleDisplayName,
                item.PublicBusinessObservationStableId,
                item.LocationAnchorStableId,
                item.BuildingStableId,
                item.SemanticPlaceStableId,
                item.CategoryCode,
                item.ExpectedCookingMinutes.ToString(CultureInfo.InvariantCulture));
            return new[] { restaurant }.Concat(item.Menus.Select(menu => string.Join('|',
                menu.MenuId.ToString(CultureInfo.InvariantCulture),
                menu.MenuStableId,
                menu.Name,
                menu.Price.ToString(CultureInfo.InvariantCulture),
                menu.DisplayOrder.ToString(CultureInfo.InvariantCulture))));
        });
        var canonical = string.Join('\n', fixtureRows
            .Prepend(GeneratorVersion)
            .Prepend("operationalEffectsAllowed=false")
            .Prepend("distributionApproved=false")
            .Prepend(EnvironmentCode)
            .Prepend(SourceKindCode)
            .Prepend(DeterministicSeed)
            .Prepend(sourceRevision)
            .Prepend(inputHash)
            .Prepend(FixtureRevision));
        return Sha256(canonical);
    }

    private static bool PackSafetyFieldsMatch(
        관찰운영검증Fixture묶음Record actual,
        관찰운영검증Fixture묶음Record expected,
        DateTime utcNow)
        => string.Equals(actual.FixturePackStableId, expected.FixturePackStableId, StringComparison.Ordinal)
           && string.Equals(actual.FixturePackStableId,
               "fixture-pack:sagajeong-restaurants:" + expected.FixtureHashSha256[..16], StringComparison.Ordinal)
           && string.Equals(actual.FixtureRevision, FixtureRevision, StringComparison.Ordinal)
           && string.Equals(actual.FixtureRevision, expected.FixtureRevision, StringComparison.Ordinal)
           && string.Equals(actual.FixtureHashSha256, expected.FixtureHashSha256, StringComparison.Ordinal)
           && string.Equals(actual.InputHashSha256, expected.InputHashSha256, StringComparison.Ordinal)
           && string.Equals(actual.SourceRevision, expected.SourceRevision, StringComparison.Ordinal)
           && string.Equals(actual.DeterministicSeed, DeterministicSeed, StringComparison.Ordinal)
           && string.Equals(actual.DeterministicSeed, expected.DeterministicSeed, StringComparison.Ordinal)
           && string.Equals(actual.SourceKindCode, SourceKindCode, StringComparison.Ordinal)
           && string.Equals(actual.SourceKindCode, expected.SourceKindCode, StringComparison.Ordinal)
           && string.Equals(actual.EnvironmentCode, EnvironmentCode, StringComparison.Ordinal)
           && string.Equals(actual.EnvironmentCode, expected.EnvironmentCode, StringComparison.Ordinal)
           && !actual.DistributionApproved
           && !expected.DistributionApproved
           && !actual.OperationalEffectsAllowed
           && !expected.OperationalEffectsAllowed
           && string.Equals(actual.GeneratorVersion, GeneratorVersion, StringComparison.Ordinal)
           && string.Equals(actual.GeneratorVersion, expected.GeneratorVersion, StringComparison.Ordinal)
           && actual.CreatedAtUtc <= utcNow
           && actual.ExpiresAtUtc > utcNow
           && actual.ExpiresAtUtc == actual.CreatedAtUtc.AddDays(30);

    private static bool BindingSafetyFieldsMatch(
        관찰운영검증Fixture결속Record actual,
        관찰운영검증Fixture결속Record expected)
        => string.Equals(actual.BindingHashSha256, BindingHash(actual), StringComparison.Ordinal)
           && string.Equals(expected.BindingHashSha256, BindingHash(expected), StringComparison.Ordinal)
           && string.Equals(actual.BindingHashSha256, expected.BindingHashSha256, StringComparison.Ordinal)
           && string.Equals(actual.FixturePackStableId, expected.FixturePackStableId, StringComparison.Ordinal)
           && string.Equals(actual.FixtureObjectStableId, expected.FixtureObjectStableId, StringComparison.Ordinal)
           && string.Equals(actual.ObjectKindCode, expected.ObjectKindCode, StringComparison.Ordinal)
           && actual.RestaurantProfileId == expected.RestaurantProfileId
           && actual.MenuId == expected.MenuId
           && string.Equals(actual.PublicBusinessObservationStableId,
               expected.PublicBusinessObservationStableId, StringComparison.Ordinal)
           && string.Equals(actual.LocationAnchorStableId, expected.LocationAnchorStableId, StringComparison.Ordinal)
           && string.Equals(actual.BuildingStableId, expected.BuildingStableId, StringComparison.Ordinal)
           && string.Equals(actual.SemanticPlaceStableId, expected.SemanticPlaceStableId, StringComparison.Ordinal)
           && string.Equals(actual.BindingPurposeCode, BindingPurposeCode, StringComparison.Ordinal)
           && string.Equals(actual.BindingPurposeCode, expected.BindingPurposeCode, StringComparison.Ordinal)
           && string.Equals(actual.AffiliationCode, AffiliationCode, StringComparison.Ordinal)
           && string.Equals(actual.AffiliationCode, expected.AffiliationCode, StringComparison.Ordinal)
           && string.Equals(actual.DisplayDisclosureCode, DisclosureCode, StringComparison.Ordinal)
           && string.Equals(actual.DisplayDisclosureCode, expected.DisplayDisclosureCode, StringComparison.Ordinal)
           && actual.ScenarioOrderAllowed
           && expected.ScenarioOrderAllowed
           && !actual.ActualOrderAllowed
           && !expected.ActualOrderAllowed
           && !actual.DistributionApproved
           && !expected.DistributionApproved
           && string.Equals(actual.ReviewStatusCode, ReviewStatusCode, StringComparison.Ordinal)
           && string.Equals(actual.ReviewStatusCode, expected.ReviewStatusCode, StringComparison.Ordinal)
           && actual.Revision == 1
           && actual.Revision == expected.Revision;

    private static string BindingHash(관찰운영검증Fixture결속Record binding)
        => Sha256(string.Join('|',
            binding.FixturePackStableId,
            binding.FixtureObjectStableId,
            binding.ObjectKindCode,
            binding.RestaurantProfileId.ToString(CultureInfo.InvariantCulture),
            binding.MenuId?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            binding.PublicBusinessObservationStableId,
            binding.LocationAnchorStableId,
            binding.BuildingStableId,
            binding.SemanticPlaceStableId,
            binding.BindingPurposeCode,
            binding.AffiliationCode,
            binding.DisplayDisclosureCode,
            binding.ScenarioOrderAllowed ? "true" : "false",
            binding.ActualOrderAllowed ? "true" : "false",
            binding.DistributionApproved ? "true" : "false",
            binding.ReviewStatusCode,
            binding.Revision.ToString(CultureInfo.InvariantCulture)));

    private static void Require(bool condition, string fieldCode)
    {
        if (!condition)
            throw new InvalidOperationException("ObservableOperationsRestaurantFixtureSchemaUnsupported:" + fieldCode);
    }

    private static string Sha256(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static long StableNumericId(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        var number = BinaryPrimitives.ReadUInt64BigEndian(hash.AsSpan(0, sizeof(ulong)));
        return ReservedIdBase + (long)(number % ReservedIdRange);
    }

    private static bool Matches(
        음식점공개프로필 profile,
        관찰운영검증RestaurantFixture definition)
        => profile.업체Id is null
           && string.Equals(profile.상호명, definition.SampleDisplayName, StringComparison.Ordinal)
           && string.Equals(profile.카테고리, definition.CategoryCode, StringComparison.Ordinal)
           && string.Equals(profile.소개, SampleProfileDescription, StringComparison.Ordinal)
           && string.IsNullOrEmpty(profile.공개주소)
           && profile.위도 == 0
           && profile.경도 == 0
           && profile.대표이미지Url is null
           && profile.최소주문금액 == 0
           && profile.예상조리분 == definition.ExpectedCookingMinutes
           && !profile.공개여부
           && !profile.주문가능여부;

    private static bool Matches(
        음식점메뉴 menu,
        long restaurantProfileId,
        관찰운영검증MenuFixture definition)
        => menu.음식점공개프로필Id == restaurantProfileId
           && string.Equals(menu.메뉴명, definition.Name, StringComparison.Ordinal)
           && string.Equals(menu.설명, SampleMenuDescription, StringComparison.Ordinal)
           && menu.판매가 == definition.Price
           && menu.대표이미지Url is null
           && !menu.공개여부
           && !menu.품절여부
           && menu.표시순서 == definition.DisplayOrder;

    private sealed record Candidate(RestaurantDirectoryItem Source, double Distance);

    private sealed class RestaurantDirectory
    {
        public string SchemaVersion { get; set; } = string.Empty;
        public string DataRevision { get; set; } = string.Empty;
        public string ProjectionKind { get; set; } = string.Empty;
        public RestaurantDirectoryScope Scope { get; set; } = new();
        public RestaurantDirectoryReadiness Readiness { get; set; } = new();
        public RestaurantDirectoryItem[] Restaurants { get; set; } = [];
    }

    private sealed class RestaurantDirectoryScope
    {
        public string Kind { get; set; } = string.Empty;
        public string WorldRegionStableId { get; set; } = string.Empty;
        public string LegalAreaStableId { get; set; } = string.Empty;
        public double CenterX { get; set; }
        public double CenterZ { get; set; }
        public double MinX { get; set; }
        public double MaxX { get; set; }
        public double MinZ { get; set; }
        public double MaxZ { get; set; }
        public string CoordinateSpace { get; set; } = string.Empty;
    }

    private sealed class RestaurantDirectoryReadiness
    {
        public string DisplayReviewStatus { get; set; } = string.Empty;
        public string OrderParticipationStatus { get; set; } = string.Empty;
        public bool PublicDisplayEnabled { get; set; }
        public bool DistributionApproved { get; set; }
        public bool OrderScenarioEligible { get; set; }
    }

    private sealed class RestaurantDirectoryItem
    {
        public string DirectoryStableId { get; set; } = string.Empty;
        public string ParentStableId { get; set; } = string.Empty;
        public string SourceIndustry { get; set; } = string.Empty;
        public double UnityX { get; set; }
        public double UnityZ { get; set; }
        public string BuildingCandidateStableId { get; set; } = string.Empty;
        public string DisplayReviewStatus { get; set; } = string.Empty;
        public string OrderParticipationStatus { get; set; } = string.Empty;
        public bool DistributionApproved { get; set; }
        public bool OrderScenarioEligible { get; set; }
        public string DataRevision { get; set; } = string.Empty;
        public string ProjectionKind { get; set; } = string.Empty;
    }
}

internal sealed record 관찰운영검증FixtureSeed(
    관찰운영검증Fixture묶음Record Pack,
    IReadOnlyList<관찰운영검증RestaurantFixture> Restaurants);

internal sealed record 관찰운영검증RestaurantFixture(
    long ProfileId,
    string ProfileStableId,
    string SampleDisplayName,
    string CategoryCode,
    int ExpectedCookingMinutes,
    string PublicBusinessObservationStableId,
    string LocationAnchorStableId,
    string BuildingStableId,
    string SemanticPlaceStableId,
    IReadOnlyList<관찰운영검증MenuFixture> Menus);

internal sealed record 관찰운영검증MenuFixture(
    long MenuId,
    string MenuStableId,
    string Name,
    decimal Price,
    int DisplayOrder);
