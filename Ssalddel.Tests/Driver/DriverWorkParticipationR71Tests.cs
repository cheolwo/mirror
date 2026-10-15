using System.Security.Claims;
using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Ssalddel.Application.Driver.Participation;
using Ssalddel.Contracts.Driver.Settings;
using Ssalddel.Controllers.Driver.Profile01;
using Ssalddel.Migrations;
using Ssalddel.Services.Dispatch.Participation;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.도메인.기사;
using 살뜰.도메인.공통;

namespace Ssalddel.Tests.Driver;

public sealed class DriverWorkParticipationR71Tests
{
    [Fact]
    public async Task FoodOnlyProfileCanSaveMultipleWishesWithoutCreatingCargoQualification()
    {
        await using var f = await Fixture.Create();
        var saved = await f.Service.UpdateAsync("driver", Input(0));
        Assert.True(saved.IsSuccess, string.Join(';', saved.Errors));
        Assert.Equal(1, saved.Value.Settings.Revision);
        Assert.True(saved.Value.FoodEligible); Assert.False(saved.Value.CargoEligible);
        Assert.True(saved.Value.Settings.CargoTransport); Assert.True(saved.Value.Settings.DirectPicking);
        Assert.False(saved.Value.SharedAdmissionEnabled);
        Assert.False(await f.Db.용달기사.AnyAsync());
        Assert.Equal(saved.Value.Settings, (await f.Service.GetAsync("driver")).Value.Settings);
    }

    [Fact]
    public async Task StaleEditorAndRepeatedFirstWriteCannotOverwriteLatestSettings()
    {
        await using var f = await Fixture.Create();
        Assert.True((await f.Service.UpdateAsync("driver", Input(0))).IsSuccess);
        var firstAgain = await f.Service.UpdateAsync("driver", Input(0));
        Assert.False(firstAgain.IsSuccess);
        var next = Input(1) with { Settings = Input(1).Settings with { FoodDelivery = false, MaxConcurrentJobs = 1 } };
        Assert.True((await f.Service.UpdateAsync("driver", next)).IsSuccess);
        Assert.False((await f.Service.UpdateAsync("driver", Input(1))).IsSuccess);
        var final = (await f.Service.GetAsync("driver")).Value.Settings;
        Assert.Equal(2, final.Revision); Assert.False(final.FoodDelivery); Assert.Equal(1, final.MaxConcurrentJobs);
    }

    [Fact]
    public async Task ChangingWishesKeepsAdmissionLockRevisionAndDoesNotRewriteWorkLedgers()
    {
        await using var f = await Fixture.Create(true);
        Assert.True((await f.Service.UpdateAsync("driver", Input(0))).IsSuccess);
        await f.Db.Set<DriverWorkParticipationRecord>().ExecuteUpdateAsync(s => s.SetProperty(x => x.WorkLockRevision, 9L));
        Assert.True((await f.Service.UpdateAsync("driver", Input(1))).IsSuccess);
        var row = await f.Db.Set<DriverWorkParticipationRecord>().AsNoTracking().SingleAsync();
        Assert.Equal(10, row.WorkLockRevision); Assert.Equal(2, row.Revision);
        Assert.Equal(1, await f.Db.배달기사.CountAsync());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("other")]
    public async Task UnauthenticatedOrUnregisteredActorCannotReadOrWrite(string? actor)
    {
        await using var f = await Fixture.Create();
        Assert.False((await f.Service.GetAsync(actor)).IsSuccess);
        Assert.False((await f.Service.UpdateAsync(actor, Input(0))).IsSuccess);
        Assert.False(await f.Db.Set<DriverWorkParticipationRecord>().AnyAsync());
    }

