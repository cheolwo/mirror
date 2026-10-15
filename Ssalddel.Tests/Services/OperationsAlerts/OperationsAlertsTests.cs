using System.Text;
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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Options;
using Ssalddel.Contracts.Admin.Operations;
using Ssalddel.Controllers.Admin;
using Ssalddel.Domain.Notifications;
using Ssalddel.Services.OperationsAlerts;
using Ssalddel.Ui.Common.Areas.BackOffice.ViewModels;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using Xunit;

namespace Ssalddel.Tests.Services.OperationsAlerts;
public sealed class OperationsAlertsTests
{
    [Fact] public async Task PushUsesOnlyConfiguredAdminInstallationAndSafeUtcPayload()
    {
        await using var f=await Fixture.Create();await f.Admin();
        f.Db.AddRange(new SsalddelMobilePushInstallation { UserId="admin",AppKey="ssalddel.admin",InstallationId="admin-device",PushToken="admin-token",PushTokenHash="admin-hash",Platform="Android" },
            new SsalddelMobilePushInstallation { UserId="admin",AppKey="ssalddel.shipper",InstallationId="other-app",PushToken="other-token",PushTokenHash="other-hash",Platform="Android" });
        await f.Db.SaveChangesAsync();
        var notice=new OperationsAlertOutbox { Id=new string('a',64),AlertId=new string('b',64),RecipientUserId="admin",CreatedAtUtc=DateTime.SpecifyKind(f.Clock.Now,DateTimeKind.Unspecified) };
        var push=new CapturingPush();
        var missing=Microsoft.Extensions.Options.Options.Create(new 살뜰.Services.Options.PushNotificationsOptions { ServiceAccountJsonPath="local-missing-fixture.json" });
        Assert.Equal("ConfigurationRequired",await new OperationsAlertPushChannel(f.Db,push,missing).SendAsync(notice,default));Assert.Empty(push.Messages);
        var configured=Microsoft.Extensions.Options.Options.Create(new 살뜰.Services.Options.PushNotificationsOptions { ServerKey="local-fake-transport-only" });
        Assert.Equal("Sent",await new OperationsAlertPushChannel(f.Db,push,configured).SendAsync(notice,default));
        var sent=Assert.Single(push.Messages);Assert.Equal("admin-token",sent.Token);Assert.True(sent.DataOnly);Assert.True(sent.HighPriority);
        Assert.EndsWith("Z",sent.Data["createdAtUtc"]);Assert.Equal(6,sent.Data.Count);
        Assert.DoesNotContain(sent.Data.Keys,k=>k.Contains("address",StringComparison.OrdinalIgnoreCase)||k.Contains("photo",StringComparison.OrdinalIgnoreCase));
    }
    private sealed class CapturingPush : 살뜰.Services.Notifications.IFcmPushService
    {
        public List<살뜰.Services.Notifications.FcmPushMessage> Messages=new();
        public Task<bool> SendAsync(살뜰.Services.Notifications.FcmPushMessage message,CancellationToken ct=default){Messages.Add(message);return Task.FromResult(true);}
        public Task<bool> SendToTokenAsync(string token,string title,string body,IReadOnlyDictionary<string,string> data,CancellationToken ct=default)=>throw new NotSupportedException();
    }
    [Fact] public async Task ActualHttpRejectsAnonymousAndMemberAndProtectsAcknowledgementRevision()
    {
        await using var f=await Fixture.Create();await f.Observe();var row=await f.Single();
        var builder=WebApplication.CreateEmptyBuilder(new WebApplicationOptions{EnvironmentName="Testing",ContentRootPath=AppContext.BaseDirectory});
        builder.WebHost.UseKestrel(k=>k.Listen(IPAddress.Loopback,0));builder.Services.AddLogging();builder.Services.AddRouting();
        builder.Services.AddSingleton(f.Db);builder.Services.AddSingleton<IOptionsMonitor<OperationsAlertsOptions>>(f.Options);
        builder.Services.AddSingleton<TimeProvider>(f.Clock);builder.Services.AddScoped<OperationsAlertCoordinator>();
        builder.Services.AddControllers().ConfigureApplicationPartManager(m=>{m.ApplicationParts.Clear();m.FeatureProviders.Clear();m.FeatureProviders.Add(new OnlyAlertControllers());}).AddControllersAsServices();
        builder.Services.AddAuthentication("Fixture").AddScheme<AuthenticationSchemeOptions,HttpAuth>("Fixture",_=>{});
        builder.Services.AddAuthorization(o=>o.AddPolicy("서버관리자전용",p=>p.RequireAuthenticatedUser().RequireRole("서버관리자")));
        await using var app=builder.Build();app.UseRouting();app.UseAuthentication();app.UseAuthorization();app.MapControllers();
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(25));await app.StartAsync(timeout.Token);
        try
        {
            using var client=new HttpClient(new HttpClientHandler{UseProxy=false,AllowAutoRedirect=false}){BaseAddress=new(Assert.Single(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses))};
            const string route="/api/v1/admin/operations-alerts";
            using var anonymous=await client.GetAsync(route,timeout.Token);Assert.Equal(HttpStatusCode.Unauthorized,anonymous.StatusCode);
            client.DefaultRequestHeaders.Add("X-Test-Role","member");using var member=await client.GetAsync(route,timeout.Token);Assert.Equal(HttpStatusCode.Forbidden,member.StatusCode);
            client.DefaultRequestHeaders.Remove("X-Test-Role");client.DefaultRequestHeaders.Add("X-Test-Role","admin");
            using var admin=await client.GetAsync(route,timeout.Token);Assert.Equal(HttpStatusCode.OK,admin.StatusCode);Assert.True(admin.Headers.CacheControl!.NoStore);
            using var stale=await client.PostAsJsonAsync(route+"/"+row.Id+"/acknowledge",new OperationsAlertAcknowledgeRequest(Guid.NewGuid(),row.Revision+99),timeout.Token);Assert.Equal(HttpStatusCode.Conflict,stale.StatusCode);
            var command=new OperationsAlertAcknowledgeRequest(Guid.NewGuid(),row.Revision);
            using var acknowledged=await client.PostAsJsonAsync(route+"/"+row.Id+"/acknowledge",command,timeout.Token);Assert.Equal(HttpStatusCode.OK,acknowledged.StatusCode);
            using var replay=await client.PostAsJsonAsync(route+"/"+row.Id+"/acknowledge",command,timeout.Token);Assert.Equal(HttpStatusCode.OK,replay.StatusCode);
            Assert.Equal("Acknowledged",(await acknowledged.Content.ReadFromJsonAsync<OperationsAlertDto>(timeout.Token))!.Status);
        }
        finally {using var stop=new CancellationTokenSource(TimeSpan.FromSeconds(5));await app.StopAsync(stop.Token);}
    }
    private sealed class OnlyAlertControllers : IApplicationFeatureProvider<ControllerFeature>
    {public void PopulateFeature(IEnumerable<ApplicationPart> parts,ControllerFeature feature)=>feature.Controllers.Add(typeof(OperationsAlertsController).GetTypeInfo());}
    public sealed class HttpAuth(IOptionsMonitor<AuthenticationSchemeOptions> options,ILoggerFactory logger,UrlEncoder encoder):AuthenticationHandler<AuthenticationSchemeOptions>(options,logger,encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var role=Request.Headers["X-Test-Role"].ToString();if(role.Length==0)return Task.FromResult(AuthenticateResult.NoResult());
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity([new(ClaimTypes.NameIdentifier,"admin"),new(ClaimTypes.Role,role=="admin"?"서버관리자":"일반회원")],Scheme.Name)),Scheme.Name)));
        }
        protected override Task HandleChallengeAsync(AuthenticationProperties p){Response.StatusCode=401;return Task.CompletedTask;}
    }
    [Fact] public void MySqlMigrationAndSnapshotMatchAlertSchemaWithoutBusinessWrites()
    {
        using var db=new SsalddelContext(new DbContextOptionsBuilder<SsalddelContext>()
            .UseMySql("Server=127.0.0.1;Database=not_opened;User ID=fixture;Password=fixture;",new MySqlServerVersion(new Version(8,4,0)),o=>o.MigrationsAssembly("Ssalddel")).Options,new Encryption());
        var current=db.GetService<IDesignTimeModel>().Model;
        var stored=db.GetService<IModelRuntimeInitializer>().Initialize(db.GetService<IMigrationsAssembly>().ModelSnapshot!.Model,designTime:true);
        var differences=db.GetService<IMigrationsModelDiffer>().GetDifferences(stored.GetRelationalModel(),current.GetRelationalModel());
        var relevant=differences.Where(x=>(x.GetType().GetProperty("Table")?.GetValue(x) as string ?? x.GetType().GetProperty("Name")?.GetValue(x) as string ?? "").StartsWith("operations_",StringComparison.Ordinal)).ToArray();
        Assert.True(relevant.Length==0,string.Join(",",relevant.Select(x=>x.GetType().Name)));
        var sql=string.Join("\n",db.GetService<IMigrationsSqlGenerator>().Generate(new Ssalddel.Migrations.AddOperationsAlerts().UpOperations,current).Select(x=>x.CommandText));
        Assert.Contains("operations_alert_outbox",sql);Assert.Contains("operations_alert_receipts",sql);
        Assert.DoesNotContain("UPDATE ",sql,StringComparison.OrdinalIgnoreCase);Assert.DoesNotContain("DROP ",sql,StringComparison.OrdinalIgnoreCase);
        Assert.True(current.FindEntityType(typeof(OperationsAlert))!.FindProperty(nameof(OperationsAlert.Revision))!.IsConcurrencyToken);
    }
    [Fact] public async Task CargoIncidentKeysetAndExplicitResolutionPreserveBusinessLedger()
    {
        await using var f = await Fixture.Create();
        f.Db.AddRange(new 살뜰.도메인.운송.비정상운송사건 { 사건StableId="cargo-a", 운송의뢰Id="request-a", UpdatedAt=f.Clock.Now, 현장진행불가=true },
            new 살뜰.도메인.운송.비정상운송사건 { 사건StableId="cargo-b", 운송의뢰Id="request-b", UpdatedAt=f.Clock.Now });
        await f.Db.SaveChangesAsync();
        var source = new CargoOperationsAlertSource(f.Db, f.Clock);
        var first = Assert.Single((await source.ReadChangesAsync(DateTime.UnixEpoch,"",1,default)).Where(x => x.ProblemCode == "CargoIncidentReviewRequired"));
        Assert.Equal("cargo-a",first.SourceId);Assert.Equal("Urgent",first.Severity);Assert.True(first.Active);
        var second=Assert.Single((await source.ReadChangesAsync(first.ChangedAtUtc,first.SourceId,1,default)).Where(x => x.ProblemCode == "CargoIncidentReviewRequired"));
        Assert.Equal("cargo-b",second.SourceId);Assert.Equal("Warning",second.Severity);
        await f.Coordinator.ObserveAsync(first, f.Clock.Now.AddHours(-1));
        var incident=await f.Db.Set<살뜰.도메인.운송.비정상운송사건>().SingleAsync(x=>x.사건StableId=="cargo-a");
        incident.상태Code=살뜰.도메인.운송.비정상운송사건상태Codes.조치결정;await f.Db.SaveChangesAsync();
        await f.Coordinator.ObserveAsync(Assert.Single((await source.ReadCurrentAsync("cargo-a",default)).Where(x => x.ProblemCode == "CargoIncidentReviewRequired")),f.Clock.Now);
        Assert.Equal("Resolved",(await f.Single()).Status);Assert.Equal("request-a",incident.운송의뢰Id);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>source.ReadCurrentAsync("missing",default));
    }
    [Fact] public async Task RepeatedObservationDeduplicatesAndReopenCreatesNewEpisode()
    {
        await using var f = await Fixture.Create();
        await f.Observe(); await f.Observe(); Assert.Equal(2, await f.Db.Set<OperationsAlertOutbox>().CountAsync());
        await f.Observe(false); Assert.Equal("Resolved", (await f.Single()).Status);
        await f.Observe(); Assert.Equal(2, (await f.Single()).Episode); Assert.Equal(4, await f.Db.Set<OperationsAlertOutbox>().CountAsync());
    }
    [Theory]
    [InlineData("DriverNotificationOutboxReviewRequired", "배차 알림 복구 소진", "서버 재조회")]
    [InlineData("AfterPickupRecoveryRequired", "픽업 후 배송 중단", "안전")]
    [InlineData("RestaurantPreparationDelay", "음식 준비 지연", "준비 상태")]
    public async Task 인증된알림상세는_문제별다음행동을_원문없이안내한다(string code, string title, string action)
    {
        await using var f = await Fixture.Create();
        await f.Coordinator.ObserveAsync(new("food", "food-fixture", code, "Urgent", true, f.Clock.Now), f.Clock.Now.AddMinutes(-1));
        var alert = await f.Single();
        var detail = Assert.IsType<OperationsAlertDto>(await f.Coordinator.DetailAsync(alert.Id));
        Assert.Contains(title, detail.Title);
        Assert.Contains(action, detail.Action);
        Assert.Equal("/food-operations/orders/food-fixture", detail.SourceHref);
    }
    [Fact] public void 미등록문제코드는_입력원문을다음행동에노출하지않는다()
    {
        const string privateInput = "private address 010-1234-5678";
        Assert.Equal("업무 진행 확인 필요", OperationsAlertPresentation.Title(privateInput));
        Assert.DoesNotContain(privateInput, OperationsAlertPresentation.Action(privateInput));
    }
    [Fact] public async Task AcknowledgeReplaysOnlySamePayloadAndDoesNotResolveBusiness()
    {
        await using var f = await Fixture.Create(); await f.Observe(); var row = await f.Single();
        var input = new OperationsAlertAcknowledgeRequest(Guid.NewGuid(), row.Revision);
        await f.Coordinator.AcknowledgeAsync(row.Id,"admin",input); await f.Coordinator.AcknowledgeAsync(row.Id,"admin",input);
        Assert.Equal("Acknowledged", (await f.Single()).Status);
        await Assert.ThrowsAsync<OperationsAlertConflictException>(() => f.Coordinator.AcknowledgeAsync(row.Id,"admin",input with { ExpectedRevision=99 }));
        await f.Observe(false); Assert.Equal("Resolved", (await f.Single()).Status);
    }
    [Fact] public async Task StaleRevisionAndOldEpisodeCannotAcknowledge()
    {
        await using var f=await Fixture.Create(); await f.Observe(); var row=await f.Single(); var id=row.Id; var rev=row.Revision;
        await f.Observe(); await Assert.ThrowsAsync<OperationsAlertConflictException>(()=>f.Coordinator.AcknowledgeAsync(id,"admin",new(Guid.NewGuid(),rev)));
    }
    [Fact] public async Task ReminderOnceAndKstDigestOnceAcrossRestart()
    {
        await using var f=await Fixture.Create(); await f.Observe(); f.Clock.Now=f.Clock.Now.AddMinutes(15);
        await f.Coordinator.ScheduleAsync(); await f.Coordinator.ScheduleAsync();
        Assert.Equal(2,await f.Db.Set<OperationsAlertOutbox>().CountAsync(x=>x.Kind=="Reminder"));
        f.Clock.Now=new(2080,1,2,0,0,0,DateTimeKind.Utc); await f.Coordinator.ScheduleAsync();
        var count=await f.Db.Set<OperationsAlertOutbox>().CountAsync(x=>x.Kind=="Digest");
        f.Db.ChangeTracker.Clear(); await f.Coordinator.ScheduleAsync(); Assert.Equal(count,await f.Db.Set<OperationsAlertOutbox>().CountAsync(x=>x.Kind=="Digest"));
        f.Clock.Now=f.Clock.Now.AddDays(1); await f.Coordinator.ScheduleAsync(); Assert.Equal(count+2,await f.Db.Set<OperationsAlertOutbox>().CountAsync(x=>x.Kind=="Digest"));
    }
    [Fact] public async Task AcknowledgedUrgentDoesNotQueueReminder()
    {
        await using var f=await Fixture.Create(); await f.Observe(); var row=await f.Single();
        await f.Coordinator.AcknowledgeAsync(row.Id,"admin",new(Guid.NewGuid(),row.Revision)); f.Clock.Now=f.Clock.Now.AddMinutes(16);
        await f.Coordinator.ScheduleAsync(); Assert.False(await f.Db.Set<OperationsAlertOutbox>().AnyAsync(x=>x.Kind=="Reminder"));
    }
    [Fact] public async Task InitialOldProblemsGroupedAndNoReminderStorm()
    {
        await using var f=await Fixture.Create(); await f.Coordinator.ObserveAsync(new("support","old","TransactionDispute","Urgent",true,f.Clock.Now.AddDays(-5)),f.Clock.Now);
        Assert.Empty(await f.Db.Set<OperationsAlertOutbox>().ToListAsync()); f.Clock.Now=f.Clock.Now.AddMinutes(20);
        await f.Coordinator.ScheduleAsync(); Assert.False(await f.Db.Set<OperationsAlertOutbox>().AnyAsync(x=>x.Kind=="Reminder"));
    }
    [Fact] public async Task FailedSourceReadKeepsExistingIssueOpenAndDoesNotAdvanceCursor()
    {
        await using var f=await Fixture.Create(); await f.Observe();
        var source=new Source { Fail=true };
        await new OperationsAlertMonitor(f.Db,f.Coordinator,[source],f.Options).ScanAsync();
        Assert.Equal("Open",(await f.Db.Set<OperationsAlert>().SingleAsync(x=>x.SourceKind=="support")).Status);
        Assert.Equal(DateTime.UnixEpoch,(await f.Db.Set<OperationsAlertCheckpoint>().SingleAsync(x=>x.Id=="support")).CursorUtc);
        Assert.Contains(await f.Db.Set<OperationsAlert>().ToListAsync(),x=>x.ProblemCode=="SourceReadFailed");
    }
    [Fact] public async Task ClosedHistoricalIssueNeverCreatesAlert()
    {
        await using var f=await Fixture.Create(); await f.Observe(false); Assert.Empty(await f.Db.Set<OperationsAlert>().ToListAsync());
    }
    [Fact] public async Task DisabledFeatureDoesNotCreateWork()
    {
        await using var f=await Fixture.Create(); f.Options.Value.Enabled=false;
        await new OperationsAlertMonitor(f.Db,f.Coordinator,[new Source()],f.Options).ScanAsync(); Assert.Empty(await f.Db.Set<OperationsAlertCheckpoint>().ToListAsync());
    }
    [Fact] public void ApiIsAdminOnlyAndNotBusinessMutation()
    { Assert.Equal("서버관리자전용", typeof(OperationsAlertsController).GetCustomAttributes(typeof(AuthorizeAttribute),true).Cast<AuthorizeAttribute>().Single().Policy); }
    [Fact] public async Task DisabledSendingHasNoExternalSideEffects()
    {
        await using var f=await Fixture.Create(); await f.Observe(); var channel=new Channel("Push");
        await f.Dispatcher(channel).SendPendingAsync(); Assert.Equal(0,channel.Calls);
        Assert.All(await f.Db.Set<OperationsAlertOutbox>().ToListAsync(),x=>Assert.Equal("Pending",x.Status));
    }
    [Fact] public async Task FailedChannelDoesNotBlockOtherChannelAndRetriesAreBounded()
    {
        await using var f=await Fixture.Create(); await f.Admin(); await f.Observe();
        var push=new Channel("Push"){Result="Failed"};var email=new Channel("Email");
        for(var attempt=0;attempt<5;attempt++)
        { await f.Observe(); await f.Dispatcher(push,email).SendPendingAsync(); f.Clock.Now=f.Clock.Now.AddMinutes(10);f.Db.ChangeTracker.Clear(); }
        Assert.Equal(5,push.Calls);Assert.Equal(1,email.Calls);
        var row=await f.Db.Set<OperationsAlertOutbox>().AsNoTracking().SingleAsync(x=>x.Channel=="Push");Assert.Equal("Failed",row.Status);Assert.Equal(5,row.Attempts);
        await f.Dispatcher(push,email).SendPendingAsync();Assert.Equal(5,push.Calls);
    }
    [Fact] public async Task RestartReclaimsExpiredLeaseAndResolvedIssueIsSuppressed()
    {
        await using var f=await Fixture.Create(); await f.Admin(); await f.Observe();
        var row=await f.Db.Set<OperationsAlertOutbox>().SingleAsync(x=>x.Channel=="Push");row.Status="Processing";row.LeaseUntilUtc=f.Clock.Now.AddSeconds(-1);await f.Db.SaveChangesAsync();f.Db.ChangeTracker.Clear();
        var push=new Channel("Push");var email=new Channel("Email");await f.Dispatcher(push,email).SendPendingAsync();Assert.Equal(1,push.Calls);
        await f.Observe(false);await f.Observe();await f.Observe(false);await f.Dispatcher(push,email).SendPendingAsync();
        Assert.Equal(1,push.Calls);Assert.Contains(await f.Db.Set<OperationsAlertOutbox>().AsNoTracking().ToListAsync(),x=>x.Status=="Suppressed");
    }
    [Fact] public async Task RevokedAdminIsNotSentAnyNotification()
    {
        await using var f=await Fixture.Create();f.Options.Value.ExternalSendEnabled=true;await f.Observe();var push=new Channel("Push");var email=new Channel("Email");
        await f.Dispatcher(push,email).SendPendingAsync();Assert.Equal(0,push.Calls+email.Calls);
        Assert.All(await f.Db.Set<OperationsAlertOutbox>().AsNoTracking().ToListAsync(),x=>Assert.Equal("Suppressed",x.Status));
    }
    [Fact] public async Task StaleSourcePreventsSendAndAcknowledgeUntilReverified()
    {
        await using var f=await Fixture.Create();await f.Admin();await f.Observe();var row=await f.Single();var rev=row.Revision;f.Clock.Now=f.Clock.Now.AddMinutes(3);
        var push=new Channel("Push");var email=new Channel("Email");await f.Dispatcher(push,email).SendPendingAsync();Assert.Equal(0,push.Calls+email.Calls);
        await Assert.ThrowsAsync<OperationsAlertConflictException>(()=>f.Coordinator.AcknowledgeAsync(row.Id,"admin",new(Guid.NewGuid(),rev)));
        await f.Observe();await f.Dispatcher(push,email).SendPendingAsync();Assert.Equal(2,push.Calls+email.Calls);
    }
    [Fact] public async Task MissingChannelConfigurationIsVisibleAndDoesNotConsumeRetries()
    {
        await using var f=await Fixture.Create();await f.Admin();await f.Observe();var push=new Channel("Push"){Result="ConfigurationRequired"};var email=new Channel("Email");
        await f.Dispatcher(push,email).SendPendingAsync();var row=await f.Db.Set<OperationsAlertOutbox>().AsNoTracking().SingleAsync(x=>x.Channel=="Push");
        Assert.Equal(0,row.Attempts);Assert.Equal("ConfigurationRequired",row.Status);
    }
    [Fact] public async Task BudgetStopsNewQuotesButAllowsActiveWorkAndSurvivesRestart()
    {
        await using var f=await Fixture.Create();var options=new BudgetMonitor();var gate=new OperationsBudgetGate(f.Db,options,f.Clock);
        Assert.True(await gate.ConsumeAsync("routes",false,default));Assert.False(await gate.ConsumeAsync("routes",false,default));
        f.Db.ChangeTracker.Clear();Assert.False(await new OperationsBudgetGate(f.Db,options,f.Clock).ConsumeAsync("routes",false,default));
        Assert.True(await gate.ConsumeAsync("routes",true,default));f.Clock.Now=f.Clock.Now.AddDays(1);Assert.True(await gate.ConsumeAsync("routes",false,default));
    }
    private sealed class BudgetMonitor : IOptionsMonitor<OperationsBudgetOptions>
    { public OperationsBudgetOptions CurrentValue=>new(){Enabled=true,ExternalRouteRequestsPerDay=1};public OperationsBudgetOptions Get(string? name)=>CurrentValue;public IDisposable? OnChange(Action<OperationsBudgetOptions,string?> listener)=>null; }
    private sealed class Channel(string key) : IOperationsAlertChannel
    { public string Key=>key; public int Calls;public string Result="Sent";public Task<string> SendAsync(OperationsAlertOutbox row,CancellationToken ct){Calls++;return Task.FromResult(Result);} }
    [Fact] public async Task UiDropsLatePreviousAccountResponse()
    {
        var client=new Client(); using var state=new OperationsAlertsState(client); state.SetOwner("first");
        var pending=state.LoadAsync("a"); state.SetOwner("second"); client.Pending.SetResult(Dto()); await pending;
        Assert.Null(state.Detail); Assert.False(state.CanAcknowledge);
    }
    [Fact] public async Task UiReadFailureRetainsDetailButDisablesMutation()
    {
        var client=new Client(); client.Pending.SetResult(Dto()); using var state=new OperationsAlertsState(client); state.SetOwner("admin"); await state.LoadAsync("a");
        Assert.True(state.CanAcknowledge); client.Fail=true; await state.LoadAsync("a"); Assert.NotNull(state.Detail); Assert.False(state.CanAcknowledge); Assert.NotNull(state.Error);
    }
    private static OperationsAlertDto Dto()=>new("a",1,2,"Urgent","Open","거래 문제 접수","확인","/privacy-support/case",DateTime.UtcNow,DateTime.UtcNow,true,[]);
    private sealed class Client : IOperationsAlertsClient
    {
        public bool Fail; public TaskCompletionSource<OperationsAlertDto?> Pending=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<OperationsAlertDto?> DetailAsync(string id,CancellationToken ct)=>Fail?Task.FromException<OperationsAlertDto?>(new Exception("private")):Pending.Task;
        public Task<OperationsAlertsPage> ListAsync(string status,int page,CancellationToken ct)=>throw new NotSupportedException();
        public Task<OperationsAlertDto> AcknowledgeAsync(string id,OperationsAlertAcknowledgeRequest input,CancellationToken ct)=>throw new NotSupportedException();
    }
    private sealed class Source : IOperationsAlertSource
    {
        public bool Fail; public string Key=>"support";
        public Task<IReadOnlyList<OperationsObservation>> ReadChangesAsync(DateTime at,string id,int take,CancellationToken ct)=>Fail?throw new Exception("private address") : Task.FromResult<IReadOnlyList<OperationsObservation>>([]);
        public Task<IReadOnlyList<OperationsObservation>> ReadCurrentAsync(string id,CancellationToken ct)=>throw new Exception("private address");
    }
    private sealed class Fixture : IAsyncDisposable
    {
        public required SqliteConnection Connection; public required SsalddelContext Db;
        public Clock Clock=new(); public Monitor Options=new(); public OperationsAlertCoordinator Coordinator=>new(Db,Options,Clock);
        public static async Task<Fixture> Create()
        {
            var connection=new SqliteConnection("Data Source=:memory:");await connection.OpenAsync();
            var db=new SsalddelContext(new DbContextOptionsBuilder<SsalddelContext>().UseSqlite(connection).Options,new Encryption());await db.Database.EnsureCreatedAsync();
            return new(){Connection=connection,Db=db};
        }
        public Task Observe(bool active=true)=>Coordinator.ObserveAsync(new("support","case-1","TransactionDispute","Urgent",active,Clock.Now),Clock.Now.AddHours(-1));
        public Task<OperationsAlert> Single()=>Db.Set<OperationsAlert>().SingleAsync();
        public OperationsAlertDispatcher Dispatcher(params IOperationsAlertChannel[] channels)=>new(Db,Options,Clock,channels,new 살뜰.Services.Options.SsalddelExecutionModePolicy(Microsoft.Extensions.Options.Options.Create(new 살뜰.Services.Options.SsalddelExecutionOptions())));
        public async Task Admin()
        {
            Options.Value.ExternalSendEnabled=true;Db.Users.Add(new ApplicationUser{Id="admin",UserName="admin"});Db.Roles.Add(new IdentityRole{Id="role",Name="서버관리자"});
            Db.UserRoles.Add(new IdentityUserRole<string>{UserId="admin",RoleId="role"});await Db.SaveChangesAsync();
        }
        public async ValueTask DisposeAsync(){await Db.DisposeAsync();await Connection.DisposeAsync();}
    }
    private sealed class Clock : TimeProvider { public DateTime Now=new(2080,1,1,23,0,0,DateTimeKind.Utc);public override DateTimeOffset GetUtcNow()=>new(Now); }
    private sealed class Monitor : IOptionsMonitor<OperationsAlertsOptions>
    { public OperationsAlertsOptions Value=new(){Enabled=true,OperatorUserId="admin"};public OperationsAlertsOptions CurrentValue=>Value;public OperationsAlertsOptions Get(string? name)=>Value;public IDisposable? OnChange(Action<OperationsAlertsOptions,string?> listener)=>null; }
    private sealed class Encryption : IPersonalDataEncryptionService
    { public string? Protect(string? v)=>v is null?null:Convert.ToBase64String(Encoding.UTF8.GetBytes(v));public string? Unprotect(string? v)=>v is null?null:Encoding.UTF8.GetString(Convert.FromBase64String(v)); }
}
