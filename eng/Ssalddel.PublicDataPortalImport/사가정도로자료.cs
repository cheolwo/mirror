using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualBasic.FileIO;
using Ssalddel.Domain.PublicData;
using Ssalddel.Infrastructure.Persistence.PublicData;
using 살뜰.Services.External.PublicData.Korea;

// 두 공식 공급원의 네 도로명 표본만 검토용으로 축적한다. 도로 선분 결합/게임 폭/공개는 하지 않는다.
internal static class 사가정도로자료
{
    internal const string Relative = "artifacts/local/public-data/sagajeong-roads-20260912-r1";
    private static readonly string[] Names = ["사가정로", "면목로", "용마산로", "면목천로"];
    private const string CsvUrl = "https://www.data.go.kr/cmm/cmm/fileDownload.do?atchFileId=FILE_000000002816005&fileDetailSn=1&insertDataPrcus=N";
    private const string SeoulBase = "https://data.seoul.go.kr/dataList/dataView.do?onepagerow=100&srvType=S&infId=OA-22650&serviceKind=0&ssUserId=SAMPLE_VIEW&strWhere=&strOrderby=&filterCol=ROD_NAM&";
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    private sealed record FileReceipt(string File, string Url, string Sha256, DateTimeOffset CollectedAtUtc);
    private sealed record Receipt(FileReceipt[] Files);
    private sealed record Candidate(string File, 외부데이터정규화Record Record);
    private static void Require(bool ok, string code) { if (!ok) throw new InvalidDataException(code); }
    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
    private static Task Save(string path, object value) => File.WriteAllTextAsync(path, JsonSerializer.Serialize(value, Json), new UTF8Encoding(false));

