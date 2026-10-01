using Ssalddel.Simulation.Contracts;
using Ssalddel.Simulation.Domain;

namespace Ssalddel.Simulation.Tests;

[Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
    Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E3,
    "프랜차이즈 공급망 Preview·Confirm·멱등성·결정성·저장 재생 계약을 검증한다.",
    Boundary = "자동 시험이며 실제 공급 계약·발주·배송 또는 Unity 실행 증거가 아니다.",
    SubmoduleKey = Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceSubmoduleKeys.E3결정성검증)]
public sealed class 프랜차이즈공급망SimulationTests
{
    private const string EstimateSource = "source:menu-ingredient-estimate:r1";

    [Fact]
    public void Preview는_세션을바꾸지않고_Confirm은_멱등이다()
    {
        var aggregate = CreateSession();
        var request = Initialization(aggregate.Revision);

        var preview = aggregate.PreviewFranchiseSupplyInitialization(request);

        Assert.True(preview.CanConfirm);
        Assert.Null(aggregate.GetFranchiseSupplyState());
        Assert.Equal(0, aggregate.Revision);

        var command = Confirm("request:franchise:init", preview, request);
        var first = aggregate.ConfirmFranchiseSupplyInitialization(command);
        var repeated = aggregate.ConfirmFranchiseSupplyInitialization(command);

        Assert.Equal(1, aggregate.Revision);
        Assert.Equal(first.SourceSessionRevision,
            repeated.SourceSessionRevision);
        Assert.Equal(2, first.Stores.Length);
        Assert.Equal(2, first.Suppliers.Length);
        Assert.Single(first.Headquarters);
        Assert.False(first.IsOperationalState);

        command.ExpectedPreviewHash = new string('f', 64);
        var conflict = Assert.Throws<SimulationConflictException>(() =>
            aggregate.ConfirmFranchiseSupplyInitialization(command));
        Assert.Equal("FranchiseSupplyCommandPayloadConflict",
            conflict.ErrorCode);
    }

    [Fact]
    public void malformed_Confirm은_null참조대신_계약오류로거부한다()
    {
        var aggregate = CreateSession();
        var placeholder = new 프랜차이즈공급망PreviewSnapshot
        {
            PreviewHash = new string('a', 64),
        };

        var initialization = Initialization(aggregate.Revision);
        initialization.Headquarters = null!;
        Assert.Equal("FranchiseSupplyHeadquartersMissing",
            Assert.Throws<SimulationContractException>(() =>
                aggregate.ConfirmFranchiseSupplyInitialization(
                    Confirm("request:malformed:headquarters", placeholder,
                        initialization))).ErrorCode);

        var agreement = SourcingAgreementOne(aggregate.Revision);
        agreement.Items = [null!];
        Assert.Equal("FranchiseSupplyContractItemInvalid",
            Assert.Throws<SimulationContractException>(() =>
                aggregate.ConfirmFranchiseSourcingAgreement(
                    Confirm("request:malformed:contract-item", placeholder,
                        agreement))).ErrorCode);

        var offer = Offer(aggregate.Revision);
        offer.Items = null!;
        Assert.Equal("FranchiseSupplyOfferItemsMissing",
            Assert.Throws<SimulationContractException>(() =>
                aggregate.ConfirmFranchiseStoreSupplyOffer(
                    Confirm("request:malformed:offer-items", placeholder,
                        offer))).ErrorCode);

        var order = StoreOrder(aggregate.Revision, "order:malformed",
            "membership:store-one", "PO-MALFORMED", 1m, 1m, 1m);
        order.Lines = [null!];
        Assert.Equal("FranchiseSupplyOrderLineInvalid",
            Assert.Throws<SimulationContractException>(() =>
                aggregate.ConfirmFranchiseStoreOrder(
                    Confirm("request:malformed:order-line", placeholder,
                        order))).ErrorCode);

        initialization = Initialization(aggregate.Revision);
        initialization.SourceStableIds = [null!];
        Assert.Equal("FranchiseSupplySourceStableIdsInvalid",
            Assert.Throws<SimulationContractException>(() =>
                aggregate.ConfirmFranchiseSupplyInitialization(
                    Confirm("request:malformed:source", placeholder,
                        initialization))).ErrorCode);
        Assert.Equal(0, aggregate.Revision);
    }

    [Fact]
    public void staleExpectedRevision은_상태변경전에_거부한다()
    {
        var aggregate = CreateSession();
        Initialize(aggregate);
        var request = SourcingAgreementOne(0);

        var error = Assert.Throws<SimulationConflictException>(() =>
            aggregate.PreviewFranchiseSourcingAgreement(request));

        Assert.Equal("SimulationExpectedRevisionMismatch", error.ErrorCode);
        Assert.Empty(aggregate.GetFranchiseSupplyState()!.SourcingAgreements);
    }

