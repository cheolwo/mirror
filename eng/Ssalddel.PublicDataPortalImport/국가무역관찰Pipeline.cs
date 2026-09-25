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

internal static class 국가무역관찰Pipeline
{
    private const string Folder="artifacts/local/public-data/korea-trade-2025-r1";
    private const string Source="kcs";
    private const string Dataset="country-annual-total-trade-2025";
    private static readonly string[] Countries=["CN","JP","US","AU","VN"];
    private static readonly string[] Metrics=["expWgt","expDlr","impWgt","impDlr"];
    private static readonly JsonSerializerOptions Pretty=new(){WriteIndented=true};
    internal sealed record Entry(string Partner,string Url,string Hash,DateTimeOffset CollectedAt);
    internal sealed record Facts(string Partner,decimal ExportKg,decimal ExportUsd,decimal ImportKg,decimal ImportUsd,int DetailRows,decimal WeightResidualKg);
    private static void Check(bool pass,string code){if(!pass)throw new InvalidDataException(code);}
    private static string Hash(byte[] data)=>Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();
    private static async Task Write(string path,byte[] bytes){await using var file=new FileStream(path,FileMode.CreateNew);await file.WriteAsync(bytes);}
    internal static Facts Parse(byte[] bytes,string partner)
    {
        Check(Countries.Contains(partner)&&bytes.Length<=50*1024*1024,"TradeScopeOrSizeInvalid");
        using var input=new MemoryStream(bytes);
        using var reader=XmlReader.Create(input,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=50*1024*1024});
        var doc=XDocument.Load(reader);Check(doc.Root?.Element("header")?.Element("resultCode")?.Value=="00","TradeProviderFailure");
        var items=doc.Root!.Element("body")?.Element("items")?.Elements("item").ToArray()??[];
        var totals=items.Where(x=>(string?)x.Element("year")=="총계").ToArray();Check(totals.Length==1,"TradeAnnualTotalMissingOrDuplicate");
        var detail=items.Where(x=>(string?)x.Element("year")!="총계").ToArray();Check(detail.Length>0,"TradeDetailsMissing");
        Check(detail.All(x=>(string?)x.Element("statCd")==partner),"TradePartnerMismatch");
        Check(detail.Select(x=>(string?)x.Element("year")).Distinct().Order().SequenceEqual(Enumerable.Range(1,12).Select(m=>$"2025.{m:00}")),"TradeTwelveMonthsRequired");
        Check(detail.Select(x=>$"{x.Element("year")?.Value}:{x.Element("hsCd")?.Value}").Distinct().Count()==detail.Length,"TradeDuplicateDetail");
        decimal Number(XElement item,string field){Check(decimal.TryParse(item.Element(field)?.Value,NumberStyles.Number,CultureInfo.InvariantCulture,out var n)&&n>=0,"TradeMetricMissingOrInvalid");return n;}
        foreach(var row in detail)foreach(var metric in Metrics)Number(row,metric);
        var total=totals[0];
        return new(partner,Number(total,"expWgt"),Number(total,"expDlr"),Number(total,"impWgt"),Number(total,"impDlr"),detail.Length,
            detail.Sum(x=>Number(x,"expWgt")+Number(x,"impWgt"))-Number(total,"expWgt")-Number(total,"impWgt"));
    }
    public static async Task RunAsync(string mode,string root,Dictionary<string,object?> result)
    {
        if(mode=="self-test")
        {
            XElement Fixture()
            {
                var items=new XElement("items",new XElement("item",new XElement("year","총계"),Metrics.Select(m=>new XElement(m,12))));
                foreach(var month in Enumerable.Range(1,12))items.Add(new XElement("item",new XElement("year",$"2025.{month:00}"),new XElement("hsCd","0101000000"),new XElement("statCd","CN"),Metrics.Select(k=>new XElement(k,1))));
                return new XElement("response",new XElement("header",new XElement("resultCode","00")),new XElement("body",items));
            }
            byte[] Bytes(XElement x)=>Encoding.UTF8.GetBytes(x.ToString());
            Check(Parse(Bytes(Fixture()),"CN").ExportKg==12,"TradeFixtureFailure");var count=1;
            void Reject(Action<XElement> change){var f=Fixture();change(f);try{Parse(Bytes(f),"CN");}catch(InvalidDataException){count++;return;}throw new InvalidDataException("TradeRejectionTestFailed");}
            Reject(f=>f.Descendants("item").Last().Remove());
            Reject(f=>f.Descendants("items").Single().Add(new XElement(f.Descendants("item").First())));
            Reject(f=>f.Descendants("statCd").First().Value="JP");
            Reject(f=>f.Descendants("expWgt").First().Value="-1");
            Reject(f=>f.Descendants("expDlr").First().Remove());
            Reject(f=>f.Descendants("resultCode").Single().Value="99");
            result["passed"]=count;return;
        }
        var folder=Path.Combine(root,Folder);Directory.CreateDirectory(folder);var receiptPath=Path.Combine(folder,"receipt.json");
        if(mode=="acquire")
        {
            Check(!File.Exists(receiptPath),"TradeReceiptAlreadyExists_UseVerify");
            using var http=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false}){Timeout=TimeSpan.FromSeconds(90),MaxResponseContentBufferSize=50*1024*1024};
            var metadata=await http.GetByteArrayAsync("https://www.data.go.kr/catalog/15100475/openapi.json");
            using(var meta=JsonDocument.Parse(metadata))Check(meta.RootElement.GetProperty("license").GetString()!.Contains("제한 없음"),"TradeLicenseReviewRequired");
            if(!File.Exists(Path.Combine(folder,"metadata.json")))await Write(Path.Combine(folder,"metadata.json"),metadata);
            using var secrets=JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"Microsoft/UserSecrets/47766019-f542-4cfc-9d4a-14b2fbfeac0e/secrets.json")));
            var key=Uri.UnescapeDataString(secrets.RootElement.GetProperty("PublicData:DataGoKrServiceKey").GetString()!);
            Check(!string.IsNullOrWhiteSpace(key),"TradeCredentialMissing");
            var entries=new List<Entry>();
            foreach(var country in Countries)
            {
                var raw=Path.Combine(folder,country+".xml");var entryFile=Path.Combine(folder,country+".receipt.json");
                if(File.Exists(entryFile)){var previous=JsonSerializer.Deserialize<Entry>(await File.ReadAllTextAsync(entryFile))!;Check(previous.Partner==country&&Hash(await File.ReadAllBytesAsync(raw))==previous.Hash,"TradeResumeHashMismatch");entries.Add(previous);continue;}
                Check(!File.Exists(raw),"TradeUnreceiptedRawRequiresReview");
                var url=$"https://apis.data.go.kr/1220000/nitemtrade/getNitemtradeList?strtYymm=202501&endYymm=202512&cntyCd={country}";
                byte[] bytes;try{bytes=await http.GetByteArrayAsync(url+"&serviceKey="+Uri.EscapeDataString(key));}catch{throw new InvalidDataException("TradeFetchFailed:"+country);}
                var body=Encoding.UTF8.GetString(bytes);Check(!body.Contains(key)&&!body.Contains(Uri.EscapeDataString(key)),"TradeCredentialEchoBlocked");
                var facts=Parse(bytes,country);var entry=new Entry(country,url,Hash(bytes),DateTimeOffset.UtcNow);
                await Write(raw,bytes);await Write(entryFile,JsonSerializer.SerializeToUtf8Bytes(entry,Pretty));entries.Add(entry);
            }
            await Write(receiptPath,JsonSerializer.SerializeToUtf8Bytes(entries,Pretty));result["acquiredCountries"]=entries.Count;return;
        }
        Check(mode is "apply" or "verify" or "preview","TradeModeInvalid");
        var receipts=JsonSerializer.Deserialize<Entry[]>(await File.ReadAllTextAsync(receiptPath))!;
        Check(receipts.Select(x=>x.Partner).Order().SequenceEqual(Countries.Order()),"TradeCountrySetInvalid");
        var records=new List<외부데이터정규화Record>();var allFacts=new List<Facts>();
        foreach(var e in receipts)
        {
            var bytes=await File.ReadAllBytesAsync(Path.Combine(folder,e.Partner+".xml"));Check(Hash(bytes)==e.Hash,"TradeRawHashMismatch");var facts=Parse(bytes,e.Partner);allFacts.Add(facts);
            var values=new[]{facts.ExportKg,facts.ExportUsd,facts.ImportKg,facts.ImportUsd};
            for(var i=0;i<4;i++)
            {
                var metric=Metrics[i];var dim=$"year=2025;partner={e.Partner};scope=TOTAL;mode=unknown;metric={metric};hash={e.Hash}";
                records.Add(new 외부데이터정규화Record{SourceId=Source,DatasetId=Dataset,StableId=$"kcs:trade:2025:{e.Partner}:{metric}",
                    RecordKey=외부데이터RecordKey.Create(Source,Dataset,"country:kr",metric,new DateTimeOffset(2025,1,1,0,0,0,TimeSpan.Zero),dim),
                    RegionStableId="country:kr",MetricCode=metric,NumericValue=values[i],UnitCode=metric.EndsWith("Wgt")?"kg":"USD",
                    TextValue=JsonSerializer.Serialize(new{partner=e.Partner,scope="TOTAL",mode="Unknown",year=2025,sourceUrl=e.Url,totalBasis="ProviderAnnualTotal",value=values[i]}),
                    EvidenceAsOfUtc=new DateTimeOffset(2025,1,1,0,0,0,TimeSpan.Zero),CollectedAtUtc=e.CollectedAt,FirstSeenAtUtc=e.CollectedAt,LastSeenAtUtc=e.CollectedAt,
                    SourceVersion="kcs-country-annual.r1",DataRevision=e.Hash,DimensionKey=dim,TemporalPrecisionCode="calendar-year",SpatialPrecisionCode="country-trade-not-route",
                    QualityCode="PendingHumanReview",LimitationCode="PrivateReviewOnly;TransportModeUnknown;NotActualVehicleCount"});
            }
        }
        var options=await 로컬공공자료Db.OptionsAsync(root);var inserted=0;
        if(mode=="apply")
        {
            result["databaseWriteAttempted"]=true;
            await using var db=new PublicDataIngestionDbContext(options);await using var tx=await db.Database.BeginTransactionAsync();
            foreach(var e in receipts)
            {
                var registration=await new 평창군공공공간원본등록Service(db).RegisterFileAsync(Path.Combine(folder,e.Partner+".xml"),
                    new 공공공간원본등록Request(Source,Dataset,"kcs-country-annual.r1",e.Hash,new DateTimeOffset(2025,1,1,0,0,0,TimeSpan.Zero),"application/xml",Folder+"/"+e.Partner+".xml"));
                foreach(var row in records.Where(x=>x.DataRevision==e.Hash))
                {
                    var prior=await db.NormalizedRecords.SingleOrDefaultAsync(x=>x.RecordKey==row.RecordKey);
                    if(prior!=null){Check(prior.NumericValue==row.NumericValue&&prior.TextValue==row.TextValue&&prior.UnitCode==row.UnitCode,"TradeImmutableConflict");continue;}
                    row.RawSnapshotId=registration.RawSnapshotId;db.NormalizedRecords.Add(row);inserted++;
                }
            }
            await db.SaveChangesAsync();await tx.CommitAsync();result["databaseCommitted"]=true;result["committed"]=true;
        }
        await using var readback=new PublicDataIngestionDbContext(options);var keys=records.Select(x=>x.RecordKey).ToList();
        var stored=await readback.NormalizedRecords.AsNoTracking().Include(x=>x.RawSnapshot).Where(x=>keys.Contains(x.RecordKey)).ToListAsync();
        Check(stored.Count==20&&stored.All(x=>records.Any(y=>x.RecordKey==y.RecordKey&&x.NumericValue==y.NumericValue&&x.TextValue==y.TextValue&&x.UnitCode==y.UnitCode)&&x.RawSnapshot?.SourceId==Source),"TradeReadbackMismatch");
        result["inserted"]=inserted;result["verified"]=stored.Count;result["target"]="hongdal-mysql-1 / hongdal_dev";result["facts"]=allFacts;
        if(mode=="preview")
        {
            var items=receipts.SelectMany(e=>new[]{"export","import"}.Select(direction=>{
                var prefix=direction=="export"?"exp":"imp";var subset=stored.Where(x=>x.DataRevision==e.Hash).ToArray();
                return new{partner=e.Partner,direction,kg=subset.Single(x=>x.MetricCode==prefix+"Wgt").NumericValue,usd=subset.Single(x=>x.MetricCode==prefix+"Dlr").NumericValue,revision=e.Hash};})).ToArray();
            var bytes=JsonSerializer.SerializeToUtf8Bytes(new{schemaVersion="country-trade-preview.v1",year=2025,localReviewOnly=true,distributionApproved=false,transportMode="Unknown",sourceUrl="https://www.data.go.kr/data/15100475/openapi.do",collectedAtUtc=receipts.Max(x=>x.CollectedAt),items},Pretty);
            var path=Path.Combine(folder,"globe-preview.json");if(File.Exists(path))Check(Hash(await File.ReadAllBytesAsync(path))==Hash(bytes),"TradePreviewConflict");else await Write(path,bytes);
            result["previewPath"]=path;result["previewHash"]=Hash(bytes);
            if(!File.Exists(path+".sha256"))await Write(path+".sha256",Encoding.UTF8.GetBytes(Hash(bytes)));
        }
    }
}
