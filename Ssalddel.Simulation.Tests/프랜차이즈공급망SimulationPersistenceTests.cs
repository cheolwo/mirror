using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Simulation.Persistence;

namespace Ssalddel.Simulation.Tests;

[Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
    Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E3,
    "프랜차이즈 공급망 Simulation projection의 관계와 발주 집계 회귀를 검증한다.",
    Boundary = "EF 모델·InMemory 회귀와 migration 생성은 실제 MySQL 적용·API 실행·Unity 표현 증거가 아니다.")]
public sealed class 프랜차이즈공급망SimulationPersistenceTests
{
    [Fact]
    public void 공급망관계는_열한개읽기Projection과세션범위복합키로정의한다()
    {
        using var db = CreateDbContext();
        var model = db.GetService<IDesignTimeModel>().Model;
        var projectionTypes = new[]
        {
            typeof(프랜차이즈공급망ProjectionEntity),
            typeof(프랜차이즈본부ProjectionEntity),
            typeof(프랜차이즈매장ProjectionEntity),
            typeof(프랜차이즈매장소속ProjectionEntity),
            typeof(프랜차이즈공급자ProjectionEntity),
            typeof(프랜차이즈조달계약ProjectionEntity),
            typeof(프랜차이즈조달계약품목ProjectionEntity),
            typeof(프랜차이즈매장공급안ProjectionEntity),
            typeof(프랜차이즈매장공급안품목ProjectionEntity),
            typeof(프랜차이즈매장발주ProjectionEntity),
            typeof(프랜차이즈매장발주품목ProjectionEntity),
        };
        var entityTypes = projectionTypes
            .Select(type => Assert.IsAssignableFrom<IEntityType>(
                model.FindEntityType(type)))
            .ToArray();

        Assert.Equal(11, entityTypes
            .Select(type => type.GetTableName())
            .Distinct(StringComparer.Ordinal)
            .Count());
        Assert.All(entityTypes, type =>
            Assert.Contains("Projection", type.GetTableName(),
                StringComparison.Ordinal));
        Assert.All(entityTypes.SelectMany(type => type.GetForeignKeys()),
            foreignKey => Assert.Equal(DeleteBehavior.Restrict,
                foreignKey.DeleteBehavior));

        var network = entityTypes.Single(type =>
            type.ClrType == typeof(프랜차이즈공급망ProjectionEntity));
        Assert.Equal(new[] { "SessionStableId", "NetworkStableId" },
            network.FindPrimaryKey()!.Properties.Select(value => value.Name));
        Assert.Equal("ascii_bin",
            network.FindProperty("NetworkStableId")!.GetCollation());

        var binaryBusinessKeys = new (Type EntityType, string PropertyName)[]
        {
            (typeof(프랜차이즈본부ProjectionEntity), "HeadquartersCode"),
            (typeof(프랜차이즈매장ProjectionEntity), "StoreCode"),
            (typeof(프랜차이즈공급자ProjectionEntity), "SupplierKey"),
            (typeof(프랜차이즈조달계약ProjectionEntity), "ContractNumber"),
            (typeof(프랜차이즈조달계약품목ProjectionEntity), "SupplierSku"),
            (typeof(프랜차이즈매장공급안ProjectionEntity), "OfferNumber"),
            (typeof(프랜차이즈매장공급안품목ProjectionEntity), "StoreSku"),
            (typeof(프랜차이즈매장발주ProjectionEntity), "OrderNumber"),
        };
        Assert.All(binaryBusinessKeys, item =>
        {
            var entityType = entityTypes.Single(type =>
                type.ClrType == item.EntityType);
            Assert.Equal("utf8mb4_bin",
                entityType.FindProperty(item.PropertyName)!.GetCollation());
        });

        var order = entityTypes.Single(type =>
            type.ClrType == typeof(프랜차이즈매장발주ProjectionEntity));
        Assert.Contains(order.GetForeignKeys(), foreignKey =>
            foreignKey.PrincipalEntityType.ClrType ==
                typeof(프랜차이즈매장소속ProjectionEntity)
            && foreignKey.Properties.Select(value => value.Name).SequenceEqual(
                new[]
                {
                    "SessionStableId",
                    "NetworkStableId",
                    "HeadquartersStableId",
                    "StoreMembershipStableId",
                    "StoreStableId",
                }));
        Assert.Contains(order.GetForeignKeys(), foreignKey =>
            foreignKey.PrincipalEntityType.ClrType ==
                typeof(프랜차이즈매장공급안ProjectionEntity));
        Assert.DoesNotContain(order.GetForeignKeys(), foreignKey =>
            foreignKey.PrincipalEntityType.ClrType ==
                typeof(프랜차이즈조달계약ProjectionEntity)
            || foreignKey.PrincipalEntityType.ClrType ==
                typeof(프랜차이즈공급자ProjectionEntity));
        Assert.Contains(order.GetIndexes(), index =>
            index.IsUnique
            && index.Properties.Select(value => value.Name).SequenceEqual(
                new[]
                {
                    "SessionStableId",
                    "NetworkStableId",
                    "StoreMembershipStableId",
                    "ClientRequestStableId",
                }));
        Assert.Contains(order.GetIndexes(), index =>
            index.IsUnique
            && index.Properties.Select(value => value.Name).SequenceEqual(
                new[]
                {
                    "SessionStableId",
                    "NetworkStableId",
                    "StoreMembershipStableId",
                    "OrderNumber",
                }));

        var line = entityTypes.Single(type =>
            type.ClrType == typeof(프랜차이즈매장발주품목ProjectionEntity));
        Assert.Contains(line.GetForeignKeys(), foreignKey =>
            foreignKey.PrincipalEntityType.ClrType ==
                typeof(프랜차이즈매장공급안품목ProjectionEntity)
            && foreignKey.Properties.Select(value => value.Name).SequenceEqual(
                new[]
                {
                    "SessionStableId",
                    "NetworkStableId",
                    "HeadquartersStableId",
                    "OfferStableId",
                    "OfferItemStableId",
                }));
    }

