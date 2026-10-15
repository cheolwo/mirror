using Ssalddel.Application.Shipper.Request;
using Ssalddel.Contracts.Shipper.Request;

namespace Ssalddel.Tests.Application.Shipper.Payment;

public sealed class ShipperDirectFareR73Tests
{
    [Fact]
    public void 화주제안은참고단가가아닌저장할배송료와기사금액이된다()
    {
        var request = Create();
        request.요금옵션!.최종운임 = 90000;
        request.요금옵션.기본운임 = 70000;
        var result = 화주직접운임제안Policy.준비(request);
        Assert.True(result.IsSuccess);
        Assert.Equal(5000m, result.Value!.최종운임);
        Assert.Equal(5000m, result.Value.기사지급예정운임);
        Assert.Equal(0m, result.Value.플랫폼수수료);
        Assert.Equal("ShipperDirectOffer", result.Value.단가출처);
        Assert.Null(result.Value.기본운임);
        Assert.Null(result.Value.Km당단가);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public void 미확정제안금액은등록할수없다(int amount)
    {
        var request = Create();
        request.결제예정금액 = amount;
        Assert.True(화주직접운임제안Policy.준비(request).IsFailed);
    }

    [Fact]
    public void 기사금액을줄이거나중간수수료를추가할수없다()
    {
        var request = Create();
        request.요금옵션!.기사지급예정운임 = 4000;
        Assert.True(화주직접운임제안Policy.준비(request).IsFailed);
        request.요금옵션.기사지급예정운임 = 5000;
        request.요금옵션.플랫폼수수료 = 1000;
        Assert.True(화주직접운임제안Policy.준비(request).IsFailed);
    }

    [Fact]
    public void 기존일반견적을직접제안으로소급변경하지않는다()
    {
        var request = Create();
        request.요금옵션!.화주직접운임제안 = false;
        Assert.Same(request.요금옵션, 화주직접운임제안Policy.준비(request).Value);
    }

    private static 화주운송의뢰생성요청 Create() => new()
    {
        결제예정금액 = 5000,
        요금옵션 = new() { 화주직접운임제안 = true, 기사지급예정운임 = 5000 }
    };
}
