using System.Net;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MudBlazor.Services;
using Ssalddel.Contracts.Driver.Food;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.DriverCurrentWork;

namespace Ssalddel.Tests.Ui.Common;

public sealed class DriverCurrentWorkRenderTests
{
    [Fact]
    public async Task 종류그림과현재업무복귀만표시하고_고객상세나수행명령을복제하지않는다()
    {
        var client = new DriverCurrentWorkPageViewModelTests.ScriptedClient
        {
            Food = _ => Task.FromResult(new FoodDeliveryDriverCurrentWorkDto
            {
                DriverId = "owner", ActiveDeliveries = [new() { OfferId = "one", RestaurantName = "음식점", WorkStatus = "MovingToDropoff",
                    Dropoff = new() { Address = "비공개 고객주소" }, Recipient = new() { ContactPhone = "010-0000-0000" } }]
            }),
            Picking = _ => throw new RoleWorkspaceAccessException(404, "private raw failure")
        };
        using var model = new DriverCurrentWorkPageViewModel(client); await model.LoadAsync();
        var html = await RenderAsync(model, client);
        Assert.Contains("지금 할 일", html); Assert.Contains("다시 확인", html);
        Assert.Contains("음식 배달", html); Assert.Contains("퀵·화물 운송", html); Assert.Contains("직접 피킹·배송", html);
        Assert.Contains("고객에게 전달 중", html); Assert.Contains("이어서 하기", html);
        Assert.Contains("href=\"/workspace/food-driver?selected=one\"", html);
        Assert.Contains("현재 확인된 업무가 없습니다.", html);
        Assert.Contains("이 업무의 조회가 아직 제공되지 않습니다.", html);
        Assert.Equal(3, html.Split("driver-offer-badge__icon", StringSplitOptions.None).Length - 1);
        Assert.DoesNotContain("비공개 고객주소", html); Assert.DoesNotContain("010-0000-0000", html);
        Assert.DoesNotContain("private raw", html); Assert.DoesNotContain("총 운임", html);
        Assert.DoesNotContain(">픽업 완료<", html); Assert.DoesNotContain(">수락<", html);
    }

    [Fact]
    public async Task 로그인필요는_0건으로꾸미지않고_기존전문로그인복귀를제공한다()
    {
        var client = new DriverCurrentWorkPageViewModelTests.ScriptedClient();
        client.Identities[RoleWorkspaceCatalog.FoodDriver] = new(null, 2, false);
        client.Identities[RoleWorkspaceCatalog.CargoDriver] = new(null, 2, false);
        using var model = new DriverCurrentWorkPageViewModel(client); await model.LoadAsync();
        var html = await RenderAsync(model, client);
        Assert.Contains("기사 로그인", html); Assert.Contains("returnUrl=%2Fdriver%2Fcurrent-work", html);
        Assert.DoesNotContain("driver-current-work__count", html);
        Assert.DoesNotContain("현재 확인된 업무가 없습니다", html);
    }

    private static async Task<string> RenderAsync(DriverCurrentWorkPageViewModel model, IDriverCurrentWorkClient client)
    {
        var services = new ServiceCollection(); services.AddLogging(); services.AddMudServices();
        services.AddSingleton(client);
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var rendered = await renderer.RenderComponentAsync<DriverCurrentWorkScreen>(ParameterView.FromDictionary(
                new Dictionary<string, object?> { [nameof(DriverCurrentWorkScreen.Model)] = model }));
            return WebUtility.HtmlDecode(rendered.ToHtmlString());
        });
    }
}
