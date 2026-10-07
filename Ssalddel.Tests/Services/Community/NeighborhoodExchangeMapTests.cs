using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.Logging.Abstractions;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Domain.Community;
using Ssalddel.Migrations;
using Ssalddel.Services.Community;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;

namespace Ssalddel.Tests.Services.Community;

public sealed class NeighborhoodExchangeMapTests
{
    private const string Region1 = "region:kr:hjd:1126057500";
    private const string Region2 = "region:kr:hjd:1126056500";
    private const string UnsupportedRegion = "region:kr:hjd:1199999999";
    private static readonly Official생활교류공개지역Source Regions = new();

    [Fact]
    public async Task 공개_동네는_공식_출처와_실제_파일_판본이_있는_30개의_대표점이다()
    {
        var regions = await Regions.목록Async();
        Assert.Equal(30, regions.Count);
        Assert.Equal(30, regions.Select(region => region.RegionKey).Distinct().Count());
        var myeonmok = Assert.Single(regions.Where(region => region.RegionKey == Region1));
        Assert.Equal("서울특별시 중랑구 면목제3.8동", myeonmok.DisplayName);
        Assert.Equal("region:kr:hjd:1126000000", myeonmok.ParentRegionKey);
        Assert.All(regions, region =>
        {
            Assert.StartsWith("서울특별시 ", region.DisplayName, StringComparison.Ordinal);
            Assert.Equal("neighborhood", region.PrecisionCode);
            Assert.Equal("KOGL-Type1", region.AnchorLicenseCode);
            Assert.Equal(Official생활교류공개지역Source.SourceSha256, region.AnchorSourceSha256);
            Assert.Contains("2023-10-31", region.AnchorSourceVintage, StringComparison.Ordinal);
            Assert.Contains("실제 물품", region.LocationBoundary, StringComparison.Ordinal);
            Assert.InRange(region.Latitude, 37.4, 37.8);
            Assert.InRange(region.Longitude, 126.7, 127.3);
        });
    }

    [Fact]
    public async Task 기존_글의_동네는_nullable이고_마이그레이션은_위치를_추론해_채우지_않는다()
    {
        var migration = new AddCommunityPublicNeighborhoodRegion();
        var added = Assert.Single(migration.UpOperations.OfType<AddColumnOperation>());
        Assert.Equal("platform_community_posts", added.Table);
        Assert.Equal("PublicNeighborhoodRegionKey", added.Name);
        Assert.True(added.IsNullable);
        Assert.Null(added.DefaultValue);
        Assert.DoesNotContain(migration.UpOperations, operation => operation is SqlOperation);
        await using var database = await TestDatabase.CreateAsync();
        var property = database.Context.Model.FindEntityType(typeof(PlatformCommunityPost))!
            .FindProperty(nameof(PlatformCommunityPost.PublicNeighborhoodRegionKey))!;
        Assert.True(property.IsNullable);
        Assert.Equal(80, property.GetMaxLength());
        var result = await Creation(database.Context).CreateAsync(Request(null), null, CancellationToken.None);
        Assert.True(result.IsSuccess, string.Join(";", result.Errors.Select(error => error.Message)));
        database.Context.ChangeTracker.Clear();
        var stored = await database.Context.PlatformCommunityPosts.SingleAsync();
        Assert.Null(stored.PublicNeighborhoodRegionKey);
    }

    [Fact]
    public async Task 사용자_선택_동네는_발행_재조회_수정_지역필터까지_같은_원장에_반영된다()
    {
        await using var database = await TestDatabase.CreateAsync();
        var published = await Creation(database.Context).CreateAsync(Request(Region1), null, CancellationToken.None);
        Assert.True(published.IsSuccess, string.Join(";", published.Errors.Select(error => error.Message)));
        Assert.Equal(Region1, published.Value.PublicNeighborhoodRegionKey);
        database.Context.ChangeTracker.Clear();
        var stored = await database.Context.PlatformCommunityPosts.SingleAsync();
        Assert.Equal(Region1, stored.PublicNeighborhoodRegionKey);
        var query = Map(database.Context);
        var before = await query.글목록Async(new() { PublicNeighborhoodRegionKey = Region1 }, 1, 20, CancellationToken.None);
        Assert.Equal(published.Value.Id, Assert.Single(before.Value.Items).Id);

        var update = Update(Region2);
        var modified = await Publishing(database.Context).수정Async(published.Value.Id, update, CancellationToken.None);
        Assert.True(modified.IsSuccess, string.Join(";", modified.Errors.Select(error => error.Message)));
        database.Context.ChangeTracker.Clear();
        Assert.Equal(Region2, (await database.Context.PlatformCommunityPosts.SingleAsync()).PublicNeighborhoodRegionKey);
        var old = await query.지도Async(new() { PublicNeighborhoodRegionKey = Region1 }, CancellationToken.None);
        Assert.Empty(old.Value.Items);
        var current = await query.지도Async(new() { PublicNeighborhoodRegionKey = Region2 }, CancellationToken.None);
        Assert.Equal(1, Assert.Single(current.Value.Items).OfferCount);

        modified = await Publishing(database.Context).수정Async(published.Value.Id, Update(null), CancellationToken.None);
        Assert.True(modified.IsSuccess);
        Assert.Null(modified.Value.PublicNeighborhoodRegionKey);
        var unlocated = await query.지도Async(new(), CancellationToken.None);
        Assert.Equal(1, unlocated.Value.UnlocatedPostCount);
        Assert.Equal(published.Value.Id, Assert.Single((await query.글목록Async(new(), 1, 20, CancellationToken.None)).Value.Items).Id);
    }

