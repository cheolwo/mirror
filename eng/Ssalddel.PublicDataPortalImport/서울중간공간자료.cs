using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Infrastructure.Persistence.PublicData;
using 살뜰.Services.External.PublicData.Korea;

// 역사적 참고 경계 원본만 반입한다. 최신 경계·Unity 배치·통행 권위가 아니다.
internal static class 서울중간공간자료
{
    const string Relative = "artifacts/local/public-data/seoul-intermediate-boundaries-20260923-r1";
    const string Source = "seoul-open-data-intermediate-boundaries";
    static readonly string[] Datasets = ["OA-22160", "OA-22161"];
    sealed record Receipt(string Dataset, string PageUrl, string File, string Hash, long Bytes,
        string License, string SourceDate, string Crs, int Records, DateTimeOffset CollectedAtUtc);
    static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    static void Require(bool ok, string code) { if (!ok) throw new InvalidDataException(code); }
    static string Match(string input, string pattern)
    {
        var match = Regex.Match(input, pattern);
        Require(match.Success, "SeoulSourceMarkupChanged"); return match.Groups[1].Value;
    }
    static async Task<byte[]> ReadBounded(HttpResponseMessage response)
    {
        Require(response.IsSuccessStatusCode, "SeoulDownloadFailed:"+(int)response.StatusCode+":"+response.RequestMessage?.RequestUri?.Host);
        await using var input = await response.Content.ReadAsStreamAsync();
        using var output = new MemoryStream(); var buffer = new byte[65536];
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        int count;
        while ((count = await input.ReadAsync(buffer, timeout.Token)) > 0)
        {
            Require(output.Length + count <= 8 * 1024 * 1024, "SeoulSizeBudgetExceeded");
            output.Write(buffer, 0, count);
        }
        return output.ToArray();
    }
    static int Inspect(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        using var zip = new ZipArchive(stream);
        Require(zip.Entries.Count <= 20 && zip.Entries.Sum(x => x.Length) < 32 * 1024 * 1024, "SeoulArchiveBudgetExceeded");
        Require(zip.Entries.Any(x => x.Name.EndsWith(".shp", StringComparison.OrdinalIgnoreCase)) &&
            zip.Entries.Any(x => x.Name.EndsWith(".prj", StringComparison.OrdinalIgnoreCase)), "SeoulShapeMissing");
        var dbf = zip.Entries.Single(x => x.Name.EndsWith(".dbf", StringComparison.OrdinalIgnoreCase));
        using var input = dbf.Open(); var header = new byte[8]; input.ReadExactly(header);
        return System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(4));
    }
    public static async Task RunAsync(string mode, string root, Dictionary<string, object?> result)
    {
        Require(mode is "acquire" or "apply" or "verify", "SeoulModeInvalid");
        var folder = Path.Combine(root, Relative); var receiptPath = Path.Combine(folder, "receipt.json");
        if (mode == "acquire")
        {
            Require(!Directory.Exists(folder) || !Directory.EnumerateFileSystemEntries(folder).Any(), "SeoulAcquisitionAlreadyExists");
            Directory.CreateDirectory(folder);
            using var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(45) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("SsalddelPublicDataResearch/1.0");
            var receipts = new List<Receipt>();
            foreach (var dataset in Datasets)
            {
                var url = $"https://data.seoul.go.kr/dataList/{dataset}/S/1/datasetView.do";
                using var pageResponse = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                var pageBytes = await ReadBounded(pageResponse); var page = System.Text.Encoding.UTF8.GetString(pageBytes);
                Require(page.Contains("공공누리 1유형") && page.Contains("2023.10.31.") && page.Contains("5181"), "SeoulLicenseOrVintageChanged");
                var form = Match(page, "<form\\s+name=\"frmFile\"([\\s\\S]*?)</form>");
                var sequence = Match(form, "name=\"infSeq\"\\s+value=\"([0-9]+)\"");
                var fileSequence = Match(page, "onclick=\"javascript:downloadFile\\('([0-9]+)'\\);\"");
                using var request = new HttpRequestMessage(HttpMethod.Post, "https://datafile.seoul.go.kr/bigfile/iot/inf/nio_download.do?&useCache=false")
                { Content = new FormUrlEncodedContent(new Dictionary<string,string> { ["infId"]=dataset,["infSeq"]=sequence,["seq"]=fileSequence,["seqNo"]=fileSequence }) };
                using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                var bytes = await ReadBounded(response); var records = Inspect(bytes);
                Require(records == (dataset == "OA-22160" ? 425 : 25), "SeoulUnexpectedRecordCount");
                await File.WriteAllBytesAsync(Path.Combine(folder, dataset + ".html"), pageBytes);
                await File.WriteAllBytesAsync(Path.Combine(folder, dataset + ".zip"), bytes);
                receipts.Add(new(dataset,url,dataset+".zip",Hash(bytes),bytes.Length,"KOGL-Type1","2023-10-31","EPSG:5181",records,DateTimeOffset.UtcNow));
            }
            await File.WriteAllTextAsync(receiptPath,JsonSerializer.Serialize(receipts,new JsonSerializerOptions { WriteIndented=true }));
        }
        var evidence = JsonSerializer.Deserialize<List<Receipt>>(await File.ReadAllTextAsync(receiptPath))!;
        Require(evidence.Count == 2 && evidence.Select(x=>x.Dataset).Order().SequenceEqual(Datasets.Order()), "SeoulReceiptScopeChanged");
        foreach(var item in evidence)
        {
            Require(item.File == item.Dataset+".zip" && item.License=="KOGL-Type1" && item.SourceDate=="2023-10-31" && item.Crs=="EPSG:5181", "SeoulReceiptInvalid");
            var bytes=await File.ReadAllBytesAsync(Path.Combine(folder,item.File));
            Require(bytes.Length==item.Bytes && Hash(bytes)==item.Hash && Inspect(bytes)==item.Records,"SeoulSourceHashChanged");
        }
        result["sources"]=evidence; result["geometryValidated"]=false; result["distributionApproved"]=false;
        if(mode=="acquire") return;
        var options=await 로컬공공자료Db.OptionsAsync(root);
        if(mode=="apply")
        {
            await using var db=new PublicDataIngestionDbContext(options);
            await using var transaction=await db.Database.BeginTransactionAsync();
            result["databaseWriteAttempted"]=true; var inserted=0;
            foreach(var item in evidence)
            {
                var saved=await new 평창군공공공간원본등록Service(db).RegisterFileAsync(Path.Combine(folder,item.File),
                    new(Source,item.Dataset,"sha256:"+item.Hash,"seoul-intermediate-boundaries.r1",new DateTimeOffset(2023,10,31,0,0,0,TimeSpan.Zero),"application/zip",Relative+"/"+item.File));
                if(saved.Inserted)
                {
                    inserted++;
                    var raw=await db.RawSnapshots.SingleAsync(x=>x.Id==saved.RawSnapshotId);
                    raw.CollectedAtUtc=item.CollectedAtUtc;
                    var run=await db.IngestionRuns.SingleAsync(x=>x.Id==raw.FirstCollectionRunId);
                    run.ErrorCode="PendingHumanReview";
                    run.ErrorSummary="HistoricalBoundary20231031;EPSG5181;KOGLType1;PrivateReviewOnly;NoUnity;GeometryNotValidated";
                    await db.SaveChangesAsync();
                }
            }
            await transaction.CommitAsync(); result["committed"]=true; result["inserted"]=inserted;
        }
        await using var verify=new PublicDataIngestionDbContext(options);
        var rows=await verify.RawSnapshots.AsNoTracking().Where(x=>x.SourceId==Source).ToListAsync();
        Require(evidence.All(e=>rows.Count(x=>x.DatasetId==e.Dataset && x.ContentHashSha256==e.Hash && x.ContentLength==e.Bytes && x.StorageLocation=="private-file://"+Relative+"/"+e.File)==1),"SeoulReadbackMismatch");
        result["verifiedRawSnapshots"]=2; result["normalizedGeometryRecords"]=0;
    }
}
