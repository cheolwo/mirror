using System.Text;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using System.Data.Common;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Ssalddel.Contracts.Common.PublicData;
using Ssalddel.Contracts.Food;
using Ssalddel.Controllers.Driver.Profile01;
using Ssalddel.Services.DeliveryExperience;
using Ssalddel.Services.PrivacyRetention;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.External.PublicData;
using 살뜰.Services.Options;
using 살뜰.도메인.음식;
using 살뜰.도메인.운송;
using 살뜰.도메인.공통;
using Xunit;

namespace Ssalddel.Tests.Services.DeliveryExperience;

public sealed class FoodDeliveryExperienceTests
{
    [Fact]
    public async Task KnownBranchWithoutNameStillCountsAndGenericLabelIsOnlyPresentation()
    {
        await using var f = await Fixture.Create();
        var (past, attempt) = await f.Order("past", completePickup: true);
        past.음식점명 = string.Empty; await f.Db.SaveChangesAsync();
        await f.Service.완료근거추가Async(past, attempt, "PickupCompleted", default); await f.Db.SaveChangesAsync();
        Assert.Equal(string.Empty, (await f.Db.Set<음식배달경험완료Outbox>().SingleAsync()).지점표시명);
        Assert.Equal(1, await f.Service.투영Async(default));
        Assert.Equal(string.Empty, (await f.Db.Set<음식배달경험Fact>().SingleAsync()).장소표시명);
        var (current, _) = await f.Order("current"); current.음식점명 = string.Empty; await f.Db.SaveChangesAsync();
        var summary = Assert.Single(f.Service.기사현재업무요약("driver", "current"));
        Assert.Equal(1, summary.CompletedCount); Assert.Equal("음식점", summary.PlaceDisplayName);
        Assert.Equal("Confirmed", summary.EvidenceStatusCode);
    }
    [Fact]
    public async Task BackfillRetriesAfterSaveBeforeCommitAndRechecksPersistedEvidence()
    {
        var fault = new OneCommitFault();
        await using var f = await Fixture.Create(commitFault: fault);
        var (order, _) = await f.Order("past", completePickup: true);
        order.상태이력.Add(new() { 다음상태 = 음식주문상태코드.픽업완료, 전이시각Utc = f.Now });
        await f.Db.SaveChangesAsync();
        fault.Armed = true;
        Assert.Equal(1, (await f.Service.과거근거점검Async(apply: true)).WouldAppend);
        Assert.Equal(1, fault.Failures);
        Assert.Equal(1, await f.Db.Set<음식배달경험완료Outbox>().CountAsync());
        Assert.Equal(0, (await f.Service.과거근거점검Async(apply: true)).WouldAppend);
    }
    [Fact]
    public async Task ProjectionRetriesAfterSaveBeforeCommitWithoutDuplicateTrackedFacts()
    {
        var fault = new OneCommitFault();
        await using var f = await Fixture.Create(commitFault: fault);
        var (order, attempt) = await f.Order("past", completeDelivery: true);
        await f.Region("past", "road-a", "apt-a");
        await f.Service.완료근거추가Async(order, attempt, "DeliveryCompleted", default); await f.Db.SaveChangesAsync();
        fault.Armed = true;
        Assert.Equal(1, await f.Service.투영Async(default));
        Assert.Equal(1, fault.Failures);
        Assert.Equal(2, await f.Db.Set<음식배달경험Fact>().CountAsync());
        Assert.NotNull((await f.Db.Set<음식배달경험완료Outbox>().AsNoTracking().SingleAsync()).ProjectedAtUtc);
        Assert.Equal(0, await f.Service.투영Async(default));
    }
    [Fact]
    public async Task ThreeOrdersCountThreeAndRetryAndReplacementAttemptCannotDuplicate()
    {
        await using var f = await Fixture.Create();
        for (var i = 1; i <= 3; i++)
        {
            var (order, attempt) = await f.Order("done-" + i, completePickup: true);
            await f.Service.완료근거추가Async(order, attempt, "PickupCompleted", default);
            await f.Service.완료근거추가Async(order, attempt, "PickupCompleted", default);
            await f.Db.SaveChangesAsync();
            attempt.시도StableId = "replacement-" + i;
            await f.Service.완료근거추가Async(order, attempt, "PickupCompleted", default);
        }
        await f.Db.SaveChangesAsync();
        Assert.Equal(3, await f.Db.Set<음식배달경험완료Outbox>().CountAsync());
        Assert.Equal(3, await f.Service.투영Async(default));
        Assert.Equal(0, await f.Service.투영Async(default));
        await f.Order("current");
        Assert.Equal(3, Assert.Single(f.Service.기사현재업무요약("driver", "current")).CompletedCount);
        Assert.Equal(3, await f.Db.Set<음식배달경험Fact>().CountAsync());
    }

