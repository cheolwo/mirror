using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Contracts.Common.Operations;
using Ssalddel.Contracts.Common.Versioning;
using 살뜰.Data;
using 살뜰.Services.Operations;

namespace Ssalddel.Services.Operations;

public static class 화주운송의뢰화물운송인계Contract
{
    public const string 출발업무유형Code = "ShipperTransportRequest";
    public const string 인계계약Code = OperatingSystemInteractionContractCodes.ShipperTransportRequestToDomesticCargo;
    public const string 인계계약Revision = OperatingSystemInteractionContractRevisions.ShipperTransportRequestToDomesticCargo;
    private const string 출발업무StableIdPrefix = "shipper-transport-request:";

    public static string 출발업무StableId(string 운송의뢰Id)
        => $"{출발업무StableIdPrefix}{운송의뢰Id.Trim()}";

    public static string 도착업무StableId(string 운송의뢰Id)
        => $"cargo-request:{운송의뢰Id.Trim()}";

    public static string 운송의뢰Id(string 출발업무StableId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(출발업무StableId);
        if (!출발업무StableId.StartsWith(출발업무StableIdPrefix, StringComparison.Ordinal))
        {
            throw new ArgumentException("화주 운송의뢰 인계 Stable ID 형식이 아닙니다.", nameof(출발업무StableId));
        }

        return 출발업무StableId[출발업무StableIdPrefix.Length..];
    }
}

