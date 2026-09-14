using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Ssalddel.Contracts.Common.DeliveryZones;
using Ssalddel.Services.WorldProjection.AdministrativeDongDiorama;
using Ssalddel.WorkflowRules.Contracts;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.DeliveryZones;

namespace Ssalddel.Tests.Services.DeliveryZones;

public sealed class 배달운영권역관리ServiceTests
{
    private const string Scope = 배달운영권역SourceScopes.NortheastSeoulRiderR1;
    private const string AreaA = "region:kr:hjd:1126056500";
    private const string AreaB = "region:kr:hjd:1126057000";
    private const string AreaC = "region:kr:hjd:1126057500";
    private const string Actor = "user:admin:test";

    [Fact]
    public async Task Draft_생성_재요청과_행정동_교체가_멱등이며_제외_이력을_남긴다()
    {
        await using var db = CreateContext();
        var service = CreateService(db);
        var create = new 배달운영권역Draft생성Request
        {
            DeliveryTerritoryStableId = "delivery-territory:kr:seoul:myeonmok-alpha",
            DisplayName = "면목 알파 배달권",
            SourceScopeStableId = Scope,
            AdministrativeAreaStableIds = [AreaA, AreaB],
            ClientRequestId = "create-alpha-1"
        };

        var created = await service.Draft생성Async(create, Actor);
        var replayed = await service.Draft생성Async(create, Actor);

        Assert.Equal(1, created.Revision);
        Assert.Equal(1, replayed.Revision);
        Assert.Equal(2, created.AdministrativeDongs.Count);
        Assert.Equal(1, await db.배달운영권역CommandReceipts.CountAsync());
        Assert.Equal(1, await db.배달운영권역변경Outbox.CountAsync());

        var replaced = await service.행정동교체Async(
            created.DeliveryTerritoryStableId,
            new 배달운영권역행정동교체Request
            {
                ExpectedRevision = 1,
                SourceScopeStableId = Scope,
                AdministrativeAreaStableIds = [AreaB, AreaC],
                ClientRequestId = "replace-alpha-2"
            },
            Actor);

        Assert.Equal(2, replaced.Revision);
        Assert.Equal(3, replaced.AdministrativeDongs.Count);
        Assert.Equal(
            배달운영권역행정동상태Codes.Excluded,
            replaced.AdministrativeDongs.Single(x => x.AdministrativeAreaStableId == AreaA).MembershipStateCode);
        Assert.Equal(
            2,
            replaced.AdministrativeDongs.Count(
                x => x.MembershipStateCode == 배달운영권역행정동상태Codes.Included));
        Assert.Equal(2, await db.배달운영권역CommandReceipts.CountAsync());
        Assert.Equal(2, await db.배달운영권역변경Outbox.CountAsync());

        var lateCreateReplay = await service.Draft생성Async(create, Actor);
        Assert.Equal(1, lateCreateReplay.Revision);
        Assert.Equal(2, lateCreateReplay.AdministrativeDongs.Count);
        Assert.DoesNotContain(
            lateCreateReplay.AdministrativeDongs,
            x => x.AdministrativeAreaStableId == AreaC);

        var replaceReplay = await service.행정동교체Async(
            created.DeliveryTerritoryStableId,
            new 배달운영권역행정동교체Request
            {
                ExpectedRevision = 1,
                SourceScopeStableId = Scope,
                AdministrativeAreaStableIds = [AreaC, AreaB],
                ClientRequestId = "replace-alpha-2"
            },
            Actor);
        Assert.Equal(2, replaceReplay.Revision);
        Assert.Equal(2, await db.배달운영권역변경Outbox.CountAsync());
        Assert.All(
            await db.배달운영권역CommandReceipts.AsNoTracking().ToArrayAsync(),
            receipt => Assert.Equal(Actor, receipt.ActorUserStableId));
        var events = await db.배달운영권역변경Outbox
            .AsNoTracking()
            .OrderBy(item => item.AggregateRevision)
            .ToArrayAsync();
        Assert.All(events, item => Assert.Equal(Actor, item.ActorUserStableId));
        using var eventPayload = JsonDocument.Parse(events[1].PayloadJson);
        Assert.Equal(Actor, eventPayload.RootElement.GetProperty("actorUserStableId").GetString());

        var reIncluded = await service.행정동교체Async(
            created.DeliveryTerritoryStableId,
            new 배달운영권역행정동교체Request
            {
                ExpectedRevision = 2,
                SourceScopeStableId = Scope,
                AdministrativeAreaStableIds = [AreaA, AreaC],
                ClientRequestId = "replace-alpha-3"
            },
            Actor);
        Assert.Equal(3, reIncluded.Revision);
        Assert.Equal(
            배달운영권역행정동상태Codes.Included,
            reIncluded.AdministrativeDongs.Single(x => x.AdministrativeAreaStableId == AreaA).MembershipStateCode);
        var revisionSets = (await db.배달운영권역변경Outbox
                .AsNoTracking()
                .OrderBy(item => item.AggregateRevision)
                .ToArrayAsync())
            .Select(item =>
            {
                using var payload = JsonDocument.Parse(item.PayloadJson);
                return payload.RootElement.GetProperty("administrativeAreaStableIds")
                    .EnumerateArray()
                    .Select(value => value.GetString()!)
                    .ToArray();
            })
            .ToArray();
        Assert.Equal([AreaA, AreaB], revisionSets[0]);
        Assert.Equal([AreaB, AreaC], revisionSets[1]);
        Assert.Equal([AreaA, AreaC], revisionSets[2]);
    }

