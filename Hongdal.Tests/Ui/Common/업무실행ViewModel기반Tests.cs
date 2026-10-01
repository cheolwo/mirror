using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Hongdal.Ui.Common.Areas.App.Components;
using Hongdal.Ui.Common.Areas.App.Services;
using Hongdal.Ui.Common.Areas.App.ViewModels;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Hongdal.Tests.Ui.Common;

public sealed class 업무실행ViewModel기반Tests
{
    [Fact]
    public void 선택Context는_대상이바뀌면_이전요청을취소하고_늦은응답을거부한다()
    {
        using var context = new 업무선택ContextViewModel();
        context.선택("ledger-a");
        using var first = context.요청시작();

        context.선택("ledger-b");

        Assert.True(first.취소Token.IsCancellationRequested);
        Assert.False(first.현재요청);
        using var second = context.요청시작();
        Assert.True(second.현재요청);
        Assert.Equal("ledger-b", second.대상Key);
    }

    [Fact]
    public async Task Api작업은_사용자취소를_실패가아닌_취소상태로보관한다()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var viewModel = new Api작업ViewModel<Api작업완료>(async token =>
        {
            started.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return Api작업완료.값;
        });

        var execution = viewModel.실행Async();
        await started.Task;
        viewModel.취소();
        await execution;

        Assert.True(viewModel.취소됨);
        Assert.False(viewModel.오류발생);
        Assert.Null(viewModel.오류);
    }

    [Fact]
    public void Api오류는_Revision충돌과_필드오류를_구조적으로보존한다()
    {
        var exception = new HongdalApiException(
            "원장 갱신 충돌",
            409,
            "원장 저장",
            "{}",
            "trace-1",
            new Dictionary<string, string[]> { ["ExpectedRevision"] = ["최신 상태를 다시 조회하세요."] });

        var error = Api작업오류.변환(exception);

        Assert.True(error.충돌);
        Assert.Equal(409, error.Http상태코드);
        Assert.Equal("trace-1", error.TraceId);
        Assert.Equal("최신 상태를 다시 조회하세요.", error.필드오류!["ExpectedRevision"].Single());
    }

    [Fact]
    public void 입력ViewModel은_변경여부와_DataAnnotation검증을_함께관리한다()
    {
        var form = new TestForm();

        form.이름 = string.Empty;
        Assert.False(form.전체검증());
        Assert.True(form.HasErrors);

        form.이름 = "테스트";
        Assert.True(form.전체검증());
        Assert.True(form.변경됨);
        Assert.True(form.저장가능);

        form.변경확정();
        Assert.False(form.변경됨);
        Assert.False(form.저장가능);
    }

    [Fact]
    public void 명령ViewModel은_표현라이브러리와무관한_다이얼로그정책을제공한다()
    {
        I명령ViewModel<object> command = new TestCommand();

        Assert.True(command.실행가능);
        Assert.Equal("테스트 삭제", command.다이얼로그정책.제목);
        Assert.Equal("삭제", command.다이얼로그정책.확인버튼문구);
        Assert.True(command.다이얼로그정책.파괴적명령);
        Assert.True(command.다이얼로그정책.성공시닫기);
    }

    [Fact]
    public void 조립ViewModel은_DI하위수명을소유하지않고_직접생성하위만폐기한다()
    {
        var injected = new TestDisposableViewModel();
        var owned = new TestDisposableViewModel();
        var parent = new TestCompositeViewModel(injected, owned);
        var changeCount = 0;
        parent.PropertyChanged += (_, _) => changeCount++;

        Assert.Same(injected, parent.Injected);
        Assert.Same(owned, parent.Owned);
        injected.RaisePropertyChanged();
        Assert.Equal(1, changeCount);

        parent.Dispose();
        injected.RaisePropertyChanged();

        Assert.False(injected.Disposed);
        Assert.True(owned.Disposed);
        Assert.Equal(1, changeCount);
    }

    [Fact]
    public void MvvmComponent는_현재Scope상태를공유하고_PageViewModel수명만관리한다()
    {
        var viewModel = new TestDisposableViewModel();
        using var services = new ServiceCollection()
            .AddTransient(_ => viewModel)
            .BuildServiceProvider();
        var component = new TestMvvmComponent();

        component.Initialize(services);

        Assert.Equal(1, viewModel.SubscriberCount);
        Assert.False(typeof(OwningComponentBase<TestDisposableViewModel>)
            .IsAssignableFrom(typeof(TestMvvmComponent)));

        component.Dispose();

        Assert.Equal(0, viewModel.SubscriberCount);
        Assert.True(viewModel.Disposed);
    }

    public sealed class TestForm : 업무입력ViewModelBase
    {
        private string _이름 = string.Empty;

        [Required]
        public string 이름
        {
            get => _이름;
            set => 입력값설정(ref _이름, value);
        }
    }

    private sealed class TestCommand()
        : 업무조각ViewModelBase("test-delete", "테스트 삭제", 업무조각유형.삭제), I명령ViewModel<object>
    {
        public object 초안 { get; } = new();

        public Task<bool> 실행Async(CancellationToken cancellationToken = default)
            => Task.FromResult(true);
    }

    private sealed class TestCompositeViewModel : 조립ViewModelBase
    {
        public TestCompositeViewModel(
            TestDisposableViewModel injected,
            TestDisposableViewModel owned)
        {
            Injected = 하위ViewModel등록(injected);
            Owned = 하위ViewModel등록(owned, 수명소유: true);
        }

        public TestDisposableViewModel Injected { get; }
        public TestDisposableViewModel Owned { get; }
    }

    private sealed class TestDisposableViewModel : INotifyPropertyChanged, IDisposable
    {
        private PropertyChangedEventHandler? _propertyChanged;

        public bool Disposed { get; private set; }
        public int SubscriberCount { get; private set; }

        public event PropertyChangedEventHandler? PropertyChanged
        {
            add
            {
                _propertyChanged += value;
                SubscriberCount++;
            }
            remove
            {
                _propertyChanged -= value;
                SubscriberCount--;
            }
        }

        public void RaisePropertyChanged()
            => _propertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Disposed)));

        public void Dispose() => Disposed = true;
    }

    private sealed class TestMvvmComponent : MvvmComponentBase<TestDisposableViewModel>
    {
        public void Initialize(IServiceProvider services)
        {
            Services = services;
            OnInitialized();
        }
    }
}