    [Fact]
    public async Task SharingStartsOffAndOnlyCurrentBranchAndOrderOwnerCanSee()
    {
        await using var f = await Fixture.Create();
        await f.Order("current");
        Assert.Single(f.Service.기사현재업무요약("driver", "current"));
        Assert.Empty(f.Service.기사현재업무요약("other-driver", "current"));
        Assert.Empty(f.Service.음식점요약("current", 10));
        await f.Share(true);
        Assert.Single(f.Service.음식점요약("current", 10));
        Assert.Empty(f.Service.음식점요약("current", 11));
        Assert.Empty(f.Service.주문자요약("current", "owner")); // 픽업 전
        var attempt = await f.Db.음식배달시도.SingleAsync();
        attempt.픽업완료시각Utc = f.Now;
        await f.Db.SaveChangesAsync();
        await f.Region("current", "road-a", "apt-a");
        Assert.Equal(2, f.Service.주문자요약("current", "owner").Count);
        Assert.Empty(f.Service.주문자요약("current", "another-customer"));
        await f.Share(false);
        Assert.Empty(f.Service.주문자요약("current", "owner"));
        Assert.Empty(f.Service.음식점요약("current", 10));
        Assert.Equal(2, f.Service.기사현재업무요약("driver", "current").Count);
    }

    [Theory]
    [InlineData("cancel")]
    [InlineData("complete")]
    [InlineData("interrupt")]
    [InlineData("replace")]
    [InlineData("queue-change")]
    public async Task ClosingOrReassigningRemovesParticipantSummary(string action)
    {
        await using var f = await Fixture.Create();
        var (order, attempt) = await f.Order("current");
        await f.Share(true);
        Assert.Single(f.Service.음식점요약("current", 10));
        switch (action)
        {
            case "cancel": order.상태 = 음식주문상태코드.취소; break;
            case "complete": attempt.전달완료시각Utc = f.Now; break;
            case "interrupt": attempt.중단시각Utc = f.Now; break;
            case "replace":
                f.Db.음식배달시도.Add(new() { 주문번호 = "current", 시도순번 = 2, 시도StableId = "next", 기사Id = "other", 제안Id = "next", 경험실행모드Code = "Simulation" });
                break;
            case "queue-change": (await f.Db.운송원장.SingleAsync()).확정기사Id = "other"; break;
        }
        await f.Db.SaveChangesAsync();
        Assert.Empty(f.Service.음식점요약("current", 10));
    }

