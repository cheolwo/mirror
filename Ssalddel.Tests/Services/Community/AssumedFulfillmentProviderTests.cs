using FluentResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.Fulfillment;
using Ssalddel.Services.Fulfillment;
using 살뜰.Data;
using 살뜰.Services.Options;

namespace Ssalddel.Tests.Services.Community;

/// <summary>가정 수행처의 Simulation 관문과 기존 SQL 모의 접수만 검증합니다. 실제 업체 API·요금·운영 증거가 아닙니다.</summary>
public sealed class AssumedFulfillmentProviderTests
{
    [Theory]
    [InlineData(false, true, SsalddelExecutionMode.Simulation, 3)]
    [InlineData(true, true, SsalddelExecutionMode.Simulation, 6)]
    [InlineData(true, false, SsalddelExecutionMode.Simulation, 0)]
    [InlineData(true, true, SsalddelExecutionMode.Operational, 0)]
    public async Task 서버목록은기능과별도optin과Simulation과원천을모두확인한다(bool assumed, bool enabled, SsalddelExecutionMode mode, int expected)
    {
        await using var f = await AutoFulfillmentCoordinatorTests.Fixture.CreateAsync(false);
        var options = new FulfillmentRoutingOptions { Enabled = enabled, AssumedProvidersEnabled = assumed };
        var service = Service(f, options, mode);
        Assert.Equal(expected, service.수행처후보(FulfillmentSourceKinds.NeighborhoodDelivery).Count);
        Assert.Equal(expected == 0 ? 0 : 3, service.수행처후보(FulfillmentSourceKinds.FoodOrder).Count);
        Assert.Empty(service.수행처후보("Unknown"));
        Assert.False(new FulfillmentRoutingOptions().AssumedProvidersEnabled);
        foreach (var descriptor in service.수행처후보(FulfillmentSourceKinds.NeighborhoodDelivery)
            .Where(x => AssumedFulfillmentProviderCatalog.IsAssumed(x.ProviderId)))
        {
            Assert.True(descriptor.IsSimulation);
            Assert.Contains("모의", descriptor.ProviderName);
        }
    }

    [Theory]
    [InlineData(false, FulfillmentSourceKinds.NeighborhoodDelivery)]
    [InlineData(true, FulfillmentSourceKinds.FoodOrder)]
    public async Task 비활성가정후보나음식원천에가정ID를동의해도등록하지않는다(bool assumed, string sourceKind)
    {
        await using var f = await AutoFulfillmentCoordinatorTests.Fixture.CreateAsync();
        var key = sourceKind == FulfillmentSourceKinds.NeighborhoodDelivery ? f.Key : f.Food.Snapshot.Key;
        var request = f.Consent(); request.AllowedProviderIds = [AssumedFulfillmentProviderCatalog.Baemin];
        var result = await Service(f, new() { Enabled = true, AssumedProvidersEnabled = assumed })
            .사전동의저장Async(key, "owner", request);
        Assert.Equal("FulfillmentConsentRequired", Code(result));
        Assert.Empty(await f.Db.AutoFulfillmentConsents.ToListAsync());
        Assert.Empty(await f.Db.FulfillmentMockJobs.ToListAsync());
    }

    [Theory]
    [InlineData(false, SsalddelExecutionMode.Simulation, FulfillmentSourceKinds.NeighborhoodDelivery)]
    [InlineData(true, SsalddelExecutionMode.Operational, FulfillmentSourceKinds.NeighborhoodDelivery)]
    [InlineData(true, SsalddelExecutionMode.Simulation, FulfillmentSourceKinds.FoodOrder)]
    public async Task 가정연결을직접호출해도관문밖에서는SQL접수를하지않는다(bool assumed, SsalddelExecutionMode mode, string sourceKind)
    {
        await using var f = await AutoFulfillmentCoordinatorTests.Fixture.CreateAsync(false);
        var options = new FulfillmentRoutingOptions { Enabled = true, AssumedProvidersEnabled = assumed };
        var provider = Assumed(f, f.Db, options, mode, AssumedFulfillmentProviderCatalog.Providers[0]);
        var key = new FulfillmentSourceKey(sourceKind, f.Key.SourceId);
        var result = await provider.접수Async(new(key, "not-persisted", provider.Id, 1, 1, "initial", 1000,
            f.Clock.Now.AddMinutes(30), null), f.Source, default);
        Assert.Equal(AutoFulfillmentStates.Failed, result.StateCode);
        Assert.Equal(0m, result.ConfirmedIncurredCostKrw);
    }

