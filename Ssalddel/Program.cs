using System.Text;
using System.Text.Json;
using System.Globalization;
using Ssalddel.Hubs;
using Ssalddel.Application.Behaviors;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Controllers;
using Ssalddel.Security;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using Serilog;
using Ssalddel.Application.Driver.Transport;
using Ssalddel.Extensions;
using Quartz;
using 살뜰.Infrastructure;
using 살뜰.Infrastructure.BackgroundJobs.DispatchQueue;
using 살뜰.Infrastructure.BackgroundJobs.Payments;
using Ssalddel.Middleware;
using 살뜰.Services.Audit;
using Ssalddel.Services.Auth;
using 살뜰.Services.Documents;
using 살뜰.Services.External.Google;
using Ssalddel.Services.Storage;
using 살뜰.Services.Images;
using 살뜰.Services.Options;
using 살뜰.Services.Sales;
using 살뜰.Services.ViewSettings;
using Ssalddel.Services.LogisticsProcessing.Warehouse;
using Ssalddel.Services.LogisticsProcessing.SalesOrders;
using 살뜰.Services.Dispatch.Queue;
using 살뜰.Services.Dispatch.Notification;
using 살뜰.Services.Notifications;
using 살뜰.Services.Payments;
using Ssalddel.Services.Driver.Development;
using Ssalddel.Services.Development;
using Ssalddel.Services.Security;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.FileProviders;
using Ssalddel.Infrastructure.Persistence.AgriculturalFisheries;
using Ssalddel.Infrastructure.Persistence.PublicData;
using Ssalddel.Infrastructure.Persistence.TraditionalMarkets;
using Ssalddel.Infrastructure.BackgroundJobs.Community;
using Ssalddel.Domain.AgriculturalFisheries;
using Ssalddel.Services.AgriculturalFisheries.Information;
using Ssalddel.Services.AgriculturalFisheries.ImportReadiness;
using Ssalddel.Contracts.Common.AgriculturalFisheries;
using Ssalddel.Contracts.Common.Customs;
using Ssalddel.Contracts.Common.Content;
using Ssalddel.Contracts.Common.Transport;
using Ssalddel.Services.Content;
using Ssalddel.Services.Community;
using Ssalddel.Services.Customs;
using Ssalddel.Services.FoodCulture;
using Ssalddel.Startup;
using 살뜰.Services.External.PublicData;
using 살뜰.Services.External.PublicData.Korea;
using Ssalddel.Simulation.Hosting;

var builder = WebApplication.CreateBuilder(args);
const string CustomsWebCorsPolicy = "SsalddelWebCustoms";
var isTestingEnvironment = builder.Environment.EnvironmentName.StartsWith(
    "Testing",
    StringComparison.Ordinal);
var isRunningInContainer = string.Equals(
    Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"),
    "true",
    StringComparison.OrdinalIgnoreCase);

if (!isRunningInContainer && !isTestingEnvironment)
{
    builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);
}

if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddUserSecrets<Program>(optional: true);
}

// appsettings.Local.json is intentionally local-only, while deployment and
// one-off maintenance commands must still be able to override it safely.
builder.Configuration
    .AddEnvironmentVariables()
    .AddCommandLine(args);

builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", "Ssalddel.LogisticsApi")
        .WriteTo.Console();

    var fileLogPath = context.Configuration["SsalddelLogging:FilePath"];
    if (string.IsNullOrWhiteSpace(fileLogPath)
        && context.HostingEnvironment.IsDevelopment()
        && !isRunningInContainer)
    {
        fileLogPath = "logs/ssalddel-.log";
    }

    if (!string.IsNullOrWhiteSpace(fileLogPath))
    {
        configuration.WriteTo.File(
            path: fileLogPath,
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 14);
    }
});

builder.Services.AddSsalddelPresentation(builder.Configuration, builder.Environment);
builder.Services.AddSsalddelApplicationCore();
builder.Services.AddCors(options =>
{
    options.AddPolicy(CustomsWebCorsPolicy, policy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("CustomsBrokerCors:AllowedOrigins").Get<string[]>() ?? [];
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
if (string.IsNullOrWhiteSpace(jwtOptions.SecretKey))
{
    throw new InvalidOperationException("Jwt:SecretKey configuration is required.");
}

builder.Services.AddScoped<AuthTokenService>();

var executionOptions = builder.Configuration
    .GetSection(SsalddelExecutionOptions.SectionName)
    .Get<SsalddelExecutionOptions>() ?? new SsalddelExecutionOptions();
if (!Enum.IsDefined(executionOptions.Mode))
{
    throw new InvalidOperationException("SsalddelExecution:Mode must be Simulation or Operational.");
}

var tossOptions = builder.Configuration.GetSection(TossPaymentsOptions.SectionName).Get<TossPaymentsOptions>() ?? new TossPaymentsOptions();
if (executionOptions.Mode == SsalddelExecutionMode.Operational && string.IsNullOrWhiteSpace(tossOptions.SecretKey))
{
    throw new InvalidOperationException("TossPayments:SecretKey configuration is required in Operational mode.");
}

builder.Services.AddSsalddelOptions(builder.Configuration);
builder.Services.AddSsalddelOperatingMarketServices(builder.Configuration);
builder.Services.AddScoped<I가입온보딩친구후보Service, 가입온보딩친구후보Service>();

var dispatchQueueJobOptions = builder.Configuration.GetSection(배차큐배치작업Options.SectionName).Get<배차큐배치작업Options>() ?? new 배차큐배치작업Options();
var foodDeliveryJobOptions = builder.Configuration.GetFoodDeliveryDispatchJobOptions();
var salesOrderSyncOptions = builder.Configuration.GetSection(SalesChannelOrderSyncOptions.SectionName).Get<SalesChannelOrderSyncOptions>() ?? new SalesChannelOrderSyncOptions();
var youTubeOptions = builder.Configuration.GetSection(YouTubeOptions.SectionName).Get<YouTubeOptions>() ?? new YouTubeOptions();
var hongikHakdangCardOptions = builder.Configuration.GetSection(HongikHakdangCardOptions.SectionName).Get<HongikHakdangCardOptions>() ?? new HongikHakdangCardOptions();
var agriculturalFisheriesBatchOptions = builder.Configuration.GetSection(AgriculturalFisheriesBatchOptions.SectionName).Get<AgriculturalFisheriesBatchOptions>() ?? new AgriculturalFisheriesBatchOptions();
var communityEditorialBatchOptions = builder.Configuration.GetSection(CommunityEditorialBatchOptions.SectionName).Get<CommunityEditorialBatchOptions>() ?? new CommunityEditorialBatchOptions();
var databaseInitializationOptions = builder.Configuration.GetSection(DatabaseInitializationOptions.SectionName).Get<DatabaseInitializationOptions>() ?? new DatabaseInitializationOptions();
var initializeDatabaseOnly = args.Any(argument =>
    string.Equals(argument, "--initialize-database", StringComparison.OrdinalIgnoreCase));

builder.Services.AddSsalddelBackgroundJobs(dispatchQueueJobOptions, salesOrderSyncOptions, youTubeOptions, hongikHakdangCardOptions, agriculturalFisheriesBatchOptions, communityEditorialBatchOptions, executionOptions, foodDeliveryJobOptions);
builder.Services.AddSsalddelPersistence(builder.Configuration);
builder.Services.AddSpatialCatalog();
builder.Services.AddSsalddelHealthChecks();

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequireDigit = true;
        options.Password.RequiredLength = 8;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireLowercase = false;
        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    })
    .AddEntityFrameworkStores<SsalddelContext>()
    .AddDefaultTokenProviders();

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SecretKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;

                if (!string.IsNullOrWhiteSpace(accessToken) &&
                    (path.StartsWithSegments("/hubs/dispatch-recommendations") ||
                     path.StartsWithSegments("/hubs/restaurant-orders") ||
                     path.StartsWithSegments(TransportRequestLedgerRealtime.HubPath) ||
                     path.StartsWithSegments(DiagramCollaborationHub.HubPath) ||
                     path.StartsWithSegments(SimulationOnlineWorldHub.HubPath)))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("서버관리자전용", policy => policy.RequireRole(역할명.서버관리자));
    options.AddPolicy("HsCode운영자전용", policy => policy.RequireRole(역할명.서버관리자, 역할명.관세사));
    options.AddPolicy("용달기사전용", policy => policy.RequireRole(역할명.용달기사, 역할명.기사));
    options.AddPolicy("화주또는판매자전용", policy => policy.RequireRole(역할명.화주, 역할명.판매자));
    options.AddPolicy("물류운영사용자전용", policy => policy.RequireRole(역할명.용달기사, 역할명.기사, 역할명.화주, 역할명.창고관리자, 역할명.서버관리자));
    options.AddPolicy("창고관리자전용", policy => policy.RequireRole(역할명.창고관리자, 역할명.서버관리자));
    options.AddPolicy("음식점운영자전용", policy => policy.RequireRole(역할명.음식점));
    options.AddPolicy("운영사용자전용", policy => policy.RequireRole(역할명.화주, 역할명.판매자, 역할명.창고관리자, 역할명.서버관리자));
});
builder.Services.AddSsalddelSimulationModule(
    builder.Configuration,
    builder.Environment);

