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
var salesOrderSyncOptions = builder.Configuration.GetSection(SalesChannelOrderSyncOptions.SectionName).Get<SalesChannelOrderSyncOptions>() ?? new SalesChannelOrderSyncOptions();
var youTubeOptions = builder.Configuration.GetSection(YouTubeOptions.SectionName).Get<YouTubeOptions>() ?? new YouTubeOptions();
var hongikHakdangCardOptions = builder.Configuration.GetSection(HongikHakdangCardOptions.SectionName).Get<HongikHakdangCardOptions>() ?? new HongikHakdangCardOptions();
var agriculturalFisheriesBatchOptions = builder.Configuration.GetSection(AgriculturalFisheriesBatchOptions.SectionName).Get<AgriculturalFisheriesBatchOptions>() ?? new AgriculturalFisheriesBatchOptions();
var communityEditorialBatchOptions = builder.Configuration.GetSection(CommunityEditorialBatchOptions.SectionName).Get<CommunityEditorialBatchOptions>() ?? new CommunityEditorialBatchOptions();
var databaseInitializationOptions = builder.Configuration.GetSection(DatabaseInitializationOptions.SectionName).Get<DatabaseInitializationOptions>() ?? new DatabaseInitializationOptions();
var initializeDatabaseOnly = args.Any(argument =>
    string.Equals(argument, "--initialize-database", StringComparison.OrdinalIgnoreCase));

builder.Services.AddSsalddelBackgroundJobs(dispatchQueueJobOptions, salesOrderSyncOptions, youTubeOptions, hongikHakdangCardOptions, agriculturalFisheriesBatchOptions, communityEditorialBatchOptions, executionOptions);
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

