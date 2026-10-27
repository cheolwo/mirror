using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Ssalddel.Application.Shipper.Request;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Controllers.Common;
using Ssalddel.Services.Community;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Options;
using 살뜰.도메인.화주;

namespace Ssalddel.Tests.Services.Community;

public sealed class CargoInformationNoticeBoundaryTests
{
    [Fact]
    public async Task 실제Controller오류응답은_UI가읽는정확한errorCode를반환한다()
    {
        var disabled = Controller(enabled: false);
        AssertProblem(await disabled.공개목록(), 404, "CargoNotice.FeatureDisabled");
        var enabled = Controller(enabled: true);
        AssertProblem(await enabled.공개상세("missing", CancellationToken.None), 404, "CargoNotice.NotFound");
        AssertProblem(await enabled.공개목록(0), 400, "CargoNotice.InvalidInput");
    }

    [Fact]
    public async Task 과거운송의뢰는_공고가아니므로_익명요약에노출하지않고_원본은보존한다()
    {
        await using var db = new SsalddelContext(new DbContextOptionsBuilder<SsalddelContext>()
            .UseInMemoryDatabase("cargo-notice-closed-public-" + Guid.NewGuid().ToString("N")).Options, new Encryption());
        db.화주운송의뢰.Add(new 화주운송의뢰 { 의뢰Id = "existing-transport", 화주Id = "owner", 화물종류 = "상자",
            픽업_도로명주소 = "비공개 출발지", 하차_도로명주소 = "비공개 도착지" });
        await db.SaveChangesAsync();
        var handler = new 공개화물요약조회QueryHandler(db);
        Assert.Empty(await handler.Handle(new 공개화물요약조회Query(1, 50), CancellationToken.None));
        Assert.Single(await db.화주운송의뢰.ToListAsync());
        Assert.Equal(EntityState.Unchanged, db.ChangeTracker.Entries<화주운송의뢰>().Single().State);
    }

    [Fact]
    public void 공개GET셋외의_내조회와모든변경은_인증을유지하며_응답을캐시하지않는다()
    {
        var type = typeof(화물정보공고Controller);
        Assert.NotNull(type.GetCustomAttribute<AuthorizeAttribute>());
        Assert.Null(type.GetCustomAttribute<AllowAnonymousAttribute>());
        Assert.True(type.GetCustomAttribute<ResponseCacheAttribute>()!.NoStore);
        var anonymous = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(method => method.GetCustomAttribute<AllowAnonymousAttribute>() is not null).ToArray();
        Assert.Equal(new[] { "공개목록", "공개상세", "지역목록" }.OrderBy(value => value, StringComparer.Ordinal),
            anonymous.Select(method => method.Name).OrderBy(value => value, StringComparer.Ordinal));
        Assert.All(anonymous, method => Assert.NotNull(method.GetCustomAttribute<HttpGetAttribute>()));
    }

    [Fact]
    public void 공개계약의필드와의존성은_실제운송금융알림을포함하지않는다()
    {
        Assert.Equal(new[] { "Content", "DropoffRegionName", "ExpiresAtUtc", "NoticeId", "PickupRegionName", "PublishedAtUtc", "Revision" },
            typeof(CargoNoticePublicDto).GetProperties().Select(value => value.Name).OrderBy(value => value, StringComparer.Ordinal));
        var dependencies = Assert.Single(typeof(화물정보공고UseCase).GetConstructors()).GetParameters().Select(value => value.ParameterType).ToArray();
        Assert.Equal(5, dependencies.Length);
        Assert.Contains(typeof(I화물정보공고Store), dependencies);
        Assert.DoesNotContain(dependencies, value => value.Name.Contains("Transport", StringComparison.Ordinal)
            || value.Name.Contains("Publisher", StringComparison.Ordinal) || value.Name.Contains("Payment", StringComparison.Ordinal)
            || value.Name.Contains("원장", StringComparison.Ordinal) || value.Name.Contains("배차", StringComparison.Ordinal));
    }

    private sealed class Encryption : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }

    private static 화물정보공고Controller Controller(bool enabled)
        => new(new 화물정보공고UseCase(null!, null!, null!, Options.Create(new 화물정보공고Options { Enabled = enabled })))
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

    private static void AssertProblem(IActionResult result, int status, string code)
    {
        var response = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(status, response.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(response.Value);
        Assert.Equal(status, problem.Status);
        Assert.Equal(code, problem.Extensions["errorCode"]);
    }
}
