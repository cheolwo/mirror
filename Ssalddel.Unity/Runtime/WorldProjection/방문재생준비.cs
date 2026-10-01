using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Ssalddel.Contracts.Common.Metadata;

namespace Ssalddel.Unity.Data.WorldProjection
{
    /// <summary>신뢰된 위치 검토 경로가 제공하는 결속 근거입니다. 이 값 자체가 지오코딩이나 위치 검증을 수행하지는 않습니다.</summary>
    [SsalddelEvidenceResponsibility(SsalddelEvidenceStage.E3,
        "기록 판본과 공개 가게 위치의 명시적인 결속 근거를 불변 값으로 전달한다.",
        Boundary = "근거의 실제 조사, 현재 경계 승인, 운영 상태 또는 Unity 배치 증거가 아니다.",
        SubmoduleKey = SsalddelEvidenceSubmoduleKeys.E3계약회귀)]
    public sealed class 방문위치결속
    {
        public 방문위치결속(string recordId, string recordRevision, string visitId, string storeId,
            string address, double latitude, double longitude, string administrativeAreaStableId,
            string coordinateFrameKey, double x, double z, string coordinateRevision,
            string boundaryRevision, string evidenceReference)
        {
            RecordId = recordId;
            RecordRevision = recordRevision;
            VisitId = visitId;
            StoreId = storeId;
            Address = address;
            Latitude = latitude;
            Longitude = longitude;
            AdministrativeAreaStableId = administrativeAreaStableId;
            CoordinateFrameKey = coordinateFrameKey;
            X = x;
            Z = z;
            CoordinateRevision = coordinateRevision;
            BoundaryRevision = boundaryRevision;
            EvidenceReference = evidenceReference;
        }

        public string RecordId { get; }
        public string RecordRevision { get; }
        public string VisitId { get; }
        public string StoreId { get; }
        public string Address { get; }
        public double Latitude { get; }
        public double Longitude { get; }
        public string AdministrativeAreaStableId { get; }
        public string CoordinateFrameKey { get; }
        public double X { get; }
        public double Z { get; }
        public string CoordinateRevision { get; }
        public string BoundaryRevision { get; }
        public string EvidenceReference { get; }
    }

    [SsalddelEvidenceResponsibility(SsalddelEvidenceStage.E3,
        "한 방문의 확인된 공통 좌표와 표시 사실을 불변 값으로 보존한다.",
        Boundary = "실제 주행선, 픽업 완료, 건물 출입구 또는 실제 화면의 증거가 아니다.",
        SubmoduleKey = SsalddelEvidenceSubmoduleKeys.E3Unity소비자회귀)]
    public sealed class 준비된방문
    {
        internal 준비된방문(RegionalPickupVisit visit, 방문위치결속 binding)
        {
            VisitId = visit.VisitId;
            StoreId = visit.StoreId;
            Sequence = visit.Sequence;
            Name = visit.Name ?? string.Empty;
            Menu = visit.Menu ?? string.Empty;
            AdministrativeAreaStableId = binding.AdministrativeAreaStableId;
            X = binding.X;
            Z = binding.Z;
            CoordinateFrameKey = binding.CoordinateFrameKey;
        }

        public string VisitId { get; }
        public string StoreId { get; }
        public int Sequence { get; }
        public string Name { get; }
        public string Menu { get; }
        public string AdministrativeAreaStableId { get; }
        public double X { get; }
        public double Z { get; }
        public string CoordinateFrameKey { get; }
    }

    [SsalddelEvidenceResponsibility(SsalddelEvidenceStage.E3,
        "기록 판본과 준비 결과를 복제 불가능한 변경 경계로 묶어 소비자에 전달한다.",
        Boundary = "자료 보관, 전송 수신, Unity 표현 완료 또는 E 단계 승격이 아니다.",
        SubmoduleKey = SsalddelEvidenceSubmoduleKeys.E3Unity소비자회귀)]
    public sealed class 방문재생준비Result
    {
        internal 방문재생준비Result(string recordId, string recordRevision, string fingerprint,
            IEnumerable<준비된방문> visits, IEnumerable<string> diagnostics)
        {
            RecordId = recordId;
            RecordRevision = recordRevision;
            InputFingerprint = fingerprint;
            Diagnostics = Array.AsReadOnly(diagnostics.Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal).ToArray());
            CanReplay = Diagnostics.Count == 0;
            // 중간 방문의 결손을 1→3 연결로 감출 수 없도록 실패 시 전체 재생 목록을 비웁니다.
            Visits = Array.AsReadOnly(CanReplay ? visits.ToArray() : Array.Empty<준비된방문>());
        }

