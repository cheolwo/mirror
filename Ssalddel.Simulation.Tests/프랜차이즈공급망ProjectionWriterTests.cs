using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Simulation.Domain;
using Ssalddel.Simulation.Persistence;

namespace Ssalddel.Simulation.Tests;

[Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
    Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E3,
    "프랜차이즈 공급망 Projection의 개정 단조성·멱등성·범위 격리와 원자 교체 회귀를 검증한다.",
    Boundary = "EF InMemory 자동 시험이며 실제 MySQL 동시 경합이나 운영 공급망 증거가 아니다.",
    SubmoduleKey = Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceSubmoduleKeys.E3계약회귀)]
public sealed class 프랜차이즈공급망ProjectionWriterTests
{
    [Fact]
    public void 늦은과거재시도는_최신Projection을_후퇴시키지않는다()
    {
        using var database = WriterDatabase.Create();
        var writer = database.Writer;
        var oldState = 프랜차이즈공급망SimulationTests.CreateThroughOffer()
            .GetFranchiseSupplyState()!;
        var latestState = 프랜차이즈공급망SimulationTests.CreateFullScenario()
            .GetFranchiseSupplyState()!;

        writer.ReplaceConfirmedState("session:writer", oldState);
        writer.ReplaceConfirmedState("session:writer", latestState);
        writer.ReplaceConfirmedState("session:writer", oldState);

        using var db = database.CreateContext();
        var root = db.FranchiseSupplyNetworkProjections.Single();
        Assert.Equal(latestState.SourceSessionRevision,
            root.SourceSessionRevision);
        Assert.Equal(latestState.ProjectionRevision,
            root.ProjectionRevision);
        Assert.Equal(2, db.FranchiseStoreOrderProjections.Count());
        Assert.Contains("source:menu-ingredient-estimate:r1",
            root.SourceStableIdsJson, StringComparison.Ordinal);
        Assert.All(db.FranchiseSourcingItemProjections, item =>
            Assert.Contains("product-identity-state:SimulationScopedCandidate",
                item.SourceStableIdsJson, StringComparison.Ordinal));
    }

    [Fact]
    public void 같은revision의_다른payload는_충돌하고_같은payload는_멱등이다()
    {
        using var database = WriterDatabase.Create();
        var state = 프랜차이즈공급망SimulationTests.CreateThroughOffer()
            .GetFranchiseSupplyState()!;
        database.Writer.ReplaceConfirmedState("session:idempotent", state);
        database.Writer.ReplaceConfirmedState("session:idempotent", state);

        var conflicting = Clone(state);
        conflicting.DataRevision = "franchise-supply.data.changed";
        var error = Assert.Throws<SimulationConflictException>(() =>
            database.Writer.ReplaceConfirmedState("session:idempotent",
                conflicting));

        Assert.Equal(프랜차이즈공급망ProjectionWriter.RevisionConflictCode,
            error.ErrorCode);
        using var db = database.CreateContext();
        Assert.Equal(state.DataRevision,
            db.FranchiseSupplyNetworkProjections.Single().DataRevision);
    }

    [Fact]
    public void 저장실패는_기존Projection과_다른세션범위를_보존한다()
    {
        var interceptor = new ThrowOnDemandSaveChangesInterceptor();
        using var database = WriterDatabase.Create(interceptor);
        var oldState = 프랜차이즈공급망SimulationTests.CreateThroughOffer()
            .GetFranchiseSupplyState()!;
        var latestState = 프랜차이즈공급망SimulationTests.CreateFullScenario()
            .GetFranchiseSupplyState()!;
        database.Writer.ReplaceConfirmedState("session:one", oldState);
        database.Writer.ReplaceConfirmedState("session:two", oldState);

        interceptor.ThrowNextSave = true;
        Assert.Throws<InvalidOperationException>(() =>
            database.Writer.ReplaceConfirmedState("session:one", latestState));

        using var db = database.CreateContext();
        var roots = db.FranchiseSupplyNetworkProjections
            .OrderBy(value => value.SessionStableId).ToArray();
        Assert.Equal(2, roots.Length);
        Assert.All(roots, root => Assert.Equal(oldState.SourceSessionRevision,
            root.SourceSessionRevision));
        Assert.Empty(db.FranchiseStoreOrderProjections);

        database.Writer.ReplaceConfirmedState("session:one", latestState);
        Assert.Equal(2, db.FranchiseSupplyNetworkProjections.Count());
        Assert.Equal(2, db.FranchiseStoreOrderProjections.Count(value =>
            value.SessionStableId == "session:one"));
        Assert.Empty(db.FranchiseStoreOrderProjections.Where(value =>
            value.SessionStableId == "session:two"));
    }

    private static 프랜차이즈공급망StateSnapshot Clone(
        프랜차이즈공급망StateSnapshot source)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(source);
        return System.Text.Json.JsonSerializer.Deserialize<
            프랜차이즈공급망StateSnapshot>(json)!;
    }

    private sealed class ThrowOnDemandSaveChangesInterceptor
        : SaveChangesInterceptor
    {
        public bool ThrowNextSave { get; set; }

        public override InterceptionResult<int> SavingChanges(
            DbContextEventData eventData,
            InterceptionResult<int> result)
        {
            if (!ThrowNextSave) return result;
            ThrowNextSave = false;
            throw new InvalidOperationException("projection-write-failed");
        }
    }

    private sealed class WriterDatabase : IDisposable
    {
        private readonly DbContextOptions<SimulationSessionDbContext> options;

        private WriterDatabase(
            DbContextOptions<SimulationSessionDbContext> options)
        {
            this.options = options;
            Writer = new 프랜차이즈공급망ProjectionWriter(
                new TestDbContextFactory(options));
        }

        public 프랜차이즈공급망ProjectionWriter Writer { get; }

        public static WriterDatabase Create(
            SaveChangesInterceptor? interceptor = null)
        {
            var builder = new DbContextOptionsBuilder<SimulationSessionDbContext>()
                .UseInMemoryDatabase("franchise-writer-"
                    + Guid.NewGuid().ToString("N"));
            if (interceptor != null) builder.AddInterceptors(interceptor);
            var database = new WriterDatabase(builder.Options);
            using var db = database.CreateContext();
            db.Database.EnsureCreated();
            return database;
        }

        public SimulationSessionDbContext CreateContext()
            => new(options);

        public void Dispose()
        {
        }
    }

    private sealed class TestDbContextFactory(
        DbContextOptions<SimulationSessionDbContext> options)
        : IDbContextFactory<SimulationSessionDbContext>
    {
        public SimulationSessionDbContext CreateDbContext() => new(options);
    }
}
