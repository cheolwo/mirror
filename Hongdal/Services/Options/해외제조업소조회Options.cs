namespace 홍달.Services.Options
{
    public sealed class 해외제조업소조회Options
    {
        public const string SectionName = "해외제조업소조회";

        public string BaseUrl { get; set; } = "https://apis.data.go.kr/1471000/IprtFoodOvseaMnftBsshInfoService02";
        public string Endpoint { get; set; } = "/getIprtFoodOvseaMnftBsshInfoInq02";
        public string ServiceKey { get; set; } = string.Empty;
        public string DefaultType { get; set; } = "xml";
        public int TimeoutSeconds { get; set; } = 20;
    }
}
