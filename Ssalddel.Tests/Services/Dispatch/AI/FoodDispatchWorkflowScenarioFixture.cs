using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Application.Driver.Food;
using Ssalddel.Application.Driver.Notification;
using Ssalddel.Application.Driver.Work;
using Ssalddel.Application.Food;
using Ssalddel.Application.Food.Commands;
using Ssalddel.Application.Food.Handlers;
using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Contracts.Common.Drivers;
using Ssalddel.Contracts.Common.Transport;
using Ssalddel.Contracts.Food;
using Ssalddel.Controllers.Driver.Food;
using Ssalddel.Controllers.Driver.Notification07;
using Ssalddel.Controllers.Driver.Settings05;
using Ssalddel.Controllers.Food;
using Ssalddel.Infrastructure.Storage.Memory;
using Ssalddel.Services.Community;
using Ssalddel.Services.Food;
using Ssalddel.Services.Notifications;
using Ssalddel.Services.Outbox;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.DeliveryZones;
using 살뜰.Services.Dispatch.AI;
using 살뜰.Services.Dispatch.Common;
using 살뜰.Services.Dispatch.Coordination;
using 살뜰.Services.Dispatch.Engine;
using 살뜰.Services.Dispatch.Notification;
using 살뜰.Services.Dispatch.Queue;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.Services.Notifications;
using 살뜰.Services.Options;
using 살뜰.Services.Storage.Local;
using 살뜰.Services.Transport;
using 살뜰.Services.Versioning;
using 살뜰.도메인.공통;
using 살뜰.도메인.기사;
using 살뜰.도메인.배차;
using 살뜰.도메인.설정;
using 살뜰.도메인.운송;
using 살뜰.도메인.음식;

namespace Ssalddel.Tests.Services.Dispatch.AI;

/// <summary>
/// r49의 합성 주문/기사만 사용하는 서버 수직 시험 fixture. 제품 host/DB/FCM/지도/GPS를 사용하지 않는다.
/// 실제 Controller, CommandHandler, SQLite 저장, 요청/알림 Outbox, 후보 정책, Ollama adapter와 기사 업무를 연결한다.
/// 외부 좌표/도로/기상/Mongo/정산 전송과 메뉴 카탈로그 검증은 명시적인 시험 경계다.
/// </summary>
public sealed partial class FoodDispatchWorkflowScenarioFixture : IAsyncDisposable
{
    public const string OrdererId = "scenario-orderer";
    public const string RestaurantUserId = "scenario-restaurant";
    public const long RestaurantId = 491;
    private decimal PickupLatitude => _regionalSeed?.PickupLatitude ?? 37.512m;
    private decimal PickupLongitude => _regionalSeed?.PickupLongitude ?? 127.012m;
    private decimal DropoffLatitude => _regionalSeed?.SyntheticDropoffLatitude ?? 37.514m;
    private decimal DropoffLongitude => _regionalSeed?.SyntheticDropoffLongitude ?? 127.014m;
    private readonly FoodDispatchRegionalSeed? _regionalSeed;
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<SsalddelContext> _dbOptions;
    private readonly HttpClient _modelHttp;
    private readonly CountingModelHandler _modelCounter;
    private readonly MutableDriverStates _states = new();
    private readonly InMemoryDriverLocationStore _locations = new();
    private readonly InMemory음식배달권실행공간Store _spaces = new();
    private readonly InMemory국내화물배달권실행공간Store _cargoSpaces = new();
    private readonly InMemoryDriverRejectedRequestStore _rejected = new();
    private readonly InMemory운영배차판정ProjectionStore _availabilityProjection = new();
    private readonly RecordingPublisher _publisher = new();
    private readonly RecordingPush _push = new();
    private readonly MemoryTokens _legacyTokens = new();
    private readonly MemoryNotificationSettings _notificationSettings = new();
    private readonly NoOpRouteService _routes = new();
    private readonly NoOpFoodLedgerOutbox _foodLedger = new();
    private readonly NoOpTransportSync _mongo = new();
    private readonly NoOpRestaurantNotification _restaurantNotifications = new();
    private readonly Flags _flags = new();
    private readonly List<string> _steps = [];
    private WebApplication _app = null!;
    private HttpClient _client = null!;
    private readonly 음식배달수요참여Options _demandOptions;
    private readonly FoodDispatchOllamaOptions _aiOptions = new() { Enabled = true, TimeoutSeconds = 90 };
    private readonly 배차큐정책Options _queueOptions = new()
    {
        음식배달동일권최소후보기사수 = 2, 음식배달후보검색반경Km = 5m,
        음식배달후보최대조회수 = 20, 음식배달평균이동속도KmH = 20m
    };