if (args.Any(argument => string.Equals(
        argument,
        "--inspect-agricultural-fisheries-product-codes",
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
    if (inspectionYear is < 1990 or > 2100)
    {
        throw new ArgumentOutOfRangeException(
            nameof(inspectionYear),
            "조사 연도는 1990년부터 2100년 사이여야 합니다.");
    }

    var inspectionStartDate = new DateOnly(inspectionYear, 1, 1);
    var inspectionEndDateExclusive = inspectionStartDate.AddYears(1);
    await using var scope = app.Services.CreateAsyncScope();
    var archiveDb = scope.ServiceProvider.GetRequiredService<AgriculturalFisheriesDbContext>();
    var latestKamisSurveyDate = await archiveDb.KamisPriceObservations
        .AsNoTracking()
        .MaxAsync(item => (DateOnly?)item.SurveyDate);
    var kamisItemCodes = await archiveDb.KamisPriceObservations
        .AsNoTracking()
        .Where(item => item.ItemCode != string.Empty)
        .GroupBy(item => new
        {
            item.CategoryCode,
            item.CategoryName,
            item.ItemCode,
            item.ItemName
        })
        .Select(group => new
        {
            group.Key.CategoryCode,
            group.Key.CategoryName,
            group.Key.ItemCode,
            group.Key.ItemName,
            LatestSurveyDate = group.Max(item => item.SurveyDate),
            KindCodeCount = group.Select(item => item.KindCode)
                .Where(code => code != string.Empty)
                .Distinct()
                .Count()
        })
        .OrderBy(item => item.CategoryCode)
        .ThenBy(item => item.ItemCode)
        .ToListAsync();
    var focusNames = new[] { "사과", "감자", "양파", "쌀", "콩", "대두", "고등어" };
    var focusProducts = kamisItemCodes
        .Where(item => focusNames.Contains(item.ItemName, StringComparer.Ordinal))
        .ToArray();
    var kamisCategories = kamisItemCodes
        .GroupBy(item => new { item.CategoryCode, item.CategoryName })
        .Select(group => new
        {
            group.Key.CategoryCode,
            group.Key.CategoryName,
            ItemCodeCount = group.Count()
        })
        .OrderBy(item => item.CategoryCode)
        .ToArray();
    var kamisConflictingItemCodes = kamisItemCodes
        .GroupBy(item => item.ItemCode)
        .Where(group => group.Select(item => item.ItemName).Distinct().Count() > 1)
        .Select(group => group.Key)
        .ToList();
    var kamisCodeNameConflictDetails = await archiveDb.KamisPriceObservations
        .AsNoTracking()
        .Where(item => kamisConflictingItemCodes.Contains(item.ItemCode))
        .GroupBy(item => new
        {
            item.ItemCode,
            item.ItemName,
            item.KindCode,
            item.KindName,
            item.FrequencyCode,
            item.FirstCollectionRunId
        })
        .Select(group => new
        {
            group.Key.ItemCode,
            group.Key.ItemName,
            group.Key.KindCode,
            group.Key.KindName,
            group.Key.FrequencyCode,
            group.Key.FirstCollectionRunId,
            ObservationCount = group.Count(),
            FirstSurveyDate = group.Min(item => item.SurveyDate),
            LatestSurveyDate = group.Max(item => item.SurveyDate),
            LatestSeenAtUtc = group.Max(item => item.LastSeenAtUtc)
        })
        .OrderBy(item => item.ItemCode)
        .ThenBy(item => item.ItemName)
        .ThenBy(item => item.KindCode)
        .ToListAsync();
    var kamisCodeNameConflictSampleCandidates = await archiveDb.KamisPriceObservations
        .AsNoTracking()
        .Where(item => kamisConflictingItemCodes.Contains(item.ItemCode))
        .OrderBy(item => item.ItemCode)
        .ThenBy(item => item.ItemName)
        .ThenBy(item => item.SurveyDate)
        .Select(item => new
        {
            item.Id,
            item.FirstCollectionRunId,
            item.ItemCode,
            item.ItemName,
            item.KindCode,
            item.KindName,
            item.SurveyDate,
            item.RawJson
        })
        .ToListAsync();
    var kamisCodeNameConflictSamples = kamisCodeNameConflictSampleCandidates
        .GroupBy(item => new { item.ItemCode, item.ItemName })
        .SelectMany(group => group.Take(3))
        .ToArray();
    var kamisYearQuery = archiveDb.KamisPriceObservations
        .AsNoTracking()
        .Where(item => item.SurveyDate >= inspectionStartDate
                       && item.SurveyDate < inspectionEndDateExclusive);
    var kamisYearObservationCount = await kamisYearQuery.CountAsync();
    var kamisYearPricedObservationCount = await kamisYearQuery
        .CountAsync(item => item.PriceKrw.HasValue);
    var kamisYearSurveyDateCount = await kamisYearQuery
        .Select(item => item.SurveyDate)
        .Distinct()
        .CountAsync();
    var kamisYearFrequencyBreakdown = await kamisYearQuery
        .GroupBy(item => item.FrequencyCode)
        .Select(group => new
        {
            FrequencyCode = group.Key,
            ObservationCount = group.Count()
        })
        .OrderBy(item => item.FrequencyCode)
        .ToListAsync();
    var kamisYearCoverage = await kamisYearQuery
        .GroupBy(item => new
        {
            item.CategoryCode,
            item.CategoryName,
            item.ItemCode,
            item.ItemName
        })
        .Select(group => new
        {
            group.Key.CategoryCode,
            group.Key.CategoryName,
            group.Key.ItemCode,
            group.Key.ItemName,
            ObservationCount = group.Count(),
            PricedObservationCount = group.Count(item => item.PriceKrw.HasValue),
            MissingPriceObservationCount = group.Count(item => !item.PriceKrw.HasValue),
            FirstSurveyDate = group.Min(item => item.SurveyDate),
            LatestSurveyDate = group.Max(item => item.SurveyDate),
            SurveyDateCount = group.Select(item => item.SurveyDate).Distinct().Count(),
            KindCodeCount = group.Select(item => item.KindCode).Distinct().Count(),
            RetailObservationCount = group.Count(item => item.ProductClassCode == "01"),
            WholesaleObservationCount = group.Count(item => item.ProductClassCode == "02")
        })
        .OrderBy(item => item.CategoryCode)
        .ThenBy(item => item.ItemCode)
        .ToListAsync();
    var kamisYearCoverageKeys = kamisYearCoverage
        .Select(item => (item.CategoryCode, item.ItemCode))
        .ToHashSet();
    var kamisCodesWithoutYearObservation = kamisItemCodes
        .Where(item => !kamisYearCoverageKeys.Contains((item.CategoryCode, item.ItemCode)))
        .Select(item => new
        {
            item.CategoryCode,
            item.CategoryName,
            item.ItemCode,
            item.ItemName
        })
        .ToArray();

    var latestAuctionSettlementDate = await archiveDb.DomesticAuctionPriceObservations
        .AsNoTracking()
        .MaxAsync(item => (DateOnly?)item.SettlementDate);
    var auctionObservationCount = await archiveDb.DomesticAuctionPriceObservations
        .AsNoTracking()
        .CountAsync();
    var auctionCorporationItemCodeCount = await archiveDb.DomesticAuctionPriceObservations
        .AsNoTracking()
        .Where(item => item.CorporationItemCode != string.Empty)
        .Select(item => new { item.CorporationCode, item.CorporationItemCode })
        .Distinct()
        .CountAsync();
    var auctionItemNameCount = await archiveDb.DomesticAuctionPriceObservations
        .AsNoTracking()
        .Where(item => item.ItemName != string.Empty)
        .Select(item => item.ItemName)
        .Distinct()
        .CountAsync();

    var hsUsdaMappings = await archiveDb.HsCommodityMappings
        .AsNoTracking()
        .Where(item => item.IsActive)
        .OrderBy(item => item.HsCode6)
        .Select(item => new
        {
            item.HsCode6,
            item.ProductNameKo,
            item.UsdaCommodityDesc,
            item.MatchQualityCode,
            item.ReviewStatusCode
        })
        .ToListAsync();
    var usdaYearQuery = archiveDb.PriceObservations
        .AsNoTracking()
        .Where(item => item.Year == inspectionYear);
    var usdaYearObservationCount = await usdaYearQuery.CountAsync();
    var usdaYearNumericObservationCount = await usdaYearQuery
        .CountAsync(item => item.NumericValue.HasValue);
    var usdaYearSuppressedObservationCount = await usdaYearQuery
        .CountAsync(item => item.IsSuppressed);
    var usdaYearCommodityCoverage = await usdaYearQuery
        .GroupBy(item => item.CommodityDesc)
        .Select(group => new
        {
            CommodityDesc = group.Key,
            ObservationCount = group.Count(),
            NumericObservationCount = group.Count(item => item.NumericValue.HasValue),
            SuppressedObservationCount = group.Count(item => item.IsSuppressed),
            UnitCount = group.Select(item => item.UnitDesc).Distinct().Count(),
            ReferencePeriodCount = group.Select(item => item.ReferencePeriodDesc).Distinct().Count(),
            LatestSourceLoadTimeUtc = group.Max(item => item.SourceLoadTimeUtc)
        })
        .OrderBy(item => item.CommodityDesc)
        .ToListAsync();
    var usdaYearReferencePeriods = await usdaYearQuery
        .GroupBy(item => new
        {
            item.CommodityDesc,
            item.ReferencePeriodDesc
        })
        .Select(group => new
        {
            group.Key.CommodityDesc,
            group.Key.ReferencePeriodDesc,
            ObservationCount = group.Count(),
            NumericObservationCount = group.Count(item => item.NumericValue.HasValue),
            SuppressedObservationCount = group.Count(item => item.IsSuppressed)
        })
        .OrderBy(item => item.CommodityDesc)
        .ThenBy(item => item.ReferencePeriodDesc)
        .ToListAsync();
    var mappedUsdaCommodityNames = hsUsdaMappings
        .Select(item => item.UsdaCommodityDesc)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);
    var mappedUsdaCommodityCoverage = usdaYearCommodityCoverage
        .Where(item => mappedUsdaCommodityNames.Contains(item.CommodityDesc))
        .ToArray();
    var usdaYearCoverageByCommodity = usdaYearCommodityCoverage
        .ToDictionary(item => item.CommodityDesc, StringComparer.OrdinalIgnoreCase);
    var kamisToUsdaCommodityAliases =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["쌀"] = ["RICE"],
            ["콩"] = ["SOYBEANS"],
            ["감자"] = ["POTATOES"],
            ["양파"] = ["ONIONS"],
            ["토마토"] = ["TOMATOES"],
            ["딸기"] = ["STRAWBERRIES"],
            ["땅콩"] = ["PEANUTS"],
            ["브로콜리"] = ["BROCCOLI"],
            ["오이"] = ["CUCUMBERS"],
            ["상추"] = ["LETTUCE"],
            ["멜론"] = ["MELONS"],
            ["사과"] = ["APPLES"],
            ["배"] = ["PEARS"],
            ["복숭아"] = ["PEACHES"],
            ["포도"] = ["GRAPES"],
            ["오렌지"] = ["ORANGES"],
            ["레몬"] = ["LEMONS"],
            ["소"] = ["CATTLE"],
            ["돼지"] = ["HOGS"],
            ["닭"] = ["CHICKENS"],
            ["계란"] = ["EGGS"],
            ["우유"] = ["MILK"]
        };
    var kamisUsdaCrosswalk = kamisItemCodes
        .Select(kamisItem =>
        {
            var commodityAliases = kamisToUsdaCommodityAliases.TryGetValue(
                    kamisItem.ItemName,
                    out var aliases)
                ? aliases.ToHashSet(StringComparer.OrdinalIgnoreCase)
                : [];

            var candidateMappings = hsUsdaMappings
                .Where(mapping =>
                    string.Equals(
                        mapping.ProductNameKo,
                        kamisItem.ItemName,
                        StringComparison.Ordinal)
                    || commodityAliases.Contains(mapping.UsdaCommodityDesc))
                .Select(mapping =>
                {
                    usdaYearCoverageByCommodity.TryGetValue(
                        mapping.UsdaCommodityDesc,
                        out var commodityCoverage);
                    return new
                    {
                        mapping.HsCode6,
                        mapping.ProductNameKo,
                        mapping.UsdaCommodityDesc,
                        mapping.MatchQualityCode,
                        mapping.ReviewStatusCode,
                        UsdaYearObservationCount =
                            commodityCoverage?.ObservationCount ?? 0,
                        UsdaYearNumericObservationCount =
                            commodityCoverage?.NumericObservationCount ?? 0,
                        ReferencePeriods = usdaYearReferencePeriods
                            .Where(period => string.Equals(
                                period.CommodityDesc,
                                mapping.UsdaCommodityDesc,
                                StringComparison.OrdinalIgnoreCase))
                            .ToArray()
                    };
                })
                .ToArray();
            var usdaCommodityCandidates = commodityAliases
                .OrderBy(commodityDesc => commodityDesc)
                .Select(commodityDesc =>
                {
                    usdaYearCoverageByCommodity.TryGetValue(
                        commodityDesc,
                        out var commodityCoverage);
                    return new
                    {
                        CommodityDesc = commodityDesc,
                        UsdaYearObservationCount =
                            commodityCoverage?.ObservationCount ?? 0,
                        UsdaYearNumericObservationCount =
                            commodityCoverage?.NumericObservationCount ?? 0,
                        UsdaYearSuppressedObservationCount =
                            commodityCoverage?.SuppressedObservationCount ?? 0,
                        ReferencePeriods = usdaYearReferencePeriods
                            .Where(period => string.Equals(
                                period.CommodityDesc,
                                commodityDesc,
                                StringComparison.OrdinalIgnoreCase))
                            .ToArray()
                    };
                })
                .ToArray();

            return new
            {
                kamisItem.CategoryCode,
                kamisItem.CategoryName,
                kamisItem.ItemCode,
                kamisItem.ItemName,
                CandidateMappingCount = candidateMappings.Length,
                UsdaCommodityCandidateCount = usdaCommodityCandidates.Length,
                HasUsdaYearObservation = usdaCommodityCandidates.Any(candidate =>
                    candidate.UsdaYearObservationCount > 0),
                UsdaCommodityCandidates = usdaCommodityCandidates,
                CandidateMappings = candidateMappings
            };
        })
        .ToArray();

    var blsArchiveService =
        scope.ServiceProvider.GetRequiredService<IBls평균소매가격ArchiveService>();
    var blsKamisComparison = blsArchiveService.GetKamisComparisonCatalog();
    var blsYearQuery = archiveDb.BlsAverageRetailPriceObservations
        .AsNoTracking()
        .Where(item =>
            item.ReferenceMonth >= inspectionStartDate
            && item.ReferenceMonth < inspectionEndDateExclusive);
    var blsYearObservationCount = await blsYearQuery.CountAsync();
    var blsYearNumericObservationCount =
        await blsYearQuery.CountAsync(item => item.PriceUsd.HasValue);
    var blsYearReferenceMonthCount = await blsYearQuery
        .Select(item => item.ReferenceMonth)
        .Distinct()
        .CountAsync();
    var blsYearLatestReferenceMonth = await blsYearQuery
        .MaxAsync(item => (DateOnly?)item.ReferenceMonth);
    var blsYearCoverage = await blsYearQuery
        .GroupBy(item => new
        {
            item.SeriesId,
            item.ItemCode,
            item.CanonicalProductKey,
            item.ProductNameKo,
            item.OriginalUnit
        })
        .Select(group => new
        {
            group.Key.SeriesId,
            group.Key.ItemCode,
            group.Key.CanonicalProductKey,
            group.Key.ProductNameKo,
            group.Key.OriginalUnit,
            ObservationCount = group.Count(),
            NumericObservationCount = group.Count(item => item.PriceUsd.HasValue),
            MissingObservationCount = group.Count(item => !item.PriceUsd.HasValue),
            FirstReferenceMonth = group.Min(item => item.ReferenceMonth),
            LatestReferenceMonth = group.Max(item => item.ReferenceMonth)
        })
        .OrderBy(item => item.SeriesId)
        .ToListAsync();
    var blsYearSeriesIds = blsYearCoverage
        .Select(item => item.SeriesId)
        .ToHashSet(StringComparer.Ordinal);
    var blsSeriesWithoutYearObservation = blsKamisComparison.Items
        .Where(item => !blsYearSeriesIds.Contains(item.SeriesId))
        .Select(item => new
        {
            item.SeriesId,
            item.BlsItemCode,
            item.CanonicalProductKey,
            item.BlsProductNameKo,
            item.MappingStatusCode
        })
        .ToArray();
    var blsKamisCandidateRows = blsKamisComparison.Items
        .SelectMany(series => series.KamisCandidates.Select(candidate => new
        {
            series.SeriesId,
            series.BlsItemCode,
            series.CanonicalProductKey,
            series.BlsProductNameKo,
            series.BlsOriginalUnit,
            candidate.KamisCategoryCode,
            candidate.KamisItemCode,
            candidate.MatchQualityCode,
            candidate.ReviewStatusCode,
            candidate.AllowsDirectPriceComparison,
            candidate.ReviewNote
        }))
        .ToArray();
    var kamisBlsCrosswalk = kamisItemCodes
        .Select(kamisItem => new
        {
            kamisItem.CategoryCode,
            kamisItem.CategoryName,
            kamisItem.ItemCode,
            kamisItem.ItemName,
            BlsCandidates = blsKamisCandidateRows
                .Where(candidate =>
                    candidate.KamisCategoryCode == kamisItem.CategoryCode
                    && candidate.KamisItemCode == kamisItem.ItemCode)
                .OrderBy(candidate => candidate.SeriesId)
                .ToArray()
        })
        .Select(item => new
        {
            item.CategoryCode,
            item.CategoryName,
            item.ItemCode,
            item.ItemName,
            CandidateSeriesCount = item.BlsCandidates.Length,
            HasDirectComparableCandidate = item.BlsCandidates.Any(candidate =>
                candidate.AllowsDirectPriceComparison),
            item.BlsCandidates
        })
        .ToArray();

    Console.WriteLine(JsonSerializer.Serialize(
        new
        {
            Kamis = new
            {
                LatestSurveyDate = latestKamisSurveyDate,
                ItemCodeCount = kamisItemCodes.Count,
                Categories = kamisCategories,
                WeeklyComparisonProducts = focusProducts,
                AllItemCodes = kamisItemCodes,
                CodeNameIntegrity = new
                {
                    ConflictingItemCodeCount = kamisConflictingItemCodes.Count,
                    ConflictingItemCodes = kamisConflictingItemCodes,
                    ConflictDetails = kamisCodeNameConflictDetails,
                    ConflictSamples = kamisCodeNameConflictSamples
                },
                YearCoverage = new
                {
                    Year = inspectionYear,
                    ObservationCount = kamisYearObservationCount,
                    PricedObservationCount = kamisYearPricedObservationCount,
                    MissingPriceObservationCount =
                        kamisYearObservationCount - kamisYearPricedObservationCount,
                    SurveyDateCount = kamisYearSurveyDateCount,
                    ItemCodeWithObservationCount = kamisYearCoverage.Count,
                    ItemCodeWithoutObservationCount = kamisCodesWithoutYearObservation.Length,
                    FrequencyBreakdown = kamisYearFrequencyBreakdown,
                    ItemCoverage = kamisYearCoverage,
                    ItemCodesWithoutObservation = kamisCodesWithoutYearObservation,
                    UnitBoundary =
                        "Period 관측은 KAMIS p_convert_kg_yn=Y에 따른 1kg 환산값이며 원 거래단위 관측과 FrequencyCode·Unit으로 구분합니다."
                }
            },
            DomesticAuction = new
            {
                LatestSettlementDate = latestAuctionSettlementDate,
                ObservationCount = auctionObservationCount,
                CorporationItemCodeCount = auctionCorporationItemCodeCount,
                ItemNameCount = auctionItemNameCount,
                CodeBoundary =
                    "법인품목코드는 도매시장 법인별 코드이며 KAMIS·HS의 전국 공통 품목코드가 아닙니다."
            },
            HsUsda = new
            {
                MappingCount = hsUsdaMappings.Count,
                ReviewedCount = hsUsdaMappings.Count(item =>
                    item.ReviewStatusCode == HsUsdaMappingReviewStatusCodes.Reviewed),
                Mappings = hsUsdaMappings,
                YearCoverage = new
                {
                    Year = inspectionYear,
                    ObservationCount = usdaYearObservationCount,
                    NumericObservationCount = usdaYearNumericObservationCount,
                    SuppressedObservationCount = usdaYearSuppressedObservationCount,
                    CommodityCount = usdaYearCommodityCoverage.Count,
                    CommodityCoverage = usdaYearCommodityCoverage,
                    ReferencePeriodCoverage = usdaYearReferencePeriods,
                    CandidateMappedCommodityCoverage = mappedUsdaCommodityCoverage,
                    KamisItemCodeCrosswalk = new
                    {
                        KamisItemCodeCount = kamisUsdaCrosswalk.Length,
                        CandidateHsMappedKamisItemCodeCount = kamisUsdaCrosswalk.Count(item =>
                            item.CandidateMappingCount > 0),
                        DirectCommodityMappedKamisItemCodeCount = kamisUsdaCrosswalk.Count(item =>
                            item.UsdaCommodityCandidateCount > 0),
                        KamisItemCodeWithUsdaObservationCount = kamisUsdaCrosswalk.Count(item =>
                            item.HasUsdaYearObservation),
                        UnmappedKamisItemCodeCount = kamisUsdaCrosswalk.Count(item =>
                            item.UsdaCommodityCandidateCount == 0),
                        Items = kamisUsdaCrosswalk
                    },
                    MappingBoundary =
                        "HS-USDA 매핑은 검토 후보이며 KAMIS 품목코드와 동일한 코드 체계가 아닙니다. KAMIS는 도·소매 관측이고 USDA PRICE RECEIVED는 생산자 수취가격이므로 가격 수준을 직접 비교하지 않습니다."
                }
            },
            BlsAverageRetail = new
            {
                blsKamisComparison.BlsCatalogObservedAt,
                blsKamisComparison.BlsSeriesCount,
                blsKamisComparison.SeriesWithCandidateCount,
                blsKamisComparison.DirectComparableCandidateSeriesCount,
                blsKamisComparison.UniqueKamisItemCodeCount,
                Crosswalk = blsKamisComparison.Items,
                blsKamisComparison.ComparisonBoundaries,
                KamisItemCodeCoverage = new
                {
                    KamisItemCodeCount = kamisBlsCrosswalk.Length,
                    KamisItemCodeWithBlsCandidateCount = kamisBlsCrosswalk.Count(item =>
                        item.CandidateSeriesCount > 0),
                    KamisItemCodeWithoutBlsCandidateCount = kamisBlsCrosswalk.Count(item =>
                        item.CandidateSeriesCount == 0),
                    Items = kamisBlsCrosswalk
                },
                YearCoverage = new
                {
                    Year = inspectionYear,
                    ObservationCount = blsYearObservationCount,
                    NumericObservationCount = blsYearNumericObservationCount,
                    ReferenceMonthCount = blsYearReferenceMonthCount,
                    LatestReferenceMonth = blsYearLatestReferenceMonth,
                    SeriesWithObservationCount = blsYearCoverage.Count,
                    SeriesWithoutObservationCount =
                        blsSeriesWithoutYearObservation.Length,
                    Items = blsYearCoverage,
                    SeriesWithoutObservation = blsSeriesWithoutYearObservation,
                    CollectionBoundary =
                        "2026년은 진행 중이므로 현재 BLS 공개 월까지만 수집하며 이후 월은 같은 RecordKey 규칙으로 누적합니다."
                }
            }
        },
        new JsonSerializerOptions { WriteIndented = true }));
    return;
}

