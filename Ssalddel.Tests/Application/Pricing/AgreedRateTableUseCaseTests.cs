using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Ssalddel.Application.Food;
using Ssalddel.Application.Driver.Settlement;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Application.Pricing;
using Ssalddel.Application.Shipper.Request;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.Finance;
using Ssalddel.Contracts.Common.Fulfillment;
using Ssalddel.Contracts.Common.Pricing;
using Ssalddel.Domain.Pricing;
using Ssalddel.Services.Community;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Dispatch.Engine;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.Services.Options;
using 살뜰.Services.Storage.Local;
using 살뜰.Services.Weather;
using 살뜰.도메인.공통;
using 살뜰.도메인.기사;
using 살뜰.도메인.운송;
using 살뜰.도메인.음식;
using 살뜰.도메인.화주;

namespace Ssalddel.Tests.Application.Pricing;

public sealed class AgreedRateTableUseCaseTests
{
    private static readonly AgreedRatePartyDto Restaurant = new(AgreedRatePartyKinds.Restaurant, "41");
    private static readonly AgreedRatePartyDto FoodDriver = new(AgreedRatePartyKinds.FoodDriver, "food-driver");
    private static readonly AgreedRatePartyDto Shipper = new(AgreedRatePartyKinds.Shipper, "shipper");
    private static readonly AgreedRatePartyDto CargoDriver = new(AgreedRatePartyKinds.CargoDriver, "cargo-driver");

    [Fact]
    public async Task 첫화주도등록계정으로사전합의를제안하고기사는같은당사자판본을독립조회한다()
    {
        await using var f = await Fixture.Create();
        var first = new AgreedRatePartyDto(AgreedRatePartyKinds.Shipper, "first-shipper");
        f.Db.Users.Add(new() { Id = first.PartyId, UserName = "synthetic-first-shipper" });
        await f.Db.SaveChangesAsync();
        var table = await f.Use().ProposeAsync(first, new()
        {
            ActorPartyKind = first.PartyKind, CounterpartyPartyKind = CargoDriver.PartyKind, CounterpartyPartyId = CargoDriver.PartyId,
            BusinessKind = AgreedRateBusinessKinds.CargoTransport, VehicleCode = "다마스", RequestId = Guid.NewGuid(),
            EffectiveFromUtc = f.Clock.Now.AddHours(-1), EffectiveUntilUtc = f.Clock.Now.AddHours(2), CargoPricing = new(10000m, 200m, 10000m)
        }, default);
        Assert.False(await f.Db.화주운송의뢰.AnyAsync(x => x.화주Id == first.PartyId));
        await using var other = f.Context();
        var read = await new AgreedRateTableUseCase(other, f.Clock).GetAsync(table.AgreementId, CargoDriver, default);
        Assert.Equal(first, read.Proposer); Assert.True(read.CanAccept);
    }

    [Fact]
    public async Task 재진입복구에쓰는업무번호는정규원문이어야하고음식점식별자앞자리0은다른당사자로생성하지않는다()
    {
        await using var f = await Fixture.Create(); var accepted = await f.AcceptFood(); var u = f.Use();
        await Error("RateTableWorkRequired", () => u.SelectAsync(accepted.AgreementId, Restaurant, Selection(Restaurant, " food-1 "), default));
        await Error("RateTablePartyUnavailable", () => u.ProposeAsync(FoodDriver, f.FoodProposal() with
        { ActorPartyKind = FoodDriver.PartyKind, CounterpartyPartyKind = Restaurant.PartyKind, CounterpartyPartyId = "041" }, default));
        Assert.Empty(await f.Db.Set<AgreedRateTableSelectionRecord>().ToArrayAsync());
    }

