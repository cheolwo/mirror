using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ssalddel.Contracts.Common.Commerce;
using Ssalddel.Controllers.Common;
using Ssalddel.Extensions;
using Ssalddel.Services.Commerce;

namespace Ssalddel.Tests.Services.Commerce;

public sealed class NeighborhoodTaskContractPolicyTests
{
    [Fact]
    public void 기존통신판매등록에서도_새건별의뢰정책은성인기본값과꺼진참여옵션을반환한다()
    {
        var services = new ServiceCollection();
        services.Add통신판매보호Services(new ConfigurationBuilder().Build());
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var policy = scope.ServiceProvider.GetRequiredService<NeighborhoodTaskContractPolicyService>().GetPolicy();

        Assert.Equal(NeighborhoodTaskContractModels.PerTaskFreelance, policy.DefaultContractModel);
        Assert.Equal("NewPerTaskProposalPreparation", policy.ScopeCode);
        Assert.Equal(19, policy.OperationalMinimumAgeYears);
        Assert.Equal("ReservedDisabled", policy.MinorParticipationStatusCode);
        Assert.True(policy.RequiresVerifiedLegalRepresentative);
        Assert.False(policy.CanCreateContracts);
        Assert.False(policy.IsLegalClassificationDetermined);
        Assert.False(policy.IsMinorParticipationEnabled);
        Assert.False(string.IsNullOrWhiteSpace(policy.Version));
        Assert.NotEmpty(policy.FutureActivationRequirements);
    }

    [Fact]
    public void 미래검토근거와업무시간을저장해도_미성년참여와계약생성을허가하지않고원문참조를노출하지않는다()
    {
        var values = PreparedConfiguration();
        using var provider = Provider(values);
        var stored = provider.GetRequiredService<IOptions<NeighborhoodTaskContractOptions>>().Value;
        var policy = provider.GetRequiredService<NeighborhoodTaskContractPolicyService>().GetPolicy();

        Assert.Equal("private-future-policy-revision", stored.MinorParticipation.PolicyRevision);
        Assert.Equal("private-legal-basis-reference", stored.MinorParticipation.LegalBasisReference);
        Assert.Equal("private-legal-review-reference", stored.MinorParticipation.LegalReviewReference);
        Assert.False(stored.MinorParticipation.Enabled);
        Assert.False(policy.IsMinorParticipationEnabled);
        Assert.False(policy.CanCreateContracts);
        Assert.False(policy.IsLegalClassificationDetermined);
        Assert.True(policy.RequiresVerifiedLegalRepresentative);
        Assert.Equal(19, policy.OperationalMinimumAgeYears);
        Assert.Equal("ReservedDisabled", policy.MinorParticipationStatusCode);

        var json = JsonSerializer.Serialize(policy);
        Assert.DoesNotContain("private-future-policy-revision", json);
        Assert.DoesNotContain("private-legal-basis-reference", json);
        Assert.DoesNotContain("private-legal-review-reference", json);
        Assert.DoesNotContain("PermittedWorkKinds", json);
        Assert.DoesNotContain("PermittedLocalTimeWindows", json);
        Assert.DoesNotContain("2035-01-01", json);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void 미성년참여Enabled설정은_미래검토근거가모두있어도실제DI옵션해결에서거부된다(bool prepared)
    {
        var values = prepared ? PreparedConfiguration() : new Dictionary<string, string?>();
        values[Key("MinorParticipation:Enabled")] = "true";
        using var provider = Provider(values);

        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<NeighborhoodTaskContractOptions>>().Value);
    }

