using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Domain.PublicData;
using Ssalddel.Infrastructure.Persistence.PublicData;

internal static class Hs무역조회Pipeline
{
    record Evidence(string dataset,string revision,string recordKey,string rawHash);
    record Measure(bool available,decimal value,string unit,Evidence[] sources);
    record Partner(int m49,string name,Measure valueUsd,Measure netWeightKg);
    record Product(string hs6,string name,string status,Partner[] partners,Evidence[] classificationSources);
    static void Check(bool ok,string code){if(!ok)throw new InvalidDataException(code);}
    static string Hash(byte[] b)=>Convert.ToHexString(SHA256.HashData(b)).ToLowerInvariant();
    static Dictionary<string,string> Dimensions(string input)=>input.Split(';',StringSplitOptions.RemoveEmptyEntries).Select(x=>x.Split('=',2)).ToDictionary(x=>x[0],x=>x[1],StringComparer.Ordinal);
    static Evidence Source(외부데이터정규화Record r)
    {
        Check(r.RawSnapshot!=null&&!string.IsNullOrWhiteSpace(r.RawSnapshot.ContentHashSha256),"HsSourceMissing");
        return new(r.DatasetId,r.DataRevision,r.RecordKey,r.RawSnapshot!.ContentHashSha256);
    }
    static Measure Metric(IEnumerable<외부데이터정규화Record> source,string unit)
    {
        var rows=source.OrderBy(x=>x.RecordKey,StringComparer.Ordinal).ToArray();
        Check(rows.Length>0&&rows.All(x=>x.UnitCode==unit&&(x.NumericValue==null||x.NumericValue>=0)),"HsUnitOrValueInvalid");
        Check(rows.Select(x=>x.NumericValue).Distinct().Count()==1,"HsConflictingObservation");
        return new(rows[0].NumericValue.HasValue,rows[0].NumericValue??0,unit,rows.Select(Source).ToArray());
    }
    public static async Task RunAsync(string mode,string root,Dictionary<string,object?> result)
    {
        Check(mode is "preview" or "self-test","HsModeInvalid");
        if(mode=="self-test")
        {
            Check(Dimensions("hs6=030214;classification=H6")["hs6"]=="030214","HsLeadingZeroLost");var passed=1;
            void Reject(Action action){try{action();}catch(InvalidDataException){passed++;return;}throw new InvalidDataException("HsRejectionFailed");}
            Reject(()=>Metric([new(){UnitCode="ton",NumericValue=1}],"kg"));
            Reject(()=>Metric([new(){UnitCode="kg",NumericValue=-1}],"kg"));
            Reject(()=>Metric([new(){UnitCode="kg",NumericValue=1},new(){UnitCode="kg",NumericValue=2}],"kg"));
            Reject(()=>Metric([new(){UnitCode="kg",NumericValue=0},new(){UnitCode="kg",NumericValue=null}],"kg"));
            result["passed"]=passed;return;
        }
        var options=await 로컬공공자료Db.OptionsAsync(root);await using var db=new PublicDataIngestionDbContext(options);
        var categories=await db.NormalizedRecords.AsNoTracking().Include(x=>x.RawSnapshot).Where(x=>x.SourceId=="un-comtrade-reference"&&x.MetricCode=="trade.hs6.category"&&x.SourceVersion=="H6 (HS 2022)").ToListAsync();
        var observations=await db.NormalizedRecords.AsNoTracking().Include(x=>x.RawSnapshot).Where(x=>x.SourceId=="un-comtrade-public"&&x.SourceVersion=="UN Comtrade H6 annual 2025"&&(x.MetricCode=="trade.import.value.usd"||x.MetricCode=="trade.import.net-weight.kg"||x.MetricCode=="trade.import.observation-status")).ToListAsync();
        Check(categories.Count>0&&observations.Count>0,"HsNoStoredData");
        var parsed=observations.Select(r=>new{row=r,d=Dimensions(r.DimensionKey)}).ToArray();
        foreach(var p in parsed)Check(p.d["classification"]=="H6"&&p.d["period"]=="2025"&&p.d["flow"]=="import"&&p.row.RegionStableId=="country:kr","HsScopeMismatch");
        var categoryGroups=categories.GroupBy(r=>Dimensions(r.DimensionKey)["hs6"]).OrderBy(g=>g.Key,StringComparer.Ordinal).ToArray();
        Check(parsed.All(x=>categoryGroups.Any(g=>g.Key==x.d["hs6"])),"HsUnlinkedTrade");
        var products=new List<Product>();
        foreach(var group in categoryGroups)
        {
            Check(group.Key.Length==6&&group.Key.All(char.IsAsciiDigit)&&group.Select(r=>r.TextValue).Distinct().Count()==1,"HsCategoryConflict");
            var records=parsed.Where(x=>x.d["hs6"]==group.Key).ToArray();
            var numeric=records.Where(x=>x.row.MetricCode!="trade.import.observation-status").ToArray();
            var partners=numeric.GroupBy(x=>int.Parse(x.d["partnerM49"],CultureInfo.InvariantCulture)).OrderBy(g=>g.Key).Select(g=>
            {
                Check(g.Select(x=>x.row.TextValue).Distinct().Count()==1,"HsPartnerNameConflict");
                return new Partner(g.Key,g.First().row.TextValue!,Metric(g.Where(x=>x.row.MetricCode=="trade.import.value.usd").Select(x=>x.row),"USD"),Metric(g.Where(x=>x.row.MetricCode=="trade.import.net-weight.kg").Select(x=>x.row),"kg"));
            }).ToArray();
            var states=records.Where(x=>x.row.MetricCode=="trade.import.observation-status").Select(x=>x.row.TextValue).Distinct().ToArray();
            Check(states.Length<=1&&!(partners.Length>0&&states.Contains("NoReportedRows")),"HsAvailabilityConflict");
            products.Add(new(group.Key,group.First().TextValue!,partners.Length>0?"Observed":states.SingleOrDefault()??"NotCollected",partners,group.OrderBy(r=>r.RecordKey,StringComparer.Ordinal).Select(Source).ToArray()));
        }
        var bytes=JsonSerializer.SerializeToUtf8Bytes(new{schemaVersion="hs-trade-index.v1",classification="H6",hsEdition="2022",reporterM49=410,year=2025,flow="import",exportStatus="NotCollected",partnerCoverage="WorldAndStoredTopPartners_NotAllCountries",transportLink="ContextOnly_NoHsModeBinding",sourceUrl="https://comtradeplus.un.org/",localReviewOnly=true,distributionApproved=false,products});
        var folder=Path.Combine(root,"artifacts/local/public-data/hs-trade-index-r1");Directory.CreateDirectory(folder);var path=Path.Combine(folder,"hs-index-"+Hash(bytes)+".json");
        if(File.Exists(path))Check(Hash(await File.ReadAllBytesAsync(path))==Hash(bytes),"HsIndexHashConflict");else{await using var f=new FileStream(path,FileMode.CreateNew);await f.WriteAsync(bytes);}
        if(!File.Exists(path+".sha256")){await using var f=new FileStream(path+".sha256",FileMode.CreateNew);await f.WriteAsync(Encoding.UTF8.GetBytes(Hash(bytes)));}
        result["products"]=products.Count;result["observed"]=products.Count(x=>x.status=="Observed");result["withoutObservations"]=products.Count(x=>x.status!="Observed");result["sourceRows"]=observations.Count;result["categoryRows"]=categories.Count;result["partnerGroups"]=products.Sum(x=>x.partners.Length);result["hash"]=Hash(bytes);result["path"]=path;
    }
}
