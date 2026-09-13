using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Ssalddel.Application.WorldProjection;
using Ssalddel.Controllers.Common;
using Ssalddel.WorkflowRules.Contracts;

namespace Ssalddel.Tests.Controllers.Common;

public sealed class 지역ExperiencePackageControllerTests
{
    [Fact]
    public async Task Catalog는_revision을ETag로내보내고_같은판본은304다()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.IfNoneMatch = "\"catalog-hash\"";
        var controller = new 지역ExperiencePackageController(new FakeUseCase())
        {
            ControllerContext = new ControllerContext { HttpContext = context }
        };

        var result = await controller.Catalog(CancellationToken.None);

        var status = Assert.IsType<StatusCodeResult>(result);
        Assert.Equal(StatusCodes.Status304NotModified, status.StatusCode);
    }

    [Fact]
    public async Task 등록되지않은지역Manifest는404다()
    {
        var controller = new 지역ExperiencePackageController(new FakeUseCase(found: false))
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var result = await controller.Manifest("world-region:kr:unknown", CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    private sealed class FakeUseCase(bool found = true) : I지역ExperiencePackage조회UseCase
    {
        public Task<RegionExperiencePackageCatalogResponse> CatalogAsync(CancellationToken cancellationToken)
            => Task.FromResult(new RegionExperiencePackageCatalogResponse { CatalogRevision = "catalog-hash" });

        public Task<RegionExperiencePackageManifest?> ManifestAsync(string regionStableId, CancellationToken cancellationToken)
            => Task.FromResult(found ? new RegionExperiencePackageManifest
            {
                RegionStableId = regionStableId,
                ManifestHashSha256 = "manifest-hash"
            } : null);
    }
}
