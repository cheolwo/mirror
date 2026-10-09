using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Core.Events;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.PrivacyRetention;
using Ssalddel.Services.Community;
using Ssalddel.Services.PrivacyRetention;
using 살뜰.Services.Options;

namespace Ssalddel.Tests.Services.Community;

public sealed class CommunityLedgerRelationMongoIntegrationTests
{
    [OrderLedgerMongoTheory]
    [InlineData(false, "detach")]
    [InlineData(true, "detach")]
    [InlineData(false, "role")]
    [InlineData(true, "role")]
    [InlineData(false, "optional")]
    [InlineData(true, "optional")]
    public async Task Child_progress_between_transaction_read_and_fence_write_prevents_destructive_root_save(
        bool genericProgress, string change)
    {
        await using var fixture = await Fixture.CreateAsync();
        var root = await fixture.SeedAsync();
        using var pause = new CommandPause(command => IsFenceWrite(command, "child-1"));
        var editingStore = fixture.CreateStore(pause.Observe);
        await editingStore.원장조회Async("root-1");
        pause.Arm();
        var editing = Task.Run(() => editingStore.원장저장Async(Change(root, change), "root-editor"));
        try
        {
            await pause.Reached.Task.WaitAsync(TimeSpan.FromSeconds(20));
            await ProgressAsync(fixture.Store, (await fixture.Store.원장조회Async("child-1"))!, genericProgress);
        }
        finally { pause.Release(); await Record.ExceptionAsync(() => editing); }

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => editing);
        Assert.Contains("다른 요청에서 먼저 변경", failure.Message);
        var rootAfter = (await fixture.Store.원장조회Async("root-1"))!;
        Assert.Equal(root.Revision, rootAfter.Revision);
        var reference = Assert.Single(rootAfter.포함원장목록);
        Assert.Equal(주문원장포함역할.운송, reference.역할);
        Assert.True(reference.필수여부);
        var childAfter = await fixture.ReadDocumentAsync("child-1");
        Assert.Equal(커뮤니티원장상태.진행중, childAfter.상태);
        Assert.Equal(2, childAfter.Revision);
        Assert.Equal(0, childAfter.관계편집Revision);
        Assert.Single(childAfter.상태이력, x => x.상태 == 커뮤니티원장상태.진행중);
        Assert.False(pause.TimedOut);
    }

    [OrderLedgerMongoTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Committed_relationship_fence_rejects_stale_generic_and_state_writes_then_allows_canonical_retry(bool genericProgress)
    {
        await using var fixture = await Fixture.CreateAsync();
        var root = await fixture.SeedAsync();
        var before = await fixture.ReadDocumentAsync("child-1");
        using var pause = new CommandPause(command => IsChildProgressWrite(command, "child-1"));
        var progressingStore = fixture.CreateStore(pause.Observe);
        var child = (await progressingStore.원장조회Async("child-1"))!;
        pause.Arm();
        var progressing = Task.Run(() => ProgressAsync(progressingStore, child, genericProgress));
        try
        {
            await pause.Reached.Task.WaitAsync(TimeSpan.FromSeconds(20));
            await fixture.Store.원장저장Async(Change(root, "detach"), "root-editor");
        }
        finally { pause.Release(); await Record.ExceptionAsync(() => progressing); }

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => progressing);
        Assert.Contains("다른 요청에서 먼저 변경", failure.Message);
        Assert.Empty((await fixture.Store.원장조회Async("root-1"))!.포함원장목록);
        var after = await fixture.ReadDocumentAsync("child-1");
        Assert.Equal(1, after.관계편집Revision);
        Assert.Equal(before.Revision, after.Revision);
        Assert.Equal(before.상태, after.상태);
        Assert.Equal(before.투영EventId, after.투영EventId);
        Assert.Equal(before.투영상태, after.투영상태);
        Assert.Equal(before.상태이력.Count, after.상태이력.Count);
        var latest = (await fixture.Store.원장조회Async("child-1"))!;
        await ProgressAsync(fixture.Store, latest, genericProgress);
        after = await fixture.ReadDocumentAsync("child-1");
        Assert.Equal(커뮤니티원장상태.진행중, after.상태);
        Assert.Equal(2, after.Revision);
        Assert.Equal(1, after.관계편집Revision);
        Assert.False(pause.TimedOut);
    }

    [OrderLedgerMongoFact]
    public async Task Conflict_on_later_child_rolls_back_earlier_fence_and_the_entire_relationship_edit()
    {
        await using var fixture = await Fixture.CreateAsync();
        var root = await fixture.SeedAsync();
        await fixture.Store.원장저장Async(new()
        { 원장Id = "child-2", 원장템플릿Key = CommunityLedgerTemplateKeys.CargoTransport, 제목 = "두 번째 독립 운송" }, "owner");
        var attach = Save(root); attach.포함원장목록 = [Reference("child-1"), Reference("child-2", order: 1)];
        root = await fixture.Store.원장저장Async(attach, "owner");
        using var pause = new CommandPause(command => IsFenceWrite(command, "child-2"));
        var editingStore = fixture.CreateStore(pause.Observe);
        await editingStore.원장조회Async("root-1");
        var detach = Save(root); detach.포함원장목록 = [];
        pause.Arm();
        var editing = Task.Run(() => editingStore.원장저장Async(detach, "root-editor"));
        try
        {
            await pause.Reached.Task.WaitAsync(TimeSpan.FromSeconds(20));
            await ProgressAsync(fixture.Store, (await fixture.Store.원장조회Async("child-2"))!, generic: false);
        }
        finally { pause.Release(); await Record.ExceptionAsync(() => editing); }
        await Assert.ThrowsAsync<InvalidOperationException>(() => editing);
        var rootAfter = (await fixture.Store.원장조회Async("root-1"))!;
        Assert.Equal(root.Revision, rootAfter.Revision);
        Assert.Equal(2, rootAfter.포함원장목록.Count);
        Assert.Equal(0, (await fixture.ReadDocumentAsync("child-1")).관계편집Revision);
        Assert.Equal(0, (await fixture.ReadDocumentAsync("child-2")).관계편집Revision);
        Assert.Equal(커뮤니티원장상태.초안, (await fixture.Store.원장조회Async("child-1"))!.상태);
        Assert.Equal(커뮤니티원장상태.진행중, (await fixture.Store.원장조회Async("child-2"))!.상태);
        Assert.False(pause.TimedOut);
    }

    [OrderLedgerMongoFact]
    public async Task Active_child_attach_ordering_and_required_strengthening_keep_normal_save_semantics()
    {
        await using var fixture = await Fixture.CreateAsync();
        var root = await fixture.SeedAsync(required: false);
        await fixture.Store.원장저장Async(new()
        {
            원장Id = "active-child", 원장템플릿Key = CommunityLedgerTemplateKeys.CargoTransport,
            제목 = "진행 중인 독립 운송", 상태 = 커뮤니티원장상태.진행중
        }, "worker");
        var request = Save(root);
        request.포함원장목록 = [Reference("child-1", required: true, order: 8), Reference("active-child", order: 2)];
        var saved = await fixture.Store.원장저장Async(request, "root-editor");
        Assert.Equal(2, saved.포함원장목록.Count);
        Assert.True(saved.포함원장목록.Single(x => x.원장Id == "child-1").필수여부);
        Assert.Equal(0, (await fixture.ReadDocumentAsync("child-1")).관계편집Revision);
        Assert.Equal(0, (await fixture.ReadDocumentAsync("active-child")).관계편집Revision);
    }

    [OrderLedgerMongoTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Changing_only_child_id_case_cannot_remove_an_existing_progressed_relationship(bool genericProgress)
    {
        await using var fixture = await Fixture.CreateAsync();
        var root = await fixture.SeedAsync();
        await ProgressAsync(fixture.Store, (await fixture.Store.원장조회Async("child-1"))!, genericProgress);
        var before = await fixture.ReadDocumentAsync("child-1");
        var request = Save(root); request.포함원장목록 = [Reference("CHILD-1")];
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Store.원장저장Async(request, "root-editor"));
        Assert.Contains("진행", failure.Message);
        var rootAfter = (await fixture.Store.원장조회Async("root-1"))!;
        Assert.Equal(root.Revision, rootAfter.Revision);
        Assert.Equal("child-1", Assert.Single(rootAfter.포함원장목록).원장Id);
        var childAfter = await fixture.ReadDocumentAsync("child-1");
        Assert.Equal(before.Revision, childAfter.Revision);
        Assert.Equal(before.상태이력.Count, childAfter.상태이력.Count);
        Assert.Equal(커뮤니티원장상태.진행중, childAfter.상태);
        Assert.Equal(0, childAfter.관계편집Revision);
        Assert.Null(await fixture.Store.원장조회Async("CHILD-1"));
    }

    [OrderLedgerMongoFact]
    public async Task Draft_child_case_change_to_an_absent_exact_identity_leaves_relationship_and_child_untouched()
    {
        await using var fixture = await Fixture.CreateAsync();
        var root = await fixture.SeedAsync();
        var child = await fixture.ReadDocumentAsync("child-1");
        var request = Save(root); request.포함원장목록 = [Reference("CHILD-1")];
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Store.원장저장Async(request, "root-editor"));
        Assert.Contains("OrderLedgerChildIdentityUnverified", failure.Message);
        var rootAfter = (await fixture.Store.원장조회Async("root-1"))!;
        Assert.Equal(root.Revision, rootAfter.Revision);
        Assert.Equal("child-1", Assert.Single(rootAfter.포함원장목록).원장Id);
        var childAfter = await fixture.ReadDocumentAsync("child-1");
        Assert.Equal(child.Revision, childAfter.Revision);
        Assert.Equal(child.투영EventId, childAfter.투영EventId);
        Assert.Equal(child.상태이력.Count, childAfter.상태이력.Count);
        Assert.Equal(0, childAfter.관계편집Revision);
        Assert.Null(await fixture.Store.원장조회Async("CHILD-1"));
    }

    [OrderLedgerMongoFact]
    public async Task Draft_relationship_can_be_replaced_by_a_distinct_existing_case_sensitive_active_child()
    {
        await using var fixture = await Fixture.CreateAsync();
        var root = await fixture.SeedAsync();
        await fixture.Store.원장저장Async(new()
        { 원장Id = "CHILD-1", 원장템플릿Key = CommunityLedgerTemplateKeys.CargoTransport,
            제목 = "정확 ID가 다른 독립 운송", 상태 = 커뮤니티원장상태.진행중 }, "worker");
        var request = Save(root); request.포함원장목록 = [Reference("CHILD-1")];
        var saved = await fixture.Store.원장저장Async(request, "root-editor");
        Assert.Equal("CHILD-1", Assert.Single(saved.포함원장목록).원장Id);
        Assert.Equal(root.Revision + 1, saved.Revision);
        var original = await fixture.ReadDocumentAsync("child-1");
        Assert.Equal(커뮤니티원장상태.초안, original.상태);
        Assert.Equal(1, original.Revision);
        Assert.Equal(1, original.관계편집Revision);
        var newlyLinked = await fixture.ReadDocumentAsync("CHILD-1");
        Assert.Equal(커뮤니티원장상태.진행중, newlyLinked.상태);
        Assert.Equal(1, newlyLinked.Revision);
        Assert.Equal(0, newlyLinked.관계편집Revision);
    }

    [OrderLedgerMongoFact]
    public async Task Generic_root_save_revalidates_current_root_and_child_even_when_request_claims_draft()
    {
        await using var fixture = await Fixture.CreateAsync();
        var root = await fixture.SeedAsync();
        await ProgressAsync(fixture.Store, (await fixture.Store.원장조회Async("child-1"))!, generic: true);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Store.원장저장Async(Change(root, "detach"), "root-editor"));
        Assert.Single((await fixture.Store.원장조회Async("root-1"))!.포함원장목록);
        var progressedRoot = (await fixture.Store.원장상태변경Async(new()
        { 원장Id = root.원장Id, 기대Revision = root.Revision, 상태 = 커뮤니티원장상태.진행중 }, "root-worker"))!;
        var resetAndDetach = Change(progressedRoot, "detach"); resetAndDetach.상태 = 커뮤니티원장상태.초안;
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Store.원장저장Async(resetAndDetach, "root-editor"));
        Assert.Equal(커뮤니티원장상태.진행중, (await fixture.Store.원장조회Async("root-1"))!.상태);
    }

    [OrderLedgerMongoTheory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Progress_then_generic_draft_reset_keeps_relationship_locked_and_history_intact(bool rootProgressed)
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.SeedAsync();
        var id = rootProgressed ? "root-1" : "child-1";
        await ProgressAsync(fixture.Store, (await fixture.Store.원장조회Async(id))!, generic: false);
        var progressed = (await fixture.Store.원장조회Async(id))!;
        var reset = Save(progressed); reset.상태 = 커뮤니티원장상태.초안;
        await fixture.Store.원장저장Async(reset, "correction-editor");
        var root = (await fixture.Store.원장조회Async("root-1"))!;
        var child = (await fixture.Store.원장조회Async("child-1"))!;
        var rootHistory = root.상태이력.Select(x => (x.EventId, x.상태, x.변경시각Utc)).ToArray();
        var childHistory = child.상태이력.Select(x => (x.EventId, x.상태, x.변경시각Utc)).ToArray();
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Store.원장저장Async(Change(root, "detach"), "root-editor"));
        var rootAfter = (await fixture.Store.원장조회Async("root-1"))!;
        var childAfter = (await fixture.Store.원장조회Async("child-1"))!;
        Assert.Single(rootAfter.포함원장목록);
        Assert.Equal(root.Revision, rootAfter.Revision);
        Assert.Equal(child.Revision, childAfter.Revision);
        Assert.Equal(rootHistory, rootAfter.상태이력.Select(x => (x.EventId, x.상태, x.변경시각Utc)).ToArray());
        Assert.Equal(childHistory, childAfter.상태이력.Select(x => (x.EventId, x.상태, x.변경시각Utc)).ToArray());
        var resetLedger = rootProgressed ? rootAfter : childAfter;
        Assert.Equal(커뮤니티원장상태.초안, resetLedger.상태);
        Assert.Contains(resetLedger.상태이력, x => x.상태 == 커뮤니티원장상태.진행중);
        Assert.Equal(0, (await fixture.ReadDocumentAsync("child-1")).관계편집Revision);
    }

    [OrderLedgerMongoFact]
    public async Task Legacy_missing_fence_is_zero_and_projection_and_privacy_replacement_preserve_committed_fence()
    {
        await using var fixture = await Fixture.CreateAsync();
        var child = await fixture.Store.원장저장Async(new()
        {
            원장Id = "child-1", 원장템플릿Key = CommunityLedgerTemplateKeys.FoodOrder,
            제목 = "개인정보 보호 시험 음식", 외부참조 = new Dictionary<string, string> { ["음식주문번호"] = "only-isolated-order" },
            블록목록 = [new() { BlockId = "recipient", BlockType = CommunityLedgerBlockTypes.Order, Title = "수령",
                Data = new Dictionary<string, string> { ["연락처전화번호"] = "010-0000-1111", ["금액"] = "1200" } }]
        }, "owner");
        var root = await fixture.Store.원장저장Async(new()
        {
            원장Id = "root-1", 원장템플릿Key = CommunityLedgerTemplateKeys.Order, 제목 = "원주문",
            포함원장목록 = [new() { 원장Id = child.원장Id, 원장템플릿Key = child.원장템플릿Key, 역할 = 주문원장포함역할.배송, 필수여부 = true }]
        }, "owner");
        await fixture.Raw.UpdateOneAsync(new BsonDocument("원장Id", "child-1"), new BsonDocument("$unset", new BsonDocument("관계편집Revision", 1)));
        await fixture.Store.원장저장Async(Change(root, "detach"), "root-editor");
        Assert.Equal(1, (await fixture.ReadDocumentAsync("child-1")).관계편집Revision);
        await fixture.Raw.UpdateOneAsync(new BsonDocument("원장Id", "child-1"),
            new BsonDocument("$set", new BsonDocument("투영다음시도시각Utc", DateTime.UtcNow.AddMinutes(-1))));
        var projection = new Mongo커뮤니티원장투영작업저장소(fixture.Client, fixture.Options);
        var work = await projection.다음작업확보Async(TimeSpan.FromMinutes(5));
        Assert.NotNull(work); Assert.Equal("child-1", work.원장.원장Id);
        await projection.완료Async(work.원장.원장Id, work.원장.Revision, work.ProcessingToken);
        Assert.Equal(1, (await fixture.ReadDocumentAsync("child-1")).관계편집Revision);
        var purge = new 배송원장개인정보파기Service(fixture.Client, fixture.Options);
        Assert.True(await purge.파기Async("child-1", 개인정보파기원천Codes.FoodOrder, "only-isolated-order", default));
        var purged = await fixture.ReadDocumentAsync("child-1");
        Assert.Equal(1, purged.관계편집Revision);
        Assert.Equal(2, purged.Revision);
        Assert.DoesNotContain("연락처전화번호", purged.블록목록.Single().Data.Keys);
        Assert.Equal("1200", purged.블록목록.Single().Data["금액"]);
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await fixture.Store.원장저장Async(
            Save((await fixture.Store.원장조회Async("child-1"))!), "owner"));
    }

    [OrderLedgerStandaloneMongoFact]
    public async Task Standalone_keeps_non_destructive_writes_but_fails_closed_for_destructive_relationship_change()
    {
        await using var fixture = await Fixture.CreateAsync(standalone: true);
        var root = await fixture.SeedAsync(required: false);
        var reorder = Save(root); reorder.포함원장목록 = [Reference("child-1", required: true, order: 3)];
        root = await fixture.Store.원장저장Async(reorder, "root-editor");
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Store.원장저장Async(Change(root, "detach"), "root-editor"));
        Assert.Contains("OrderLedgerRelationTransactionRequired", failure.Message);
        Assert.Equal(root.Revision, (await fixture.Store.원장조회Async("root-1"))!.Revision);
        Assert.Single((await fixture.Store.원장조회Async("root-1"))!.포함원장목록);
        Assert.Equal(0, (await fixture.ReadDocumentAsync("child-1")).관계편집Revision);
        await ProgressAsync(fixture.Store, (await fixture.Store.원장조회Async("child-1"))!, generic: false);
        Assert.Equal(커뮤니티원장상태.진행중, (await fixture.Store.원장조회Async("child-1"))!.상태);
    }

    private static async Task ProgressAsync(Mongo커뮤니티원장저장소 store, 커뮤니티원장Dto child, bool generic)
    {
        if (generic)
        {
            var request = Save(child); request.상태 = 커뮤니티원장상태.진행중;
            await store.원장저장Async(request, "child-worker");
        }
        else
            await store.원장상태변경Async(new() { 원장Id = child.원장Id, 기대Revision = child.Revision,
                이전상태 = child.상태, 상태 = 커뮤니티원장상태.진행중 }, "child-worker");
    }

    private static 커뮤니티원장저장요청 Change(커뮤니티원장Dto root, string change)
    {
        var request = Save(root);
        var reference = root.포함원장목록.Single();
        request.포함원장목록 = change == "detach" ? [] : [new()
        {
            원장Id = reference.원장Id, 원장템플릿Key = reference.원장템플릿Key,
            역할 = change == "role" ? 주문원장포함역할.배송 : reference.역할,
            관계유형 = reference.관계유형, 필수여부 = change != "optional" && reference.필수여부,
            표시순서 = reference.표시순서
        }];
        return request;
    }

    private static 커뮤니티원장저장요청 Save(커뮤니티원장Dto value) => new()
    {
        원장Id = value.원장Id, 기대Revision = value.Revision, 커뮤니티Id = value.커뮤니티Id,
        원장템플릿Key = value.원장템플릿Key, 제목 = value.제목, 상태 = value.상태,
        블록목록 = value.블록목록, 참여자목록 = value.참여자목록, 포함원장목록 = value.포함원장목록,
        외부참조 = value.외부참조, 확장속성 = value.확장속성
    };

    private static 커뮤니티포함원장참조Dto Reference(string id, bool required = true, int order = 0) => new()
    { 원장Id = id, 원장템플릿Key = CommunityLedgerTemplateKeys.CargoTransport, 역할 = 주문원장포함역할.운송, 필수여부 = required, 표시순서 = order };

    private static bool IsFenceWrite(CommandStartedEvent command, string childId)
        => command.CommandName == "update" && Contains(command.Command, "원장Id", childId)
           && Contains(command.Command, "$inc", "관계편집Revision");

    private static bool IsChildProgressWrite(CommandStartedEvent command, string childId)
        => command.CommandName is "update" or "findAndModify" && Contains(command.Command, "원장Id", childId)
           && Contains(command.Command, "상태", 커뮤니티원장상태.진행중);

    private static bool Contains(BsonValue value, string name, string text)
    {
        if (value is BsonDocument document)
            return document.Any(element => element.Name == name
                && (element.Value.IsString && element.Value.AsString == text
                    || element.Value is BsonDocument nested && nested.Contains(text)))
                || document.Any(element => Contains(element.Value, name, text));
        return value is BsonArray array && array.Any(item => Contains(item, name, text));
    }

    private sealed class CommandPause(Func<CommandStartedEvent, bool> matches) : IDisposable
    {
        private readonly ManualResetEventSlim release = new(false);
        private int armed;
        private int triggered;
        public TaskCompletionSource Reached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool TimedOut { get; private set; }
        public void Arm() => Volatile.Write(ref armed, 1);
        public void Observe(CommandStartedEvent command)
        {
            if (Volatile.Read(ref armed) == 0 || !matches(command) || Interlocked.CompareExchange(ref triggered, 1, 0) != 0) return;
            Reached.TrySetResult();
            TimedOut = !release.Wait(TimeSpan.FromSeconds(25));
        }
        public void Release() => release.Set();
        public void Dispose() { release.Set(); release.Dispose(); }
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly string connection;
        private readonly string databaseName;
        private readonly List<MongoClient> monitoredClients = [];
        private Fixture(string connection, string databaseName)
        {
            this.connection = connection; this.databaseName = databaseName;
            Client = new MongoClient(connection); Database = Client.GetDatabase(databaseName);
            Options = Microsoft.Extensions.Options.Options.Create(new MongoDbOptions { Database = databaseName });
            Store = new(Client, Options); Raw = Database.GetCollection<BsonDocument>("community_ledgers");
        }
        public MongoClient Client { get; }
        public IMongoDatabase Database { get; }
        public IOptions<MongoDbOptions> Options { get; }
        public Mongo커뮤니티원장저장소 Store { get; }
        public IMongoCollection<BsonDocument> Raw { get; }
        public static async Task<Fixture> CreateAsync(bool standalone = false)
        {
            var variable = standalone ? OrderLedgerStandaloneMongoFactAttribute.Variable : OrderLedgerMongoFactAttribute.Variable;
            var connection = Environment.GetEnvironmentVariable(variable) ?? throw new InvalidOperationException("Isolated order-ledger Mongo connection is required.");
            if (!Uri.TryCreate(connection, UriKind.Absolute, out var uri) || uri.Scheme != "mongodb"
                || uri.Host is not ("127.0.0.1" or "localhost" or "::1"))
                throw new InvalidOperationException("Order-ledger integration must use a loopback Mongo instance.");
            var name = "r41_order_ledgers_" + Guid.NewGuid().ToString("N");
            var fixture = new Fixture(connection, name);
            var hello = await fixture.Database.RunCommandAsync<BsonDocument>(new BsonDocument("hello", 1));
            Assert.Equal(!standalone, hello.Contains("setName"));
            return fixture;
        }
        public Mongo커뮤니티원장저장소 CreateStore(Action<CommandStartedEvent> observer)
        {
            var settings = MongoClientSettings.FromConnectionString(connection);
            settings.ClusterConfigurator = cluster => cluster.Subscribe(observer);
            var client = new MongoClient(settings);
            monitoredClients.Add(client);
            return new(client, Options);
        }
        public async Task<커뮤니티원장Dto> SeedAsync(bool required = true)
        {
            await Store.원장저장Async(new() { 원장Id = "child-1", 원장템플릿Key = CommunityLedgerTemplateKeys.CargoTransport, 제목 = "독립 운송" }, "owner");
            return await Store.원장저장Async(new() { 원장Id = "root-1", 원장템플릿Key = CommunityLedgerTemplateKeys.Order,
                제목 = "원주문", 포함원장목록 = [Reference("child-1", required)] }, "owner");
        }
        public Task<커뮤니티원장문서> ReadDocumentAsync(string id)
            => Database.GetCollection<커뮤니티원장문서>("community_ledgers").Find(x => x.원장Id == id).SingleAsync();
        public async ValueTask DisposeAsync()
        {
            try
            {
                if (!databaseName.StartsWith("r41_order_ledgers_", StringComparison.Ordinal) || databaseName.Length != 50)
                    throw new InvalidOperationException("Isolated order-ledger cleanup identity mismatch.");
                await Client.DropDatabaseAsync(databaseName);
            }
            finally
            {
                foreach (var client in monitoredClients) client.Dispose();
                Client.Dispose();
            }
        }
    }
}

public sealed class OrderLedgerMongoFactAttribute : FactAttribute
{
    public const string Variable = "SSALDDEL_ORDER_LEDGER_TEST_MONGO";
    public OrderLedgerMongoFactAttribute()
    { if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(Variable))) Skip = "격리된 replica set Mongo 시험 연결이 지정되지 않았습니다."; }
}

public sealed class OrderLedgerMongoTheoryAttribute : TheoryAttribute
{
    public OrderLedgerMongoTheoryAttribute()
    { if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(OrderLedgerMongoFactAttribute.Variable))) Skip = "격리된 replica set Mongo 시험 연결이 지정되지 않았습니다."; }
}

public sealed class OrderLedgerStandaloneMongoFactAttribute : FactAttribute
{
    public const string Variable = "SSALDDEL_ORDER_LEDGER_STANDALONE_TEST_MONGO";
    public OrderLedgerStandaloneMongoFactAttribute()
    { if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(Variable))) Skip = "격리된 standalone Mongo 시험 연결이 지정되지 않았습니다."; }
}
