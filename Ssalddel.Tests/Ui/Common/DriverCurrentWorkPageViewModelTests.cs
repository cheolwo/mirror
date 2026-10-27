using Ssalddel.Contracts.Common.Warehouse;
using Ssalddel.Contracts.Driver.Food;
using Ssalddel.Contracts.Driver.Transport;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.DriverCurrentWork;

namespace Ssalddel.Tests.Ui.Common;

public sealed class DriverCurrentWorkPageViewModelTests
{
    [Fact]
    public async Task 현재업무만_전문종류와단위를유지하고_정확한화면에연결한다()
    {
        var client = new ScriptedClient();
        client.Food = _ => Task.FromResult(new FoodDeliveryDriverCurrentWorkDto
        {
            DriverId = "owner", Recommendations = [new() { OfferId = "not-accepted" }],
            ActiveDeliveries = [new() { OfferId = "food:one", RestaurantName = "음식점", WorkStatus = "MovingToPickup" }]
        });
        client.Cargo = _ => Task.FromResult(new 기사화물운송작업공간응답
        { 활성운송목록 = [new() { Id = 7, 기사_운송자 = "owner", 상태 = "상차지도착", 출발지 = "고객 상세주소", 수령자연락처 = "010-9999-9999" }] });
        client.Picking = _ => Task.FromResult(new DistributedWorkspaceDto
        {
            CurrentUserId = "owner", Waves = [new() { WaveId = "offered", DriverId = "owner", StatusCode = "Offered" },
                new() { WaveId = "wave-one", DriverId = "owner", StatusCode = "Accepted", CurrentStopId = "pick", AllowedActions = ["pick"],
                    Stops = [new("pick", 1, DistributedFulfillmentCodes.Pickup, "site", [], default, default, default, 0, "Pending", "고객 상세주소", 37, 127)] }]
        });
        using var model = new DriverCurrentWorkPageViewModel(client);
        await model.LoadAsync();

        Assert.All(model.Sections, section => Assert.Equal(DriverCurrentWorkLoadState.Ready, section.State));
        Assert.Equal(["건", "건", "개 순회"], model.Sections.Select(section => section.Unit));
        Assert.Equal("/workspace/food-driver?selected=food%3Aone", Assert.Single(model.Sections[0].Items).Href);
        Assert.Equal("/workspace/cargo-driver?selected=7", Assert.Single(model.Sections[1].Items).Href);
        Assert.Equal("/workspace/cargo-driver/distributed/round?selected=wave-one", Assert.Single(model.Sections[2].Items).Href);
        Assert.Equal("현재 거점에서 직접 피킹", model.Sections[2].Items[0].Status);
        Assert.DoesNotContain(model.Sections.SelectMany(section => section.Items), item => item.Id is "offered" or "not-accepted");
        Assert.DoesNotContain("고객 상세주소", string.Join(' ', model.Sections.SelectMany(section => section.Items)));
        Assert.DoesNotContain("010-9999-9999", string.Join(' ', model.Sections.SelectMany(section => section.Items)));
    }

    [Fact]
    public async Task 다른계정으로로그인한음식업무는조회하거나함께표시하지않는다()
    {
        var client = new ScriptedClient(); client.Identities[RoleWorkspaceCatalog.FoodDriver] = new("different", 99, true);
        using var model = new DriverCurrentWorkPageViewModel(client);
        await model.LoadAsync();
        Assert.Equal(DriverCurrentWorkLoadState.OtherAccount, model.Sections[0].State);
        Assert.DoesNotContain("food", client.Reads);
        Assert.Contains("cargo", client.Reads); Assert.Contains("picking", client.Reads);
        Assert.NotNull(model.AccountNotice);
        Assert.All(model.Sections, section => Assert.Empty(section.Items));
    }

    [Fact]
    public async Task 같은계정의역할별서로다른인증판본은_각세션의현재판본으로검사한다()
    {
        var client = new ScriptedClient(); client.Identities[RoleWorkspaceCatalog.FoodDriver] = new("owner", 44, true);
        using var model = new DriverCurrentWorkPageViewModel(client); await model.LoadAsync();
        Assert.All(model.Sections, section => Assert.Equal(DriverCurrentWorkLoadState.Empty, section.State));
        Assert.Null(model.AccountNotice); Assert.Equal(3, client.Reads.Count);
    }

