using System;
using System.Threading;
using System.Threading.Tasks;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Unity.Data.Campaigns;
using Ssalddel.Unity.WorldProjection;

namespace Ssalddel.Unity.Campaigns
{
    public interface ISimulation절기운영CampaignDecoder
    {
        Simulation절기운영CampaignStateSnapshot Decode(string json);
    }

    /// <summary>
    /// 절기 운영 Campaign을 인증 GET으로만 읽고 메모리 Interpreter에 전달합니다.
    /// Command 전송, 로컬 저장, 자동 Session·지역 전환을 제공하지 않습니다.
    /// </summary>
    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E2,
        "절기 운영 Campaign 인증 GET 응답을 Unity 읽기 전용 Interpreter에 전달한다.",
        SubmoduleKey = Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceSubmoduleKeys.E2Unity권위Client,
        Boundary = "Command 전송·로컬 저장·Campaign 전이·자동 문맥 전환을 제공하지 않는다.")]
    public sealed class Simulation절기운영CampaignClient
    {
        private readonly IOperationalWorldProjectionTransport transport;
        private readonly ISimulation절기운영CampaignDecoder decoder;
        private readonly Simulation절기운영CampaignInterpreter interpreter;

        public Simulation절기운영CampaignClient(
            IOperationalWorldProjectionTransport transport,
            ISimulation절기운영CampaignDecoder decoder,
            Simulation절기운영CampaignInterpreter interpreter)
        {
            this.transport = transport
                ?? throw new ArgumentNullException(nameof(transport));
            this.decoder = decoder
                ?? throw new ArgumentNullException(nameof(decoder));
            this.interpreter = interpreter
                ?? throw new ArgumentNullException(nameof(interpreter));
        }

        public async Task<Simulation절기운영CampaignApplyResult> RefreshAsync(
            string sessionStableId,
            string expectedAreaStableId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(sessionStableId))
                throw new ArgumentException(
                    "SeasonalOperationsCampaignSessionRequired",
                    nameof(sessionStableId));
            if (string.IsNullOrWhiteSpace(expectedAreaStableId))
                throw new ArgumentException(
                    "SeasonalOperationsCampaignAreaRequired",
                    nameof(expectedAreaStableId));
            var route = "api/simulation/v1/sessions/"
                + Uri.EscapeDataString(sessionStableId.Trim())
                + "/seasonal-operations-campaign";
            try
            {
                var json = await transport.GetAsync(route,
                    allowNotFound: false,
                    requiresAuthentication: true,
                    cancellationToken).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(json))
                    return interpreter.RetainFailure(
                        "SeasonalOperationsCampaignResponseEmpty");
                return interpreter.Apply(sessionStableId,
                    expectedAreaStableId, decoder.Decode(json));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                return interpreter.RetainFailure(
                    "SeasonalOperationsCampaignRefreshFailed");
            }
        }

        public void Clear() => interpreter.Clear();
    }
}
