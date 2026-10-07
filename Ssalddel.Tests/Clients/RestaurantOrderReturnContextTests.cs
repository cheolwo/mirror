using System.Net;
using RestaurantDeskApp.Services;
using Ssalddel.Client.Infrastructure.Security;

namespace Ssalddel.Tests.Clients;

public sealed class RestaurantOrderReturnContextTests
{
    [Fact]
    public void SameOwnerReturnsOnceToTheCurrentOrderAfterExpiry()
    {
        var context = new RestaurantOrderReturnContext();
        context.TrackOrder("owner-a", "FOOD-A");
        context.EndSession(explicitLogout: false, endingOwner: "owner-a");

        Assert.Equal("/orders/FOOD-A", context.ConsumeReturnRoute("owner-a"));
        Assert.Null(context.ConsumeReturnRoute("owner-a"));
    }

    [Fact]
    public void DifferentOwnerCannotResumeOrRetainPreviousOrdersReturn()
    {
        var context = Suspended();
        Assert.Null(context.ConsumeReturnRoute("owner-b"));
        Assert.Null(context.ConsumeReturnRoute("owner-a"));
    }

    [Fact]
    public void ExplicitLogoutClearsEvenASuspendedOrder()
    {
        var context = Suspended();
        context.EndSession(explicitLogout: true, endingOwner: null);
        Assert.Null(context.ConsumeReturnRoute("owner-a"));
    }

    [Fact]
    public void AnonymousLoginCheckDoesNotDiscardAnExpiredOwnersReturn()
    {
        var context = Suspended();
        context.EndSession(explicitLogout: false, endingOwner: null);
        Assert.Null(context.ConsumeReturnRoute(null));
        Assert.Equal("/orders/FOOD-A", context.ConsumeReturnRoute("owner-a"));
    }

    [Fact]
    public void FollowingAnotherProtectedPageClearsTheOldReturn()
    {
        var context = Suspended();
        context.Clear();
        context.EndSession(explicitLogout: false, endingOwner: "owner-a");
        Assert.Null(context.ConsumeReturnRoute("owner-a"));
    }

    [Fact]
    public void OrdinaryNavigationIsNotARequestToResumeAfterLogin()
    {
        var context = new RestaurantOrderReturnContext();
        context.TrackOrder("owner-a", "FOOD-A");
        Assert.Null(context.ConsumeReturnRoute("owner-a"));
    }

    [Fact]
    public void DeepLinkWithoutRestoredOwnerStillReturnsOnlyAnEncodedInternalOrderRoute()
    {
        const string orderNo = "FOOD A/../?returnUrl=https://external.invalid";
        var context = new RestaurantOrderReturnContext();
        context.TrackOrder(null, orderNo);
        context.EndSession(explicitLogout: false, endingOwner: null);

        Assert.Equal($"/orders/{Uri.EscapeDataString(orderNo)}", context.ConsumeReturnRoute("owner-a"));
    }

    [Fact]
    public void AnOldOwnersLateExpiryCannotChangeTheCurrentOwnersReturn()
    {
        var context = new RestaurantOrderReturnContext();
        context.TrackOrder("owner-b", "FOOD-B");
        context.EndSession(explicitLogout: false, endingOwner: "owner-b");
        context.EndSession(explicitLogout: false, endingOwner: "owner-a");
        context.SuspendForLogin("owner-a", "FOOD-A");

        Assert.Equal("/orders/FOOD-B", context.ConsumeReturnRoute("owner-b"));
    }

    [Fact]
    public async Task DefinitiveAuthRejectionPreservesTheOwnerBeforeTheSessionIsCleared()
    {
        using var http = new HttpClient(new RejectingHandler()) { BaseAddress = new("http://localhost/") };
        var session = new ClientAuthSession(new MemoryStore(), new ClientSessionGuard());
        var auth = new RestaurantAuthService(http, session);
        await session.ApplyAsync(new("access", DateTime.UtcNow.AddHours(1), "refresh", DateTime.UtcNow.AddDays(1),
            "owner-a", "음식점", ["음식점"]));
        auth.OrderReturn.TrackOrder(session.UserId, "FOOD-A");

        var result = await auth.EnsureAccessTokenAsync(forceRefresh: true);

        Assert.True(result.RequiresLogin);
        Assert.False(session.IsAuthenticated);
        Assert.Equal("/orders/FOOD-A", auth.OrderReturn.ConsumeReturnRoute("owner-a"));
    }

    [Fact]
    public async Task ExplicitAuthLogoutDoesNotReturnToAnEarlierOrder()
    {
        using var http = new HttpClient(new RejectingHandler()) { BaseAddress = new("http://localhost/") };
        var session = new ClientAuthSession(new MemoryStore(), new ClientSessionGuard());
        var auth = new RestaurantAuthService(http, session);
        await session.ApplyAsync(new("access", DateTime.UtcNow.AddHours(1), "refresh", DateTime.UtcNow.AddDays(1),
            "owner-a", "음식점", ["음식점"]));
        auth.OrderReturn.TrackOrder(session.UserId, "FOOD-A");
        auth.OrderReturn.SuspendForLogin(session.UserId, "FOOD-A");

        await auth.LogoutAsync();

        Assert.Null(auth.OrderReturn.ConsumeReturnRoute("owner-a"));
    }

    private static RestaurantOrderReturnContext Suspended()
    {
        var context = new RestaurantOrderReturnContext();
        context.TrackOrder("owner-a", "FOOD-A");
        context.EndSession(explicitLogout: false, endingOwner: "owner-a");
        return context;
    }

    private sealed class RejectingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized));
    }

    private sealed class MemoryStore : IClientSecureTokenStore
    {
        private ClientAuthTokenSnapshot? value;
        public Task<ClientAuthTokenSnapshot?> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(value);
        public Task SaveAsync(ClientAuthTokenSnapshot snapshot, CancellationToken cancellationToken = default)
        { value = snapshot; return Task.CompletedTask; }
        public Task ClearAsync(CancellationToken cancellationToken = default)
        { value = null; return Task.CompletedTask; }
    }
}