        public bool CanReplay { get; }
        public string RecordId { get; }
        public string RecordRevision { get; }
        public string InputFingerprint { get; }
        public IReadOnlyList<준비된방문> Visits { get; }
        public IReadOnlyList<string> Diagnostics { get; }
    }

    [SsalddelEvidenceResponsibility(SsalddelEvidenceStage.E3,
        "비운영 방문의 순서, 명시적 위치 결속과 동일 입력 안정성을 순수 C#으로 검사한다.",
        Boundary = "위치 조사, 권한 확인, DB, 전송, 실제 Unity 배치나 운영 완료 책임이 아니다.",
        SubmoduleKey = SsalddelEvidenceSubmoduleKeys.E3결정성검증)]
    public static class 방문재생준비
    {
        private const int 최대방문수 = 10000;

        /// <summary>전체 기록의 1부터 시작하는 방문 순서를 준비합니다. 부분 구간 선택은 준비된 결과의 소비자 책임입니다.</summary>
        public static 방문재생준비Result Prepare(RegionalPickupRecord? record, string recordRevision,
            IReadOnlyList<방문위치결속>? locationBindings)
        {
            var diagnostics = new List<string>();
            var ready = new List<준비된방문>();
            if (record == null || record.SchemaVersion != "delivery-visit-record.v2"
                || string.IsNullOrWhiteSpace(record.RecordId) || !record.LocalReviewOnly
                || record.IsOperationalState || record.Visits == null
                || record.Visits.Length == 0 || record.Visits.Length > 최대방문수)
                return 거부(record?.RecordId, recordRevision, "RegionalPickupRecordInvalid");
            if (string.IsNullOrWhiteSpace(recordRevision))
                return 거부(record.RecordId, recordRevision, "RecordRevisionRequired");
            if (locationBindings == null || locationBindings.Count > 최대방문수)
                return 거부(record.RecordId, recordRevision, "VisitLocationBindingsInvalid");

            // 원본 DTO와 배열을 외부가 수정해도 이미 준비한 결과와 fingerprint는 변하지 않습니다.
            var source = 복제(record);
            var bindings = locationBindings.ToArray();
            var fingerprint = 입력Fingerprint(source, recordRevision, bindings);
            var visitsById = new HashSet<string>(StringComparer.Ordinal);
            var sequences = new HashSet<int>();
            var bindingById = new Dictionary<string, 방문위치결속>(StringComparer.Ordinal);
            foreach (var binding in bindings)
            {
                if (binding == null || string.IsNullOrWhiteSpace(binding.VisitId))
                {
                    diagnostics.Add("VisitLocationBindingInvalid");
                    continue;
                }
                if (bindingById.ContainsKey(binding.VisitId))
                    diagnostics.Add("DuplicateVisitLocationBinding:" + binding.VisitId);
                else bindingById.Add(binding.VisitId, binding);
            }

            string? frame = null;
            var storeLocations = new Dictionary<string, 방문위치결속>(StringComparer.Ordinal);
            foreach (var visit in source.Visits.OrderBy(value => value?.Sequence ?? int.MinValue)
                         .ThenBy(value => value?.VisitId, StringComparer.Ordinal))
            {
                if (visit == null || string.IsNullOrWhiteSpace(visit.VisitId)
                    || string.IsNullOrWhiteSpace(visit.StoreId) || visit.EventKind != "RecordedVisit"
                    || visit.PickupCompletionConfirmed)
                {
                    diagnostics.Add("RegionalPickupVisitInvalid");
                    continue;
                }
                if (!visitsById.Add(visit.VisitId)) diagnostics.Add("DuplicateVisitId:" + visit.VisitId);
                if (visit.Sequence <= 0) diagnostics.Add("VisitSequenceInvalid:" + visit.VisitId);
                if (!sequences.Add(visit.Sequence)) diagnostics.Add("DuplicateVisitSequence");
                if (!bindingById.TryGetValue(visit.VisitId, out var binding))
                {
                    diagnostics.Add("VisitLocationBindingMissing:" + visit.VisitId);
                    continue;
                }
                var issue = 결속검사(source.RecordId, recordRevision, visit, binding);
                if (issue != null)
                {
                    diagnostics.Add(issue + ":" + visit.VisitId);
                    continue;
                }
                if (frame != null && frame != binding.CoordinateFrameKey)
                    diagnostics.Add("CoordinateFrameMismatch");
                frame = frame ?? binding.CoordinateFrameKey;
                if (storeLocations.TryGetValue(visit.StoreId, out var previous))
                {
                    if (!동일매장위치(previous, binding))
                        diagnostics.Add("StoreLocationConflict:" + visit.StoreId);
                }
                else storeLocations.Add(visit.StoreId, binding);
                ready.Add(new 준비된방문(visit, binding));
            }
            for (var sequence = 1; sequence <= source.Visits.Length; sequence++)
                if (!sequences.Contains(sequence)) diagnostics.Add("VisitSequenceMissing");
            foreach (var id in bindingById.Keys)
                if (!visitsById.Contains(id)) diagnostics.Add("VisitLocationBindingUnknown:" + id);
            return new 방문재생준비Result(source.RecordId, recordRevision, fingerprint, ready, diagnostics);
        }

        private static 방문재생준비Result 거부(string? recordId, string? revision, string diagnostic)
            => new 방문재생준비Result(recordId ?? string.Empty, revision ?? string.Empty, string.Empty,
                Array.Empty<준비된방문>(), new[] { diagnostic });

        private static string? 결속검사(string recordId, string revision, RegionalPickupVisit visit, 방문위치결속 binding)
        {
            // v2의 double 기본값은 좌표 누락과 구분되지 않습니다. 0축 값은 보수적으로 별도 확인을 요구합니다.
            if (!위경도유효(visit.Latitude, visit.Longitude) || visit.Latitude == 0 || visit.Longitude == 0
                || string.IsNullOrWhiteSpace(visit.CoordinateStatus)
                || string.Equals(visit.CoordinateStatus, "Missing", StringComparison.OrdinalIgnoreCase))
                return "VisitCoordinatesMissingOrInvalid";
            if (string.IsNullOrWhiteSpace(visit.Address)) return "VisitAddressRequired";
            if (visit.AreaBindingStatus != "Verified" || string.IsNullOrWhiteSpace(visit.AreaBindingEvidence)
                || string.IsNullOrWhiteSpace(visit.AdministrativeAreaStableId)) return "VisitAreaUnresolved";
            if (binding.RecordId != recordId || binding.RecordRevision != revision
                || binding.VisitId != visit.VisitId || binding.StoreId != visit.StoreId
                || binding.Address != visit.Address || binding.Latitude != visit.Latitude
                || binding.Longitude != visit.Longitude
                || binding.AdministrativeAreaStableId != visit.AdministrativeAreaStableId)
                return "VisitLocationBindingMismatch";
            if (string.IsNullOrWhiteSpace(binding.CoordinateRevision)
                || string.IsNullOrWhiteSpace(binding.BoundaryRevision)
                || string.IsNullOrWhiteSpace(binding.EvidenceReference)) return "VisitLocationEvidenceRequired";
            if (string.IsNullOrWhiteSpace(binding.CoordinateFrameKey)
                || !유한값(binding.X) || !유한값(binding.Z)) return "VisitCommonCoordinatesInvalid";
            return null;
        }

        private static bool 동일매장위치(방문위치결속 a, 방문위치결속 b)
            => a.Address == b.Address && a.Latitude == b.Latitude && a.Longitude == b.Longitude
               && a.AdministrativeAreaStableId == b.AdministrativeAreaStableId
               && a.CoordinateFrameKey == b.CoordinateFrameKey && a.X == b.X && a.Z == b.Z;

        private static bool 유한값(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        private static bool 위경도유효(double latitude, double longitude)
            => 유한값(latitude) && 유한값(longitude) && latitude >= -90 && latitude <= 90
               && longitude >= -180 && longitude <= 180;

        private static RegionalPickupRecord 복제(RegionalPickupRecord source)
            => new RegionalPickupRecord
            {
                SchemaVersion = source.SchemaVersion, RecordId = source.RecordId,
                LocalReviewOnly = source.LocalReviewOnly, IsOperationalState = source.IsOperationalState,
                Visits = source.Visits.Select(v => v == null ? null! : new RegionalPickupVisit
                {
                    VisitId = v.VisitId, StoreId = v.StoreId, Name = v.Name, Menu = v.Menu,
                    Sequence = v.Sequence, Latitude = v.Latitude, Longitude = v.Longitude,
                    Address = v.Address, AddressStatus = v.AddressStatus, CoordinateStatus = v.CoordinateStatus,
                    MenuStatus = v.MenuStatus, VerificationNote = v.VerificationNote,
                    SourceUrls = v.SourceUrls == null ? null! : (string[])v.SourceUrls.Clone(),
                    RegionHint = v.RegionHint, AdministrativeAreaStableId = v.AdministrativeAreaStableId,
                    StationModuleStableId = v.StationModuleStableId, AreaBindingStatus = v.AreaBindingStatus,
                    AreaBindingEvidence = v.AreaBindingEvidence, RecordedAt = v.RecordedAt,
                    EventKind = v.EventKind, PickupCompletionConfirmed = v.PickupCompletionConfirmed
                }).ToArray()
            };

        private static string 입력Fingerprint(RegionalPickupRecord source, string revision, 방문위치결속[] bindings)
        {
            // 길이 접두사가 있는 이진 인코딩으로 구분자 충돌과 현재 문화권/실행 시각 영향을 배제합니다.
            using (var bytes = new MemoryStream())
            {
                using (var writer = new BinaryWriter(bytes, Encoding.UTF8, true))
                {
                    문자열(writer, "visit-playback-preparation.v1");
                    문자열(writer, source.SchemaVersion); 문자열(writer, source.RecordId); 문자열(writer, revision);
                    writer.Write(source.LocalReviewOnly); writer.Write(source.IsOperationalState);
                    writer.Write(source.Visits.Length);
                    foreach (var visit in source.Visits.OrderBy(v => v?.Sequence ?? int.MinValue)
                                 .ThenBy(v => v?.VisitId, StringComparer.Ordinal))
                    {
                        writer.Write(visit != null);
                        if (visit == null) continue;
                        문자열(writer, visit.VisitId); 문자열(writer, visit.StoreId); writer.Write(visit.Sequence);
                        문자열(writer, visit.Name); 문자열(writer, visit.Menu); 문자열(writer, visit.Address);
                        writer.Write(visit.Latitude); writer.Write(visit.Longitude);
                        문자열(writer, visit.AddressStatus); 문자열(writer, visit.CoordinateStatus);
                        문자열(writer, visit.MenuStatus); 문자열(writer, visit.VerificationNote);
                        문자열(writer, visit.RegionHint); 문자열(writer, visit.AdministrativeAreaStableId);
                        문자열(writer, visit.StationModuleStableId); 문자열(writer, visit.AreaBindingStatus);
                        문자열(writer, visit.AreaBindingEvidence); 문자열(writer, visit.RecordedAt);
                        문자열(writer, visit.EventKind); writer.Write(visit.PickupCompletionConfirmed);
                        writer.Write(visit.SourceUrls?.Length ?? -1);
                        if (visit.SourceUrls != null) foreach (var url in visit.SourceUrls) 문자열(writer, url);
                    }
                    writer.Write(bindings.Length);
                    foreach (var binding in bindings.OrderBy(b => b?.VisitId, StringComparer.Ordinal))
                    {
                        writer.Write(binding != null);
                        if (binding == null) continue;
                        문자열(writer, binding.RecordId); 문자열(writer, binding.RecordRevision);
                        문자열(writer, binding.VisitId); 문자열(writer, binding.StoreId); 문자열(writer, binding.Address);
                        writer.Write(binding.Latitude); writer.Write(binding.Longitude);
                        문자열(writer, binding.AdministrativeAreaStableId); 문자열(writer, binding.CoordinateFrameKey);
                        writer.Write(binding.X); writer.Write(binding.Z);
                        문자열(writer, binding.CoordinateRevision); 문자열(writer, binding.BoundaryRevision);
                        문자열(writer, binding.EvidenceReference);
                    }
                }
                using (var sha256 = SHA256.Create())
                    return BitConverter.ToString(sha256.ComputeHash(bytes.ToArray())).Replace("-", string.Empty);
            }
        }

        private static void 문자열(BinaryWriter writer, string? value)
        {
            writer.Write(value != null);
            if (value != null) writer.Write(value);
        }
    }
}