    [Fact]
    public async Task 선택근거당사자가변조되면가격해시가같아도재조회와후보및새산정을차단한다()
    {
        await using var f = await Fixture.Create(); var accepted = await f.AcceptFood(); var u = f.Use();
        var selected = await u.SelectAsync(accepted.AgreementId, Restaurant, Selection(Restaurant, "food-1"), default);
        var row = await f.Db.Set<AgreedRateTableSelectionRecord>().SingleAsync();
        row.SnapshotJson = JsonSerializer.Serialize(selected.Agreement with
        { Counterparty = new(AgreedRatePartyKinds.FoodDriver, "other-driver") }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        await f.Db.SaveChangesAsync();
        await Error("RateTableSelectionEvidenceInvalid", () => u.GetSelectionAsync(accepted.AgreementId, "food-1", FoodDriver, default));
        await Error("RateTableSelectionEvidenceInvalid", () => u.SelectedDriverAsync(AgreedRateBusinessKinds.FoodDelivery, "food-1", default));
        await Error("RateTableSelectionEvidenceInvalid", () => u.SelectedAsync(AgreedRateBusinessKinds.FoodDelivery, "food-1", FoodDriver.PartyId, "Motorcycle", default));
    }

    [Fact]
    public async Task 양당사자는독립재조회로동일합의판본을확인하고다른당사자는접근하지못한다()
    {
        await using var f = await Fixture.Create();
        var proposed = await f.Use().ProposeAsync(Restaurant, f.FoodProposal(), default);
        await using var driverDb = f.Context();
        var driver = new AgreedRateTableUseCase(driverDb, f.Clock);
        var received = await driver.GetAsync(proposed.AgreementId, FoodDriver, default);
        Assert.True(received.CanAccept); Assert.False(proposed.CanAccept); Assert.False(received.CanPreview);
        var accepted = await driver.ActAsync(received.AgreementId, FoodDriver, Action(FoodDriver, "Accept", 1), default);
        await using var ownerDb = f.Context();
        var owner = new AgreedRateTableUseCase(ownerDb, f.Clock);
        var reloaded = await owner.GetAsync(accepted.AgreementId, Restaurant, default);
        Assert.Equal("Accepted", reloaded.StatusCode); Assert.Equal(2, reloaded.Revision);
        Assert.Equal(accepted.FoodPricing, reloaded.FoodPricing);
        Assert.True(reloaded.CanSelect); Assert.True(reloaded.CanPreview);
        Assert.False(accepted.CanSelect); Assert.True(accepted.CanPreview);
        Assert.Equal(2, reloaded.History.Count);
        Assert.Single(await driver.ListAsync(FoodDriver, default));
        var third = new AgreedRatePartyDto(AgreedRatePartyKinds.FoodDriver, "other-driver");
        Assert.Empty(await owner.ListAsync(third, default));
        await Error("RateTableNotFound", () => owner.GetAsync(accepted.AgreementId, third, default));
    }

    [Fact]
    public async Task 기사도제안할수있고상대만수락거절하며일방적자기수락은차단한다()
    {
        await using var f = await Fixture.Create();
        var u = f.Use();
        var p = f.FoodProposal() with { ActorPartyKind = FoodDriver.PartyKind, CounterpartyPartyKind = Restaurant.PartyKind,
            CounterpartyPartyId = Restaurant.PartyId };
        var table = await u.ProposeAsync(FoodDriver, p, default);
        await Error("RateTableCounterpartyRequired", () => u.ActAsync(table.AgreementId, FoodDriver, Action(FoodDriver, "Accept", 1), default));
        var rejected = await u.ActAsync(table.AgreementId, Restaurant, Action(Restaurant, "Reject", 1), default);
        Assert.Equal("Rejected", rejected.StatusCode); Assert.False(rejected.CanSelect);
        await Error("RateTableStateConflict", () => u.ActAsync(table.AgreementId, Restaurant, Action(Restaurant, "Accept", 2), default));
    }

    [Fact]
    public async Task 응답유실뒤동일요청은같은결과를반환하고입력이바뀌면409로차단한다()
    {
        await using var f = await Fixture.Create();
        var p = f.FoodProposal(); var u = f.Use();
        var first = await u.ProposeAsync(Restaurant, p, default);
        await using var retryDb = f.Context(); var retry = new AgreedRateTableUseCase(retryDb, f.Clock);
        Assert.Equal(first.AgreementId, (await retry.ProposeAsync(Restaurant, p, default)).AgreementId);
        await Error("RateTableRequestContentConflict", () => retry.ProposeAsync(Restaurant, p with { VehicleCode = "Car" }, default));
        var action = Action(FoodDriver, "Accept", 1);
        var accepted = await u.ActAsync(first.AgreementId, FoodDriver, action, default);
        await u.ActAsync(first.AgreementId, Restaurant, Action(Restaurant, "Withdraw", 2), default);
        var original = await retry.ActAsync(first.AgreementId, FoodDriver, action, default);
        Assert.Equal("Accepted", original.StatusCode); Assert.Equal(accepted.Revision, original.Revision);
        Assert.Equal("Withdrawn", (await retry.GetAsync(first.AgreementId, FoodDriver, default)).StatusCode);
        Assert.Equal(1, await f.Db.Set<AgreedRateTableRecord>().CountAsync());
        Assert.Equal(3, await f.Db.Set<AgreedRateTableCommandReceipt>().CountAsync());
    }

    [Fact]
    public async Task 두DB컨텍스트가같은판본을읽어도CAS는한상태전환만허용한다()
    {
        await using var f = await Fixture.Create();
        var table = await f.Use().ProposeAsync(Restaurant, f.FoodProposal(), default);
        await using var one = f.Context(); await using var two = f.Context();
        _ = await one.Set<AgreedRateTableRecord>().SingleAsync();
        _ = await two.Set<AgreedRateTableRecord>().SingleAsync();
        await new AgreedRateTableUseCase(one, f.Clock).ActAsync(table.AgreementId, Restaurant, Action(Restaurant, "Withdraw", 1), default);
        await Error("RateTableRevisionConflict", () => new AgreedRateTableUseCase(two, f.Clock)
            .ActAsync(table.AgreementId, FoodDriver, Action(FoodDriver, "Accept", 1), default));
        await using var check = f.Context();
        Assert.Equal("Withdrawn", (await check.Set<AgreedRateTableRecord>().SingleAsync()).StatusCode);
        Assert.Equal(2, await check.Set<AgreedRateTableCommandReceipt>().CountAsync());
        Assert.True(check.Model.FindEntityType(typeof(AgreedRateTableRecord))!.FindProperty("Revision")!.IsConcurrencyToken);
        Assert.Equal(3, check.Model.FindEntityType(typeof(AgreedRateTableCommandReceipt))!.FindPrimaryKey()!.Properties.Count);
        Assert.Equal(2, check.Model.FindEntityType(typeof(AgreedRateTableSelectionRecord))!.FindPrimaryKey()!.Properties.Count);
    }

    [Fact]
    public async Task 유효기간과업종항목판본은명시검증하고미수락합의를계산하지않는다()
    {
        await using var f = await Fixture.Create(); var u = f.Use(); var p = f.FoodProposal();
        await Error("RateTableBusinessPartyMismatch", () => u.ProposeAsync(Restaurant, p with { BusinessKind = AgreedRateBusinessKinds.CargoTransport }, default));
        await Error("RateTableFoodTermsRequired", () => u.ProposeAsync(Restaurant, p with { CargoPricing = new(100, 10, 100) }, default));
        await Error("RateTableUtcWindowRequired", () => u.ProposeAsync(Restaurant, p with { EffectiveFromUtc = DateTime.SpecifyKind(p.EffectiveFromUtc, DateTimeKind.Unspecified) }, default));
        var table = await u.ProposeAsync(Restaurant, p with { EffectiveFromUtc = f.Clock.Now.AddMinutes(10) }, default);
        await Error("RateTableNotAccepted", () => u.PreviewAsync(table.AgreementId, Restaurant, Preview(Restaurant, 1), default));
        await u.ActAsync(table.AgreementId, FoodDriver, Action(FoodDriver, "Accept", 1), default);
        await Error("RateTableNotYetEffective", () => u.PreviewAsync(table.AgreementId, FoodDriver, Preview(FoodDriver, 2), default));
        f.Clock.Now = p.EffectiveUntilUtc;
        await Error("RateTableExpired", () => u.PreviewAsync(table.AgreementId, Restaurant, Preview(Restaurant, 2), default));
        Assert.Equal("Expired", (await u.GetAsync(table.AgreementId, Restaurant, default)).EffectiveStatusCode);
    }

    [Fact]
    public async Task 음식미리보기는기존거리올림과r26분담을사용하고화물항목과사용자상한을검증한다()
    {
        await using var f = await Fixture.Create(); var accepted = await f.AcceptFood(); var u = f.Use();
        var result = await u.PreviewAsync(accepted.AgreementId, FoodDriver, Preview(FoodDriver, 2), default);
        Assert.Equal(1500m, result.TotalAmountKrw); Assert.Equal(500m, result.FoodBreakdown!.PickupFeeKrw);
        Assert.Equal(300m, result.FoodBreakdown.DistanceFeeKrw);
        await Error("RateTableFoodCargoTermsMismatch", () => u.PreviewAsync(accepted.AgreementId, Restaurant,
            Preview(Restaurant, 2) with { WaitingFeeKrw = 1 }, default));
        var sharing = Preview(Restaurant, 2) with
        {
            CostSharingPolicy = new() { Mode = DeliveryCostSharingModes.FixedContribution, FixedAmountKrw = 600m }, MaxUserChargeKrw = 900m
        };
        var shared = await u.PreviewAsync(accepted.AgreementId, Restaurant, sharing, default);
        Assert.Equal(600m, shared.RestaurantContributionKrw); Assert.Equal(900m, shared.UserChargeKrw);
        await Error("RateTableUserChargeLimitExceeded", () => u.PreviewAsync(accepted.AgreementId, Restaurant, sharing with { MaxUserChargeKrw = 899m }, default));
        Assert.Empty(await f.Db.Set<AgreedRateTableSelectionRecord>().ToArrayAsync());
    }

    [Fact]
    public async Task 음식업무선택은실제소유주문계획원장과기사차량을확인하고default계산은보존한다()
    {
        await using var f = await Fixture.Create(); var accepted = await f.AcceptFood(); var u = f.Use();
        await Error("RateTableWorkOwnerRequired", () => u.SelectAsync(accepted.AgreementId, FoodDriver, Selection(FoodDriver, "food-1"), default));
        await Error("RateTableWorkNotFound", () => u.SelectAsync(accepted.AgreementId, Restaurant, Selection(Restaurant, "other-owner-order"), default));
        var selected = await u.SelectAsync(accepted.AgreementId, Restaurant, Selection(Restaurant, "food-1"), default);
        Assert.Equal("SelectedForNextOffer", selected.StatusCode); Assert.Null(selected.AgreedAmountKrw);
        Assert.Equal("food-driver", await u.SelectedDriverAsync(AgreedRateBusinessKinds.FoodDelivery, "food-1", default));
        await using var independent = f.Context(); var other = new AgreedRateTableUseCase(independent, f.Clock);
        Assert.Equal(selected.Agreement, (await other.GetSelectionAsync(accepted.AgreementId, "food-1", FoodDriver, default)).Agreement);
        await Error("RateTableSelectedDriverMismatch", () => u.SelectedAsync(AgreedRateBusinessKinds.FoodDelivery, "food-1", "other-driver", "Motorcycle", default));
        await Error("RateTableVehicleMismatch", () => u.SelectedAsync(AgreedRateBusinessKinds.FoodDelivery, "food-1", "food-driver", "Car", default));
        var pricing = f.Pricing(u); var queue = await f.Db.운송원장.SingleAsync(x => x.원본의뢰Id == "food-1");
        var agreed = await pricing.산정Async(queue, "food-driver");
        Assert.Equal(1500m, agreed.요금.기사지급예정액); Assert.Equal(selected.Agreement, agreed.합의요금표);
        Assert.Contains($"|agreement:{accepted.AgreementId}:2:{selected.Agreement.TermsDigest}", agreed.정책판본);
        var ordinary = await pricing.산정Async(new 운송원장 { 의뢰Id = "ordinary", 원본의뢰Id = "ordinary", 배차업무유형 = 상태값.배차업무유형.음식배달,
            픽업_위도 = 37m, 픽업_경도 = 127m, 하차_위도 = 37.01m, 하차_경도 = 127.01m }, "food-driver");
        Assert.Equal(2770m, ordinary.요금.기사지급예정액); Assert.Null(ordinary.합의요금표);
    }

    [Theory]
    [InlineData("Offer")]
    [InlineData("Assigned")]
    [InlineData("Cancelled")]
    public async Task 이미제안확정취소된음식업무에선택해과거금액을바꾸지않는다(string state)
    {
        await using var f = await Fixture.Create(); var accepted = await f.AcceptFood();
        var q = await f.Db.운송원장.SingleAsync(x => x.원본의뢰Id == "food-1");
        if (state == "Offer") q.기사제안요금계산근거Json = "frozen-original";
        if (state == "Assigned") q.확정기사Id = "food-driver";
        if (state == "Cancelled") (await f.Db.음식주문.SingleAsync()).상태 = "주문취소";
        q.기사지급예정액 = 1234m; await f.Db.SaveChangesAsync();
        await Error("RateTableWorkAlreadyFrozen", () => f.Use().SelectAsync(accepted.AgreementId, Restaurant, Selection(Restaurant, "food-1"), default));
        Assert.Empty(await f.Db.Set<AgreedRateTableSelectionRecord>().ToArrayAsync());
        Assert.Equal(1234m, q.기사지급예정액);
    }

    [Fact]
    public async Task 철회뒤새산정은차단하고과거음식정산은당시합의판본과금액을반환한다()
    {
        await using var f = await Fixture.Create(); var accepted = await f.AcceptFood(); var u = f.Use();
        await u.SelectAsync(accepted.AgreementId, Restaurant, Selection(Restaurant, "food-1"), default);
        var q = await f.Db.운송원장.SingleAsync(x => x.원본의뢰Id == "food-1");
        var decision = await f.Pricing(u).산정Async(q, "food-driver");
        var settlement = new 음식주문기사정산 { 기사Id = "food-driver", 세전대금 = decision.요금.기사지급예정액,
            요금정책판본 = decision.정책판본, 요금계산근거Json = JsonSerializer.Serialize(decision) };
        await u.ActAsync(accepted.AgreementId, FoodDriver, Action(FoodDriver, "Withdraw", 2), default);
        await Error("RateTableRevisionConflict", () => f.Pricing(u).산정Async(q, "food-driver"));
        var frozen = 음식주문기사정산Recorder.ToDto(settlement);
        Assert.Equal(1500m, frozen.GrossAmount); Assert.Equal("VerifiedComponents", frozen.PricingBreakdown.EvidenceStatusCode);
        Assert.Equal(decision.합의요금표, frozen.PricingBreakdown.AgreedRateTable);
        Assert.Equal(500m, frozen.PricingBreakdown.PickupAmount);
        var tampered = JsonNode.Parse(settlement.요금계산근거Json)!;
        tampered["합의요금표"]!["TermsDigest"] = new string('0', 64);
        settlement.요금계산근거Json = tampered.ToJsonString();
        var invalid = 음식주문기사정산Recorder.ToDto(settlement);
        Assert.Equal(1500m, invalid.GrossAmount); Assert.Equal("InvalidEvidence", invalid.PricingBreakdown.EvidenceStatusCode);
        Assert.Null(invalid.PricingBreakdown.AgreedRateTable);
    }

    [Fact]
    public async Task 화물선택은기준견적결제액을보존하며수행대가와동일판본을원장에저장한다()
    {
        await using var f = await Fixture.Create(); var accepted = await f.AcceptCargo(); var sync = new Sync(f.Db);
        var u = f.Use(sync); var request = Selection(Shipper, "cargo-1");
        var selected = await u.SelectAsync(accepted.AgreementId, Shipper, request, default);
        Assert.Equal("Applied", selected.StatusCode); Assert.Equal(13100m, selected.AgreedAmountKrw);
        var work = await f.Db.화주운송의뢰.AsNoTracking().SingleAsync();
        var fare = await f.Db.운임구성.AsNoTracking().SingleAsync();
        Assert.Equal(15000m, work.최종운임); Assert.Equal(15000, work.결제예정금액);
        Assert.Equal(15000m, fare.최종운임); Assert.Equal("baseline-fixture", fare.단가출처);
        Assert.Equal(13100m, fare.기사지급예정운임); Assert.Equal(상태값.결제상태.결제대기, work.결제상태);
        Assert.Equal("결제대기", work.정산상태);
        Assert.NotNull(sync.Saved);
        var settlementBlock = sync.Saved!.블록목록.Single(x => x.Data.ContainsKey("사전합의요금표SnapshotJson"));
        Assert.Equal(fare.합의요금표SnapshotJson, settlementBlock.Data["사전합의요금표SnapshotJson"]);
        Assert.Equal(13100m, decimal.Parse(settlementBlock.Data["기사지급예정운임"], System.Globalization.CultureInfo.InvariantCulture));
        var mapped = 화주운송의뢰매퍼.To응답(work, fareComposition: fare);
        Assert.Equal(selected.Agreement, mapped.요금옵션!.AgreedRateTable);
        Assert.Equal("StoredAcceptedAgreement", mapped.요금옵션.AgreedRateTableEvidenceStatusCode);
        await using var independent = f.Context();
        var checkedSelection = await new AgreedRateTableUseCase(independent, f.Clock).GetSelectionAsync(accepted.AgreementId, "cargo-1", CargoDriver, default);
        Assert.Equal(selected, checkedSelection);
        Assert.Null(await AgreedCargoAcceptancePolicy.CheckAsync(independent, work, "cargo-driver", default));
        Assert.Equal("RateTableSelectedDriverMismatch", await AgreedCargoAcceptancePolicy.CheckAsync(independent, work, "other-driver", default));
        await u.ActAsync(accepted.AgreementId, Shipper, Action(Shipper, "Withdraw", 2), default);
        Assert.Equal(selected, await u.SelectAsync(accepted.AgreementId, Shipper, request, default));
        Assert.Equal(1, sync.Calls);
        Assert.Equal(13100m, (await f.Db.운임구성.AsNoTracking().SingleAsync()).기사지급예정운임);
        Assert.Null(await AgreedCargoAcceptancePolicy.CheckAsync(independent, work, "cargo-driver", default));
    }

    [Fact]
    public async Task 화물원장동기화미확인은지급완료로승격하지않고동일선택요청으로재대조한다()
    {
        await using var f = await Fixture.Create(); var accepted = await f.AcceptCargo(); var sync = new Sync(f.Db) { Available = false };
        var u = f.Use(sync); var request = Selection(Shipper, "cargo-1");
        var pending = await u.SelectAsync(accepted.AgreementId, Shipper, request, default);
        Assert.Equal("LedgerSyncPending", pending.StatusCode); Assert.Equal(13100m, pending.AgreedAmountKrw);
        Assert.True(pending.CanRetrySynchronization); Assert.Equal(request.RequestId, pending.SelectionRequestId);
        var work = await f.Db.화주운송의뢰.AsNoTracking().SingleAsync();
        Assert.Equal("RateTableLedgerSyncPending", await AgreedCargoAcceptancePolicy.CheckAsync(f.Db, work, "cargo-driver", default));
        await using var reloadDb = f.Context(); var reloadSync = new Sync(reloadDb);
        var reload = new AgreedRateTableUseCase(reloadDb, f.Clock, reloadSync);
        var saved = await reload.GetSelectionAsync(accepted.AgreementId, "cargo-1", Shipper, default);
        Assert.True(saved.CanRetrySynchronization);
        Assert.False((await reload.GetSelectionAsync(accepted.AgreementId, "cargo-1", CargoDriver, default)).CanRetrySynchronization);
        var done = await reload.SelectAsync(saved.Agreement.AgreementId, Shipper, new()
        { ActorPartyKind = Shipper.PartyKind, WorkId = saved.WorkId, ExpectedRevision = saved.Agreement.Revision, RequestId = saved.SelectionRequestId }, default);
        Assert.Equal("Applied", done.StatusCode); Assert.Equal(pending.Agreement, done.Agreement);
        Assert.False(done.CanRetrySynchronization);
        Assert.Equal(1, await f.Db.Set<AgreedRateTableSelectionRecord>().CountAsync());
        Assert.Equal(3, await f.Db.Set<AgreedRateTableCommandReceipt>().CountAsync());
        Assert.Equal(상태값.결제상태.결제대기, work.결제상태);
    }

    [Fact]
    public async Task 화물동결뒤일반견적수정은차단하고철회후기사정산조회도당시금액과판본을보존한다()
    {
        await using var f = await Fixture.Create(); var accepted = await f.AcceptCargo(); var u = f.Use(new Sync(f.Db));
        var selected = await u.SelectAsync(accepted.AgreementId, Shipper, Selection(Shipper, "cargo-1"), default);
        var current = new Current("shipper", "화주");
        var edit = new 의뢰수정CommandHandler(f.Db, new 화주운송업무담당자UseCase(f.Db, current, f.Clock));
        var editResult = await edit.Handle(new("cargo-1", new(null, null, null), new(null, null, null, null, null, null, null),
            new(null, null, null, null, null, null, null, null), new(null, null, null, null, null, null, null, null), new(null), null,
            new() { 기사지급예정운임 = 1m }), default);
        Assert.True(editResult.IsFailed);
        Assert.Contains(editResult.Errors, x => x.Metadata.TryGetValue("ErrorCode", out var code) && (string)code == "FreightAgreedPricingFrozen");
        await u.ActAsync(accepted.AgreementId, CargoDriver, Action(CargoDriver, "Withdraw", 2), default);
        f.Db.운송원장.Add(new() { 의뢰Id = "cargo-1", 운송번호 = "cargo-1", 원본의뢰Id = "cargo-1", 확정기사Id = "cargo-driver",
            기사_운송자 = "cargo-driver", 상태 = "인수완료", 도착 = f.Clock.Now.AddMinutes(30) });
        await f.Db.SaveChangesAsync();
        await using var queryDb = f.Context();
        var result = await new 기사지급준비UseCase(queryDb).월별조회Async("cargo-driver", 2026, 10);
        Assert.True(result.IsSuccess); var item = Assert.Single(result.Value.Items);
        Assert.Equal(13100m, item.ExpectedPayoutAmount); Assert.Equal(selected.Agreement, item.AgreedRateTable);
        Assert.Equal("StoredAcceptedAgreement", item.AgreedRateTableEvidenceStatusCode);
        Assert.False(item.IsReadyForPayoutPreparation); Assert.Equal("결제대기", item.ShipperPaymentStatus);
        Assert.Equal(0m, result.Value.ReadyForPayoutPreparationTotal);
        var fare = await queryDb.운임구성.SingleAsync(); fare.기사지급예정운임 = 999m; await queryDb.SaveChangesAsync();
        var invalid = await new 기사지급준비UseCase(queryDb).월별조회Async("cargo-driver", 2026, 10);
        var bad = Assert.Single(invalid.Value.Items);
        Assert.Equal("InvalidEvidence", bad.AgreedRateTableEvidenceStatusCode); Assert.Null(bad.AgreedRateTable);
        Assert.False(bad.IsReadyForPayoutPreparation); Assert.Equal("RateTableEvidenceInvalid", bad.ReadinessCode);
    }

    [Theory]
    [InlineData("Paid")]
    [InlineData("Approved")]
    [InlineData("Assigned")]
    [InlineData("Distance")]
    public async Task 화물의승인확정미확인거리에는합의선택을적용하지않는다(string state)
    {
        await using var f = await Fixture.Create(); var accepted = await f.AcceptCargo();
        var work = await f.Db.화주운송의뢰.SingleAsync(); var fare = await f.Db.운임구성.SingleAsync();
        if (state == "Paid") work.결제상태 = 상태값.결제상태.결제완료;
        if (state == "Approved") work.정산상태 = "후불승인완료";
        if (state == "Assigned") work.배차상태 = 상태값.배차상태.배차확정;
        if (state == "Distance") fare.예상거리Km = null;
        await f.Db.SaveChangesAsync();
        await Error(state == "Distance" ? "RateTableStoredDistanceRequired" : "RateTableWorkAlreadyFrozen",
            () => f.Use().SelectAsync(accepted.AgreementId, Shipper, Selection(Shipper, "cargo-1"), default));
        Assert.Empty(await f.Db.Set<AgreedRateTableSelectionRecord>().ToArrayAsync());
        Assert.Null((await f.Db.운임구성.AsNoTracking().SingleAsync()).합의요금표SnapshotJson);
        Assert.Equal(15000m, (await f.Db.운임구성.AsNoTracking().SingleAsync()).기사지급예정운임);
    }

    private static AgreedRateTableActionRequest Action(AgreedRatePartyDto p, string code, long rev)
        => new() { ActorPartyKind = p.PartyKind, ActionCode = code, ExpectedRevision = rev, RequestId = Guid.NewGuid() };
    private static AgreedRateTablePreviewRequest Preview(AgreedRatePartyDto p, long rev)
        => new() { ActorPartyKind = p.PartyKind, ExpectedRevision = rev, DistanceKm = 1.25m, DistanceBasisCode = "RouteEstimate" };
    private static AgreedRateTableSelectionRequest Selection(AgreedRatePartyDto p, string work)
        => new() { ActorPartyKind = p.PartyKind, ExpectedRevision = 2, WorkId = work, RequestId = Guid.NewGuid() };
    private static async Task Error(string code, Func<Task> call)
        => Assert.Equal(code, (await Assert.ThrowsAsync<AgreedRateTableException>(call)).Code);

    private sealed class Clock : TimeProvider
    {
        public DateTime Now { get; set; } = new(2026, 10, 8, 3, 30, 0, DateTimeKind.Utc);
        public override DateTimeOffset GetUtcNow() => new(Now);
    }
    private sealed class Fixture(SqliteConnection connection, SsalddelContext db) : IAsyncDisposable
    {
        public SsalddelContext Db { get; } = db;
        public Clock Clock { get; } = new();
        public SsalddelContext Context() => new(new DbContextOptionsBuilder<SsalddelContext>().UseSqlite(connection).Options, new Encryption());
        public AgreedRateTableUseCase Use(I운송원장Mongo동기화Service? sync = null) => new(Db, Clock, sync);
        public 음식배달기사제안요금Service Pricing(IAgreedRateTableUseCase agreed)
            => new(Db, new Route(), new Weather(), Clock, new SsalddelExecutionModePolicy(Options.Create(new SsalddelExecutionOptions
                { Mode = SsalddelExecutionMode.Simulation })), agreedRates: agreed);
        public AgreedRateTableProposalRequest FoodProposal() => new()
        {
            ActorPartyKind = Restaurant.PartyKind, CounterpartyPartyKind = FoodDriver.PartyKind, CounterpartyPartyId = FoodDriver.PartyId,
            BusinessKind = AgreedRateBusinessKinds.FoodDelivery, VehicleCode = "Motorcycle", RequestId = Guid.NewGuid(),
            EffectiveFromUtc = Clock.Now.AddHours(-1), EffectiveUntilUtc = Clock.Now.AddHours(2),
            FoodPricing = new("fixture-food-r1", new(2026, 10, 8), new(2026, 10, 8), 500m, 700m, 100, 100m, "Ceiling", 1000, 0)
        };
        public async Task<AgreedRateTableDto> AcceptFood()
        {
            var t = await Use().ProposeAsync(Restaurant, FoodProposal(), default);
            return await Use().ActAsync(t.AgreementId, FoodDriver, Action(FoodDriver, "Accept", 1), default);
        }
        public async Task<AgreedRateTableDto> AcceptCargo()
        {
            var t = await Use().ProposeAsync(Shipper, new()
            {
                ActorPartyKind = Shipper.PartyKind, CounterpartyPartyKind = CargoDriver.PartyKind, CounterpartyPartyId = CargoDriver.PartyId,
                BusinessKind = AgreedRateBusinessKinds.CargoTransport, VehicleCode = "다마스", RequestId = Guid.NewGuid(),
                EffectiveFromUtc = Clock.Now.AddHours(-1), EffectiveUntilUtc = Clock.Now.AddHours(2), CargoPricing = new(10000m, 200m, 10000m)
            }, default);
            return await Use().ActAsync(t.AgreementId, CargoDriver, Action(CargoDriver, "Accept", 1), default);
        }
        public static async Task<Fixture> Create()
        {
            var c = new SqliteConnection("Data Source=:memory:"); await c.OpenAsync();
            var db = new SsalddelContext(new DbContextOptionsBuilder<SsalddelContext>().UseSqlite(c).Options, new Encryption());
            await db.Database.EnsureCreatedAsync(); var f = new Fixture(c, db);
            db.음식점공개프로필.Add(new() { Id = 41, 상호명 = "합성 가게" });
            db.배달기사.Add(new() { 기사Id = FoodDriver.PartyId, 차량 = "오토바이" });
            db.용달기사.Add(new() { 기사Id = CargoDriver.PartyId });
            db.음식주문.Add(new() { 주문번호 = "food-1", 클라이언트요청Id = Guid.NewGuid(), 음식점Id = 41, 상태 = "조리중" });
            db.운송원장.Add(new() { 의뢰Id = "queue-food-1", 원본의뢰Id = "food-1", 배차업무유형 = 상태값.배차업무유형.음식배달,
                상태 = 상태값.배차대기상태.대기,
                픽업_위도 = 37m, 픽업_경도 = 127m, 하차_위도 = 37.01m, 하차_경도 = 127.01m,
                UpdatedAt = f.Clock.Now.AddMinutes(-10) });
            var fare = new 운임구성 { 의뢰Id = "cargo-1", 기본운임 = 13000m, 거리운임 = 2000m, 최소운임 = 15000m,
                최종운임 = 15000m, 기사지급예정운임 = 15000m, 예상거리Km = 15.5m, Km당단가 = 129.03m,
                거리계산방식 = "입력거리", 단가출처 = "baseline-fixture" };
            db.운임구성.Add(fare); await db.SaveChangesAsync();
            db.화주운송의뢰.Add(new() { 의뢰Id = "cargo-1", 화주Id = Shipper.PartyId, 주문자UserId = Shipper.PartyId,
                차량종류 = "다마스", 운임구성Id = fare.Id, 최종운임 = 15000m, 결제예정금액 = 15000, UpdatedAt = f.Clock.Now.AddMinutes(-10) });
            await db.SaveChangesAsync(); db.ChangeTracker.Clear(); return f;
        }
        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await connection.DisposeAsync(); }
    }
    private sealed class Encryption : IPersonalDataEncryptionService
    { public string? Protect(string? value) => value; public string? Unprotect(string? value) => value; }
    private sealed record Current(string? UserId, string? Role) : ICurrentUserAccessor;
    private sealed class Weather : I픽업지기상관측Client
    {
        public Task<픽업지기상관측결과> 조회Async(decimal? latitude, decimal? longitude, CancellationToken ct = default)
            => Task.FromResult(픽업지기상관측결과.사용불가("Synthetic"));
    }
    private sealed class Route : I배차추천경로Service
    {
        public Task<배차경로좌표?> ResolveOriginLocationAsync(string id, 용달기사? driver, DriverLocationSnapshot? location, 배차추천검색조건? criteria) => Task.FromResult<배차경로좌표?>(null);
        public Task<배차경로좌표?> ResolveRouteAnchorLocationAsync(string id, 용달기사? driver, DriverLocationSnapshot? location) => Task.FromResult<배차경로좌표?>(null);
        public Task<배차경로예상결과?> EstimateRouteAsync(배차경로좌표? origin, 배차경로좌표? destination)
            => Task.FromResult<배차경로예상결과?>(new(1.25m, TimeSpan.FromMinutes(5), 0, "Directions5", true));
        public Task<배차경로예상결과?> EstimateRouteAsync(배차경로좌표? origin, 배차경로좌표? destination, string option, CancellationToken ct)
            => Task.FromResult<배차경로예상결과?>(new(1.25m, TimeSpan.FromMinutes(5), 0, "Directions5", true) { RouteOption = option });
        public Task<배차경로예상결과?> EstimateOrderedRouteAsync(배차경로좌표? origin, IReadOnlyList<배차경로좌표> stops, CancellationToken ct = default) => Task.FromResult<배차경로예상결과?>(null);
        public Task<배차삽입경로예상결과?> EstimateInsertionDelayAsync(배차경로좌표? origin, 배차경로좌표? anchor, 배차경로좌표? pickup, 배차경로좌표? dropoff) => Task.FromResult<배차삽입경로예상결과?>(null);
        public decimal? CalculateDistanceKm(배차경로좌표 from, 배차경로좌표 to) => throw new InvalidOperationException();
    }
    private sealed class Sync(SsalddelContext db) : I운송원장Mongo동기화Service
    {
        public bool Available { get; set; } = true;
        public int Calls { get; private set; }
        public 커뮤니티원장저장요청? Saved { get; private set; }
        public async Task<커뮤니티원장Dto?> 화주운송의뢰동기화Async(화주운송의뢰 work, string by, CancellationToken ct = default)
        {
            Calls++; if (!Available) return null;
            var fare = await db.운임구성.AsNoTracking().SingleAsync(x => x.의뢰Id == work.의뢰Id, ct);
            Saved = 운송원장Mongo동기화Builder.저장요청생성(work, null, fare);
            return new() { 원장Id = Saved.원장Id!, Revision = 1, 블록목록 = Saved.블록목록 };
        }
        public Task<커뮤니티원장Dto?> 운송실행투영동기화Async(운송원장 work, string by, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<운송원장Mongo동기화상태> 상태조회Async(string id, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
