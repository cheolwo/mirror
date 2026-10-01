using System.Globalization;
using System.Text.Json.Nodes;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Unity.Data.WorldProjection;

namespace Ssalddel.Unity.Tests;

[SsalddelEvidenceResponsibility(
    SsalddelEvidenceStage.E3,
    "방문 JSON의 명시 매핑·필수 존재·권위 차단·문법·입력 상한과 실패 시 개인정보 비노출을 검증한다.",
    Boundary = "실제 방문·위치 근거, DB/API 전송, Unity 컴파일·Play Mode·Game View 증거가 아니다.",
    SubmoduleKey = SsalddelEvidenceSubmoduleKeys.E3계약회귀)]
public sealed class 방문기록JsonDecoderTests
{
    private readonly 방문기록JsonDecoder decoder = new();

    [Fact]
    public void 기존CamelCaseV2의한글과모든방문필드를새사본으로읽는다()
    {
        var 원문 = 기록();
        var 사본 = decoder.Decode(원문);
        Assert.Equal("delivery-visit-record.v2", 사본.SchemaVersion);
        Assert.Equal("record:review", 사본.RecordId);
        Assert.True(사본.LocalReviewOnly);
        Assert.False(사본.IsOperationalState);
        var 방문 = Assert.Single(사본.Visits);
        Assert.Equal("visit:1", 방문.VisitId);
        Assert.Equal("store:1", 방문.StoreId);
        Assert.Equal("검토용 가게 🍚", 방문.Name);
        Assert.Equal("메뉴\n참고", 방문.Menu);
        Assert.Equal(1, 방문.Sequence);
        Assert.Equal(37.5801, 방문.Latitude);
        Assert.Equal(127.0862, 방문.Longitude);
        Assert.Equal("검토용 주소", 방문.Address);
        Assert.Equal("Candidate", 방문.AddressStatus);
        Assert.Equal("Unverified", 방문.CoordinateStatus);
        Assert.Equal("Recorded", 방문.MenuStatus);
        Assert.Equal("추가 확인 필요", 방문.VerificationNote);
        Assert.Equal(new[] { "https://example.invalid/review/1" }, 방문.SourceUrls);
        Assert.Equal("검토 구역", 방문.RegionHint);
        Assert.Null(방문.AdministrativeAreaStableId);
        Assert.Equal("station:sample", 방문.StationModuleStableId);
        Assert.Equal("Unresolved", 방문.AreaBindingStatus);
        Assert.Equal("review:pending", 방문.AreaBindingEvidence);
        Assert.Equal("2026-09-26T09:00:00+09:00", 방문.RecordedAt);
        Assert.Equal("RecordedVisit", 방문.EventKind);
        Assert.False(방문.PickupCompletionConfirmed);
        방문.SourceUrls[0] = "changed";
        방문.Name = "changed";
        var 다시읽기 = decoder.Decode(원문).Visits[0];
        Assert.Equal("검토용 가게 🍚", 다시읽기.Name);
        Assert.Equal("https://example.invalid/review/1", 다시읽기.SourceUrls[0]);
    }

    [Fact]
    public void 지역과시각의명시Null을승인이나현재시각으로채우지않는다()
    {
        var 입력 = JsonNode.Parse(기록())!;
        var 방문 = 입력["visits"]![0]!;
        foreach (var 이름 in new[] { "administrativeAreaStableId", "stationModuleStableId", "areaBindingEvidence", "recordedAt" })
            방문[이름] = null;
        var 결과 = Assert.Single(decoder.Decode(입력.ToJsonString()).Visits);
        Assert.Null(결과.AdministrativeAreaStableId);
        Assert.Null(결과.StationModuleStableId);
        Assert.Null(결과.AreaBindingEvidence);
        Assert.Null(결과.RecordedAt);
    }

    [Theory]
    [InlineData("schemaVersion")]
    [InlineData("recordId")]
    [InlineData("localReviewOnly")]
    [InlineData("isOperationalState")]
    [InlineData("visits")]
    public void 최상위필수값누락과Null을거절한다(string 필드)
    {
        필수값검사(필드, false);
    }

    [Theory]
    [InlineData("visitId")]
    [InlineData("storeId")]
    [InlineData("sequence")]
    [InlineData("latitude")]
    [InlineData("longitude")]
    [InlineData("eventKind")]
    [InlineData("pickupCompletionConfirmed")]
    public void 방문필수값누락과Null을기본값으로채우지않는다(string 필드)
    {
        필수값검사(필드, true);
    }

