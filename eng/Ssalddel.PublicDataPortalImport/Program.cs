using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Domain.PublicData;
using Ssalddel.Infrastructure.Persistence.PublicData;
using 살뜰.Services.External.PublicData.Korea;

// 기존 원본 등록 서비스/공공자료 저장소만 호출. HTTP host/게시/전체 migration은 실행하지 않는다.
var result = new Dictionary<string, object?> { ["databaseWriteAttempted"]=false, ["committed"]=false };
try
{
    const string jungnangMarketVisualPrefix = "jungnang-market-visual-";
    if (args.Length == 2 && args[0].StartsWith(jungnangMarketVisualPrefix, StringComparison.Ordinal))
    {
        await 중랑구전통시장시각자료.RunAsync(
            args[0][jungnangMarketVisualPrefix.Length..],
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(args[1])),
            result);
        Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented=true }));
        return 0;
    }
    const string sagajeongDataGoPhotoPrefix = "sagajeong-data-go-photo-";
    if (args.Length == 2 && args[0].StartsWith(sagajeongDataGoPhotoPrefix, StringComparison.Ordinal))
    {
        await 사가정공공데이터포털사진자료.RunAsync(
            args[0][sagajeongDataGoPhotoPrefix.Length..],
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(args[1])),
            result);
        Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented=true }));
        return 0;
    }
    const string sagajeongPhotoPrefix = "sagajeong-photo-";
    if (args.Length == 2 && args[0].StartsWith(sagajeongPhotoPrefix, StringComparison.Ordinal))
    {
        await 사가정공공사진자료.RunAsync(
            args[0][sagajeongPhotoPrefix.Length..],
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(args[1])),
            result);
        Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented=true }));
        return 0;
    }
    const string myeonmokStationSpatialPrefix = "myeonmok-station-spatial-";
    if (args.Length == 2 && args[0].StartsWith(myeonmokStationSpatialPrefix, StringComparison.Ordinal))
    {
        await 면목역공간자료.RunAsync(
            args[0][myeonmokStationSpatialPrefix.Length..],
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(args[1])),
            result);
        Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented=true }));
        return 0;
    }
    const string yongmasanStationSpatialPrefix = "yongmasan-station-spatial-";
    if (args.Length == 2 && args[0].StartsWith(yongmasanStationSpatialPrefix, StringComparison.Ordinal))
    {
        await 용마산역공간자료.RunAsync(
            args[0][yongmasanStationSpatialPrefix.Length..],
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(args[1])),
            result);
        Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented=true }));
        return 0;
    }
    if (args.Length == 2 && args[0].StartsWith("station-reference-", StringComparison.Ordinal))
    {
        await 철도역기준자료.RunAsync(args[0][18..], Path.GetFullPath(args[1]), result);
        Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented=true }));
        return 0;
    }
    const string sagajeongPresentationBuildingEvidencePrefix = "sagajeong-presentation-building-evidence-";
    if (args.Length == 2 && args[0].StartsWith(sagajeongPresentationBuildingEvidencePrefix, StringComparison.Ordinal))
    {
        await 사가정화면건물결속주소자료.RunAsync(
            args[0][sagajeongPresentationBuildingEvidencePrefix.Length..],
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(args[1])),
            result);
        Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented=true }));
        return 0;
    }
    const string sagajeongBuildingAddressPrefix = "sagajeong-building-address-";
    if (args.Length == 2 && args[0].StartsWith(sagajeongBuildingAddressPrefix, StringComparison.Ordinal))
    {
        await 사가정건물도로명주소자료.RunAsync(
            args[0][sagajeongBuildingAddressPrefix.Length..],
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(args[1])),
            result);
        Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented=true }));
        return 0;
    }
    if (args.Length == 2 && args[0].StartsWith("sagajeong-building-", StringComparison.Ordinal))
    {
        await 사가정건물자료대장.RunAsync(args[0][19..], Path.GetFullPath(args[1]), result);
        Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented=true }));
        return 0;
    }
    if (args.Length == 2 && args[0].StartsWith("sagajeong-link-", StringComparison.Ordinal))
    {
        await 사가정표준링크자료.RunAsync(args[0][15..], Path.GetFullPath(args[1]), result);
        Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented=true }));
        return 0;
    }
    if (args.Length == 2 && args[0].StartsWith("sagajeong-road-", StringComparison.Ordinal))
    {
        await 사가정도로자료.RunAsync(args[0][15..], Path.GetFullPath(args[1]), result);
        Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented=true }));
        return 0;
    }
    const string sagajeongRestaurantDirectoryPrefix = "sagajeong-restaurant-directory-";
    if (args.Length == 2 && args[0].StartsWith(sagajeongRestaurantDirectoryPrefix, StringComparison.Ordinal))
    {
        await 사가정음식점Directory자료.RunAsync(
            args[0][sagajeongRestaurantDirectoryPrefix.Length..],
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(args[1])),
            result);
        Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented=true }));
        return 0;
    }
    const string sagajeongTrafficPrefix = "sagajeong-traffic-";
    if (args.Length == 2 && args[0].StartsWith(sagajeongTrafficPrefix, StringComparison.Ordinal))
    {
        await 사가정차선신호자료.RunAsync(args[0][sagajeongTrafficPrefix.Length..], Path.GetFullPath(args[1]), result);
        Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented=true }));
        return 0;
    }
    const string sagajeongCrosswalkPrefix = "sagajeong-crosswalk-";
    if (args.Length == 2 && args[0].StartsWith(sagajeongCrosswalkPrefix, StringComparison.Ordinal))
    {
        await 사가정횡단보도자료.RunAsync(args[0][sagajeongCrosswalkPrefix.Length..], Path.GetFullPath(args[1]), result);
        Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented=true }));
        return 0;
    }
    const string sagajeongSpatialSupplementPrefix = "sagajeong-spatial-supplement-";
    if (args.Length == 2 && args[0].StartsWith(sagajeongSpatialSupplementPrefix, StringComparison.Ordinal))
    {
        await 사가정공간보충자료.RunAsync(
            args[0][sagajeongSpatialSupplementPrefix.Length..],
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(args[1])),
            result);
        Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented=true }));
        return 0;
    }
    if (args.Length == 2 && args[0].StartsWith("admin-dong-data-", StringComparison.Ordinal))
    {
        await 면목동행정동생활인구.RunAsync(args[0][16..], Path.TrimEndingDirectorySeparator(Path.GetFullPath(args[1])), result);
        Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented=true }));
        return 0;
    }
    const string administrativeDongCrosswalkCandidatePrefix = "admin-dong-crosswalk-candidate-";
    if (args.Length == 2 && args[0].StartsWith(administrativeDongCrosswalkCandidatePrefix, StringComparison.Ordinal))
    {
        await 행정동횡단보도후보.RunAsync(
            args[0][administrativeDongCrosswalkCandidatePrefix.Length..],
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(args[1])),
            result);
        Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented=true }));
        return 0;
    }
    const string administrativeDongIntersectionCandidatePrefix = "admin-dong-intersection-candidate-";
    if (args.Length == 2 && args[0].StartsWith(administrativeDongIntersectionCandidatePrefix, StringComparison.Ordinal))
    {
        await 행정동교차로점후보.RunAsync(
            args[0][administrativeDongIntersectionCandidatePrefix.Length..],
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(args[1])),
            result);
        Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented=true }));
        return 0;
    }
    const string administrativeDongWalkNetworkCandidatePrefix = "admin-dong-walk-network-candidate-";
    if (args.Length == 2 && args[0].StartsWith(administrativeDongWalkNetworkCandidatePrefix, StringComparison.Ordinal))
    {
        await 행정동보행망후보.RunAsync(
            args[0][administrativeDongWalkNetworkCandidatePrefix.Length..],
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(args[1])),
            result);
        Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented=true }));
        return 0;
    }
    const string administrativeDongBusinessCandidatePrefix = "admin-dong-business-candidate-";
    if (args.Length == 2 && args[0].StartsWith(administrativeDongBusinessCandidatePrefix, StringComparison.Ordinal))
    {
        await 행정동사업장후보.RunAsync(
            args[0][administrativeDongBusinessCandidatePrefix.Length..],
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(args[1])),
            result);
        Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented=true }));
        return 0;
    }
    const string administrativeDongBuildingAddressCandidatePrefix = "admin-dong-building-address-candidate-";
    if (args.Length == 2 && args[0].StartsWith(administrativeDongBuildingAddressCandidatePrefix, StringComparison.Ordinal))
    {
        await 행정동건물주소후보.RunAsync(
            args[0][administrativeDongBuildingAddressCandidatePrefix.Length..],
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(args[1])),
            result);
        Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented=true }));
        return 0;
    }
    const string administrativeDongDioramaBatchPrefix = "admin-dong-diorama-batch-";
    if (args.Length == 2 && args[0].StartsWith(administrativeDongDioramaBatchPrefix, StringComparison.Ordinal))
    {
        await 행정동디오라마Batch.RunAsync(
            args[0][administrativeDongDioramaBatchPrefix.Length..],
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(args[1])),
            result);
        Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented=true }));
        return 0;
    }
    if (args.Length == 2 && args[0].StartsWith("admin-dong-diorama-", StringComparison.Ordinal))
    {
        await 행정동실자료완결.RunAsync(args[0][19..], Path.TrimEndingDirectorySeparator(Path.GetFullPath(args[1])), result);
        Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented=true }));
        return 0;
    }
    if (args.Length >= 2 && args[0].StartsWith("local-life-", StringComparison.Ordinal))
    {
        await 로컬생활자료Import.RunAsync(args[0][11..], Path.GetFullPath(args[1]), args.Skip(2).ToArray(), result);
        Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions {WriteIndented=true}));
        return 0;
    }
    if (args.Length is 2 or 3 && args[0].StartsWith("catalog-", StringComparison.Ordinal))
    {
        var areaStableId=args.Length==3 && args[2].StartsWith("--area-stable-id=",StringComparison.Ordinal)
            ? args[2][17..] : args.Length==2 ? null : throw new InvalidDataException("SpatialAreaArgumentInvalid");
        await 공간자료CatalogImport.RunAsync(args[0][8..], Path.TrimEndingDirectorySeparator(Path.GetFullPath(args[1])), result,areaStableId);
        Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented=true }));
        return 0;
    }
    if (args.Length == 2 && args[0].StartsWith("diorama-", StringComparison.Ordinal))
    {
        await 면목동디오라마자료.RunAsync(args[0][8..], Path.TrimEndingDirectorySeparator(Path.GetFullPath(args[1])), result);
        Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented=true }));
        return 0;
    }
    if (args.Length == 2 && args[0].StartsWith("land-", StringComparison.Ordinal))
    {
        await 면목동공간결속.RunAsync(args[0][5..], Path.TrimEndingDirectorySeparator(Path.GetFullPath(args[1])), result);
        Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented=true }));
        return 0;
    }
    if (args.Length == 2 && args[0].StartsWith("business-", StringComparison.Ordinal))
    {
        await 면목동사업체수집.RunAsync(args[0][9..], Path.TrimEndingDirectorySeparator(Path.GetFullPath(args[1])), result);
        Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented=true }));
        return 0;
    }
    if (args.Length == 2 && args[0].StartsWith("address-link-", StringComparison.Ordinal))
    {
        await 면목동주소공간연결.RunAsync(args[0][13..], Path.TrimEndingDirectorySeparator(Path.GetFullPath(args[1])), result);
        Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented=true }));
        return 0;
    }
    if (args.Length == 2 && args[0].StartsWith("myeonmok-", StringComparison.Ordinal))
    {
        await 면목동주소자료.RunAsync(args[0][9..], Path.TrimEndingDirectorySeparator(Path.GetFullPath(args[1])), result);
        Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented=true }));
        return 0;
    }
    if (args.Length == 2 && args[0].StartsWith("spatial-", StringComparison.Ordinal))
    {
        await 중랑구공간자료.RunAsync(args[0][8..], Path.TrimEndingDirectorySeparator(Path.GetFullPath(args[1])), result);
        Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented=true }));
        return 0;
    }
    if (args.Length == 2 && args[0] == "food-unit-acquire")
    {
        await 먹거리가격표본Acquisition.RunAsync(Path.GetFullPath(args[1]),true);
        return 0;
    }
    if (args.Length == 2 && args[0].StartsWith("food-unit-",StringComparison.Ordinal))
    {
        await 먹거리가격단위Review.RunAsync(args[0][10..],Path.TrimEndingDirectorySeparator(Path.GetFullPath(args[1])),result);
        Console.WriteLine(JsonSerializer.Serialize(result,new JsonSerializerOptions { WriteIndented=true }));
        return 0;
    }
    if (args.Length == 2 && args[0] == "food-acquire")
    {
        await 먹거리가격표본Acquisition.RunAsync(Path.GetFullPath(args[1]));
        return 0;
    }
    if (args.Length == 2 && args[0].StartsWith("food-",StringComparison.Ordinal))
    {
        await 먹거리가격표본Import.RunAsync(args[0][5..],Path.TrimEndingDirectorySeparator(Path.GetFullPath(args[1])),result);
        Console.WriteLine(JsonSerializer.Serialize(result,new JsonSerializerOptions { WriteIndented=true }));
        return 0;
    }
    if (args.Length != 2 || args[0] is not ("self-test" or "preview" or "apply" or "verify"))
        throw new InvalidDataException("UsageModeRepositoryRootRequired");
    var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(args[1]));
    var relative = "artifacts/local/public-data/eight-life-domains/market-20260908-r1";
    var folder = Path.Combine(root, relative);
    var expected = new Dictionary<string,string>
    {
        ["markets.json"]="13FFD04A946EC7EEC28222C8C3762F2E77AAB919CD250888CDAAF7F51CCE303B",
        ["metadata.json"]="43922DC39FA0730D7DBBC39C6CB5511954339FF57BDB59114B7CB4DE57E4DAE8",
        ["metadata.rdf"]="D0927198A012DA06BBDED3432321A312A82F7C1112E069E88B670084D527C7AB",
    };
    foreach (var file in expected)
        Require(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(folder,file.Key)))) == file.Value,"InputHashChanged");
    var json = File.ReadAllText(Path.Combine(folder,"markets.json"));
    using var receipt = JsonDocument.Parse(File.ReadAllText(Path.Combine(folder,"acquisition.json")));
    var collected = receipt.RootElement.GetProperty("files")[0].GetProperty("collectedAtUtc").GetDateTimeOffset();
    using var raw = JsonDocument.Parse(json);
    Require(raw.RootElement.GetArrayLength()==1393,"InputCountChanged");
    var rows = 전통시장표본Parser.Parse(json,collected);
    Require(rows.Count==12,"SelectionCountChanged");
    if (args[0]=="self-test")
    {
        var first = raw.RootElement.EnumerateArray().First(x=>x.GetProperty("RDNMADR").GetString()!.StartsWith("서울특별시 중랑구 ",StringComparison.Ordinal)).GetRawText();
        var fixture = "["+first+"]";
        var one = 전통시장표본Parser.Parse(fixture,collected).Single();
        Require(one.QualityCode=="PendingHumanReview" && one.NumericValue==null && one.TextValue.Contains("장미제일시장"),"ProjectionFailed");
        Require(전통시장표본Parser.Parse(json,collected).Select(x=>x.TextValue).SequenceEqual(rows.Select(x=>x.TextValue)),"Nondeterministic");
        Require(전통시장표본Parser.Parse("[]",collected).Count==0,"EmptySelectionFailed");
        Require(전통시장표본Parser.Parse(fixture.Replace("서울특별시 중랑구 ","서울특별시 다른구 "),collected).Count==0,"ScopeLeak");
        var invalids = new[] { "{}", "["+first+","+first+"]", fixture.Replace("2025-11-10","2026-01-01"), fixture.Replace("B553077","OTHER"), fixture.Replace("37.60518362","NaN"), fixture.Replace("\"STOR_NUMBER\":\"57\"","\"STOR_NUMBER\":\"-1\""), fixture.Replace("\"MRKT_NM\":\"장미제일시장\"","\"MRKT_NM\":null") };
        foreach (var invalid in invalids)
        {
            var rejected=false;
            try { _=전통시장표본Parser.Parse(invalid,collected); } catch (InvalidDataException) { rejected=true; }
            Require(rejected,"NegativeFixtureNotRejected");
        }
        result["selfTestsPassed"]=11; result["selectedRows"]=rows.Count;
    }
    else
    {
        var options=await 로컬공공자료Db.OptionsAsync(root);
        var keys=rows.Select(x=>x.RecordKey).ToList();
        await using(var db=new PublicDataIngestionDbContext(options))
        {
            var before=await db.NormalizedRecords.AsNoTracking().Where(x=>keys.Contains(x.RecordKey)).ToListAsync();
            Require(before.All(x=>rows.Any(y=>y.RecordKey==x.RecordKey && y.TextValue==x.TextValue && x.QualityCode=="PendingHumanReview")),"ExistingRecordConflict");
            result["beforeCount"]=before.Count;
            if(args[0]=="apply")
            {
                await db.Database.OpenConnectionAsync();
                await using var command=db.Database.GetDbConnection().CreateCommand();
                command.CommandText="SELECT GET_LOCK('mirror:public-data:market-15012894',0)";
                Require(Convert.ToInt32(await command.ExecuteScalarAsync())==1,"ImportBusy");
                await using var transaction=await db.Database.BeginTransactionAsync();
                result["databaseWriteAttempted"]=true;
                var service=new 평창군공공공간원본등록Service(db);
                var source=await service.RegisterFileAsync(Path.Combine(folder,"markets.json"),new 공공공간원본등록Request(전통시장표본Parser.SourceId,전통시장표본Parser.DatasetId,전통시장표본Parser.Version,전통시장표본Parser.Revision,null,"application/json",relative+"/markets.json"));
                foreach(var row in rows) row.RawSnapshotId=source.RawSnapshotId;
                var saved=await new EfExternalDataIngestionStore(db).UpsertNormalizedAsync(rows);
                Require(saved.UpdatedCount==0,"UnexpectedUpdate");
                if(source.Inserted)
                {
                    var snapshot=await db.RawSnapshots.SingleAsync(x=>x.Id==source.RawSnapshotId);
                    snapshot.CollectedAtUtc=collected;
                    var run=await db.IngestionRuns.SingleAsync(x=>x.Id==snapshot.FirstCollectionRunId);
                    run.StatusCode=외부데이터수집StatusCodes.Partial; run.FetchedCount=1393; run.NormalizedCount=12;
                    run.InsertedCount=saved.InsertedCount; run.ErrorCode="PendingHumanReview";
                    run.ErrorSummary="Private review only; selected Jungnang 12/1393. No publication/runtime. Source and license: artifacts/local/public-data/eight-life-domains/market-20260908-r1/acquisition.json";
                    await db.SaveChangesAsync();
                }
                await transaction.CommitAsync();
                result["committed"]=true; result["inserted"]=saved.InsertedCount; result["existing"]=saved.ExistingCount;
            }
        }
        // 앞 저장 연결을 닫은 뒤 별도 문맥으로 재조회. preview/verify는 읽기 전용이다.
        await using var verify=new PublicDataIngestionDbContext(options);
        var stored=await verify.NormalizedRecords.AsNoTracking().Where(x=>keys.Contains(x.RecordKey)).Include(x=>x.RawSnapshot).ToListAsync();
        if(args[0]!="preview") Require(stored.Count==12,"ReadbackCountMismatch");
        Require(stored.All(x=>rows.Any(y=>y.RecordKey==x.RecordKey && y.TextValue==x.TextValue && x.SourceVersion==y.SourceVersion && x.QualityCode==y.QualityCode && x.LimitationCode==y.LimitationCode) && x.RawSnapshot!.ContentHashSha256==expected["markets.json"].ToLowerInvariant()),"ReadbackMismatch");
        result["verifiedRows"]=stored.Count; result["target"]="hongdal-mysql-1 / hongdal_dev";
        result["rows"]=stored.OrderBy(x=>x.Id).Select(x=>new { x.Id,x.RawSnapshotId,x.StableId,x.QualityCode,x.EvidenceAsOfUtc });
    }
    result["mode"]=args[0]; Console.WriteLine(JsonSerializer.Serialize(result,new JsonSerializerOptions { WriteIndented=true })); return 0;
}
catch(Exception ex)
{
    result["errorCode"]=ex is InvalidDataException ? ex.Message : ex.GetType().Name;
    var databaseError = ex.InnerException;
    while (databaseError?.InnerException is not null) databaseError = databaseError.InnerException;
    if (databaseError is MySqlConnector.MySqlException mysqlException)
    {
        result["databaseErrorNumber"] = mysqlException.Number;
        result["databaseSqlState"] = mysqlException.SqlState;
    }
    Console.WriteLine(JsonSerializer.Serialize(result)); return 1; // 연결문자열/원예외/비밀값을 출력하지 않는다.
}
static void Require(bool ok,string code) { if(!ok) throw new InvalidDataException(code); }
