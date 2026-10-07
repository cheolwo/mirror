using System.Globalization;
using Ssalddel.Contracts.Common.Inbound;
using Ssalddel.Contracts.Common.Inventory;
using Ssalddel.Contracts.Common.Warehouse;
using Ssalddel.Contracts.Driver.Action;
using Ssalddel.Contracts.Driver.Recommendation;
using Ssalddel.Contracts.Driver.Transport;
using Ssalddel.Contracts.Shipper.Request;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;

namespace Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Cargo;

/// <summary>화주 역할의 인증 세션으로 기존 운송 의뢰 계약만 조회합니다.</summary>
public sealed class ShipperWorkspaceApiClient(IRoleWorkspaceApi api)
{
    public Task<IReadOnlyList<화주운송의뢰응답>> ListAsync(CancellationToken ct)
        => api.GetAsync<IReadOnlyList<화주운송의뢰응답>>(RoleWorkspaceCatalog.Shipper,
            "api/v1/shipper/requests?page=1&pageSize=200", ct);

    public Task<화주운송의뢰응답> GetAsync(string id, CancellationToken ct)
        => api.GetAsync<화주운송의뢰응답>(RoleWorkspaceCatalog.Shipper,
            "api/v1/shipper/requests/" + CargoWorkspaceRoutes.Segment(id), ct);
}

/// <summary>음식 배달 endpoint와 구분된 기존 화물기사 endpoint를 소비합니다.</summary>
public sealed class CargoDriverWorkspaceApiClient(IRoleWorkspaceApi api)
{
    public Task<기사화물운송작업공간응답> WorkspaceAsync(CancellationToken ct)
        => api.GetAsync<기사화물운송작업공간응답>(RoleWorkspaceCatalog.CargoDriver,
            "api/v1/driver/transports/workspace", ct);

    public Task<기사운송상세응답> TransportAsync(long id, CancellationToken ct)
        => api.GetAsync<기사운송상세응답>(RoleWorkspaceCatalog.CargoDriver, TransportPath(id), ct);

    public Task<IReadOnlyList<기사배차추천항목응답>> RecommendationsAsync(CancellationToken ct)
        => api.GetAsync<IReadOnlyList<기사배차추천항목응답>>(RoleWorkspaceCatalog.CargoDriver,
            "api/v1/driver/recommendations", ct);

    public Task<기사운송의뢰상세응답> RequestAsync(string id, CancellationToken ct)
        => api.GetAsync<기사운송의뢰상세응답>(RoleWorkspaceCatalog.CargoDriver,
            "api/v1/driver/requests/" + CargoWorkspaceRoutes.Segment(id), ct);

    public Task<기사운송상태변경응답?> ArriveAsync(long id, bool pickup, CancellationToken ct)
        => api.PostAsync<기사운송상태변경응답>(RoleWorkspaceCatalog.CargoDriver,
            TransportPath(id) + (pickup ? "/arrive-pickup" : "/arrive-dropoff"), new { }, ct);

    public Task<기사운송상태변경응답?> PickupAsync(long id, 기사운송상차완료요청 body, CancellationToken ct)
        => api.PostAsync<기사운송상태변경응답>(RoleWorkspaceCatalog.CargoDriver,
            TransportPath(id) + "/pickup-complete", body, ct);

    public Task<기사운송상태변경응답?> DropoffAsync(long id, 기사운송하차완료요청 body, CancellationToken ct)
        => api.PostAsync<기사운송상태변경응답>(RoleWorkspaceCatalog.CargoDriver,
            TransportPath(id) + "/complete", body, ct);

    public Task<기사운송요약응답?> IssueAsync(long id, 기사운송문제신고요청 body, CancellationToken ct)
        => api.PostAsync<기사운송요약응답>(RoleWorkspaceCatalog.CargoDriver,
            TransportPath(id) + "/report-issue", body, ct);

    public Task<기사배차처리응답?> AcceptAsync(string id, 기사화물배차수락요청 body, CancellationToken ct)
        => api.PostAsync<기사배차처리응답>(RoleWorkspaceCatalog.CargoDriver,
            "api/v1/driver/dispatch-actions/" + CargoWorkspaceRoutes.Segment(id) + "/accept", body, ct);

    private static string TransportPath(long id)
        => "api/v1/driver/transports/" + CargoWorkspaceRoutes.Number(id);
}

/// <summary>창고 세션과 활성 공정 권한을 사용하며 다른 역할의 자격을 재사용하지 않습니다.</summary>
public sealed class WarehouseWorkspaceApiClient(IRoleWorkspaceApi api)
{
    private const string Root = "api/v1/warehouse-operations/";

    public async Task<창고작업진입확인응답> VerifyAsync(string process, CancellationToken ct)
        => await api.PostAsync<창고작업진입확인응답>(RoleWorkspaceCatalog.Warehouse,
            Root + "work-entry/verify", new 창고작업진입확인요청 { ProcessCode = process }, ct)
           ?? throw new InvalidOperationException("창고 작업 권한 확인 응답이 없습니다.");

