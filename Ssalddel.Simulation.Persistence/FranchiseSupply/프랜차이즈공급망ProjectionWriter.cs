using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Simulation.Application;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Simulation.Domain;

namespace Ssalddel.Simulation.Persistence;

public sealed class 프랜차이즈공급망ProjectionWriter(
    IDbContextFactory<SimulationSessionDbContext> dbContextFactory)
    : I프랜차이즈공급망ProjectionWriter
{
    public const string RevisionConflictCode =
        "FranchiseSupplyProjectionRevisionConflict";

    public bool PersistenceEnabled => true;

    public void ReplaceConfirmedState(string sessionStableId,
        프랜차이즈공급망StateSnapshot state)
    {
        if (string.IsNullOrWhiteSpace(sessionStableId))
            throw new ArgumentException(nameof(sessionStableId));
        if (state == null) throw new ArgumentNullException(nameof(state));
        if (!state.SimulationOnly || state.IsOperationalState)
            throw new SimulationContractException(
                "FranchiseSupplyProjectionOperationalStateForbidden");

        var sessionId = sessionStableId.Trim();
        var networkId = state.NetworkStableId.Trim();
        var incomingHash = CalculateStatePayloadHash(state);
        using var db = dbContextFactory.CreateDbContext();
        var isMySql = string.Equals(db.Database.ProviderName,
            "Pomelo.EntityFrameworkCore.MySql",
            StringComparison.Ordinal);
        var isRelational = db.Database.IsRelational();
        var lockName = Sha256("franchise-supply|" + sessionId + "|"
            + networkId);
        if (isMySql)
        {
            db.Database.OpenConnection();
            AcquireMySqlScopeLock(db, lockName);
        }
        try
        {
            using var transaction = isRelational
                ? db.Database.BeginTransaction()
                : null;
            var existing = isMySql
                ? db.FranchiseSupplyNetworkProjections.FromSqlInterpolated(
                    $"SELECT * FROM `시뮬레이션_프랜차이즈공급망Projection` WHERE `세션고유식별자` = {sessionId} AND `공급망고유식별자` = {networkId} FOR UPDATE")
                    .AsNoTracking()
                    .SingleOrDefault()
                : db.FranchiseSupplyNetworkProjections
                    .AsNoTracking()
                    .SingleOrDefault(value => value.SessionStableId == sessionId
                        && value.NetworkStableId == networkId);

            if (existing != null)
            {
                var sourceComparison = state.SourceSessionRevision.CompareTo(
                    existing.SourceSessionRevision);
                var projectionComparison = state.ProjectionRevision.CompareTo(
                    existing.ProjectionRevision);
                if (sourceComparison <= 0 && projectionComparison <= 0
                    && (sourceComparison < 0 || projectionComparison < 0))
                    return;
                if (sourceComparison == 0 && projectionComparison == 0)
                {
                    if (!string.Equals(existing.StatePayloadHashSha256,
                            incomingHash, StringComparison.Ordinal))
                        throw new SimulationConflictException(
                            RevisionConflictCode);
                    return;
                }
                if (sourceComparison <= 0 || projectionComparison <= 0)
                    throw new SimulationConflictException(
                        RevisionConflictCode);

                DeleteScope(db, sessionId, networkId, isRelational);
            }

            AddState(db, sessionId, state, incomingHash);
            db.SaveChanges();
            transaction?.Commit();
        }
        finally
        {
            if (isMySql) ReleaseMySqlScopeLock(db, lockName);
        }
    }

    public static string CalculateStatePayloadHash(
        프랜차이즈공급망StateSnapshot state)
    {
        if (state == null) throw new ArgumentNullException(nameof(state));
        return Sha256(JsonSerializer.Serialize(state));
    }

    private static string Sha256(string value)
    {
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(
            value))).ToLowerInvariant();
    }

    private static void AcquireMySqlScopeLock(
        SimulationSessionDbContext db, string lockName)
    {
        using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT GET_LOCK(@lockName, 30)";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@lockName";
        parameter.Value = lockName;
        command.Parameters.Add(parameter);
        if (Convert.ToInt32(command.ExecuteScalar()) != 1)
            throw new TimeoutException(
                "FranchiseSupplyProjectionScopeLockTimeout");
    }

    private static void ReleaseMySqlScopeLock(
        SimulationSessionDbContext db, string lockName)
    {
        using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT RELEASE_LOCK(@lockName)";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@lockName";
        parameter.Value = lockName;
        command.Parameters.Add(parameter);
        command.ExecuteScalar();
    }

    private static void DeleteScope(SimulationSessionDbContext db,
        string sessionStableId, string networkStableId,
        bool executeImmediately)
    {
        if (!executeImmediately)
        {
            db.FranchiseStoreOrderLineProjections.RemoveRange(
                db.FranchiseStoreOrderLineProjections.Where(value =>
                    value.SessionStableId == sessionStableId
                    && value.NetworkStableId == networkStableId));
            db.FranchiseStoreOrderProjections.RemoveRange(
                db.FranchiseStoreOrderProjections.Where(value =>
                    value.SessionStableId == sessionStableId
                    && value.NetworkStableId == networkStableId));
            db.FranchiseStoreSupplyOfferItemProjections.RemoveRange(
                db.FranchiseStoreSupplyOfferItemProjections.Where(value =>
                    value.SessionStableId == sessionStableId
                    && value.NetworkStableId == networkStableId));
            db.FranchiseStoreSupplyOfferProjections.RemoveRange(
                db.FranchiseStoreSupplyOfferProjections.Where(value =>
                    value.SessionStableId == sessionStableId
                    && value.NetworkStableId == networkStableId));
            db.FranchiseSourcingItemProjections.RemoveRange(
                db.FranchiseSourcingItemProjections.Where(value =>
                    value.SessionStableId == sessionStableId
                    && value.NetworkStableId == networkStableId));
            db.FranchiseSourcingAgreementProjections.RemoveRange(
                db.FranchiseSourcingAgreementProjections.Where(value =>
                    value.SessionStableId == sessionStableId
                    && value.NetworkStableId == networkStableId));
            db.FranchiseStoreMembershipProjections.RemoveRange(
                db.FranchiseStoreMembershipProjections.Where(value =>
                    value.SessionStableId == sessionStableId
                    && value.NetworkStableId == networkStableId));
            db.FranchiseStoreProjections.RemoveRange(
                db.FranchiseStoreProjections.Where(value =>
                    value.SessionStableId == sessionStableId
                    && value.NetworkStableId == networkStableId));
            db.FranchiseSupplierProjections.RemoveRange(
                db.FranchiseSupplierProjections.Where(value =>
                    value.SessionStableId == sessionStableId
                    && value.NetworkStableId == networkStableId));
            db.FranchiseHeadquartersProjections.RemoveRange(
                db.FranchiseHeadquartersProjections.Where(value =>
                    value.SessionStableId == sessionStableId
                    && value.NetworkStableId == networkStableId));
            db.FranchiseSupplyNetworkProjections.RemoveRange(
                db.FranchiseSupplyNetworkProjections.Where(value =>
                    value.SessionStableId == sessionStableId
                    && value.NetworkStableId == networkStableId));
            return;
        }
        db.FranchiseStoreOrderLineProjections.Where(value =>
            value.SessionStableId == sessionStableId
            && value.NetworkStableId == networkStableId).ExecuteDelete();
        db.FranchiseStoreOrderProjections.Where(value =>
            value.SessionStableId == sessionStableId
            && value.NetworkStableId == networkStableId).ExecuteDelete();
        db.FranchiseStoreSupplyOfferItemProjections.Where(value =>
            value.SessionStableId == sessionStableId
            && value.NetworkStableId == networkStableId).ExecuteDelete();
        db.FranchiseStoreSupplyOfferProjections.Where(value =>
            value.SessionStableId == sessionStableId
            && value.NetworkStableId == networkStableId).ExecuteDelete();
        db.FranchiseSourcingItemProjections.Where(value =>
            value.SessionStableId == sessionStableId
            && value.NetworkStableId == networkStableId).ExecuteDelete();
        db.FranchiseSourcingAgreementProjections.Where(value =>
            value.SessionStableId == sessionStableId
            && value.NetworkStableId == networkStableId).ExecuteDelete();
        db.FranchiseStoreMembershipProjections.Where(value =>
            value.SessionStableId == sessionStableId
            && value.NetworkStableId == networkStableId).ExecuteDelete();
        db.FranchiseStoreProjections.Where(value =>
            value.SessionStableId == sessionStableId
            && value.NetworkStableId == networkStableId).ExecuteDelete();
        db.FranchiseSupplierProjections.Where(value =>
            value.SessionStableId == sessionStableId
            && value.NetworkStableId == networkStableId).ExecuteDelete();
        db.FranchiseHeadquartersProjections.Where(value =>
            value.SessionStableId == sessionStableId
            && value.NetworkStableId == networkStableId).ExecuteDelete();
        db.FranchiseSupplyNetworkProjections.Where(value =>
            value.SessionStableId == sessionStableId
            && value.NetworkStableId == networkStableId).ExecuteDelete();
    }

    private static void AddState(SimulationSessionDbContext db,
        string sessionStableId, 프랜차이즈공급망StateSnapshot state,
        string statePayloadHashSha256)
    {
        var networkId = state.NetworkStableId;
        var generatedAt = DateTimeOffset.UtcNow;
        db.FranchiseSupplyNetworkProjections.Add(new()
        {
            SessionStableId = sessionStableId,
            NetworkStableId = networkId,
            ScenarioStableId = state.ScenarioStableId,
            SourceKindCode = 프랜차이즈공급망SimulationCodes.검증표본,
            ModeCode = SimulationModeCodes.Simulation,
            StateAuthorityCode =
                프랜차이즈공급망SimulationCodes.SessionAggregate권위,
            IsSynthetic = true,
            IsOperationalState = false,
            IsReadOnlyProjection = true,
            DataRevision = state.DataRevision,
            RuleRevision = state.RuleRevision,
            SourceStableIdsJson = SourcesJson(state.SourceStableIds),
            StatePayloadHashSha256 = statePayloadHashSha256,
            SourceSessionRevision = state.SourceSessionRevision,
            ProjectionRevision = state.ProjectionRevision,
            AsOfWorldTick = state.AsOfWorldTick,
            GeneratedAtUtc = generatedAt,
        });
        db.FranchiseHeadquartersProjections.AddRange(state.Headquarters.Select(value =>
            new 프랜차이즈본부ProjectionEntity
            {
                SessionStableId = sessionStableId,
                NetworkStableId = networkId,
                HeadquartersStableId = value.HeadquartersStableId,
                HeadquartersCode = value.HeadquartersCode,
                DisplayName = value.DisplayName,
                StatusCode = value.StatusCode,
                Revision = value.Revision,
            }));
        db.FranchiseStoreProjections.AddRange(state.Stores.Select(value =>
            new 프랜차이즈매장ProjectionEntity
            {
                SessionStableId = sessionStableId,
                NetworkStableId = networkId,
                StoreStableId = value.StoreStableId,
                StoreCode = value.StoreCode,
                DisplayName = value.DisplayName,
                SemanticPlaceStableId = value.SemanticPlaceStableId,
                StatusCode = value.StatusCode,
                Revision = value.Revision,
            }));
        db.FranchiseStoreMembershipProjections.AddRange(
            state.StoreMemberships.Select(value =>
                new 프랜차이즈매장소속ProjectionEntity
                {
                    SessionStableId = sessionStableId,
                    NetworkStableId = networkId,
                    MembershipStableId = value.MembershipStableId,
                    HeadquartersStableId = value.HeadquartersStableId,
                    StoreStableId = value.StoreStableId,
                    StatusCode = value.StatusCode,
                    EffectiveFromWorldTick = value.EffectiveFromWorldTick,
                    EffectiveUntilWorldTick = value.EffectiveUntilWorldTick,
                    Revision = value.Revision,
                }));
        db.FranchiseSupplierProjections.AddRange(state.Suppliers.Select(value =>
            new 프랜차이즈공급자ProjectionEntity
            {
                SessionStableId = sessionStableId,
                NetworkStableId = networkId,
                SupplierStableId = value.SupplierStableId,
                SupplierKey = value.SupplierKey,
                DisplayName = value.DisplayName,
                SupplierKindCode = value.SupplierKindCode,
                StatusCode = value.StatusCode,
                Revision = value.Revision,
            }));
        db.FranchiseSourcingAgreementProjections.AddRange(
            state.SourcingAgreements.Select(value =>
                new 프랜차이즈조달계약ProjectionEntity
                {
                    SessionStableId = sessionStableId,
                    NetworkStableId = networkId,
                    ContractStableId = value.ContractStableId,
                    HeadquartersStableId = value.HeadquartersStableId,
                    SupplierStableId = value.SupplierStableId,
                    ContractNumber = value.ContractNumber,
                    ContractDocumentVersion = value.ContractDocumentVersion,
                    StatusCode = value.StatusCode,
                    EffectiveFromWorldTick = value.EffectiveFromWorldTick,
                    EffectiveUntilWorldTick = value.EffectiveUntilWorldTick,
                    CurrencyCode = value.CurrencyCode,
                    Revision = value.Revision,
                }));
        db.FranchiseSourcingItemProjections.AddRange(
            state.SourcingAgreementItems.Select(value =>
                new 프랜차이즈조달계약품목ProjectionEntity
                {
                    SessionStableId = sessionStableId,
                    NetworkStableId = networkId,
                    ContractItemStableId = value.ContractItemStableId,
                    HeadquartersStableId = value.HeadquartersStableId,
                    ContractStableId = value.ContractStableId,
                    ProductStableId = value.ProductStableId,
                    SupplierSku = value.SupplierSku,
                    ItemName = value.ItemName,
                    OrderUnitCode = value.OrderUnitCode,
                    PackageContentQuantity = value.PackageContentQuantity,
                    PackageContentUnitCode = value.PackageContentUnitCode,
                    ConversionRuleRevision = value.ConversionRuleRevision,
                    UnitPrice = value.UnitPrice,
                    MinimumOrderQuantity = value.MinimumOrderQuantity,
                    MaximumOrderQuantity = value.MaximumOrderQuantity,
                    StorageConditionCode = value.StorageConditionCode,
                    StatusCode = value.StatusCode,
                    Revision = value.Revision,
                    SourceStableIdsJson = SourcesJson(value.SourceStableIds),
                }));
        db.FranchiseStoreSupplyOfferProjections.AddRange(
            state.StoreSupplyOffers.Select(value =>
                new 프랜차이즈매장공급안ProjectionEntity
                {
                    SessionStableId = sessionStableId,
                    NetworkStableId = networkId,
                    OfferStableId = value.OfferStableId,
                    HeadquartersStableId = value.HeadquartersStableId,
                    OfferNumber = value.OfferNumber,
                    OfferDocumentVersion = value.OfferDocumentVersion,
                    CommercialFlowModelCode = value.CommercialFlowModelCode,
                    FulfillmentModelCode = value.FulfillmentModelCode,
                    StatusCode = value.StatusCode,
                    EffectiveFromWorldTick = value.EffectiveFromWorldTick,
                    EffectiveUntilWorldTick = value.EffectiveUntilWorldTick,
                    CurrencyCode = value.CurrencyCode,
                    Revision = value.Revision,
                }));
        db.FranchiseStoreSupplyOfferItemProjections.AddRange(
            state.StoreSupplyOfferItems.Select(value =>
                new 프랜차이즈매장공급안품목ProjectionEntity
                {
                    SessionStableId = sessionStableId,
                    NetworkStableId = networkId,
                    OfferItemStableId = value.OfferItemStableId,
                    HeadquartersStableId = value.HeadquartersStableId,
                    OfferStableId = value.OfferStableId,
                    SourceContractStableId = value.SourceContractStableId,
                    SourceContractItemStableId = value.SourceContractItemStableId,
                    ProductStableId = value.ProductStableId,
                    StoreSku = value.StoreSku,
                    ItemName = value.ItemName,
                    OrderUnitCode = value.OrderUnitCode,
                    PackageContentQuantity = value.PackageContentQuantity,
                    PackageContentUnitCode = value.PackageContentUnitCode,
                    ConversionRuleRevision = value.ConversionRuleRevision,
                    UnitPrice = value.UnitPrice,
                    MinimumOrderQuantity = value.MinimumOrderQuantity,
                    MaximumOrderQuantity = value.MaximumOrderQuantity,
                    StorageConditionCode = value.StorageConditionCode,
                    StatusCode = value.StatusCode,
                    Revision = value.Revision,
                    SourceStableIdsJson = SourcesJson(value.SourceStableIds),
                }));
        db.FranchiseStoreOrderProjections.AddRange(state.StoreOrders.Select(value =>
            new 프랜차이즈매장발주ProjectionEntity
            {
                SessionStableId = sessionStableId,
                NetworkStableId = networkId,
                OrderStableId = value.OrderStableId,
                HeadquartersStableId = value.HeadquartersStableId,
                StoreMembershipStableId = value.StoreMembershipStableId,
                StoreStableId = value.StoreStableId,
                OfferStableId = value.OfferStableId,
                ClientRequestStableId = value.ClientRequestStableId,
                RequestPayloadHashSha256 = value.RequestPayloadHashSha256,
                OrderNumber = value.OrderNumber,
                OfferDocumentVersionSnapshot = value.OfferDocumentVersionSnapshot,
                SellerHeadquartersStableIdSnapshot =
                    value.SellerHeadquartersStableIdSnapshot,
                CommercialFlowModelCodeSnapshot =
                    value.CommercialFlowModelCodeSnapshot,
                FulfillmentModelCodeSnapshot = value.FulfillmentModelCodeSnapshot,
                DeliverySiteStableIdSnapshot = value.DeliverySiteStableIdSnapshot,
                CurrencyCodeSnapshot = value.CurrencyCodeSnapshot,
                StatusCode = value.StatusCode,
                RequestedDeliveryWorldTick = value.RequestedDeliveryWorldTick,
                CreatedWorldTick = value.CreatedWorldTick,
                SubmittedWorldTick = value.SubmittedWorldTick,
                TotalAmountSnapshot = value.TotalAmountSnapshot,
                Revision = value.Revision,
            }));
        db.FranchiseStoreOrderLineProjections.AddRange(
            state.StoreOrderLines.Select(value =>
                new 프랜차이즈매장발주품목ProjectionEntity
                {
                    SessionStableId = sessionStableId,
                    NetworkStableId = networkId,
                    OrderLineStableId = value.OrderLineStableId,
                    HeadquartersStableId = value.HeadquartersStableId,
                    OfferStableId = value.OfferStableId,
                    OrderStableId = value.OrderStableId,
                    OfferItemStableId = value.OfferItemStableId,
                    ProductStableIdSnapshot = value.ProductStableIdSnapshot,
                    StoreSkuSnapshot = value.StoreSkuSnapshot,
                    ItemNameSnapshot = value.ItemNameSnapshot,
                    OrderUnitCodeSnapshot = value.OrderUnitCodeSnapshot,
                    PackageContentQuantitySnapshot =
                        value.PackageContentQuantitySnapshot,
                    PackageContentUnitCodeSnapshot =
                        value.PackageContentUnitCodeSnapshot,
                    ConversionRuleRevisionSnapshot =
                        value.ConversionRuleRevisionSnapshot,
                    UnitPriceSnapshot = value.UnitPriceSnapshot,
                    CurrencyCodeSnapshot = value.CurrencyCodeSnapshot,
                    RequestedOrderUnitQuantity = value.RequestedOrderUnitQuantity,
                    RequestedContentQuantitySnapshot =
                        value.RequestedContentQuantitySnapshot,
                    AcceptedOrderUnitQuantity = value.AcceptedOrderUnitQuantity,
                    LineAmountSnapshot = value.LineAmountSnapshot,
                    Revision = value.Revision,
                }));
    }

    private static string SourcesJson(IEnumerable<string>? sources)
        => JsonSerializer.Serialize((sources ?? Array.Empty<string>())
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal));
}
