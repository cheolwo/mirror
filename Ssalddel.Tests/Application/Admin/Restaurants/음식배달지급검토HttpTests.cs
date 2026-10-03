using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ssalddel.Application.Admin.Restaurants;
using Ssalddel.Contracts.Common.Finance;
using Ssalddel.Controllers.Admin;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Options;

namespace Ssalddel.Tests.Application.Admin.Restaurants;

// 격리된 루프백 HTTP·시험 인증·InMemory DB다. 제품 로그인이나 실제 송금 검증은 아니다.
public sealed class 음식배달지급검토HttpTests
{
    [Theory]
    [InlineData("payout-preview", null, 401)]
    [InlineData("payout-preview", "member", 403)]
    [InlineData("payout-preview", "admin", 200)]
    [InlineData("settlement-preview", null, 401)]
    [InlineData("settlement-preview", "member", 403)]
    [InlineData("settlement-preview", "admin", 200)]
    public async Task 관리자만_검토계산에접근하고_정책과원장에는_저장하지않는다(string endpoint, string? actor, int status)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var options = new DbContextOptionsBuilder<SsalddelContext>().UseInMemoryDatabase($"payout-preview-{Guid.NewGuid():N}").Options;
        await using var db = new SsalddelContext(options, new PassThroughEncryptionService());
        var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Testing", ApplicationName = typeof(음식배달요금정책Controller).Assembly.GetName().Name,
            ContentRootPath = AppContext.BaseDirectory
        });
        builder.WebHost.UseKestrel(options => options.Listen(IPAddress.Loopback, 0));
        builder.Services.AddLogging();
        builder.Services.AddRouting();
        builder.Services.AddSingleton<I음식운영관리UseCase>(new 음식운영관리UseCase(db, TimeProvider.System,
            new SsalddelExecutionModePolicy(Options.Create(new SsalddelExecutionOptions { Mode = SsalddelExecutionMode.Simulation }))));
        builder.Services.AddScoped<I음식배달지급검토UseCase, 음식배달지급검토UseCase>();
        builder.Services.AddControllers().ConfigureApplicationPartManager(manager =>
        {
            manager.ApplicationParts.Clear(); manager.FeatureProviders.Clear();
            manager.FeatureProviders.Add(new OnlyPricingController());
        });
        builder.Services.AddAuthentication("Fixture").AddScheme<AuthenticationSchemeOptions, FixtureAuthentication>("Fixture", _ => { });
        builder.Services.AddAuthorization(options => options.AddPolicy("서버관리자전용", policy => policy.RequireRole("admin")));
        await using var app = builder.Build();
        app.UseRouting(); app.UseAuthentication(); app.UseAuthorization(); app.MapControllers();
        await app.StartAsync(deadline.Token);
        try
        {
            var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            using var client = new HttpClient { BaseAddress = new Uri(address) };
            if (actor is not null) client.DefaultRequestHeaders.Add("X-Fixture-Actor", actor);
            var start = new DateOnly(2026, 10, 1);
            var end = start.AddDays(6);
            object body = endpoint == "payout-preview"
                ? new FoodDeliveryPayoutPricingReviewRequest(new("fixture.r1", start, end, 700m, 700m, 100, 80m, "Floor"),
                    [new("delivery-1", start, 2.914m, "AppDisplayed", "Morning", 700m, 300m, 0m)])
                : new FoodDeliverySettlementReviewRequest(start, end, 100000m, 0m, 0m, 0m,
                    new() { Revision = "fixture.r1", EffectiveFrom = start, EffectiveThrough = end });
            using var response = await client.PostAsJsonAsync($"api/v1/admin/food-delivery-pricing-policy/{endpoint}", body, deadline.Token);
            Assert.Equal(status, (int)response.StatusCode);
            if (status == 200)
            {
                if (endpoint == "payout-preview")
                {
                    var result = await response.Content.ReadFromJsonAsync<FoodDeliveryPayoutPricingReviewResponse>(deadline.Token);
                    Assert.Equal(4720m, result!.TotalGrossPayoutKrw);
                }
                else
                {
                    var result = await response.Content.ReadFromJsonAsync<FoodDeliverySettlementReviewResponse>(deadline.Token);
                    Assert.Null(result!.NetPayoutKrw);
                    Assert.Equal("NeedsReview", result.StatusCode);
                }
                var invalid = new FoodDeliveryPayoutPricingReviewRequest(new("fixture.r1", start, end, 700m, 700m, 100, 80m, "Floor"),
                    [new("delivery-1", start, null, "AppDisplayed", "Morning", 700m, 300m, 0m)]);
                using var rejected = await client.PostAsJsonAsync("api/v1/admin/food-delivery-pricing-policy/payout-preview", invalid, deadline.Token);
                Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
                // 숫자 누락을 기본 0원으로 받아들이지 않는다. 확인된 0은 명시해야 한다.
                using var missing = await client.PostAsJsonAsync("api/v1/admin/food-delivery-pricing-policy/payout-preview", new
                {
                    Policy = invalid.Policy,
                    Deliveries = new[] { new { DeliveryKey = "missing-fees", ServiceDate = start, DistanceKm = 0m,
                        DistanceBasisCode = "AppDisplayed", TimeBandCode = "Morning" } }
                }, deadline.Token);
                Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
                using var missingMission = await client.PostAsJsonAsync("api/v1/admin/food-delivery-pricing-policy/settlement-preview", new
                {
                    PeriodStart = start, PeriodEnd = end, DeliveryGrossKrw = 100000m,
                    PeriodPromotionGrossKrw = 0m, MilestoneBonusGrossKrw = 0m,
                    Policy = new FoodDeliverySettlementReviewPolicy { Revision = "fixture.r1", EffectiveFrom = start, EffectiveThrough = end }
                }, deadline.Token);
                Assert.Equal(HttpStatusCode.BadRequest, missingMission.StatusCode);
            }
            Assert.Empty(db.ChangeTracker.Entries());
            Assert.Equal(0, await db.음식운영정책.CountAsync(deadline.Token));
        }
        finally
        {
            using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await app.StopAsync(stop.Token);
        }
    }

    private sealed class OnlyPricingController : IApplicationFeatureProvider<ControllerFeature>
    {
        public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature)
            => feature.Controllers.Add(typeof(음식배달요금정책Controller).GetTypeInfo());
    }

    public sealed class FixtureAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger, UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var actor = Request.Headers["X-Fixture-Actor"].ToString();
            if (string.IsNullOrEmpty(actor)) return Task.FromResult(AuthenticateResult.NoResult());
            var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "synthetic-actor"), new Claim(ClaimTypes.Role, actor)], Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
        }
    }

    private sealed class PassThroughEncryptionService : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
}
