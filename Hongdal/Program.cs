using System.Text;
using System.Globalization;
using Hongdal.Hubs;
using Hongdal.Application.Behaviors;
using Hongdal.Application.CommandProcessing;
using Hongdal.Controllers;
using Hongdal.Security;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using Serilog;
using Hongdal.Application.Driver.Transport;
using Hongdal.Extensions;
using Quartz;
using 홍달.Infrastructure;
using 홍달.Infrastructure.BackgroundJobs.DispatchQueue;
using 홍달.Infrastructure.BackgroundJobs.Payments;
using Hongdal.Middleware;
using 홍달.Services.Audit;
using Hongdal.Services.Auth;
using 홍달.Services.Documents;
using 홍달.Services.External.Google;
using 홍달.Services.External.KieAi;
using 홍달.Services.Images;
using 홍달.Services.Options;
using 홍달.Services.Sales;
using 홍달.Services.ViewSettings;
using Hongdal.Services.LogisticsProcessing.Warehouse;
using Hongdal.Services.LogisticsProcessing.SalesOrders;
using 홍달.Services.Dispatch.Queue;
using 홍달.Services.Dispatch.Notification;
using 홍달.Services.Notifications;
using 홍달.Services.Payments;
using Hongdal.Services.Driver.Development;
using Hongdal.Services.Development;
using Hongdal.Services.Security;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.FileProviders;
using Hongdal.Infrastructure.Persistence.AgriculturalFisheries;
using Hongdal.Infrastructure.Persistence.TraditionalMarkets;
using Hongdal.Services.AgriculturalFisheries.Information;
using Hongdal.Startup;

var builder = WebApplication.CreateBuilder(args);
const string CustomsWebCorsPolicy = "HongdalWebCustoms";
var isRunningInContainer = string.Equals(
    Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"),
    "true",
    StringComparison.OrdinalIgnoreCase);

if (!isRunningInContainer)
{
    builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);
}

if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddUserSecrets<Program>(optional: true);
}

builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", "Hongdal.LogisticsApi")
        .WriteTo.Console();

    var fileLogPath = context.Configuration["HongdalLogging:FilePath"];
    if (string.IsNullOrWhiteSpace(fileLogPath)
        && context.HostingEnvironment.IsDevelopment()
        && !isRunningInContainer)
    {
        fileLogPath = "logs/hongdal-.log";
    }

    if (!string.IsNullOrWhiteSpace(fileLogPath))
    {
        configuration.WriteTo.File(
            path: fileLogPath,
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 14);
    }
});

builder.Services.AddHongdalPresentation();
builder.Services.AddHongdalApplicationCore();
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
    .GetSection(HongdalExecutionOptions.SectionName)
    .Get<HongdalExecutionOptions>() ?? new HongdalExecutionOptions();
if (!Enum.IsDefined(executionOptions.Mode))
{
    throw new InvalidOperationException("HongdalExecution:Mode must be Simulation or Operational.");
}

var tossOptions = builder.Configuration.GetSection(TossPaymentsOptions.SectionName).Get<TossPaymentsOptions>() ?? new TossPaymentsOptions();
if (executionOptions.Mode == HongdalExecutionMode.Operational && string.IsNullOrWhiteSpace(tossOptions.SecretKey))
{
    throw new InvalidOperationException("TossPayments:SecretKey configuration is required in Operational mode.");
}

builder.Services.AddHongdalOptions(builder.Configuration);
builder.Services.AddHongdalOperatingMarketServices(builder.Configuration);
builder.Services.AddScoped<I가입온보딩인연후보Service, 가입온보딩인연후보Service>();

