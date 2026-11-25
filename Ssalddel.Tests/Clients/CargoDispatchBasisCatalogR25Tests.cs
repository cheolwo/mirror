using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Ssalddel.Contracts.Common.Dispatch;

namespace Ssalddel.Tests.Clients;

/// <summary>공통 설명 판본과 JSON 호환 계약입니다. 개인 근거 조회·실제 메일 발송 시험이 아닙니다.</summary>
public sealed class CargoDispatchBasisCatalogR25Tests
{
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);
    // r1 문구·순서 수정으로 갱신하지 않습니다. 의미를 바꿀 때는 새 catalog 판본을 추가합니다.
    private const string R1Hash = "67A0E4EC58A33017840A80042DF3FB5A7DABA8D87DBC70D0F44880F97539C9C8";

    [Fact]
    public void 공통설명은접수다섯개와판정네개를고정한순서로제공한다()
    {
        var current = CargoDispatchBasisCatalog.Current;
        Assert.Equal("cargo-dispatch-basis.r1", current.Version);
        Assert.Equal(new[]
        {
            "intake.weight", "intake.mode", "intake.temperature", "intake.pickup-deadline", "intake.dropoff-deadline",
            "decision.plan", "decision.loading", "decision.pickup-order", "decision.dropoff-order"
        }, current.Definitions.Select(x => x.Code));
        Assert.Equal(new[]
        {
            "당시 접수 중량", "당시 운송 방식", "당시 온도 조건", "당시 상차 마감", "당시 하차 마감",
            "당시 수행계획", "당시 구간 적재", "이번 상차 순서", "이번 하차 순서"
        }, current.Definitions.Select(x => x.Label));
        Assert.All(current.Definitions.Take(5), x => Assert.Equal("intake", x.Kind));
        Assert.All(current.Definitions.Skip(5), x => Assert.Equal("decision", x.Kind));
        Assert.Equal(9, current.Definitions.Select(x => x.Code).Distinct(StringComparer.Ordinal).Count());
        Assert.All(current.Definitions, x => Assert.False(string.IsNullOrWhiteSpace(x.Explanation)));
    }

    [Fact]
    public void R1설명문구와순서는고정지문과기본JSON직렬화에함께결속된다()
    {
        var current = CargoDispatchBasisCatalog.Current;
        Console.WriteLine("R25CatalogHash=" + current.ContentHash);
        var canonical = JsonSerializer.Serialize(new { current.Version, current.Definitions });
        var expected = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
        Assert.Equal(R1Hash, current.ContentHash);
        Assert.Equal(expected, current.ContentHash);
        Assert.Matches("^[0-9A-F]{64}$", current.ContentHash);
        Assert.DoesNotContain("ContentHash", canonical);
    }

    [Fact]
    public void 판본이나문구나순서가다르면같은설명지문이되지않는다()
    {
        var current = CargoDispatchBasisCatalog.Current;
        Assert.NotEqual(current.ContentHash, Hash(new { Version = "cargo-dispatch-basis.r2", current.Definitions }));
        Assert.NotEqual(current.ContentHash, Hash(new { current.Version, Definitions = current.Definitions.Reverse().ToArray() }));
        var changed = current.Definitions.Select((x, index) => index == 0 ? x with { Explanation = x.Explanation + " 변경" } : x).ToArray();
        Assert.NotEqual(current.ContentHash, Hash(new { current.Version, Definitions = changed }));
        Assert.Equal(R1Hash, CargoDispatchBasisCatalog.Current.ContentHash);
    }

    [Fact]
    public void 공개컬렉션과판본속성으로는기존설명을변경할수없다()
    {
        var current = CargoDispatchBasisCatalog.Current;
        var list = Assert.IsAssignableFrom<IList<CargoDispatchBasisDefinition>>(current.Definitions);
        Assert.True(list.IsReadOnly);
        var first = list[0]; var changed = first with { Label = "수정" };
        Assert.Throws<NotSupportedException>(() => list[0] = changed);
        Assert.Throws<NotSupportedException>(() => list.Add(changed));
        Assert.Throws<NotSupportedException>(() => list.Clear());
        Assert.Same(first, current.Definitions[0]);
        Assert.Equal(9, current.Definitions.Count); Assert.Equal(R1Hash, current.ContentHash);
        Assert.Empty(typeof(CargoDispatchBasisCatalogVersion).GetConstructors());
        Assert.All(typeof(CargoDispatchBasisCatalogVersion).GetProperties(), x => Assert.False(x.CanWrite));
    }

    [Fact]
    public void 등록한정확한판본은동일한불변설명을조회한다()
    {
        Assert.True(CargoDispatchBasisCatalog.TryGet("cargo-dispatch-basis.r1", out var saved));
        Assert.Same(CargoDispatchBasisCatalog.Current, saved);
        Assert.Equal(R1Hash, saved.ContentHash);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("cargo-dispatch-basis.r0")]
    [InlineData("cargo-dispatch-basis.r2")]
    [InlineData("CARGO-DISPATCH-BASIS.R1")]
    [InlineData(" cargo-dispatch-basis.r1")]
    [InlineData("cargo-dispatch-basis.r1 ")]
    public void 알수없는판본을현재설명으로보충하지않는다(string? version)
    {
        Assert.False(CargoDispatchBasisCatalog.TryGet(version!, out var catalog));
        Assert.Null(catalog);
    }

    [Fact]
    public void 새연결정보가없는JSON은공통판본미연결로남는다()
    {
        var basis = JsonSerializer.Deserialize<CargoDispatchBasisEvidence>("{}", WebJson)!;
        Assert.Equal("legacy-unlinked", basis.Status);
        Assert.Null(basis.CatalogVersion); Assert.Null(basis.CatalogContentHash);
        Assert.Equal(0, basis.RecordedFactCount); Assert.Equal(0, basis.IntakeFactCount);
        Assert.Equal(0, basis.DecisionFactCount); Assert.Equal(0, basis.MissingFactCount);
        Assert.Empty(basis.Entries);
    }

    [Fact]
    public void 미확인판정과누락은JSON왕복뒤에도서로다르다()
    {
        var catalog = CargoDispatchBasisCatalog.Current;
        var basis = new CargoDispatchBasisEvidence
        {
            Status = "partial", CatalogVersion = catalog.Version, CatalogContentHash = catalog.ContentHash,
            RecordedFactCount = 1, IntakeFactCount = 0, DecisionFactCount = 1, MissingFactCount = 8,
            Entries = catalog.Definitions.Select(x => new CargoDispatchBasisEntry(x.Code, x.Label, x.Kind, x.Explanation,
                x.Code == "decision.plan" ? "not-confirmed" : "missing", x.Code == "decision.plan" ? "가능 미확인" : null)).ToArray()
        };
        var json = JsonSerializer.Serialize(basis, WebJson);
        var restored = JsonSerializer.Deserialize<CargoDispatchBasisEvidence>(json, WebJson)!;
        Assert.Equal("partial", restored.Status); Assert.Equal(R1Hash, restored.CatalogContentHash);
        Assert.Equal(1, restored.RecordedFactCount); Assert.Equal(8, restored.MissingFactCount);
        var negative = Assert.Single(restored.Entries.Where(x => x.State == "not-confirmed"));
        Assert.Equal("decision.plan", negative.Code); Assert.Equal("가능 미확인", negative.Value);
        Assert.All(restored.Entries.Where(x => x.State == "missing"), x => Assert.Null(x.Value));
        Assert.Equal(basis.Entries, restored.Entries);
    }

    [Fact]
    public void 준비자료의기본값은발송이나수신동의를의미하지않는다()
    {
        var context = JsonSerializer.Deserialize<CargoDispatchNoticeContext>("{}", WebJson)!;
        Assert.Equal("cargo-dispatch-notice.r1", context.TemplateVersion);
        Assert.Equal("preparation-only", context.Purpose); Assert.Empty(context.Lines);
        Assert.Equal("legacy-unlinked", context.Basis.Status);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(context, WebJson));
        Assert.False(json.RootElement.TryGetProperty("email", out _));
        Assert.False(json.RootElement.TryGetProperty("recipient", out _));
        Assert.False(json.RootElement.TryGetProperty("sentAtUtc", out _));
    }

    [Fact]
    public void 안내준비JSON은사건식별자와표현판본과공통판본을그대로왕복한다()
    {
        var context = new CargoDispatchNoticeContext
        {
            EvidenceId = "123", RequestId = "request-a", RecommendationRound = 3,
            OccurredAtUtc = new(2026, 10, 28, 1, 2, 3, DateTimeKind.Utc), MethodLabel = "화물 배차 엔진 추천 저장",
            Subject = "저장된 배차 설명", Lines = ["당시 접수 조건을 확인합니다.", "발송하지 않은 준비 자료입니다."],
            Basis = new() { Status = "partial", CatalogVersion = "cargo-dispatch-basis.r1", CatalogContentHash = R1Hash }
        };
        var restored = JsonSerializer.Deserialize<CargoDispatchNoticeContext>(JsonSerializer.Serialize(context, WebJson), WebJson)!;
        Assert.Equal(context.EvidenceId, restored.EvidenceId); Assert.Equal(context.RequestId, restored.RequestId);
        Assert.Equal(context.OccurredAtUtc, restored.OccurredAtUtc); Assert.Equal(context.RecommendationRound, restored.RecommendationRound);
        Assert.Equal(context.MethodLabel, restored.MethodLabel); Assert.Equal(context.Subject, restored.Subject);
        Assert.Equal(context.Lines, restored.Lines); Assert.Equal("preparation-only", restored.Purpose);
        Assert.Equal("cargo-dispatch-notice.r1", restored.TemplateVersion); Assert.Equal(R1Hash, restored.Basis.CatalogContentHash);
    }

    [Fact]
    public void 기존설명JSON과두항목Fact생성및분해는공통판본추가와독립적으로유지된다()
    {
        const string oldJson = """{"status":"recorded","items":[{"evidenceId":"123","requestId":"request-a","facts":[{"label":"당시 접수 중량","value":"100kg"}]}]}""";
        var restored = JsonSerializer.Deserialize<CargoDispatchExplanationResponse>(oldJson, WebJson)!;
        Assert.Equal("recorded", restored.Status); Assert.Equal("123", Assert.Single(restored.Items).EvidenceId);
        var fact = Assert.Single(restored.Items[0].Facts);
        var (label, value) = fact;
        Assert.Equal("당시 접수 중량", label); Assert.Equal("100kg", value);
        Assert.Equal(fact, new CargoDispatchExplanationFact(label, value));
    }

    [Fact]
    public void 구형소비자는추가된공통근거필드를무시하고기존사건과사실을읽는다()
    {
        var wire = JsonSerializer.Serialize(new
        {
            EvidenceId = "123", Facts = new[] { new CargoDispatchExplanationFact("당시 접수 중량", "100kg") },
            Basis = new CargoDispatchBasisEvidence { CatalogVersion = "cargo-dispatch-basis.r1", CatalogContentHash = R1Hash }
        }, WebJson);
        var legacy = JsonSerializer.Deserialize<LegacyItem>(wire, WebJson)!;
        Assert.Equal("123", legacy.EvidenceId); Assert.Equal("100kg", Assert.Single(legacy.Facts).Value);
    }

    private static string Hash<T>(T value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value))));
    private sealed class LegacyItem
    {
        public string EvidenceId { get; set; } = string.Empty;
        public IReadOnlyList<CargoDispatchExplanationFact> Facts { get; set; } = [];
    }
}
