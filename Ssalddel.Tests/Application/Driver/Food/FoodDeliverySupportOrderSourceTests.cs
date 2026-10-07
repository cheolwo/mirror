using System.Reflection;
using FDriverApp.PageModels;
using Ssalddel.Application.Driver.Food;
using Ssalddel.Contracts.Common.Drivers;
using Ssalddel.Contracts.Driver.Food;
using 살뜰.도메인.음식;
using 살뜰.도메인.운송;

namespace Ssalddel.Tests.Application.Driver.Food;

public sealed class FoodDeliverySupportOrderSourceTests
{
    [Theory]
    [InlineData("attempt-order", "original-order", "offer-id", "attempt-order")]
    [InlineData("", "original-order", "offer-id", "original-order")]
    [InlineData(null, "", "legacy-order", "legacy-order")]
    public void 지원연결주문번호는수행정본을투영하며제안제목에서추정하지않는다(string? attemptOrder, string originalOrder, string transportOrder, string expected)
    {
        var transport = new 운송원장 { 의뢰Id = transportOrder, 원본의뢰Id = originalOrder };
        var offer = new DriverWorkOfferDto("completely-different-offer", "food", "food", "delivery", "title is not an order", "", new("", "", 0, 0), new("", "", 0, 0), 4000, 1, "");
        var attempt = attemptOrder is null ? null : new 음식배달시도 { 주문번호 = attemptOrder };
        var method = typeof(FoodDeliveryDriverWorkspaceUseCase).GetMethod("ToActiveDelivery", BindingFlags.NonPublic | BindingFlags.Static)!;
        var dto = Assert.IsType<FoodDeliveryDriverActiveDeliveryDto>(method.Invoke(null, [transport, offer, attempt, null]));
        Assert.Equal(expected, dto.OrderNo); Assert.NotEqual(dto.OfferId, dto.OrderNo); Assert.NotEqual(dto.OrderSummary, dto.OrderNo);
        var preview = ActiveDeliveryPreview.From(dto); Assert.Equal(expected, preview.OrderNo); Assert.True(preview.HasSupportSource);
    }
    [Fact]
    public void 구버전서버가주문번호를주지않으면제목이나제안ID로진입을만들지않는다()
    {
        var preview = ActiveDeliveryPreview.From(new() { OfferId = "offer-123", OrderSummary = "FOOD-guess" });
        Assert.Empty(preview.OrderNo); Assert.False(preview.HasSupportSource);
    }
}
