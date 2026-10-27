using Ssalddel.Application.Shipper.Request;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Shipper.Request;
using 살뜰.도메인.공통;
using 살뜰.도메인.화주;

namespace Ssalddel.Tests.Application.Shipper.Request;

public sealed class 의뢰입력정책분리Tests
{
    [Fact]
    public void 동일모바일접수의_기존ID벡터와_원문사용자결속을_보존한다()
    {
        var clientId = 화주운송의뢰생성입력Policy.접수요청Id정규화("  shipper-app-00000000000000000000000000000001  ");
        Assert.True(화주운송의뢰생성입력Policy.모바일접수(clientId));
        Assert.Equal("2cc2e8ac-49c1-60e9-28d9-0d96216dbd86",
            화주운송의뢰생성입력Policy.고정의뢰Id("shipper-raw", clientId, true));
        Assert.NotEqual(화주운송의뢰생성입력Policy.고정의뢰Id("shipper-raw", clientId, true),
            화주운송의뢰생성입력Policy.고정의뢰Id(" shipper-raw ", clientId, true));
        Assert.False(화주운송의뢰생성입력Policy.모바일접수("shipper-app-invalid"));
        Assert.Null(화주운송의뢰생성입력Policy.고정의뢰Id("shipper-raw", "legacy-request", false));
        Assert.NotNull(화주운송의뢰생성입력Policy.고정의뢰Id("shipper-raw",
            NeighborhoodDeliveryRoutes.ClientRequestPrefix + "legacy", false));
    }

    [Fact]
    public void 입력오류의_선후와_미지정하차시간창을_보존한다()
    {
        var command = Command() with { 화물종류 = " ", 픽업도로명주소 = "", 픽업작업시간분 = -1m };
        Assert.Equal("화물종류 is required", 화주운송의뢰생성입력Policy.입력오류(command));
        Assert.Equal("픽업 주소 도로명주소 is required",
            화주운송의뢰생성입력Policy.입력오류(command with { 화물종류 = "상자" }));
        Assert.Null(화주운송의뢰생성입력Policy.입력오류(Command() with
        { 하차시간창시작일시 = Command().픽업시간창시작일시, 하차시간창종료일시 = null }));
    }

    [Theory]
    [InlineData(null, "결제대기")]
    [InlineData("", "결제대기")]
    [InlineData("   ", "결제대기")]
    [InlineData(" 결제완료 ", "결제완료")]
    public void 결제상태만_기존기준대로_정규화한다(string? input, string expected)
        => Assert.Equal(expected, 화주운송의뢰생성입력Policy.결제상태정규화(input));

    [Theory]
    [InlineData(정산시점.선결제, null, "결제대기")]
    [InlineData(정산시점.현장지급, 증빙방식.인수증, "현장수금예정")]
    [InlineData(정산시점.운송완료후정산, null, "청구대기")]
    [InlineData(정산시점.운송완료후정산, 증빙방식.없음, "청구대기")]
    [InlineData(정산시점.운송완료후정산, 증빙방식.인수증, "인수증대기")]
    [InlineData(정산시점.월말정산, 증빙방식.없음, "후불승인대기")]
    public void 생성의_미지정증빙과_수정의_명시증빙은_기존정산상태를_유지한다(
        정산시점 time, 증빙방식? evidence, string expected)
        => Assert.Equal(expected, 화주운송정산상태Policy.판정(time, evidence));

    [Fact]
    public void 생성정산의_미지정과_명시0_소수금액_및_결제수단원문을_구분한다()
    {
        var omitted = 화주운송의뢰생성Factory.정산입력(Command());
        Assert.Null(omitted.최종운임);
        Assert.Null(omitted.결제예정금액);
        Assert.Equal("카드", omitted.결제수단);
        var supplied = 화주운송의뢰생성Factory.정산입력(Command() with { 결제수단 = " ", 최종운임 = 5000.75m });
        Assert.Equal(" ", supplied.결제수단);
        Assert.Equal(5000, supplied.결제예정금액);
        Assert.Equal(0, 화주운송의뢰생성Factory.정산입력(Command() with { 결제예정금액 = 0 }).결제예정금액);
        Assert.Throws<OverflowException>(() => 화주운송의뢰생성Factory.정산입력(Command() with { 최종운임 = decimal.MaxValue }));
    }

