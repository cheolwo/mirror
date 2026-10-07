using System.Net;
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
        var html = await RenderAsync(new FoodDeliveryDriverWorkspaceDto
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
        var html = await RenderAsync(new FoodDeliveryDriverWorkspaceDto { ActiveDeliveries = [active] });
        Assert.Contains("픽업지 기본 주소", html);
        Assert.Contains("4,180원", html);
        Assert.Contains("10/5 12:10 (한국)", html);
        Assert.Contains(arrived ? "음식 픽업 확인" : "음식점 도착", html);
        if (arrived) Assert.Contains("재조리 음식을 픽업해 주세요.", html);
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
        var html = await RenderAsync(new FoodDeliveryDriverWorkspaceDto { ActiveDeliveries = [ActiveDelivery()] }, expanded);
        Assert.Contains("고객 전달 주소", html);
        Assert.Contains("초인종 없이 문 앞에 놓아 주세요.", html);
        Assert.Contains("4,180원", html);
        Assert.Contains("고객 전달 완료", html);
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

    private static FoodDeliveryDriverActiveDeliveryDto ActiveDelivery() => new()
    {
        OfferId = "offer-a", RestaurantName = "음식점", DriverPayout = 4180, DeliveryAttemptId = "attempt-a",
        WorkStatus = DriverWorkOfferStatus.MovingToDropoff, OrderSummary = "길게 나열된 메뉴 정보",
        Pickup = new() { Address = "픽업지 기본 주소" }, Dropoff = new() { Address = "고객 전달 주소" },
        DisplayedPreparationReadyAtUtc = new(2026, 10, 5, 3, 10, 0, DateTimeKind.Utc),
        Recipient = new() { DisplayName = "수령인 이름", ContactPhone = "010-1234-5678", DeliveryInstructions = "초인종 없이 문 앞에 놓아 주세요." },
        AvailableActions = [new() { ActionId = 음식배달가능행동Ids.기사전달완료 }]
    };

    private static async Task<string> RenderAsync(FoodDeliveryDriverWorkspaceDto workspace, bool expanded = false)
    {
        var api = new FoodRoleWorkspaceAdapterTests.Api();
        api.Reads["api/v1/driver/food-deliveries/workspace"] = workspace;
        api.Reads["api/v1/driver/food-deliveries/work/status"] = new 기사운행상태응답 { Status = "운행중" };
        api.Reads["api/v1/driver/operational-dispatch/availability"] = new 운영배차수신상태Dto
        { 수신의사Code = 운영배차수신의사Code.On, 실효상태Code = 운영배차실효상태Code.배차가능 };
        var state = new RoleWorkspaceState();
        state.Save("food-driver", new(null, expanded, null));
        using var model = new RoleWorkspaceViewModel([new FoodDriverRoleWorkspaceAdapter(api, new NoLocation())], new Access(), state);
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
                new Dictionary<string, object?> { [nameof(RoleMapWorkspace.RoleKey)] = "food-driver" }));
            return WebUtility.HtmlDecode(output.ToHtmlString());
        });
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
