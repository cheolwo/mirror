using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Infrastructure.Persistence.PublicData;
using 살뜰.Services.External.PublicData.Korea;

internal static class 광역경계참고자료
{
    const string Relative="artifacts/local/public-data/korea-admin1-context-r1";
    const string Source="natural-earth-admin1-context";
    const string Url="https://naciscdn.org/naturalearth/10m/cultural/ne_10m_admin_1_states_provinces.zip";
    sealed record Receipt(string Url,string Hash,long Bytes,DateTimeOffset CollectedAtUtc,string License);
    public static async Task RunAsync(string mode,string root,Dictionary<string,object?> result)
    {
        if(mode is not ("acquire" or "apply" or "verify"))throw new InvalidDataException("ModeInvalid");
        var folder=Path.Combine(root,Relative);var path=Path.Combine(folder,"admin1.zip");var receiptPath=Path.Combine(folder,"receipt.json");
        if(mode=="acquire")
        {
            if(File.Exists(path)||File.Exists(receiptPath))throw new InvalidDataException("FrozenInputAlreadyExists");
            using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(90));
            using var http=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false});
            using var response=await http.GetAsync(Url,HttpCompletionOption.ResponseHeadersRead,timeout.Token);response.EnsureSuccessStatusCode();
            using var memory=new MemoryStream();await using var input=await response.Content.ReadAsStreamAsync(timeout.Token);
            var buffer=new byte[65536];int n;
            while((n=await input.ReadAsync(buffer,timeout.Token))>0){if(memory.Length+n>24*1024*1024)throw new InvalidDataException("TooLarge");memory.Write(buffer,0,n);}
            var bytes=memory.ToArray();if(bytes.Length<4||bytes[0]!=80||bytes[1]!=75)throw new InvalidDataException("ZipExpected");
            Directory.CreateDirectory(folder);await File.WriteAllBytesAsync(path,bytes);
            await File.WriteAllTextAsync(receiptPath,JsonSerializer.Serialize(new Receipt(Url,Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),bytes.Length,DateTimeOffset.UtcNow,"https://www.naturalearthdata.com/about/terms-of-use/"),new JsonSerializerOptions{WriteIndented=true}));
        }
        var r=JsonSerializer.Deserialize<Receipt>(await File.ReadAllTextAsync(receiptPath))!;
        using(var file=File.OpenRead(path))if(r.Url!=Url||new FileInfo(path).Length!=r.Bytes||Convert.ToHexString(await SHA256.HashDataAsync(file)).ToLowerInvariant()!=r.Hash)throw new InvalidDataException("FrozenHashMismatch");
        result["receipt"]=r;if(mode=="acquire")return;
        var options=await 로컬공공자료Db.OptionsAsync(root);
        if(mode=="apply")
        {
            await using var db=new PublicDataIngestionDbContext(options);await using var tx=await db.Database.BeginTransactionAsync();result["databaseWriteAttempted"]=true;
            var saved=await new 평창군공공공간원본등록Service(db).RegisterFileAsync(path,new(Source,"admin1","sha256:"+r.Hash,"admin1-context.r1",null,"application/zip",Relative+"/admin1.zip"));
            if(saved.Inserted){var raw=await db.RawSnapshots.SingleAsync(x=>x.Id==saved.RawSnapshotId);raw.CollectedAtUtc=r.CollectedAtUtc;var run=await db.IngestionRuns.SingleAsync(x=>x.Id==raw.FirstCollectionRunId);run.ErrorCode="PendingHumanReview";run.ErrorSummary="PrivateReviewOnly;NaturalEarthPublicDomain;Scale1To10Million;NotCurrentOfficialAdministrativeBoundary";await db.SaveChangesAsync();}
            await tx.CommitAsync();result["inserted"]=saved.Inserted?1:0;result["committed"]=true;
        }
        await using var verify=new PublicDataIngestionDbContext(options);
        var rows=await verify.RawSnapshots.AsNoTracking().Where(x=>x.SourceId==Source&&x.DatasetId=="admin1").ToListAsync();
        if(rows.Count(x=>x.ContentHashSha256==r.Hash&&x.ContentLength==r.Bytes&&x.StorageLocation=="private-file://"+Relative+"/admin1.zip")!=1)throw new InvalidDataException("ReadbackMismatch");
        result["verifiedRawSnapshots"]=1;
    }
}
