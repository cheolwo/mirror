using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Completion;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.DistributedFulfillment;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;

namespace Ssalddel.Tests.Ui.Common;

public sealed class WorkflowCompletionFeedbackTests
{
    private static WorkflowCompletionNotice Notice(string key = "step-a", bool final = false) => new(key,
        final ? "전달을 완료했습니다" : "피킹을 완료했습니다", "저장한 결과를 확인했습니다.", "다음 업무를 확인해 주세요.", final,
        WorkflowCompletionVisuals.PickingIconPath, "#0F766E");

    [Fact] public void 안내확인과_모드변경은_이미확인한_단계를_재통지하지않는다()
    {
        var state = new WorkflowCompletionFeedback();
        Assert.True(state.Record(Notice())); Assert.NotNull(state.Current);
        state.Dismiss(); Assert.Null(state.Current); Assert.False(state.Record(Notice()));
        state.Mode = WorkflowCompletionDisplayMode.Brief;
        Assert.False(state.Record(Notice())); Assert.Single(state.History);
        Assert.True(state.Record(Notice("step-b"))); Assert.NotNull(state.Current);
        state.Mode = WorkflowCompletionDisplayMode.EveryStep; Assert.Null(state.Current);
    }
    [Fact] public void 최종전달만은_중간완료를_기록하되_자동창을_만들지않는다()
    {
        var state = new WorkflowCompletionFeedback { Mode = WorkflowCompletionDisplayMode.FinalOnly };
        state.Record(Notice()); Assert.Null(state.Current); Assert.Single(state.History);
        state.Record(Notice("delivery", true)); Assert.True(state.Current!.IsFinal);
    }
    [Fact] public void 최근안내는_열개지만_이전에_닫은결과도_중복통지하지않는다()
    {
        var state = new WorkflowCompletionFeedback();
        for (var i = 0; i < 12; i++) state.Record(Notice("step-" + i));
        Assert.Equal(10, state.History.Count); Assert.False(state.Record(Notice("step-0")));
        state.Clear(); Assert.Null(state.Current); Assert.Empty(state.History);
    }
    [Fact] public void 알수없는_표시모드는_사용하지않는다()
    { var state = new WorkflowCompletionFeedback(); Assert.Throws<ArgumentOutOfRangeException>(() => state.Mode = (WorkflowCompletionDisplayMode)99); }

    [Fact] public async Task 직접피킹은_사진업로드가아닌_영수증과_정본확인후_알린다()
    {
        var transport = DistributedUiTransport.Round();
        using var model = new 분산이행PageViewModel(new(transport, "cargo-driver"), "round", new Test업무사진촬영Adapter());
        await model.LoadAsync("wave-a"); Assert.Empty(model.CompletionFeedback.History);
        await model.CapturePhotoAsync(DistributedPhotoWorkflowTests.PickWork(), "피킹", "pick", "waves/wave-a/pick-batches", ["pick"], 1);
        Assert.Empty(model.CompletionFeedback.History);
        await model.CompletePhotoAsync(true);
        Assert.Equal("피킹을 완료했습니다", model.CompletionFeedback.Current?.Title);
        Assert.False(model.CompletionFeedback.Current!.IsFinal);
        model.CompletionFeedback.Dismiss();
        await model.LoadAsync("wave-a"); Assert.Null(model.CompletionFeedback.Current); Assert.Single(model.CompletionFeedback.History);
        transport.SetIdentity(new("another", 2, true)); Assert.Empty(model.CompletionFeedback.History);
    }
    [Fact] public async Task 직접피킹의_응답유실은_원영수증_재확인후_한번만_알린다()
    {
        var transport = DistributedUiTransport.Round(); var lost = false;
        transport.AfterPost = _ => { if (!lost) { lost = true; throw new HttpRequestException("lost response"); } };
        using var model = new 분산이행PageViewModel(new(transport, "cargo-driver"), "round", new Test업무사진촬영Adapter());
        await model.LoadAsync("wave-a");
        await model.CapturePhotoAsync(DistributedPhotoWorkflowTests.PickWork(), "피킹", "pick", "waves/wave-a/pick-batches", ["pick"], 1);
        await model.CompletePhotoAsync(true);
        Assert.True(model.HasPendingCommand); Assert.Empty(model.CompletionFeedback.History);
        await model.RetryPendingAsync();
        Assert.Single(transport.Posts); Assert.Single(model.CompletionFeedback.History); Assert.NotNull(model.CompletionFeedback.Current);
        await model.RetryPendingAsync(); Assert.Single(model.CompletionFeedback.History);
    }
}
