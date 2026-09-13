using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Domain.PublicData;
using Ssalddel.Infrastructure.Persistence.PublicData;
using 살뜰.Services.External.PublicData.Korea;
using static 면목동공간결속;

// 기존 지도 건물에서 출발하는 비공개 자료 대장. 주소/필지 후보를 높이 적용 승인으로 승격하지 않는다.
internal static class 사가정건물자료대장
{
    private const string Relative = "artifacts/local/public-data/sagajeong-building-ledger-r1";
    private const string MapPath = "C:/Users/user/ssalddel/Assets/Ssalddel/Resources/SagajeongReference.json";
    private const string Dataset = "sagajeong-building-evidence-ledger";
    private const string Source = "mirror-derived-public-data-review";
    private const string Revision = "sagajeong-building-ledger.r1";
    private const string Boundary = "PrivateReviewOnly;CandidateNotIdentity;NoPublication;NoUnityApply;NoHeightInference;NoOperationalWrite";
    private static readonly (string Name,string Path,string Hash)[] Inputs =
    [
        ("map",MapPath,면목동주소공간연결.MapHash),
        ("links","artifacts/local/public-data/myeonmok-business-20260908-r1/connection.json","D109A0AF26608E9627551600B37FE8B22F473893D5F6FEA542E83E5D1FD176E2"),
        ("business","artifacts/local/public-data/myeonmok-business-20260908-r1/business-readback.json","EC5F0D22859A3661411896E0EDC52B14B123D8FF533481B514FEDBC8140D5CF1")
    ];
    private static void Check(bool ok,string code)=>면목동공간결속.Require(ok,"BuildingLedger:"+code);
    private static Dictionary<string,JsonNode> Load(string root)=>Inputs.ToDictionary(x=>x.Name,x=>
    {
        var path=Path.Combine(root,x.Path);Check(HashFile(path)==x.Hash,"InputDrift:"+x.Name);return Read(path);
    });
    internal static string Lot(string address)
    {
        var m=Regex.Match(address,@"^서울특별시 중랑구 면목동 (산\s*)?(\d{1,4})(?:-(\d{1,4}))?$");
        return m.Success?"1126010100"+(m.Groups[1].Success?"2":"1")+m.Groups[2].Value.PadLeft(4,'0')
            +(m.Groups[3].Success?m.Groups[3].Value:"0").PadLeft(4,'0'):"";
    }
    internal static decimal? Positive(string raw,bool integer=false)
        => decimal.TryParse(raw,NumberStyles.AllowDecimalPoint|NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture,out var value)
            && value>0 && (!integer || decimal.Truncate(value)==value)?value:null;
    private static string MapAddress(JsonNode b)
    {
        var street=S(b,"street");var number=S(b,"houseNumber");
        return street.Length>0&&Regex.IsMatch(number,@"^\d+(?:-\d+)?$")?Address("서울특별시 중랑구 "+street+" "+number):"";
    }
    private static JsonArray Connections(Dictionary<string,JsonNode> input)
    {
        var ids=input["map"]["buildings"]!.AsArray().Select(b=>S(b,"id")).ToHashSet(StringComparer.Ordinal);
        var observations=input["business"]["rows"]!.AsArray().ToDictionary(x=>S(x,"StableId"),x=>x!);
        var output=new JsonArray();var seen=new HashSet<string>();
        foreach(var link in input["links"]["links"]!.AsArray())
        {
            var item=link!["item"]!;var id=S(item,"id");Check(seen.Add(id),"DuplicateSourceId");
            var buildings=link["buildingIds"]!.AsArray().Select(x=>x!.ToString()).ToArray();
            Check(buildings.Distinct().Count()==buildings.Length&&buildings.All(ids.Contains),"UnknownBuilding");
            if(buildings.Length==0)continue;
            observations.TryGetValue(id,out var row);var observation=row?["observation"];
            output.Add(new JsonObject {
                ["sourceRecordId"]=id,["sourceId"]=S(item,"sourceId"),["datasetId"]=S(item,"datasetId"),
                ["sourceHash"]=S(item,"sourceHash"),["evidenceAsOf"]=S(item,"evidenceAsOf"),
                ["addressKey"]=S(link,"addressKey"),["result"]=S(link,"result"),["buildingIds"]=JsonSerializer.SerializeToNode(buildings),
                ["buildingManagementNumber"]=S(observation,"BuildingManagementNumber"),
                ["lotKey"]=Lot(S(observation,"LotAddress")),
                ["boundary"]="AddressCandidateOnly;ShopFloorIsNotBuildingStoreys;NoVerifiedOccupancy"
            });
        }
        return new JsonArray(output.OrderBy(x=>S(x,"sourceRecordId"),StringComparer.Ordinal).Select(x=>x!.DeepClone()).ToArray());
    }
    private static JsonObject Project(Dictionary<string,JsonNode> input,JsonArray connections,면목동공간원천.GisEvidence gis)
    {
        var map=input["map"];Check(S(map,"revision")=="sagajeong-reference.r3","MapRevision");
        var buildings=map["buildings"]!.AsArray();var ids=new HashSet<string>();var rows=new JsonArray();
        foreach(var b in buildings.OrderBy(x=>S(x,"id"),StringComparer.Ordinal))
        {
            var id=S(b,"id");Check(id.Length>0&&ids.Add(id),"DuplicateBuilding");
            var height=Positive(S(b,"height"));Check(height!=null,"InvalidCurrentHeight");
            var kind=S(b,"heightKind");Check(kind is "OsmHeightTag" or "SymbolicHeight","UnknownHeightKind");
            var refs=connections.Where(x=>x!["buildingIds"]!.AsArray().Any(v=>v!.ToString()==id)).ToArray();
            var lots=refs.Select(x=>S(x,"lotKey")).Where(x=>x.Length>0).Distinct().Order(StringComparer.Ordinal).ToArray();
            var candidates=gis.Rows.Where(g=>lots.Contains(g.Fields["A2"],StringComparer.Ordinal)).OrderBy(g=>g.Index).Select(g=>new {
                sourceRecordIndex=g.Index,gisId=g.Fields["A1"],lotKey=g.Fields["A2"],
                rawHeight=g.Fields["A16"],heightMeters=Positive(g.Fields["A16"]),
                rawAboveGroundFloors=g.Fields["A26"],aboveGroundFloors=Positive(g.Fields["A26"],true),
                sourceUpdate=g.Fields["A22"],relation="SameParcelCandidateNotBuildingIdentity",rightsStatus=gis.RightsStatus
            }).ToArray();
            var road=MapAddress(b!);var floors=Positive(S(b,"levelsText"),true);
            rows.Add(new JsonObject {
                ["buildingId"]=id,["roadAddressKey"]=road,["currentHeight"]=height,["currentHeightKind"]=kind,
                ["rawLevels"]=S(b,"levelsText"),["aboveGroundFloorsCandidate"]=floors,
                ["sourceRecordIds"]=JsonSerializer.SerializeToNode(refs.Select(x=>S(x,"sourceRecordId")).ToArray()),
                ["buildingManagementNumberCandidates"]=JsonSerializer.SerializeToNode(refs.Select(x=>S(x,"buildingManagementNumber")).Where(x=>x.Length>0).Distinct().Order(StringComparer.Ordinal).ToArray()),
                ["lotCandidates"]=JsonSerializer.SerializeToNode(lots),["gisCandidates"]=JsonSerializer.SerializeToNode(candidates),
                ["matchingStatus"]=road.Length==0?"AddressMissing":refs.Length==0?"NoCollectedAddressCandidate":
                    refs.Any(x=>S(x,"result")=="MultipleBuildingsForAddress")?"AmbiguousAddress":"AddressCandidateOnly",
                ["heightAction"]=kind=="OsmHeightTag"?"KeepSourceHeight":floors!=null?"AwaitFloorPolicy":candidates.Length>0?"AwaitIdentityAndRights":"KeepTemporaryHeight",
                ["nextCollectionNeed"]=kind=="OsmHeightTag"?"OptionalCrossCheck":road.Length==0?"OfficialAddressOrFootprint":lots.Length==0?"BuildingIdentityAndHeightOrStoreys":"BuildingRegisterTitle",
                ["unityApplyAllowed"]=false,["proposedHeight"]=null
            });
        }
        Check(rows.Count==602,"MapCountChanged");
        return new JsonObject {
            ["schema"]=Revision,["mapRevision"]=S(map,"revision"),["mapHash"]=면목동주소공간연결.MapHash,
            ["gisSourceHash"]=gis.SourceHash,["gisRightsStatus"]=gis.RightsStatus,["boundary"]=Boundary,
            ["connections"]=connections.DeepClone(),["buildings"]=rows,
            ["summary"]=JsonSerializer.SerializeToNode(new {
                buildings=rows.Count,sourceHeight=rows.Count(x=>S(x,"currentHeightKind")=="OsmHeightTag"),
                temporaryHeight=rows.Count(x=>S(x,"currentHeightKind")=="SymbolicHeight"),
                withAddress=rows.Count(x=>S(x,"roadAddressKey").Length>0),
                withCollectedCandidates=rows.Count(x=>x!["sourceRecordIds"]!.AsArray().Count>0),
                withGisParcelCandidates=rows.Count(x=>x!["gisCandidates"]!.AsArray().Count>0),
                osmFloorCandidates=rows.Count(x=>S(x,"heightAction")=="AwaitFloorPolicy"),
                uniqueGisRows=gis.Rows.Length,eligibleHeightChanges=0
            })
        };
    }
    public static async Task RunAsync(string mode,string root,Dictionary<string,object?> result)
    {
        if(mode.StartsWith("register-",StringComparison.Ordinal))
        {
            Load(root);VerifyFiles(Path.Combine(root,Relative));
            await 사가정건축물대장검토.RunAsync(mode[9..],root,result);return;
        }
        Check(mode is "prepare" or "self-test" or "replay" or "acquire" or "retry-approved" or "preview" or "apply" or "verify","Mode");
        var input=Load(root);var connections=Connections(input);var folder=Path.Combine(root,Relative);
        if(mode is "prepare" or "replay" or "self-test")
        {
            var lots=connections.Select(x=>S(x,"lotKey")).Where(x=>x.Length>0).ToHashSet(StringComparer.Ordinal);
            var gis=면목동공간원천.ReadGis(root,lots);var ledger=Project(input,connections,gis);
            result["summary"]=ledger["summary"]!.DeepClone();result["ledgerHash"]=Hash(JsonSerializer.SerializeToUtf8Bytes(ledger,Json));
            if(mode=="self-test")
            {
                int tests=0;void Test(bool ok){Check(ok,"SelfTest:"+(tests+1));tests++;}
                Test(Positive("0")==null);Test(Positive("NaN")==null);Test(Positive("-1")==null);
                Test(Positive("3.5",true)==null);Test(Positive("3",true)==3);Test(Positive("23.5")==23.5m);
                Test(Lot("서울특별시 중랑구 면목동 1-2")=="1126010100100010002");Test(Lot("서울특별시 중랑구 상봉동 1")=="");
                Test(JsonNode.DeepEquals(ledger,Project(input,connections,gis)));
                Test(ledger["buildings"]!.AsArray().All(x=>x!["unityApplyAllowed"]!.GetValue<bool>()==false&&x["proposedHeight"]==null));
                var changed=Load(root);changed["map"]["buildings"]!.AsArray().Add(changed["map"]["buildings"]![0]!.DeepClone());
                bool rejected=false;try{Project(changed,connections,gis);}catch(InvalidDataException){rejected=true;}Test(rejected);
                Test(connections.All(x=>!x!.AsObject().ContainsKey("Floor")));
                Test(MapAddress(JsonNode.Parse("{\"street\":\"면목로\",\"houseNumber\":\"300-1\"}")!)=="서울특별시 중랑구 면목로 300-1");
                Test(MapAddress(JsonNode.Parse("{\"street\":\"면목로\",\"houseNumber\":\"300 2층\"}")!)=="");
                Test(ledger["buildings"]!.AsArray().Where(x=>S(x,"matchingStatus")=="AmbiguousAddress").All(x=>!x!["unityApplyAllowed"]!.GetValue<bool>()));
                foreach(var fault in new[]{"MapRevision","HeightKind","ZeroHeight","MissingBuildingReference"})
                {
                    changed=Load(root);var testLinks=(JsonArray)connections.DeepClone();
                    if(fault=="MapRevision")changed["map"]["revision"]="changed";
                    if(fault=="HeightKind")changed["map"]["buildings"]![0]!["heightKind"]="Unreviewed";
                    if(fault=="ZeroHeight")changed["map"]["buildings"]![0]!["height"]=0;
                    if(fault=="MissingBuildingReference")changed["links"]["links"]![0]!["buildingIds"]=JsonSerializer.SerializeToNode(new[]{"unknown"});
                    rejected=false;try{if(fault=="MissingBuildingReference")Connections(changed);else Project(changed,testLinks,gis);}catch(InvalidDataException){rejected=true;}Test(rejected);
                }
                result["testsPassed"]=tests;return;
            }
            if(mode=="replay") { VerifyFiles(folder);Check(JsonNode.DeepEquals(Read(Path.Combine(folder,"ledger.json")),ledger),"ReplayMismatch");result["replayPassed"]=true;return; }
            Check(!Directory.Exists(folder),"OutputExists");Directory.CreateDirectory(folder);
            await Save(folder,"gis-candidates.json",gis);await Save(folder,"ledger.json",ledger);
            var selected=connections.Where(x=>S(x,"result")=="UniqueAddressCandidateNotVerifiedOccupancy"&&S(x,"lotKey").Length>0)
                .Where(x=>input["map"]["buildings"]!.AsArray().Any(b=>S(b,"id")==x!["buildingIds"]![0]!.ToString()&&S(b,"heightKind")=="SymbolicHeight"))
                .GroupBy(x=>S(x,"lotKey")).OrderBy(g=>g.Key,StringComparer.Ordinal).Take(3).Select(g=>g.First()!).ToArray();
            var samples=selected.Select(x=>new Sample(S(x,"sourceRecordId"),"건물높이","",S(x,"addressKey"),"",S(x,"buildingManagementNumber"),S(x,"lotKey"),
                "SourceBusinessLotAddress;CandidateOnly","","",S(x,"result"),x["buildingIds"]!.AsArray().Select(v=>v!.ToString()).ToArray(),
                "OneAddressCandidate;TemporaryHeight;DistinctLotOrdinal;Max3",S(x,"sourceId"),S(x,"sourceHash"),S(x,"evidenceAsOf"))).ToArray();
            await Save(folder,"selection.json",new Selection(Revision,DateTimeOffset.UtcNow.ToString("O"),Inputs.Select(x=>new Input(x.Path,x.Hash,new FileInfo(Path.Combine(root,x.Path)).Length)).ToArray(),samples));
            await Save(folder,"manifest.json",new {revision=Revision,createdAtUtc=DateTimeOffset.UtcNow,files=new[]{"ledger.json","gis-candidates.json","selection.json"}.Select(name=>new {name,sha256=HashFile(Path.Combine(folder,name))}).ToArray()});
            VerifyFiles(folder);result["selectedAdditionalLots"]=samples.Length;result["folder"]=folder;return;
        }
        VerifyFiles(folder);
        if(mode is "acquire" or "retry-approved")
        {
            var selection=JsonSerializer.Deserialize<Selection>(File.ReadAllBytes(Path.Combine(folder,"selection.json")),Json)!;
            Check(selection.Revision==Revision&&selection.Samples.Length<=3,"AcquisitionScope");
            if(mode=="retry-approved")
            {
                var previous=Read(Path.Combine(folder,"acquisition.json"));
                Check(previous["buildingQueries"]!.AsArray().Any(x=>S(x,"error")=="HttpStatus:403")
                    &&!Directory.GetFiles(folder,"building-lot-*.json").Any(),"RetryOnlyAfterBlockedEmptyAcquisition");
                var retryFolder=Path.Combine(folder,"retry-after-approval-r1");
                Check(!Directory.Exists(retryFolder),"ApprovedRetryAlreadyAttempted");
                Directory.CreateDirectory(retryFolder);
                await Save(retryFolder,"selection.json",selection);
                await Save(retryFolder,"authorization.json",new {reason="UserConfirmedApiApplicationAndRequestedExistingCredentialRetry",
                    priorAcquisitionHash=HashFile(Path.Combine(folder,"acquisition.json")),selectionHash=HashFile(Path.Combine(folder,"selection.json")),
                    maxLots=3,automaticRetry=false,createdAtUtc=DateTimeOffset.UtcNow});
                folder=retryFolder;
            }
            await 면목동공간원천.Acquire(root,folder,selection,result);return;
        }
        await Persist(root,folder,mode,result);
    }
    private static void VerifyFiles(string folder)
    {
        var manifest=Read(Path.Combine(folder,"manifest.json"));Check(S(manifest,"revision")==Revision,"ManifestRevision");
        var files=manifest["files"]!.AsArray();Check(files.Count==3,"ManifestCount");
        var names=files.Select(x=>S(x,"name")).Order(StringComparer.Ordinal).ToArray();
        Check(names.SequenceEqual(new[]{"gis-candidates.json","ledger.json","selection.json"}),"ManifestPaths");
        foreach(var file in files)Check(HashFile(Path.Combine(folder,S(file,"name")))==S(file,"sha256"),"FrozenFileDrift");
    }
    private static async Task Persist(string root,string folder,string mode,Dictionary<string,object?> result)
    {
        var ledger=Read(Path.Combine(folder,"ledger.json"));var hash=HashFile(Path.Combine(folder,"ledger.json")).ToLowerInvariant();
        var timestamp=DateTimeOffset.Parse(S(Read(Path.Combine(folder,"manifest.json")),"createdAtUtc"),CultureInfo.InvariantCulture);
        timestamp=DateTimeOffset.FromUnixTimeMilliseconds(timestamp.ToUnixTimeMilliseconds());
        var rows=ledger["buildings"]!.AsArray().Select(b=> {
            var id=S(b,"buildingId");var dimension="building="+id+";ledger-sha256="+hash;
            return new 외부데이터정규화Record {
                SourceId=Source,DatasetId=Dataset,StableId="sagajeong-building-evidence:"+id,RegionStableId="region:kr:sig:11260",
                MetricCode="building-evidence-coverage",DimensionKey=dimension,
                RecordKey=외부데이터RecordKey.Create(Source,Dataset,"region:kr:sig:11260","building-evidence-coverage",timestamp,dimension),
                TextValue=JsonSerializer.Serialize(new {buildingId=id,ledgerFile=Relative+"/ledger.json",ledgerSha256=hash,
                    matchingStatus=S(b,"matchingStatus"),heightAction=S(b,"heightAction"),nextCollectionNeed=S(b,"nextCollectionNeed"),mapHash=면목동주소공간연결.MapHash},Compact),
                NumericValue=null,UnitCode="building-evidence-reference",SourceVersion="ledger-sha256:"+hash,DataRevision=Revision,
                EvidenceAsOfUtc=timestamp,CollectedAtUtc=timestamp,FirstSeenAtUtc=timestamp,LastSeenAtUtc=timestamp,
                SpatialPrecisionCode="existing-osm-building-reference",TemporalPrecisionCode="review-time-not-observation",
                QualityCode="PendingHumanReview",LimitationCode=Boundary
            };
        }).ToList();
        var keys=rows.Select(x=>x.RecordKey).ToList();Check(rows.Count==602&&keys.Distinct().Count()==602,"PersistenceCount");
        var options=await 로컬공공자료Db.OptionsAsync(root);
        await using(var db=new PublicDataIngestionDbContext(options))
        {
            if(mode=="apply")
            {
                await db.Database.OpenConnectionAsync();await using var command=db.Database.GetDbConnection().CreateCommand();
                command.CommandText="SELECT GET_LOCK('mirror:public-data:sagajeong-building-ledger-r1',0)";
                Check(Convert.ToInt32(await command.ExecuteScalarAsync())==1,"ImportBusy");
                await using var transaction=await db.Database.BeginTransactionAsync();
                var before=await db.NormalizedRecords.AsNoTracking().Where(x=>keys.Contains(x.RecordKey)).ToListAsync();
                Check(before.All(a=>rows.Any(b=>Equivalent(a,b))),"ExistingConflict");VerifyFiles(folder);Load(root);
                result["databaseWriteAttempted"]=true;
                var service=new 평창군공공공간원본등록Service(db);long rawId=0;
                foreach(var name in new[]{"gis-candidates.json","ledger.json"})
                {
                    var registered=await service.RegisterFileAsync(Path.Combine(folder,name),new(Source,Dataset,"ledger-sha256:"+hash,Revision,null,"application/json",Relative+"/"+name));
                    if(name=="ledger.json")rawId=registered.RawSnapshotId;
                    if(registered.Inserted){var raw=await db.RawSnapshots.SingleAsync(x=>x.Id==registered.RawSnapshotId);
                        var run=await db.IngestionRuns.SingleAsync(x=>x.Id==raw.FirstCollectionRunId);run.StatusCode=외부데이터수집StatusCodes.Partial;
                        run.ErrorCode="PendingHumanReview";run.ErrorSummary="Derived address/parcel candidates only; no official building assignment or Unity changes.";await db.SaveChangesAsync();}
                }
                foreach(var row in rows)row.RawSnapshotId=rawId;
                var saved=await new EfExternalDataIngestionStore(db).UpsertNormalizedAsync(rows);
                Check(saved.UpdatedCount==0,"UnexpectedUpdate");await transaction.CommitAsync();
                result["committed"]=true;result["inserted"]=saved.InsertedCount;result["existing"]=saved.ExistingCount;
            }
        }
        await using var verify=new PublicDataIngestionDbContext(options);
        var stored=await verify.NormalizedRecords.AsNoTracking().Include(x=>x.RawSnapshot).Where(x=>keys.Contains(x.RecordKey)).ToListAsync();
        if(mode!="preview")Check(stored.Count==602,"ReadbackCount");
        Check(stored.All(a=>rows.Any(b=>Equivalent(a,b))&&a.RawSnapshot?.ContentHashSha256==hash&&a.RawSnapshot.EvidenceAsOfUtc==null),"ReadbackMismatch");
        result["verifiedRows"]=stored.Count;result["rawSnapshotIds"]=stored.Select(x=>x.RawSnapshotId).Distinct().ToArray();
        result["firstId"]=stored.Count>0?stored.Min(x=>x.Id):null;result["lastId"]=stored.Count>0?stored.Max(x=>x.Id):null;
        result["target"]="hongdal-mysql-1 / hongdal_dev";result["summary"]=ledger["summary"]!.DeepClone();
        await Save(folder,mode+"-"+DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmssfff")+".json",result);
    }
    private static bool Equivalent(외부데이터정규화Record a,외부데이터정규화Record b)
        =>a.RecordKey==b.RecordKey&&a.StableId==b.StableId&&a.SourceId==b.SourceId&&a.DatasetId==b.DatasetId&&a.RegionStableId==b.RegionStableId
        &&a.TextValue==b.TextValue&&a.NumericValue==null&&a.UnitCode==b.UnitCode&&a.MetricCode==b.MetricCode&&a.DimensionKey==b.DimensionKey
        &&a.SourceVersion==b.SourceVersion&&a.DataRevision==b.DataRevision&&a.QualityCode==b.QualityCode&&a.LimitationCode==b.LimitationCode
        &&a.SpatialPrecisionCode==b.SpatialPrecisionCode&&a.TemporalPrecisionCode==b.TemporalPrecisionCode&&a.EvidenceAsOfUtc==b.EvidenceAsOfUtc
        &&a.CollectedAtUtc==b.CollectedAtUtc&&a.FirstSeenAtUtc==b.FirstSeenAtUtc;
}
