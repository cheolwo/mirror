using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Ssalddel.Contracts.Common.Orderer;

namespace Ssalddel.Ui.Common.Areas.BackOffice.ViewModels;

internal static class 같이수입준비초안Codec
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static 같이수입준비원장저장요청 Clone(같이수입준비원장저장요청 source)
        => JsonSerializer.Deserialize<같이수입준비원장저장요청>(
               JsonSerializer.Serialize(source, JsonOptions),
               JsonOptions)
           ?? new 같이수입준비원장저장요청();

    public static string Fingerprint(같이수입준비원장저장요청 request)
    {
        var fingerprintRequest = Clone(request);
        fingerprintRequest.요청멱등키 = string.Empty;
        fingerprintRequest.기대Revision = null;
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
                JsonSerializer.Serialize(fingerprintRequest, JsonOptions))))
            .ToLowerInvariant();
    }
}
