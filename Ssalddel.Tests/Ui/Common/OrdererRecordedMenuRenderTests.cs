using System.Net;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MudBlazor.Services;
using Ssalddel.Ui.Common.Areas.App.Components.Food;

namespace Ssalddel.Tests.Ui.Common;

public sealed class OrdererRecordedMenuRenderTests
{
    [Fact]
    public async Task 과거가격종류를_분리하고_실제주문행동을_만들지않는다()
    {
        var item = new OrdererRecordedMenuObservation
        {
            Id = "private-replay-menu-a", BranchId = "branch-a", BranchName = "커피 사가정점", MenuName = "라떼",
            ServiceDate = new(2026, 9, 30), Quantity = 2, DisplayedItemAmountKrw = 16000, OrderAmountKrw = 23000,
            ImageUrl = "/_local/orderer-menu-images/image-a", ImageMenuName = "라떼", Options = ["아이스"]
        };
        var html = await Render<OrdererRecordedMenuCatalog>(new()
        {
            [nameof(OrdererRecordedMenuCatalog.Items)] = new[] { item }
        });
        Assert.Contains("현재 판매가·판매 여부를 뜻하지 않습니다", html);
        Assert.Contains("공식 메뉴 참고 이미지", html);
        Assert.Contains("당시 상품·지점·옵션이 모두 같은 사진으로 확인된 것은 아닙니다", html);
        Assert.Matches(@"기록된 단가</dt>\s*<dd\b[^>]*>미확인", html);
        Assert.Matches(@"당시 메뉴 금액</dt>\s*<dd\b[^>]*>16,000원", html);
        Assert.Matches(@"당시 주문 합계</dt>\s*<dd\b[^>]*>23,000원", html);
        Assert.Contains("2026.09.30", html);
        Assert.Contains("2개", html);
        Assert.Contains("아이스", html);
        Assert.Contains("data-public-use-approved=\"false\"", html);
        Assert.Contains("data-image-options-match-confirmed=\"false\"", html);
        Assert.Contains("loading=\"lazy\"", html);
        Assert.Contains("src=\"/_local/orderer-menu-images/image-a\"", html);
        Assert.Contains("alt=\"라떼 메뉴 사진\"", html);
        Assert.DoesNotContain("주문 제출", html);
        Assert.DoesNotContain("수량 늘리기", html);
        Assert.DoesNotContain("옵션 일치 미확인 · 공개 사용", html);
        Assert.DoesNotContain("8,000원", html);
        Assert.DoesNotContain(">0원<", html);
    }

    [Fact]
    public async Task 선택한지점과검색어에맞는_관측만보여준다()
    {
        var html = await Render<OrdererRecordedMenuCatalog>(new()
        {
            [nameof(OrdererRecordedMenuCatalog.Items)] = new OrdererRecordedMenuObservation[]
            {
                new() { Id = "a", BranchId = "one", BranchName = "첫 지점", MenuName = "라떼" },
                new() { Id = "b", BranchId = "two", BranchName = "다른 지점", MenuName = "라떼" },
                new() { Id = "c", BranchId = "one", BranchName = "첫 지점", MenuName = "다른 메뉴" }
            },
            [nameof(OrdererRecordedMenuCatalog.SelectedBranchId)] = "one",
            [nameof(OrdererRecordedMenuCatalog.SearchText)] = "라떼"
        });
        Assert.Contains("data-recorded-menu-id=\"a\"", html);
        Assert.DoesNotContain("data-recorded-menu-id=\"b\"", html);
        Assert.DoesNotContain("data-recorded-menu-id=\"c\"", html);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("data:image/png;base64,abc")]
    [InlineData("file:///C:/private.jpg")]
    public async Task 사진누락이나_위험한주소에는_대체표시를사용한다(string? url)
    {
        var html = await Render<OrdererMenuImage>(new()
        {
            [nameof(OrdererMenuImage.ImageUrl)] = url,
            [nameof(OrdererMenuImage.MenuName)] = "확인 메뉴"
        });
        Assert.Contains("data-menu-image-state=\"missing\"", html);
        Assert.Contains("사진 없음", html);
        Assert.Contains("aria-label=\"확인 메뉴 메뉴 사진\"", html);
        Assert.DoesNotContain("<img", html);
        Assert.DoesNotContain("base64", html);
        Assert.DoesNotContain("file:///", html);
    }

    [Fact]
    public async Task 없는메뉴를_샘플이나_무료메뉴로_채우지않는다()
    {
        var html = await Render<OrdererRecordedMenuCatalog>([]);
        Assert.Contains("확인된 메뉴 기록이 없습니다", html);
        Assert.DoesNotContain("data-recorded-menu-id", html);
        Assert.DoesNotContain("0원", html);
    }

    private static async Task<string> Render<T>(Dictionary<string, object?> parameters) where T : IComponent
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMudServices();
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () => WebUtility.HtmlDecode(
            (await renderer.RenderComponentAsync<T>(ParameterView.FromDictionary(parameters))).ToHtmlString()));
    }
}
