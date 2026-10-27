using System.Reflection;
using System.Text.Json;
using Ssalddel.Contracts.Shipper.Request;
using Ssalddel.Contracts.Common.Privacy;
using Microsoft.AspNetCore.Components;
using Ssalddel.Client.Infrastructure.Security;
using Ssalddel.Tests.UiCommon;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.ViewModels;
using SsalddelApp.Models.Shipper;
using SsalddelApp.Services;
using SsalddelApp.ViewModels.Shipper;

namespace Ssalddel.Tests.Clients;

public sealed class ShipperRequestSubmissionRecoveryTests
{
    [Fact]
    public async Task 서버저장후응답유실은_GET으로원등록만확인한다()
    {
        var (operations, stub) = Operations();
        stub.Add = _ => { stub.Existing = new() { 의뢰Id = "ledger-one" }; throw new IOException("lost response"); };
        var navigation = new Navigation();
        using var model = new ShipperRequestAuthoringPageViewModel(ShipperRequestContactWindowTests.CompleteState(), operations, navigation,
            recovery: new VolatileWorkflowRecoveryStore());
        await model.InitializeAsync(); await ShipperConsentFixture.ConfirmAsync(model); await model.SubmitAsync();
        Assert.Single(stub.Posts); Assert.Single(stub.Gets);
        Assert.False(model.HasPendingRequest); Assert.Equal(ShipperRoutes.RequestDetailFor("ledger-one"), navigation.LastPath);
    }

    [Fact]
    public async Task 결과미확인중_등록과초기화는새POST를만들지않고_명시재시도도원문을쓴다()
    {
        var (operations, stub) = Operations();
        stub.Add = _ => throw new IOException("lost response");
        var state = ShipperRequestContactWindowTests.CompleteState();
        state.상차연락처전화번호 = "010-1111-2222";
        using var model = new ShipperRequestAuthoringPageViewModel(state, operations, new Navigation(), recovery: new VolatileWorkflowRecoveryStore());
        await model.InitializeAsync(); await ShipperConsentFixture.ConfirmAsync(model); await model.SubmitAsync();
        var key = stub.Posts.Single().의뢰Id;
        state.상차연락처전화번호 = "010-9999-9999";
        await model.SubmitAsync(); await model.ResetAsync();
        Assert.Single(stub.Posts); Assert.True(model.CanRetryPendingRequest);
        stub.Add = _ => Task.FromResult(new ShipperRequestItem { 의뢰Id = "ledger-one" });
        await model.RetryPendingAsync();
        Assert.Equal(2, stub.Posts.Count); Assert.All(stub.Posts, x => Assert.Equal(key, x.의뢰Id));
        Assert.Equal("010-1111-2222", stub.Posts[1].픽업정보!.연락처.전화번호);
    }

    [Fact]
    public async Task 명시재시도도_직전조회가실패하면_보존한POST를재전송하지않는다()
    {
        var (operations, stub) = Operations();
        stub.Add = _ => throw new IOException("lost response");
        using var model = new ShipperRequestAuthoringPageViewModel(ShipperRequestContactWindowTests.CompleteState(), operations,
            new Navigation(), recovery: new VolatileWorkflowRecoveryStore());
        await model.InitializeAsync(); await ShipperConsentFixture.ConfirmAsync(model); await model.SubmitAsync();
        Assert.True(model.CanRetryPendingRequest);
        stub.FailGet = true;
        await model.RetryPendingAsync();
        Assert.Single(stub.Posts);
        Assert.Equal(2, stub.Gets.Count);
        Assert.True(model.HasPendingRequest);
        Assert.False(model.CanRetryPendingRequest);
        Assert.Contains("이전 접수 결과를 확인하지 못했습니다", model.StatusMessage);
    }

