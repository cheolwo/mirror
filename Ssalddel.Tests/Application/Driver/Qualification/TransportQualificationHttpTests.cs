using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentResults;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ssalddel.Application.Driver.Profile;
using Ssalddel.Application.Driver.Qualification;
using Ssalddel.Contracts.Common.Operations;
using Ssalddel.Contracts.Driver.Profile;
using Ssalddel.Contracts.Driver.Settings;
using Ssalddel.Controllers.Admin.Master06;
using Ssalddel.Controllers.Driver.Profile01;
using Ssalddel.Filters;
using Ssalddel.Migrations;
using Ssalddel.Services.Dispatch.Compliance;
using Ssalddel.Services.Operations;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Options;
using 살뜰.Services.Versioning;
using 살뜰.도메인.공통;
using 살뜰.도메인.기사;

namespace Ssalddel.Tests.Application.Driver.Qualification;

/// <summary>
/// 실제 기존 Controller·인증/권한·기능 filter·JSON·UseCase·SQLite를 격리 loopback HTTP로 검증한다.
/// 헤더 인증과 기관 등록 준비값은 시험용이며 실제 로그인·영업 등록·공식 자격조회·휴대폰 증거가 아니다.
/// </summary>
public sealed class TransportQualificationHttpTests
{
    private const string Own = "/api/v1/drivers/me/transport-qualification";
    private const string Admin = "/api/v1/admin/drivers/driver-1/transport-qualification";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    // 테스트 안에서만 사용하는 메모리 키. 파일·환경변수·사용자 키 저장소를 만들지 않는다.
    private static readonly DataProtectionPersonalDataEncryptionService Encryption = new(new EphemeralDataProtectionProvider());

