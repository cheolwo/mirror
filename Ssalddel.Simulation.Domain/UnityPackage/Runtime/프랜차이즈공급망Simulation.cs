using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;
using Ssalddel.Simulation.Contracts;

namespace Ssalddel.Simulation.Domain
{
    public sealed partial class 경영SimulationSessionAggregate
    {
        private 프랜차이즈공급망StateSnapshot? franchiseSupplyState;
        private readonly Dictionary<string, AppliedFranchiseSupplyCommand>
            appliedFranchiseSupplyCommands = new(StringComparer.Ordinal);

        public 프랜차이즈공급망StateSnapshot? GetFranchiseSupplyState()
        {
            lock (gate) return CloneFranchiseSupplyState(franchiseSupplyState);
        }

        public 프랜차이즈공급망PreviewSnapshot PreviewFranchiseSupplyInitialization(
            프랜차이즈공급망초기화PreviewRequest request)
        {
            lock (gate) return BuildInitializationPreview(request);
        }

        public 프랜차이즈공급망StateSnapshot ConfirmFranchiseSupplyInitialization(
            프랜차이즈공급망CommandConfirmRequest<
                프랜차이즈공급망초기화PreviewRequest> request)
            => ConfirmFranchiseSupplyCommand(request,
                프랜차이즈공급망SimulationContractCodes.Initialization,
                BuildInitializationPreview,
                CreateInitialFranchiseSupplyState,
                BuildInitializationCanonical);

        public 프랜차이즈공급망PreviewSnapshot PreviewFranchiseSourcingAgreement(
            프랜차이즈조달계약PreviewRequest request)
        {
            lock (gate) return BuildSourcingAgreementPreview(request);
        }

        public 프랜차이즈공급망StateSnapshot ConfirmFranchiseSourcingAgreement(
            프랜차이즈공급망CommandConfirmRequest<
                프랜차이즈조달계약PreviewRequest> request)
            => ConfirmFranchiseSupplyCommand(request,
                프랜차이즈공급망SimulationContractCodes.SourcingAgreement,
                BuildSourcingAgreementPreview,
                ApplySourcingAgreement,
                BuildSourcingAgreementCanonical);

        public 프랜차이즈공급망PreviewSnapshot PreviewFranchiseStoreSupplyOffer(
            프랜차이즈매장공급안PreviewRequest request)
        {
            lock (gate) return BuildStoreSupplyOfferPreview(request);
        }

        public 프랜차이즈공급망StateSnapshot ConfirmFranchiseStoreSupplyOffer(
            프랜차이즈공급망CommandConfirmRequest<
                프랜차이즈매장공급안PreviewRequest> request)
            => ConfirmFranchiseSupplyCommand(request,
                프랜차이즈공급망SimulationContractCodes.StoreSupplyOffer,
                BuildStoreSupplyOfferPreview,
                ApplyStoreSupplyOffer,
                BuildStoreSupplyOfferCanonical);

        public 프랜차이즈공급망PreviewSnapshot PreviewFranchiseStoreOrder(
            프랜차이즈매장발주PreviewRequest request)
        {
            lock (gate) return BuildStoreOrderPreview(request);
        }

        public 프랜차이즈공급망StateSnapshot ConfirmFranchiseStoreOrder(
            프랜차이즈공급망CommandConfirmRequest<
                프랜차이즈매장발주PreviewRequest> request)
            => ConfirmFranchiseSupplyCommand(request,
                프랜차이즈공급망SimulationContractCodes.StoreOrder,
                BuildStoreOrderPreview,
                ApplyStoreOrder,
                BuildStoreOrderCanonical);

        private 프랜차이즈공급망StateSnapshot ConfirmFranchiseSupplyCommand<TPreview>(
            프랜차이즈공급망CommandConfirmRequest<TPreview> request,
            string actionCode,
            Func<TPreview, 프랜차이즈공급망PreviewSnapshot> previewFactory,
            Func<TPreview, string, 프랜차이즈공급망StateSnapshot> mutation,
            Func<TPreview, string> requestCanonical)
        {
            if (request == null || request.PreviewRequest == null)
                throw new ArgumentNullException(nameof(request));
            lock (gate)
            {
                var clientRequestId = request.ClientRequestId?.Trim()
                    ?? string.Empty;
                var expectedPreviewHash = request.ExpectedPreviewHash?.Trim()
                    ?? string.Empty;
                var previewRequest = CloneFranchiseSupplyRequest(
                    request.PreviewRequest);
                RequireProjectionStableId(clientRequestId, 200,
                    "FranchiseSupplyClientRequestIdInvalid");
                if (!IsFranchiseSupplySha256(expectedPreviewHash))
                    throw new SimulationContractException(
                        "FranchiseSupplyExpectedPreviewHashInvalid");
                var signature = FranchiseSupplySha256(Canonical(actionCode,
                    clientRequestId, expectedPreviewHash,
                    requestCanonical(previewRequest)));
                if (appliedFranchiseSupplyCommands.TryGetValue(clientRequestId,
                        out var applied))
                {
                    if (!string.Equals(signature, applied.Signature,
                            StringComparison.Ordinal))
                        throw new SimulationConflictException(
                            "FranchiseSupplyCommandPayloadConflict");
                    return CloneFranchiseSupplyState(applied.State)!;
                }
                if (appliedCommands.ContainsKey(clientRequestId)
                    || HasAppliedDecisionCommand(clientRequestId)
                    || HasDifferentKindCommand(clientRequestId))
                    throw new SimulationConflictException(
                        "SimulationCommandKindConflict");

                var preview = previewFactory(previewRequest);
                if (!string.Equals(preview.PreviewHash,
                        expectedPreviewHash,
                        StringComparison.Ordinal))
                    throw new SimulationConflictException(
                        "FranchiseSupplyPreviewMismatch");
                if (!preview.CanConfirm)
                    throw new SimulationConflictException(
                        preview.BlockReasonCodes.FirstOrDefault()
                        ?? "FranchiseSupplyCommandBlocked");

                var next = mutation(previewRequest, clientRequestId);
                Revision++;
                next.SourceSessionRevision = Revision;
                next.ProjectionRevision = (franchiseSupplyState?.ProjectionRevision ?? 0) + 1;
                next.AsOfWorldTick = CurrentTick;
                franchiseSupplyState = CloneFranchiseSupplyState(next);
                var result = CloneFranchiseSupplyState(next)!;
                appliedFranchiseSupplyCommands.Add(clientRequestId,
                    new AppliedFranchiseSupplyCommand(signature, result));
                AppendFranchiseSupplyTransition(clientRequestId, signature, result);
                return CloneFranchiseSupplyState(result)!;
            }
        }

        internal void ReplayFranchiseSupplyTransition(
            string clientRequestId,
            string commandSignature,
            프랜차이즈공급망StateSnapshot state)
        {
            RequireStableId(clientRequestId,
                "FranchiseSupplyClientRequestIdInvalid");
            if (!IsFranchiseSupplySha256(commandSignature) || state == null)
                throw new SimulationContractException(
                    "FranchiseSupplyReplayRecordInvalid");
            ValidateFranchiseSupplyState(state);
            lock (gate)
            {
                var normalizedCommandId = clientRequestId.Trim();
                if (appliedFranchiseSupplyCommands.ContainsKey(
                        normalizedCommandId)
                    || appliedCommands.ContainsKey(normalizedCommandId)
                    || HasAppliedDecisionCommand(normalizedCommandId)
                    || HasDifferentKindCommand(normalizedCommandId))
                    throw new SimulationConflictException(
                        "SimulationCommandKindConflict");
                if (state.SourceSessionRevision != Revision + 1)
                    throw new SimulationConflictException(
                        "FranchiseSupplyReplayRevisionMismatch");
                Revision++;
                franchiseSupplyState = CloneFranchiseSupplyState(state);
                appliedFranchiseSupplyCommands.Add(normalizedCommandId,
                    new AppliedFranchiseSupplyCommand(commandSignature.Trim(), state));
                AppendFranchiseSupplyTransition(clientRequestId,
                    commandSignature, state);
            }
        }

