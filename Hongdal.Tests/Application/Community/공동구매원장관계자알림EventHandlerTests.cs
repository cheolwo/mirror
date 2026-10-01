using System.Text.Json;
using Hongdal.Application.Community.Events;
using Hongdal.Application.Community.Handlers;
using Hongdal.Contracts.Common.Community;
using Hongdal.Services.Community;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using 홍달.Services.Notifications;

namespace Hongdal.Tests.Application.Community;

public sealed class 공동구매원장관계자알림EventHandlerTests
{
    [Fact]
    public async Task 일반초안생성은_알리지않고_가원장생성과_후속변경만_관계자알림을_적재한다()
    {
        var service = new RecordingNotificationService();
        using var services = new ServiceCollection()
            .AddSingleton<I공동구매원장관계자알림Service>(service)
            .BuildServiceProvider();
        var handler = new 공동구매원장관계자알림EventHandler(
            services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<공동구매원장관계자알림EventHandler>.Instance);

        await handler.Handle(CreateEvent(CommunityLedgerTemplateKeys.GroupPurchase, revision: 1, "저장"), default);
        await handler.Handle(CreateEvent(
            CommunityLedgerTemplateKeys.GroupPurchase,
            revision: 1,
            "저장",
            provisional: true), default);
        await handler.Handle(CreateEvent(CommunityLedgerTemplateKeys.GroupPurchase, revision: 2, "저장"), default);
        await handler.Handle(CreateEvent(CommunityLedgerTemplateKeys.GroupPurchase, revision: 3, "상태변경"), default);
        await handler.Handle(CreateEvent(CommunityLedgerTemplateKeys.GroupImport, revision: 2, "저장"), default);

        Assert.Equal(3, service.Events.Count);
        Assert.Equal(["저장", "저장", "상태변경"], service.Events.Select(item => item.ChangeType));
    }

    [Fact]
    public void 알림대상은_생성자와_참여자를_합치고_변경자와_중복을_제외한다()
    {
        var ledger = CreateLedger(CommunityLedgerTemplateKeys.GroupPurchase, revision: 2);
        ledger.생성자UserId = "owner";
        ledger.참여자목록 =
        [
            Participant("owner"),
            Participant("actor"),
            Participant("member-1"),
            Participant("MEMBER-1"),
            Participant(null)
        ];

        var recipients = 공동구매원장관계자알림Policy.ResolveRecipientUserIds(ledger, "actor");

        Assert.Equal(["owner", "member-1"], recipients);
    }

    [Fact]
    public void 알림Payload는_대상과_변경요약과_원장링크를_포함한다()
    {
        var ledger = CreateLedger(CommunityLedgerTemplateKeys.GroupPurchase, revision: 4);
        ledger.제목 = "아파트 감자 공동구매";
        ledger.상태 = 커뮤니티원장상태.진행중;
        ledger.현재단계Key = "구매 조건 합의";

        var json = 공동구매원장관계자알림Policy.BuildPayload(ledger, "member-1", "상태변경");
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Equal(Command알림FeatureNames.공동구매원장변경, root.GetProperty("notificationType").GetString());
        Assert.Equal("member-1", root.GetProperty("targetUserId").GetString());
        Assert.Equal("group-purchase-ledger-1", root.GetProperty("ledgerId").GetString());
        Assert.Equal(4, root.GetProperty("ledgerRevision").GetInt64());
        Assert.Contains("구매 조건 합의", root.GetProperty("body").GetString(), StringComparison.Ordinal);
        Assert.Equal(
            "/community/group-purchase?ledgerId=group-purchase-ledger-1",
            root.GetProperty("deepLink").GetString());
        Assert.Equal("Push", root.GetProperty("channels")[0].GetString());
    }

