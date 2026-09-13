using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Ssalddel.Contracts.Common.Versioning;
using Ssalddel.Services.Development.ObservableOperations;
using Ssalddel.WorkflowRules.Contracts;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;

namespace Ssalddel.Tests.Services.Development.ObservableOperations;

public sealed class 관찰운영검증TimelineTests
{
    [Fact]
    public void 네_OS의_정상과_회복_여덟_사례를_증가_revision으로_정의한다()
    {
        var first = 관찰운영검증Runner.Definitions("verification-run-a");
        var second = 관찰운영검증Runner.Definitions("verification-run-b");

        Assert.Equal(8, first.Cases.Count);
        Assert.Equal(77, first.Steps.Count);
        Assert.Equal(2, first.Cases.Count(item => item.OperatingSystemId == OperationalWorldOperatingSystemIds.FoodDelivery));
        Assert.Equal(2, first.Cases.Count(item => item.OperatingSystemId == OperationalWorldOperatingSystemIds.DomesticCargoTransport));
        Assert.Equal(2, first.Cases.Count(item => item.OperatingSystemId == OperationalWorldOperatingSystemIds.WarehouseCommerceFulfillment));
        Assert.Equal(2, first.Cases.Count(item => item.OperatingSystemId == OperationalWorldOperatingSystemIds.SsalddelMartUrbanLogistics));
        Assert.Equal(
            [
                "cargo-normal", "cargo-recovery", "food-normal", "food-recovery", "mart-normal", "mart-recovery",
                WarehouseLifecycleValidationCaseCodes.Normal,
                WarehouseLifecycleValidationCaseCodes.QuantityMismatchRecovery
            ],
            first.Cases.Select(item => item.CaseCode).Order(StringComparer.Ordinal).ToArray());

        var allowedItemKinds = new HashSet<string>(StringComparer.Ordinal)
        {
            OperationalWorldSceneItemKinds.CompletedLifecycle,
            OperationalWorldSceneItemKinds.CargoHandoff,
            OperationalWorldSceneItemKinds.WarehouseTask,
            OperationalWorldSceneItemKinds.WarehouseActor
        };
        foreach (var group in first.Steps.GroupBy(item => item.CaseCode, StringComparer.Ordinal))
        {
            var ordered = group.OrderBy(item => item.StepSequence).ToArray();
            Assert.Equal(Enumerable.Range(1, ordered.Length), ordered.Select(item => item.StepSequence));
            Assert.Equal(Enumerable.Range(1, ordered.Length).Select(value => (long)value), ordered.Select(item => item.Revision));
            Assert.True(ordered.Zip(ordered.Skip(1), (left, right) => left.ScheduledOffsetMilliseconds < right.ScheduledOffsetMilliseconds).All(value => value));
            Assert.Single(ordered.Select(item => item.WorkStableId).Distinct(StringComparer.Ordinal));
            Assert.All(ordered, item =>
            {
                Assert.Contains(item.ItemKind, allowedItemKinds);
                Assert.Equal(OperationalWorldSceneSourceKinds.VerificationSample, item.SourceKindCode);
                Assert.Equal(64, item.StepHashSha256.Length);
            });
        }

        var firstHashes = first.Steps.OrderBy(item => item.CaseCode).ThenBy(item => item.StepSequence)
            .Select(item => item.StepHashSha256).ToArray();
        var secondHashes = second.Steps.OrderBy(item => item.CaseCode).ThenBy(item => item.StepSequence)
            .Select(item => item.StepHashSha256).ToArray();
        Assert.Equal(firstHashes, secondHashes);
        Assert.Equal("83f933695d8cd2c14b46088dd0180c8b7231779be7a46d53170909f6130739a5",
            TimelineFingerprint(first.Steps));
        Assert.All(first.Steps.Where(item => item.CaseCode.Contains("recovery", StringComparison.Ordinal)), item =>
            Assert.Contains(item.AttentionStateCode,
                new[]
                {
                    OperationalWorldAttentionStateCodes.Active,
                    OperationalWorldAttentionStateCodes.RecoveryPending,
                    OperationalWorldAttentionStateCodes.Recovered
                }));
    }

    [Fact]
    public void 창고_사례는_승인된_공통_lifecycle과_회복_경로에_결속된다()
    {
        var definitions = 관찰운영검증Runner.Definitions("verification-run-warehouse");

        foreach (var caseCode in new[]
                 {
                     WarehouseLifecycleValidationCaseCodes.Normal,
                     WarehouseLifecycleValidationCaseCodes.QuantityMismatchRecovery
                 })
        {
            var expected = WarehouseLifecycleValidationCatalog.Get(caseCode);
            var actual = definitions.Steps.Where(item => item.CaseCode == caseCode)
                .OrderBy(item => item.StepSequence)
                .Select(item => item.LifecycleStageCode)
                .ToArray();
            Assert.Equal(expected.StageIds, actual);
        }
    }

    [Fact]
    public async Task 단계_원장은_독립_context_재조회에서도_순서와_hash를_보존한다()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<관찰운영검증DbContext>().UseSqlite(connection).Options;
        var definitions = 관찰운영검증Runner.Definitions("verification-run-readback");

