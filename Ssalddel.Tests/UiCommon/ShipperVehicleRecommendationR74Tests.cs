using System.Text.Json;
using Ssalddel.Contracts.Shipper.Request;
using Ssalddel.Ui.Common.Areas.App.Models;
using Ssalddel.Ui.Common.Areas.App.ViewModels;

namespace Ssalddel.Tests.UiCommon;

/// <summary>공통 작성 상태의 실제 추천·초안·동시응답 보호 시험. 서버 실행이나 실제 차량 적재 증거는 아니다.</summary>
public sealed class ShipperVehicleRecommendationR74Tests
{
    [Fact]
    public void 새초안은_포장하나를보여주고_구판의미확인은_임의로확정하지않는다()
    {
        var state = new 운송의뢰작성ViewModel();
        Assert.Equal(1, state.화물수량);
        Assert.True(state.화물바닥회전가능여부);
        Assert.True(state.화물적층가능여부);
        var legacy = JsonSerializer.Deserialize<운송모델작성Draft>("{}")!;
        state.ApplyDraft(legacy);
        Assert.Null(state.화물수량);
        Assert.Null(state.화물바닥회전가능여부);
        Assert.Null(state.화물적층가능여부);
        Assert.False(state.차량추천조회가능);
        Assert.NotEmpty(state.적재입력.경고);
    }

    [Fact]
    public void 원입력과보수적계산값을_구분하며_회전적층false와원문을_초안에보존한다()
    {
        var state = Cargo();
        state.화물수량 = 3;
        state.화물길이Mm = 1000; state.화물폭Mm = 500; state.화물높이Mm = 400;
        state.화물부피Cbm = .1m;
        state.화물바닥회전가능여부 = false; state.화물적층가능여부 = false;
        state.화물파손주의여부 = true; state.요청사항 = "옆으로 눕히지 마세요.";
        var restored = new 운송의뢰작성ViewModel();
        restored.ApplyDraft(JsonSerializer.Deserialize<운송모델작성Draft>(JsonSerializer.Serialize(state.ToDraft()))!);
        Assert.Equal(.1m, restored.화물부피Cbm);
        Assert.Equal(.6m, restored.적재입력.치수추정부피Cbm);
        Assert.Equal(.6m, restored.적재입력.유효부피Cbm);
        Assert.False(restored.화물바닥회전가능여부);
        Assert.False(restored.화물적층가능여부);
        Assert.True(restored.화물파손주의여부);
        Assert.Equal(state.요청사항, restored.요청사항);
        Assert.Equal(state.적재입력.입력지문, restored.적재입력.입력지문);
        Assert.Equal(restored.적재입력.입력지문,
            화물적재입력Policy.계산(화물적재입력Policy.입력생성(restored.차량추천입력생성())).입력지문);
    }

    [Fact]
    public async Task 서버후보만_명시선택하고_조건변경시_기존선택을재확인한다()
    {
        var state = Cargo();
        Assert.Empty(state.추천차량종류목록); // 기존 20kg 간이 추천을 만들지 않는다.
        await state.차량추천조회Async((request, _) => Task.FromResult(Response(request)));
        Assert.Equal(["검증용 차량"], state.추천차량종류목록);
        Assert.Null(state.차량종류); // 조회와 선택은 별도다.
        Assert.False(state.서버추천차량선택("오토바이 퀵"));
        Assert.True(state.서버추천차량선택("검증용 차량"));
        state.화물적층가능여부 = false;
        Assert.False(state.차량추천현재유효);
        Assert.True(state.적용차량재확인필요);
        Assert.Empty(state.추천차량종류목록);
        Assert.False(state.서버추천차량선택("검증용 차량"));
        Assert.Contains(state.GetMobileStepErrors(ShipperRequestMobileStep.Vehicle), x => x.Contains("다시 확인"));
        await state.차량추천조회Async((request, _) => Task.FromResult(Response(request)));
        Assert.True(state.적용차량재확인필요);
        state.화물기반추천적용();
        Assert.False(state.적용차량재확인필요);
    }