    [Fact]
    public void 의뢰구성은_주소연락처원문과_미지정값을_보존하고_조회된좌표를_사용한다()
    {
        var command = Command() with { 화물설명 = "  원문 상자  ", 화물수량 = null, 화물부피Cbm = null,
            픽업연락처이름 = "  담당  ", 픽업위도 = 99m, 픽업경도 = 99m, 픽업작업시간분 = 0m,
            하차작업시간분 = null };
        var entity = Entity(command);
        Assert.Equal("  원문 상자  ", entity.화물설명);
        Assert.Equal("  담당  ", entity.픽업_연락처_이름);
        Assert.Null(entity.화물수량);
        Assert.Null(entity.화물부피Cbm);
        Assert.Equal(37m, entity.픽업_위도);
        Assert.Equal(127m, entity.픽업_경도);
        Assert.Equal(0m, entity.픽업_작업시간분);
        Assert.Null(entity.하차_작업시간분);
        Assert.Equal(default(DateTime), entity.CreatedAt);
        Assert.Equal(default(DateTime), entity.UpdatedAt);
    }

    [Fact]
    public void 운임구성_생성조건과_모의수행의_정산제외를_유지한다()
    {
        Assert.False(화주운송의뢰생성입력Policy.운임구성필요(Command() with { 대기료 = 100m }));
        Assert.True(화주운송의뢰생성입력Policy.운임구성필요(Command() with { 최종운임 = 0m }));
        var command = Command() with { SimulationFulfillment = true, SimulationAutomaticFulfillment = true };
        var entity = Entity(command);
        Assert.True(entity.모의처리처수행);
        Assert.True(entity.자동처리처수행);
        Assert.Equal("모의 수행·정산 제외", entity.정산상태);
    }

    [Fact]
    public void 수정의_미제공과_공백원문_명시false를_같게취급하지않는다()
    {
        var entity = Entity(Command());
        var empty = new 위치정보입력값(null, null, null, null, null, null, null, null);
        Assert.False(화주운송의뢰수정Policy.HasChanges(empty));
        Assert.True(화주운송의뢰수정Policy.HasChanges(empty with { 상세주소 = "" }));
        Assert.True(화주운송의뢰수정Policy.HasChanges(empty with { 작업시간분 = 0m }));
        Assert.True(화주운송의뢰수정Policy.HasChanges(empty with { 작업시간입력초기화 = true }));
        Assert.False(화주운송의뢰수정Policy.HasSettlementChanges(new(" ", null), entity));
        Assert.True(화주운송의뢰수정Policy.HasSettlementChanges(new(" 카드 ", null), entity));
        var cargo = new 화물정보입력값(null, null, null, null, null, null, null);
        Assert.False(화주운송의뢰수정Policy.HasCargoConditionChanges(cargo, entity));
        Assert.True(화주운송의뢰수정Policy.HasCargoConditionChanges(cargo with { 화물바닥회전가능여부 = false }, entity));
    }

    private static 화주운송의뢰 Entity(의뢰생성Command command)
        => 화주운송의뢰생성Factory.생성(command, "fixed-request", "actor", "shipper", "request-original",
            상태값.결제상태.결제대기, 화주운송의뢰생성Factory.정산입력(command), (37m, 127m), (38m, 128m));

    private static 의뢰생성Command Command()
        => 화주운송의뢰일괄등록지원.To의뢰생성Command(new()
        {
            화물종류 = "합성 상자", 픽업도로명주소 = "합성 상차 도로", 하차도로명주소 = "합성 하차 도로",
            픽업연락처전화번호 = "010-0000-0000",
            픽업시간창시작일시 = new DateTime(2026, 10, 20, 1, 0, 0, DateTimeKind.Utc),
            픽업시간창종료일시 = new DateTime(2026, 10, 20, 2, 0, 0, DateTimeKind.Utc)
        }, new()) with { 정산조건 = null, 결제수단 = null };
}
