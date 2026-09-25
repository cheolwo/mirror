using System.Security.Cryptography;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Domain.PublicData;
using Ssalddel.Infrastructure.Persistence.PublicData;

// 비공개 조사 자료의 조회 전용 첫 층. 공급망/매장 성분/운영 상태를 확정하지 않는다.
internal static class 메뉴재료기반Query
{
    internal const string Dataset = "menu-ingredient-prices-20260924-r1";
    internal const string ScopePath = "eng/public-data/menu-ingredient-prices/scope.r1.json";
    internal const string SourceFolder = "artifacts/local/public-data/menu-ingredient-prices/20260924-r1";
    internal const string OutputFolder = "artifacts/local/public-data/menu-ingredient-foundation";
    internal static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNameCaseInsensitive = true
    };

    public static async Task RunAsync(string 모드, string 루트, Dictionary<string, object?> 결과)
    {
        Require(모드 is "query" or "self-test", "IngredientFoundationModeInvalid");
        if (모드 == "self-test")
        {
            결과["selfTestsPassed"] = 메뉴재료기반QueryTests.Run();
            return;
        }

        var 범위원문 = await File.ReadAllBytesAsync(Path.Combine(루트, ScopePath));
        var 원본폴더 = Path.Combine(루트, SourceFolder);
        var 수집대장원문 = await File.ReadAllBytesAsync(Path.Combine(원본폴더, "receipt.json"));
        var 수집대장 = JsonSerializer.Deserialize<메뉴재료가격조사.FileReceipt[]>(수집대장원문, Json)!;
        대장검증(수집대장);
        foreach (var 원본 in 수집대장)
        {
            var 원문 = await File.ReadAllBytesAsync(Path.Combine(원본폴더, 원본.Name));
            Require(Hash(원문) == 원본.Sha256, "IngredientFoundationSourceHashMismatch");
        }
        Require(Hash(범위원문) == 수집대장.Single(x => x.Kind == "scope").Sha256,
            "IngredientFoundationScopeHashMismatch");

        // 기존 도구의 로컬 소유/DB 검증을 재사용. 외부 공급자나 운영 HTTP는 호출하지 않는다.
        var 옵션 = await 로컬공공자료Db.OptionsAsync(루트);
        async Task<JsonElement> 독립조회()
        {
            await using var 저장소 = new PublicDataIngestionDbContext(옵션);
            await 저장소.Database.OpenConnectionAsync();
            await 저장소.Database.ExecuteSqlRawAsync("SET TRANSACTION READ ONLY");
            await using var 읽기 = await 저장소.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead);
            var 기록 = await 저장소.NormalizedRecords.AsNoTracking().Include(x => x.RawSnapshot)
                .Where(x => x.DatasetId == Dataset).OrderBy(x => x.RecordKey).Take(3001).ToListAsync();
            Require(기록.Count <= 3000, "IngredientFoundationRowLimitExceeded");
            var 사본 = Build(범위원문, 수집대장, 기록);
            Require(!저장소.ChangeTracker.HasChanges(), "IngredientFoundationUnexpectedTrackedWrite");
            // 읽기 트랜잭션 dispose: SaveChanges/원장 변경 없음.
            return 사본;
        }

        var 첫조회 = await 독립조회();
        var 재조회 = await 독립조회();
        var 출력 = JsonSerializer.SerializeToUtf8Bytes(첫조회, Json);
        Require(출력.SequenceEqual(JsonSerializer.SerializeToUtf8Bytes(재조회, Json)),
            "IngredientFoundationReadbackChanged");
        var 판본 = Hash(출력);
        var 출력폴더 = Path.Combine(루트, OutputFolder);
        Directory.CreateDirectory(출력폴더);
        var 출력경로 = Path.Combine(출력폴더, 판본 + ".json");
        if (File.Exists(출력경로))
            Require((await File.ReadAllBytesAsync(출력경로)).SequenceEqual(출력), "IngredientFoundationOutputConflict");
        else
        {
            await using var 파일 = new FileStream(출력경로, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            await 파일.WriteAsync(출력);
        }

        결과["mode"] = 모드;
        결과["summary"] = 첫조회.GetProperty("summary").Clone();
        결과["rows"] = 첫조회.GetProperty("ingredients").EnumerateArray().Select(x => new
        {
            id = x.GetProperty("ingredientId").GetString(),
            name = x.GetProperty("name").GetString(),
            storedCandidate = x.GetProperty("identity").GetProperty("storedCandidateStatus").GetString(),
            hsCandidates = x.GetProperty("classifications").GetProperty("hs").GetArrayLength(),
            kamisCandidates = x.GetProperty("classifications").GetProperty("kamis").GetArrayLength(),
            evidenceReferences = x.GetProperty("evidenceReferences").GetArrayLength(),
            origin = x.GetProperty("origin").GetProperty("status").GetString()
        }).ToArray();
        결과설정(결과, 판본, 출력경로, 수집대장원문);
    }

    static void 결과설정(Dictionary<string, object?> 결과, string 판본, string 출력경로, byte[] 수집대장원문)
    {
        결과["projectionSha256"] = 판본;
        결과["receiptSha256"] = Hash(수집대장원문);
        결과["output"] = 출력경로;
        결과["independentReadbacks"] = 2;
        결과["databaseWriteAttempted"] = false;
        결과["committed"] = false;
        결과["providerHttpCalls"] = 0;
        결과["stage"] = "completed";
    }

    internal static JsonElement Build(byte[] 범위원문, 메뉴재료가격조사.FileReceipt[] 수집대장,
        IReadOnlyList<외부데이터정규화Record> 기록)
    {
        대장검증(수집대장);
        Require(기록.Count <= 3000, "IngredientFoundationRowLimitExceeded");
        using var 문서 = JsonDocument.Parse(범위원문);
        var 범위 = 문서.RootElement;
        Require(범위.GetProperty("schemaVersion").GetString() == "menu-ingredient-price-research.v1"
            && 범위.GetProperty("period").GetString() == "2025"
            && 범위.GetProperty("mappingState").GetString() == "CandidateOnly", "IngredientFoundationScopeInvalid");
        var 연구판본 = 범위.GetProperty("recipeResearchRevision").GetString()!;
        var 레시피판본 = 범위.GetProperty("recipeSnapshotSha256").GetString()!;
        Require(IsHash(연구판본) && IsHash(레시피판본), "IngredientFoundationRecipeRevisionInvalid");
        var 범위Hash = Hash(범위원문);
        Require(수집대장.Single(x => x.Kind == "scope").Sha256 == 범위Hash, "IngredientFoundationScopeHashMismatch");
        var 재료들 = 범위.GetProperty("ingredients").Deserialize<메뉴재료가격조사.Ingredient[]>(Json)!;
        Require(재료들.Length is > 0 and <= 128 && 재료들.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count() == 재료들.Length,
            "IngredientFoundationDuplicateIngredient");
        foreach (var 재료 in 재료들)
        {
            Require(Regex.IsMatch(재료.Id, "^[a-z0-9-]{1,80}$") && !string.IsNullOrWhiteSpace(재료.Name)
                && !string.IsNullOrWhiteSpace(재료.Constraint) && !string.IsNullOrWhiteSpace(재료.Basis), "IngredientFoundationIdentityInvalid");
            Require(재료.Hs6 != null && 재료.KamisCodes != null, "IngredientFoundationClassificationMissing");
            Require(재료.Hs6.All(x => Regex.IsMatch(x, "^[0-9]{6}$")) && 재료.KamisCodes.All(x => Regex.IsMatch(x, "^[0-9]{3,4}$")),
                "IngredientFoundationClassificationInvalid");
            Require(재료.Hs6.Distinct().Count() == 재료.Hs6.Length && 재료.KamisCodes.Distinct().Count() == 재료.KamisCodes.Length,
                "IngredientFoundationDuplicateClassification");
        }
        Require(기록.Select(x => x.RecordKey).Distinct(StringComparer.Ordinal).Count() == 기록.Count
            && 기록.Select(x => x.StableId).Distinct(StringComparer.Ordinal).Count() == 기록.Count, "IngredientFoundationDuplicateRecord");
        var 순서기록 = 기록.OrderBy(x => x.RecordKey, StringComparer.Ordinal).ToArray();
        var 대장색인 = 수집대장.ToDictionary(x => x.Name, StringComparer.Ordinal);
        var 차원색인 = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        foreach (var 행 in 순서기록)
        {
            var 원본이름 = 행.DimensionKey.Split(';')[0];
            Require(대장색인.ContainsKey(원본이름), "IngredientFoundationReceiptMissing");
            var 원본 = 대장색인[원본이름];
            Require(행.DatasetId == Dataset && 행.SourceId == 원본.Source && 행.DataRevision == 원본.Sha256
                && 행.RawSnapshot != null && 행.RawSnapshot.ContentHashSha256 == 원본.Sha256
                && 행.RawSnapshot.SourceId == 원본.Source && 행.RawSnapshot.DatasetId == Dataset,
                "IngredientFoundationStoredProvenanceMismatch");
            Require(!string.IsNullOrWhiteSpace(행.StableId) && !string.IsNullOrWhiteSpace(행.RecordKey), "IngredientFoundationRecordIdentityMissing");
            Require(행.RecordKey == 외부데이터RecordKey.Create(행.SourceId, 행.DatasetId, 행.RegionStableId,
                행.MetricCode, 행.EvidenceAsOfUtc, 행.DimensionKey), "IngredientFoundationRecordKeyMismatch");
            Require(행.StableId == "menu-price:" + 행.RecordKey, "IngredientFoundationStableIdMismatch");
            Require(행.SourceVersion == 원본.Kind + ":" + 원본.Context, "IngredientFoundationSourceVersionMismatch");
            var 차원 = 차원해석(행.DimensionKey);
            차원색인.Add(행.RecordKey, 차원);
            if (행.MetricCode == "ingredient.hs.candidate")
            {
                var 저장재료 = JsonSerializer.Deserialize<메뉴재료가격조사.Ingredient>(행.TextValue, Json)!;
                var 재료 = 재료들.SingleOrDefault(x => x.Id == 저장재료.Id);
                Require(재료 != null && 행.DimensionKey == 원본.Name + ";" + 재료.Id
                    && JsonSerializer.Serialize(저장재료, Json) == JsonSerializer.Serialize(재료, Json), "IngredientFoundationStoredCandidateConflict");
            }
            else if (행.MetricCode.StartsWith("trade.", StringComparison.Ordinal))
            {
                Require(차원.TryGetValue("hs6", out var 코드) && 재료들.Any(x => x.Hs6.Contains(코드))
                    && 차원.GetValueOrDefault("period") == "2025" && 차원.GetValueOrDefault("classification") == "H6"
                    && 차원.GetValueOrDefault("reporter") == "410" && 차원.GetValueOrDefault("partner") == "0",
                    "IngredientFoundationTradeScopeMismatch");
                var 방향 = 차원.GetValueOrDefault("flow");
                Require((방향 == "M" && 행.MetricCode.StartsWith("trade.import.", StringComparison.Ordinal))
                    || (방향 == "X" && 행.MetricCode.StartsWith("trade.export.", StringComparison.Ordinal)), "IngredientFoundationTradeDirectionMismatch");
            }
            else if (행.MetricCode == "domestic.kamis.price.krw")
            {
                var 재료 = 재료들.SingleOrDefault(x => x.Id == 차원.GetValueOrDefault("ingredient"));
                Require(재료 != null && 재료.KamisCodes.Contains(차원.GetValueOrDefault("item")), "IngredientFoundationKamisMappingConflict");
            }
            else Require(행.MetricCode is "research.source.status" or "research.source.caveat", "IngredientFoundationUnknownMetric");
        }

        object 근거참조(외부데이터정규화Record 행)
        {
            var 원본 = 대장색인[행.DimensionKey.Split(';')[0]];
            var 차원 = 차원색인[행.RecordKey];
            return new
            {
                recordKey = 행.RecordKey, stableId = 행.StableId, sourceId = 행.SourceId, datasetId = 행.DatasetId,
                metricCode = 행.MetricCode, dimensionKey = 행.DimensionKey, dataRevision = 행.DataRevision,
                rawSha256 = 행.RawSnapshot!.ContentHashSha256, sourceVersion = 행.SourceVersion,
                sourceUrl = 원본.Url, sourceStatus = 원본.Status,
                evidenceAsOfUtc = 행.EvidenceAsOfUtc, collectedAtUtc = 행.CollectedAtUtc,
                unitCode = 행.UnitCode, regionStableId = 행.RegionStableId,
                temporalPrecisionCode = 행.TemporalPrecisionCode, spatialPrecisionCode = 행.SpatialPrecisionCode,
                qualityCode = 행.QualityCode, limitationCode = 행.LimitationCode,
                interpretationCode = 행.MetricCode == "domestic.kamis.price.krw"
                    ? (차원.GetValueOrDefault("category") == "500" ? "LivestockNotWholesale_SeoulUnverified_01And02NotIndependent" : "DomesticMarketQuote_NotPurchasePriceOrOrigin")
                    : (행.MetricCode.StartsWith("trade.", StringComparison.Ordinal) ? "TradeStatistic_NotPurchasePriceOrSupplyRoute" : "ResearchReferenceOnly")
            };
        }

        var 조회항목 = new List<object>();
        var 연결기록 = new HashSet<string>(StringComparer.Ordinal);
        var 저장후보수 = 0; var 국내연결재료수 = 0;
        foreach (var 재료 in 재료들.OrderBy(x => x.Id, StringComparer.Ordinal))
        {
            var 후보기록 = 순서기록.Where(x => x.MetricCode == "ingredient.hs.candidate" && x.DimensionKey == "scope.json;" + 재료.Id).ToArray();
            Require(후보기록.Length <= 1, "IngredientFoundationAmbiguousCandidate");
            var 관련기록 = 순서기록.Where(x => 후보기록.Contains(x)
                || (x.MetricCode.StartsWith("trade.", StringComparison.Ordinal) && 재료.Hs6.Contains(차원색인[x.RecordKey].GetValueOrDefault("hs6")))
                || (x.MetricCode == "domestic.kamis.price.krw" && 차원색인[x.RecordKey].GetValueOrDefault("ingredient") == 재료.Id)).ToArray();
            foreach (var 행 in 관련기록) 연결기록.Add(행.RecordKey);
            if (후보기록.Length == 1) 저장후보수++;
            var 국내기록 = 관련기록.Where(x => x.MetricCode == "domestic.kamis.price.krw").ToArray();
            if (국내기록.Length > 0) 국내연결재료수++;
            var 결손 = new List<string> { "RestaurantOriginNotVerified", "GlobalProductIdentityNotLinked", "IngredientToMenuRecipeEdgesNotResolved" };
            if (후보기록.Length == 0) 결손.Add("StoredIngredientCandidateMissing");
            if (재료.KamisCodes.Length == 0) 결손.Add("NoKamisMappingCandidate");
            else if (국내기록.Length == 0) 결손.Add("NoStoredKamisEvidence");
            조회항목.Add(new
            {
                ingredientId = 재료.Id, name = 재료.Name,
                identity = new
                {
                    namespaceCode = "menu-ingredient-price-research.v1", storedCandidateStatus = 후보기록.Length == 1 ? "Linked" : "Missing",
                    storedCandidateStableId = 후보기록.SingleOrDefault()?.StableId,
                    canonicalProductStableId = (string?)null, officialIngredientId = (long?)null,
                    note = "ResearchScopedIdentity_NotOperationalProductIdentity"
                },
                specification = new { status = "NeedsReview", constraint = 재료.Constraint, basis = 재료.Basis },
                origin = new { status = "Unknown", countryCodes = Array.Empty<string>(), reasonCode = "RestaurantOriginNotVerified" },
                recipeResearch = new { researchRevision = 연구판본, snapshotSha256 = 레시피판본, status = "BatchReferenceOnly",
                    reasonCode = "IngredientToMenuRecipeEdgesNotResolved", menuIds = Array.Empty<string>(), recipeIds = Array.Empty<string>() },
                classifications = new
                {
                    hs = 재료.Hs6.Order(StringComparer.Ordinal).Select(코드 => new
                    {
                        code = 코드, scheme = "HS", revision = "HS2022", level = 6, mappingStatus = "Candidate",
                        evidenceStatus = 관련기록.Any(x => 차원색인[x.RecordKey].GetValueOrDefault("hs6") == 코드) ? "StoredReferences" : "NoStoredReferences",
                        evidenceRecordKeys = 관련기록.Where(x => 차원색인[x.RecordKey].GetValueOrDefault("hs6") == 코드).Select(x => x.RecordKey).ToArray(),
                        mappingBasisRecordKey = 후보기록.SingleOrDefault()?.RecordKey,
                        interpretation = "ClassificationCandidate_NotProofOfImport"
                    }).ToArray(),
                    kamis = 재료.KamisCodes.Order(StringComparer.Ordinal).Select(코드 => new
                    {
                        code = 코드, scheme = "KAMIS", mappingStatus = "Candidate",
                        evidenceStatus = 국내기록.Any(x => 차원색인[x.RecordKey].GetValueOrDefault("item") == 코드) ? "StoredReferences" : "NoStoredReferences",
                        evidenceRecordKeys = 국내기록.Where(x => 차원색인[x.RecordKey].GetValueOrDefault("item") == 코드).Select(x => x.RecordKey).ToArray(),
                        mappingBasisRecordKey = 후보기록.SingleOrDefault()?.RecordKey,
                        interpretation = "MarketItemCandidate_NotProofOfDomesticOrigin"
                    }).ToArray()
                },
                evidenceReferences = 관련기록.Select(근거참조).ToArray(), gapCodes = 결손.ToArray()
            });
        }
        // 순서·DB surrogate ID와 무관. 수치/원문을 반환하지 않더라도 입력 변화는 판본에 반영한다.
        var 입력판본 = Hash(JsonSerializer.SerializeToUtf8Bytes(순서기록.Select(x => new
        {
            x.RecordKey, x.StableId, x.SourceId, x.DatasetId, x.RegionStableId, x.MetricCode,
            x.NumericValue, x.TextValue, x.UnitCode, x.EvidenceAsOfUtc, x.CollectedAtUtc,
            x.DimensionKey, x.SourceVersion, x.DataRevision, x.QualityCode, x.LimitationCode,
            x.TemporalPrecisionCode, x.SpatialPrecisionCode, rawSha256 = x.RawSnapshot!.ContentHashSha256
        }), Json));
        return JsonSerializer.SerializeToElement(new
        {
            schemaVersion = "menu-ingredient-foundation.v1", sourceDatasetId = Dataset, scopeSha256 = 범위Hash,
            inputRecordsSha256 = 입력판본, readOnly = true, use = "PrivateReviewOnly;NotRestaurantComposition;NotPurchaseCost;NoOperationalEffect",
            summary = new { ingredientCount = 재료들.Length, storedCandidateCount = 저장후보수,
                hsCandidateCount = 재료들.Sum(x => x.Hs6.Length), ingredientsWithKamisEvidence = 국내연결재료수,
                unknownOriginCount = 재료들.Length, unresolvedMenuRecipeCount = 재료들.Length,
                sourceRecordCount = 기록.Count, linkedRecordCount = 연결기록.Count,
                unassignedRecordCount = 기록.Count - 연결기록.Count },
            ingredients = 조회항목.ToArray(),
            unassignedEvidenceReferences = 순서기록.Where(x => !연결기록.Contains(x.RecordKey)).Select(근거참조).ToArray()
        }, Json);
    }

    internal static void 대장검증(메뉴재료가격조사.FileReceipt[] 수집대장)
    {
        Require(수집대장 is { Length: > 0 and <= 100 } && 수집대장.Select(x => x.Name).Distinct(StringComparer.Ordinal).Count() == 수집대장.Length
            && 수집대장.Count(x => x.Kind == "scope" && x.Name == "scope.json") == 1 && 수집대장.Count(x => x.Kind == "scope") == 1,
            "IngredientFoundationReceiptInvalid");
        foreach (var 원본 in 수집대장)
        {
            Require(Regex.IsMatch(원본.Name, "^[a-zA-Z0-9-]+[.]json$") && IsHash(원본.Sha256), "IngredientFoundationReceiptPathInvalid");
            Require(!Regex.IsMatch(원본.Url, "(?i)(p_cert|servicekey|api[_-]?key|token|password|secret)"), "IngredientFoundationUnsafeSourceUrl");
        }
    }

    static Dictionary<string, string> 차원해석(string 차원)
    {
        var 결과 = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var 부분 in 차원.Split(';').Skip(1))
        {
            var 쌍 = 부분.Split('=', 2);
            if (쌍.Length == 2) Require(결과.TryAdd(쌍[0], 쌍[1]), "IngredientFoundationDuplicateDimension");
        }
        return 결과;
    }
    internal static string Hash(byte[] 원문) => Convert.ToHexString(SHA256.HashData(원문)).ToLowerInvariant();
    static bool IsHash(string? 값) => 값 != null && Regex.IsMatch(값, "^[0-9a-f]{64}$");
    internal static void Require([DoesNotReturnIf(false)] bool 조건, string 코드) { if (!조건) throw new InvalidDataException(코드); }
}