    [Fact]
    public void 본부1_공급사2_매장2_재료3은_계약_공급안_발주로_연결된다()
    {
        var aggregate = CreateFullScenario();
        var state = aggregate.GetFranchiseSupplyState()!;

        Assert.Single(state.Headquarters);
        Assert.Equal(2, state.Suppliers.Length);
        Assert.Equal(2, state.Stores.Length);
        Assert.Equal(2, state.SourcingAgreements.Length);
        Assert.Equal(3, state.SourcingAgreementItems.Length);
        Assert.Single(state.StoreSupplyOffers);
        Assert.Equal(3, state.StoreSupplyOfferItems.Length);
        Assert.Equal(2, state.StoreOrders.Length);
        Assert.Equal(6, state.StoreOrderLines.Length);
        Assert.All(state.SourcingAgreementItems, item =>
            Assert.Contains(EstimateSource, item.SourceStableIds));
        Assert.All(state.StoreSupplyOfferItems, item =>
            Assert.Contains(EstimateSource, item.SourceStableIds));

        var chicken = state.StoreOrderLines.Where(value =>
            value.ProductStableIdSnapshot == "product:ingredient:chicken").ToArray();
        var rice = state.StoreOrderLines.Where(value =>
            value.ProductStableIdSnapshot == "product:ingredient:rice").ToArray();
        var beef = state.StoreOrderLines.Where(value =>
            value.ProductStableIdSnapshot == "product:ingredient:beef").ToArray();
        Assert.All(chicken, value => Assert.Equal("BOX",
            value.OrderUnitCodeSnapshot));
        Assert.All(rice, value => Assert.Equal("BAG",
            value.OrderUnitCodeSnapshot));
        Assert.All(beef, value => Assert.Equal("BOX",
            value.OrderUnitCodeSnapshot));
        Assert.Equal(30m, chicken.Sum(value =>
            value.RequestedContentQuantitySnapshot));
        Assert.Equal(60m, rice.Sum(value =>
            value.RequestedContentQuantitySnapshot));
        Assert.Equal(20m, beef.Sum(value =>
            value.RequestedContentQuantitySnapshot));
        Assert.All(state.StoreOrderLines, value => Assert.Equal("KGM",
            value.PackageContentUnitCodeSnapshot));
        Assert.All(state.StoreOrders, value => Assert.Equal(
            프랜차이즈공급망SimulationCodes.본부자체배송,
            value.FulfillmentModelCodeSnapshot));
    }

    [Fact]
    public void 발주_ClientRequestId의_같은payload는_멱등이고_다른payload는_충돌한다()
    {
        var aggregate = CreateThroughOffer();
        var request = StoreOrder(aggregate.Revision, "order:store-one",
            "membership:store-one", "PO-STORE-001", 1m, 1m, 1m);
        var preview = aggregate.PreviewFranchiseStoreOrder(request);
        var command = Confirm("request:store-order:one", preview, request);

        var first = aggregate.ConfirmFranchiseStoreOrder(command);
        var repeated = aggregate.ConfirmFranchiseStoreOrder(command);

        Assert.Equal(first.SourceSessionRevision,
            repeated.SourceSessionRevision);
        Assert.Single(repeated.StoreOrders);

        command.PreviewRequest.OrderNumber = "PO-CHANGED";
        var error = Assert.Throws<SimulationConflictException>(() =>
            aggregate.ConfirmFranchiseStoreOrder(command));
        Assert.Equal("FranchiseSupplyCommandPayloadConflict",
            error.ErrorCode);
    }

    [Fact]
    public void V33_saveReplay는_공급망상태와_멱등receipt를_복원한다()
    {
        var aggregate = CreateSession();
        var request = Initialization(aggregate.Revision);
        var preview = aggregate.PreviewFranchiseSupplyInitialization(request);
        var command = Confirm("request:franchise:init", preview, request);
        var confirmed = aggregate.ConfirmFranchiseSupplyInitialization(command);
        var package = aggregate.CreateSavePackage(new SimulationSessionSaveRequest
        {
            SaveStableId = "save:franchise-supply:v33",
            ExpectedRevision = aggregate.Revision,
        });

        Assert.Equal(SimulationSaveSchemaVersions.V33, package.SchemaVersion);
        Assert.NotNull(package.FranchiseSupply);
        Assert.False(string.IsNullOrWhiteSpace(
            package.FranchiseSupplyBaseSchemaVersion));

        var restored = SimulationSessionReplay.Restore(package);
        var restoredState = restored.GetFranchiseSupplyState()!;
        Assert.Equal(confirmed.SourceSessionRevision,
            restoredState.SourceSessionRevision);
        Assert.Equal(confirmed.NetworkStableId, restoredState.NetworkStableId);

        var repeated = restored.ConfirmFranchiseSupplyInitialization(command);
        Assert.Equal(restoredState.SourceSessionRevision,
            repeated.SourceSessionRevision);
        Assert.Equal(package.SavedWorldRevision, restored.Revision);
    }