var youTubeCountrySyncArgument = args.FirstOrDefault(argument =>
    argument.StartsWith("--sync-youtube-country=", StringComparison.OrdinalIgnoreCase));
if (youTubeCountrySyncArgument is not null)
{
    var countryCode = youTubeCountrySyncArgument["--sync-youtube-country=".Length..];
    if (string.IsNullOrWhiteSpace(countryCode))
    {
        throw new InvalidOperationException(
            "--sync-youtube-country에는 국가 코드가 필요합니다. 예: --sync-youtube-country=JP");
    }

    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<SsalddelContext>();
    await db.Database.MigrateAsync();
    var service = scope.ServiceProvider.GetRequiredService<IYouTube채널감시Service>();
    var result = await service.국가별동기화Async(countryCode, CancellationToken.None);
    app.Logger.LogInformation(
        "YouTube 국가별 일회성 동기화 완료. CountryCode={CountryCode}, Executed={Executed}, Channels={Channels}, Received={Received}, Added={Added}, NewUploads={NewUploads}, Message={Message}",
        result.국가코드,
        result.동기화결과.실행됨,
        result.동기화결과.처리채널수,
        result.동기화결과.수신영상수,
        result.동기화결과.추가영상수,
        result.동기화결과.신규업로드수,
        result.동기화결과.메시지);
    return;
}

