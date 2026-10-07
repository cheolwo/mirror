using FluentResults;
using 살뜰.Services.DeliveryZones;
using 살뜰.Services.Dispatch.Coordination;
using 살뜰.Services.Storage.Local;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MySqlConnector;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Application.Driver.DispatchAction;
using Ssalddel.Application.Driver.Transport;
using Ssalddel.Application.Shipper.Request;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.Privacy;
using Ssalddel.Infrastructure.BackgroundJobs;
using Ssalddel.Infrastructure.Storage.Memory;
using Ssalddel.Services.Community;
using Ssalddel.Services.Privacy;
using Ssalddel.Services.LogisticsProcessing.SalesOrders;
using Ssalddel.Ui.Common.Areas.App.Services;
using 살뜰.Data;
using 살뜰.Services.Dispatch.Queue;
using 살뜰.Services.Dispatch.Engine;
using 살뜰.Services.Dispatch.Notification;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.Services.Dispatch.Continuity;
using 살뜰.Services.External.Google;
using 살뜰.Services.Options;
using 살뜰.Services.Versioning;
using 살뜰.도메인.공통;
using static NeighborhoodExchangePreview.FlexibleDeliveryFixtures;
namespace NeighborhoodExchangePreview;

// Opt-in loopback profile. SQL/Mongo and business handlers are real; external boundaries are explicit fixtures.
internal static class FlexibleDeliveryPreview
{
    public static void AddFlexibleDatabase(this WebApplicationBuilder builder, string evidence)
    {
        var raw = Environment.GetEnvironmentVariable("SSALDDEL_FLEX_MYSQL") ?? throw new InvalidOperationException("Isolated MySQL connection required.");
        var sql = new MySqlConnectionStringBuilder(raw);
        if (sql.Server is not ("127.0.0.1" or "localhost" or "::1") || !sql.Database.StartsWith("neighborhood_flex_r22_", StringComparison.Ordinal))
            throw new InvalidOperationException("Only a task-specific loopback database is allowed.");
        File.WriteAllText(Path.Combine(evidence, "mysql-database.txt"), sql.Database);
        builder.Services.AddDbContext<SsalddelContext>(o => o.UseMySql(raw, new MySqlServerVersion(new Version(8, 4, 0))));
    }
    public static void AddFlexibleDelivery(this WebApplicationBuilder builder)
    {
        var s = builder.Services;
        s.AddScoped<INeighborhoodDeliveryClient, NeighborhoodDeliveryClient>();
        s.AddScoped<생활배송협업Guard>(); s.AddScoped<I생활배송협업Guard, FailureProbeGuard>();
        s.AddScoped<I생활배송배차선택Service, 생활배송배차선택Service>();
        s.AddScoped<Mongo커뮤니티원장저장소>();
        s.AddScoped<I커뮤니티원장투영작업저장소, Mongo커뮤니티원장투영작업저장소>();
        s.AddScoped<I커뮤니티원장저장소, 이벤트발행커뮤니티원장저장소>();
        s.AddScoped<I원장업무투영동기화Handler, 운송원장업무투영Handler>();
        s.AddScoped<I커뮤니티원장업무투영동기화Service, 커뮤니티원장업무투영동기화Service>();
        s.AddScoped<Ssalddel.Application.Community.Handlers.커뮤니티원장업무투영EventHandler>();
        s.AddScoped<I운송원장Mongo동기화Service, 운송원장Mongo동기화Service>();
        s.AddScoped(typeof(I신청개인정보동의증적Store), typeof(신청개인정보동의증적Service).Assembly.GetType("Ssalddel.Services.Privacy.Mongo신청개인정보동의증적Store")!);
        s.AddScoped<I신청개인정보동의증적Service, 신청개인정보동의증적Service>();
        s.AddScoped<I생활배송기사정보제공동의Service, 생활배송기사정보제공동의Service>();
        s.AddScoped<IGeocodingService, FixtureGeocoding>();
        s.AddScoped<I화주운송기준운임Service, FixtureFare>();
        s.AddSingleton<InMemory음식배달권실행공간Store>(); s.AddSingleton<InMemory국내화물배달권실행공간Store>();
        s.AddScoped<I운송의뢰배차대기Service>(sp => new 운송의뢰배차대기Service(sp.GetRequiredService<SsalddelContext>(), new 운송의뢰배차원천분류Service(),
            new 운송원장배달권연결Service(new 원장배달권투영Service(sp.GetRequiredService<SsalddelContext>())),
            sp.GetRequiredService<InMemory음식배달권실행공간Store>(), sp.GetRequiredService<InMemory국내화물배달권실행공간Store>()));
        s.AddScoped<화주운송업무담당자UseCase>();
        s.AddScoped<ISender>(sp => new FixtureSender(
            new 의뢰생성CommandHandler(sp.GetRequiredService<SsalddelContext>(), sp.GetRequiredService<IGeocodingService>(), sp.GetRequiredService<ICurrentUserAccessor>(), sp.GetRequiredService<I운송원장Mongo동기화Service>(), new FreightPolicy(), new Handoff()),
            new 화주운송의뢰현장지급처리CommandHandler(sp.GetRequiredService<SsalddelContext>(), sp.GetRequiredService<ICurrentUserAccessor>(), sp.GetRequiredService<I운송의뢰배차대기Service>(), sp.GetRequiredService<I운송원장Mongo동기화Service>(), sp.GetRequiredService<화주운송업무담당자UseCase>()),
            new 의뢰단건조회QueryHandler(sp.GetRequiredService<SsalddelContext>(), sp.GetRequiredService<화주운송업무담당자UseCase>())));
        s.AddScoped<I화주운송의뢰UseCase>(sp => new 화주운송의뢰UseCase(sp.GetRequiredService<ISender>(), null!, null!, sp.GetRequiredService<I화주운송기준운임Service>(), new 화주운송요금정책검토Service(), sp.GetRequiredService<화주운송업무담당자UseCase>(), null!));
        s.AddScoped<I생활배송의뢰UseCase, 생활배송의뢰UseCase>();
        s.Configure<SsalddelExecutionOptions>(o => o.Mode = SsalddelExecutionMode.Operational);
        s.AddSingleton<ISsalddelExecutionModePolicy, SsalddelExecutionModePolicy>();
        s.Configure<VersionFeatureFlagsOptions>(o => { o.DomesticTransportWorkflow = true; o.CargoYongdalV1 = true; });
        s.AddSingleton<IVersionFeatureFlagService, VersionFeatureFlagService>();
        s.Configure<SalesChannelOrderSyncOptions>(_ => { }); s.AddSingleton<ISsalddelBackgroundJobActivationPolicy, SsalddelBackgroundJobActivationPolicy>();
        s.AddScoped<I배차업무정책, FixtureCandidatePolicy>();
        s.AddScoped<I운송의뢰배차원천분류Service, 운송의뢰배차원천분류Service>();
        s.AddScoped<I화물용달배차흐름Resolver, 화물용달배차흐름Resolver>();
        s.AddScoped<I운송의뢰배차엔진, 화물용달배차엔진>(); s.AddScoped<I운영체제배차EngineCatalog, 운영체제배차EngineCatalog>();
        s.AddScoped<I배차추천후보선정Service, 배차추천후보선정Service>(); s.Configure<배차큐정책Options>(_ => { });
        s.AddSingleton<I국내화물운송기사상태Store, InMemory국내화물운송기사상태Store>();
        s.AddScoped<I국내화물운송기사상태Service, 국내화물운송기사상태Service>();
        s.AddScoped<I음식배달배차흐름Resolver, 음식배달배차흐름Resolver>();
        s.AddScoped<I배차추천알림Service, FixtureNotification>(); s.AddScoped<I배차대기원장전환Service, 배차대기원장전환Service>();
        s.AddScoped<I공개배차Service, 공개배차Service>(); s.AddScoped<I화물배차수락적격성Service, FixtureEligibility>();
        s.AddScoped<I화물연속배차UseCase, FixtureContinuity>(); s.AddScoped<IPublisher, ProjectionPublisher>();
        s.AddScoped<I참여자실행권한검사, 참여자실행권한검사>(); s.AddScoped<IWorkRelationshipSnapshotCollector, WorkRelationshipSnapshotCollector>();
        s.AddScoped<배차수락CommandHandler>(); s.AddScoped<I기사운송상태전이Service, 기사운송상태전이Service>();
        s.AddScoped<I기사운송상태변경CommandExecutor, 기사운송상태변경CommandExecutor>();
        s.AddScoped<I운송증빙첨부JsonWriter, 운송증빙첨부JsonWriter>();
        s.AddScoped<운송상차지도착CommandHandler>(); s.AddScoped<운송상차완료CommandHandler>();
        s.AddScoped<운송하차지도착CommandHandler>(); s.AddScoped<운송인수완료CommandHandler>();
    }
    public static void MapFlexibleDelivery(this WebApplication app)
    {
        var api = app.MapGroup("/" + NeighborhoodDeliveryRoutes.Api);
        api.AddEndpointFilter(async (ctx, next) => { ctx.HttpContext.Response.Headers.CacheControl = "private, no-store";
            return ctx.HttpContext.RequestServices.GetRequiredService<ICurrentUserAccessor>().UserId is null ? Results.Unauthorized() : await next(ctx); });
        api.MapPost("/quote", async (NeighborhoodDeliveryRequest r, I생활배송의뢰UseCase s, CancellationToken ct) => Json(await s.견적Async(r, ct)));
        api.MapPost("", async (NeighborhoodDeliveryRequest r, I생활배송의뢰UseCase s, CancellationToken ct) => Json(await s.등록Async(r, ct)));
        api.MapGet("/mine", async (I생활배송의뢰UseCase s, CancellationToken ct) => Results.Json(await s.내목록Async(1, ct)));
        api.MapGet("/{id}", async (string id, I생활배송의뢰UseCase s, CancellationToken ct) => Read(await s.내상세Async(id, ct)));
        api.MapPost("/{id}/dispatch-choice", async (string id, NeighborhoodDispatchChoiceRequest r, I생활배송배차선택Service s, I생활배송의뢰UseCase deliveries, CancellationToken ct) => {
            var result = await s.선택Async(id, r, ct); return result.IsSuccess ? Read(await deliveries.내상세Async(id, ct)) : Failure(result.Errors); });
        api.MapPost("/{id}/driver-disclosure", async (string id, NeighborhoodDeliveryDisclosureRequest r, I생활배송기사정보제공동의Service s, CancellationToken ct) => Json(await s.기록Async(id, r, ct)));
        app.MapPost("/api/v1/common/application-privacy-consents", async (신청개인정보동의기록Request r, I신청개인정보동의증적Service s, ICurrentUserAccessor user, CancellationToken ct) => Results.Json(await s.동의기록Async(r, user.UserId!, ct)));
        app.MapPost("/preview/flex/{id}/recommend", async (string id, I배차대기원장전환Service s, ISsalddelBackgroundJobActivationPolicy gate, CancellationToken ct) =>
            gate.Evaluate(SsalddelBackgroundWorkloadKeys.DomesticTransportDispatch).IsEnabled ? Results.Json(await s.계획배차에서추천으로전환Async(id, ct)) : Results.Conflict());
        app.MapPost("/preview/flex/{id}/fixture-no-candidate", (string id) => { NoCandidate[id] = true; return Results.Ok(); });
        app.MapPost("/preview/flex/{id}/retry", async (string id, I배차대기원장전환Service s, CancellationToken ct) => Results.Json(await s.추천대기처리Async(id, ct)));
        app.MapPost("/preview/flex/{id}/decline", async (string id, I배차대기원장전환Service s, ICurrentUserAccessor user, CancellationToken ct) => Results.Json(await s.추천거절처리Async(id, user.UserId!, ct)));
        app.MapPost("/preview/flex/{id}/release", async (string id, I배차대기원장전환Service s, ICurrentUserAccessor user, CancellationToken ct) => Results.Json(await s.배차수락취소처리Async(id, user.UserId!, "fixture release", ct)));
        app.MapPost("/preview/flex/privacy/{id:guid}/withdraw", async (Guid id, I신청개인정보동의증적Service s, ICurrentUserAccessor user, CancellationToken ct) => Results.Json(await s.철회Async(id, new() { 철회사유 = "fixture withdrawal" }, user.UserId!, ct)));
        app.MapGet("/preview/flex/public", async (I공개배차Service s, ICurrentUserAccessor user, CancellationToken ct) => Results.Json(await s.GetPublicDispatchesAsync(user.UserId!, ct)));
        app.MapPost("/preview/flex/{id}/accept", async (string id, AcceptInput r, 배차수락CommandHandler s, ICurrentUserAccessor user, CancellationToken ct) => Json(await s.Handle(new 배차수락Command(user.UserId!, id, r.Round), ct)));
        app.MapPost("/preview/flex/{id:long}/transport/{step}", async (long id, string step, IServiceProvider sp, ICurrentUserAccessor user, CancellationToken ct) => {
            var d = user.UserId!;
            return step switch {
                "pickup-arrive" => Json(await sp.GetRequiredService<운송상차지도착CommandHandler>().Handle(new(d,id), ct)),
                "pickup" => Json(await sp.GetRequiredService<운송상차완료CommandHandler>().Handle(new(d,id) { 상차사진ObjectName = "fixture/pickup-evidence" }, ct)),
                "dropoff-arrive" => Json(await sp.GetRequiredService<운송하차지도착CommandHandler>().Handle(new(d,id), ct)),
                "dropoff" => Json(await sp.GetRequiredService<운송인수완료CommandHandler>().Handle(new(d,id) { 하차사진ObjectName = "fixture/dropoff-evidence" }, ct)),
                _ => Results.BadRequest() }; });
        app.MapGet("/preview/flex/{id}/evidence", async (string id, SsalddelContext db, I운송원장Mongo동기화Service sync, CancellationToken ct) => {
            var q = await db.운송원장.AsNoTracking().SingleOrDefaultAsync(x => x.의뢰Id == id, ct);
            if (q is not null) await sync.운송실행투영동기화Async(q, "fixture-evidence", ct);
            return Results.Json(new { QueueId = q?.Id, Mode = q?.생활배송배차방식, Ready = q?.생활배송수락준비완료, Round = q?.추천라운드,
                RequestCount = await db.화주운송의뢰.CountAsync(x => x.의뢰Id == id, ct), QueueCount = await db.운송원장.CountAsync(x => x.의뢰Id == id, ct),
                Driver = q?.확정기사Id, Candidate = q?.현재추천대상기사Id, State = q?.상태, Mongo = await sync.상태조회Async(id, ct), Events = await db.운송이벤트.CountAsync(x => x.의뢰Id == id, ct) }); });
    }
    private static IResult Json<T>(Result<T> r) => r.IsSuccess ? Results.Json(r.Value) : Failure(r.Errors);
    private static IResult Read<T>(T? r) => r is null ? Results.NotFound() : Results.Json(r);
    private static IResult Failure(IReadOnlyList<IError> errors) => Results.Problem(statusCode: errors.FirstOrDefault()?.Metadata.GetValueOrDefault("StatusCode") as int? ?? 409,
        detail: errors.FirstOrDefault()?.Message, extensions: new Dictionary<string,object?> { ["errorCode"] = errors.FirstOrDefault()?.Metadata.GetValueOrDefault("ErrorCode") });
    internal sealed record AcceptInput(int Round);
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, bool> NoCandidate = new();
    private sealed class FixtureCandidatePolicy : I배차업무정책
    {
        public int 배차업무유형 => 상태값.배차업무유형.용달운송;
        public Task<배차추천후보?> 다음후보선정Async(살뜰.도메인.운송.운송원장 q, string? excluded = null, CancellationToken ct = default)
            => Task.FromResult<배차추천후보?>(NoCandidate.ContainsKey(q.의뢰Id) ? null : new(excluded == "preview-driver-a" ? "preview-driver-b" : "preview-driver-a", 1m, "FixtureCandidate: synthetic availability, compatibility and route"));
    }
    private sealed class FixtureEligibility : I화물배차수락적격성Service
    {
        public Task<화물배차수락적격성결과> 평가Async(string driver, 살뜰.도메인.화주.화주운송의뢰 r, IReadOnlyCollection<string> warnings, CancellationToken ct = default)
            => Task.FromResult(new 화물배차수락적격성결과(driver is "preview-driver-a" or "preview-driver-b", "FixtureEligibility", "가상 적격 기사만 수락 가능", [], [], [], null, null));
    }
    private sealed class FailureProbeGuard(생활배송협업Guard inner) : I생활배송협업Guard
    {
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, bool> Failed = new();
        public Task<Result> 획득Async(NeighborhoodDeliveryRequest r, string actor, string fingerprint, CancellationToken ct) => inner.획득Async(r, actor, fingerprint, ct);
        public Task<Result> 연결확정Async(NeighborhoodDeliveryRequest r, string actor, string requestId, CancellationToken ct)
            => r.Notes == "fixture-fail-link" && Failed.TryAdd(r.ClientRequestId, true)
                ? Task.FromResult(Result.Fail(new Error("Injected fixture failure after SQL persistence, before Mongo link").WithMetadata("StatusCode",409)))
                : inner.연결확정Async(r, actor, requestId, ct);
    }
    private sealed class ProjectionPublisher(IServiceScopeFactory scopes) : IPublisher
    {
        public async Task Publish(object notification, CancellationToken ct = default)
        {
            if (notification is Ssalddel.Application.Community.Events.커뮤니티원장변경됨Event changed)
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<Ssalddel.Application.Community.Handlers.커뮤니티원장업무투영EventHandler>().Handle(changed, ct);
            }
            // External notifications remain an explicit fixture.
        }
        public Task Publish<T>(T value, CancellationToken ct = default) where T : INotification => Publish((object)value, ct);
    }
    private sealed class FixtureNotification : I배차추천알림Service
    {
        public Task 추천알림요청생성Async(long q, string r, string d, int round, CancellationToken ct = default) => Task.CompletedTask;
        public Task<int> 대기알림발송Async(int take = 100, CancellationToken ct = default) => Task.FromResult(0);
    }
}