        private void AppendFranchiseSupplyTransition(
            string clientRequestId,
            string commandSignature,
            프랜차이즈공급망StateSnapshot state)
            => commandLog.Add(new SimulationCommandLogEntrySnapshot
            {
                Sequence = commandLog.Count + 1L,
                CommandTypeCode = SimulationCommandTypeCodes
                    .FranchiseSupplyChainStateTransition,
                AppliedWorldTick = CurrentTick,
                ResultingWorldRevision = Revision,
                FranchiseSupplyClientRequestId = clientRequestId.Trim(),
                FranchiseSupplyCommandSignature = commandSignature.Trim(),
                FranchiseSupplyState = CloneFranchiseSupplyState(state),
            });

        private 프랜차이즈공급망PreviewSnapshot BuildInitializationPreview(
            프랜차이즈공급망초기화PreviewRequest request)
        {
            ValidateInitialization(request);
            EnsureFranchiseSupplyExpectedRevision(request.ExpectedRevision);
            var blocks = franchiseSupplyState == null
                ? Array.Empty<string>()
                : new[] { "FranchiseSupplyAlreadyInitialized" };
            return Preview(프랜차이즈공급망SimulationContractCodes.Initialization,
                request.ExpectedRevision, BuildInitializationCanonical(request), blocks);
        }

        private 프랜차이즈공급망PreviewSnapshot BuildSourcingAgreementPreview(
            프랜차이즈조달계약PreviewRequest request)
        {
            ValidateSourcingAgreement(request);
            EnsureFranchiseSupplyExpectedRevision(request.ExpectedRevision);
            var state = RequireFranchiseSupplyState();
            RequireHeadquarters(state, request.HeadquartersStableId);
            RequireSupplier(state, request.SupplierStableId);
            var blocks = new List<string>();
            if (state.SourcingAgreements.Any(value =>
                    value.ContractStableId == request.ContractStableId.Trim()))
                blocks.Add("FranchiseSupplySourcingAgreementAlreadyExists");
            if (state.SourcingAgreements.Any(value =>
                    value.HeadquartersStableId == request.HeadquartersStableId.Trim()
                    && value.ContractNumber == request.ContractNumber.Trim()))
                blocks.Add("FranchiseSupplyContractNumberAlreadyExists");
            if (request.Items.Any(item => state.SourcingAgreementItems.Any(existing =>
                    existing.ContractItemStableId == item.ContractItemStableId.Trim())))
                blocks.Add("FranchiseSupplyContractItemAlreadyExists");
            return Preview(프랜차이즈공급망SimulationContractCodes.SourcingAgreement,
                request.ExpectedRevision, BuildSourcingAgreementCanonical(request),
                blocks.Distinct(StringComparer.Ordinal).ToArray());
        }

        private 프랜차이즈공급망PreviewSnapshot BuildStoreSupplyOfferPreview(
            프랜차이즈매장공급안PreviewRequest request)
        {
            ValidateStoreSupplyOffer(request);
            EnsureFranchiseSupplyExpectedRevision(request.ExpectedRevision);
            var state = RequireFranchiseSupplyState();
            RequireHeadquarters(state, request.HeadquartersStableId);
            foreach (var item in request.Items)
            {
                var source = state.SourcingAgreementItems.SingleOrDefault(value =>
                    value.ContractStableId == item.SourceContractStableId.Trim()
                    && value.ContractItemStableId ==
                        item.SourceContractItemStableId.Trim());
                if (source == null
                    || source.HeadquartersStableId !=
                        request.HeadquartersStableId.Trim())
                    throw new SimulationNotFoundException(
                        "FranchiseSupplySourcingItemNotFound");
                var contract = state.SourcingAgreements.Single(value =>
                    value.ContractStableId == source.ContractStableId);
                if (contract.StatusCode != 프랜차이즈공급망SimulationCodes.계약활성
                    || contract.CurrencyCode != request.CurrencyCode.Trim()
                    || request.EffectiveFromWorldTick <
                        contract.EffectiveFromWorldTick
                    || contract.EffectiveUntilWorldTick.HasValue
                    && (!request.EffectiveUntilWorldTick.HasValue
                        || request.EffectiveUntilWorldTick.Value >
                            contract.EffectiveUntilWorldTick.Value))
                    throw new SimulationConflictException(
                        "FranchiseSupplySourcingAgreementNotEligibleForOffer");
            }
            var blocks = new List<string>();
            if (state.StoreSupplyOffers.Any(value =>
                    value.OfferStableId == request.OfferStableId.Trim()))
                blocks.Add("FranchiseSupplyStoreOfferAlreadyExists");
            if (state.StoreSupplyOffers.Any(value =>
                    value.HeadquartersStableId == request.HeadquartersStableId.Trim()
                    && value.OfferNumber == request.OfferNumber.Trim()))
                blocks.Add("FranchiseSupplyOfferNumberAlreadyExists");
            if (request.Items.Any(item => state.StoreSupplyOfferItems.Any(existing =>
                    existing.OfferItemStableId == item.OfferItemStableId.Trim())))
                blocks.Add("FranchiseSupplyOfferItemAlreadyExists");
            return Preview(프랜차이즈공급망SimulationContractCodes.StoreSupplyOffer,
                request.ExpectedRevision, BuildStoreSupplyOfferCanonical(request),
                blocks.Distinct(StringComparer.Ordinal).ToArray());
        }

        private 프랜차이즈공급망PreviewSnapshot BuildStoreOrderPreview(
            프랜차이즈매장발주PreviewRequest request)
        {
            ValidateStoreOrder(request);
            EnsureFranchiseSupplyExpectedRevision(request.ExpectedRevision);
            var state = RequireFranchiseSupplyState();
            var membership = state.StoreMemberships.SingleOrDefault(value =>
                value.MembershipStableId == request.StoreMembershipStableId.Trim())
                ?? throw new SimulationNotFoundException(
                    "FranchiseSupplyStoreMembershipNotFound");
            var offer = state.StoreSupplyOffers.SingleOrDefault(value =>
                value.OfferStableId == request.OfferStableId.Trim())
                ?? throw new SimulationNotFoundException(
                    "FranchiseSupplyStoreOfferNotFound");
            if (membership.HeadquartersStableId != offer.HeadquartersStableId)
                throw new SimulationConflictException(
                    "FranchiseSupplyStoreOfferHeadquartersMismatch");
            var store = state.Stores.Single(value =>
                value.StoreStableId == membership.StoreStableId);
            if (!string.Equals(store.SemanticPlaceStableId,
                    request.DeliverySiteStableId.Trim(),
                    StringComparison.Ordinal))
                throw new SimulationConflictException(
                    "FranchiseSupplyDeliverySiteStoreMismatch");
            if (membership.StatusCode != 프랜차이즈공급망SimulationCodes.활성
                || membership.EffectiveFromWorldTick > CurrentTick
                || membership.EffectiveUntilWorldTick.HasValue
                && membership.EffectiveUntilWorldTick.Value <
                    request.RequestedDeliveryWorldTick)
                throw new SimulationConflictException(
                    "FranchiseSupplyStoreMembershipInactive");
            if (offer.StatusCode != 프랜차이즈공급망SimulationCodes.계약활성
                || offer.EffectiveFromWorldTick > CurrentTick
                || offer.EffectiveUntilWorldTick.HasValue
                && offer.EffectiveUntilWorldTick.Value <
                    request.RequestedDeliveryWorldTick)
                throw new SimulationConflictException(
                    "FranchiseSupplyStoreOfferInactive");
            foreach (var line in request.Lines)
            {
                var item = state.StoreSupplyOfferItems.SingleOrDefault(value =>
                    value.OfferStableId == offer.OfferStableId
                    && value.OfferItemStableId == line.OfferItemStableId.Trim())
                    ?? throw new SimulationNotFoundException(
                        "FranchiseSupplyStoreOfferItemNotFound");
                if (line.RequestedOrderUnitQuantity < item.MinimumOrderQuantity
                    || item.MaximumOrderQuantity.HasValue
                    && line.RequestedOrderUnitQuantity >
                        item.MaximumOrderQuantity.Value)
                    throw new SimulationConflictException(
                        "FranchiseSupplyStoreOrderQuantityOutsideOffer");
                MultiplyProjectionDecimal(line.RequestedOrderUnitQuantity,
                    item.PackageContentQuantity, 4,
                    "FranchiseSupplyRequestedContentQuantityInvalid");
                MultiplyProjectionDecimal(line.RequestedOrderUnitQuantity,
                    item.UnitPrice, 2,
                    "FranchiseSupplyLineAmountInvalid");
            }
            SumProjectionDecimals(request.Lines.Select(line =>
                {
                    var item = state.StoreSupplyOfferItems.Single(value =>
                        value.OfferStableId == offer.OfferStableId
                        && value.OfferItemStableId ==
                            line.OfferItemStableId.Trim());
                    return MultiplyProjectionDecimal(
                        line.RequestedOrderUnitQuantity, item.UnitPrice, 2,
                        "FranchiseSupplyLineAmountInvalid");
                }), 2, "FranchiseSupplyOrderTotalAmountInvalid");
            var blocks = new List<string>();
            if (state.StoreOrders.Any(value =>
                    value.OrderStableId == request.OrderStableId.Trim()))
                blocks.Add("FranchiseSupplyStoreOrderAlreadyExists");
            if (state.StoreOrders.Any(value =>
                    value.StoreMembershipStableId == membership.MembershipStableId
                    && value.OrderNumber == request.OrderNumber.Trim()))
                blocks.Add("FranchiseSupplyOrderNumberAlreadyExists");
            if (request.Lines.Any(line => state.StoreOrderLines.Any(existing =>
                    existing.OrderLineStableId == line.OrderLineStableId.Trim())))
                blocks.Add("FranchiseSupplyOrderLineAlreadyExists");
            return Preview(프랜차이즈공급망SimulationContractCodes.StoreOrder,
                request.ExpectedRevision, BuildStoreOrderCanonical(request),
                blocks.Distinct(StringComparer.Ordinal).ToArray());
        }