    [Fact]
    public async Task 낮은_revision과_다른_권역의_현행_행정동_중복을_거절한다()
    {
        await using var db = CreateContext();
        var service = CreateService(db);
        var first = await service.Draft생성Async(new 배달운영권역Draft생성Request
        {
            DeliveryTerritoryStableId = "delivery-territory:kr:seoul:first",
            DisplayName = "첫 권역",
            AdministrativeAreaStableIds = [AreaA],
            ClientRequestId = "create-first"
        }, Actor);

        var conflict = await Assert.ThrowsAsync<배달운영권역ConflictException>(() =>
            service.Draft생성Async(new 배달운영권역Draft생성Request
            {
                DeliveryTerritoryStableId = "delivery-territory:kr:seoul:second",
                DisplayName = "둘째 권역",
                AdministrativeAreaStableIds = [AreaA],
                ClientRequestId = "create-second"
            }, Actor));
        Assert.StartsWith("AdministrativeDongAlreadyAssigned:", conflict.Message, StringComparison.Ordinal);

        await service.행정동교체Async(
            first.DeliveryTerritoryStableId,
            new 배달운영권역행정동교체Request
            {
                ExpectedRevision = 1,
                AdministrativeAreaStableIds = [AreaA, AreaB],
                ClientRequestId = "first-revision-2"
            },
            Actor);
        var stale = await Assert.ThrowsAsync<배달운영권역ConcurrencyException>(() =>
            service.행정동교체Async(
                first.DeliveryTerritoryStableId,
                new 배달운영권역행정동교체Request
                {
                    ExpectedRevision = 1,
                    AdministrativeAreaStableIds = [AreaA],
                    ClientRequestId = "stale-request"
                },
                Actor));
        Assert.Equal("DeliveryTerritoryRevisionConflict", stale.Message);
    }

    [Fact]
    public async Task 같은_ClientRequestId의_다른_payload를_거절한다()
    {
        await using var db = CreateContext();
        var service = CreateService(db);
        await service.Draft생성Async(new 배달운영권역Draft생성Request
        {
            DeliveryTerritoryStableId = "delivery-territory:kr:seoul:idempotent",
            DisplayName = "멱등 권역",
            AdministrativeAreaStableIds = [AreaA],
            ClientRequestId = "same-request-id"
        }, Actor);

        var exception = await Assert.ThrowsAsync<배달운영권역ConflictException>(() =>
            service.Draft생성Async(new 배달운영권역Draft생성Request
            {
                DeliveryTerritoryStableId = "delivery-territory:kr:seoul:idempotent",
                DisplayName = "멱등 권역",
                AdministrativeAreaStableIds = [AreaB],
                ClientRequestId = "same-request-id"
            }, Actor));

        Assert.Equal("ClientRequestPayloadConflict", exception.Message);
    }

