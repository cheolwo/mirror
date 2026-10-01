namespace 홍달.Services.Options
{
    public sealed class KieAiOptions
    {
        public const string SectionName = "KieAi";

        public string ApiKey { get; set; } = string.Empty;
        public string BaseUrl { get; set; } = "https://api.kie.ai";
        public string CreateTaskPath { get; set; } = "/api/v1/jobs/createTask";
        public string GetTaskPathTemplate { get; set; } = "/api/v1/jobs/recordInfo?taskId={taskId}";
        public string Model { get; set; } = "gpt-image-2-text-to-image";
        public string? CallbackBaseUrl { get; set; }
        public int TimeoutSeconds { get; set; } = 60;
        public int PollingIntervalSeconds { get; set; } = 15;
        public int MaxPollingMinutes { get; set; } = 15;
    }
}