        private 프랜차이즈공급망PreviewSnapshot Preview(
            string actionCode, long expectedRevision, string requestCanonical,
            string[] blocks)
        {
            var requestHash = FranchiseSupplySha256(requestCanonical);
            return new 프랜차이즈공급망PreviewSnapshot
            {
                ActionCode = actionCode,
                ExpectedRevision = expectedRevision,
                CanConfirm = blocks.Length == 0,
                BlockReasonCodes = blocks,
                RequestPayloadHashSha256 = requestHash,
                PreviewHash = FranchiseSupplySha256(Canonical(
                    프랜차이즈공급망SimulationContractCodes.SchemaVersion,
                    SessionStableId, actionCode, expectedRevision, requestHash,
                    string.Join(",", blocks))),
            };
        }

        private 프랜차이즈공급망StateSnapshot CreateInitialFranchiseSupplyState(
            프랜차이즈공급망초기화PreviewRequest request, string _)
        {
            var resultingRevision = Revision + 1;
            var headquarters = request.Headquarters
                ?? throw new SimulationContractException(
                    "FranchiseSupplyHeadquartersMissing");
            return new 프랜차이즈공급망StateSnapshot
            {
                NetworkStableId = request.NetworkStableId.Trim(),
                ScenarioStableId = ScenarioStableId,
                DataRevision = request.DataRevision.Trim(),
                SourceStableIds = NormalizeSources(request.SourceStableIds),
                Headquarters = new[]
                {
                    new 프랜차이즈본부Snapshot
                    {
                        HeadquartersStableId = headquarters.HeadquartersStableId.Trim(),
                        HeadquartersCode = headquarters.HeadquartersCode.Trim(),
                        DisplayName = headquarters.DisplayName.Trim(),
                        Revision = resultingRevision,
                    },
                },
                Stores = request.Stores.OrderBy(value => value.StoreStableId,
                    StringComparer.Ordinal).Select(value =>
                    new 프랜차이즈매장Snapshot
                    {
                        StoreStableId = value.StoreStableId.Trim(),
                        StoreCode = value.StoreCode.Trim(),
                        DisplayName = value.DisplayName.Trim(),
                        SemanticPlaceStableId = value.SemanticPlaceStableId.Trim(),
                        Revision = resultingRevision,
                    }).ToArray(),
                StoreMemberships = request.Stores.OrderBy(value =>
                    value.MembershipStableId, StringComparer.Ordinal).Select(value =>
                    new 프랜차이즈매장소속Snapshot
                    {
                        MembershipStableId = value.MembershipStableId.Trim(),
                        HeadquartersStableId = headquarters.HeadquartersStableId.Trim(),
                        StoreStableId = value.StoreStableId.Trim(),
                        EffectiveFromWorldTick = CurrentTick,
                        Revision = resultingRevision,
                    }).ToArray(),
                Suppliers = request.Suppliers.OrderBy(value =>
                    value.SupplierStableId, StringComparer.Ordinal).Select(value =>
                    new 프랜차이즈공급자Snapshot
                    {
                        SupplierStableId = value.SupplierStableId.Trim(),
                        SupplierKey = value.SupplierKey.Trim(),
                        DisplayName = value.DisplayName.Trim(),
                        SupplierKindCode = value.SupplierKindCode.Trim(),
                        Revision = resultingRevision,
                    }).ToArray(),
            };
        }

        private 프랜차이즈공급망StateSnapshot ApplySourcingAgreement(
            프랜차이즈조달계약PreviewRequest request, string _)
        {
            var state = CloneFranchiseSupplyState(RequireFranchiseSupplyState())!;
            var revision = Revision + 1;
            state.SourcingAgreements = state.SourcingAgreements.Append(
                new 프랜차이즈조달계약Snapshot
                {
                    ContractStableId = request.ContractStableId.Trim(),
                    HeadquartersStableId = request.HeadquartersStableId.Trim(),
                    SupplierStableId = request.SupplierStableId.Trim(),
                    ContractNumber = request.ContractNumber.Trim(),
                    ContractDocumentVersion = request.ContractDocumentVersion.Trim(),
                    EffectiveFromWorldTick = request.EffectiveFromWorldTick,
                    EffectiveUntilWorldTick = request.EffectiveUntilWorldTick,
                    CurrencyCode = request.CurrencyCode.Trim(),
                    Revision = revision,
                }).OrderBy(value => value.ContractStableId,
                    StringComparer.Ordinal).ToArray();
            state.SourcingAgreementItems = state.SourcingAgreementItems.Concat(
                request.Items.Select(value => new 프랜차이즈조달계약품목Snapshot
                {
                    ContractItemStableId = value.ContractItemStableId.Trim(),
                    HeadquartersStableId = request.HeadquartersStableId.Trim(),
                    ContractStableId = request.ContractStableId.Trim(),
                    ProductStableId = value.ProductStableId.Trim(),
                    SupplierSku = value.SupplierSku.Trim(),
                    ItemName = value.ItemName.Trim(),
                    OrderUnitCode = value.OrderUnitCode.Trim(),
                    PackageContentQuantity = value.PackageContentQuantity,
                    PackageContentUnitCode = value.PackageContentUnitCode.Trim(),
                    ConversionRuleRevision = value.ConversionRuleRevision.Trim(),
                    UnitPrice = value.UnitPrice,
                    MinimumOrderQuantity = value.MinimumOrderQuantity,
                    MaximumOrderQuantity = value.MaximumOrderQuantity,
                    StorageConditionCode = value.StorageConditionCode.Trim(),
                    Revision = revision,
                    SourceStableIds = NormalizeSources(value.SourceStableIds
                        .Concat(request.SourceStableIds).ToArray()),
                })).OrderBy(value => value.ContractItemStableId,
                    StringComparer.Ordinal).ToArray();
            state.SourceStableIds = NormalizeSources(state.SourceStableIds
                .Concat(request.SourceStableIds).ToArray());
            return state;
        }