builder.Services.AddAgriculturalFisheriesInformationModule();
builder.Services.AddHongikAcademyContentMapModule();
builder.Services.AddSsalddelHttpClients();
builder.Services.AddApifyAmazonProductResearch(builder.Configuration);
builder.Services.AddApifyInteriorProductObservation(builder.Configuration);
builder.Services.AddApifySocialMediaResearch(builder.Configuration);
builder.Services.AddApifyYouTubeContentCollection(builder.Configuration);
builder.Services.AddFreeSocialMediaResearch(builder.Configuration);
builder.Services.AddOfficialNewsRss(builder.Configuration);
builder.Services.AddYouTubeSocialContextWorkspace(builder.Configuration);
builder.Services.AddSsalddelDomainServices();
builder.Services.AddSsalddelAgreedRateTables();
Ssalddel.Extensions.DistributedFulfillmentRegistration.AddDistributedFulfillment(builder.Services, builder.Configuration);
Ssalddel.Services.WorldProjection.DistributedBuildingCandidateRegistration.AddDistributedBuildingCandidates(builder.Services, builder.Configuration);
builder.Services.Add통신판매보호Services(builder.Configuration);
Ssalddel.Services.PrivacyRetention.개인정보보존ServiceRegistration.Add개인정보보존Services(builder.Services, builder.Configuration);
Ssalddel.Services.PrivacySupport.보호지원ServiceCollectionExtensions.Add거래개인정보지원Services(builder.Services, builder.Configuration);
Ssalddel.Services.OperationsAlerts.OperationsAlertsRegistration.AddOperationsAlerts(builder.Services, builder.Configuration);
builder.Services.AddFoodDeliveryExperience(builder.Configuration);
var developmentReadOnly = builder.Environment.IsDevelopment()
                          && executionOptions.Mode == SsalddelExecutionMode.Simulation
                          && executionOptions.DevelopmentReadOnly;
if (developmentReadOnly)
{
    builder.Services.RemoveAll<IHostedService>();
}
if (isTestingEnvironment)
{
    builder.Services.RemoveAll<IHostedService>();
}
if (builder.Environment.IsDevelopment())
{
    builder.Services.Replace(ServiceDescriptor.Singleton<IObjectStorageService, DevelopmentLocalStorageService>());
}
builder.Services.AddSingleton<Ssalddel.Services.Orderer.IRestaurantSearchPolicyStore, Ssalddel.Services.Orderer.InMemoryRestaurantSearchPolicyStore>();
builder.Services.AddSingleton<I기사개발스냅샷Provider, InMemory기사개발스냅샷Provider>();

Ssalddel.Services.Development.FoodObserver.음식배달관찰검증Hosting.Add음식배달관찰검증(builder);
Ssalddel.Services.Development.ObservableOperations.관찰운영검증Hosting.Add관찰운영검증(builder);
Ssalddel.Services.Development.MobileFieldTest.모바일음식배차시험Hosting.Add모바일음식배차시험(builder);
var app = builder.Build();
app.Logger.LogInformation("Ssalddel execution mode: {ExecutionMode}", executionOptions.Mode);
if (developmentReadOnly)
{
    app.Logger.LogInformation("Development read-only mode is active; application hosted services are disabled.");
}

if (await AppContextImageBatchCommandLine.TryRunAsync(
        args,
        app.Services,
        app.Environment.ContentRootPath,
        app.Logger,
        CancellationToken.None))
{
    return;
}

if (await AppContextImageAssetPublishCommandLine.TryRunAsync(
        args,
        app.Services,
        app.Environment.ContentRootPath,
        app.Logger,
        CancellationToken.None))
{
    return;
}

if (await 지역문화이미지자산PublishCommandLine.TryRunAsync(
        args,
        app.Services,
        app.Environment.ContentRootPath,
        app.Logger,
        CancellationToken.None))
{
    return;
}

if (await 대한민국공간공공데이터CommandLine.TryRunAsync(
        args,
        app.Services,
        app.Logger,
        CancellationToken.None))
{
    return;
}