    [Theory]
    [InlineData("revision")]
    [InlineData("negative-revision")]
    [InlineData("overflow-revision")]
    [InlineData("vehicle")]
    [InlineData("zero-count")]
    [InlineData("limit")]
    [InlineData("picking-only")]
    [InlineData("zero-weight")]
    [InlineData("negative-volume")]
    [InlineData("precision")]
    public async Task InvalidPreferencesNeverPersist(string kind)
    {
        await using var f = await Fixture.Create();
        var input = Input(0);
        input = kind switch
        {
            "revision" => input with { Settings = input.Settings with { Revision = 1 } },
            "negative-revision" => input with { ExpectedRevision = -1, Settings = input.Settings with { Revision = -1 } },
            "overflow-revision" => input with { ExpectedRevision = long.MaxValue, Settings = input.Settings with { Revision = long.MaxValue } },
            "vehicle" => input with { Settings = input.Settings with { VehicleKind = "unknown" } },
            "zero-count" => input with { Settings = input.Settings with { MaxConcurrentJobs = 0 } },
            "limit" => input with { Settings = input.Settings with { MaxConcurrentJobs = 5 } },
            "picking-only" => input with { Settings = input.Settings with { QuickDelivery = false, CargoTransport = false, DirectPicking = true } },
            "zero-weight" => input with { Settings = input.Settings with { CapacityKg = 0 } },
            "negative-volume" => input with { Settings = input.Settings with { CapacityLitres = -1 } },
            _ => input with { Settings = input.Settings with { CapacityKg = .0001m } }
        };
        var result = await f.Service.UpdateAsync("driver", input);
        Assert.False(result.IsSuccess); Assert.False(await f.Db.Set<DriverWorkParticipationRecord>().AnyAsync());
    }

    [Fact]
    public async Task EmptyMeasurementsStayUnknownAndAllWorkCanBeDeselected()
    {
        await using var f = await Fixture.Create();
        var input = Input(0) with { Settings = Input(0).Settings with
        { FoodDelivery = false, QuickDelivery = false, CargoTransport = false, DirectPicking = false, CapacityKg = null, CapacityLitres = null } };
        var result = await f.Service.UpdateAsync("driver", input);
        Assert.True(result.IsSuccess); Assert.Null(result.Value.Settings.CapacityKg); Assert.Null(result.Value.Settings.CapacityLitres);
    }

