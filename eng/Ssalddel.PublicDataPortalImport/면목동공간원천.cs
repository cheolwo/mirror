using System.Buffers.Binary;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using static 면목동공간결속;

internal static class 면목동공간원천
{
    private static void Require(bool ok,string code)=>면목동공간결속.Require(ok,code);
    internal sealed record GisRow(int Index,Dictionary<string,string> Fields,object Geometry);
    internal sealed record GisEvidence(string SourceHash,string Crs,string EncodingStatus,string RightsStatus,int RowsRead,int MyeonmokRows,GisRow[] Rows);
    private static readonly (string Id,string Url)[] Metadata=
    [
        ("legal-boundary","https://www.data.go.kr/catalog/15045881/fileData.json"),
        ("building-register","https://www.data.go.kr/catalog/15134735/openapi.json"),
        ("continuous-parcel","https://www.data.go.kr/catalog/15056910/openapi.json"),
        ("land-characteristics","https://www.data.go.kr/catalog/15123549/openapi.json")
    ];
    internal static async Task Acquire(string root,string folder,Selection selection,Dictionary<string,object?> result,bool resume=false)
    {
        Require(!File.Exists(Path.Combine(folder,"acquisition.json")),"LandAcquisitionCompleted");
        if(!resume){Require(!File.Exists(Path.Combine(folder,"acquisition-claim.json")),"LandAcquisitionAlreadyAttempted");await Save(folder,"acquisition-claim.json",new{selectionHash=HashFile(Path.Combine(folder,"selection.json")),maxBuildingLots=30,maxPagesPerLot=3,rowsPerPage=100,maxResponseBytes=2*1024*1024,maxTotalBytes=32*1024*1024,timeoutSeconds=30,retry=0,redirect=false});}
        else
        {
            Require(File.Exists(Path.Combine(folder,"acquisition-failure-01.json"))&&!Directory.GetFiles(folder,"building-lot-*.json").Any(),"ResumeOnlyBeforeAnyBuildingRequest");
            foreach(var input in Read(Path.Combine(folder,"acquisition-failure-01.json"))["preserved"]!.AsArray())Require(HashFile(Path.Combine(folder,S(input,"file")))==S(input,"sha256"),"ResumeInputDrift");
            await Save(folder,"acquisition-resume-claim.json",new{reason="UTF8BOM secret JSON reader corrected; initial building requests 0; metadata/spec reused without refetch",at=DateTimeOffset.UtcNow});
        }
        using var handler=new HttpClientHandler{AllowAutoRedirect=false};using var client=new HttpClient(handler){Timeout=TimeSpan.FromSeconds(30)};
        long total=0;var sources=new List<object>();var queries=new List<object>();
        async Task<byte[]> Fetch(string url)
        {
            using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(30));using var response=await client.GetAsync(url,HttpCompletionOption.ResponseHeadersRead,deadline.Token);
            Require(response.IsSuccessStatusCode,"HttpStatus:"+(int)response.StatusCode);
            await using var stream=await response.Content.ReadAsStreamAsync(deadline.Token);using var memory=new MemoryStream();var buffer=new byte[8192];int n;
            while((n=await stream.ReadAsync(buffer,deadline.Token))>0){total+=n;Require(memory.Length+n<=2*1024*1024&&total<=32*1024*1024,"LandHttpBudget");memory.Write(buffer,0,n);}return memory.ToArray();
        }
        async Task Receipt(string name)=>await Save(folder,name,new{schema="myeonmok-land-acquisition.r1",selectionHash=HashFile(Path.Combine(folder,"selection.json")),observedAtUtc=DateTimeOffset.UtcNow,sources,buildingQueries=queries,totalBytes=total});
        foreach(var source in Metadata)
        {
            try
            {
                var file=source.Id+"-metadata.json";var data=resume?Read(Path.Combine(folder,file)):JsonNode.Parse(await Fetch(source.Url))!;Require(S(data,"name")!="","MetadataShape");
                if(!resume)await Save(folder,file,data);sources.Add(new{source.Id,source.Url,status="MetadataAcquired;NotSpatialPayload",file,sha256=HashFile(Path.Combine(folder,file)),name=S(data,"name"),license=S(data,"license"),modified=S(data,"dateModified"),temporalCoverage=S(data,"temporalCoverage"),updateInterval=S(data,"datasetTimeInterval"),format=S(data,"encodingFormat")});
            }
            catch(Exception ex){sources.Add(new{source.Id,source.Url,status="MetadataUnavailable",error=ex is InvalidDataException?ex.Message:ex.GetType().Name});}
        }
        // 공식 포털에 실린 host/path를 확인하고 비밀값이 없는 명세 부분만 보존한다.
        bool contract=false;
        try
        {
            if(resume){var existing=Read(Path.Combine(folder,"building-api-spec.json"));Require(S(existing,"host")=="apis.data.go.kr/1613000/BldRgstHubService"&&existing["paths"]?["/getBrTitleInfo"]!=null,"BuildingApiContractChanged");contract=true;}
            else {
            var bytes=await Fetch("https://www.data.go.kr/data/15134735/openapi.do");var html=Encoding.UTF8.GetString(bytes);
            var match=Regex.Match(html,@"const swaggerJson = `(?<spec>.*?)`;",RegexOptions.Singleline);Require(match.Success,"BuildingApiSpecMissing");
            // 포털의 JavaScript 문자열 이스케이프와 내부 JSON을 구분한다.
            var specText=match.Groups["spec"].Value.Replace("\\\\","\\");
            JsonNode spec;try{spec=JsonNode.Parse(specText)!;}catch{spec=JsonNode.Parse(match.Groups["spec"].Value)!;}
            Require(S(spec,"host")=="apis.data.go.kr/1613000/BldRgstHubService"&&spec["paths"]?["/getBrTitleInfo"]!=null,"BuildingApiContractChanged");
            await Save(folder,"building-api-spec.json",spec);contract=true;}
        }
        catch(Exception ex){sources.Add(new{Id="building-api-spec",status="Unavailable",error=ex is InvalidDataException?ex.Message:ex.GetType().Name});}
        var secretPath=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"Microsoft/UserSecrets/47766019-f542-4cfc-9d4a-14b2fbfeac0e/secrets.json");
        string key="";if(File.Exists(secretPath)){var secrets=JsonNode.Parse(await File.ReadAllTextAsync(secretPath))!;key=S(secrets,"PublicData:DataGoKrServiceKey");}
        var blocked=contract?(string.IsNullOrWhiteSpace(key)?"MissingCredential":""):"ApiContractNotVerified";int queryIndex=0;
        foreach(var lot in selection.Samples.Select(x=>x.LotKey).Where(x=>x.Length>0).Distinct(StringComparer.Ordinal))
        {
            if(blocked!=""){queries.Add(new{lotKey=lot,status="NotRequested",reason=blocked});continue;}
            try
            {
                Require(Regex.IsMatch(lot,@"^1126010100[12]\d{8}$"),"BuildingQueryScope");var items=new List<JsonNode>();int totalCount=-1;
                for(int page=1;page<=3;page++)
                {
                    var url="https://apis.data.go.kr/1613000/BldRgstHubService/getBrTitleInfo?serviceKey="+Uri.EscapeDataString(Uri.UnescapeDataString(key))+"&_type=json&sigunguCd=11260&bjdongCd=10100&platGbCd="+(lot[10]=='1'?"0":"1")+"&bun="+lot.Substring(11,4)+"&ji="+lot.Substring(15,4)+"&numOfRows=100&pageNo="+page;
                    var bytes=await Fetch(url);var text=Encoding.UTF8.GetString(bytes);
                    Require(!text.Contains(key,StringComparison.Ordinal)&&!text.Contains(Uri.EscapeDataString(key),StringComparison.Ordinal),"CredentialEchoRejected");
                    JsonNode data;try{data=JsonNode.Parse(bytes)!;}catch{blocked=text.Contains("SERVICE_KEY_IS_NOT_REGISTERED_ERROR",StringComparison.Ordinal)?"SERVICE_KEY_IS_NOT_REGISTERED_ERROR":"NonJsonResponse";throw new InvalidDataException(blocked);}
                    var code=S(data["response"]?["header"],"resultCode");Require(code=="00","BuildingResultCode:"+code);
                    var body=data["response"]!["body"]!;var count=int.Parse(S(body,"totalCount"),CultureInfo.InvariantCulture);Require(count<=300,"BuildingLotRowBudget");if(totalCount<0)totalCount=count;Require(totalCount==count,"BuildingPaginationChanged");
                    var pageItems=body["items"] is JsonObject obj?obj["item"]:null;
                    var batch=pageItems is JsonArray array?array.Select(x=>x!).ToArray():pageItems is JsonObject?[pageItems]:[];
                    // 소유자 개인정보는 요청하지 않으며 표제부의 선택한 공개 속성만 보존한다.
                    foreach(var raw in batch){var selected=new JsonObject();foreach(var name in new[]{"mgmBldrgstPk","regstrGbCd","regstrKindCd","platPlc","newPlatPlc","sigunguCd","bjdongCd","platGbCd","bun","ji","bldNm","dongNm","mainPurpsCd","mainPurpsCdNm","etcPurps","strctCd","strctCdNm","archArea","totArea","platArea","heit","grndFlrCnt","ugrndFlrCnt","useAprDay","hhldCnt","fmlyCnt","hoCnt","crtnDay","naRoadCd","naMainBun","naSubBun","naUgrndCd"})selected[name]=raw[name]?.DeepClone();items.Add(selected);}
                    if(items.Count==count)break;Require(batch.Length>0,"BuildingEmptyPage");
                }
                Require(items.Count==totalCount,"BuildingIncompletePagination");Require(items.Select(x=>S(x,"mgmBldrgstPk")).Distinct().Count()==items.Count,"BuildingDuplicatePk");
                var filename="building-lot-"+(++queryIndex).ToString("D2")+".json";await Save(folder,filename,new{lotKey=lot,query="sigungu11260/bjdong10100/lot;getBrTitleInfo",totalCount,items,observedAtUtc=DateTimeOffset.UtcNow,format="SelectedPublicFields;NotCompleteHttpBody"});
                queries.Add(new{lotKey=lot,status="Acquired",file=filename,sha256=HashFile(Path.Combine(folder,filename)),totalCount});
            }
            catch(Exception ex)
            {
                var error=ex is InvalidDataException?ex.Message:ex.GetType().Name;queries.Add(new{lotKey=lot,status="Blocked",error});blocked=error;
            }
        }
        await Receipt("acquisition.json");result["metadataSources"]=sources.Count;result["buildingLots"]=queries.Count;result["firstBuildingBlock"]=blocked;result["bytesReceived"]=total;
    }
    internal static GisEvidence ReadGis(string root,HashSet<string> lots)
    {
        var path=Path.Combine(root,"artifacts/local/neighborhood-source-acquisition/AL_D010_11_20260809.zip");Require(HashFile(path)=="674C5A9583996DD6B8946525EDAD8197BE79A634F1DB00D39E2B3133D0D2A755","GisSourceDrift");
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);var encoding=Encoding.GetEncoding(949,EncoderFallback.ExceptionFallback,DecoderFallback.ExceptionFallback);
        using var zip=ZipFile.OpenRead(path);using var dbf=zip.GetEntry("AL_D010_11_20260809.dbf")!.Open();var header=new byte[32];dbf.ReadExactly(header);
        int count=BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(4)),hlen=BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(8)),rlen=BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(10));
        Require(count==695761&&hlen==961&&rlen==1815,"GisDbfHeader");var fields=new List<(string Name,int Offset,int Length)>();int offset=1;
        for(int i=0;i<29;i++){var descriptor=new byte[32];dbf.ReadExactly(descriptor);var name=Encoding.ASCII.GetString(descriptor,0,11).TrimEnd('\0');Require(name=="A"+i,"GisFieldContract");fields.Add((name,offset,descriptor[16]));offset+=descriptor[16];}Require(dbf.ReadByte()==13&&offset==rlen,"GisDbfLayout");
        var picked=new Dictionary<int,Dictionary<string,string>>();var row=new byte[rlen];int regionCount=0;
        string Value(string field,Encoding decoder){var f=fields.Single(x=>x.Name==field);return decoder.GetString(row,f.Offset,f.Length).Trim(' ','\0');}
        for(int i=0;i<count;i++)
        {
            dbf.ReadExactly(row);Require(row[0]==32,"GisDeletedOrUnknownRow");if(Value("A3",Encoding.ASCII)!="1126010100")continue;regionCount++;
            if(!lots.Contains(Value("A2",Encoding.ASCII)))continue;
            Require(picked.Count<2000,"GisSampleCandidateBudget");picked.Add(i,fields.ToDictionary(x=>x.Name,x=>encoding.GetString(row,x.Offset,x.Length).Trim(' ','\0')));
        }
        Require(regionCount==13389,"GisRegionCountChanged");using var prjReader=new StreamReader(zip.GetEntry("AL_D010_11_20260809.prj")!.Open());var prj=prjReader.ReadToEnd();Require(prj.Contains("5186",StringComparison.Ordinal)&&prj.Contains("GRS",StringComparison.Ordinal),"GisCrsContract");
        using var shp=zip.GetEntry("AL_D010_11_20260809.shp")!.Open();var shpHeader=new byte[100];shp.ReadExactly(shpHeader);Require(BinaryPrimitives.ReadInt32BigEndian(shpHeader)==9994&&BinaryPrimitives.ReadInt32LittleEndian(shpHeader.AsSpan(32))==5,"ShpHeader");
        var found=new Dictionary<int,object>();int record=0;var recordHeader=new byte[8];
        while(shp.Read(recordHeader,0,1)==1)
        {
            shp.ReadExactly(recordHeader.AsSpan(1));Require(BinaryPrimitives.ReadInt32BigEndian(recordHeader)==record+1,"ShpRecordOrder");int bytes=checked(BinaryPrimitives.ReadInt32BigEndian(recordHeader.AsSpan(4))*2);Require(bytes>=4&&bytes<=2*1024*1024,"ShpRecordBudget");var body=new byte[bytes];shp.ReadExactly(body);
            if(picked.ContainsKey(record))found.Add(record,Geometry(body));record++;
        }
        Require(record==count&&found.Count==picked.Count,"ShpDbfAlignment");
        return new(HashFile(path),prj,"StrictCP949CandidateOnly;NoCpg;ProviderEncodingUnconfirmed","LicenseDisplayConflict;PrivateReviewOnly;NoPublicationOrGameUse",count,regionCount,picked.Select(x=>new GisRow(x.Key,x.Value,found[x.Key])).ToArray());
    }
    private static object Geometry(byte[] body)
    {
        if(BinaryPrimitives.ReadInt32LittleEndian(body)!=5)return new{status="UnsupportedOrNullShape",type=BinaryPrimitives.ReadInt32LittleEndian(body),sha256=Hash(body)};
        Require(body.Length>=44,"ShpPolygonTruncated");int parts=BinaryPrimitives.ReadInt32LittleEndian(body.AsSpan(36)),points=BinaryPrimitives.ReadInt32LittleEndian(body.AsSpan(40));
        Require(parts>0&&points>=4&&body.Length==44+4*parts+16*points,"ShpPolygonLength");
        var starts=Enumerable.Range(0,parts).Select(i=>BinaryPrimitives.ReadInt32LittleEndian(body.AsSpan(44+i*4))).Append(points).ToArray();Require(starts[0]==0&&starts.Zip(starts.Skip(1)).All(x=>x.First<x.Second),"ShpPartOrder");
        var xy=Enumerable.Range(0,points).Select(i=>new[]{BitConverter.ToDouble(body,44+4*parts+i*16),BitConverter.ToDouble(body,52+4*parts+i*16)}).ToArray();
        Require(xy.All(x=>x.All(double.IsFinite)),"ShpNonFinite");var closed=Enumerable.Range(0,parts).All(i=>starts[i+1]-starts[i]>=4&&xy[starts[i]].SequenceEqual(xy[starts[i+1]-1]));
        return new{status=closed?"FiniteClosedRings;TopologyNotValidated":"OpenOrShortRing",parts,points,bbox=new[]{BitConverter.ToDouble(body,4),BitConverter.ToDouble(body,12),BitConverter.ToDouble(body,20),BitConverter.ToDouble(body,28)},sha256=Hash(body),topologicalValidity="NotTested",crs="EPSG:5186",coordinateTransformPerformed=false};
    }
    internal static int SelfTest()
    {
        int n=0;void Test(bool ok){Require(ok,"GisSelfTest:"+(n+1));n++;}
        Test(JsonSerializer.Serialize(Geometry(new byte[4])).Contains("UnsupportedOrNullShape",StringComparison.Ordinal));
        var polygon=new byte[44+4+5*16];BinaryPrimitives.WriteInt32LittleEndian(polygon,5);BinaryPrimitives.WriteInt32LittleEndian(polygon.AsSpan(36),1);BinaryPrimitives.WriteInt32LittleEndian(polygon.AsSpan(40),5);
        var points=new[]{new[]{0d,0d},new[]{1d,0d},new[]{1d,1d},new[]{0d,1d},new[]{0d,0d}};
        for(int i=0;i<5;i++){BitConverter.GetBytes(points[i][0]).CopyTo(polygon,48+i*16);BitConverter.GetBytes(points[i][1]).CopyTo(polygon,56+i*16);}
        Test(JsonSerializer.Serialize(Geometry(polygon)).Contains("FiniteClosedRings",StringComparison.Ordinal));
        var broken=(byte[])polygon.Clone();BitConverter.GetBytes(2d).CopyTo(broken,48+4*16);Test(JsonSerializer.Serialize(Geometry(broken)).Contains("OpenOrShortRing",StringComparison.Ordinal));
        var nan=(byte[])polygon.Clone();BitConverter.GetBytes(double.NaN).CopyTo(nan,48);bool blocked=false;try{Geometry(nan);}catch(InvalidDataException){blocked=true;}Test(blocked);
        blocked=false;try{Geometry(polygon[..^1]);}catch(InvalidDataException){blocked=true;}Test(blocked);return n;
    }
}