    private FoodDispatchWorkflowScenarioFixture(string scenarioId, SqliteConnection connection,
        DbContextOptions<SsalddelContext> dbOptions, HttpClient modelHttp, FoodDispatchRegionalSeed? regionalSeed)
    {
        ScenarioId = scenarioId; _connection = connection; _dbOptions = dbOptions; _regionalSeed = regionalSeed;
        _modelCounter = new CountingModelHandler(modelHttp);
        _modelHttp = new HttpClient(_modelCounter) { Timeout = Timeout.InfiniteTimeSpan };
        _demandOptions = new() { Enabled = scenarioId == "demand-capacity" };
    }

    public string ScenarioId { get; }
    public Uri BaseAddress => _client.BaseAddress!;
    public string DriverId { get; private set; } = "scenario-older";
    public string OfferId { get; private set; } = string.Empty;
    public string OrderNo => OfferId;
    public string AccessToken => $"fixture-driver:{DriverId}";
    public IReadOnlyList<FcmPushMessage> PushMessages => _push.Messages;
    public IReadOnlyList<string> Steps => _steps;
    public FoodDispatchWorkflowRecommendationEvidence? RecommendationEvidence { get; private set; }
    public 음식배달수요참여결과? DemandEvidence { get; private set; }
    public int ModelRequests => _modelCounter.Calls;
    public bool ThrowNotificationCreation { get; set; }
    public bool PushSucceeded { get => _push.Succeeded; set => _push.Succeeded = value; }

