using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Options;
using Ssalddel.Client.Infrastructure;
using Ssalddel.Client.Infrastructure.Security;
using Ssalddel.Client.Infrastructure.Transport;
using Ssalddel.Contracts.Common.Privacy;
using Ssalddel.Contracts.Shipper.Request;
using Ssalddel.Tests.UiCommon;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.ViewModels;
using SsalddelApp.Models.Shipper;
using SsalddelApp.Services;
using SsalddelApp.ViewModels.Shipper;

namespace Ssalddel.Tests.Clients;

public sealed class ShipperRestartRecoveryTests
{
    [Fact]
    public async Task 전송전입력은새저장소와VM에서도복원하되_동의와POST는복원하지않는다()
    {
        using var f = await Fixture.Create();
        var state = ShipperRequestContactWindowTests.CompleteState();
        state.상차상세주소 = "합성 상차 위치"; state.상차연락처전화번호 = "010-0000-0001";
        state.화물수량 = 8; state.요청사항 = "합성 취급 조건";
        using (var first = f.Authoring(state))
        { await first.InitializeAsync(); await ShipperConsentFixture.ConfirmAsync(first); await first.AutoSaveAsync(); }
        var restored = new 운송의뢰작성ViewModel();
        using var second = f.Authoring(restored); await second.InitializeAsync();
        Assert.Equal("합성 상차 위치", restored.상차상세주소);
        Assert.Equal("010-0000-0001", restored.상차연락처전화번호);
        Assert.Equal(8, restored.화물수량); Assert.Equal("합성 취급 조건", restored.요청사항);
        Assert.False(second.CollectionConsent); Assert.False(second.AgeConfirmed); Assert.Null(second.PrivacyConsentEvidenceId);
        Assert.Empty(f.Stub.RequestPosts); Assert.Empty(f.Stub.RequestGets);
        Assert.Contains("복원", second.StatusMessage);
    }

    [Fact]
    public async Task 초기화는영속초안도정리하며_다른계정의입력은복원하지않는다()
    {
        using var f = await Fixture.Create();
        using (var first = f.Authoring(ShipperRequestContactWindowTests.CompleteState()))
        { await first.InitializeAsync(); await first.SaveAsync(); }
        await f.Auth.ApplyAsync(ShipperRecoveryFixture.Snapshot("shipper-b"));
        using (var another = f.Authoring(new()))
        { await another.InitializeAsync(); Assert.Empty(another.State.상차도로명주소); }
        await f.Auth.ApplyAsync(ShipperRecoveryFixture.Snapshot());
        using (var restored = f.Authoring(new()))
        { await restored.InitializeAsync(); Assert.NotEmpty(restored.State.상차도로명주소); await restored.ResetAsync(); }
        using var final = f.Authoring(new()); await final.InitializeAsync();
        Assert.Empty(final.State.상차도로명주소); Assert.Empty(f.Stub.RequestPosts);
    }

    [Fact]
    public async Task 다른서버에서는같은계정의초안이나미확정접수를조회전송하지않는다()
    {
        using var f = await Fixture.Create();
        f.Stub.FailRequestPost = true;
        using (var first = f.Authoring(ShipperRequestContactWindowTests.CompleteState()))
        { await first.InitializeAsync(); await first.SaveAsync(); await ShipperConsentFixture.ConfirmAsync(first); await first.SubmitAsync(); }
        Assert.Single(f.Stub.RequestPosts); var reads = f.Stub.RequestGets.Count;
        f.Endpoint = new("https://another.invalid/api/");
        using var second = f.Authoring(new()); await second.InitializeAsync();
        Assert.Empty(second.State.상차도로명주소); Assert.False(second.HasPendingRequest);
        Assert.Equal(reads, f.Stub.RequestGets.Count); Assert.Single(f.Stub.RequestPosts);
    }

    [Fact]
    public async Task 서버정보없는이전접수는보존하고_새서버로자동조회하거나재전송하지않는다()
    {
        using var f = await Fixture.Create();
        var store = f.Store();
        await store.SaveAsync(new("shipper-a", ShipperRequestAuthoringPageViewModel.RecoveryScope,
            Guid.NewGuid(), null, DateTime.UtcNow), default);
        using var model = f.Authoring(new()); await model.InitializeAsync();
        await model.SubmitAsync(); await model.RecoverPendingAsync(); await model.RetryPendingAsync();
        Assert.True(model.HasPendingRequest); Assert.Empty(f.Stub.RequestPosts); Assert.Empty(f.Stub.RequestGets);
        Assert.Contains("서버", model.StatusMessage);
    }

