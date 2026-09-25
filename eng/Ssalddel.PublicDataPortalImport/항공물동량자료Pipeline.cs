using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Domain.PublicData;
using Ssalddel.Infrastructure.Persistence.PublicData;
using 살뜰.Services.External.PublicData.Korea;

internal static class 항공물동량자료Pipeline
{
    private const string Folder="artifacts/local/public-data/air-movement-2025-r1";
    private const string Source="iiac",Dataset="country-air-movement-2025";
    private const string Page="https://www.data.go.kr/data/15062056/fileData.do";
    private const string Download="https://www.data.go.kr/cmm/cmm/fileDownload.do?atchFileId=FILE_000000003583710&fileDetailSn=2&insertDataPrcus=N";
    private static readonly Dictionary<string,string> Countries=new(){["중국"]="CN",["일본"]="JP",["미국"]="US",["호주"]="AU",["베트남"]="VN"};
    private static readonly string[] Fields=["직화물(kg)","환적화물(kg)","우편물(kg)","운항(편)"];
    private static readonly string[] Metrics=["directCargoKg","transferCargoKg","mailKg","flightMovements"];
    internal sealed record Point(string Partner,int Month,string Direction,string AircraftType,decimal[] Values);
    private sealed record Receipt(string Hash,DateTimeOffset CollectedAt,int RawRows,string Url);
    private static string Hash(byte[] bytes)=>Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    private static void Check(bool pass,string code){if(!pass)throw new InvalidDataException(code);}
    private static async Task Write(string path,byte[] bytes){await using var f=new FileStream(path,FileMode.CreateNew);await f.WriteAsync(bytes);}
    internal static (int RawRows,Point[] Points) Parse(byte[] bytes)
    {
        Check(bytes.Length<=20*1024*1024,"AirMovementTooLarge");
        using var json=JsonDocument.Parse(Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF'));
        Check(json.RootElement.ValueKind==JsonValueKind.Array,"AirMovementArrayRequired");
        var rows=json.RootElement.EnumerateArray().ToArray();Check(rows.Length>0&&rows.Length<100000,"AirMovementRowCountInvalid");
        var seen=new HashSet<string>();var sums=new Dictionary<(string,int,string,string),decimal[]>();var months=new HashSet<int>();
        string Get(JsonElement r,string key){Check(r.TryGetProperty(key,out var v)&&v.ValueKind==JsonValueKind.String,"AirMovementFieldMissing:"+key);return v.GetString()!.Trim();}
        foreach(var r in rows)
        {
            Check(Get(r,"년도")=="2025","AirMovementWrongYear");
            Check(int.TryParse(Get(r,"월"),out var month)&&month>=1&&month<=12,"AirMovementMonthInvalid");months.Add(month);
            var country=Get(r,"국가");var domestic=Get(r,"국제_국내");var direction=Get(r,"도착_출발");var aircraft=Get(r,"여객_화물");
            Check(direction is "도착" or "출발" && aircraft is "여객" or "화물","AirMovementCategoryInvalid");
            var key=string.Join("|",month,country,domestic,direction,aircraft,Get(r,"항공사(ICAO)"),Get(r,"항공사(IATA)"),Get(r,"공항"),Get(r,"정기_부정기"),Get(r,"노선"));
            Check(seen.Add(key),"AirMovementDuplicateRow");
            var values=Fields.Select(f=>{Check(decimal.TryParse(Get(r,f),NumberStyles.Number,CultureInfo.InvariantCulture,out var n)&&n>=0,"AirMovementNumberInvalid");return n;}).ToArray();
            Check(values[3]==decimal.Truncate(values[3]),"AirMovementFractionalFlight");
            if(domestic!="국제"||!Countries.TryGetValue(country,out var code))continue;
            var dimension=(code,month,direction=="도착"?"arrival":"departure",aircraft=="여객"?"passenger":"freighter");
            if(!sums.TryGetValue(dimension,out var sum))sums[dimension]=sum=new decimal[4];
            for(var i=0;i<4;i++)sum[i]+=values[i];
        }
        Check(months.Count==12,"AirMovementTwelveMonthsRequired");
        foreach(var country in Countries.Values)foreach(var direction in new[]{"arrival","departure"})
            Check(sums.Keys.Where(k=>k.Item1==country&&k.Item3==direction).Select(k=>k.Item2).Distinct().Count()==12,"AirMovementCountryCoverageMissing");
        return(rows.Length,sums.OrderBy(x=>x.Key.Item1).ThenBy(x=>x.Key.Item2).ThenBy(x=>x.Key.Item3).ThenBy(x=>x.Key.Item4)
            .Select(x=>new Point(x.Key.Item1,x.Key.Item2,x.Key.Item3,x.Key.Item4,x.Value)).ToArray());
    }
    public static async Task RunAsync(string mode,string root,Dictionary<string,object?> result)
    {
        var folder=Path.Combine(root,Folder);Directory.CreateDirectory(folder);var raw=Path.Combine(folder,"iiac-2025.json");var receiptPath=Path.Combine(folder,"receipt.json");
        if(mode=="acquire")
        {
            Check(!File.Exists(receiptPath)&&!File.Exists(raw),"AirMovementAlreadyAcquired");
            using var http=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false}){Timeout=TimeSpan.FromSeconds(45),MaxResponseContentBufferSize=20*1024*1024};
            http.DefaultRequestHeaders.UserAgent.ParseAdd("Ssalddel-PublicDataReview/1.0");
            foreach(var id in new[]{"15151805","15151809","15095071","15062056"})
            {
                if(File.Exists(Path.Combine(folder,id+".metadata.json")))continue;
                var metadata=await http.GetByteArrayAsync($"https://www.data.go.kr/catalog/{id}/{(id=="15062056"?"fileData":"openapi")}.json");
                using var m=JsonDocument.Parse(metadata);Check(m.RootElement.GetProperty("license").GetString()!.Contains("제한 없음"),"AirMovementLicenseReviewRequired");
                await Write(Path.Combine(folder,id+".metadata.json"),metadata);
            }
            using var response=await http.PostAsync("https://www.data.go.kr/tcs/dss/selectDpkDetailInfo.do",new FormUrlEncodedContent(new Dictionary<string,string>{{"publicDataDetailPk","uddi:ce34b4c8-e4a5-4cdd-9939-e35e43f8574e"}}));response.EnsureSuccessStatusCode();
            var detail=await response.Content.ReadAsByteArrayAsync();var html=Encoding.UTF8.GetString(detail);
            Check(html.Contains("FILE_000000003583710")&&html.Contains("이용허락범위 제한 없음"),"AirMovementHistoricFileChanged");if(!File.Exists(Path.Combine(folder,"2025-detail.html")))await Write(Path.Combine(folder,"2025-detail.html"),detail);
            byte[] bytes;try{bytes=await http.GetByteArrayAsync(Download);}catch(HttpRequestException ex){throw new InvalidDataException("AirMovementDownloadFailed:"+ex.StatusCode+":"+ex.HttpRequestError);}
            var parsed=Parse(bytes);
            await Write(raw,bytes);await Write(receiptPath,JsonSerializer.SerializeToUtf8Bytes(new Receipt(Hash(bytes),DateTimeOffset.UtcNow,parsed.RawRows,Download)));
            result["rawRows"]=parsed.RawRows;result["groups"]=parsed.Points.Length;result["hash"]=Hash(bytes);return;
        }
        Check(mode is "apply" or "verify" or "self-test","AirMovementModeInvalid");
        var receipt=JsonSerializer.Deserialize<Receipt>(await File.ReadAllTextAsync(receiptPath))!;var data=await File.ReadAllBytesAsync(raw);Check(Hash(data)==receipt.Hash,"AirMovementHashMismatch");
        var facts=Parse(data);Check(facts.RawRows==receipt.RawRows&&receipt.Url==Download,"AirMovementReceiptMismatch");
        if(mode=="self-test")
        {
            var text=Encoding.UTF8.GetString(data).TrimStart('\uFEFF');var passed=1;
            void Reject(string value){try{Parse(Encoding.UTF8.GetBytes(value));}catch(InvalidDataException){passed++;return;}throw new InvalidDataException("AirMovementRejectionFailed");}
            Reject(text.Replace("\"2025\"","\"2026\""));
            using var d=JsonDocument.Parse(text);var first=d.RootElement[0].GetRawText();
            Reject("["+first+"]");Reject(text.TrimEnd().TrimEnd(']')+","+first+"]");
            Reject(text.Replace("직화물(kg)","missingCargo"));
            result["passed"]=passed;return;
        }
        var expected=new List<외부데이터정규화Record>();
        foreach(var p in facts.Points)for(var i=0;i<4;i++)
        {
            var metric=Metrics[i];var at=new DateTimeOffset(2025,p.Month,1,0,0,0,TimeSpan.Zero);
            var dim=$"partner={p.Partner};direction={p.Direction};aircraft={p.AircraftType};hash={receipt.Hash}";
            expected.Add(new 외부데이터정규화Record{SourceId=Source,DatasetId=Dataset,StableId=$"iiac:2025:{p.Month:00}:{p.Partner}:{p.Direction}:{p.AircraftType}:{metric}",
                RecordKey=외부데이터RecordKey.Create(Source,Dataset,"point:airport:icn",metric,at,dim),RegionStableId="point:airport:icn",MetricCode=metric,NumericValue=p.Values[i],UnitCode=i==3?"flight-movement":"kg",
                TextValue=JsonSerializer.Serialize(new{partner=p.Partner,month=p.Month,direction=p.Direction,aircraftType=p.AircraftType,scope="ICNOnly",sourceUrl=Page,metric,value=p.Values[i]}),
                EvidenceAsOfUtc=at,CollectedAtUtc=receipt.CollectedAt,FirstSeenAtUtc=receipt.CollectedAt,LastSeenAtUtc=receipt.CollectedAt,
                SourceVersion="iiac-monthly-movement.r1",DataRevision=receipt.Hash,DimensionKey=dim,TemporalPrecisionCode="calendar-month",SpatialPrecisionCode="airport-country-route-statistics",
                QualityCode="PendingHumanReview",LimitationCode="PrivateReviewOnly;ICNOnly;NotCustomsTrade;NotUniqueAircraft;BaggageExcluded"});
        }
        var options=await 로컬공공자료Db.OptionsAsync(root);var inserted=0;
        if(mode=="apply")
        {
            result["databaseWriteAttempted"]=true;await using var db=new PublicDataIngestionDbContext(options);await using var tx=await db.Database.BeginTransactionAsync();
            var source=await new 평창군공공공간원본등록Service(db).RegisterFileAsync(raw,new 공공공간원본등록Request(Source,Dataset,"iiac-monthly-movement.r1",receipt.Hash,new DateTimeOffset(2025,1,1,0,0,0,TimeSpan.Zero),"application/json",Folder+"/iiac-2025.json"));
            var prior=await db.NormalizedRecords.Where(x=>x.SourceId==Source&&x.DatasetId==Dataset&&x.DataRevision==receipt.Hash).ToDictionaryAsync(x=>x.RecordKey);
            foreach(var record in expected)
            {
                if(prior.TryGetValue(record.RecordKey,out var previous)){Check(previous.NumericValue==record.NumericValue&&previous.TextValue==record.TextValue&&previous.UnitCode==record.UnitCode,"AirMovementImmutableConflict");continue;}
                record.RawSnapshotId=source.RawSnapshotId;db.NormalizedRecords.Add(record);inserted++;
            }
            await db.SaveChangesAsync();await tx.CommitAsync();result["committed"]=true;
        }
        await using var read=new PublicDataIngestionDbContext(options);
        var stored=await read.NormalizedRecords.AsNoTracking().Include(x=>x.RawSnapshot).Where(x=>x.SourceId==Source&&x.DatasetId==Dataset&&x.DataRevision==receipt.Hash).ToDictionaryAsync(x=>x.RecordKey);
        Check(stored.Count==expected.Count&&expected.All(e=>stored.TryGetValue(e.RecordKey,out var r)&&r.NumericValue==e.NumericValue&&r.UnitCode==e.UnitCode&&r.TextValue==e.TextValue&&r.RawSnapshot?.SourceId==Source),"AirMovementReadbackMismatch");
        result["inserted"]=inserted;result["verified"]=stored.Count;result["rawRows"]=facts.RawRows;result["hash"]=receipt.Hash;
        result["annual"]=facts.Points.GroupBy(p=>p.Partner).Select(g=>new{partner=g.Key,directCargoKg=g.Sum(p=>p.Values[0]),transferCargoKg=g.Sum(p=>p.Values[1]),mailKg=g.Sum(p=>p.Values[2]),flightMovements=g.Sum(p=>p.Values[3])});
    }
}