    [Fact]
    public void V33_공급망상태변조는_replayHash에서_거부한다()
    {
        var aggregate = CreateFullScenario();
        var package = aggregate.CreateSavePackage(new SimulationSessionSaveRequest
        {
            SaveStableId = "save:franchise-supply:tampered",
            ExpectedRevision = aggregate.Revision,
        });
        package.FranchiseSupply!.DataRevision = "tampered";
        package.Snapshot.FranchiseSupply!.DataRevision = "tampered";

        var error = Assert.Throws<SimulationConflictException>(() =>
            SimulationSessionReplay.Restore(package));

        Assert.Equal("SimulationReplayHashMismatch", error.ErrorCode);
    }

    [Fact]
    public void V33_nullable경계변조는_같은hash로통과하지못하고_재봉인해도거부된다()
    {
        var aggregate = CreateSession();
        Initialize(aggregate);
        var package = aggregate.CreateSavePackage(new SimulationSessionSaveRequest
        {
            SaveStableId = "save:franchise-supply:nullable-boundary",
            ExpectedRevision = aggregate.Revision,
        });

        package.FranchiseSupply!.StoreMemberships[0]
            .EffectiveUntilWorldTick = -1;
        package.Snapshot.FranchiseSupply!.StoreMemberships[0]
            .EffectiveUntilWorldTick = -1;
        package.CommandLog.Single(value =>
                value.FranchiseSupplyState != null)
            .FranchiseSupplyState!.StoreMemberships[0]
            .EffectiveUntilWorldTick = -1;

        Assert.NotEqual(package.ReplayHash,
            SimulationReplayHasher.Calculate(package));
        var stateError = Assert.Throws<SimulationContractException>(() =>
            SimulationSessionReplay.Restore(package));
        Assert.Equal("FranchiseSupplyStateInvalid", stateError.ErrorCode);

        package.ReplayHash = SimulationReplayHasher.Calculate(package);
        stateError = Assert.Throws<SimulationContractException>(() =>
            SimulationSessionReplay.Restore(package));
        Assert.Equal("FranchiseSupplyStateInvalid", stateError.ErrorCode);
    }

    [Fact]
    public void SourceStableIds_배열경계는_preview_멱등key와_V33hash에서구분된다()
    {
        var aggregate = CreateSession();
        var separated = Initialization(aggregate.Revision);
        separated.SourceStableIds = ["source:a", "source:b"];
        var joined = Initialization(aggregate.Revision);
        joined.SourceStableIds = ["source:a,source:b"];
        var separatedPreview = aggregate
            .PreviewFranchiseSupplyInitialization(separated);
        var joinedPreview = aggregate
            .PreviewFranchiseSupplyInitialization(joined);

        Assert.NotEqual(separatedPreview.RequestPayloadHashSha256,
            joinedPreview.RequestPayloadHashSha256);
        Assert.NotEqual(separatedPreview.PreviewHash,
            joinedPreview.PreviewHash);

        const string clientRequestId = "request:source-boundary";
        aggregate.ConfirmFranchiseSupplyInitialization(
            Confirm(clientRequestId, separatedPreview, separated));
        var collision = Assert.Throws<SimulationConflictException>(() =>
            aggregate.ConfirmFranchiseSupplyInitialization(
                Confirm(clientRequestId, joinedPreview, joined)));
        Assert.Equal("FranchiseSupplyCommandPayloadConflict",
            collision.ErrorCode);

        var package = aggregate.CreateSavePackage(new SimulationSessionSaveRequest
        {
            SaveStableId = "save:franchise-supply:source-boundary",
            ExpectedRevision = aggregate.Revision,
        });
        package.FranchiseSupply!.SourceStableIds = ["source:a,source:b"];
        package.Snapshot.FranchiseSupply!.SourceStableIds =
            ["source:a,source:b"];
        package.CommandLog.Single(value =>
                value.FranchiseSupplyState != null)
            .FranchiseSupplyState!.SourceStableIds = ["source:a,source:b"];

        Assert.NotEqual(package.ReplayHash,
            SimulationReplayHasher.Calculate(package));
        var replayError = Assert.Throws<SimulationConflictException>(() =>
            SimulationSessionReplay.Restore(package));
        Assert.Equal("SimulationReplayHashMismatch", replayError.ErrorCode);
    }