    [Fact]
    public async Task 앱재진입은_보호원문을복원하고_GET만하여_같은번호로명시재시도한다()
    {
        var auth = new AuthSession(new ShipperRecoveryTokenStore(), new ClientSessionGuard());
        await auth.ApplyAsync(ShipperRecoveryFixture.Snapshot());
        var store = new VolatileWorkflowRecoveryStore();
        var (operations, stub) = Operations(); stub.Add = _ => throw new IOException("lost response");
        var original = ShipperRequestContactWindowTests.CompleteState(); original.상차상세주소 = "상차장 2";
        original.화물수량 = 7; original.화물중량Kg = 12.5m; original.화물부피Cbm = .48m;
        original.화물길이Mm = 450; original.화물폭Mm = 350; original.화물높이Mm = 250; original.팔레트개수 = 2;
        original.온도조건 = "냉장"; original.화물설명 = "원래 취급 조건";
        original.화물적재형태 = "일반 화물(박스/팔레트)";
        original.결제수단 = "계좌이체"; original.요청사항 = "상차 전 연락";
        original.대기료 = 0m; original.수작업비 = 1500m; original.할증 = null;
        original.알선소Id = "broker-original"; original.절차메모 = "최초 절차";
        using (var first = new ShipperRequestAuthoringPageViewModel(original, operations, new Navigation(), auth, store))
        { await first.InitializeAsync(); await ShipperConsentFixture.ConfirmAsync(first); await first.SubmitAsync(); }
        var key = stub.Posts.Single().의뢰Id;
        var originalPayload = JsonSerializer.Serialize(stub.Posts.Single());
        original.화물수량 = 99; original.화물중량Kg = 99m; original.온도조건 = "냉동";
        original.화물길이Mm = 999; original.팔레트개수 = 99;
        original.요청사항 = "바뀐 요청"; original.대기료 = 999m; original.결제수단 = "카드";
        var restored = new 운송의뢰작성ViewModel();
        using var second = new ShipperRequestAuthoringPageViewModel(restored, operations, new Navigation(), auth, store);
        await second.InitializeAsync();
        Assert.Single(stub.Posts); Assert.True(second.HasPendingRequest); Assert.True(second.CanRetryPendingRequest);
        Assert.Equal("상차장 2", restored.상차상세주소); Assert.True(restored.서버등록가능);
        Assert.Equal(7, restored.화물수량); Assert.Equal(12.5m, restored.화물중량Kg);
        Assert.Equal(450, restored.화물길이Mm); Assert.Equal(350, restored.화물폭Mm);
        Assert.Equal(250, restored.화물높이Mm); Assert.Equal(2, restored.팔레트개수);
        Assert.Equal("냉장", restored.온도조건); Assert.Equal("계좌이체", restored.결제수단);
        stub.Add = _ => Task.FromResult(new ShipperRequestItem { 의뢰Id = "ledger-one" });
        await second.RetryPendingAsync();
        Assert.Equal(key, stub.Posts[1].의뢰Id);
        Assert.Equal(originalPayload, JsonSerializer.Serialize(stub.Posts[1]));
        Assert.Null(await store.LoadAsync("shipper-a", ShipperRequestAuthoringPageViewModel.RecoveryScope, default));
    }

    [Fact]
    public async Task 현재화물추천실패는_초기접수준비와분리하고_입력을유지해재조회한다()
    {
        var (operations, stub) = Operations(); stub.FailVehicles = true;
        var state = ShipperRequestContactWindowTests.CompleteState();
        using var model = new ShipperRequestAuthoringPageViewModel(state, operations, new Navigation());
        await model.InitializeAsync(); Assert.True(model.RegistrationEnabled); Assert.True(state.서버등록가능);
        Assert.Equal(0, stub.VehicleCalls);
        var before = JsonSerializer.Serialize(state.ToDraft());
        await state.차량추천조회Async(model.RecommendVehiclesAsync);
        Assert.NotNull(state.차량추천조회오류); Assert.Empty(state.추천차량종류목록);
        Assert.Equal(before, JsonSerializer.Serialize(state.ToDraft()));
        stub.FailVehicles = false;
        await state.차량추천조회Async(model.RecommendVehiclesAsync);
        Assert.True(model.RegistrationEnabled); Assert.True(state.차량추천현재유효);
        Assert.Equal(2, stub.VehicleCalls); Assert.Single(state.추천차량종류목록);
    }

    [Fact]
    public async Task 동시에열린작성화면은_보호저장의기존접수를덮지않고_그번호로복구한다()
    {
        var store = new VolatileWorkflowRecoveryStore(); var (operations, stub) = Operations();
        var pending = new TaskCompletionSource<ShipperRequestItem>(); stub.Add = _ => pending.Task;
        using var first = new ShipperRequestAuthoringPageViewModel(ShipperRequestContactWindowTests.CompleteState(), operations, new Navigation(), recovery: store);
        using var second = new ShipperRequestAuthoringPageViewModel(ShipperRequestContactWindowTests.CompleteState(), operations, new Navigation(), recovery: store);
        await first.InitializeAsync(); await second.InitializeAsync();
        await ShipperConsentFixture.ConfirmAsync(first); await ShipperConsentFixture.ConfirmAsync(second);
        var submitting = first.SubmitAsync(); var key = stub.Posts.Single().의뢰Id;
        await second.SubmitAsync(); Assert.Single(stub.Posts); Assert.True(second.HasPendingRequest);
        Assert.Equal(key, stub.Gets.Last());
        pending.SetResult(new() { 의뢰Id = "ledger-one" }); await submitting;
    }

    [Fact]
    public async Task 보호저장실패나조회실패는_POST하지않고_불명확한접수를유지한다()
    {
        var (operations, stub) = Operations(); stub.FailGet = true;
        using var model = new ShipperRequestAuthoringPageViewModel(ShipperRequestContactWindowTests.CompleteState(), operations,
            new Navigation(), recovery: new FailingStore());
        await model.InitializeAsync(); await ShipperConsentFixture.ConfirmAsync(model); await model.SubmitAsync(); await model.RetryPendingAsync();
        Assert.Empty(stub.Posts); Assert.True(model.HasPendingRequest); Assert.False(model.CanRetryPendingRequest);
    }

