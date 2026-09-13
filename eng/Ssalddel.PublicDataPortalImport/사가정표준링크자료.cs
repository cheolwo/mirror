using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Domain.PublicData;
using Ssalddel.Infrastructure.Persistence.PublicData;
using 살뜰.Services.External.PublicData.Korea;

// 동결된 사가정 구간 표본만 기존 수집 저장소에 등록한다. 전국 적재/도로 결합/게임 적용 금지.
internal static class 사가정표준링크자료
{
    private const string Relative="artifacts/local/public-data/sagajeong-nodelink-20260912-r1";
    private const string Source="its-standard-nodelink";
    private const string Dataset="data-go-kr-15025526-sagajeong";
    private const string ArchiveHash="5bbf5a01d677b6dcb941cc5954fcc256d6ded60d96f95336a23fc2b53b39d4e4";
    private const string SubsetHash="02e8794a9cd8bdbf2193da1471b989a67a63c13901b73bd388c7cb57c11a8db7";
    private const string Limit="PrivateReviewOnly;NoPublication;NoRuntime;NoOsmJoin;NoWidthInference;DirectedLinkNotWholeRoad;NoSurveyAccuracyGuarantee";
    private static void Require(bool ok,string code){if(!ok)throw new InvalidDataException(code);}
    private static string Hash(string file){using var stream=File.OpenRead(file);return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();}
    private static readonly JsonSerializerOptions Json=new(){WriteIndented=true};

