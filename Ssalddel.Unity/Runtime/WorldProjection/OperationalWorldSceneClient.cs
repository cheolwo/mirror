using System;
using System.Threading;
using System.Threading.Tasks;
using Ssalddel.WorkflowRules.Contracts;

namespace Ssalddel.Unity.WorldProjection
{
    public interface IOperationalWorldSceneDecoder
    {
        OperationalWorldSceneResponse Decode(string json);
    }

    /// <summary>
    /// 인증된 운영 지역 장면을 읽어 메모리 해석기에 적용합니다. 원문 JSON 또는 해석 결과를
    /// 파일·PlayerPrefs·Save에 쓰지 않으며, Command 전송 기능을 포함하지 않습니다.
    /// </summary>
    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E3,
        "인증 GET 응답을 Unity 메모리 해석기에 전달하는 계약 경계를 제공한다.",
        Boundary = "실제 Unity Scene, Play Mode, Game View 또는 운영 Command 실행 증거가 아니다.")]
    public sealed class OperationalWorldSceneClient
    {
        private readonly IOperationalWorldProjectionTransport transport;
        private readonly IOperationalWorldSceneDecoder decoder;
        private readonly Data.WorldProjection.OperationalWorldSceneInterpreter interpreter;

        public OperationalWorldSceneClient(
            IOperationalWorldProjectionTransport transport,
            IOperationalWorldSceneDecoder decoder,
            Data.WorldProjection.OperationalWorldSceneInterpreter interpreter)
        {
            this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
            this.decoder = decoder ?? throw new ArgumentNullException(nameof(decoder));
            this.interpreter = interpreter ?? throw new ArgumentNullException(nameof(interpreter));
        }

        public async Task<Data.WorldProjection.OperationalWorldSceneApplyResult> RefreshAsync(
            string areaStableId,
            long cursor,
            DateTime utcNow,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(areaStableId))
                throw new ArgumentException("OperationalWorldSceneAreaRequired", nameof(areaStableId));
            if (cursor < 0) throw new ArgumentOutOfRangeException(nameof(cursor));

            var route = "api/v1/world/areas/"
                        + Uri.EscapeDataString(areaStableId.Trim())
                        + "/scene-snapshots?cursor="
                        + cursor;
            var json = await transport.GetAsync(
                route,
                allowNotFound: false,
                requiresAuthentication: true,
                cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(json))
                throw new OperationalWorldProjectionException("OperationalApiResponseEmpty", route);
            return interpreter.Apply(decoder.Decode(json), utcNow);
        }

        public Data.WorldProjection.OperationalWorldSceneApplyResult Expire(DateTime utcNow)
            => interpreter.Expire(utcNow);

        public void Clear() => interpreter.Clear();
    }
}
