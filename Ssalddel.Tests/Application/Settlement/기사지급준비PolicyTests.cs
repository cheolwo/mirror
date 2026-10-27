using Ssalddel.Application.Driver.Settlement;
using Ssalddel.Application.Settlement;
using Ssalddel.Contracts.Driver.Settlement;
using Ssalddel.Contracts.Shipper.Request;
using 살뜰.도메인.공통;
using 살뜰.도메인.화주;

namespace Ssalddel.Tests.Application.Settlement;

public sealed class 기사지급준비PolicyTests
{
    [Theory]
    [InlineData("missing-source", 기사지급준비상태코드.원천의뢰없음)]
    [InlineData("incident-before-simulation", 기사지급준비상태코드.비정상운송검토보류)]
    [InlineData("simulation-before-amount", "SimulationFulfillmentExcluded")]
    [InlineData("amount-before-field-collection", 기사지급준비상태코드.지급예정운임없음)]
    [InlineData("negative-amount", 기사지급준비상태코드.지급예정운임없음)]
    [InlineData("field-collected", 기사지급준비상태코드.현장수금확인)]
    [InlineData("field-pending", 기사지급준비상태코드.현장수금대기)]
    [InlineData("collection-before-account", 기사지급준비상태코드.화주수납대기)]
    [InlineData("account-missing", 기사지급준비상태코드.정산계좌없음)]
    [InlineData("account-unverified", 기사지급준비상태코드.정산계좌미확인)]
    [InlineData("payment-confirmed", 기사지급준비상태코드.지급준비가능)]
    [InlineData("deposit-confirmed", 기사지급준비상태코드.지급준비가능)]
    [InlineData("settlement-completed", 기사지급준비상태코드.지급준비가능)]
    public void 여러_역할이_공유하는_보류와_준비_판정의_우선순위를_보존한다(string scenario, string expected)
    {
        화주운송의뢰? request = new()
        {
            결제상태 = 상태값.결제상태.결제완료,
            정산시점 = 정산시점.선결제.ToString(),
            정산상태 = 운임정산상태.결제대기.ToString()
        };
        decimal? amount = 4000m;
        var hasAccount = true;
        var verified = true;
        var incident = false;
        switch (scenario)
        {
            case "missing-source": request = null; incident = true; break;
            case "incident-before-simulation": request.모의처리처수행 = true; amount = null; incident = true; break;
            case "simulation-before-amount": request.모의처리처수행 = true; amount = null; hasAccount = false; break;
            case "amount-before-field-collection":
                amount = null; request.정산시점 = 정산시점.현장지급.ToString(); request.현장수금확인일시 = DateTime.UnixEpoch; break;
            case "negative-amount": amount = -1m; break;
            case "field-collected":
                request.정산시점 = 정산시점.현장지급.ToString(); request.현장수금확인일시 = DateTime.UnixEpoch; hasAccount = false; break;
            case "field-pending": request.정산시점 = 정산시점.현장지급.ToString(); hasAccount = false; break;
            case "collection-before-account": request.결제상태 = "대기"; hasAccount = false; break;
            case "account-missing": hasAccount = false; verified = false; break;
            case "account-unverified": verified = false; break;
            case "deposit-confirmed": request.결제상태 = "대기"; request.정산상태 = 운임정산상태.입금확인완료.ToString(); break;
            case "settlement-completed": request.결제상태 = "대기"; request.정산상태 = 운임정산상태.정산완료.ToString(); break;
        }

        var decision = 기사지급준비Policy.판정(request, amount, hasAccount, verified, incident);

        Assert.Equal(expected, decision.Code);
        Assert.Equal(expected == 기사지급준비상태코드.지급준비가능, decision.IsReady);
        Assert.Equal(decision, 기사지급준비UseCase.준비상태판정(request, amount, hasAccount, verified, incident));
    }
}
