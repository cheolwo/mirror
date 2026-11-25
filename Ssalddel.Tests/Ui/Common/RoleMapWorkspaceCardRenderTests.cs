using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using MudBlazor.Services;
using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Contracts.Common.Drivers;
using Ssalddel.Contracts.Common.Workflow;
using Ssalddel.Contracts.Driver.Food;
using Ssalddel.Contracts.Driver.Work;
using Ssalddel.Contracts.Food;
using Ssalddel.Ui.Common.Areas.App.Models;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Food;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.Tests.Ui.Common;

public sealed class RoleMapWorkspaceCardRenderTests
{
    [Fact]
    public async Task 수락전_접힌카드에_배달료_거리_픽업지가_보이고_긴메뉴는_상세로_남는다()
    {
        var html = await RenderAsync(new FoodDeliveryDriverCurrentWorkDto
        {
            Recommendations = [new() { OfferId = "offer-a", RestaurantName = "음식점", DriverPayout = 4720, DistanceKm = 3.414m,
                Pickup = new() { Address = "픽업지 기본 주소" }, Dropoff = new() { Address = "수락 전 전달 주소" },
                OrderSummary = "길게 나열된 메뉴 정보", AvailableActions = [new() { ActionId = 음식배달가능행동Ids.기사제안수락 }] }]
        });
        Assert.Contains("현재 업무 핵심 정보", html);
        Assert.Contains("픽업지 기본 주소", html);
        Assert.Contains("4,720원", html);
        Assert.Contains("3.414 km", html);
        Assert.Contains("배달 수락", html);
        Assert.Contains("aria-expanded=\"false\"", html);
        Assert.DoesNotContain("길게 나열된 메뉴 정보", html);
        Assert.DoesNotContain("수락 전 전달 주소", html);
        Assert.DoesNotContain("id=\"role-workspace-details\"", html);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 픽업이동과대기_기본카드는_픽업지와준비시간을_보이고_고객정보를_꺼내지않는다(bool arrived)
    {
        var active = ActiveDelivery();
        active.WorkStatus = DriverWorkOfferStatus.Accepted;
        active.RestaurantArrivedAtUtc = arrived ? new DateTime(2026, 10, 5, 3, 0, 0, DateTimeKind.Utc) : null;
        active.CurrentPreparationRound = arrived ? 2 : 1;
        active.AvailableActions = [new() { ActionId = arrived ? 음식배달가능행동Ids.기사픽업확인 : 음식배달가능행동Ids.기사가게도착 }];
        var html = await RenderAsync(new FoodDeliveryDriverCurrentWorkDto { ActiveDeliveries = [active] });
        Assert.Contains("픽업지 기본 주소", html);
        Assert.Contains("4,180원", html);
        Assert.Contains("10/5 12:10 (한국)", html);
        Assert.Contains(arrived ? "픽업 완료" : "가게 도착", html);
        if (arrived) Assert.Contains("재조리 음식을 받은 뒤 픽업 완료를 눌러 주세요.", html);
        Assert.DoesNotContain("고객 전달 주소", html);
        Assert.DoesNotContain("초인종 없이 문 앞에 놓아 주세요.", html);
        Assert.DoesNotContain("수령인 이름", html);
        Assert.DoesNotContain("010-1234-5678", html);
        Assert.DoesNotContain("길게 나열된 메뉴 정보", html);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 전달_기본카드는_주소와필수요청을_보이고_메뉴와가린연락처는_상세에_남는다(bool expanded)
    {
        var html = await RenderAsync(new FoodDeliveryDriverCurrentWorkDto { ActiveDeliveries = [ActiveDelivery()] }, expanded);
        Assert.Contains("고객 전달 주소", html);
        Assert.Contains("초인종 없이 문 앞에 놓아 주세요.", html);
        Assert.Contains("4,180원", html);
        Assert.Contains("전달 완료", html);
        Assert.DoesNotContain("010-1234-5678", html);
        Assert.DoesNotContain("픽업지 기본 주소", html);
        if (expanded)
        {
            Assert.Contains("길게 나열된 메뉴 정보", html);
            Assert.Contains("수령인 이름", html);
            Assert.Contains("•••• 5678", html);
            Assert.Contains("id=\"role-workspace-details\"", html);
        }
        else
        {
            Assert.DoesNotContain("길게 나열된 메뉴 정보", html);
            Assert.DoesNotContain("수령인 이름", html);
            Assert.DoesNotContain("•••• 5678", html);
            Assert.DoesNotContain("id=\"role-workspace-details\"", html);
        }
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    public async Task 픽업불허_대기안내는_요약에_한번만_보이고_다른_비활성사유와_버튼은_남는다(
        bool recooking, bool ready, bool expanded)
    {
        var active = PickupWaiting(recooking, ready);
        active.AvailableActions = [new()
        {
            ActionId = 음식배달가능행동Ids.기사배달중단,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(-1)
        }];
        var guide = FoodDriverStagePresentation.Guide(active, FoodDriverStage.PickupWaiting);
        const string otherReason = "가능 행동의 확인 시각이 지났습니다. 최신 상태를 자동으로 확인합니다.";

        var html = await RenderAsync(new FoodDeliveryDriverCurrentWorkDto { ActiveDeliveries = [active] }, expanded);

        Assert.Matches("<h3[^>]*>픽업 확인</h3>", html);
        Assert.Equal(1, Occurrences(html, guide));
        Assert.Equal(1, Occurrences(html, otherReason));
        Assert.Contains("픽업 완료", html);
        Assert.True(Regex.IsMatch(PrimaryButtonTag(html), @"\bdisabled(?:=|\s|>)"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 픽업허용_카드는_받은뒤_완료안내와_활성버튼을_보존한다(bool recooking)
    {
        var active = PickupWaiting(recooking, ready: true);
        active.AvailableActions = [new() { ActionId = 음식배달가능행동Ids.기사픽업확인 }];

        var html = await RenderAsync(new FoodDeliveryDriverCurrentWorkDto { ActiveDeliveries = [active] });

        Assert.Equal(1, Occurrences(html, recooking
            ? "재조리 음식을 받은 뒤 픽업 완료를 눌러 주세요."
            : "음식을 받은 뒤 픽업 완료를 눌러 주세요."));
        Assert.Contains("픽업 완료", html);
        Assert.False(Regex.IsMatch(PrimaryButtonTag(html), @"\bdisabled(?:=|\s|>)"));
    }

    [Theory]
    [InlineData("food-driver", false, false, 1)]
    [InlineData("food-driver", true, false, 1)]
    [InlineData("restaurant", true, true, 2)]
    [InlineData("restaurant", false, false, 1)]
    public async Task 요약요청이_없거나_다른역할이면_비활성안내를_보존한다(
        string roleKey, bool hasSummary, bool hasRequest, int expectedOccurrences)
    {
        const string reason = "현재 작업을 다시 확인하고 있습니다.";
        var item = new RoleWorkspaceItem("item-a", "가상 업무", "대기",
            Actions: [new(음식배달가능행동Ids.기사픽업확인, "업무 확인", IsPrimary: true, Enabled: false, DisabledReason: reason)],
            Summary: hasSummary ? new("작업 장소", "가상 작업 장소",
                Request: hasRequest ? new("확인 안내", reason) : null) : null);

        var html = await RenderAsync(new SnapshotAdapter(roleKey, item));

        Assert.Equal(expectedOccurrences, Occurrences(html, reason));
        Assert.True(Regex.IsMatch(PrimaryButtonTag(html), @"\bdisabled(?:=|\s|>)"));
    }

    [Fact]
    public async Task 요약요청에_일부만_겹치는_비활성사유는_별도로_보존한다()
    {
        const string reason = "현재 작업을 다시 확인하고 있습니다.";
        var item = new RoleWorkspaceItem("item-a", "가상 업무", "대기",
            Actions: [new(음식배달가능행동Ids.기사픽업확인, "업무 확인", IsPrimary: true, Enabled: false, DisabledReason: reason)],
            Summary: new("작업 장소", "가상 작업 장소", Request: new("확인 안내", reason + " 잠시 기다려 주세요.")));

        var html = await RenderAsync(new SnapshotAdapter("food-driver", item));

        Assert.Equal(2, Occurrences(html, reason));
    }

    private static FoodDeliveryDriverActiveDeliveryDto PickupWaiting(bool recooking, bool ready)
    {
        var active = ActiveDelivery();
        active.WorkStatus = DriverWorkOfferStatus.MovingToPickup;
        active.RestaurantArrivedAtUtc = new(2026, 10, 5, 3, 0, 0, DateTimeKind.Utc);
        active.CurrentPreparationRound = recooking ? 2 : 1;
        active.RecookingRequestedAtUtc = recooking ? new(2026, 10, 5, 2, 55, 0, DateTimeKind.Utc) : null;
        active.CurrentPickupReadyAtUtc = ready ? new(2026, 10, 5, 3, 5, 0, DateTimeKind.Utc) : null;
        active.AvailableActions = [];
        return active;
    }

    private static int Occurrences(string html, string text) => html.Split(text, StringSplitOptions.None).Length - 1;

    private static string PrimaryButtonTag(string html)
    {
        var button = Regex.Match(html, "<button\\b[^>]*class=\"is-primary\"[^>]*>");
        Assert.True(button.Success, "현재 업무의 주 행동 버튼이 보여야 합니다.");
        return button.Value;
    }

    private static FoodDeliveryDriverActiveDeliveryDto ActiveDelivery() => new()
    {
        OfferId = "offer-a", RestaurantName = "음식점", DriverPayout = 4180, DeliveryAttemptId = "attempt-a",
        WorkStatus = DriverWorkOfferStatus.MovingToDropoff, OrderSummary = "길게 나열된 메뉴 정보",
        Pickup = new() { Address = "픽업지 기본 주소" }, Dropoff = new() { Address = "고객 전달 주소" },
        DisplayedPreparationReadyAtUtc = new(2026, 10, 5, 3, 10, 0, DateTimeKind.Utc),
        Recipient = new() { DisplayName = "수령인 이름", ContactPhone = "010-1234-5678", DeliveryInstructions = "초인종 없이 문 앞에 놓아 주세요." },
        AvailableActions = [new() { ActionId = 음식배달가능행동Ids.기사전달완료 }]
    };

    private static async Task<string> RenderAsync(FoodDeliveryDriverCurrentWorkDto workspace, bool expanded = false)
    {
        var api = new FoodRoleWorkspaceAdapterTests.Api();
        api.Reads["api/v1/driver/food-deliveries/workspace/current"] = workspace;
        api.Reads["api/v1/driver/food-deliveries/work/status"] = new 기사운행상태응답 { Status = "운행중" };
        api.Reads["api/v1/driver/operational-dispatch/availability"] = new 운영배차수신상태Dto
        { 수신의사Code = 운영배차수신의사Code.On, 실효상태Code = 운영배차실효상태Code.배차가능 };
        var html = await RenderAsync(new FoodDriverRoleWorkspaceAdapter(api, new NoLocation()), expanded);
        Assert.Empty(api.Writes);
        return html;
    }

    private static async Task<string> RenderAsync(IRoleWorkspaceAdapter adapter, bool expanded = false)
    {
        var state = new RoleWorkspaceState();
        state.Save(adapter.RoleKey, new(null, expanded, null));
        using var model = new RoleWorkspaceViewModel([adapter], new Access(), state);
        var services = new ServiceCollection();
        services.AddLogging(); services.AddMudServices();
        services.AddSingleton(model);
        services.AddSingleton<NavigationManager, TestNavigation>();
        services.AddSingleton<INeighborhoodMapHost, MapHost>();
        services.AddSingleton<IJSRuntime, StaticRenderJs>();
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var output = await renderer.RenderComponentAsync<RoleMapWorkspace>(ParameterView.FromDictionary(
                new Dictionary<string, object?> { [nameof(RoleMapWorkspace.RoleKey)] = adapter.RoleKey }));
            return WebUtility.HtmlDecode(output.ToHtmlString());
        });
    }

    private sealed class SnapshotAdapter(string roleKey, RoleWorkspaceItem item) : IRoleWorkspaceAdapter
    {
        public string RoleKey => roleKey;
        public Task<RoleWorkspaceSnapshot> LoadAsync(string? selectedId, CancellationToken cancellationToken)
            => Task.FromResult(new RoleWorkspaceSnapshot(roleKey, [item], SelectedId: item.Id));
        public Task PerformAsync(string itemId, string actionKey, Guid requestId, CancellationToken cancellationToken)
            => throw new InvalidOperationException("정적 카드 렌더에서 업무 명령을 실행하면 안 됩니다.");
        public void Clear() { }
    }

    private sealed class NoLocation : IRoleWorkspaceLocationProvider
    { public Task<RoleWorkspaceLocation?> GetCurrentAsync(CancellationToken cancellationToken) => Task.FromResult<RoleWorkspaceLocation?>(null); }
    private sealed class StaticRenderJs : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            => throw new InvalidOperationException("정적 카드 렌더는 브라우저 스크립트를 실행하지 않습니다.");
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
            => InvokeAsync<TValue>(identifier, args);
    }
    private sealed class Access : IRoleWorkspaceAccess
    {
        public event Action? Changed { add { } remove { } }
        public RoleWorkspaceIdentity GetIdentity(string roleKey) => new("owner-a", 1, true);
        public Task EnsureInitializedAsync(string roleKey, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SignInAsync(string roleKey, string name, string password, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SignOutAsync(string roleKey, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
    private sealed class MapHost : INeighborhoodMapHost
    {
        public Task<NeighborhoodMapHostStatus> RenderAsync(string id, NeighborhoodMapRenderState state, Func<string, Task> selected, CancellationToken cancellationToken = default)
            => Task.FromResult(new NeighborhoodMapHostStatus("ready"));
        public Task HideAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
    private sealed class TestNavigation : NavigationManager
    {
        public TestNavigation() => Initialize("http://localhost/", "http://localhost/workspace/food-driver");
        protected override void NavigateToCore(string uri, bool forceLoad) { }
    }
}