    [Fact]
    public async Task 미로그인상태는업무0건으로바꾸지않고_전문조회없이로그인으로복귀한다()
    {
        var client = new ScriptedClient();
        client.Identities[RoleWorkspaceCatalog.FoodDriver] = new(null, 0, false);
        client.Identities[RoleWorkspaceCatalog.CargoDriver] = new(null, 0, false);
        using var model = new DriverCurrentWorkPageViewModel(client); await model.LoadAsync();
        Assert.Empty(client.Reads);
        Assert.All(model.Sections, section =>
        {
            Assert.Equal(DriverCurrentWorkLoadState.LoginRequired, section.State);
            Assert.Contains("returnUrl=%2Fdriver%2Fcurrent-work", section.LoginHref);
        });
    }

    [Theory]
    [InlineData(401, DriverCurrentWorkLoadState.LoginRequired)]
    [InlineData(403, DriverCurrentWorkLoadState.Forbidden)]
    [InlineData(404, DriverCurrentWorkLoadState.Unavailable)]
    [InlineData(503, DriverCurrentWorkLoadState.Error)]
    public async Task 한영역실패는다른영역의성공빈목록과구별하고_원시오류를노출하지않는다(int code, DriverCurrentWorkLoadState expected)
    {
        var client = new ScriptedClient { Food = _ => throw new RoleWorkspaceAccessException(code, "private raw English failure") };
        using var model = new DriverCurrentWorkPageViewModel(client); await model.LoadAsync();
        Assert.Equal(expected, model.Sections[0].State);
        Assert.Equal(DriverCurrentWorkLoadState.Empty, model.Sections[1].State);
        Assert.Equal(DriverCurrentWorkLoadState.Empty, model.Sections[2].State);
        Assert.DoesNotContain("private", model.Sections[0].Notice); Assert.False(model.IsBusy);
    }