if (args.Any(argument => string.Equals(
        argument,
        "--analyze-kamis-packaging-fcl",
        StringComparison.OrdinalIgnoreCase)))
{
    var yearArgument = args.FirstOrDefault(argument =>
        argument.StartsWith("--year=", StringComparison.OrdinalIgnoreCase));
    var sourceYear = int.TryParse(
        yearArgument?["--year=".Length..],
        NumberStyles.None,
        CultureInfo.InvariantCulture,
        out var parsedSourceYear)
        ? parsedSourceYear
        : DateTime.Now.Year;

    await using var scope = app.Services.CreateAsyncScope();
    var archiveDb = scope.ServiceProvider.GetRequiredService<AgriculturalFisheriesDbContext>();
    await archiveDb.Database.MigrateAsync();
    var analysisService =
        scope.ServiceProvider.GetRequiredService<I농수산물포장Fcl분석Service>();
    var result = await analysisService.분석저장Async(sourceYear, CancellationToken.None);
    var report = await analysisService.조회Async(
        sourceYear,
        itemCode: null,
        categoryCode: null,
        CancellationToken.None);

    app.Logger.LogInformation(
        "KAMIS 품목 포장·FCL 분석 DB 저장 완료. SourceYear={SourceYear}, Items={Items}, Inserted={Inserted}, Updated={Updated}, AnalyzedAtUtc={AnalyzedAtUtc}",
        result.SourceYear,
        result.ItemCount,
        result.InsertedCount,
        result.UpdatedCount,
        result.AnalyzedAtUtc);
    Console.WriteLine(JsonSerializer.Serialize(
        report,
        new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = true
        }));
    return;
}

if (args.Any(argument => string.Equals(
        argument,
        "--publish-weekly-country-product-comparison",
        StringComparison.OrdinalIgnoreCase)))
{
    await using var scope = app.Services.CreateAsyncScope();
    var archiveDb = scope.ServiceProvider.GetRequiredService<AgriculturalFisheriesDbContext>();
    await archiveDb.Database.MigrateAsync();
    var runner = scope.ServiceProvider.GetRequiredService<CommunityEditorialBatchRunner>();
    await runner.RunWeeklyCountryProductComparisonAsync(CancellationToken.None);

    var snapshot = await archiveDb.WeeklyCountryProductComparisonSnapshots
        .AsNoTracking()
        .OrderByDescending(item => item.WeekEndDate)
        .ThenByDescending(item => item.Id)
        .FirstOrDefaultAsync();
    var itemCount = snapshot is null
        ? 0
        : await archiveDb.WeeklyCountryProductComparisonItems
            .AsNoTracking()
            .CountAsync(item => item.SnapshotId == snapshot.Id);
    var countryBreakdown = snapshot is null
        ? []
        : await archiveDb.WeeklyCountryProductComparisonItems
            .AsNoTracking()
            .Where(item => item.SnapshotId == snapshot.Id)
            .GroupBy(item => new { item.CountryCode, item.StatusCode })
            .Select(group => new
            {
                group.Key.CountryCode,
                group.Key.StatusCode,
                Count = group.Count()
            })
            .OrderBy(item => item.CountryCode)
            .ThenBy(item => item.StatusCode)
            .ToListAsync();
    var postId = snapshot is null
        ? null
        : await scope.ServiceProvider.GetRequiredService<SsalddelContext>()
            .PlatformCommunityPosts
            .AsNoTracking()
            .Where(post => !post.IsDeleted
                           && post.AuthorUserId
                           == CommunityAutomatedPostPublication.BuildSystemAuthorKey(
                               CommunityAutomatedPostSourceKeys.WeeklyCountryProductComparison,
                               snapshot.PeriodKey))
            .Select(post => (long?)post.Id)
            .FirstOrDefaultAsync();
    app.Logger.LogInformation(
        "주간 한미중 농수산물 비교 일회 실행 완료. SnapshotId={SnapshotId}, PeriodKey={PeriodKey}, Available={Available}, Items={Items}, CommunityPostId={CommunityPostId}",
        snapshot?.Id,
        snapshot?.PeriodKey,
        snapshot?.AvailableObservationCount ?? 0,
        itemCount,
        postId);
    app.Logger.LogInformation(
        "주간 한미중 비교 국가별 저장 현황: {CountryBreakdown}",
        string.Join(
            ", ",
            countryBreakdown.Select(item =>
                $"{item.CountryCode}/{item.StatusCode}={item.Count}")));
    return;
}

