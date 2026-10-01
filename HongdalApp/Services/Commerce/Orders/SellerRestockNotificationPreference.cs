namespace HongdalApp.Services.Commerce.Orders;

public sealed class SellerRestockNotificationPreference
{
    public string SellerUserId { get; set; } = string.Empty;

    public string SellerName { get; set; } = string.Empty;

    public string KakaoTalkChannelName { get; set; } = string.Empty;

    public bool AdminAllowsKakaoTalk { get; set; }

    public bool SellerWantsKakaoTalk { get; set; }

    public bool UseInternalNotification { get; set; } = true;

    public bool CanSendKakaoTalk => AdminAllowsKakaoTalk && SellerWantsKakaoTalk;

    public string StatusLabel => CanSendKakaoTalk
        ? "카카오톡 발송"
        : !AdminAllowsKakaoTalk
            ? "관리자 차단"
            : "판매자 수신거부";
}
