using System.Text.Json;
using Ssalddel.Contracts.Shipper.Request;
using Ssalddel.Ui.Common.Areas.App.ViewModels;

namespace Ssalddel.Tests.UiCommon;

public sealed class ShipperRequestMobileValidationTests
{
    [Fact]
    public void 모바일필수오류의합계는기존전체필수규칙과같고상위네단계를보존한다()
    {
        var state = new 운송의뢰작성ViewModel();
        var parts = ShipperRequestMobileStepCatalog.Steps.Where(x => x.Step != ShipperRequestMobileStep.Review)
            .SelectMany(x => state.GetMobileStepErrors(x.Step)).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(state.필수입력오류목록.Select(x => x.내용).Order(StringComparer.Ordinal), parts);
        Assert.Equal(state.필수입력오류목록.Select(x => x.내용), state.GetMobileStepErrors(ShipperRequestMobileStep.Review));
        Assert.Equal(4, state.단계목록.Count);
        Assert.All(state.필수입력오류목록, x => Assert.Equal(x.단계, ShipperRequestMobileStepCatalog.Get(x.MobileStep!.Value).MajorStep));
        Assert.False(state.서버등록가능);
    }

    [Fact]
    public void 화물종류와포장규격은다른문맥이며앞뒤미입력오류를현재단계에쌓지않는다()
    {
        var state = new 운송의뢰작성ViewModel();
        Assert.Single(state.GetMobileStepErrors(ShipperRequestMobileStep.Cargo));
        Assert.Equal(4, state.GetMobileStepErrors(ShipperRequestMobileStep.Package).Count);
        state.화물종류 = "시험 상자";
        Assert.Empty(state.GetMobileStepErrors(ShipperRequestMobileStep.Cargo));
        Assert.NotEmpty(state.GetMobileStepErrors(ShipperRequestMobileStep.Package));
        state.화물중량Kg = 3; state.화물길이Mm = 200; state.화물폭Mm = 100; state.화물높이Mm = 100;
        Assert.Empty(state.GetMobileStepErrors(ShipperRequestMobileStep.Package));
        Assert.NotEmpty(state.GetMobileStepErrors(ShipperRequestMobileStep.Pickup));
        Assert.False(state.서버등록가능);
    }

    [Fact]
    public void 수량이나부피로전체중량과치수누락을대체하지않는다()
    {
        var state = new 운송의뢰작성ViewModel { 화물종류 = "시험 상자", 화물수량 = 10, 화물부피Cbm = 1 };
        Assert.Equal(4, state.GetMobileStepErrors(ShipperRequestMobileStep.Package).Count);
        state.화물중량Kg = 0; state.화물길이Mm = -1; state.화물폭Mm = 0;
        Assert.Equal(4, state.GetMobileStepErrors(ShipperRequestMobileStep.Package).Count);
        Assert.Equal(4, state.GetMobileStepErrors(ShipperRequestMobileStep.Package).Distinct(StringComparer.Ordinal).Count());
        state.화물수량 = 0; state.화물부피Cbm = 0;
        Assert.Equal(6, state.GetMobileStepErrors(ShipperRequestMobileStep.Package).Count); // 서로 다른 수량·부피 오류는 보존한다.
    }

    [Fact]
    public void 출발과도착의주소는분리하고기존선택연락처를새필수로만들지않는다()
    {
        var state = new 운송의뢰작성ViewModel { 상차도로명주소 = "시험 출발지" };
        Assert.Empty(state.GetMobileStepErrors(ShipperRequestMobileStep.Pickup));
        Assert.Single(state.GetMobileStepErrors(ShipperRequestMobileStep.Dropoff));
        state.하차도로명주소 = "시험 도착지";
        Assert.Empty(state.GetMobileStepErrors(ShipperRequestMobileStep.Dropoff));
        Assert.Contains(state.입력검증메시지목록, x => !x.필수 && x.내용.Contains("상차 담당자 연락처"));
        Assert.Contains(state.입력검증메시지목록, x => !x.필수 && x.내용.Contains("하차 담당자 연락처"));
        Assert.NotEmpty(state.GetMobileStepErrors(ShipperRequestMobileStep.Schedule));
    }