if (args.Any(argument => string.Equals(
        argument,
        "--inspect-kamis-centered-usda-ams-prices",
        StringComparison.OrdinalIgnoreCase)))
{
    var yearArgument = args.FirstOrDefault(argument =>
        argument.StartsWith("--year=", StringComparison.OrdinalIgnoreCase));
    var inspectionYear = int.TryParse(
        yearArgument?["--year=".Length..],
        NumberStyles.None,
        CultureInfo.InvariantCulture,
        out var parsedInspectionYear)
        ? parsedInspectionYear
        : DateTime.Now.Year;
    var itemCodeArgument = args.FirstOrDefault(argument =>
        argument.StartsWith("--item-code=", StringComparison.OrdinalIgnoreCase));
    var pageSizeArgument = args.FirstOrDefault(argument =>
        argument.StartsWith("--page-size=", StringComparison.OrdinalIgnoreCase));
    var pageSize = int.TryParse(
        pageSizeArgument?["--page-size=".Length..],
        NumberStyles.None,
        CultureInfo.InvariantCulture,
        out var parsedPageSize)
        ? parsedPageSize
        : 10;
    var kamisPointsArgument = args.FirstOrDefault(argument =>
        argument.StartsWith("--kamis-points=", StringComparison.OrdinalIgnoreCase));
    var kamisPoints = int.TryParse(
        kamisPointsArgument?["--kamis-points=".Length..],
        NumberStyles.None,
        CultureInfo.InvariantCulture,
        out var parsedKamisPoints)
        ? parsedKamisPoints
        : 2;
    var amsPointsArgument = args.FirstOrDefault(argument =>
        argument.StartsWith("--ams-points=", StringComparison.OrdinalIgnoreCase));
    var amsPoints = int.TryParse(
        amsPointsArgument?["--ams-points=".Length..],
        NumberStyles.None,
        CultureInfo.InvariantCulture,
        out var parsedAmsPoints)
        ? parsedAmsPoints
        : 1;

    await using var scope = app.Services.CreateAsyncScope();
    if (args.Any(argument => string.Equals(
            argument,
            "--include-import-prices",
            StringComparison.OrdinalIgnoreCase)))
    {
        var countryCodeArgument = args.FirstOrDefault(argument =>
            argument.StartsWith(
                "--country-code=",
                StringComparison.OrdinalIgnoreCase));
        var referenceMonthArgument = args.FirstOrDefault(argument =>
            argument.StartsWith(
                "--reference-month=",
                StringComparison.OrdinalIgnoreCase));
        var hsCodeArgument = args.FirstOrDefault(argument =>
            argument.StartsWith("--hs-code=", StringComparison.OrdinalIgnoreCase));
        var importLookbackArgument = args.FirstOrDefault(argument =>
            argument.StartsWith(
                "--import-lookback-months=",
                StringComparison.OrdinalIgnoreCase));
        var importLookbackMonths = int.TryParse(
            importLookbackArgument?["--import-lookback-months=".Length..],
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out var parsedImportLookbackMonths)
            ? parsedImportLookbackMonths
            : 3;
        var fxRateArgument = args.FirstOrDefault(argument =>
            argument.StartsWith(
                "--fx-rate-krw-per-usd=",
                StringComparison.OrdinalIgnoreCase));
        var fxRateKrwPerUsd = decimal.TryParse(
            fxRateArgument?["--fx-rate-krw-per-usd=".Length..],
            NumberStyles.Number,
            CultureInfo.InvariantCulture,
            out var parsedFxRate)
            ? parsedFxRate
            : (decimal?)null;
        var hsCandidatesArgument = args.FirstOrDefault(argument =>
            argument.StartsWith(
                "--hs-candidates-per-item=",
                StringComparison.OrdinalIgnoreCase));
        var hsCandidatesPerItem = int.TryParse(
            hsCandidatesArgument?["--hs-candidates-per-item=".Length..],
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out var parsedHsCandidatesPerItem)
            ? parsedHsCandidatesPerItem
            : 1;

        var decisionSupportService = scope.ServiceProvider
            .GetRequiredService<IKamis중심같이수입가격QueryService>();
        var decisionSupportResponse = await decisionSupportService.GetAsync(
            new Kamis중심같이수입가격Query
            {
                Year = inspectionYear,
                ItemCode = itemCodeArgument?["--item-code=".Length..],
                HsCode = hsCodeArgument?["--hs-code=".Length..],
                OnlyAmsMapped = args.Any(argument => string.Equals(
                    argument,
                    "--only-mapped",
                    StringComparison.OrdinalIgnoreCase)),
                PageSize = pageSize,
                KamisPointsPerItem = kamisPoints,
                AmsPointsPerStage = amsPoints,
                CountryCode =
                    countryCodeArgument?["--country-code=".Length..]
                    ?? "CN",
                ReferenceMonth =
                    referenceMonthArgument?["--reference-month=".Length..],
                ImportLookbackMonths = importLookbackMonths,
                FxRateKrwPerUsd = fxRateKrwPerUsd,
                HsPriceCandidatesPerItem = hsCandidatesPerItem
            });
        Console.WriteLine(JsonSerializer.Serialize(
            decisionSupportResponse,
            new JsonSerializerOptions
            {
                WriteIndented = true
            }));
        return;
    }

    var comparisonService = scope.ServiceProvider
        .GetRequiredService<IKamis중심UsdaAms가격비교QueryService>();
    var response = await comparisonService.GetAsync(
        new Kamis중심UsdaAms가격비교Query
        {
            Year = inspectionYear,
            ItemCode = itemCodeArgument?["--item-code=".Length..],
            OnlyMapped = args.Any(argument => string.Equals(
                argument,
                "--only-mapped",
                StringComparison.OrdinalIgnoreCase)),
            PageSize = pageSize,
            KamisPointsPerItem = kamisPoints,
            AmsPointsPerStage = amsPoints
        });
    Console.WriteLine(JsonSerializer.Serialize(
        response,
        new JsonSerializerOptions
        {
            WriteIndented = true
        }));
    return;
}

if (args.Any(argument => string.Equals(
        argument,
        "--confirm-common-food-product-candidates",
        StringComparison.OrdinalIgnoreCase)))
{
    var yearArgument = args.FirstOrDefault(argument =>
        argument.StartsWith("--year=", StringComparison.OrdinalIgnoreCase));
    var hashArgument = args.FirstOrDefault(argument =>
        argument.StartsWith("--expected-preview-hash=", StringComparison.OrdinalIgnoreCase));
    var confirmedByArgument = args.FirstOrDefault(argument =>
        argument.StartsWith("--confirmed-by=", StringComparison.OrdinalIgnoreCase));
    var promotionYear = int.TryParse(
        yearArgument?["--year=".Length..],
        NumberStyles.None,
        CultureInfo.InvariantCulture,
        out var parsedPromotionYear)
        ? parsedPromotionYear
        : DateTime.Now.Year;
    var expectedPreviewHash = hashArgument?["--expected-preview-hash=".Length..]
        ?? throw new ArgumentException("--expected-preview-hash is required.");
    var confirmedBy = confirmedByArgument?["--confirmed-by=".Length..]
        ?? throw new ArgumentException("--confirmed-by is required.");
    await using var scope = app.Services.CreateAsyncScope();
    var promotionUseCase = scope.ServiceProvider
        .GetRequiredService<I공통식품품목기존Data대조UseCase>();
    var promotion = await promotionUseCase.PromoteCandidatesAsync(
        promotionYear,
        expectedPreviewHash,
        confirmedBy);
    Console.WriteLine(JsonSerializer.Serialize(
        promotion,
        new JsonSerializerOptions
        {
            WriteIndented = true
        }));
    return;
}

if (args.Any(argument => string.Equals(
        argument,
        "--preview-common-food-product-reconciliation",
        StringComparison.OrdinalIgnoreCase)))
{
    var yearArgument = args.FirstOrDefault(argument =>
        argument.StartsWith("--year=", StringComparison.OrdinalIgnoreCase));
    var previewYear = int.TryParse(
        yearArgument?["--year=".Length..],
        NumberStyles.None,
        CultureInfo.InvariantCulture,
        out var parsedPreviewYear)
        ? parsedPreviewYear
        : DateTime.Now.Year;
    await using var scope = app.Services.CreateAsyncScope();
    var previewUseCase = scope.ServiceProvider
        .GetRequiredService<I공통식품품목기존Data대조UseCase>();
    var preview = await previewUseCase.PreviewAsync(previewYear);
    Console.WriteLine(JsonSerializer.Serialize(
        preview,
        new JsonSerializerOptions
        {
            WriteIndented = true
        }));
    return;
}

if (await 농수산물품목코드InspectCommandLine.TryRunAsync(args, app.Services))
{
    return;
}

if (await YouTube국가동기화CommandLine.TryRunAsync(args, app.Services, app.Logger))
{
    return;
}

if (await Bls평균소매가격수집CommandLine.TryRunAsync(args, app.Services, app.Logger))
{
    return;
}

if (await 국제농수산가격수집CommandLine.TryRunAsync(args, app.Services, app.Logger))
{
    return;
}

if (await UsdaAms공개사업체CommandLine.TryRunAsync(
        args,
        app.Services,
        app.Logger))
{
    return;
}

