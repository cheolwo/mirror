using System;
using System.Collections.Generic;
using System.Linq;
using Ssalddel.Contracts.Common.Metadata;

namespace Ssalddel.Simulation.Application
{
    public enum 동네이동수단 { Vehicle = 0, Pedestrian = 1, Motorcycle = 2 }
    public enum 동네통행검토 { Unknown, Reviewed, Blocked }
    public enum 동네도로방향 { Unknown, Forward, Both }
    public enum 동네노드역할 { Junction, VehicleStop, Entrance }

    /// <summary>동쪽 X, 북쪽 Z, 미터 단위의 지역 평면 좌표. 높이/지지면은 포함하지 않는다.</summary>
    [SsalddelEvidenceResponsibility(SsalddelEvidenceStage.E1, "지역 평면 좌표의 축과 단위를 고정한다.",
        SubmoduleKey = SsalddelEvidenceSubmoduleKeys.E1공간계약, Boundary = "Unity 배치·높이·실제 통행 가능성을 확정하지 않는다.")]
    public readonly struct 동네평면좌표 : IEquatable<동네평면좌표>
    {
        public double X { get; }
        public double Z { get; }
        internal 동네평면좌표(double x, double z) { X = x; Z = z; }
        public double 거리(동네평면좌표 other)
            => Math.Sqrt((X - other.X) * (X - other.X) + (Z - other.Z) * (Z - other.Z));
        public bool Equals(동네평면좌표 other) => X == other.X && Z == other.Z;
        public override bool Equals(object? obj) => obj is 동네평면좌표 other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Z);
    }

    [SsalddelEvidenceResponsibility(SsalddelEvidenceStage.E1, "원본과 정규화 좌표의 데이터 계보를 보존한다.",
        SubmoduleKey = SsalddelEvidenceSubmoduleKeys.E1공간계약, Boundary = "출처 문자열·자체 신고 검토 상태는 권리나 공간 승인 증거를 대신하지 않는다.")]
    public sealed class 동네공간출처
    {
        public string SourceId { get; }
        public string DatasetId { get; }
        public string SourceVersion { get; }
        public DateTimeOffset ObservedAtUtc { get; }
        public string RawContentHashSha256 { get; }
        public string SourceCrs { get; }
        public string ProjectedCrs { get; }
        public string NormalizationEvidenceRef { get; }
        public string LicenseEvidenceRef { get; }
        public string LicenseReviewStatus { get; }
        public bool IsSynthetic { get; }

        internal 동네공간출처(string sourceId, string datasetId, string sourceVersion,
            DateTimeOffset observed, string rawHash, string sourceCrs, string projectedCrs,
            string normalizationRef, string licenseRef, string licenseStatus, bool synthetic)
        {
            SourceId = sourceId; DatasetId = datasetId; SourceVersion = sourceVersion;
            ObservedAtUtc = observed; RawContentHashSha256 = rawHash; SourceCrs = sourceCrs;
            ProjectedCrs = projectedCrs; NormalizationEvidenceRef = normalizationRef;
            LicenseEvidenceRef = licenseRef; LicenseReviewStatus = licenseStatus; IsSynthetic = synthetic;
        }
    }

    [SsalddelEvidenceResponsibility(SsalddelEvidenceStage.E1, "명시적으로 주어진 이동망 접점을 보존한다.",
        SubmoduleKey = SsalddelEvidenceSubmoduleKeys.E1공간계약, Boundary = "점의 가까움이나 건물 윤곽으로 출입구·연결을 생성하지 않는다.")]
    public sealed class 동네공간노드
    {
        public string StableId { get; }
        public 동네평면좌표 Position { get; }
        public 동네노드역할 Role { get; }
        public string? BuildingId { get; }
        internal 동네공간노드(string id, 동네평면좌표 position, 동네노드역할 role, string? buildingId)
        { StableId = id; Position = position; Role = role; BuildingId = buildingId; }
    }

    [SsalddelEvidenceResponsibility(SsalddelEvidenceStage.E1, "도로·도보 접근의 명시적 방향과 통행 검토를 보존한다.",
        SubmoduleKey = SsalddelEvidenceSubmoduleKeys.E1공간계약, Boundary = "검토 사본이며 실제 지형·차폭·충돌 검증을 대신하지 않는다.")]
    public sealed class 동네공간도로
    {
        public string StableId { get; }
        public string FromNodeId { get; }
        public string ToNodeId { get; }
        public 동네도로방향 Direction { get; }
        public 동네통행검토 AccessReview { get; }
        public string ReviewEvidenceRef { get; }
        public IReadOnlyList<동네이동수단> Modes { get; }
        public IReadOnlyList<동네평면좌표> Points { get; }
        public double LengthMeters { get; }
        public double? WidthMeters { get; }

        internal 동네공간도로(string id, string from, string to, 동네도로방향 direction,
            동네통행검토 access, string reviewRef, IEnumerable<동네이동수단> modes,
            IEnumerable<동네평면좌표> points, double? width)
        {
            StableId = id; FromNodeId = from; ToNodeId = to; Direction = direction;
            AccessReview = access; ReviewEvidenceRef = reviewRef; WidthMeters = width;
            Modes = Array.AsReadOnly(modes.ToArray()); Points = Array.AsReadOnly(points.ToArray());
            for (var i = 1; i < Points.Count; i++) LengthMeters += Points[i - 1].거리(Points[i]);
        }
    }

    [SsalddelEvidenceResponsibility(SsalddelEvidenceStage.E1, "원본 건물 윤곽과 높이 미확인을 구별한다.",
        SubmoduleKey = SsalddelEvidenceSubmoduleKeys.E1공간계약, Boundary = "건물 중심 출입구·정차점·평지·실존 업소를 생성하지 않는다.")]
    public sealed class 동네건물윤곽
    {
        public string StableId { get; }
        public IReadOnlyList<동네평면좌표> Ring { get; }
        public double? HeightMeters { get; }
        internal 동네건물윤곽(string id, IEnumerable<동네평면좌표> ring, double? height)
        { StableId = id; Ring = Array.AsReadOnly(ring.ToArray()); HeightMeters = height; }
    }

    /// <summary>사전 검사용 불변 공간 사본. 실제 자료여도 WI/공간 승인 없이 런타임으로 승격하지 않는다.</summary>
    [SsalddelEvidenceResponsibility(SsalddelEvidenceStage.E1, "공간 사본을 정확한 파일 판본과 출처에 결속한다.",
        SubmoduleKey = SsalddelEvidenceSubmoduleKeys.E1공간계약, Boundary = "후보 계산 전용이며 Scene·세션·운영 데이터에 자동 적용하지 않는다.")]
    public sealed class 동네공간Snapshot
    {
        public string StableId { get; }
        public string Revision { get; }
        public 동네공간출처 Source { get; }
        public 동네평면좌표 ProjectedOrigin { get; }
        public IReadOnlyList<double> ProjectedBounds { get; }
        public IReadOnlyList<동네공간노드> Nodes { get; }
        public IReadOnlyList<동네공간도로> Roads { get; }
        public IReadOnlyList<동네건물윤곽> Buildings { get; }
        public bool RuntimeAuthorized => false;

        internal 동네공간Snapshot(string id, string revision, 동네공간출처 source,
            동네평면좌표 origin, IEnumerable<double> bounds, IEnumerable<동네공간노드> nodes,
            IEnumerable<동네공간도로> roads, IEnumerable<동네건물윤곽> buildings)
        {
            StableId = id; Revision = revision; Source = source; ProjectedOrigin = origin;
            ProjectedBounds = Array.AsReadOnly(bounds.ToArray());
            Nodes = Array.AsReadOnly(nodes.OrderBy(x => x.StableId, StringComparer.Ordinal).ToArray());
            Roads = Array.AsReadOnly(roads.OrderBy(x => x.StableId, StringComparer.Ordinal).ToArray());
            Buildings = Array.AsReadOnly(buildings.OrderBy(x => x.StableId, StringComparer.Ordinal).ToArray());
        }
    }
}
