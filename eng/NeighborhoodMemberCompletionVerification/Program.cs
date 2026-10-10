using System.Net;
using System.Reflection;
using System.Security.Claims;
using System.Security.Cryptography;
using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using MySqlConnector;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Controllers.Common;
using Ssalddel.Services.Community;
using Ssalddel.Services.Privacy;
using Ssalddel.Services.Storage;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Options;

namespace NeighborhoodMemberCompletionVerification;

internal static class Program
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static async Task<int> Main(string[] args)
    {
        VerificationSettings? settings = null;
        try
        {
            settings = VerificationSettings.Load(args);
            Directory.CreateDirectory(settings.ContentRoot);
            var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions
            {
                EnvironmentName = "Development", ContentRootPath = settings.ContentRoot
            });
            builder.WebHost.UseKestrel(server => server.Listen(IPAddress.Loopback, 0));
            builder.Logging.ClearProviders();
            builder.Services.AddLogging();
            builder.Services.AddRouting();
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddDataProtection()
                .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(settings.EvidenceRoot, ".private", "keys")))
                .SetApplicationName("NeighborhoodMemberCompletionVerification." + settings.Database);
            builder.Services.AddSingleton<IPersonalDataEncryptionService, DataProtectionPersonalDataEncryptionService>();
            builder.Services.AddDbContext<SsalddelContext>(options =>
                options.UseMySql(settings.SqlConnection, new MySqlServerVersion(new Version(8, 4, 0))));
            builder.Services.AddSingleton<IMongoClient>(new MongoClient(settings.MongoConnection));
            builder.Services.Configure<MongoDbOptions>(options =>
            {
                options.Database = settings.Database; options.ConnectionString = settings.MongoConnection;
            });
            builder.Services.AddSingleton(TimeProvider.System);
            builder.Services.Configure<SsalddelExecutionOptions>(options => options.Mode = SsalddelExecutionMode.Simulation);
            builder.Services.AddSingleton<ISsalddelExecutionModePolicy, SsalddelExecutionModePolicy>();
            builder.Services.Configure<NeighborhoodMemberDeliveryOptions>(options => options.Enabled = true);
            builder.Services.AddScoped<ICurrentUserAccessor, HttpContextCurrentUserAccessor>();
            builder.Services.AddSingleton<SyntheticConsent>();
            builder.Services.AddSingleton<I신청개인정보동의증적Service>(provider => provider.GetRequiredService<SyntheticConsent>());
            builder.Services.AddSingleton<살뜰.Services.Dispatch.Recommendation.I배차추천경로Service, SyntheticRoute>();
            builder.Services.AddSingleton<IObjectStorageService, DevelopmentLocalStorageService>();
            builder.Services.AddScoped<I생활협업Store, Mongo생활협업Store>();
            builder.Services.AddScoped<I커뮤니티원장저장소, Mongo커뮤니티원장저장소>();
            builder.Services.AddScoped<I운송원장Mongo동기화Service, 운송원장Mongo동기화Service>();
            builder.Services.AddScoped<I생활협업배송Source, Ef생활협업배송Source>();
            builder.Services.AddScoped<I생활배송협업Guard, 생활배송협업Guard>();
            builder.Services.AddScoped<NeighborhoodMemberDeliveryUseCase>();
            builder.Services.AddScoped<I생활협업UseCase>(provider => new 생활협업UseCase(
                provider.GetRequiredService<I생활협업Store>(), provider.GetRequiredService<SsalddelContext>(),
                provider.GetRequiredService<ICurrentUserAccessor>(), provider.GetRequiredService<TimeProvider>(),
                deliveries: provider.GetRequiredService<I생활협업배송Source>(),
                memberDeliveries: provider.GetRequiredService<NeighborhoodMemberDeliveryUseCase>()));
            builder.Services.AddControllers().ConfigureApplicationPartManager(manager =>
            {
                manager.ApplicationParts.Clear(); manager.FeatureProviders.Clear();
                manager.FeatureProviders.Add(new ProductControllers());
            }).AddControllersAsServices();

            // 검증 주체의 토큰·서명 키는 이 프로세스 메모리에만 존재한다.
            var signingKey = new SymmetricSecurityKey(RandomNumberGenerator.GetBytes(64));
            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.RequireHttpsMetadata = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true, ValidIssuer = settings.Database,
                    ValidateAudience = true, ValidAudience = settings.Database,
                    ValidateIssuerSigningKey = true, IssuerSigningKey = signingKey,
                    ValidateLifetime = true, ClockSkew = TimeSpan.Zero,
                    NameClaimType = ClaimTypes.NameIdentifier, RoleClaimType = ClaimTypes.Role
                };
            });
            builder.Services.AddAuthorization();
            await using var app = builder.Build();
            app.UseRouting(); app.UseAuthentication(); app.UseAuthorization(); app.MapControllers();
            using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(6));

            // 공용 DB에서는 실행되지 않는다. GUID로 생성한 완전히 비어 있는 검증 DB만 허용한다.
            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<SsalddelContext>();
                await using (var connection = new MySqlConnection(settings.SqlConnection))
                {
                    await connection.OpenAsync(deadline.Token);
                    await using var query = connection.CreateCommand();
                    query.CommandText = "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = @database";
                    query.Parameters.AddWithValue("@database", settings.Database);
                    if (Convert.ToInt64(await query.ExecuteScalarAsync(deadline.Token)) != 0)
                        throw new VerificationFailure("FreshEmptyMySqlDatabaseRequired");
                }
                var mongoDatabase = scope.ServiceProvider.GetRequiredService<IMongoClient>().GetDatabase(settings.Database);
                using (var collections = await mongoDatabase.ListCollectionNamesAsync(cancellationToken: deadline.Token))
                    if ((await collections.ToListAsync(deadline.Token)).Count != 0)
                        throw new VerificationFailure("FreshEmptyMongoDatabaseRequired");
                await db.Database.EnsureCreatedAsync(deadline.Token);
                await mongoDatabase
                    .RunCommandAsync<MongoDB.Bson.BsonDocument>(new MongoDB.Bson.BsonDocument("ping", 1), cancellationToken: deadline.Token);
            }
            await app.StartAsync(deadline.Token);
            try
            {
                var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
                string Token(string actor) => new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
                    issuer: settings.Database, audience: settings.Database,
                    claims: [new Claim(ClaimTypes.NameIdentifier, actor), new Claim(ClaimTypes.Role, "일반회원")],
                    notBefore: DateTime.UtcNow.AddSeconds(-1), expires: DateTime.UtcNow.AddMinutes(10),
                    signingCredentials: new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256)));
                var scenario = new CompletionScenarios(app.Services, new Uri(address), Token, deadline.Token);
                IReadOnlyList<object> results;
                try { results = await scenario.RunAsync(); }
                finally
                {
                    await File.WriteAllTextAsync(Path.Combine(settings.EvidenceRoot, "http-calls.json"),
                        JsonSerializer.Serialize(scenario.Calls, Json));
                }
                var summary = new
                {
                    schemaVersion = "neighborhood-member-completion.r46", status = "Passed", settings.Database,
                    executionMode = "Simulation", authenticatedProductControllers = true, jwtSignatureValidated = true,
                    mysqlActual = true, mongoActual = true, privateLocalPhotoBytesVerified = true,
                    communityLedgerEventWrapperRegistered = false, eventOutboxEndToEndVerified = false,
                    mongoTransportMemberCollaborationFieldsPublished = false,
                    fixtureBoundaries = new[] { "Public post and SQL delivery intake fixture", "Synthetic account claims and consent", "Synthetic coordinates and route with IsActual flag", "No actual adult identity, GPS, provider or FCM verification" },
                    results, migrationsApplied = false, schemaBasis = "EnsureCreated from current EF model in a new task database",
                    operationalFeatureEnabled = false, externalEffects = false, paymentCompleted = false,
                    actualDeviceVerified = false, apkInstalled = false, generatedAtUtc = DateTime.UtcNow
                };
                await File.WriteAllTextAsync(Path.Combine(settings.EvidenceRoot, "verification.json"), JsonSerializer.Serialize(summary, Json), deadline.Token);
                Console.WriteLine(JsonSerializer.Serialize(new { status = "Passed", scenarios = results.Count, evidence = settings.EvidenceRoot }, Json));
            }
            finally
            {
                using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await app.StopAsync(stop.Token);
            }
            return 0;
        }
        catch (Exception exception)
        {
            // DB 예외·연결 문자열·JWT·원문 주소를 출력하지 않는다.
            var failure = new { status = "Failed", type = exception.GetType().Name,
                code = exception is VerificationFailure known ? known.Code : "VerificationDependencyOrImplementationFailure" };
            if (settings is not null)
                await File.WriteAllTextAsync(Path.Combine(settings.EvidenceRoot, "failure.json"), JsonSerializer.Serialize(failure, Json));
            Console.Error.WriteLine(JsonSerializer.Serialize(failure, Json));
            return 1;
        }
    }

    private sealed class ProductControllers : IApplicationFeatureProvider<ControllerFeature>
    {
        public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature)
        {
            feature.Controllers.Add(typeof(NeighborhoodMemberDeliveryController).GetTypeInfo());
            feature.Controllers.Add(typeof(생활협업Controller).GetTypeInfo());
        }
    }
}