if (args.Any(argument =>
        string.Equals(
            argument,
            "--collect-usda-ams-market-prices",
            StringComparison.OrdinalIgnoreCase)))
{
    var yearArgument = args.FirstOrDefault(argument =>
        argument.StartsWith("--year=", StringComparison.OrdinalIgnoreCase));
    var year = int.TryParse(
        yearArgument?["--year=".Length..],
        NumberStyles.None,
        CultureInfo.InvariantCulture,
        out var parsedYear)
        ? parsedYear
        : DateTime.UtcNow.Year;
    var dateToArgument = args.FirstOrDefault(argument =>
        argument.StartsWith("--date-to=", StringComparison.OrdinalIgnoreCase));
    var dateTo = dateToArgument?["--date-to=".Length..];
    var marketTypes = args
        .Where(argument =>
            argument.StartsWith("--market-type=", StringComparison.OrdinalIgnoreCase))
        .Select(argument => argument["--market-type=".Length..])
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .ToArray();

    await using var scope = app.Services.CreateAsyncScope();
    var archiveDb = scope.ServiceProvider
        .GetRequiredService<AgriculturalFisheriesDbContext>();
    await archiveDb.Database.MigrateAsync();
    var archiveService =
        scope.ServiceProvider.GetRequiredService<IUsdaAms시장가격ArchiveService>();
    var result = await archiveService.CollectAsync(
        new UsdaAms시장가격수집요청
        {
            Year = year,
            DateTo = dateTo,
            MarketTypes = marketTypes
        });
    app.Logger.LogInformation(
        "USDA AMS 시장가격 DB 저장 완료. Range={DateFrom}-{DateTo}, Reports={Reports}, Slices={Slices}, RunId={RunId}, Fetched={Fetched}, Inserted={Inserted}, Existing={Existing}, LatestReferenceDate={LatestReferenceDate}",
        result.DateFrom,
        result.DateTo,
        result.DiscoveredReportCount,
        result.CompletedSliceCount,
        result.CollectionRunId,
        result.FetchedCount,
        result.InsertedCount,
        result.ExistingCount,
        result.LatestReferenceDate);
    return;
}

if (args.Any(argument =>
        string.Equals(argument, "--collect-usda-nass-prices", StringComparison.OrdinalIgnoreCase)))
{
    var yearFromArgument = args.FirstOrDefault(argument =>
        argument.StartsWith("--year-from=", StringComparison.OrdinalIgnoreCase));
    var yearFrom = int.TryParse(
        yearFromArgument?["--year-from=".Length..],
        out var parsedYearFrom)
        ? parsedYearFrom
        : DateTime.UtcNow.Year - 1;

    await using var scope = app.Services.CreateAsyncScope();
    var archiveDb = scope.ServiceProvider.GetRequiredService<AgriculturalFisheriesDbContext>();
    await archiveDb.Database.MigrateAsync();
    var archiveService = scope.ServiceProvider.GetRequiredService<IUsdaNassPriceArchiveService>();
    var archiveResult = await archiveService.CollectRecentMonthlyPricesAsync(yearFrom);
    app.Logger.LogInformation(
        "USDA NASS DB 저장 완료. RunId={RunId}, Fetched={Fetched}, Inserted={Inserted}, Existing={Existing}, Mappings={Mappings}, LatestSourceLoad={LatestSourceLoad}",
        archiveResult.CollectionRunId,
        archiveResult.FetchedCount,
        archiveResult.InsertedCount,
        archiveResult.ExistingCount,
        archiveResult.MappingCount,
        archiveResult.LatestSourceLoadTimeUtc);
    return;
}

if (args.Any(argument =>
        string.Equals(argument, "--collect-kamis-price-period", StringComparison.OrdinalIgnoreCase)))
{
    var startDateArgument = args.FirstOrDefault(argument =>
        argument.StartsWith("--start-date=", StringComparison.OrdinalIgnoreCase));
    var startDate = DateOnly.TryParseExact(
        startDateArgument?["--start-date=".Length..],
        "yyyy-MM-dd",
        CultureInfo.InvariantCulture,
        DateTimeStyles.None,
        out var parsedStartDate)
        ? parsedStartDate
        : new DateOnly(DateTime.Now.Year, 1, 1);
    var endDateArgument = args.FirstOrDefault(argument =>
        argument.StartsWith("--end-date=", StringComparison.OrdinalIgnoreCase));
    var endDate = DateOnly.TryParseExact(
        endDateArgument?["--end-date=".Length..],
        "yyyy-MM-dd",
        CultureInfo.InvariantCulture,
        DateTimeStyles.None,
        out var parsedEndDate)
        ? parsedEndDate
        : DateOnly.FromDateTime(DateTime.Now);

    await using var scope = app.Services.CreateAsyncScope();
    var archiveDb = scope.ServiceProvider.GetRequiredService<AgriculturalFisheriesDbContext>();
    await archiveDb.Database.MigrateAsync();
    var observedItemCodes = await archiveDb.KamisPriceObservations
        .AsNoTracking()
        .Where(item => item.ItemCode != string.Empty)
        .Select(item => item.ItemCode)
        .Distinct()
        .OrderBy(itemCode => itemCode)
        .ToListAsync();
    if (observedItemCodes.Count == 0)
    {
        throw new InvalidOperationException(
            "KAMIS 기간 전수조사의 기준이 될 기존 관측 품목코드가 없습니다.");
    }

    var archiveService = scope.ServiceProvider.GetRequiredService<IKamisPriceArchiveService>();
    var archiveResult = await archiveService.CollectPeriodPricesForItemCodesAsync(
        startDate,
        endDate,
        observedItemCodes);
    app.Logger.LogInformation(
        "KAMIS 기존 품목코드 기간 가격 DB 저장 완료. Range={StartDate}~{EndDate}, ItemCodes={ItemCodes}, RunId={RunId}, Fetched={Fetched}, Inserted={Inserted}, Updated={Updated}, Existing={Existing}, LatestSurveyDate={LatestSurveyDate}",
        startDate,
        endDate,
        observedItemCodes.Count,
        archiveResult.CollectionRunId,
        archiveResult.FetchedCount,
        archiveResult.InsertedCount,
        archiveResult.UpdatedCount,
        archiveResult.ExistingCount,
        archiveResult.LatestSurveyDate);
    return;
}