    [Fact]
    public async Task RoadAndApartmentCountIndependentlyAndSimulationCannotReadOperationalCounts()
    {
        await using var f = await Fixture.Create();
        var (one, first) = await f.Order("one", completeDelivery: true); await f.Region("one", "road-a", "apt-a");
        var (two, second) = await f.Order("two", completeDelivery: true); await f.Region("two", "road-a", "apt-b");
        await f.Service.완료근거추가Async(one, first, "DeliveryCompleted", default);
        await f.Service.완료근거추가Async(two, second, "DeliveryCompleted", default);
        await f.Db.SaveChangesAsync(); await f.Service.투영Async(default);
        await f.Order("current", completePickup: true); await f.Region("current", "road-a", "apt-a"); await f.Share(true);
        var summaries = f.Service.주문자요약("current", "owner");
        Assert.Equal(2, summaries.Single(x => x.PlaceKind == "Road").CompletedCount);
        Assert.Equal(1, summaries.Single(x => x.PlaceKind == "ApartmentComplex").CompletedCount);
        Assert.All(summaries, x => { Assert.Equal("Simulation", x.ExecutionModeCode); Assert.Equal(f.Now, x.AsOfUtc); });
        Assert.Empty(f.ServiceFor(SsalddelExecutionMode.Operational).주문자요약("current", "owner"));
        Assert.DoesNotContain("101동", System.Text.Json.JsonSerializer.Serialize(summaries));
        Assert.DoesNotContain("customer-private", System.Text.Json.JsonSerializer.Serialize(summaries));
    }

    [Fact]
    public async Task MissingRegionDoesNotBecomeZeroAndPendingProjectionDoesNotBecomeConfirmed()
    {
        await using var f = await Fixture.Create();
        var (order, attempt) = await f.Order("done", completePickup: true);
        await f.Service.완료근거추가Async(order, attempt, "PickupCompleted", default); await f.Db.SaveChangesAsync();
        await f.Order("current"); await f.Share(true);
        var summary = Assert.Single(f.Service.음식점요약("current", 10));
        Assert.Null(summary.CompletedCount); Assert.Equal("Pending", summary.EvidenceStatusCode);
        var current = await f.Db.음식배달시도.SingleAsync(x => x.주문번호 == "current"); current.픽업완료시각Utc = f.Now;
        await f.Db.SaveChangesAsync();
        Assert.Empty(f.Service.주문자요약("current", "owner"));
    }

    [Fact]
    public async Task LegacyAndUnknownModeAreExcludedAndReportedAsPartial()
    {
        await using var f = await Fixture.Create();
        var (one, first) = await f.Order("legacy", completePickup: true); first.유산추정여부 = true;
        var (two, second) = await f.Order("unknown", completePickup: true); second.경험실행모드Code = null;
        await f.Db.SaveChangesAsync();
        await f.Service.완료근거추가Async(one, first, "PickupCompleted", default);
        await f.Service.완료근거추가Async(two, second, "PickupCompleted", default);
        Assert.Empty(f.Db.Set<음식배달경험완료Outbox>().Local);
        await f.Order("current");
        Assert.Equal("Partial", Assert.Single(f.Service.기사현재업무요약("driver", "current")).EvidenceStatusCode);
        var audit = await f.Service.과거근거점검Async();
        Assert.Equal(1, audit.SkippedLegacy); Assert.Equal(1, audit.SkippedUnknownMode); Assert.Equal(0, audit.WouldAppend);
    }

    [Fact]
    public async Task BackfillDefaultsToDryRunRequiresStatusEvidenceAndReapplyIsIdempotent()
    {
        await using var f = await Fixture.Create();
        var (order, attempt) = await f.Order("past", completePickup: true);
        order.상태이력.Add(new() { 다음상태 = 음식주문상태코드.픽업완료, 전이시각Utc = f.Now }); await f.Db.SaveChangesAsync();
        var dry = await f.Service.과거근거점검Async(); Assert.Equal(1, dry.WouldAppend);
        Assert.Equal(0, await f.Db.Set<음식배달경험완료Outbox>().CountAsync());
        Assert.Equal(1, (await f.Service.과거근거점검Async(apply: true)).WouldAppend);
        Assert.Equal(0, (await f.Service.과거근거점검Async(apply: true)).WouldAppend);
        Assert.Equal(1, await f.Db.Set<음식배달경험완료Outbox>().CountAsync());
        var (unclear, _) = await f.Order("unclear", completePickup: true);
        Assert.True((await f.Service.과거근거점검Async()).SkippedUnclearEvidence > 0);
    }

