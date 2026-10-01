namespace HongdalApp.Services.Customs;

public sealed class ProductHsCodeInferenceService : IProductHsCodeInferenceService
{
    public IReadOnlyList<HsCodeSuggestion> Suggest(string cargoName, string flowDirection)
    {
        var normalized = cargoName.ToLowerInvariant();
        var suggestions = new List<HsCodeSuggestion>();

        if (normalized.Contains("의자") || normalized.Contains("chair"))
        {
            suggestions.Add(new HsCodeSuggestion
            {
                HsCode = "9401.69",
                Description = "목재 프레임 의자류 후보",
                ConfidenceScore = 0.82m,
                Reason = "상품명에 의자/chair 키워드가 있고 가구류 수출입 흐름입니다."
            });
        }

        if (normalized.Contains("식품") || normalized.Contains("간편식") || normalized.Contains("food"))
        {
            suggestions.Add(new HsCodeSuggestion
            {
                HsCode = "2106.90",
                Description = "기타 조제식료품 후보",
                ConfidenceScore = 0.66m,
                Reason = "상품명에 식품/food 계열 키워드가 포함되어 있습니다."
            });
        }

        if (normalized.Contains("전자") || normalized.Contains("device") || normalized.Contains("battery"))
        {
            suggestions.Add(new HsCodeSuggestion
            {
                HsCode = "8543.70",
                Description = "기타 전기기기 후보",
                ConfidenceScore = 0.58m,
                Reason = "전자제품 계열 키워드가 있어 전기기기 후보로 분류했습니다."
            });
        }

        if (suggestions.Count == 0)
        {
            suggestions.Add(new HsCodeSuggestion
            {
                HsCode = "검토필요",
                Description = "관세사 확인 필요",
                ConfidenceScore = 0.20m,
                Reason = $"{flowDirection} 물류 흐름이지만 상품명만으로 HS 후보를 좁히기 어렵습니다."
            });
        }

        return suggestions.OrderByDescending(x => x.ConfidenceScore).ToList();
    }
}
