using Microsoft.AspNetCore.Components;

namespace SsalddelAdminApp.Services;

public abstract class DispatchTrainingPageBase : ComponentBase, IAsyncDisposable
{
    [Inject] protected DispatchTrainingPageState State { get; set; } = default!;
    [Inject] protected NavigationManager Navigation { get; set; } = default!;
    protected virtual bool DecisionsMode => false;
    private readonly CancellationTokenSource lifetime = new();
    private Task? monitor;
    private bool disposed;
    private bool wasForeground;
    protected override async Task OnInitializedAsync()
    {
        wasForeground = State.IsForeground; State.Changed += OnChanged;
        await State.InitializeAsync(DecisionsMode);
        if (!disposed) monitor = MonitorAsync(lifetime.Token);
    }
    protected Task Refresh() => State.CapabilityChecked && State.Enabled && !State.RequiresLogin
        ? State.RefreshAsync(DecisionsMode) : State.InitializeAsync(DecisionsMode);
    protected void Login() => Navigation.NavigateTo("/login?returnUrl=" + Uri.EscapeDataString(DecisionsMode ? "/training/dispatch/decisions" : "/training/dispatch"));
    private void OnChanged() => _ = HandleChangedAsync();
    private async Task HandleChangedAsync()
    {
        if (disposed) return;
        try
        {
            await InvokeAsync(async () =>
            {
                if (disposed) return;
                var resumed = State.IsForeground && !wasForeground; wasForeground = State.IsForeground;
                if (resumed && State.Enabled && State.IsAdmin) await State.RefreshAsync(DecisionsMode);
                if (!disposed) StateHasChanged();
            });
        }
        catch (InvalidOperationException) when (disposed) { }
    }
    private async Task MonitorAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        try
        {
            while (await timer.WaitForNextTickAsync(ct))
                await InvokeAsync(() => State.RefreshAsync(DecisionsMode));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
    }
    public async ValueTask DisposeAsync()
    {
        if (disposed) return;
        disposed = true; State.Changed -= OnChanged; lifetime.Cancel(); State.Dispose();
        if (monitor is not null) { try { await monitor; } catch (OperationCanceledException) { } }
        lifetime.Dispose();
    }
}
