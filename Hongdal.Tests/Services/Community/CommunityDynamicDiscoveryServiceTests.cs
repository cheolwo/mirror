using Hongdal.Contracts.Common.Community;
using Hongdal.Contracts.Restaurants;
using Hongdal.Domain.Community;
using Hongdal.Services.Community;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using 홍달.Data;
using 홍달.Infrastructure.Security;
using 홍달.Services.Options;
using 홍달.도메인.공통;
using 홍달.도메인.사용자;
using 홍달.도메인.운송;
using 홍달.도메인.화주;

namespace Hongdal.Tests.Services.Community;

public sealed class CommunityDynamicDiscoveryServiceTests
{
    [Fact]
    public void 동적주제카탈로그는_네업무영역마다_두세부주제를제공한다()
    {
        using var db = CreateContext();
        var service = CreateService(db, new FakeRestaurantDirectory([]));

        var catalog = service.GetCatalog();

        Assert.Equal(4, catalog.Domains.Count);
        Assert.All(catalog.Domains, domain => Assert.Equal(2, domain.Topics.Count));
        Assert.Collection(
            catalog.Domains,
            domain => Assert.Equal(["입고", "출고"], domain.Topics.Select(topic => topic.DisplayName)),
            domain => Assert.Equal(["개별주문", "공동주문"], domain.Topics.Select(topic => topic.DisplayName)),
            domain => Assert.Equal(["음식", "화물"], domain.Topics.Select(topic => topic.DisplayName)),
            domain => Assert.Equal(["상차", "하차"], domain.Topics.Select(topic => topic.DisplayName)));
    }

    [Fact]
    public void 한글업무신호는_창고주문판매운송의_세부주제로분류된다()
    {
        var classifier = new CommunityDynamicTopicClassifier();

        var matches = classifier.Classify(
            "공동주문 음식 화물 입고와 출고",
            "개별 주문을 합친 뒤 상차하고 하차하여 인수합니다.");

        Assert.Equal(8, matches.Select(match => match.TopicKey).Distinct().Count());
        Assert.Contains(matches, match => match.TopicKey == CommunityDynamicTopicCodes.WarehouseInbound);
        Assert.Contains(matches, match => match.TopicKey == CommunityDynamicTopicCodes.WarehouseOutbound);
        Assert.Contains(matches, match => match.TopicKey == CommunityDynamicTopicCodes.IndividualOrder);
        Assert.Contains(matches, match => match.TopicKey == CommunityDynamicTopicCodes.GroupOrder);
        Assert.Contains(matches, match => match.TopicKey == CommunityDynamicTopicCodes.TransportLoading);
        Assert.Contains(matches, match => match.TopicKey == CommunityDynamicTopicCodes.TransportUnloading);
    }

    [Fact]
    public void 한게시글에서_음식과화물_동적주제를_함께찾을수있다()
    {
        var classifier = new CommunityDynamicTopicClassifier();

        var matches = classifier.Classify(
            "지역 식당 식재료를 같이 운송해 볼까요",
            "음식점까지 냉장 화물로 옮길 방법을 이야기합니다.");

        Assert.Contains(matches, match => match.TopicKey == CommunityDynamicTopicCodes.Food);
        Assert.Contains(matches, match => match.TopicKey == CommunityDynamicTopicCodes.Cargo);
    }

    [Fact]
    public async Task 음식점은_명시적위치동의후_최대7킬로미터안에서만_보여준다()
    {
        await using var db = CreateContext();
        var directory = new FakeRestaurantDirectory(
        [
            Restaurant(1, "가까운 식당", 2.4m),
            Restaurant(2, "먼 식당", 7.1m)
        ]);
        var service = CreateService(db, directory);
        var source = FoodSource();

        var withoutConsent = await service.DiscoverAsync(source, new CommunityPostContextDiscoveryRequest
        {
            CurrentLatitude = 37.55m,
            CurrentLongitude = 126.85m,
            RadiusKm = 10m
        });
        var withConsent = await service.DiscoverAsync(source, new CommunityPostContextDiscoveryRequest
        {
            CurrentLatitude = 37.55m,
            CurrentLongitude = 126.85m,
            RadiusKm = 10m,
            ConfirmTransientLocationUse = true
        });

        Assert.Empty(withoutConsent.NearbyRestaurants);
        var restaurant = Assert.Single(withConsent.NearbyRestaurants);
        Assert.Equal("가까운 식당", restaurant.Name);
        Assert.Equal(7m, withConsent.LocationPolicy.AppliedRadiusKm);
        Assert.True(withConsent.LocationPolicy.ConsentConfirmed);
        Assert.False(withConsent.LocationPolicy.LocationPersisted);
        Assert.Equal(7m, directory.LastRadiusKm);
    }