    public static async Task RunAsync(string mode, string root, Dictionary<string,object?> result)
    {
        Require(mode is "acquire" or "acquire-resume" or "self-test" or "preview" or "apply" or "verify", "RoadModeInvalid");
        var folder = Path.Combine(root, Relative);
        if (mode is "acquire" or "acquire-resume") { await Acquire(folder,mode=="acquire-resume"); result["acquired"] = true; return; }
        var receipt = JsonSerializer.Deserialize<Receipt>(await File.ReadAllTextAsync(Path.Combine(folder,"acquisition.json")))!;
        foreach (var f in receipt.Files) Require(Hash(Path.Combine(folder, f.File)) == f.Sha256, "RoadInputHashChanged");
        var candidates = Parse(folder, receipt);
        Require(candidates.Count == 8 && candidates.Select(x=>x.Record.RecordKey).Distinct().Count()==8, "RoadSelectionCount");
        Require(candidates.All(x=>x.Record.TextValue.Length<=2000),"RoadRecordTooLong");
        result["selectedRows"] = candidates.Count;
        if (mode == "self-test")
        {
            Require(candidates.Select(x=>x.Record.TextValue).SequenceEqual(Parse(folder,receipt).Select(x=>x.Record.TextValue)), "RoadNondeterministic");
            Require(candidates.All(x=>x.Record.QualityCode=="PendingHumanReview" && x.Record.LimitationCode.Contains("NoRuntime")), "RoadReviewBoundary");
            Require(candidates.Count(x=>x.Record.NumericValue!=null)==4, "RoadWidthBandNotScalar");
            var test=Csv("연번,노선명,위치,폭(미터),연장(미터),총면적(제곱미터),불투수 포장면적(제곱미터)\n1,\"신내로17,19길\",미상,2,10,20,\n");
            Require(test.Single()[1]=="신내로17,19길" && test.Single()[6]=="", "RoadCsvQuoteOrBlank");
            foreach(var bad in new[]{"wrong\n1", "연번,노선명,위치,폭(미터),연장(미터),총면적(제곱미터),불투수 포장면적(제곱미터)\n1,a,b,2"})
            { bool rejected=false; try { Csv(bad); } catch(InvalidDataException) { rejected=true; } Require(rejected,"RoadInvalidCsvAccepted"); }
            Require(DecimalOrNull("")==null && DecimalOrNull("0")==0, "RoadMissingNotZero");
            result["selfTestsPassed"] = 8; await Save(Path.Combine(folder,"self-test.json"),result); return;
        }
        var options = await 로컬공공자료Db.OptionsAsync(root); // 현재 Compose·컨테이너·포트·DB 확인. 비밀값 출력 금지.
        var keys=candidates.Select(x=>x.Record.RecordKey).ToList();
        await using(var db=new PublicDataIngestionDbContext(options))
        {
            if(mode=="apply")
            {
                await db.Database.OpenConnectionAsync();
                await using var command=db.Database.GetDbConnection().CreateCommand();
                command.CommandText="SELECT GET_LOCK('mirror:public-data:sagajeong-roads-r1',0)";
                Require(Convert.ToInt32(await command.ExecuteScalarAsync())==1,"RoadImportBusy");
                await using var transaction=await db.Database.BeginTransactionAsync();
                var before=await db.NormalizedRecords.AsNoTracking().Where(x=>keys.Contains(x.RecordKey)).ToListAsync();
                Require(before.All(x=>candidates.Any(y=>Equivalent(x,y.Record))),"RoadExistingConflict");
                int inserted=0,existing=0;
                result["databaseWriteAttempted"]=true;
                foreach(var group in candidates.GroupBy(x=>x.File))
                {
                    var first=group.First().Record; var file=receipt.Files.Single(x=>x.File==group.Key);
                    var registered=await new 평창군공공공간원본등록Service(db).RegisterFileAsync(Path.Combine(folder,group.Key),
                        new(first.SourceId,first.DatasetId,first.SourceVersion,first.DataRevision,null,
                            group.Key.EndsWith(".csv")?"text/csv; charset=windows-949":"application/json",Relative+"/"+group.Key));
                    var rows=group.Select(x=>x.Record).ToList(); foreach(var row in rows) row.RawSnapshotId=registered.RawSnapshotId;
                    var saved=await new EfExternalDataIngestionStore(db).UpsertNormalizedAsync(rows);
                    Require(saved.UpdatedCount==0,"RoadUnexpectedUpdate"); inserted+=saved.InsertedCount;existing+=saved.ExistingCount;
                    if(registered.Inserted)
                    {
                        var raw=await db.RawSnapshots.SingleAsync(x=>x.Id==registered.RawSnapshotId); raw.CollectedAtUtc=file.CollectedAtUtc;
                        var run=await db.IngestionRuns.SingleAsync(x=>x.Id==raw.FirstCollectionRunId);
                        run.StatusCode=외부데이터수집StatusCodes.Partial;run.NormalizedCount=rows.Count;run.InsertedCount=saved.InsertedCount;
                        run.ErrorCode="PendingHumanReview";run.ErrorSummary="Named source observation only; no segment geometry; no public/runtime use. Scope/date/rights/conflicts in TextValue and acquisition.json.";
                        await db.SaveChangesAsync();
                    }
                }
                await transaction.CommitAsync();result["committed"]=true;result["inserted"]=inserted;result["existing"]=existing;
            }
        }
        await using var verify=new PublicDataIngestionDbContext(options);
        var stored=await verify.NormalizedRecords.AsNoTracking().Include(x=>x.RawSnapshot).Where(x=>keys.Contains(x.RecordKey)).ToListAsync();
        if(mode!="preview") Require(stored.Count==8,"RoadReadbackCount");
        Require(stored.All(x=>candidates.Any(y=>Equivalent(x,y.Record) && x.RawSnapshot!=null && x.RawSnapshot.ContentHashSha256==receipt.Files.Single(f=>f.File==y.File).Sha256)),"RoadReadbackMismatch");
        result["target"]="hongdal-mysql-1 / hongdal_dev";result["verifiedRows"]=stored.Count;
        result["rows"]=stored.Select(x=>new{x.Id,x.RawSnapshotId,x.StableId,x.NumericValue,x.QualityCode});
        await Save(Path.Combine(folder,mode+"-"+DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmssfff")+".json"),result);
    }

    private static async Task Acquire(string folder,bool resume)
    {
        var files=new List<FileReceipt>();
        if(resume)
        {
            Require(!File.Exists(Path.Combine(folder,"acquisition.json")),"RoadAcquisitionCompleted");
            var failed=File.Exists(Path.Combine(folder,"acquisition-resume-failed.json"))?"acquisition-resume-failed.json":"acquisition-failed.json";
            files.AddRange(JsonSerializer.Deserialize<Receipt>(await File.ReadAllTextAsync(Path.Combine(folder,failed)))!.Files);
            foreach(var f in files)Require(Hash(Path.Combine(folder,f.File))==f.Sha256,"RoadResumeHashMismatch");
        }
        else {Require(!Directory.Exists(folder),"RoadAcquisitionAlreadyExists");Directory.CreateDirectory(folder);}
        using var client=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false}) {Timeout=TimeSpan.FromSeconds(25)};
        client.DefaultRequestHeaders.UserAgent.ParseAdd("MirrorPublicDataResearch/1.0");
        client.DefaultRequestHeaders.Accept.ParseAdd("*/*");
        async Task Fetch(string name,string url)
        {
            if(files.Any(x=>x.File==name && x.Url==url))return;
            Require(!File.Exists(Path.Combine(folder,name)),"RoadUnreceiptedFileConflict");
            using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(25));
            using var response=await client.GetAsync(url,HttpCompletionOption.ResponseHeadersRead,deadline.Token);
            Require(response.IsSuccessStatusCode,"RoadHttpStatus:"+(int)response.StatusCode);
            await using var stream=await response.Content.ReadAsStreamAsync(deadline.Token); using var buffer=new MemoryStream();
            var bytes=new byte[8192];int n;
            while((n=await stream.ReadAsync(bytes,deadline.Token))>0){Require(buffer.Length+n<=2*1024*1024,"RoadResponseBudget");buffer.Write(bytes,0,n);}
            await File.WriteAllBytesAsync(Path.Combine(folder,name),buffer.ToArray());
            files.Add(new(name,url,Hash(Path.Combine(folder,name)),DateTimeOffset.UtcNow));
        }
        try
        {
            await Fetch("sidewalk-metadata.json","https://www.data.go.kr/catalog/15114354/fileData.json");
            await Fetch("route-metadata.json","https://www.data.go.kr/catalog/15047292/fileData.json");
            await Fetch("seoul-dataset.html","https://data.seoul.go.kr/dataList/OA-22650/S/1/datasetView.do");
            await Fetch("seoul-sheet.html","https://data.seoul.go.kr/dataList/sheetView.do?infId=OA-22650&srvType=S");
            using var metadata=JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(folder,"sidewalk-metadata.json")));
            Require(metadata.RootElement.GetProperty("license").GetString()=="이용허락범위 제한 없음","RoadSidewalkRightsChanged");
            Require((await File.ReadAllTextAsync(Path.Combine(folder,"seoul-dataset.html"))).Contains("공공누리 1유형"),"RoadSeoulRightsChanged");
            await Fetch("sidewalk.csv",CsvUrl);
            for(int i=0;i<Names.Length;i++)
            {
                for(int page=1;page<=2;page++)
                {
                    var file=$"route-{i+1}-{page}.json";
                    await Fetch(file,SeoulBase+"pageNo="+page+"&txtFilter="+Uri.EscapeDataString(Names[i]));
                    using var data=ReadSeoul(await File.ReadAllTextAsync(Path.Combine(folder,file)));
                    var count=data.RootElement.GetProperty("page").GetProperty("totalCount").GetInt32();
                    Require(data.RootElement.GetProperty("result").GetString()=="ok" && count<=200,"RoadQueryBudgetOrStatus");
                    if(count<=page*100)break;
                }
            }
            await Save(Path.Combine(folder,"acquisition.json"),new Receipt(files.ToArray()));
        }
        catch { await Save(Path.Combine(folder,resume?"acquisition-resume-failed.json":"acquisition-failed.json"),new Receipt(files.ToArray()));throw; }
    }

    private static List<Candidate> Parse(string folder,Receipt receipt)
    {
        var result=new List<Candidate>();Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var rows=Csv(Encoding.GetEncoding(949,new EncoderExceptionFallback(),new DecoderExceptionFallback()).GetString(File.ReadAllBytes(Path.Combine(folder,"sidewalk.csv"))));
        Require(rows.Count==19 && rows.Select(x=>x[0]).Distinct().Count()==19,"RoadCsvCount");
        foreach(var name in Names)
        {
            var row=rows.Single(x=>x[1]==name);var width=DecimalOrNull(row[3]);Require(width is >0 and <100,"RoadWidthInvalid");
            var length=DecimalOrNull(row[4]);var area=DecimalOrNull(row[5]);
            Add("sidewalk.csv","jungnang-sidewalk-pavement","data-go-kr-15114354",name,width,"m",
                new{roadName=name,sourceRow=row[0],location=row[2],widthMeters=width,lengthMeters=length,areaSquareMeters=area,
                    impermeableAreaSquareMeters=DecimalOrNull(row[6]),widthMeaning="SidewalkPavementOnly;NotCarriagewayOrWholeRoad",
                    qualityNote=width*length==area?"NoArithmeticMismatch":"WidthTimesLengthDiffersFromTotalArea;MeaningUnverified",
                    sourceDateLabel="20230608",dateMeaning="DatasetTitleDate;ObservationDateUnverified",sourceUrl="https://www.data.go.kr/data/15114354/fileData.do",
                    license="이용허락범위 제한 없음",segmentMatch="NotEstablished",currentness="Historical;NotVerifiedCurrent"});
        }
        for(int i=0;i<Names.Length;i++)
        {
            var pages=receipt.Files.Where(x=>x.File.StartsWith($"route-{i+1}-",StringComparison.Ordinal)).OrderBy(x=>x.File).ToArray();
            var all=new List<(string File,JsonElement Row)>();int? total=null;
            foreach(var file in pages)
            {
                using var data=ReadSeoul(File.ReadAllText(Path.Combine(folder,file.File)));
                int count=data.RootElement.GetProperty("page").GetProperty("totalCount").GetInt32();
                Require(total==null||total==count,"RoadPaginationDrift");total=count;
                all.AddRange(data.RootElement.GetProperty("list").EnumerateArray().Select(x=>(file.File,x.Clone())));
            }
            Require(all.Count==total && all.Select(x=>x.Row.GetProperty("RNUM").GetString()).Distinct().Count()==total,"RoadIncompleteOrDuplicatePage");
            var match=all.Single(x=>x.Row.GetProperty("ROD_NAM").GetString()==Names[i]);
            Add(match.File,"seoul-road-route","seoul-OA-22650",Names[i],null,"width-band-text",
                new{roadName=Names[i],sourceFields=match.Row,sourceUrl="https://data.seoul.go.kr/dataList/OA-22650/S/1/datasetView.do",
                    license="공공누리 1유형",widthMeaning="SourceClassificationBand;NotMeasuredWidth",segmentMatch="NotEstablished",
                    qualityNote="MajorRoadClassificationNeedsReview;NoGeometryOrLaneCount",dateMeaning="ObservationDateUnavailable;AcquisitionTimeOnly"});
        }
        return result;
        void Add(string file,string source,string dataset,string name,decimal? number,string unit,object observation)
        {
            var f=receipt.Files.Single(x=>x.File==file);var dimension="road-name="+name+";raw-sha256="+f.Sha256;
            var collected=DateTimeOffset.FromUnixTimeMilliseconds(f.CollectedAtUtc.ToUnixTimeMilliseconds());
            result.Add(new(file,new 외부데이터정규화Record {SourceId=source,DatasetId=dataset,StableId=source+":"+name,
                RegionStableId="region:kr:sig:11260",MetricCode="road-source-review",
                RecordKey=외부데이터RecordKey.Create(source,dataset,"region:kr:sig:11260","road-source-review",collected,dimension),
                NumericValue=number,TextValue=JsonSerializer.Serialize(observation),UnitCode=unit,DimensionKey=dimension,
                // 기존 정규화 모델은 날짜 필수. 수집 시각을 색인용으로만 사용하며 관측일로 승격하지 않는다.
                SourceVersion="download-sha256:"+f.Sha256,DataRevision="sagajeong-road-review.r1",EvidenceAsOfUtc=collected,
                CollectedAtUtc=collected,FirstSeenAtUtc=collected,LastSeenAtUtc=collected,
                SpatialPrecisionCode="road-name-only-unmatched",TemporalPrecisionCode="acquisition-only-not-observation",
                QualityCode="PendingHumanReview",LimitationCode="PrivateReviewOnly;NoPublication;NoRuntime;NoGeometryMatch;NoWidthInference"}));
        }
    }
    private static bool Equivalent(외부데이터정규화Record a,외부데이터정규화Record b)=>a.RecordKey==b.RecordKey && a.StableId==b.StableId && a.TextValue==b.TextValue && a.NumericValue==b.NumericValue && a.UnitCode==b.UnitCode && a.SourceVersion==b.SourceVersion && a.DataRevision==b.DataRevision && a.QualityCode==b.QualityCode && a.LimitationCode==b.LimitationCode && a.EvidenceAsOfUtc==b.EvidenceAsOfUtc;
    // 서울 미리보기 응답은 따옴표 없는 속성명을 쓴다. 코드 실행 없이 데이터 파서로만 읽고 원본 바이트는 보존한다.
    private static JsonDocument ReadSeoul(string text)=>JsonDocument.Parse(Newtonsoft.Json.Linq.JObject.Parse(text).ToString(Newtonsoft.Json.Formatting.None));
    private static decimal? DecimalOrNull(string value)=>string.IsNullOrWhiteSpace(value)?null:decimal.Parse(value,NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture);
    private static List<string[]> Csv(string text)
    {
        using var parser=new TextFieldParser(new StringReader(text)){TextFieldType=FieldType.Delimited,HasFieldsEnclosedInQuotes=true};parser.SetDelimiters(",");
        var expected=new[]{"연번","노선명","위치","폭(미터)","연장(미터)","총면적(제곱미터)","불투수 포장면적(제곱미터)"};
        Require(parser.ReadFields()?.SequenceEqual(expected)==true,"RoadCsvSchema");var rows=new List<string[]>();
        while(!parser.EndOfData){var row=parser.ReadFields()!;Require(row.Length==7,"RoadCsvColumnCount");rows.Add(row);}return rows;
    }
}