    [Fact]
    public async Task 인수증응답유실과재실행후미등록GET은_중복전송을허용하지않는다()
    {
        using var f = await Fixture.Create(); f.Stub.FailReceiptPost = true;
        using (var first = f.Detail())
        {
            await first.InitializeReceiptAsync("request-a"); Confirm(first); await first.RegisterReceiptAsync();
            Assert.True(first.ReceiptNeedsResultCheck); Assert.False(first.State.IsBusy);
        }
        using var second = f.Detail(); await second.InitializeReceiptAsync("request-a");
        Assert.True(second.ReceiptNeedsResultCheck); Assert.True(second.ReceiptInputLocked);
        Confirm(second); await second.RegisterReceiptAsync(); await second.ConfirmReceiptResultAsync();
        Assert.Equal(1, f.Stub.ReceiptPosts); Assert.True(second.ReceiptNeedsResultCheck);
        var record = f.Storage.Values.Select(x => JsonSerializer.Deserialize<WorkflowRecoveryRecord>(x)!).Single();
        Assert.Null(record.PayloadJson); Assert.Equal("request-a", record.Target);
        Assert.DoesNotContain("receipt-number", f.Storage.Values.Single());
        Assert.DoesNotContain("합성 검수 메모", f.Storage.Values.Single());
    }

    [Fact]
    public async Task 인수증재실행후정본등록만확인되면_이번요청성공으로추측하지않고보호를정리한다()
    {
        using var f = await Fixture.Create(); f.Stub.FailReceiptPost = true;
        using (var first = f.Detail())
        { await first.InitializeReceiptAsync("request-a"); Confirm(first); await first.RegisterReceiptAsync(); }
        f.Stub.Current.인수증번호 = "receipt-number"; f.Stub.Current.인수증등록일시 = DateTime.UtcNow;
        using var second = f.Detail(); await second.InitializeReceiptAsync("request-a");
        Assert.False(second.ReceiptNeedsResultCheck); Assert.Contains("일치 여부 미확인", second.State.StatusMessage);
        Assert.Equal(1, f.Stub.ReceiptPosts); Assert.Empty(f.Storage); Assert.False(second.CanRegisterReceipt);
    }

    [Fact]
    public async Task 인수증보호저장실패는전송을막고_다른계정재실행은기존기록을노출하지않는다()
    {
        using var f = await Fixture.Create();
        f.FailWrites = true;
        using (var first = f.Detail())
        { await first.InitializeReceiptAsync("request-a"); Confirm(first); await first.RegisterReceiptAsync(); Assert.False(first.CanRegisterReceipt); }
        Assert.Equal(0, f.Stub.ReceiptPosts);
        f.FailWrites = false; f.Stub.FailReceiptPost = true;
        using (var first = f.Detail())
        { await first.InitializeReceiptAsync("request-a"); Confirm(first); await first.RegisterReceiptAsync(); }
        Assert.Single(f.Storage);
        await f.Auth.ApplyAsync(ShipperRecoveryFixture.Snapshot("shipper-b"));
        using var second = f.Detail(); await second.InitializeReceiptAsync("request-a");
        Assert.False(second.ReceiptNeedsResultCheck); Assert.Empty(second.ReceiptNumber); Assert.Single(f.Storage);
    }

    [Fact]
    public async Task 쓰기가조용히실패해도_접수는POST하지않고_초안은복원가능하다고표시하지않는다()
    {
        using var f = await Fixture.Create(); f.NoOpWrites = true;
        using var model = f.Authoring(ShipperRequestContactWindowTests.CompleteState());
        await model.InitializeAsync(); await model.SaveAsync();
        Assert.DoesNotContain("저장했습니다", model.StatusMessage);
        Assert.Contains("저장하지 못했습니다", model.StatusMessage); Assert.Empty(f.Storage);
        await ShipperConsentFixture.ConfirmAsync(model); await model.SubmitAsync();
        Assert.Empty(f.Stub.RequestPosts); Assert.True(model.HasPendingRequest); Assert.Empty(f.Storage);
    }

    [Fact]
    public async Task 인수증조회뒤서버가바뀌면_다시조회와검수확인전등록할수없다()
    {
        using var f = await Fixture.Create(); using var model = f.Detail();
        await model.InitializeReceiptAsync("request-a"); Confirm(model); Assert.True(model.CanRegisterReceipt);
        f.Endpoint = new("https://another.invalid/api/");
        Assert.True(model.ReceiptInputLocked); Assert.False(model.CanRegisterReceipt);
        await model.RegisterReceiptAsync(); Assert.Equal(0, f.Stub.ReceiptPosts);
        await model.InitializeReceiptAsync("request-a");
        Assert.Empty(model.ReceiptNumber); Assert.Empty(model.ReceiptMemo); Assert.False(model.ReceiptAcceptanceConfirmed);
        Assert.False(model.CanRegisterReceipt); Confirm(model); Assert.True(model.CanRegisterReceipt);
    }