    [Theory]
    [InlineData("schemaVersion", "\"delivery-visit-record.v1\"", false)]
    [InlineData("recordId", "\" \"", false)]
    [InlineData("localReviewOnly", "false", false)]
    [InlineData("isOperationalState", "true", false)]
    [InlineData("localReviewOnly", "\"true\"", false)]
    [InlineData("isOperationalState", "0", false)]
    [InlineData("visits", "{}", false)]
    [InlineData("eventKind", "\"PickupCompleted\"", true)]
    [InlineData("pickupCompletionConfirmed", "true", true)]
    [InlineData("pickupCompletionConfirmed", "\"false\"", true)]
    [InlineData("visitId", "\"\"", true)]
    [InlineData("sequence", "\"1\"", true)]
    [InlineData("sequence", "1.5", true)]
    [InlineData("sequence", "2147483648", true)]
    [InlineData("latitude", "\"37.5\"", true)]
    [InlineData("latitude", "90.001", true)]
    [InlineData("longitude", "-180.001", true)]
    [InlineData("name", "{}", true)]
    [InlineData("name", "null", true)]
    [InlineData("sourceUrls", "[null]", true)]
    [InlineData("sourceUrls", "[1]", true)]
    [InlineData("sourceUrls", "\"https://example.invalid\"", true)]
    public void 권위변경과잘못된필드타입을거절한다(string 필드, string 값, bool 방문필드)
    {
        var 입력 = JsonNode.Parse(기록())!;
        var 대상 = 방문필드 ? 입력["visits"]![0]! : 입력;
        대상[필드] = JsonNode.Parse(값);
        Assert.Throws<ArgumentException>(() => decoder.Decode(입력.ToJsonString()));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \r\n\t")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("{\"x\":1,}")]
    [InlineData("{\"x\":[1,]}")]
    [InlineData("{\"x\":01}")]
    [InlineData("{\"x\":+1}")]
    [InlineData("{\"x\":.1}")]
    [InlineData("{\"x\":1.}")]
    [InlineData("{\"x\":1e}")]
    [InlineData("{\"x\":NaN}")]
    [InlineData("{\"x\":Infinity}")]
    [InlineData("{\"x\":true}{}")]
    [InlineData("{\"x\":true}false")]
    [InlineData("{\"x\":true} trailing")]
    [InlineData("{\"x\":/*comment*/1}")]
    [InlineData("{\"x\":\"\\q\"}")]
    [InlineData("{\"x\":\"\\uAC0\"}")]
    [InlineData("{\"x\":\"\\uD800\"}")]
    [InlineData("{\"x\":\"line\nfeed\"}")]
    public void 잘못된문서나느슨한Json확장을허용하지않는다(string? json)
    {
        Assert.Throws<ArgumentException>(() => decoder.Decode(json!));
    }

    [Theory]
    [InlineData("\"isOperationalState\":false", "\"isOperationalState\":false,\"isOperationalState\":true")]
    [InlineData("\"eventKind\":\"RecordedVisit\"", "\"eventKind\":\"RecordedVisit\",\"eventKind\":\"RecordedVisit\"")]
    [InlineData("\"isOperationalState\":false", "\"isOperationalState\":false,\"isOperational\\u0053tate\":false")]
    public void 순서와이스케이프에관계없이중복필드를거절한다(string 기존, string 중복)
    {
        Assert.Contains("VisitRecordDuplicateField", Assert.Throws<ArgumentException>(() =>
            decoder.Decode(기록().Replace(기존, 중복))).Message);
    }

    [Fact]
    public void 추가필드안의중복이나다형성메타데이터도거절한다()
    {
        foreach (var 추가 in new[] { "\"extra\":{\"x\":1,\"x\":2},", "\"__type\":\"injected\"," })
            Assert.Throws<ArgumentException>(() => decoder.Decode("{" + 추가 + 기록().Substring(1)));
    }

    [Fact]
    public void 방문Null과중복Id는거절하고같은매장재방문은보존한다()
    {
        Assert.Throws<ArgumentException>(() => decoder.Decode(기록("null")));
        Assert.Throws<ArgumentException>(() => decoder.Decode(기록(방문() + "," + 방문())));
        var 재방문 = decoder.Decode(기록(방문() + "," + 방문(2)));
        Assert.Equal(2, 재방문.Visits.Length);
        Assert.Equal(재방문.Visits[0].StoreId, 재방문.Visits[1].StoreId);
    }