    [Fact]
    public async Task 행정동_Module은_공식_관할과_게시된_Mongo_manifest를_분리해_보여준다()
    {
        await using var db = CreateContext();
        var service = CreateService(db);
        await service.Draft생성Async(new 배달운영권역Draft생성Request
        {
            DeliveryTerritoryStableId = "delivery-territory:kr:seoul:module",
            DisplayName = "모듈 권역",
            AdministrativeAreaStableIds = [AreaA],
            ClientRequestId = "create-module"
        }, Actor);

        var modules = await service.행정동Module목록Async(Scope);

        Assert.Equal(3, modules.Count);
        var published = modules.Single(x => x.AdministrativeAreaStableId == AreaC);
        Assert.Equal(AdministrativeDongDioramaReadinessCodes.PrivateReviewOnly, published.DioramaReadinessCode);
        Assert.Equal("projection-hash", published.DioramaProjectionHashSha256);
        Assert.False(published.DistributionApproved);
        var unpublished = modules.Single(x => x.AdministrativeAreaStableId == AreaB);
        Assert.Equal(행정동운영Module상태Codes.WaitingForSpatialProjection, unpublished.DioramaReadinessCode);
        Assert.Null(unpublished.DioramaProjectionHashSha256);
        var assigned = modules.Single(x => x.AdministrativeAreaStableId == AreaA);
        Assert.Equal("delivery-territory:kr:seoul:module", assigned.AssignedDeliveryTerritoryStableId);
    }

    private static SsalddelContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<SsalddelContext>()
            .UseInMemoryDatabase($"delivery-territory-{Guid.NewGuid():N}")
            .Options;
        return new SsalddelContext(options, new DummyEncryption());
    }

    private static 배달운영권역관리Service CreateService(SsalddelContext db)
        => new(db, new FakeSource(), new FakeDioramaStore());

    private sealed class FakeSource : I행정동배달운영권역Source
    {
        public Task<IReadOnlyList<배달운영권역행정동SourceItem>> 조회Async(
            string sourceScopeStableId,
            CancellationToken cancellationToken = default)
        {
            Assert.Equal(Scope, sourceScopeStableId);
            var items = new[] { AreaA, AreaB, AreaC }
                .Select((area, index) => new 배달운영권역행정동SourceItem(
                    area,
                    $"행정동 {index + 1}",
                    [new 배달운영권역법정동SourceItem("region:kr:bjd:1126010100", "서울특별시 중랑구 면목동")],
                    "mois-resident-registration-codes",
                    "korea-administrative-legal-jurisdictions",
                    "jscode20260301.zip",
                    "mois-hjd-bjd-test",
                    new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero)))
                .ToArray();
            return Task.FromResult<IReadOnlyList<배달운영권역행정동SourceItem>>(items);
        }
    }

    private sealed class FakeDioramaStore : I행정동디오라마ProjectionStore
    {
        public Task PublishAsync(
            행정동디오라마ProjectionBuildResult projection,
            CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<AdministrativeDongDioramaManifest?> FindManifestAsync(
            string administrativeAreaStableId,
            CancellationToken cancellationToken)
            => Task.FromResult<AdministrativeDongDioramaManifest?>(
                administrativeAreaStableId == AreaC
                    ? new AdministrativeDongDioramaManifest
                    {
                        AdministrativeAreaStableId = AreaC,
                        DisplayName = "면목제3.8동",
                        ReadinessCode = AdministrativeDongDioramaReadinessCodes.PrivateReviewOnly,
                        SourceVintage = "2026-06-11",
                        ProjectionHashSha256 = "projection-hash",
                        ObservationPresentationOnly = true,
                        DistributionApproved = false
                    }
                    : null);

        public Task<AdministrativeDongDioramaTile?> FindTileAsync(
            string administrativeAreaStableId,
            string tileStableId,
            CancellationToken cancellationToken)
            => Task.FromResult<AdministrativeDongDioramaTile?>(null);

        public Task<AdministrativeDongDisplayOverlayResponse?> FindDisplayOverlaysAsync(
            string administrativeAreaStableId,
            CancellationToken cancellationToken)
            => Task.FromResult<AdministrativeDongDisplayOverlayResponse?>(null);
    }

    private sealed class DummyEncryption : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
}
