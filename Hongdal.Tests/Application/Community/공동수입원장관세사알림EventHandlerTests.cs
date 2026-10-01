using System.Text.Json;
using Hongdal.Application.Community.Events;
using Hongdal.Application.Community.Handlers;
using Hongdal.Contracts.Common.Community;
using Hongdal.Services.Community;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Hongdal.Tests.Application.Community;

public sealed class 공동수입원장관세사알림EventHandlerTests
{
    [Fact]
    public async Task 최초등록된공동수입원장만_관세사알림을적재한다()
    {
        var service = new RecordingNotificationService();
        using var services = new ServiceCollection()
            .AddSingleton<I공동수입원장관세사알림Service>(service)
            .BuildServiceProvider();
        var handler = new 공동수입원장관세사알림EventHandler(
            services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<공동수입원장관세사알림EventHandler>.Instance);

        await handler.Handle(CreateEvent(CommunityLedgerTemplateKeys.GroupImport, revision: 1, "저장"), CancellationToken.None);
        await handler.Handle(CreateEvent(CommunityLedgerTemplateKeys.GroupImport, revision: 2, "저장"), CancellationToken.None);
        await handler.Handle(CreateEvent(CommunityLedgerTemplateKeys.GroupPurchase, revision: 1, "저장"), CancellationToken.None);
        await handler.Handle(CreateEvent(CommunityLedgerTemplateKeys.GroupImport, revision: 1, "상태변경"), CancellationToken.None);

        var queued = Assert.Single(service.Ledgers);
        Assert.Equal("import-ledger-1", queued.원장Id);
        Assert.Equal("event-1", service.EventIds.Single());
    }

    [Fact]
    public void Hs코드추출_확장속성과블록을합치고중복을제거한다()
    {
        var ledger = CreateLedger(CommunityLedgerTemplateKeys.GroupImport, revision: 1);
        ledger.확장속성 = new Dictionary<string, string>
        {
            ["groupImport.hsCodes"] = "[\"2106.90\",\"8543.70\"]"
        };
        ledger.블록목록 =
        [
            new 커뮤니티원장블록Dto
            {
                Data = new Dictionary<string, string>
                {
                    ["HS코드"] = "2106.90 | 9401.69",
                    ["수량"] = "1000"
                }
            }
        ];

        var codes = 공동수입원장관세사알림Policy.ExtractHsCodes(ledger);

        Assert.Equal(["2106.90", "8543.70", "9401.69"], codes);
    }

    [Fact]
    public void 알림Payload_관세사대상과원장Hs코드링크만포함한다()
    {
        var ledger = CreateLedger(CommunityLedgerTemplateKeys.GroupImport, revision: 1);
        ledger.제목 = "홍길동님의 비공개 공동수입";
        ledger.생성자표시명 = "홍길동";

        var json = 공동수입원장관세사알림Policy.BuildPayload(
            ledger,
            "broker-user-1",
            ["2106.90", "8543.70"]);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Equal("broker-user-1", root.GetProperty("targetUserId").GetString());
        Assert.Equal("import-ledger-1", root.GetProperty("ledgerId").GetString());
        Assert.Equal("2106.90,8543.70", root.GetProperty("hsCodes").GetString());
        Assert.Contains("communityLedgerId=import-ledger-1", root.GetProperty("deepLink").GetString(), StringComparison.Ordinal);
        Assert.DoesNotContain("홍길동", json, StringComparison.Ordinal);
    }

    [Fact]
    public void TraceId_같은원장이벤트는항상같은64자리중복방지키를만든다()
    {
        var first = 공동수입원장관세사알림Policy.BuildTraceId("event-1", "ledger-1:1");
        var second = 공동수입원장관세사알림Policy.BuildTraceId("event-1", "ignored");
        var fallback = 공동수입원장관세사알림Policy.BuildTraceId(null, "ledger-1:1");

        Assert.Equal(first, second);
        Assert.Equal(64, first.Length);
        Assert.NotEqual(first, fallback);
    }

    private static 커뮤니티원장변경됨Event CreateEvent(string templateKey, long revision, string changeType)
        => new(
            CreateLedger(templateKey, revision),
            changeType,
            "user-1",
            null,
            new DateTime(2026, 7, 15, 1, 2, 3, DateTimeKind.Utc),
            "event-1");

    private static 커뮤니티원장Dto CreateLedger(string templateKey, long revision)
        => new()
        {
            원장Id = "import-ledger-1",
            Revision = revision,
            커뮤니티Id = "platform",
            원장템플릿Key = templateKey,
            제목 = "공동수입 검토",
            상태 = 커뮤니티원장상태.초안
        };

    private sealed class RecordingNotificationService : I공동수입원장관세사알림Service
    {
        public List<커뮤니티원장Dto> Ledgers { get; } = [];
        public List<string> EventIds { get; } = [];

        public Task<int> 등록알림적재Async(
            커뮤니티원장Dto 원장,
            string eventId,
            DateTime occurredAtUtc,
            CancellationToken cancellationToken = default)
        {
            Ledgers.Add(원장);
            EventIds.Add(eventId);
            return Task.FromResult(1);
        }
    }
}