        private 프랜차이즈공급망StateSnapshot ApplyStoreSupplyOffer(
            프랜차이즈매장공급안PreviewRequest request, string _)
        {
            var state = CloneFranchiseSupplyState(RequireFranchiseSupplyState())!;
            var revision = Revision + 1;
            state.StoreSupplyOffers = state.StoreSupplyOffers.Append(
                new 프랜차이즈매장공급안Snapshot
                {
                    OfferStableId = request.OfferStableId.Trim(),
                    HeadquartersStableId = request.HeadquartersStableId.Trim(),
                    OfferNumber = request.OfferNumber.Trim(),
                    OfferDocumentVersion = request.OfferDocumentVersion.Trim(),
                    EffectiveFromWorldTick = request.EffectiveFromWorldTick,
                    EffectiveUntilWorldTick = request.EffectiveUntilWorldTick,
                    CurrencyCode = request.CurrencyCode.Trim(),
                    Revision = revision,
                }).OrderBy(value => value.OfferStableId,
                    StringComparer.Ordinal).ToArray();
            state.StoreSupplyOfferItems = state.StoreSupplyOfferItems.Concat(
                request.Items.Select(value =>
                {
                    var source = state.SourcingAgreementItems.Single(item =>
                        item.ContractStableId == value.SourceContractStableId.Trim()
                        && item.ContractItemStableId ==
                            value.SourceContractItemStableId.Trim());
                    return new 프랜차이즈매장공급안품목Snapshot
                    {
                        OfferItemStableId = value.OfferItemStableId.Trim(),
                        HeadquartersStableId = request.HeadquartersStableId.Trim(),
                        OfferStableId = request.OfferStableId.Trim(),
                        SourceContractStableId = source.ContractStableId,
                        SourceContractItemStableId = source.ContractItemStableId,
                        ProductStableId = source.ProductStableId,
                        StoreSku = value.StoreSku.Trim(),
                        ItemName = source.ItemName,
                        OrderUnitCode = source.OrderUnitCode,
                        PackageContentQuantity = source.PackageContentQuantity,
                        PackageContentUnitCode = source.PackageContentUnitCode,
                        ConversionRuleRevision = source.ConversionRuleRevision,
                        UnitPrice = value.UnitPrice,
                        MinimumOrderQuantity = value.MinimumOrderQuantity,
                        MaximumOrderQuantity = value.MaximumOrderQuantity,
                        StorageConditionCode = source.StorageConditionCode,
                        Revision = revision,
                        SourceStableIds = NormalizeSources(source.SourceStableIds
                            .Concat(value.SourceStableIds)
                            .Concat(request.SourceStableIds).ToArray()),
                    };
                })).OrderBy(value => value.OfferItemStableId,
                    StringComparer.Ordinal).ToArray();
            state.SourceStableIds = NormalizeSources(state.SourceStableIds
                .Concat(request.SourceStableIds).ToArray());
            return state;
        }

        private 프랜차이즈공급망StateSnapshot ApplyStoreOrder(
            프랜차이즈매장발주PreviewRequest request,
            string clientRequestId)
        {
            var state = CloneFranchiseSupplyState(RequireFranchiseSupplyState())!;
            var revision = Revision + 1;
            var membership = state.StoreMemberships.Single(value =>
                value.MembershipStableId == request.StoreMembershipStableId.Trim());
            var offer = state.StoreSupplyOffers.Single(value =>
                value.OfferStableId == request.OfferStableId.Trim());
            var requestHash = FranchiseSupplySha256(BuildStoreOrderCanonical(request));
            var lines = request.Lines.Select(value =>
            {
                var item = state.StoreSupplyOfferItems.Single(candidate =>
                    candidate.OfferStableId == offer.OfferStableId
                    && candidate.OfferItemStableId == value.OfferItemStableId.Trim());
                return new 프랜차이즈매장발주품목Snapshot
                {
                    OrderLineStableId = value.OrderLineStableId.Trim(),
                    HeadquartersStableId = offer.HeadquartersStableId,
                    OfferStableId = offer.OfferStableId,
                    OrderStableId = request.OrderStableId.Trim(),
                    OfferItemStableId = item.OfferItemStableId,
                    ProductStableIdSnapshot = item.ProductStableId,
                    StoreSkuSnapshot = item.StoreSku,
                    ItemNameSnapshot = item.ItemName,
                    OrderUnitCodeSnapshot = item.OrderUnitCode,
                    PackageContentQuantitySnapshot = item.PackageContentQuantity,
                    PackageContentUnitCodeSnapshot = item.PackageContentUnitCode,
                    ConversionRuleRevisionSnapshot = item.ConversionRuleRevision,
                    UnitPriceSnapshot = item.UnitPrice,
                    CurrencyCodeSnapshot = offer.CurrencyCode,
                    RequestedOrderUnitQuantity = value.RequestedOrderUnitQuantity,
                    RequestedContentQuantitySnapshot =
                        MultiplyProjectionDecimal(
                            value.RequestedOrderUnitQuantity,
                            item.PackageContentQuantity, 4,
                            "FranchiseSupplyRequestedContentQuantityInvalid"),
                    LineAmountSnapshot = MultiplyProjectionDecimal(
                        value.RequestedOrderUnitQuantity, item.UnitPrice, 2,
                        "FranchiseSupplyLineAmountInvalid"),
                    Revision = revision,
                };
            }).OrderBy(value => value.OrderLineStableId,
                StringComparer.Ordinal).ToArray();
            state.StoreOrders = state.StoreOrders.Append(
                new 프랜차이즈매장발주Snapshot
                {
                    OrderStableId = request.OrderStableId.Trim(),
                    HeadquartersStableId = offer.HeadquartersStableId,
                    StoreMembershipStableId = membership.MembershipStableId,
                    StoreStableId = membership.StoreStableId,
                    OfferStableId = offer.OfferStableId,
                    ClientRequestStableId = clientRequestId,
                    RequestPayloadHashSha256 = requestHash,
                    OrderNumber = request.OrderNumber.Trim(),
                    OfferDocumentVersionSnapshot = offer.OfferDocumentVersion,
                    SellerHeadquartersStableIdSnapshot =
                        offer.HeadquartersStableId,
                    CommercialFlowModelCodeSnapshot =
                        offer.CommercialFlowModelCode,
                    FulfillmentModelCodeSnapshot = offer.FulfillmentModelCode,
                    DeliverySiteStableIdSnapshot =
                        request.DeliverySiteStableId.Trim(),
                    CurrencyCodeSnapshot = offer.CurrencyCode,
                    RequestedDeliveryWorldTick =
                        request.RequestedDeliveryWorldTick,
                    CreatedWorldTick = CurrentTick,
                    SubmittedWorldTick = CurrentTick,
                    TotalAmountSnapshot = SumProjectionDecimals(lines.Select(
                            value => value.LineAmountSnapshot), 2,
                        "FranchiseSupplyOrderTotalAmountInvalid"),
                    Revision = revision,
                }).OrderBy(value => value.OrderStableId,
                    StringComparer.Ordinal).ToArray();
            state.StoreOrderLines = state.StoreOrderLines.Concat(lines)
                .OrderBy(value => value.OrderLineStableId,
                    StringComparer.Ordinal).ToArray();
            state.SourceStableIds = NormalizeSources(state.SourceStableIds
                .Concat(request.SourceStableIds).ToArray());
            return state;
        }

        private void EnsureFranchiseSupplyExpectedRevision(long expectedRevision)
        {
            if (expectedRevision < 0)
                throw new SimulationContractException(
                    "SimulationExpectedRevisionInvalid");
            if (expectedRevision != Revision)
                throw new SimulationConflictException(
                    "SimulationExpectedRevisionMismatch");
        }

        private 프랜차이즈공급망StateSnapshot RequireFranchiseSupplyState()
            => franchiseSupplyState ?? throw new SimulationNotFoundException(
                "FranchiseSupplyNetworkNotInitialized");

        private static void RequireHeadquarters(
            프랜차이즈공급망StateSnapshot state, string stableId)
        {
            if (!state.Headquarters.Any(value =>
                    value.HeadquartersStableId == stableId.Trim()))
                throw new SimulationNotFoundException(
                    "FranchiseSupplyHeadquartersNotFound");
        }

        private static void RequireSupplier(
            프랜차이즈공급망StateSnapshot state, string stableId)
        {
            if (!state.Suppliers.Any(value =>
                    value.SupplierStableId == stableId.Trim()))
                throw new SimulationNotFoundException(
                    "FranchiseSupplySupplierNotFound");
        }

