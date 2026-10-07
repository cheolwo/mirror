using System.Text.Json.Nodes;
using Ssalddel.Application.Driver.Transport;
using 살뜰.도메인.운송;

namespace Ssalddel.Tests.Application.Driver.Transport;

public class 운송증빙첨부JsonWriterTests
{
    [Theory]
    [InlineData("pickup-complete-photo")]
    [InlineData("dropoff-complete-photo")]
    public void 같은완료사진재전송은_최초시각과주소및메타데이터를그대로유지한다(string kind)
    {
        var writer = new 운송증빙첨부JsonWriter();
        var transport = new 운송원장();
        var firstTime = new DateTime(2026, 10, 4, 1, 2, 3, DateTimeKind.Utc);
        writer.추가(transport, new(kind, "proof/photo.jpg", "https://test.invalid/original",
            "driver-1", firstTime, new Dictionary<string, object?> { ["receiptConfirmed"] = true }));
        var firstJson = transport.첨부_json;

        writer.추가(transport, new(kind, " proof/photo.jpg ", "https://test.invalid/retry",
            "driver-1", firstTime.AddMinutes(1), new Dictionary<string, object?> { ["receiptConfirmed"] = false }));

        Assert.Equal(firstJson, transport.첨부_json);
        var item = Assert.Single(JsonNode.Parse(transport.첨부_json)!.AsArray())!.AsObject();
        Assert.Equal(firstTime, item["recordedAtUtc"]!.GetValue<DateTime>());
        Assert.Equal("https://test.invalid/original", item["url"]!.GetValue<string>());
        Assert.True(item["receiptConfirmed"]!.GetValue<bool>());
    }

    [Fact]
    public void 다른사진이나상하차단계는_각각별도증빙으로저장한다()
    {
        var writer = new 운송증빙첨부JsonWriter();
        var transport = new 운송원장();
        var time = DateTime.UtcNow;
        writer.추가(transport, new("pickup-complete-photo", "proof/one.jpg", null,
            "driver-1", time, new Dictionary<string, object?>()));
        writer.추가(transport, new("pickup-complete-photo", "proof/two.jpg", null,
            "driver-1", time, new Dictionary<string, object?>()));
        writer.추가(transport, new("dropoff-complete-photo", "proof/one.jpg", null,
            "driver-1", time, new Dictionary<string, object?>()));

        Assert.Equal(3, JsonNode.Parse(transport.첨부_json)!.AsArray().Count);
    }

    [Fact]
    public void 동일사진을사용한서로다른예외신고를_완료사진재시도로오인하지않는다()
    {
        var writer = new 운송증빙첨부JsonWriter();
        var transport = new 운송원장();
        foreach (var exception in new[] { "수량불일치", "파손" })
            writer.추가(transport, new("transport-field-exception", "proof/one.jpg", null,
                "driver-1", DateTime.UtcNow, new Dictionary<string, object?> { ["exceptionCode"] = exception }));

        var items = JsonNode.Parse(transport.첨부_json)!.AsArray();
        Assert.Equal(2, items.Count);
        Assert.Equal("수량불일치", items[0]!["exceptionCode"]!.GetValue<string>());
        Assert.Equal("파손", items[1]!["exceptionCode"]!.GetValue<string>());
    }

    [Fact]
    public void 추가_운송예외_메타데이터를_첨부Json에_보존한다()
    {
        var writer = new 운송증빙첨부JsonWriter();
        var 운송 = new 운송원장 { 첨부_json = "[]" };

        writer.추가(
            운송,
            new 운송증빙첨부(
                "transport-field-exception",
                "proof/pickup-missing.jpg",
                "https://example.test/proof/pickup-missing.jpg",
                "driver-1",
                new DateTime(2026, 7, 9, 1, 2, 3, DateTimeKind.Utc),
                new Dictionary<string, object?>
                {
                    ["stage"] = "상차",
                    ["exceptionCode"] = "상차물건없음",
                    ["adminReviewRequired"] = true,
                    ["nextAction"] = "관리자 확인을 기다려 주세요."
                }));

        var attachments = JsonNode.Parse(운송.첨부_json)!.AsArray();
        var item = attachments[0]!.AsObject();

        Assert.Equal("transport-field-exception", item["kind"]!.GetValue<string>());
        Assert.Equal("상차", item["stage"]!.GetValue<string>());
        Assert.Equal("상차물건없음", item["exceptionCode"]!.GetValue<string>());
        Assert.True(item["adminReviewRequired"]!.GetValue<bool>());
    }
}
