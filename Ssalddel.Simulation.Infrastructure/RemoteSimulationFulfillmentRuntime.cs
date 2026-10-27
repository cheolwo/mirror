using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Ssalddel.Simulation.Contracts;

namespace Ssalddel.Simulation.Infrastructure
{
    /// <summary>같은 Simulation 세션의 출고 API를 전달하는 HTTP Adapter다. 자동 재시도하지 않는다.</summary>
    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E2,
        "RemoteHost의 다단계 출고 조회·미리보기·확정을 전달한다.",
        Boundary = "규칙·상태·인증·재시도 정책을 재구현하지 않는 HTTP Adapter다.",
        SubmoduleKey = Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceSubmoduleKeys.E2원격HostAdapter)]
    public sealed class RemoteSimulationFulfillmentRuntime : ISimulationFulfillmentRuntime
    {
        private readonly HttpClient client;
        private readonly JsonSerializerOptions jsonOptions;
        private readonly RemoteSimulationHttpJsonClient transport;

        public RemoteSimulationFulfillmentRuntime(
            HttpClient client, JsonSerializerOptions? jsonOptions = null)
        {
            this.client = client ?? throw new ArgumentNullException(nameof(client));
            this.jsonOptions = jsonOptions ?? new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true,
            };
            transport = new RemoteSimulationHttpJsonClient(client, this.jsonOptions);
        }

        public async ValueTask<SimulationFulfillmentStateSnapshot?> GetFulfillmentStateAsync(
            string sessionStableId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(sessionStableId))
                throw new ArgumentException("Simulation Session 식별자가 필요합니다.", nameof(sessionStableId));
            using var message = new HttpRequestMessage(HttpMethod.Get,
                "api/simulation/v1/sessions/" + Uri.EscapeDataString(sessionStableId.Trim())
                + "/fulfillment");
            using var response = await client.SendAsync(message, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            if (response.StatusCode == HttpStatusCode.NoContent) return null;
            var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(json))
                throw new InvalidOperationException("RemoteSimulationResponseEmpty");
            return JsonSerializer.Deserialize<SimulationFulfillmentStateSnapshot>(json, jsonOptions);
        }

        public ValueTask<SimulationFulfillmentPlanSnapshot> PreviewFulfillmentAsync(
            string sessionStableId, SimulationFulfillmentOrderRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return transport.PostAsync<SimulationFulfillmentOrderRequest, SimulationFulfillmentPlanSnapshot>(
                sessionStableId, "fulfillment/previews", request, cancellationToken);
        }

        public ValueTask<경영SimulationSessionSnapshot> ConfirmFulfillmentAsync(
            string sessionStableId, SimulationFulfillmentCommand request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return transport.PostAsync<SimulationFulfillmentCommand, 경영SimulationSessionSnapshot>(
                sessionStableId, "fulfillment/confirm", request, cancellationToken);
        }
    }
}
