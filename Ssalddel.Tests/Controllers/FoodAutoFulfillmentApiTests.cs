using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using FluentResults;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ssalddel.Application.Food;
using Ssalddel.Contracts.Common.Fulfillment;
using Ssalddel.Contracts.Food;
using Ssalddel.Controllers.Food;
using Ssalddel.Controllers.Admin;
using Ssalddel.Services.Fulfillment;
using Ssalddel.Filters;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Options;
using 살뜰.Services.Versioning;
using 살뜰.도메인.음식;

namespace Ssalddel.Tests.Controllers;

/// <summary>실제 Controller/UseCase와 SQLite의 HTTP 권한·범위·설정 판본 시험입니다.
/// 인증과 Coordinator는 대역이며 실제 로그인·수행처 선정·기사 배송·운영 DB 검증은 아닙니다.</summary>
public sealed class FoodAutoFulfillmentApiTests
{
    [Fact]
    public async Task 실제HTTP의합의취소는_공식기능경계와당사자권한을거쳐_양측동의뒤취소영수증만반환한다()
    {
        await using var host = await Fixture.CreateAsync();
        var order = await host.Db.음식주문.SingleAsync();
        order.모의처리처수행 = false; order.상태 = 음식주문상태코드.주문확인; order.음식점수락시각Utc = DateTime.UtcNow;
        order.상태이력.Add(new() { 이전상태 = 음식주문상태코드.주문대기, 다음상태 = order.상태, 사유 = "음식점 주문 수락", 전이시각Utc = DateTime.UtcNow });
        await host.Db.SaveChangesAsync();
        const string path = "/api/v1/food-orders/order-a/cancellation-agreements";
        using var anonymous = await host.Client.GetAsync(path);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        host.Actor("member-a");
        var proposal = new 음식주문취소합의제안요청 { ClientRequestId = Guid.NewGuid(), ExpectedOrderRevision = 1,
            OrdererCostKrw = 0, RestaurantCostKrw = 0, CostBasisReference = "explicit-agreement" };
        using var proposed = await host.Client.PostAsJsonAsync(path, proposal);
        Assert.Equal(HttpStatusCode.OK, proposed.StatusCode); Assert.True(proposed.Headers.CacheControl?.NoStore);
        var value = (await proposed.Content.ReadFromJsonAsync<음식주문취소합의응답>())!;
        Assert.False(value.OrdererAgreed); Assert.False(value.RestaurantAgreed);
        Assert.DoesNotContain("private-address", await proposed.Content.ReadAsStringAsync());
        Assert.DoesNotContain("private-phone", await proposed.Content.ReadAsStringAsync());
        var itemPath = path + "/" + value.AgreementId;
        host.Actor("other-member");
        using var other = await host.Client.GetAsync(itemPath);
        Assert.Equal(HttpStatusCode.NotFound, other.StatusCode); Assert.True(other.Headers.CacheControl?.NoStore);
        host.Actor("member-a");
        using var buyerConsent = await host.Client.PostAsJsonAsync(itemPath + "/consents", new 음식주문취소합의변경요청
        { ClientRequestId = Guid.NewGuid(), ExpectedAgreementRevision = value.Revision, ExpectedOrderRevision = 1, Granted = true });
        Assert.Equal(HttpStatusCode.OK, buyerConsent.StatusCode);
        value = (await buyerConsent.Content.ReadFromJsonAsync<음식주문취소합의응답>())!;
        host.Actor("merchant-a", restaurantId: 101);
        using var sellerConsent = await host.Client.PostAsJsonAsync(itemPath + "/consents", new 음식주문취소합의변경요청
        { ClientRequestId = Guid.NewGuid(), ExpectedAgreementRevision = value.Revision, ExpectedOrderRevision = 1, Granted = true });
        Assert.Equal(HttpStatusCode.OK, sellerConsent.StatusCode);
        value = (await sellerConsent.Content.ReadFromJsonAsync<음식주문취소합의응답>())!;
        var apply = new 음식주문취소합의변경요청 { ClientRequestId = Guid.NewGuid(), ExpectedAgreementRevision = value.Revision,
            ExpectedOrderRevision = 1, Granted = true };
        using var applied = await host.Client.PostAsJsonAsync(itemPath + "/apply", apply);
        Assert.Equal(HttpStatusCode.OK, applied.StatusCode);
        Assert.Equal("Cancelled", (await applied.Content.ReadFromJsonAsync<음식주문취소합의응답>())!.StateCode);
        using var checkedReceipt = await host.Client.GetAsync(itemPath + "?clientRequestId=" + apply.ClientRequestId.ToString("D"));
        Assert.Equal(HttpStatusCode.OK, checkedReceipt.StatusCode);
        var receipt = (await checkedReceipt.Content.ReadFromJsonAsync<음식주문취소합의응답>())!;
        Assert.Equal(apply.ClientRequestId, receipt.ConfirmedClientRequestId); Assert.Equal("PendingExternalSettlement", receipt.FinanceStatusCode);
        Assert.Empty(await host.Db.음식주문기사정산.ToListAsync()); Assert.Empty(await host.Db.결제.ToListAsync());
        host.FeatureFlags.Active = false;
        using var disabled = await host.Client.GetAsync(itemPath);
        Assert.Equal(HttpStatusCode.NotFound, disabled.StatusCode);
        Assert.DoesNotContain("FeatureBoundaryUnclassified", await disabled.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task 모의사건HTTP는_관리자와Simulation관문을모두요구하며_일반회원은호출하지못한다()
    {
        await using var host = await Fixture.CreateAsync();
        const string path = "/api/v1/admin/fulfillment-simulation/attempt-a/evidence";
        var request = new FulfillmentMockEvidenceRequest { ClientRequestId = Guid.NewGuid(), ExpectedRevision = 1,
            EvidenceKind = FulfillmentEvidenceKinds.DriverAssigned, ExternalDriverReference = "mock-driver" };
        host.Actor("member-a");
        using var denied = await host.Client.PostAsJsonAsync(path, request);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Equal(0, host.Coordinator.EvidenceWrites);
        host.Actor("fixture-admin");
        host.Coordinator.IsEnabled = false;
        using var disabled = await host.Client.PostAsJsonAsync(path, request);
        Assert.Equal(HttpStatusCode.Forbidden, disabled.StatusCode);
        Assert.Equal("FulfillmentRoutingDisabled", await ErrorCode(disabled));
        Assert.Equal(0, host.Coordinator.EvidenceWrites);
        host.Coordinator.IsEnabled = true;
        using var enabled = await host.Client.PostAsJsonAsync(path, request);
        Assert.Equal(HttpStatusCode.OK, enabled.StatusCode);
        Assert.True(enabled.Headers.CacheControl?.Private);
        Assert.True(enabled.Headers.CacheControl?.NoStore);
        Assert.Equal(1, host.Coordinator.EvidenceWrites);
    }

    [Fact]
    public async Task 실제HTTP는_익명과다른주문자를차단하고_본인수행상태만반환한다()
    {
        await using var host = await Fixture.CreateAsync();
        var path = "/api/v1/food-orders/order-a/fulfillment";
        using var anonymous = await host.Client.GetAsync(path);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(0, host.Coordinator.StateReads);

        host.Actor("other-member");
        using var other = await host.Client.GetAsync(path);
        Assert.Equal(HttpStatusCode.NotFound, other.StatusCode);
        Assert.Equal("FoodOrderNotFound", await ErrorCode(other));
        Assert.Equal(0, host.Coordinator.StateReads);
        Assert.DoesNotContain("private-address", await other.Content.ReadAsStringAsync());

        host.Actor("member-a");
        using var own = await host.Client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        Assert.True(own.Headers.CacheControl?.Private);
        Assert.True(own.Headers.CacheControl?.NoStore);
        var snapshot = await own.Content.ReadFromJsonAsync<AutoFulfillmentSnapshot>();
        Assert.Equal(FulfillmentSourceKinds.FoodOrder, snapshot!.SourceKind);
        Assert.Equal("order-a", snapshot.SourceId);
        Assert.Equal(1000m, snapshot.RestaurantContributionKrw);
        Assert.Equal(2000m, snapshot.UserChargeKrw);
        Assert.False(snapshot.IsOperationalSettlementEligible);
        Assert.Equal(1, host.Coordinator.StateReads);
        Assert.Empty(await host.Db.결제.ToListAsync());
        Assert.Empty(await host.Db.운송원장.ToListAsync());
    }

    [Fact]
    public async Task 식당정액설정은_인증된식당범위에서만저장되고_재시도와오래된판본을구분한다()
    {
        await using var host = await Fixture.CreateAsync();
        const string path = "/api/v1/food-orders/restaurant/fulfillment-settings";
        var request = new RestaurantFulfillmentSupportRequest
        { ClientRequestId = Guid.NewGuid(), ExpectedRevision = 0, FixedContributionKrw = 1000, Enabled = true, Agreed = true,
            CostSharingPolicy = new() { Mode = "FixedContribution", FixedAmountKrw = 1000 } };
        host.Actor("member-a");
        using var customer = await host.Client.PutAsJsonAsync(path, request);
        Assert.Equal(HttpStatusCode.Forbidden, customer.StatusCode);
        Assert.Empty(await host.Db.RestaurantFulfillmentSupports.ToListAsync());

        host.Actor("merchant-a", restaurantId: 101);
        using var saved = await host.Client.PutAsJsonAsync(path, request);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        Assert.True(saved.Headers.CacheControl?.NoStore);
        var settings = await saved.Content.ReadFromJsonAsync<RestaurantFulfillmentSupportResponse>();
        Assert.Equal(101, settings!.RestaurantId);
        Assert.Equal(1, settings.Revision);
        Assert.Equal(1000m, settings.FixedContributionKrw);
        using var replay = await host.Client.PutAsJsonAsync(path, request);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal(1, (await replay.Content.ReadFromJsonAsync<RestaurantFulfillmentSupportResponse>())!.Revision);

        using var stale = await host.Client.PutAsJsonAsync(path, new RestaurantFulfillmentSupportRequest
        { ClientRequestId = Guid.NewGuid(), ExpectedRevision = 0, FixedContributionKrw = 2000, Enabled = true, Agreed = true,
            CostSharingPolicy = new() { Mode = "FixedContribution", FixedAmountKrw = 2000 } });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal("RestaurantSupportRevisionConflict", await ErrorCode(stale));

        host.Actor("merchant-b", restaurantId: 202);
        using var scoped = await host.Client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, scoped.StatusCode);
        Assert.Equal(202, (await scoped.Content.ReadFromJsonAsync<RestaurantFulfillmentSupportResponse>())!.RestaurantId);
        Assert.Equal(1000m, (await host.Db.RestaurantFulfillmentSupports.AsNoTracking().SingleAsync()).FixedContributionKrw);
        Assert.Equal("merchant-a", (await host.Db.RestaurantFulfillmentSupports.AsNoTracking().SingleAsync()).ActorId);
    }

    [Fact]
    public async Task 음식조건조회는_메뉴나고객주소를먼저강요하지않고_공개식당지원조건만반환한다()
    {
        await using var host = await Fixture.CreateAsync();
        host.Actor("member-a");
        using var response = await host.Client.PostAsJsonAsync("/api/v1/food-orders/fulfillment-quote", new 음식주문등록요청 { 음식점Id = 101 });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var quote = await response.Content.ReadFromJsonAsync<FoodAutoFulfillmentQuoteResponse>();
        Assert.Equal(101, quote!.RestaurantId);
        Assert.False(quote.Enabled); // 정액 지원 조건이 아직 저장되지 않은 식당입니다.
        Assert.Equal(0m, quote.RestaurantSupportKrw);
        Assert.Equal(0, host.Coordinator.StateReads);
        Assert.DoesNotContain("private-address", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("CustomerPays")]
    [InlineData("FixedContribution")]
    [InlineData("PercentageShare")]
    [InlineData("RestaurantPays")]
    public async Task 네비용정책은_실제설정HTTP저장과재조회_주문조건JSON에서같은판본으로전달된다(string mode)
    {
        await using var host = await Fixture.CreateAsync();
        const string path = "/api/v1/food-orders/restaurant/fulfillment-settings";
        var policy = Policy(mode);
        var request = new RestaurantFulfillmentSupportRequest { ClientRequestId = Guid.NewGuid(), ExpectedRevision = 0,
            Enabled = true, Agreed = true, CostSharingPolicy = policy, FixedContributionKrw = policy.FixedAmountKrw ?? 0 };
        host.Actor("merchant-a", restaurantId: 101);
        using (var lostResponse = await host.Client.PutAsJsonAsync(path, request))
            Assert.Equal(HttpStatusCode.OK, lostResponse.StatusCode);
        using var recovered = await host.Client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, recovered.StatusCode); Assert.True(recovered.Headers.CacheControl?.NoStore);
        var restored = await recovered.Content.ReadFromJsonAsync<RestaurantFulfillmentSupportResponse>();
        Assert.Equal(mode, restored!.CostSharingPolicy!.Mode); Assert.Equal(1, restored.Revision);
        Assert.Equal(policy.RestaurantSharePercent, restored.CostSharingPolicy.RestaurantSharePercent);
        Assert.Equal(policy.FixedAmountKrw, restored.CostSharingPolicy.FixedAmountKrw);
        using var replay = await host.Client.PutAsJsonAsync(path, request);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal(1, (await replay.Content.ReadFromJsonAsync<RestaurantFulfillmentSupportResponse>())!.Revision);
        if (mode == "PercentageShare")
        {
            request.CostSharingPolicy!.RestaurantSharePercent = 33.330m;
            using var equivalentReplay = await host.Client.PutAsJsonAsync(path, request);
            Assert.Equal(HttpStatusCode.OK, equivalentReplay.StatusCode);
            Assert.Equal(1, (await equivalentReplay.Content.ReadFromJsonAsync<RestaurantFulfillmentSupportResponse>())!.Revision);
        }
        Assert.Single(await host.Db.RestaurantFulfillmentSupports.ToListAsync());
        host.Actor("member-a");
        using var quoteResponse = await host.Client.PostAsJsonAsync("/api/v1/food-orders/fulfillment-quote", new 음식주문등록요청 { 음식점Id = 101 });
        Assert.Equal(HttpStatusCode.OK, quoteResponse.StatusCode);
        var quote = await quoteResponse.Content.ReadFromJsonAsync<FoodAutoFulfillmentQuoteResponse>();
        Assert.True(quote!.Enabled); Assert.Equal(mode, quote.CostSharingPolicy!.Mode);
        Assert.Equal(restored.Revision, quote.RestaurantSupportRevision);
        Assert.DoesNotContain("private-address", await quoteResponse.Content.ReadAsStringAsync());
        Assert.Equal(0, host.Coordinator.StateReads); Assert.Empty(await host.Db.결제.ToListAsync());
    }

    [Fact]
    public async Task 같은설정요청ID의다른정책과_구형정액덮어쓰기는_이미저장된비율정책을바꾸지않는다()
    {
        await using var host = await Fixture.CreateAsync();
        const string path = "/api/v1/food-orders/restaurant/fulfillment-settings";
        host.Actor("merchant-a", restaurantId: 101);
        var id = Guid.NewGuid();
        using var saved = await host.Client.PutAsJsonAsync(path, new RestaurantFulfillmentSupportRequest
        { ClientRequestId = id, ExpectedRevision = 0, Enabled = true, Agreed = true, CostSharingPolicy = Policy("PercentageShare") });
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        using var conflict = await host.Client.PutAsJsonAsync(path, new RestaurantFulfillmentSupportRequest
        { ClientRequestId = id, ExpectedRevision = 0, Enabled = true, Agreed = true, CostSharingPolicy = Policy("RestaurantPays") });
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode); Assert.Equal("IdempotencyConflict", await ErrorCode(conflict));
        using var oldClient = await host.Client.PutAsJsonAsync(path, new RestaurantFulfillmentSupportRequest
        { ClientRequestId = Guid.NewGuid(), ExpectedRevision = 1, FixedContributionKrw = 1000, Enabled = true, Agreed = true });
        Assert.Equal(HttpStatusCode.Conflict, oldClient.StatusCode);
        Assert.Equal("CostSharingPolicyUpgradeRequired", await ErrorCode(oldClient));
        using var after = await host.Client.GetAsync(path);
        var unchanged = await after.Content.ReadFromJsonAsync<RestaurantFulfillmentSupportResponse>();
        Assert.Equal(1, unchanged!.Revision); Assert.Equal("PercentageShare", unchanged.CostSharingPolicy!.Mode);
        Assert.Equal(33.33m, unchanged.CostSharingPolicy.RestaurantSharePercent);
    }

