using Hongdal.Contracts.Common.Content;

namespace Hongdal.Tests.Services.Content;

public sealed class YouTube음식채널조사CatalogTests
{
    [Fact]
    public void 운영시작카탈로그는_검증된고유채널과분류를가진다()
    {
        var channels = YouTube음식채널조사Catalog.항목;

        Assert.True(channels.Count >= 25);
        Assert.Equal(channels.Count, channels.Select(item => item.ChannelId).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(channels.Count, channels.Select(item => item.Handle).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(channels, channel =>
        {
            Assert.StartsWith("UC", channel.ChannelId, StringComparison.Ordinal);
            Assert.StartsWith("@", channel.Handle, StringComparison.Ordinal);
            Assert.StartsWith("https://www.youtube.com/@", channel.공식채널Url, StringComparison.Ordinal);
            Assert.Equal(2, channel.국가코드.Length);
            Assert.InRange(channel.구매발견점수, 0, 100);
            Assert.InRange(channel.수입발견점수, 0, 100);
            Assert.NotEmpty(channel.분류코드목록);
            Assert.All(channel.분류코드목록, category =>
                Assert.Contains(category, YouTube음식채널분류코드.전체));
            Assert.Equal(DateTimeKind.Utc, channel.조사확인일시Utc.Kind);
        });

        Assert.Contains(channels, channel => channel.국가코드 == YouTube채널수집국가코드.한국);
        Assert.Contains(channels, channel => channel.국가코드 == YouTube채널수집국가코드.미국);
    }
}