    [Theory]
    [InlineData(UnsupportedRegion, NeighborhoodExchange.WorkflowTag, NeighborhoodExchange.Offer, false)]
    [InlineData("37.5,127.0", NeighborhoodExchange.WorkflowTag, NeighborhoodExchange.Offer, false)]
    [InlineData(Region1, "국내 화물 운송", NeighborhoodExchange.Offer, false)]
    [InlineData(Region1, NeighborhoodExchange.WorkflowTag, "판매자", false)]
    [InlineData(Region1, NeighborhoodExchange.WorkflowTag, NeighborhoodExchange.Offer, true)]
    public async Task 미검증_동네나_잘못된_글은_동네를_공개하는_원장으로_저장하지_않는다(
        string key, string workflow, string intent, bool report)
    {
        await using var database = await TestDatabase.CreateAsync();
        var request = Request(key);
        request.WorkflowTag = workflow;
        request.RoleTag = intent;
        request.IsReportBoardPost = report;
        var result = await Creation(database.Context).CreateAsync(request, null, CancellationToken.None);
        Assert.True(result.IsFailed);
        Assert.Empty(await database.Context.PlatformCommunityPosts.IgnoreQueryFilters().ToListAsync());
    }

    [Fact]
    public async Task 검증_지역_서비스가_없으면_새_동네_공개를_허용하지_않고_기존_미선택_글은_허용한다()
    {
        await using var database = await TestDatabase.CreateAsync();
        var service = Creation(database.Context, useRegions: false);
        Assert.True((await service.CreateAsync(Request(Region1), null, CancellationToken.None)).IsFailed);
        Assert.True((await service.CreateAsync(Request(null), null, CancellationToken.None)).IsSuccess);
        Assert.Single(await database.Context.PlatformCommunityPosts.ToListAsync());
    }

    [Fact]
    public async Task 지도는_공개_교류만_집계하고_미선택은_목록에_보존하며_미지원_동네는_핀을_만들지_않는다()
    {
        await using var database = await TestDatabase.CreateAsync();
        var offer = Stored(Region1);
        var need = Stored(Region1, NeighborhoodExchange.Need);
        var second = Stored(Region2);
        var unlocated = Stored(null);
        var unsupported = Stored(UnsupportedRegion);
        var deleted = Stored(Region1); deleted.IsDeleted = true;
        var report = Stored(Region1); report.IsReportBoardPost = true;
        var scheduled = Stored(Region1); scheduled.PublicationStatusCode = PlatformCommunityPostPublicationStatusCodes.Scheduled;
        var otherWorkflow = Stored(Region1); otherWorkflow.WorkflowTag = "다른 업무";
        var otherCategory = Stored(Region1); otherCategory.Category = "신고·분쟁";
        var otherIntent = Stored(Region1); otherIntent.RoleTag = "배송해요";
        database.Context.PlatformCommunityPosts.AddRange(offer, need, second, unlocated, unsupported, deleted,
            report, scheduled, otherWorkflow, otherCategory, otherIntent);
        await database.Context.SaveChangesAsync();
        database.Context.ChangeTracker.Clear();
        var query = Map(database.Context);
        var result = await query.지도Async(new(), CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Items.Count);
        var marker = Assert.Single(result.Value.Items.Where(item => item.Region.RegionKey == Region1));
        Assert.Equal(1, marker.OfferCount);
        Assert.Equal(1, marker.NeedCount);
        Assert.Equal(2, marker.PostCount);
        Assert.Equal(1, result.Value.UnlocatedPostCount);
        Assert.Equal(1, result.Value.UnsupportedRegionPostCount);
        var official = Assert.Single((await Regions.목록Async()).Where(region => region.RegionKey == Region1));
        Assert.Equal(official.Latitude, marker.Region.Latitude);
        Assert.Equal(official.Longitude, marker.Region.Longitude);

        var filtered = await query.글목록Async(new() { PublicNeighborhoodRegionKey = Region1, Intent = NeighborhoodExchange.Need },
            1, 20, CancellationToken.None);
        Assert.Equal(need.Id, Assert.Single(filtered.Value.Items).Id);
        var all = await query.글목록Async(new(), 1, 20, CancellationToken.None);
        Assert.Equal(5, all.Value.TotalCount);
        Assert.Contains(all.Value.Items, item => item.Id == unlocated.Id && item.PublicNeighborhoodRegionKey is null);
        var page2 = await query.글목록Async(new(), 2, 2, CancellationToken.None);
        Assert.Equal(2, page2.Value.Items.Count);
        Assert.DoesNotContain(page2.Value.Items, item => item.Id == scheduled.Id || item.Id == report.Id || item.Id == deleted.Id);
    }