if (args.Any(argument =>
        string.Equals(argument, "--collect-kamis-prices", StringComparison.OrdinalIgnoreCase)))
{
    var targetDateArgument = args.FirstOrDefault(argument =>
        argument.StartsWith("--target-date=", StringComparison.OrdinalIgnoreCase));
    var defaultTargetDate = DateOnly.FromDateTime(DateTime.Now.AddDays(-1));
    var targetDate = DateOnly.TryParseExact(
        targetDateArgument?["--target-date=".Length..],
        "yyyy-MM-dd",
        CultureInfo.InvariantCulture,
        DateTimeStyles.None,
        out var parsedTargetDate)
        ? parsedTargetDate
        : defaultTargetDate;

    await using var scope = app.Services.CreateAsyncScope();
    var archiveDb = scope.ServiceProvider.GetRequiredService<AgriculturalFisheriesDbContext>();
    await archiveDb.Database.MigrateAsync();
    var archiveService = scope.ServiceProvider.GetRequiredService<IKamisPriceArchiveService>();
    var archiveResult = await archiveService.CollectDailyPricesAsync(targetDate);
    app.Logger.LogInformation(
        "KAMIS 국내 가격 DB 저장 완료. RunId={RunId}, Fetched={Fetched}, Inserted={Inserted}, Updated={Updated}, Existing={Existing}, LatestSurveyDate={LatestSurveyDate}",
        archiveResult.CollectionRunId,
        archiveResult.FetchedCount,
        archiveResult.InsertedCount,
        archiveResult.UpdatedCount,
        archiveResult.ExistingCount,
        archiveResult.LatestSurveyDate);
    return;
}

if (args.Any(argument =>
        string.Equals(argument, "--collect-kamis-price-history", StringComparison.OrdinalIgnoreCase)))
{
    var endDateArgument = args.FirstOrDefault(argument =>
        argument.StartsWith("--end-date=", StringComparison.OrdinalIgnoreCase));
    var today = DateOnly.FromDateTime(DateTime.Now);
    var defaultEndDate = new DateOnly(today.Year, today.Month, 1).AddDays(-1);
    var endDate = DateOnly.TryParseExact(
        endDateArgument?["--end-date=".Length..],
        "yyyy-MM-dd",
        CultureInfo.InvariantCulture,
        DateTimeStyles.None,
        out var parsedEndDate)
        ? parsedEndDate
        : defaultEndDate;
    var startDateArgument = args.FirstOrDefault(argument =>
        argument.StartsWith("--start-date=", StringComparison.OrdinalIgnoreCase));
    var defaultStartDate = new DateOnly(endDate.Year, endDate.Month, 1).AddMonths(-11);
    var startDate = DateOnly.TryParseExact(
        startDateArgument?["--start-date=".Length..],
        "yyyy-MM-dd",
        CultureInfo.InvariantCulture,
        DateTimeStyles.None,
        out var parsedStartDate)
        ? parsedStartDate
        : defaultStartDate;

    await using var scope = app.Services.CreateAsyncScope();
    var archiveDb = scope.ServiceProvider.GetRequiredService<AgriculturalFisheriesDbContext>();
    await archiveDb.Database.MigrateAsync();
    var archiveService = scope.ServiceProvider.GetRequiredService<IKamisPriceArchiveService>();
    var archiveResult = await archiveService.CollectMonthlyPricesAsync(startDate, endDate);
    app.Logger.LogInformation(
        "KAMIS 국내 1년 월평균 가격 DB 저장 완료. Range={StartDate}~{EndDate}, RunId={RunId}, Fetched={Fetched}, Inserted={Inserted}, Updated={Updated}, Existing={Existing}, LatestSurveyDate={LatestSurveyDate}",
        startDate,
        endDate,
        archiveResult.CollectionRunId,
        archiveResult.FetchedCount,
        archiveResult.InsertedCount,
        archiveResult.UpdatedCount,
        archiveResult.ExistingCount,
        archiveResult.LatestSurveyDate);
    return;
}

if (args.Any(argument => string.Equals(
        argument,
        "--index-official-food-recipe-ingredients",
        StringComparison.OrdinalIgnoreCase)))
{
    var sourceArgument = args.FirstOrDefault(argument =>
        argument.StartsWith("--source=", StringComparison.OrdinalIgnoreCase));
    var sourceKey = sourceArgument?["--source=".Length..];
    var maxItemsArgument = args.FirstOrDefault(argument =>
        argument.StartsWith("--max-items=", StringComparison.OrdinalIgnoreCase));
    var maxItems = int.TryParse(
        maxItemsArgument?["--max-items=".Length..],
        NumberStyles.Integer,
        CultureInfo.InvariantCulture,
        out var parsedMaxItems)
        ? parsedMaxItems
        : 5000;
    var force = args.Any(argument => string.Equals(
        argument,
        "--force",
        StringComparison.OrdinalIgnoreCase));

    await using var scope = app.Services.CreateAsyncScope();
    var archiveDb = scope.ServiceProvider.GetRequiredService<AgriculturalFisheriesDbContext>();
    await archiveDb.Database.MigrateAsync();
    var ingredientIndexService = scope.ServiceProvider
        .GetRequiredService<IOfficialFoodRecipeIngredientIndexService>();
    var indexResult = await ingredientIndexService.RebuildAsync(
        new OfficialFoodIngredientIndexRequest(sourceKey, maxItems, force));
    app.Logger.LogInformation(
        "공식 음식 레시피 재료 전산화 완료. Source={SourceKey}, Variants={Variants}, RecipeIngredients={RecipeIngredients}, CatalogIngredients={CatalogIngredients}, PendingReview={PendingReview}, Categories={Categories}",
        indexResult.SourceKey ?? "all",
        indexResult.ProcessedRecipeVariantCount,
        indexResult.RecipeIngredientCount,
        indexResult.CatalogIngredientCount,
        indexResult.PendingReviewIngredientCount,
        string.Join(
            ", ",
            indexResult.CategoryCounts
                .OrderBy(item => item.Key)
                .Select(item => $"{item.Key}:{item.Value}")));
    return;
}

