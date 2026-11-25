using FluentResults;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Application.HumanResources;
using Ssalddel.Application.Warehouse;
using Ssalddel.Contracts.Common.Hr;
using Ssalddel.Contracts.Common.Inventory;
using Ssalddel.Domain.HumanResources;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Notifications;
using 살뜰.Services.Options;
using 살뜰.도메인.창고;
using System.Text.Json;

namespace Ssalddel.Tests.Application.HumanResources;

public sealed class 근무가능요청UseCaseTests
{
    [Fact] public async Task 격리브라우저용으로_실제UseCase의요청답변확정응답을내보낸다()
    {
        await using var f = await Fixture.Create(); var input = f.Request();
        var directory = Environment.GetEnvironmentVariable("WORKFORCE_FIXTURE_EXPORT_DIR");
        if (!string.IsNullOrWhiteSpace(directory))
        {
            using var document = System.Text.Json.JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(directory, "../warehouse-member-time-boundary-20261108/usecase-fixtures.json")));
            var periodJson = document.RootElement.GetProperty("normal").GetProperty("selected").GetProperty("period");
            var period = periodJson.Deserialize<OutboundPeriodResponse>(new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!;
            f.Source.Group = period.Workflow!.SelectedGroup!;
            f.Source.Groups = period.Workflow.Groups;
            f.Source.WarehouseName = period.WarehouseName;
            f.Source.ObservedAtUtc = period.ObservedAtUtc;
            input.GroupKey = f.Source.Group.Key;
        }
        var context = (await f.Manager.문맥Async(input, default)).Value;
        var empty = (await f.Manager.목록Async(101, default)).Value;
        var created = (await f.Manager.생성Async(input, default)).Value;
        var second = f.Request(); second.WorkStartUtc = input.WorkStartUtc.AddDays(1); second.WorkEndUtc = input.WorkEndUtc.AddDays(1);
        second.ReplyByUtc = input.ReplyByUtc.AddDays(1); second.NeededWorkers = 2; second.ProposedHourlyAmount = 12000.5m;
        await f.Manager.생성Async(second, default);
        var managerCreated = (await f.Manager.목록Async(101, default)).Value;
        var inbox = (await f.Worker("a").목록Async(null, default)).Value;
        var preference = (await f.Worker("a").동의조회Async(default)).Value;
        var available = (await f.Reply("a", inbox.Items.Single(x => x.RequestId == created.RequestId).Recipients.Single(), 근무가능상태코드.Available)).Value;
        var managerAvailable = (await f.Manager.목록Async(101, default)).Value;
        var target = managerAvailable.Items.Single(x => x.RequestId == created.RequestId).Recipients.Single(x => x.Status == 근무가능상태코드.Available);
        var confirmed = (await f.Manager.답변Async(target.InvitationId, new() { Revision = target.Revision }, true, default, 101)).Value;
        var ownConfirmed = (await f.Worker("a").목록Async(null, default)).Value;
        if (string.IsNullOrWhiteSpace(directory)) return;
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, "usecase-fixtures.json"), System.Text.Json.JsonSerializer.Serialize(new { input, context, empty, created, managerCreated, inbox, preference, available, managerAvailable, confirmed, ownConfirmed }, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web) { WriteIndented = true }));
    }
    [Fact] public async Task 요청_본인답변_계획확정_철회는_고용과출근을변경하지않는다()
    {
        await using var f = await Fixture.Create();
        var created = await f.Manager.생성Async(f.Request(), default);
        Assert.True(created.IsSuccess, string.Join(",", created.Errors));
        Assert.Equal(0, created.Value.ConfirmedWorkers);
        var own = (await f.Worker("a").목록Async(null, default)).Value.Items.Single();
        Assert.Single(own.Recipients);
        Assert.Equal("내 답변", own.Recipients[0].DisplayName);
        var available = await f.Reply("a", own.Recipients[0], 근무가능상태코드.Available);
        Assert.Equal(0, available.Value.ConfirmedWorkers);
        var manager = (await f.Manager.목록Async(101, default)).Value.Items.Single();
        var candidate = manager.Recipients.Single(x => x.Status == 근무가능상태코드.Available);
        var confirmed = await f.Manager.답변Async(candidate.InvitationId, new() { Revision = candidate.Revision }, true, default, 101);
        Assert.Equal(1, confirmed.Value.ConfirmedWorkers);
        var mine = (await f.Worker("a").목록Async(null, default)).Value.Items.Single().Recipients.Single();
        var withdrawn = await f.Reply("a", mine, 근무가능상태코드.Withdrawn);
        Assert.Equal(0, withdrawn.Value.ConfirmedWorkers);
        Assert.Empty(f.Db.HrRoleAssignments);
        Assert.Equal(2, await f.Db.창고사용자.CountAsync());
        Assert.Equal(0, f.Kakao.Calls);
    }
    [Fact] public async Task 같은요청은_기한후에도동일결과_다른내용은충돌한다()
    {
        await using var f = await Fixture.Create(); var request = f.Request();
        var first = await f.Manager.생성Async(request, default);
        f.Clock.Current = request.WorkEndUtc.AddDays(1);
        var again = await f.Manager.생성Async(request, default);
        Assert.Equal(first.Value.RequestId, again.Value.RequestId);
        request.NeededWorkers = 2;
        AssertCode(409, await f.Manager.생성Async(request, default));
        Assert.Single(f.Db.근무가능요청);
    }
    [Theory]
    [InlineData("outsider")][InlineData("consent")][InlineData("group")][InlineData("quantity")][InlineData("incomplete")][InlineData("sms")][InlineData("kakao")][InlineData("null-key")]
    public async Task 원천과등록동의연결이없으면_저장발송하지않는다(string scenario)
    {
        await using var f = await Fixture.Create(); var request = f.Request();
        switch (scenario)
        {
            case "outsider": request.RecipientUserIds = ["unknown"]; break;
            case "consent": f.Db.근무연락동의.Single(x => x.UserId == "a").AllowInApp = false; await f.Db.SaveChangesAsync(); break;
            case "group": request.GroupKey = "forged"; break;
            case "quantity": f.Source.Group.CompletedQuantity = null; break;
            case "incomplete": f.Source.Group.MembershipComplete = false; break;
            case "sms": request.Channel = 근무연락채널.Sms; break;
            case "kakao": request.Channel = 근무연락채널.Kakao; break;
            case "null-key": request.GroupKey = null!; break;
        }
        var result = await f.Manager.생성Async(request, default);
        Assert.True(result.IsFailed); Assert.Empty(f.Db.근무가능요청); Assert.Empty(f.Db.근무가능대상); Assert.Equal(0, f.Kakao.Calls);
    }
    [Fact] public async Task 타인답변_다른창고확정_미로그인_비관리자를거부한다()
    {
        await using var f = await Fixture.Create(); var created = (await f.Manager.생성Async(f.Request(), default)).Value;
        var a = (await f.Worker("a").목록Async(null, default)).Value.Items.Single().Recipients.Single();
        AssertCode(404, await f.Reply("b", a, 근무가능상태코드.Available));
        AssertCode(404, await f.Manager.답변Async(a.InvitationId, new() { Revision = a.Revision }, true, default, 102));
        AssertCode(400, await f.Reply("a", a, 근무가능상태코드.Confirmed));
        AssertCode(404, await f.Worker("a").목록Async(101, default));
        AssertCode(401, await f.Worker("").목록Async(null, default));
        Assert.Empty((await f.Worker("unknown").목록Async(null, default)).Value.Items);
    }
    [Fact] public async Task 정원초과_오래된답변_겹친시간계획을거부한다()
    {
        await using var f = await Fixture.Create(); await f.Manager.생성Async(f.Request(), default);
        foreach (var id in new[] { "a", "b" })
        {
            var target = (await f.Worker(id).목록Async(null, default)).Value.Items.Single().Recipients.Single();
            var available = await f.Reply(id, target, 근무가능상태코드.Available);
            Assert.True(available.IsSuccess);
            AssertCode(409, await f.Reply(id, target, 근무가능상태코드.Unavailable));
        }
        var items = (await f.Manager.목록Async(101, default)).Value.Items.Single().Recipients;
        Assert.True((await f.Manager.답변Async(items[0].InvitationId, new() { Revision = items[0].Revision }, true, default, 101)).IsSuccess);
        AssertCode(409, await f.Manager.답변Async(items[1].InvitationId, new() { Revision = items[1].Revision }, true, default, 101));
        f.Source.Group.Key = "picking:second";
        var second = f.Request(); second.GroupKey = f.Source.Group.Key;
        Assert.True((await f.Manager.생성Async(second, default)).IsSuccess);
        var confirmedUser = await f.Db.근무가능대상.Where(x => x.Status == 근무가능상태코드.Confirmed).Select(x => x.UserId).SingleAsync();
        var newest = (await f.Worker(confirmedUser).목록Async(null, default)).Value.Items.Single(x => x.GroupKey == second.GroupKey).Recipients.Single();
        AssertCode(409, await f.Reply(confirmedUser, newest, 근무가능상태코드.Available));
    }
    [Fact] public async Task 기한뒤에는답변불가_업무시작전철회는가능_재조회시각은UTC다()
    {
        await using var f = await Fixture.Create(); var request = f.Request(); request.GroupPage = 1; await f.Manager.생성Async(request, default);
        f.Db.ChangeTracker.Clear();
        var mine = (await f.Worker("a").목록Async(null, default)).Value.Items.Single();
        Assert.Equal(DateTimeKind.Utc, mine.WorkStartUtc.Kind); Assert.Equal(request.PeriodStartUtc, mine.PeriodStartUtc);
        Assert.Equal(1, mine.GroupPage);
        Assert.EndsWith("Z\"", System.Text.Json.JsonSerializer.Serialize(mine.WorkStartUtc));
        f.Clock.Current = request.ReplyByUtc.AddMinutes(1);
        AssertCode(409, await f.Reply("a", mine.Recipients.Single(), 근무가능상태코드.Available));
        Assert.True((await f.Reply("a", mine.Recipients.Single(), 근무가능상태코드.Withdrawn)).IsSuccess);
    }
    [Theory][InlineData(true)][InlineData(false)]
    public async Task 카카오는신규대상만한번접수하고_불명확결과를재발송하지않는다(bool accepted)
    {
        await using var f = await Fixture.Create(); f.EnableKakao(); f.Kakao.Accepted = accepted;
        var request = f.Request(); request.Channel = 근무연락채널.Kakao;
        var first = await f.Manager.생성Async(request, default);
        Assert.True(first.IsSuccess); Assert.Equal(2, f.Kakao.Calls);
        Assert.All(first.Value.Recipients, x => Assert.Equal(accepted ? "ProviderAccepted" : "Uncertain", x.DeliveryStatus));
        await f.Manager.생성Async(request, default); Assert.Equal(2, f.Kakao.Calls);
    }
    [Fact] public async Task 수신설정은본인만저장하고_오래된판본을덮어쓰지않는다()
    {
        await using var f = await Fixture.Create(); var use = f.Worker("c");
        Assert.False((await use.동의조회Async(default)).Value.AllowInApp);
        AssertCode(400, await use.동의저장Async(new() { AllowKakao = true }, default));
        Assert.True((await use.동의저장Async(new() { AllowInApp = true }, default)).IsSuccess);
        AssertCode(409, await use.동의저장Async(new(), default));
    }
    [Fact] public void 추정은양수가정만사용하고_정밀도범위밖은미확인이다()
    {
        Assert.Equal(3, 근무인력추정.필요인원(101, 20, 2));
        Assert.Null(근무인력추정.필요인원(100, null, 2)); Assert.Null(근무인력추정.필요인원(100, 0, 2));
        Assert.Null(근무인력추정.필요인원(1, 0.0000000000000000000000000001m, 0.0000000000000000000000000001m));
    }
    [Fact] public async Task 최근50개뒤의요청도_다음페이지에서확인할수있다()
    {
        await using var f = await Fixture.Create();
        for (var i = 0; i < 51; i++) f.Db.근무가능요청.Add(new() { Id = Guid.NewGuid(), ManagerUserId = "manager", ClientRequestId = Guid.NewGuid(), WarehouseId = 101, CreatedAtUtc = f.Clock.Current.AddMinutes(i) });
        await f.Db.SaveChangesAsync();
        var first = (await f.Manager.목록Async(101, default)).Value;
        var second = (await f.Manager.목록Async(101, default, 1)).Value;
        Assert.Equal(50, first.Items.Count); Assert.True(first.HasMore); Assert.Single(second.Items); Assert.False(second.HasMore);
        Assert.DoesNotContain(second.Items[0].RequestId, first.Items.Select(x => x.RequestId));
        AssertCode(400, await f.Manager.목록Async(101, default, -1));
    }
    [Fact] public async Task 외부전송직전의동의철회는_남은전송을막는다()
    {
        await using var f = await Fixture.Create(); f.EnableKakao();
        f.Kakao.OnSend = async () => { foreach (var pref in f.Db.근무연락동의) pref.AllowKakao = false; await f.Db.SaveChangesAsync(); };
        var request = f.Request(); request.Channel = 근무연락채널.Kakao;
        var result = (await f.Manager.생성Async(request, default)).Value;
        Assert.Equal(1, f.Kakao.Calls);
        Assert.Single(result.Recipients, x => x.DeliveryStatus == "NotSent");
        Assert.Single(result.Recipients, x => x.DeliveryStatus == "ProviderAccepted");
    }
    [Fact] public async Task 답변마감후에도_이미가능한답변을업무시작전계획에확정한다()
    {
        await using var f = await Fixture.Create(); var input = f.Request(); await f.Manager.생성Async(input, default);
        var mine = (await f.Worker("a").목록Async(null, default)).Value.Items.Single().Recipients.Single();
        await f.Reply("a", mine, 근무가능상태코드.Available);
        f.Clock.Current = input.ReplyByUtc.AddMinutes(1);
        var target = (await f.Manager.목록Async(101, default)).Value.Items.Single().Recipients.Single(x => x.Status == 근무가능상태코드.Available);
        Assert.True(target.CanConfirm);
        Assert.True((await f.Manager.답변Async(target.InvitationId, new() { Revision = target.Revision }, true, default, 101)).IsSuccess);
        var duplicate = f.Request(); duplicate.WorkStartUtc = input.WorkStartUtc; duplicate.WorkEndUtc = input.WorkEndUtc; duplicate.ReplyByUtc = f.Clock.Current.AddMinutes(10);
        AssertCode(409, await f.Manager.생성Async(duplicate, default));
    }
    [Fact] public async Task 금액은저장정밀도를넘겨자동반올림하지않는다()
    {
        await using var f = await Fixture.Create(); var input = f.Request(); input.ProposedHourlyAmount = 12000.125m;
        AssertCode(400, await f.Manager.생성Async(input, default)); Assert.Empty(f.Db.근무가능요청);
        input.ProposedHourlyAmount = 12000.5m;
        Assert.Equal(12000.5m, (await f.Manager.생성Async(input, default)).Value.ProposedHourlyAmount);
    }
    private static void AssertCode<T>(int code, Result<T> result) { Assert.True(result.IsFailed); Assert.Equal(code, result.Errors[0].Metadata["StatusCode"]); }

    internal sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        internal SsalddelContext Db { get; }
        internal FakeSource Source { get; } = new();
        internal FakeKakao Kakao { get; } = new();
        internal Clock Clock { get; } = new();
        private readonly KakaoAlimTalkOptions options = new();
        internal 근무가능요청UseCase Manager => Make("manager", "창고관리자");
        internal 근무가능요청UseCase Worker(string id) => Make(id, "창고작업자");
        private 근무가능요청UseCase Make(string id, string role) => new(Db, new Actor(id, role), Source, Kakao, Options.Create(options), Clock);
        private Fixture(SqliteConnection c, SsalddelContext db) { connection = c; Db = db; }
        internal 근무가능요청등록요청 Request() => new() { ClientRequestId = Guid.NewGuid(), WarehouseId = 101, Stage = "picking", GroupKey = Source.Group.Key,
            PeriodStartUtc = Clock.Current.Date.AddDays(-1), PeriodEndUtc = Clock.Current.Date.AddDays(-1).AddHours(3),
            WorkStartUtc = Clock.Current.AddHours(4), WorkEndUtc = Clock.Current.AddHours(8), ReplyByUtc = Clock.Current.AddHours(3), NeededWorkers = 1, RecipientUserIds = ["a", "b"], ConfirmedAvailabilityOnly = true };
        internal Task<Result<근무가능요청응답>> Reply(string id, 근무가능대상응답 target, string status)
            => Worker(id).답변Async(target.InvitationId, new() { Revision = target.Revision, Status = status }, false, default);
        internal void EnableKakao() { options.Enabled = true; options.BaseUrl = "https://example.invalid"; options.ApiKey = "test"; options.SenderKey = "test"; options.WorkforceAvailabilityTemplateCode = "fixture"; options.WorkforceReplyBaseUrl = "https://example.invalid"; }
        internal static async Task<Fixture> Create()
        {
            var c = new SqliteConnection("Data Source=:memory:"); await c.OpenAsync();
            var db = new SsalddelContext(new DbContextOptionsBuilder<SsalddelContext>().UseSqlite(c).Options, new Encryption());
            await db.Database.EnsureCreatedAsync();
            db.창고.Add(new 창고 { Id = 101, 소유자UserId = "manager", 창고명 = "가상 동쪽 창고" });
            foreach (var id in new[] { "a", "b" })
            {
                db.Users.Add(new() { Id = id, UserName = "가상 작업자 " + id, PhoneNumber = "01000000000", PhoneNumberConfirmed = true });
                db.창고사용자.Add(new() { 창고Id = 101, UserId = id, 역할명 = "피킹 작업자" });
                db.근무연락동의.Add(new() { UserId = id, AllowInApp = true, AllowKakao = true });
            }
            await db.SaveChangesAsync(); return new(c, db);
        }
        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await connection.DisposeAsync(); }
    }
    internal sealed class Clock : TimeProvider
    {
        internal DateTime Current = new(2026, 11, 8, 0, 0, 0, DateTimeKind.Utc);
        public override DateTimeOffset GetUtcNow() => new(Current);
    }
    private sealed record Actor(string? UserId, string? Role) : ICurrentUserAccessor;
    private sealed class Encryption : IPersonalDataEncryptionService { public string? Protect(string? value) => value; public string? Unprotect(string? value) => value; }
    internal sealed class FakeKakao : IKakaoAlimTalkService
    {
        internal int Calls; internal bool Accepted = true;
        internal Func<Task>? OnSend;
        public async Task<bool> SendAsync(KakaoAlimTalkMessage message, CancellationToken ct = default) { Calls++; if (OnSend is not null) await OnSend(); return Accepted; }
    }
    internal sealed class FakeSource : I출고예정검토UseCase
    {
        internal string WarehouseName = "가상 동쪽 창고";
        internal DateTime ObservedAtUtc = new(2026, 11, 8, 0, 0, 0, DateTimeKind.Utc);
        internal OutboundPeriodGroup Group = new() { Key = "picking:batch:11", Label = "오전 상품 피킹 · 가상 묶음", Quantity = 120, CompletedQuantity = 20, MembershipComplete = true, TaskCount = 3, SourcePlanCount = 2 };
        internal IReadOnlyList<OutboundPeriodGroup>? Groups;
        public Task<Result<출고예정검토목록페이지응답>> 목록Async(출고예정검토목록조회요청 request, CancellationToken ct)
            => Task.FromResult(Result.Ok(new 출고예정검토목록페이지응답 { Period = new() { WarehouseId = 101, WarehouseName = WarehouseName, ObservedAtUtc = ObservedAtUtc,
                Workflow = new() { SelectedStage = "picking", Groups = Groups ?? [Group], SelectedGroup = request.BatchKey == Group.Key ? Group : null } } }));
        public Task<Result<출고예정검토상세응답>> 상세Async(long id, CancellationToken ct) => throw new NotSupportedException();
    }
}
