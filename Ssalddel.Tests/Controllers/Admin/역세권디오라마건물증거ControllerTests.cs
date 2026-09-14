using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Ssalddel.Application.WorldProjection;
using Ssalddel.Contracts.Common.WorldProjection;
using Ssalddel.Controllers.Admin;
using Ssalddel.WorkflowRules.Contracts;

namespace Ssalddel.Tests.Controllers.Admin;

public sealed class 역세권디오라마건물증거ControllerTests
{
    private const string Hash = "2426E8ABB083EFA3BA07B06675584768ECD1A30E97436E6CE1AED4A4ABA1CA47";

    [Fact]
    public void API는_서버관리자전용이고_공개UnityRoute와분리된다()
    {
        var type = typeof(역세권디오라마건물증거Controller);

        Assert.Equal("서버관리자전용", type.GetCustomAttribute<AuthorizeAttribute>()?.Policy);
        Assert.Equal(역세권디오라마건물증거Routes.Base,
            type.GetCustomAttribute<RouteAttribute>()?.Template);
        Assert.StartsWith("api/v1/admin/", 역세권디오라마건물증거Routes.Base, StringComparison.Ordinal);
        Assert.DoesNotContain("api/v1/world/stations/", 역세권디오라마건물증거Routes.Base, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 개발환경의같은ETag요청은_304와PrivateNoCache를반환한다()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.IfNoneMatch = "\"" + Hash + "\"";
        var controller = Controller(new FakeUseCase(), Environments.Development, context);

        var result = await controller.PresentationBuildingBindings(
            StationDioramaPolicy.SagajeongTransitStationStableId,
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status304NotModified, Assert.IsType<StatusCodeResult>(result).StatusCode);
        Assert.Equal("\"" + Hash + "\"", context.Response.Headers.ETag);
        Assert.Equal("private, no-cache, no-store", context.Response.Headers.CacheControl);
        Assert.Equal("no-cache", context.Response.Headers.Pragma);
    }

    [Fact]
    public async Task 개발환경이아니면_원장을읽지않고404다()
    {
        var useCase = new FakeUseCase();
        var context = new DefaultHttpContext();
        var controller = Controller(useCase, Environments.Production, context);

        var result = await controller.Manifest(
            StationDioramaPolicy.SagajeongTransitStationStableId,
            CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
        Assert.Equal(0, useCase.CallCount);
        Assert.Equal("private, no-cache, no-store", context.Response.Headers.CacheControl);
    }

    [Fact]
    public async Task 누락또는판본불일치는_표본fallback없이503이다()
    {
        var controller = Controller(new FakeUseCase(unavailable: true), Environments.Development,
            new DefaultHttpContext());

        var result = await controller.Manifest(
            StationDioramaPolicy.SagajeongTransitStationStableId,
            CancellationToken.None);

        var response = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, response.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(response.Value);
        Assert.Equal("FrozenEvidenceMismatch", problem.Extensions["code"]);
    }

    [Fact]
    public async Task DB또는역직렬화예외는_내부원인을노출하지않고503이다()
    {
        var controller = Controller(new FakeUseCase(unexpected: true), Environments.Development,
            new DefaultHttpContext());

        var result = await controller.PresentationBuildingAddresses(
            StationDioramaPolicy.SagajeongTransitStationStableId,
            CancellationToken.None);

        var response = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, response.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(response.Value);
        Assert.Equal("StationDioramaBuildingEvidenceReadUnavailable", problem.Extensions["code"]);
        Assert.DoesNotContain("secret-db-message", problem.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 잘못된역ID는400_등록된다른역은404로_사가정만제한한다()
    {
        var useCase = new FakeUseCase();
        var controller = Controller(useCase, Environments.Development, new DefaultHttpContext());

        Assert.IsType<BadRequestResult>(await controller.Manifest("not-a-station", CancellationToken.None));
        Assert.IsType<NotFoundResult>(await controller.Manifest(
            StationDioramaPolicy.MyeonmokTransitStationStableId, CancellationToken.None));
        Assert.Equal(0, useCase.CallCount);
    }

    private static 역세권디오라마건물증거Controller Controller(
        I역세권디오라마건물증거조회UseCase useCase,
        string environment,
        HttpContext context) => new(useCase, Environment(environment))
    {
        ControllerContext = new ControllerContext { HttpContext = context }
    };

    private static IHostEnvironment Environment(string name) => new FakeEnvironment
    {
        EnvironmentName = name,
        ApplicationName = "Ssalddel.Tests",
        ContentRootPath = AppContext.BaseDirectory,
        ContentRootFileProvider = new NullFileProvider()
    };

    private sealed class FakeUseCase(bool unavailable = false, bool unexpected = false) : I역세권디오라마건물증거조회UseCase
    {
        public int CallCount { get; private set; }

        public Task<역세권디오라마건물증거ManifestDto?> ManifestAsync(
            string transitStationStableId, CancellationToken cancellationToken)
        {
            Read();
            return Task.FromResult<역세권디오라마건물증거ManifestDto?>(new()
            {
                TransitStationStableId = transitStationStableId,
                ProjectionHashSha256 = Hash
            });
        }

        public Task<화면건물결속LedgerDto?> PresentationBuildingBindingsAsync(
            string transitStationStableId, CancellationToken cancellationToken)
        {
            Read();
            return Task.FromResult<화면건물결속LedgerDto?>(new() { ProjectionHashSha256 = Hash });
        }

        public Task<화면건물주소LedgerDto?> PresentationBuildingAddressesAsync(
            string transitStationStableId, CancellationToken cancellationToken)
        {
            Read();
            return Task.FromResult<화면건물주소LedgerDto?>(new() { ProjectionHashSha256 = Hash });
        }

        private void Read()
        {
            CallCount++;
            if (unexpected) throw new InvalidOperationException("secret-db-message");
            if (unavailable) throw new 역세권디오라마건물증거UnavailableException("FrozenEvidenceMismatch");
        }
    }

    private sealed class FakeEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = string.Empty;
        public string ApplicationName { get; set; } = string.Empty;
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
