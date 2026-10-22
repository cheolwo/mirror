using Ssalddel.Contracts.Shipper.Payment;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.ViewModels;
using Xunit;

namespace Ssalddel.Tests.Ui.Common;

public sealed class ShipperPrepaymentR16Tests
{
    [Fact]
    public async Task New_request_waits_for_payment_and_verifies_canonical_result()
    {
        var f = new Fixture(); using var vm = f.Model();
        await vm.LoadAsync("request-1"); Assert.True(vm.CanPay);
        await vm.ConfirmSimulationAsync();
        Assert.True(vm.State!.PaymentConfirmed); Assert.False(vm.CanPay); Assert.False(vm.NeedsResultCheck);
        Assert.Equal(1, f.Api.Posts); Assert.Equal(2, f.Api.Reads);
        Assert.Equal(12000, f.Api.Body!.Amount); Assert.Equal("request-1", f.Api.Body.의뢰Id);
        Assert.Null(await f.Store.LoadAsync("owner", f.Scope, default));
        Assert.Contains("실제 결제 없음", vm.Message);
    }

    [Fact]
    public async Task Timeout_keeps_original_observation_across_restart_and_checks_without_reposting()
    {
        var f = new Fixture(); f.Api.PostFailure = new TaskCanceledException("timeout");
        using (var first = f.Model())
        {
            await first.LoadAsync("request-1"); await first.ConfirmSimulationAsync();
            Assert.True(first.NeedsResultCheck); Assert.False(first.CanPay);
            await first.ConfirmSimulationAsync(); Assert.Equal(1, f.Api.Posts);
        }
        using var next = f.Model(); await next.LoadAsync("request-1");
        Assert.True(next.NeedsResultCheck); Assert.False(next.CanPay);
        f.Api.Paid = true; await next.LoadAsync("request-1");
        Assert.False(next.NeedsResultCheck); Assert.True(next.State!.PaymentConfirmed); Assert.Equal(1, f.Api.Posts);
    }

    [Fact]
    public async Task Confirmation_response_is_not_a_substitute_for_canonical_payment_record()
    {
        var f = new Fixture(); f.Api.SetPaidOnPost = false; using var vm = f.Model();
        await vm.LoadAsync("request-1"); await vm.ConfirmSimulationAsync();
        Assert.False(vm.State!.PaymentConfirmed); Assert.True(vm.NeedsResultCheck); Assert.False(vm.CanPay);
    }

    [Fact]
    public async Task Explicit_post_rejection_requires_fresh_lookup_before_another_attempt()
    {
        var f = new Fixture(); f.Api.PostFailure = new RoleWorkspaceAccessException(409, "changed");
        using var vm = f.Model(); await vm.LoadAsync("request-1"); await vm.ConfirmSimulationAsync();
        Assert.False(vm.NeedsResultCheck); Assert.False(vm.CanPay); Assert.Null(vm.State);
        f.Api.PostFailure = null; await vm.LoadAsync("request-1");
        Assert.True(vm.CanPay);
    }

    [Fact]
    public async Task Failed_get_after_successful_post_does_not_clear_payment_protection()
    {
        var f = new Fixture(); f.Api.FailReadAfterPost = true;
        using var vm = f.Model(); await vm.LoadAsync("request-1"); await vm.ConfirmSimulationAsync();
        Assert.True(vm.NeedsResultCheck); Assert.False(vm.CanPay); Assert.Equal(1, f.Api.Posts);
        Assert.NotNull(await f.Store.LoadAsync("owner", f.Scope, default));
    }

    [Fact]
    public async Task Failed_protection_save_prevents_payment_request()
    {
        var f = new Fixture(); using var vm = f.Model(new FailingStore());
        await vm.LoadAsync("request-1"); await vm.ConfirmSimulationAsync();
        Assert.Equal(0, f.Api.Posts); Assert.Contains("시작하지 못", vm.Message);
    }