    [Fact]
    public void Projection_DB_unique범위는_Preview에서_먼저차단한다()
    {
        var agreements = CreateSession();
        Initialize(agreements);
        ConfirmAgreement(agreements,
            SourcingAgreementOne(agreements.Revision),
            "request:agreement:one");
        var duplicateNumber = SourcingAgreementTwo(agreements.Revision);
        duplicateNumber.ContractNumber = "SOURCE-2026-001";
        Assert.Contains("FranchiseSupplyContractNumberAlreadyExists",
            agreements.PreviewFranchiseSourcingAgreement(duplicateNumber)
                .BlockReasonCodes);
        var duplicateContractItem = SourcingAgreementTwo(agreements.Revision);
        duplicateContractItem.Items[0].ContractItemStableId =
            "contract-item:chicken";
        Assert.Contains("FranchiseSupplyContractItemAlreadyExists",
            agreements.PreviewFranchiseSourcingAgreement(
                duplicateContractItem).BlockReasonCodes);

        var offers = CreateThroughOffer();
        var duplicateOfferNumber = Offer(offers.Revision);
        duplicateOfferNumber.OfferStableId = "offer:stores:r2";
        foreach (var item in duplicateOfferNumber.Items)
            item.OfferItemStableId += ":r2";
        Assert.Contains("FranchiseSupplyOfferNumberAlreadyExists",
            offers.PreviewFranchiseStoreSupplyOffer(duplicateOfferNumber)
                .BlockReasonCodes);
        var duplicateOfferItem = Offer(offers.Revision);
        duplicateOfferItem.OfferStableId = "offer:stores:r3";
        duplicateOfferItem.OfferNumber = "STORE-SUPPLY-2026-003";
        Assert.Contains("FranchiseSupplyOfferItemAlreadyExists",
            offers.PreviewFranchiseStoreSupplyOffer(duplicateOfferItem)
                .BlockReasonCodes);

        var orders = CreateThroughOffer();
        var firstOrder = StoreOrder(orders.Revision, "order:store-one",
            "membership:store-one", "PO-STORE-001", 1m, 1m, 1m);
        ConfirmOrder(orders, firstOrder, "request:store-order:one");
        var duplicateLine = StoreOrder(orders.Revision, "order:store-two",
            "membership:store-two", "PO-STORE-002", 1m, 1m, 1m);
        duplicateLine.Lines[0].OrderLineStableId =
            firstOrder.Lines[0].OrderLineStableId;
        Assert.Contains("FranchiseSupplyOrderLineAlreadyExists",
            orders.PreviewFranchiseStoreOrder(duplicateLine)
                .BlockReasonCodes);
    }

    [Fact]
    public void Projection_schema밖의_ID_통화_길이_decimal은_Confirm전에거부한다()
    {
        var invalidIdAggregate = CreateSession();
        var invalidId = Initialization(invalidIdAggregate.Revision);
        invalidId.NetworkStableId = "공급망:비ASCII";
        Assert.Equal("FranchiseSupplyNetworkStableIdInvalid",
            Assert.Throws<SimulationContractException>(() =>
                invalidIdAggregate.PreviewFranchiseSupplyInitialization(
                    invalidId)).ErrorCode);

        var aggregate = CreateSession();
        Initialize(aggregate);
        var invalidCurrency = SourcingAgreementOne(aggregate.Revision);
        invalidCurrency.CurrencyCode = "KRWW";
        Assert.Equal("FranchiseSupplyCurrencyInvalid",
            Assert.Throws<SimulationContractException>(() =>
                aggregate.PreviewFranchiseSourcingAgreement(
                    invalidCurrency)).ErrorCode);

        var invalidDecimal = SourcingAgreementOne(aggregate.Revision);
        invalidDecimal.Items[0].PackageContentQuantity = 10.00001m;
        Assert.Equal("FranchiseSupplyPackQuantityOrPriceInvalid",
            Assert.Throws<SimulationContractException>(() =>
                aggregate.PreviewFranchiseSourcingAgreement(
                    invalidDecimal)).ErrorCode);

        var decimalMinimum = SourcingAgreementOne(aggregate.Revision);
        decimalMinimum.Items[0].UnitPrice = decimal.MinValue;
        Assert.Equal("FranchiseSupplyPackQuantityOrPriceInvalid",
            Assert.Throws<SimulationContractException>(() =>
                aggregate.PreviewFranchiseSourcingAgreement(
                    decimalMinimum)).ErrorCode);

        var invalidLength = SourcingAgreementOne(aggregate.Revision);
        invalidLength.ContractNumber = new string('N', 101);
        Assert.Equal("FranchiseSupplyContractNumberInvalid",
            Assert.Throws<SimulationContractException>(() =>
                aggregate.PreviewFranchiseSourcingAgreement(
                    invalidLength)).ErrorCode);
    }