    [Fact]
    public void 같은표본StableId는_서로다른두세션에함께저장된다()
    {
        using var db = CreateDbContext();
        db.Database.EnsureCreated();
        db.AddRange(CreateProjectionGraph("session:one", 10m, 6m));
        db.AddRange(CreateProjectionGraph("session:two", 4m, 3m));

        db.SaveChanges();

        Assert.Equal(2, db.FranchiseSupplyNetworkProjections.Count());
        Assert.Equal(4, db.FranchiseStoreOrderProjections.Count());
        Assert.Equal(2, db.FranchiseSourcingAgreementProjections
            .Count(value => value.ContractStableId == "sourcing-agreement:sample"));
    }

    [Fact]
    public void 두매장발주는_상류조달계약이아닌본부공급안으로집계한다()
    {
        using var db = CreateDbContext();
        db.Database.EnsureCreated();
        db.AddRange(CreateProjectionGraph("session:demand", 10m, 6m));
        db.SaveChanges();

        var demand = db.FranchiseStoreOrderLineProjections
            .Where(value => value.SessionStableId == "session:demand"
                && value.OfferItemStableId == "store-offer-item:potato-box")
            .GroupBy(value => new
            {
                value.OrderUnitCodeSnapshot,
                value.PackageContentUnitCodeSnapshot,
            })
            .Select(group => new
            {
                OrderUnit = group.Key.OrderUnitCodeSnapshot,
                ContentUnit = group.Key.PackageContentUnitCodeSnapshot,
                OrderUnitQuantity = group.Sum(value =>
                    value.RequestedOrderUnitQuantity),
                ContentQuantity = group.Sum(value =>
                    value.RequestedContentQuantitySnapshot),
                StoreCount = group.Select(value => value.Order.StoreStableId)
                    .Distinct().Count(),
            })
            .Single();

        Assert.Equal("BOX", demand.OrderUnit);
        Assert.Equal("KGM", demand.ContentUnit);
        Assert.Equal(16m, demand.OrderUnitQuantity);
        Assert.Equal(160m, demand.ContentQuantity);
        Assert.Equal(2, demand.StoreCount);
    }

    [Fact]
    public void 공급망기본값은_SessionAggregate권위의읽기전용합성Simulation이다()
    {
        var projection = new 프랜차이즈공급망ProjectionEntity();
        var offer = new 프랜차이즈매장공급안ProjectionEntity();

        Assert.Equal(SimulationModeCodes.Simulation, projection.ModeCode);
        Assert.Equal(프랜차이즈공급망SimulationCodes.SessionAggregate권위,
            projection.StateAuthorityCode);
        Assert.True(projection.IsSynthetic);
        Assert.True(projection.IsReadOnlyProjection);
        Assert.False(projection.IsOperationalState);
        Assert.Equal(프랜차이즈공급망SimulationCodes.본부재판매,
            offer.CommercialFlowModelCode);
        Assert.Equal(프랜차이즈공급망SimulationCodes.본부자체배송,
            offer.FulfillmentModelCode);
    }

