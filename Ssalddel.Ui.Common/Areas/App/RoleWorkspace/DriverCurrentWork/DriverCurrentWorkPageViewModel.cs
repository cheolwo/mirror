using System.Globalization;
using Ssalddel.Contracts.Common.Warehouse;
using Ssalddel.Contracts.Driver.Food;
using Ssalddel.Contracts.Driver.Transport;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Cargo;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.DistributedFulfillment;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Food;

namespace Ssalddel.Ui.Common.Areas.App.RoleWorkspace.DriverCurrentWork;

public enum DriverCurrentWorkLoadState { NotLoaded, Loading, Ready, Empty, LoginRequired, OtherAccount, Forbidden, Unavailable, Error }
public sealed record DriverCurrentWorkItem(string Id, string Title, string Status, string NextAction, string Href);
public sealed record DriverCurrentWorkSection(string Key, string Title, string Unit, DriverCurrentWorkLoadState State,
    IReadOnlyList<DriverCurrentWorkItem> Items, string Notice, string LoginHref);

/// <summary>한 계정의 역할별 현재 업무 조회와 페이지 수명만 소유합니다. 통합 수락·순서·합계는 만들지 않습니다.</summary>
public sealed class DriverCurrentWorkPageViewModel : IDisposable
{
    public const string Route = "/driver/current-work";
    private const string Food = RoleWorkspaceCatalog.FoodDriver, Cargo = RoleWorkspaceCatalog.CargoDriver;
    private readonly IDriverCurrentWorkClient client;
    private readonly TimeSpan requestTimeout;
    private readonly object stateGate = new();
    private readonly CancellationTokenSource lifetime = new();
    private CancellationTokenSource? operationCancellation;
    private Dictionary<string, RoleWorkspaceIdentity>? ownerSnapshot;
    private long generation;
    private bool initializing, disposed;
    private DriverCurrentWorkSection[] sections = InitialSections();

    public DriverCurrentWorkPageViewModel(IDriverCurrentWorkClient client, TimeSpan? requestTimeout = null)
    {
        this.client = client; this.requestTimeout = requestTimeout ?? TimeSpan.FromSeconds(15);
        if (this.requestTimeout <= TimeSpan.Zero || this.requestTimeout > TimeSpan.FromMinutes(1)) throw new ArgumentOutOfRangeException(nameof(requestTimeout));
        client.Changed += IdentityChanged;
    }
    public event Action? Changed;
    public bool IsBusy { get; private set; }
    public IReadOnlyList<DriverCurrentWorkSection> Sections => ownerSnapshot is not null
        && ownerSnapshot.Any(pair => pair.Value != client.Identity(pair.Key)) ? InitialSections() : sections;
    public string? AccountNotice { get; private set; }

    public async Task LoadAsync()
    {
        if (disposed || IsBusy) return;
        IsBusy = initializing = true;
        sections = InitialSections(DriverCurrentWorkLoadState.Loading);
        AccountNotice = null; Notify();
        var initErrors = new Dictionary<string, Exception>();
        foreach (var role in new[] { Cargo, Food })
        {
            using var initializationTimeout = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
            initializationTimeout.CancelAfter(requestTimeout);
            try { await client.InitializeAsync(role, initializationTimeout.Token).WaitAsync(initializationTimeout.Token); }
            catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { return; }
            catch (Exception error) { initErrors[role] = error; }
            if (disposed) return;
        }
        initializing = false;
        ownerSnapshot = Capture();
        var snapshot = ownerSnapshot;
        var reference = Valid(snapshot[Cargo]) ? snapshot[Cargo] : Valid(snapshot[Food]) ? snapshot[Food] : null;
        var operation = ++generation;
        operationCancellation?.Dispose();
        operationCancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        var token = operationCancellation.Token;
        var owner = reference?.OwnerId;
        if (Valid(snapshot[Food]) && Valid(snapshot[Cargo]) && snapshot[Food].OwnerId != snapshot[Cargo].OwnerId)
            AccountNotice = "현재 운송 계정을 기준으로 보여줍니다. 음식에도 같은 계정으로 로그인하면 함께 확인할 수 있습니다.";
        await Task.WhenAll(LoadSectionAsync("food", Food, snapshot, owner, operation, token, initErrors),
            LoadSectionAsync("cargo", Cargo, snapshot, owner, operation, token, initErrors),
            LoadSectionAsync("picking", Cargo, snapshot, owner, operation, token, initErrors));
        if (disposed || generation != operation) return;
        if (!Current(snapshot, operation, token)) { IdentityChanged(); return; }
        IsBusy = false; Notify();
    }

