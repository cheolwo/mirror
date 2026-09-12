using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ssalddel.WorkflowRules.Contracts;

namespace Ssalddel.Unity.WorldProjection
{
    public interface IAdministrativeDongDioramaDecoder
    {
        AdministrativeDongDioramaManifest DecodeManifest(string json);
        AdministrativeDongDioramaTile DecodeTile(string json);
        AdministrativeDongDisplayOverlayResponse DecodeDisplayOverlays(string json);
    }

    /// <summary>
    /// 서버가 게시한 행정동 디오라마 판본을 GET으로만 읽습니다. 이 형식에는 Command와
    /// 로컬 저장 기능이 없으며, 장면을 직접 생성하는 책임도 포함하지 않습니다.
    /// </summary>
    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E3,
        "행정동 디오라마 manifest, tile, 표시 오버레이를 인증 GET으로 메모리 해석기에 전달한다.",
        Boundary = "실제 행정동 자료 게시, Unity Scene 배치, Play Mode 또는 Game View 증거가 아니다.")]
    public sealed class AdministrativeDongDioramaClient
    {
        private readonly IOperationalWorldProjectionTransport transport;
        private readonly IAdministrativeDongDioramaDecoder decoder;
        private readonly Data.WorldProjection.AdministrativeDongDioramaInterpreter interpreter;

        public AdministrativeDongDioramaClient(
            IOperationalWorldProjectionTransport transport,
            IAdministrativeDongDioramaDecoder decoder,
            Data.WorldProjection.AdministrativeDongDioramaInterpreter interpreter)
        {
            this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
            this.decoder = decoder ?? throw new ArgumentNullException(nameof(decoder));
            this.interpreter = interpreter ?? throw new ArgumentNullException(nameof(interpreter));
        }

        public async Task<Data.WorldProjection.AdministrativeDongDioramaApplyResult> RefreshAsync(
            string administrativeAreaStableId,
            CancellationToken cancellationToken = default)
        {
            if (!AdministrativeDongDioramaPolicy.IsAdministrativeAreaStableId(administrativeAreaStableId))
                throw new ArgumentException("AdministrativeDongStableIdInvalid", nameof(administrativeAreaStableId));
            var area = Uri.EscapeDataString(administrativeAreaStableId.Trim());
            var prefix = "api/v1/world/administrative-areas/" + area;
            var manifestJson = await GetRequiredAsync(prefix + "/diorama-manifest", cancellationToken)
                .ConfigureAwait(false);
            var manifest = decoder.DecodeManifest(manifestJson);
            var tiles = new List<AdministrativeDongDioramaTile>();
            foreach (var summary in manifest.Tiles ?? Array.Empty<AdministrativeDongDioramaTileSummary>())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var tileJson = await GetRequiredAsync(
                    prefix + "/diorama-tiles/" + Uri.EscapeDataString(summary.TileStableId),
                    cancellationToken).ConfigureAwait(false);
                tiles.Add(decoder.DecodeTile(tileJson));
            }
            var overlaysJson = await GetRequiredAsync(prefix + "/display-overlays", cancellationToken)
                .ConfigureAwait(false);
            var overlays = decoder.DecodeDisplayOverlays(overlaysJson);
            cancellationToken.ThrowIfCancellationRequested();
            return interpreter.Apply(manifest, tiles.ToArray(), overlays);
        }

        public void Clear() => interpreter.Clear();

        private async Task<string> GetRequiredAsync(string route, CancellationToken cancellationToken)
        {
            var json = await transport.GetAsync(
                route,
                allowNotFound: false,
                requiresAuthentication: true,
                cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(json))
                throw new OperationalWorldProjectionException("AdministrativeDongDioramaApiResponseEmpty", route);
            return json;
        }
    }
}