    [Fact]
    public async Task BackfillHonorsDeletionBarrier()
    {
        await using var f = await Fixture.Create();
        var (order, _) = await f.Order("purged", completePickup: true);
        order.상태이력.Add(new() { 다음상태 = 음식주문상태코드.픽업완료, 전이시각Utc = f.Now }); await f.Db.SaveChangesAsync();
        var service = f.ServiceFor(SsalddelExecutionMode.Simulation, new DeletedPrivacyStore());
        Assert.Equal(0, (await service.과거근거점검Async(apply: true)).WouldAppend);
        Assert.Equal(0, await f.Db.Set<음식배달경험완료Outbox>().CountAsync());
    }

    [Fact]
    public async Task FeatureOffDoesNotRequireNewTables()
    {
        await using var f = await Fixture.Create(false);
        f.Options.Enabled = false;
        Assert.Empty(f.Service.기사현재업무요약("driver", "missing"));
        Assert.Empty(f.Service.음식점요약("missing", 1));
        Assert.Equal(0, await f.Service.투영Async(default));
        Assert.Equal(0, (await f.Service.과거근거점검Async(apply: true)).Scanned);
        await f.Service.원천지역최소화Async("missing", default);
    }

    [Fact]
    public async Task PrivacyPurgeRemovesSourceRegionAndStopsPendingAreaReprojection()
    {
        await using var f = await Fixture.Create();
        var (order, attempt) = await f.Order("past", completeDelivery: true);
        await f.Region("past", "road-a", "apt-a");
        await f.Service.완료근거추가Async(order, attempt, "DeliveryCompleted", default); await f.Db.SaveChangesAsync();
        await f.Service.원천지역최소화Async("past", default); await f.Db.SaveChangesAsync();
        Assert.Equal(0, await f.Db.Set<음식배달전달지역Snapshot>().CountAsync());
        var outbox = await f.Db.Set<음식배달경험완료Outbox>().SingleAsync();
        Assert.Null(outbox.도로Id); Assert.Null(outbox.단지Id); Assert.NotNull(outbox.ProjectedAtUtc);
        Assert.Equal(0, await f.Service.투영Async(default));
        Assert.Equal(0, await f.Db.Set<음식배달경험Fact>().CountAsync());
    }

    [Fact]
    public async Task SharingPreferenceRevisionPreventsLostUpdate()
    {
        await using var f = await Fixture.Create();
        var controller = new 기사배달경험공유Controller(new Ssalddel.Application.Driver.Settings.기사배달경험공유UseCase(f.Db, f.Service, f.Clock))
        {
            ControllerContext = new() { HttpContext = new DefaultHttpContext { User = new(new ClaimsIdentity([new(ClaimTypes.NameIdentifier, "driver")], "test")) } }
        };
        var initial = Assert.IsType<OkObjectResult>(await controller.조회(default));
        Assert.False(Assert.IsType<DeliveryExperienceSharingPreferenceDto>(initial.Value).ShareWithCurrentParticipants);
        Assert.IsType<OkObjectResult>(await controller.저장(new() { ExpectedRevision = 0, ShareWithCurrentParticipants = true }, default));
        Assert.Equal(409, Assert.IsType<ObjectResult>(await controller.저장(new() { ExpectedRevision = 0, ShareWithCurrentParticipants = false }, default)).StatusCode);
        Assert.True((await f.Db.Set<음식배달경험공유설정>().SingleAsync()).현재관계자공유);
        Assert.IsType<OkObjectResult>(await controller.저장(new() { ExpectedRevision = 1, ShareWithCurrentParticipants = false }, default));
        Assert.False((await f.Db.Set<음식배달경험공유설정>().SingleAsync()).현재관계자공유);
    }