    [Fact]
    public async Task 고객전액정책과자동기능사용여부는_지원0원이나None모드로합쳐지지않는다()
    {
        await using var host = await Fixture.CreateAsync();
        const string path = "/api/v1/food-orders/restaurant/fulfillment-settings";
        host.Actor("merchant-a", restaurantId: 101);
        using var enabled = await host.Client.PutAsJsonAsync(path, new RestaurantFulfillmentSupportRequest
        { ClientRequestId = Guid.NewGuid(), ExpectedRevision = 0, Enabled = true, Agreed = true, CostSharingPolicy = Policy("CustomerPays") });
        Assert.Equal(HttpStatusCode.OK, enabled.StatusCode);
        using var enabledQuote = await host.Client.PostAsJsonAsync("/api/v1/food-orders/fulfillment-quote", new 음식주문등록요청 { 음식점Id = 101 });
        var before = await enabledQuote.Content.ReadFromJsonAsync<FoodAutoFulfillmentQuoteResponse>();
        Assert.True(before!.Enabled); Assert.Equal("CustomerPays", before.CostSharingPolicy!.Mode);
        Assert.Equal(0m, before.RestaurantSupportKrw);
        using var disabled = await host.Client.PutAsJsonAsync(path, new RestaurantFulfillmentSupportRequest
        { ClientRequestId = Guid.NewGuid(), ExpectedRevision = 1, Enabled = false, Agreed = true, CostSharingPolicy = Policy("CustomerPays") });
        Assert.Equal(HttpStatusCode.OK, disabled.StatusCode);
        using var disabledQuote = await host.Client.PostAsJsonAsync("/api/v1/food-orders/fulfillment-quote", new 음식주문등록요청 { 음식점Id = 101 });
        var after = await disabledQuote.Content.ReadFromJsonAsync<FoodAutoFulfillmentQuoteResponse>();
        Assert.False(after!.Enabled); Assert.Equal("CustomerPays", after.CostSharingPolicy!.Mode);
        Assert.Equal(2, after.RestaurantSupportRevision);
    }