    [Fact]
    public async Task ControllerUsesAuthenticatedSubjectAndHasNoCallerSelectedDriverParameter()
    {
        await using var f = await Fixture.Create();
        var controller = new DriverWorkParticipationController(f.Service)
        {
            ControllerContext = new() { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "driver")], "test")) } }
        };
        Assert.NotNull(typeof(DriverWorkParticipationController).GetCustomAttributes(typeof(AuthorizeAttribute), true).SingleOrDefault());
        var result = Assert.IsType<OkObjectResult>(await controller.저장(Input(0), default));
        Assert.NotNull(result.Value);
        Assert.Equal("driver", (await f.Db.Set<DriverWorkParticipationRecord>().AsNoTracking().SingleAsync()).UserId);
        Assert.DoesNotContain(typeof(DriverWorkParticipationUpdate).GetProperties(), x => x.Name == "DriverId");
    }

    [Fact]
    public void MigrationAddsOnlyPrivatePreferenceAndLockTable()
    {
        var migration = new AddDriverWorkParticipation();
        var table = Assert.Single(migration.UpOperations.OfType<CreateTableOperation>());
        Assert.Equal("driver_work_participation", table.Name);
        Assert.Equal("UserId", Assert.Single(table.PrimaryKey!.Columns));
        Assert.Empty(migration.UpOperations.OfType<SqlOperation>());
        Assert.Contains(table.Columns, x => x.Name == "CapacityKg" && x.IsNullable);
        Assert.Contains(table.Columns, x => x.Name == "CapacityLitres" && x.IsNullable);
    }

    [Fact]
    public async Task ActualLoopbackHttpRequiresDriverRoleAndOwnIdentityAndProtectsRevision()
    {
        await using var f = await Fixture.Create();
        var builder = WebApplication.CreateEmptyBuilder(new() { EnvironmentName = "Testing", ContentRootPath = AppContext.BaseDirectory });
        builder.WebHost.UseKestrel(o => o.Listen(IPAddress.Loopback, 0));
        builder.Services.AddLogging(); builder.Services.AddRouting();
        builder.Services.AddSingleton<IDriverWorkParticipationUseCase>(f.Service);
        builder.Services.AddControllers().ConfigureApplicationPartManager(m =>
        { m.ApplicationParts.Clear(); m.FeatureProviders.Clear(); m.FeatureProviders.Add(new OnlyParticipationController()); }).AddControllersAsServices();
        builder.Services.AddAuthentication("R71").AddScheme<AuthenticationSchemeOptions, FixtureAuth>("R71", _ => { });
        builder.Services.AddAuthorization();
        await using var app = builder.Build();
        app.UseRouting(); app.UseAuthentication(); app.UseAuthorization(); app.MapControllers();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await app.StartAsync(timeout.Token);
        try
        {
            using var client = new HttpClient(new HttpClientHandler { UseProxy = false, AllowAutoRedirect = false })
            { BaseAddress = new(Assert.Single(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses)) };
            const string route = "/api/v1/driver/work-participation";
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(route, timeout.Token)).StatusCode);
            client.DefaultRequestHeaders.Add("X-R71-Actor", "driver");
            client.DefaultRequestHeaders.Add("X-R71-Role", "member");
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync(route, Input(0), timeout.Token)).StatusCode);
            client.DefaultRequestHeaders.Remove("X-R71-Role"); client.DefaultRequestHeaders.Add("X-R71-Role", "driver");
            var read = await client.GetAsync(route, timeout.Token);
            Assert.Equal(HttpStatusCode.OK, read.StatusCode); Assert.True(read.Headers.CacheControl?.NoStore);
            var initial = await read.Content.ReadFromJsonAsync<DriverWorkParticipationResponse>(timeout.Token);
            Assert.Equal("driver", initial!.DriverId); Assert.Equal(0, initial.Settings.Revision);
            Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync(route, Input(0), timeout.Token)).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync(route, Input(0), timeout.Token)).StatusCode);
            client.DefaultRequestHeaders.Remove("X-R71-Actor"); client.DefaultRequestHeaders.Add("X-R71-Actor", "other");
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(route, timeout.Token)).StatusCode);
            Assert.Equal("driver", (await f.Db.Set<DriverWorkParticipationRecord>().AsNoTracking().SingleAsync()).UserId);
        }
        finally { await app.StopAsync(timeout.Token); }
    }

    private sealed class OnlyParticipationController : IApplicationFeatureProvider<ControllerFeature>
    {
        public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature)
            => feature.Controllers.Add(typeof(DriverWorkParticipationController).GetTypeInfo());
    }
    private sealed class FixtureAuth(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var actor = Request.Headers["X-R71-Actor"].ToString();
            if (string.IsNullOrWhiteSpace(actor)) return Task.FromResult(AuthenticateResult.NoResult());
            var role = Request.Headers["X-R71-Role"].ToString();
            var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, actor), new Claim(ClaimTypes.Role, role == "driver" ? 역할명.기사 : role)], Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
        }
    }

    private static DriverWorkParticipationUpdate Input(long revision) => new()
    {
        ExpectedRevision = revision,
        Settings = new() { Revision = revision, FoodDelivery = true, QuickDelivery = true, CargoTransport = true,
            DirectPicking = true, VehicleKind = DriverVehicleKinds.Motorcycle, MaxConcurrentJobs = 3, CapacityKg = 20, CapacityLitres = 80 }
    };

    private sealed class Fixture : IAsyncDisposable
    {
        public required SqliteConnection Connection { get; init; }
        public required SsalddelContext Db { get; init; }
        public required DriverWorkParticipationUseCase Service { get; init; }
        public static async Task<Fixture> Create(bool enabled = false)
        {
            var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
            var db = new SsalddelContext(new DbContextOptionsBuilder<SsalddelContext>().UseSqlite(connection).Options, new Encryption());
            var migration = new AddDriverWorkParticipation { ActiveProvider = db.Database.ProviderName! };
            foreach (var command in db.GetService<IMigrationsSqlGenerator>().Generate(migration.UpOperations))
                await db.Database.ExecuteSqlRawAsync(command.CommandText);
            await db.Database.ExecuteSqlRawAsync("CREATE TABLE 배달기사 (기사Id TEXT, 상태 TEXT); INSERT INTO 배달기사(기사Id,상태) VALUES ('driver','활동중'); CREATE TABLE 용달기사 (기사Id TEXT, 상태 TEXT);");
            var options = Options.Create(new SharedDriverWorkOptions { Enabled = enabled });
            return new() { Connection = connection, Db = db, Service = new(db, options, TimeProvider.System, new SharedDriverWorkAdmissionService(db, options)) };
        }
        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await Connection.DisposeAsync(); }
    }
    private sealed class Encryption : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
}
