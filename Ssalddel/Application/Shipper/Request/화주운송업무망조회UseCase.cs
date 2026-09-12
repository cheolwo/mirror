using FluentResults;
using Microsoft.AspNetCore.Http;
using Ssalddel.ApiMetadata;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Contracts.Common.Operations;
using Ssalddel.Contracts.Common.Versioning;
using Ssalddel.Services.Operations;

namespace Ssalddel.Application.Shipper.Request;

public interface I화주운송업무망조회UseCase
{
    Task<Result<OrderWorkNetworkProjection>> 조회Async(
        string 운송의뢰Id,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 화주 운송 서비스 주문과 화물운송 OS의 왕복 인계를 읽기 전용 업무망으로 조합합니다.
/// 원본 의뢰·운송·인계 상태를 변경하지 않고 주소·연락처·좌표·당사자 식별자를 투영하지 않습니다.
/// </summary>
[SsalddelApiWorkflow(SsalddelWorkflow.DomesticTransport)]
[SsalddelUseCase(
    "화주 운송 주문 업무망 조회",
    Summary = "화주 운송관리에서 화물운송을 거쳐 화주 인수로 돌아오는 실제 OS 인계를 읽기 전용으로 조합합니다.")]
[SsalddelUseCaseActor(SsalddelActor.Shipper)]
[SsalddelCodeMetadata(
    SsalddelCodeFeatureKeys.OperationalLogisticsOs,
    SsalddelCodeLayer.Application,
    "기존 화주·화물·OS 인계 원장을 한 운송 서비스 주문의 읽기 전용 업무망으로 조합한다.",
    Effects = SsalddelCodeEffect.PersistentRead,
    FlowOrder = 76,
    StepKey = "application.shipper-transport-order-work-network",
    ExecutionStage = SsalddelCodeExecutionStage.Query,
    ReadsFrom = SsalddelCodeDataScope.OperationalState,
    WritesTo = SsalddelCodeDataScope.None,
    Boundary = "원본 원장을 합치거나 상태를 전진시키지 않으며 주소·연락처·좌표·사용자·기사 식별자를 반환하지 않는다.")]
public sealed class 화주운송업무망조회UseCase(
    SsalddelContext db,
    I화주운송업무담당자UseCase operatorUseCase,
    TimeProvider timeProvider) : I화주운송업무망조회UseCase
{
    public async Task<Result<OrderWorkNetworkProjection>> 조회Async(
        string 운송의뢰Id,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(운송의뢰Id))
        {
            return NotFound();
        }

        var requestId = 운송의뢰Id.Trim();
        var request = await db.화주운송의뢰
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.의뢰Id == requestId, cancellationToken);
        if (request is null
            || !await operatorUseCase.권한보유Async(
                request,
                운송업무권한Codes.진행조회,
                cancellationToken))
        {
            return NotFound();
        }

        var sourceWorkStableId = 화주운송의뢰화물운송인계Contract.출발업무StableId(requestId);
        var outgoing = await db.운영체제업무인계
            .AsNoTracking()
            .Where(x => x.출발운영체제Id == OperatingSystemIds.ShipperTransportManagement
                        && x.도착운영체제Id == OperatingSystemIds.DomesticCargoTransport
                        && x.출발업무StableId == sourceWorkStableId
                        && x.인계계약Code == OperatingSystemInteractionContractCodes.ShipperTransportRequestToDomesticCargo)
            .OrderByDescending(x => x.UpdatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var transport = await db.운송원장
            .AsNoTracking()
            .Where(x => x.의뢰Id == requestId)
            .OrderByDescending(x => x.UpdatedAt)
            .Select(x => new TransportState(x.Id, x.상태, x.UpdatedAt))
            .FirstOrDefaultAsync(cancellationToken);

        var completionSourceStableId = transport is null
            ? string.Empty
            : 화물운송완료화주인수인계Contract.출발업무StableId(transport.Id);
        var completion = string.IsNullOrEmpty(completionSourceStableId)
            ? null
            : await db.운영체제업무인계
                .AsNoTracking()
                .Where(x => x.출발운영체제Id == OperatingSystemIds.DomesticCargoTransport
                            && x.도착운영체제Id == OperatingSystemIds.ShipperTransportManagement
                            && x.출발업무StableId == completionSourceStableId
                            && x.인계계약Code == OperatingSystemInteractionContractCodes.DomesticCargoCompletionToShipperAcceptance)
                .OrderByDescending(x => x.UpdatedAt)
                .FirstOrDefaultAsync(cancellationToken);

        var outgoingDefinition = OperatingSystemInteractionCatalog.Get(
            OperatingSystemInteractionIds.ShipperRequestToCargo);
        var completionDefinition = OperatingSystemInteractionCatalog.Get(
            OperatingSystemInteractionIds.CargoCompletionToShipperAcceptance);
        var cargoWorkStableId = !string.IsNullOrWhiteSpace(outgoing?.도착업무StableId)
            ? outgoing.도착업무StableId
            : 화주운송의뢰화물운송인계Contract.도착업무StableId(requestId);
        var acceptanceWorkStableId = !string.IsNullOrWhiteSpace(completion?.도착업무StableId)
            ? completion.도착업무StableId
            : 화물운송완료화주인수인계Contract.도착업무StableId(requestId);

        var nodes = new List<OrderWorkNetworkNodeDto>
        {
            new()
            {
                WorkStableId = sourceWorkStableId,
                NodeTypeCode = OrderWorkNetworkNodeTypeCodes.OrderRoot,
                OperatingSystemId = OperatingSystemIds.ShipperTransportManagement,
                LifecycleStageId = outgoing is null
                    ? OperatingSystemLifecycleStageIds.ShipperRequestCommitment
                    : outgoingDefinition.SourceLifecycleStageId ?? string.Empty,
                StateCode = request.상태,
                SourceRevision = outgoing?.출발업무Revision ?? AsUtc(request.CreatedAt).Ticks,
                SourceRevisionBasis = outgoing is null ? "RequestCreatedAtUtcTicks" : "HandoffSourceRevision"
            }
        };
        var interactions = new List<OrderWorkNetworkInteractionDto>();
        var pending = new List<string>();

        if (outgoing is null)
        {
            pending.Add(OrderWorkNetworkPendingCodes.CargoHandoffPending);
        }
        else
        {
            nodes.Add(new OrderWorkNetworkNodeDto
            {
                WorkStableId = cargoWorkStableId,
                NodeTypeCode = OrderWorkNetworkNodeTypeCodes.ChildExecution,
                OperatingSystemId = OperatingSystemIds.DomesticCargoTransport,
                LifecycleStageId = outgoingDefinition.TargetLifecycleStageId ?? string.Empty,
                StateCode = transport?.StateCode ?? outgoing.상태Code,
                SourceRevision = transport is null ? outgoing.Revision : AsUtc(transport.UpdatedAtUtc).Ticks,
                SourceRevisionBasis = transport is null ? "HandoffRevision" : "TransportUpdatedAtUtcTicks"
            });
            interactions.Add(ToInteraction(outgoingDefinition, outgoing));
        }

        if (transport is null)
        {
            pending.Add(OrderWorkNetworkPendingCodes.CargoExecutionLedgerPending);
        }

        if (completion is null)
        {
            pending.Add(OrderWorkNetworkPendingCodes.CompletionReturnPending);
        }
        else
        {
            nodes.Add(new OrderWorkNetworkNodeDto
            {
                WorkStableId = completion.출발업무StableId,
                NodeTypeCode = OrderWorkNetworkNodeTypeCodes.Result,
                OperatingSystemId = OperatingSystemIds.DomesticCargoTransport,
                LifecycleStageId = completionDefinition.SourceLifecycleStageId ?? string.Empty,
                StateCode = completion.상태Code,
                SourceRevision = completion.출발업무Revision,
                SourceRevisionBasis = "HandoffSourceRevision"
            });
            nodes.Add(new OrderWorkNetworkNodeDto
            {
                WorkStableId = acceptanceWorkStableId,
                NodeTypeCode = OrderWorkNetworkNodeTypeCodes.Acceptance,
                OperatingSystemId = OperatingSystemIds.ShipperTransportManagement,
                LifecycleStageId = completionDefinition.TargetLifecycleStageId ?? string.Empty,
                StateCode = completion.상태Code,
                SourceRevision = completion.Revision,
                SourceRevisionBasis = "HandoffRevision"
            });
            interactions.Add(ToInteraction(completionDefinition, completion));
            if (completion.상태Code != 운영체제업무인계상태Codes.수락됨)
            {
                pending.Add(OrderWorkNetworkPendingCodes.ShipperAcceptancePending);
            }
        }

        return Result.Ok(new OrderWorkNetworkProjection
        {
            RootTypeCode = OrderWorkNetworkRootTypeCodes.TransportServiceOrder,
            RootWorkStableId = sourceWorkStableId,
            CorrelationStableId = sourceWorkStableId,
            RootOperatingSystemId = OperatingSystemIds.ShipperTransportManagement,
            RoundTripCompleted = transport is not null
                                 && outgoing?.상태Code == 운영체제업무인계상태Codes.수락됨
                                 && completion?.상태Code == 운영체제업무인계상태Codes.수락됨,
            Nodes = nodes,
            Interactions = interactions,
            PendingCodes = pending.Distinct(StringComparer.Ordinal).ToArray(),
            ContainsPersonalData = false,
            GeneratedAtUtc = timeProvider.GetUtcNow().UtcDateTime
        });
    }

    private static OrderWorkNetworkInteractionDto ToInteraction(
        OperatingSystemInteractionDefinition definition,
        살뜰.도메인.운영.운영체제업무인계 handoff)
        => new()
        {
            InteractionId = definition.InteractionId,
            ContractCode = handoff.인계계약Code,
            ContractRevision = handoff.인계계약Revision,
            SourceWorkStableId = handoff.출발업무StableId,
            TargetWorkStableId = handoff.도착업무StableId,
            HandoffStableId = handoff.인계StableId,
            HandoffRevision = handoff.Revision,
            HandoffStateCode = handoff.상태Code,
            CurrentResponsibleOperatingSystemId = handoff.현재책임운영체제Id
        };

    private static DateTime AsUtc(DateTime value)
        => value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private static Result<OrderWorkNetworkProjection> NotFound()
        => Result.Fail<OrderWorkNetworkProjection>(new Error("운송 의뢰를 찾을 수 없습니다.")
            .WithMetadata("StatusCode", StatusCodes.Status404NotFound)
            .WithMetadata("ErrorCode", "TransportRequestNotFound"));

    private sealed record TransportState(long Id, string StateCode, DateTime UpdatedAtUtc);
}
