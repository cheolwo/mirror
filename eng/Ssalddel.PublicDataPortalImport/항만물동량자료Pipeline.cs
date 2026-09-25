using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Domain.PublicData;
using Ssalddel.Infrastructure.Persistence.PublicData;
using 살뜰.Services.External.PublicData.Korea;

internal static class 항만물동량자료Pipeline
{
    const string Folder="artifacts/local/public-data/kcs-port-movement-2025-r1";
    const string Dataset="country-port-movement-2025",Version="kcs-port-movement.r1";
    static readonly string[] Countries=["중국","일본","미국","호주","베트남"];
    static readonly string[] Metrics=["blCnt","blWght","teuEmcntnGcnt","teuFclGcnt","teuGcnt","teuLclGcnt"];
    record Receipt(string Id,string Url,string Hash,DateTimeOffset At,int Rows);
    static void Check(bool ok,string code){if(!ok)throw new InvalidDataException(code);}
    static string Hash(byte[] bytes)=>Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    static async Task Write(string path,byte[] bytes){await using var file=new FileStream(path,FileMode.CreateNew);await file.WriteAsync(bytes);}
    internal static Dictionary<string,string>[] Parse(byte[] bytes)
    {
        Check(bytes.Length<=32*1024*1024,"PortSizeLimit");
        using var stream=new MemoryStream(bytes);using var reader=XmlReader.Create(stream,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=32*1024*1024});
        var xml=XDocument.Load(reader);Check(xml.Root?.Element("header")?.Element("resultCode")?.Value=="00","PortProviderFailure");
        var rows=xml.Descendants("item").Select(x=>x.Elements().ToDictionary(e=>e.Name.LocalName,e=>e.Value.Trim())).ToArray();
        Check(rows.Length>0,"PortEmptyResponse");
        var total=xml.Descendants("totalCount").SingleOrDefault()?.Value;
        if(total!=null)Check(int.TryParse(total,out var count)&&count==rows.Length,"PortTruncatedResponse");
        var keys=new HashSet<string>();
        foreach(var row in rows)
        {
            Check(row.TryGetValue("yyyymm",out var month)&&Enumerable.Range(1,12).Any(m=>month==$"2025.{m:00}"),"PortWrongPeriod");
            Check(row.ContainsKey("arvlPlcNm")&&row.ContainsKey("dptrPlcNm"),"PortPlaceMissing");
            Check(keys.Add(month+"|"+row["arvlPlcNm"]+"|"+row["dptrPlcNm"]),"PortDuplicateRow");
            foreach(var metric in Metrics)Check(row.TryGetValue(metric,out var value)&&decimal.TryParse(value,NumberStyles.Number,CultureInfo.InvariantCulture,out var number)&&number>=0,"PortMetricInvalid");
        }
        Check(rows.Select(x=>x["yyyymm"]).Distinct().Count()==12,"PortMonthCoverageMissing");return rows;
    }
    public static async Task RunAsync(string mode,string root,Dictionary<string,object?> result)
    {
        Check(mode is "acquire" or "apply" or "verify" or "self-test" or "preview","PortModeInvalid");
        var folder=Path.Combine(root,Folder);Directory.CreateDirectory(folder);
        var ids=new[]{"arrival-sea","arrival-air","departure-sea","departure-air"};
        if(mode=="acquire")
        {
            using var http=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false}){Timeout=TimeSpan.FromSeconds(60),MaxResponseContentBufferSize=32*1024*1024};
            http.DefaultRequestHeaders.UserAgent.ParseAdd("Ssalddel-PublicDataReview/1.0");
            foreach(var id in new[]{"15151805","15151809"})
            {
                var meta=await http.GetByteArrayAsync($"https://www.data.go.kr/catalog/{id}/openapi.json");
                using var json=JsonDocument.Parse(meta);Check(json.RootElement.GetProperty("license").GetString()!.Contains("제한 없음"),"PortLicenseReviewRequired");
                var path=Path.Combine(folder,id+".metadata.json");if(!File.Exists(path))await Write(path,meta);
            }
            using var secrets=JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"Microsoft/UserSecrets/47766019-f542-4cfc-9d4a-14b2fbfeac0e/secrets.json")));
            var key=Uri.UnescapeDataString(secrets.RootElement.GetProperty("PublicData:DataGoKrServiceKey").GetString()!);Check(!string.IsNullOrWhiteSpace(key),"PortCredentialMissing");
            foreach(var id in ids)
            {
                var path=Path.Combine(folder,id+".xml");var receiptPath=Path.Combine(folder,id+".receipt.json");
                if(File.Exists(receiptPath)){var saved=JsonSerializer.Deserialize<Receipt>(await File.ReadAllTextAsync(receiptPath))!;Check(saved.Id==id&&Hash(await File.ReadAllBytesAsync(path))==saved.Hash,"PortResumeMismatch");continue;}
                Check(!File.Exists(path),"PortUnreceiptedFile");var arrival=id.StartsWith("arrival");
                var endpoint=arrival?"cntyperetprgtqtyacrs/getCntyPerEtprGtqtyAcrs":"cntyperportgtqtyacrs/getCntyPerPortGtqtyAcrs";
                var url=$"https://apis.data.go.kr/1220000/{endpoint}?strtYymm=202501&endYymm=202512&cargTpcd={(arrival?"I":"E")}&seaFlghTpcd={(id.EndsWith("sea")?"10":"40")}";
                byte[] bytes;try{bytes=await http.GetByteArrayAsync(url+"&serviceKey="+Uri.EscapeDataString(key));}catch{throw new InvalidDataException("PortFetchFailed:"+id);}
                var text=Encoding.UTF8.GetString(bytes);Check(!text.Contains(key)&&!text.Contains(Uri.EscapeDataString(key)),"PortCredentialEcho");var rows=Parse(bytes);
                await Write(path,bytes);await Write(receiptPath,JsonSerializer.SerializeToUtf8Bytes(new Receipt(id,url,Hash(bytes),DateTimeOffset.UtcNow,rows.Length)));
            }
            result["acquiredResponses"]=4;return;
        }
        var expected=new List<외부데이터정규화Record>();var receipts=new List<Receipt>();var rawCount=0;
        foreach(var id in ids)
        {
            var receipt=JsonSerializer.Deserialize<Receipt>(await File.ReadAllTextAsync(Path.Combine(folder,id+".receipt.json")))!;receipts.Add(receipt);
            var bytes=await File.ReadAllBytesAsync(Path.Combine(folder,id+".xml"));Check(receipt.Id==id&&Hash(bytes)==receipt.Hash,"PortHashMismatch");var rows=Parse(bytes);Check(rows.Length==receipt.Rows,"PortReceiptRowMismatch");rawCount+=rows.Length;
            foreach(var row in rows.Where(x=>Countries.Contains(x[id.StartsWith("arrival")?"dptrPlcNm":"arvlPlcNm"])))
            {
                var at=new DateTimeOffset(2025,int.Parse(row["yyyymm"].Split('.')[1]),1,0,0,0,TimeSpan.Zero);
                var identity=Hash(Encoding.UTF8.GetBytes(id+"|"+row["yyyymm"]+"|"+row["dptrPlcNm"]+"|"+row["arvlPlcNm"]));
                expected.Add(new 외부데이터정규화Record{SourceId="kcs",DatasetId=Dataset,StableId="kcs:port:"+identity,RecordKey=외부데이터RecordKey.Create("kcs",Dataset,"country:KR","portMovementSourceRow",at,identity),RegionStableId="country:KR",MetricCode="portMovementSourceRow",NumericValue=null,UnitCode="source-row",TextValue=JsonSerializer.Serialize(new{direction=id,sourceUrl=receipt.Url,fields=row}),EvidenceAsOfUtc=at,CollectedAtUtc=receipt.At,FirstSeenAtUtc=receipt.At,LastSeenAtUtc=receipt.At,SourceVersion=Version,DataRevision=receipt.Hash,DimensionKey=identity,TemporalPrecisionCode="calendar-month",SpatialPrecisionCode="country-port",QualityCode="PendingHumanReview",LimitationCode="PrivateReviewOnly;WeightUnitReviewRequired;NoPortAggregation;NotVesselCount"});
            }
        }
        if(mode=="self-test")
        {
            var bytes=await File.ReadAllBytesAsync(Path.Combine(folder,ids[0]+".xml"));var doc=XDocument.Parse(Encoding.UTF8.GetString(bytes));var passed=1;
            void Reject(Action<XDocument> edit){var copy=new XDocument(doc);edit(copy);try{Parse(Encoding.UTF8.GetBytes(copy.ToString()));}catch(InvalidDataException){passed++;return;}throw new InvalidDataException("PortRejectionTestFailed");}
            Reject(d=>d.Descendants("resultCode").Single().Value="99");Reject(d=>d.Descendants("yyyymm").First().Value="2026.01");Reject(d=>d.Descendants("blCnt").First().Value="-1");Reject(d=>d.Descendants("blWght").First().Remove());Reject(d=>d.Descendants("items").Single().Add(new XElement(d.Descendants("item").First())));
            result["passed"]=passed;result["selectedRows"]=expected.Count;return;
        }
        var options=await 로컬공공자료Db.OptionsAsync(root);var inserted=0;
        if(mode=="apply")
        {
            result["databaseWriteAttempted"]=true;await using var db=new PublicDataIngestionDbContext(options);await using var tx=await db.Database.BeginTransactionAsync();
            var previous=await db.NormalizedRecords.Where(x=>x.SourceId=="kcs"&&x.DatasetId==Dataset).ToDictionaryAsync(x=>x.RecordKey);
            foreach(var receipt in receipts)
            {
                var source=await new 평창군공공공간원본등록Service(db).RegisterFileAsync(Path.Combine(folder,receipt.Id+".xml"),new 공공공간원본등록Request("kcs",Dataset,Version,receipt.Hash,new DateTimeOffset(2025,1,1,0,0,0,TimeSpan.Zero),"application/xml",Folder+"/"+receipt.Id+".xml"));
                foreach(var row in expected.Where(x=>x.DataRevision==receipt.Hash)){if(previous.TryGetValue(row.RecordKey,out var prior)){Check(prior.TextValue==row.TextValue&&prior.DataRevision==row.DataRevision&&prior.NumericValue==null,"PortImmutableConflict");continue;}row.RawSnapshotId=source.RawSnapshotId;db.NormalizedRecords.Add(row);inserted++;}
            }
            await db.SaveChangesAsync();await tx.CommitAsync();result["committed"]=true;
        }
        await using var read=new PublicDataIngestionDbContext(options);var stored=await read.NormalizedRecords.AsNoTracking().Include(x=>x.RawSnapshot).Where(x=>x.SourceId=="kcs"&&x.DatasetId==Dataset).ToDictionaryAsync(x=>x.RecordKey);
        Check(stored.Count==expected.Count&&expected.All(x=>stored.TryGetValue(x.RecordKey,out var s)&&s.TextValue==x.TextValue&&s.DataRevision==x.DataRevision&&s.NumericValue==null&&s.RawSnapshot?.SourceId=="kcs"),"PortReadbackMismatch");
        result["inserted"]=inserted;result["verified"]=stored.Count;result["rawRows"]=rawCount;result["groups"]=receipts.Select(r=>new{id=r.Id,rawRows=r.Rows,selectedRows=expected.Count(x=>x.DataRevision==r.Hash),hash=r.Hash});
        if(mode=="preview")
        {
            var codes=new Dictionary<string,string>{{"중국","CN"},{"일본","JP"},{"미국","US"},{"호주","AU"},{"베트남","VN"}};
            var observations=stored.Values.Select(r=>
            {
                using var j=JsonDocument.Parse(r.TextValue!);var e=j.RootElement;var direction=e.GetProperty("direction").GetString()!;
                var fields=e.GetProperty("fields");var arrival=direction.StartsWith("arrival");
                return new{partner=codes[fields.GetProperty(arrival?"dptrPlcNm":"arvlPlcNm").GetString()!],direction=arrival?"import":"export",mode=direction.EndsWith("sea")?"Sea":"Air",month=fields.GetProperty("yyyymm").GetString()!,port=fields.GetProperty(arrival?"arvlPlcNm":"dptrPlcNm").GetString()!,blCount=decimal.Parse(fields.GetProperty("blCnt").GetString()!,NumberStyles.Number,CultureInfo.InvariantCulture),rawWeight=fields.GetProperty("blWght").GetString()!,revision=r.DataRevision,stableId=r.StableId,sourceUrl=arrival?"https://www.data.go.kr/data/15151805/openapi.do":"https://www.data.go.kr/data/15151809/openapi.do"};
            }).GroupBy(x=>new{x.partner,x.direction,x.mode}).Select(g=>g.OrderByDescending(x=>x.month,StringComparer.Ordinal).ThenByDescending(x=>x.blCount).ThenBy(x=>x.stableId,StringComparer.Ordinal).First()).OrderBy(x=>x.partner,StringComparer.Ordinal).ThenBy(x=>x.mode,StringComparer.Ordinal).ThenBy(x=>x.direction,StringComparer.Ordinal).ToArray();
            var bytes=JsonSerializer.SerializeToUtf8Bytes(new{schemaVersion="transport-observation-preview.v1",year=2025,localReviewOnly=true,distributionApproved=false,selectionRule="LatestMonthLargestBlRow",collectedAtUtc=receipts.Max(r=>r.At).ToString("O"),items=observations});
            var path=Path.Combine(folder,"transport-preview.json");
            if(File.Exists(path))Check(Hash(await File.ReadAllBytesAsync(path))==Hash(bytes),"PortPreviewImmutableConflict");else await Write(path,bytes);
            if(File.Exists(path+".sha256"))Check((await File.ReadAllTextAsync(path+".sha256")).Trim()==Hash(bytes),"PortPreviewHashConflict");else await Write(path+".sha256",Encoding.UTF8.GetBytes(Hash(bytes)));
            result["previewCount"]=observations.Length;result["previewHash"]=Hash(bytes);
        }
    }
}