    [Theory]
    [InlineData(SsalddelExecutionMode.Operational, FulfillmentSourceKinds.NeighborhoodDelivery)]
    [InlineData(SsalddelExecutionMode.Simulation, FulfillmentSourceKinds.FoodOrder)]
    public async Task 가정연결직접조회취소도운영모드나잘못된원천에서는SQL에접근하지않는다(SsalddelExecutionMode mode, string sourceKind)
    {
        await using var f = await AutoFulfillmentCoordinatorTests.Fixture.CreateAsync(false);
        var provider = Assumed(f, f.Db, new() { Enabled = true, AssumedProvidersEnabled = true }, mode,
            AssumedFulfillmentProviderCatalog.Providers[0]);
        var attempt = new FulfillmentAttemptContext(new(sourceKind, "not-persisted"), "not-persisted", provider.Id, 1, 1,
            "initial", 1000, f.Clock.Now.AddMinutes(30), null);
        Assert.Equal(AutoFulfillmentStates.ResultUnknown, (await provider.조회Async(attempt, f.Source, default)).StateCode);
        Assert.Equal(AutoFulfillmentStates.CancelPending, (await provider.취소Async(attempt, f.Source, default)).StateCode);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    public async Task Simulation에서기능과optin을꺼도기존시도확인취소는보존한다(bool enabled, bool assumed)
    {
        await using var f = await AutoFulfillmentCoordinatorTests.Fixture.CreateAsync();
        f.Source.Snapshot = f.Source.Snapshot with { PackageSizeCode = NeighborhoodPackageSizes.Small };
        var service = Service(f); var consent = f.Consent(); consent.AllowedProviderIds = [AssumedFulfillmentProviderCatalog.KakaoTQuick];
        await service.사전동의저장Async(f.Key, "owner", consent);
        var state = (await service.선정실행Async(f.Key, Guid.NewGuid())).Value;
        var attempt = new FulfillmentAttemptContext(f.Key, state.AttemptId!, state.ProviderId!, state.Revision,
            state.ConsentRevision, "initial", state.TotalCostKrw!.Value, state.EstimatedCompletionUtc!.Value, state.ExternalRequestId);
        var disabled = Assumed(f, f.Db, new() { Enabled = enabled, AssumedProvidersEnabled = assumed }, SsalddelExecutionMode.Simulation,
            AssumedFulfillmentProviderCatalog.Providers[0]);
        Assert.Equal(AutoFulfillmentStates.Accepted, (await disabled.조회Async(attempt, f.Source, default)).StateCode);
        Assert.Equal(AutoFulfillmentStates.Cancelled, (await disabled.취소Async(attempt, f.Source, default)).StateCode);
        Assert.Single(await f.Db.FulfillmentMockJobs.ToListAsync());
    }

    [Fact]
    public async Task 운영모드의직접조회취소는보존된모의job을변경하지않는다()
    {
        await using var f = await AutoFulfillmentCoordinatorTests.Fixture.CreateAsync();
        f.Source.Snapshot = f.Source.Snapshot with { PackageSizeCode = NeighborhoodPackageSizes.Small };
        var service = Service(f); var consent = f.Consent(); consent.AllowedProviderIds = [AssumedFulfillmentProviderCatalog.KakaoTQuick];
        await service.사전동의저장Async(f.Key, "owner", consent);
        var state = (await service.선정실행Async(f.Key, Guid.NewGuid())).Value;
        var attempt = new FulfillmentAttemptContext(f.Key, state.AttemptId!, state.ProviderId!, state.Revision,
            state.ConsentRevision, "initial", state.TotalCostKrw!.Value, state.EstimatedCompletionUtc!.Value, state.ExternalRequestId);
        var provider = Assumed(f, f.Db, new() { Enabled = true, AssumedProvidersEnabled = true }, SsalddelExecutionMode.Operational,
            AssumedFulfillmentProviderCatalog.Providers[0]);
        Assert.Equal(AutoFulfillmentStates.ResultUnknown, (await provider.조회Async(attempt, f.Source, default)).StateCode);
        Assert.Equal(AutoFulfillmentStates.CancelPending, (await provider.취소Async(attempt, f.Source, default)).StateCode);
        Assert.Equal(AutoFulfillmentStates.Accepted, (await f.Db.FulfillmentMockJobs.AsNoTracking().SingleAsync()).StateCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Unknown")]
    public async Task 크기가명시되지않은가정후보는보류하고임의크기나접수를만들지않는다(string? size)
    {
        await using var f = await AutoFulfillmentCoordinatorTests.Fixture.CreateAsync();
        f.Source.Snapshot = f.Source.Snapshot with { PackageSizeCode = size };
        var service = Service(f);
        var consent = f.Consent(); consent.AllowedProviderIds = AssumedFulfillmentProviderCatalog.Providers.Select(x => x.ProviderId).ToArray();
        Assert.True((await service.사전동의저장Async(f.Key, "owner", consent)).IsSuccess);
        var result = await service.선정실행Async(f.Key, Guid.NewGuid());
        Assert.Equal(AutoFulfillmentStates.NeedsReview, result.Value.StateCode);
        Assert.Equal("NoFeasibleProvider", result.Value.ReviewCode);
        Assert.Empty(await f.Db.NeighborhoodFulfillmentAttempts.ToListAsync());
        Assert.Empty(await f.Db.FulfillmentMockJobs.ToListAsync());
        Assert.Equal(size, f.Source.Snapshot.PackageSizeCode);
    }

    [Theory]
    [InlineData(NeighborhoodPackageSizes.Small)]
    [InlineData(NeighborhoodPackageSizes.Medium)]
    [InlineData(NeighborhoodPackageSizes.Large)]
    public async Task 명시한크기로합성견적과모의접수를확인하고재시작해도같은시도를조회한다(string size)
    {
        await using var f = await AutoFulfillmentCoordinatorTests.Fixture.CreateAsync();
        f.Source.Snapshot = f.Source.Snapshot with { PackageSizeCode = size };
        var service = Service(f);
        var consent = f.Consent(); consent.AllowedProviderIds = [AssumedFulfillmentProviderCatalog.Baemin];
        Assert.True((await service.사전동의저장Async(f.Key, "owner", consent)).IsSuccess);
        Assert.True((await service.사전동의저장Async(f.Key, "owner", consent)).IsSuccess);
        var selected = (await service.선정실행Async(f.Key, Guid.NewGuid())).Value;
        Assert.Equal(AutoFulfillmentStates.Accepted, selected.StateCode);
        Assert.Equal("배민 연동 가정 · 모의", selected.ProviderName);
        Assert.Equal(11500m, selected.TotalCostKrw);
        Assert.True(selected.IsSimulation); Assert.False(selected.IsOperationalSettlementEligible);
        await using var restored = f.OpenDb();
        var read = (await Service(f, db: restored).선정실행Async(f.Key, Guid.NewGuid())).Value;
        Assert.Equal(selected.AttemptId, read.AttemptId);
        Assert.Equal(selected.ExternalRequestId, read.ExternalRequestId);
        Assert.Single(await restored.NeighborhoodFulfillmentAttempts.ToListAsync());
        Assert.Single(await restored.FulfillmentMockJobs.ToListAsync());
        Assert.Empty(await restored.운송원장.ToListAsync());
    }

    [Fact]
    public async Task 여섯등록후보를동의할수있고중복ID나등록밖ID는거부한다()
    {
        await using var f = await AutoFulfillmentCoordinatorTests.Fixture.CreateAsync();
        var service = Service(f); var request = f.Consent();
        request.AllowedProviderIds = service.수행처후보(FulfillmentSourceKinds.NeighborhoodDelivery).Select(x => x.ProviderId).ToArray();
        Assert.Equal(6, request.AllowedProviderIds.Count);
        Assert.True((await service.사전동의저장Async(f.Key, "owner", request)).IsSuccess);
        request = f.Consent(); request.ExpectedRevision = 1; request.AllowedProviderIds = ["internal", "internal"];
        Assert.Equal("FulfillmentConsentRequired", Code(await service.사전동의저장Async(f.Key, "owner", request)));
        request.AllowedProviderIds = ["not-registered"];
        Assert.Equal("FulfillmentConsentRequired", Code(await service.사전동의저장Async(f.Key, "owner", request)));
    }

    [Theory]
    [InlineData(5)]
    [InlineData(7)]
    public async Task 후보수하드코딩없이동의목록을한번씩소진하고보류한다(int count)
    {
        await using var f = await AutoFulfillmentCoordinatorTests.Fixture.CreateAsync();
        var providers = Enumerable.Range(1, count).Select(x => new 즉시실패Provider("registered-" + x, f.Clock)).ToArray();
        var service = Service(f, customProviders: providers);
        var consent = f.Consent(); consent.AllowedProviderIds = providers.Select(x => x.Id).ToArray();
        Assert.True((await service.사전동의저장Async(f.Key, "owner", consent)).IsSuccess);
        var result = await service.선정실행Async(f.Key, Guid.NewGuid());
        Assert.Equal(AutoFulfillmentStates.NeedsReview, result.Value.StateCode);
        Assert.Equal("ProvidersExhausted", result.Value.ReviewCode);
        Assert.Equal(count, await f.Db.NeighborhoodFulfillmentAttempts.CountAsync());
        Assert.All(providers, x => Assert.Equal(1, x.Submits));
        await service.선정실행Async(f.Key, Guid.NewGuid());
        Assert.Equal(count, await f.Db.NeighborhoodFulfillmentAttempts.CountAsync());
        Assert.All(providers, x => Assert.Equal(1, x.Submits));
    }

    [Fact]
    public async Task optin철회후기존시도는확인하지만실패후다른가정업체에접수하지않는다()
    {
        await using var f = await AutoFulfillmentCoordinatorTests.Fixture.CreateAsync();
        f.Source.Snapshot = f.Source.Snapshot with { PackageSizeCode = NeighborhoodPackageSizes.Small };
        var options = new FulfillmentRoutingOptions { Enabled = true, AssumedProvidersEnabled = true };
        var service = Service(f, options); var consent = f.Consent();
        consent.AllowedProviderIds = AssumedFulfillmentProviderCatalog.Providers.Select(x => x.ProviderId).ToArray();
        await service.사전동의저장Async(f.Key, "owner", consent);
        var first = (await service.선정실행Async(f.Key, Guid.NewGuid())).Value;
        options.AssumedProvidersEnabled = false;
        Assert.DoesNotContain(service.수행처후보(FulfillmentSourceKinds.NeighborhoodDelivery), x => AssumedFulfillmentProviderCatalog.IsAssumed(x.ProviderId));
        var read = (await service.선정실행Async(f.Key, Guid.NewGuid())).Value;
        Assert.Equal(first.AttemptId, read.AttemptId); Assert.Equal(AutoFulfillmentStates.Accepted, read.StateCode);
        var blocked = await service.모의증거기록Async(first.AttemptId!, new()
        {
            ClientRequestId = Guid.NewGuid(), ExpectedRevision = read.Revision,
            EvidenceKind = FulfillmentEvidenceKinds.Rejected, ConfirmedIncurredCostKrw = 0
        });
        Assert.Equal(AutoFulfillmentStates.NeedsReview, blocked.Value.StateCode);
        Assert.Equal("ProvidersUnavailable", blocked.Value.ReviewCode);
        Assert.Single(await f.Db.NeighborhoodFulfillmentAttempts.ToListAsync());
        Assert.Single(await f.Db.FulfillmentMockJobs.ToListAsync());
    }

    [Fact]
    public async Task 접수응답유실은동일시도재조회로복구하고업체를바꾸지않는다()
    {
        await using var f = await AutoFulfillmentCoordinatorTests.Fixture.CreateAsync();
        f.Source.Snapshot = f.Source.Snapshot with { PackageSizeCode = NeighborhoodPackageSizes.Small };
        var options = new FulfillmentRoutingOptions { Enabled = true, AssumedProvidersEnabled = true };
        var inner = Assumed(f, f.Db, options, SsalddelExecutionMode.Simulation, AssumedFulfillmentProviderCatalog.Providers[0]);
        var losing = new AutoFulfillmentCoordinatorTests.RecordingProvider(inner) { LoseResponse = true };
        var others = AssumedFulfillmentProviderCatalog.Providers.Skip(1).Select(x => Assumed(f, f.Db, options, SsalddelExecutionMode.Simulation, x));
        var service = Service(f, options, customProviders: new I자동수행Provider[] { losing }.Concat(others));
        var consent = f.Consent(); consent.AllowedProviderIds = [losing.Id];
        await service.사전동의저장Async(f.Key, "owner", consent);
        var unknown = (await service.선정실행Async(f.Key, Guid.NewGuid())).Value;
        Assert.Equal(AutoFulfillmentStates.ResultUnknown, unknown.StateCode);
        var recovered = (await service.선정실행Async(f.Key, Guid.NewGuid())).Value;
        Assert.Equal(AutoFulfillmentStates.Accepted, recovered.StateCode);
        Assert.Equal(unknown.AttemptId, recovered.AttemptId);
        Assert.Equal(1, losing.Submits);
        Assert.Single(await f.Db.NeighborhoodFulfillmentAttempts.ToListAsync());
        Assert.Single(await f.Db.FulfillmentMockJobs.ToListAsync());
    }

    [Fact]
    public async Task 실패발생비용이불명확하면가정후보재선정을막는다()
    {
        await using var f = await AutoFulfillmentCoordinatorTests.Fixture.CreateAsync();
        f.Source.Snapshot = f.Source.Snapshot with { PackageSizeCode = NeighborhoodPackageSizes.Small };
        var service = Service(f); var consent = f.Consent();
        consent.AllowedProviderIds = AssumedFulfillmentProviderCatalog.Providers.Select(x => x.ProviderId).ToArray();
        await service.사전동의저장Async(f.Key, "owner", consent);
        var selected = (await service.선정실행Async(f.Key, Guid.NewGuid())).Value;
        var result = await service.모의증거기록Async(selected.AttemptId!, new()
        {
            ClientRequestId = Guid.NewGuid(), ExpectedRevision = selected.Revision, EvidenceKind = FulfillmentEvidenceKinds.Rejected
        });
        Assert.Equal("IncurredCostUnknown", result.Value.ReviewCode);
        Assert.Null(result.Value.TotalCostKrw);
        Assert.Single(await f.Db.NeighborhoodFulfillmentAttempts.ToListAsync());
    }

    [Theory]
    [InlineData(AssumedFulfillmentProviderCatalog.KakaoTQuick)]
    [InlineData(AssumedFulfillmentProviderCatalog.Baemin)]
    [InlineData(AssumedFulfillmentProviderCatalog.CoupangEats)]
    public async Task 각가정연결의배정픽업전달은늦은판본을거부하고지급을확정하지않는다(string providerId)
    {
        await using var f = await AutoFulfillmentCoordinatorTests.Fixture.CreateAsync();
        f.Source.Snapshot = f.Source.Snapshot with { PackageSizeCode = NeighborhoodPackageSizes.Small };
        var service = Service(f); var consent = f.Consent(); consent.AllowedProviderIds = [providerId];
        await service.사전동의저장Async(f.Key, "owner", consent);
        var state = (await service.선정실행Async(f.Key, Guid.NewGuid())).Value;
        var firstRevision = state.Revision;
        foreach (var kind in new[] { FulfillmentEvidenceKinds.DriverAssigned, FulfillmentEvidenceKinds.PickedUp, FulfillmentEvidenceKinds.Delivered })
        {
            var result = await service.모의증거기록Async(state.AttemptId!, new()
            {
                ClientRequestId = Guid.NewGuid(), ExpectedRevision = state.Revision, EvidenceKind = kind,
                ExternalDriverReference = "simulation-driver"
            });
            Assert.True(result.IsSuccess); state = result.Value;
        }
        Assert.Equal(AutoFulfillmentStates.Completed, state.StateCode);
        Assert.False(state.IsOperationalSettlementEligible); Assert.True(state.IsSimulation);
        var late = await service.모의증거기록Async(state.AttemptId!, new()
        {
            ClientRequestId = Guid.NewGuid(), ExpectedRevision = firstRevision, EvidenceKind = FulfillmentEvidenceKinds.PickedUp
        });
        Assert.Equal("FulfillmentRevisionConflict", Code(late));
        Assert.Single(await f.Db.NeighborhoodFulfillmentAttempts.ToListAsync());
        Assert.Empty(await f.Db.운송원장.ToListAsync());
    }

    private static 자동수행Coordinator Service(AutoFulfillmentCoordinatorTests.Fixture f,
        FulfillmentRoutingOptions? options = null, SsalddelExecutionMode mode = SsalddelExecutionMode.Simulation,
        SsalddelContext? db = null, IEnumerable<I자동수행Provider>? customProviders = null)
    {
        options ??= new() { Enabled = true, AssumedProvidersEnabled = true };
        db ??= f.Db;
        var providers = customProviders ?? new I자동수행Provider[]
        {
            new 자체자동수행Provider(f.Clock),
            new 모의자동수행Provider(db, f.Clock, "mock-quick", "모의 퀵 배송", 1.15m, 25, 25),
            new 모의자동수행Provider(db, f.Clock, "mock-local", "모의 생활 배송", .85m, 60, 10)
        }.Concat(AssumedFulfillmentProviderCatalog.Providers.Select(x => Assumed(f, db, options, mode, x)));
        return new(db, [f.Source, f.Food], providers, Options.Create(options),
            new SsalddelExecutionModePolicy(Options.Create(new SsalddelExecutionOptions { Mode = mode })), f.Clock);
    }

    private static 가정자동수행Provider Assumed(AutoFulfillmentCoordinatorTests.Fixture f, SsalddelContext db,
        FulfillmentRoutingOptions options, SsalddelExecutionMode mode, AutoFulfillmentProviderDescriptor descriptor)
        => new(db, f.Clock, descriptor, Options.Create(options),
            new SsalddelExecutionModePolicy(Options.Create(new SsalddelExecutionOptions { Mode = mode })));

    private static string? Code(Result<AutoFulfillmentSnapshot> result)
        => result.Errors.FirstOrDefault()?.Metadata.GetValueOrDefault("ErrorCode") as string;

    private sealed class 즉시실패Provider(string id, TimeProvider clock) : I자동수행Provider
    {
        public string Id => id;
        public string Name => "등록된 소진 검증 수행처 · 모의";
        public int Submits { get; private set; }
        public Task<AutoFulfillmentQuote> 견적Async(FulfillmentSourceSnapshot source, DateTime? deadline, CancellationToken ct)
            => Task.FromResult(new AutoFulfillmentQuote(Id, Name, true, 0, clock.GetUtcNow().UtcDateTime.AddMinutes(10)));
        public Task<FulfillmentExecutionResult> 접수Async(FulfillmentAttemptContext attempt, I수행원천Adapter source, CancellationToken ct)
        { Submits++; return Task.FromResult(new FulfillmentExecutionResult(AutoFulfillmentStates.Failed, ConfirmedIncurredCostKrw: 0)); }
        public Task<FulfillmentExecutionResult> 조회Async(FulfillmentAttemptContext attempt, I수행원천Adapter source, CancellationToken ct)
            => Task.FromResult(new FulfillmentExecutionResult(AutoFulfillmentStates.Failed, ConfirmedIncurredCostKrw: 0));
        public Task<FulfillmentExecutionResult> 취소Async(FulfillmentAttemptContext attempt, I수행원천Adapter source, CancellationToken ct)
            => Task.FromResult(new FulfillmentExecutionResult(AutoFulfillmentStates.Cancelled, ConfirmedIncurredCostKrw: 0));
    }
}