if (args.Any(argument =>
        string.Equals(
            argument,
            "--collect-bls-average-retail-prices",
            StringComparison.OrdinalIgnoreCase)))
{
    var yearFromArgument = args.FirstOrDefault(argument =>
        argument.StartsWith("--year-from=", StringComparison.OrdinalIgnoreCase));
    var yearFrom = int.TryParse(
        yearFromArgument?["--year-from=".Length..],
        NumberStyles.None,
        CultureInfo.InvariantCulture,
        out var parsedYearFrom)
        ? parsedYearFrom
        : DateTime.UtcNow.Year;
    var yearToArgument = args.FirstOrDefault(argument =>
        argument.StartsWith("--year-to=", StringComparison.OrdinalIgnoreCase));
    var yearTo = int.TryParse(
        yearToArgument?["--year-to=".Length..],
        NumberStyles.None,
        CultureInfo.InvariantCulture,
        out var parsedYearTo)
        ? parsedYearTo
        : yearFrom;

    await using var scope = app.Services.CreateAsyncScope();
    var archiveDb = scope.ServiceProvider.GetRequiredService<AgriculturalFisheriesDbContext>();
    await archiveDb.Database.MigrateAsync();
    var archiveService =
        scope.ServiceProvider.GetRequiredService<IBls평균소매가격ArchiveService>();
    var archiveResult = await archiveService.CollectAsync(
        new Bls평균소매가격수집요청
        {
            YearFrom = yearFrom,
            YearTo = yearTo
        });
    app.Logger.LogInformation(
        "BLS 미국 평균 소매가격 DB 저장 완료. Range={YearFrom}-{YearTo}, Series={Series}, RunId={RunId}, Fetched={Fetched}, Inserted={Inserted}, Updated={Updated}, Existing={Existing}, LatestReferenceMonth={LatestReferenceMonth}",
        archiveResult.YearFrom,
        archiveResult.YearTo,
        archiveResult.RequestedSeriesCount,
        archiveResult.CollectionRunId,
        archiveResult.FetchedCount,
        archiveResult.InsertedCount,
        archiveResult.UpdatedCount,
        archiveResult.ExistingCount,
        archiveResult.LatestReferenceMonth);
    return;
}