    [Fact]
    public void 개별decimal범위안의_수량곱셈overflow도_계약오류로거부한다()
    {
        var aggregate = CreateSession();
        Initialize(aggregate);
        var agreement = SourcingAgreementOne(aggregate.Revision);
        agreement.Items = [agreement.Items[0]];
        agreement.Items[0].PackageContentQuantity = 1m;
        agreement.Items[0].UnitPrice = 9999999999999999.99m;
        agreement.Items[0].MaximumOrderQuantity = null;
        ConfirmAgreement(aggregate, agreement, "request:agreement:overflow");

        var offer = Offer(aggregate.Revision);
        offer.Items = [offer.Items[0]];
        offer.Items[0].UnitPrice = 9999999999999999.99m;
        offer.Items[0].MaximumOrderQuantity = null;
        var offerPreview = aggregate.PreviewFranchiseStoreSupplyOffer(offer);
        aggregate.ConfirmFranchiseStoreSupplyOffer(
            Confirm("request:offer:overflow", offerPreview, offer));

        var order = StoreOrder(aggregate.Revision, "order:overflow",
            "membership:store-one", "PO-OVERFLOW", 1m, 1m, 1m);
        order.Lines = [order.Lines[0]];
        order.Lines[0].RequestedOrderUnitQuantity =
            99999999999999.9999m;

        var error = Assert.Throws<SimulationContractException>(() =>
            aggregate.PreviewFranchiseStoreOrder(order));
        Assert.Equal("FranchiseSupplyLineAmountInvalid", error.ErrorCode);
        Assert.Empty(aggregate.GetFranchiseSupplyState()!.StoreOrders);
    }

    [Fact]
    public void 발주는_매장SemanticPlace와_세션수명_계약통화를지켜야한다()
    {
        var aggregate = CreateThroughOffer();
        var wrongSite = StoreOrder(aggregate.Revision, "order:wrong-site",
            "membership:store-one", "PO-WRONG-SITE", 1m, 1m, 1m);
        wrongSite.DeliverySiteStableId = "semantic-place:store-two";
        Assert.Equal("FranchiseSupplyDeliverySiteStoreMismatch",
            Assert.Throws<SimulationConflictException>(() =>
                aggregate.PreviewFranchiseStoreOrder(wrongSite)).ErrorCode);

        var outsideSession = StoreOrder(aggregate.Revision,
            "order:outside-session", "membership:store-one",
            "PO-OUTSIDE", 1m, 1m, 1m);
        outsideSession.RequestedDeliveryWorldTick = 29;
        Assert.Equal("FranchiseSupplyRequestedDeliveryTickInvalid",
            Assert.Throws<SimulationContractException>(() =>
                aggregate.PreviewFranchiseStoreOrder(outsideSession)).ErrorCode);

        var offerAggregate = CreateSession();
        Initialize(offerAggregate);
        ConfirmAgreement(offerAggregate,
            SourcingAgreementOne(offerAggregate.Revision),
            "request:agreement:one");
        ConfirmAgreement(offerAggregate,
            SourcingAgreementTwo(offerAggregate.Revision),
            "request:agreement:two");
        var currencyMismatch = Offer(offerAggregate.Revision);
        currencyMismatch.CurrencyCode = "USD";
        Assert.Equal("FranchiseSupplySourcingAgreementNotEligibleForOffer",
            Assert.Throws<SimulationConflictException>(() =>
                offerAggregate.PreviewFranchiseStoreSupplyOffer(
                    currencyMismatch)).ErrorCode);
    }

    [Fact]
    public void 공급망과기존Tick은_같은CommandId를_양방향으로거부한다()
    {
        const string shared = "command:shared-kind";
        var supplyFirst = CreateSession();
        var initialization = Initialization(supplyFirst.Revision);
        var preview = supplyFirst.PreviewFranchiseSupplyInitialization(
            initialization);
        supplyFirst.ConfirmFranchiseSupplyInitialization(
            Confirm(shared, preview, initialization));
        Assert.Equal("SimulationCommandKindConflict",
            Assert.Throws<SimulationConflictException>(() =>
                supplyFirst.Advance(new 경영SimulationTick진행Request
                {
                    CommandId = shared,
                    ExpectedRevision = supplyFirst.Revision,
                    TickCount = 1,
                })).ErrorCode);

        var tickFirst = CreateSession();
        tickFirst.Advance(new 경영SimulationTick진행Request
        {
            CommandId = shared,
            ExpectedRevision = tickFirst.Revision,
            TickCount = 1,
        });
        initialization = Initialization(tickFirst.Revision);
        preview = tickFirst.PreviewFranchiseSupplyInitialization(
            initialization);
        Assert.Equal("SimulationCommandKindConflict",
            Assert.Throws<SimulationConflictException>(() =>
                tickFirst.ConfirmFranchiseSupplyInitialization(
                    Confirm(shared, preview, initialization))).ErrorCode);
    }

