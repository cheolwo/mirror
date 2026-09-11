using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Simulation.Application;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Simulation.Domain;

namespace Ssalddel.Simulation.Hosting.Controllers;

[ApiController]
[Authorize(Policy = SsalddelSimulationAuthorizationPolicies.OnlineWorld)]
[Route("api/simulation/v1/online-worlds")]
[SsalddelCodeMetadata(
    SsalddelCodeFeatureKeys.SimulationSessionLifecycle,
    SsalddelCodeLayer.Api,
    "인증된 플레이어의 공식 지속 세계와 비공개 협동 방 경계를 제공한다.",
    StepKey = "api.online-world",
    DependsOnStepKeys = new[] { "contract.online-world" },
    ExecutionStage = SsalddelCodeExecutionStage.Confirm,
    Effects = SsalddelCodeEffect.StateMutation,
    ReadsFrom = SsalddelCodeDataScope.SimulationState,
    WritesTo = SsalddelCodeDataScope.SimulationState,
    FlowOrder = 21,
    Boundary = "JWT 행위자를 서버에서 확정하고 운영 DB·운영 업무 상태를 변경하지 않는다.")]
[Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
    Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E2,
    "인증된 온라인 세계 RemoteHost HTTP 실행 경계를 제공한다.",
    Boundary = "HTTP route 존재는 실제 Unity 연결·부하 또는 운영 배포 증거가 아니다.",
    SubmoduleKey = Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceSubmoduleKeys.E2원격HostAdapter)]
public sealed class SimulationOnlineWorldsController(
    SimulationOnlineWorldService service,
    SimulationOnlineMeditationBridgeService meditationBridge,
    SimulationOnlineNatureSessionProvisioningService sessionProvisioning,
    SimulationOnlineCooperativeLoggingService cooperativeLogging,
    IHubContext<SimulationOnlineWorldHub> hub) : SimulationApiControllerBase
{
    [HttpGet]
    public ActionResult<SimulationOnlineWorldDirectorySnapshot> Directory()
        => Ok(service.Directory());

    [HttpGet("{worldStableId}")]
    public ActionResult<SimulationOnlineWorldStateSnapshot> GetWorld(
        string worldStableId) => Ok(service.GetWorld(worldStableId));

    [HttpGet("me/meditation")]
    public ActionResult<SimulationAccountMeditationSnapshot> MyMeditation()
        => Ok(service.GetAccountMeditation(Actor()));

    [HttpPost("private-rooms")]
    public ActionResult<SimulationOnlineWorldMutationResult> CreatePrivateRoom(
        [FromBody] SimulationPrivateRoomCreateRequest request)
        => Ok(service.CreatePrivateRoom(Actor(), request));

    [HttpPost("{worldStableId}/joins")]
    public ActionResult<SimulationOnlineWorldMutationResult> Join(
        string worldStableId,
        [FromBody] SimulationOnlineWorldJoinRequest request)
    {
        RequireRouteWorld(worldStableId, request.WorldStableId);
        return Ok(service.Join(Actor(), request));
    }

    [HttpPost("{worldStableId}/leaves")]
    public ActionResult<SimulationOnlineWorldMutationResult> Leave(
        string worldStableId,
        [FromBody] SimulationOnlineWorldLeaveRequest request)
    {
        RequireRouteWorld(worldStableId, request.WorldStableId);
        return Ok(service.Leave(Actor(), request));
    }

    [HttpPost("{worldStableId}/parties")]
    public ActionResult<SimulationOnlineWorldMutationResult> CreateParty(
        string worldStableId,
        [FromBody] SimulationOnlinePartyCreateRequest request)
    {
        RequireRouteWorld(worldStableId, request.WorldStableId);
        return Ok(service.CreateParty(Actor(), request));
    }

    [HttpPost("{worldStableId}/signals")]
    public async Task<ActionResult<SimulationOnlineWorldMutationResult>> Signal(
        string worldStableId,
        [FromBody] SimulationFixedSignalSendRequest request)
    {
        RequireRouteWorld(worldStableId, request.WorldStableId);
        var result = service.SendFixedSignal(Actor(), request);
        if (result.Applied)
            await hub.Clients.Group(SimulationOnlineWorldHub.GroupName(worldStableId))
                .SendAsync("FixedSignal", result.World.RecentSignals.Last());
        return Ok(result);
    }

    [HttpPost("{worldStableId}/area-set-transfers")]
    public ActionResult<SimulationOnlineWorldMutationResult> TransferAreaSet(
        string worldStableId,
        [FromBody] SimulationOnlineAreaSetTransferRequest request)
    {
        RequireRouteWorld(worldStableId, request.WorldStableId);
        return Ok(service.TransferAreaSet(Actor(), request));
    }

    [HttpPost("{worldStableId}/meditation-syncs")]
    public ActionResult<SimulationOnlineWorldMutationResult> SyncMeditation(
        string worldStableId,
        [FromBody] SimulationOnlineMeditationSyncRequest request)
    {
        RequireRouteWorld(worldStableId, request.WorldStableId);
        return Ok(meditationBridge.Sync(Actor(), request));
    }

    [HttpPost("{worldStableId}/authority-session-provisions")]
    public ActionResult<SimulationOnlineAuthoritySessionRuntimeSnapshot>
        ProvisionAuthoritySession(string worldStableId,
            [FromBody] SimulationOnlineAuthoritySessionProvisionRequest request)
    {
        RequireRouteWorld(worldStableId, request.WorldStableId);
        return Ok(sessionProvisioning.Ensure(Actor(), request));
    }

    [HttpPost("{worldStableId}/logging/begins")]
    public ActionResult<SimulationOnlineLoggingResultSnapshot> BeginLogging(
        string worldStableId,
        [FromBody] SimulationOnlineLoggingBeginRequest request)
    {
        RequireRouteWorld(worldStableId, request.WorldStableId);
        return Ok(cooperativeLogging.Begin(Actor(), request));
    }

    [HttpPost("{worldStableId}/logging/focus-attempts")]
    public ActionResult<SimulationOnlineLoggingResultSnapshot> FocusLogging(
        string worldStableId,
        [FromBody] SimulationOnlineLoggingFocusRequest request)
    {
        RequireRouteWorld(worldStableId, request.WorldStableId);
        return Ok(cooperativeLogging.Focus(Actor(), request));
    }

    [HttpPost("{worldStableId}/logging/completions")]
    public ActionResult<SimulationOnlineLoggingResultSnapshot> CompleteLogging(
        string worldStableId,
        [FromBody] SimulationOnlineLoggingCompleteRequest request)
    {
        RequireRouteWorld(worldStableId, request.WorldStableId);
        return Ok(cooperativeLogging.Complete(Actor(), request));
    }

    [HttpPost("{worldStableId}/logging/reconnects")]
    public ActionResult<SimulationOnlineLoggingReconnectSnapshot>
        ReconnectLogging(string worldStableId,
            [FromBody] SimulationOnlineLoggingReconnectRequest request)
    {
        RequireRouteWorld(worldStableId, request.WorldStableId);
        return Ok(cooperativeLogging.Reconnect(Actor(), request));
    }

    private string Actor() => User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue("sub")
        ?? throw new SimulationContractException(
            "SimulationAuthenticatedPlayerRequired");

    private static void RequireRouteWorld(string route, string body)
    {
        if (!string.Equals(route?.Trim(), body?.Trim(),
                StringComparison.Ordinal))
            throw new SimulationContractException(
                "SimulationOnlineWorldRouteMismatch");
    }
}