    private async Task LoadSectionAsync(string key, string role, Dictionary<string, RoleWorkspaceIdentity> snapshot,
        string? owner, long operation, CancellationToken token, Dictionary<string, Exception> initErrors)
    {
        if (!Current(snapshot, operation, token)) return;
        if (initErrors.TryGetValue(role, out var initError)) { Publish(key, Failure(initError), [], snapshot, operation, token); return; }
        if (!Valid(snapshot[role])) { Publish(key, DriverCurrentWorkLoadState.LoginRequired, [], snapshot, operation, token); return; }
        if (snapshot[role].OwnerId != owner) { Publish(key, DriverCurrentWorkLoadState.OtherAccount, [], snapshot, operation, token); return; }
        try
        {
            using var queryTimeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            queryTimeout.CancelAfter(requestTimeout);
            var queryToken = queryTimeout.Token;
            IReadOnlyList<DriverCurrentWorkItem> items = key switch
            {
                "food" => FoodItems(await client.FoodAsync(queryToken).WaitAsync(queryToken), owner!),
                "cargo" => CargoItems(await client.CargoAsync(queryToken).WaitAsync(queryToken), owner!),
                _ => PickingItems(await client.PickingAsync(queryToken).WaitAsync(queryToken), owner!)
            };
            Publish(key, items.Count == 0 ? DriverCurrentWorkLoadState.Empty : DriverCurrentWorkLoadState.Ready,
                items, snapshot, operation, token);
        }
        catch (OperationCanceledException) when (!Current(snapshot, operation, token)) { }
        catch (Exception error) { Publish(key, Failure(error), [], snapshot, operation, token); }
    }

    private void Publish(string key, DriverCurrentWorkLoadState state, IReadOnlyList<DriverCurrentWorkItem> items,
        Dictionary<string, RoleWorkspaceIdentity> snapshot, long operation, CancellationToken token)
    {
        lock (stateGate)
        {
            if (!Current(snapshot, operation, token)) return;
            sections = sections.Select(section => section.Key == key ? section with
            { State = state, Items = items, Notice = Notice(state) } : section).ToArray();
        }
        Notify();
    }

    private static IReadOnlyList<DriverCurrentWorkItem> FoodItems(FoodDeliveryDriverCurrentWorkDto value, string owner)
    {
        if (value is null || value.DriverId != owner) throw new InvalidDataException();
        return value.ActiveDeliveries.Where(item => !CargoWorkspacePresentation.IsClosed(item.TransportStatus)
                && item.WorkStatus is not "Completed" and not "Cancelled")
            .Select(item =>
            {
                var id = RequireId(item.OfferId);
                var stage = FoodDriverStagePresentation.Resolve(item, DateTime.UtcNow);
                return new DriverCurrentWorkItem(id, string.IsNullOrWhiteSpace(item.RestaurantName) ? "음식 배달" : item.RestaurantName,
                    FoodDriverStagePresentation.Text(stage, item.RestaurantArrivedAtUtc.HasValue),
                    "배달 단계 확인", RoleWorkspaceNavigation.Href(Food, id));
            }).DistinctBy(item => item.Id, StringComparer.Ordinal).ToArray();
    }