    private static SimulationSessionDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SimulationSessionDbContext>()
            .UseInMemoryDatabase("franchise-supply-" + Guid.NewGuid().ToString("N"))
            .Options;
        return new SimulationSessionDbContext(options);
    }

    private static object[] CreateProjectionGraph(
        string sessionStableId,
        decimal firstQuantity,
        decimal secondQuantity)
    {
        const string network = "franchise-network:sample";
        const string headquarters = "franchise-headquarters:sample";
        const string supplier = "supplier:sample";
        const string contract = "sourcing-agreement:sample";
        const string contractItem = "sourcing-item:potato-box";
        const string offer = "store-offer:sample";
        const string offerItem = "store-offer-item:potato-box";
        var now = DateTimeOffset.Parse("2026-09-25T00:00:00Z");

        return
        [
            new 프랜차이즈공급망ProjectionEntity
            {
                SessionStableId = sessionStableId,
                NetworkStableId = network,
                ScenarioStableId = "scenario:franchise-supply",
                DataRevision = "franchise-supply.data.r1",
                RuleRevision = "franchise-supply.rules.r1",
                SourceSessionRevision = 7,
                ProjectionRevision = 1,
                AsOfWorldTick = 12,
                GeneratedAtUtc = now,
            },
            new 프랜차이즈본부ProjectionEntity
            {
                SessionStableId = sessionStableId,
                NetworkStableId = network,
                HeadquartersStableId = headquarters,
                HeadquartersCode = "HQ-001",
                DisplayName = "가상 프랜차이즈 본부",
            },
            Store(sessionStableId, network, "store:one", "STORE-001", "가상 매장 1호"),
            Store(sessionStableId, network, "store:two", "STORE-002", "가상 매장 2호"),
            Membership(sessionStableId, network, headquarters,
                "membership:one", "store:one"),
            Membership(sessionStableId, network, headquarters,
                "membership:two", "store:two"),
            new 프랜차이즈공급자ProjectionEntity
            {
                SessionStableId = sessionStableId,
                NetworkStableId = network,
                SupplierStableId = supplier,
                SupplierKey = "SUPPLIER-001",
                DisplayName = "가상 식자재 공급사",
                SupplierKindCode = "FoodIngredientSupplier",
            },
            new 프랜차이즈조달계약ProjectionEntity
            {
                SessionStableId = sessionStableId,
                NetworkStableId = network,
                ContractStableId = contract,
                HeadquartersStableId = headquarters,
                SupplierStableId = supplier,
                ContractNumber = "SAMPLE-2026-001",
                ContractDocumentVersion = "r1",
                StatusCode = 프랜차이즈공급망SimulationCodes.계약활성,
                CurrencyCode = "KRW",
            },
            new 프랜차이즈조달계약품목ProjectionEntity
            {
                SessionStableId = sessionStableId,
                NetworkStableId = network,
                ContractItemStableId = contractItem,
                HeadquartersStableId = headquarters,
                ContractStableId = contract,
                ProductStableId = "simulation-product:potato-box",
                SupplierSku = "SUPPLIER-POTATO-10KG",
                ItemName = "감자 10kg 상자",
                OrderUnitCode = "BOX",
                PackageContentQuantity = 10m,
                PackageContentUnitCode = "KGM",
                ConversionRuleRevision = "unit-conversion.r1",
                UnitPrice = 22000m,
                MinimumOrderQuantity = 1m,
                StorageConditionCode = "Ambient",
            },
            new 프랜차이즈매장공급안ProjectionEntity
            {
                SessionStableId = sessionStableId,
                NetworkStableId = network,
                OfferStableId = offer,
                HeadquartersStableId = headquarters,
                OfferNumber = "STORE-SUPPLY-2026-001",
                OfferDocumentVersion = "r1",
                StatusCode = 프랜차이즈공급망SimulationCodes.계약활성,
                CurrencyCode = "KRW",
            },
            new 프랜차이즈매장공급안품목ProjectionEntity
            {
                SessionStableId = sessionStableId,
                NetworkStableId = network,
                OfferItemStableId = offerItem,
                HeadquartersStableId = headquarters,
                OfferStableId = offer,
                SourceContractStableId = contract,
                SourceContractItemStableId = contractItem,
                ProductStableId = "simulation-product:potato-box",
                StoreSku = "STORE-POTATO-10KG",
                ItemName = "감자 10kg 상자",
                OrderUnitCode = "BOX",
                PackageContentQuantity = 10m,
                PackageContentUnitCode = "KGM",
                ConversionRuleRevision = "unit-conversion.r1",
                UnitPrice = 24000m,
                MinimumOrderQuantity = 1m,
                StorageConditionCode = "Ambient",
            },
            Order(sessionStableId, network, headquarters, offer,
                "order:one", "membership:one", "store:one",
                "request:one", "PO-001", firstQuantity),
            Order(sessionStableId, network, headquarters, offer,
                "order:two", "membership:two", "store:two",
                "request:two", "PO-002", secondQuantity),
            Line(sessionStableId, network, headquarters, offer, offerItem,
                "order-line:one", "order:one", firstQuantity),
            Line(sessionStableId, network, headquarters, offer, offerItem,
                "order-line:two", "order:two", secondQuantity),
        ];
    }

    private static 프랜차이즈매장ProjectionEntity Store(
        string session,
        string network,
        string stableId,
        string code,
        string name) => new()
        {
            SessionStableId = session,
            NetworkStableId = network,
            StoreStableId = stableId,
            StoreCode = code,
            DisplayName = name,
            SemanticPlaceStableId = "semantic-place:" + code.ToLowerInvariant(),
        };

    private static 프랜차이즈매장소속ProjectionEntity Membership(
        string session,
        string network,
        string headquarters,
        string stableId,
        string store) => new()
        {
            SessionStableId = session,
            NetworkStableId = network,
            MembershipStableId = stableId,
            HeadquartersStableId = headquarters,
            StoreStableId = store,
        };

    private static 프랜차이즈매장발주ProjectionEntity Order(
        string session,
        string network,
        string headquarters,
        string offer,
        string stableId,
        string membership,
        string store,
        string clientRequest,
        string orderNumber,
        decimal quantity) => new()
        {
            SessionStableId = session,
            NetworkStableId = network,
            OrderStableId = stableId,
            HeadquartersStableId = headquarters,
            StoreMembershipStableId = membership,
            StoreStableId = store,
            OfferStableId = offer,
            ClientRequestStableId = clientRequest,
            RequestPayloadHashSha256 = new string('a', 64),
            OrderNumber = orderNumber,
            OfferDocumentVersionSnapshot = "r1",
            SellerHeadquartersStableIdSnapshot = headquarters,
            CommercialFlowModelCodeSnapshot =
                프랜차이즈공급망SimulationCodes.본부재판매,
            FulfillmentModelCodeSnapshot =
                프랜차이즈공급망SimulationCodes.본부자체배송,
            DeliverySiteStableIdSnapshot = "delivery-site:" + store,
            CurrencyCodeSnapshot = "KRW",
            StatusCode = 프랜차이즈공급망SimulationCodes.발주제출,
            RequestedDeliveryWorldTick = 20,
            CreatedWorldTick = 10,
            SubmittedWorldTick = 11,
            TotalAmountSnapshot = quantity * 24000m,
        };

    private static 프랜차이즈매장발주품목ProjectionEntity Line(
        string session,
        string network,
        string headquarters,
        string offer,
        string offerItem,
        string stableId,
        string order,
        decimal quantity) => new()
        {
            SessionStableId = session,
            NetworkStableId = network,
            OrderLineStableId = stableId,
            HeadquartersStableId = headquarters,
            OfferStableId = offer,
            OrderStableId = order,
            OfferItemStableId = offerItem,
            ProductStableIdSnapshot = "simulation-product:potato-box",
            StoreSkuSnapshot = "STORE-POTATO-10KG",
            ItemNameSnapshot = "감자 10kg 상자",
            OrderUnitCodeSnapshot = "BOX",
            PackageContentQuantitySnapshot = 10m,
            PackageContentUnitCodeSnapshot = "KGM",
            ConversionRuleRevisionSnapshot = "unit-conversion.r1",
            UnitPriceSnapshot = 24000m,
            CurrencyCodeSnapshot = "KRW",
            RequestedOrderUnitQuantity = quantity,
            RequestedContentQuantitySnapshot = quantity * 10m,
            LineAmountSnapshot = quantity * 24000m,
        };
}