    [Fact]
    public async Task 미검증_지역_조회와_다른_사용자의_동네_수정은_거절한다()
    {
        await using var database = await TestDatabase.CreateAsync();
        var result = await Creation(database.Context, user: new User("owner")).CreateAsync(Request(Region1), null, CancellationToken.None);
        Assert.True(result.IsSuccess, string.Join(";", result.Errors.Select(error => error.Message)));
        var changed = await Publishing(database.Context, new User("intruder")).수정Async(result.Value.Id, Update(Region2), CancellationToken.None);
        Assert.True(changed.IsFailed);
        database.Context.ChangeTracker.Clear();
        Assert.Equal(Region1, (await database.Context.PlatformCommunityPosts.SingleAsync()).PublicNeighborhoodRegionKey);
        var query = Map(database.Context);
        Assert.True((await query.지도Async(new() { PublicNeighborhoodRegionKey = UnsupportedRegion }, CancellationToken.None)).IsFailed);
        Assert.True((await query.글목록Async(new() { Intent = "배송해요" }, 1, 20, CancellationToken.None)).IsFailed);
    }

    private static PlatformCommunityPostCreateRequest Request(string? key) => new()
    {
        AppKey = "platform", Category = PlatformCommunityPostCategories.General,
        WorkflowTag = NeighborhoodExchange.WorkflowTag, RoleTag = NeighborhoodExchange.Offer,
        Title = "커피를 나눠요", Body = "궁금한 점은 공개 댓글로 먼저 이야기해요.", Nickname = "작성자", Password = "post-password",
        PublicNeighborhoodRegionKey = key
    };

    private static PlatformCommunityPostUpdateRequest Update(string? key) => new()
    {
        Category = PlatformCommunityPostCategories.General, WorkflowTag = NeighborhoodExchange.WorkflowTag,
        RoleTag = NeighborhoodExchange.Offer, Title = "커피를 나눠요", Body = "동네 선택을 바꿉니다.",
        Nickname = "작성자", Password = "post-password", PublicNeighborhoodRegionKey = key
    };

    private static PlatformCommunityPost Stored(string? key, string intent = NeighborhoodExchange.Offer) => new()
    {
        AppKey = "platform", Category = PlatformCommunityPostCategories.General,
        WorkflowTag = NeighborhoodExchange.WorkflowTag, RoleTag = intent,
        Title = "지역 집계 글", Body = "공개 교류 본문", Nickname = "작성자", PasswordHash = "fixture-only",
        PublicNeighborhoodRegionKey = key, PublicationStatusCode = PlatformCommunityPostPublicationStatusCodes.Published,
        CreatedAtUtc = new DateTime(2026, 10, 5, 0, 0, 0, DateTimeKind.Utc)
    };

    private static 커뮤니티게시글생성Service Creation(SsalddelContext db, bool useRegions = true, User? user = null)
        => new(db, new 커뮤니티게시글음성작업예약Service(), new CommunityKeywordNotificationQueue(), null!,
            null!, new AllowWrite(), user ?? new User(null), new Publisher(), NullLogger<커뮤니티게시글생성Service>.Instance,
            useRegions ? Regions : null);
    private static 커뮤니티게시글발행UseCase Publishing(SsalddelContext db, User? user = null)
        => new(null!, db, null!, new EmptyLedger(), new AllowWrite(), user ?? new User(null), Regions);
    private static 생활교류지도조회UseCase Map(SsalddelContext db) => new(db, Regions, new User(null));
    private sealed record User(string? UserId) : ICurrentUserAccessor { public string? Role => null; }
    private sealed class AllowWrite : ICommunityBoardWritePolicy
    {
        public Task<bool> CanWriteAsync(string? appKey, string? category, string? userId, CancellationToken cancellationToken = default)
            => Task.FromResult(true);
    }
    private sealed class EmptyLedger : I게시글원장표시ContextService
    {
        public Task<PlatformCommunityPostLedgerContextResponse?> 조회Async(string? 원장Id, string? 사용자UserId, CancellationToken cancellationToken)
            => Task.FromResult<PlatformCommunityPostLedgerContextResponse?>(null);
        public Task<PlatformCommunityPostLedgerContextResponse?> 비식별성립사례조회Async(string? 원장Id, CancellationToken cancellationToken)
            => Task.FromResult<PlatformCommunityPostLedgerContextResponse?>(null);
    }
    private sealed class Publisher : IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification => Task.CompletedTask;
    }
    private sealed class TestDatabase(SqliteConnection connection, SsalddelContext context) : IAsyncDisposable
    {
        public SsalddelContext Context { get; } = context;
        public static async Task<TestDatabase> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var context = new SsalddelContext(new DbContextOptionsBuilder<SsalddelContext>().UseSqlite(connection).Options, new Encryption());
            await context.Database.EnsureCreatedAsync();
            return new(connection, context);
        }
        public async ValueTask DisposeAsync() { await Context.DisposeAsync(); await connection.DisposeAsync(); }
    }
    private sealed class Encryption : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
}
