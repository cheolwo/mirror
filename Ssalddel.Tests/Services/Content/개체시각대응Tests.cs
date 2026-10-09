using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Contracts.Admin.Content;
using Ssalddel.Domain.Content;
using Ssalddel.Infrastructure.Persistence.WorldProjection;
using Ssalddel.Services.Content;

namespace Ssalddel.Tests.Services.Content;

public sealed class 개체시각대응Tests
{
    internal static 개체시각대상Dto Target => new("food.product", "product:potato", "CommonFoodIdentity", "Public",
        "r1", "Reference", "Inventory", "Product", "감자");
    internal static 개체시각후보Dto Candidate => new("urban.cargo.cardboard-box", "catalog.r1", new('A', 64),
        new('B', 64), "ApprovedForContext", "docs/Reports/fixture.md", new('C', 64));
    internal static 개체시각대상Query Query => new(Target.Kind, Target.StableId, Target.Purpose);
    private static ClaimsPrincipal User(string id = "admin", bool admin = true) => new(new ClaimsIdentity(
        new[] { new Claim(ClaimTypes.NameIdentifier, id), new Claim(ClaimTypes.Role, admin ? 살뜰.Data.역할명.서버관리자 : "User") }, "Test"));

    [Theory]
    [InlineData("anonymous", "Unauthorized")]
    [InlineData("regular", "Forbidden")]
    [InlineData("other", "PrincipalMismatch")]
    [InlineData("disabled", "FeatureDisabled")]
    public async Task 권한실패는원천이나대응자료를읽지않는다(string mode, string expected)
    {
        await using var f = new Fixture();
        if (mode == "disabled") f.Options.Value.Enabled = false;
        var u = mode switch { "anonymous" => new ClaimsPrincipal(), "regular" => User(admin: false), "other" => User("other"), _ => User() };
        Assert.Equal(expected, (await f.Service.ResolveAsync(u, Query, default)).Diagnostic);
        Assert.Equal(expected, (await f.Service.ExecuteAsync(u, f.Request(), default)).Diagnostic);
        Assert.Equal(0, f.Source.Calls);
        Assert.Empty(await f.Db.Bindings.ToArrayAsync());
    }

    [Fact]
    public async Task 초안저장_검토_승인_독립재조회_이력을원자적으로보존한다()
    {
        await using var f = new Fixture();
        var b = await f.Approve("specific", false);
        await using var other = f.NewContext();
        var persisted = await other.Bindings.SingleAsync();
        Assert.Equal(b.Revision, persisted.Revision);
        Assert.Equal(3, await other.History.CountAsync());
        var selection = await f.Service.ResolveAsync(User(), Query, default);
        Assert.Equal("Selected", selection.Diagnostic);
        Assert.False(selection.IsFallback);
        Assert.False(selection.CanApplyToScene);
        var history = await f.Service.HistoryAsync(User(), "specific", Query, default);
        Assert.Equal(new long[] { 3, 2, 1 }, history.Items.Select(x => x.Revision));
        Assert.Single((await f.Service.ListAsync(User(), Query, default)).Items);
    }

    [Fact]
    public async Task 개별미승인이나오래된판본은같은문맥승인기본으로만대체한다()
    {
        await using var f = new Fixture();
        await f.Approve("default", true);
        await f.Approve("specific", false);
        f.Source.Target = Target with { Revision = "r2" };
        var selection = await f.Service.ResolveAsync(User(), Query, default);
        Assert.True(selection.IsFallback);
        Assert.Equal("default", selection.BindingId);
        Assert.Contains("RecordSpecific:SourceRevisionChanged", selection.Limitations!);
        f.Source.Target = Target with { StateCode = "Growing" };
        Assert.Equal("Unmapped", (await f.Service.ResolveAsync(User(), Query, default)).Diagnostic);
    }

    [Fact]
    public async Task 개정하면승인무효화하고제외하면기본대체한다()
    {
        await using var f = new Fixture();
        await f.Approve("default", true);
        var b = await f.Approve("specific", false);
        var revision = await f.Service.ExecuteAsync(User(), f.Request("specific", b.Revision), default);
        Assert.Equal(개체시각대응Codes.Draft, revision.Binding!.ReviewState);
        Assert.Null(revision.Binding.ReviewerId);
        Assert.True((await f.Service.ResolveAsync(User(), Query, default)).IsFallback);
        var excluded = await f.Service.ExecuteAsync(User(), f.Request("default", 3, true,
            개체시각대응Action.Exclude), default);
        Assert.True(excluded.Success);
        Assert.Equal("Unmapped", (await f.Service.ResolveAsync(User(), Query, default)).Diagnostic);
    }

