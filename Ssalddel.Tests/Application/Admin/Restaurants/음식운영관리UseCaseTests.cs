using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Ssalddel.Application.Admin.Restaurants;
using Ssalddel.Contracts.Admin.Restaurants;
using Ssalddel.Controllers.Admin;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Options;
using 살뜰.도메인.음식;

namespace Ssalddel.Tests.Application.Admin.Restaurants;

public sealed class 음식운영관리UseCaseTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 17, 5, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task 리뷰운영목록은_메인Db리뷰와음식점이름을조합한다()
    {
        await using var context = CreateContext();
        context.음식점공개프로필.Add(new 음식점공개프로필
        {
            Id = 81,
            상호명 = "메인 원장 식당",
            카테고리 = "한식",
            공개주소 = "서울시",
            공개여부 = true,
            주문가능여부 = true
        });
        context.음식점리뷰.Add(new 음식점리뷰
        {
            Id = 91,
            음식점Id = 81,
            주문자UserId = "review-owner",
            주문번호 = "FOOD-ADMIN-1",
            별점 = 2,
            내용 = "운영 검토가 필요합니다.",
            사진UrlsJson = "[\"https://example.test/photo.jpg\"]",
            관리자검토필요여부 = true,
            현재노출여부 = true
        });
        await context.SaveChangesAsync();
        var useCase = CreateUseCase(context);

        var result = await useCase.리뷰목록Async(CancellationToken.None);

        Assert.True(result.IsSuccess);
        var review = Assert.Single(result.Value.Items);
        Assert.Equal("메인 원장 식당", review.음식점명);
        Assert.Equal("review-owner", review.주문자UserId);
        Assert.True(review.사진포함여부);
        Assert.True(review.관리자검토필요여부);
    }

    [Fact]
    public async Task 배달요금정책은_메인Db에저장되고_재조회와감사정보가일치한다()
    {
        await using var context = CreateContext();
        var useCase = CreateUseCase(context);
        var request = new 음식배달요금정책응답
        {
            BaseFee = 3500m,
            IncludedDistanceMeters = 1200,
            DistanceUnitMeters = 100,
            DistanceUnitFee = 150m,
            MinimumFee = 3300m,
            DriverBasePayout = 2800m,
            DriverDistanceUnitPayout = 100m,
            DriverMinimumPayout = 2700m
        };

        var updated = await useCase.배달요금정책수정Async(
            request,
            "admin-user",
            CancellationToken.None);
        context.ChangeTracker.Clear();
        var reloaded = await CreateUseCase(context)
            .배달요금정책조회Async(CancellationToken.None);

        Assert.True(updated.IsSuccess);
        Assert.True(reloaded.IsSuccess);
        Assert.Equal(3500m, reloaded.Value.BaseFee);
        Assert.Equal(150m, reloaded.Value.DistanceUnitFee);
        Assert.Equal("admin-user", reloaded.Value.UpdatedByUserId);
        Assert.NotEqual(default, reloaded.Value.UpdatedAtUtc);
        Assert.Equal(1, await context.음식운영정책.CountAsync());
    }

    [Fact]
    public async Task 잘못된요금정책은_저장하지않는다()
    {
        await using var context = CreateContext();
        var useCase = CreateUseCase(context);

        var result = await useCase.배달요금정책수정Async(
            new 음식배달요금정책응답
            {
                BaseFee = -1,
                DistanceUnitMeters = 0
            },
            "admin-user",
            CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal(400, result.Errors.Single().Metadata["StatusCode"]);
        Assert.Empty(context.음식운영정책);
    }

    [Fact]
    public async Task 한시수요할증은_승인카드와시간만_같은요청Id로한번저장한다()
    {
        await using var context = CreateContext();
        var useCase = CreateUseCase(context);
        var request = new 음식배달한시수요할증적용요청
        {
            SurchargeAmount = 1000m,
            DurationMinutes = 30,
            ExpectedRevision = 0,
            ClientRequestId = Guid.NewGuid()
        };

        var first = await useCase.한시수요할증적용Async(request, "admin-user", CancellationToken.None);
        var replay = await useCase.한시수요할증적용Async(request, "admin-user", CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.True(replay.IsSuccess);
        Assert.True(first.Value.IsActive);
        Assert.Equal(1000m, first.Value.SurchargeAmount);
        Assert.Equal(30, first.Value.DurationMinutes);
        Assert.Equal(1, first.Value.Revision);
        Assert.Equal(first.Value.Revision, replay.Value.Revision);
        Assert.Equal(Now.UtcDateTime.AddMinutes(30), first.Value.ExpiresAtUtc);
        Assert.Equal(1, await context.음식운영정책.CountAsync());
    }

    [Theory]
    [InlineData(700, 30)]
    [InlineData(1000, 20)]
    public async Task 승인되지않은_할증액이나시간은_저장하지않는다(
        decimal amount,
        int durationMinutes)
    {
        await using var context = CreateContext();
        var result = await CreateUseCase(context).한시수요할증적용Async(
            new 음식배달한시수요할증적용요청
            {
                SurchargeAmount = amount,
                DurationMinutes = durationMinutes,
                ExpectedRevision = 0,
                ClientRequestId = Guid.NewGuid()
            },
            "admin-user",
            CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal(400, result.Errors.Single().Metadata["StatusCode"]);
        Assert.Empty(context.음식운영정책);
    }

    [Fact]
    public async Task 운영모드에서는_한시수요할증을_변경하지않는다()
    {
        await using var context = CreateContext();
        var result = await CreateUseCase(context, SsalddelExecutionMode.Operational)
            .한시수요할증적용Async(
                new 음식배달한시수요할증적용요청
                {
                    SurchargeAmount = 500m,
                    DurationMinutes = 15,
                    ExpectedRevision = 0,
                    ClientRequestId = Guid.NewGuid()
                },
                "admin-user",
                CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal(403, result.Errors.Single().Metadata["StatusCode"]);
        Assert.Empty(context.음식운영정책);
    }

    [Fact]
    public async Task 운영모드조회는_남아있는Simulation할증을_활성상태로노출하지않는다()
    {
        await using var context = CreateContext();
        context.음식운영정책.Add(new 음식운영정책
        {
            Id = 1,
            기사한시수요할증액 = 1500m,
            기사한시수요할증시작일시Utc = Now.UtcDateTime.AddMinutes(-5),
            기사한시수요할증종료일시Utc = Now.UtcDateTime.AddMinutes(25),
            기사한시수요할증Revision = 3,
            UpdatedAtUtc = Now.UtcDateTime
        });
        await context.SaveChangesAsync();

        var result = await CreateUseCase(context, SsalddelExecutionMode.Operational)
            .한시수요할증조회Async(CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.IsActive);
        Assert.False(result.Value.CanApply);
        Assert.Equal(0m, result.Value.SurchargeAmount);
        Assert.Equal(3, result.Value.Revision);
    }

    [Fact]
    public async Task 오래된_revision으로_한시수요할증을_덮어쓰지않는다()
    {
        await using var context = CreateContext();
        var useCase = CreateUseCase(context);
        var first = await useCase.한시수요할증적용Async(
            new 음식배달한시수요할증적용요청
            {
                SurchargeAmount = 500m,
                DurationMinutes = 15,
                ExpectedRevision = 0,
                ClientRequestId = Guid.NewGuid()
            },
            "admin-user",
            CancellationToken.None);

        var stale = await useCase.한시수요할증적용Async(
            new 음식배달한시수요할증적용요청
            {
                SurchargeAmount = 1500m,
                DurationMinutes = 60,
                ExpectedRevision = 0,
                ClientRequestId = Guid.NewGuid()
            },
            "admin-user",
            CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.True(stale.IsFailed);
        Assert.Equal(409, stale.Errors.Single().Metadata["StatusCode"]);

        var stored = await context.음식운영정책.SingleAsync();
        Assert.Equal(500m, stored.기사한시수요할증액);
        Assert.Equal(1, stored.기사한시수요할증Revision);
    }

    [Theory]
    [InlineData(nameof(음식배달요금정책Controller.한시수요할증조회), typeof(HttpGetAttribute))]
    [InlineData(nameof(음식배달요금정책Controller.한시수요할증적용), typeof(HttpPutAttribute))]
    public void 한시수요할증Api는_기존요금정책아래_명시적경로를사용한다(
        string methodName,
        Type attributeType)
    {
        var method = typeof(음식배달요금정책Controller).GetMethod(methodName);
        var attribute = Assert.Single(method!.GetCustomAttributes(attributeType, false));
        Assert.Equal(
            "temporary-demand-surcharge",
            ((HttpMethodAttribute)attribute).Template);
    }

    [Theory]
    [InlineData(typeof(음식점리뷰관리Controller), "api/v1/admin/restaurant-reviews")]
    [InlineData(typeof(음식배달요금정책Controller), "api/v1/admin/food-delivery-pricing-policy")]
    public void 음식운영관리Api는_서버관리자정책과기존경로를유지한다(
        Type controllerType,
        string expectedRoute)
    {
        Assert.Equal(
            "서버관리자전용",
            controllerType.GetCustomAttribute<AuthorizeAttribute>()?.Policy);
        Assert.Equal(
            expectedRoute,
            controllerType.GetCustomAttribute<RouteAttribute>()?.Template);
    }

    private static SsalddelContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<SsalddelContext>()
            .UseInMemoryDatabase($"food-operations-{Guid.NewGuid():N}")
            .Options;
        return new SsalddelContext(options, new PassThroughEncryptionService());
    }

    private static 음식운영관리UseCase CreateUseCase(
        SsalddelContext context,
        SsalddelExecutionMode mode = SsalddelExecutionMode.Simulation)
        => new(
            context,
            new FixedTimeProvider(Now),
            new SsalddelExecutionModePolicy(Options.Create(new SsalddelExecutionOptions { Mode = mode })));

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class PassThroughEncryptionService : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
}