    [Fact]
    public async Task 격리실제Http에서_본인제출_관리자검토_암호화저장_만료_정지와개정판보호가연결된다()
    {
        await using var host = await Host.Create();
        using (var anonymous = await host.Client.GetAsync(Own)) Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        host.Actor("driver-1");
        var initial = await host.Get(Own);
        Assert.Equal("driver-1", initial.DriverId); Assert.Equal(운송자격Codes.NotSubmitted, initial.Status);
        Assert.False(initial.Eligibility.CanReceiveNewWork);
        var input = Submission();
        var forgedBody = JsonSerializer.SerializeToNode(input, Json)!.AsObject(); forgedBody["driverId"] = "driver-2";
        using var submit = await host.Client.PostAsync(Own, JsonContent.Create(forgedBody));
        Assert.Equal(HttpStatusCode.OK, submit.StatusCode);
        Assert.True(submit.Headers.CacheControl?.NoStore);
        var pending = (await submit.Content.ReadFromJsonAsync<운송자격상세Dto>())!;
        Assert.Equal("driver-1", pending.DriverId); Assert.Equal(운송자격Codes.Pending, pending.Status);
        Assert.Equal(1, pending.Revision); Assert.False(pending.Eligibility.CanReceiveNewWork);
        Assert.Equal(운송자격Codes.Pending, (await host.Get(Own)).Status);
        using (var guessed = await host.Client.GetAsync("/api/v1/drivers/driver-1/transport-qualification"))
            Assert.Equal(HttpStatusCode.NotFound, guessed.StatusCode);

        host.Actor("driver-2");
        var unrelated = await host.Get(Own);
        Assert.Equal("driver-2", unrelated.DriverId); Assert.Empty(unrelated.Evidence);
        Assert.Equal(운송자격Codes.NotSubmitted, unrelated.Status);
        using (var adminRead = await host.Client.GetAsync(Admin)) Assert.Equal(HttpStatusCode.Forbidden, adminRead.StatusCode);
        using (var adminWrite = await host.Client.PostAsJsonAsync(Admin + "/review", Review(pending, host.Clock.Utc)))
            Assert.Equal(HttpStatusCode.Forbidden, adminWrite.StatusCode);

        host.Actor("driver-1", admin: true);
        using (var self = await host.Client.PostAsJsonAsync(Admin + "/review", Review(pending, host.Clock.Utc)))
            Assert.Equal(HttpStatusCode.Forbidden, self.StatusCode);
        host.Actor("reviewer-1", admin: true);
        var approved = await host.Post(Admin + "/review", Review(pending, host.Clock.Utc));
        Assert.Equal(운송자격Codes.Valid, approved.Status); Assert.Equal(2, approved.Revision);
        Assert.False(approved.OfficialAuthorityVerified); Assert.True(approved.Eligibility.CanReceiveNewWork);
        Assert.True((await host.Guard.평가Async("driver-1")).CanReceiveNewWork);

        // 1차 migration을 실제 SQLite에 적용한 컬럼에서 암호문을 읽어 확인한다.
        // 평문/암호문/키를 결과 로그로 출력하지 않는다.
        await using (var command = host.Connection.CreateCommand())
        {
            command.CommandText = "SELECT PrivateDetailsJson, HistoryJson FROM driver_transport_qualification WHERE DriverId='driver-1'";
            await using var reader = await command.ExecuteReaderAsync(); Assert.True(await reader.ReadAsync());
            foreach (var index in new[] { 0, 1 })
            {
                var stored = reader.GetString(index); Assert.StartsWith("pd:v1:", stored);
                Assert.DoesNotContain("private-evidence:", stored);
                Assert.DoesNotContain("vehicle:", stored);
                Assert.NotEqual(stored, Encryption.Unprotect(stored));
            }
        }
        await using (var command = host.Connection.CreateCommand())
        {
            command.CommandText = "SELECT 현재실차참조 FROM 용달기사 WHERE 기사Id='driver-1'";
            var stored = (string)(await command.ExecuteScalarAsync())!;
            Assert.StartsWith("pd:v1:", stored); Assert.Equal(input.ActualVehicleReference, Encryption.Unprotect(stored));
        }

        host.Actor("driver-1"); Assert.Equal(운송자격Codes.Valid, (await host.Get(Own)).Status);
        host.Clock.Utc = host.Clock.Utc.AddDays(2);
        var expired = await host.Get(Own);
        Assert.Equal(운송자격Codes.Expired, expired.Status); Assert.False(expired.Eligibility.CanReceiveNewWork);
        var expiredGuard = await host.Guard.평가Async("driver-1");
        Assert.False(expiredGuard.CanReceiveNewWork); Assert.Equal("Qualification.Expired", expiredGuard.ReasonCode);
        var resubmitted = await host.Post(Own, input with { ExpectedRevision = approved.Revision });
        Assert.Equal(3, resubmitted.Revision); Assert.Equal(운송자격Codes.Pending, resubmitted.Status);
        using (var stale = await host.Client.PostAsJsonAsync(Own, input with { ExpectedRevision = approved.Revision }))
            Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);