public interface I화주운송의뢰화물운송인계Service
{
    Task<운영체제업무인계Dto> 인계Async(
        string 운송의뢰Id,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 화주가 확정한 운송 조건의 실행 책임을 화물운송 OS에 넘깁니다.
/// 동일 서버에 저장된 화주 의뢰가 존재할 때만 인계를 수락하며 배차나 기사 상태는 변경하지 않습니다.
/// </summary>
[SsalddelCodeMetadata(
    SsalddelCodeFeatureKeys.OperationalLogisticsOs,
    SsalddelCodeLayer.Application,
    "확정·저장된 화주 운송의뢰를 별도 OS 인계 원장으로 국내 화물 운송 실행에 결속한다.",
    Effects = SsalddelCodeEffect.PersistentRead | SsalddelCodeEffect.PersistentWrite,
    FlowOrder = 35,
    StepKey = "application.shipper-transport-request-handoff",
    ExecutionStage = SsalddelCodeExecutionStage.Confirm,
    ReadsFrom = SsalddelCodeDataScope.OperationalState,
    WritesTo = SsalddelCodeDataScope.OperationalState,
    Boundary = "화주 의뢰·운임 조건을 복제하거나 배차를 시작하지 않는다. 도착 업무가 확인된 뒤 책임 인계만 수락한다.")]
public sealed class 화주운송의뢰화물운송인계Service(
    SsalddelContext db,
    I운영체제업무인계Coordinator handoffCoordinator,
    TimeProvider timeProvider) : I화주운송의뢰화물운송인계Service
{
    public async Task<운영체제업무인계Dto> 인계Async(
        string 운송의뢰Id,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(운송의뢰Id);
        var requestId = 운송의뢰Id.Trim();
        var request = await db.화주운송의뢰
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.의뢰Id == requestId, cancellationToken)
            ?? throw new KeyNotFoundException("화물운송 OS에 인계할 화주 운송의뢰를 찾을 수 없습니다.");

        var sourceRevision = AsUtc(request.CreatedAt).Ticks;
        var handoffRequestId = DeterministicGuid($"shipper-transport-request:{requestId}:handoff:v1");
        var handoffStableId = $"os-handoff:{handoffRequestId:N}";
        var handoff = await handoffCoordinator.조회Async(handoffStableId, cancellationToken);
        if (handoff is null)
        {
            var now = timeProvider.GetUtcNow().UtcDateTime;
            handoff = await handoffCoordinator.요청Async(new 운영체제업무인계생성요청
            {
                클라이언트요청Id = handoffRequestId,
                출발운영체제Id = OperatingSystemIds.ShipperTransportManagement,
                도착운영체제Id = OperatingSystemIds.DomesticCargoTransport,
                출발업무유형Code = 화주운송의뢰화물운송인계Contract.출발업무유형Code,
                출발업무StableId = 화주운송의뢰화물운송인계Contract.출발업무StableId(requestId),
                출발업무Revision = sourceRevision,
                인계계약Code = 화주운송의뢰화물운송인계Contract.인계계약Code,
                인계계약Revision = 화주운송의뢰화물운송인계Contract.인계계약Revision,
                최소상태사본Json = JsonSerializer.Serialize(new
                {
                    cargoRequestStableId = 화주운송의뢰화물운송인계Contract.도착업무StableId(requestId),
                    cargoType = request.화물종류,
                    quantity = request.화물수량,
                    weightKg = request.화물중량Kg,
                    volumeCbm = request.화물부피Cbm,
                    palletCount = request.화물팔레트개수,
                    temperatureCondition = request.화물온도조건,
                    fragile = request.화물파손주의여부,
                    transportMode = request.운송방식,
                    vehicleType = request.차량종류,
                    pickupWindowStartAtUtc = AsNullableUtc(request.픽업_시간창_시작일시),
                    pickupWindowEndAtUtc = AsNullableUtc(request.픽업_시간창_종료일시),
                    dropoffWindowStartAtUtc = AsNullableUtc(request.하차_시간창_시작일시),
                    dropoffWindowEndAtUtc = AsNullableUtc(request.하차_시간창_종료일시),
                    finalFare = request.최종운임,
                    settlementTime = request.정산시점,
                    commitmentStageId = OperatingSystemLifecycleStageIds.ShipperRequestCommitment,
                    handoffStageId = OperatingSystemLifecycleStageIds.ShipperTransportHandoff
                }),
                만료시각Utc = now.AddHours(24)
            }, cancellationToken);
        }
        else
        {
            기존인계검증(handoff, requestId, sourceRevision);
        }

        if (handoff.상태Code == 운영체제업무인계상태Codes.수락됨)
        {
            return handoff;
        }
        if (handoff.상태Code is 운영체제업무인계상태Codes.거절됨 or 운영체제업무인계상태Codes.만료됨)
        {
            throw new InvalidOperationException($"종결된 화주 운송의뢰 인계는 다시 수락할 수 없습니다. Status={handoff.상태Code}");
        }

        return await handoffCoordinator.결정Async(
            handoff.인계StableId,
            new 운영체제업무인계결정요청
            {
                클라이언트요청Id = DeterministicGuid($"shipper-transport-request:{requestId}:accept:v1"),
                예상Revision = handoff.Revision,
                응답운영체제Id = OperatingSystemIds.DomesticCargoTransport,
                결정Code = 운영체제업무인계결정Codes.수락,
                도착업무StableId = 화주운송의뢰화물운송인계Contract.도착업무StableId(requestId)
            },
            cancellationToken);
    }

    private static void 기존인계검증(
        운영체제업무인계Dto handoff,
        string requestId,
        long sourceRevision)
    {
        if (handoff.출발운영체제Id != OperatingSystemIds.ShipperTransportManagement
            || handoff.도착운영체제Id != OperatingSystemIds.DomesticCargoTransport
            || handoff.출발업무유형Code != 화주운송의뢰화물운송인계Contract.출발업무유형Code
            || handoff.출발업무StableId != 화주운송의뢰화물운송인계Contract.출발업무StableId(requestId)
            || handoff.출발업무Revision != sourceRevision
            || handoff.인계계약Code != 화주운송의뢰화물운송인계Contract.인계계약Code
            || handoff.인계계약Revision != 화주운송의뢰화물운송인계Contract.인계계약Revision)
        {
            throw new InvalidOperationException("기존 화주 운송의뢰 인계가 현재 의뢰·판본·계약과 일치하지 않습니다.");
        }
    }

    private static DateTime AsUtc(DateTime value)
        => value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private static DateTime? AsNullableUtc(DateTime value)
        => value == default ? null : AsUtc(value);

    private static DateTime? AsNullableUtc(DateTime? value)
        => value.HasValue ? AsUtc(value.Value) : null;

    private static Guid DeterministicGuid(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        var bytes = new byte[16];
        Array.Copy(hash, bytes, bytes.Length);
        return new Guid(bytes);
    }
}
