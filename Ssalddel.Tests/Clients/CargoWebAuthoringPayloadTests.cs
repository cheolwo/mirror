using System.Reflection;
using Ssalddel.Contracts.Shipper.Request;
using Ssalddel.Ui.Common.Areas.App.ViewModels;
using Ssalddel.WebApp.Services;

namespace Ssalddel.Tests.Clients;

public sealed class CargoWebAuthoringPayloadTests
{
    [Fact]
    public void 웹등록도_입력담당자와한국시간을_그대로같은계약에전달한다()
    {
        var viewModel = new 운송의뢰작성ViewModel
        {
            상차도로명주소 = "합성 상차 주소", 하차도로명주소 = "합성 하차 주소",
            상차연락처이름 = "합성 상차 담당자", 상차연락처전화번호 = "test-pickup",
            하차연락처이름 = "합성 하차 담당자", 하차연락처전화번호 = "test-dropoff",
            상차시간창시작일시 = new DateTime(2026, 10, 3, 9, 30, 0),
            상차시간창종료일시 = new DateTime(2026, 10, 3, 10, 30, 0),
            하차시간창시작일시 = new DateTime(2026, 10, 3, 12, 0, 0),
            하차시간창종료일시 = new DateTime(2026, 10, 3, 13, 0, 0)
        };
        var payload = Payload(viewModel);
        Assert.Equal("test-pickup", payload.픽업!.연락처.전화번호);
        Assert.Equal("합성 하차 담당자", payload.하차!.연락처.이름);
        Assert.Equal(new DateTime(2026, 10, 3, 0, 30, 0, DateTimeKind.Utc), payload.픽업.시간창!.시작일시);
        Assert.Equal(new DateTime(2026, 10, 3, 4, 0, 0, DateTimeKind.Utc), payload.하차.시간창!.종료일시);
    }

    [Fact]
    public void 미입력하차시간이나전화번호를_샘플로채우지않는다()
    {
        var payload = Payload(new 운송의뢰작성ViewModel());
        Assert.Null(payload.하차!.시간창);
        Assert.Empty(payload.하차.연락처.전화번호);
        Assert.Null(payload.픽업!.시간창);
        Assert.Empty(payload.픽업.연락처.이름);
        Assert.Null(payload.화물.길이Mm); Assert.Null(payload.화물.폭Mm);
        Assert.Null(payload.화물.높이Mm); Assert.Null(payload.화물.팔레트개수);
    }

    [Fact]
    public void 웹등록은_입력한포장치수와팔레트를_원문계약에보존한다()
    {
        var state = new 운송의뢰작성ViewModel { 화물종류 = "상자", 화물중량Kg = 12.5m,
            화물길이Mm = 400, 화물폭Mm = 300, 화물높이Mm = 200, 팔레트개수 = 2 };
        var payload = Payload(state);
        Assert.Equal(12.5m, payload.화물.중량Kg);
        Assert.Equal(400, payload.화물.길이Mm); Assert.Equal(300, payload.화물.폭Mm);
        Assert.Equal(200, payload.화물.높이Mm); Assert.Equal(2, payload.화물.팔레트개수);
    }

    private static 화주운송의뢰생성요청 Payload(운송의뢰작성ViewModel source)
        => (화주운송의뢰생성요청)typeof(화주운송의뢰등록Service)
            .GetMethod("ToCreateRequest", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [source, "synthetic-shipper", null, ""])!;
}
