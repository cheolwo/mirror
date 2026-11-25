namespace 살뜰.Services.Options;

public sealed class KakaoAlimTalkOptions
{
    public const string SectionName = "KakaoAlimTalk";

    public bool Enabled { get; set; }

    public string BaseUrl { get; set; } = string.Empty;

    public string SendPath { get; set; } = "/messages/alimtalk";

    public string ApiKey { get; set; } = string.Empty;

    public string SenderKey { get; set; } = string.Empty;

    public string DispatchAcceptedTemplateCode { get; set; } = "dispatch_accepted";

    public string DispatchPickupApproachTemplateCode { get; set; } = "dispatch_pickup_approach";

    public string SettlementDepositReminderTemplateCode { get; set; } = "settlement_deposit_reminder";

    public string WorkforceAvailabilityTemplateCode { get; set; } = string.Empty;

    public string WorkforceReplyBaseUrl { get; set; } = string.Empty;

    public int TimeoutSeconds { get; set; } = 10;
}