    [Fact]
    public void 가원장생성알림은_실행확정이아님을_명시한다()
    {
        var ledger = CreateLedger(CommunityLedgerTemplateKeys.GroupPurchase, revision: 1, provisional: true);

        var json = 공동구매원장관계자알림Policy.BuildPayload(ledger, "member-1", "저장");
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Contains("가원장", root.GetProperty("title").GetString(), StringComparison.Ordinal);
        Assert.Contains("비구속적", root.GetProperty("body").GetString(), StringComparison.Ordinal);
        Assert.Contains("운송 주선은 확정되지 않았습니다", root.GetProperty("body").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public void 가원장생성알림은_빈역할슬롯과_참여의향경계를_한번안내한다()
    {
        var ledger = CreateLedger(CommunityLedgerTemplateKeys.GroupPurchase, revision: 1, provisional: true);
        ledger.외부참조 = new Dictionary<string, string>
        {
            ["SourceCommunityPostId"] = "42"
        };

        var json = 공동구매원장관계자알림Policy.BuildPayload(ledger, "member-1", "저장");
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.True(root.GetProperty("roleInterestInvitation").GetBoolean());
        Assert.True(root.GetProperty("openRoleSlotCount").GetInt32() > 0);
        Assert.Contains(
            root.GetProperty("openRoleSlots").EnumerateArray(),
            slot => slot.GetProperty("roleCode").GetString() == CommunityPostPartyRoleCodes.Buyer);
        Assert.Contains("비어 있는 역할", root.GetProperty("body").GetString(), StringComparison.Ordinal);
        Assert.Contains("비구속적 참여 의향", root.GetProperty("body").GetString(), StringComparison.Ordinal);
        Assert.Contains("배정되거나 확정되지는 않", root.GetProperty("body").GetString(), StringComparison.Ordinal);
        Assert.Equal("/community/posts/42", root.GetProperty("deepLink").GetString());
        Assert.True(root.GetProperty("requiresExplicitRoleAcceptance").GetBoolean());
        Assert.True(root.GetProperty("platformDoesNotAssignWork").GetBoolean());
    }

    [Fact]
    public void 후속원장변경알림은_같은빈역할초대를_반복하지않는다()
    {
        var ledger = CreateLedger(CommunityLedgerTemplateKeys.GroupPurchase, revision: 2, provisional: true);
        ledger.외부참조 = new Dictionary<string, string>
        {
            ["SourceCommunityPostId"] = "42"
        };

        var json = 공동구매원장관계자알림Policy.BuildPayload(ledger, "member-1", "저장");
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.False(root.GetProperty("roleInterestInvitation").GetBoolean());
        Assert.Equal(0, root.GetProperty("openRoleSlotCount").GetInt32());
        Assert.Empty(root.GetProperty("openRoleSlots").EnumerateArray());
        Assert.Equal(
            "/community/group-purchase?ledgerId=group-purchase-ledger-1",
            root.GetProperty("deepLink").GetString());
    }

    [Fact]
    public void 역할참여알림은_역할과_외부면허확인경계를_명시한다()
    {
        var ledger = CreateLedger(CommunityLedgerTemplateKeys.GroupPurchase, revision: 2);
        ledger.확장속성 = new Dictionary<string, string>
        {
            [CommunityPostProvisionalLedgerPolicy.LastPartyRoleJoinRevisionAttributeKey] = "2",
            [CommunityPostProvisionalLedgerPolicy.LastPartyRoleJoinedDisplayNameAttributeKey] = "해상 물류 담당자",
            [CommunityPostProvisionalLedgerPolicy.LastPartyRoleJoinedRoleCodeAttributeKey] =
                CommunityPostPartyRoleCodes.OceanFreightForwarder
        };

        var json = 공동구매원장관계자알림Policy.BuildPayload(ledger, "member-1", "저장");
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Contains("새 역할", root.GetProperty("title").GetString(), StringComparison.Ordinal);
        Assert.Contains("해상 운송 주선업자", root.GetProperty("body").GetString(), StringComparison.Ordinal);
        Assert.Contains("외부 면허·등록 확인", root.GetProperty("body").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public void 거래당사자역할수락알림은_계약과_최종책임이아님을_명시한다()
    {
        var ledger = CreateLedger(CommunityLedgerTemplateKeys.GroupPurchase, revision: 3);
        ledger.확장속성 = new Dictionary<string, string>
        {
            [CommunityPostProvisionalLedgerPolicy.LastPartyRoleJoinRevisionAttributeKey] = "3",
            [CommunityPostProvisionalLedgerPolicy.LastPartyRoleJoinedDisplayNameAttributeKey] = "구매 검토자",
            [CommunityPostProvisionalLedgerPolicy.LastPartyRoleJoinedRoleCodeAttributeKey] =
                CommunityPostPartyRoleCodes.Buyer
        };

        var json = 공동구매원장관계자알림Policy.BuildPayload(ledger, "member-1", "저장");
        using var document = JsonDocument.Parse(json);
        var body = document.RootElement.GetProperty("body").GetString();

        Assert.Contains("구매자 역할을 비구속적으로 수락", body, StringComparison.Ordinal);
        Assert.Contains("주문·계약·결제", body, StringComparison.Ordinal);
        Assert.Contains("별도 합의 전까지 확정되지 않습니다", body, StringComparison.Ordinal);
    }

    [Fact]
    public void 동일이벤트는_항상_같은_중복방지키를_만든다()
    {
        var first = 공동구매원장관계자알림Policy.BuildTraceId("event-1", "fallback-1");
        var second = 공동구매원장관계자알림Policy.BuildTraceId("event-1", "fallback-2");

        Assert.Equal(first, second);
        Assert.Equal(64, first.Length);
    }

    private static 커뮤니티원장변경됨Event CreateEvent(
        string templateKey,
        long revision,
        string changeType,
        bool provisional = false)
        => new(
            CreateLedger(templateKey, revision, provisional),
            changeType,
            "actor",
            null,
            new DateTime(2026, 7, 15, 1, 2, 3, DateTimeKind.Utc),
            $"event-{revision}-{changeType}");

    private static 커뮤니티원장Dto CreateLedger(string templateKey, long revision, bool provisional = false)
        => new()
        {
            원장Id = "group-purchase-ledger-1",
            Revision = revision,
            커뮤니티Id = "platform",
            원장템플릿Key = templateKey,
            제목 = "감자 공동구매",
            상태 = 커뮤니티원장상태.초안,
            생성자UserId = "owner",
            참여자목록 = [Participant("member-1"), Participant("actor")],
            확장속성 = provisional
                ? new Dictionary<string, string>
                {
                    [CommunityPostProvisionalLedgerPolicy.LedgerMaturityAttributeKey] = CommunityPostProvisionalLedgerPolicy.LedgerMaturityCode,
                    [CommunityPostProvisionalLedgerPolicy.BindingEffectAttributeKey] = CommunityPostProvisionalLedgerPolicy.NonBindingEffectCode,
                    [CommunityPostProvisionalLedgerPolicy.ParticipantNotificationsAttributeKey] = bool.TrueString
                }
                : new Dictionary<string, string>()
        };

    private static 커뮤니티원장참여자Dto Participant(string? userId)
        => new() { UserId = userId, DisplayName = userId ?? "익명" };

    private sealed class RecordingNotificationService : I공동구매원장관계자알림Service
    {
        public List<(string ChangeType, string Actor)> Events { get; } = [];

        public Task<int> 변경알림적재Async(
            커뮤니티원장Dto 원장,
            string 변경유형,
            string 변경자UserId,
            string eventId,
            DateTime occurredAtUtc,
            CancellationToken cancellationToken = default)
        {
            Events.Add((변경유형, 변경자UserId));
            return Task.FromResult(1);
        }
    }
}