    [Fact]
    public async Task 원천상태가바뀐과거대응도이력조회와제외가가능하다()
    {
        await using var f = new Fixture();
        await f.Approve("specific", false);
        f.Source.Target = Target with { StateCode = "Other", Revision = "r2" };
        Assert.Equal(3, (await f.Service.HistoryAsync(User(), "specific", Query, default)).Items.Count);
        var result = await f.Service.ExecuteAsync(User(), f.Request("specific", 3, action: 개체시각대응Action.Exclude), default);
        Assert.True(result.Success, result.Diagnostic);
        Assert.Equal("Reference", result.Binding!.Target.StateCode);
        f.Source.Target = Target;
        Assert.Equal("Unmapped", (await f.Service.ResolveAsync(User(), Query, default)).Diagnostic);
    }

    [Fact]
    public async Task 종류기본은다른실제레코드에도적용되고개별우선을침범하지않는다()
    {
        await using var f = new Fixture();
        await f.Approve("default", true);
        f.Source.Target = Target with { StableId = "product:apple", DisplayName = "사과" };
        var result = await f.Service.ResolveAsync(User(), Query with { StableId = "product:apple" }, default);
        Assert.True(result.IsFallback);
        // 이 Fixture는 검토된 같은 종류 기본의 선택 순서만 증명한다. 실제 감자 외형을 사과로 승인한 근거는 아니다.
        Assert.Equal("product:apple", result.Target!.StableId);
    }

    [Fact]
    public void MySql전용스키마는대응구성WI참여와전수목록을구분한다()
    {
        using var db = new 개체시각대응DbContext(new DbContextOptionsBuilder<개체시각대응DbContext>()
            .UseMySql("Server=127.0.0.1;Database=not-used;User=test;Password=not-used", new MySqlServerVersion(new Version(8, 4, 0))).Options);
        var generated = db.Database.GenerateCreateScript(); // 접속 없이 모델로 생성
        // Pomelo 기본 ALTER DATABASE는 제한 DDL의 범위가 아니다.
        var sql = System.Text.RegularExpressions.Regex.Replace(generated,
            @"(?m)^ALTER DATABASE CHARACTER SET utf8mb4;\s*", "");
        Assert.Equal(12, System.Text.RegularExpressions.Regex.Matches(sql, "CREATE TABLE").Count);
        Assert.Contains("world_visual_inventory_classifications", sql);
        Assert.Contains("InventorySnapshotId", sql);
        Assert.Contains("world_visual_asset_versions", sql);
        Assert.Contains("world_entity_visual_bindings", sql);
        Assert.Contains("world_entity_visual_binding_history", sql);
        Assert.Contains("UNIQUE INDEX", sql);
        Assert.DoesNotContain("DROP TABLE", sql);
        Assert.DoesNotContain("ALTER DATABASE", sql);
        Assert.DoesNotContain("__EFMigrationsHistory", sql);
        var root = TestRepositoryRoot.Find();
        Assert.True(File.Exists(Path.Combine(root, "Ssalddel.Tests/Ssalddel.Tests.csproj")));
        var output = Path.Combine(root, "artifacts/local/validation/game-object-auto-assignment-d440/schema-test", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(output, "mapping-schema.mysql.sql"), sql);
    }

    [Fact]
    public async Task 같은요청키는이력재반환_다른내용은충돌하며저장개수가늘지않는다()
    {
        await using var f = new Fixture();
        var r = f.Request();
        Assert.True((await f.Service.ExecuteAsync(User(), r, default)).Success);
        var duplicate = await f.Service.ExecuteAsync(User(), r, default);
        Assert.True(duplicate.Duplicate);
        Assert.Equal("IdempotencyConflict", (await f.Service.ExecuteAsync(User(), r with { Note = "different" }, default)).Diagnostic);
        Assert.Equal(1, await f.Db.History.CountAsync());
    }

    [Fact]
    public async Task 오래된개정과다른ID의같은문맥을거부한다()
    {
        await using var f = new Fixture();
        await f.Service.ExecuteAsync(User(), f.Request(), default);
        Assert.Equal("RevisionConflict", (await f.Service.ExecuteAsync(User(), f.Request(), default)).Diagnostic);
        Assert.Equal("BindingConflict", (await f.Service.ExecuteAsync(User(), f.Request("another"), default)).Diagnostic);
        Assert.Equal(1, await f.Db.History.CountAsync());
    }

