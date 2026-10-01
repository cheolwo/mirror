using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;

// 배달 관찰 사실, 공공 레시피 참고 후보, Simulation 수요를 서로 다른 상태로 유지하는 조회 전용 계산기다.
internal static class 메뉴재료수요분석
{
    internal const string CatalogPath = "eng/public-data/menu-ingredient-demand/catalog.r1.json";
    internal const string ScenarioPath = "eng/public-data/menu-ingredient-demand/scenario.sample.r1.json";
    internal const string OutputFolder = "artifacts/local/public-data/menu-ingredient-demand";
    internal const string EstimateEvidenceStableId = "source:menu-ingredient-estimate:r1";
    internal static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static async Task RunAsync(string 모드, string 루트, Dictionary<string, object?> 결과)
    {
        Require(모드 is "analyze" or "self-test", "MenuIngredientDemandModeInvalid");
        var 카탈로그원문 = await File.ReadAllBytesAsync(Path.Combine(루트, CatalogPath));
        var 시나리오원문 = await File.ReadAllBytesAsync(Path.Combine(루트, ScenarioPath));
        var 기반원문 = await File.ReadAllBytesAsync(Path.Combine(루트, 메뉴재료기반Query.ScopePath));

        if (모드 == "self-test")
        {
            결과["selfTestsPassed"] = 메뉴재료수요분석Tests.Run(카탈로그원문, 시나리오원문, 기반원문);
            결과["databaseWriteAttempted"] = false;
            결과["operationalOrderCreated"] = false;
            결과["stage"] = "completed";
            return;
        }

        var 분석 = Build(카탈로그원문, 시나리오원문, 기반원문);
        var 출력 = JsonSerializer.SerializeToUtf8Bytes(분석, Json);
        var 판본 = Hash(출력);
        var 출력폴더 = Path.Combine(루트, OutputFolder);
        Directory.CreateDirectory(출력폴더);
        var 출력경로 = Path.Combine(출력폴더, 판본 + ".json");
        if (File.Exists(출력경로))
            Require((await File.ReadAllBytesAsync(출력경로)).SequenceEqual(출력), "MenuIngredientDemandOutputConflict");
        else
            await File.WriteAllBytesAsync(출력경로, 출력);

        결과["mode"] = 모드;
        결과["projectionSha256"] = 판본;
        결과["catalogSha256"] = Hash(카탈로그원문);
        결과["scenarioSha256"] = Hash(시나리오원문);
        결과["summary"] = 분석.GetProperty("summary").Clone();
        결과["output"] = 출력경로;
        결과["databaseWriteAttempted"] = false;
        결과["operationalOrderCreated"] = false;
        결과["stage"] = "completed";
    }

