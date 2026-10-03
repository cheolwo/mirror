using Ssalddel.Application.Driver.Transport;
using Ssalddel.Application.Shipper.Request;
using 살뜰.도메인.공통;
using 살뜰.도메인.화주;

namespace Ssalddel.Tests.Application.Shipper.Request;

public sealed class 화주운송배차진입정책Tests
{
    [Theory]
    [InlineData("선결제", "결제완료", "결제완료")]
    [InlineData("운송완료후정산", "결제대기", "후불승인완료")]
    [InlineData("월말정산", "결제대기", "후불승인완료")]
    [InlineData("현장지급", "결제대기", "현장수금예정")]
    public void 결제확보나명시적인정산조건이확인되면_실제결제상태를바꾸지않고허용한다(
        string settlementTime,
        string paymentStatus,
        string settlementStatus)
    {
        var request = CreateRequest(settlementTime, paymentStatus, settlementStatus);

        var result = 화주운송배차진입정책.판정(request);

        Assert.True(result.가능);
        Assert.Equal(paymentStatus, request.결제상태);
        Assert.Equal(settlementStatus, request.정산상태);
    }

    [Theory]
    [InlineData("선결제", "결제대기", "결제대기")]
    [InlineData("선결제", "결제대기", "후불승인완료")]
    [InlineData("운송완료후정산", "결제대기", "청구대기")]
    [InlineData("운송완료후정산", "결제대기", "인수증대기")]
    [InlineData("운송완료후정산", "결제대기", "인수증등록완료")]
    [InlineData("월말정산", "결제대기", "후불승인대기")]
    [InlineData("현장지급", "결제대기", "정산조건작성됨")]
    public void 승인전정산조건을_결제확보나후불승인으로취급하지않는다(
        string settlementTime,
        string paymentStatus,
        string settlementStatus)
    {
        var request = CreateRequest(settlementTime, paymentStatus, settlementStatus);

        Assert.False(화주운송배차진입정책.판정(request).가능);
        Assert.Equal(paymentStatus, request.결제상태);
    }

    [Theory]
    [InlineData("결제취소", "후불승인완료")]
    [InlineData("환불됨", "후불승인완료")]
    [InlineData("결제완료", "정산취소")]
    [InlineData("결제완료", "비정상운송검토보류")]
    [InlineData("결제대기", "비정상운송검토보류")]
    [InlineData("결제완료", "미수발생")]
    public void 취소환불보류미수는_결제나후불승인값보다우선해차단한다(
        string paymentStatus,
        string settlementStatus)
    {
        var request = CreateRequest("운송완료후정산", paymentStatus, settlementStatus);

        Assert.False(화주운송배차진입정책.판정(request).가능);
    }

    [Theory]
    [InlineData("취소", "매칭중")]
    [InlineData("생성됨", "취소")]
    [InlineData("알수없는상태", "매칭중")]
    public void 취소되거나알수없는의뢰상태는_정산승인이있어도차단한다(
        string requestStatus,
        string dispatchStatus)
    {
        var request = CreateRequest("운송완료후정산", "결제대기", "후불승인완료");
        request.상태 = requestStatus;
        request.배차상태 = dispatchStatus;

        Assert.False(화주운송배차진입정책.판정(request).가능);
    }

    [Theory]
    [InlineData("알수없는시점", "결제완료", "결제완료")]
    [InlineData("0", "결제완료", "결제완료")]
    [InlineData("운송완료후정산", "알수없는결제", "후불승인완료")]
    [InlineData("운송완료후정산", "결제완료", "알수없는정산")]
    [InlineData("운송완료후정산", "결제완료", "2")]
    [InlineData("운송완료후정산", "결제대기", "")]
    public void 확인할수없는계약값이나숫자열거값은_수락하지않는다(
        string settlementTime,
        string paymentStatus,
        string settlementStatus)
    {
        var request = CreateRequest(settlementTime, paymentStatus, settlementStatus);

        Assert.False(화주운송배차진입정책.판정(request).가능);
    }

    [Fact]
    public void 후불배차승인은_실제입금확인과분리되고_완료후청구대상은유지된다()
    {
        var request = CreateRequest("운송완료후정산", "결제대기", "후불승인완료");

        Assert.True(화주운송배차진입정책.판정(request).가능);
        Assert.True(운송완료입금요청정책.입금요청대상인가(request));
        Assert.Equal(상태값.결제상태.결제대기, request.결제상태);
        Assert.Null(request.현장수금확인일시);
    }

    private static 화주운송의뢰 CreateRequest(
        string settlementTime,
        string paymentStatus,
        string settlementStatus)
        => new()
        {
            의뢰Id = "warehouse-outbound-policy",
            정산시점 = settlementTime,
            결제상태 = paymentStatus,
            정산상태 = settlementStatus
        };
}