if (args.Any(argument => string.Equals(
        argument,
        "--research-official-food-ingredient-companies",
        StringComparison.OrdinalIgnoreCase)))
{
    var maxItemsArgument = args.FirstOrDefault(argument =>
        argument.StartsWith("--max-items=", StringComparison.OrdinalIgnoreCase));
    var maxItems = int.TryParse(
        maxItemsArgument?["--max-items=".Length..],
        NumberStyles.Integer,
        CultureInfo.InvariantCulture,
        out var parsedMaxItems)
        ? parsedMaxItems
        : 500;
    var candidatesArgument = args.FirstOrDefault(argument =>
        argument.StartsWith("--candidates-per-ingredient=", StringComparison.OrdinalIgnoreCase));
    var candidatesPerIngredient = int.TryParse(
        candidatesArgument?["--candidates-per-ingredient=".Length..],
        NumberStyles.Integer,
        CultureInfo.InvariantCulture,
        out var parsedCandidatesPerIngredient)
        ? parsedCandidatesPerIngredient
        : 100;
    var refreshDaysArgument = args.FirstOrDefault(argument =>
        argument.StartsWith("--refresh-after-days=", StringComparison.OrdinalIgnoreCase));
    var refreshAfterDays = int.TryParse(
        refreshDaysArgument?["--refresh-after-days=".Length..],
        NumberStyles.Integer,
        CultureInfo.InvariantCulture,
        out var parsedRefreshAfterDays)
        ? parsedRefreshAfterDays
        : 30;
    var delayArgument = args.FirstOrDefault(argument =>
        argument.StartsWith("--request-delay-ms=", StringComparison.OrdinalIgnoreCase));
    var requestDelayMilliseconds = int.TryParse(
        delayArgument?["--request-delay-ms=".Length..],
        NumberStyles.Integer,
        CultureInfo.InvariantCulture,
        out var parsedRequestDelayMilliseconds)
        ? parsedRequestDelayMilliseconds
        : 250;
    var force = args.Any(argument => string.Equals(
        argument,
        "--force",
        StringComparison.OrdinalIgnoreCase));

    await using var scope = app.Services.CreateAsyncScope();
    var archiveDb = scope.ServiceProvider.GetRequiredService<AgriculturalFisheriesDbContext>();
    await archiveDb.Database.MigrateAsync();
    var archiveService = scope.ServiceProvider
        .GetRequiredService<IOfficialFoodIngredientCompanyArchiveService>();
    var result = await archiveService.CollectCatalogAsync(
        new OfficialFoodIngredientCompanyCollectionRequest(
            maxItems,
            candidatesPerIngredient,
            force,
            refreshAfterDays,
            requestDelayMilliseconds));
    app.Logger.LogInformation(
        "공식 음식 재료별 국내외 기업 조사 전산화 완료. Run={RunKey}, Status={Status}, Requested={Requested}, Processed={Processed}, Failed={Failed}, Evidence={Evidence}",
        result.RunKey,
        result.StatusCode,
        result.RequestedIngredientCount,
        result.ProcessedIngredientCount,
        result.FailedIngredientCount,
        result.ObservedEvidenceCount);
    return;
}

if (args.Any(argument => string.Equals(
        argument,
        "--index-official-food-ingredient-prices",
        StringComparison.OrdinalIgnoreCase)))
{
    var maxItemsArgument = args.FirstOrDefault(argument =>
        argument.StartsWith("--max-items=", StringComparison.OrdinalIgnoreCase));
    var maxItems = int.TryParse(
        maxItemsArgument?["--max-items=".Length..],
        NumberStyles.Integer,
        CultureInfo.InvariantCulture,
        out var parsedMaxItems)
        ? parsedMaxItems
        : 5000;
    var force = args.Any(argument => string.Equals(
        argument,
        "--force",
        StringComparison.OrdinalIgnoreCase));

    await using var scope = app.Services.CreateAsyncScope();
    var archiveDb = scope.ServiceProvider.GetRequiredService<AgriculturalFisheriesDbContext>();
    await archiveDb.Database.MigrateAsync();
    var priceService = scope.ServiceProvider
        .GetRequiredService<IOfficialFoodIngredientPublicPriceService>();
    var indexResult = await priceService.RebuildMappingsAsync(
        new OfficialFoodIngredientPriceIndexRequest(maxItems, force));
    app.Logger.LogInformation(
        "공식 음식 재료 공공가격 매핑 완료. Processed={Processed}, MappedIngredients={MappedIngredients}, Mappings={Mappings}, KR={Korean}, US={UnitedStates}, Unmapped={Unmapped}, PricedIngredients={PricedIngredients}, KoreanPrices={KoreanPrices}, UnitedStatesPrices={UnitedStatesPrices}",
        indexResult.ProcessedIngredientCount,
        indexResult.MappedIngredientCount,
        indexResult.MappingCount,
        indexResult.KoreanMappingCount,
        indexResult.UnitedStatesMappingCount,
        indexResult.UnmappedIngredientCount,
        indexResult.PricedIngredientCount,
        indexResult.KoreanPriceCount,
        indexResult.UnitedStatesPriceCount);
    return;
}

if (await 관세청Hsk카탈로그ImportCommandLine.TryRunAsync(args, app.Services, app.Logger))
{
    return;
}

if (args.Any(argument => string.Equals(
        argument,
        "--index-official-food-ingredient-hs-codes",
        StringComparison.OrdinalIgnoreCase)))
{
    var maxItemsArgument = args.FirstOrDefault(argument =>
        argument.StartsWith("--max-items=", StringComparison.OrdinalIgnoreCase));
    var maxItems = int.TryParse(
        maxItemsArgument?["--max-items=".Length..],
        NumberStyles.Integer,
        CultureInfo.InvariantCulture,
        out var parsedMaxItems)
        ? parsedMaxItems
        : 5000;
    var countryCodesArgument = args.FirstOrDefault(argument =>
        argument.StartsWith("--countries=", StringComparison.OrdinalIgnoreCase));
    var countryCodes = countryCodesArgument?["--countries=".Length..]
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(countryCode => countryCode.ToUpperInvariant())
        .Distinct(StringComparer.Ordinal)
        .ToArray();
    var force = args.Any(argument => string.Equals(
        argument,
        "--force",
        StringComparison.OrdinalIgnoreCase));

    await using var scope = app.Services.CreateAsyncScope();
    var archiveDb = scope.ServiceProvider.GetRequiredService<AgriculturalFisheriesDbContext>();
    await archiveDb.Database.MigrateAsync();
    var mappingService = scope.ServiceProvider
        .GetRequiredService<IOfficialFoodIngredientHsMappingService>();
    var result = await mappingService.RebuildAsync(
        new OfficialFoodIngredientHsIndexRequest(maxItems, force, countryCodes));
    app.Logger.LogInformation(
        "공식 음식 재료 HS 후보 연결 완료. Processed={Processed}, Mapped={Mapped}, Candidates={Candidates}, Unmapped={Unmapped}, CatalogVersions={CatalogVersions}, CatalogEntries={CatalogEntries}, Countries={Countries}",
        result.ProcessedIngredientCount,
        result.MappedIngredientCount,
        result.CandidateCount,
        result.UnmappedIngredientCount,
        result.ActiveCatalogVersionCount,
        result.ActiveCatalogEntryCount,
        string.Join(", ", result.CountryCandidateCounts.Select(item => $"{item.Key}:{item.Value}")));
    return;
}

