using System.Text.Json;

// 실제 체크인 입력과 합성 변형을 함께 검사한다. DB·네트워크·운영 발주는 사용하지 않는다.
internal static class 메뉴재료수요분석Tests
{
    private sealed class 시험입력
    {
        public required 메뉴재료수요분석.수요Catalog Catalog { get; set; }
        public required 메뉴재료수요분석.수요Scenario Scenario { get; set; }
    }

    public static int Run(byte[] 카탈로그원문, byte[] 시나리오원문, byte[] 기반원문)
    {
        var 통과수 = 0;
        void 확인(bool 조건, string 이름)
        {
            메뉴재료수요분석.Require(조건, "MenuIngredientDemandTest:" + 이름);
            통과수++;
        }
        void 거절(Action<시험입력> 변형, string 예상코드)
        {
            var 입력 = new 시험입력 { Catalog = CloneCatalog(카탈로그원문), Scenario = CloneScenario(시나리오원문) };
            변형(입력);
            try { 메뉴재료수요분석.Build(메뉴재료수요분석.Bytes(입력.Catalog), 메뉴재료수요분석.Bytes(입력.Scenario), 기반원문); }
            catch (InvalidDataException 예외)
            {
                확인(예외.Message == 예상코드, 예상코드);
                return;
            }
            throw new InvalidDataException("MenuIngredientDemandTest:ExpectedRejection:" + 예상코드);
        }

        var 결과 = 메뉴재료수요분석.Build(카탈로그원문, 시나리오원문, 기반원문);
        확인(결과.GetProperty("authorityCode").GetString() == "Simulation"
            && 결과.GetProperty("operationalEffectCode").GetString() == "None"
            && !결과.GetProperty("autoCreatePurchaseOrder").GetBoolean(), "NoOperationalEffect");
        확인(결과.GetProperty("summary").GetProperty("menuCount").GetInt32() == 3
            && 결과.GetProperty("summary").GetProperty("ingredientCount").GetInt32() == 7
            && 결과.GetProperty("summary").GetProperty("simulationEstimateLineCount").GetInt32() == 9,
            "SampleCoverage");
        확인(결과.GetProperty("summary").GetProperty("observedFactCount").GetInt32() == 3
            && 결과.GetProperty("summary").GetProperty("referenceCandidateCount").GetInt32() == 3,
            "EvidenceClassesRemainSeparate");

        var 메뉴수요 = 결과.GetProperty("menuDemand").EnumerateArray().ToArray();
        확인(메뉴수요.Select(x => x.GetProperty("storeStableId").GetString()).SequenceEqual(
            메뉴수요.Select(x => x.GetProperty("storeStableId").GetString()).Order(StringComparer.Ordinal)), "MenuReverseLookupStableOrder");
        확인(메뉴수요.All(x => x.GetProperty("menuEvidence").GetProperty("classCode").GetString() == "ObservedFact"
            && x.GetProperty("recipeEvidence").GetProperty("classCode").GetString() == "ReferenceCandidate"
            && x.GetProperty("authorityCode").GetString() == "Simulation"
            && !string.IsNullOrWhiteSpace(x.GetProperty("observedStoreName").GetString())), "FactReferenceSimulationSeparated");
        확인(메뉴수요.SelectMany(x => x.GetProperty("ingredients").EnumerateArray()).All(x =>
            x.GetProperty("evidenceStableIds").EnumerateArray().Any(e => e.GetString() == 메뉴재료수요분석.EstimateEvidenceStableId)),
            "EstimateEvidenceAttached");

        var 재료수요 = 결과.GetProperty("ingredientDemand").EnumerateArray().ToArray();
        JsonElement 재료(string id) => 재료수요.Single(x => x.GetProperty("ingredientId").GetString() == id);
        var 닭 = 재료("chicken");
        확인(닭.GetProperty("simulationProductStableId").GetString() == "product:ingredient:chicken"
            && 닭.GetProperty("canonicalProductStableId").ValueKind == JsonValueKind.Null
            && 닭.GetProperty("productIdentityStateCode").GetString() == "SimulationScopedCandidate"
            && 닭.GetProperty("contentUnitCode").GetString() == "KGM"
            && 닭.GetProperty("minimumDemandQuantity").GetDecimal() == 9.9m
            && 닭.GetProperty("maximumDemandQuantity").GetDecimal() == 12.1m, "ChickenKgDemand");
        확인(닭.GetProperty("purchaseUnitCode").GetString() == "BOX"
            && 닭.GetProperty("purchaseUnitContentQuantity").GetDecimal() == 10m
            && 닭.GetProperty("purchaseSpecificationStateCode").GetString() == "SimulationAssumption"
            && 닭.GetProperty("minimumPurchaseUnitCount").GetInt64() == 1
            && 닭.GetProperty("maximumPurchaseUnitCount").GetInt64() == 2, "ChickenBoxCeiling");
        var 소 = 재료("beef");
        확인(소.GetProperty("simulationProductStableId").GetString() == "product:ingredient:beef"
            && 소.GetProperty("minimumDemandQuantity").GetDecimal() == 7.2m
            && 소.GetProperty("maximumDemandQuantity").GetDecimal() == 8.8m
            && 소.GetProperty("purchaseUnitCode").GetString() == "BOX"
            && 소.GetProperty("purchaseUnitContentQuantity").GetDecimal() == 5m
            && 소.GetProperty("minimumPurchaseUnitCount").GetInt64() == 2, "BeefBoxCeiling");
        var 쌀 = 재료("rice");
        확인(쌀.GetProperty("simulationProductStableId").GetString() == "product:ingredient:rice"
            && 쌀.GetProperty("minimumDemandQuantity").GetDecimal() == 27m
            && 쌀.GetProperty("maximumDemandQuantity").GetDecimal() == 33m
            && 쌀.GetProperty("purchaseUnitCode").GetString() == "BAG"
            && 쌀.GetProperty("purchaseUnitContentQuantity").GetDecimal() == 20m
            && 쌀.GetProperty("maximumPurchaseUnitCount").GetInt64() == 2, "RiceBagCeiling");
        확인(재료("onion").GetProperty("sourceMenus").GetArrayLength() == 2
            && 재료("garlic").GetProperty("sourceMenus").GetArrayLength() == 2, "IngredientToMenuReverseLookup");
        확인(재료수요.Select(x => x.GetProperty("simulationProductStableId").GetString()).SequenceEqual(
            재료수요.Select(x => x.GetProperty("simulationProductStableId").GetString()).Order(StringComparer.Ordinal)), "IngredientStableOrder");
        확인(결과.GetRawText() == 메뉴재료수요분석.Build(카탈로그원문, 시나리오원문, 기반원문).GetRawText(), "DeterministicProjection");
        확인(메뉴재료수요분석.Kg(12_345m) == 12.345m
            && 메뉴재료수요분석.Ceiling(10.001m, 10m) == 2
            && 메뉴재료수요분석.Ceiling(10m, 10m) == 1, "UnitConversionBoundary");

        거절(x => x.Scenario = x.Scenario with { AuthorityCode = "Operational" }, "MenuIngredientDemandScenarioAuthorityInvalid");
        거절(x => x.Scenario = x.Scenario with { AutoCreatePurchaseOrder = true }, "MenuIngredientDemandScenarioAuthorityInvalid");
        거절(x => x.Scenario.Orders[0] = x.Scenario.Orders[0] with { PlannedServingCount = 0 }, "MenuIngredientDemandScenarioRequestInvalid");
        거절(x => x.Scenario.Orders[0] = x.Scenario.Orders[0] with { MenuStableId = "observed-menu:000000000000000000000000" }, "MenuIngredientDemandScenarioRequestInvalid");
        거절(x => x.Scenario.Orders[1] = x.Scenario.Orders[1] with { RequestStableId = x.Scenario.Orders[0].RequestStableId }, "MenuIngredientDemandDuplicateRequest");
        거절(x => x.Catalog.Menus[0] = x.Catalog.Menus[0] with
        {
            RecipeEvidence = x.Catalog.Menus[0].RecipeEvidence with { ActualBranchRecipeVerified = true }
        }, "MenuIngredientDemandRecipeBoundaryInvalid");
        거절(x => x.Catalog.Menus[0] = x.Catalog.Menus[0] with
        {
            MenuEvidence = x.Catalog.Menus[0].MenuEvidence with { FactScope = "ActualRecipe" }
        }, "MenuIngredientDemandObservationBoundaryInvalid");
        거절(x => x.Catalog.Menus[0].Ingredients[0] = x.Catalog.Menus[0].Ingredients[0] with
        {
            EstimateStateCode = "Fact"
        }, "MenuIngredientDemandEstimateInvalid");
        거절(x => x.Catalog.Menus[0].Ingredients[0] = x.Catalog.Menus[0].Ingredients[0] with
        {
            IngredientId = "invented"
        }, "MenuIngredientDemandIngredientIdentityInvalid");
        거절(x => x.Catalog.Menus[0].Ingredients[0] = x.Catalog.Menus[0].Ingredients[0] with
        {
            SimulationProductStableId = "product:ingredient:other"
        }, "MenuIngredientDemandIngredientIdentityInvalid");
        거절(x => x.Catalog.Menus[0].Ingredients[0] = x.Catalog.Menus[0].Ingredients[0] with
        {
            ContentUnitCode = "GRM"
        }, "MenuIngredientDemandPurchaseUnitInvalid");
        거절(x => x.Catalog.Menus[0].Ingredients[0] = x.Catalog.Menus[0].Ingredients[0] with
        {
            PurchaseUnitContentQuantity = 0
        }, "MenuIngredientDemandPurchaseUnitInvalid");
        거절(x => x.Catalog.Menus[0].Ingredients[0] = x.Catalog.Menus[0].Ingredients[0] with
        {
            MaximumGramsPerServing = 1
        }, "MenuIngredientDemandEstimateInvalid");
        거절(x => x.Catalog.Menus[0].Ingredients[0] = x.Catalog.Menus[0].Ingredients[0] with
        {
            SourceServingCount = -1
        }, "MenuIngredientDemandReferenceQuantityInvalid");
        거절(x => x.Catalog = x.Catalog with
        {
            Foundation = x.Catalog.Foundation with { ScopeSha256 = new string('0', 64) }
        }, "MenuIngredientDemandFoundationMismatch");
        거절(x => x.Catalog.Menus[2].Ingredients[1] = x.Catalog.Menus[2].Ingredients[1] with
        {
            PurchaseUnitContentQuantity = 11
        }, "MenuIngredientDemandProductSpecificationConflict");

        return 통과수;
    }

    private static 메뉴재료수요분석.수요Catalog CloneCatalog(byte[] bytes) =>
        JsonSerializer.Deserialize<메뉴재료수요분석.수요Catalog>(bytes, 메뉴재료수요분석.Json)!;

    private static 메뉴재료수요분석.수요Scenario CloneScenario(byte[] bytes) =>
        JsonSerializer.Deserialize<메뉴재료수요분석.수요Scenario>(bytes, 메뉴재료수요분석.Json)!;

}