        private void ValidateInitialization(
            프랜차이즈공급망초기화PreviewRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            RequireProjectionStableId(SessionStableId, 200,
                "FranchiseSupplySessionStableIdInvalid");
            RequireProjectionStableId(ScenarioStableId, 200,
                "FranchiseSupplyScenarioStableIdInvalid");
            RequireProjectionStableId(request.NetworkStableId, 200,
                "FranchiseSupplyNetworkStableIdInvalid");
            RequireProjectionText(request.DataRevision, 120,
                "FranchiseSupplyDataRevisionInvalid");
            if (request.Headquarters == null)
                throw new SimulationContractException(
                    "FranchiseSupplyHeadquartersMissing");
            ValidateIdentity(request.Headquarters.HeadquartersStableId, 200,
                request.Headquarters.HeadquartersCode,
                80,
                request.Headquarters.DisplayName,
                200,
                "FranchiseSupplyHeadquartersInvalid");
            if (request.Stores == null || request.Stores.Length == 0
                || request.Suppliers == null || request.Suppliers.Length == 0)
                throw new SimulationContractException(
                    "FranchiseSupplyParticipantsMissing");
            foreach (var store in request.Stores)
            {
                if (store == null) throw new SimulationContractException(
                    "FranchiseSupplyStoreInvalid");
                ValidateIdentity(store.StoreStableId, 200, store.StoreCode,
                    80, store.DisplayName, 200,
                    "FranchiseSupplyStoreInvalid");
                RequireProjectionStableId(store.SemanticPlaceStableId, 200,
                    "FranchiseSupplyStorePlaceInvalid");
                RequireProjectionStableId(store.MembershipStableId, 200,
                    "FranchiseSupplyStoreMembershipInvalid");
            }
            foreach (var supplier in request.Suppliers)
            {
                if (supplier == null) throw new SimulationContractException(
                    "FranchiseSupplySupplierInvalid");
                ValidateIdentity(supplier.SupplierStableId, 200,
                    supplier.SupplierKey, 160, supplier.DisplayName, 200,
                    "FranchiseSupplySupplierInvalid");
                RequireProjectionText(supplier.SupplierKindCode, 60,
                    "FranchiseSupplySupplierKindInvalid");
            }
            EnsureDistinct(request.Stores.Select(value => value.StoreStableId),
                "FranchiseSupplyStoreDuplicate");
            EnsureDistinct(request.Stores.Select(value => value.StoreCode),
                "FranchiseSupplyStoreCodeDuplicate");
            EnsureDistinct(request.Stores.Select(value => value.MembershipStableId),
                "FranchiseSupplyMembershipDuplicate");
            EnsureDistinct(request.Suppliers.Select(value => value.SupplierStableId),
                "FranchiseSupplySupplierDuplicate");
            EnsureDistinct(request.Suppliers.Select(value => value.SupplierKey),
                "FranchiseSupplySupplierKeyDuplicate");
            ValidateSources(request.SourceStableIds);
        }

        private static void ValidateSourcingAgreement(
            프랜차이즈조달계약PreviewRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            RequireProjectionStableId(request.ContractStableId, 200,
                "FranchiseSupplyContractStableIdInvalid");
            RequireProjectionStableId(request.HeadquartersStableId, 200,
                "FranchiseSupplyHeadquartersStableIdInvalid");
            RequireProjectionStableId(request.SupplierStableId, 200,
                "FranchiseSupplySupplierStableIdInvalid");
            RequireProjectionText(request.ContractNumber, 100,
                "FranchiseSupplyContractNumberInvalid");
            RequireProjectionText(request.ContractDocumentVersion, 100,
                "FranchiseSupplyContractDocumentVersionInvalid");
            ValidateEffectivePeriod(request.EffectiveFromWorldTick,
                request.EffectiveUntilWorldTick);
            ValidateCurrency(request.CurrencyCode);
            if (request.Items == null || request.Items.Length == 0)
                throw new SimulationContractException(
                    "FranchiseSupplyContractItemsMissing");
            foreach (var item in request.Items)
            {
                if (item == null) throw new SimulationContractException(
                    "FranchiseSupplyContractItemInvalid");
                RequireProjectionStableId(item.ContractItemStableId, 200,
                    "FranchiseSupplyContractItemStableIdInvalid");
                RequireProjectionStableId(item.ProductStableId, 200,
                    "FranchiseSupplyProductStableIdInvalid");
                RequireProjectionText(item.SupplierSku, 120,
                    "FranchiseSupplySupplierSkuInvalid");
                RequireProjectionText(item.ItemName, 200,
                    "FranchiseSupplyItemNameInvalid");
                ValidatePack(item.OrderUnitCode,
                    item.PackageContentQuantity,
                    item.PackageContentUnitCode,
                    item.ConversionRuleRevision,
                    item.UnitPrice,
                    item.MinimumOrderQuantity,
                    item.MaximumOrderQuantity,
                    item.StorageConditionCode);
                ValidateSources(item.SourceStableIds);
            }
            EnsureDistinct(request.Items.Select(value =>
                value.ContractItemStableId),
                "FranchiseSupplyContractItemDuplicate");
            EnsureDistinct(request.Items.Select(value => value.SupplierSku),
                "FranchiseSupplySupplierSkuDuplicate");
            ValidateSources(request.SourceStableIds);
        }

        private static void ValidateStoreSupplyOffer(
            프랜차이즈매장공급안PreviewRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            RequireProjectionStableId(request.OfferStableId, 200,
                "FranchiseSupplyOfferStableIdInvalid");
            RequireProjectionStableId(request.HeadquartersStableId, 200,
                "FranchiseSupplyHeadquartersStableIdInvalid");
            RequireProjectionText(request.OfferNumber, 100,
                "FranchiseSupplyOfferNumberInvalid");
            RequireProjectionText(request.OfferDocumentVersion, 100,
                "FranchiseSupplyOfferDocumentVersionInvalid");
            ValidateEffectivePeriod(request.EffectiveFromWorldTick,
                request.EffectiveUntilWorldTick);
            ValidateCurrency(request.CurrencyCode);
            if (request.Items == null || request.Items.Length == 0)
                throw new SimulationContractException(
                    "FranchiseSupplyOfferItemsMissing");
            foreach (var item in request.Items)
            {
                if (item == null) throw new SimulationContractException(
                    "FranchiseSupplyOfferItemInvalid");
                RequireProjectionStableId(item.OfferItemStableId, 200,
                    "FranchiseSupplyOfferItemStableIdInvalid");
                RequireProjectionStableId(item.SourceContractStableId, 200,
                    "FranchiseSupplyContractStableIdInvalid");
                RequireProjectionStableId(item.SourceContractItemStableId, 200,
                    "FranchiseSupplyContractItemStableIdInvalid");
                RequireProjectionText(item.StoreSku, 120,
                    "FranchiseSupplyStoreSkuInvalid");
                if (item.UnitPrice < 0 || item.MinimumOrderQuantity <= 0
                    || item.MaximumOrderQuantity.HasValue
                    && item.MaximumOrderQuantity < item.MinimumOrderQuantity)
                    throw new SimulationContractException(
                        "FranchiseSupplyOfferQuantityOrPriceInvalid");
                ValidateProjectionDecimal(item.UnitPrice, 2,
                    "FranchiseSupplyOfferQuantityOrPriceInvalid");
                ValidateProjectionDecimal(item.MinimumOrderQuantity, 4,
                    "FranchiseSupplyOfferQuantityOrPriceInvalid");
                if (item.MaximumOrderQuantity.HasValue)
                    ValidateProjectionDecimal(item.MaximumOrderQuantity.Value, 4,
                        "FranchiseSupplyOfferQuantityOrPriceInvalid");
                ValidateSources(item.SourceStableIds);
            }
            EnsureDistinct(request.Items.Select(value => value.OfferItemStableId),
                "FranchiseSupplyOfferItemDuplicate");
            EnsureDistinct(request.Items.Select(value => value.StoreSku),
                "FranchiseSupplyStoreSkuDuplicate");
            ValidateSources(request.SourceStableIds);
        }