    public static async Task RunAsync(string mode,string root,Dictionary<string,object?> result)
    {
        Require(mode is "self-test" or "preview" or "apply" or "verify","LinkModeInvalid");
        var folder=Path.Combine(root,Relative);
        Require(Hash(Path.Combine(folder,"nodelink.zip"))==ArchiveHash,"LinkArchiveHashMismatch");
        Require(Hash(Path.Combine(folder,"sagajeong-links.json"))==SubsetHash,"LinkSubsetHashMismatch");
        using var acquisition=JsonDocument.Parse(File.ReadAllText(Path.Combine(folder,"acquisition.json")));
        Require(acquisition.RootElement.GetProperty("status").GetString()=="Downloaded","LinkAcquisitionIncomplete");
        foreach(var file in acquisition.RootElement.GetProperty("files").EnumerateArray())
        {
            var name=file.GetProperty("file").GetString()!;
            Require(Path.GetFileName(name)==name && !name.Contains('/') && !name.Contains('\\'),"LinkReceiptPathInvalid");
            Require(Hash(Path.Combine(folder,name))==file.GetProperty("sha256").GetString()!.ToLowerInvariant(),"LinkReceiptHashMismatch");
        }
        using var license=JsonDocument.Parse(File.ReadAllText(Path.Combine(folder,"license-metadata.json")));
        Require(license.RootElement.GetProperty("license").GetString()=="이용허락범위 제한 없음","LinkRightsChanged");
        var collected=acquisition.RootElement.GetProperty("files").EnumerateArray().Single(x=>x.GetProperty("file").GetString()=="nodelink.zip").GetProperty("collectedAtUtc").GetDateTimeOffset();
        collected=DateTimeOffset.FromUnixTimeMilliseconds(collected.ToUnixTimeMilliseconds());
        using var subset=JsonDocument.Parse(File.ReadAllText(Path.Combine(folder,"sagajeong-links.json")));
        var rows=Parse(subset.RootElement,collected);
        result["selectedRows"]=rows.Count;
        if(mode=="self-test")
        {
            Require(rows.All(x=>x.QualityCode=="PendingHumanReview" && x.LimitationCode==Limit),"LinkReviewBoundary");
            Require(rows.Select(x=>x.TextValue).SequenceEqual(Parse(subset.RootElement,collected).Select(x=>x.TextValue)),"LinkParserNotDeterministic");
            Require(rows.Select(x=>x.RecordKey).Distinct().Count()==160,"LinkDuplicateKeys");
            Require(rows.All(x=>x.NumericValue is >=1 and <=3),"LinkUnexpectedFixtureLaneRange");
            Require(rows.All(x=>x.TextValue.Length<=2000 && x.UnitCode=="lanes-per-directed-link"),"LinkPayloadOrUnitInvalid");
            bool rejected=false; using var empty=JsonDocument.Parse("{\"metadata\":{\"archiveSha256\":\"changed\"}}");
            try{Parse(empty.RootElement,collected);}catch(InvalidDataException){rejected=true;}
            Require(rejected,"LinkChangedArchiveAccepted");
            foreach(var fault in new[]{"NegativeLanes","DuplicateLink","MissingLink"})
            {
                var changed=JsonNode.Parse(subset.RootElement.GetRawText())!;var features=changed["features"]!.AsArray();
                if(fault=="NegativeLanes")features[0]!["properties"]!["LANES"]=-1;
                if(fault=="DuplicateLink")features[1]=features[0]!.DeepClone();
                if(fault=="MissingLink")features.RemoveAt(0);
                using var invalid=JsonDocument.Parse(changed.ToJsonString());rejected=false;
                try{Parse(invalid.RootElement,collected);}catch(InvalidDataException){rejected=true;}
                Require(rejected,"LinkInvalidFixtureAccepted:"+fault);
            }
            result["selfTestsPassed"]=9; await Save(); return;
        }
        var options=await 로컬공공자료Db.OptionsAsync(root);
        var keys=rows.Select(x=>x.RecordKey).ToList();
        await using(var db=new PublicDataIngestionDbContext(options))
        {
            if(mode=="apply")
            {
                await db.Database.OpenConnectionAsync();
                await using var command=db.Database.GetDbConnection().CreateCommand();
                command.CommandText="SELECT GET_LOCK('mirror:public-data:sagajeong-nodelink-r1',0)";
                Require(Convert.ToInt32(await command.ExecuteScalarAsync())==1,"LinkImportBusy");
                await using var transaction=await db.Database.BeginTransactionAsync();
                var before=await db.NormalizedRecords.AsNoTracking().Where(x=>keys.Contains(x.RecordKey)).ToListAsync();
                Require(before.All(x=>rows.Any(y=>Equivalent(x,y))),"LinkExistingConflict");
                result["databaseWriteAttempted"]=true;
                var service=new 평창군공공공간원본등록Service(db);
                // ZIP 원본과 파생 표본 각각의 hash/계보를 등록. 전국 행은 정규화 테이블에 넣지 않는다.
                foreach(var name in new[]{"nodelink.zip","sagajeong-links.json"})
                {
                    var registered=await service.RegisterFileAsync(Path.Combine(folder,name),new(Source,Dataset,
                        "archive-sha256:"+ArchiveHash,"sagajeong-nodelink-review.r1",null,
                        name.EndsWith(".zip")?"application/zip":"application/geo+json",Relative+"/"+name));
                    if(name.EndsWith(".json"))foreach(var row in rows)row.RawSnapshotId=registered.RawSnapshotId;
                    if(registered.Inserted)
                    {
                        var raw=await db.RawSnapshots.SingleAsync(x=>x.Id==registered.RawSnapshotId);raw.CollectedAtUtc=collected;
                        var run=await db.IngestionRuns.SingleAsync(x=>x.Id==raw.FirstCollectionRunId);
                        run.StatusCode=외부데이터수집StatusCodes.Partial;run.ErrorCode="PendingHumanReview";
                        run.ErrorSummary="Private source archive/subset only. 160 directed links; no national rows, game/OSM use or measured width. See extraction.json.";
                        await db.SaveChangesAsync();
                    }
                }
                var saved=await new EfExternalDataIngestionStore(db).UpsertNormalizedAsync(rows);
                Require(saved.UpdatedCount==0,"LinkUnexpectedUpdate");
                await transaction.CommitAsync();result["committed"]=true;result["inserted"]=saved.InsertedCount;result["existing"]=saved.ExistingCount;
            }
        }
        await using var verify=new PublicDataIngestionDbContext(options);
        var stored=await verify.NormalizedRecords.AsNoTracking().Include(x=>x.RawSnapshot).Where(x=>keys.Contains(x.RecordKey)).ToListAsync();
        if(mode!="preview")Require(stored.Count==160,"LinkReadbackCountMismatch");
        Require(stored.All(x=>rows.Any(y=>Equivalent(x,y)) && x.RawSnapshot?.ContentHashSha256==SubsetHash),"LinkReadbackMismatch");
        var origins=await verify.RawSnapshots.AsNoTracking().Where(x=>x.SourceId==Source && x.DatasetId==Dataset
            && (x.ContentHashSha256==ArchiveHash || x.ContentHashSha256==SubsetHash)).ToListAsync();
        if(mode!="preview")Require(origins.Count==2 && origins.All(x=>x.EvidenceAsOfUtc==null),"LinkOriginReadbackMismatch");
        result["sourceFiles"]=origins.Select(x=>new{x.Id,x.OriginalFileName,x.ContentHashSha256}).ToArray();
        result["verifiedRows"]=stored.Count;result["target"]="hongdal-mysql-1 / hongdal_dev";
        result["firstId"]=stored.Count>0?stored.Min(x=>x.Id):null;result["lastId"]=stored.Count>0?stored.Max(x=>x.Id):null;
        result["rawSnapshotIds"]=stored.Select(x=>x.RawSnapshotId).Distinct().ToArray();await Save();
        async Task Save()=>await File.WriteAllTextAsync(Path.Combine(folder,mode+"-"+DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmssfff")+".json"),JsonSerializer.Serialize(result,Json));
    }

    private static List<외부데이터정규화Record> Parse(JsonElement root,DateTimeOffset collected)
    {
        Require(root.GetProperty("metadata").GetProperty("archiveSha256").GetString()==ArchiveHash,"LinkMetadataHashMismatch");
        var result=new List<외부데이터정규화Record>();
        foreach(var feature in root.GetProperty("features").EnumerateArray())
        {
            var p=feature.GetProperty("properties");var id=p.GetProperty("LINK_ID").GetString()!;
            var lanes=p.GetProperty("LANES").GetInt32();Require(lanes>=0 && lanes<=30,"LinkLaneValueInvalid");
            Require(id.Length==10 && id.All(char.IsAsciiDigit) && id==feature.GetProperty("id").GetString(),"LinkIdInvalid");
            var dimension="link="+id+";subset-sha256="+SubsetHash;
            var payload=JsonSerializer.Serialize(new{sourceFields=p,geometryFile="sagajeong-links.json",geometryFeatureId=id,
                subsetSha256=SubsetHash,archiveSha256=ArchiveHash,sourceRecordIndex=feature.GetProperty("sourceRecordIndex").GetInt32(),
                laneMeaning="PerDirectedLink;NotWholeRoadWidth",dateMeaning="UPDATEDATE is source update; collection and release are not observation dates"});
            Require(payload.Length<=2000,"LinkPayloadTooLarge");
            result.Add(new(){SourceId=Source,DatasetId=Dataset,StableId=Source+":"+id,RegionStableId="region:kr:sig:11260",
                MetricCode="road-directed-link-lanes",RecordKey=외부데이터RecordKey.Create(Source,Dataset,"region:kr:sig:11260","road-directed-link-lanes",collected,dimension),
                DimensionKey=dimension,NumericValue=lanes,TextValue=payload,UnitCode="lanes-per-directed-link",
                SourceVersion="archive-sha256:"+ArchiveHash,DataRevision="sagajeong-nodelink-review.r1",
                EvidenceAsOfUtc=collected,CollectedAtUtc=collected,FirstSeenAtUtc=collected,LastSeenAtUtc=collected,
                SpatialPrecisionCode="bbox-intersection-full-directed-link",TemporalPrecisionCode="acquisition-only-not-observation",
                QualityCode="PendingHumanReview",LimitationCode=Limit});
        }
        Require(result.Count==160 && result.Select(x=>x.StableId).Distinct().Count()==160,"LinkSubsetCountOrDuplicate");return result;
    }
    private static bool Equivalent(외부데이터정규화Record a,외부데이터정규화Record b)=>a.RecordKey==b.RecordKey && a.StableId==b.StableId && a.TextValue==b.TextValue && a.NumericValue==b.NumericValue && a.SourceVersion==b.SourceVersion && a.DataRevision==b.DataRevision && a.UnitCode==b.UnitCode && a.QualityCode==b.QualityCode && a.LimitationCode==b.LimitationCode && a.EvidenceAsOfUtc==b.EvidenceAsOfUtc;
}
