using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ssalddel.Contracts.Food;
using Ssalddel.Controllers.Driver.Profile01;
using Ssalddel.Services.DeliveryExperience;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Options;
using 살뜰.도메인.공통;
using 살뜰.도메인.음식;

namespace Ssalddel.Tests.Services.DeliveryExperience;

public sealed class FoodDeliveryExperienceHttpTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Actual_loopback_http_enforces_role_ownership_revision_and_feature_off(bool enabled)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new SsalddelContext(new DbContextOptionsBuilder<SsalddelContext>()
            .UseSqlite(connection).Options, new FixtureEncryption());
        // OFF 검사는 새 테이블이 아직 없는 기존 DB에서도 조회/저장이 안전한지 확인합니다.
        if (enabled) await db.Database.EnsureCreatedAsync();
        var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions
        { EnvironmentName = "Testing", ContentRootPath = AppContext.BaseDirectory });
        builder.WebHost.UseKestrel(options => options.Listen(IPAddress.Loopback, 0));
        builder.Services.AddLogging();
        builder.Services.AddRouting();
        builder.Services.AddSingleton(db);
        builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
        builder.Services.AddSingleton<IOptions<FoodDeliveryExperienceOptions>>(
            Microsoft.Extensions.Options.Options.Create(new FoodDeliveryExperienceOptions { Enabled = enabled }));
        builder.Services.AddSingleton<ISsalddelExecutionModePolicy>(new SsalddelExecutionModePolicy(
            Microsoft.Extensions.Options.Options.Create(new SsalddelExecutionOptions { Mode = SsalddelExecutionMode.Simulation })));
        builder.Services.AddScoped<음식배달경험Service>();
        builder.Services.AddScoped<Ssalddel.Application.Driver.Settings.기사배달경험공유UseCase>();
        builder.Services.AddControllers().ConfigureApplicationPartManager(manager =>
        {
            manager.ApplicationParts.Clear(); manager.FeatureProviders.Clear();
            manager.FeatureProviders.Add(new OnlyExperienceController());
        }).AddControllersAsServices();
        builder.Services.AddAuthentication("Fixture").AddScheme<AuthenticationSchemeOptions, FixtureAuth>("Fixture", _ => { });
        builder.Services.AddAuthorization();
        await using var app = builder.Build();
        app.UseRouting(); app.UseAuthentication(); app.UseAuthorization(); app.MapControllers();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await app.StartAsync(timeout.Token);
        try
        {
            using var client = new HttpClient(new HttpClientHandler { UseProxy = false, AllowAutoRedirect = false })
            { BaseAddress = new(Assert.Single(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses)) };
            const string route = "/api/v1/driver/preferences/delivery-experience-sharing";
            using var anonymous = await client.GetAsync(route, timeout.Token);
            Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
            client.DefaultRequestHeaders.Add("X-Fixture-Role", "member");
            using var member = await client.GetAsync(route, timeout.Token);
            Assert.Equal(HttpStatusCode.Forbidden, member.StatusCode);
            client.DefaultRequestHeaders.Remove("X-Fixture-Role");
            client.DefaultRequestHeaders.Add("X-Fixture-Role", "driver");
            client.DefaultRequestHeaders.Add("X-Fixture-Actor", "driver-one");
            using var read = await client.GetAsync(route, timeout.Token);
            Assert.Equal(HttpStatusCode.OK, read.StatusCode);
            Assert.True(read.Headers.CacheControl!.NoStore);
            var initial = (await read.Content.ReadFromJsonAsync<DeliveryExperienceSharingPreferenceDto>(timeout.Token))!;
            Assert.Equal(enabled, initial.Enabled); Assert.False(initial.ShareWithCurrentParticipants); Assert.Equal(0, initial.Revision);
            using var write = await client.PutAsJsonAsync(route, new DeliveryExperienceSharingPreferenceRequest
            { ExpectedRevision = 0, ShareWithCurrentParticipants = true }, timeout.Token);
            Assert.Equal(enabled ? HttpStatusCode.OK : HttpStatusCode.ServiceUnavailable, write.StatusCode);
            if (!enabled) return;
            var stored = (await write.Content.ReadFromJsonAsync<DeliveryExperienceSharingPreferenceDto>(timeout.Token))!;
            Assert.True(stored.ShareWithCurrentParticipants); Assert.Equal(1, stored.Revision);
            using var stale = await client.PutAsJsonAsync(route, new DeliveryExperienceSharingPreferenceRequest
            { ExpectedRevision = 0, ShareWithCurrentParticipants = false }, timeout.Token);
            Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode); Assert.True(stale.Headers.CacheControl!.NoStore);
            using var invalid = await client.PutAsJsonAsync(route, new DeliveryExperienceSharingPreferenceRequest
            { ExpectedRevision = -1 }, timeout.Token);
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
            client.DefaultRequestHeaders.Remove("X-Fixture-Actor");
            client.DefaultRequestHeaders.Add("X-Fixture-Actor", "driver-two");
            using var otherRead = await client.GetAsync(route, timeout.Token);
            var other = (await otherRead.Content.ReadFromJsonAsync<DeliveryExperienceSharingPreferenceDto>(timeout.Token))!;
            Assert.False(other.ShareWithCurrentParticipants); Assert.Equal(0, other.Revision);
            Assert.Equal("driver-one", Assert.Single(await db.Set<음식배달경험공유설정>().ToListAsync(timeout.Token)).기사Id);
        }
        finally { using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(5)); await app.StopAsync(stop.Token); }
    }

    private sealed class OnlyExperienceController : IApplicationFeatureProvider<ControllerFeature>
    {
        public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature)
            => feature.Controllers.Add(typeof(기사배달경험공유Controller).GetTypeInfo());
    }
    public sealed class FixtureAuth(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var role = Request.Headers["X-Fixture-Role"].ToString();
            if (role.Length == 0) return Task.FromResult(AuthenticateResult.NoResult());
            var actor = Request.Headers["X-Fixture-Actor"].ToString();
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, actor.Length == 0 ? "member" : actor),
                 new Claim(ClaimTypes.Role, role == "driver" ? 역할명.기사 : 역할명.커뮤니티회원)], Scheme.Name)), Scheme.Name)));
        }
        protected override Task HandleChallengeAsync(AuthenticationProperties properties)
        { Response.StatusCode = 401; return Task.CompletedTask; }
    }
    private sealed class FixtureEncryption : IPersonalDataEncryptionService
    { public string? Protect(string? value) => value; public string? Unprotect(string? value) => value; }
}