    [Fact]
    public void 시간단계는필수상차시간창과선택하차시간창의기존짝검사를따른다()
    {
        var state = new 운송의뢰작성ViewModel();
        Assert.Single(state.GetMobileStepErrors(ShipperRequestMobileStep.Schedule));
        state.상차시간창시작일시 = new(2026, 10, 20, 10, 0, 0);
        state.상차시간창종료일시 = new(2026, 10, 20, 11, 0, 0);
        Assert.Empty(state.GetMobileStepErrors(ShipperRequestMobileStep.Schedule));
        state.하차시간창시작일시 = new(2026, 10, 20, 12, 0, 0);
        Assert.Single(state.GetMobileStepErrors(ShipperRequestMobileStep.Schedule));
        state.하차시간창종료일시 = new(2026, 10, 20, 13, 0, 0);
        Assert.Empty(state.GetMobileStepErrors(ShipperRequestMobileStep.Schedule));
        state.상차시간창종료일시 = state.상차시간창시작일시;
        Assert.Single(state.GetMobileStepErrors(ShipperRequestMobileStep.Schedule));
    }

    [Fact]
    public void 차량선택과기존지급조건오류는각자단계에만나타난다()
    {
        var state = new 운송의뢰작성ViewModel { 결제수단 = "", 결제예정금액 = null };
        Assert.Single(state.GetMobileStepErrors(ShipperRequestMobileStep.Vehicle));
        Assert.Equal(2, state.GetMobileStepErrors(ShipperRequestMobileStep.Payment).Count);
        state.차량종류 = "1톤 카고";
        Assert.Empty(state.GetMobileStepErrors(ShipperRequestMobileStep.Vehicle));
        Assert.Equal(2, state.GetMobileStepErrors(ShipperRequestMobileStep.Payment).Count);
        state.결제수단 = "계좌이체"; state.결제예정금액 = 5000;
        Assert.Empty(state.GetMobileStepErrors(ShipperRequestMobileStep.Payment));
        Assert.False(state.서버등록가능);
    }

    [Fact]
    public void 단계조회와앞뒤탐색은초안과기존검토결과를변경하지않는다()
    {
        var state = Complete();
        var before = JsonSerializer.Serialize(state.ToDraft());
        foreach (var definition in ShipperRequestMobileStepCatalog.Steps.Concat(ShipperRequestMobileStepCatalog.Steps.Reverse()))
        {
            Assert.Empty(state.GetMobileStepErrors(definition.Step));
            _ = ShipperRequestMobileStepCatalog.Previous(definition.Step);
            _ = ShipperRequestMobileStepCatalog.Next(definition.Step);
        }
        var restored = new 운송의뢰작성ViewModel(); restored.ApplyDraft(state.ToDraft());
        Assert.Equal(before, JsonSerializer.Serialize(state.ToDraft()));
        Assert.Equal(before, JsonSerializer.Serialize(restored.ToDraft()));
        Assert.True(state.서버등록가능); Assert.True(restored.서버등록가능);
        Assert.All(state.단계목록, x => Assert.True(x.완료));
        Assert.Throws<ArgumentOutOfRangeException>(() => state.GetMobileStepErrors((ShipperRequestMobileStep)999));
    }

