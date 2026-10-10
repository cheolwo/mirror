using Ssalddel.Ui.Common.Areas.App.Components.Food;

namespace Ssalddel.Tests.Ui.Common;

public sealed class OrdererRecordedMenuPresentationTests
{
    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-1)]
    public void 미확인이나유효하지않은가격을_무료판매가로_표시하지않는다(int? amount)
        => Assert.Equal("미확인", OrdererRecordedMenuPresentation.Amount(amount));

    [Fact]
    public void 항목표시금액이나주문총액에서_단가를_역산하지않는다()
    {
        var item = new OrdererRecordedMenuObservation
        {
            Quantity = 2, UnitPriceKrw = null, DisplayedItemAmountKrw = 16000, OrderAmountKrw = 23000
        };
        Assert.Equal("미확인", OrdererRecordedMenuPresentation.Amount(item.UnitPriceKrw));
        Assert.Equal("16,000원", OrdererRecordedMenuPresentation.Amount(item.DisplayedItemAmountKrw));
        Assert.Equal("23,000원", OrdererRecordedMenuPresentation.Amount(item.OrderAmountKrw));
        Assert.False(item.PublicUseApproved);
        Assert.False(item.ImageOptionsMatchConfirmed);
    }

    [Fact]
    public void 매장식별자로_지점을나누고_메뉴와옵션을_검색한다()
    {
        OrdererRecordedMenuObservation[] items =
        [
            new() { Id = "a", BranchId = "one", BranchName = "커피 사가정점", MenuName = "라떼", Options = ["아이스"] },
            new() { Id = "b", BranchId = "two", BranchName = "커피 면목점", MenuName = "라떼", Options = ["따뜻하게"] },
            new() { Id = "c", BranchId = "one", BranchName = "커피 사가정점", MenuName = "빵" }
        ];
        Assert.Equal("a", Assert.Single(OrdererRecordedMenuPresentation.Filter(items, "one", "라떼")).Id);
        Assert.Equal("b", Assert.Single(OrdererRecordedMenuPresentation.Filter(items, null, "따뜻")).Id);
        Assert.Empty(OrdererRecordedMenuPresentation.Filter(items, "missing", null));
        Assert.Equal(3, OrdererRecordedMenuPresentation.Filter(items, null, " ").Count);
    }

    [Theory]
    [InlineData("https://official.example/menu.jpg")]
    [InlineData("/_local/orderer-menu-images/asset-1")]
    [InlineData("images/menu.jpg")]
    [InlineData("../images/menu.jpg")]
    public void 메뉴사진은_https나_내부상대주소를_표시할수있다(string url)
        => Assert.Equal(url, OrdererMenuImage.SafeImageUrl(url));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("file:///C:/private/menu.jpg")]
    [InlineData("data:image/png;base64,abc")]
    [InlineData("javascript:alert(1)")]
    [InlineData("http://official.example/menu.jpg")]
    [InlineData("https:official.example/menu.jpg")]
    [InlineData("http:/official.example/menu.jpg")]
    [InlineData("//official.example/menu.jpg")]
    [InlineData("/\\official.example/menu.jpg")]
    [InlineData("https://user:secret@official.example/menu.jpg")]
    [InlineData("https://official.example/\nmenu.jpg")]
    [InlineData("#image")]
    [InlineData("?image=1")]
    public void 허용하지않는사진주소를_브라우저에_전달하지않는다(string? url)
        => Assert.Null(OrdererMenuImage.SafeImageUrl(url));
}
