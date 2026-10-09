using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Ssalddel.Application.Pricing;
using Ssalddel.Contracts.Common.Pricing;
using Ssalddel.Contracts.Food;
using Ssalddel.Controllers.Common;
using 살뜰.Services.Versioning;
using 살뜰.도메인.공통;

namespace Ssalddel.Tests.Application.Pricing;

public sealed class AgreedRateTablesControllerTests
{
    [Fact]
    public async Task 익명또는역할권한없는기사와음식점요청은서비스에도달하지않는다()
    {
        var port = new CapturingUseCase(); var c = Controller(port);
        Assert.IsType<UnauthorizedResult>(await c.List(AgreedRatePartyKinds.Shipper, default));
        c.ControllerContext.HttpContext.User = User("owner");
        Assert.IsType<ForbidResult>(await c.List(AgreedRatePartyKinds.FoodDriver, default));
        Assert.IsType<ForbidResult>(await c.List(AgreedRatePartyKinds.Restaurant, default));
        Assert.Null(port.Actor);
    }

    [Fact]
    public async Task 음식점은입력한사용자ID대신인증된음식점범위로조회한다()
    {
        var port = new CapturingUseCase(); var c = Controller(port);
        c.ControllerContext.HttpContext.User = User("account-owner", new Claim(음식점접근ClaimTypes.음식점Id, "41"));
        Assert.IsType<OkObjectResult>(await c.List(AgreedRatePartyKinds.Restaurant, default));
        Assert.Equal(new AgreedRatePartyDto(AgreedRatePartyKinds.Restaurant, "41"), port.Actor);
    }

    [Theory]
    [InlineData(AgreedRatePartyKinds.FoodDriver)]
    [InlineData(AgreedRatePartyKinds.CargoDriver)]
    [InlineData(AgreedRatePartyKinds.Shipper)]
    public async Task 기사화주는현재인증사용자와요청역할을함께결속한다(string kind)
    {
        var port = new CapturingUseCase(); var c = Controller(port);
        c.ControllerContext.HttpContext.User = User("authenticated-id", new Claim(ClaimTypes.Role, "기사"));
        Assert.IsType<OkObjectResult>(await c.List(kind, default));
        Assert.Equal(new AgreedRatePartyDto(kind, "authenticated-id"), port.Actor);
    }

    [Theory]
    [InlineData(AgreedRatePartyKinds.FoodDriver, VersionFeatureFlagKeys.FoodDeliveryWorkflow)]
    [InlineData(AgreedRatePartyKinds.CargoDriver, VersionFeatureFlagKeys.DomesticTransportWorkflow)]
    public async Task 업무별기존feature가꺼져있으면다른업종권한으로우회하지못한다(string kind, string expectedFeature)
    {
        var port = new CapturingUseCase(); var flags = new Flags { Enabled = false }; var c = Controller(port, flags);
        c.ControllerContext.HttpContext.User = User("authenticated-id", new Claim(ClaimTypes.Role, "기사"));
        Assert.IsType<NotFoundObjectResult>(await c.List(kind, default));
        Assert.Equal(expectedFeature, flags.Last); Assert.Null(port.Actor);
    }

    private static AgreedRateTablesController Controller(CapturingUseCase port, Flags? flags = null)
        => new(port, new Authorization(), flags ?? new())
        { ControllerContext = new() { HttpContext = new DefaultHttpContext() } };
    private static ClaimsPrincipal User(string id, params Claim[] claims)
        => new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, id), .. claims], "Test"));
    private sealed class Authorization : IAuthorizationService
    {
        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object? resource, IEnumerable<IAuthorizationRequirement> requirements)
            => Task.FromResult(AuthorizationResult.Success());
        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object? resource, string policyName)
            => Task.FromResult(AuthorizationResult.Success());
    }
    private sealed class Flags : IVersionFeatureFlagService
    {
        public bool Enabled { get; set; } = true; public string? Last { get; private set; }
        public bool IsEnabled(string key) { Last = key; return Enabled; }
        public IReadOnlyDictionary<string, bool> GetAll() => new Dictionary<string, bool>();
    }
    private sealed class CapturingUseCase : IAgreedRateTableUseCase
    {
        public AgreedRatePartyDto? Actor { get; private set; }
        public Task<IReadOnlyList<AgreedRateTableDto>> ListAsync(AgreedRatePartyDto actor, CancellationToken ct)
        { Actor = actor; return Task.FromResult<IReadOnlyList<AgreedRateTableDto>>([]); }
        public Task<AgreedRateTableDto> GetAsync(string id, AgreedRatePartyDto actor, CancellationToken ct) => throw new NotSupportedException();
        public Task<AgreedRateTableDto> ProposeAsync(AgreedRatePartyDto actor, AgreedRateTableProposalRequest request, CancellationToken ct) => throw new NotSupportedException();
        public Task<AgreedRateTableDto> ActAsync(string id, AgreedRatePartyDto actor, AgreedRateTableActionRequest request, CancellationToken ct) => throw new NotSupportedException();
        public Task<AgreedRateTablePreviewDto> PreviewAsync(string id, AgreedRatePartyDto actor, AgreedRateTablePreviewRequest request, CancellationToken ct) => throw new NotSupportedException();
        public Task<AgreedRateTableSelectionDto> SelectAsync(string id, AgreedRatePartyDto actor, AgreedRateTableSelectionRequest request, CancellationToken ct) => throw new NotSupportedException();
        public Task<AgreedRateTableSelectionDto> GetSelectionAsync(string id, string workId, AgreedRatePartyDto actor, CancellationToken ct) => throw new NotSupportedException();
        public Task<AgreedRateTableSnapshotDto?> SelectedAsync(string business, string workId, string driverId, string vehicle, CancellationToken ct) => throw new NotSupportedException();
        public Task<string?> SelectedDriverAsync(string business, string workId, CancellationToken ct) => throw new NotSupportedException();
    }
}