        private void ValidateStoreOrder(
            프랜차이즈매장발주PreviewRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            RequireProjectionStableId(request.OrderStableId, 200,
                "FranchiseSupplyOrderStableIdInvalid");
            RequireProjectionStableId(request.StoreMembershipStableId, 200,
                "FranchiseSupplyStoreMembershipInvalid");
            RequireProjectionStableId(request.OfferStableId, 200,
                "FranchiseSupplyOfferStableIdInvalid");
            RequireProjectionText(request.OrderNumber, 100,
                "FranchiseSupplyOrderNumberInvalid");
            RequireProjectionStableId(request.DeliverySiteStableId, 200,
                "FranchiseSupplyDeliverySiteInvalid");
            if (request.RequestedDeliveryWorldTick < CurrentTick
                || request.RequestedDeliveryWorldTick > DurationTicks)
                throw new SimulationContractException(
                    "FranchiseSupplyRequestedDeliveryTickInvalid");
            if (request.Lines == null || request.Lines.Length == 0)
                throw new SimulationContractException(
                    "FranchiseSupplyOrderLinesMissing");
            foreach (var line in request.Lines)
            {
                if (line == null) throw new SimulationContractException(
                    "FranchiseSupplyOrderLineInvalid");
                RequireProjectionStableId(line.OrderLineStableId, 200,
                    "FranchiseSupplyOrderLineStableIdInvalid");
                RequireProjectionStableId(line.OfferItemStableId, 200,
                    "FranchiseSupplyOfferItemStableIdInvalid");
                if (line.RequestedOrderUnitQuantity <= 0)
                    throw new SimulationContractException(
                        "FranchiseSupplyOrderQuantityInvalid");
                ValidateProjectionDecimal(line.RequestedOrderUnitQuantity, 4,
                    "FranchiseSupplyOrderQuantityInvalid");
            }
            EnsureDistinct(request.Lines.Select(value => value.OrderLineStableId),
                "FranchiseSupplyOrderLineDuplicate");
            EnsureDistinct(request.Lines.Select(value => value.OfferItemStableId),
                "FranchiseSupplyOfferItemDuplicate");
            ValidateSources(request.SourceStableIds);
        }

        private static void ValidateIdentity(string stableId, int stableIdLength,
            string code, int codeLength, string displayName,
            int displayNameLength, string errorCode)
        {
            RequireProjectionStableId(stableId, stableIdLength, errorCode);
            RequireProjectionText(code, codeLength, errorCode);
            RequireProjectionText(displayName, displayNameLength, errorCode);
        }

        private static void ValidatePack(string orderUnit,
            decimal packageQuantity, string contentUnit, string conversionRevision,
            decimal unitPrice, decimal minimum, decimal? maximum, string storage)
        {
            RequireProjectionText(orderUnit, 40,
                "FranchiseSupplyOrderUnitInvalid");
            RequireProjectionText(contentUnit, 40,
                "FranchiseSupplyContentUnitInvalid");
            RequireProjectionText(conversionRevision, 120,
                "FranchiseSupplyConversionRevisionInvalid");
            RequireProjectionText(storage, 60,
                "FranchiseSupplyStorageConditionInvalid");
            if (packageQuantity <= 0 || unitPrice < 0 || minimum <= 0
                || maximum.HasValue && maximum < minimum)
                throw new SimulationContractException(
                    "FranchiseSupplyPackQuantityOrPriceInvalid");
            ValidateProjectionDecimal(packageQuantity, 4,
                "FranchiseSupplyPackQuantityOrPriceInvalid");
            ValidateProjectionDecimal(unitPrice, 2,
                "FranchiseSupplyPackQuantityOrPriceInvalid");
            ValidateProjectionDecimal(minimum, 4,
                "FranchiseSupplyPackQuantityOrPriceInvalid");
            if (maximum.HasValue)
                ValidateProjectionDecimal(maximum.Value, 4,
                    "FranchiseSupplyPackQuantityOrPriceInvalid");
        }

        private static void ValidateEffectivePeriod(int from, int? until)
        {
            if (from < 0 || until.HasValue && until < from)
                throw new SimulationContractException(
                    "FranchiseSupplyEffectivePeriodInvalid");
        }

        private static void ValidateSources(string[]? sources)
        {
            if (sources == null || sources.Any(string.IsNullOrWhiteSpace))
                throw new SimulationContractException(
                    "FranchiseSupplySourceStableIdsInvalid");
            EnsureDistinct(sources,
                "FranchiseSupplySourceStableIdsDuplicate");
            foreach (var source in sources)
                RequireProjectionStableId(source, 200,
                    "FranchiseSupplySourceStableIdsInvalid");
        }

        private static void ValidateCurrency(string value)
        {
            var normalized = value?.Trim() ?? string.Empty;
            if (normalized.Length != 3 || normalized.Any(character =>
                    character < 'A' || character > 'Z'))
                throw new SimulationContractException(
                    "FranchiseSupplyCurrencyInvalid");
        }

        private static void RequireProjectionStableId(string value,
            int maxLength, string errorCode)
        {
            var normalized = value?.Trim() ?? string.Empty;
            if (normalized.Length == 0 || normalized.Length > maxLength
                || normalized.Any(character => character > 0x7f))
                throw new SimulationContractException(errorCode);
        }

        private static void RequireProjectionText(string value,
            int maxLength, string errorCode)
        {
            var normalized = value?.Trim() ?? string.Empty;
            if (normalized.Length == 0 || normalized.Length > maxLength)
                throw new SimulationContractException(errorCode);
        }

        private static void ValidateProjectionDecimal(decimal value,
            int scale, string errorCode)
        {
            var maximum = scale == 2
                ? 9999999999999999.99m
                : 99999999999999.9999m;
            if (value < -maximum || value > maximum
                || decimal.Round(value, scale, MidpointRounding.ToEven) != value)
                throw new SimulationContractException(errorCode);
        }

        private static decimal MultiplyProjectionDecimal(decimal left,
            decimal right, int scale, string errorCode)
        {
            try
            {
                var result = checked(left * right);
                ValidateProjectionDecimal(result, scale, errorCode);
                return result;
            }
            catch (OverflowException)
            {
                throw new SimulationContractException(errorCode);
            }
        }

        private static decimal SumProjectionDecimals(
            IEnumerable<decimal> values, int scale, string errorCode)
        {
            try
            {
                var result = 0m;
                foreach (var value in values) result = checked(result + value);
                ValidateProjectionDecimal(result, scale, errorCode);
                return result;
            }
            catch (OverflowException)
            {
                throw new SimulationContractException(errorCode);
            }
        }

        private static T CloneFranchiseSupplyRequest<T>(T source)
        {
            var serializer = new DataContractSerializer(typeof(T));
            using var stream = new MemoryStream();
            serializer.WriteObject(stream, source);
            stream.Position = 0;
            return (T)serializer.ReadObject(stream)!;
        }

        private bool HasAppliedFranchiseSupplyCommand(string commandId)
            => appliedFranchiseSupplyCommands.ContainsKey(commandId);

        private static void EnsureDistinct(IEnumerable<string> values,
            string errorCode)
        {
            var normalized = values.Select(value => value?.Trim()
                ?? string.Empty).ToArray();
            if (normalized.Any(string.IsNullOrWhiteSpace)
                || normalized.Distinct(StringComparer.Ordinal).Count()
                    != normalized.Length)
                throw new SimulationContractException(errorCode);
        }

        private static string[] NormalizeSources(IEnumerable<string>? values)
            => (values ?? Array.Empty<string>())
                .Select(value => value?.Trim() ?? string.Empty)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal).ToArray();

        private static bool IsFranchiseSupplySha256(string? value)
            => value?.Length == 64 && value.All(character =>
                character >= '0' && character <= '9'
                || character >= 'a' && character <= 'f');

