using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Application.Admin.Food;
using Ssalddel.Controllers.Admin.Food;
using Ssalddel.Contracts.Food;
using Ssalddel.Contracts.Common.Versioning;
using Ssalddel.Filters;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Versioning;
using 살뜰.도메인.음식;

namespace Ssalddel.Tests.Application.Admin.Food;

public sealed class AdminFoodOperationsListTests
{
    [Fact]
    public async Task 음식목록은_고객정보없이_최신변경순서와페이지를조회한다()
    {
        await using var db = Context();
        db.음식주문.AddRange(Order("ONE", "식당", 1), Order("TWO", "식당", 2), Order("THREE", "다른 식당", 3));
        await db.SaveChangesAsync();
        var result = await new 음식주문운영추적UseCase(db).조회목록Async(page: 2, pageSize: 2);
        Assert.Equal(3, result.TotalCount); Assert.Equal(2, result.Page);
        Assert.Equal("ONE", Assert.Single(result.Items).OrderNo);
        var json = JsonSerializer.Serialize(result);
        Assert.DoesNotContain("010-1234-5678", json);
        Assert.DoesNotContain("상세주소", json); Assert.DoesNotContain("수령인", json);
        Assert.True(result.ServerNowUtc > DateTime.MinValue);
    }

    [Fact]
    public async Task 검색은_주문번호나음식점에만적용하고_조건변경은_다시조회한다()
    {
        await using var db = Context();
        db.음식주문.AddRange(Order("FOOD-ONE", "카페", 1), Order("FOOD-TWO", "식당", 2));
        await db.SaveChangesAsync();
        var service = new 음식주문운영추적UseCase(db);
        Assert.Equal("FOOD-ONE", Assert.Single((await service.조회목록Async(" 카페 ")).Items).OrderNo);
        Assert.Equal("FOOD-TWO", Assert.Single((await service.조회목록Async("TWO")).Items).OrderNo);
        Assert.Empty((await service.조회목록Async("010-1234")).Items);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(-1, 20)]
    [InlineData(10001, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 51)]
    public async Task 과도하거나잘못된목록범위는_빈성공으로대체하지않는다(int page, int size)
    {
        await using var db = Context();
        await Assert.ThrowsAsync<ArgumentException>(() => new 음식주문운영추적UseCase(db).조회목록Async(page: page, pageSize: size));
    }

    [Fact]
    public async Task 검색어가너무길면_서버가거절한다()
    {
        await using var db = Context();
        await Assert.ThrowsAsync<ArgumentException>(() => new 음식주문운영추적UseCase(db).조회목록Async(new string('x', 101)));
    }

    [Fact]
    public void 음식목록API는_관리자권한과음식기능을유지한다()
    {
        var type = typeof(음식주문운영추적Controller);
        Assert.Equal("서버관리자전용", type.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>().Single().Policy);
        Assert.NotEmpty(type.GetCustomAttributes(typeof(RequireVersionFeatureAttribute), true));
        var action = type.GetMethod(nameof(음식주문운영추적Controller.조회목록))!;
        Assert.Equal("operations", action.GetCustomAttributes(typeof(HttpGetAttribute), true).Cast<HttpGetAttribute>().Single().Template);
        var descriptor = new Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor { ControllerTypeInfo = type.GetTypeInfo(), MethodInfo = action };
        Assert.Equal(VersionFeatureFlagKeys.FoodDeliveryWorkflow, SsalddelApiFeatureBoundaryFilter.ResolveFeatureKey(descriptor));
    }

    [Theory]
    [InlineData("/food-operations", PageInteractionBoundary.ReadOnly)]
    [InlineData("/food-operations/orders/FOOD-ONE", PageInteractionBoundary.ReadOnly)]
    [InlineData("/food-operations/reviews/FOOD-ONE", PageInteractionBoundary.PlatformPersistence)]
    [InlineData("/food-operations/demand-surcharge", PageInteractionBoundary.PlatformPersistence)]
    public void 음식운영페이지는_조회와저장책임을구별하고_음식기능에만연결한다(string path, PageInteractionBoundary boundary)
    {
        Assert.True(SsalddelPageCapabilityCatalog.TryResolve(SsalddelPageAppCodes.Admin, path, out var rule));
        Assert.Equal(boundary, rule.Boundary);
        Assert.True(rule.RequiresAuthentication);
        Assert.Equal(VersionFeatureFlagKeys.FoodDeliveryWorkflow, Assert.Single(rule.FeatureKeys));
        Assert.Equal("FoodDelivery", Assert.Single(rule.WorkflowCodes));
    }

    private static 음식주문 Order(string id, string name, int hour) => new()
    {
        주문번호 = id, 주문자UserId = "orderer", 음식점Id = 1, 음식점명 = name,
        수령인명 = "수령인", 수령인연락처 = "010-1234-5678", 수령지상세주소 = "상세주소",
        상태 = 음식주문상태코드.주문대기, CreatedAt = new DateTime(2026, 10, 4, 0, 0, 0, DateTimeKind.Utc),
        UpdatedAt = new DateTime(2026, 10, 4, hour, 0, 0, DateTimeKind.Utc)
    };
    private static SsalddelContext Context() => new(new DbContextOptionsBuilder<SsalddelContext>()
        .UseInMemoryDatabase("admin-food-" + Guid.NewGuid()).Options, new Encryption());
    private sealed class Encryption : IPersonalDataEncryptionService
    { public string? Protect(string? value) => value; public string? Unprotect(string? value) => value; }
}