    internal static JsonElement Build(byte[] 카탈로그원문, byte[] 시나리오원문, byte[] 기반원문)
    {
        var 카탈로그 = JsonSerializer.Deserialize<수요Catalog>(카탈로그원문, Json);
        var 시나리오 = JsonSerializer.Deserialize<수요Scenario>(시나리오원문, Json);
        Require(카탈로그 != null && 시나리오 != null, "MenuIngredientDemandInputInvalid");
        Require(카탈로그.SchemaVersion == "menu-ingredient-demand-catalog.v1"
            && 카탈로그.Foundation.DatasetId == 메뉴재료기반Query.Dataset
            && 카탈로그.Foundation.ScopePath == 메뉴재료기반Query.ScopePath
            && 카탈로그.Foundation.ScopeSha256 == Hash(기반원문), "MenuIngredientDemandFoundationMismatch");
        Require(카탈로그.EstimateEvidenceStableId == EstimateEvidenceStableId
            && 카탈로그.Use == "SimulationPlanningOnly;NoOperationalOrder;NotActualBranchRecipe;PendingHumanReview"
            && !카탈로그.SourceReview.ActualBranchRecipeVerified, "MenuIngredientDemandAuthorityBoundaryInvalid");
        Require(IsHash(카탈로그.SourceReview.ResearchRevision)
            && IsHash(카탈로그.SourceReview.MenuReviewSha256)
            && IsHash(카탈로그.SourceReview.RecipeLinksSha256)
            && IsHash(카탈로그.SourceReview.RecipeSnapshotSha256), "MenuIngredientDemandSourceRevisionInvalid");

        using var 기반문서 = JsonDocument.Parse(기반원문);
        var 기반재료 = 기반문서.RootElement.GetProperty("ingredients").EnumerateArray()
            .ToDictionary(x => x.GetProperty("id").GetString()!, x => x.GetProperty("name").GetString()!, StringComparer.Ordinal);
        Require(카탈로그.Menus is { Length: > 0 and <= 100 }
            && 카탈로그.Menus.Select(x => x.MenuStableId).Distinct(StringComparer.Ordinal).Count() == 카탈로그.Menus.Length,
            "MenuIngredientDemandDuplicateMenu");

        foreach (var 메뉴 in 카탈로그.Menus)
        {
            Require(Regex.IsMatch(메뉴.MenuStableId, "^observed-menu:[a-f0-9]{24}$")
                && !string.IsNullOrWhiteSpace(메뉴.DisplayName) && !string.IsNullOrWhiteSpace(메뉴.StoreDisplayName),
                "MenuIngredientDemandMenuIdentityInvalid");
            Require(메뉴.MenuEvidence.ClassCode == "ObservedFact"
                && 메뉴.MenuEvidence.StateCode == "RecordedDeliveryMenu"
                && 메뉴.MenuEvidence.FactScope == "StoreAndMenuNameOnly"
                && 메뉴.MenuEvidence.StableId == "source:delivery-menu-observation:" + 메뉴.MenuStableId["observed-menu:".Length..],
                "MenuIngredientDemandObservationBoundaryInvalid");
            Require(메뉴.RecipeEvidence.ClassCode == "ReferenceCandidate"
                && 메뉴.RecipeEvidence.StateCode == "PendingReview"
                && !메뉴.RecipeEvidence.ActualBranchRecipeVerified
                && 메뉴.RecipeEvidence.StableId == "source:mfds-recipe:" + 메뉴.RecipeEvidence.RecordKey
                && IsHash(메뉴.RecipeEvidence.RecordKey) && IsHash(메뉴.RecipeEvidence.SourceContentChecksum)
                && Uri.TryCreate(메뉴.RecipeEvidence.OriginalUrl, UriKind.Absolute, out var 출처Url)
                && 출처Url.Scheme == Uri.UriSchemeHttps, "MenuIngredientDemandRecipeBoundaryInvalid");
            Require(메뉴.Ingredients is { Length: > 0 and <= 64 }
                && 메뉴.Ingredients.Select(x => x.IngredientId).Distinct(StringComparer.Ordinal).Count() == 메뉴.Ingredients.Length,
                "MenuIngredientDemandDuplicateIngredient");

            foreach (var 재료 in 메뉴.Ingredients)
            {
                Require(기반재료.TryGetValue(재료.IngredientId, out var 이름) && 이름 == 재료.IngredientName
                    && 재료.SimulationProductStableId == "product:ingredient:" + 재료.IngredientId,
                    "MenuIngredientDemandIngredientIdentityInvalid");
                Require(재료.ReferenceQuantityGrams > 0 && 재료.MinimumGramsPerServing > 0
                    && 재료.MaximumGramsPerServing >= 재료.MinimumGramsPerServing
                    && 재료.EstimateStateCode == "SimulationEstimate"
                    && 재료.ConfidenceCode is "Low" or "Medium" or "High"
                    && !string.IsNullOrWhiteSpace(재료.RangeBasis), "MenuIngredientDemandEstimateInvalid");
                Require(재료.SourceServingCount is null or > 0
                    && !string.IsNullOrWhiteSpace(재료.SourceServingBasisCode)
                    && !string.IsNullOrWhiteSpace(재료.SourceExpression), "MenuIngredientDemandReferenceQuantityInvalid");
                Require(재료.ContentUnitCode == "KGM" && 재료.PurchaseUnitContentQuantity > 0
                    && Regex.IsMatch(재료.PurchaseUnitCode, "^[A-Z]{2,12}$"), "MenuIngredientDemandPurchaseUnitInvalid");
            }
        }

        Require(시나리오.SchemaVersion == "menu-ingredient-demand-scenario.v1"
            && 시나리오.AuthorityCode == "Simulation" && !시나리오.AutoCreatePurchaseOrder,
            "MenuIngredientDemandScenarioAuthorityInvalid");
        Require(시나리오.Orders is { Length: > 0 and <= 1000 }
            && 시나리오.Orders.Select(x => x.RequestStableId).Distinct(StringComparer.Ordinal).Count() == 시나리오.Orders.Length,
            "MenuIngredientDemandDuplicateRequest");
        var 메뉴색인 = 카탈로그.Menus.ToDictionary(x => x.MenuStableId, StringComparer.Ordinal);
        foreach (var 요청 in 시나리오.Orders)
            Require(!string.IsNullOrWhiteSpace(요청.RequestStableId) && !string.IsNullOrWhiteSpace(요청.StoreStableId)
                && 메뉴색인.ContainsKey(요청.MenuStableId) && 요청.PlannedServingCount is > 0 and <= 1_000_000,
                "MenuIngredientDemandScenarioRequestInvalid");

        var 메뉴결과 = 시나리오.Orders
            .OrderBy(x => x.StoreStableId, StringComparer.Ordinal).ThenBy(x => x.MenuStableId, StringComparer.Ordinal)
            .Select(요청 =>
            {
                var 메뉴 = 메뉴색인[요청.MenuStableId];
                return new
                {
                    requestStableId = 요청.RequestStableId,
                    storeStableId = 요청.StoreStableId,
                    menuStableId = 메뉴.MenuStableId,
                    menuName = 메뉴.DisplayName,
                    observedStoreName = 메뉴.StoreDisplayName,
                    plannedServingCount = 요청.PlannedServingCount,
                    authorityCode = "Simulation",
                    menuEvidence = 메뉴.MenuEvidence,
                    recipeEvidence = 메뉴.RecipeEvidence,
                    estimateEvidenceStableId = 카탈로그.EstimateEvidenceStableId,
                    ingredients = 메뉴.Ingredients.OrderBy(x => x.SimulationProductStableId, StringComparer.Ordinal).Select(재료 =>
                    {
                        var 최소 = Kg(재료.MinimumGramsPerServing * 요청.PlannedServingCount);
                        var 최대 = Kg(재료.MaximumGramsPerServing * 요청.PlannedServingCount);
                        return new
                        {
                            재료.IngredientId, 재료.IngredientName, 재료.SimulationProductStableId,
                            canonicalProductStableId = (string?)null,
                            productIdentityStateCode = "SimulationScopedCandidate",
                            contentUnitCode = 재료.ContentUnitCode,
                            재료.ReferenceQuantityGrams,
                            재료.SourceServingCount,
                            재료.SourceServingBasisCode,
                            재료.SourceExpression,
                            재료.MinimumGramsPerServing,
                            재료.MaximumGramsPerServing,
                            minimumDemandQuantity = 최소,
                            maximumDemandQuantity = 최대,
                            재료.PurchaseUnitCode,
                            재료.PurchaseUnitContentQuantity,
                            purchaseSpecificationStateCode = "SimulationAssumption",
                            minimumPurchaseUnitCount = Ceiling(최소, 재료.PurchaseUnitContentQuantity),
                            maximumPurchaseUnitCount = Ceiling(최대, 재료.PurchaseUnitContentQuantity),
                            재료.EstimateStateCode, 재료.ConfidenceCode, 재료.RangeBasis,
                            evidenceStableIds = new[] { 메뉴.MenuEvidence.StableId, 메뉴.RecipeEvidence.StableId, 카탈로그.EstimateEvidenceStableId }
                        };
                    }).ToArray(),
                    unresolvedRecipeComponents = 메뉴.UnresolvedRecipeComponents
                };
            }).ToArray();

        var 재료결과 = 시나리오.Orders
            .SelectMany(요청 => 메뉴색인[요청.MenuStableId].Ingredients.Select(재료 => new
            {
                요청.RequestStableId, 요청.StoreStableId, 요청.MenuStableId, 요청.PlannedServingCount,
                Menu = 메뉴색인[요청.MenuStableId], Ingredient = 재료
            }))
            .GroupBy(x => x.Ingredient.SimulationProductStableId, StringComparer.Ordinal)
            .OrderBy(x => x.Key, StringComparer.Ordinal)
            .Select(그룹 =>
            {
                var 기준 = 그룹.First().Ingredient;
                Require(그룹.All(x => x.Ingredient.IngredientId == 기준.IngredientId
                    && x.Ingredient.ContentUnitCode == 기준.ContentUnitCode
                    && x.Ingredient.PurchaseUnitCode == 기준.PurchaseUnitCode
                    && x.Ingredient.PurchaseUnitContentQuantity == 기준.PurchaseUnitContentQuantity),
                    "MenuIngredientDemandProductSpecificationConflict");
                var 최소 = Kg(그룹.Sum(x => x.Ingredient.MinimumGramsPerServing * x.PlannedServingCount));
                var 최대 = Kg(그룹.Sum(x => x.Ingredient.MaximumGramsPerServing * x.PlannedServingCount));
                return new
                {
                    ingredientId = 기준.IngredientId,
                    ingredientName = 기준.IngredientName,
                    simulationProductStableId = 기준.SimulationProductStableId,
                    canonicalProductStableId = (string?)null,
                    productIdentityStateCode = "SimulationScopedCandidate",
                    contentUnitCode = 기준.ContentUnitCode,
                    minimumDemandQuantity = 최소,
                    maximumDemandQuantity = 최대,
                    purchaseUnitCode = 기준.PurchaseUnitCode,
                    purchaseUnitContentQuantity = 기준.PurchaseUnitContentQuantity,
                    purchaseSpecificationStateCode = "SimulationAssumption",
                    minimumPurchaseUnitCount = Ceiling(최소, 기준.PurchaseUnitContentQuantity),
                    maximumPurchaseUnitCount = Ceiling(최대, 기준.PurchaseUnitContentQuantity),
                    demandStateCode = "SimulationDerivedEstimate",
                    sourceMenus = 그룹.OrderBy(x => x.MenuStableId, StringComparer.Ordinal).ThenBy(x => x.StoreStableId, StringComparer.Ordinal)
                        .Select(x => new { x.MenuStableId, menuName = x.Menu.DisplayName, x.StoreStableId, x.PlannedServingCount, x.RequestStableId }).ToArray(),
                    evidenceStableIds = 그룹.SelectMany(x => new[] { x.Menu.MenuEvidence.StableId, x.Menu.RecipeEvidence.StableId, 카탈로그.EstimateEvidenceStableId })
                        .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray()
                };
            }).ToArray();

        return JsonSerializer.SerializeToElement(new
        {
            schemaVersion = "menu-ingredient-demand-projection.v1",
            카탈로그.CatalogStableId,
            시나리오.ScenarioStableId,
            authorityCode = "Simulation",
            operationalEffectCode = "None",
            autoCreatePurchaseOrder = false,
            inputSha256 = new { catalog = Hash(카탈로그원문), scenario = Hash(시나리오원문), ingredientFoundation = Hash(기반원문) },
            sourceReview = 카탈로그.SourceReview,
            summary = new
            {
                requestCount = 시나리오.Orders.Length,
                menuCount = 시나리오.Orders.Select(x => x.MenuStableId).Distinct(StringComparer.Ordinal).Count(),
                ingredientCount = 재료결과.Length,
                plannedServingCount = 시나리오.Orders.Sum(x => x.PlannedServingCount),
                observedFactCount = 메뉴결과.Length,
                referenceCandidateCount = 메뉴결과.Length,
                simulationEstimateLineCount = 메뉴결과.Sum(x => x.ingredients.Length)
            },
            menuDemand = 메뉴결과,
            ingredientDemand = 재료결과
        }, Json);
    }

