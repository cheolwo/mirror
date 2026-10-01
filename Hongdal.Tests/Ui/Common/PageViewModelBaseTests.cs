using Hongdal.Ui.Common.Areas.App.ViewModels;

namespace Hongdal.Tests.Ui.Common;

public sealed class PageViewModelBaseTests
{
    [Fact]
    public async Task 초기화는_성공후중복실행하지않고_새로고침은다시실행한다()
    {
        using var viewModel = new TestPageViewModel();

        Assert.True(await viewModel.초기화Async());
        Assert.True(await viewModel.초기화Async());
        Assert.Equal(1, viewModel.실행횟수);
        Assert.True(viewModel.초기화됨);
        Assert.Null(viewModel.오류메시지);

        Assert.True(await viewModel.새로고침Async());
        Assert.Equal(2, viewModel.실행횟수);
        Assert.True(viewModel.마지막실행은새로고침);
    }

    [Fact]
    public async Task 취소는_오류가아닌취소상태로정리한다()
    {
        using var viewModel = new TestPageViewModel(waitUntilCanceled: true);

        var loading = viewModel.초기화Async();
        await viewModel.시작됨.Task;
        viewModel.취소();

        Assert.False(await loading);
        Assert.Equal(PageViewModel상태.취소됨, viewModel.상태);
        Assert.Null(viewModel.오류메시지);
    }

    [Fact]
    public async Task 예외는_실패상태와오류메시지로보존한다()
    {
        using var viewModel = new TestPageViewModel(errorMessage: "페이지 조회 실패");

        Assert.False(await viewModel.초기화Async());
        Assert.Equal(PageViewModel상태.실패, viewModel.상태);
        Assert.Equal("페이지 조회 실패", viewModel.오류메시지);
    }

    private sealed class TestPageViewModel(
        bool waitUntilCanceled = false,
        string? errorMessage = null) : PageViewModelBase
    {
        public TaskCompletionSource 시작됨 { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public int 실행횟수 { get; private set; }
        public bool 마지막실행은새로고침 { get; private set; }

        protected override async Task 불러오기Async(
            bool 새로고침,
            CancellationToken cancellationToken)
        {
            실행횟수++;
            마지막실행은새로고침 = 새로고침;
            시작됨.TrySetResult();

            if (!string.IsNullOrWhiteSpace(errorMessage))
            {
                throw new InvalidOperationException(errorMessage);
            }

            if (waitUntilCanceled)
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
        }
    }
}
