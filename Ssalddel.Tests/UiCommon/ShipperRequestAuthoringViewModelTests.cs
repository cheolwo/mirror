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

    [Fact]
    public void 수량만있는기존초안은_그대로복구하고_등록전중량과치수를안내한다()
    {
        var source = new 운송의뢰작성ViewModel { 화물종류 = "상자", 화물수량 = 3 };
        var draft = source.ToDraft();
        var restored = new 운송의뢰작성ViewModel(); restored.ApplyDraft(draft);
        Assert.Equal(3, restored.화물수량);
        Assert.Null(restored.화물중량Kg); Assert.Null(restored.화물길이Mm);
        Assert.Null(restored.화물폭Mm); Assert.Null(restored.화물높이Mm);
        Assert.False(restored.화물정보입력됨); Assert.False(restored.서버등록가능);
        Assert.Equal(4, restored.화물필수보완목록.Count);
        Assert.Contains(restored.화물필수보완목록, x => x.Contains("포장·깔판"));
        Assert.All(restored.필수입력오류목록.Where(x => x.구분 == "화물 정보"),
            x => Assert.Equal(ShipperRequestAuthoringStep.Cargo, x.단계));
    }

    [Fact]
    public void 포장치수와팔레트는_Draft왕복에서보존하고_명시초기화로만비운다()
    {
        var source = CreateCompleteState(); source.팔레트개수 = 2;
        var restored = new 운송의뢰작성ViewModel(); restored.ApplyDraft(source.ToDraft());
        Assert.Equal(1000, restored.화물길이Mm); Assert.Equal(800, restored.화물폭Mm);
        Assert.Equal(600, restored.화물높이Mm); Assert.Equal(2, restored.팔레트개수);
        Assert.True(restored.서버등록가능);
        restored.Reset();
        Assert.Null(restored.화물길이Mm); Assert.Null(restored.화물폭Mm);
        Assert.Null(restored.화물높이Mm); Assert.Null(restored.팔레트개수);
    }

    [Fact]
    public void 영이하중량과치수는_수량이나부피가있어도_등록가능으로보지않는다()
    {
        var state = CreateCompleteState();
        state.화물중량Kg = 0m; state.화물길이Mm = 0;
        state.화물폭Mm = -1; state.화물높이Mm = null; state.화물부피Cbm = 1m;
        Assert.False(state.서버등록가능);
        Assert.Equal(4, state.화물필수보완목록.Count);
    }

    private static 운송의뢰작성ViewModel CreateCompleteState()
        => new()
        {
            화물종류 = "공동구매 식재료",
            화물수량 = 24,
            화물중량Kg = 180m,
            화물길이Mm = 1000, 화물폭Mm = 800, 화물높이Mm = 600,
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
