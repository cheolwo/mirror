using System.Reflection;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using FluentResults;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Ssalddel.ApiMetadata;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Shipper.Request;
using Ssalddel.Controllers.Common;
using Ssalddel.Domain.Community;
using Ssalddel.Services.Community;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Options;
using 살뜰.도메인.화주;

namespace Ssalddel.Tests.Services.Community;

/// <summary>
/// Controller 호출에 실제 처리처 Service와 격리 SQLite를 연결하여 소유권·오류·상태 저장·응답 계약을 확인합니다.
/// 상세 재조회 및 공급자는 대역입니다. HTTP는 격리 인증 fixture를 거치며 운영 로그인·업체 접수·배차·지급 검증이 아닙니다.
/// </summary>
public sealed class NeighborhoodFulfillmentApiTests
{
    [Fact]
    public async Task 실제HTTP는_익명접근을차단하고_처리처갱신을클라이언트상세계약으로역직렬화한다()
    {
        await using var fixture = await Fixture.CreateAsync();
        var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Testing", ApplicationName = typeof(생활배송의뢰Controller).Assembly.GetName().Name,
            ContentRootPath = AppContext.BaseDirectory
        });
        builder.WebHost.UseKestrel(options => options.Listen(IPAddress.Loopback, 0));
        builder.Services.AddLogging();
        builder.Services.AddRouting();
        builder.Services.AddControllers().ConfigureApplicationPartManager(manager =>
        {
            manager.ApplicationParts.Clear(); manager.FeatureProviders.Clear();
            manager.FeatureProviders.Add(new OnlyDeliveryController());
        }).AddControllersAsServices();
        builder.Services.AddSingleton(fixture.Controller());
        builder.Services.AddAuthentication("Fixture").AddScheme<AuthenticationSchemeOptions, FixtureAuthentication>("Fixture", _ => { });
        builder.Services.AddAuthorization();
        await using var app = builder.Build();
        app.UseRouting(); app.UseAuthentication(); app.UseAuthorization(); app.MapControllers();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await app.StartAsync(deadline.Token);
        try
        {
            var addresses = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!;
            using var handler = new HttpClientHandler { AllowAutoRedirect = false, UseProxy = false };
            using var client = new HttpClient(handler) { BaseAddress = new Uri(Assert.Single(addresses.Addresses)) };
            var path = "/" + NeighborhoodDeliveryRoutes.Api + "/delivery-a/fulfillment-refresh";
            using var anonymous = await client.PostAsJsonAsync(path, new NeighborhoodFulfillmentActionRequest
                { ClientRequestId = Guid.NewGuid(), ExpectedRevision = 2 }, deadline.Token);
            Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
            Assert.Equal(0, fixture.Provider.Calls);
            client.DefaultRequestHeaders.Add("X-Fixture-Actor", "member-a");
            using var response = await client.PostAsJsonAsync(path, new NeighborhoodFulfillmentActionRequest
                { ClientRequestId = Guid.NewGuid(), ExpectedRevision = 2 }, deadline.Token);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(response.Headers.CacheControl?.NoStore);
            Assert.True(response.Headers.CacheControl?.Private);
            var detail = await response.Content.ReadFromJsonAsync<NeighborhoodDeliveryResponse>(deadline.Token);
            Assert.Equal("delivery-a", detail!.RequestId);
            Assert.Equal(NeighborhoodFulfillmentStates.InTransit, detail.Fulfillment!.StateCode);
            Assert.False(detail.Fulfillment.IsOperationalSettlementEligible);
            Assert.Equal(1, fixture.Provider.Calls);
            Assert.Equal(detail.Fulfillment.Revision, (await fixture.Db.NeighborhoodFulfillments.AsNoTracking().SingleAsync()).Revision);
        }
        finally
        {
            using var stopDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await app.StopAsync(stopDeadline.Token);
        }
    }

    [Fact]
    public void 처리처명령은_인증된기존경로를사용하며_영속새로고침을단순조회로분류하지않는다()
    {
        var type = typeof(생활배송의뢰Controller);
        Assert.NotNull(type.GetCustomAttribute<AuthorizeAttribute>());
        Assert.Equal(NeighborhoodDeliveryRoutes.Api, type.GetCustomAttribute<RouteAttribute>()?.Template);
        Assert.Equal("{requestId}/fulfillment-options", type.GetMethod(nameof(생활배송의뢰Controller.처리처견적))?.GetCustomAttribute<HttpGetAttribute>()?.Template);
        foreach (var (methodName, route) in new[]
        {
            (nameof(생활배송의뢰Controller.처리처선택), "{requestId}/fulfillment-choice"),
            (nameof(생활배송의뢰Controller.처리처새로고침), "{requestId}/fulfillment-refresh"),
            (nameof(생활배송의뢰Controller.처리처취소), "{requestId}/fulfillment-cancel")
        })
        {
            var method = type.GetMethod(methodName)!;
            Assert.Equal(route, method.GetCustomAttribute<HttpPostAttribute>()?.Template);
            Assert.DoesNotContain(method.GetCustomAttributes<SsalddelApiOperationAttribute>(), value => value.Operation == SsalddelOperation.Browse);
        }
    }

    [Theory]
    [InlineData("quote")]
    [InlineData("choice")]
    [InlineData("refresh")]
    [InlineData("cancel")]
    public async Task 다른회원은_조회와명령모두404이며_원장과공급자호출을바꾸지않는다(string operation)
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Current.UserId = "other-member";
        var controller = fixture.Controller();
        var result = await InvokeAsync(controller, operation);
        var problem = Problem(result, 404, "RequestNotFound");
        Assert.Equal("private, no-store", controller.Response.Headers.CacheControl.ToString());
        Assert.DoesNotContain("private-address", JsonSerializer.Serialize(problem));
        Assert.DoesNotContain("010-0000-0000", JsonSerializer.Serialize(problem));
        Assert.Equal(0, fixture.Provider.Calls);
        Assert.Equal(2, (await fixture.Db.NeighborhoodFulfillments.AsNoTracking().SingleAsync()).Revision);
        Assert.Equal(NeighborhoodFulfillmentStates.Accepted, (await fixture.Db.NeighborhoodFulfillmentAttempts.AsNoTracking().SingleAsync()).StateCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 빈명령번호는400이며_수행시도를갱신하지않는다(bool cancel)
    {
        await using var fixture = await Fixture.CreateAsync();
        var controller = fixture.Controller();
        var request = new NeighborhoodFulfillmentActionRequest { ClientRequestId = Guid.Empty, ExpectedRevision = 2 };
        var result = cancel ? await controller.처리처취소("delivery-a", request, default)
            : await controller.처리처새로고침("delivery-a", request, default);
        Problem(result, 400, "InvalidFulfillmentAction");
        Assert.Equal("private, no-store", controller.Response.Headers.CacheControl.ToString());
        Assert.Equal(0, fixture.Provider.Calls);
        Assert.Equal(2, (await fixture.Db.NeighborhoodFulfillments.AsNoTracking().SingleAsync()).Revision);
    }

    [Fact]
    public async Task 오래된판본은409이며_최신상태를공급자호출없이보존한다()
    {
        await using var fixture = await Fixture.CreateAsync();
        var controller = fixture.Controller();
        var result = await controller.처리처새로고침("delivery-a", new() { ClientRequestId = Guid.NewGuid(), ExpectedRevision = 1 }, default);
        Problem(result, 409, "FulfillmentRevisionConflict");
        Assert.Equal("private, no-store", controller.Response.Headers.CacheControl.ToString());
        Assert.Equal(0, fixture.Provider.Calls);
        Assert.Equal(2, (await fixture.Db.NeighborhoodFulfillments.AsNoTracking().SingleAsync()).Revision);
    }

    [Fact]
    public async Task 정상새로고침은_실제상태저장뒤_기존상세응답에처리처를포함한다()
    {
        await using var fixture = await Fixture.CreateAsync();
        var controller = fixture.Controller();
        var result = await controller.처리처새로고침("delivery-a", new() { ClientRequestId = Guid.NewGuid(), ExpectedRevision = 2 }, default);
        var detail = Assert.IsType<NeighborhoodDeliveryResponse>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal("delivery-a", detail.RequestId);
        Assert.Equal("mock-review", detail.Fulfillment!.ProviderId);
        Assert.Equal(NeighborhoodFulfillmentStates.InTransit, detail.Fulfillment.StateCode);
        Assert.False(detail.Fulfillment.IsOperationalSettlementEligible);
        Assert.Equal(detail.Fulfillment.Revision, (await fixture.Db.NeighborhoodFulfillments.AsNoTracking().SingleAsync()).Revision);
        Assert.Equal(NeighborhoodFulfillmentStates.InTransit, (await fixture.Db.NeighborhoodFulfillmentAttempts.AsNoTracking().SingleAsync()).StateCode);
        Assert.Equal("private, no-store", controller.Response.Headers.CacheControl.ToString());
        Assert.Equal(1, fixture.Provider.Calls);
        Assert.Empty(await fixture.Db.운송원장.ToListAsync());
    }

    [Fact]
    public async Task 연결서비스가없으면503이며_모의성공을대신반환하지않는다()
    {
        var controller = new 생활배송의뢰Controller(null!, null!, null!, null!)
        { ControllerContext = new() { HttpContext = new DefaultHttpContext() } };
        var result = await controller.처리처새로고침("delivery-a", new() { ClientRequestId = Guid.NewGuid(), ExpectedRevision = 2 }, default);
        Assert.Equal(503, Assert.IsType<StatusCodeResult>(result).StatusCode);
        Assert.Equal("private, no-store", controller.Response.Headers.CacheControl.ToString());
    }

    private static Task<IActionResult> InvokeAsync(생활배송의뢰Controller controller, string operation)
        => operation switch
        {
            "quote" => controller.처리처견적("delivery-a", default),
            "choice" => controller.처리처선택("delivery-a", new() { ClientRequestId = Guid.NewGuid(), ExpectedRevision = 2,
                Selection = new() { ProviderId = "mock-review", QuoteId = "other-quote", ConfirmedCostKrw = 8000, ProviderInformationSharingAgreed = true } }, default),
            "cancel" => controller.처리처취소("delivery-a", new() { ClientRequestId = Guid.NewGuid(), ExpectedRevision = 2 }, default),
            _ => controller.처리처새로고침("delivery-a", new() { ClientRequestId = Guid.NewGuid(), ExpectedRevision = 2 }, default)
        };

    private static ProblemDetails Problem(IActionResult result, int status, string code)
    {
        var response = Assert.IsType<ObjectResult>(result);
        Assert.Equal(status, response.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(response.Value);
        Assert.Equal(code, problem.Extensions["errorCode"]);
        return problem;
    }

    private sealed class Fixture(SqliteConnection connection, SsalddelContext db) : IAsyncDisposable
    {
        public SsalddelContext Db { get; } = db;
        public Current Current { get; } = new();
        public Provider Provider { get; } = new();
        public I생활배송처리처Service Service { get; private set; } = null!;
        public static async Task<Fixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var db = new SsalddelContext(new DbContextOptionsBuilder<SsalddelContext>().UseSqlite(connection).Options, new Encryption());
            await db.Database.EnsureCreatedAsync();
            var fixture = new Fixture(connection, db);
            var now = DateTime.UtcNow;
            db.화주운송의뢰.Add(new 화주운송의뢰 { 의뢰Id = "delivery-a", 주문자UserId = "member-a", 화주Id = "member-a",
                클라이언트요청Id = NeighborhoodDeliveryRoutes.ClientRequestPrefix + Guid.NewGuid().ToString("N"),
                픽업_도로명주소 = "private-address", 픽업_연락처_전화번호 = "010-0000-0000", 모의처리처수행 = true });
            db.NeighborhoodFulfillmentAttempts.Add(new NeighborhoodFulfillmentAttemptRecord { AttemptId = "attempt-a", RequestId = "delivery-a",
                ClientRequestId = Guid.NewGuid(), ProviderId = "mock-review", QuoteId = "quote-a", StateCode = NeighborhoodFulfillmentStates.Accepted,
                ExternalRequestId = "mock:attempt-a", CostKrw = 8000, IsSimulation = true, SharingAgreed = true,
                CreatedAtUtc = now.AddMinutes(-1), UpdatedAtUtc = now, EstimatedCompletionUtc = now.AddMinutes(20), Revision = 2 });
            db.NeighborhoodFulfillments.Add(new NeighborhoodFulfillmentRecord { RequestId = "delivery-a", ActorId = "member-a",
                AttemptId = "attempt-a", Revision = 2, UpdatedAtUtc = now });
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            fixture.Service = new 생활배송처리처Service(db, fixture.Current, [fixture.Provider],
                Options.Create(new NeighborhoodFulfillmentOptions { Enabled = true }),
                new SsalddelExecutionModePolicy(Options.Create(new SsalddelExecutionOptions { Mode = SsalddelExecutionMode.Simulation })), TimeProvider.System, null!);
            return fixture;
        }
        public 생활배송의뢰Controller Controller() => new(new Details(Service), null!, null!, null!, Service)
        { ControllerContext = new() { HttpContext = new DefaultHttpContext() } };
        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await connection.DisposeAsync(); }
    }
    private sealed class Current : ICurrentUserAccessor
    {
        public string? UserId { get; set; } = "member-a";
        public string? Role => "일반회원";
    }
    private sealed class Encryption : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
    private sealed class Provider : IFulfillmentProvider
    {
        public string Id => "mock-review";
        public string Name => "API 경계 시험 공급자";
        public bool IsSimulation => true;
        public FulfillmentProviderCapabilities Capabilities => new(20, 30, 20, ["오토바이"]);
        public int Calls { get; private set; }
        public Task<FulfillmentProviderQuote> QuoteAsync(FulfillmentQuoteInput input, CancellationToken ct) => throw new NotSupportedException();
        public Task<FulfillmentProviderResult> SubmitAsync(FulfillmentProviderOperation operation, CancellationToken ct) => throw new NotSupportedException();
        public Task<FulfillmentProviderResult> ReadAsync(FulfillmentProviderOperation operation, CancellationToken ct)
        { Calls++; return Task.FromResult(new FulfillmentProviderResult(NeighborhoodFulfillmentStates.InTransit, operation.ExternalRequestId)); }
        public Task<FulfillmentProviderResult> CancelAsync(FulfillmentProviderOperation operation, CancellationToken ct)
        { Calls++; return Task.FromResult(new FulfillmentProviderResult(NeighborhoodFulfillmentStates.Cancelled, operation.ExternalRequestId)); }
    }
    private sealed class Details(I생활배송처리처Service service) : I생활배송의뢰UseCase
    {
        public async Task<NeighborhoodDeliveryResponse?> 내상세Async(string requestId, CancellationToken cancellationToken = default)
        {
            var snapshot = await service.내상태Async(requestId, cancellationToken);
            return snapshot is null ? null : new() { RequestId = requestId, Fulfillment = snapshot };
        }
        public Task<Result<NeighborhoodDeliveryQuoteResponse>> 견적Async(NeighborhoodDeliveryRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<NeighborhoodDeliveryResponse>> 등록Async(NeighborhoodDeliveryRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<NeighborhoodDeliveryResponse>> 내목록Async(int page = 1, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class OnlyDeliveryController : IApplicationFeatureProvider<ControllerFeature>
    {
        public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature)
            => feature.Controllers.Add(typeof(생활배송의뢰Controller).GetTypeInfo());
    }
    public sealed class FixtureAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger, UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var actor = Request.Headers["X-Fixture-Actor"].ToString();
            if (string.IsNullOrWhiteSpace(actor)) return Task.FromResult(AuthenticateResult.NoResult());
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(
                new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, actor)], Scheme.Name)), Scheme.Name)));
        }
        protected override Task HandleChallengeAsync(AuthenticationProperties properties)
        { Response.StatusCode = StatusCodes.Status401Unauthorized; return Task.CompletedTask; }
    }
}
