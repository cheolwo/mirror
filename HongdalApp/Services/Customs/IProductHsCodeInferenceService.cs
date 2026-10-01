namespace HongdalApp.Services.Customs;

public interface IProductHsCodeInferenceService
{
    IReadOnlyList<HsCodeSuggestion> Suggest(string cargoName, string flowDirection);
}
