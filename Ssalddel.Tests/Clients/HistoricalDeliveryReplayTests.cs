using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RoleWorkspacePreview;
using Ssalddel.Application.Admin.Food;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Application.Driver.Food;
using Ssalddel.Contracts.Admin.Food;
using Ssalddel.Contracts.Food;
using Xunit.Abstractions;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Dispatch.Engine;
using 살뜰.Services.Options;
using 살뜰.도메인.공통;
using 살뜰.도메인.음식;
using 살뜰.도메인.운송;

namespace Ssalddel.Tests.Clients;

/// <summary>개인 자료는 선택한 로컬 파일에서만 읽습니다. SQL 계정·주문 상태·시각은 독립 Simulation 자료입니다.</summary>
public sealed class HistoricalDeliveryReplayTests(ITestOutputHelper output)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private const decimal SyntheticRequiredOrderAmount = 12345m;

    [Theory]
    [InlineData("OperationalInput")]
    [InlineData("OtherDriver")]
    [InlineData("DuplicateOrder")]
    [InlineData("DuplicateBranch")]
    [InlineData("UnlinkedBranch")]
    [InlineData("NegativeFee")]
    public void 로컬자료경계와배달지점관계를위반하면정상자료로읽지않는다(string scenario)
    {
        var data = SyntheticDataset();
        data = scenario switch
        {
            "OperationalInput" => data with { LocalVerificationOnly = false },
            "OtherDriver" => data with { DriverId = "operational-driver" },
            "DuplicateOrder" => data with { Records = [.. data.Records, data.Records[0]] },
            "DuplicateBranch" => data with { Branches = [.. data.Branches, data.Branches[0]] },
            "UnlinkedBranch" => data with { Records = [data.Records[0] with { BranchId = "unlinked" }] },
            "NegativeFee" => data with { Records = [data.Records[0] with { DeliveryFeeKrw = -1m }] },
            _ => throw new ArgumentOutOfRangeException(nameof(scenario))
        };
        Assert.Throws<InvalidDataException>(data.Validate);
    }

    [Fact]
    public void 미확인주문금액과표시항목금액은단가0이나수량나눗셈으로바뀌지않는다()
    {
        var data = JsonSerializer.Deserialize<HistoricalDeliveryDataset>(
            JsonSerializer.Serialize(SyntheticDataset(), Json), Json)!;
        data.Validate();
        var unknown = data.Records[1];
        Assert.Null(unknown.FoodOrderAmountKrw);
        Assert.Null(unknown.ActualCompletedAtUtc);
        var item = Assert.Single(unknown.MenuItems);
        Assert.Equal(2, item.Quantity);
        Assert.Equal(14000m, item.DisplayedItemAmountKrw);
        Assert.Null(item.UnitPriceKrw);
        Assert.Equal("DisplayedLineAmount", item.AmountBasis);
        Assert.Equal(data.Records[0].PickupVisitId, unknown.PickupVisitId);
        Assert.NotEqual(data.Records[0].BranchId, unknown.BranchId);
    }

    [Fact]
    public void 같은픽업주소의다른상호는음식점별조회에서합쳐지지않고다른기사조회는차단한다()
    {
        var data = SyntheticDataset();
        var projection = new HistoricalDeliveryProjection(data);
        Assert.Equal(["replay-c", "replay-a"], projection.ForBranch("branch-a").Select(x => x.RecordId).ToArray());
        Assert.Equal("replay-b", Assert.Single(projection.ForBranch("branch-b")).RecordId);
        Assert.Equal(2, projection.Daily(data.CoverageStart).CompletedOrderCount);
        Assert.Throws<UnauthorizedAccessException>(() => projection.Daily(data.CoverageStart, "different-replay-driver"));
        var observation = projection.Settlement(data.Records[1]);
        Assert.Equal("HistoricalReplay", observation.PricingBreakdown.EvidenceScopeCode);
        Assert.Equal("MissingEvidence", observation.PricingBreakdown.EvidenceStatusCode);
        Assert.Null(observation.PricingBreakdown.DistanceKm);
        Assert.Null(observation.DeductionAmount);
        Assert.Null(observation.NetAmount);
        Assert.False(observation.IsActualTransferCompleted);
        Assert.Null(data.Records[1].FoodOrderAmountKrw);
    }

    [Fact]
    public async Task 선택한로컬자료전체는SQL기사일별조회와건수배달료가일치하고재가져오기가중복되지않는다()
    {
        var path = Environment.GetEnvironmentVariable("SSALDDEL_HISTORICAL_DELIVERY_DATASET");
        var usesLocalHistory = !string.IsNullOrWhiteSpace(path);
        var data = usesLocalHistory ? HistoricalDeliveryDataset.Read(path!) : SyntheticDataset();
        data.Validate();
        var projection = new HistoricalDeliveryProjection(data);
        var before = JsonSerializer.Serialize(data, Json);
        await using var fixture = await Fixture.CreateAsync(data);
        await fixture.ImportAsync(data);
        Assert.Equal(data.Records.Length, await fixture.Context.음식주문기사정산.CountAsync());
        Assert.Equal(data.Records.Length, await fixture.Context.음식배달시도.CountAsync());
        var dailyProof = new List<object>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var day in data.Records.GroupBy(x => x.ServiceDate).OrderBy(x => x.Key))
        {
            var actual = await fixture.UseCase().GetDailySettlementAsync(data.DriverId, day.Key, default);
            Assert.Equal(day.Count(), actual.CompletedOrderCount);
            Assert.Equal(day.Sum(x => x.DeliveryFeeKrw), actual.GrossAmountTotal);
            Assert.Equal(day.Sum(x => x.DeliveryFeeKrw), actual.KnownGrossAmountTotal);
            var projected = projection.Daily(day.Key);
            Assert.Equal(projected.CompletedOrderCount, actual.CompletedOrderCount);
            Assert.Equal(projected.GrossAmountTotal, actual.GrossAmountTotal);
            Assert.All(projected.OrderSettlements, x => Assert.Equal("HistoricalReplay", x.PricingBreakdown.EvidenceScopeCode));
            Assert.Equal(day.Select(x => SettlementId(x.RecordId)).Order(), actual.OrderSettlements.Select(x => x.SettlementId).Order());
            foreach (var row in actual.OrderSettlements)
            {
                Assert.True(seen.Add(row.SettlementId));
                var source = day.Single(x => SettlementId(x.RecordId) == row.SettlementId);
                Assert.Equal(data.Branches.Single(x => x.BranchId == source.BranchId).DisplayName, row.RestaurantName);
                Assert.Equal(SyntheticCompletedAt(source), row.CompletedAtUtc);
                Assert.Equal(day.Key, DateOnly.FromDateTime(row.CompletedAtUtc.AddHours(9)));
                Assert.Null(source.ActualCompletedAtUtc);
                AssertUnconfirmedFinancialEvidence(row);
            }
            var other = await fixture.UseCase().GetDailySettlementAsync("different-replay-driver", day.Key, default);
            Assert.Empty(other.OrderSettlements);
            Assert.Equal(0, other.CompletedOrderCount);
            Assert.Null(actual.DeductionAmountTotal);
            Assert.Null(actual.NetAmountTotal);
            Assert.Null(actual.ActualTransferAmountTotal);
            Assert.Equal(0, actual.ActualTransferCompletedOrderCount);
            dailyProof.Add(new
            {
                serviceDate = day.Key, count = actual.CompletedOrderCount, deliveryFeeKrw = actual.GrossAmountTotal,
                sourceAppDistanceKm = day.Sum(x => x.DistanceKm), sqlDistanceStatus = "NotAcceptedOfferEvidence"
            });
        }
        Assert.Equal(data.Records.Length, seen.Count);
        Assert.Equal(before, JsonSerializer.Serialize(data, Json));
        Assert.All(fixture.Context.ChangeTracker.Entries(), x => Assert.Equal(EntityState.Unchanged, x.State));
        var proof = new
        {
            dataSelection = usesLocalHistory ? "ExplicitLocalDataset" : "SyntheticDatasetOnly",
            localVerificationOnly = true,
            executionMode = "Simulation",
            sourceLedgerSha256 = data.SourceLedgerSha256,
            deliveryCount = seen.Count,
            branchCount = data.Branches.Length,
            pickupVisitCount = data.Records.Select(x => x.PickupVisitId).Distinct().Count(),
            actualCompletedTimesKnown = data.Records.Count(x => x.ActualCompletedAtUtc.HasValue),
            sqlCompletionTimestampBasis = "SyntheticTimestamp: service date KST12:00 + sequence seconds",
            requiredSqlOrderAmount = "Synthetic 12,345 KRW for required FK rows only; not historical menu/order-price verification",
            historicalFeeEvidenceScope = "HistoricalReplay; SQL accepted-offer evidence remains missing",
            actualTransferVerified = false,
            daily = dailyProof
        };
        var report = JsonSerializer.Serialize(proof, new JsonSerializerOptions(Json) { WriteIndented = true });
        output.WriteLine(report);
        if (usesLocalHistory)
            await File.WriteAllTextAsync(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path!))!,
                "server-usecase-verification.json"), report);
    }

    [Fact]
    public async Task 과거날짜는오늘로옮기지않고3일만료뒤정산만제공하며다른기사상세는차단한다()
    {
        var data = SyntheticDataset();
        await using var fixture = await Fixture.CreateAsync(data);
        var source = data.Records[0];
        var result = await fixture.UseCase().GetCompletedDeliveryDetailAsync(data.DriverId,
            SettlementId(source.RecordId), default);
        Assert.NotNull(result);
        Assert.Equal("Expired", result.DetailAccessStatusCode);
        Assert.Equal(SyntheticCompletedAt(source).AddDays(3), result.DetailExpiresAtUtc);
        Assert.Equal(source.DeliveryFeeKrw, result.Settlement.GrossAmount);
        Assert.Null(result.OrderDetails);
        Assert.Null(result.CustomerDetails);
        AssertUnconfirmedFinancialEvidence(result.Settlement);
        Assert.Null(await fixture.UseCase().GetCompletedDeliveryDetailAsync("different-replay-driver",
            SettlementId(source.RecordId), default));
        Assert.Null(await fixture.UseCase().GetCompletedDeliveryDetailAsync(data.DriverId,
            "missing-replay-settlement", default));
        var order = await fixture.Context.음식주문.AsNoTracking().SingleAsync(x => x.주문번호 == OrderNo(source.RecordId));
        Assert.Equal(SyntheticRequiredOrderAmount, order.총주문금액);
        Assert.True(order.모의처리처수행);
        Assert.Equal(source.ServiceDate, DateOnly.FromDateTime(order.UpdatedAt.AddHours(9)));
        Assert.Empty(order.수령인명);
        Assert.Empty(order.수령인연락처);
        Assert.Empty(order.수령지주소);
        Assert.Null(data.Records[1].FoodOrderAmountKrw);
    }

    [Fact]
    public async Task 로컬이력용최소주문은명시모의수행이라관리자모의지급검증대상도되지않는다()
    {
        var data = SyntheticDataset();
        await using var fixture = await Fixture.CreateAsync(data);
        var source = data.Records[0];
        var useCase = new 음식주문기사정산UseCase(fixture.Context,
            new CurrentUser("replay-admin", 역할명.서버관리자), new Execution(), fixture.Clock);
        var result = await useCase.모의지급검증Async(OrderNo(source.RecordId), new FoodDeliverySimulatedPayoutRequest
        {
            IdempotencyKey = "replay-never-payout", ExpectedSettlementRevision = 1,
            ConfirmedGrossAmount = source.DeliveryFeeKrw, ConfirmedDeductionAmount = 0m,
            DeductionEvidenceReference = "synthetic-test-only", OutcomeCode = "Succeeded"
        }, default);
        Assert.True(result.IsFailed);
        Assert.Contains(result.Errors, x => x.Message.Contains("모의 수행은 기사 지급·정산 대상이 아닙니다", StringComparison.Ordinal));
        Assert.Empty(await fixture.Context.음식주문기사지급검증.ToListAsync());
        var daily = await fixture.UseCase().GetDailySettlementAsync(data.DriverId, source.ServiceDate, default);
        Assert.All(daily.OrderSettlements, AssertUnconfirmedFinancialEvidence);
    }

    private static void AssertUnconfirmedFinancialEvidence(FoodDeliveryOrderSettlementDto row)
    {
        Assert.Equal("Simulation", row.ExecutionModeCode);
        Assert.Equal("Simulation", row.ServerExecutionModeCode);
        Assert.Equal("MissingEvidence", row.PricingBreakdown.EvidenceStatusCode);
        Assert.Null(row.PricingBreakdown.DistanceKm);
        Assert.Null(row.PricingBreakdown.PickupAmount);
        Assert.Null(row.PricingBreakdown.DropoffAmount);
        Assert.Null(row.DeductionAmount);
        Assert.Null(row.NetAmount);
        Assert.Null(row.ReceiptConfirmedAtUtc);
        Assert.Equal("NotRequested", row.PayoutStatusCode);
        Assert.Empty(row.SimulationPaymentId);
        Assert.Null(row.SimulationVerifiedAtUtc);
        Assert.Empty(row.SimulationPayments);
        Assert.False(row.IsActualTransferCompleted);
    }

    private static HistoricalDeliveryDataset SyntheticDataset()
    {
        var day = new DateOnly(2026, 9, 1);
        return new()
        {
            LocalVerificationOnly = true, CoverageStart = day, CoverageEnd = day.AddDays(1),
            Branches =
            [
                new() { BranchId = "branch-a", DisplayName = "가상 음식점 A", PublicPickupAddress = "가상 공개 주소" },
                new() { BranchId = "branch-b", DisplayName = "가상 음식점 B", PublicPickupAddress = "가상 공개 주소" }
            ],
            Records =
            [
                new() { RecordId = "replay-a", ServiceDate = day, Sequence = 1, BranchId = "branch-a",
                    PickupVisitId = "shared-pickup", DeliveryFeeKrw = 4000m, DistanceKm = 1.2m,
                    FoodOrderAmountKrw = 18000m,
                    MenuItems = [new() { Name = "가상 메뉴 A", Quantity = 2, UnitPriceKrw = 9000m, AmountBasis = "ConfirmedUnitPrice" }] },
                new() { RecordId = "replay-b", ServiceDate = day, Sequence = 2, BranchId = "branch-b",
                    PickupVisitId = "shared-pickup", DeliveryFeeKrw = 2100m, DistanceKm = 0m,
                    MenuItems = [new() { Name = "가상 메뉴 B", Quantity = 2, DisplayedItemAmountKrw = 14000m,
                        AmountBasis = "DisplayedLineAmount" }] },
                new() { RecordId = "replay-c", ServiceDate = day.AddDays(1), Sequence = 1, BranchId = "branch-a",
                    PickupVisitId = "other-pickup", DeliveryFeeKrw = 3200m, DistanceKm = 2.345m }
            ]
        };
    }

    private static string SettlementId(string id) => "settlement:" + id;
    private static string OrderNo(string id) => "order:" + id;
    private static DateTime SyntheticCompletedAt(HistoricalDeliveryRecord source) => DateTime.SpecifyKind(
        source.ServiceDate.ToDateTime(new TimeOnly(12, 0)).AddHours(-9).AddSeconds(source.Sequence), DateTimeKind.Utc);

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        public SsalddelContext Context { get; }
        public TimeProvider Clock { get; }
        private Fixture(SqliteConnection connection, HistoricalDeliveryDataset data)
        {
            this.connection = connection;
            Context = new(new DbContextOptionsBuilder<SsalddelContext>().UseSqlite(connection).Options, new Encryption());
            Clock = new FixedClock(DateTime.SpecifyKind(data.CoverageEnd.ToDateTime(new TimeOnly(12, 0)).AddDays(4), DateTimeKind.Utc));
        }
        public static async Task<Fixture> CreateAsync(HistoricalDeliveryDataset data)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var fixture = new Fixture(connection, data);
            await fixture.Context.Database.EnsureCreatedAsync();
            await fixture.ImportAsync(data);
            return fixture;
        }

        public async Task ImportAsync(HistoricalDeliveryDataset data)
        {
            var existing = (await Context.음식주문기사정산.Select(x => x.정산StableId).ToArrayAsync()).ToHashSet(StringComparer.Ordinal);
            var rows = data.Records.Where(x => !existing.Contains(SettlementId(x.RecordId))).Select(source =>
            {
                var now = SyntheticCompletedAt(source);
                var branch = data.Branches.Single(x => x.BranchId == source.BranchId);
                // Non-nullable product order totals cannot represent unknown historical amounts.
                // This FK scaffold is explicitly synthetic, never copied back to the source dataset or returned as historical order detail.
                var order = new 음식주문
                {
                    주문번호 = OrderNo(source.RecordId), 주문자UserId = "replay-orderer", 음식점명 = branch.DisplayName,
                    음식점주소 = branch.PublicPickupAddress ?? "", 상태 = 음식주문상태코드.전달완료,
                    모의처리처수행 = true, 총주문금액 = SyntheticRequiredOrderAmount, UpdatedAt = now
                };
                var transport = new 운송원장
                {
                    의뢰Id = "offer:" + source.RecordId, 운송번호 = "transport:" + source.RecordId,
                    원본의뢰유형 = 운송의뢰배차원천유형.음식점주문, 원본의뢰Id = order.주문번호,
                    기사_운송자 = data.DriverId, 확정기사Id = data.DriverId,
                    배차업무유형 = 상태값.배차업무유형.음식배달, 배차큐단계 = 상태값.배차큐단계.종료
                };
                var attempt = new 음식배달시도
                {
                    시도StableId = "attempt:" + source.RecordId, 주문번호 = order.주문번호,
                    제안Id = transport.의뢰Id, 기사Id = data.DriverId, 시도순번 = 1,
                    상태Code = 음식배달시도상태Code.전달완료, 경험실행모드Code = "Simulation",
                    수락시각Utc = now.AddMinutes(-20), 전달완료시각Utc = now
                };
                Context.음식주문.Add(order); Context.운송원장.Add(transport); Context.음식배달시도.Add(attempt);
                return (source, order, transport, attempt);
            }).ToArray();
            await Context.SaveChangesAsync();
            foreach (var (source, order, transport, attempt) in rows)
                Context.음식주문기사정산.Add(new()
                {
                    정산StableId = SettlementId(source.RecordId), 음식주문Id = order.Id, 주문번호 = order.주문번호,
                    음식점명 = order.음식점명, 운송Id = transport.Id,
                    배달시도Id = attempt.Id, 배달시도 = attempt, 배달시도StableId = attempt.시도StableId,
                    기사Id = data.DriverId, 세전대금 = source.DeliveryFeeKrw,
                    요금정책판본 = "historical-replay.synthetic.r1", 요금계산근거Json = "",
                    실행모드Code = "Simulation", 정산상태Code = 음식주문기사정산상태Code.수령확인대기,
                    보류사유 = "과거 관측 배달료만 재사용한 독립 로컬 검증; 원본 수령·공제·수락 요금 근거 없음",
                    전달완료시각Utc = SyntheticCompletedAt(source), CreatedAtUtc = SyntheticCompletedAt(source),
                    UpdatedAtUtc = SyntheticCompletedAt(source)
                });
            await Context.SaveChangesAsync();
        }

        public FoodDeliveryDriverWorkspaceUseCase UseCase() => new(Context, null!, null!, new Execution(), null!, Clock,
            Options.Create(new FoodDeliveryCompletedDetailAccessOptions { WindowMinutes = 4320 }));
        public async ValueTask DisposeAsync() { await Context.DisposeAsync(); await connection.DisposeAsync(); }
    }

    private sealed class Encryption : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
    private sealed class FixedClock(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(now);
    }
    private sealed record CurrentUser(string? UserId, string? Role) : ICurrentUserAccessor;
    private sealed class Execution : ISsalddelExecutionModePolicy
    {
        public SsalddelExecutionMode Mode => SsalddelExecutionMode.Simulation;
        public bool IsSimulation => true;
        public bool IsOperational => false;
    }
}
