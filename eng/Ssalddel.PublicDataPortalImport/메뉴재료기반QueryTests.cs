using System.Text;
using System.Text.Json;
using Ssalddel.Domain.PublicData;

// 기존 CLI 자체 시험 관례를 따른다. 합성 입력만 사용하며 DB/네트워크/파일 쓰기 없음.
internal static class 메뉴재료기반QueryTests
{
    sealed record 시험입력(byte[] 범위, 메뉴재료가격조사.FileReceipt[] 대장, List<외부데이터정규화Record> 기록);

    public static int Run()
    {
        var 통과수 = 0;
        void 확인(bool 조건, string 이름)
        {
            메뉴재료기반Query.Require(조건, "IngredientFoundationTest:" + 이름);
            통과수++;
        }
        void 거절(Action<시험입력> 변형, string 예상코드)
        {
            var 입력 = Fixture();
            변형(입력);
            try { Build(입력); }
            catch (InvalidDataException 예외)
            {
                확인(예외.Message == 예상코드, 예상코드);
                return;
            }
            throw new InvalidDataException("IngredientFoundationTest:ExpectedRejection:" + 예상코드);
        }

        var 기준 = Fixture();
        var 결과 = Build(기준);
        var 항목 = 결과.GetProperty("ingredients").EnumerateArray().ToArray();
        JsonElement 재료(string 코드) => 항목.Single(x => x.GetProperty("ingredientId").GetString() == 코드);
        확인(항목.Length == 3 && 결과.GetProperty("summary").GetProperty("storedCandidateCount").GetInt32() == 3, "BatchCoverage");
        확인(항목.Select(x => x.GetProperty("ingredientId").GetString()).SequenceEqual(new[] { "coffee", "milk", "rice" }), "StableOrder");
        확인(항목.All(x => x.GetProperty("origin").GetProperty("status").GetString() == "Unknown"
            && x.GetProperty("origin").GetProperty("countryCodes").GetArrayLength() == 0), "OriginNeverInferred");
        확인(항목.All(x => x.GetProperty("identity").GetProperty("canonicalProductStableId").ValueKind == JsonValueKind.Null), "NoGlobalIdentityFabrication");
        확인(항목.All(x => x.GetProperty("recipeResearch").GetProperty("menuIds").GetArrayLength() == 0
            && x.GetProperty("recipeResearch").GetProperty("status").GetString() == "BatchReferenceOnly"), "NoIngredientRecipeEdgeFabrication");
        확인(재료("coffee").GetProperty("classifications").GetProperty("hs").GetArrayLength() == 2, "CoffeeVariantsKeptSeparate");
        확인(재료("rice").GetProperty("classifications").GetProperty("hs")[0].GetProperty("mappingStatus").GetString() == "Candidate", "CandidateNotPromoted");
        확인(재료("coffee").GetProperty("gapCodes").EnumerateArray().Any(x => x.GetString() == "NoKamisMappingCandidate"), "UnlinkedKamisReason");
        확인(재료("milk").GetProperty("evidenceReferences").EnumerateArray().Any(x => x.GetProperty("unitCode").GetString() == "KRW/1L"), "LitresNotConverted");
        확인(재료("rice").GetProperty("evidenceReferences").EnumerateArray().Any(x => x.GetProperty("unitCode").GetString() == "KRW/20kg"), "OriginalPackageUnit");
        확인(재료("milk").GetProperty("evidenceReferences").EnumerateArray().Any(x =>
            x.GetProperty("interpretationCode").GetString() == "LivestockNotWholesale_SeoulUnverified_01And02NotIndependent"), "LivestockCaveat");
        확인(결과.GetProperty("unassignedEvidenceReferences").GetArrayLength() == 1, "CaveatNotLost");
        확인(결과.GetProperty("readOnly").GetBoolean() && !결과.ToString().Contains("SecretUnprojectedText", StringComparison.Ordinal), "RawTextNotProjected");
        확인(항목.SelectMany(x => x.GetProperty("evidenceReferences").EnumerateArray()).All(x =>
            x.TryGetProperty("rawSha256", out _) && x.TryGetProperty("collectedAtUtc", out _) && x.TryGetProperty("qualityCode", out _)), "ProvenancePreserved");
        기준.기록.Reverse();
        Array.Reverse(기준.대장);
        확인(결과.GetRawText() == Build(기준).GetRawText(), "OrderIndependentDeterminism");
        foreach (var 행 in 기준.기록) { 행.Id += 999; 행.RawSnapshotId += 999; }
        확인(결과.GetRawText() == Build(기준).GetRawText(), "SurrogateIdsIgnored");
        기준.기록.Single(x => x.MetricCode == "domestic.kamis.price.krw" && x.UnitCode == "KRW/1L").NumericValue = 3000;
        확인(결과.GetProperty("inputRecordsSha256").GetString() != Build(기준).GetProperty("inputRecordsSha256").GetString(), "InputValueChangeVersioned");

        var 누락 = Fixture();
        누락.기록.RemoveAll(x => x.DimensionKey == "scope.json;rice");
        var 누락결과 = Build(누락);
        확인(누락결과.GetProperty("ingredients").GetArrayLength() == 3
            && 누락결과.GetProperty("summary").GetProperty("storedCandidateCount").GetInt32() == 2, "OneMissingDoesNotHideOthers");
        확인(누락결과.GetProperty("ingredients").EnumerateArray().Single(x => x.GetProperty("ingredientId").GetString() == "rice")
            .GetProperty("gapCodes").EnumerateArray().Any(x => x.GetString() == "StoredIngredientCandidateMissing"), "MissingCandidateReason");
        누락.기록.Clear();
        var 빈결과 = Build(누락);
        확인(빈결과.GetProperty("summary").GetProperty("storedCandidateCount").GetInt32() == 0
            && 빈결과.GetProperty("summary").GetProperty("sourceRecordCount").GetInt32() == 0, "EmptyStoreNotSuccessFallback");
        확인(빈결과.GetProperty("ingredients")[0].GetProperty("classifications").GetProperty("hs")[0]
            .GetProperty("evidenceStatus").GetString() == "NoStoredReferences", "ClassificationEvidenceMissing");

        거절(x => x.기록.Add(x.기록[0]), "IngredientFoundationDuplicateRecord");
        거절(x => x.기록[0].DataRevision = new string('0', 64), "IngredientFoundationStoredProvenanceMismatch");
        거절(x => x.기록[0].RawSnapshot = null, "IngredientFoundationStoredProvenanceMismatch");
        거절(x => x.기록[0].SourceId = "different-source", "IngredientFoundationStoredProvenanceMismatch");
        거절(x => x.기록[0].DatasetId = "other-dataset", "IngredientFoundationStoredProvenanceMismatch");
        거절(x => x.기록[0].SourceVersion = "changed", "IngredientFoundationSourceVersionMismatch");
        거절(x => x.기록[0].RecordKey = "changed", "IngredientFoundationRecordKeyMismatch");
        거절(x => x.기록[0].StableId = "other:identity", "IngredientFoundationStableIdMismatch");
        거절(x => x.기록[0].TextValue = x.기록[0].TextValue.Replace("커피", "임의 재료", StringComparison.Ordinal), "IngredientFoundationStoredCandidateConflict");
        거절(x => x.대장[0] = x.대장[0] with { Name = "../scope.json" }, "IngredientFoundationReceiptInvalid");
        거절(x => x.대장[1] = x.대장[1] with { Name = "../trade.json" }, "IngredientFoundationReceiptPathInvalid");
        거절(x => x.대장[1] = x.대장[1] with { Url = "https://example.invalid/?serviceKey=hidden" }, "IngredientFoundationUnsafeSourceUrl");
        거절(x => x.대장[0] = x.대장[0] with { Sha256 = new string('0', 64) }, "IngredientFoundationScopeHashMismatch");
        거절(x => { var 행 = x.기록.Single(r => r.MetricCode == "trade.import.unit-value.usd-kg");
            행.DimensionKey = 행.DimensionKey.Replace("period=2025", "period=2024"); 키갱신(행); }, "IngredientFoundationTradeScopeMismatch");
        거절(x => { var 행 = x.기록.Single(r => r.MetricCode == "trade.import.unit-value.usd-kg");
            행.DimensionKey = 행.DimensionKey.Replace("flow=M", "flow=X"); 키갱신(행); }, "IngredientFoundationTradeDirectionMismatch");
        거절(x => { var 행 = x.기록.Single(r => r.MetricCode == "trade.import.unit-value.usd-kg");
            행.DimensionKey += ";hs6=090121"; 키갱신(행); }, "IngredientFoundationDuplicateDimension");
        거절(x => { var 행 = x.기록.Single(r => r.UnitCode == "KRW/1L");
            행.DimensionKey = 행.DimensionKey.Replace("item=9908", "item=111"); 키갱신(행); }, "IngredientFoundationKamisMappingConflict");

        // scope를 바꾼 시험은 scope receipt hash도 갱신해 구조 검증까지 도달한다.
        void 범위거절(Func<string, string> 변형, string 코드)
        {
            var 입력 = Fixture();
            var 변경 = Encoding.UTF8.GetBytes(변형(Encoding.UTF8.GetString(입력.범위)));
            입력.대장[0] = 입력.대장[0] with { Sha256 = 메뉴재료기반Query.Hash(변경) };
            try { 메뉴재료기반Query.Build(변경, 입력.대장, []); }
            catch (InvalidDataException 예외) { 확인(예외.Message == 코드, 코드); return; }
            throw new InvalidDataException("IngredientFoundationTest:ScopeExpectedRejection");
        }
        범위거절(x => x.Replace("CandidateOnly", "Confirmed"), "IngredientFoundationScopeInvalid");
        범위거절(x => x.Replace("090111", "90111"), "IngredientFoundationClassificationInvalid");
        범위거절(x => x.Replace("090121", "090111"), "IngredientFoundationDuplicateClassification");
        범위거절(x => x.Replace("\"Id\": \"rice\"", "\"Id\": \"milk\""), "IngredientFoundationDuplicateIngredient");
        범위거절(x => x.Replace(new string('a', 64), "missing"), "IngredientFoundationRecipeRevisionInvalid");
        return 통과수;
    }