        private static string FranchiseSupplySha256(string value)
        {
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(
                    Encoding.UTF8.GetBytes(value)))
                .Replace("-", string.Empty).ToLowerInvariant();
        }

        private static string Canonical(params object?[] values)
        {
            var target = new StringBuilder();
            foreach (var value in values)
            {
                var text = Convert.ToString(value,
                    CultureInfo.InvariantCulture) ?? string.Empty;
                target.Append(text.Length.ToString(CultureInfo.InvariantCulture));
                target.Append(':').Append(text).Append('|');
            }
            return target.ToString();
        }

        private static string CanonicalNullable<T>(T? value)
            where T : struct
            => value.HasValue
                ? Canonical(true, value.Value)
                : Canonical(false);

        private static string CanonicalText(string? value)
            => value?.Trim() ?? string.Empty;

        private static string CanonicalStringSequence(
            IEnumerable<string>? values)
        {
            if (values == null) return Canonical(false);
            var materialized = values.ToArray();
            var target = new StringBuilder(Canonical(true,
                materialized.Length));
            foreach (var value in materialized)
                target.Append(Canonical(value != null,
                    value ?? string.Empty));
            return target.ToString();
        }

        private static string BuildInitializationCanonical(
            프랜차이즈공급망초기화PreviewRequest request)
        {
            if (request.Headquarters == null)
                throw new SimulationContractException(
                    "FranchiseSupplyHeadquartersMissing");
            if (request.Stores == null || request.Suppliers == null)
                throw new SimulationContractException(
                    "FranchiseSupplyParticipantsMissing");
            if (request.Stores.Any(value => value == null))
                throw new SimulationContractException(
                    "FranchiseSupplyStoreInvalid");
            if (request.Suppliers.Any(value => value == null))
                throw new SimulationContractException(
                    "FranchiseSupplySupplierInvalid");
            var target = new StringBuilder(Canonical(request.ExpectedRevision,
                CanonicalText(request.NetworkStableId),
                CanonicalText(request.DataRevision),
                CanonicalText(request.Headquarters.HeadquartersStableId),
                CanonicalText(request.Headquarters.HeadquartersCode),
                CanonicalText(request.Headquarters.DisplayName)));
            foreach (var value in request.Stores.OrderBy(value =>
                         value.StoreStableId, StringComparer.Ordinal))
                target.Append(Canonical(CanonicalText(value.StoreStableId),
                    CanonicalText(value.StoreCode),
                    CanonicalText(value.DisplayName),
                    CanonicalText(value.SemanticPlaceStableId),
                    CanonicalText(value.MembershipStableId)));
            foreach (var value in request.Suppliers.OrderBy(value =>
                         value.SupplierStableId, StringComparer.Ordinal))
                target.Append(Canonical(CanonicalText(value.SupplierStableId),
                    CanonicalText(value.SupplierKey),
                    CanonicalText(value.DisplayName),
                    CanonicalText(value.SupplierKindCode)));
            target.Append(CanonicalStringSequence(
                NormalizeSources(request.SourceStableIds)));
            return target.ToString();
        }

        private static string BuildSourcingAgreementCanonical(
            프랜차이즈조달계약PreviewRequest request)
        {
            if (request.Items == null)
                throw new SimulationContractException(
                    "FranchiseSupplyContractItemsMissing");
            if (request.Items.Any(value => value == null))
                throw new SimulationContractException(
                    "FranchiseSupplyContractItemInvalid");
            var target = new StringBuilder(Canonical(request.ExpectedRevision,
                CanonicalText(request.ContractStableId),
                CanonicalText(request.HeadquartersStableId),
                CanonicalText(request.SupplierStableId),
                CanonicalText(request.ContractNumber),
                CanonicalText(request.ContractDocumentVersion),
                request.EffectiveFromWorldTick,
                CanonicalNullable(request.EffectiveUntilWorldTick),
                CanonicalText(request.CurrencyCode)));
            foreach (var item in request.Items.OrderBy(value =>
                         value.ContractItemStableId, StringComparer.Ordinal))
                target.Append(Canonical(
                    CanonicalText(item.ContractItemStableId),
                    CanonicalText(item.ProductStableId),
                    CanonicalText(item.SupplierSku),
                    CanonicalText(item.ItemName),
                    CanonicalText(item.OrderUnitCode),
                    item.PackageContentQuantity,
                    CanonicalText(item.PackageContentUnitCode),
                    CanonicalText(item.ConversionRuleRevision), item.UnitPrice,
                    item.MinimumOrderQuantity,
                    CanonicalNullable(item.MaximumOrderQuantity),
                    CanonicalText(item.StorageConditionCode),
                    CanonicalStringSequence(
                        NormalizeSources(item.SourceStableIds))));
            target.Append(CanonicalStringSequence(
                NormalizeSources(request.SourceStableIds)));
            return target.ToString();
        }

        private static string BuildStoreSupplyOfferCanonical(
            프랜차이즈매장공급안PreviewRequest request)
        {
            if (request.Items == null)
                throw new SimulationContractException(
                    "FranchiseSupplyOfferItemsMissing");
            if (request.Items.Any(value => value == null))
                throw new SimulationContractException(
                    "FranchiseSupplyOfferItemInvalid");
            var target = new StringBuilder(Canonical(request.ExpectedRevision,
                CanonicalText(request.OfferStableId),
                CanonicalText(request.HeadquartersStableId),
                CanonicalText(request.OfferNumber),
                CanonicalText(request.OfferDocumentVersion),
                request.EffectiveFromWorldTick,
                CanonicalNullable(request.EffectiveUntilWorldTick),
                CanonicalText(request.CurrencyCode)));
            foreach (var item in request.Items.OrderBy(value =>
                         value.OfferItemStableId, StringComparer.Ordinal))
                target.Append(Canonical(CanonicalText(item.OfferItemStableId),
                    CanonicalText(item.SourceContractStableId),
                    CanonicalText(item.SourceContractItemStableId),
                    CanonicalText(item.StoreSku),
                    item.UnitPrice, item.MinimumOrderQuantity,
                    CanonicalNullable(item.MaximumOrderQuantity),
                    CanonicalStringSequence(
                        NormalizeSources(item.SourceStableIds))));
            target.Append(CanonicalStringSequence(
                NormalizeSources(request.SourceStableIds)));
            return target.ToString();
        }

        private static string BuildStoreOrderCanonical(
            프랜차이즈매장발주PreviewRequest request)
        {
            if (request.Lines == null)
                throw new SimulationContractException(
                    "FranchiseSupplyOrderLinesMissing");
            if (request.Lines.Any(value => value == null))
                throw new SimulationContractException(
                    "FranchiseSupplyOrderLineInvalid");
            var target = new StringBuilder(Canonical(request.ExpectedRevision,
                CanonicalText(request.OrderStableId),
                CanonicalText(request.StoreMembershipStableId),
                CanonicalText(request.OfferStableId),
                CanonicalText(request.OrderNumber),
                CanonicalText(request.DeliverySiteStableId),
                request.RequestedDeliveryWorldTick));
            foreach (var line in request.Lines.OrderBy(value =>
                         value.OrderLineStableId, StringComparer.Ordinal))
                target.Append(Canonical(CanonicalText(line.OrderLineStableId),
                    CanonicalText(line.OfferItemStableId),
                    line.RequestedOrderUnitQuantity));
            target.Append(CanonicalStringSequence(
                NormalizeSources(request.SourceStableIds)));
            return target.ToString();
        }

        internal static string BuildFranchiseSupplyStatePayloadKey(
            프랜차이즈공급망StateSnapshot? state)
        {
            if (state == null) return string.Empty;
            var target = new StringBuilder(Canonical(state.SchemaVersion,
                state.RuleRevision, state.NetworkStableId,
                state.ScenarioStableId, state.DataRevision,
                state.SourceSessionRevision, state.ProjectionRevision,
                state.AsOfWorldTick, state.SimulationOnly,
                state.IsOperationalState,
                CanonicalStringSequence(state.SourceStableIds)));
            AppendRows(target, state.Headquarters, value => Canonical(
                value.HeadquartersStableId, value.HeadquartersCode,
                value.DisplayName, value.StatusCode, value.Revision));
            AppendRows(target, state.Stores, value => Canonical(
                value.StoreStableId, value.StoreCode, value.DisplayName,
                value.SemanticPlaceStableId, value.StatusCode, value.Revision));
            AppendRows(target, state.StoreMemberships, value => Canonical(
                value.MembershipStableId, value.HeadquartersStableId,
                value.StoreStableId, value.StatusCode,
                value.EffectiveFromWorldTick,
                CanonicalNullable(value.EffectiveUntilWorldTick),
                value.Revision));
            AppendRows(target, state.Suppliers, value => Canonical(
                value.SupplierStableId, value.SupplierKey, value.DisplayName,
                value.SupplierKindCode, value.StatusCode, value.Revision));
            AppendRows(target, state.SourcingAgreements, value => Canonical(
                value.ContractStableId, value.HeadquartersStableId,
                value.SupplierStableId, value.ContractNumber,
                value.ContractDocumentVersion, value.StatusCode,
                value.EffectiveFromWorldTick,
                CanonicalNullable(value.EffectiveUntilWorldTick),
                value.CurrencyCode,
                value.Revision));
            AppendRows(target, state.SourcingAgreementItems, value => Canonical(
                value.ContractItemStableId, value.HeadquartersStableId,
                value.ContractStableId, value.ProductStableId,
                value.SupplierSku, value.ItemName, value.OrderUnitCode,
                value.PackageContentQuantity, value.PackageContentUnitCode,
                value.ConversionRuleRevision, value.UnitPrice,
                value.MinimumOrderQuantity,
                CanonicalNullable(value.MaximumOrderQuantity),
                value.StorageConditionCode, value.StatusCode, value.Revision,
                CanonicalStringSequence(value.SourceStableIds)));
            AppendRows(target, state.StoreSupplyOffers, value => Canonical(
                value.OfferStableId, value.HeadquartersStableId,
                value.OfferNumber, value.OfferDocumentVersion,
                value.CommercialFlowModelCode, value.FulfillmentModelCode,
                value.StatusCode, value.EffectiveFromWorldTick,
                CanonicalNullable(value.EffectiveUntilWorldTick),
                value.CurrencyCode,
                value.Revision));
            AppendRows(target, state.StoreSupplyOfferItems, value => Canonical(
                value.OfferItemStableId, value.HeadquartersStableId,
                value.OfferStableId, value.SourceContractStableId,
                value.SourceContractItemStableId, value.ProductStableId,
                value.StoreSku, value.ItemName, value.OrderUnitCode,
                value.PackageContentQuantity, value.PackageContentUnitCode,
                value.ConversionRuleRevision, value.UnitPrice,
                value.MinimumOrderQuantity,
                CanonicalNullable(value.MaximumOrderQuantity),
                value.StorageConditionCode, value.StatusCode, value.Revision,
                CanonicalStringSequence(value.SourceStableIds)));
            AppendRows(target, state.StoreOrders, value => Canonical(
                value.OrderStableId, value.HeadquartersStableId,
                value.StoreMembershipStableId, value.StoreStableId,
                value.OfferStableId, value.ClientRequestStableId,
                value.RequestPayloadHashSha256, value.OrderNumber,
                value.OfferDocumentVersionSnapshot,
                value.SellerHeadquartersStableIdSnapshot,
                value.CommercialFlowModelCodeSnapshot,
                value.FulfillmentModelCodeSnapshot,
                value.DeliverySiteStableIdSnapshot,
                value.CurrencyCodeSnapshot, value.StatusCode,
                value.RequestedDeliveryWorldTick, value.CreatedWorldTick,
                CanonicalNullable(value.SubmittedWorldTick),
                value.TotalAmountSnapshot,
                value.Revision));
            AppendRows(target, state.StoreOrderLines, value => Canonical(
                value.OrderLineStableId, value.HeadquartersStableId,
                value.OfferStableId, value.OrderStableId,
                value.OfferItemStableId, value.ProductStableIdSnapshot,
                value.StoreSkuSnapshot, value.ItemNameSnapshot,
                value.OrderUnitCodeSnapshot,
                value.PackageContentQuantitySnapshot,
                value.PackageContentUnitCodeSnapshot,
                value.ConversionRuleRevisionSnapshot,
                value.UnitPriceSnapshot, value.CurrencyCodeSnapshot,
                value.RequestedOrderUnitQuantity,
                value.RequestedContentQuantitySnapshot,
                CanonicalNullable(value.AcceptedOrderUnitQuantity),
                value.LineAmountSnapshot, value.Revision));
            return target.ToString();
        }

        private static void AppendRows<T>(StringBuilder target,
            T[]? rows, Func<T, string> canonical)
        {
            var values = rows ?? Array.Empty<T>();
            target.Append(Canonical(values.Length));
            foreach (var value in values) target.Append(canonical(value));
        }

        internal static 프랜차이즈공급망StateSnapshot?
            CloneFranchiseSupplyState(프랜차이즈공급망StateSnapshot? source)
        {
            if (source == null) return null;
            var serializer = new DataContractSerializer(
                typeof(프랜차이즈공급망StateSnapshot));
            using var stream = new MemoryStream();
            serializer.WriteObject(stream, source);
            stream.Position = 0;
            return (프랜차이즈공급망StateSnapshot)serializer.ReadObject(stream)!;
        }

        internal static void ValidateFranchiseSupplyState(
            프랜차이즈공급망StateSnapshot state)
        {
            if (state == null
                || state.SchemaVersion !=
                    프랜차이즈공급망SimulationContractCodes.SchemaVersion
                || state.RuleRevision !=
                    프랜차이즈공급망SimulationContractCodes.RuleRevision
                || !state.SimulationOnly || state.IsOperationalState
                || string.IsNullOrWhiteSpace(state.NetworkStableId)
                || state.Headquarters?.Length != 1
                || state.Stores == null || state.Suppliers == null
                || state.StoreMemberships == null
                || state.SourcingAgreements == null
                || state.SourcingAgreementItems == null
                || state.StoreSupplyOffers == null
                 || state.StoreSupplyOfferItems == null
                 || state.StoreOrders == null || state.StoreOrderLines == null)
                throw new SimulationContractException(
                    "FranchiseSupplyStateInvalid");

            if (state.Headquarters.Any(value => value == null)
                || state.Stores.Any(value => value == null)
                || state.StoreMemberships.Any(value => value == null)
                || state.Suppliers.Any(value => value == null)
                || state.SourcingAgreements.Any(value => value == null)
                || state.SourcingAgreementItems.Any(value => value == null)
                || state.StoreSupplyOffers.Any(value => value == null)
                || state.StoreSupplyOfferItems.Any(value => value == null)
                || state.StoreOrders.Any(value => value == null)
                || state.StoreOrderLines.Any(value => value == null)
                || state.SourceSessionRevision <= 0
                || state.ProjectionRevision <= 0
                || state.AsOfWorldTick < 0
                || !AreValidStateSources(state.SourceStableIds)
                || state.StoreMemberships.Any(value =>
                    HasInvalidStatePeriod(value.EffectiveFromWorldTick,
                        value.EffectiveUntilWorldTick))
                || state.SourcingAgreements.Any(value =>
                    HasInvalidStatePeriod(value.EffectiveFromWorldTick,
                        value.EffectiveUntilWorldTick))
                || state.StoreSupplyOffers.Any(value =>
                    HasInvalidStatePeriod(value.EffectiveFromWorldTick,
                        value.EffectiveUntilWorldTick))
                || state.SourcingAgreementItems.Any(value =>
                    HasInvalidStateQuantityRange(value.MinimumOrderQuantity,
                        value.MaximumOrderQuantity)
                    || !AreValidStateSources(value.SourceStableIds))
                || state.StoreSupplyOfferItems.Any(value =>
                    HasInvalidStateQuantityRange(value.MinimumOrderQuantity,
                        value.MaximumOrderQuantity)
                    || !AreValidStateSources(value.SourceStableIds))
                || state.StoreOrders.Any(value =>
                    value.SubmittedWorldTick.HasValue
                    && value.SubmittedWorldTick.Value < 0)
                || state.StoreOrderLines.Any(value =>
                    value.AcceptedOrderUnitQuantity.HasValue
                    && (value.AcceptedOrderUnitQuantity.Value < 0
                        || value.AcceptedOrderUnitQuantity.Value >
                            value.RequestedOrderUnitQuantity)))
                throw new SimulationContractException(
                    "FranchiseSupplyStateInvalid");
        }

        private static bool HasInvalidStatePeriod(int from, int? until)
            => from < 0 || until.HasValue && until.Value < from;

        private static bool HasInvalidStateQuantityRange(decimal minimum,
            decimal? maximum)
            => minimum <= 0 || maximum.HasValue && maximum.Value < minimum;

        private static bool AreValidStateSources(string[]? sources)
        {
            if (sources == null || sources.Any(string.IsNullOrWhiteSpace))
                return false;
            var normalized = sources.Select(value => value.Trim())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            return sources.SequenceEqual(normalized, StringComparer.Ordinal)
                && normalized.All(value => value.Length <= 200
                    && value.All(character => character <= 0x7f));
        }

        private sealed class AppliedFranchiseSupplyCommand
        {
            public AppliedFranchiseSupplyCommand(string signature,
                프랜차이즈공급망StateSnapshot state)
            {
                Signature = signature;
                State = CloneFranchiseSupplyState(state)!;
            }

            public string Signature { get; }
            public 프랜차이즈공급망StateSnapshot State { get; }
        }
    }
}
