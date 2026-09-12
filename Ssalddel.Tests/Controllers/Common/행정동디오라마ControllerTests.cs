using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Ssalddel.Application.WorldProjection;
using Ssalddel.Controllers.Common;
using Ssalddel.WorkflowRules.Contracts;

namespace Ssalddel.Tests.Controllers.Common;

public sealed class 행정동디오라마ControllerTests
{
    private const string AreaId = "region:kr:hjd:1126057500";

    [Fact]
    public async Task Manifest는_hash를ETag로내보내고_같은판본요청은304를반환한다()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.IfNoneMatch = "\"hash:one\"";
        var controller = new 행정동디오라마Controller(new FakeUseCase())
        {
            ControllerContext = new ControllerContext { HttpContext = context }
        };

        var result = await controller.Manifest(AreaId, CancellationToken.None);

        var status = Assert.IsType<StatusCodeResult>(result);
        Assert.Equal(StatusCodes.Status304NotModified, status.StatusCode);
    }

    [Fact]
    public async Task 게시된행정동이없으면_가짜경계를만들지않고404를반환한다()
    {
        var controller = new 행정동디오라마Controller(new FakeUseCase(found: false))
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var result = await controller.Manifest(AreaId, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    private sealed class FakeUseCase(bool found = true) : I행정동디오라마조회UseCase
    {
        public Task<AdministrativeDongDioramaManifest?> ManifestAsync(string administrativeAreaStableId, CancellationToken cancellationToken)
            => Task.FromResult(found ? new AdministrativeDongDioramaManifest
            {
                AdministrativeAreaStableId = administrativeAreaStableId,
                ProjectionHashSha256 = "hash:one"
            } : null);

        public Task<AdministrativeDongDioramaTile?> TileAsync(string administrativeAreaStableId, string tileStableId, CancellationToken cancellationToken)
            => Task.FromResult<AdministrativeDongDioramaTile?>(null);

        public Task<AdministrativeDongDisplayOverlayResponse?> DisplayOverlaysAsync(string administrativeAreaStableId, DateTime asOfUtc, CancellationToken cancellationToken)
            => Task.FromResult<AdministrativeDongDisplayOverlayResponse?>(null);
    }
}
