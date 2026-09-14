using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Ssalddel.Contracts.Common.DeliveryZones;
using Ssalddel.Controllers.Admin.DeliveryZones;
using 살뜰.Services.DeliveryZones;

namespace Ssalddel.Tests.Controllers.Admin.DeliveryZones;

public sealed class 배달운영권역ControllerTests
{
    [Fact]
    public void 관리자_정책과_고정_route를_요구한다()
    {
        var type = typeof(배달운영권역Controller);

        var authorize = Assert.Single(type.GetCustomAttributes<AuthorizeAttribute>());
        Assert.Equal("서버관리자전용", authorize.Policy);
        Assert.Equal(
            배달운영권역Routes.AdminBase,
            Assert.Single(type.GetCustomAttributes<RouteAttribute>()).Template);
    }

    [Fact]
    public async Task Draft_생성은_상세_위치를_가리키는_201을_반환한다()
    {
        var expected = new 배달운영권역Dto
        {
            DeliveryTerritoryStableId = "delivery-territory:kr:seoul:test",
            DisplayName = "시험 권역",
            Revision = 1
        };
        var service = new StubService(expected);
        var controller = CreateController(service);

        var result = await controller.Draft생성(
            new 배달운영권역Draft생성Request(),
            CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal(201, created.StatusCode);
        Assert.Equal(nameof(배달운영권역Controller.조회), created.ActionName);
        Assert.Same(expected, created.Value);
        Assert.Equal("user:admin:controller-test", service.LastActorUserStableId);
    }

    [Fact]
    public async Task 경쟁_revision은_409_공식자료_결손은_503으로_구분한다()
    {
        var conflictController = CreateController(
            new ThrowingService(new 배달운영권역ConcurrencyException("DeliveryTerritoryRevisionConflict")));
        var conflict = await conflictController.행정동교체(
            "delivery-territory:kr:seoul:test",
            new 배달운영권역행정동교체Request(),
            CancellationToken.None);
        Assert.Equal(409, Assert.IsType<ObjectResult>(conflict).StatusCode);

        var unavailableController = CreateController(
            new ThrowingService(new 배달운영권역SourceUnavailableException("AdministrativeDongReferenceUnavailable")));
        var unavailable = await unavailableController.행정동Module목록(
            배달운영권역SourceScopes.NortheastSeoulRiderR1,
            CancellationToken.None);
        Assert.Equal(503, Assert.IsType<ObjectResult>(unavailable).StatusCode);
    }

    [Fact]
    public async Task 저장_예외의_내부_메시지는_API에_노출하지_않는다()
    {
        var controller = CreateController(
            new ThrowingService(new DbUpdateException("sensitive-database-detail")));

        var result = await controller.Draft생성(
            new 배달운영권역Draft생성Request(),
            CancellationToken.None);

        var conflict = Assert.IsType<ObjectResult>(result);
        Assert.Equal(409, conflict.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(conflict.Value);
        Assert.Equal("DeliveryTerritoryPersistenceConflict", problem.Detail);
        Assert.DoesNotContain("sensitive", problem.Detail, StringComparison.Ordinal);
    }

    private static 배달운영권역Controller CreateController(I배달운영권역관리Service service)
    {
        var controller = new 배달운영권역Controller(
            service,
            NullLogger<배달운영권역Controller>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim(ClaimTypes.NameIdentifier, "user:admin:controller-test")
                    ], "test"))
                }
            }
        };
        return controller;
    }

    private sealed class StubService(배달운영권역Dto value) : I배달운영권역관리Service
    {
        public string? LastActorUserStableId { get; private set; }

        public Task<IReadOnlyList<행정동운영ModuleDto>> 행정동Module목록Async(
            string sourceScopeStableId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<행정동운영ModuleDto>>([]);

        public Task<IReadOnlyList<배달운영권역Dto>> 목록Async(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<배달운영권역Dto>>([value]);

        public Task<배달운영권역Dto?> 조회Async(
            string deliveryTerritoryStableId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<배달운영권역Dto?>(value);

        public Task<배달운영권역Dto> Draft생성Async(
            배달운영권역Draft생성Request request,
            string actorUserStableId,
            CancellationToken cancellationToken = default)
        {
            LastActorUserStableId = actorUserStableId;
            return Task.FromResult(value);
        }

        public Task<배달운영권역Dto> 행정동교체Async(
            string deliveryTerritoryStableId,
            배달운영권역행정동교체Request request,
            string actorUserStableId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(value);
    }

    private sealed class ThrowingService(Exception exception) : I배달운영권역관리Service
    {
        public Task<IReadOnlyList<행정동운영ModuleDto>> 행정동Module목록Async(
            string sourceScopeStableId,
            CancellationToken cancellationToken = default)
            => Task.FromException<IReadOnlyList<행정동운영ModuleDto>>(exception);

        public Task<IReadOnlyList<배달운영권역Dto>> 목록Async(CancellationToken cancellationToken = default)
            => Task.FromException<IReadOnlyList<배달운영권역Dto>>(exception);

        public Task<배달운영권역Dto?> 조회Async(
            string deliveryTerritoryStableId,
            CancellationToken cancellationToken = default)
            => Task.FromException<배달운영권역Dto?>(exception);

        public Task<배달운영권역Dto> Draft생성Async(
            배달운영권역Draft생성Request request,
            string actorUserStableId,
            CancellationToken cancellationToken = default)
            => Task.FromException<배달운영권역Dto>(exception);

        public Task<배달운영권역Dto> 행정동교체Async(
            string deliveryTerritoryStableId,
            배달운영권역행정동교체Request request,
            string actorUserStableId,
            CancellationToken cancellationToken = default)
            => Task.FromException<배달운영권역Dto>(exception);
    }
}
