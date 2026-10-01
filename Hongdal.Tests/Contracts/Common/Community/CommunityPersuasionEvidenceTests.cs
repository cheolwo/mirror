using Hongdal.Contracts.Common.Community;

namespace Hongdal.Tests.Contracts.Common.Community;

public sealed class CommunityPersuasionEvidenceTests
{
    [Fact]
    public void EvidenceChart_RoundTripsThroughReadableBodyBlock()
    {
        var block = ValidBlock();

        var encoded = CommunityEvidenceChartTextCodec.Encode(block);
        var decoded = Assert.Single(CommunityEvidenceChartTextCodec.DecodeAll(encoded));

        Assert.Contains(CommunityEvidenceChartTextCodec.StartMarker, encoded, StringComparison.Ordinal);
        Assert.Contains("출처: KAMIS 농산물 유통정보", encoded, StringComparison.Ordinal);
        Assert.Equal(block.Title, decoded.Title);
        Assert.Equal(block.Claim, decoded.Claim);
        Assert.Equal(block.SourceUrl, decoded.SourceUrl);
        Assert.Equal(block.Points, decoded.Points);
    }

    [Fact]
    public void EvidenceBlock_IsRemovedFromTranslationAndTextPreviewWithoutLosingSurroundingBody()
    {
        var body = string.Join(
            Environment.NewLine,
            "앞에서 설명한 서원입니다.",
            string.Empty,
            CommunityEvidenceChartTextCodec.Encode(ValidBlock()),
            string.Empty,
            "그래프를 보고 함께 확인할 질문입니다.");

        var visibleBody = CommunityEvidenceChartTextCodec.StripBlocks(body);

        Assert.Contains("앞에서 설명한 서원입니다.", visibleBody, StringComparison.Ordinal);
        Assert.Contains("함께 확인할 질문", visibleBody, StringComparison.Ordinal);
        Assert.DoesNotContain(CommunityEvidenceChartTextCodec.StartMarker, visibleBody, StringComparison.Ordinal);
        Assert.DoesNotContain("KAMIS", visibleBody, StringComparison.Ordinal);
    }

    [Fact]
    public void EvidenceBlock_CanReplaceTheLastStoredChartWithoutChangingSurroundingBody()
    {
        var original = ValidBlock();
        var replacement = WithValues(
            original,
            CommunityEvidenceChartTypeCodes.Line,
            [new("개별 주문", 18m), new("20명 공동 주문", 7m)]);
        var body = $"설명{Environment.NewLine}{CommunityEvidenceChartTextCodec.Encode(original)}{Environment.NewLine}질문";

        var replaced = CommunityEvidenceChartTextCodec.TryReplaceLastBlock(
            body,
            replacement,
            out var updatedBody);

        Assert.True(replaced);
        Assert.StartsWith("설명", updatedBody, StringComparison.Ordinal);
        Assert.EndsWith("질문", updatedBody, StringComparison.Ordinal);
        var decoded = Assert.Single(CommunityEvidenceChartTextCodec.DecodeAll(updatedBody));
        Assert.Equal(CommunityEvidenceChartTypeCodes.Line, decoded.ChartTypeCode);
        Assert.Equal(7m, decoded.Points[1].Value);
    }

    [Fact]
    public void DonutChart_RejectsNegativeValues()
    {
        var block = WithValues(
            ValidBlock(),
            CommunityEvidenceChartTypeCodes.Donut,
            [new("구매자", 10m), new("공급자", -2m)]);

        var validation = CommunityEvidenceChartPolicy.Validate(block);

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, error => error.Contains("도넛 그래프", StringComparison.Ordinal));
    }

    [Fact]
    public void Statistics_ExposeTotalAverageRangeAndFirstToLastChange()
    {
        var statistics = CommunityEvidenceChartPolicy.CalculateStatistics(ValidBlock());

        Assert.Equal(24_000m, statistics.Total);
        Assert.Equal(8_000m, statistics.Average);
        Assert.Equal(7_000m, statistics.Minimum);
        Assert.Equal(9_000m, statistics.Maximum);
        Assert.Equal(-22.22m, statistics.FirstToLastChangePercent);
    }

    private static CommunityEvidenceChartBlock ValidBlock()
        => new()
        {
            ChartTypeCode = CommunityEvidenceChartTypeCodes.Bar,
            Title = "공동구매 참여 수량별 예상 도착 단가",
            Claim = "참여 수량이 모이면 단위당 고정 물류비 부담이 낮아집니다.",
            SeriesLabel = "예상 도착 단가",
            Unit = "KRW/kg",
            SourceLabel = "KAMIS 농산물 유통정보",
            SourceUrl = "https://www.kamis.or.kr/",
            ReferenceDate = "2026-07-19",
            Interpretation = "현재 가정에서는 참여 수량이 늘수록 예상 단가가 낮아집니다.",
            Limitation = "실제 견적, 품질, 세금과 운송 조건에 따라 달라질 수 있습니다.",
            Points =
            [
                new("10명", 9_000m),
                new("20명", 8_000m),
                new("30명", 7_000m)
            ]
        };

    private static CommunityEvidenceChartBlock WithValues(
        CommunityEvidenceChartBlock block,
        string chartTypeCode,
        IReadOnlyList<CommunityEvidenceChartPoint> points)
        => new()
        {
            ChartTypeCode = chartTypeCode,
            Title = block.Title,
            Claim = block.Claim,
            SeriesLabel = block.SeriesLabel,
            Unit = block.Unit,
            SourceLabel = block.SourceLabel,
            SourceUrl = block.SourceUrl,
            ReferenceDate = block.ReferenceDate,
            Interpretation = block.Interpretation,
            Limitation = block.Limitation,
            Points = points
        };
}