    [Theory]
    [InlineData("None", null)]
    [InlineData("PercentageShare", null)]
    [InlineData("PercentageShare", "100.01")]
    [InlineData("PercentageShare", "33.333")]
    public async Task 미지원방식과불명확한비율은_HTTP입력오류이며설정을저장하지않는다(string mode, string? percent)
    {
        await using var host = await Fixture.CreateAsync();
        host.Actor("merchant-a", restaurantId: 101);
        using var response = await host.Client.PutAsJsonAsync("/api/v1/food-orders/restaurant/fulfillment-settings",
            new RestaurantFulfillmentSupportRequest { ClientRequestId = Guid.NewGuid(), ExpectedRevision = 0, Enabled = true, Agreed = true,
                CostSharingPolicy = new() { Mode = mode, RestaurantSharePercent = percent is null ? null : decimal.Parse(percent, System.Globalization.CultureInfo.InvariantCulture) } });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode); Assert.Equal("InvalidCostSharingPolicy", await ErrorCode(response));
        Assert.Empty(await host.Db.RestaurantFulfillmentSupports.ToListAsync());
    }

    private static DeliveryCostSharingPolicy Policy(string mode) => new()
    { Mode = mode, FixedAmountKrw = mode == "FixedContribution" ? 1000m : null,
        RestaurantSharePercent = mode == "PercentageShare" ? 33.33m : null };

    private static async Task<string?> ErrorCode(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("errorCode").GetString();
    }

    private sealed class Fixture(SqliteConnection connection, SsalddelContext db, WebApplication app,
        HttpClient client, Coordinator coordinator, Flags flags) : IAsyncDisposable
    {
        public SsalddelContext Db { get; } = db;
        public HttpClient Client { get; } = client;
        public Coordinator Coordinator { get; } = coordinator;
        public Flags FeatureFlags { get; } = flags;
        public void Actor(string actorId, long? restaurantId = null)
        {
            Client.DefaultRequestHeaders.Remove("X-Fixture-Actor");
            Client.DefaultRequestHeaders.Remove("X-Fixture-Restaurant");
            Client.DefaultRequestHeaders.Add("X-Fixture-Actor", actorId);
            if (restaurantId.HasValue) Client.DefaultRequestHeaders.Add("X-Fixture-Restaurant", restaurantId.Value.ToString());
        }
        public static async Task<Fixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var db = new SsalddelContext(new DbContextOptionsBuilder<SsalddelContext>().UseSqlite(connection).Options, new Encryption());
            await db.Database.EnsureCreatedAsync();
            db.음식점공개프로필.AddRange(
                new 음식점공개프로필 { Id = 101, 상호명 = "시험 식당 A", 공개여부 = true },
                new 음식점공개프로필 { Id = 202, 상호명 = "시험 식당 B", 공개여부 = true });
            db.음식주문.Add(new 음식주문 { 주문번호 = "order-a", 음식점Id = 101, 주문자UserId = "member-a",
                수령지주소 = "private-address", 수령인연락처 = "private-phone", 모의처리처수행 = true });
            await db.SaveChangesAsync();
            var coordinator = new Coordinator();
            var useCase = new 음식자동수행UseCase(db, coordinator,
                new SsalddelExecutionModePolicy(Options.Create(new SsalddelExecutionOptions { Mode = SsalddelExecutionMode.Simulation })), TimeProvider.System);
            var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions
            { EnvironmentName = "Testing", ApplicationName = typeof(음식주문Controller).Assembly.GetName().Name, ContentRootPath = AppContext.BaseDirectory });
            builder.WebHost.UseKestrel(options => options.Listen(IPAddress.Loopback, 0));
            builder.Services.AddLogging(); builder.Services.AddRouting();
            var flags = new Flags(); builder.Services.AddSingleton<IVersionFeatureFlagService>(flags);
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddControllers(options => options.Filters.Add<SsalddelApiFeatureBoundaryFilter>()).ConfigureApplicationPartManager(manager =>
            { manager.ApplicationParts.Clear(); manager.FeatureProviders.Clear(); manager.FeatureProviders.Add(new OnlyFoodController()); }).AddControllersAsServices();
            builder.Services.AddTransient(provider => new 음식주문Controller(null!, null!, null!, autoFulfillment: useCase,
                cancellationAgreements: new 음식주문취소합의UseCase(db, provider.GetRequiredService<IHttpContextAccessor>(), TimeProvider.System)));
            builder.Services.AddTransient(_ => new 자동수행모의Controller(coordinator));
            builder.Services.AddAuthentication("Fixture").AddScheme<AuthenticationSchemeOptions, FixtureAuthentication>("Fixture", _ => { });
            builder.Services.AddAuthorization(options =>
            {
                options.AddPolicy("음식점운영자전용", policy => policy.RequireAuthenticatedUser().RequireClaim("fixture-role", "restaurant"));
                options.AddPolicy("서버관리자전용", policy => policy.RequireAuthenticatedUser().RequireClaim("fixture-role", "admin"));
            });
            var app = builder.Build();
            app.UseRouting(); app.UseAuthentication(); app.UseAuthorization(); app.MapControllers();
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            await app.StartAsync(deadline.Token);
            var addresses = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!;
            var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseProxy = false })
            { BaseAddress = new Uri(Assert.Single(addresses.Addresses)), Timeout = TimeSpan.FromSeconds(20) };
            return new(connection, db, app, client, coordinator, flags);
        }
        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await app.StopAsync(deadline.Token); await app.DisposeAsync();
            await Db.DisposeAsync(); await connection.DisposeAsync();
        }
    }
    private sealed class OnlyFoodController : IApplicationFeatureProvider<ControllerFeature>
    {
        public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature)
        { feature.Controllers.Add(typeof(음식주문Controller).GetTypeInfo()); feature.Controllers.Add(typeof(자동수행모의Controller).GetTypeInfo()); }
    }
    private sealed class Flags : IVersionFeatureFlagService
    { public bool Active { get; set; } = true; public bool IsEnabled(string featureKey) => Active; public IReadOnlyDictionary<string, bool> GetAll() => new Dictionary<string, bool>(); }
    private sealed class Encryption : IPersonalDataEncryptionService
    { public string? Protect(string? value) => value; public string? Unprotect(string? value) => value; }
    public sealed class FixtureAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var actor = Request.Headers["X-Fixture-Actor"].ToString();
            if (string.IsNullOrWhiteSpace(actor)) return Task.FromResult(AuthenticateResult.NoResult());
            var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, actor) };
            if (actor == "fixture-admin") claims.Add(new("fixture-role", "admin"));
            var restaurant = Request.Headers["X-Fixture-Restaurant"].ToString();
            if (!string.IsNullOrEmpty(restaurant)) { claims.Add(new(음식점접근ClaimTypes.음식점Id, restaurant)); claims.Add(new("fixture-role", "restaurant")); claims.Add(new(ClaimTypes.Role, 살뜰.Data.역할명.음식점)); }
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name)), Scheme.Name)));
        }
        protected override Task HandleChallengeAsync(AuthenticationProperties properties) { Response.StatusCode = 401; return Task.CompletedTask; }
        protected override Task HandleForbiddenAsync(AuthenticationProperties properties) { Response.StatusCode = 403; return Task.CompletedTask; }
    }
    private sealed class Coordinator : I자동수행Coordinator
    {
        public bool IsEnabled { get; set; } = true;
        public bool Enabled => IsEnabled;
        public int EvidenceWrites { get; private set; }
        public int StateReads { get; private set; }
        public Task<AutoFulfillmentSnapshot?> 상태Async(FulfillmentSourceKey key, CancellationToken ct = default)
        {
            StateReads++;
            return Task.FromResult<AutoFulfillmentSnapshot?>(new() { SourceKind = key.SourceKind, SourceId = key.SourceId,
                StateCode = AutoFulfillmentStates.Accepted, TotalCostKrw = 3000, UserChargeKrw = 2000, RestaurantContributionKrw = 1000 });
        }
        public Task<Result<AutoFulfillmentSnapshot>> 사전동의저장Async(FulfillmentSourceKey key, string ownerId, AutoFulfillmentConsent consent, CancellationToken ct = default) => throw new NotSupportedException();
        public Task 예약Async(FulfillmentSourceKey key, Guid operationId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Result<AutoFulfillmentSnapshot>> 선정실행Async(FulfillmentSourceKey key, Guid operationId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Result<AutoFulfillmentSnapshot>> 재확인Async(FulfillmentSourceKey key, AutoFulfillmentActionRequest request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Result<AutoFulfillmentSnapshot>> 취소Async(FulfillmentSourceKey key, AutoFulfillmentActionRequest request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> 내부수행허용Async(FulfillmentSourceKey key, string? attemptId = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Result<AutoFulfillmentSnapshot>> 모의증거기록Async(string attemptId, FulfillmentMockEvidenceRequest request, CancellationToken ct = default)
        { EvidenceWrites++; return Task.FromResult(Result.Ok(new AutoFulfillmentSnapshot { SourceKind = FulfillmentSourceKinds.FoodOrder, SourceId = "order-a", AttemptId = attemptId })); }
        public Task<int> 대기처리Async(int take = 100, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