    [Fact]
    public void 공급망과IntegratedWorld는_복원뒤에도_같은CommandId를_양방향으로거부한다()
    {
        const string shared = "command:shared-integrated-kind";
        var supplyFirst = CreateSessionWithIntegratedWorld();
        var initialization = Initialization(supplyFirst.Revision);
        var preview = supplyFirst.PreviewFranchiseSupplyInitialization(
            initialization);
        supplyFirst.ConfirmFranchiseSupplyInitialization(
            Confirm(shared, preview, initialization));

        Assert.Equal("SimulationCommandKindConflict",
            Assert.Throws<SimulationConflictException>(() =>
                supplyFirst.ConfirmIntegratedWorldCommand(
                    IntegratedManufacturing(shared,
                        supplyFirst.Revision))).ErrorCode);

        var supplyFirstSave = supplyFirst.CreateSavePackage(
            new SimulationSessionSaveRequest
            {
                SaveStableId = "save:franchise-supply:integrated-supply-first",
                ExpectedRevision = supplyFirst.Revision,
            });
        var restoredSupplyFirst = SimulationSessionReplay.Restore(
            supplyFirstSave);
        Assert.Equal("SimulationCommandKindConflict",
            Assert.Throws<SimulationConflictException>(() =>
                restoredSupplyFirst.ConfirmIntegratedWorldCommand(
                    IntegratedManufacturing(shared,
                        restoredSupplyFirst.Revision))).ErrorCode);

        var integratedFirst = CreateSessionWithIntegratedWorld();
        integratedFirst.ConfirmIntegratedWorldCommand(
            IntegratedManufacturing(shared, integratedFirst.Revision));
        initialization = Initialization(integratedFirst.Revision);
        preview = integratedFirst.PreviewFranchiseSupplyInitialization(
            initialization);
        Assert.Equal("SimulationCommandKindConflict",
            Assert.Throws<SimulationConflictException>(() =>
                integratedFirst.ConfirmFranchiseSupplyInitialization(
                    Confirm(shared, preview, initialization))).ErrorCode);

        integratedFirst.ConfirmFranchiseSupplyInitialization(
            Confirm("request:franchise:init:after-integrated", preview,
                initialization));
        var integratedFirstSave = integratedFirst.CreateSavePackage(
            new SimulationSessionSaveRequest
            {
                SaveStableId = "save:franchise-supply:integrated-first",
                ExpectedRevision = integratedFirst.Revision,
            });
        var restoredIntegratedFirst = SimulationSessionReplay.Restore(
            integratedFirstSave);
        var agreement = SourcingAgreementOne(
            restoredIntegratedFirst.Revision);
        var agreementPreview = restoredIntegratedFirst
            .PreviewFranchiseSourcingAgreement(agreement);
        Assert.Equal("SimulationCommandKindConflict",
            Assert.Throws<SimulationConflictException>(() =>
                restoredIntegratedFirst.ConfirmFranchiseSourcingAgreement(
                    Confirm(shared, agreementPreview, agreement))).ErrorCode);
    }

    internal static 경영SimulationSessionAggregate CreateFullScenario()
    {
        var aggregate = CreateThroughOffer();
        ConfirmOrder(aggregate, StoreOrder(aggregate.Revision,
            "order:store-one", "membership:store-one", "PO-STORE-001",
            1m, 1m, 2m), "request:store-order:one");
        ConfirmOrder(aggregate, StoreOrder(aggregate.Revision,
            "order:store-two", "membership:store-two", "PO-STORE-002",
            2m, 2m, 2m), "request:store-order:two");
        return aggregate;
    }

    internal static 경영SimulationSessionAggregate CreateThroughOffer()
    {
        var aggregate = CreateSession();
        Initialize(aggregate);
        ConfirmAgreement(aggregate, SourcingAgreementOne(aggregate.Revision),
            "request:agreement:one");
        ConfirmAgreement(aggregate, SourcingAgreementTwo(aggregate.Revision),
            "request:agreement:two");
        var offer = Offer(aggregate.Revision);
        var preview = aggregate.PreviewFranchiseStoreSupplyOffer(offer);
        aggregate.ConfirmFranchiseStoreSupplyOffer(
            Confirm("request:offer:one", preview, offer));
        return aggregate;
    }

