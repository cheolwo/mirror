using Ssalddel.Contracts.Shipper.Request;
using Ssalddel.Ui.Common.Areas.App.ViewModels;
using Ssalddel.Ui.Common.Areas.App.ViewModels.Transport;

namespace Ssalddel.Tests.UiCommon;

public sealed class ShipperVehicleRecommendationSessionTests
{
    private const string CancelMessage = "추천 확인이 중단되었습니다. 입력을 유지했으니 다시 확인해 주세요.";
    private const string FailureMessage = "차량 추천을 확인하지 못했습니다. 입력을 유지했으니 다시 확인해 주세요.";

    [Fact]
    public async Task 입력을되돌리면_기존추천과선택을_현재지문으로다시판정한다()
    {
        var state = Cargo();
        await Recommend(state);
        state.화물기반추천적용();
        var candidate = Assert.Single(state.서버추천후보);
        var originalHeight = state.화물높이Mm;

        state.화물높이Mm++;
        Assert.False(state.차량추천현재유효);
        Assert.True(state.차량추천조건변경됨);
        Assert.True(state.적용차량재확인필요);
        Assert.Empty(state.서버추천후보);

        state.화물높이Mm = originalHeight;
        Assert.True(state.차량추천현재유효);
        Assert.False(state.차량추천조건변경됨);
        Assert.False(state.적용차량재확인필요);
        Assert.Same(candidate, Assert.Single(state.서버추천후보));

        state.차량종류 = "직접 바꾼 선택";
        Assert.True(state.적용차량재확인필요);
        state.차량종류 = candidate.차량종류;
        Assert.False(state.적용차량재확인필요);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 사전취소도_provider를호출하며_성공폐기와취소예외문구를구분한다(bool throwsCancellation)
    {
        var state = Cargo();
        var token = new CancellationToken(true);
        var calls = 0;

        await state.차량추천조회Async((request, actualToken) =>
        {
            calls++;
            Assert.Equal(token, actualToken);
            return throwsCancellation
                ? Task.FromCanceled<차량추천응답>(actualToken)
                : Task.FromResult(Response(request));
        }, token);

        Assert.Equal(1, calls);
        Assert.False(state.차량추천조회중);
        Assert.False(state.차량추천현재유효);
        Assert.Equal(throwsCancellation ? CancelMessage : null, state.차량추천조회오류);
    }

    [Fact]
    public async Task 대기중취소뒤성공한응답은_중단문구없이폐기한다()
    {
        var state = Cargo();
        var request = state.차량추천입력생성();
        using var cancellation = new CancellationTokenSource();
        var reply = Reply();
        var running = state.차량추천조회Async((_, _) => reply.Task, cancellation.Token);

        cancellation.Cancel();
        reply.SetResult(Response(request));
        await running;

        Assert.False(state.차량추천조회중);
        Assert.False(state.차량추천현재유효);
        Assert.Null(state.차량추천조회오류);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task 실패문구는_실패시점의현재지문을다시읽고_취소예외종류를구분한다(bool cancellation, bool changedInput)
    {
        var state = Cargo();
        var reply = Reply();
        // Token 취소 여부와 별개로 provider가 던진 예외 종류를 유지합니다.
        var running = state.차량추천조회Async((_, _) => reply.Task);
        if (changedInput) state.화물중량Kg++;

        reply.SetException(cancellation ? new OperationCanceledException() : new IOException("private fixture details"));
        await running;

        Assert.False(state.차량추천조회중);
        Assert.Equal(changedInput ? null : cancellation ? CancelMessage : FailureMessage, state.차량추천조회오류);
        Assert.Empty(state.추천차량종류목록);
    }

    [Fact]
    public async Task nullProvider는_진행중조회와선택출처를초기화하지않고_Task로실패한다()
    {
        var state = Cargo();
        await Recommend(state);
        state.화물기반추천적용();
        var request = state.차량추천입력생성();
        var reply = Reply();
        var running = state.차량추천조회Async((_, _) => reply.Task);

        Task? rejected = null;
        Assert.Null(Record.Exception(() => { rejected = state.차량추천조회Async(null!); }));
        var failure = await Assert.ThrowsAsync<ArgumentNullException>(() => rejected!);
        Assert.Equal("provider", failure.ParamName);
        Assert.True(state.차량추천조회중);
        Assert.True(state.ToDraft().추천차량재확인필요);

        reply.SetResult(Response(request));
        await running;
        Assert.True(state.차량추천현재유효);
        Assert.False(state.적용차량재확인필요);
    }

    [Fact]
    public async Task 이전조회는_자동취소하지않으며_이전finally가최신조회중상태를지우지않는다()
    {
        var state = Cargo();
        var request = state.차량추천입력생성();
        using var oldCancellation = new CancellationTokenSource();
        using var latestCancellation = new CancellationTokenSource();
        var oldReply = Reply();
        var latestReply = Reply();
        var old = state.차량추천조회Async((_, token) =>
        {
            Assert.Equal(oldCancellation.Token, token);
            return oldReply.Task;
        }, oldCancellation.Token);
        var latest = state.차량추천조회Async((_, token) =>
        {
            Assert.Equal(latestCancellation.Token, token);
            return latestReply.Task;
        }, latestCancellation.Token);

        Assert.False(oldCancellation.IsCancellationRequested);
        oldReply.SetResult(Response(request, "이전 차량"));
        await old;
        Assert.True(state.차량추천조회중);
        Assert.Empty(state.추천차량종류목록);

        latestReply.SetResult(Response(request, "최신 차량"));
        await latest;
        Assert.False(state.차량추천조회중);
        Assert.Equal(["최신 차량"], state.추천차량종류목록);
    }

    [Fact]
    public async Task 접수복사는_같은응답참조를보존하며_조회와초기화수명은서로독립한다()
    {
        var source = Cargo();
        await Recommend(source);
        source.화물기반추천적용();
        var frozen = source.접수입력고정복사();
        Assert.Same(Assert.Single(source.서버추천후보), Assert.Single(frozen.서버추천후보));

        var request = source.차량추천입력생성();
        var reply = Reply();
        var running = source.차량추천조회Async((_, _) => reply.Task);
        Assert.True(source.차량추천조회중);
        Assert.True(frozen.차량추천현재유효);
        Assert.False(frozen.차량추천조회중);

        frozen.Reset();
        Assert.True(source.차량추천조회중);
        reply.SetResult(Response(request));
        await running;
        Assert.True(source.차량추천현재유효);
        Assert.False(frozen.차량추천현재유효);

        var secondFrozen = source.접수입력고정복사();
        source.Reset();
        Assert.True(secondFrozen.차량추천현재유효);
        Assert.False(secondFrozen.적용차량재확인필요);
        Assert.Equal("검증용 차량", secondFrozen.차량종류);
    }

    [Fact]
    public async Task 고정복사의조건변경과새선택은_원본선택을교체하지않는다()
    {
        var source = Cargo();
        await Recommend(source);
        source.화물기반추천적용();
        var frozen = source.접수입력고정복사();

        frozen.화물수량++;
        await Recommend(frozen, "복사본 차량");
        frozen.화물기반추천적용();

        Assert.Equal("복사본 차량", frozen.차량종류);
        Assert.False(frozen.적용차량재확인필요);
        Assert.Equal("검증용 차량", source.차량종류);
        Assert.Equal(["검증용 차량"], source.추천차량종류목록);
        Assert.False(source.적용차량재확인필요);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 미선택이나임의선택변경은_접수복사에추천응답을승계하지않는다(bool manuallyChanged)
    {
        var source = Cargo();
        await Recommend(source);
        if (manuallyChanged)
        {
            source.화물기반추천적용();
            source.차량종류 = "후보 밖 직접 선택";
        }

        var frozen = source.접수입력고정복사();

        Assert.False(frozen.차량추천현재유효);
        Assert.Empty(frozen.서버추천후보);
        Assert.Equal(manuallyChanged, frozen.적용차량재확인필요);
        Assert.Equal(source.차량종류, frozen.차량종류);
    }

    [Fact]
    public async Task 복원초안의false출처는_단순추천초기화가보존한선택출처를대체한다()
    {
        var state = Cargo();
        await Recommend(state);
        state.화물기반추천적용();
        state.차량추천초기화();
        Assert.True(state.적용차량재확인필요);
        var draft = state.ToDraft();
        draft.추천차량재확인필요 = false;

        state.ApplyDraft(draft);

        Assert.False(state.차량추천현재유효);
        Assert.False(state.적용차량재확인필요);
        Assert.False(state.ToDraft().추천차량재확인필요);
        Assert.Equal("검증용 차량", state.차량종류);
        state.Reset();
        Assert.Null(state.차량종류);
    }

    [Fact]
    public async Task null초안도_입력을읽다실패하기전에_기존추천요청을무효화한다()
    {
        var state = Cargo();
        await Recommend(state);
        state.화물기반추천적용();
        var request = state.차량추천입력생성();
        var reply = Reply();
        var running = state.차량추천조회Async((_, _) => reply.Task);

        Assert.Throws<NullReferenceException>(() => state.ApplyDraft(null!));
        Assert.False(state.차량추천조회중);
        Assert.True(state.적용차량재확인필요);
        reply.SetResult(Response(request));
        await running;

        Assert.False(state.차량추천현재유효);
        Assert.Empty(state.추천차량종류목록);
        Assert.Equal("검증용 차량", state.차량종류);
    }

    [Fact]
    public async Task 내부Session도_await후와속성조회때마다_현재입력delegate를읽는다()
    {
        var state = Cargo();
        var request = state.차량추천입력생성();
        var fingerprint = state.적재입력.입력지문;
        var currentFingerprint = fingerprint;
        var ready = true;
        string? vehicle = null;
        var session = new 차량추천조회Session(() => currentFingerprint, () => ready,
            () => request, () => vehicle, selected => vehicle = selected);
        var reply = Reply();
        var running = session.조회Async((_, _) => reply.Task);

        currentFingerprint = "changed-while-awaiting";
        reply.SetResult(Response(request));
        await running;
        Assert.False(session.차량추천현재유효);

        currentFingerprint = fingerprint;
        await session.조회Async((_, _) => Task.FromResult(Response(request)));
        Assert.True(session.서버추천차량선택("검증용 차량"));
        currentFingerprint = "changed-after-selection";
        Assert.True(session.적용차량재확인필요);
        currentFingerprint = fingerprint;
        Assert.False(session.적용차량재확인필요);

        ready = false;
        await session.조회Async((_, _) => throw new InvalidOperationException("미완성 입력에서 provider를 호출했습니다."));
        Assert.Equal("전체 중량·포장 크기와 회전·적층 조건을 먼저 확인해 주세요.", session.차량추천조회오류);
        Assert.False(session.차량추천조회중);
    }

    private static 운송의뢰작성ViewModel Cargo() => ShipperVehicleRecommendationR74Tests.Cargo();
    private static 차량추천응답 Response(차량추천요청 request, string name = "검증용 차량")
        => ShipperVehicleRecommendationR74Tests.Response(request, name: name);
    private static Task Recommend(운송의뢰작성ViewModel state, string name = "검증용 차량")
        => state.차량추천조회Async((request, _) => Task.FromResult(Response(request, name)));
    private static TaskCompletionSource<차량추천응답> Reply() => new(TaskCreationOptions.RunContinuationsAsynchronously);
}