    [Fact]
    public void 방문수를기존V2상한이하로제한한다()
    {
        var 상한방문 = string.Join(",", Enumerable.Range(1, 방문기록JsonDecoder.MaxVisits).Select(간단방문));
        Assert.Equal(방문기록JsonDecoder.MaxVisits, decoder.Decode(기록(상한방문)).Visits.Length);
        Assert.Contains("VisitRecordVisitLimitExceeded", Assert.Throws<ArgumentException>(() =>
            decoder.Decode(기록(상한방문 + "," + 간단방문(방문기록JsonDecoder.MaxVisits + 1)))).Message);
        Assert.Empty(decoder.Decode(기록(string.Empty)).Visits);
    }

    [Fact]
    public void 큰입력과지나친중첩을변환전에거절한다()
    {
        Assert.Contains("VisitRecordJsonTooLarge", Assert.Throws<ArgumentException>(() =>
            decoder.Decode(new string('a', 방문기록JsonDecoder.MaxInputUtf8Bytes + 1))).Message);
        var 다중바이트 = "{\"extra\":\"" + new string('가', 방문기록JsonDecoder.MaxInputUtf8Bytes / 3 + 1) + "\"}";
        Assert.Contains("VisitRecordJsonTooLarge", Assert.Throws<ArgumentException>(() => decoder.Decode(다중바이트)).Message);
        var 깊은배열 = new string('[', 방문기록JsonDecoder.MaxDepth + 1) + "0" + new string(']', 방문기록JsonDecoder.MaxDepth + 1);
        Assert.Contains("VisitRecordJsonDepthExceeded", Assert.Throws<ArgumentException>(() => decoder.Decode(깊은배열)).Message);
    }

    [Fact]
    public void 좌표수치는문화권과무관하고명시한영점도그대로보존한다()
    {
        var 원래문화 = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            Assert.Equal(37.5801, decoder.Decode(기록()).Visits[0].Latitude);
            var 영점 = 기록().Replace("37.5801", "0").Replace("127.0862", "0");
            Assert.Equal(0, decoder.Decode(영점).Visits[0].Latitude);
            Assert.Equal(0, decoder.Decode(영점).Visits[0].Longitude);
            Assert.Throws<ArgumentException>(() => decoder.Decode(기록().Replace("37.5801", "1e999")));
        }
        finally { CultureInfo.CurrentCulture = 원래문화; }
    }

    [Fact]
    public void 오류와내부예외에원문이나상호주소를싣지않는다()
    {
        const string 비공개 = "비공개상호-비공개주소-원문";
        var 오류 = Assert.Throws<ArgumentException>(() => decoder.Decode("{\"name\":\"" + 비공개 + "\",broken}"));
        Assert.DoesNotContain(비공개, 오류.ToString());
        Assert.Null(오류.InnerException);
        Assert.Equal("json", 오류.ParamName);
    }

    private void 필수값검사(string 필드, bool 방문필드)
    {
        var 입력 = JsonNode.Parse(기록())!;
        var 대상 = (JsonObject)(방문필드 ? 입력["visits"]![0]! : 입력);
        대상.Remove(필드);
        Assert.Throws<ArgumentException>(() => decoder.Decode(입력.ToJsonString()));
        대상[필드] = null;
        Assert.Throws<ArgumentException>(() => decoder.Decode(입력.ToJsonString()));
    }

    private static string 기록(string? 방문들 = null) =>
        "{\"schemaVersion\":\"delivery-visit-record.v2\",\"recordId\":\"record:review\"," +
        "\"localReviewOnly\":true,\"isOperationalState\":false,\"visits\":[" + (방문들 ?? 방문()) + "]}";

    private static string 간단방문(int 번호) =>
        "{\"visitId\":\"visit:" + 번호 + "\",\"storeId\":\"store:1\",\"sequence\":" + 번호 +
        ",\"latitude\":37.5801,\"longitude\":127.0862,\"eventKind\":\"RecordedVisit\",\"pickupCompletionConfirmed\":false}";

    private static string 방문(int 번호 = 1) => 간단방문(번호).TrimEnd('}') + "," +
        "\"name\":\"\\uAC80토용 가게 \\uD83C\\uDF5A\",\"menu\":\"메뉴\\n참고\",\"address\":\"검토용 주소\"," +
        "\"addressStatus\":\"Candidate\",\"coordinateStatus\":\"Unverified\",\"menuStatus\":\"Recorded\"," +
        "\"verificationNote\":\"추가 확인 필요\",\"sourceUrls\":[\"https:\\/\\/example.invalid/review/1\"]," +
        "\"regionHint\":\"검토 구역\",\"administrativeAreaStableId\":null,\"stationModuleStableId\":\"station:sample\"," +
        "\"areaBindingStatus\":\"Unresolved\",\"areaBindingEvidence\":\"review:pending\",\"recordedAt\":\"2026-09-26T09:00:00+09:00\"}";
}
