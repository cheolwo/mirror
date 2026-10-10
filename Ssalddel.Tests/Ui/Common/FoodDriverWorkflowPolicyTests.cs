using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Food;

namespace Ssalddel.Tests.Ui.Common;

public sealed class FoodDriverWorkflowPolicyTests
{
    [Theory]
    [InlineData(false, "운행중", "On", FoodDriverStartRecoveryDecision.None)]
    [InlineData(true, "", "On", FoodDriverStartRecoveryDecision.AwaitCanonical)]
    [InlineData(true, "future", "Off", FoodDriverStartRecoveryDecision.AwaitCanonical)]
    [InlineData(true, "대기", "Off", FoodDriverStartRecoveryDecision.ClearStopped)]
    [InlineData(true, "운행종료", "On", FoodDriverStartRecoveryDecision.ClearStopped)]
    [InlineData(true, "운행중", "On", FoodDriverStartRecoveryDecision.Complete)]
    [InlineData(true, "운행중", "Off", FoodDriverStartRecoveryDecision.EnableReception)]
    [InlineData(true, "운행중", "future", FoodDriverStartRecoveryDecision.AwaitCanonical)]
    public void CanonicalRecovery_DoesNotInferUnknownStateOrRepeatStart(bool pending, string status, string intent,
        FoodDriverStartRecoveryDecision expected)
    {
        // Codes are stable server strings; the UI policy cannot create a Start POST decision.
        var canonicalIntent = intent == "On" ? 운영배차수신의사Code.On : intent == "Off" ? 운영배차수신의사Code.Off : intent;
        Assert.Equal(expected, FoodDriverWorkRecoveryPolicy.ResolveStart(pending, status, canonicalIntent));
    }

    [Fact]
    public async Task CommonShell_UsesRecoveryContractForAnotherRole_WithoutFoodAdapterKnowledge()
    {
        var adapter = new RecoverableAdapter(); var clock = new Clock();
        using var model = new RoleWorkspaceViewModel([adapter], new Access(), new RoleWorkspaceState(), clock);
        await model.InitializeAsync("orderer", null);
        await model.PerformAsync(string.Empty, "preference-change");
        Assert.Equal("결과 확인 중", model.CommandError); Assert.Equal(1, adapter.Attempts);
        await model.PerformAsync(string.Empty, "preference-change"); Assert.Equal(1, adapter.Attempts);
        model.Suspend(); Assert.Equal(1, adapter.TransientClears); Assert.True(adapter.PreferenceStateUnconfirmed);
        adapter.Canonical = true; await model.ResumeAsync();
        Assert.Null(model.CommandError); Assert.False(adapter.PreferenceStateUnconfirmed);
        Assert.Equal(1, adapter.Attempts);
    }

    private sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow() => new(2026, 10, 9, 4, 0, 0, TimeSpan.Zero); }
    private sealed class Access : IRoleWorkspaceAccess
    {
        public event Action? Changed { add { } remove { } }
        public RoleWorkspaceIdentity GetIdentity(string roleKey) => new("owner-a", 1, true);
        public Task EnsureInitializedAsync(string roleKey, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SignInAsync(string roleKey, string name, string password, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SignOutAsync(string roleKey, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
    private sealed class RecoverableAdapter : IRoleWorkspaceAdapter, IRoleWorkspaceRecovery
    {
        public string RoleKey => "orderer";
        public bool StartStateUnconfirmed => false;
        public bool PreferenceStateUnconfirmed { get; private set; }
        public bool Canonical; public int Attempts, TransientClears;
        public Task<RoleWorkspaceSnapshot> LoadAsync(string? selectedId, CancellationToken cancellationToken)
            => Task.FromResult(new RoleWorkspaceSnapshot(RoleKey, [], EmptyActions: [new("preference-change", "변경")]));
        public Task PerformAsync(string itemId, string actionKey, Guid requestId, CancellationToken cancellationToken)
        { Attempts++; throw new InvalidOperationException("응답 유실"); }
        public bool BlocksAction(string actionKey) => PreferenceStateUnconfirmed;
        public string? RecordUnconfirmedAction(string actionKey) { PreferenceStateUnconfirmed = true; return "결과 확인 중"; }
        public RoleWorkspaceRecoveryResult ResolveCanonicalActions()
        {
            if (!Canonical || !PreferenceStateUnconfirmed) return new([], false);
            PreferenceStateUnconfirmed = false; return new(["preference-change"], true, ActionKey: "preference-change");
        }
        public void ClearTransientSnapshot() => TransientClears++;
        public void Clear() => PreferenceStateUnconfirmed = false;
    }
}