    [Fact]
    public async Task Missing_existing_protection_is_not_proof_that_payment_was_never_sent()
    {
        var f = new Fixture(); var store = new DroppingStore(); f.Api.PostFailure = new TaskCanceledException();
        using var vm = f.Model(store); await vm.LoadAsync("request-1"); await vm.ConfirmSimulationAsync();
        Assert.True(vm.NeedsResultCheck);
        store.DropReads = true; await vm.LoadAsync("request-1");
        Assert.True(vm.NeedsResultCheck); Assert.False(vm.CanPay); Assert.Null(vm.State);
        await vm.ConfirmSimulationAsync(); Assert.Equal(1, f.Api.Posts);
    }

    [Fact]
    public async Task Wrong_request_response_cannot_confirm_or_enable_payment()
    {
        var f = new Fixture(); f.Api.ReturnId = "other";
        using var vm = f.Model(); await vm.LoadAsync("request-1");
        Assert.Null(vm.State); Assert.False(vm.CanPay); Assert.Equal(0, f.Api.Posts);
    }

    [Fact]
    public async Task Disabled_server_option_cannot_be_bypassed_by_client()
    {
        var f = new Fixture(); f.Api.Enabled = false;
        using var vm = f.Model(); await vm.LoadAsync("request-1"); await vm.ConfirmSimulationAsync();
        Assert.False(vm.CanPay); Assert.Equal(0, f.Api.Posts);
    }

    [Fact]
    public async Task Operational_state_does_not_offer_fake_payment()
    {
        var f = new Fixture(); f.Api.Simulation = false;
        using var vm = f.Model(); await vm.LoadAsync("request-1"); await vm.ConfirmSimulationAsync();
        Assert.False(vm.CanPay); Assert.Equal(0, f.Api.Posts); Assert.Contains("온라인 결제 연결 준비", vm.Message);
    }

    [Fact]
    public async Task Server_started_payment_without_local_record_requires_result_check()
    {
        var f = new Fixture(); f.Api.ResultCheckRequired = true;
        using var vm = f.Model(); await vm.LoadAsync("request-1"); await vm.ConfirmSimulationAsync();
        Assert.True(vm.NeedsResultCheck); Assert.False(vm.CanPay); Assert.Equal(0, f.Api.Posts);
        Assert.Contains("다시 결제하지", vm.Message);
    }

    [Fact]
    public async Task Natural_session_expiry_clears_busy_state_but_keeps_payment_observation_for_relogin()
    {
        var f = new Fixture(); var scope = f.Scope;
        f.Api.AfterPost = () => f.Access.Identity = new("owner", 1, false);
        using var vm = f.Model(); await vm.LoadAsync("request-1"); await vm.ConfirmSimulationAsync();
        Assert.False(vm.IsBusy); Assert.Null(vm.State); Assert.False(vm.CanPay); Assert.Contains("로그인", vm.Message);
        Assert.NotNull(await f.Store.LoadAsync("owner", scope, default));
        f.Access.Identity = new("owner", 2, true); await vm.LoadAsync("request-1");
        Assert.True(vm.State!.PaymentConfirmed); Assert.False(vm.NeedsResultCheck); Assert.Equal(1, f.Api.Posts);
    }

    [Fact]
    public async Task Different_account_and_server_do_not_reuse_another_pending_payment()
    {
        var f = new Fixture(); f.Api.PostFailure = new TaskCanceledException();
        using (var old = f.Model()) { await old.LoadAsync("request-1"); await old.ConfirmSimulationAsync(); }
        f.Access.Identity = new("other-owner", 2, true);
        using (var other = f.Model()) { await other.LoadAsync("request-1"); Assert.False(other.NeedsResultCheck); }
        f.Access.Identity = new("owner", 3, true); f.Endpoint = new("https://other.invalid/");
        using var server = f.Model(); await server.LoadAsync("request-1"); Assert.False(server.NeedsResultCheck);
    }

