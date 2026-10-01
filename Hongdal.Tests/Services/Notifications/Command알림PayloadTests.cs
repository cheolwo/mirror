using System.Text.Json;
using 홍달.Services.Notifications;

namespace Hongdal.Tests.Services.Notifications;

public sealed class Command알림PayloadTests
{
    [Fact]
    public void Parse_예약시각이_미래면_아직_발송대상으로_보지_않는다()
    {
        var now = new DateTime(2026, 7, 9, 3, 0, 0, DateTimeKind.Utc);
        var payloadJson = JsonSerializer.Serialize(new
        {
            targetUserId = "shipper-1",
            requestId = "request-1",
            scheduledAtUtc = now.AddMinutes(30),
            channels = new[] { "Push" }
        });

        var payload = Command알림Payload.Parse(payloadJson);

        Assert.True(payload.IsScheduledForFuture(now));
    }

    [Fact]
    public void Parse_입금요청_필드와_수신번호_기본값을_읽는다()
    {
        var payloadJson = JsonSerializer.Serialize(new
        {
            알림유형 = Command알림FeatureNames.운송완료입금요청,
            shipperUserId = "shipper-1",
            driverId = "driver-1",
            requestId = "request-1",
            paymentId = "payment-1",
            orderId = "order-1",
            paymentFlow = "TossPayments.VirtualAccount",
            amount = 123000,
            reminderDay = 3,
            pickupContactPhone = "010-1111-2222",
            channels = new[] { "Push", "AlimTalk" }
        });

        var payload = Command알림Payload.Parse(payloadJson);

        Assert.Equal(Command알림FeatureNames.운송완료입금요청, payload.NotificationType);
        Assert.Equal("shipper-1", payload.TargetUserId);
        Assert.Equal("payment-1", payload.PaymentId);
        Assert.Equal("order-1", payload.OrderId);
        Assert.Equal("TossPayments.VirtualAccount", payload.PaymentFlow);
        Assert.Equal(123000, payload.Amount);
        Assert.Equal("123,000원", payload.AmountText);
        Assert.Equal(3, payload.ReminderDay);
        Assert.Equal("010-1111-2222", payload.RecipientPhone);
        Assert.Contains("AlimTalk", payload.Channels);
    }

    [Fact]
    public void Parse_공동수입원장관세사알림필드를보존한다()
    {
        const string json = """
            {
              "notificationType": "CommunityGroupImportRegistered",
              "targetUserId": "broker-1",
              "ledgerId": "ledger-1",
              "hsCodes": "2106.90,8543.70",
              "deepLink": "/customs/hs-codes?communityLedgerId=ledger-1",
              "title": "새 공동수입 원장이 등록되었습니다",
              "body": "검토 요청입니다.",
              "channels": ["Push"]
            }
            """;

        var payload = Command알림Payload.Parse(json);

        Assert.Equal(Command알림FeatureNames.공동수입원장등록, payload.NotificationType);
        Assert.Equal("broker-1", payload.TargetUserId);
        Assert.Equal("ledger-1", payload.LedgerId);
        Assert.Equal("2106.90,8543.70", payload.HsCodes);
        Assert.Equal("/customs/hs-codes?communityLedgerId=ledger-1", payload.DeepLink);
        Assert.Contains(Command알림FeatureNames.공동수입원장등록, Command알림FeatureNames.발송지원목록);
    }

    [Fact]
    public void Parse_기존관세사Payload의PascalCase대상도읽는다()
    {
        const string json = """
            {
              "TargetBrokerParticipantId": "broker-legacy",
              "알림유형": "HS코드검토요청"
            }
            """;

        var payload = Command알림Payload.Parse(json);

        Assert.Equal("broker-legacy", payload.TargetUserId);
        Assert.Equal("HS코드검토요청", payload.NotificationType);
    }

    [Fact]
    public void Parse_공동구매원장변경_관계자Push필드를_보존한다()
    {
        const string json = """
            {
              "notificationType": "CommunityGroupPurchaseLedgerChanged",
              "targetUserId": "member-1",
              "ledgerId": "group-purchase-ledger-1",
              "deepLink": "/community/group-purchase?ledgerId=group-purchase-ledger-1",
              "title": "공동구매 원장이 변경되었습니다",
              "body": "구매 조건이 변경되었습니다.",
              "channels": ["Push"]
            }
            """;

        var payload = Command알림Payload.Parse(json);

        Assert.Equal(Command알림FeatureNames.공동구매원장변경, payload.NotificationType);
        Assert.Equal("member-1", payload.TargetUserId);
        Assert.Equal("group-purchase-ledger-1", payload.LedgerId);
        Assert.Equal("/community/group-purchase?ledgerId=group-purchase-ledger-1", payload.DeepLink);
        Assert.Contains("Push", payload.Channels);
        Assert.Contains(Command알림FeatureNames.공동구매원장변경, Command알림FeatureNames.발송지원목록);
    }
}
