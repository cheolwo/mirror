using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Ssalddel.Contracts.Admin.Food;
using Ssalddel.Contracts.Common.Workflow;
using Ssalddel.Contracts.Driver.Food;
using Ssalddel.Contracts.Food;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Food;

namespace Ssalddel.Tests.Ui.Common;

public sealed class FoodRoleActionInputsRenderTests
{
    [Theory]
    [InlineData("orderer", 음식배달가능행동Ids.주문취소, "취소 사유", 2)]
    [InlineData("restaurant", 음식배달가능행동Ids.음식점주문수락, "음식점 이름", 6)]
    [InlineData("restaurant", 음식배달가능행동Ids.음식점조리시간변경, "이번 주문 조리 예상 시간(분)", 2)]
    [InlineData("restaurant", 음식배달가능행동Ids.음식점주문거절, "사유·메모", 1)]
    [InlineData("food-driver", 음식배달가능행동Ids.기사배달중단, "이번 배달 중단 사유", 2)]
    [InlineData("operator", OperatorRoleWorkspaceAdapter.ReviewInterruption, "검토할 중단 배달", 4)]
    public async Task ExtractedInputsKeepOneParentFieldsetAndRoleSpecificLabelOrder(string role, string action, string firstLabel, int labelCount)
    {
        var api = CreateApi(role, action);
        var html = await RenderAsync<FoodRoleActionForm>(new()
        {
            [nameof(FoodRoleActionForm.RoleKey)] = role, [nameof(FoodRoleActionForm.ItemId)] = "work-a",
            [nameof(FoodRoleActionForm.ActionKey)] = action
        }, api);
        Assert.Equal(1, Regex.Matches(html, "<fieldset\\b").Count);
        Assert.Matches("<fieldset\\b[^>]*>\\s*(?:<!--.*?-->\\s*)*<label\\b", html);
        Assert.Equal(labelCount, Regex.Matches(html, "<label\\b").Count);
        var first = html.IndexOf(firstLabel, StringComparison.Ordinal);
        var memo = html.IndexOf(role == "operator" ? "판정 근거" : "사유·메모", StringComparison.Ordinal);
        Assert.True(first >= 0 && first <= memo);
        Assert.True(memo < html.IndexOf("요청 내용 확인", StringComparison.Ordinal));
        Assert.Contains("요청 내용 확인", html); Assert.Empty(api.Writes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ImmediatePickupKeepsExistingInputVisibilityAndBoundValues(bool immediate)
    {
        var api = CreateApi("restaurant", 음식배달가능행동Ids.음식점주문수락);
        using var model = new FoodRoleActionViewModel(api, new Access());
        await model.InitializeAsync("restaurant", "work-a", 음식배달가능행동Ids.음식점주문수락);
        model.ImmediatePickup = immediate; model.PreparationMinutes = 30; model.RestaurantDetailAddress = "1층"; model.Reason = "준비 메모";
        var html = await RenderAsync<FoodRoleActionInputs>(new()
        {
            [nameof(FoodRoleActionInputs.Model)] = model, [nameof(FoodRoleActionInputs.RoleKey)] = "restaurant",
            [nameof(FoodRoleActionInputs.ActionKey)] = 음식배달가능행동Ids.음식점주문수락
        }, api);
        Assert.StartsWith("<label", html.TrimStart()); Assert.DoesNotContain("<fieldset", html);
        Assert.Contains("value=\"1층\"", html); Assert.Contains("준비 메모", html);
        Assert.Equal(!immediate, html.Contains("이번 주문 조리 예상 시간(분)", StringComparison.Ordinal));
        if (immediate) Assert.Matches("type=\"checkbox\"[^>]*checked", html);
        else Assert.Contains("value=\"30\"", html);
        Assert.Empty(api.Writes);
    }

    private static FoodRoleWorkspaceAdapterTests.Api CreateApi(string role, string action)
    {
        var api = new FoodRoleWorkspaceAdapterTests.Api();
        var allowed = new 업무가능행동Dto { ActionId = action, ExpectedRevision = 2 };
        if (role == "restaurant") api.Reads["api/v1/food-orders/restaurant/inbox/work-a"] = new 음식주문응답
            { 주문번호 = "work-a", 음식점명 = "음식점", 음식점주소 = "공개 픽업 주소", 조리예상분 = 15, AvailableActions = [allowed] };
        else if (role == "orderer") api.Reads["api/v1/food-orders/work-a"] = new 주문자음식주문상세응답
            { 주문 = new() { 주문번호 = "work-a" }, AvailableActions = [allowed] };
        else if (role == "food-driver") api.Reads["api/v1/driver/food-deliveries/workspace/current"] = new FoodDeliveryDriverCurrentWorkDto
            { DriverId = "user-a", ActiveDeliveries = [new() { OfferId = "work-a", DeliveryAttemptId = "attempt-a", AvailableActions = [allowed] }] };
        else api.Reads["api/v1/admin/food-orders/work-a/operations-trace"] = new 음식주문운영추적응답
            { 주문번호 = "work-a", 배달시도목록 = [new() { 시도StableId = "attempt-a", 시도순번 = 1, 상태Code = 음식배달시도상태Code.중단 }] };
        return api;
    }

    private static async Task<string> RenderAsync<T>(Dictionary<string, object?> parameters, IRoleWorkspaceApi api) where T : IComponent
    {
        var services = new ServiceCollection(); services.AddLogging();
        services.AddSingleton(api); services.AddSingleton<IRoleWorkspaceAccess>(new Access());
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () => WebUtility.HtmlDecode(
            (await renderer.RenderComponentAsync<T>(ParameterView.FromDictionary(parameters))).ToHtmlString()));
    }

    private sealed class Access : IRoleWorkspaceAccess
    {
        public event Action? Changed { add { } remove { } }
        public RoleWorkspaceIdentity GetIdentity(string roleKey) => new("user-a", 1, true);
        public Task EnsureInitializedAsync(string roleKey, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SignInAsync(string roleKey, string name, string password, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SignOutAsync(string roleKey, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