    [Fact]
    public async Task 추천에서고른차량은_조건변경또는복원을반복해도_재확인표시를잃지않는다()
    {
        var state = Cargo();
        await state.차량추천조회Async((request, _) => Task.FromResult(Response(request)));
        state.화물기반추천적용();
        Assert.False(state.적용차량재확인필요);
        state.화물높이Mm++;
        var restored = new 운송의뢰작성ViewModel();
        restored.ApplyDraft(state.ToDraft());
        Assert.Equal("검증용 차량", restored.차량종류);
        Assert.True(restored.적용차량재확인필요);
        Assert.Empty(restored.추천차량종류목록);
        restored.화물중량Kg++;
        restored.ApplyDraft(restored.ToDraft());
        Assert.True(restored.적용차량재확인필요);
        Assert.Contains(restored.GetMobileStepErrors(ShipperRequestMobileStep.Vehicle), x => x.Contains("다시 확인"));
        await restored.차량추천조회Async((request, _) => Task.FromResult(Response(request)));
        restored.화물기반추천적용();
        Assert.False(restored.적용차량재확인필요);
        restored.ApplyDraft(restored.ToDraft());
        Assert.True(restored.적용차량재확인필요); // 변경이 없어도 응답 자체는 복원하지 않는다.
    }

    [Fact]
    public async Task 같은호출의정상접수복사는_현재선택을유지하되_변경된선택을재확인한것으로바꾸지않는다()
    {
        var state = Cargo();
        state.상차도로명주소 = "합성 출발지"; state.하차도로명주소 = "합성 도착지";
        state.상차시간창시작일시 = new(2030, 1, 1, 9, 0, 0);
        state.상차시간창종료일시 = new(2030, 1, 1, 10, 0, 0);
        state.결제예정금액 = 6000;
        await state.차량추천조회Async((request, _) => Task.FromResult(Response(request)));
        state.화물기반추천적용();
        Assert.True(state.서버등록가능);
        var frozen = state.접수입력고정복사();
        Assert.True(frozen.서버등록가능);
        Assert.True(frozen.차량추천현재유효);
        state.화물높이Mm++;
        Assert.True(frozen.서버등록가능);
        Assert.NotEqual(state.화물높이Mm, frozen.화물높이Mm);
        Assert.False(state.접수입력고정복사().서버등록가능);
        var restored = new 운송의뢰작성ViewModel(); restored.ApplyDraft(frozen.ToDraft());
        Assert.False(restored.서버등록가능);
        Assert.False(restored.접수입력고정복사().서버등록가능);
    }

    [Fact]
    public async Task 추천응답폐기는_선택출처를지우지않고_명시초기화만새초안으로바꾼다()
    {
        var state = Cargo();
        await state.차량추천조회Async((request, _) => Task.FromResult(Response(request)));
        state.화물기반추천적용();
        state.차량추천초기화();
        Assert.True(state.적용차량재확인필요);
        Assert.True(state.ToDraft().추천차량재확인필요);
        Assert.Empty(state.추천차량종류목록);
        state.Reset();
        Assert.False(state.적용차량재확인필요);
        Assert.False(state.ToDraft().추천차량재확인필요);
        Assert.Null(state.차량종류);
    }

    [Fact]
    public async Task 연락처와시간창수정은_적재추천을시간약속판정으로_확대하지않는다()
    {
        var state = Cargo();
        await state.차량추천조회Async((request, _) => Task.FromResult(Response(request)));
        state.화물기반추천적용();
        var signature = state.적재입력.입력지문;
        state.상차연락처전화번호 = "합성 연락처";
        state.상차시간창시작일시 = new(2030, 1, 1, 9, 0, 0);
        state.상차시간창종료일시 = new(2030, 1, 1, 10, 0, 0);
        Assert.Equal(signature, state.적재입력.입력지문);
        Assert.True(state.차량추천현재유효);
        Assert.Contains("시간", state.추천운송설명);
    }

    [Theory]
    [InlineData("failure")]
    [InlineData("empty")]
    [InlineData("wrong-input")]
    [InlineData("old-revision")]
    public async Task 실패와빈목록과오래된응답은_고정차량을보충하지않는다(string mode)
    {
        var state = Cargo(); state.차량종류 = "사용자가 남긴 선택";
        await state.차량추천조회Async((request, _) =>
        {
            if (mode == "failure") return Task.FromException<차량추천응답>(new IOException("private-path-token"));
            var response = Response(request, mode == "empty");
            if (mode == "wrong-input") response.적재조건 = 화물적재입력Policy.계산(new() { 화물종류 = "다른 입력" });
            if (mode == "old-revision") response.적재조건 = response.적재조건! with { 계산판본 = "old" };
            return Task.FromResult(response);
        });
        Assert.Empty(state.추천차량종류목록);
        state.화물기반추천적용();
        Assert.Equal("사용자가 남긴 선택", state.차량종류);
        Assert.False(state.차량추천조회중);
        Assert.DoesNotContain("private-path-token", state.추천운송설명);
        if (mode == "empty") { Assert.True(state.차량추천현재유효); Assert.Contains("찾지 못", state.추천운송설명); }
        else { Assert.False(state.차량추천현재유효); Assert.NotNull(state.차량추천조회오류); }
    }