    [Fact]
    public async Task DestinationRequiresExactOfficialIdsAndKeepsOnlyRoadOrIndependentComplexName()
    {
        var ports = new AddressPorts();
        var resolver = new 음식배달전달지역Resolver(ports, ports, TimeProvider.System, NullLogger<음식배달전달지역Resolver>.Instance);
        var selected = new FoodDeliveryDestinationIdentityRequest { BuildingManagementNumber = "official-building", RoadNameCode = "official-road" };
        var result = await resolver.확인Async(AddressPorts.Address, selected, default);
        Assert.Equal("official-road", result.도로Id); Assert.Equal("서울특별시 중랑구 동일로72길", result.도로표시명);
        Assert.Equal("apt-official", result.단지Id); Assert.Equal("테스트단지", result.단지표시명);
        Assert.DoesNotContain("9", result.도로표시명);
        await Assert.ThrowsAsync<Ssalddel.Services.Food.음식주문입력확인Exception>(() => resolver.확인Async("서울특별시 중랑구 동일로72길 11", selected, default));
        ports.Healthy = false;
        Assert.Null((await resolver.확인Async(AddressPorts.Address, selected, default)).도로Id);
        Assert.Null((await resolver.확인Async(AddressPorts.Address, null, default)).도로Id);
    }

