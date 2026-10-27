using System.Security.Cryptography;
using System.Text;

namespace Ssalddel.Tests.Ui.Common;

public sealed partial class DocumentOutputCompatibilityR2Tests
{
    [Fact]
    public void 운송장과입출고품목표의전체HTML평문은_추출전22개기준출력과동일하다()
    {
        var outputs = DocumentOutputR2Fixture.Create();

        Assert.Equal(Expected.Count, outputs.Count);
        foreach (var (name, output) in outputs)
        {
            var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(DocumentOutputR2Fixture.NormalizedJson(output))));
            Assert.True(Expected.TryGetValue(name, out var expected), name);
            Assert.True(expected == hash, $"문서 출력 변경: {name}; expected={expected}; actual={hash}");
        }
    }
}