    [Fact]
    public async Task 관계형동시수정은상태와감사를함께롤백한다()
    {
        await using var f = new Fixture();
        await f.Service.ExecuteAsync(User(), f.Request(), default);
        await using var stale = f.NewContext();
        var row = await stale.Bindings.SingleAsync();
        await f.Service.ExecuteAsync(User(), f.Request("specific", 1), default);
        row.Revision = 2;
        stale.History.Add(new 개체시각대응이력 { RequestKeyHash = "stale", BindingId = row.BindingId, Revision = 9,
            RequestHash = "test", ReviewerId = "admin", Action = "test", Note = "test", StateJson = "{}" });
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => stale.SaveChangesAsync());
        await using var verify = f.NewContext();
        Assert.Equal(2, (await verify.Bindings.SingleAsync()).Revision);
        Assert.Equal(2, await verify.History.CountAsync());
    }

    [Theory]
    [InlineData("SourceConflict")]
    [InlineData("SourceAccessOrQueryFailed")]
    [InlineData("NotFoundOrNotAuthorized")]
    public async Task 원천실패를기본대체로숨기지않는다(string diagnostic)
    {
        await using var f = new Fixture();
        await f.Approve("default", true);
        f.Source.Diagnostic = diagnostic;
        var result = await f.Service.ResolveAsync(User(), Query, default);
        Assert.Equal(diagnostic, result.Diagnostic);
        Assert.Null(result.VisualKey);
    }

    [Fact]
    public async Task 승인후Catalog변경은저장승인을선택권한으로쓰지못한다()
    {
        await using var f = new Fixture();
        await f.Approve("default", true);
        f.Options.Value.Entries[0] = f.Options.Value.Entries[0] with { Candidate = Candidate with { AssetFingerprint = new('D', 64) } };
        var result = await f.Service.ResolveAsync(User(), Query, default);
        Assert.Equal("Unmapped", result.Diagnostic);
        Assert.Contains("TypeDefault:StaleCandidate", result.Limitations!);
    }

    [Fact]
    public async Task 저장상태와검색키의불일치는미연결이나기본대체로숨기지않는다()
    {
        await using var f = new Fixture();
        await f.Approve("specific", false);
        (await f.Db.Bindings.SingleAsync()).ReviewState = "Corrupt";
        await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.ResolveAsync(User(), Query, default));
    }

    [Fact]
    public async Task 미등록후보는초안검토만가능하며승인불가()
    {
        await using var f = new Fixture();
        f.Options.Value.Entries.Clear();
        await f.Service.ExecuteAsync(User(), f.Request(), default);
        await f.Service.ExecuteAsync(User(), f.Request("specific", 1, action: 개체시각대응Action.SubmitReview), default);
        var result = await f.Service.ExecuteAsync(User(), f.Request("specific", 2, action: 개체시각대응Action.Approve), default);
        Assert.Equal("UnregisteredCandidate", result.Diagnostic);
        Assert.Equal(2, await f.Db.History.CountAsync());
    }

    [Theory]
    [InlineData("C:/private/asset.prefab")]
    [InlineData("docs/../../secret")]
    [InlineData("https://example.com/secret")]
    public async Task 로컬경로나외부업로드주소를근거로저장하지않는다(string path)
    {
        await using var f = new Fixture();
        var result = await f.Service.ExecuteAsync(User(), f.Request() with { Candidate = Candidate with { EvidenceRef = path } }, default);
        Assert.Equal("InvalidCandidate", result.Diagnostic);
        Assert.Empty(await f.Db.History.ToArrayAsync());
    }

    [Theory]
    [InlineData("scope")]
    [InlineData("kind")]
    [InlineData("purpose")]
    [InlineData("state")]
    [InlineData("representation")]
    [InlineData("source")]
    public void 다른문맥은기본형을빌려쓰지않는다(string field)
    {
        var other = field switch {
            "scope" => Target with { AccessScope = "private" }, "kind" => Target with { Kind = "mart.product" },
            "purpose" => Target with { Purpose = "Growing" }, "state" => Target with { StateCode = "Growing" },
            "representation" => Target with { Representation = "Building" }, _ => Target with { SourceKey = "Other" } };
        var b = new 개체시각대응Dto("default", 3, other, true, Candidate, 개체시각대응Codes.Approved, "admin", DateTime.UtcNow);
        Assert.Equal("Unmapped", 개체시각선택Policy.Select(Target, [b], new ValidCatalog()).Diagnostic);
    }

    [Fact]
    public void 중복후보는첫번째를임의선정하지않는다()
    {
        var b = new 개체시각대응Dto("specific", 3, Target, false, Candidate, 개체시각대응Codes.Approved, "admin", DateTime.UtcNow);
        Assert.Equal("BindingConflict", 개체시각선택Policy.Select(Target, [b, b with { BindingId = "other" }], new ValidCatalog()).Diagnostic);
    }
    [Fact]
    public void 개별자산은다른품목이나종류전체기본으로확대할수없다()
    {
        var options = new Monitor();
        options.Value.Entries[0] = options.Value.Entries[0] with { RecordStableId = "product:potato", AllowTypeDefault = false };
        var catalog = new 개체시각자산Catalog(options);
        Assert.Equal("Valid", catalog.Check(Target, Candidate));
        Assert.Equal("TypeDefaultNotApproved", catalog.Check(Target, Candidate, true));
        Assert.Equal("RecordCandidateMismatch", catalog.Check(Target with { StableId = "product:apple" }, Candidate));
    }
    [Theory]
    [InlineData("duplicate", "CatalogConflict")]
    [InlineData("missing", "UnregisteredCandidate")]
    [InlineData("unreviewed", "FitnessNotApproved")]
    public void 자산판정공백을임의선정으로대체하지않는다(string mode, string expected)
    {
        var options = new Monitor();
        var candidate = Candidate;
        if (mode == "duplicate") options.Value.Entries.Add(options.Value.Entries[0]);
        if (mode == "missing") options.Value.Entries.Clear();
        if (mode == "unreviewed") { candidate = Candidate with { Fitness = "Unreviewed" }; options.Value.Entries[0] = options.Value.Entries[0] with { Candidate = candidate }; }
        Assert.Equal(expected, new 개체시각자산Catalog(options).Check(Target, candidate));
    }
    private sealed class ValidCatalog : I개체시각자산Catalog { public string Check(개체시각대상Dto t, 개체시각후보Dto? c, bool typeDefault = false) => "Valid"; }
    internal sealed class Monitor : IOptionsMonitor<개체시각자산Options>
    {
        public 개체시각자산Options Value = new() { Enabled = true, Entries = [new(Target.Kind, Target.StateCode, Target.Purpose, Target.Representation, Candidate, AllowTypeDefault: true)] };
        public 개체시각자산Options CurrentValue => Value;
        public 개체시각자산Options Get(string? name) => Value;
        public IDisposable? OnChange(Action<개체시각자산Options, string?> listener) => null;
    }
    private sealed class Current : ICurrentUserAccessor { public string? UserId => "admin"; public string? Role => 살뜰.Data.역할명.서버관리자; }
    private sealed class Source : I개체시각대상Reader
    {
        public 개체시각대상Dto Target = 개체시각대응Tests.Target;
        public string Diagnostic = "Found";
        public int Calls;
        public Task<개체시각대상ReadResult> ReadAsync(개체시각대상Query q, CancellationToken ct)
        { Calls++; return Task.FromResult(new 개체시각대상ReadResult(Diagnostic, Diagnostic == "Found" ? Target : null)); }
    }
    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection = new("Data Source=:memory:");
        private readonly ServiceProvider provider;
        public 개체시각대응DbContext Db { get; }
        public Monitor Options { get; } = new();
        public Source Source { get; } = new();
        public 개체시각대응UseCase Service { get; }
        public Fixture()
        {
            connection.Open();
            Db = NewContext();
            Db.Database.EnsureCreated();
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddAuthorizationCore(o => o.AddPolicy(개체시각대응Codes.Policy, p => p.RequireRole(살뜰.Data.역할명.서버관리자)));
            provider = services.BuildServiceProvider();
            Service = new(Db, Source, new 개체시각자산Catalog(Options), provider.GetRequiredService<IAuthorizationService>(), new Current(), Options, TimeProvider.System);
        }
        public 개체시각대응DbContext NewContext() => new(new DbContextOptionsBuilder<개체시각대응DbContext>().UseSqlite(connection).Options);
        public 개체시각대응Request Request(string id = "specific", long revision = 0, bool typeDefault = false,
            개체시각대응Action action = 개체시각대응Action.SaveDraft) => new(id, revision, Guid.NewGuid().ToString("N"), action,
                "격리 시험 근거", Query, typeDefault, action == 개체시각대응Action.SaveDraft ? Candidate : null);
        public async Task<개체시각대응Dto> Approve(string id, bool type)
        {
            개체시각대응Result? r = null;
            foreach (var action in new[] { 개체시각대응Action.SaveDraft, 개체시각대응Action.SubmitReview, 개체시각대응Action.Approve })
            {
                r = await Service.ExecuteAsync(User(), Request(id, r?.Binding?.Revision ?? 0, type, action), default);
                Assert.True(r.Success, r.Diagnostic);
            }
            return r!.Binding!;
        }
        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await connection.DisposeAsync(); await provider.DisposeAsync(); }
    }
}