    [Theory]
    [InlineData("{\"BuildingManagementNumber\":null,\"RoadNameCode\":\"official-road\"}")]
    [InlineData("{\"BuildingManagementNumber\":\"official-building\",\"RoadNameCode\":null}")]
    public async Task ExplicitJsonNullIdentityReturnsInputErrorBeforeExternalLookup(string json)
    {
        var ports = new AddressPorts { RoadFailure = new InvalidOperationException("lookup must not run") };
        var resolver = new 음식배달전달지역Resolver(ports, ports, TimeProvider.System, NullLogger<음식배달전달지역Resolver>.Instance);
        var selected = System.Text.Json.JsonSerializer.Deserialize<FoodDeliveryDestinationIdentityRequest>(json)!;
        var exception = await Assert.ThrowsAsync<Ssalddel.Services.Food.음식주문입력확인Exception>(
            () => resolver.확인Async(AddressPorts.Address, selected, default));
        Assert.Equal("DestinationIdentityInvalid", exception.ErrorCode);
        Assert.Equal(0, ports.RoadCalls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OptionalRoadProviderFailureKeepsUnknownWithoutBlockingOrder(bool timeout)
    {
        var ports = new AddressPorts { RoadFailure = timeout ? new OperationCanceledException() : new HttpRequestException("private address must not be logged") };
        var resolver = new 음식배달전달지역Resolver(ports, ports, TimeProvider.System, NullLogger<음식배달전달지역Resolver>.Instance);
        var selected = new FoodDeliveryDestinationIdentityRequest { BuildingManagementNumber = "official-building", RoadNameCode = "official-road" };
        var snapshot = await resolver.확인Async(AddressPorts.Address, selected, default);
        Assert.Equal("Unknown", snapshot.확인상태Code); Assert.Null(snapshot.도로Id); Assert.Null(snapshot.단지Id);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        ports.RoadFailure = new OperationCanceledException(cancellation.Token);
        await Assert.ThrowsAsync<OperationCanceledException>(() => resolver.확인Async(AddressPorts.Address, selected, cancellation.Token));
    }

    [Fact]
    public async Task OptionalApartmentTimeoutKeepsConfirmedRoadAndCallerCancellationStillPropagates()
    {
        var ports = new AddressPorts { ApartmentFailure = new TaskCanceledException("provider timeout") };
        var resolver = new 음식배달전달지역Resolver(ports, ports, TimeProvider.System, NullLogger<음식배달전달지역Resolver>.Instance);
        var selected = new FoodDeliveryDestinationIdentityRequest { BuildingManagementNumber = "official-building", RoadNameCode = "official-road" };
        var snapshot = await resolver.확인Async(AddressPorts.Address, selected, default);
        Assert.Equal("Confirmed", snapshot.확인상태Code); Assert.Equal("official-road", snapshot.도로Id); Assert.Null(snapshot.단지Id);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        ports.ApartmentFailure = new OperationCanceledException(cancellation.Token);
        await Assert.ThrowsAsync<OperationCanceledException>(() => resolver.확인Async(AddressPorts.Address, selected, cancellation.Token));
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public required SqliteConnection Connection; public required SsalddelContext Db;
        public FoodDeliveryExperienceOptions Options = new() { Enabled = true }; public FixedClock Clock = new();
        public DateTime Now => Clock.GetUtcNow().UtcDateTime;
        public 음식배달경험Service Service => ServiceFor(SsalddelExecutionMode.Simulation);
        public 음식배달경험Service ServiceFor(SsalddelExecutionMode mode, I개인정보보존Store? privacy = null)
            => new(Db, Microsoft.Extensions.Options.Options.Create(Options), new SsalddelExecutionModePolicy(Microsoft.Extensions.Options.Options.Create(new SsalddelExecutionOptions { Mode = mode })), Clock, NullLogger<음식배달경험Service>.Instance, privacy);
        public static async Task<Fixture> Create(bool schema = true, OneCommitFault? commitFault = null)
        {
            var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
            var builder = new DbContextOptionsBuilder<SsalddelContext>().UseSqlite(connection);
            if (commitFault is not null)
                builder.UseSqlite(connection, options => options.ExecutionStrategy(dependencies => new OneRetryStrategy(dependencies))).AddInterceptors(commitFault);
            var db = new SsalddelContext(builder.Options, new Encryption());
            if (schema) await db.Database.EnsureCreatedAsync();
            return new() { Connection = connection, Db = db };
        }
        public async Task<(음식주문, 음식배달시도)> Order(string number, bool completePickup = false, bool completeDelivery = false)
        {
            var order = new 음식주문 { 주문번호 = number, 음식점Id = 10, 음식점명 = "테스트음식점", 주문자UserId = "owner", 상태 = 음식주문상태코드.기사배정,
                수령지상세주소 = "101동 customer-private", CreatedAt = Now, UpdatedAt = Now };
            var attempt = new 음식배달시도 { 주문번호 = number, 시도StableId = "attempt-" + number, 제안Id = "offer-" + number, 기사Id = "driver", 시도순번 = 1,
                경험실행모드Code = "Simulation", Revision = 1, 수락시각Utc = Now, 픽업완료시각Utc = completePickup || completeDelivery ? Now : null, 전달완료시각Utc = completeDelivery ? Now : null };
            Db.AddRange(order, attempt, new 운송원장 { 의뢰Id = attempt.제안Id, 원본의뢰Id = number, 배차업무유형 = 상태값.배차업무유형.음식배달,
                확정기사Id = "driver", 배차큐단계 = 상태값.배차큐단계.확정, CreatedAt = Now, UpdatedAt = Now });
            await Db.SaveChangesAsync(); return (order, attempt);
        }
        public async Task Region(string orderNo, string road, string? apt)
        {
            Db.Set<음식배달전달지역Snapshot>().Add(new() { 주문번호 = orderNo, 도로Id = road, 도로표시명 = "동일로72길", 단지Id = apt,
                단지표시명 = apt is null ? null : "확인한 단지", 확인상태Code = "Confirmed", VerifiedAtUtc = Now }); await Db.SaveChangesAsync();
        }
        public async Task Share(bool enabled)
        {
            var setting = await Db.Set<음식배달경험공유설정>().SingleOrDefaultAsync();
            if (setting is null) { setting = new() { 기사Id = "driver" }; Db.Add(setting); }
            setting.현재관계자공유 = enabled; setting.Revision++; await Db.SaveChangesAsync();
        }
        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await Connection.DisposeAsync(); }
    }
    private sealed class FixedClock : TimeProvider { public override DateTimeOffset GetUtcNow() => new(2080, 1, 2, 3, 4, 5, TimeSpan.Zero); }
    private sealed class FixtureTransientException : Exception { }
    private sealed class OneRetryStrategy(ExecutionStrategyDependencies dependencies) : ExecutionStrategy(dependencies, 1, TimeSpan.Zero)
    {
        protected override bool ShouldRetryOn(Exception exception) => exception is FixtureTransientException;
    }
    private sealed class OneCommitFault : DbTransactionInterceptor
    {
        public bool Armed; public int Failures;
        public override ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction, TransactionEventData eventData,
            InterceptionResult result, CancellationToken cancellationToken = default)
        {
            if (Armed && Failures == 0) { Failures++; throw new FixtureTransientException(); }
            return base.TransactionCommittingAsync(transaction, eventData, result, cancellationToken);
        }
    }
    private sealed class Encryption : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value is null ? null : Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
        public string? Unprotect(string? value) => value is null ? null : Encoding.UTF8.GetString(Convert.FromBase64String(value));
    }
    private sealed class AddressPorts : IRoadAddressLookupService, IApartmentComplexLookupService
    {
        public const string Address = "서울특별시 중랑구 동일로72길 9"; public bool Healthy = true;
        public Exception? RoadFailure, ApartmentFailure; public int RoadCalls;
        public Task<PublicDataLookupResponse<RoadAddressItem>> SearchAsync(RoadAddressSearchRequest request, CancellationToken ct = default)
        {
            RoadCalls++;
            return RoadFailure is not null ? Task.FromException<PublicDataLookupResponse<RoadAddressItem>>(RoadFailure)
                : Task.FromResult(new PublicDataLookupResponse<RoadAddressItem> { Success = Healthy, Items = [new() { RoadAddress = Address, BuildingManagementNo = "official-building", RoadNameManagementNo = "official-road", AdministrativeCode = "1126000000" }] });
        }
        public Task<PublicDataLookupResponse<ApartmentComplexItem>> SearchAsync(ApartmentComplexSearchRequest request, CancellationToken ct = default)
            => ApartmentFailure is not null ? Task.FromException<PublicDataLookupResponse<ApartmentComplexItem>>(ApartmentFailure)
                : Task.FromResult(new PublicDataLookupResponse<ApartmentComplexItem> { Success = true, Items = [new() { ComplexCode = "apt-official", ComplexName = "테스트단지", RoadAddress = Address }] });
        public Task<PublicDataLookupResponse<ApartmentComplexBasicItem>> GetBasicInfoAsync(ApartmentComplexBasicRequest request, CancellationToken ct = default)
            => Task.FromResult(new PublicDataLookupResponse<ApartmentComplexBasicItem> { Success = true, Items = [new() { ComplexCode = "apt-official", ComplexName = "테스트단지", RoadAddress = Address }] });
    }
    private sealed class DeletedPrivacyStore : I개인정보보존Store
    {
        public Task<bool> HasDeletionAsync(string s, string r, CancellationToken ct = default) => Task.FromResult(true);
        public Task<개인정보파기Job?> GetAsync(string s, CancellationToken ct = default) => throw new NotSupportedException();
        public Task CreateAsync(개인정보파기Job j, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> ReplaceAsync(개인정보파기Job j, long r, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<개인정보파기Job>> DueAsync(DateTime n, int l, CancellationToken ct = default, int m = 8) => throw new NotSupportedException();
        public Task SaveEvidenceAsync(string k, string s, string r, string j, DateTime e, CancellationToken ct = default) => throw new NotSupportedException();
        public Task MarkDeletionAsync(개인정보파기Job j, DateTime n, CancellationToken ct = default) => throw new NotSupportedException();
        public Task VerifyDeletionAsync(string k, DateTime n, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<int> DeleteExpiredEvidenceAsync(DateTime n, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
