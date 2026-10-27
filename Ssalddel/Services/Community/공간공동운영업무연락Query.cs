using System.Security.Cryptography;
using System.Text;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Application.Driver.Transport;
using Ssalddel.Application.Shipper.Request;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Driver.Transport;
using Ssalddel.Contracts.Shipper.Request;
using Ssalddel.Services.LogisticsProcessing.Warehouse;
using 살뜰.Data;
using 살뜰.Services.Options;
using 살뜰.도메인.공통;
using 살뜰.도메인.창고;

namespace Ssalddel.Services.Community;

public interface I공간공동운영업무연락Query
{
    Task<공간공동운영업무연락Response?> 조회Async(string ledgerId, string cycleId, string stageKey,
        long expectedRevision, CancellationToken cancellationToken = default);
}

/// <summary>기존 전문 조회가 허용한 등록 업무 연락처만 읽습니다. 역할 참여자나 계정 연락처에서 담당자를 추정하지 않습니다.</summary>
public sealed class 공간공동운영업무연락Query(I공간공동운영UseCase cooperation, I공간공동운영통합Query integrated,
    SsalddelContext db, IWarehouseOperationService warehouses, ISender sender, ICurrentUserAccessor currentUser,
    ISsalddelExecutionModePolicy execution, TimeProvider clock) : I공간공동운영업무연락Query
{
    private static readonly IReadOnlyDictionary<string, string[]> StageKinds = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        ["production"] = [], ["inbound"] = ["InboundPlan"], ["storage"] = ["DistributedSite", "StorageAgreement"],
        ["order"] = ["RootOrderLedger"], ["outbound"] = ["DistributedWave"],
        ["delivery"] = ["DistributedOrder", "MartOrder"], ["settlement"] = ["Settlement"]
    };

    public async Task<공간공동운영업무연락Response?> 조회Async(string ledgerId, string cycleId, string stageKey,
        long expectedRevision, CancellationToken cancellationToken = default)
    {
        var actor = currentUser.UserId?.Trim();
        if (string.IsNullOrWhiteSpace(actor)) return null;
        if (!Id(ledgerId) || !Id(cycleId) || expectedRevision <= 0 || !StageKinds.ContainsKey(stageKey ?? ""))
            throw new 공간공동운영업무연락Exception(400);
        var detail = await cooperation.상세Async(ledgerId, cancellationToken);
        if (detail is null) return null;
        if (detail.LedgerId != ledgerId || detail.Revision != expectedRevision || detail.ExecutionMode != execution.Mode.ToString())
            throw new 공간공동운영업무연락Exception(409);
        var snapshot = await integrated.조회Async(ledgerId, cancellationToken);
        if (snapshot is null) return null;
        if (snapshot.LedgerId != ledgerId || snapshot.Revision != expectedRevision || snapshot.ExecutionMode != detail.ExecutionMode)
            throw new 공간공동운영업무연락Exception(409);
        var cycles = detail.Cycles.Where(cycle => cycle.CycleId == cycleId).ToArray();
        var projected = snapshot.Cycles.Where(cycle => cycle.CycleId == cycleId).ToArray();
        if (cycles.Length != 1 || projected.Length != 1) return null;
        var cycle = cycles[0]; var selected = projected[0];
        if (selected.NodeKey != "cycle:" + cycleId || selected.Number != cycle.Number
            || selected.TermsRevision != cycle.TermsRevision || selected.StatusCode != cycle.StatusCode
            || selected.SharedSourceKeys.Count != selected.SharedSourceKeys.Distinct(StringComparer.Ordinal).Count())
            throw new 공간공동운영업무연락Exception(409);
        var allowedKeys = cycle.References.Select(reference => SourceKey(reference.KindCode, reference.ReferenceId)).ToHashSet(StringComparer.Ordinal);
        if (selected.SharedSourceKeys.Any(key => !allowedKeys.Contains(key))) throw new 공간공동운영업무연락Exception(409);
        var sourceRows = selected.SharedSourceKeys.Select(key => snapshot.SharedSources.Where(source => source.NodeKey == key).ToArray()).ToArray();
        if (sourceRows.Any(rows => rows.Length != 1 || rows[0].NodeKey != SourceKey(rows[0].KindCode, rows[0].ReferenceId)))
            throw new 공간공동운영업무연락Exception(409);
        var scopedSources = sourceRows.Select(rows => rows[0]).ToArray();
        var proofs = new Dictionary<string, SourceProof>(StringComparer.Ordinal);
        var guards = new List<Func<CancellationToken, Task<bool>>>();
        var contacts = new List<공간공동운영업무연락Dto>();
        var results = new List<공간공동운영업무연락원천Dto>();
        foreach (var source in scopedSources.Where(source => StageKinds[stageKey].Contains(source.KindCode, StringComparer.Ordinal)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var status = "unsupported";
            if (source.KindCode is "DistributedSite" or "DistributedOrder" or "DistributedWave")
            {
                var proof = Proof(detail, cycleId, source);
                if (proof is null) status = "not-visible";
                else
                {
                    proofs[source.NodeKey] = proof;
                    var before = contacts.Count;
                    try
                    {
                        status = source.KindCode switch
                        {
                            "DistributedSite" => await SiteContact(actor, source, proof, contacts, guards, cancellationToken),
                            "DistributedOrder" => await OrderContact(actor, source, proof, null, contacts, guards, cancellationToken),
                            _ => await WaveContacts(actor, source, proof, detail, cycleId, scopedSources, proofs, contacts, guards, cancellationToken)
                        };
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                    catch (Exception) { contacts.RemoveRange(before, contacts.Count - before); status = "temporarily-unavailable"; }
                }
            }
            results.Add(new() { SourceNodeKey = source.NodeKey, KindCode = source.KindCode, StatusCode = status });
        }
        // 전문 조회 중 상위 조건·연결·권한·원천 판본이 달라지면 전화번호를 반환하지 않습니다.
        var final = await cooperation.상세Async(ledgerId, cancellationToken);
        if (actor != currentUser.UserId?.Trim() || final is null || final.LedgerId != ledgerId || final.Revision != expectedRevision
            || final.ExecutionMode != execution.Mode.ToString() || !SameCycle(cycle, final.Cycles.Where(item => item.CycleId == cycleId).ToArray()))
            throw new 공간공동운영업무연락Exception(409);
        foreach (var proof in proofs.Values)
            if (!SameProof(proof, ReadProof(final, cycleId, proof.Kind, proof.Id))) throw new 공간공동운영업무연락Exception(409);
        foreach (var guard in guards)
        {
            try { if (!await guard(cancellationToken)) throw new 공간공동운영업무연락Exception(409); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (공간공동운영업무연락Exception) { throw; }
            catch (Exception) { throw new 공간공동운영업무연락Exception(409); }
        }
        if (actor != currentUser.UserId?.Trim() || final.ExecutionMode != execution.Mode.ToString())
            throw new 공간공동운영업무연락Exception(409);
        return new()
        {
            LedgerId = ledgerId, CycleId = cycleId, StageKey = stageKey, Revision = expectedRevision, AsOfUtc = clock.GetUtcNow(),
            Contacts = contacts.DistinctBy(contact => contact.ContactKey).ToArray(), Sources = results,
            StatusCode = contacts.Count > 0 ? "available" : results.Count == 0
                ? stageKey is "storage" or "delivery" or "outbound" ? "not-linked" : "unsupported"
                : results.Any(result => result.StatusCode == "temporarily-unavailable") ? "temporarily-unavailable"
                : results.Any(result => result.StatusCode == "source-changed") ? "source-changed"
                : results.Any(result => result.StatusCode == "not-visible") ? "not-visible"
                : results.Any(result => result.StatusCode == "unregistered") ? "unregistered"
                : results.Any(result => result.StatusCode == "not-linked") ? "not-linked" : "unsupported"
        };
    }

    private async Task<string> SiteContact(string actor, 공간공동운영통합원천Dto source, SourceProof proof,
        List<공간공동운영업무연락Dto> output, List<Func<CancellationToken, Task<bool>>> guards, CancellationToken ct)
    {
        var site = await db.Set<분산거점Record>().AsNoTracking().SingleOrDefaultAsync(row => row.SiteId == source.ReferenceId, ct);
        if (site is null || site.Revision != proof.Revision || site.WarehouseId <= 0 || site.StatusCode != "Active"
            || !site.OwnerConsented || !site.ManagerConsented) return "source-changed";
        var warehouse = await db.창고.AsNoTracking().Where(row => row.Id == site.WarehouseId)
            .Select(row => new WarehouseAnchor(row.Id, row.UpdatedAt, row.IsActive)).SingleOrDefaultAsync(ct);
        if (warehouse is null || !warehouse.Active) return "source-changed";
        var visible = (await warehouses.GetWarehousesAsync(ct)).Items.Where(row => row.Id == site.WarehouseId && row.IsActive).ToArray();
        if (visible.Length != 1) return "not-visible";
        guards.Add(token => db.Set<분산거점Record>().AsNoTracking().AnyAsync(row => row.SiteId == site.SiteId
            && row.Revision == site.Revision && row.WarehouseId == site.WarehouseId && row.StatusCode == "Active"
            && row.OwnerConsented && row.ManagerConsented, token));
        guards.Add(token => db.창고.AsNoTracking().AnyAsync(row => row.Id == warehouse.Id && row.IsActive && row.UpdatedAt == warehouse.UpdatedAt, token));
        var displayName = visible[0].담당자명; var phone = visible[0].연락처;
        guards.Add(async token =>
        {
            // 창고사용자 권한은 창고 UpdatedAt과 별도로 바뀔 수 있으므로 같은 현재 계정의 전문 포트를 다시 조회합니다.
            var latest = (await warehouses.GetWarehousesAsync(token)).Items.Where(row => row.Id == site.WarehouseId && row.IsActive).ToArray();
            return latest.Length == 1 && latest[0].담당자명 == displayName && latest[0].연락처 == phone;
        });
        Add(output, source, proof.Revision, "warehouse-business", "등록 창고 업무 연락처", visible[0].담당자명, visible[0].연락처,
            site.WarehouseId + ":" + warehouse.UpdatedAt.Ticks);
        return string.IsNullOrWhiteSpace(visible[0].연락처) ? "unregistered" : "available";
    }

    private async Task<string> WaveContacts(string actor, 공간공동운영통합원천Dto source, SourceProof proof,
        공간공동운영상세Response detail, string cycleId, IReadOnlyList<공간공동운영통합원천Dto> scoped,
        Dictionary<string, SourceProof> proofs, List<공간공동운영업무연락Dto> output,
        List<Func<CancellationToken, Task<bool>>> guards, CancellationToken ct)
    {
        var wave = await db.Set<분산배송WaveRecord>().AsNoTracking().SingleOrDefaultAsync(row => row.WaveId == source.ReferenceId, ct);
        if (wave is null || wave.Revision != proof.Revision || wave.StatusCode is "Cancelled" or "Rejected" or "Expired") return "source-changed";
        guards.Add(token => db.Set<분산배송WaveRecord>().AsNoTracking().AnyAsync(row => row.WaveId == wave.WaveId
            && row.Revision == wave.Revision && row.StatusCode == wave.StatusCode && row.DriverId == wave.DriverId, token));
        var statuses = new List<string>();
        // 묶음의 전체 주문을 열거하지 않고 이번 회차에서 이미 조회 허용된 주문 원천만 사용합니다.
        foreach (var orderSource in scoped.Where(item => item.KindCode == "DistributedOrder"))
        {
            var orderProof = Proof(detail, cycleId, orderSource);
            if (orderProof is null) continue;
            var belongs = await db.Set<분산배송주문Record>().AsNoTracking().AnyAsync(row => row.OrderId == orderSource.ReferenceId
                && row.WaveId == wave.WaveId && row.Revision == orderProof.Revision, ct);
            if (!belongs) continue;
            proofs[orderSource.NodeKey] = orderProof;
            var before = output.Count;
            statuses.Add(await OrderContact(actor, source, orderProof, orderSource.ReferenceId, output, guards, ct));
            for (var index = before; index < output.Count; index++)
                output[index].ContactKey = "work-contact:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
                    output[index].ContactKey + ":wave-revision:" + proof.Revision)));
        }
        return statuses.Contains("available") ? "available" : statuses.Contains("source-changed") ? "source-changed"
            : statuses.Contains("not-visible") ? "not-visible" : statuses.Contains("unregistered") ? "unregistered" : "not-linked";
    }

    private async Task<string> OrderContact(string actor, 공간공동운영통합원천Dto source, SourceProof proof, string? childOrderId,
        List<공간공동운영업무연락Dto> output, List<Func<CancellationToken, Task<bool>>> guards, CancellationToken ct)
    {
        var id = childOrderId ?? source.ReferenceId;
        var order = await db.Set<분산배송주문Record>().AsNoTracking().SingleOrDefaultAsync(row => row.OrderId == id, ct);
        if (order is null || order.Revision != proof.Revision || !Id(order.CargoRequestId)
            || order.StatusCode is "Cancelled" or "Rejected" or "Expired") return "source-changed";
        var cargo = await db.화주운송의뢰.AsNoTracking().Where(row => row.의뢰Id == order.CargoRequestId)
            .Select(row => new CargoAnchor(row.의뢰Id, row.화주Id, row.주문자UserId, row.UpdatedAt, row.상태, row.배차상태)).SingleOrDefaultAsync(ct);
        if (cargo is null || cargo.Seller != order.SellerUserId || cargo.Buyer != order.CustomerUserId
            || cargo.DispatchState == 상태값.배차상태.취소) return "source-changed";
        var before = output.Count;
        // 기존 현재 계정의 상세 Query가 제공한 현장 연락처만 투영합니다. DB의 전화 필드는 조회하지 않습니다.
        var request = await sender.Send(new 의뢰단건조회Query(cargo.Id), ct);
        if (request is not null)
        {
            if (request.의뢰Id != cargo.Id || request.화주Id != cargo.Seller || request.주문자UserId != cargo.Buyer) return "source-changed";
            var pickupName = request.픽업?.연락처.이름; var pickupPhone = request.픽업?.연락처.전화번호;
            var dropoffName = request.하차?.연락처.이름; var dropoffPhone = request.하차?.연락처.전화번호;
            guards.Add(async token =>
            {
                // 업무담당자 배정의 별도 판본도 기존 Query가 소유합니다. 보관한 번호를 권한 근거로 재사용하지 않습니다.
                var latest = await sender.Send(new 의뢰단건조회Query(cargo.Id), token);
                return latest is not null && latest.의뢰Id == cargo.Id && latest.화주Id == cargo.Seller && latest.주문자UserId == cargo.Buyer
                    && latest.픽업?.연락처.이름 == pickupName && latest.픽업?.연락처.전화번호 == pickupPhone
                    && latest.하차?.연락처.이름 == dropoffName && latest.하차?.연락처.전화번호 == dropoffPhone;
            });
            Add(output, source, SourceRevision(source, proof), "pickup", "등록 상차 현장 연락처",
                request.픽업?.연락처.이름, request.픽업?.연락처.전화번호, id + ":" + proof.Revision + ":" + cargo.Id + ":" + cargo.UpdatedAt.Ticks);
            Add(output, source, SourceRevision(source, proof), "dropoff", "등록 수령 현장 연락처",
                request.하차?.연락처.이름, request.하차?.연락처.전화번호, id + ":" + proof.Revision + ":" + cargo.Id + ":" + cargo.UpdatedAt.Ticks);
        }
        else
        {
            var transports = await db.운송원장.AsNoTracking().Where(row => row.의뢰Id == cargo.Id && row.기사_운송자 == actor
                && row.확정기사Id == actor && row.배차업무유형 == 상태값.배차업무유형.용달운송
                && row.배차큐단계 != 상태값.배차큐단계.종료).Select(row => new { row.Id, row.UpdatedAt }).Take(2).ToArrayAsync(ct);
            if (transports.Length != 1) return "not-visible";
            var transport = transports[0];
            var view = await sender.Send(new 운송상세조회Query(actor, transport.Id), ct);
            if (view is null || view.개인정보제공보류 || view.Id != transport.Id || view.기사_운송자 != actor) return "not-visible";
            if (view.UpdatedAt != transport.UpdatedAt) return "source-changed";
            var pickupName = view.상차담당자명; var pickupPhone = view.상차연락처;
            var dropoffName = view.수령자명; var dropoffPhone = view.수령자연락처;
            guards.Add(async token =>
            {
                // 기사 정보 제공 동의는 운송 UpdatedAt과 별도이므로 본인의 기존 상세 Query로 재확인합니다.
                var latest = await sender.Send(new 운송상세조회Query(actor, transport.Id), token);
                return latest is not null && !latest.개인정보제공보류 && latest.Id == transport.Id && latest.기사_운송자 == actor
                    && latest.UpdatedAt == transport.UpdatedAt && latest.상차담당자명 == pickupName && latest.상차연락처 == pickupPhone
                    && latest.수령자명 == dropoffName && latest.수령자연락처 == dropoffPhone;
            });
            Add(output, source, SourceRevision(source, proof), "pickup", "등록 상차 현장 연락처", view.상차담당자명,
                view.상차연락처, id + ":" + proof.Revision + ":" + cargo.Id + ":" + transport.Id + ":" + transport.UpdatedAt.Ticks);
            Add(output, source, SourceRevision(source, proof), "dropoff", "등록 수령 현장 연락처", view.수령자명,
                view.수령자연락처, id + ":" + proof.Revision + ":" + cargo.Id + ":" + transport.Id + ":" + transport.UpdatedAt.Ticks);
            guards.Add(token => db.운송원장.AsNoTracking().AnyAsync(row => row.Id == transport.Id && row.의뢰Id == cargo.Id
                && row.기사_운송자 == actor && row.확정기사Id == actor && row.UpdatedAt == transport.UpdatedAt
                && row.배차업무유형 == 상태값.배차업무유형.용달운송 && row.배차큐단계 != 상태값.배차큐단계.종료, token));
        }
        guards.Add(token => db.Set<분산배송주문Record>().AsNoTracking().AnyAsync(row => row.OrderId == id
            && row.Revision == order.Revision && row.WaveId == order.WaveId && row.CargoRequestId == cargo.Id
            && row.SellerUserId == cargo.Seller && row.CustomerUserId == cargo.Buyer && row.StatusCode == order.StatusCode, token));
        guards.Add(token => db.화주운송의뢰.AsNoTracking().AnyAsync(row => row.의뢰Id == cargo.Id && row.UpdatedAt == cargo.UpdatedAt
            && row.화주Id == cargo.Seller && row.주문자UserId == cargo.Buyer && row.상태 == cargo.State && row.배차상태 == cargo.DispatchState, token));
        return output.Count > before ? "available" : "unregistered";
    }

    private static long SourceRevision(공간공동운영통합원천Dto source, SourceProof proof)
        => proof.Revision;
    private static void Add(List<공간공동운영업무연락Dto> output, 공간공동운영통합원천Dto source, long revision,
        string purpose, string label, string? displayName, string? phone, string context)
    {
        if (string.IsNullOrWhiteSpace(phone)) return;
        output.Add(new()
        {
            ContactKey = "work-contact:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source.NodeKey + ":" + purpose + ":" + context))),
            SourceNodeKey = source.NodeKey, PurposeCode = purpose, Label = label,
            DisplayName = displayName ?? "", PhoneNumber = phone, SourceRevision = revision, Href = source.Href
        });
    }

    private static SourceProof? Proof(공간공동운영상세Response detail, string cycleId, 공간공동운영통합원천Dto source)
    {
        var proof = ReadProof(detail, cycleId, source.KindCode, source.ReferenceId);
        return source.Verified && !source.RequiresAttention && proof is not null && source.StatusCode == proof.State
            && source.Completed == proof.Completed ? proof : null;
    }
    private static SourceProof? ReadProof(공간공동운영상세Response detail, string cycleId, string kind, string id)
    {
        if (detail.SharedOutcomes is not { MatchesCurrentExecutionMode: true } outcomes || outcomes.ExecutionMode != detail.ExecutionMode) return null;
        var cycles = outcomes.Cycles.Where(cycle => cycle.CycleId == cycleId).ToArray();
        if (cycles.Length != 1) return null;
        var sources = cycles[0].Sources.Where(source => source.KindCode == kind && source.ReferenceId == id).ToArray();
        var current = sources.Length == 1 ? sources[0] : null;
        return current is { Verified: true, RequiresAttention: false, VerifiedRevision: > 0 }
            ? new(kind, id, current.VerifiedRevision.Value, current.StatusCode, current.Completed) : null;
    }
    private static bool SameProof(SourceProof before, SourceProof? after) => before == after;
    private static bool SameCycle(공간공동운영회차Dto before, 공간공동운영회차Dto[] after)
        => after.Length == 1 && before.Number == after[0].Number && before.TermsRevision == after[0].TermsRevision
            && before.StatusCode == after[0].StatusCode && before.References.Select(reference => (reference.KindCode, reference.ReferenceId, reference.ParentId, reference.ExpectedRevision))
                .OrderBy(reference => reference.KindCode).ThenBy(reference => reference.ReferenceId).SequenceEqual(after[0].References
                    .Select(reference => (reference.KindCode, reference.ReferenceId, reference.ParentId, reference.ExpectedRevision))
                    .OrderBy(reference => reference.KindCode).ThenBy(reference => reference.ReferenceId));
    private static string SourceKey(string kind, string id) => "source:" + kind + ":" + id;
    private static bool Id(string? value) => !string.IsNullOrWhiteSpace(value) && value == value.Trim() && value.Length <= 191;
    private sealed record SourceProof(string Kind, string Id, long Revision, string State, bool Completed);
    private sealed record WarehouseAnchor(long Id, DateTime UpdatedAt, bool Active);
    private sealed record CargoAnchor(string Id, string Seller, string Buyer, DateTime UpdatedAt, string State, string DispatchState);
}

public sealed class 공간공동운영업무연락Exception(int statusCode) : Exception("SpaceWorkContactReadRejected")
{ public int StatusCode { get; } = statusCode; }
