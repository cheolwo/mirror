using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using Ssalddel.Contracts.Common.Metadata;

namespace Ssalddel.Simulation.Application
{
    /// <summary>
    /// GIS 전처리 후 전용 JSON만 읽는다. RFC 7946 GeoJSON/SHP/NGI 파서나 재투영기가 아니다.
    /// 실제 좌표계 검증·재투영·클리핑은 전처리가 소유하며 여기서는 단위/범위/결속을 검사한다.
    /// </summary>
    [SsalddelCodeMetadata(SsalddelCodeFeatureKeys.SimulationWorldDerivation, SsalddelCodeLayer.Application,
        "전처리된 동네 도형을 검증하고 불변 공간 후보로 읽는다.",
        StepKey = "application.neighborhood-import", FlowOrder = 100,
        ExecutionStage = SsalddelCodeExecutionStage.Projection, ReadsFrom = SsalddelCodeDataScope.SharedPublicData,
        Effects = SsalddelCodeEffect.None,
        Boundary = "주어진 바이트만 읽는다. 실제 자료 확보·좌표 재투영·DB/World 적용은 별도다.")]
    [SsalddelEvidenceResponsibility(SsalddelEvidenceStage.E2, "정규화 공간 입력의 계보·단위·도형·연결을 검증한다.",
        SubmoduleKey = SsalddelEvidenceSubmoduleKeys.E2공간실행,
        Boundary = "순수 가져오기 후보이며 외부 요청·DB 저장·공간 승인·Runtime 발현을 하지 않는다.")]
    public sealed class 동네공간ImportService
    {
        public const int MaximumBytes = 4 * 1024 * 1024;
        public const int MaximumFeatures = 5000;
        public const int MaximumPoints = 100000;
        public const int MaximumPointsPerGeometry = 512;

