using WarehouseManagerApp.Components.Layout;

namespace Ssalddel.Tests.Clients;

public sealed class WarehousePageDisplayContextTests
{
    [Fact]
    public void DisposingPreviousPanelDoesNotClearCurrentPanel()
    {
        var context = new WarehousePageDisplayContext();
        var previous = context.Acquire("https://app/warehouse/inbounds/expected");
        using var current = context.Acquire("https://app/work/outbound/packing");

        previous.Dispose();

        Assert.False(previous.IsCurrent);
        Assert.True(current.IsCurrent);
        Assert.True(context.AuthenticationOnly);
        current.Dispose();
        Assert.False(context.AuthenticationOnly);
    }

    [Fact]
    public void NavigationClearsAuthenticationUntilDestinationPanelAcquires()
    {
        var context = new WarehousePageDisplayContext();
        using var previous = context.Acquire("https://app/warehouse/inbounds/expected");

        context.ResetForNavigation("https://app/work/outbound/packing");

        Assert.False(previous.IsCurrent);
        Assert.False(context.AuthenticationOnly);
        using var current = context.Acquire("https://app/work/outbound/packing");
        previous.Dispose();
        Assert.True(current.IsCurrent);
        Assert.True(context.AuthenticationOnly);
    }

    [Fact]
    public void NavigationEventAfterDestinationPanelDoesNotInvalidateIt()
    {
        var context = new WarehousePageDisplayContext();
        using var current = context.Acquire("https://app/work/outbound/packing?inboundItemId=42");

        context.ResetForNavigation("https://app/work/outbound/packing?inboundItemId=42");

        Assert.True(current.IsFor("https://app/work/outbound/packing?inboundItemId=42"));
        Assert.False(current.IsFor("https://app/work/outbound/packing?inboundItemId=43"));
        Assert.True(context.AuthenticationOnly);
    }

    [Fact]
    public void StoppedLayoutCannotBeChangedByOldOrLatePanels()
    {
        var context = new WarehousePageDisplayContext();
        using var previous = context.Acquire("https://app/warehouse/inbounds/expected");
        var changes = 0;
        context.Changed += () => changes++;

        context.Stop();
        previous.Dispose();
        context.ResetForNavigation("https://app/work/outbound/packing");
        using var late = context.Acquire("https://app/work/outbound/packing");
        late.Dispose();

        Assert.False(previous.IsCurrent);
        Assert.False(late.IsCurrent);
        Assert.Equal(0, changes);
    }
}