        await using (var write = new 관찰운영검증DbContext(options))
        {
            await write.Database.EnsureCreatedAsync();
            write.Runs.Add(new 관찰운영검증실행Record
            {
                RunStableId = "verification-run-readback",
                StatusCode = 관찰운영검증상태Codes.Running,
                AreaStableId = 관찰운영검증Runner.DefaultAreaStableId,
                DurationSeconds = 600,
                CreatedAtUtc = DateTime.UnixEpoch,
                UpdatedAtUtc = DateTime.UnixEpoch,
                Revision = 1
            });
            write.Cases.AddRange(definitions.Cases);
            write.Steps.AddRange(definitions.Steps);
            await write.SaveChangesAsync();
        }

        await using var read = new 관찰운영검증DbContext(options);
        Assert.Equal(8, await read.Cases.AsNoTracking().CountAsync());
        var rows = await read.Steps.AsNoTracking().OrderBy(item => item.ScheduledOffsetMilliseconds)
            .ThenBy(item => item.CaseCode).ThenBy(item => item.StepSequence).ToArrayAsync();
        Assert.Equal(definitions.Steps.Count, rows.Length);
        Assert.Equal(definitions.Steps.Select(item => item.StepHashSha256).Order(), rows.Select(item => item.StepHashSha256).Order());
    }

    [Fact]
    public void 단계_상태_사본은_개인정보없이_같은_snapshot_ID의_revision을_올린다()
    {
        var definitions = 관찰운영검증Runner.Definitions("verification-run-projection");
        var steps = definitions.Steps.Where(item => item.CaseCode == "food-normal")
            .OrderBy(item => item.StepSequence).Take(2).ToArray();
        var now = new DateTime(2026, 9, 13, 12, 0, 0, DateTimeKind.Utc);
        steps[0].OccurredAtUtc = now;
        steps[1].OccurredAtUtc = now.AddSeconds(10);

        var firstItem = 관찰운영검증Runner.ToSceneItem(steps[0], now);
        var secondItem = 관찰운영검증Runner.ToSceneItem(steps[1], now.AddSeconds(10));
        var firstDocument = Mongo관찰운영검증ProjectionStore.CreateProjectionDocument(firstItem);
        var duplicateDocument = Mongo관찰운영검증ProjectionStore.CreateProjectionDocument(firstItem);
        var secondDocument = Mongo관찰운영검증ProjectionStore.CreateProjectionDocument(secondItem);
        var outbox = 관찰운영검증Runner.CreateOutbox(steps[0], firstItem, now);

        Assert.Equal(firstItem.SnapshotStableId, secondItem.SnapshotStableId);
        Assert.True(secondItem.Revision > firstItem.Revision);
        Assert.Equal(관찰운영검증Runner.TimelineFixtureRevision, firstDocument.TimelineFixtureRevision);
        Assert.Equal(firstDocument.ProjectionHashSha256, duplicateDocument.ProjectionHashSha256);
        Assert.NotEqual(firstDocument.ProjectionHashSha256, secondDocument.ProjectionHashSha256);
        Assert.False(Mongo관찰운영검증ProjectionStore.ShouldReplace(firstDocument, duplicateDocument));
        Assert.True(Mongo관찰운영검증ProjectionStore.ShouldReplace(firstDocument, secondDocument));
        Assert.Throws<InvalidOperationException>(() =>
            Mongo관찰운영검증ProjectionStore.ShouldReplace(secondDocument, firstDocument));
        var conflictItem = 관찰운영검증Runner.ToSceneItem(steps[0], now);
        conflictItem.LifecycleStageCode = "conflicting-stage";
        var conflictDocument = Mongo관찰운영검증ProjectionStore.CreateProjectionDocument(conflictItem);
        var conflict = Assert.Throws<InvalidOperationException>(() =>
            Mongo관찰운영검증ProjectionStore.ShouldReplace(firstDocument, conflictDocument));
        Assert.Equal("ObservableOperationsProjectionRevisionConflict", conflict.Message);
        var changedPublishedItem = 관찰운영검증Runner.ToSceneItem(steps[0], now);
        changedPublishedItem.PublishedAtUtc = changedPublishedItem.PublishedAtUtc.AddSeconds(1);
        var changedPublishedDocument =
            Mongo관찰운영검증ProjectionStore.CreateProjectionDocument(changedPublishedItem);
        Assert.NotEqual(firstDocument.ProjectionHashSha256, changedPublishedDocument.ProjectionHashSha256);
        Assert.Equal("ObservableOperationsProjectionRevisionConflict",
            Assert.Throws<InvalidOperationException>(() =>
                Mongo관찰운영검증ProjectionStore.ShouldReplace(firstDocument, changedPublishedDocument)).Message);
        var changedExpiryItem = 관찰운영검증Runner.ToSceneItem(steps[0], now);
        changedExpiryItem.ExpiresAtUtc = changedExpiryItem.ExpiresAtUtc.AddSeconds(1);
        var changedExpiryDocument =
            Mongo관찰운영검증ProjectionStore.CreateProjectionDocument(changedExpiryItem);
        Assert.NotEqual(firstDocument.ProjectionHashSha256, changedExpiryDocument.ProjectionHashSha256);
        Assert.Equal("ObservableOperationsProjectionRevisionConflict",
            Assert.Throws<InvalidOperationException>(() =>
                Mongo관찰운영검증ProjectionStore.ShouldReplace(firstDocument, changedExpiryDocument)).Message);
        Assert.Equal(firstItem.WorkStableId, secondItem.WorkStableId);
        Assert.False(firstItem.LocalStorageAllowed);
        Assert.False(firstItem.ReplayAllowed);
        using var representation = JsonDocument.Parse(firstItem.RepresentationDataJson);
        Assert.False(representation.RootElement.GetProperty("personalDataIncluded").GetBoolean());
        Assert.True(representation.RootElement.GetProperty("synthetic").GetBoolean());
        Assert.Equal("ObservableOperationLifecycleStepProjected", outbox.EventTypeCode);
        Assert.Contains(":s1:r1", outbox.EventStableId, StringComparison.Ordinal);
        var payload = JsonSerializer.Deserialize<OperationalWorldSceneItem>(outbox.PayloadJson);
        Assert.NotNull(payload);
        Assert.Equal(firstItem.SnapshotStableId, payload!.SnapshotStableId);

        var roundTrip = Mongo관찰운영검증ProjectionStore.ToSceneItem(firstDocument);
        using var roundTripRepresentation = JsonDocument.Parse(roundTrip.RepresentationDataJson);
        Assert.Equal(관찰운영검증Runner.TimelineFixtureRevision,
            roundTripRepresentation.RootElement.GetProperty("fixtureRevision").GetString());
        var differentTimelineItem = 관찰운영검증Runner.ToSceneItem(steps[0], now);
        differentTimelineItem.RepresentationDataJson = differentTimelineItem.RepresentationDataJson.Replace(
            관찰운영검증Runner.TimelineFixtureRevision,
            "observable-operations-lifecycle.r5",
            StringComparison.Ordinal);
        var differentTimelineDocument =
            Mongo관찰운영검증ProjectionStore.CreateProjectionDocument(differentTimelineItem);
        Assert.NotEqual(firstDocument.ProjectionHashSha256, differentTimelineDocument.ProjectionHashSha256);
    }

    [Fact]
    public void Mongo_조회는_지역의_최신_유효_run하나만_선택한다()
    {
        var now = new DateTime(2026, 9, 13, 12, 0, 0, DateTimeKind.Utc);
        var area = 관찰운영검증Runner.DefaultAreaStableId;
        var documents = new[]
        {
            ProjectionDocument("run:old", area, now.AddMinutes(-10), now.AddMinutes(20)),
            ProjectionDocument("run:latest", area, now.AddMinutes(-2), now.AddMinutes(28)),
            ProjectionDocument("run:latest", area, now.AddMinutes(-1), now.AddMinutes(29)),
            ProjectionDocument("run:expired-newer", area, now.AddMinutes(1), now.AddSeconds(-1)),
            ProjectionDocument("run:other-area", "area:other", now.AddMinutes(2), now.AddMinutes(30)),
            ProjectionDocument(string.Empty, area, now.AddMinutes(3), now.AddMinutes(30))
        };

        var selected = Mongo관찰운영검증ProjectionStore.SelectLatestValidScenarioRunStableId(
            documents, area, now);

        Assert.Equal("run:latest", selected);
        Assert.Equal(2, documents.Count(item => item.ScenarioRunStableId == selected));
    }

    [Fact]
    public async Task 같은_사례의_앞_revision_실패는_뒤_게시를_막고_stale_retry는_superseded로_종결한다()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var dbOptions = new DbContextOptionsBuilder<관찰운영검증DbContext>().UseSqlite(connection).Options;
        await using (var seedDb = new 관찰운영검증DbContext(dbOptions))
        {
            await seedDb.Database.EnsureCreatedAsync();
            seedDb.Runs.Add(new 관찰운영검증실행Record
            {
                RunStableId = "verification-run-outbox-order",
                StatusCode = 관찰운영검증상태Codes.Running,
                AreaStableId = 관찰운영검증Runner.DefaultAreaStableId,
                DurationSeconds = 600,
                CreatedAtUtc = DateTime.UnixEpoch,
                UpdatedAtUtc = DateTime.UnixEpoch,
                Revision = 1
            });
            var steps = 관찰운영검증Runner.Definitions("verification-run-outbox-order").Steps
                .Where(item => item.CaseCode == "food-normal")
                .OrderBy(item => item.Revision)
                .Take(2)
                .ToArray();
            var occurredAt = new DateTime(2026, 9, 13, 12, 0, 0, DateTimeKind.Utc);
            steps[0].OccurredAtUtc = occurredAt;
            steps[1].OccurredAtUtc = occurredAt.AddSeconds(1);
            var revision1 = 관찰운영검증Runner.CreateOutbox(
                steps[0], 관찰운영검증Runner.ToSceneItem(steps[0], occurredAt), occurredAt);
            var revision2 = 관찰운영검증Runner.CreateOutbox(
                steps[1], 관찰운영검증Runner.ToSceneItem(steps[1], occurredAt.AddSeconds(1)), occurredAt);
            // 삽입 순서와 무관하게 payload revision 순서를 지켜야 합니다.
            seedDb.Outbox.AddRange(revision2, revision1);
            await seedDb.SaveChangesAsync();
        }

        var writer = new 순서검증ProjectionWriter();
        using var services = new ServiceCollection().BuildServiceProvider();
        var runner = new 관찰운영검증Runner(
            new TestDbContextFactory(dbOptions),
            services.GetRequiredService<IServiceScopeFactory>(),
            writer,
            new NoOpRunStateWriter(),
            new 관찰운영검증Options(),
            TimeProvider.System,
            NullLogger<관찰운영검증Runner>.Instance);
        var now = new DateTime(2026, 9, 13, 12, 1, 0, DateTimeKind.Utc);

        await runner.PublishPendingAsync("verification-run-outbox-order", now, CancellationToken.None);
        await using (var failedRead = new 관찰운영검증DbContext(dbOptions))
        {
            var rows = await failedRead.Outbox.OrderBy(item => item.Id).ToArrayAsync();
            Assert.Equal(관찰운영검증상태Codes.Pending, rows.Single(item => ReadRevision(item) == 2).StatusCode);
            var failed = rows.Single(item => ReadRevision(item) == 1);
            Assert.Equal(관찰운영검증상태Codes.Failed, failed.StatusCode);
            failed.StatusCode = 관찰운영검증상태Codes.Pending;
            failed.NextAttemptAtUtc = now;
            failed.LastErrorCode = string.Empty;
            await failedRead.SaveChangesAsync();
        }
        Assert.Equal([1L], writer.AttemptedRevisions);

        await runner.PublishPendingAsync("verification-run-outbox-order", now, CancellationToken.None);
        await using var completedRead = new 관찰운영검증DbContext(dbOptions);
        var completed = await completedRead.Outbox.OrderBy(item => item.Id).ToArrayAsync();
        var superseded = completed.Single(item => ReadRevision(item) == 1);
        Assert.Equal(관찰운영검증상태Codes.Superseded, superseded.StatusCode);
        Assert.Equal("ObservableOperationsProjectionRevisionStale", superseded.LastErrorCode);
        Assert.NotNull(superseded.ProcessedAtUtc);
        Assert.Equal(관찰운영검증상태Codes.Published,
            completed.Single(item => ReadRevision(item) == 2).StatusCode);
        Assert.DoesNotContain(completed, item => item.StatusCode == 관찰운영검증상태Codes.Failed);
        Assert.Equal([1L, 1L, 2L], writer.AttemptedRevisions);
    }

    [Fact]
    public async Task 시간이_끝나도_게시_실패가_남으면_Running이고_명시적_재시도_후에만_Completed다()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var dbOptions = new DbContextOptionsBuilder<관찰운영검증DbContext>().UseSqlite(connection).Options;
        const string runStableId = "verification-run-completion-retry";
        var definitions = 관찰운영검증Runner.Definitions(runStableId);
        var failedStep = definitions.Steps.Single(item => item.CaseCode == "food-normal" && item.StepSequence == 2);
        var supersededStep = definitions.Steps.Single(item => item.CaseCode == "cargo-normal" && item.StepSequence == 1);
        await SeedCompletionCandidateAsync(
            dbOptions,
            runStableId,
            definitions,
            step => ReferenceEquals(step, failedStep)
                ? 관찰운영검증상태Codes.Failed
                : ReferenceEquals(step, supersededStep)
                    ? 관찰운영검증상태Codes.Superseded
                    : 관찰운영검증상태Codes.Published);

        var writer = new 항상성공ProjectionWriter();
        var runner = CreateRunner(dbOptions, writer);
        var now = new DateTime(2026, 9, 13, 12, 10, 0, DateTimeKind.Utc);

        Assert.False(await runner.TryCompleteRunAsync(runStableId, now, CancellationToken.None));
        await using (var runningRead = new 관찰운영검증DbContext(dbOptions))
        {
            Assert.Equal(관찰운영검증상태Codes.Running,
                (await runningRead.Runs.SingleAsync()).StatusCode);
            var failedRows = await runningRead.Outbox.Where(item => item.CaseCode == failedStep.CaseCode)
                .ToArrayAsync();
            var retry = failedRows.Single(item => ReadRevision(item) == failedStep.Revision);
            retry.StatusCode = 관찰운영검증상태Codes.Pending;
            retry.NextAttemptAtUtc = now;
            retry.LastErrorCode = string.Empty;
            await runningRead.SaveChangesAsync();
        }

        await runner.PublishPendingAsync(runStableId, now, CancellationToken.None);

        await using var completedRead = new 관찰운영검증DbContext(dbOptions);
        var completedRun = await completedRead.Runs.AsNoTracking().SingleAsync();
        Assert.Equal(관찰운영검증상태Codes.Completed, completedRun.StatusCode);
        Assert.Equal(600, completedRun.ElapsedBeforeResumeSeconds);
        Assert.Null(completedRun.ResumedAtUtc);
        Assert.Single(writer.PublishedItems);
        Assert.Equal(0, await completedRead.Outbox.CountAsync(item =>
            item.StatusCode == 관찰운영검증상태Codes.Pending
            || item.StatusCode == 관찰운영검증상태Codes.Failed));
    }

    [Fact]
    public async Task 사례의_최종_revision_Outbox가_superseded면_실행을_완료하지_않는다()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var dbOptions = new DbContextOptionsBuilder<관찰운영검증DbContext>().UseSqlite(connection).Options;
        const string runStableId = "verification-run-final-stale";
        var definitions = 관찰운영검증Runner.Definitions(runStableId);
        var finalStep = definitions.Steps.Where(item => item.CaseCode == "food-normal")
            .MaxBy(item => item.Revision)!;
        await SeedCompletionCandidateAsync(
            dbOptions,
            runStableId,
            definitions,
            step => ReferenceEquals(step, finalStep)
                ? 관찰운영검증상태Codes.Superseded
                : 관찰운영검증상태Codes.Published);
        var runner = CreateRunner(dbOptions, new 항상성공ProjectionWriter());
        var now = new DateTime(2026, 9, 13, 12, 10, 0, DateTimeKind.Utc);

        Assert.False(await runner.TryCompleteRunAsync(runStableId, now, CancellationToken.None));

        await using var read = new 관찰운영검증DbContext(dbOptions);
        Assert.Equal(관찰운영검증상태Codes.Running, (await read.Runs.SingleAsync()).StatusCode);
        Assert.Equal(0, await read.Outbox.CountAsync(item =>
            item.StatusCode == 관찰운영검증상태Codes.Pending
            || item.StatusCode == 관찰운영검증상태Codes.Failed));
    }

    [Fact]
    public async Task 실행_음식점_Fixture_계보는_시작_값을_동결하고_실제_pack_무결성을_따로_판정한다()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var dbOptions = new DbContextOptionsBuilder<관찰운영검증DbContext>().UseSqlite(connection).Options;
        var now = new DateTime(2026, 9, 13, 12, 0, 0, DateTimeKind.Utc);
        var validPack = CreateSafePack('a', now);
        var expiredPack = CreateSafePack('d', now.AddDays(-31));
        await using (var seed = new 관찰운영검증DbContext(dbOptions))
        {
            await seed.Database.EnsureCreatedAsync();
            seed.FixturePacks.AddRange(validPack, expiredPack);
            seed.Runs.AddRange(
                관찰운영검증Runner.CreateRunRecord(
                    "run:valid", 600, validPack.FixturePackStableId,
                    validPack.FixtureHashSha256, validPack.FixtureRevision, now),
                관찰운영검증Runner.CreateRunRecord(
                    "run:tampered-hash", 600, validPack.FixturePackStableId,
                    new string('b', 64), validPack.FixtureRevision, now),
                관찰운영검증Runner.CreateRunRecord(
                    "run:missing-pack", 600, "fixture-pack:sagajeong-restaurants:" + new string('c', 16),
                    new string('c', 64), 관찰운영검증FixtureFactory.FixtureRevision, now),
                관찰운영검증Runner.CreateRunRecord(
                    "run:expired", 600, expiredPack.FixturePackStableId,
                    expiredPack.FixtureHashSha256, expiredPack.FixtureRevision, now),
                관찰운영검증Runner.CreateRunRecord(
                    "run:legacy", 600, string.Empty, string.Empty, string.Empty, now));
            await seed.SaveChangesAsync();
        }

        var runner = CreateRunner(dbOptions, new 항상성공ProjectionWriter());
        var valid = await runner.ReadRunAsync("run:valid", now, CancellationToken.None);
        var tampered = await runner.ReadRunAsync("run:tampered-hash", now, CancellationToken.None);
        var missing = await runner.ReadRunAsync("run:missing-pack", now, CancellationToken.None);
        var expired = await runner.ReadRunAsync("run:expired", now, CancellationToken.None);
        var legacy = await runner.ReadRunAsync("run:legacy", now, CancellationToken.None);

        Assert.Equal(관찰운영검증Fixture상태Codes.Seeded, valid.FixtureStatusCode);
        Assert.Equal(validPack.FixturePackStableId, valid.FixturePackStableId);
        Assert.Equal(validPack.FixtureHashSha256, valid.FixtureHashSha256);
        Assert.Equal(validPack.FixtureRevision, valid.FixtureRevision);
        Assert.Equal(관찰운영검증Fixture상태Codes.InvalidRunFixtureLineage, tampered.FixtureStatusCode);
        Assert.Equal(관찰운영검증Fixture상태Codes.InvalidRunFixtureLineage, missing.FixtureStatusCode);
        Assert.Equal(관찰운영검증Fixture상태Codes.ExpiredRunFixture, expired.FixtureStatusCode);
        Assert.Equal(관찰운영검증Fixture상태Codes.UnboundLegacyRun, legacy.FixtureStatusCode);
    }

    [Fact]
    public async Task 실제_상호를_복제하지_않고_12개_위치_ID에_비공개_샘플_메뉴를_결속한다()
    {
        var path = Path.Combine(Path.GetTempPath(), $"sagajeong-restaurants-{Guid.NewGuid():N}.json");
        try
        {
            var industries = new[]
            {
                "카페", "김밥/만두/분식", "닭/오리고기 구이/찜", "국/탕/찌개류", "냉면/밀면",
                "피자", "마라탕/훠궈", "백반/한정식", "해산물 구이/찜", "일식", "햄버거", "족발/보쌈", "샌드위치", "돈가스"
            };
            var restaurants = industries.Select((industry, index) => new
            {
                directoryStableId = $"directory:sagajeong:test:{index:00}",
                parentStableId = $"business:test:{index:00}",
                displayNameVerbatim = $"실제상호-{index:00}",
                sourceIndustry = industry,
                unityX = 550d + index,
                unityZ = 8d + index,
                buildingCandidateStableId = $"osm:way:{1000 + index}",
                displayReviewStatus = "PendingHumanReview",
                orderParticipationStatus = "Disabled",
                distributionApproved = false,
                orderScenarioEligible = false,
                dataRevision = "test-directory.r1",
                projectionKind = "DerivedProjectionFromStoredObservation"
            }).ToArray();
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(new
            {
                schemaVersion = "sagajeong-restaurant-directory.v1",
                dataRevision = "test-directory.r1",
                projectionKind = "DerivedProjectionFromStoredObservation",
                scope = new
                {
                    kind = "StationCenteredUnitySquare",
                    worldRegionStableId = "world-region:kr:seoul:jungnang:sagajeong.r1",
                    legalAreaStableId = "region:kr:bjd:1126010100",
                    centerX = 550,
                    centerZ = 8,
                    minX = 50,
                    maxX = 1050,
                    minZ = -492,
                    maxZ = 508,
                    coordinateSpace = "SagajeongReference.UnityXZ"
                },
                readiness = new
                {
                    displayReviewStatus = "PendingHumanReview",
                    orderParticipationStatus = "Disabled",
                    publicDisplayEnabled = false,
                    distributionApproved = false,
                    orderScenarioEligible = false
                },
                restaurants
            }));
            var now = new DateTime(2026, 9, 13, 12, 0, 0, DateTimeKind.Utc);

            var first = await 관찰운영검증FixtureFactory.ReadAsync(path, now, CancellationToken.None);
            var second = await 관찰운영검증FixtureFactory.ReadAsync(path, now, CancellationToken.None);
            Assert.NotNull(first);
            Assert.NotNull(second);
            Assert.Equal(first!.Pack.FixtureHashSha256, second!.Pack.FixtureHashSha256);
            Assert.Equal("sagajeong-restaurant-fixture.v2", first.Pack.FixtureRevision);
            Assert.Equal(12, first.Restaurants.Count);
            Assert.All(first.Restaurants, restaurant =>
            {
                Assert.StartsWith("[샘플]", restaurant.SampleDisplayName, StringComparison.Ordinal);
                Assert.DoesNotContain("실제상호", restaurant.SampleDisplayName, StringComparison.Ordinal);
                Assert.InRange(restaurant.Menus.Count, 2, 3);
                Assert.All(restaurant.Menus, menu => Assert.StartsWith("[샘플]", menu.Name, StringComparison.Ordinal));
            });
            var fixtureJson = JsonSerializer.Serialize(first);
            Assert.DoesNotContain("실제상호", fixtureJson, StringComparison.Ordinal);
            Assert.DoesNotContain("sourceRoadAddress", fixtureJson, StringComparison.OrdinalIgnoreCase);

            var appOptions = new DbContextOptionsBuilder<SsalddelContext>()
                .UseInMemoryDatabase($"observable-fixture-app-{Guid.NewGuid():N}").Options;
            var verificationOptions = new DbContextOptionsBuilder<관찰운영검증DbContext>()
                .UseInMemoryDatabase($"observable-fixture-ledger-{Guid.NewGuid():N}").Options;
            await using var applicationDb = new SsalddelContext(appOptions, new PassThroughEncryption());
            await using var verificationDb = new 관찰운영검증DbContext(verificationOptions);

            await 관찰운영검증FixtureFactory.PersistAsync(first, applicationDb, verificationDb, now, CancellationToken.None);
            await 관찰운영검증FixtureFactory.PersistAsync(first, applicationDb, verificationDb, now, CancellationToken.None);

            var profiles = await applicationDb.음식점공개프로필.AsNoTracking().ToArrayAsync();
            var menus = await applicationDb.음식점메뉴.AsNoTracking().ToArrayAsync();
            var bindings = await verificationDb.FixtureBindings.AsNoTracking().ToArrayAsync();
            Assert.Equal(12, profiles.Length);
            Assert.InRange(menus.Length, 24, 36);
            Assert.Equal(12 + menus.Length, bindings.Length);
            Assert.All(profiles, profile =>
            {
                Assert.False(profile.공개여부);
                Assert.False(profile.주문가능여부);
                Assert.DoesNotContain("실제상호", profile.상호명, StringComparison.Ordinal);
            });
            Assert.All(bindings, binding =>
            {
                Assert.Equal(관찰운영검증FixtureFactory.AffiliationCode, binding.AffiliationCode);
                Assert.Equal(관찰운영검증FixtureFactory.BindingPurposeCode, binding.BindingPurposeCode);
                Assert.False(binding.ActualOrderAllowed);
                Assert.False(binding.DistributionApproved);
                Assert.NotEmpty(binding.PublicBusinessObservationStableId);
                Assert.NotEmpty(binding.LocationAnchorStableId);
            });

            verificationDb.ChangeTracker.Clear();
            var changedPack = await verificationDb.FixturePacks.SingleAsync();
            changedPack.OperationalEffectsAllowed = true;
            await verificationDb.SaveChangesAsync();
            var packConflict = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                관찰운영검증FixtureFactory.PersistAsync(
                    first, applicationDb, verificationDb, now, CancellationToken.None));
            Assert.Equal("ObservableOperationsRestaurantFixturePackConflict", packConflict.Message);
            changedPack.OperationalEffectsAllowed = false;
            await verificationDb.SaveChangesAsync();

            var changedProfile = await applicationDb.음식점공개프로필.FirstAsync();
            var originalDescription = changedProfile.소개;
            changedProfile.소개 = "다른 Fixture가 사용 중인 행";
            await applicationDb.SaveChangesAsync();
            var conflict = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                관찰운영검증FixtureFactory.PersistAsync(
                    first, applicationDb, verificationDb, now, CancellationToken.None));
            Assert.Equal("ObservableOperationsRestaurantFixtureProfileIdConflict", conflict.Message);
            changedProfile.소개 = originalDescription;
            await applicationDb.SaveChangesAsync();

            var changedBinding = await verificationDb.FixtureBindings.FirstAsync();
            changedBinding.ActualOrderAllowed = true;
            await verificationDb.SaveChangesAsync();
            var bindingConflict = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                관찰운영검증FixtureFactory.PersistAsync(
                    first, applicationDb, verificationDb, now, CancellationToken.None));
            Assert.Equal("ObservableOperationsRestaurantFixtureBindingConflict", bindingConflict.Message);
            changedBinding.ActualOrderAllowed = false;
            await verificationDb.SaveChangesAsync();

            verificationDb.ChangeTracker.Clear();
            verificationDb.FixtureBindings.Add(new 관찰운영검증Fixture결속Record
            {
                FixturePackStableId = first.Pack.FixturePackStableId,
                FixtureObjectStableId = "fixture-object:unexpected-extra-binding",
                ObjectKindCode = "Unexpected",
                BindingHashSha256 = new string('0', 64)
            });
            await verificationDb.SaveChangesAsync();
            var bindingSetConflict = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                관찰운영검증FixtureFactory.PersistAsync(
                    first, applicationDb, verificationDb, now, CancellationToken.None));
            Assert.Equal("ObservableOperationsRestaurantFixtureBindingSetConflict", bindingSetConflict.Message);

            var emptyAppOptions = new DbContextOptionsBuilder<SsalddelContext>()
                .UseInMemoryDatabase($"observable-fixture-empty-app-{Guid.NewGuid():N}").Options;
            await using var emptyApplicationDb = new SsalddelContext(emptyAppOptions, new PassThroughEncryption());
            var preflightConflict = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                관찰운영검증FixtureFactory.PersistAsync(
                    first, emptyApplicationDb, verificationDb, now, CancellationToken.None));
            Assert.Equal("ObservableOperationsRestaurantFixtureBindingSetConflict", preflightConflict.Message);
            Assert.Empty(await emptyApplicationDb.음식점공개프로필.AsNoTracking().ToArrayAsync());
            Assert.Empty(await emptyApplicationDb.음식점메뉴.AsNoTracking().ToArrayAsync());
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private static string TimelineFingerprint(IEnumerable<관찰운영검증단계Record> steps)
    {
        var canonical = string.Join('\n', steps
            .OrderBy(item => item.CaseCode, StringComparer.Ordinal)
            .ThenBy(item => item.StepSequence)
            .Select(item => $"{item.CaseCode}|{item.StepSequence}|{item.StepHashSha256}"));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    private static 관찰운영검증ProjectionDocument ProjectionDocument(
        string runStableId,
        string areaStableId,
        DateTime publishedAtUtc,
        DateTime expiresAtUtc)
        => new()
        {
            SnapshotStableId = $"snapshot:{runStableId}:{publishedAtUtc.Ticks}",
            ScenarioRunStableId = runStableId,
            AreaStableId = areaStableId,
            PublishedAtUtc = publishedAtUtc,
            ExpiresAtUtc = expiresAtUtc
        };

    private static 관찰운영검증Fixture묶음Record CreateSafePack(char hashCharacter, DateTime createdAtUtc)
    {
        var fixtureHash = new string(hashCharacter, 64);
        return new 관찰운영검증Fixture묶음Record
        {
            FixturePackStableId = "fixture-pack:sagajeong-restaurants:" + fixtureHash[..16],
            FixtureRevision = 관찰운영검증FixtureFactory.FixtureRevision,
            FixtureHashSha256 = fixtureHash,
            InputHashSha256 = new string('e', 64),
            SourceRevision = "test-directory.r1",
            DeterministicSeed = 관찰운영검증FixtureFactory.DeterministicSeed,
            SourceKindCode = 관찰운영검증FixtureFactory.SourceKindCode,
            EnvironmentCode = 관찰운영검증FixtureFactory.EnvironmentCode,
            DistributionApproved = false,
            OperationalEffectsAllowed = false,
            GeneratorVersion = 관찰운영검증FixtureFactory.GeneratorVersion,
            CreatedAtUtc = createdAtUtc,
            ExpiresAtUtc = createdAtUtc.AddDays(30)
        };
    }

    private static async Task SeedCompletionCandidateAsync(
        DbContextOptions<관찰운영검증DbContext> dbOptions,
        string runStableId,
        관찰운영검증TimelineDefinitions definitions,
        Func<관찰운영검증단계Record, string> outboxStatus)
    {
        var now = new DateTime(2026, 9, 13, 12, 0, 0, DateTimeKind.Utc);
        var pack = CreateSafePack('f', now);
        var run = 관찰운영검증Runner.CreateRunRecord(
            runStableId,
            600,
            pack.FixturePackStableId,
            pack.FixtureHashSha256,
            pack.FixtureRevision,
            now);
        run.ElapsedBeforeResumeSeconds = 600;
        run.ResumedAtUtc = null;
        foreach (var step in definitions.Steps)
        {
            step.StateCode = 관찰운영검증상태Codes.Published;
            step.OccurredAtUtc = now.AddMilliseconds(step.ScheduledOffsetMilliseconds);
        }
        foreach (var item in definitions.Cases)
        {
            var final = definitions.Steps.Where(step => step.CaseCode == item.CaseCode)
                .MaxBy(step => step.Revision)!;
            item.LifecycleStageCode = final.LifecycleStageCode;
            item.AttentionStateCode = final.AttentionStateCode;
            item.ObjectKindCode = final.ObjectKindCode;
            item.ItemKind = final.ItemKind;
            item.RoleCode = final.RoleCode;
            item.SemanticPlaceStableId = final.SemanticPlaceStableId;
            item.RelationStableIdsJson = final.RelationStableIdsJson;
            item.StateCode = 관찰운영검증상태Codes.Published;
            item.Revision = final.Revision;
            item.OccurredAtUtc = final.OccurredAtUtc;
        }

        await using var db = new 관찰운영검증DbContext(dbOptions);
        await db.Database.EnsureCreatedAsync();
        db.FixturePacks.Add(pack);
        db.Runs.Add(run);
        db.Cases.AddRange(definitions.Cases);
        db.Steps.AddRange(definitions.Steps);
        foreach (var step in definitions.Steps)
        {
            var occurredAt = step.OccurredAtUtc!.Value;
            var outbox = 관찰운영검증Runner.CreateOutbox(
                step, 관찰운영검증Runner.ToSceneItem(step, occurredAt), occurredAt);
            outbox.StatusCode = outboxStatus(step);
            if (outbox.StatusCode == 관찰운영검증상태Codes.Failed)
                outbox.LastErrorCode = "SyntheticProjectionFailure";
            db.Outbox.Add(outbox);
        }
        await db.SaveChangesAsync();
    }

    private static 관찰운영검증Runner CreateRunner(
        DbContextOptions<관찰운영검증DbContext> dbOptions,
        I관찰운영검증ProjectionWriter writer)
    {
        var services = new ServiceCollection().BuildServiceProvider();
        return new 관찰운영검증Runner(
            new TestDbContextFactory(dbOptions),
            services.GetRequiredService<IServiceScopeFactory>(),
            writer,
            new NoOpRunStateWriter(),
            new 관찰운영검증Options(),
            TimeProvider.System,
            NullLogger<관찰운영검증Runner>.Instance);
    }

    private static long ReadRevision(관찰운영검증OutboxRecord record)
        => JsonSerializer.Deserialize<OperationalWorldSceneItem>(record.PayloadJson)?.Revision ?? 0;

    private sealed class TestDbContextFactory(DbContextOptions<관찰운영검증DbContext> options)
        : IDbContextFactory<관찰운영검증DbContext>
    {
        public 관찰운영검증DbContext CreateDbContext() => new(options);

        public Task<관찰운영검증DbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(CreateDbContext());
    }

    private sealed class 순서검증ProjectionWriter : I관찰운영검증ProjectionWriter
    {
        public List<long> AttemptedRevisions { get; } = [];

        public Task UpsertAsync(OperationalWorldSceneItem item, CancellationToken cancellationToken)
        {
            AttemptedRevisions.Add(item.Revision);
            if (AttemptedRevisions.Count == 1)
                throw new InvalidOperationException("SyntheticProjectionFailure");
            if (item.Revision == 1)
                throw new InvalidOperationException("ObservableOperationsProjectionRevisionStale");
            return Task.CompletedTask;
        }
    }

    private sealed class 항상성공ProjectionWriter : I관찰운영검증ProjectionWriter
    {
        public List<OperationalWorldSceneItem> PublishedItems { get; } = [];

        public Task UpsertAsync(OperationalWorldSceneItem item, CancellationToken cancellationToken)
        {
            PublishedItems.Add(item);
            return Task.CompletedTask;
        }
    }

    private sealed class NoOpRunStateWriter : I관찰운영검증RunStateWriter
    {
        public Task WriteAsync(관찰운영검증상태 state, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    private sealed class PassThroughEncryption : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
}