    [Fact]
    public async Task 화물글은_플랫폼역할후보와_공개배차화물만_비식별로_보여준다()
    {
        await using var db = CreateContext();
        var role = new IdentityRole("화물운송주선업자") { Id = "role-broker" };
        db.Roles.Add(role);
        db.UserRoles.Add(new IdentityUserRole<string> { UserId = "broker-1", RoleId = role.Id });
        db.홍달참여자.Add(new 홍달참여자
        {
            Id = "broker-1",
            표시이름 = "함께운송",
            활성화여부 = true
        });
        db.화주운송의뢰.Add(new 화주운송의뢰
        {
            의뢰Id = "request-public-1",
            화주Id = "shipper-1",
            주문자UserId = "shipper-1",
            화물종류 = "냉장 채소",
            화물중량Kg = 850m,
            차량종류 = "1톤 냉장",
            픽업_시간창_시작일시 = DateTime.UtcNow.AddHours(2)
        });
        db.운송원장.Add(new 운송원장
        {
            운송번호 = "transport-1",
            의뢰Id = "request-public-1",
            화주Id = "shipper-1",
            배차업무유형 = 상태값.배차업무유형.용달운송,
            배차큐단계 = 상태값.배차큐단계.공개배차,
            배차노출상태 = 상태값.배차노출상태.공개중,
            픽업_도로명주소 = "서울 강서구 화곡로 123",
            하차_도로명주소 = "경기 고양시 덕양구 중앙로 30",
            공개전환시각 = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        var service = CreateService(db, new FakeRestaurantDirectory([]));

        var result = await service.DiscoverAsync(CargoSource(), null);

        var provider = Assert.Single(result.FreightProviderCandidates);
        Assert.Equal("함께운송", provider.DisplayName);
        Assert.True(provider.PlatformRoleVerified);
        Assert.True(provider.ExternalLicenseVerificationRequired);
        var freight = Assert.Single(result.PublicFreightCandidates);
        Assert.Equal("서울 강서구", freight.PickupAreaSummary);
        Assert.DoesNotContain("화곡로", freight.PickupAreaSummary);
        Assert.True(freight.IsExplicitPublicDispatch);
        Assert.False(result.IsBrokerageEnabled);
        Assert.False(result.AutomaticallySelectsProvider);
        Assert.False(result.AutomaticallyDispatchesFreight);
    }

    [Fact]
    public async Task 동적피드는_원래게시판을바꾸지않고_본문신호로글을모은다()
    {
        await using var db = CreateContext();
        db.PlatformCommunityPosts.AddRange(
            Post(1, "자유·생활", "동네 음식점 국수 이야기", "새 메뉴가 맛있어요"),
            Post(2, "자유·생활", "주말 산책", "공원에서 만나요"));
        await db.SaveChangesAsync();
        var service = CreateService(db, new FakeRestaurantDirectory([]));

        var feed = await service.GetFeedAsync(CommunityDynamicTopicCodes.Food, 1, 20);

        Assert.NotNull(feed);
        var item = Assert.Single(feed.Items);
        Assert.Equal(1, item.PostId);
        Assert.Equal("자유·생활", item.Category);
        Assert.Equal(1, feed.TotalCount);
    }

    [Fact]
    public async Task 기존_food경로는_판매음식주제로정규화한다()
    {
        await using var db = CreateContext();
        db.PlatformCommunityPosts.Add(Post(1, "자유·생활", "동네 식당", "음식을 나눕니다"));
        await db.SaveChangesAsync();
        var service = CreateService(db, new FakeRestaurantDirectory([]));

        var feed = await service.GetFeedAsync(CommunityDynamicTopicCodes.LegacyFood, 1, 20);

        Assert.NotNull(feed);
        Assert.Equal(CommunityDynamicTopicDomainCodes.Sales, feed.DomainKey);
        Assert.Equal(CommunityDynamicTopicCodes.Food, feed.TopicKey);
        Assert.Single(feed.Items);
    }

    private static CommunityDynamicDiscoveryService CreateService(
        HongdalContext db,
        ICommunityNearbyRestaurantDirectory directory)
        => new(
            db,
            new CommunityDynamicTopicClassifier(),
            directory,
            Options.Create(new CommunityContextDiscoveryOptions()));

    private static HongdalContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<HongdalContext>()
            .UseInMemoryDatabase($"community-dynamic-discovery-{Guid.NewGuid():N}")
            .Options;
        return new HongdalContext(options, new DummyPersonalDataEncryptionService());
    }

    private static CommunityPostOpportunitySource FoodSource()
        => new(10, "platform", "오늘 먹은 음식 이야기", "동네 식당도 궁금합니다.", "writer", null);

    private static CommunityPostOpportunitySource CargoSource()
        => new(11, "platform", "화물 운송을 함께 알아봅니다", "주선 역할과 공개 화물을 확인합니다.", "writer", null);

    private static 음식점요약응답 Restaurant(long id, string name, decimal distanceKm)
        => new()
        {
            Id = id,
            상호명 = name,
            카테고리 = "한식",
            주소 = "서울 강서구",
            거리Km = distanceKm,
            평균평점 = 4.5m,
            리뷰수 = 10,
            주문가능여부 = true
        };

    private static PlatformCommunityPost Post(long id, string category, string title, string body)
        => new()
        {
            Id = id,
            AppKey = "platform",
            Category = category,
            WorkflowTag = "생활",
            RoleTag = "플랫폼 구성원",
            Title = title,
            Body = body,
            Nickname = "이웃",
            PasswordHash = "hash",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

    private sealed class FakeRestaurantDirectory(IReadOnlyList<음식점요약응답> items)
        : ICommunityNearbyRestaurantDirectory
    {
        public decimal? LastRadiusKm { get; private set; }

        public Task<CommunityNearbyRestaurantLookupResult> FindAsync(
            decimal latitude,
            decimal longitude,
            decimal radiusKm,
            int limit,
            CancellationToken cancellationToken = default)
        {
            LastRadiusKm = radiusKm;
            return Task.FromResult(new CommunityNearbyRestaurantLookupResult(true, true, items));
        }
    }

    private sealed class DummyPersonalDataEncryptionService : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
}