    private sealed class Fixture
    {
        public readonly ApiStub Api = new(); public readonly AccessStub Access = new();
        public readonly VolatileWorkflowRecoveryStore Store = new();
        public Uri Endpoint = new("https://test.invalid/");
        private WorkflowRecoveryContext Context() => new(() => Access.Identity, () => Endpoint);
        public string Scope => Context().Capture().Scope("distributed.shipper.prepayment");
        public ShipperPrepaymentViewModel Model(IWorkflowRecoveryStore? store = null) => new(Api, Access, store ?? Store, Context());
    }
    private sealed class AccessStub : IRoleWorkspaceAccess
    {
        public RoleWorkspaceIdentity Identity = new("owner", 1, true);
        public event Action? Changed { add { } remove { } }
        public RoleWorkspaceIdentity GetIdentity(string roleKey) => Identity;
        public Task EnsureInitializedAsync(string roleKey, CancellationToken ct = default) => Task.CompletedTask;
        public Task SignInAsync(string r, string n, string p, CancellationToken ct = default) => Task.CompletedTask;
        public Task SignOutAsync(string r, CancellationToken ct = default) => Task.CompletedTask;
    }
    private sealed class ApiStub : IRoleWorkspaceApi
    {
        public bool Paid, FailReadAfterPost, ResultCheckRequired; public bool SetPaidOnPost = true, Enabled = true, Simulation = true;
        public int Posts, Reads; public string? ReturnId; public Exception? PostFailure; public 페이크결제승인요청? Body;
        public Action? AfterPost;
        public Task<T> GetAsync<T>(string role, string path, CancellationToken ct)
        {
            Reads++;
            if (FailReadAfterPost && Posts > 0) throw new RoleWorkspaceAccessException(404, "read failed");
            var id = Uri.UnescapeDataString(path.Split('/')[4]);
            return Task.FromResult((T)(object)new 화주선결제조회응답
            {
                의뢰Id = ReturnId ?? id, Amount = 12000, Enabled = Enabled, CanPay = Enabled && !Paid,
                PaymentConfirmed = Paid, SimulationPayment = Paid, SimulationAvailable = Simulation,
                ResultCheckRequired = ResultCheckRequired,
                DispatchStatus = Paid ? "매칭중" : "미시작"
            });
        }
        public Task<T?> PostAsync<T>(string role, string path, object body, CancellationToken ct)
        {
            Posts++; Body = Assert.IsType<페이크결제승인요청>(body);
            if (PostFailure is not null) throw PostFailure;
            Paid = SetPaidOnPost;
            AfterPost?.Invoke();
            return Task.FromResult((T?)(object)new 페이크결제승인응답 { 의뢰Id = Body.의뢰Id, Amount = Body.Amount, 결제상태 = "결제완료" });
        }
        public Task<T?> PutAsync<T>(string r, string p, object b, CancellationToken ct) => throw new NotSupportedException();
        public Task<T?> UploadAsync<T>(string r, string p, HttpContent b, CancellationToken ct) => throw new NotSupportedException();
    }
    private sealed class FailingStore : IWorkflowRecoveryStore
    {
        public Task<WorkflowRecoveryRecord?> LoadAsync(string o, string s, CancellationToken ct) => Task.FromResult<WorkflowRecoveryRecord?>(null);
        public Task SaveAsync(WorkflowRecoveryRecord r, CancellationToken ct) => throw new IOException("disk");
        public Task RemoveAsync(string o, string s, Guid id, CancellationToken ct) => Task.CompletedTask;
    }
    private sealed class DroppingStore : IWorkflowRecoveryStore
    {
        private readonly VolatileWorkflowRecoveryStore inner = new();
        public bool DropReads;
        public Task<WorkflowRecoveryRecord?> LoadAsync(string o, string s, CancellationToken ct)
            => DropReads ? Task.FromResult<WorkflowRecoveryRecord?>(null) : inner.LoadAsync(o, s, ct);
        public Task SaveAsync(WorkflowRecoveryRecord r, CancellationToken ct) => inner.SaveAsync(r, ct);
        public Task RemoveAsync(string o, string s, Guid id, CancellationToken ct) => inner.RemoveAsync(o, s, id, ct);
    }
}
