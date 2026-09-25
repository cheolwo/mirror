using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Infrastructure.Persistence.PublicData;
using 살뜰.Services.External.PublicData.Korea;

internal static class 이천지형원본
{
    const string Relative="artifacts/local/public-data/icheon-terrain-20260923-r1";
    const string Tile="Copernicus_DSM_COG_10_N37_00_E127_00_DEM";
    const string Source="copernicus-glo30-public-icheon-review";
    const string Url="https://copernicus-dem-30m.s3.amazonaws.com/"+Tile+"/"+Tile+".tif";
    sealed record Receipt(string Url,string Hash,long Bytes,DateTimeOffset CollectedAtUtc,string License,string Review);
    public static async Task RunAsync(string mode,string root,Dictionary<string,object?> result)
    {
        if(mode is not ("acquire" or "apply" or "verify"))throw new InvalidDataException("ModeInvalid");
        var folder=Path.Combine(root,Relative);var path=Path.Combine(folder,Tile+".tif");var receiptPath=Path.Combine(folder,"receipt.json");
        if(mode=="acquire")
        {
            if(File.Exists(path)||File.Exists(receiptPath))throw new InvalidDataException("FrozenInputAlreadyExists");
            using var http=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false}){Timeout=TimeSpan.FromSeconds(120)};
            using var response=await http.GetAsync(Url,HttpCompletionOption.ResponseHeadersRead);response.EnsureSuccessStatusCode();
            const int limit=100*1024*1024;
            if(response.Content.Headers.ContentLength>limit)throw new InvalidDataException("TooLarge");
            using var memory=new MemoryStream();await using var input=await response.Content.ReadAsStreamAsync();
            var buffer=new byte[65536];int n;
            while((n=await input.ReadAsync(buffer))>0){if(memory.Length+n>limit)throw new InvalidDataException("TooLarge");await memory.WriteAsync(buffer.AsMemory(0,n));}
            var data=memory.ToArray();
            if(data.Length<8 || data[0]!=73 || data[1]!=73 || data[2]!=42)throw new InvalidDataException("TiffHeaderInvalid");
            Directory.CreateDirectory(folder);await File.WriteAllBytesAsync(path,data);
            var receipt=new Receipt(Url,Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant(),data.LongLength,DateTimeOffset.UtcNow,
                "https://documentation.dataspace.copernicus.eu/APIs/SentinelHub/Data/DEM/resources/license/License-COPDEM-30.pdf",
                "PrivateReviewOnly;PendingHumanReview;DSMNotBareEarth;2021Release;NoUnityDistribution");
            await File.WriteAllTextAsync(receiptPath,JsonSerializer.Serialize(receipt,new JsonSerializerOptions{WriteIndented=true}));
        }
        var r=JsonSerializer.Deserialize<Receipt>(await File.ReadAllTextAsync(receiptPath))!;
        using(var f=File.OpenRead(path))if(r.Url!=Url||new FileInfo(path).Length!=r.Bytes||Convert.ToHexString(await SHA256.HashDataAsync(f)).ToLowerInvariant()!=r.Hash)throw new InvalidDataException("FrozenHashMismatch");
        result["receipt"]=r;if(mode=="acquire")return;
        var options=await 로컬공공자료Db.OptionsAsync(root);
        if(mode=="apply")
        {
            await using var db=new PublicDataIngestionDbContext(options);await using var tx=await db.Database.BeginTransactionAsync();
            result["databaseWriteAttempted"]=true;
            var saved=await new 평창군공공공간원본등록Service(db).RegisterFileAsync(path,new(Source,Tile,"sha256:"+r.Hash,"icheon-terrain.r1",null,"image/tiff",Relative+"/"+Tile+".tif"));
            if(saved.Inserted){var raw=await db.RawSnapshots.SingleAsync(x=>x.Id==saved.RawSnapshotId);raw.CollectedAtUtc=r.CollectedAtUtc;var run=await db.IngestionRuns.SingleAsync(x=>x.Id==raw.FirstCollectionRunId);run.ErrorCode="PendingHumanReview";run.ErrorSummary=r.Review;await db.SaveChangesAsync();}
            await tx.CommitAsync();result["inserted"]=saved.Inserted?1:0;result["committed"]=true;
        }
        await using var verify=new PublicDataIngestionDbContext(options);
        var rows=await verify.RawSnapshots.AsNoTracking().Where(x=>x.SourceId==Source&&x.DatasetId==Tile).ToListAsync();
        if(rows.Count(x=>x.ContentHashSha256==r.Hash&&x.ContentLength==r.Bytes&&x.StorageLocation=="private-file://"+Relative+"/"+Tile+".tif")!=1)throw new InvalidDataException("ReadbackMismatch");
        result["verifiedRawSnapshots"]=1;result["normalizedTerrainRows"]=0;
    }
}
