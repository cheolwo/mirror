using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Ssalddel.Application.WorldProjection;
using Ssalddel.Controllers.Common;
using Ssalddel.WorkflowRules.Contracts;

namespace Ssalddel.Tests.Controllers.Common;

public sealed class 지역이동망ControllerTests
{
    [Fact]
    public async Task 개발환경Manifest는_projectionHash를ETag로내보내고_같은요청은304다()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.IfNoneMatch = "\"" + Hash + "\"";
        var controller = new 지역이동망Controller(new FakeUseCase(), Environment(Environments.Development))
        {
            ControllerContext = new ControllerContext { HttpContext = context }
        };

        var result = await controller.Manifest(
            RegionMobilityGraphPolicy.FirstRegionStableId,
            CancellationToken.None);

        var status = Assert.IsType<StatusCodeResult>(result);
        Assert.Equal(StatusCodes.Status304NotModified, status.StatusCode);
    }

    [Fact]
    public async Task 개발환경이아니면_검토후보를읽지않고404다()
    {
        var useCase = new FakeUseCase();
        var controller = new 지역이동망Controller(useCase, Environment(Environments.Production))
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var result = await controller.Manifest(
            RegionMobilityGraphPolicy.FirstRegionStableId,
            CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
        Assert.False(useCase.Read);
    }

    private const string Hash = "59DD155C0D49BEB0612062DF6E1571A751FE0EC1E9DA4AD4AF41A879EB4FE6F7";

    private static IHostEnvironment Environment(string name) => new FakeEnvironment
    {
        EnvironmentName = name,
        ApplicationName = "Ssalddel.Tests",
        ContentRootPath = AppContext.BaseDirectory,
        ContentRootFileProvider = new NullFileProvider()
    };

    private sealed class FakeUseCase : I지역이동망조회UseCase
    {
        public bool Read { get; private set; }

        public Task<RegionMobilityGraphManifest?> ManifestAsync(
            string regionStableId,
            CancellationToken cancellationToken)
        {
            Read = true;
            return Task.FromResult<RegionMobilityGraphManifest?>(new RegionMobilityGraphManifest
            {
                RegionStableId = regionStableId,
                ProjectionHashSha256 = Hash
            });
        }

        public Task<RegionMobilityGraphTile?> TileAsync(
            string regionStableId,
            string tileStableId,
            CancellationToken cancellationToken)
            => Task.FromResult<RegionMobilityGraphTile?>(null);
    }

    private sealed class FakeEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = string.Empty;
        public string ApplicationName { get; set; } = string.Empty;
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
