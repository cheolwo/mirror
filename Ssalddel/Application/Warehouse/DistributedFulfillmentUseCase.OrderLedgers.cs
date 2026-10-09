using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Common.Warehouse;
using 살뜰.도메인.창고;

namespace Ssalddel.Application.Warehouse;

internal sealed record 분산예약합의Snapshot(string ReservationId, string OrderId, string LotId, int Quantity,
    string AgreementId, long AgreementRevision, int TermsVersion, string TermsJson, DateTime ReservedAtUtc);

internal static class 분산주문원장Metadata
{
    public static Dictionary<string, DistributedOrderLedgerConnection> Connections(분산배송WaveRecord wave)
        => JsonNode.Parse(wave.RouteEvidenceJson)?["orderLedgerConnections"]?.Deserialize<Dictionary<string, DistributedOrderLedgerConnection>>(
            new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? new(StringComparer.Ordinal);
    public static Dictionary<string, 분산예약합의Snapshot> Reservations(분산배송WaveRecord wave)
        => JsonNode.Parse(wave.RouteEvidenceJson)?["reservationTerms"]?.Deserialize<Dictionary<string, 분산예약합의Snapshot>>(
            new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? new(StringComparer.Ordinal);
    public static void Connection(분산배송WaveRecord wave, DistributedOrderLedgerConnection connection)
    {
        var all = Connections(wave); all[connection.OrderId] = connection;
        var json = JsonNode.Parse(wave.RouteEvidenceJson) as JsonObject ?? new();
        json["orderLedgerConnections"] = JsonSerializer.SerializeToNode(all, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        wave.RouteEvidenceJson = json.ToJsonString();
    }
    public static void Reservation(분산배송WaveRecord wave, 분산재고예약Record reservation, 분산보관합의Record agreement, DateTime at)
    {
        var all = Reservations(wave); all[reservation.ReservationId] = new(reservation.ReservationId, reservation.OrderId, reservation.LotId,
            reservation.Quantity, agreement.AgreementId, agreement.Revision, agreement.TermsVersion, agreement.TermsJson, at);
        var json = JsonNode.Parse(wave.RouteEvidenceJson) as JsonObject ?? new();
        json["reservationTerms"] = JsonSerializer.SerializeToNode(all, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        wave.RouteEvidenceJson = json.ToJsonString();
    }
    public static void Preserve(JsonObject previous, JsonObject next)
    {
        foreach (var key in new[] { "orderLedgerConnections", "reservationTerms" })
            if (previous[key] is { } value) next[key] = value.DeepClone();
    }
}

public sealed partial class 분산물류UseCase
{
    private async Task<DistributedRootOrderSnapshot?> RootOrder(DistributedActor actor, DistributedOrderRequest request,
        string customerUserId, CancellationToken ct)
    {
        if (request.RootOrderLedgerId is null && request.ExpectedRootRevision is null) return null;
        Require(request.RootOrderLedgerId is not null && request.ExpectedRootRevision.HasValue, "DistributedRootOrderLedgerInputInvalid", 400);
        Require(orderLedgers is not null, "DistributedOrderLedgerAdapterUnavailable", 503);
        return await orderLedgers!.ValidateAsync(actor.UserId, request.RootOrderLedgerId!, request.ExpectedRootRevision!.Value,
            actor.UserId, customerUserId, ct);
    }
    private async Task<DistributedCommandResponse> ResumeOrderLedger(DistributedActor actor, string kind, string target,
        DistributedCommandResponse result, CancellationToken ct)
    {
        if (kind is not "AddOrder" and not "LinkOrderLedger") return result;
        var wave = await Query<분산배송WaveRecord>().AsNoTracking().SingleAsync(x => x.WaveId == result.AggregateId, ct);
        var connection = 분산주문원장Metadata.Connections(wave).Values.SingleOrDefault(x => kind == "AddOrder"
            ? x.OrderRequestId == result.RequestId && x.SellerUserId == actor.UserId
            : x.RequestId == result.RequestId && target == wave.WaveId + "/" + x.OrderId);
        if (connection is null) return result;
        if (orderLedgers is null) return result with { OrderLedgerConnectionStatusCode = "Pending", OrderLedgerConnectionErrorCode = "DistributedOrderLedgerAdapterUnavailable" };
        var state = await orderLedgers.SynchronizeAsync(actor.UserId, connection, ct);
        return result with { OrderLedgerConnectionStatusCode = state.StatusCode, OrderLedgerConnectionErrorCode = state.ErrorCode };
    }
    public Task<DistributedCommandResponse> LinkOrderLedgerAsync(DistributedActor actor, string waveId, string orderId,
        DistributedOrderLedgerLinkRequest request, CancellationToken ct)
        => Execute(actor, "LinkOrderLedger", waveId + "/" + orderId, request, async () =>
        {
            var wave = await Wave(waveId, ct); Revision(wave.Revision, request);
            var order = await Query<분산배송주문Record>().SingleOrDefaultAsync(x => x.WaveId == waveId && x.OrderId == orderId, ct);
            Require(order is not null && (order.SellerUserId == actor.UserId || order.CustomerUserId == actor.UserId), "DistributedOrderLedgerPartyRequired", 403);
            Require(orderLedgers is not null, "DistributedOrderLedgerAdapterUnavailable", 503);
            var existing = 분산주문원장Metadata.Connections(wave).GetValueOrDefault(orderId);
            Require(existing is null || existing.RootOrderLedgerId == request.RootOrderLedgerId, "DistributedRootOrderLedgerReplacementForbidden");
            var root = await orderLedgers!.ValidateAsync(actor.UserId, request.RootOrderLedgerId, request.ExpectedRootRevision,
                order!.SellerUserId, order.CustomerUserId, ct);
            var outbounds = await Query<분산재고예약Record>().Where(x => x.OrderId == orderId).Select(x => x.OutboundPlanId).ToArrayAsync(ct);
            분산주문원장Metadata.Connection(wave, new()
            {
                OrderId = orderId, RootOrderLedgerId = root.RootOrderLedgerId, ExpectedRootRevision = root.Revision,
                RootBusinessDigest = root.BusinessDigest, SellerUserId = order.SellerUserId, CustomerUserId = order.CustomerUserId,
                CargoRequestId = order.CargoRequestId, OutboundPlanIds = outbounds, RequestId = request.RequestId,
                OrderRequestId = existing?.OrderRequestId ?? Guid.Empty, RecordedAtUtc = Now
            });
            wave.Revision++; return (waveId, wave.Revision, wave.StatusCode, (string?)null, 0);
        }, ct);
}