    [Theory]
    [InlineData("food")]
    [InlineData("cargo")]
    [InlineData("picking")]
    public async Task 서버응답의소유자가다르면개인업무를표시하지않는다(string kind)
    {
        var client = new ScriptedClient();
        if (kind == "food") client.Food = _ => Task.FromResult(new FoodDeliveryDriverCurrentWorkDto { DriverId = "other" });
        if (kind == "cargo") client.Cargo = _ => Task.FromResult(new 기사화물운송작업공간응답 { 활성운송목록 = [new() { Id = 1, 기사_운송자 = "other" }] });
        if (kind == "picking") client.Picking = _ => Task.FromResult(new DistributedWorkspaceDto { CurrentUserId = "owner", Waves = [new() { WaveId = "foreign", DriverId = "other", StatusCode = "Accepted" }] });
        using var model = new DriverCurrentWorkPageViewModel(client); await model.LoadAsync();
        Assert.Equal(DriverCurrentWorkLoadState.Error, model.Sections.Single(section => section.Key == kind).State);
        Assert.All(model.Sections, section => Assert.Empty(section.Items));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 계정변경과같은계정재로그인은즉시내용을지우고_늦은개인응답을버린다(bool sameOwner)
    {
        var late = new TaskCompletionSource<FoodDeliveryDriverCurrentWorkDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new ScriptedClient { Food = _ => late.Task };
        using var model = new DriverCurrentWorkPageViewModel(client);
        var loading = model.LoadAsync();
        Assert.True(model.IsBusy);
        client.Change(RoleWorkspaceCatalog.FoodDriver, new(sameOwner ? "owner" : "changed", 2, true));
        Assert.False(model.IsBusy); Assert.All(model.Sections, section => Assert.Empty(section.Items));
        late.SetResult(new() { DriverId = "owner", ActiveDeliveries = [new() { OfferId = "old", RestaurantName = "이전 음식점" }] });
        await loading;
        Assert.All(model.Sections, section => Assert.Equal(DriverCurrentWorkLoadState.NotLoaded, section.State));
        Assert.DoesNotContain(model.Sections.SelectMany(section => section.Items), item => item.Title == "이전 음식점");
    }

    [Fact]
    public async Task 이벤트없이인증판본이바뀌어도_현재내용과늦은실패는표시하지않는다()
    {
        var late = new TaskCompletionSource<FoodDeliveryDriverCurrentWorkDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new ScriptedClient { Food = _ => late.Task };
        using var model = new DriverCurrentWorkPageViewModel(client); var loading = model.LoadAsync();
        client.Identities[RoleWorkspaceCatalog.CargoDriver] = new("owner", 2, true);
        Assert.All(model.Sections, section => Assert.Empty(section.Items));
        late.SetException(new InvalidOperationException("late private failure")); await loading;
        Assert.False(model.IsBusy);
        Assert.All(model.Sections, section => Assert.Equal(DriverCurrentWorkLoadState.NotLoaded, section.State));
    }

    [Fact]
    public async Task 초기세션복원중변경이벤트는_복원후현재계정조회만허용한다()
    {
        var client = new ScriptedClient();
        client.Identities[RoleWorkspaceCatalog.CargoDriver] = new(null, 0, false);
        client.Initializing = role => { if (role == RoleWorkspaceCatalog.CargoDriver) client.Change(role, new("owner", 7, true)); };
        using var model = new DriverCurrentWorkPageViewModel(client); await model.LoadAsync();
        Assert.Equal(3, client.Reads.Count); Assert.False(model.IsBusy);
        Assert.All(model.Sections, section => Assert.Equal(DriverCurrentWorkLoadState.Empty, section.State));
    }

    [Fact]
    public async Task 페이지이탈뒤늦은응답은_표시나추가조회로이어지지않는다()
    {
        var late = new TaskCompletionSource<FoodDeliveryDriverCurrentWorkDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new ScriptedClient { Food = _ => late.Task };
        var model = new DriverCurrentWorkPageViewModel(client); var loading = model.LoadAsync(); model.Dispose();
        late.SetResult(new() { DriverId = "owner", ActiveDeliveries = [new() { OfferId = "old" }] }); await loading;
        var count = client.Reads.Count; await model.LoadAsync(); Assert.Equal(count, client.Reads.Count);
        Assert.All(model.Sections, section => Assert.Empty(section.Items));
    }

    [Fact]
    public async Task 제안만있는순회는비어있고_수락미확정과반납책임은따로안내한다()
    {
        var client = new ScriptedClient { Picking = _ => Task.FromResult(new DistributedWorkspaceDto
        {
            CurrentUserId = "owner", Waves = [new() { WaveId = "offered", DriverId = "owner", StatusCode = "Offered" },
                new() { WaveId = "pending", DriverId = "owner", StatusCode = "AcceptancePending" },
                new() { WaveId = "return", DriverId = "owner", StatusCode = "Cancelled", Orders = [new("o", "ref", "r", "ReturnRequired", 1, 1, 0, 0, false, false, null, null, [])],
                    Reservations = [new("r", "o", "l", "s", "loc", "owner", "sku", 1, 1, 0, 0, 0, 1, [])] }]
        }) };
        using var model = new DriverCurrentWorkPageViewModel(client); await model.LoadAsync();
        Assert.Equal(2, model.Sections[2].Items.Count);
        Assert.Equal("수락 결과 확인 필요", model.Sections[2].Items[0].Status);
        Assert.Equal("물품 반납 확인 필요", model.Sections[2].Items[1].Status);
    }

    [Theory]
    [InlineData("Cancelled", "ReturnRequired", "물품 반납 확인 필요")]
    [InlineData("NeedsReplan", "Picked", "보유 물품과 진행 조건 확인 필요")]
    public async Task 만료배정해제로예약행이숨겨져도_서버주문별보유수량으로반납책임을유지한다(string waveState, string orderState, string expected)
    {
        // 실제 MapWave의 validGrant=false 형태: 주문별 수량은 남고 Reservations와 현재 방문 권한은 숨겨집니다.
        var client = new ScriptedClient { Picking = _ => Task.FromResult(new DistributedWorkspaceDto
        {
            CurrentUserId = "owner", Waves = [new() { WaveId = "redacted", DriverId = "owner", StatusCode = waveState,
                CurrentStopId = null, Reservations = [], AllowedActions = [],
                Orders = [new("order", "ref", "cargo", orderState, 0, 3, 1, 1, false, false, null, null, [])] }]
        }) };
        using var model = new DriverCurrentWorkPageViewModel(client); await model.LoadAsync();
        var item = Assert.Single(model.Sections[2].Items);
        Assert.Equal(DriverCurrentWorkLoadState.Ready, model.Sections[2].State);
        Assert.Equal(expected, item.Status);
        Assert.Equal("/workspace/cargo-driver/distributed/round?selected=redacted", item.Href);
        Assert.DoesNotContain("직접 피킹", item.Status); Assert.DoesNotContain("고객에게 전달", item.Status);
    }

    [Theory]
    [InlineData("Pickup", "pick", "현재 거점에서 직접 피킹")]
    [InlineData("Delivery", "deliver", "현재 고객에게 전달")]
    public async Task 현재방문종류와서버허용행동이함께맞을때만_수행안내를표시한다(string kind, string action, string expected)
    {
        DistributedWaveDto Wave(string id, IReadOnlyList<string> actions, string stopState = "Pending") => new()
        {
            WaveId = id, DriverId = "owner", StatusCode = "Accepted", CurrentStopId = "stop", AllowedActions = actions,
            Stops = [new("stop", 1, kind, "site", [], default, default, default, 0, stopState, null, null, null)]
        };
        var client = new ScriptedClient { Picking = _ => Task.FromResult(new DistributedWorkspaceDto
        {
            CurrentUserId = "owner", Waves = [Wave("valid", [action]), Wave("expired", []),
                Wave("wrong-action", [action == "pick" ? "deliver" : "pick"]), Wave("old-stop", [action], "Completed")]
        }) };
        using var model = new DriverCurrentWorkPageViewModel(client); await model.LoadAsync();
        Assert.Equal(expected, model.Sections[2].Items[0].Status);
        Assert.All(model.Sections[2].Items.Skip(1), item => Assert.Equal("순회 진행 조건 확인 필요", item.Status));
    }

    [Fact]
    public async Task 주문별반납이모두정리된취소순회를_현재보유책임으로되살리지않는다()
    {
        var client = new ScriptedClient { Picking = _ => Task.FromResult(new DistributedWorkspaceDto
        {
            CurrentUserId = "owner", Waves = [new() { WaveId = "returned", DriverId = "owner", StatusCode = "Cancelled", Reservations = [],
                Orders = [new("order", "ref", "cargo", "ReturnRequired", 0, 3, 1, 2, false, false, null, null, [])] }]
        }) };
        using var model = new DriverCurrentWorkPageViewModel(client); await model.LoadAsync();
        Assert.Equal(DriverCurrentWorkLoadState.Empty, model.Sections[2].State); Assert.Empty(model.Sections[2].Items);
    }

    [Fact]
    public async Task 반납책임은다른주문의보유수량과혼동하지않는다()
    {
        var client = new ScriptedClient { Picking = _ => Task.FromResult(new DistributedWorkspaceDto
        {
            CurrentUserId = "owner", Waves = [new() { WaveId = "mixed-orders", DriverId = "owner", StatusCode = "NeedsReplan", Reservations = [],
                Orders = [new("returned", "ref1", "cargo1", "ReturnRequired", 0, 1, 0, 1, false, false, null, null, []),
                    new("active", "ref2", "cargo2", "Picked", 0, 2, 0, 0, false, false, null, null, [])] }]
        }) };
        using var model = new DriverCurrentWorkPageViewModel(client); await model.LoadAsync();
        Assert.Equal("보유 물품과 진행 조건 확인 필요", Assert.Single(model.Sections[2].Items).Status);
    }

    [Theory]
    [InlineData("../foreign")]
    [InlineData("https://foreign.example")]
    [InlineData("..")]
    public async Task 잘못된전문대상은다른화면이나외부주소로대체하지않는다(string id)
    {
        var client = new ScriptedClient { Food = _ => Task.FromResult(new FoodDeliveryDriverCurrentWorkDto
        { DriverId = "owner", ActiveDeliveries = [new() { OfferId = id }] }) };
        using var model = new DriverCurrentWorkPageViewModel(client); await model.LoadAsync();
        Assert.Equal(DriverCurrentWorkLoadState.Error, model.Sections[0].State); Assert.Empty(model.Sections[0].Items);
    }

    [Fact]
    public async Task 응답없는영역은시간초과로끝내고_다른영역은유지하며_명시재조회할수있다()
    {
        var never = new TaskCompletionSource<FoodDeliveryDriverCurrentWorkDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new ScriptedClient { Food = _ => never.Task };
        using var model = new DriverCurrentWorkPageViewModel(client, TimeSpan.FromMilliseconds(20));
        await model.LoadAsync();
        Assert.False(model.IsBusy); Assert.Equal(DriverCurrentWorkLoadState.Error, model.Sections[0].State);
        Assert.Equal(DriverCurrentWorkLoadState.Empty, model.Sections[1].State);
        client.Food = _ => Task.FromResult(new FoodDeliveryDriverCurrentWorkDto { DriverId = "owner" });
        await model.LoadAsync();
        Assert.All(model.Sections, section => Assert.Equal(DriverCurrentWorkLoadState.Empty, section.State));
        never.SetResult(new() { DriverId = "owner", ActiveDeliveries = [new() { OfferId = "expired-request" }] });
        Assert.All(model.Sections, section => Assert.Empty(section.Items));
    }

    [Fact]
    public async Task 조회완료뒤로그아웃은즉시업무를지우며_다음조회는최신판본을사용한다()
    {
        var client = new ScriptedClient { Food = _ => Task.FromResult(new FoodDeliveryDriverCurrentWorkDto
        { DriverId = "owner", ActiveDeliveries = [new() { OfferId = "first" }] }) };
        using var model = new DriverCurrentWorkPageViewModel(client); await model.LoadAsync();
        Assert.Single(model.Sections[0].Items);
        client.Change(RoleWorkspaceCatalog.FoodDriver, new(null, 2, false));
        Assert.All(model.Sections, section => Assert.Empty(section.Items));
        await model.LoadAsync(); Assert.Equal(DriverCurrentWorkLoadState.LoginRequired, model.Sections[0].State);
        client.Change(RoleWorkspaceCatalog.FoodDriver, new("owner", 3, true));
        client.Food = _ => Task.FromResult(new FoodDeliveryDriverCurrentWorkDto
        { DriverId = "owner", ActiveDeliveries = [new() { OfferId = "latest" }] });
        await model.LoadAsync(); Assert.Equal("latest", Assert.Single(model.Sections[0].Items).Id);
    }

    internal sealed class ScriptedClient : IDriverCurrentWorkClient
    {
        public event Action? Changed;
        public Dictionary<string, RoleWorkspaceIdentity> Identities { get; } = new()
        { [RoleWorkspaceCatalog.FoodDriver] = new("owner", 1, true), [RoleWorkspaceCatalog.CargoDriver] = new("owner", 1, true) };
        public List<string> Reads { get; } = [];
        public Action<string>? Initializing { get; set; }
        public Func<CancellationToken, Task<FoodDeliveryDriverCurrentWorkDto>> Food { get; set; } = _ => Task.FromResult(new FoodDeliveryDriverCurrentWorkDto { DriverId = "owner" });
        public Func<CancellationToken, Task<기사화물운송작업공간응답>> Cargo { get; set; } = _ => Task.FromResult(new 기사화물운송작업공간응답());
        public Func<CancellationToken, Task<DistributedWorkspaceDto>> Picking { get; set; } = _ => Task.FromResult(new DistributedWorkspaceDto { CurrentUserId = "owner" });
        public RoleWorkspaceIdentity Identity(string roleKey) => Identities[roleKey];
        public Task InitializeAsync(string roleKey, CancellationToken cancellationToken) { Initializing?.Invoke(roleKey); return Task.CompletedTask; }
        public Task<FoodDeliveryDriverCurrentWorkDto> FoodAsync(CancellationToken cancellationToken) { Reads.Add("food"); return Food(cancellationToken); }
        public Task<기사화물운송작업공간응답> CargoAsync(CancellationToken cancellationToken) { Reads.Add("cargo"); return Cargo(cancellationToken); }
        public Task<DistributedWorkspaceDto> PickingAsync(CancellationToken cancellationToken) { Reads.Add("picking"); return Picking(cancellationToken); }
        public void Change(string role, RoleWorkspaceIdentity identity) { Identities[role] = identity; Changed?.Invoke(); }
    }
}