    [Fact]
    public async Task 역순응답은_가장최근입력의추천을덮지않는다()
    {
        var state = Cargo();
        var oldRequest = state.차량추천입력생성();
        var oldReply = new TaskCompletionSource<차량추천응답>(TaskCreationOptions.RunContinuationsAsynchronously);
        var old = state.차량추천조회Async((_, _) => oldReply.Task);
        Assert.True(state.차량추천조회중);
        state.화물수량 = 2;
        await state.차량추천조회Async((request, _) => Task.FromResult(Response(request, name: "최신 차량")));
        oldReply.SetResult(Response(oldRequest, name: "오래된 차량"));
        await old;
        Assert.Equal(["최신 차량"], state.추천차량종류목록);
        Assert.False(state.차량추천조회중);
    }

    [Theory]
    [InlineData("change")]
    [InlineData("reset")]
    [InlineData("restore")]
    public async Task 조회후입력변경과초안교체는_뒤늦은응답을버린다(string operation)
    {
        var state = Cargo(); var request = state.차량추천입력생성();
        var reply = new TaskCompletionSource<차량추천응답>(TaskCreationOptions.RunContinuationsAsynchronously);
        var running = state.차량추천조회Async((_, _) => reply.Task);
        if (operation == "change") state.화물높이Mm++;
        else if (operation == "reset") state.Reset();
        else state.ApplyDraft(state.ToDraft());
        reply.SetResult(Response(request)); await running;
        Assert.False(state.차량추천현재유효);
        Assert.Empty(state.추천차량종류목록);
        Assert.False(state.차량추천조회중);
    }

    [Fact]
    public async Task 추천선택후실패나빈목록재조회는_기존선택문구를보존하되_접수전재확인이필요하다()
    {
        var state = Cargo();
        await state.차량추천조회Async((request, _) => Task.FromResult(Response(request)));
        state.화물기반추천적용();
        await state.차량추천조회Async((_, _) => Task.FromException<차량추천응답>(new IOException()));
        Assert.Equal("검증용 차량", state.차량종류);
        Assert.True(state.적용차량재확인필요);
        Assert.Contains(state.GetMobileStepErrors(ShipperRequestMobileStep.Vehicle), x => x.Contains("다시 확인"));
        await state.차량추천조회Async((request, _) => Task.FromResult(Response(request, empty: true)));
        Assert.True(state.적용차량재확인필요);
        Assert.True(state.접수입력고정복사().적용차량재확인필요);
    }

    [Fact]
    public async Task 실패한재조회는_직전후보를현재성공인것처럼보이지않는다()
    {
        var state = Cargo();
        await state.차량추천조회Async((request, _) => Task.FromResult(Response(request)));
        await state.차량추천조회Async((_, _) => Task.FromException<차량추천응답>(new IOException()));
        Assert.Empty(state.추천차량종류목록);
        Assert.False(state.차량추천현재유효);
        Assert.Contains("다시 확인", state.추천운송설명);
    }

    internal static 운송의뢰작성ViewModel Cargo() => new()
    {
        화물종류 = "합성 상자", 화물설명 = "시험용 봉인 포장", 화물수량 = 1,
        화물중량Kg = 8m, 화물길이Mm = 400, 화물폭Mm = 300, 화물높이Mm = 200,
        화물바닥회전가능여부 = true, 화물적층가능여부 = true
    };

    internal static 차량추천응답 Response(차량추천요청 request, bool empty = false, string name = "검증용 차량") => new()
    {
        적재조건 = 화물적재입력Policy.계산(화물적재입력Policy.입력생성(request)),
        후보목록 = empty ? [] : [new() { 차량코드 = "fixture-only", 차량종류 = name, 단일운송가능여부 = true }],
        추천사유 = ["입력한 포장 크기·전체 중량 기준의 시험용 응답입니다."]
    };
}
