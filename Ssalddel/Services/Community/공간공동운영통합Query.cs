using Microsoft.EntityFrameworkCore;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Application.Mart;
using Ssalddel.Application.Warehouse;
using Ssalddel.Contracts.Common.Community;
using 살뜰.Data;
using 살뜰.Services.Options;
using 살뜰.Services.Versioning;
using 살뜰.도메인.창고;

namespace Ssalddel.Services.Community;

public interface I공간공동운영통합Query
{
    Task<공간공동운영통합Response?> 조회Async(string ledgerId, CancellationToken cancellationToken = default);
}

/// <summary>기존 상위 원장·당사자 주문·전문 원천을 조회만 합니다. 별도 master나 완료 상태를 저장하지 않습니다.</summary>
public sealed class 공간공동운영통합Query(I공간공동운영UseCase cooperation, I공간공동운영주문UseCase orders,
    I커뮤니티원장저장소 ledgers, SsalddelContext db, ICurrentUserAccessor currentUser,
    ISsalddelExecutionModePolicy execution, IVersionFeatureFlagService features, TimeProvider clock) : I공간공동운영통합Query
{
    public async Task<공간공동운영통합Response?> 조회Async(string ledgerId, CancellationToken cancellationToken = default)
    {
        var actor = currentUser.UserId?.Trim();
        if (string.IsNullOrWhiteSpace(actor)) return null;
        var detail = await cooperation.상세Async(ledgerId, cancellationToken);
        if (detail is null) return null;
        var result = new 공간공동운영통합Response
        { LedgerId = detail.LedgerId, Revision = detail.Revision, ExecutionMode = detail.ExecutionMode };
        var matchesMode = detail.ExecutionMode == execution.Mode.ToString();
        var states = new Dictionary<string, IReadOnlyList<공간공동운영원천상태Dto>>(StringComparer.Ordinal);
        foreach (var cycle in detail.Cycles)
        {
            var observed = detail.SharedOutcomes?.Cycles.Where(item => item.CycleId == cycle.CycleId).ToArray() ?? [];
            var raw = observed.Length == 1 ? observed[0].Sources : [];
            states[cycle.CycleId] = cycle.References.GroupBy(reference => (reference.KindCode, reference.ReferenceId))
                .Select(group =>
                {
                    var reference = group.First();
                    var found = raw.Where(source => source.KindCode == reference.KindCode && source.ReferenceId == reference.ReferenceId).ToArray();
                    return matchesMode && group.Count() == 1 && found.Length == 1 ? found[0] : new 공간공동운영원천상태Dto
                    { KindCode = reference.KindCode, ReferenceId = reference.ReferenceId,
                        StatusCode = matchesMode ? "source-unverified" : "execution-mode-mismatch",
                        Label = "이 연결의 현재 근거를 확인하지 못했습니다.", RequiresAttention = true };
                }).ToArray();
        }
        var shared = states.Values.SelectMany(items => items).GroupBy(item => (item.KindCode, item.ReferenceId))
            .Select(group =>
            {
                var first = group.First();
                var consistent = group.All(item => item.Verified == first.Verified && item.Completed == first.Completed
                    && item.StatusCode == first.StatusCode && item.RequiresAttention == first.RequiresAttention
                    && item.VerifiedRevision == first.VerifiedRevision);
                return new 공간공동운영통합원천Dto
                {
                    NodeKey = SourceKey(first.KindCode, first.ReferenceId), KindCode = first.KindCode, ReferenceId = first.ReferenceId,
                    StatusCode = consistent ? first.StatusCode : "source-context-conflict", Label = consistent ? first.Label : "회차별 원천 조회 결과가 달라 다시 확인해야 합니다.",
                    Verified = consistent && first.Verified, Completed = consistent && first.Verified && first.Completed,
                    RequiresAttention = !consistent || !first.Verified || first.RequiresAttention,
                    Href = consistent && first.Verified ? first.Href : ""
                };
            }).ToDictionary(item => item.NodeKey, StringComparer.Ordinal);
        var orderRows = await ReadOrders(detail, matchesMode, result, cancellationToken);
        var relations = new List<공간공동운영통합관계Dto>();
        var cycles = new List<공간공동운영통합회차Dto>();
        foreach (var cycle in detail.Cycles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var item = new 공간공동운영통합회차Dto
            {
                NodeKey = "cycle:" + cycle.CycleId, CycleId = cycle.CycleId, Number = cycle.Number,
                TermsRevision = cycle.TermsRevision, StatusCode = cycle.StatusCode,
                SharedSourceKeys = states[cycle.CycleId].Select(source => SourceKey(source.KindCode, source.ReferenceId)).ToArray()
            };
            relations.Add(new() { FromKey = "cooperation:" + detail.LedgerId, ToKey = item.NodeKey, RelationCode = "cooperation-cycle" });
            foreach (var key in item.SharedSourceKeys)
                relations.Add(new() { FromKey = item.NodeKey, ToKey = key, RelationCode = "cycle-source-reference" });
            var visible = new List<공간공동운영통합주문Dto>();
            foreach (var order in orderRows.Where(order => order.Terms.CycleId == cycle.CycleId))
            {
                var orderNode = new 공간공동운영통합주문Dto
                {
                    NodeKey = "order:" + order.OrderRequestId.ToString("N"), OrderRequestId = order.OrderRequestId,
                    TermsVersion = order.TermsVersion, ProductName = order.ProductName,
                    AgreementStatusCode = order.StatusCode, RootOrderLedgerId = order.RootOrderLedgerId
                };
                await ReadOrder(detail, actor, order, orderNode, item.SharedSourceKeys, shared, cancellationToken);
                visible.Add(orderNode);
                relations.Add(new() { FromKey = item.NodeKey, ToKey = orderNode.NodeKey, RelationCode = "cycle-order-agreement" });
                foreach (var source in orderNode.Children)
                    relations.Add(new() { FromKey = orderNode.NodeKey, ToKey = source.NodeKey, RelationCode = "source-lineage" });
            }
            item.Orders = visible; cycles.Add(item);
        }
        result.Cycles = cycles; result.SharedSources = shared.Values.ToArray();
        result.Relations = relations.DistinctBy(link => (link.FromKey, link.ToKey, link.RelationCode)).ToArray();
        result.HasIncompleteVisibility = result.OrdersReadStatusCode != "current" || result.SharedSources.Any(source => !source.Verified || source.RequiresAttention)
            || cycles.SelectMany(cycle => cycle.Orders).Any(order => order.RootReadStatusCode is not ("verified-link" or "not-created"));
        result.StatusCode = result.HasIncompleteVisibility ? "partial" : "current";
        result.AsOfUtc = clock.GetUtcNow();
        return result;
    }

    private async Task<IReadOnlyList<공간공동운영주문Response>> ReadOrders(공간공동운영상세Response detail,
        bool matchesMode, 공간공동운영통합Response result, CancellationToken ct)
    {
        if (!matchesMode) { result.OrdersReadStatusCode = "execution-mode-mismatch"; return []; }
        if (!features.IsEnabled(VersionFeatureFlagKeys.SsalddelMartWorkflow))
        { result.OrdersReadStatusCode = "feature-disabled"; return []; }
        try
        {
            // 기존 API와 같은 구매자·판매자 필터를 거친 결과만 소비합니다. 보호 주문 부록을 다시 해석하지 않습니다.
            var rows = await orders.목록Async(detail.LedgerId, ct);
            if (rows.LedgerId != detail.LedgerId || rows.Revision != detail.Revision)
            { result.OrdersReadStatusCode = "revision-changed"; return []; }
            var unique = rows.Items.GroupBy(order => order.OrderRequestId).ToArray();
            if (unique.Any(group => group.Key == Guid.Empty || group.Count() != 1)
                || rows.Items.Any(order => order.LedgerId != detail.LedgerId || order.Revision != detail.Revision
                    || order.OrderRequestId != order.Terms.OrderRequestId || !detail.Cycles.Any(cycle => cycle.CycleId == order.Terms.CycleId)))
            { result.OrdersReadStatusCode = "source-unverified"; return []; }
            return rows.Items;
        }
        catch (SpaceOrderException error) when (error.Code == "SpaceOrderWorkflowDisabled")
        { result.OrdersReadStatusCode = "workflow-disabled"; return []; }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception) { result.OrdersReadStatusCode = "temporarily-unavailable"; return []; }
    }

    private async Task ReadOrder(공간공동운영상세Response detail, string actor, 공간공동운영주문Response order,
        공간공동운영통합주문Dto target, IReadOnlyList<string> cycleSourceKeys,
        IReadOnlyDictionary<string, 공간공동운영통합원천Dto> shared, CancellationToken ct)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(order.RootOrderLedgerId)) return;
            var root = await ledgers.원장조회Async(order.RootOrderLedgerId, ct);
            if (root is null) { target.RootReadStatusCode = order.StatusCode == "proposed" ? "not-created" : "not-visible"; return; }
            if (root.생성자UserId != actor && !root.참여자목록.Any(p => p.UserId == actor && p.ParticipationState is not ("철회" or "취소" or "거절")))
            { target.RootReadStatusCode = "not-visible"; return; }
            if (root.원장Id != "space-order:" + order.OrderRequestId.ToString("N") || root.원장Id != order.RootOrderLedgerId
                || root.커뮤니티Id != "platform" || root.원장템플릿Key != CommunityLedgerTemplateKeys.Order
                || root.외부참조.GetValueOrDefault("space-cooperation-id") != detail.LedgerId
                || root.외부참조.GetValueOrDefault("space-cooperation-order-request") != order.OrderRequestId.ToString("N")
                || !await orders.원주문연결검증Async(detail.LedgerId, detail.Revision, order.OrderRequestId, root, ct))
            { target.RootReadStatusCode = "lineage-unverified"; return; }
            target.RootReadStatusCode = "verified-link";
            if (string.IsNullOrWhiteSpace(order.DistributedOrderId)) return;
            var actual = await db.Set<분산배송주문Record>().AsNoTracking().SingleOrDefaultAsync(row => row.OrderId == order.DistributedOrderId, ct);
            if (actual is null || actual.SellerUserId != actor && actual.CustomerUserId != actor
                || actual.WaveId != order.Terms.WaveId || actual.CargoRequestId != order.Terms.CargoRequestId
                || actual.OrderReference != root.원장Id)
            { target.RootReadStatusCode = "lineage-unverified"; return; }
            var wave = await db.Set<분산배송WaveRecord>().AsNoTracking().SingleOrDefaultAsync(row => row.WaveId == actual.WaveId, ct);
            var connection = wave is null ? null : 분산주문원장Metadata.Connections(wave).GetValueOrDefault(actual.OrderId);
            if (connection is null || connection.RootOrderLedgerId != root.원장Id || connection.SellerUserId != actual.SellerUserId
                || connection.CustomerUserId != actual.CustomerUserId || connection.CargoRequestId != actual.CargoRequestId
                || !root.참여자목록.Any(p => p.UserId == actual.SellerUserId && p.RoleLabel == "판매자" && p.ParticipationState == "참여중")
                || !root.참여자목록.Any(p => p.UserId == actual.CustomerUserId && p.RoleLabel == "구매자" && p.ParticipationState == "참여중"))
            { target.RootReadStatusCode = "lineage-unverified"; return; }
            var reservations = await db.Set<분산재고예약Record>().AsNoTracking().Where(row => row.OrderId == actual.OrderId).ToArrayAsync(ct);
            if (reservations.Length == 0 || reservations.Any(row => row.WaveId != actual.WaveId || row.LotId != order.Terms.LotId || row.Quantity <= 0)
                || reservations.Sum(row => (long)row.Quantity) != order.Terms.Quantity)
            { target.RootReadStatusCode = "lineage-unverified"; return; }
            var lot = await db.Set<분산재고LotRecord>().AsNoTracking().SingleOrDefaultAsync(row => row.LotId == order.Terms.LotId, ct);
            if (lot is null || lot.SiteId != order.Terms.SiteId || lot.SellerUserId != actual.SellerUserId)
            { target.RootReadStatusCode = "lineage-unverified"; return; }
            var allowed = new HashSet<string>(StringComparer.Ordinal)
            {
                SourceKey("DistributedOrder", actual.OrderId), SourceKey("DistributedWave", actual.WaveId),
                SourceKey("InboundPlan", lot.PlanId), SourceKey("DistributedSite", lot.SiteId)
            };
            if (!string.IsNullOrWhiteSpace(lot.StorageAgreementId)) allowed.Add(SourceKey("StorageAgreement", lot.StorageAgreementId));
            // 같은 회차의 다른 주문이나 근거 미확인 자료를 이 주문의 하위 원장으로 붙이지 않습니다.
            target.Children = cycleSourceKeys.Where(key => allowed.Contains(key) && shared[key].Verified)
                .Select(key => shared[key]).ToArray();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception) { target.RootReadStatusCode = "temporarily-unavailable"; target.Children = []; }
    }

    private static string SourceKey(string kind, string id) => "source:" + kind + ":" + id;
}