    private static IReadOnlyList<DriverCurrentWorkItem> CargoItems(기사화물운송작업공간응답 value, string owner)
    {
        if (value is null || value.활성운송목록.Any(item => item.기사_운송자 != owner)) throw new InvalidDataException();
        return value.활성운송목록.Where(item => !CargoWorkspacePresentation.IsClosed(item.상태)).Select(item =>
        {
            if (item.Id <= 0) throw new InvalidDataException();
            var id = item.Id.ToString(CultureInfo.InvariantCulture);
            var status = item.개인정보제공보류 ? "접근 확인 필요" : item.운송진행보류 ? "진행 조건 확인 필요" : item.상태 switch
            {
                "배차대기" or "배차확정" or "확정" or "매칭중" or "이동중" => "상차지 이동 중",
                "상차지도착" => "상차 확인 대기",
                "상차완료" or "운송중" => "하차지 이동 중",
                "하차지도착" => "하차 확인 대기",
                _ => "현재 운송 확인 필요"
            };
            return new DriverCurrentWorkItem(id, "화물 운송", status, "운송 단계 확인", RoleWorkspaceNavigation.Href(Cargo, id));
        }).DistinctBy(item => item.Id, StringComparer.Ordinal).ToArray();
    }

    private static IReadOnlyList<DriverCurrentWorkItem> PickingItems(DistributedWorkspaceDto value, string owner)
    {
        if (value is null || value.CurrentUserId != owner || value.Waves.Any(wave => wave.DriverId != owner)) throw new InvalidDataException();
        return value.Waves.Where(wave => wave.StatusCode != "Offered" && (wave.StatusCode is "Accepted" or "InProgress" or "AcceptancePending"
                || HasHeldGoods(wave)))
            .Select(wave =>
            {
                var id = RequireId(wave.WaveId);
                var heldReturn = wave.StatusCode == "Cancelled" && HasHeldGoods(wave)
                    || wave.Orders.Any(order => (order.StatusCode is "ReturnRequired" or "Cancelled")
                        && (order.PickedQuantity > order.DeliveredQuantity + order.ReturnedQuantity
                            || wave.Reservations.Any(line => line.OrderId == order.OrderId
                                && line.PickedQuantity > line.DeliveredQuantity + line.ReturnedQuantity)));
                var current = wave.Stops.FirstOrDefault(stop => stop.StopId == wave.CurrentStopId);
                var canPick = current is { Kind: DistributedFulfillmentCodes.Pickup, StatusCode: "Pending" }
                    && wave.AllowedActions.Contains("pick", StringComparer.Ordinal);
                var canDeliver = current is { Kind: DistributedFulfillmentCodes.Delivery, StatusCode: "Pending" }
                    && wave.AllowedActions.Contains("deliver", StringComparer.Ordinal);
                var status = wave.StatusCode == "AcceptancePending" ? "수락 결과 확인 필요" : heldReturn ? "물품 반납 확인 필요"
                    : canPick ? "현재 거점에서 직접 피킹" : canDeliver ? "현재 고객에게 전달"
                    : HasHeldGoods(wave) ? "보유 물품과 진행 조건 확인 필요" : "순회 진행 조건 확인 필요";
                return new DriverCurrentWorkItem(id, "피킹·배송 순회", status,
                    heldReturn ? "반납 상태 확인" : wave.StatusCode == "AcceptancePending" ? "수락 결과 확인"
                        : canPick || canDeliver ? "순회 단계 확인" : "수행 조건 확인",
                    분산이행Navigation.Href(Cargo, "round", id));
            }).DistinctBy(item => item.Id, StringComparer.Ordinal).ToArray();
    }

    // 만료·배정 해제 투영은 Reservations를 숨겨도 Orders의 보유 수량은 남깁니다.
    // 어느 쪽의 미정리 물품도 0으로 만들지 않으며, 이 표시는 수행 권한을 부여하지 않습니다.
    private static bool HasHeldGoods(DistributedWaveDto wave)
        => wave.Orders.Any(order => order.PickedQuantity > order.DeliveredQuantity + order.ReturnedQuantity)
            || wave.Reservations.Any(line => line.PickedQuantity > line.DeliveredQuantity + line.ReturnedQuantity);

