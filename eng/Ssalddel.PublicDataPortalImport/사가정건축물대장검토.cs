using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Domain.PublicData;
using Ssalddel.Infrastructure.Persistence.PublicData;
using 살뜰.Services.External.PublicData.Korea;
using static 면목동공간결속;

// 승인 후 재수집한 최대 3필지의 표제부를 검토 자료로 저장한다. 원래 대장/지도/공식 Assignment는 수정하지 않는다.
internal static class 사가정건축물대장검토
{
    private const string Relative="artifacts/local/public-data/sagajeong-building-ledger-r1/retry-after-approval-r1";
    private const string Source="molit-building-hub";
    private const string Dataset="sagajeong-building-title-review";
    private const string Revision="sagajeong-building-title-review.r1";
    private const string Limit="PrivateReviewOnly;AddressParcelCandidate;NotOsmIdentity;NoUnityApply;NoFloorHeightInference;ZeroHeightIsMissing";
    private static void Check(bool ok,string code)=>면목동공간결속.Require(ok,"BuildingTitleReview:"+code);
    private static string Lot(JsonNode row)
    {
        var bun=S(row,"bun");var ji=S(row,"ji");var type=S(row,"platGbCd");
        if(!int.TryParse(bun,out var b)||!int.TryParse(ji,out var j)||b is <0 or >9999||j is <0 or >9999||type is not ("0" or "1"))return "";
        return S(row,"sigunguCd")+S(row,"bjdongCd")+(type=="0"?"1":"2")+b.ToString("D4",CultureInfo.InvariantCulture)+j.ToString("D4",CultureInfo.InvariantCulture);
    }
    internal static string Match(JsonNode item,Sample sample,int recordCount)
        =>Lot(item)!=sample.LotKey?"ParcelMismatch":Address(S(item,"newPlatPlc"))!=Address(sample.RoadAddress)?"RoadAddressMismatch":
            recordCount!=1||sample.OsmBuildingIds.Length!=1?"MultipleCandidates":"RoadAndParcelAgreeCandidate";
    public static async Task RunAsync(string mode,string root,Dictionary<string,object?> result)
    {
        Check(mode is "self-test" or "preview" or "apply" or "verify","Mode");
        var folder=Path.Combine(root,Relative);var selection=JsonSerializer.Deserialize<Selection>(File.ReadAllBytes(Path.Combine(folder,"selection.json")),Json)!;
        Check(selection.Revision=="sagajeong-building-ledger.r1"&&selection.Samples.Length==3,"Selection");
        var original=Path.Combine(root,"artifacts/local/public-data/sagajeong-building-ledger-r1/selection.json");
        Check(HashFile(original)==HashFile(Path.Combine(folder,"selection.json")),"SelectionDrift");
        var receipt=Read(Path.Combine(folder,"acquisition.json"));Check(S(receipt,"selectionHash")==HashFile(original),"ReceiptSelection");
        var metadata=Read(Path.Combine(folder,"building-register-metadata.json"));Check(S(metadata,"license")=="이용허락범위 제한 없음","RightsChanged");
        var metadataReceipt=receipt["sources"]!.AsArray().Single(x=>S(x,"Id")=="building-register");
        Check(HashFile(Path.Combine(folder,"building-register-metadata.json"))==S(metadataReceipt,"sha256"),"MetadataDrift");
        var rows=new List<외부데이터정규화Record>();var files=new Dictionary<string,string>();var records=new List<JsonNode>();
        var seen=new HashSet<string>();var seenLots=new HashSet<string>();
        foreach(var query in receipt["buildingQueries"]!.AsArray())
        {
            var lot=S(query,"lotKey");Check(seenLots.Add(lot),"DuplicateLot");var sample=selection.Samples.Single(x=>x.LotKey==lot);
            if(S(query,"status")!="Acquired")continue;
            var name=S(query,"file");Check(Path.GetFileName(name)==name&&!name.Contains('/')&&!name.Contains('\\'),"ResponsePath");
            var path=Path.Combine(folder,name);var hash=HashFile(path).ToLowerInvariant();Check(hash==S(query,"sha256").ToLowerInvariant(),"ResponseDrift");
            var data=Read(path);Check(S(data,"lotKey")==lot,"ResponseLot");var items=data["items"]!.AsArray();
            Check(items.Count==data["totalCount"]!.GetValue<int>()&&items.Count==query!["totalCount"]!.GetValue<int>()&&items.Count<=300,"ResponseCount");
            var time=DateTimeOffset.Parse(S(data,"observedAtUtc"),CultureInfo.InvariantCulture);
            time=DateTimeOffset.FromUnixTimeMilliseconds(time.ToUnixTimeMilliseconds());
            foreach(var item in items)
            {
                var pk=S(item,"mgmBldrgstPk");Check(pk.Length>0&&seen.Add(pk),"DuplicateRegisterPk");
                var match=Match(item!,sample,items.Count);var height=사가정건물자료대장.Positive(S(item,"heit"));
                var payload=new {registerPk=pk,osmBuildingCandidates=sample.OsmBuildingIds,lotKey=lot,roadAddress=S(item,"newPlatPlc"),
                    rawHeight=S(item,"heit"),heightMeters=height,rawAboveGroundFloors=S(item,"grndFlrCnt"),
                    aboveGroundFloors=사가정건물자료대장.Positive(S(item,"grndFlrCnt"),true),rawUndergroundFloors=S(item,"ugrndFlrCnt"),
                    recordCreationDate=S(item,"crtnDay"),dateMeaning="RecordCreationNotObservation",matchingStatus=match,
                    unityApplyAllowed=false,sourceFile=Relative+"/"+name,sourceHash=hash};
                var text=JsonSerializer.Serialize(payload,Compact);Check(text.Length<=2000,"PayloadBudget");records.Add(JsonNode.Parse(text)!);
                var dimension="register="+pk+";source-sha256="+hash;
                rows.Add(new(){SourceId=Source,DatasetId=Dataset,StableId=Source+":"+pk,RegionStableId="region:kr:sig:11260",
                    MetricCode="building-register-height-review",DimensionKey=dimension,RecordKey=외부데이터RecordKey.Create(Source,Dataset,"region:kr:sig:11260","building-register-height-review",time,dimension),
                    TextValue=text,NumericValue=height,UnitCode="m",SourceVersion="source-sha256:"+hash,DataRevision=Revision,
                    EvidenceAsOfUtc=time,CollectedAtUtc=time,FirstSeenAtUtc=time,LastSeenAtUtc=time,
                    SpatialPrecisionCode="source-parcel-road-address-candidate",TemporalPrecisionCode="collection-not-observation",
                    QualityCode="PendingHumanReview",LimitationCode=Limit});files[pk]=name;
            }
        }
        Check(seenLots.Count==selection.Samples.Length,"MissingQueryReceipt");
        result["receivedRecords"]=rows.Count;result["positiveHeights"]=rows.Count(x=>x.NumericValue!=null);
        result["matchingCandidates"]=records.Count(x=>S(x,"matchingStatus")=="RoadAndParcelAgreeCandidate");
        if(mode=="self-test")
        {
            var first=Read(Path.Combine(folder,files.First().Value))["items"]![0]!;
            var sample=selection.Samples.Single(x=>x.LotKey==Lot(first));int n=0;void Test(bool ok){Check(ok,"SelfTest:"+(n+1));n++;}
            Test(Match(first,sample,1)=="RoadAndParcelAgreeCandidate");Test(Match(first,sample,2)=="MultipleCandidates");
            var bad=first.DeepClone();bad["bun"]="9999";Test(Match(bad,sample,1)=="ParcelMismatch");
            bad=first.DeepClone();bad["newPlatPlc"]="서울특별시 중랑구 면목로 9999";Test(Match(bad,sample,1)=="RoadAddressMismatch");
            Test(사가정건물자료대장.Positive("0")==null);Test(사가정건물자료대장.Positive("8.3")==8.3m);
            Test(rows.All(x=>x.QualityCode=="PendingHumanReview"&&x.LimitationCode==Limit));
            Test(records.All(x=>!x["unityApplyAllowed"]!.GetValue<bool>()));result["testsPassed"]=n;return;
        }
        Check(rows.Count>0,"NoRecordsToPersist");
        var keys=rows.Select(x=>x.RecordKey).ToList();var options=await 로컬공공자료Db.OptionsAsync(root);
        await using(var db=new PublicDataIngestionDbContext(options))
        {
            if(mode=="apply")
            {
                await db.Database.OpenConnectionAsync();await using var command=db.Database.GetDbConnection().CreateCommand();
                command.CommandText="SELECT GET_LOCK('mirror:public-data:sagajeong-building-title-r1',0)";Check(Convert.ToInt32(await command.ExecuteScalarAsync())==1,"ImportBusy");
                await using var transaction=await db.Database.BeginTransactionAsync();
                var before=await db.NormalizedRecords.AsNoTracking().Where(x=>keys.Contains(x.RecordKey)).ToListAsync();Check(before.All(a=>rows.Any(b=>Same(a,b))),"ExistingConflict");
                result["databaseWriteAttempted"]=true;var service=new 평창군공공공간원본등록Service(db);
                foreach(var name in files.Values.Distinct())
                {
                    var hash=HashFile(Path.Combine(folder,name)).ToLowerInvariant();
                    var registered=await service.RegisterFileAsync(Path.Combine(folder,name),new(Source,Dataset,"source-sha256:"+hash,Revision,null,"application/json",Relative+"/"+name));
                    foreach(var row in rows.Where(x=>x.SourceVersion=="source-sha256:"+hash))row.RawSnapshotId=registered.RawSnapshotId;
                    if(registered.Inserted){var raw=await db.RawSnapshots.SingleAsync(x=>x.Id==registered.RawSnapshotId);var run=await db.IngestionRuns.SingleAsync(x=>x.Id==raw.FirstCollectionRunId);
                        run.StatusCode=외부데이터수집StatusCodes.Partial;run.ErrorCode="PendingHumanReview";run.ErrorSummary="Selected building title fields; no official OSM assignment or Unity changes.";await db.SaveChangesAsync();}
                }
                var saved=await new EfExternalDataIngestionStore(db).UpsertNormalizedAsync(rows);Check(saved.UpdatedCount==0,"UnexpectedUpdate");await transaction.CommitAsync();
                result["committed"]=true;result["inserted"]=saved.InsertedCount;result["existing"]=saved.ExistingCount;
            }
        }
        await using var verify=new PublicDataIngestionDbContext(options);
        var stored=await verify.NormalizedRecords.AsNoTracking().Include(x=>x.RawSnapshot).Where(x=>keys.Contains(x.RecordKey)).ToListAsync();
        if(mode!="preview")Check(stored.Count==rows.Count,"ReadbackCount");
        Check(stored.All(a=>rows.Any(b=>Same(a,b))&&a.SourceVersion=="source-sha256:"+a.RawSnapshot!.ContentHashSha256&&a.RawSnapshot.EvidenceAsOfUtc==null),"ReadbackMismatch");
        result["verifiedRows"]=stored.Count;result["rawSnapshotIds"]=stored.Select(x=>x.RawSnapshotId).Distinct().ToArray();
        result["recordIds"]=stored.Select(x=>x.Id).Order().ToArray();result["target"]="hongdal-mysql-1 / hongdal_dev";
        var report=Path.Combine(folder,"height-review.json");var projected=new {revision=Revision,records};
        if(File.Exists(report))Check(JsonNode.DeepEquals(Read(report),JsonSerializer.SerializeToNode(projected,Json)),"ReviewDrift");else await Save(folder,"height-review.json",projected);
        await Save(folder,"register-"+mode+"-"+DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmssfff")+".json",result);
    }
    private static bool Same(외부데이터정규화Record a,외부데이터정규화Record b)=>a.RecordKey==b.RecordKey&&a.StableId==b.StableId&&a.TextValue==b.TextValue
        &&a.NumericValue==b.NumericValue&&a.QualityCode==b.QualityCode&&a.LimitationCode==b.LimitationCode&&a.UnitCode==b.UnitCode&&a.DataRevision==b.DataRevision
        &&a.SourceId==b.SourceId&&a.DatasetId==b.DatasetId&&a.SourceVersion==b.SourceVersion&&a.RegionStableId==b.RegionStableId&&a.DimensionKey==b.DimensionKey&&a.MetricCode==b.MetricCode
        &&a.SpatialPrecisionCode==b.SpatialPrecisionCode&&a.TemporalPrecisionCode==b.TemporalPrecisionCode&&a.EvidenceAsOfUtc==b.EvidenceAsOfUtc&&a.CollectedAtUtc==b.CollectedAtUtc&&a.FirstSeenAtUtc==b.FirstSeenAtUtc;
}
