using System.Text.RegularExpressions;
using Ssalddel.Contracts.Common.Commerce;
using Ssalddel.Ui.Common.Areas.App.Components.Food;

namespace Ssalddel.Tests.Ui.Common;

public sealed partial class OrdererFoodOrderComposerRecoveryTests
{
    [Fact]
    public async Task 필수수령정보가없으면_버튼가까이에입력할항목을안내하고_기존제출잠금을유지한다()
    {
        var services = new FoodServices();
        using var page = await CreatePageAsync(services, new AuthenticationService { SignedIn = true });
        page.작성.수령인명 = "";
        page.작성.연락처 = "";
        page.작성.주소 = "";

        var html = await RenderComposerAsync(page.인증, page.작성);

        Assert.Contains("수령인 이름 (필수)", html);
        Assert.Contains("수령인 연락처 (필수)", html);
        Assert.Contains("배달 주소 (필수)", html);
        Assert.Contains("상세 주소 (선택)", html);
        Assert.Contains("전달 요청사항 (선택)", html);
        Assert.Contains("수령인 이름·연락처·배달 주소 입력을 완료해 주세요.", html);
        var basicHtml = Regex.Replace(html, "<details\\b[^>]*>.*?</details>", "", RegexOptions.Singleline);
        Assert.Contains("수령인 이름 (필수)", basicHtml);
        Assert.Contains("수령인 연락처 (필수)", basicHtml);
        Assert.Contains("배달 주소 (필수)", basicHtml);
        Assert.Contains("판매자 정보", basicHtml);
        Assert.Contains("거래 당사자와 거래·개인정보 안내를 확인했습니다.", basicHtml);
        Assert.Contains("현장결제 메모만 저장됩니다.", basicHtml);
        Assert.Contains("수령인 이름·연락처·배달 주소 입력을 완료해 주세요.", basicHtml);
        Assert.DoesNotContain("상세 주소 (선택)", basicHtml);
        Assert.DoesNotContain("전달 요청사항 (선택)", basicHtml);
        var button = Regex.Match(html, "<button\\b[^>]*aria-describedby=\"food-order-readiness-[^\"]+\"[^>]*>");
        Assert.True(button.Success);
        Assert.Contains("disabled", button.Value);
        Assert.False(page.작성.제출가능);
        Assert.Empty(services.Requests);
    }

    [Fact]
    public async Task 보조입력의접힌표시는_입력됨을알리고_재렌더가초안과요청Id를지우거나동의하지않는다()
    {
        var services = new FoodServices();
        using var page = await CreatePageAsync(services, new AuthenticationService { SignedIn = true });
        var requestId = page.작성.클라이언트요청Id;

        var html = await RenderComposerAsync(page.인증, page.작성);
        var optional = Regex.Match(html, "<details\\b[^>]*class=\"food-order-composer__optional[^\"]*\"[^>]*>.*?</details>", RegexOptions.Singleline);
        Assert.True(optional.Success);
        Assert.DoesNotContain(" open", optional.Value.Split('>')[0]);
        Assert.Contains("입력됨", optional.Value);
        Assert.Contains("상세 주소", optional.Value);
        Assert.Contains("문 앞", optional.Value);
        AssertDraft(page.작성, requestId);

        await RenderComposerAsync(page.인증, page.작성);
        AssertDraft(page.작성, requestId);
        Assert.Null(page.작성.거래보호확인);
        Assert.Empty(services.Requests);
    }

    [Fact]
    public async Task 최소주문금액부족은_기존메뉴가격으로계산한차액을안내하고_주문을자동제출하지않는다()
    {
        var services = new FoodServices();
        using var page = await CreatePageAsync(services, new AuthenticationService { SignedIn = true });
        page.작성.메뉴수량변경(1001, -1);

        var html = await RenderComposerAsync(page.인증, page.작성);

        Assert.Contains("최소 주문금액까지 3,500원 더 선택해 주세요.", html);
        Assert.False(page.작성.제출가능);
        Assert.Equal(4_500m, page.작성.주문금액);
        Assert.Empty(services.Requests);
    }

    [Theory]
    [InlineData(false, "현재 주문을 받지 않는 음식점입니다")]
    [InlineData(null, "음식점과 메뉴를 먼저 선택해 주세요")]
    public async Task 음식점의주문불가와미확인을_같은빈메뉴상태로오인시키지않는다(bool? available, string expected)
    {
        var services = new FoodServices();
        using var page = await CreatePageAsync(services, new AuthenticationService { SignedIn = true });
        var restaurant = available.HasValue ? CreateRestaurant() : null;
        if (restaurant is not null) restaurant.음식점.주문가능여부 = available!.Value;
        page.작성.음식점설정(restaurant);

        Assert.Contains(expected, OrdererRestaurantPresentation.SubmissionReadinessMessage(page.작성));
        Assert.False(page.작성.제출가능);
        Assert.Empty(services.Requests);
    }

    [Fact]
    public async Task 준비안내는_동의나메뉴재확인을대신하지않고_기존요청Id도변경하지않는다()
    {
        var services = new FoodServices();
        using var page = await CreatePageAsync(services, new AuthenticationService { SignedIn = true });
        Assert.Equal("거래 안내를 확인하고 동의해 주세요.", OrdererRestaurantPresentation.SubmissionReadinessMessage(page.작성));
        Assert.Null(page.작성.거래보호확인);
        page.작성.거래보호확인 = new 거래보호확인Request { NoticeAccepted = true };
        var requestId = page.작성.클라이언트요청Id;
        Assert.Null(OrdererRestaurantPresentation.SubmissionReadinessMessage(page.작성));

        Assert.True(page.작성.메뉴확인시작());
        Assert.Equal("최신 메뉴와 가격을 확인하고 있습니다.", OrdererRestaurantPresentation.SubmissionReadinessMessage(page.작성));
        Assert.False(page.작성.제출가능);
        page.작성.메뉴확인실패(null);
        Assert.Equal("최신 메뉴를 다시 확인해 주세요.", OrdererRestaurantPresentation.SubmissionReadinessMessage(page.작성));
        Assert.False(page.작성.제출가능);
        Assert.Equal(requestId, page.작성.클라이언트요청Id);
        Assert.Empty(services.Requests);
    }
}
