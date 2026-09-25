using System;
using System.Collections.Generic;
using System.Linq;
namespace Ssalddel.Unity.Data.WorldProjection
{
    /// <summary>영상 참조 없이 공유하는 과거 방문. 지역 소속은 별도 근거로 확인한다.</summary>
    public sealed class RegionalPickupVisit
    {
        public string VisitId { get; set; } = string.Empty;
        public string StoreId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Menu { get; set; } = string.Empty;
        public int Sequence { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string Address { get; set; } = string.Empty;
        public string AddressStatus { get; set; } = string.Empty;
        public string CoordinateStatus { get; set; } = string.Empty;
        public string MenuStatus { get; set; } = string.Empty;
        public string VerificationNote { get; set; } = string.Empty;
        public string[] SourceUrls { get; set; } = Array.Empty<string>();
        public string RegionHint { get; set; } = string.Empty;
        public string? AdministrativeAreaStableId { get; set; }
        public string? StationModuleStableId { get; set; }
        public string AreaBindingStatus { get; set; } = "Unresolved";
        public string? AreaBindingEvidence { get; set; }
        public string? RecordedAt { get; set; }
        public string EventKind { get; set; } = "RecordedVisit";
        public bool PickupCompletionConfirmed { get; set; }
    }
    public sealed class RegionalPickupRecord
    {
        public string SchemaVersion { get; set; } = string.Empty;
        public string RecordId { get; set; } = string.Empty;
        public bool LocalReviewOnly { get; set; }
        public bool IsOperationalState { get; set; }
        public RegionalPickupVisit[] Visits { get; set; } = Array.Empty<RegionalPickupVisit>();
    }
    public sealed class RegionalPickupBindingResult
    {
        public RegionalPickupVisit[] Visits { get; set; } = Array.Empty<RegionalPickupVisit>();
        public int UnresolvedCount { get; set; }
        public int OtherModuleCount { get; set; }
        public int UniqueStoreCount => Visits.Select(v=>v.StoreId).Distinct(StringComparer.Ordinal).Count();
    }
    public static class RegionalPickupRecordBinding
    {
        public static RegionalPickupBindingResult Bind(string moduleId, bool stationModule, RegionalPickupRecord record)
        {
            if(string.IsNullOrWhiteSpace(moduleId))throw new ArgumentException("ModuleIdRequired");
            if(record==null || record.SchemaVersion!="delivery-visit-record.v2" || string.IsNullOrWhiteSpace(record.RecordId) ||
                !record.LocalReviewOnly || record.IsOperationalState || record.Visits==null || record.Visits.Length>10000)
                throw new ArgumentException("RegionalPickupRecordInvalid");
            var ids=new HashSet<string>(StringComparer.Ordinal);
            var result=new RegionalPickupBindingResult();var matching=new List<RegionalPickupVisit>();
            foreach(var v in record.Visits)
            {
                if(v==null || string.IsNullOrWhiteSpace(v.VisitId) || !ids.Add(v.VisitId) || string.IsNullOrWhiteSpace(v.StoreId) ||
                    v.EventKind!="RecordedVisit" || v.PickupCompletionConfirmed)throw new ArgumentException("RegionalPickupVisitInvalid");
                var area=stationModule?v.StationModuleStableId:v.AdministrativeAreaStableId;
                if(v.AreaBindingStatus!="Verified" || string.IsNullOrWhiteSpace(v.AreaBindingEvidence) || string.IsNullOrWhiteSpace(area))result.UnresolvedCount++;
                else if(string.Equals(area,moduleId,StringComparison.Ordinal))matching.Add(v);
                else result.OtherModuleCount++;
            }
            result.Visits=matching.ToArray();return result;
        }
    }
}
