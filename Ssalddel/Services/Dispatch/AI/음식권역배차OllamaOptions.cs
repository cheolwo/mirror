namespace 살뜰.Services.Dispatch.AI;

/// <summary>권역 검토는 기본 비활성이고 기존 후보 AI와 별도로 켭니다.</summary>
public sealed class 음식권역배차OllamaOptions
{
    public const string SectionName = "FoodRegionalDispatchOllama";
    public bool Enabled { get; set; }
    public string BaseUrl { get; set; } = "http://127.0.0.1:12681/";
    public string Model { get; set; } = FoodDispatchOllamaOptions.AllowedModel;
    public int TimeoutSeconds { get; set; } = 10;
    public int MaxPlans { get; set; } = 12;

    public bool Validate() => new FoodDispatchOllamaOptions
    {
        BaseUrl = BaseUrl, Model = Model, TimeoutSeconds = TimeoutSeconds, MaxCandidates = MaxPlans
    }.Validate();
}