if (args.Any(argument =>
        string.Equals(
            argument,
            "--collect-international-agricultural-prices",
            StringComparison.OrdinalIgnoreCase)))
{
    var sourceArgument = args.FirstOrDefault(argument =>
        argument.StartsWith("--source=", StringComparison.OrdinalIgnoreCase));
    var sourceKey = sourceArgument?["--source=".Length..]?.Trim();
    if (string.IsNullOrWhiteSpace(sourceKey))
    {
        throw new InvalidOperationException(
            "--collect-international-agricultural-prices에는 --source가 필요합니다.");
    }

    var yearFromArgument = args.FirstOrDefault(argument =>
        argument.StartsWith("--year-from=", StringComparison.OrdinalIgnoreCase));
    var yearFrom = int.TryParse(
        yearFromArgument?["--year-from=".Length..],
        NumberStyles.None,
        CultureInfo.InvariantCulture,
        out var parsedYearFrom)
        ? parsedYearFrom
        : 0;
    var yearToArgument = args.FirstOrDefault(argument =>
        argument.StartsWith("--year-to=", StringComparison.OrdinalIgnoreCase));
    var yearTo = int.TryParse(
        yearToArgument?["--year-to=".Length..],
        NumberStyles.None,
        CultureInfo.InvariantCulture,
        out var parsedYearTo)
        ? parsedYearTo
        : yearFrom;

    await using var scope = app.Services.CreateAsyncScope();
    var archiveDb = scope.ServiceProvider
        .GetRequiredService<AgriculturalFisheriesDbContext>();
    await archiveDb.Database.MigrateAsync();
    var archiveService =
        scope.ServiceProvider.GetRequiredService<I국제농수산가격ArchiveService>();
    var archiveResult = await archiveService.CollectAsync(
        new 국제농수산가격수집요청
        {
            SourceKey = sourceKey,
            YearFrom = yearFrom,
            YearTo = yearTo
        });
    app.Logger.LogInformation(
        "국제 농수산 가격 DB 저장 완료. Source={SourceKey}, Range={YearFrom}-{YearTo}, RunId={RunId}, Fetched={Fetched}, Inserted={Inserted}, Updated={Updated}, Existing={Existing}, LatestReferenceDate={LatestReferenceDate}",
        archiveResult.SourceKey,
        archiveResult.YearFrom,
        archiveResult.YearTo,
        archiveResult.CollectionRunId,
        archiveResult.FetchedCount,
        archiveResult.InsertedCount,
        archiveResult.UpdatedCount,
        archiveResult.ExistingCount,
        archiveResult.LatestReferenceDate);
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

if (args.Any(argument => string.Equals(
        argument,
        "--import-kcs-hsk-catalog",
        StringComparison.OrdinalIgnoreCase)))
{
    var yearArgument = args.FirstOrDefault(argument =>
        argument.StartsWith("--year=", StringComparison.OrdinalIgnoreCase));
    var year = int.TryParse(
        yearArgument?["--year=".Length..],
        NumberStyles.Integer,
        CultureInfo.InvariantCulture,
        out var parsedYear)
        ? parsedYear
        : DateTime.UtcNow.Year;
    var chaptersArgument = args.FirstOrDefault(argument =>
        argument.StartsWith("--chapters=", StringComparison.OrdinalIgnoreCase));
    var chapters = KcsHskFoodChapterSelection.Parse(chaptersArgument?["--chapters=".Length..]);
    var delayArgument = args.FirstOrDefault(argument =>
        argument.StartsWith("--request-delay-ms=", StringComparison.OrdinalIgnoreCase));
    var requestDelayMilliseconds = int.TryParse(
        delayArgument?["--request-delay-ms=".Length..],
        NumberStyles.Integer,
        CultureInfo.InvariantCulture,
        out var parsedRequestDelayMilliseconds)
        ? parsedRequestDelayMilliseconds
        : 150;
    var force = args.Any(argument => string.Equals(
        argument,
        "--force",
        StringComparison.OrdinalIgnoreCase));

    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<SsalddelContext>();
    await db.Database.MigrateAsync();
    var importService = scope.ServiceProvider.GetRequiredService<IKcsHskCatalogImportService>();
    var result = await importService.ImportAsync(new KcsHskCatalogImportRequest(
        year,
        chapters,
        requestDelayMilliseconds,
        force));
    app.Logger.LogInformation(
        "관세청 HSK 식품 카탈로그 처리 완료. Imported={Imported}, VersionId={VersionId}, Entries={Entries}, Added={Added}, Updated={Updated}, Deactivated={Deactivated}, Requests={Requests}, EffectiveFrom={EffectiveFrom:yyyy-MM-dd}",
        result.Imported,
        result.CatalogVersionId,
        result.EntryCount,
        result.AddedCount,
        result.UpdatedCount,
        result.DeactivatedCount,
        result.RequestCount,
        result.EffectiveFrom);
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