    public Task<창고목록응답> WarehousesAsync(CancellationToken ct)
        => GetAsync<창고목록응답>("warehouses", ct);
    public Task<입고요청목록응답> InboundsAsync(CancellationToken ct)
        => GetAsync<입고요청목록응답>("inbounds", ct);
    public Task<입고검수대상페이지응답> InspectionsAsync(CancellationToken ct)
        => GetAsync<입고검수대상페이지응답>("inventory/inspection-targets?inspectionStatus=대기&page=0&pageSize=50", ct);
    public Task<적재작업목록페이지응답> PutAwayAsync(CancellationToken ct)
        => GetAsync<적재작업목록페이지응답>("put-away-tasks?status=대기&page=0&pageSize=50", ct);
    public Task<피킹작업목록페이지응답> PickingAsync(CancellationToken ct)
        => GetAsync<피킹작업목록페이지응답>("picking-tasks?status=전체&page=0&pageSize=50", ct);
    public Task<포장작업목록페이지응답> PackingAsync(CancellationToken ct)
        => GetAsync<포장작업목록페이지응답>("packing-tasks?status=대기&page=0&pageSize=50", ct);
    public Task<출고인계준비목록페이지응답> HandoffAsync(CancellationToken ct)
        => GetAsync<출고인계준비목록페이지응답>("outbound-handoff-tasks?status=대기&page=0&pageSize=50", ct);
    public Task<출고예정검토목록페이지응답> OutboundReviewsAsync(CancellationToken ct)
        => GetAsync<출고예정검토목록페이지응답>("outbound-plan-reviews?status=전체&page=0&pageSize=50", ct);

    public Task<T> DetailAsync<T>(string kind, string id, CancellationToken ct)
        => GetAsync<T>(TaskPath(kind, id), ct);
    public Task<T?> CompleteAsync<T>(string kind, string id, object body, CancellationToken ct)
        => api.PostAsync<T>(RoleWorkspaceCatalog.Warehouse, Root + TaskPath(kind, id) + "/complete", body, ct);
    public Task<피킹작업결과응답?> StartPickingAsync(string id, CancellationToken ct)
        => api.PostAsync<피킹작업결과응답>(RoleWorkspaceCatalog.Warehouse,
            Root + TaskPath("picking", id) + "/start", new { }, ct);

    private Task<T> GetAsync<T>(string suffix, CancellationToken ct)
        => api.GetAsync<T>(RoleWorkspaceCatalog.Warehouse, Root + suffix, ct);

    private static string TaskPath(string kind, string id) => kind switch
    {
        "inbound" => "inbounds/" + CargoWorkspaceRoutes.PositiveNumber(id),
        "inspection" => "inventory/" + CargoWorkspaceRoutes.PositiveNumber(id) + "/inspection-target",
        "put-away" => "put-away-tasks/" + CargoWorkspaceRoutes.PositiveNumber(id),
        "picking" => "picking-tasks/" + CargoWorkspaceRoutes.Segment(id),
        "packing" => "packing-tasks/" + CargoWorkspaceRoutes.PositiveNumber(id),
        "handoff" => "outbound-handoff-tasks/" + CargoWorkspaceRoutes.PositiveNumber(id),
        "outbound" => "outbound-plan-reviews/" + CargoWorkspaceRoutes.PositiveNumber(id),
        _ => throw new ArgumentException("지원하지 않는 창고 업무입니다.", nameof(kind))
    };
}

public static class CargoWorkspaceRoutes
{
    public static string DriverLogin(long id, string step)
        => Login(RoleWorkspaceCatalog.CargoDriver, () => DriverInput(id, step));
    public static string OfferLogin(string id)
        => Login(RoleWorkspaceCatalog.CargoDriver, () => Offer(id));
    public static string WarehouseLogin(string kind, string id)
        => Login(RoleWorkspaceCatalog.Warehouse, () =>
        {
            if (kind == "transport-draft") _ = PositiveNumber(id);
            else _ = WarehouseWorkspaceAdapter.ParseId(kind + ":" + id);
            return WarehouseInput(kind, id);
        });
    private static string Login(string role, Func<string> inputRoute)
    {
        string destination;
        try { destination = inputRoute(); }
        catch (ArgumentException) { destination = ReturnTo(role); }
        return RoleWorkspaceNavigation.WithReturn("/workspace-login/" + role, destination)!;
    }
    public static string DriverInput(long id, string step)
        => step is "pickup" or "dropoff" or "issue"
            ? $"/workspace/cargo-driver/transports/{Number(id)}/{step}"
            : throw new ArgumentException("지원하지 않는 운송 업무입니다.", nameof(step));
    public static string Offer(string id) => "/workspace/cargo-driver/offers/" + Segment(id);
    public static string WarehouseInput(string kind, string id)
        => kind is "inbound" or "inspection" or "put-away" or "picking" or "packing" or "handoff" or "outbound" or "transport-draft"
            ? $"/workspace/warehouse/tasks/{kind}/{Segment(id)}"
            : throw new ArgumentException("지원하지 않는 창고 업무입니다.", nameof(kind));
    public static string ReturnTo(string role, string? itemId = null)
        => RoleWorkspaceNavigation.Href(role, itemId);
    public static string Segment(string value)
        => !string.IsNullOrWhiteSpace(value) ? Uri.EscapeDataString(value.Trim())
            : throw new ArgumentException("업무 식별자가 필요합니다.", nameof(value));
    public static string Number(long value)
        => value > 0 ? value.ToString(CultureInfo.InvariantCulture)
            : throw new ArgumentOutOfRangeException(nameof(value));
    public static string PositiveNumber(string value)
        => long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed)
            ? Number(parsed) : throw new ArgumentException("업무 식별자가 올바르지 않습니다.", nameof(value));
}