internal sealed record VerificationSettings(string Database, string SqlConnection, string MongoConnection, string EvidenceRoot)
{
    public string ContentRoot => Path.Combine(EvidenceRoot, "host-content");
    public static VerificationSettings Load(string[] args)
    {
        var sql = new MySqlConnectionStringBuilder(Environment.GetEnvironmentVariable("SSALDDEL_MEMBER_R46_MYSQL")
            ?? throw new VerificationFailure("IsolatedMySqlEnvironmentRequired"));
        if (sql.Server != "127.0.0.1" || sql.Port != 13306 || !Regex.IsMatch(sql.Database, "^neighborhood_member_r46_[a-f0-9]{32}$"))
            throw new VerificationFailure("IsolatedMySqlBoundaryMismatch");
        var connection = Environment.GetEnvironmentVariable("SSALDDEL_MEMBER_R46_MONGO")
            ?? throw new VerificationFailure("LoopbackMongoEnvironmentRequired");
        var mongo = MongoUrl.Create(connection);
        if (mongo.Servers.Any(server => server.Host is not ("127.0.0.1" or "localhost" or "::1"))
            || mongo.DatabaseName is { Length: > 0 } database && database != sql.Database)
            throw new VerificationFailure("LoopbackMongoBoundaryMismatch");
        var option = Array.IndexOf(args, "--evidence-root");
        if (option < 0 || option + 1 >= args.Length) throw new VerificationFailure("EvidenceRootRequired");
        var output = Path.GetFullPath(args[option + 1]);
        var repository = FindRepository();
        var allowed = Path.GetFullPath(Path.Combine(repository, "artifacts", "local")) + Path.DirectorySeparatorChar;
        if (!output.StartsWith(allowed, StringComparison.OrdinalIgnoreCase) || File.Exists(Path.Combine(output, "verification.json")))
            throw new VerificationFailure("FreshLocalEvidenceRootRequired");
        Directory.CreateDirectory(output);
        return new(sql.Database, sql.ConnectionString, connection, output);
    }
    private static string FindRepository()
    {
        for (var directory = new DirectoryInfo(Environment.CurrentDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "Ssalddel", "Ssalddel.csproj"))) return directory.FullName;
        throw new VerificationFailure("RepositoryRootRequired");
    }
}

internal sealed class VerificationFailure(string code) : Exception(code)
{
    public string Code { get; } = code;
}
