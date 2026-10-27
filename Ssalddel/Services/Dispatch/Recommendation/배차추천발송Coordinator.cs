using Microsoft.AspNetCore.SignalR;
using Ssalddel.Contracts.Common.Drivers;
using Ssalddel.Hubs;

namespace 살뜰.Services.Dispatch.Recommendation;

internal static class 배차추천발송Coordinator
{
    internal static async Task SendAsync(
        string driverId,
        IReadOnlyList<DispatchRecommendationDto> recommendations,
        IHubContext<DispatchRecommendationHub> hubContext,
        IDriverRecommendationPushService pushService,
        IDispatchRecommendationLogStore logStore,
        Func<string, string> buildDriverGroup)
    {
        await hubContext.Clients
            .Group(buildDriverGroup(driverId))
            .SendAsync(DriverDispatchRealtimeContract.RecommendationsEvent, recommendations);
        await pushService.SendAsync(driverId, recommendations);

        await logStore.AppendAsync(new DispatchRecommendationLogEntry(
            driverId,
            DateTime.UtcNow,
            recommendations.Count,
            recommendations.Select(x => x.의뢰Id).ToList()));
    }
}
