using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ssalddel.Contracts.Common.Metadata;

namespace Ssalddel.Simulation.Application
{
    [SsalddelEvidenceResponsibility(SsalddelEvidenceStage.E1, "경로 후보와 실패 사유를 공간 판본에 결속한다.",
        SubmoduleKey = SsalddelEvidenceSubmoduleKeys.E1공간계약, Boundary = "배차·도착·픽업·수령 결과가 아닌 사전 검사 결과다.")]
    public sealed class 동네이동경로후보
    {
        public string SnapshotId { get; }
        public string Revision { get; }
        public 동네이동수단 Mode { get; }
        public bool Found { get; }
        public string ReasonCode { get; }
        public IReadOnlyList<string> EdgeIds { get; }
        public IReadOnlyList<동네평면좌표> Points { get; }
        public double LengthMeters { get; }
        public bool RuntimeAuthorized => false;

        internal 동네이동경로후보(동네공간Snapshot snapshot, 동네이동수단 mode, bool found,
            string reason, IEnumerable<string> edgeIds, IEnumerable<동네평면좌표> points, double length)
        {
            SnapshotId = snapshot.StableId; Revision = snapshot.Revision; Mode = mode;
            Found = found; ReasonCode = reason; LengthMeters = length;
            EdgeIds = Array.AsReadOnly(edgeIds.ToArray()); Points = Array.AsReadOnly(points.ToArray());
        }
    }

    /// <summary>
    /// 명시된 연결만 사용하는 순수 Dijkstra 경로 후보 계산. 2D 거리이며 경사/교통/시간 비용이 아니다.
    /// 같은 거리에서는 노드 ID, 간선 ID의 ordinal 순서를 사용해 입력 배열 순서에 의존하지 않는다.
    /// </summary>
    [SsalddelCodeMetadata(SsalddelCodeFeatureKeys.SimulationWorldDerivation, SsalddelCodeLayer.Application,
        "정규화 동네 공간의 방향·통행 조건에 맞는 경로 후보를 반환한다.",
        StepKey = "application.neighborhood-route", DependsOnStepKeys = new[] { "application.neighborhood-import" },
        FlowOrder = 110, ExecutionStage = SsalddelCodeExecutionStage.Preview,
        ReadsFrom = SsalddelCodeDataScope.DerivedWorld, Effects = SsalddelCodeEffect.None,
        Boundary = "불변 공간 후보를 읽는 순수 계산이며 실제 배차·Actor 이동·주문 상태는 변경하지 않는다.")]
    [SsalddelEvidenceResponsibility(SsalddelEvidenceStage.E2, "검토된 방향·이동 수단에 따른 최단 경로 후보를 계산한다.",
        SubmoduleKey = SsalddelEvidenceSubmoduleKeys.E2공간실행,
        Boundary = "명령을 확정하거나 상태·Actor 위치를 변경하지 않는다. 실제 공간/통행 승인은 별도다.")]
    public sealed class 동네이동경로Engine
    {
        public 동네이동경로후보 탐색(동네공간Snapshot snapshot, string expectedRevision,
            string fromNodeId, string toNodeId, 동네이동수단 mode)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (snapshot.Revision != expectedRevision) throw new InvalidDataException("NeighborhoodRevisionMismatch");
            if (!Enum.IsDefined(typeof(동네이동수단), mode)) throw new ArgumentOutOfRangeException(nameof(mode));
            var nodes = snapshot.Nodes.ToDictionary(x => x.StableId, StringComparer.Ordinal);
            if (string.IsNullOrWhiteSpace(fromNodeId) || string.IsNullOrWhiteSpace(toNodeId)
                || !nodes.ContainsKey(fromNodeId) || !nodes.ContainsKey(toNodeId)) return 실패("NeighborhoodNodeNotFound");
            if ((mode == 동네이동수단.Vehicle || mode == 동네이동수단.Motorcycle)
                && (nodes[fromNodeId].Role == 동네노드역할.Entrance || nodes[toNodeId].Role == 동네노드역할.Entrance))
                return 실패("NeighborhoodVehicleEntranceForbidden");
            if (fromNodeId == toNodeId)
                return new 동네이동경로후보(snapshot, mode, true, "NeighborhoodAlreadyAtNode",
                    Array.Empty<string>(), new[] { nodes[fromNodeId].Position }, 0);