    internal static 경영SimulationSessionAggregate CreateSession()
        => new(new 경영SimulationSession생성Request
        {
            ClientRequestId = Guid.NewGuid(),
            ScenarioStableId = "scenario:franchise-supply:small",
            ScenarioDataRevision = "fixture.r1",
            ScenarioSeed = 925,
            RuleRevision = "simulation.rule.r1",
            DurationTicks = 28,
            WorldContext = new SimulationWorldContext생성Request
            {
                FactionStableId = "faction:franchise-headquarters",
                TerritoryStableId = "region:kr:synthetic",
                SettlementStableId = "settlement:franchise-small",
                GameDateStartsOn = new DateTimeOffset(2026, 9, 25,
                    0, 0, 0, TimeSpan.Zero),
            },
        });

    private static 경영SimulationSessionAggregate CreateSessionWithIntegratedWorld()
        => new(new 경영SimulationSession생성Request
        {
            ClientRequestId = Guid.NewGuid(),
            ScenarioStableId = "scenario:franchise-supply:integrated",
            ScenarioDataRevision = "fixture.r1",
            ScenarioSeed = 925,
            RuleRevision = "simulation.rule.r1",
            DurationTicks = 28,
            WorldContext = new SimulationWorldContext생성Request
            {
                FactionStableId = "faction:franchise-headquarters",
                TerritoryStableId = "region:kr:synthetic",
                SettlementStableId = "settlement:franchise-small",
                GameDateStartsOn = new DateTimeOffset(2026, 9, 25,
                    0, 0, 0, TimeSpan.Zero),
            },
            IntegratedWorld = H5IntegratedWorldScenarioFixture.Create(),
        });

    private static SimulationIntegratedWorldCommandRequest IntegratedManufacturing(
        string commandId, long revision) => new()
    {
        ActionCode = SimulationIntegratedWorldActionCodes.ManufacturingOrder,
        CommandId = commandId,
        ExpectedRevision = revision,
        Manufacturing = new SimulationManufacturingOrderPayload
        {
            RecipeStableId = "recipe:transport-box",
            PreferredManufacturingFacilityStableId = "facility:hub:workshop",
        },
    };

    internal static 프랜차이즈공급망초기화PreviewRequest Initialization(
        long revision) => new()
    {
        ExpectedRevision = revision,
        NetworkStableId = "franchise-network:small",
        DataRevision = "franchise-supply.data.r1",
        Headquarters = new()
        {
            HeadquartersStableId = "franchise-headquarters:small",
            HeadquartersCode = "HQ-001",
            DisplayName = "가상 프랜차이즈 본부",
        },
        Stores =
        [
            new()
            {
                StoreStableId = "store:one",
                StoreCode = "STORE-001",
                DisplayName = "가상 매장 1호",
                SemanticPlaceStableId = "semantic-place:store-one",
                MembershipStableId = "membership:store-one",
            },
            new()
            {
                StoreStableId = "store:two",
                StoreCode = "STORE-002",
                DisplayName = "가상 매장 2호",
                SemanticPlaceStableId = "semantic-place:store-two",
                MembershipStableId = "membership:store-two",
            },
        ],
        Suppliers =
        [
            new()
            {
                SupplierStableId = "supplier:one",
                SupplierKey = "SUPPLIER-001",
                DisplayName = "가상 농축산 공급사",
            },
            new()
            {
                SupplierStableId = "supplier:two",
                SupplierKey = "SUPPLIER-002",
                DisplayName = "가상 육류 공급사",
            },
        ],
        SourceStableIds = ["fixture:franchise-supply-small:r1", EstimateSource],
    };

    private static void Initialize(경영SimulationSessionAggregate aggregate)
    {
        var request = Initialization(aggregate.Revision);
        var preview = aggregate.PreviewFranchiseSupplyInitialization(request);
        aggregate.ConfirmFranchiseSupplyInitialization(
            Confirm("request:franchise:init", preview, request));
    }

    private static 프랜차이즈조달계약PreviewRequest SourcingAgreementOne(
        long revision) => new()
    {
        ExpectedRevision = revision,
        ContractStableId = "contract:supplier-one",
        HeadquartersStableId = "franchise-headquarters:small",
        SupplierStableId = "supplier:one",
        ContractNumber = "SOURCE-2026-001",
        ContractDocumentVersion = "r1",
        CurrencyCode = "KRW",
        Items =
        [
            ContractItem("contract-item:chicken",
                "product:ingredient:chicken", "CHICKEN-10KG",
                "닭고기 10kg", "BOX", 10m, 42000m),
            ContractItem("contract-item:rice",
                "product:ingredient:rice", "RICE-20KG",
                "쌀 20kg", "BAG", 20m, 51000m),
        ],
        SourceStableIds = [EstimateSource],
    };