var officialFoodRecipeSourceKey = args.Any(argument =>
        string.Equals(argument, "--collect-mfds-recipes", StringComparison.OrdinalIgnoreCase))
    ? OfficialFoodRecipeSourceKeys.MfdsCookRecipe
    : args.Any(argument =>
        string.Equals(argument, "--collect-rda-local-food", StringComparison.OrdinalIgnoreCase))
        ? OfficialFoodRecipeSourceKeys.RdaLocalFood
        : args.Any(argument =>
            string.Equals(argument, "--collect-maff-regional-cuisines", StringComparison.OrdinalIgnoreCase))
            ? OfficialFoodRecipeSourceKeys.MaffRegionalCuisine
            : args.Any(argument =>
                string.Equals(argument, "--collect-nhs-recipes", StringComparison.OrdinalIgnoreCase))
                ? OfficialFoodRecipeSourceKeys.NhsHealthierFamilies
                : args.Any(argument =>
                    string.Equals(argument, "--collect-official-food-recipes", StringComparison.OrdinalIgnoreCase))
                    ? args.FirstOrDefault(argument =>
                            argument.StartsWith("--source=", StringComparison.OrdinalIgnoreCase))?
                        ["--source=".Length..]
                    : null;
if (!string.IsNullOrWhiteSpace(officialFoodRecipeSourceKey))
{
    var maxPagesArgument = args.FirstOrDefault(argument =>
        argument.StartsWith("--max-pages=", StringComparison.OrdinalIgnoreCase));
    var maxPages = int.TryParse(
        maxPagesArgument?["--max-pages=".Length..],
        NumberStyles.Integer,
        CultureInfo.InvariantCulture,
        out var parsedMaxPages)
        ? parsedMaxPages
        : 1;
    var maxItemsArgument = args.FirstOrDefault(argument =>
        argument.StartsWith("--max-items=", StringComparison.OrdinalIgnoreCase));
    var maxItems = int.TryParse(
        maxItemsArgument?["--max-items=".Length..],
        NumberStyles.Integer,
        CultureInfo.InvariantCulture,
        out var parsedMaxItems)
        ? parsedMaxItems
        : 100;

    await using var scope = app.Services.CreateAsyncScope();
    var archiveDb = scope.ServiceProvider.GetRequiredService<AgriculturalFisheriesDbContext>();
    await archiveDb.Database.MigrateAsync();
    var archiveService = scope.ServiceProvider.GetRequiredService<IOfficialFoodRecipeArchiveService>();
    var archiveResult = await archiveService.CollectAsync(new OfficialFoodRecipeCollectionRequest(
        officialFoodRecipeSourceKey,
        maxPages,
        maxItems));
    app.Logger.LogInformation(
        "공식 음식 레시피 DB 저장 완료. Source={SourceKey}, RunId={RunId}, Fetched={Fetched}, Inserted={Inserted}, Updated={Updated}, Existing={Existing}",
        archiveResult.SourceKey,
        archiveResult.CollectionRunId,
        archiveResult.FetchedCount,
        archiveResult.InsertedCount,
        archiveResult.UpdatedCount,
        archiveResult.ExistingCount);
    return;
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    var localStorageRoot = Path.Combine(
        app.Environment.ContentRootPath,
        DevelopmentLocalStorageService.PublicStorageDirectoryName);
    Directory.CreateDirectory(localStorageRoot);
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(localStorageRoot),
        RequestPath = DevelopmentLocalStorageService.PublicRequestPath
    });
}

if (databaseInitializationOptions.RunAtStartup || initializeDatabaseOnly)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<SsalddelContext>();
    await DatabaseCompatibilityInitializer.InitializeAsync(
        db,
        scope.ServiceProvider,
        app.Environment,
        app.Logger,
        databaseInitializationOptions.FailOnError || initializeDatabaseOnly);
}
else
{
    app.Logger.LogInformation(
        "Database initialization at startup is disabled. Run the application once with --initialize-database during deployment.");
}

if (initializeDatabaseOnly)
{
    app.Logger.LogInformation("Database initialization command completed.");
    return;
}

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

if (!isRunningInContainer)
{
    app.UseHttpsRedirection();
}

app.UseSerilogRequestLogging(options =>
{
    options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("TraceId", httpContext.TraceIdentifier);
        diagnosticContext.Set("UserName", httpContext.User?.Identity?.Name ?? string.Empty);
    };
});

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;

        if (exception is ValidationException validationException)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.Response.ContentType = "application/problem+json";

            var errors = validationException.Errors
                .GroupBy(x => x.PropertyName)
                .ToDictionary(
                    x => x.Key,
                    x => x.Select(e => e.ErrorMessage).ToArray());

            var problem = new ValidationProblemDetails(errors)
            {
                Title = "요청값 검증에 실패했습니다.",
                Status = StatusCodes.Status400BadRequest
            };
            problem.Extensions["errorCode"] = "ValidationFailed";
            problem.Extensions["traceId"] = context.TraceIdentifier;

            await context.Response.WriteAsJsonAsync(problem);
            return;
        }

        if (exception is InvalidOperationException invalidOperationException)
        {
            var failure = Result응답확장.실패분류(invalidOperationException.Message);
            context.Response.StatusCode = failure.StatusCode;
            context.Response.ContentType = "application/problem+json";

            var problem = new ProblemDetails
            {
                Title = invalidOperationException.Message,
                Status = failure.StatusCode,
                Type = failure.Type,
                Detail = failure.Detail,
                Instance = context.Request.Path.Value
            };
            problem.Extensions["errors"] = new[] { invalidOperationException.Message };
            problem.Extensions["errorCode"] = failure.Code;
            problem.Extensions["traceId"] = context.TraceIdentifier;

            await context.Response.WriteAsJsonAsync(problem);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/problem+json";
        var serverProblem = new ProblemDetails
        {
            Title = "서버 오류가 발생했습니다.",
            Status = StatusCodes.Status500InternalServerError,
            Type = "https://httpstatuses.com/500",
            Detail = "예상하지 못한 서버 오류입니다. traceId로 서버 로그를 확인해야 합니다.",
            Instance = context.Request.Path.Value
        };
        serverProblem.Extensions["errorCode"] = "ServerError";
        serverProblem.Extensions["traceId"] = context.TraceIdentifier;
        await context.Response.WriteAsJsonAsync(serverProblem);
    });
});

app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseCors(CustomsWebCorsPolicy);
app.UseAuthorization();

app.UseMiddleware<IsmsPEncryptedTransportMiddleware>();
app.UseMiddleware<HrRoleAccessMiddleware>();
app.UseMiddleware<사용자행위로그Middleware>();
app.MapControllers();
Ssalddel.Services.Development.FoodObserver.음식배달관찰검증Hosting.Map음식배달관찰검증(app);
app.MapHub<DispatchRecommendationHub>(
    Ssalddel.Contracts.Common.Drivers.DriverDispatchRealtimeContract.HubPath);
app.MapHub<RestaurantOrderHub>("/hubs/restaurant-orders");
app.MapHub<TransportRequestLedgerHub>(TransportRequestLedgerRealtime.HubPath);
app.MapHub<DiagramCollaborationHub>(DiagramCollaborationHub.HubPath);
app.MapHub<SimulationOnlineWorldHub>(SimulationOnlineWorldHub.HubPath);
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("live")
}).AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready")
}).AllowAnonymous();

app.Run();