            var adjacency = nodes.Keys.ToDictionary(x => x, _ => new List<방향간선>(), StringComparer.Ordinal);
            foreach (var road in snapshot.Roads)
            {
                if (road.AccessReview != 동네통행검토.Reviewed || road.Direction == 동네도로방향.Unknown
                    || !road.Modes.Contains(mode)) continue;
                adjacency[road.FromNodeId].Add(new 방향간선(road, false));
                if (road.Direction == 동네도로방향.Both) adjacency[road.ToNodeId].Add(new 방향간선(road, true));
            }
            // Snapshot이 이미 정렬돼 있지만 경로 판정 책임 안에서도 순서를 명시한다.
            foreach (var edges in adjacency.Values)
                edges.Sort((a, b) => StringComparer.Ordinal.Compare(a.Road.StableId, b.Road.StableId));
            var distance = new Dictionary<string, double>(StringComparer.Ordinal) { [fromNodeId] = 0 };
            var previous = new Dictionary<string, 방향간선>(StringComparer.Ordinal);
            var settled = new HashSet<string>(StringComparer.Ordinal);
            var queue = new SortedSet<(double Cost, string Node)>(Comparer<(double Cost, string Node)>.Create((a, b) =>
            {
                var cost = a.Cost.CompareTo(b.Cost);
                return cost != 0 ? cost : StringComparer.Ordinal.Compare(a.Node, b.Node);
            })) { (0, fromNodeId) };

            while (queue.Count > 0)
            {
                var current = queue.Min;
                queue.Remove(current);
                if (!settled.Add(current.Node)) continue;
                if (current.Node == toNodeId) break;
                foreach (var edge in adjacency[current.Node])
                {
                    if (settled.Contains(edge.To)) continue;
                    var nextCost = current.Cost + edge.Road.LengthMeters;
                    if (distance.TryGetValue(edge.To, out var oldCost))
                    {
                        if (nextCost >= oldCost) continue;
                        queue.Remove((oldCost, edge.To));
                    }
                    distance[edge.To] = nextCost;
                    previous[edge.To] = edge;
                    queue.Add((nextCost, edge.To));
                }
            }
            if (!settled.Contains(toNodeId)) return 실패("NeighborhoodNoTraversableRoute");
            var chosen = new List<방향간선>();
            for (var node = toNodeId; node != fromNodeId;)
            {
                var edge = previous[node];
                chosen.Add(edge);
                node = edge.From;
            }
            chosen.Reverse();
            var points = new List<동네평면좌표> { nodes[fromNodeId].Position };
            foreach (var edge in chosen)
            {
                var line = edge.Reverse ? edge.Road.Points.Reverse().ToArray() : edge.Road.Points.ToArray();
                points.AddRange(line.Skip(1));
            }
            return new 동네이동경로후보(snapshot, mode, true, "NeighborhoodRouteCandidate",
                chosen.Select(x => x.Road.StableId), points, distance[toNodeId]);

            동네이동경로후보 실패(string reason) => new 동네이동경로후보(snapshot, mode, false, reason,
                Array.Empty<string>(), Array.Empty<동네평면좌표>(), 0);
        }

        private sealed class 방향간선
        {
            public 동네공간도로 Road { get; }
            public bool Reverse { get; }
            public string From => Reverse ? Road.ToNodeId : Road.FromNodeId;
            public string To => Reverse ? Road.FromNodeId : Road.ToNodeId;
            public 방향간선(동네공간도로 road, bool reverse) { Road = road; Reverse = reverse; }
        }
    }
}
