namespace HongdalApp.Services.Customs;

public sealed class HsCodeSuggestion
{
    public string HsCode { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public decimal ConfidenceScore { get; set; }

    public string Reason { get; set; } = string.Empty;
}
