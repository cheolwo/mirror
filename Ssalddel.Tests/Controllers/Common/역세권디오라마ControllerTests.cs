using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Ssalddel.ApiMetadata;
using Ssalddel.Application.WorldProjection;
using Ssalddel.Controllers.Common;
using Ssalddel.Filters;
using Ssalddel.WorkflowRules.Contracts;
using 살뜰.Services.Versioning;

namespace Ssalddel.Tests.Controllers.Common;

public sealed class 역세권디오라마ControllerTests
{
    [Fact]
    public void 역API는_기존지역패키지기능플래그와_구분된역식별자route를사용한다()
    {
        var type = typeof(역세권디오라마Controller);
        var version = type.GetCustomAttribute<SsalddelApiVersionAttribute>();
        var feature = Assert.Single(type.GetCustomAttributes<RequireVersionFeatureAttribute>());
        var route = type.GetCustomAttribute<RouteAttribute>();

        Assert.NotNull(type.GetCustomAttribute<AuthorizeAttribute>());
        Assert.NotNull(version);
        Assert.Equal(SsalddelProductVersion.V0_0, version.Version);
        Assert.Equal(VersionFeatureFlagKeys.RegionExperiencePackages, version.FeatureKey);
        Assert.Equal(VersionFeatureFlagKeys.RegionExperiencePackages, version.WorkflowKey);
        Assert.Equal(
            VersionFeatureFlagKeys.RegionExperiencePackages,
            Assert.IsType<string>(Assert.Single(feature.Arguments!)));
        Assert.Equal(StationDioramaRoutes.Catalog, route?.Template);
        Assert.Equal(
            "api/v1/world/stations/{transitStationStableId}/diorama-manifest",
            StationDioramaRoutes.Manifest);
    }

    [Fact]
    public async Task Catalog는_revision을ETag로내보내고_같은판본은304다()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.IfNoneMatch = "\"catalog-hash\"";
        var controller = new 역세권디오라마Controller(new FakeUseCase())
        {
            ControllerContext = new ControllerContext { HttpContext = context }
        };

        var result = await controller.Catalog(CancellationToken.None);

        var status = Assert.IsType<StatusCodeResult>(result);
        Assert.Equal(StatusCodes.Status304NotModified, status.StatusCode);
    }

    [Fact]
    public async Task Manifest는_사본ETag와private캐시를내보내고_등록되지않은역은404다()
    {
        var context = new DefaultHttpContext();
        var controller = new 역세권디오라마Controller(new FakeUseCase())
        {
            ControllerContext = new ControllerContext { HttpContext = context }
        };

        var found = await controller.Manifest(
            StationDioramaPolicy.SagajeongTransitStationStableId,
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(found);
        Assert.IsType<StationDioramaManifest>(ok.Value);
        Assert.Equal("\"manifest-hash\"", context.Response.Headers.ETag);
        Assert.Equal("private, max-age=60", context.Response.Headers.CacheControl);

        var missingController = new 역세권디오라마Controller(new FakeUseCase(found: false))
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        var missing = await missingController.Manifest(
            "station:kr:kric:s1107:9999",
            CancellationToken.None);
        Assert.IsType<NotFoundResult>(missing);
    }

    [Fact]
    public async Task 잘못된TransitStationStableId는_UseCase호출없이400이다()
    {
        var useCase = new FakeUseCase();
        var controller = new 역세권디오라마Controller(useCase)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var result = await controller.Manifest("workstation:packing:01", CancellationToken.None);

        Assert.IsType<BadRequestResult>(result);
        Assert.Equal(0, useCase.ManifestCallCount);
    }

    private sealed class FakeUseCase(bool found = true) : I역세권디오라마조회UseCase
    {
        public int ManifestCallCount { get; private set; }

        public Task<StationDioramaCatalogResponse> CatalogAsync(CancellationToken cancellationToken)
            => Task.FromResult(new StationDioramaCatalogResponse { CatalogRevision = "catalog-hash" });

        public Task<StationDioramaManifest?> ManifestAsync(
            string transitStationStableId,
            CancellationToken cancellationToken)
        {
            ManifestCallCount++;
            return Task.FromResult(found
                ? new StationDioramaManifest
                {
                    TransitStationStableId = transitStationStableId,
                    ManifestHashSha256 = "manifest-hash"
                }
                : null);
        }
    }
}