    [Fact]
    public async Task 인수증조회도중서버가바뀌면_늦은A서버정본을B서버입력으로사용하지않는다()
    {
        using var f = await Fixture.Create();
        var response = new TaskCompletionSource<ShipperRequestItem?>(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Stub.ReceiptRead = () => response.Task;
        using var model = f.Detail(); var running = model.InitializeReceiptAsync("request-a");
        f.Endpoint = new("https://another.invalid/api/"); response.SetResult(f.Stub.Current); await running;
        Assert.Null(model.State.Request); Assert.False(model.CanRegisterReceipt); Assert.Empty(model.ReceiptNumber);
        Assert.Contains("서버", model.State.StatusMessage); await model.RegisterReceiptAsync(); Assert.Equal(0, f.Stub.ReceiptPosts);
    }

    private static void Confirm(ShipperRequestDetailPageViewModel model)
    { model.SetReceiptNumber("receipt-number"); model.SetReceiptMemo("합성 검수 메모"); model.SetReceiptAcceptanceConfirmed(true); }

    private sealed class Fixture : IDisposable
    {
        public AuthSession Auth { get; } = new(new ShipperRecoveryTokenStore(), new ClientSessionGuard());
        public Dictionary<string, string> Storage { get; } = [];
        public Uri Endpoint = new("https://one.invalid/api/");
        public bool FailWrites, NoOpWrites;
        public IShipperOperationsService Operations { get; }
        public Stub Stub { get; }
        private readonly HttpClient http = new() { BaseAddress = new("https://one.invalid/api/") };
        public Fixture() { Operations = DispatchProxy.Create<IShipperOperationsService, Stub>(); Stub = (Stub)(object)Operations; }
        public static async Task<Fixture> Create() { var f = new Fixture(); await f.Auth.ApplyAsync(ShipperRecoveryFixture.Snapshot()); return f; }
        public IWorkflowRecoveryStore Store() => new MauiWorkflowRecoveryStore(Auth,
            key => Task.FromResult(Storage.GetValueOrDefault(key)),
            (key, value) => { if (FailWrites) throw new IOException(); if (!NoOpWrites) Storage[key] = value; return Task.CompletedTask; },
            key => Storage.Remove(key));
        private IWorkflowRecoveryContext Context() => new WorkflowRecoveryContext(
            () => new RoleWorkspaceIdentity(Auth.UserId, Auth.SessionRevision, Auth.IsLoggedIn), () => Endpoint);
        public ShipperRequestAuthoringPageViewModel Authoring(운송의뢰작성ViewModel state)
            => new(state, Operations, new Navigation(), Auth, Store(), Context());
        public ShipperRequestDetailPageViewModel Detail()
        {
            var options = Options.Create(new ClientDataModeOptions());
            return new(Operations, new TransportRequestLedgerObserver(), new FakeShipperPaymentService(http, Auth, options),
                options, Auth, Store(), Context());
        }
        public void Dispose() => http.Dispose();
    }
    public class Stub : DispatchProxy
    {
        public List<ShipperRequestItem> RequestPosts { get; } = [];
        public List<string> RequestGets { get; } = [];
        public int ReceiptPosts;
        public bool FailRequestPost, FailReceiptPost;
        public Func<Task<ShipperRequestItem?>>? ReceiptRead;
        public ShipperRequestItem Current = new() { 의뢰Id = "request-a", 운송상태 = "인수완료", 정산상태 = "입금대기" };
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            switch (method?.Name)
            {
                case nameof(IShipperOperationsService.RecordPrivacyConsentAsync):
                    return Task.FromResult<신청개인정보동의증적Response?>(ShipperConsentFixture.Response((신청개인정보동의기록Request)args![0]!));
                case nameof(IShipperOperationsService.AddRequestAsync):
                    RequestPosts.Add((ShipperRequestItem)args![0]!);
                    return FailRequestPost ? Task.FromException<ShipperRequestItem>(new IOException()) : Task.FromResult(new ShipperRequestItem { 의뢰Id = "created-a" });
                case nameof(IShipperOperationsService.GetRequestByClientRequestIdAsync):
                    RequestGets.Add((string)args![0]!); return Task.FromResult<ShipperRequestItem?>(null);
                case nameof(IShipperOperationsService.GetRequestForReceiptAsync):
                    return ReceiptRead?.Invoke() ?? Task.FromResult<ShipperRequestItem?>(Current);
                case nameof(IShipperOperationsService.RegisterReceiptAsync):
                    ReceiptPosts++; return FailReceiptPost ? Task.FromException(new IOException()) : Task.CompletedTask;
                default: throw new NotSupportedException(method?.Name);
            }
        }
    }
    private sealed class Navigation : NavigationManager
    {
        public Navigation() => Initialize("https://one.invalid/", "https://one.invalid/shipper/request/review");
        protected override void NavigateToCore(string uri, bool forceLoad) { }
        protected override void NavigateToCore(string uri, NavigationOptions options) { }
    }
}
