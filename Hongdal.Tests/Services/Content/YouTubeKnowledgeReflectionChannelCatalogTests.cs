using Hongdal.Contracts.Common.Content;

namespace Hongdal.Tests.Services.Content;

public sealed class YouTubeKnowledgeReflectionChannelCatalogTests
{
    [Fact]
    public void 대표채널은_순위가아닌_주제관점과공식출처로모듈화한다()
    {
        Assert.Contains(YouTube지식성찰채널Catalog.항목, item => item.Key == "hongik-hakdang");
        Assert.Contains(YouTube지식성찰채널Catalog.항목, item => item.Key == "ted");
        Assert.Contains(YouTube지식성찰채널Catalog.항목, item => item.Key == "bible-project");
        Assert.Contains(YouTube지식성찰채널Catalog.항목, item => item.Key == "plum-village");
        Assert.All(YouTube지식성찰채널Catalog.항목, item =>
        {
            Assert.NotEmpty(item.주제코드목록);
            Assert.NotEmpty(item.관점표시);
            Assert.StartsWith("https://", item.공식출처Url, StringComparison.OrdinalIgnoreCase);
            Assert.All(item.주제코드목록, code => Assert.Contains(code, YouTube지식성찰주제코드.전체));
            Assert.True(!string.IsNullOrWhiteSpace(item.ChannelId) || !string.IsNullOrWhiteSpace(item.Handle));
            Assert.NotEmpty(item.메모);
        });
    }

    [Theory]
    [InlineData("TED", "@TED")]
    [InlineData("@bigthink", "@bigthink")]
    public void Handle은_at기호를포함한형식으로정규화한다(string input, string expected)
    {
        Assert.Equal(expected, YouTube지식성찰채널Catalog.NormalizeHandle(input));
    }
}