    private static string RequireId(string? value) => RoleWorkspaceNavigation.StableId(value) is { } id
        && id is not "." and not ".." ? id : throw new InvalidDataException();
    private static bool Valid(RoleWorkspaceIdentity value) => value.IsAuthenticated && !string.IsNullOrWhiteSpace(value.OwnerId);
    private Dictionary<string, RoleWorkspaceIdentity> Capture() => new() { [Food] = client.Identity(Food), [Cargo] = client.Identity(Cargo) };
    private bool Current(Dictionary<string, RoleWorkspaceIdentity> snapshot, long operation, CancellationToken token)
        => !disposed && !token.IsCancellationRequested && generation == operation
            && snapshot.All(pair => pair.Value == client.Identity(pair.Key));
    private static DriverCurrentWorkLoadState Failure(Exception error) => error is RoleWorkspaceAccessException access ? access.StatusCode switch
    { 401 => DriverCurrentWorkLoadState.LoginRequired, 403 => DriverCurrentWorkLoadState.Forbidden,
        404 => DriverCurrentWorkLoadState.Unavailable, _ => DriverCurrentWorkLoadState.Error } : DriverCurrentWorkLoadState.Error;
    private static string Notice(DriverCurrentWorkLoadState state) => state switch
    {
        DriverCurrentWorkLoadState.Loading => "확인 중입니다.",
        DriverCurrentWorkLoadState.Empty => "현재 확인된 업무가 없습니다.",
        DriverCurrentWorkLoadState.LoginRequired => "해당 기사 계정으로 로그인해 주세요.",
        DriverCurrentWorkLoadState.OtherAccount => "다른 계정의 업무는 함께 표시하지 않습니다. 같은 계정으로 로그인해 주세요.",
        DriverCurrentWorkLoadState.Forbidden => "이 업무의 수행 자격을 확인해 주세요.",
        DriverCurrentWorkLoadState.Unavailable => "이 업무의 조회가 아직 제공되지 않습니다.",
        DriverCurrentWorkLoadState.Error => "업무를 확인하지 못했습니다. 다시 확인해 주세요.",
        DriverCurrentWorkLoadState.NotLoaded => "현재 계정으로 업무를 확인해 주세요.",
        _ => ""
    };
    private static DriverCurrentWorkSection[] InitialSections(DriverCurrentWorkLoadState state = DriverCurrentWorkLoadState.NotLoaded)
        => [Create("food", "음식 배달", "건", Food, state), Create("cargo", "퀵·화물 운송", "건", Cargo, state), Create("picking", "직접 피킹·배송", "개 순회", Cargo, state)];
    private static DriverCurrentWorkSection Create(string key, string title, string unit, string role, DriverCurrentWorkLoadState state)
        => new(key, title, unit, state, [], Notice(state),
            RoleWorkspaceNavigation.WithReturn("/workspace-login/" + role, Route)!);
    private void IdentityChanged()
    {
        lock (stateGate)
        {
            if (disposed || ownerSnapshot is not null && ownerSnapshot.All(pair => pair.Value == client.Identity(pair.Key))) return;
            ++generation; operationCancellation?.Cancel(); ownerSnapshot = null;
            sections = InitialSections(); AccountNotice = null;
            if (!initializing) IsBusy = false;
        }
        Notify();
    }
    private void Notify() => Changed?.Invoke();
    public void Dispose()
    {
        if (disposed) return; disposed = true; ++generation;
        client.Changed -= IdentityChanged; operationCancellation?.Cancel(); operationCancellation?.Dispose();
        lifetime.Cancel(); lifetime.Dispose(); sections = InitialSections();
    }
}
