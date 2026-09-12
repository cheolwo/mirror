using System;
using System.Threading;
using System.Threading.Tasks;

namespace Ssalddel.Unity.Data.WorldProjection
{
    public sealed class OperationalOsWorldObservationRefreshResult
    {
        public OperationalWorldSceneApplyResult SceneResult { get; set; } =
            new OperationalWorldSceneApplyResult();
        public OperationalOsObservationRouteResult OsObservationResult { get; set; } =
            new OperationalOsObservationRouteResult();
    }

    /// <summary>
    /// 인증 GET·장면 해석·OS별 메모리 분배를 한 번의 읽기 전용 갱신으로 잇습니다.
    /// Scene 또는 GameObject를 만들지 않으며 운영 Command를 노출하지 않습니다.
    /// </summary>
    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E3,
        "인증 GET, 장면 해석과 OS별 메모리 분배를 읽기 전용 갱신으로 연결한다.",
        Boundary = "Unity package 순수 C# 연결이며 실제 서버, Scene, Play Mode 또는 Game View 증거가 아니다.")]
    public sealed class OperationalOsWorldObservationSession
    {
        private readonly Ssalddel.Unity.WorldProjection.OperationalWorldSceneClient client;
        private readonly OperationalOsObservationRouter router;

        public OperationalOsWorldObservationSession(
            Ssalddel.Unity.WorldProjection.OperationalWorldSceneClient client,
            OperationalOsObservationRouter router)
        {
            this.client = client ?? throw new ArgumentNullException(nameof(client));
            this.router = router ?? throw new ArgumentNullException(nameof(router));
        }

        public async Task<OperationalOsWorldObservationRefreshResult> RefreshAsync(
            string areaStableId,
            long cursor,
            DateTime utcNow,
            CancellationToken cancellationToken = default)
        {
            var sceneResult = await client.RefreshAsync(
                areaStableId,
                cursor,
                utcNow,
                cancellationToken).ConfigureAwait(false);
            return Combine(sceneResult);
        }

        public OperationalOsWorldObservationRefreshResult Expire(DateTime utcNow)
            => Combine(client.Expire(utcNow));

        public OperationalOsWorldObservationRefreshResult Clear()
        {
            client.Clear();
            return Combine(new OperationalWorldSceneApplyResult { Accepted = true });
        }

        private OperationalOsWorldObservationRefreshResult Combine(
            OperationalWorldSceneApplyResult sceneResult)
            => new OperationalOsWorldObservationRefreshResult
            {
                SceneResult = sceneResult,
                OsObservationResult = router.Route(sceneResult)
            };
    }
}
