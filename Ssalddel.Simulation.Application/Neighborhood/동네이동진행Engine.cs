using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Ssalddel.Contracts.Common.Metadata;

namespace Ssalddel.Simulation.Application
{
    [SsalddelEvidenceResponsibility(SsalddelEvidenceStage.E1,
        "경로 지문과 Tick·거리·차단 상태를 불변 진행 후보로 보존한다.",
        Boundary = "도착·픽업·배송 권위 결과나 Session 저장 데이터가 아니다.")]
    public sealed class 동네이동진행후보
    {
        public string RouteFingerprint { get; }
        public int Tick { get; }
        public double DistanceMeters { get; }
        public 동네평면좌표 Position { get; }
        public bool Blocked { get; }
        public bool ReachedCandidateEnd { get; }
        public string ReasonCode => Blocked ? "NeighborhoodMovementBlocked" : string.Empty;
        public bool RuntimeAuthorized => false;

        internal 동네이동진행후보(string fingerprint, int tick, double distance,
            동네평면좌표 position, bool blocked, bool reached)
        {
            RouteFingerprint = fingerprint; Tick = tick; DistanceMeters = distance;
            Position = position; Blocked = blocked; ReachedCandidateEnd = reached;
        }
    }

    /// <summary>검토된 경로를 따라 한 Tick의 진행 후보를 계산한다. Actor/주문 상태는 바꾸지 않는다.</summary>
    [SsalddelCodeMetadata(SsalddelCodeFeatureKeys.SimulationWorldDerivation, SsalddelCodeLayer.Application,
        "경로 후보의 거리 진행·차단·복원 후보를 계산한다.",
        StepKey = "application.neighborhood-movement-candidate",
        DependsOnStepKeys = new[] { "application.neighborhood-route" }, FlowOrder = 120,
        ExecutionStage = SsalddelCodeExecutionStage.Preview, ReadsFrom = SsalddelCodeDataScope.DerivedWorld,
        Effects = SsalddelCodeEffect.None, Boundary = "기존 Actor/주문/Session 상태를 변경하지 않는 후보 계산이다.")]
    [SsalddelEvidenceResponsibility(SsalddelEvidenceStage.E2,
        "차량·오토바이·도보의 거리 진행과 차단·동일 경로 복원 후보를 계산한다.",
        Boundary = "독립 순수 지원 모듈이며 Runtime 연결·실제 통행·배송 결과를 확정하지 않는다.")]
    public sealed class 동네이동진행Engine
    {
        public const double VehicleMetersPerTick = 5;
        public const double PedestrianMetersPerTick = 1;

        public 동네이동진행후보 시작(동네이동경로후보 route)
        {
            var fingerprint = 지문(route);
            return 결과(route, fingerprint, 0, 0, false);
        }

        public 동네이동진행후보 진행(동네이동경로후보 route, 동네이동진행후보 current,
            int nextTick, bool blocked = false)
        {
            if (current == null) throw new ArgumentNullException(nameof(current));
            var fingerprint = 지문(route);
            if (!string.Equals(fingerprint, current.RouteFingerprint, StringComparison.Ordinal))
                throw new InvalidDataException("NeighborhoodMovementRouteMismatch");
            if (nextTick == current.Tick)
            {
                if (blocked != current.Blocked)
                    throw new InvalidDataException("NeighborhoodMovementTickPayloadConflict");
                return current;
            }
            if (current.Tick == int.MaxValue || nextTick != current.Tick + 1)
                throw new InvalidDataException("NeighborhoodMovementTickSequenceInvalid");
            var speed = 차량계열(route.Mode) ? VehicleMetersPerTick : PedestrianMetersPerTick;
            var distance = blocked ? current.DistanceMeters : Math.Min(route.LengthMeters, current.DistanceMeters + speed);
            return 결과(route, fingerprint, nextTick, distance, blocked);
        }

        public 동네이동진행후보 복원(동네이동경로후보 route, string expectedFingerprint,
            int tick, double distanceMeters, bool blocked)
        {
            var fingerprint = 지문(route);
            if (!string.Equals(fingerprint, expectedFingerprint, StringComparison.Ordinal))
                throw new InvalidDataException("NeighborhoodMovementRouteMismatch");
            var speed = 차량계열(route.Mode) ? VehicleMetersPerTick : PedestrianMetersPerTick;
            if (tick < 0 || double.IsNaN(distanceMeters) || double.IsInfinity(distanceMeters)
                || distanceMeters < 0 || distanceMeters > route.LengthMeters
                || distanceMeters > (double)tick * speed || (tick == 0 && blocked)
                || (distanceMeters < route.LengthMeters && distanceMeters % speed != 0))
                throw new InvalidDataException("NeighborhoodMovementRestoreInvalid");
            return 결과(route, fingerprint, tick, distanceMeters, blocked);
        }

        private static 동네이동진행후보 결과(동네이동경로후보 route, string fingerprint,
            int tick, double distance, bool blocked)
        {
            var position = route.Points[route.Points.Count - 1];
            if (distance == route.LengthMeters)
                return new 동네이동진행후보(fingerprint, tick, distance, position, blocked, true);
            var remaining = distance;
            for (var i = 1; i < route.Points.Count; i++)
            {
                var from = route.Points[i - 1];
                var to = route.Points[i];
                var length = from.거리(to);
                if (length == 0) continue;
                if (remaining <= length)
                {
                    var fraction = remaining / length;
                    position = new 동네평면좌표(from.X + (to.X - from.X) * fraction,
                        from.Z + (to.Z - from.Z) * fraction);
                    break;
                }
                remaining -= length;
            }
            return new 동네이동진행후보(fingerprint, tick, distance, position, blocked,
                distance == route.LengthMeters);
        }

        private static string 지문(동네이동경로후보 route)
        {
            if (route == null) throw new ArgumentNullException(nameof(route));
            if (!route.Found || route.Points.Count == 0)
                throw new InvalidDataException("NeighborhoodMovementRouteUnavailable");
            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                writer.Write("neighborhood-movement.r1");
                writer.Write(route.SnapshotId); writer.Write(route.Revision); writer.Write((int)route.Mode);
                writer.Write(route.LengthMeters); writer.Write(route.EdgeIds.Count);
                foreach (var edge in route.EdgeIds) writer.Write(edge);
                writer.Write(route.Points.Count);
                foreach (var point in route.Points) { writer.Write(point.X); writer.Write(point.Z); }
            }
            using var hash = SHA256.Create();
            return BitConverter.ToString(hash.ComputeHash(stream.ToArray())).Replace("-", string.Empty);
        }

        private static bool 차량계열(동네이동수단 mode)
            => mode == 동네이동수단.Vehicle || mode == 동네이동수단.Motorcycle;
    }
}
