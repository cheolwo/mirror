using Ssalddel.Contracts.Shipper.Request;
using Ssalddel.Ui.Common.Areas.App.ViewModels;

namespace Ssalddel.Tests.UiCommon;

public sealed class ShipperRequestAuthoringViewModelTests
{
    [Fact]
    public void 빈초안의필수오류는_각책임Screen단계를가리킨다()
    {
        var target = new 운송의뢰작성ViewModel();

        var requiredSteps = target.필수입력오류목록
            .Select(message => message.단계)
            .Distinct()
            .ToArray();

        Assert.Contains(ShipperRequestAuthoringStep.Cargo, requiredSteps);
        Assert.Contains(ShipperRequestAuthoringStep.Transport, requiredSteps);
        Assert.Contains(ShipperRequestAuthoringStep.Procedure, requiredSteps);
        Assert.DoesNotContain(ShipperRequestAuthoringStep.Review, requiredSteps);
    }

    [Fact]
    public void 모든Screen은_같은DraftRoundTrip을사용한다()
    {
        var source = CreateCompleteState();
        var draft = source.ToDraft();
        var restored = new 운송의뢰작성ViewModel();

        restored.ApplyDraft(draft);

        Assert.Equal(source.화물종류, restored.화물종류);
        Assert.Equal(source.상차도로명주소, restored.상차도로명주소);
        Assert.Equal(source.하차도로명주소, restored.하차도로명주소);
        Assert.Equal(source.차량종류, restored.차량종류);
        Assert.Equal(source.결제예정금액, restored.결제예정금액);
        Assert.True(restored.서버등록가능);
        Assert.All(restored.단계목록, step => Assert.True(step.완료));
    }

    [Fact]
    public void 재알선금지와다단계알선은_Procedure경고와Draft정책에함께남는다()
    {
        var target = CreateCompleteState();
        target.재알선금지 = true;
        target.알선단계 = 2;
        target.알선소Id = "BROKER-17";

        var warnings = target.입력검증메시지목록
            .Where(message => message.단계 == ShipperRequestAuthoringStep.Procedure)
            .Select(message => message.내용)
            .ToArray();
        var draft = target.ToDraft();

        Assert.Contains(warnings, value => value.Contains("재알선차단필요", StringComparison.Ordinal));
        Assert.Contains(warnings, value => value.Contains("재알선의심", StringComparison.Ordinal));
        Assert.True(draft.정책위반);
        Assert.True(draft.재알선의심);
    }

    private static 운송의뢰작성ViewModel CreateCompleteState()
        => new()
        {
            화물종류 = "공동구매 식재료",
            화물수량 = 24,
            화물중량Kg = 180m,
            상차도로명주소 = "서울시 공개 상차지",
            하차도로명주소 = "서울시 공개 하차지",
            상차시간창시작일시 = new DateTime(2026, 10, 3, 9, 0, 0),
            상차시간창종료일시 = new DateTime(2026, 10, 3, 10, 0, 0),
            차량종류 = "1톤 카고",
            결제수단 = "카드",
            결제예정금액 = 180_000,
            기준운임 = 160_000m,
            기사지급예정운임 = 130_000
        };
}