    internal static decimal Kg(decimal grams) => decimal.Round(grams / 1000m, 6, MidpointRounding.AwayFromZero);
    internal static long Ceiling(decimal quantity, decimal packageQuantity) => checked((long)decimal.Ceiling(quantity / packageQuantity));
    internal static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    internal static bool IsHash(string value) => Regex.IsMatch(value ?? "", "^[a-f0-9]{64}$");
    internal static byte[] Bytes<T>(T value) => JsonSerializer.SerializeToUtf8Bytes(value, Json);

    internal sealed record 수요Catalog(
        string SchemaVersion,
        string CatalogStableId,
        기반Ref Foundation,
        조사Ref SourceReview,
        string EstimateEvidenceStableId,
        string Use,
        메뉴Entry[] Menus);
    internal sealed record 기반Ref(string DatasetId, string ScopePath, string ScopeSha256);
    internal sealed record 조사Ref(
        string ResearchRevision,
        string MenuReviewSha256,
        string RecipeLinksSha256,
        string RecipeSnapshotSha256,
        string ReviewedOn,
        bool ActualBranchRecipeVerified);
    internal sealed record 메뉴Entry(
        string MenuStableId,
        string DisplayName,
        string StoreDisplayName,
        메뉴근거 MenuEvidence,
        레시피근거 RecipeEvidence,
        재료Entry[] Ingredients,
        string[] UnresolvedRecipeComponents);
    internal sealed record 메뉴근거(string StableId, string ClassCode, string StateCode, string FactScope);
    internal sealed record 레시피근거(
        string StableId,
        string ClassCode,
        string StateCode,
        string RecordKey,
        string Title,
        string SourceKey,
        string SourceContentChecksum,
        string CollectedAtUtc,
        string OriginalUrl,
        string MatchScope,
        bool ActualBranchRecipeVerified);
    internal sealed record 재료Entry(
        string IngredientId,
        string IngredientName,
        string SimulationProductStableId,
        decimal ReferenceQuantityGrams,
        decimal? SourceServingCount,
        string SourceServingBasisCode,
        string SourceExpression,
        decimal MinimumGramsPerServing,
        decimal MaximumGramsPerServing,
        string EstimateStateCode,
        string ConfidenceCode,
        string RangeBasis,
        string PurchaseUnitCode,
        decimal PurchaseUnitContentQuantity,
        string ContentUnitCode);
    internal sealed record 수요Scenario(
        string SchemaVersion,
        string ScenarioStableId,
        string AuthorityCode,
        bool AutoCreatePurchaseOrder,
        수요Request[] Orders);
    internal sealed record 수요Request(
        string RequestStableId,
        string StoreStableId,
        string MenuStableId,
        int PlannedServingCount);

    internal static void Require([DoesNotReturnIf(false)] bool condition, string code)
    {
        if (!condition) throw new InvalidDataException(code);
    }
}
