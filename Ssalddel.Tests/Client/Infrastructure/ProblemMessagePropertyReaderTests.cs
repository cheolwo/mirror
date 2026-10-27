using System.Text.Json;
using Ssalddel.Client.Infrastructure.Http;

namespace Ssalddel.Tests.Client.Infrastructure;

public sealed class ProblemMessagePropertyReaderTests
{
    [Theory]
    [InlineData("{\"detail\":\" first \",\"message\":\"second\",\"title\":\"third\"}", " first ")]
    [InlineData("{\"detail\":\" \",\"message\":\"second\",\"title\":\"third\"}", "second")]
    [InlineData("{\"detail\":123,\"message\":null,\"title\":\"third\"}", "third")]
    [InlineData("{\"Detail\":\"different-case\",\"message\":\"message\"}", "message")]
    [InlineData("{\"detail\":\" \",\"message\":\"\\t\",\"title\":\"\"}", null)]
    [InlineData("{}", null)]
    [InlineData("{\"detail\":\"old\",\"detail\":\"last\"}", "last")]
    public void 속성우선순위와공백건너뛰기및문자열원문을유지한다(string json, string? expected)
    {
        using var document = JsonDocument.Parse(json);

        Assert.Equal(expected, ProblemMessagePropertyReader.Read(document.RootElement));
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("123")]
    [InlineData("true")]
    [InlineData("null")]
    [InlineData("\"text\"")]
    public void 객체여부검사와비JSON처리정책을호출자대신추가하지않는다(string json)
    {
        using var document = JsonDocument.Parse(json);

        Assert.Throws<InvalidOperationException>(() => ProblemMessagePropertyReader.Read(document.RootElement));
    }
}
