using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Ssalddel.Simulation.Application;
using Ssalddel.Simulation.Domain;

namespace Ssalddel.Simulation.Hosting;

[Authorize(Policy = SsalddelSimulationAuthorizationPolicies.OnlineWorld)]
public sealed class SimulationOnlineWorldHub(
    SimulationOnlineWorldService service) : Hub
{
    public const string HubPath = "/hubs/simulation-online-world";

    public async Task JoinWorld(string worldStableId)
    {
        var actor = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? Context.User?.FindFirstValue("sub")
            ?? throw new SimulationContractException(
                "SimulationAuthenticatedPlayerRequired");
        if (!service.IsConnectedParticipant(worldStableId, actor))
            throw new HubException("SimulationOnlineParticipantRequired");
        await Groups.AddToGroupAsync(Context.ConnectionId,
            GroupName(worldStableId));
    }

    public static string GroupName(string worldStableId)
        => "simulation-world:" + worldStableId.Trim();
}
