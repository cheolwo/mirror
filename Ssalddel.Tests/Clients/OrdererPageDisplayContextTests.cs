using OrdererApp.Components.Layout;

namespace Ssalddel.Tests.Clients;

public sealed class OrdererPageDisplayContextTests
{
    [Fact]
    public void PreviousPageCannotClearOrUpdateCurrentAuthenticationMode()
    {
        var context = new OrdererPageDisplayContext();
        var previous = context.Acquire(true, "https://app/food/restaurants");
        using var current = context.Acquire(true, "https://app/mart/orders/request");

        Assert.False(previous.SetAuthenticationMode(false));
        previous.Dispose();

        Assert.True(current.IsCurrent);
        Assert.True(context.AuthenticationOnly);
        Assert.True(current.SetAuthenticationMode(false));
        Assert.False(context.AuthenticationOnly);
    }

    [Fact]
    public void NavigationInvalidatesOldPageBeforeDestinationTakesOwnership()
    {
        var context = new OrdererPageDisplayContext();
        using var previous = context.Acquire(true, "https://app/food/restaurants");

        context.ResetForNavigation("https://app/mart/orders/request");

        Assert.False(previous.IsCurrent);
        Assert.False(previous.SetAuthenticationMode(true));
        Assert.False(context.AuthenticationOnly);
        using var current = context.Acquire(true, "https://app/mart/orders/request");
        previous.Dispose();
        Assert.True(current.IsCurrent);
        Assert.True(context.AuthenticationOnly);
    }

    [Fact]
    public void NavigationEventAfterDestinationPageRetainsItsMode()
    {
        var context = new OrdererPageDisplayContext();
        using var current = context.Acquire(true, "https://app/food/restaurants?restaurantId=42");

        context.ResetForNavigation("https://app/food/restaurants?restaurantId=42");

        Assert.True(current.IsFor("https://app/food/restaurants?restaurantId=42"));
        Assert.False(current.IsFor("https://app/food/restaurants?restaurantId=43"));
        Assert.True(context.AuthenticationOnly);
    }

    [Fact]
    public void StoppedLayoutRejectsOldAndLatePageUpdates()
    {
        var context = new OrdererPageDisplayContext();
        using var previous = context.Acquire(true, "https://app/food/restaurants");
        var changes = 0;
        context.Changed += () => changes++;

        context.Stop();
        Assert.False(previous.SetAuthenticationMode(false));
        previous.Dispose();
        context.ResetForNavigation("https://app/mart/orders/request");
        using var late = context.Acquire(false, "https://app/mart/orders/request");

        Assert.False(late.SetAuthenticationMode(true));
        Assert.False(late.IsCurrent);
        Assert.Equal(0, changes);
    }
}