    [Theory]
    [InlineData("Employment")]
    [InlineData("HrEmploymentContract")]
    [InlineData("")]
    public void 설정으로건별의뢰기본값을고용계약이나미확정모델로바꿀수없다(string model)
    {
        using var provider = Provider(new Dictionary<string, string?> { [Key("DefaultContractModel")] = model });

        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<NeighborhoodTaskContractOptions>>().Value);
    }

    [Fact]
    public void 직접생성으로DI옵션검증을건너뛰어도서비스가참여활성화를거부한다()
    {
        var service = new NeighborhoodTaskContractPolicyService(Options.Create(new NeighborhoodTaskContractOptions
        {
            MinorParticipation = new() { Enabled = true }
        }));

        Assert.Throws<OptionsValidationException>(() => service.GetPolicy());
    }

    [Fact]
    public void 기존등록과전용등록을함께호출해도검증기와정책서비스가중복등록되지않는다()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(PreparedConfiguration()).Build();
        var services = new ServiceCollection();
        services.Add통신판매보호Services(configuration).AddNeighborhoodTaskContractPolicy(configuration);

        Assert.Single(services.Where(x => x.ServiceType == typeof(IValidateOptions<NeighborhoodTaskContractOptions>)));
        Assert.Single(services.Where(x => x.ServiceType == typeof(NeighborhoodTaskContractPolicyService)));
        using var provider = services.BuildServiceProvider();
        Assert.False(provider.GetRequiredService<NeighborhoodTaskContractPolicyService>().GetPolicy().IsMinorParticipationEnabled);
        var minor = provider.GetRequiredService<IOptions<NeighborhoodTaskContractOptions>>().Value.MinorParticipation;
        Assert.Equal("Picking", Assert.Single(minor.PermittedWorkKinds));
        Assert.Equal("09:00-12:00", Assert.Single(minor.PermittedLocalTimeWindows));
    }

    [Fact]
    public async Task 미래검토후보가있어도미성년활성화설정은실제서버시작에서거부한다()
    {
        var values = PreparedConfiguration();
        values[Key("MinorParticipation:Enabled")] = "true";

        await Assert.ThrowsAsync<OptionsValidationException>(async () =>
        {
            await using var host = await PolicyHost.Create(values);
        });
    }

    [Theory]
    [InlineData("start-offset")]
    [InlineData("end-offset")]
    [InlineData("reversed-period")]
    [InlineData("negative-age")]
    [InlineData("adult-age")]
    [InlineData("unknown-work")]
    [InlineData("duplicate-work")]
    [InlineData("invalid-window")]
    [InlineData("empty-window")]
    public void 꺼진미래옵션도잘못된시행시각기간과나이범위를저장하지않는다(string fault)
    {
        var values = PreparedConfiguration();
        switch (fault)
        {
            case "start-offset": values[Key("MinorParticipation:EffectiveFromUtc")] = "2035-01-01T00:00:00+09:00"; break;
            case "end-offset": values[Key("MinorParticipation:EffectiveUntilUtc")] = "2036-01-01T00:00:00+09:00"; break;
            case "reversed-period": values[Key("MinorParticipation:EffectiveUntilUtc")] = "2034-01-01T00:00:00Z"; break;
            case "negative-age": values[Key("MinorParticipation:MinimumAgeYears")] = "-1"; break;
            case "adult-age": values[Key("MinorParticipation:MinimumAgeYears")] = "19"; break;
            case "unknown-work": values[Key("MinorParticipation:PermittedWorkKinds:0")] = "unrecognized-work"; break;
            case "duplicate-work": values[Key("MinorParticipation:PermittedWorkKinds:1")] = "Picking"; break;
            case "invalid-window": values[Key("MinorParticipation:PermittedLocalTimeWindows:0")] = "25:00-26:00"; break;
            case "empty-window": values[Key("MinorParticipation:PermittedLocalTimeWindows:0")] = "09:00-09:00"; break;
        }
        using var provider = Provider(values);

        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<NeighborhoodTaskContractOptions>>().Value);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(18)]
    public void 나이범위경계값은미래준비자료로만보관되고현재참여허가가되지않는다(int age)
    {
        var values = PreparedConfiguration();
        values[Key("MinorParticipation:MinimumAgeYears")] = age.ToString(System.Globalization.CultureInfo.InvariantCulture);
        using var provider = Provider(values);

        Assert.Equal(age, provider.GetRequiredService<IOptions<NeighborhoodTaskContractOptions>>().Value.MinorParticipation.MinimumAgeYears);
        Assert.False(provider.GetRequiredService<NeighborhoodTaskContractPolicyService>().GetPolicy().IsMinorParticipationEnabled);
    }

    [Fact]
    public async Task 실제정책조회HTTP는익명과계정없는인증을거부하고인증계정에도준비정보만반환한다()
    {
        await using var host = await PolicyHost.Create(PreparedConfiguration());
        const string route = "/api/v1/common/commerce/task-contract-policy";
        using (var anonymous = await host.Client.GetAsync(route))
            Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        host.Client.DefaultRequestHeaders.Add("X-Policy-Authenticated", "true");
        using (var withoutActor = await host.Client.GetAsync(route))
            Assert.Equal(HttpStatusCode.Unauthorized, withoutActor.StatusCode);

        host.Client.DefaultRequestHeaders.Add("X-Policy-Actor", "policy-reader");
        using var response = await host.Client.GetAsync(route);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        var body = await response.Content.ReadAsStringAsync();
        var policy = await response.Content.ReadFromJsonAsync<NeighborhoodTaskContractPolicyDto>();
        Assert.NotNull(policy);
        Assert.Equal(NeighborhoodTaskContractModels.PerTaskFreelance, policy.DefaultContractModel);
        Assert.Equal("NewPerTaskProposalPreparation", policy.ScopeCode);
        Assert.Equal(19, policy.OperationalMinimumAgeYears);
        Assert.False(policy.CanCreateContracts);
        Assert.False(policy.IsLegalClassificationDetermined);
        Assert.False(policy.IsMinorParticipationEnabled);
        Assert.True(policy.RequiresVerifiedLegalRepresentative);
        Assert.Equal("ReservedDisabled", policy.MinorParticipationStatusCode);
        Assert.DoesNotContain("private-future-policy-revision", body);
        Assert.DoesNotContain("private-legal-basis-reference", body);
        Assert.DoesNotContain("private-legal-review-reference", body);
        Assert.DoesNotContain("2035-01-01", body);
        Assert.DoesNotContain("09:00-12:00", body);
    }

    private static string Key(string value) => NeighborhoodTaskContractOptions.SectionName + ":" + value;

    private static Dictionary<string, string?> PreparedConfiguration() => new()
    {
        [Key("DefaultContractModel")] = NeighborhoodTaskContractModels.PerTaskFreelance,
        [Key("MinorParticipation:Enabled")] = "false",
        [Key("MinorParticipation:PolicyRevision")] = "private-future-policy-revision",
        [Key("MinorParticipation:LegalBasisReference")] = "private-legal-basis-reference",
        [Key("MinorParticipation:LegalReviewReference")] = "private-legal-review-reference",
        [Key("MinorParticipation:EffectiveFromUtc")] = "2035-01-01T00:00:00Z",
        [Key("MinorParticipation:EffectiveUntilUtc")] = "2036-01-01T00:00:00Z",
        [Key("MinorParticipation:MinimumAgeYears")] = "16",
        [Key("MinorParticipation:PermittedWorkKinds:0")] = "Picking",
        [Key("MinorParticipation:PermittedLocalTimeWindows:0")] = "09:00-12:00"
    };

    private static ServiceProvider Provider(Dictionary<string, string?> values)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var services = new ServiceCollection();
        services.AddNeighborhoodTaskContractPolicy(configuration);
        return services.BuildServiceProvider();
    }

    private sealed class PolicyHost(WebApplication app, HttpClient client) : IAsyncDisposable
    {
        public HttpClient Client { get; } = client;

        public static async Task<PolicyHost> Create(Dictionary<string, string?> values)
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
            builder.Logging.ClearProviders();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Configuration.AddInMemoryCollection(values);
            builder.Services.AddNeighborhoodTaskContractPolicy(builder.Configuration);
            builder.Services.AddControllers().ConfigureApplicationPartManager(manager =>
            {
                manager.ApplicationParts.Clear();
                manager.FeatureProviders.Clear();
                manager.FeatureProviders.Add(new OnlyCommerceController());
            }).AddControllersAsServices();
            // 이번 정책 액션은 기존 판매자·DB·신원기관 액션을 호출하지 않습니다.
            builder.Services.AddTransient(_ => new 통신판매보호Controller(null!, null!, null!));
            builder.Services.AddAuthentication("PolicyFixture")
                .AddScheme<AuthenticationSchemeOptions, PolicyAuthentication>("PolicyFixture", _ => { });
            builder.Services.AddAuthorization();
            var app = builder.Build();
            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapControllers();
            try
            {
                using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
                await app.StartAsync(deadline.Token);
                var addresses = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!;
                var client = new HttpClient(new HttpClientHandler { UseProxy = false, AllowAutoRedirect = false })
                {
                    BaseAddress = new Uri(Assert.Single(addresses.Addresses)),
                    Timeout = TimeSpan.FromSeconds(20)
                };
                return new PolicyHost(app, client);
            }
            catch
            {
                await app.DisposeAsync();
                throw;
            }
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await app.StopAsync(deadline.Token);
            await app.DisposeAsync();
        }
    }

    private sealed class OnlyCommerceController : IApplicationFeatureProvider<ControllerFeature>
    {
        public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature)
            => feature.Controllers.Add(typeof(통신판매보호Controller).GetTypeInfo());
    }

    private sealed class PolicyAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (Request.Headers["X-Policy-Authenticated"] != "true")
                return Task.FromResult(AuthenticateResult.NoResult());
            var actor = Request.Headers["X-Policy-Actor"].ToString();
            var claims = new List<Claim>();
            if (!string.IsNullOrWhiteSpace(actor)) claims.Add(new Claim(ClaimTypes.NameIdentifier, actor));
            var identity = new ClaimsIdentity(claims, Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
        }

        protected override Task HandleChallengeAsync(AuthenticationProperties properties)
        {
            Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        }
    }
}