        public 동네공간Snapshot 읽기(byte[] utf8, string expectedContentHashSha256)
        {
            if (utf8 == null) throw new ArgumentNullException(nameof(utf8));
            요구(utf8.Length > 0 && utf8.Length <= MaximumBytes, "NeighborhoodInputSizeInvalid");
            요구(해시형식(expectedContentHashSha256), "NeighborhoodExpectedHashRequired");
            // 호출자가 나중에 입력 버퍼를 수정하더라도 해시와 파싱 대상은 동일한 바이트다.
            var bytes = (byte[])utf8.Clone();
            string hash;
            using (var sha = SHA256.Create())
                hash = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "");
            요구(string.Equals(hash, expectedContentHashSha256, StringComparison.OrdinalIgnoreCase),
                "NeighborhoodContentHashMismatch");
            try
            {
                using var document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 24 });
                중복속성검사(document.RootElement);
                return 파싱(document.RootElement, hash);
            }
            catch (JsonException) { throw new InvalidDataException("NeighborhoodJsonInvalid"); }
            catch (InvalidOperationException) { throw new InvalidDataException("NeighborhoodJsonShapeInvalid"); }
            catch (KeyNotFoundException) { throw new InvalidDataException("NeighborhoodFieldMissing"); }
            catch (FormatException) { throw new InvalidDataException("NeighborhoodNumberInvalid"); }
        }

        private static 동네공간Snapshot 파싱(JsonElement root, string hash)
        {
            요구(문자열(root, "schemaVersion") == "neighborhood-geography.v1", "NeighborhoodSchemaUnsupported");
            var id = 문자열(root, "stableId");
            var source = 출처(root.GetProperty("provenance"));
            var coordinate = root.GetProperty("coordinates");
            요구(문자열(coordinate, "unit") == "m" && 문자열(coordinate, "axisOrder") == "EastingNorthing",
                "NeighborhoodCoordinateConventionUnsupported");
            var bounds = 수열(coordinate.GetProperty("bounds"), 4);
            요구(bounds[2] > bounds[0] && bounds[3] > bounds[1]
                && bounds[2] - bounds[0] <= 1000 && bounds[3] - bounds[1] <= 1000,
                "NeighborhoodBoundsInvalid");
            var originValues = 수열(coordinate.GetProperty("origin"), 2);
            요구(범위내(originValues[0], originValues[1], bounds), "NeighborhoodOriginOutsideBounds");
            var origin = new 동네평면좌표(originValues[0], originValues[1]);
            var features = root.GetProperty("features");
            요구(features.ValueKind == JsonValueKind.Array && features.GetArrayLength() > 0
                && features.GetArrayLength() <= MaximumFeatures, "NeighborhoodFeatureCountInvalid");
            var nodes = new List<동네공간노드>();
            var roads = new List<동네공간도로>();
            var buildings = new List<동네건물윤곽>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var pointCount = 0;
            foreach (var feature in features.EnumerateArray())
            {
                var featureId = 문자열(feature, "id");
                요구(ids.Add(featureId), "NeighborhoodDuplicateFeatureId");
                var geometry = feature.GetProperty("geometry");
                var properties = feature.GetProperty("properties");
                var kind = 문자열(feature, "kind");
                if (kind == "Node")
                {
                    요구(문자열(geometry, "type") == "Point", "NeighborhoodNodeGeometryInvalid");
                    var position = 위치(geometry.GetProperty("coordinates"), origin, bounds);
                    pointCount++;
                    nodes.Add(new 동네공간노드(featureId, position,
                        열거<동네노드역할>(properties, "role"), 선택문자열(properties, "buildingId")));
                }
                else if (kind == "Road")
                {
                    요구(문자열(geometry, "type") == "LineString", "NeighborhoodRoadGeometryInvalid");
                    var points = 위치열(geometry.GetProperty("coordinates"), origin, bounds);
                    요구(points.Count >= 2, "NeighborhoodRoadTooShort");
                    pointCount += points.Count;
                    var modesElement = properties.GetProperty("modes");
                    요구(modesElement.ValueKind == JsonValueKind.Array && modesElement.GetArrayLength() is >= 1 and <= 3,
                        "NeighborhoodTravelModesInvalid");
                    var modes = modesElement.EnumerateArray().Select(열거<동네이동수단>).ToArray();
                    요구(modes.Distinct().Count() == modes.Length, "NeighborhoodTravelModesInvalid");
                    var review = 열거<동네통행검토>(properties, "accessReview");
                    var evidence = 선택문자열(properties, "reviewEvidenceRef") ?? string.Empty;
                    요구(review != 동네통행검토.Reviewed || evidence.Length > 0, "NeighborhoodAccessEvidenceMissing");
                    roads.Add(new 동네공간도로(featureId, 문자열(properties, "fromNodeId"),
                        문자열(properties, "toNodeId"), 열거<동네도로방향>(properties, "direction"),
                        review, evidence, modes, points, 양수선택(properties, "widthMeters")));
                }
                else if (kind == "Building")
                {
                    요구(문자열(geometry, "type") == "Polygon", "NeighborhoodBuildingGeometryUnsupported");
                    var rings = geometry.GetProperty("coordinates");
                    요구(rings.ValueKind == JsonValueKind.Array && rings.GetArrayLength() == 1,
                        "NeighborhoodPolygonHolesUnsupported");
                    var ring = 위치열(rings[0], origin, bounds);
                    단순다각형검사(ring);
                    pointCount += ring.Count;
                    buildings.Add(new 동네건물윤곽(featureId, ring, 양수선택(properties, "heightMeters")));
                }
                else throw new InvalidDataException("NeighborhoodFeatureKindUnsupported");
                요구(pointCount <= MaximumPoints, "NeighborhoodPointCountExceeded");
            }

            요구(nodes.Count > 0 && roads.Count > 0, "NeighborhoodNetworkMissing");
            var nodeMap = nodes.ToDictionary(x => x.StableId, StringComparer.Ordinal);
            var buildingIds = new HashSet<string>(buildings.Select(x => x.StableId), StringComparer.Ordinal);
            foreach (var node in nodes)
                요구(node.Role == 동네노드역할.Entrance
                    ? node.BuildingId != null && buildingIds.Contains(node.BuildingId)
                    : node.BuildingId == null, "NeighborhoodEntranceBuildingInvalid");
            foreach (var road in roads)
            {
                요구(road.FromNodeId != road.ToNodeId && nodeMap.ContainsKey(road.FromNodeId)
                    && nodeMap.ContainsKey(road.ToNodeId), "NeighborhoodRoadNodeInvalid");
                var from = nodeMap[road.FromNodeId];
                var to = nodeMap[road.ToNodeId];
                // 오차를 숨기는 자동 snap은 하지 않는다. 전처리에서 같은 접점 좌표를 공유해야 한다.
                요구(from.Position.Equals(road.Points[0]) && to.Position.Equals(road.Points[road.Points.Count - 1]),
                    "NeighborhoodRoadEndpointMismatch");
                요구(!(road.Modes.Contains(동네이동수단.Vehicle)
                        || road.Modes.Contains(동네이동수단.Motorcycle))
                    || (from.Role != 동네노드역할.Entrance && to.Role != 동네노드역할.Entrance),
                    "NeighborhoodVehicleEntranceForbidden");
            }
            return new 동네공간Snapshot(id, hash, source, origin, bounds, nodes, roads, buildings);
        }

        private static 동네공간출처 출처(JsonElement value)
        {
            var kind = 문자열(value, "dataKind");
            요구(kind == "SyntheticFixture" || kind == "PublicData", "NeighborhoodDataKindInvalid");
            var synthetic = kind == "SyntheticFixture";
            var sourceId = 문자열(value, "sourceId");
            var datasetId = 문자열(value, "datasetId");
            var sourceCrs = 문자열(value, "sourceCrs");
            var projectedCrs = 문자열(value, "projectedCrs");
            요구(synthetic ? projectedCrs == "LOCAL:SYNTHETIC" && sourceCrs == "LOCAL:SYNTHETIC"
                : projectedCrs == "EPSG:5186", "NeighborhoodProjectedCrsUnsupported");
            // 실제 자료의 원본 CRS를 5186으로 가정하지 않는다. 전처리에서 출처 CRS와 변환 근거를 보존한다.
            요구(synthetic ? sourceId == "synthetic-fixture" : sourceId != "synthetic-fixture"
                && sourceCrs != "LOCAL:SYNTHETIC", "NeighborhoodProvenanceKindMismatch");
            var rawHash = 문자열(value, "rawContentHashSha256");
            요구(해시형식(rawHash), "NeighborhoodRawHashInvalid");
            var observedText = 문자열(value, "observedAtUtc");
            요구(DateTimeOffset.TryParse(observedText, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var observed) && observed.Offset == TimeSpan.Zero && observed.Year >= 2000
                && (observedText.EndsWith("Z", StringComparison.Ordinal) || observedText.EndsWith("+00:00", StringComparison.Ordinal)),
                "NeighborhoodSourceTimeInvalid");
            var licenseStatus = 문자열(value, "licenseReviewStatus");
            요구(synthetic ? licenseStatus == "SyntheticTestOnly"
                : licenseStatus == "PendingReview" || licenseStatus == "ReviewedForPrototype",
                "NeighborhoodLicenseReviewInvalid");
            return new 동네공간출처(sourceId, datasetId, 문자열(value, "sourceVersion"), observed,
                rawHash.ToUpperInvariant(), sourceCrs, projectedCrs,
                문자열(value, "normalizationEvidenceRef"), 문자열(value, "licenseEvidenceRef"), licenseStatus, synthetic);
        }

        private static List<동네평면좌표> 위치열(JsonElement value, 동네평면좌표 origin, double[] bounds)
        {
            요구(value.ValueKind == JsonValueKind.Array && value.GetArrayLength() <= MaximumPointsPerGeometry,
                "NeighborhoodGeometryPointCountInvalid");
            var points = value.EnumerateArray().Select(x => 위치(x, origin, bounds)).ToList();
            for (var i = 1; i < points.Count; i++)
                요구(points[i - 1].거리(points[i]) > 0, "NeighborhoodZeroLengthSegment");
            return points;
        }

        private static 동네평면좌표 위치(JsonElement value, 동네평면좌표 origin, double[] bounds)
        {
            var point = 수열(value, 2);
            요구(범위내(point[0], point[1], bounds), "NeighborhoodPointOutsideBounds");
            return new 동네평면좌표(point[0] - origin.X, point[1] - origin.Z);
        }

        private static bool 범위내(double x, double z, double[] b) => x >= b[0] && x <= b[2] && z >= b[1] && z <= b[3];

        private static double[] 수열(JsonElement value, int length)
        {
            요구(value.ValueKind == JsonValueKind.Array && value.GetArrayLength() == length,
                "NeighborhoodCoordinateDimensionInvalid");
            var numbers = value.EnumerateArray().Select(x => x.GetDouble()).ToArray();
            요구(numbers.All(x => !double.IsNaN(x) && !double.IsInfinity(x) && Math.Abs(x) <= 10000000),
                "NeighborhoodCoordinateInvalid");
            return numbers;
        }

        private static double? 양수선택(JsonElement value, string name)
        {
            if (!value.TryGetProperty(name, out var item) || item.ValueKind == JsonValueKind.Null) return null;
            var number = item.GetDouble();
            요구(!double.IsNaN(number) && !double.IsInfinity(number) && number > 0 && number <= 1000,
                "NeighborhoodDimensionInvalid");
            return number;
        }

        private static string 문자열(JsonElement value, string name)
        {
            var text = 선택문자열(value, name);
            요구(text != null, "NeighborhoodFieldMissing:" + name);
            return text!;
        }

        private static string? 선택문자열(JsonElement value, string name)
        {
            if (!value.TryGetProperty(name, out var item) || item.ValueKind == JsonValueKind.Null) return null;
            var text = item.GetString();
            요구(!string.IsNullOrWhiteSpace(text) && text!.Length <= 512 && text == text.Trim()
                && !text.Any(char.IsControl), "NeighborhoodTextInvalid:" + name);
            return text;
        }

        private static T 열거<T>(JsonElement value, string name) where T : struct
            => 열거<T>(value.GetProperty(name));

        private static T 열거<T>(JsonElement value) where T : struct
        {
            var text = value.GetString();
            요구(text != null && Enum.GetNames(typeof(T)).Contains(text), "NeighborhoodEnumInvalid");
            return (T)Enum.Parse(typeof(T), text!);
        }

        private static bool 해시형식(string value) => value != null && value.Length == 64
            && value.All(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'));

        private static void 중복속성검사(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (var property in element.EnumerateObject())
                {
                    요구(names.Add(property.Name), "NeighborhoodDuplicateJsonProperty");
                    중복속성검사(property.Value);
                }
            }
            else if (element.ValueKind == JsonValueKind.Array)
                foreach (var item in element.EnumerateArray()) 중복속성검사(item);
        }

        private static void 단순다각형검사(IReadOnlyList<동네평면좌표> ring)
        {
            요구(ring.Count >= 4 && ring[0].Equals(ring[ring.Count - 1]), "NeighborhoodPolygonNotClosed");
            var twiceArea = 0.0;
            for (var i = 0; i < ring.Count - 1; i++)
            {
                twiceArea += ring[i].X * ring[i + 1].Z - ring[i + 1].X * ring[i].Z;
                for (var j = i + 1; j < ring.Count - 1; j++)
                {
                    if (j == i + 1 || (i == 0 && j == ring.Count - 2)) continue;
                    요구(!교차(ring[i], ring[i + 1], ring[j], ring[j + 1]), "NeighborhoodPolygonSelfIntersection");
                }
                // 인접한 변이 같은 선을 되짚는 윤곽도 거부한다.
                var before = ring[(i + ring.Count - 2) % (ring.Count - 1)];
                var current = ring[i];
                var after = ring[i + 1];
                요구(외적(before, current, after) != 0
                    || (before.X - current.X) * (after.X - current.X)
                       + (before.Z - current.Z) * (after.Z - current.Z) <= 0,
                    "NeighborhoodPolygonSelfIntersection");
            }
            요구(Math.Abs(twiceArea) > 0.000001, "NeighborhoodPolygonAreaInvalid");
        }

        private static double 외적(동네평면좌표 a, 동네평면좌표 b, 동네평면좌표 c)
            => (b.X - a.X) * (c.Z - a.Z) - (b.Z - a.Z) * (c.X - a.X);

        private static bool 교차(동네평면좌표 a, 동네평면좌표 b, 동네평면좌표 c, 동네평면좌표 d)
        {
            var abC = 외적(a, b, c); var abD = 외적(a, b, d);
            var cdA = 외적(c, d, a); var cdB = 외적(c, d, b);
            return (Math.Sign(abC) * Math.Sign(abD) < 0 && Math.Sign(cdA) * Math.Sign(cdB) < 0)
                || (abC == 0 && 선분내(a, b, c)) || (abD == 0 && 선분내(a, b, d))
                || (cdA == 0 && 선분내(c, d, a)) || (cdB == 0 && 선분내(c, d, b));
        }

        private static bool 선분내(동네평면좌표 a, 동네평면좌표 b, 동네평면좌표 p)
            => p.X >= Math.Min(a.X, b.X) && p.X <= Math.Max(a.X, b.X)
                && p.Z >= Math.Min(a.Z, b.Z) && p.Z <= Math.Max(a.Z, b.Z);

        private static void 요구(bool condition, string code)
        { if (!condition) throw new InvalidDataException(code); }
    }
}
