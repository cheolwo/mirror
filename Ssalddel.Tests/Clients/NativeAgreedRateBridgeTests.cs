using System.Net;
using System.Net.Http.Json;
using FDriverApp.Services;
using Ssalddel.Contracts.Common.Pricing;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;

namespace Ssalddel.Tests.Clients;

public sealed class NativeAgreedRateBridgeTests
{
    [Fact]
    public async Task DriverPricing_UsesExistingBearerAndRejectsOtherRoleOrEndpoint()
    {
        var session = new FDriverTestSession(); string? auth = null;
        var handler = new FDriverTestHttpHandler((request, _) =>
        {
            auth = request.Headers.Authorization?.ToString();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(Array.Empty<AgreedRateTableDto>()) });
        });
        var http = new HttpClient(handler) { BaseAddress = new("https://local.invalid/") };
        var bridge = new FDriverAgreedRateWorkspaceBridge(http, session, new(http, session));
        await bridge.GetAsync<IReadOnlyList<AgreedRateTableDto>>("food-driver", "api/v1/agreed-rate-tables?partyKind=FoodDriver", default);
        Assert.Equal("Bearer test-access", auth);
        await Assert.ThrowsAsync<RoleWorkspaceAccessException>(() => bridge.GetAsync<object>("restaurant", "api/v1/agreed-rate-tables", default));
        await Assert.ThrowsAsync<RoleWorkspaceAccessException>(() => bridge.GetAsync<object>("food-driver", "api/v1/admin/accounts", default));
        Assert.Single(handler.Paths);
    }

    [Fact]
    public async Task DriverPricingDelayedReply_AfterLogoutIsDiscarded()
    {
        var session = new FDriverTestSession();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reply = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new FDriverTestHttpHandler((_, _) => { entered.SetResult(); return reply.Task; });
        var http = new HttpClient(handler) { BaseAddress = new("https://local.invalid/") };
        var bridge = new FDriverAgreedRateWorkspaceBridge(http, session, new(http, session));
        var request = bridge.GetAsync<IReadOnlyList<AgreedRateTableDto>>("food-driver", "api/v1/agreed-rate-tables", default);
        await entered.Task; await session.ClearAsync(); reply.SetResult(new(HttpStatusCode.OK) { Content = JsonContent.Create(Array.Empty<AgreedRateTableDto>()) });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => request);
        Assert.False(bridge.GetIdentity("food-driver").IsAuthenticated);
    }
}