    static JsonElement Build(시험입력 입력) => 메뉴재료기반Query.Build(입력.범위, 입력.대장, 입력.기록);

    static void 키갱신(외부데이터정규화Record 행)
    {
        행.RecordKey = 외부데이터RecordKey.Create(행.SourceId, 행.DatasetId, 행.RegionStableId, 행.MetricCode, 행.EvidenceAsOfUtc, 행.DimensionKey);
        행.StableId = "menu-price:" + 행.RecordKey;
    }

    static 시험입력 Fixture()
    {
        var 재료 = new[]
        {
            new 메뉴재료가격조사.Ingredient("coffee", "커피", "일반 구성 참고", ["090111", "090121"], [], "생두와 볶은 원두 구별"),
            new 메뉴재료가격조사.Ingredient("rice", "쌀", "일반 구성 참고", ["100630"], ["111"], "품종·원산지 미확인"),
            new 메뉴재료가격조사.Ingredient("milk", "우유", "일반 구성 참고", ["040120"], ["9908"], "L→kg 자동 환산 금지")
        };
        var 범위 = JsonSerializer.SerializeToUtf8Bytes(new
        {
            schemaVersion = "menu-ingredient-price-research.v1", period = "2025", mappingState = "CandidateOnly",
            recipeResearchRevision = new string('a', 64), recipeSnapshotSha256 = new string('b', 64), ingredients = 재료
        }, 메뉴재료기반Query.Json);
        var 시각 = new DateTimeOffset(2026, 9, 24, 0, 0, 0, TimeSpan.Zero);
        var 대장 = new[]
        {
            new 메뉴재료가격조사.FileReceipt("scope.json", "scope", "manual-menu-ingredient-research", 메뉴재료기반Query.ScopePath, 메뉴재료기반Query.Hash(범위), 시각, "Success", "CandidateOnly"),
            new 메뉴재료가격조사.FileReceipt("trade.json", "imports", "un-comtrade-local-snapshot", "local:synthetic", new string('c', 64), 시각, "Success", "2025;H6;M;World"),
            new 메뉴재료가격조사.FileReceipt("kamis.json", "kamis", "at-kamis", "https://example.invalid/kamis", new string('d', 64), 시각, "Success", "class=01;category=100"),
            new 메뉴재료가격조사.FileReceipt("livestock.json", "kamis", "at-kamis", "https://example.invalid/kamis", new string('e', 64), 시각, "Success", "class=02;category=500")
        };
        var 기록 = new List<외부데이터정규화Record>();
        void 추가(int 파일, string 지표, string 차원, string 원문, string 단위, decimal? 값 = null)
        {
            var 원본 = 대장[파일];
            var 행 = new 외부데이터정규화Record
            {
                SourceId = 원본.Source, DatasetId = 메뉴재료기반Query.Dataset, RegionStableId = "country:kr",
                MetricCode = 지표, DimensionKey = 원본.Name + ";" + 차원, TextValue = 원문, UnitCode = 단위, NumericValue = 값,
                EvidenceAsOfUtc = 시각, CollectedAtUtc = 시각, SourceVersion = 원본.Kind + ":" + 원본.Context, DataRevision = 원본.Sha256,
                QualityCode = "PendingHumanReview", LimitationCode = "PrivateReviewOnly",
                RawSnapshot = new() { SourceId = 원본.Source, DatasetId = 메뉴재료기반Query.Dataset, ContentHashSha256 = 원본.Sha256 }
            };
            키갱신(행); 기록.Add(행);
        }
        foreach (var 항목 in 재료) 추가(0, "ingredient.hs.candidate", 항목.Id, JsonSerializer.Serialize(항목, 메뉴재료기반Query.Json), "candidate");
        추가(1, "trade.import.unit-value.usd-kg", "hs6=090111;flow=M;period=2025;reporter=410;partner=0;classification=H6", "SecretUnprojectedText", "USD/kg", 7);
        추가(2, "domestic.kamis.price.krw", "class=01;category=100;ingredient=rice;item=111", "SecretUnprojectedText", "KRW/20kg", 60000);
        추가(3, "domestic.kamis.price.krw", "class=02;category=500;ingredient=milk;item=9908", "SecretUnprojectedText", "KRW/1L", 2900);
        추가(3, "research.source.caveat", "category=500", "SecretUnprojectedText", "interpretation");
        return new(범위, 대장, 기록);
    }
}