var dispatchQueueJobOptions = builder.Configuration.GetSection(배차큐배치작업Options.SectionName).Get<배차큐배치작업Options>() ?? new 배차큐배치작업Options();
var salesOrderSyncOptions = builder.Configuration.GetSection(SalesChannelOrderSyncOptions.SectionName).Get<SalesChannelOrderSyncOptions>() ?? new SalesChannelOrderSyncOptions();
var youTubeOptions = builder.Configuration.GetSection(YouTubeOptions.SectionName).Get<YouTubeOptions>() ?? new YouTubeOptions();
var hongikHakdangCardOptions = builder.Configuration.GetSection(HongikHakdangCardOptions.SectionName).Get<HongikHakdangCardOptions>() ?? new HongikHakdangCardOptions();
var agriculturalFisheriesBatchOptions = builder.Configuration.GetSection(AgriculturalFisheriesBatchOptions.SectionName).Get<AgriculturalFisheriesBatchOptions>() ?? new AgriculturalFisheriesBatchOptions();
var communityEditorialBatchOptions = builder.Configuration.GetSection(CommunityEditorialBatchOptions.SectionName).Get<CommunityEditorialBatchOptions>() ?? new CommunityEditorialBatchOptions();
var databaseInitializationOptions = builder.Configuration.GetSection(DatabaseInitializationOptions.SectionName).Get<DatabaseInitializationOptions>() ?? new DatabaseInitializationOptions();
var initializeDatabaseOnly = args.Any(argument =>
    string.Equals(argument, "--initialize-database", StringComparison.OrdinalIgnoreCase));

builder.Services.AddHongdalBackgroundJobs(dispatchQueueJobOptions, salesOrderSyncOptions, youTubeOptions, hongikHakdangCardOptions, agriculturalFisheriesBatchOptions, communityEditorialBatchOptions, executionOptions);
builder.Services.AddHongdalPersistence(builder.Configuration);
builder.Services.AddHongdalHealthChecks();

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequireDigit = true;
        options.Password.RequiredLength = 8;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireLowercase = false;
    })
    .AddEntityFrameworkStores<HongdalContext>()
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
                     path.StartsWithSegments(DiagramCollaborationHub.HubPath)))
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
    options.AddPolicy("운영사용자전용", policy => policy.RequireRole(역할명.화주, 역할명.판매자, 역할명.창고관리자, 역할명.서버관리자));
});

builder.Services.AddAgriculturalFisheriesInformationModule();
builder.Services.AddHongdalHttpClients();
builder.Services.AddApifyAmazonProductResearch(builder.Configuration);
builder.Services.AddApifySocialMediaResearch(builder.Configuration);
builder.Services.AddApifyYouTubeTranscriptResearch(builder.Configuration);
builder.Services.AddFreeSocialMediaResearch(builder.Configuration);
builder.Services.AddYouTubeSocialContextWorkspace(builder.Configuration);
builder.Services.AddHongdalDomainServices();
if (builder.Environment.IsDevelopment())
{
    builder.Services.Replace(ServiceDescriptor.Singleton<IGoogleCloudStorageService, DevelopmentLocalCloudStorageService>());
}
builder.Services.AddSingleton<Hongdal.Services.Orderer.IRestaurantSearchPolicyStore, Hongdal.Services.Orderer.InMemoryRestaurantSearchPolicyStore>();
builder.Services.AddSingleton<I기사개발스냅샷Provider, InMemory기사개발스냅샷Provider>();

var app = builder.Build();
app.Logger.LogInformation("Hongdal execution mode: {ExecutionMode}", executionOptions.Mode);

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

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    var localStorageRoot = Path.Combine(
        app.Environment.ContentRootPath,
        DevelopmentLocalCloudStorageService.StorageDirectoryName);
    Directory.CreateDirectory(localStorageRoot);
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(localStorageRoot),
        RequestPath = DevelopmentLocalCloudStorageService.RequestPath
    });
}

if (databaseInitializationOptions.RunAtStartup || initializeDatabaseOnly)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<HongdalContext>();
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

app.UseAuthentication();
app.UseCors(CustomsWebCorsPolicy);
app.UseAuthorization();

app.UseMiddleware<IsmsPEncryptedTransportMiddleware>();
app.UseMiddleware<HrRoleAccessMiddleware>();
app.UseMiddleware<사용자행위로그Middleware>();
app.MapControllers();
app.MapHub<DispatchRecommendationHub>("/hubs/dispatch-recommendations");
app.MapHub<RestaurantOrderHub>("/hubs/restaurant-orders");
app.MapHub<DiagramCollaborationHub>(DiagramCollaborationHub.HubPath);
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("live")
}).AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready")
}).AllowAnonymous();

app.Run();
