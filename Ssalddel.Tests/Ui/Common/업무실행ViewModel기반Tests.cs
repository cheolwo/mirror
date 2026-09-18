using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Ssalddel.Ui.Common.Areas.App.Components;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.ViewModels;
using Ssalddel.Contracts.Common.Workflow;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Ssalddel.Tests.Ui.Common;

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
        var exception = new SsalddelApiException(
            "원장 갱신 충돌",
            409,
            "원장 저장",
            "{}",
            "trace-1",
            new Dictionary<string, string[]> { ["ExpectedRevision"] = ["최신 상태를 다시 조회하세요."] },
            업무실패분류Codes.판본충돌,
            업무실패책임역할Codes.음식배달운영체제,
            requiresStateRefresh: true,
            업무재시도정책Codes.상태재조회후사용자재시도,
            [업무복구행동Ids.상태전체재조회],
            currentRevision: 12);

        var error = Api작업오류.변환(exception);

        Assert.True(error.충돌);
        Assert.Equal(409, error.Http상태코드);
        Assert.Equal("trace-1", error.TraceId);
        Assert.Equal("최신 상태를 다시 조회하세요.", error.필드오류!["ExpectedRevision"].Single());
        Assert.Equal(업무실패분류Codes.판본충돌, error.실패분류Code);
        Assert.True(error.상태전체재조회필요);
        Assert.Equal(12, error.현재Revision);
        Assert.Contains(업무복구행동Ids.상태전체재조회, error.복구가능행동!);
    }

    [Fact]
    public void 통신장애는_동일멱등요청1회복구행동으로정규화한다()
    {
        var error = Api작업오류.변환(new HttpRequestException("연결 실패"));

        Assert.True(error.재시도가능);
        Assert.Equal(업무실패분류Codes.일시기술장애, error.실패분류Code);
        Assert.Equal(업무재시도정책Codes.동일멱등요청1회, error.재시도정책Code);
        Assert.Contains(업무복구행동Ids.동일요청멱등재시도, error.복구가능행동!);
    }

    [Fact]
    public void ApiProblemParser는_서버복구계약을손실없이읽는다()
    {
        const string body = """
            {
              "detail": "판본 충돌",
              "traceId": "trace-2",
              "failureClassCode": "RevisionConflict",
              "responsibilityRoleCode": "FoodDeliveryOS",
              "requiresStateRefresh": true,
              "retryPolicyCode": "AfterStateRefresh",
              "availableRecoveryActions": ["RefreshState"],
              "currentRevision": 9
            }
            """;

        var problem = SsalddelApiProblemParser.Parse(body);

        Assert.Equal("판본 충돌", problem.Message);
        Assert.True(problem.RequiresStateRefresh);
        Assert.Equal(9, problem.CurrentRevision);
        Assert.Contains(업무복구행동Ids.상태전체재조회, problem.AvailableRecoveryActions);
    }

    [Fact]
    public async Task 멱등재시도실행기는_통신장애에만_같은요청으로한번재시도한다()
    {
        var request = new object();
        var observedRequests = new List<object>();

        var result = await 업무멱등재시도실행기.한번Async<string>(_ =>
        {
            observedRequests.Add(request);
            if (observedRequests.Count == 1)
            {
                throw new HttpRequestException("일시 연결 실패");
            }

            return Task.FromResult("완료");
        });

        Assert.Equal("완료", result);
        Assert.Equal(2, observedRequests.Count);
        Assert.Same(observedRequests[0], observedRequests[1]);
    }

    [Fact]
    public async Task 멱등재시도실행기는_업무규칙실패를자동재시도하지않는다()
    {
        var attempts = 0;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            업무멱등재시도실행기.한번Async<string>(_ =>
            {
                attempts++;
                throw new InvalidOperationException("현재 상태에서는 실행할 수 없습니다.");
            }));

        Assert.Equal(1, attempts);
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