    public static async Task<FoodDispatchWorkflowScenarioFixture> CreateAsync(
        string scenarioId, HttpClient modelHttp, string? databasePath = null, FoodDispatchRegionalSeed? regionalSeed = null)
    {
        if (scenarioId is not ("same-region" or "adjacent-region" or "demand-capacity"))
            throw new ArgumentException("Unknown synthetic scenario.", nameof(scenarioId));
        ArgumentNullException.ThrowIfNull(modelHttp);
        regionalSeed?.Validate();
        if (databasePath is not null && File.Exists(databasePath)) throw new IOException("Evidence database already exists.");
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        { DataSource = databasePath ?? $"r49-{Guid.NewGuid():N}", Mode = databasePath is null ? SqliteOpenMode.Memory : SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared }.ToString());
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<SsalddelContext>().UseSqlite(connection.ConnectionString).Options;
        var fixture = new FoodDispatchWorkflowScenarioFixture(scenarioId, connection, options, modelHttp, regionalSeed);
        try
        {
            await using (var db = fixture.CreateContext()) { await db.Database.EnsureCreatedAsync(); await fixture.SeedAsync(db); }
            await fixture.StartHttpAsync();
            await fixture.CreateAndAcceptOrderAsync();
            return fixture;
        }
        catch { await fixture.DisposeAsync(); throw; }
    }

    public SsalddelContext CreateContext() => new(_dbOptions, new Encryption());
    private ISsalddelExecutionModePolicy Execution => new SsalddelExecutionModePolicy(
        Options.Create(new SsalddelExecutionOptions { Mode = SsalddelExecutionMode.Simulation }));
    private I운영배차공통UseCase Availability(SsalddelContext db) => new 운영배차공통UseCase(
        new Ef운영배차활동원장Store(db), _availabilityProjection, new 운영배차수신상태Policy(),
        new 운영배차단기지표Calculator(), TimeProvider.System, NullLogger<운영배차공통UseCase>.Instance);
    private IFoodDispatchCandidateAI AI() => new OllamaFoodDispatchCandidateAI(_modelHttp, Options.Create(_aiOptions));
    private 음식배달수요참여Service Demand(SsalddelContext db) => new(db, _spaces, _states, Availability(db),
        Options.Create(_demandOptions), new 음식배달수요참여Policy(), NullLogger<음식배달수요참여Service>.Instance, TimeProvider.System);
    private 음식배달배차업무정책 Policy(SsalddelContext db) => new(_states, _rejected, _routes, _spaces,
        new EfSsalddelFoodOrderStore(db), Options.Create(_queueOptions), Availability(db),
        수요참여: Demand(db), candidateAI: AI());
    private 배차추천알림Service Notifications(SsalddelContext db) => new(db,
        new AppScopedDriverPushTokenStore(db, _legacyTokens), _push, NullLogger<배차추천알림Service>.Instance);
    private 배차대기원장전환Service Transition(SsalddelContext db)
    {
        var ai = AI();
        var engine = new 음식배달배차엔진([Policy(db)], new 운송의뢰배차원천분류Service(),
            new 음식배달배차흐름Resolver(), NullLogger<음식배달배차엔진>.Instance);
        var candidates = new 배차추천후보선정Service(db, new 운영체제배차EngineCatalog([engine]), ai);
        return new(db, Options.Create(_queueOptions), candidates, new NotificationCreationBoundary(this, Notifications(db)),
            null!, new 음식배달배차흐름Resolver(),
            new 음식배달기사제안요금Service(db, _routes, new PricingWeather(), TimeProvider.System, Execution),
            new 음식배달기사추천기록Service(_states), Options.Create(_demandOptions), ai);
    }
    private 음식배차요청OutboxService DispatchRequests(SsalddelContext db) => new(db,
        new 운송의뢰배차대기Service(db, new 운송의뢰배차원천분류Service(), new SyntheticAreaProjection(), _spaces, _cargoSpaces),
        _mongo, _foodLedger, new SyntheticTransportRealtime(), _restaurantNotifications,
        new EfSsalddelFoodOrderStore(db), new SyntheticGeocoder(DropoffLatitude, DropoffLongitude), NullLogger<음식배차요청OutboxService>.Instance);
    private 음식배달기사업무Service DriverWork(SsalddelContext db, ICurrentUserAccessor actor) => new(db,
        _locations, _routes, Transition(db), _spaces, new EfSsalddelFoodOrderStore(db), _foodLedger,
        _mongo, _restaurantNotifications, new NoOpSettlement(), _publisher, actor,
        NullLogger<음식배달기사업무Service>.Instance, Availability(db));

    private async Task SeedAsync(SsalddelContext db)
    {
        db.음식점공개프로필.Add(new 음식점공개프로필 { Id = RestaurantId, 상호명 = _regionalSeed?.PublicStoreName ?? "합성 시험 식당", 공개여부 = true });
        db.음식운영정책.Add(new 음식운영정책 { 기사픽업지급액 = 700m });
        var now = DateTime.UtcNow;
        var latitude = _regionalSeed?.PickupLatitude ?? (ScenarioId == "adjacent-region" ? 37.526m : 37.5121m);
        foreach (var definition in new[] { (Id: "scenario-older", Aging: 100m, Delta: 0.0001m),
                     (Id: "scenario-near", Aging: 0m, Delta: 0m), (Id: "scenario-off", Aging: 200m, Delta: 0.0002m) })
        {
            db.용달기사.Add(new 용달기사 { 기사Id = definition.Id, 상태 = "활동중", 차량 = "자동차", 운행상태 = 상태값.기사운행상태.운행중 });
            db.Set<배달기사>().Add(new 배달기사 { 기사Id = definition.Id, 차량 = "자동차" });
            var shift = new 기사근무 { 기사Id = definition.Id, ActiveDriverId = definition.Id,
                시작시각 = now.AddMinutes(definition.Id == "scenario-older" ? -30 : -10),
                시작모드 = "immediate", 운송실행유형 = 운송실행유형코드.음식배달 };
            db.기사근무.Add(shift); await db.SaveChangesAsync();
            var driverLatitude = latitude + (_regionalSeed is null ? definition.Delta : 0m);
            await _states.UpsertAsync(new(definition.Id, shift.Id, 상태값.기사운행상태.운행중, shift.시작시각,
                now.AddMinutes(-30), definition.Aging, driverLatitude, PickupLongitude, 5m, now, now,
                null, null, 0, "immediate", "synthetic", null, AppKey: 기사앱식별자.FoodDeliveryDriverApp));
            var key = 음식배달권정책.판정(new(driverLatitude, PickupLongitude), null).배달권키;
            await _spaces.Upsert기사Async(key, definition.Id, 음식배달권정책.인접배달권키조회(key));
            await Availability(db).기사의사변경Async(definition.Id, new() { 클라이언트요청Id = Guid.NewGuid(),
                수신의사Code = definition.Id == "scenario-off" ? 운영배차수신의사Code.Off : 운영배차수신의사Code.On });
            await new SsalddelMobilePushInstallationService(db).UpsertAsync(definition.Id,
                new($"synthetic-installation-{definition.Id}", 기사앱식별자.FoodDeliveryDriverApp, "Android", $"synthetic-token-{definition.Id}"), default);
        }
        await db.SaveChangesAsync();
    }

    private async Task StartHttpAsync()
    {
        var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions
        { EnvironmentName = "Testing", ApplicationName = typeof(음식주문Controller).Assembly.GetName().Name, ContentRootPath = AppContext.BaseDirectory });
        builder.WebHost.UseKestrel(options => options.Listen(IPAddress.Loopback, 0));
        builder.Services.AddLogging(); builder.Services.AddRouting(); builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped(_ => CreateContext());
        builder.Services.AddSingleton<IVersionFeatureFlagService>(_flags);
        builder.Services.AddSingleton<I국내화물운송기사상태Store>(_states);
        builder.Services.AddSingleton<IPublisher>(_publisher);
        builder.Services.AddMediatR(config => config.RegisterServicesFromAssemblyContaining<음식주문등록CommandHandler>());
        // RecordingPublisher intentionally suppresses production Event handlers; actual durable bridges are explicitly drained.
        builder.Services.AddSingleton<IPublisher>(_publisher);
        builder.Services.AddScoped<ISsalddelFoodOrderStore, EfSsalddelFoodOrderStore>();
        builder.Services.AddSingleton<I음식주문메뉴검증Service, PassThroughMenuValidationService>();
        builder.Services.AddScoped<I음식배차요청OutboxService>(sp => DispatchRequests(sp.GetRequiredService<SsalddelContext>()));
        builder.Services.AddScoped<I음식주문접수UseCase, 음식주문접수UseCase>();
        builder.Services.AddScoped<I주문자음식주문조회UseCase, 주문자음식주문조회UseCase>();
        builder.Services.AddScoped<I음식점음식주문조회UseCase, 음식점음식주문조회UseCase>();
        builder.Services.AddScoped<I운영배차공통UseCase>(sp => Availability(sp.GetRequiredService<SsalddelContext>()));
        builder.Services.AddScoped<ICurrentUserAccessor, HttpActor>();
        builder.Services.AddScoped<I음식배달기사업무Service>(sp => DriverWork(sp.GetRequiredService<SsalddelContext>(), sp.GetRequiredService<ICurrentUserAccessor>()));
        builder.Services.AddScoped<IFoodDeliveryDriverWorkspaceUseCase>(sp => new FoodDeliveryDriverWorkspaceUseCase(
            sp.GetRequiredService<SsalddelContext>(), sp.GetRequiredService<I음식배달기사업무Service>(), null!, Execution, _flags));
        builder.Services.AddScoped<I기사알림UseCase>(sp => new 기사알림UseCase(_legacyTokens, _notificationSettings,
            sp.GetRequiredService<SsalddelContext>(), NullLogger<기사알림UseCase>.Instance));
        builder.Services.AddControllers().ConfigureApplicationPartManager(manager =>
        { manager.ApplicationParts.Clear(); manager.FeatureProviders.Clear(); manager.FeatureProviders.Add(new Controllers()); }).AddControllersAsServices();
        builder.Services.AddTransient(sp => new 음식배달기사업무Controller(sp.GetRequiredService<IFoodDeliveryDriverWorkspaceUseCase>(),
            null!, sp.GetRequiredService<I음식배달기사업무Service>(), sp.GetRequiredService<ISender>()));
        builder.Services.AddAuthentication("Fixture").AddScheme<AuthenticationSchemeOptions, FixtureAuthentication>("Fixture", _ => { });
        builder.Services.AddAuthorization(options => options.AddPolicy("음식점운영자전용", policy =>
            policy.RequireAuthenticatedUser().RequireClaim("fixture-role", "restaurant")));
        _app = builder.Build();
        _app.UseRouting(); _app.UseAuthentication(); _app.UseAuthorization(); _app.MapControllers();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await _app.StartAsync(deadline.Token);
        var addresses = _app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!;
        _client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseProxy = false })
        { BaseAddress = new Uri(Assert.Single(addresses.Addresses)), Timeout = TimeSpan.FromSeconds(20) };
    }

    private async Task CreateAndAcceptOrderAsync()
    {
        Actor("fixture-orderer");
        var request = new 음식주문등록요청 { 클라이언트요청Id = Guid.NewGuid(), 음식점Id = RestaurantId,
            수령인정보 = new() { 수령인명 = "합성 주문자", 연락처 = "010-0000-0000", 주소 = "합성 전달 도로", 주문자본인수령여부 = true },
            상품목록 = [new() { 메뉴Id = 49, 상품명 = _regionalSeed?.HistoricalMenuName ?? "합성 메뉴", 수량 = 1,
                단가 = _regionalSeed?.SyntheticUnitPriceKrw ?? 9000m }] };
        using var created = await _client.PostAsJsonAsync("api/v1/food-orders", request);
        await RequireSuccessAsync(created);
        await using (var db = CreateContext()) OfferId = await db.음식주문.Select(x => x.주문번호).SingleAsync();
        _steps.Add("OrderCreatedThroughAuthenticatedHttp");
        Actor("fixture-restaurant");
        using var accepted = await _client.PostAsJsonAsync($"api/v1/food-orders/{OfferId}/restaurant-acceptance",
            new 음식점주문수락요청 { 클라이언트요청Id = Guid.NewGuid(), 음식점명 = _regionalSeed?.PublicStoreName ?? "합성 시험 식당",
                음식점주소 = _regionalSeed?.PublicPickupAddress ?? "합성 픽업 도로", 음식점위도 = PickupLatitude, 음식점경도 = PickupLongitude, 조리예상분 = 10 });
        await RequireSuccessAsync(accepted);
        _steps.Add("RestaurantAcceptedThroughScopedHttp");
        await using (var db = CreateContext())
        {
            Assert.Single(await db.음식마트원장동기화Outbox.ToListAsync());
            Assert.Equal(1, await DispatchRequests(db).대기항목처리Async());
            Assert.Equal("Succeeded", (await db.음식마트원장동기화Outbox.SingleAsync()).처리상태);
            Assert.Equal(OfferId, (await db.운송원장.SingleAsync()).의뢰Id);
        }
        _steps.Add("DispatchRequestOutboxCreatedRealQueue");
        Actor(AccessToken);
    }

    public async Task RecommendAsync()
    {
        await using var db = CreateContext();
        var queue = await db.운송원장.SingleAsync();
        var trace = await Policy(db).후보검토Async(queue);
        var eligible = FoodDispatchCandidateConstraints.Eligible(trace);
        Assert.NotNull(trace.선정후보);
        Assert.Equal(ScenarioId == "demand-capacity" ? 1 : 2, eligible.Count);
        if (_demandOptions.Enabled) DemandEvidence = await Demand(db).평가Async(queue);
        var transition = queue.배차큐단계 == 상태값.배차큐단계.계획배차
            ? await Transition(db).계획배차에서추천으로전환Async(OfferId)
            : await Transition(db).추천대기처리Async(OfferId);
        Assert.True(transition.전환여부);
        db.ChangeTracker.Clear(); queue = await db.운송원장.SingleAsync();
        DriverId = queue.현재추천대상기사Id ?? throw new InvalidOperationException("No actual recommendation stored.");
        var audit = await db.운송이벤트.OrderByDescending(x => x.Id).FirstAsync();
        using var metadata = JsonDocument.Parse(audit.메타데이터);
        var reason = metadata.RootElement.TryGetProperty("CandidateReason", out var r) ? r.GetString() ?? "" : "";
        RecommendationEvidence = new(trace.선정후보!.DriverId, DriverId, ModelRequests,
            reason.Contains("FoodDispatchAI.Applied", StringComparison.Ordinal), reason, eligible.Count, trace);
        _steps.Add("ActualCandidatePolicyAndAIStoredRecommendationAndFrozenFee");
        Actor(AccessToken);
    }

    public async Task RetryRecommendationAsync()
    {
        await using var db = CreateContext();
        await Transition(db).추천대기처리Async(OfferId);
    }

    public async Task RetryNotificationRequestAsync()
    {
        await using var db = CreateContext();
        var queue = await db.운송원장.SingleAsync();
        await Notifications(db).추천알림요청생성Async(queue.Id, queue.의뢰Id,
            queue.현재추천대상기사Id ?? throw new InvalidOperationException("No recommended driver."), queue.추천라운드);
    }

    public async Task DispatchPendingNotificationsAsync()
    {
        await using var db = CreateContext();
        await Notifications(db).업무유형별대기알림발송Async(상태값.배차업무유형.음식배달);
        _steps.Add("FoodNotificationOutboxDrainedToRecordingFCMBoundary");
    }

    public async Task SetPushInstallationActiveAsync(bool active)
    {
        await using var db = CreateContext();
        var row = await db.SsalddelMobilePushInstallations.SingleAsync(x => x.UserId == DriverId);
        row.IsActive = active; await db.SaveChangesAsync();
    }

    public async Task SetDriverLocationAsync(bool atPickup)
    {
        var lat = atPickup ? PickupLatitude : DropoffLatitude;
        var lon = atPickup ? PickupLongitude : DropoffLongitude;
        var now = DateTime.UtcNow;
        var state = await _states.GetAsync(DriverId) ?? throw new InvalidOperationException();
        await _states.UpsertAsync(state with { Latitude = lat, Longitude = lon, 위치기록시각Utc = now, 위치수신시각Utc = now });
        _locations.Upsert(new DriverLocationSnapshot(DriverId, lat, lon, 5m, 상태값.기사운행상태.운행중, now, now));
    }

    public async Task MarkPreparationReadyAsync()
    {
        Actor("fixture-restaurant");
        await using (var db = CreateContext())
        {
            var order = new EfSsalddelFoodOrderStore(db).GetOrder(OfferId)
                ?? throw new InvalidOperationException("Synthetic order missing.");
            using var started = await _client.PostAsJsonAsync($"api/v1/food-orders/{OfferId}/restaurant-progress",
                new 음식점주문진행변경요청 { 클라이언트요청Id = Guid.NewGuid(), 예상Revision = order.Revision,
                    작업 = 음식점주문진행작업코드.조리시작 });
            await RequireSuccessAsync(started);
        }
        using var ready = await _client.PostAsJsonAsync($"api/v1/food-orders/{OfferId}/restaurant-progress",
            new 음식점주문진행변경요청 { 클라이언트요청Id = Guid.NewGuid(), 작업 = 음식점주문진행작업코드.픽업준비 });
        await RequireSuccessAsync(ready); _steps.Add("RestaurantStartedAndPreparedThroughHttp"); Actor(AccessToken);
    }

    public async Task ConfirmReceiptAsync()
    {
        Actor("fixture-orderer");
        using var received = await _client.PostAsJsonAsync($"api/v1/food-orders/{OfferId}/receipt-confirmation",
            new 주문자음식주문수령확인요청 { 클라이언트요청Id = Guid.NewGuid(), 확인메모 = "합성 시험 수령" });
        await RequireSuccessAsync(received); _steps.Add("CustomerReceiptConfirmedSeparatelyThroughHttp"); Actor(AccessToken);
    }

    public async Task<FoodDispatchWorkflowSnapshot> ReadSnapshotAsync()
    {
        await using var db = CreateContext();
        var order = await db.음식주문.AsNoTracking().SingleAsync();
        var queue = await db.운송원장.AsNoTracking().SingleAsync();
        var attempts = await db.음식배달시도.AsNoTracking().OrderBy(x => x.Id).ToListAsync();
        var last = attempts.LastOrDefault();
        return new(order.상태, queue.상태, queue.배차큐단계, queue.배차노출상태, queue.추천라운드,
            queue.현재추천대상기사Id, queue.확정기사Id, queue.기사지급예정액, await db.음식주문.CountAsync(),
            await db.운송원장.CountAsync(), await db.음식마트원장동기화Outbox.CountAsync(),
            await db.음식마트원장동기화Outbox.OrderBy(x => x.Id).Select(x => x.처리상태).ToArrayAsync(),
            await db.배차추천알림Outbox.CountAsync(), await db.배차추천알림Outbox.OrderBy(x => x.Id).Select(x => x.발송상태).ToArrayAsync(),
            attempts.Count, last?.Revision, last?.가게도착시각Utc is not null, last?.픽업완료시각Utc is not null,
            last?.전달완료시각Utc is not null, order.상태 == 음식주문상태코드.수령확인);
    }

    public HttpClient CreateActorClient(string? bearerToken)
    {
        var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseProxy = false })
        { BaseAddress = BaseAddress, Timeout = TimeSpan.FromSeconds(20) };
        if (bearerToken is not null) client.DefaultRequestHeaders.Authorization = new("Bearer", bearerToken);
        return client;
    }
    private void Actor(string token) => _client.DefaultRequestHeaders.Authorization = new("Bearer", token);
    private static async Task RequireSuccessAsync(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Synthetic HTTP failed ({(int)response.StatusCode}): {await response.Content.ReadAsStringAsync()}");
    }
    public async ValueTask DisposeAsync()
    {
        _client?.Dispose();
        if (_app is not null) { using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5)); await _app.StopAsync(deadline.Token); await _app.DisposeAsync(); }
        _modelHttp.Dispose(); await _connection.DisposeAsync();
    }
}

public sealed record FoodDispatchWorkflowRecommendationEvidence(string BaselineDriverId, string SelectedDriverId,
    int ModelRequests, bool Applied, string Reason, int EligibleCount, 음식배달후보검토결과 Trace);

public sealed record FoodDispatchWorkflowSnapshot(string OrderStatus, string QueueStatus, int QueueStage,
    int ExposureStatus, int RecommendationRound, string? RecommendedDriverId, string? ConfirmedDriverId,
    decimal? FrozenFee, int OrderCount, int QueueCount, int DispatchRequestOutboxCount, string[] DispatchRequestOutboxStatuses,
    int NotificationOutboxCount, string[] NotificationOutboxStatuses, int AttemptCount, long? AttemptRevision,
    bool Arrived, bool PickedUp, bool Delivered, bool CustomerReceived);