    [Fact]
    public async Task 만료된접수는_번호를유지해조회하지만_원문없는POST로우회하지않는다()
    {
        var store = new VolatileWorkflowRecoveryStore();
        var id = Guid.NewGuid();
        await store.SaveAsync(new("local-session", ShipperRequestAuthoringPageViewModel.RecoveryScope, id, null,
            DateTime.UtcNow.AddDays(-4)), default);
        var (operations, stub) = Operations();
        using var model = new ShipperRequestAuthoringPageViewModel(new(), operations, new Navigation(), recovery: store);
        await model.InitializeAsync(); await model.SubmitAsync(); await model.RetryPendingAsync();
        Assert.True(model.PendingInputExpired); Assert.False(model.CanRetryPendingRequest); Assert.Empty(stub.Posts);
        Assert.All(stub.Gets, x => Assert.Equal($"shipper-app-{id:N}", x));
    }

    [Fact]
    public async Task 계정변경후_늦은등록응답은새계정화면으로이동하지않는다()
    {
        var auth = new AuthSession(new ShipperRecoveryTokenStore(), new ClientSessionGuard());
        await auth.ApplyAsync(ShipperRecoveryFixture.Snapshot());
        var (operations, stub) = Operations(); var pending = new TaskCompletionSource<ShipperRequestItem>();
        stub.Add = _ => pending.Task; var navigation = new Navigation(); var store = new VolatileWorkflowRecoveryStore();
        using var model = new ShipperRequestAuthoringPageViewModel(ShipperRequestContactWindowTests.CompleteState(), operations, navigation, auth, store);
        await model.InitializeAsync(); await ShipperConsentFixture.ConfirmAsync(model); var submit = model.SubmitAsync();
        await auth.ApplyAsync(ShipperRecoveryFixture.Snapshot("shipper-b"));
        pending.SetResult(new() { 의뢰Id = "ledger-one" }); await submit;
        Assert.Null(navigation.LastPath); Assert.False(model.HasPendingRequest); Assert.Empty(model.State.상차도로명주소);
        Assert.NotNull(await store.LoadAsync("shipper-a", ShipperRequestAuthoringPageViewModel.RecoveryScope, default));
        await model.InitializeAsync(); Assert.False(model.HasPendingRequest);
    }

    private static (IShipperOperationsService, OperationsStub) Operations()
    {
        var operations = DispatchProxy.Create<IShipperOperationsService, OperationsStub>();
        return (operations, (OperationsStub)(object)operations);
    }
    public class OperationsStub : DispatchProxy
    {
        public List<ShipperRequestItem> Posts { get; } = [];
        public List<string> Gets { get; } = [];
        public ShipperRequestItem? Existing;
        public bool FailVehicles, FailGet;
        public int VehicleCalls;
        public Func<ShipperRequestItem, Task<ShipperRequestItem>> Add = _ => Task.FromResult(new ShipperRequestItem { 의뢰Id = "ledger-one" });
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method?.Name == nameof(IShipperOperationsService.RecordPrivacyConsentAsync))
                return Task.FromResult<신청개인정보동의증적Response?>(ShipperConsentFixture.Response((신청개인정보동의기록Request)args![0]!));
            if (method?.Name == nameof(IShipperOperationsService.GetVehicleTypesAsync))
            { VehicleCalls++; return FailVehicles ? Task.FromException<IReadOnlyList<string>>(new IOException("vehicles unavailable")) : Task.FromResult<IReadOnlyList<string>>(["1톤 카고"]); }
            if (method?.Name == nameof(IShipperOperationsService.RecommendVehiclesAsync))
            {
                VehicleCalls++;
                var input = (차량추천요청)args![0]!;
                return FailVehicles ? Task.FromException<차량추천응답>(new IOException("vehicles unavailable"))
                    : Task.FromResult(Ssalddel.Tests.UiCommon.ShipperVehicleRecommendationR74Tests.Response(input));
            }
            if (method?.Name == nameof(IShipperOperationsService.AddRequestAsync))
            { var input = (ShipperRequestItem)args![0]!; Posts.Add(input); return Add(input); }
            if (method?.Name == nameof(IShipperOperationsService.GetRequestByClientRequestIdAsync))
            { Gets.Add((string)args![0]!); return FailGet ? Task.FromException<ShipperRequestItem?>(new IOException("result unavailable")) : Task.FromResult(Existing); }
            throw new NotSupportedException();
        }
    }
    private sealed class FailingStore : IWorkflowRecoveryStore
    {
        public Task<WorkflowRecoveryRecord?> LoadAsync(string owner, string scope, CancellationToken ct) => Task.FromResult<WorkflowRecoveryRecord?>(null);
        public Task SaveAsync(WorkflowRecoveryRecord record, CancellationToken ct) => throw new IOException("protected storage unavailable");
        public Task RemoveAsync(string owner, string scope, Guid id, CancellationToken ct) => throw new NotSupportedException();
    }
    private sealed class Navigation : NavigationManager
    {
        public string? LastPath;
        public Navigation() => Initialize("https://test.invalid/", "https://test.invalid/shipper/request/review");
        protected override void NavigateToCore(string uri, bool forceLoad) => LastPath = uri;
        protected override void NavigateToCore(string uri, NavigationOptions options) => LastPath = uri;
    }
}