        host.Actor("reviewer-1", admin: true);
        var suspended = await host.Post(Admin + "/review", Review(resubmitted, host.Clock.Utc) with { Decision = 운송자격Codes.Suspended });
        Assert.Equal(운송자격Codes.Suspended, suspended.Status);
        host.Actor("driver-1");
        using (var selfResume = await host.Client.PostAsJsonAsync(Own, input with { ExpectedRevision = suspended.Revision }))
            Assert.Equal(HttpStatusCode.Conflict, selfResume.StatusCode);
        var suspendedGuard = await host.Guard.평가Async("driver-1");
        Assert.False(suspendedGuard.CanReceiveNewWork); Assert.Equal("Qualification.Suspended", suspendedGuard.ReasonCode);
        host.Db.ChangeTracker.Clear();
        Assert.Equal("driver-1", (await host.Db.운송자격.AsNoTracking().SingleAsync()).DriverId);
        Assert.Equal(suspended.Revision, (await host.Get(Own)).Revision);
    }

    [Fact]
    public async Task 격리실제Http에서도_기능꺼짐과기능키누락을_404로차단한다()
    {
        await using var host = await Host.Create(enabled: false); host.Actor("driver-1");
        using (var disabled = await host.Client.GetAsync(Own))
        {
            Assert.Equal(HttpStatusCode.NotFound, disabled.StatusCode);
            Assert.Equal("FeatureDisabled", JsonNode.Parse(await disabled.Content.ReadAsStringAsync())!["errorCode"]!.GetValue<string>());
        }
        using (var deniedSubmit = await host.Client.PostAsJsonAsync(Own, Submission())) Assert.Equal(HttpStatusCode.NotFound, deniedSubmit.StatusCode);
        host.Actor("reviewer-1", admin: true);
        using (var disabledAdmin = await host.Client.GetAsync(Admin)) Assert.Equal(HttpStatusCode.NotFound, disabledAdmin.StatusCode);
        Assert.Empty(await host.Db.운송자격.AsNoTracking().ToArrayAsync());
        // 현재 me 조회에는 국내 화물 기능키가 명시돼 있으므로 기능 OFF를 검증한다.
        using (var disabledProfile = await host.Client.GetAsync("/api/v1/drivers/me"))
        {
            Assert.Equal(HttpStatusCode.NotFound, disabledProfile.StatusCode);
            Assert.Equal("FeatureDisabled", JsonNode.Parse(await disabledProfile.Content.ReadAsStringAsync())!["errorCode"]!.GetValue<string>());
        }
        var driverIdsBefore = await host.Db.용달기사.AsNoTracking().OrderBy(x => x.기사Id).Select(x => x.기사Id).ToArrayAsync();
        host.Actor("driver-new");
        // 등록 액션은 여전히 미분류다. 유효한 DTO를 보내 binding 400이 아닌 경계 404임을 확인한다.
        using (var unclassified = await host.Client.PostAsJsonAsync("/api/v1/drivers/register", new 용달기사등록요청
        { 기사명 = "격리 시험 기사", 연락처 = "000-0000-0000", 차량 = "격리 시험 차량", 주_활동지역 = "격리 시험 지역" }))
        {
            Assert.Equal(HttpStatusCode.NotFound, unclassified.StatusCode);
            Assert.Equal("FeatureBoundaryUnclassified", JsonNode.Parse(await unclassified.Content.ReadAsStringAsync())!["errorCode"]!.GetValue<string>());
        }
        Assert.Equal(0, host.ProfileReadCalls);
        Assert.Equal(0, host.ProfileRegistrationCalls);
        Assert.Equal(driverIdsBefore, await host.Db.용달기사.AsNoTracking().OrderBy(x => x.기사Id).Select(x => x.기사Id).ToArrayAsync());
        Assert.Empty(await host.Db.운송자격.AsNoTracking().ToArrayAsync());
    }

    private static 운송자격제출요청 Submission() => new()
    {
        VehicleKind = 운송자격Codes.FreightVehicle, VehicleProfileValue = "격리 시험 차량",
        ActualVehicleReference = "vehicle:3dc005ab-2d8f-4169-b7d9-eb9604719757",
        TransportOperatorReference = "operator:74ca2496-0f69-4e81-883e-67c6690ca15f",
        Evidence = 운송자격Codes.RequiredFor(운송자격Codes.FreightVehicle).Select(x => new 운송자격근거Dto
        { RequirementCode = x, PrivateEvidenceReference = "private-evidence:" + Guid.NewGuid(), Summary = "격리 시험 비공개 근거" }).ToArray()
    };
    private static 운송자격검토요청 Review(운송자격상세Dto data, DateTime now) => new()
    {
        ExpectedRevision = data.Revision, Decision = 운송자격Codes.Valid, ActualVehicleReference = data.ActualVehicleReference,
        ReviewReason = "격리 시험에서 제출된 기사와 실차 근거의 검토 상태를 확인했습니다.", RecheckAtUtc = now.AddDays(1),
        Evidence = data.Evidence.Select(x => x with { VerificationLevel = 운송자격Codes.DocumentReview,
            ReviewReason = "격리 시험의 해당 준비 자료와 적용 대상 및 기간을 확인했습니다.",
            ValidFromUtc = now.AddDays(-1), ValidUntilUtc = now.AddDays(30) }).ToArray()
    };

    private sealed class Host(SqliteConnection connection, SsalddelContext db, Clock clock,
        WebApplication app, HttpClient client, I신규화물업무자격Guard guard) : IAsyncDisposable
    {
        public SqliteConnection Connection { get; } = connection;
        public SsalddelContext Db { get; } = db;
        public Clock Clock { get; } = clock;
        public HttpClient Client { get; } = client;
        public I신규화물업무자격Guard Guard { get; } = guard;
        public int ProfileReadCalls => ((UnusedProfile)app.Services.GetRequiredService<I용달기사프로필UseCase>()).ReadCalls;
        public int ProfileRegistrationCalls => ((UnusedProfile)app.Services.GetRequiredService<I용달기사프로필UseCase>()).RegistrationCalls;
        public void Actor(string id, bool admin = false)
        {
            Client.DefaultRequestHeaders.Remove("X-Qualification-Actor"); Client.DefaultRequestHeaders.Remove("X-Qualification-Role");
            Client.DefaultRequestHeaders.Add("X-Qualification-Actor", id);
            Client.DefaultRequestHeaders.Add("X-Qualification-Role", admin ? "admin" : "driver");
        }
        public async Task<운송자격상세Dto> Get(string route)
        {
            using var response = await Client.GetAsync(route);
            Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
            Assert.True(response.Headers.CacheControl?.NoStore);
            return (await response.Content.ReadFromJsonAsync<운송자격상세Dto>())!;
        }
        public async Task<운송자격상세Dto> Post<T>(string route, T request)
        {
            using var response = await Client.PostAsJsonAsync(route, request);
            Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
            Assert.True(response.Headers.CacheControl?.NoStore);
            return (await response.Content.ReadFromJsonAsync<운송자격상세Dto>())!;
        }
        public static async Task<Host> Create(bool enabled = true)
        {
            var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
            var db = new QualificationHttpDbContext(new DbContextOptionsBuilder<SsalddelContext>().UseSqlite(connection).Options);
            try
            {
                // 다른 업무 DB를 만들지 않고, 기존 기사/참여 모델의 필요한 표만 만든 뒤 새 실제 migration을 적용한다.
                var model = db.GetService<IDesignTimeModel>().Model;
                var differ = db.GetService<IMigrationsModelDiffer>();
                var wanted = new[] { "용달기사", "배달기사", "driver_work_participation" };
                var operations = differ.GetDifferences(null, model.GetRelationalModel()).OfType<CreateTableOperation>()
                    .Where(x => wanted.Contains(x.Name)).ToArray();
                Assert.Equal(3, operations.Length);
                foreach (var table in operations) table.Columns.RemoveAll(x => x.Name == "현재실차참조");
                var generator = db.GetService<IMigrationsSqlGenerator>();
                foreach (var command in generator.Generate(operations, model)) await db.Database.ExecuteSqlRawAsync(command.CommandText);
                var migration = new 운송자격검토준비 { ActiveProvider = db.Database.ProviderName! };
                foreach (var command in generator.Generate(migration.UpOperations, model)) await db.Database.ExecuteSqlRawAsync(command.CommandText);
                foreach (var id in new[] { "driver-1", "driver-2" })
                {
                    db.용달기사.Add(new 용달기사 { 기사Id = id, 차량 = "격리 시험 차량" });
                    db.Set<DriverWorkParticipationRecord>().Add(new() { UserId = id, VehicleKind = DriverVehicleKinds.Truck });
                }
                await db.SaveChangesAsync(); db.ChangeTracker.Clear();
                var clock = new Clock(new(2026, 10, 19, 0, 0, 0, DateTimeKind.Utc));
                var execution = new SsalddelExecutionModePolicy(Options.Create(new SsalddelExecutionOptions { Mode = SsalddelExecutionMode.Operational }));
                var service = new 운송자격UseCase(db, Options.Create(new 운송자격Options()), execution, clock);
                var guard = new 신규화물업무자격Guard(service, new FixturePlatformReadiness(), execution);
                var builder = WebApplication.CreateEmptyBuilder(new() { EnvironmentName = "Testing", ContentRootPath = AppContext.BaseDirectory });
                builder.WebHost.UseKestrel(o => o.Listen(IPAddress.Loopback, 0));
                builder.Services.AddLogging(); builder.Services.AddRouting();
                builder.Services.AddSingleton<I운송자격UseCase>(service);
                builder.Services.AddSingleton<I용달기사프로필UseCase>(new UnusedProfile());
                builder.Services.AddSingleton<ISender>(new UnusedSender());
                builder.Services.AddSingleton<IVersionFeatureFlagService>(new Flags(enabled));
                builder.Services.AddControllers(o => o.Filters.Add<SsalddelApiFeatureBoundaryFilter>()).ConfigureApplicationPartManager(m =>
                { m.ApplicationParts.Clear(); m.FeatureProviders.Clear(); m.FeatureProviders.Add(new OnlyQualificationControllers()); }).AddControllersAsServices();
                builder.Services.AddAuthentication("QualificationFixture").AddScheme<AuthenticationSchemeOptions, HeaderAuthentication>("QualificationFixture", _ => { });
                builder.Services.AddAuthorization(o => o.AddPolicy("서버관리자전용", p => p.RequireRole(역할명.서버관리자)));
                var app = builder.Build(); app.UseRouting(); app.UseAuthentication(); app.UseAuthorization(); app.MapControllers();
                try
                {
                    using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30)); await app.StartAsync(deadline.Token);
                    var address = Assert.Single(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses);
                    return new(connection, db, clock, app, new HttpClient(new HttpClientHandler { UseProxy = false, AllowAutoRedirect = false })
                    { BaseAddress = new(address), Timeout = TimeSpan.FromSeconds(30) }, guard);
                }
                catch { await app.DisposeAsync(); throw; }
            }
            catch { await db.DisposeAsync(); await connection.DisposeAsync(); throw; }
        }
        public async ValueTask DisposeAsync()
        {
            Client.Dispose(); using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await app.StopAsync(deadline.Token); await app.DisposeAsync(); await Db.DisposeAsync(); await Connection.DisposeAsync();
        }
    }

    private sealed class QualificationHttpDbContext(DbContextOptions<SsalddelContext> options) : SsalddelContext(options, Encryption) { }
    private sealed class Clock(DateTime now) : TimeProvider { public DateTime Utc { get; set; } = now; public override DateTimeOffset GetUtcNow() => new(Utc); }
    private sealed class OnlyQualificationControllers : IApplicationFeatureProvider<ControllerFeature>
    { public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature)
        { feature.Controllers.Add(typeof(용달기사Controller).GetTypeInfo()); feature.Controllers.Add(typeof(기사관리Controller).GetTypeInfo()); } }
    private sealed class Flags(bool enabled) : IVersionFeatureFlagService
    { public bool IsEnabled(string key) => enabled && key == VersionFeatureFlagKeys.DomesticTransportWorkflow;
        public IReadOnlyDictionary<string, bool> GetAll() => new Dictionary<string, bool> { [VersionFeatureFlagKeys.DomesticTransportWorkflow] = enabled }; }
    private sealed class HeaderAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var actor = Request.Headers["X-Qualification-Actor"].ToString();
            if (string.IsNullOrWhiteSpace(actor)) return Task.FromResult(AuthenticateResult.NoResult());
            var role = Request.Headers["X-Qualification-Role"].ToString() == "admin" ? 역할명.서버관리자 : 역할명.기사;
            var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, actor), new Claim(ClaimTypes.Role, role)], Scheme.Name));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
        }
    }
    /// <summary>기관 등록을 시험 사실로 위장하지 않기 위한 fixture 값. 실제 서비스 등록/설정을 바꾸지 않는다.</summary>
    private sealed class FixturePlatformReadiness : I화물플랫폼등록준비Service
    { public FreightPlatformReadinessDto 조회() => new() { FreeConnectionConfigured = true, IsPreparationComplete = true,
        ActualRegistrationConfirmed = true, RequiredAuthorityProceduresConfirmed = true }; }
    private sealed class UnusedProfile : I용달기사프로필UseCase
    {
        public int ReadCalls { get; private set; }
        public int RegistrationCalls { get; private set; }
        public Task<Result<용달기사등록응답>> 등록Async(string? id, 용달기사등록요청? request)
        { RegistrationCalls++; throw new NotSupportedException(); }
        public Task<Result<용달기사등록응답>> 내프로필조회Async(string? id)
        { ReadCalls++; throw new NotSupportedException(); }
    }
    private sealed class UnusedSender : ISender
    {
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest => throw new NotSupportedException();
        public Task<object?> Send(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