    private static 프랜차이즈조달계약PreviewRequest SourcingAgreementTwo(
        long revision) => new()
    {
        ExpectedRevision = revision,
        ContractStableId = "contract:supplier-two",
        HeadquartersStableId = "franchise-headquarters:small",
        SupplierStableId = "supplier:two",
        ContractNumber = "SOURCE-2026-002",
        ContractDocumentVersion = "r1",
        CurrencyCode = "KRW",
        Items =
        [
            ContractItem("contract-item:beef",
                "product:ingredient:beef", "BEEF-5KG",
                "소고기 5kg", "BOX", 5m, 79000m),
        ],
        SourceStableIds = [EstimateSource],
    };

    private static 프랜차이즈조달계약품목SimulationInput ContractItem(
        string id, string product, string sku, string name,
        string orderUnit, decimal packageQuantity, decimal price) => new()
    {
        ContractItemStableId = id,
        ProductStableId = product,
        SupplierSku = sku,
        ItemName = name,
        OrderUnitCode = orderUnit,
        PackageContentQuantity = packageQuantity,
        PackageContentUnitCode = "KGM",
        ConversionRuleRevision =
            프랜차이즈공급망SimulationContractCodes.UnitConversionRevision,
        UnitPrice = price,
        MinimumOrderQuantity = 1m,
        StorageConditionCode = "Chilled",
        SourceStableIds = [EstimateSource,
            "product-identity-state:SimulationScopedCandidate"],
    };

    private static void ConfirmAgreement(
        경영SimulationSessionAggregate aggregate,
        프랜차이즈조달계약PreviewRequest request,
        string clientRequestId)
    {
        var preview = aggregate.PreviewFranchiseSourcingAgreement(request);
        aggregate.ConfirmFranchiseSourcingAgreement(
            Confirm(clientRequestId, preview, request));
    }

    private static 프랜차이즈매장공급안PreviewRequest Offer(long revision)
        => new()
        {
            ExpectedRevision = revision,
            OfferStableId = "offer:stores:r1",
            HeadquartersStableId = "franchise-headquarters:small",
            OfferNumber = "STORE-SUPPLY-2026-001",
            OfferDocumentVersion = "r1",
            CurrencyCode = "KRW",
            Items =
            [
                OfferItem("offer-item:chicken", "contract:supplier-one",
                    "contract-item:chicken", "STORE-CHICKEN-10KG", 45000m),
                OfferItem("offer-item:rice", "contract:supplier-one",
                    "contract-item:rice", "STORE-RICE-20KG", 55000m),
                OfferItem("offer-item:beef", "contract:supplier-two",
                    "contract-item:beef", "STORE-BEEF-5KG", 85000m),
            ],
            SourceStableIds = [EstimateSource],
        };

    private static 프랜차이즈매장공급안품목SimulationInput OfferItem(
        string id, string contract, string contractItem, string sku,
        decimal price) => new()
    {
        OfferItemStableId = id,
        SourceContractStableId = contract,
        SourceContractItemStableId = contractItem,
        StoreSku = sku,
        UnitPrice = price,
        MinimumOrderQuantity = 1m,
        SourceStableIds = [EstimateSource],
    };

    private static 프랜차이즈매장발주PreviewRequest StoreOrder(
        long revision, string orderId, string membership, string number,
        decimal chicken, decimal rice, decimal beef) => new()
    {
        ExpectedRevision = revision,
        OrderStableId = orderId,
        StoreMembershipStableId = membership,
        OfferStableId = "offer:stores:r1",
        OrderNumber = number,
        DeliverySiteStableId = membership == "membership:store-one"
            ? "semantic-place:store-one"
            : "semantic-place:store-two",
        RequestedDeliveryWorldTick = 3,
        Lines =
        [
            new()
            {
                OrderLineStableId = orderId + ":chicken",
                OfferItemStableId = "offer-item:chicken",
                RequestedOrderUnitQuantity = chicken,
            },
            new()
            {
                OrderLineStableId = orderId + ":rice",
                OfferItemStableId = "offer-item:rice",
                RequestedOrderUnitQuantity = rice,
            },
            new()
            {
                OrderLineStableId = orderId + ":beef",
                OfferItemStableId = "offer-item:beef",
                RequestedOrderUnitQuantity = beef,
            },
        ],
        SourceStableIds = [EstimateSource],
    };

    private static void ConfirmOrder(경영SimulationSessionAggregate aggregate,
        프랜차이즈매장발주PreviewRequest request,
        string clientRequestId)
    {
        var preview = aggregate.PreviewFranchiseStoreOrder(request);
        aggregate.ConfirmFranchiseStoreOrder(
            Confirm(clientRequestId, preview, request));
    }

    private static 프랜차이즈공급망CommandConfirmRequest<T> Confirm<T>(
        string clientRequestId, 프랜차이즈공급망PreviewSnapshot preview,
        T request) => new()
    {
        ClientRequestId = clientRequestId,
        ExpectedPreviewHash = preview.PreviewHash,
        PreviewRequest = request,
    };
}
