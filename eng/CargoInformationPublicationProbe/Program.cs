using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Controllers.Common;
using Ssalddel.Services.Community;
using 살뜰.Services.Options;

namespace CargoInformationPublicationProbe;

/// <summary>새 임시 Mongo와 루프백 시험 인증에서 실제 공고 HTTP/저장 경계만 검증한다.</summary>
internal static class Program
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private static readonly List<object> Checks = [];
    private const string Owner = "cargo-notice-review-owner";
    private const string Other = "cargo-notice-review-other";
    private static string output = "";

    public static async Task<int> Main(string[] args)
    {
        WebApplication? app = null;
        try
        {
            if (args.Length != 6 || args[0] != "--output" || args[2] != "--mongo" || args[4] != "--database")
                throw new InvalidOperationException("Use --output <new-artifact-directory> --mongo <isolated-loopback-uri> --database <codex_cargo_info_r2_unique>");
            var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
            var allowed = Path.GetFullPath(Path.Combine(root, "artifacts/local/cargo-info-publication-r2")) + Path.DirectorySeparatorChar;
            output = Path.GetFullPath(args[1]);
            if (!output.StartsWith(allowed, StringComparison.OrdinalIgnoreCase) || Directory.Exists(output))
                throw new InvalidOperationException("A new output directory under the r2 artifact root is required.");
            var mongoUrl = new MongoUrl(args[3]);
            if (mongoUrl.Server.Host != "127.0.0.1" || mongoUrl.Username is not null || mongoUrl.Server.Port is 27017 or 27021)
                throw new InvalidOperationException("Only the dedicated loopback review Mongo port is allowed.");
            var databaseName = args[5];
            if (!databaseName.StartsWith("codex_cargo_info_r2_", StringComparison.Ordinal) || databaseName.Length < 35
                || databaseName.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '_'))
                throw new InvalidOperationException("A unique review database is required.");
            Directory.CreateDirectory(output);
            var mongo = new MongoClient(args[3]);
            var database = mongo.GetDatabase(databaseName);
            await database.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));
            var beforeCollections = await (await database.ListCollectionNamesAsync()).ToListAsync();
            Require("newIsolatedDatabase", beforeCollections.Count == 0);
            var clock = new ReviewClock(new DateTimeOffset(2026, 10, 25, 1, 0, 0, TimeSpan.Zero));
            var options = new 화물정보공고Options { Enabled = true };
            app = await Start(mongo, databaseName, clock, options);
            using var anon = Client(app);
            using var owner = Client(app, Owner);
            using var other = Client(app, Other);
            const string api = "/" + CargoNoticeRoutes.Api;
            var regions = await Get<IReadOnlyList<CargoNoticeRegion>>(anon, api + "/regions");
            Require("publicRegionsHaveNoCoordinates", regions.Count >= 2 && !JsonSerializer.Serialize(regions, Json).Contains("latitude", StringComparison.OrdinalIgnoreCase));
            var content = new CargoNoticeContent
            {
                Title = "상자 화물 정보", GoodsName = "빈 종이 상자", Quantity = 2, WeightKg = 4.5m,
                VehicleKind = CargoNoticeVehicleKinds.Van,
                PickupRegionKey = regions[0].RegionKey, DropoffRegionKey = regions[1].RegionKey,
            };
            await Status(anon, HttpMethod.Get, api + "/mine", null, HttpStatusCode.Unauthorized, "anonymousCannotReadOwners");
            await Status(anon, HttpMethod.Post, api, new CargoNoticeDraftRequest { ClientRequestId = Guid.NewGuid(), Content = content }, HttpStatusCode.Unauthorized, "anonymousCannotWrite");
            var createId = Guid.NewGuid();
            var create = new CargoNoticeDraftRequest { ClientRequestId = createId, ExpectedRevision = 0, Content = content };
            var first = await Mutate(owner, HttpMethod.Post, api, create);
            var id = Uri.EscapeDataString(first.Notice.NoticeId);
            Require("draftHasOneRevision", first.Notice.Status == CargoNoticeStatus.Draft && first.Notice.Revision == 1);
            Require("draftIsNotPublic", (await Get<CargoNoticeListResponse>(anon, api)).Items.Count == 0);
            await Status(anon, HttpMethod.Get, api + "/" + id, null, HttpStatusCode.NotFound, "draftDirectLinkIsPrivate", "CargoNotice.NotFound");
            await Status(owner, HttpMethod.Get, api + "/requests/" + Guid.NewGuid().ToString("D"), null,
                HttpStatusCode.NotFound, "unappliedRequestPreservesTypedNotFound", "CargoNotice.NotFound");
            var refused = new CargoNoticePublishRequest
            {
                ClientRequestId = Guid.NewGuid(), ExpectedRevision = 1, ExpectedContentRevision = 1,
                ConsentToPublicPublication = false, ExpiresAtUtc = clock.GetUtcNow().AddHours(2),
            };
            await Status(owner, HttpMethod.Post, api + "/" + id + "/publish", refused, HttpStatusCode.BadRequest, "missingPublicationConsentRejected", "CargoNotice.PublicConsentRequired");
            var publish = new CargoNoticePublishRequest
            {
                ClientRequestId = Guid.NewGuid(), ExpectedRevision = 1, ExpectedContentRevision = 1,
                ConsentToPublicPublication = true, ExpiresAtUtc = clock.GetUtcNow().AddHours(2),
            };
            var published = await Mutate(owner, HttpMethod.Post, api + "/" + id + "/publish", publish);
            Require("publishedAfterExplicitConsent", published.Notice.Status == CargoNoticeStatus.Published && published.Notice.Revision == 2);
            var publicRaw = await anon.GetStringAsync(api);
            var publicList = JsonSerializer.Deserialize<CargoNoticeListResponse>(publicRaw, Json)!;
            Require("publicListContainsOnlyPublishedNotice", publicList.Items.Count == 1 && publicList.Items[0].NoticeId == first.Notice.NoticeId);
            Require("publicDtoOmitsPrivateAndOperationalFields", !new[] { "ownerUserId", "operations", "fingerprint", "transportId", "phone", "latitude", "longitude", "의뢰Id", Owner }.Any(x => publicRaw.Contains(x, StringComparison.OrdinalIgnoreCase)));
            var updateA = new CargoNoticeDraftRequest { ClientRequestId = Guid.NewGuid(), ExpectedRevision = 2, Content = Clone(content, "상자 정보 보완 A") };
            var updateB = new CargoNoticeDraftRequest { ClientRequestId = Guid.NewGuid(), ExpectedRevision = 2, Content = Clone(content, "상자 정보 보완 B") };
            await Status(other, HttpMethod.Put, api + "/" + id, updateA, HttpStatusCode.NotFound, "otherUserCannotModify");
            using var stale = await owner.PutAsJsonAsync(api + "/" + id, new CargoNoticeDraftRequest { ClientRequestId = Guid.NewGuid(), ExpectedRevision = 1, Content = content }, Json);
            Require("staleRevisionRejected", stale.StatusCode == HttpStatusCode.Conflict);
            var race = await Task.WhenAll(owner.PutAsJsonAsync(api + "/" + id, updateA, Json), owner.PutAsJsonAsync(api + "/" + id, updateB, Json));
            Require("mongoCasHasOneWinner", race.Count(x => x.IsSuccessStatusCode) == 1 && race.Count(x => x.StatusCode == HttpStatusCode.Conflict) == 1);
            foreach (var response in race) response.Dispose();
            var latest = await Get<CargoNoticeOwnerDto>(owner, api + "/mine/" + id);
            Require("editRequiresFreshPublication", latest.Revision == 3 && latest.ContentRevision == 2 && latest.Status == CargoNoticeStatus.Draft
                && (await Get<CargoNoticeListResponse>(anon, api)).Items.Count == 0);
            var replay = await Mutate(owner, HttpMethod.Post, api, create);
            Require("lostCreateResponseReplaysOriginalReceipt", replay.ClientRequestId == createId && replay.Notice.Revision == 1 && replay.Notice.NoticeId == first.Notice.NoticeId);
            Require("replayDoesNotRollBackCurrentRevision", (await Get<CargoNoticeOwnerDto>(owner, api + "/mine/" + id)).Revision == 3);
            await Status(owner, HttpMethod.Post, api, new CargoNoticeDraftRequest { ClientRequestId = createId, Content = Clone(content, "내용이 다른 재사용 요청") }, HttpStatusCode.Conflict, "sameRequestWithDifferentPayloadRejected");
            await Status(owner, HttpMethod.Post, api + "/" + id + "/publish", new CargoNoticePublishRequest
            {
                ClientRequestId = Guid.NewGuid(), ExpectedRevision = 3, ExpectedContentRevision = 1,
                ConsentToPublicPublication = true, ExpiresAtUtc = clock.GetUtcNow().AddHours(2),
            }, HttpStatusCode.Conflict, "oldContentConsentRejected");
            var republished = await Mutate(owner, HttpMethod.Post, api + "/" + id + "/publish", new CargoNoticePublishRequest
            {
                ClientRequestId = Guid.NewGuid(), ExpectedRevision = 3, ExpectedContentRevision = 2,
                ConsentToPublicPublication = true, ExpiresAtUtc = clock.GetUtcNow().AddHours(2),
            });
            var withdraw = new CargoNoticeMutationRequest { ClientRequestId = Guid.NewGuid(), ExpectedRevision = republished.Notice.Revision };
            var withdrawn = await Mutate(owner, HttpMethod.Post, api + "/" + id + "/withdraw", withdraw);
            Require("withdrawalImmediatelyHidesNotice", withdrawn.Notice.Status == CargoNoticeStatus.Withdrawn && (await Get<CargoNoticeListResponse>(anon, api)).Items.Count == 0);
            var receipt = await Get<CargoNoticeCommandResponse>(owner, api + "/requests/" + withdraw.ClientRequestId.ToString("D") + "?noticeId=" + id);
            Require("requestReceiptBoundToOwner", receipt.ClientRequestId == withdraw.ClientRequestId && receipt.Notice.Revision == withdrawn.Notice.Revision);
            await Status(other, HttpMethod.Get, api + "/requests/" + withdraw.ClientRequestId.ToString("D") + "?noticeId=" + id, null, HttpStatusCode.NotFound, "otherUserCannotReadReceipt");
            var second = await Mutate(owner, HttpMethod.Post, api, new CargoNoticeDraftRequest { ClientRequestId = Guid.NewGuid(), Content = Clone(content, "만료 확인 공고") });
            var secondId = Uri.EscapeDataString(second.Notice.NoticeId);
            var expiry = clock.GetUtcNow().AddHours(1);
            await Mutate(owner, HttpMethod.Post, api + "/" + secondId + "/publish", new CargoNoticePublishRequest
            {
                ClientRequestId = Guid.NewGuid(), ExpectedRevision = 1, ExpectedContentRevision = 1,
                ConsentToPublicPublication = true, ExpiresAtUtc = expiry,
            });
            await app.StopAsync(); await app.DisposeAsync(); app = null;
            app = await Start(mongo, databaseName, clock, options);
            using var anonAfter = Client(app);
            using var ownerAfter = Client(app, Owner);
            var restored = await Get<CargoNoticeOwnerListResponse>(ownerAfter, api + "/mine");
            Require("restartRestoresOnlyOwnerNotices", restored.Items.Count == 2 && restored.Items.Any(x => x.Status == CargoNoticeStatus.Withdrawn)
                && restored.Items.Any(x => x.NoticeId == second.Notice.NoticeId && x.Status == CargoNoticeStatus.Published));
            Require("restartRestoresPublicNotice", (await Get<CargoNoticeListResponse>(anonAfter, api)).Items.Count == 1);
            clock.Set(expiry);
            Require("exactExpiryIsNotPublic", (await Get<CargoNoticeListResponse>(anonAfter, api)).Items.Count == 0);
            await Status(anonAfter, HttpMethod.Get, api + "/" + secondId, null, HttpStatusCode.NotFound, "expiredDirectLinkHidden");
            var expired = await Get<CargoNoticeOwnerDto>(ownerAfter, api + "/mine/" + secondId);
            Require("ownerSeesExpiredWithoutWorker", expired.Status == CargoNoticeStatus.Expired);
            options.Enabled = false;
            await Status(anonAfter, HttpMethod.Get, api, null, HttpStatusCode.NotFound, "disabledIsNotSuccessfulEmptyFallback", "CargoNotice.FeatureDisabled");
            var collections = await (await database.ListCollectionNamesAsync()).ToListAsync();
            Require("noOtherCollectionsOrTransportSideEffects", collections.Count == 1 && collections.All(x => x == Mongo화물정보공고Store.CollectionName));
            var saved = await database.GetCollection<BsonDocument>(Mongo화물정보공고Store.CollectionName).CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty);
            Require("oneDocumentPerNotice", saved == 2);
            await File.WriteAllTextAsync(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new
            {
                status = "Passed", generatedAtUtc = DateTimeOffset.UtcNow, checks = Checks, database = databaseName,
                collections, storedNotices = saved, boundary = "Actual controller/HTTP/Mongo; isolated test auth and synthetic data; no production startup, transport services or operational database",
                actualDevice = false, operationalActivation = false,
            }, Json));
            Console.WriteLine($"Passed {Checks.Count} HTTP/Mongo checks; artifacts: {output}");
            return 0;
        }
        catch (Exception ex)
        {
            if (output.Length > 0 && Directory.Exists(output))
                await File.WriteAllTextAsync(Path.Combine(output, "failed.json"), JsonSerializer.Serialize(new { status = "Failed", error = ex.ToString(), checks = Checks }, Json));
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
        finally { if (app is not null) { await app.StopAsync(); await app.DisposeAsync(); } }
    }

    private static async Task<WebApplication> Start(IMongoClient mongo, string database, TimeProvider clock, 화물정보공고Options options)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development", Args = [] });
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        builder.Services.AddControllers().AddApplicationPart(typeof(화물정보공고Controller).Assembly)
            .ConfigureApplicationPartManager(m => m.FeatureProviders.Add(new NoticeOnlyControllers()));
        builder.Services.AddAuthentication("CargoNoticeReview").AddScheme<AuthenticationSchemeOptions, ReviewAuthentication>("CargoNoticeReview", _ => { });
        builder.Services.AddAuthorization(); builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<ICurrentUserAccessor, HttpContextCurrentUserAccessor>();
        builder.Services.AddSingleton(mongo);
        builder.Services.AddSingleton(clock);
        builder.Services.AddSingleton<IOptions<화물정보공고Options>>(Options.Create(options));
        builder.Services.AddSingleton<IOptions<MongoDbOptions>>(Options.Create(new MongoDbOptions { Database = database }));
        builder.Services.AddSingleton<I생활교류공개지역Source, Official생활교류공개지역Source>();
        builder.Services.AddScoped<I화물정보공고Store, Mongo화물정보공고Store>();
        builder.Services.AddScoped<I화물정보공고UseCase, 화물정보공고UseCase>();
        var app = builder.Build();
        app.Use(async (context, next) =>
        {
            if (context.Connection.RemoteIpAddress is not { } ip || !IPAddress.IsLoopback(ip)) { context.Response.StatusCode = 404; return; }
            await next();
        });
        app.UseAuthentication(); app.UseAuthorization(); app.MapControllers();
        await app.StartAsync(); return app;
    }

    private static HttpClient Client(WebApplication app, string? actor = null)
    {
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        var client = new HttpClient { BaseAddress = new Uri(address), Timeout = TimeSpan.FromSeconds(30) };
        if (actor is not null) client.DefaultRequestHeaders.Add("X-Cargo-Review-User", actor);
        return client;
    }
    private static async Task<T> Get<T>(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"GET {path}: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }
    private static async Task<CargoNoticeCommandResponse> Mutate(HttpClient client, HttpMethod method, string path, object payload)
    {
        using var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(payload, options: Json) };
        using var response = await client.SendAsync(request);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"{method} {path}: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<CargoNoticeCommandResponse>(Json))!;
    }
    private static async Task Status(HttpClient client, HttpMethod method, string path, object? payload, HttpStatusCode expected, string check, string? expectedErrorCode = null)
    {
        using var request = new HttpRequestMessage(method, path) { Content = payload is null ? null : JsonContent.Create(payload, options: Json) };
        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        var errorMatches = true;
        if (expectedErrorCode is not null)
        {
            using var problem = JsonDocument.Parse(body);
            errorMatches = problem.RootElement.TryGetProperty("errorCode", out var code) && code.GetString() == expectedErrorCode;
        }
        Require(check, response.StatusCode == expected && errorMatches,
            $"expected {(int)expected} / {expectedErrorCode}, got {(int)response.StatusCode}: {body}");
    }
    private static CargoNoticeContent Clone(CargoNoticeContent content, string title)
    {
        var clone = JsonSerializer.Deserialize<CargoNoticeContent>(JsonSerializer.Serialize(content, Json), Json)!;
        clone.Title = title; return clone;
    }
    private static void Require(string name, bool passed, string? detail = null)
    {
        Checks.Add(new { name, passed, detail = passed ? null : detail });
        if (!passed) throw new InvalidOperationException(name + " · " + detail);
    }
    private sealed class NoticeOnlyControllers : IApplicationFeatureProvider<ControllerFeature>
    {
        public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature)
        {
            foreach (var type in feature.Controllers.Where(x => x.AsType() != typeof(화물정보공고Controller)).ToArray()) feature.Controllers.Remove(type);
        }
    }
    private sealed class ReviewClock(DateTimeOffset initial) : TimeProvider
    {
        private DateTimeOffset value = initial;
        public override DateTimeOffset GetUtcNow() => value;
        public void Set(DateTimeOffset now) => value = now;
    }
    private sealed class ReviewAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var actor = Request.Headers["X-Cargo-Review-User"].ToString();
            if (actor.Length == 0) return Task.FromResult(AuthenticateResult.NoResult());
            if (actor is not Owner and not Other) return Task.FromResult(AuthenticateResult.Fail("Review identity rejected"));
            var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, actor), new Claim(ClaimTypes.Role, "회원")], Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
        }
    }
}