    [Fact]
    public void 선택연락처의확인링크는각각출발과도착입력으로돌아가며필수로바뀌지않는다()
    {
        var state = Complete();
        state.상차연락처전화번호 = string.Empty;
        state.하차연락처전화번호 = string.Empty;

        var pickup = Assert.Single(state.입력검증메시지목록.Where(x => x.내용.Contains("상차 담당자 연락처")));
        var dropoff = Assert.Single(state.입력검증메시지목록.Where(x => x.내용.Contains("하차 담당자 연락처")));
        Assert.Equal(ShipperRequestMobileStep.Pickup, pickup.MobileStep);
        Assert.Equal(ShipperRequestMobileStep.Dropoff, dropoff.MobileStep);
        Assert.All(new[] { pickup, dropoff }, message =>
        {
            Assert.False(message.필수);
            Assert.Equal(ShipperRequestAuthoringStep.Transport, message.단계);
        });
        Assert.Empty(state.GetMobileStepErrors(ShipperRequestMobileStep.Pickup));
        Assert.Empty(state.GetMobileStepErrors(ShipperRequestMobileStep.Dropoff));
        Assert.True(state.서버등록가능);
    }

    [Fact]
    public void 운송정책의주소와현장요청안내는실제모바일입력문맥으로연결된다()
    {
        var state = new 운송의뢰작성ViewModel { 화물적재형태 = "벌크/비포장" };
        var warnings = state.운송정보경고목록;
        var messages = state.입력검증메시지목록.Where(x => warnings.Contains(x.내용)).ToArray();

        Assert.Equal(warnings, messages.Select(x => x.내용));
        Assert.Equal(ShipperRequestMobileStep.Pickup,
            Assert.Single(messages.Where(x => x.내용 == "상차지 주소를 입력해야 합니다.")).MobileStep);
        Assert.Equal(ShipperRequestMobileStep.Dropoff,
            Assert.Single(messages.Where(x => x.내용 == "하차지 주소를 입력해야 합니다.")).MobileStep);
        Assert.Equal(ShipperRequestMobileStep.Schedule,
            Assert.Single(messages.Where(x => x.내용.Contains("요청사항"))).MobileStep);
        Assert.All(messages, message =>
        {
            Assert.False(message.필수);
            Assert.Equal(ShipperRequestAuthoringStep.Transport, message.단계);
        });
    }

    [Fact]
    public void 요금과기사보수및알선안내는차량대신비용입력으로돌아가며기존경고를보존한다()
    {
        var state = Complete();
        state.기준운임 = 10000;
        state.기사지급예정운임 = 2000;
        state.알선단계 = 2;
        state.재알선금지 = true;
        state.알선소Id = "synthetic-broker";
        var warnings = state.요금정책경고목록;
        var messages = state.입력검증메시지목록.Where(x => warnings.Contains(x.내용)).ToArray();

        Assert.NotEmpty(warnings);
        Assert.Equal(warnings, messages.Select(x => x.내용));
        Assert.All(messages, message =>
        {
            Assert.Equal(ShipperRequestMobileStep.Payment, message.MobileStep);
            Assert.Equal(ShipperRequestAuthoringStep.Procedure, message.단계);
            Assert.False(message.필수);
        });
        Assert.Empty(state.GetMobileStepErrors(ShipperRequestMobileStep.Vehicle));
        Assert.Empty(state.GetMobileStepErrors(ShipperRequestMobileStep.Payment));
        Assert.True(state.서버등록가능);
    }

    private static 운송의뢰작성ViewModel Complete() => new()
    {
        화물종류 = "시험 상자", 화물수량 = 2, 화물중량Kg = 4, 화물길이Mm = 300, 화물폭Mm = 200, 화물높이Mm = 100,
        상차도로명주소 = "시험 출발지", 상차상세주소 = "시험 상차장", 상차연락처이름 = "시험 담당자",
        상차연락처전화번호 = "010-0000-0000", 하차도로명주소 = "시험 도착지", 하차상세주소 = "시험 하차장",
        상차시간창시작일시 = new(2026, 10, 20, 10, 0, 0), 상차시간창종료일시 = new(2026, 10, 20, 11, 0, 0),
        차량종류 = "1톤 카고", 결제수단 = "계좌이체", 결제예정금액 = 5000, 기사지급예정운임 = 5000
    };
}
