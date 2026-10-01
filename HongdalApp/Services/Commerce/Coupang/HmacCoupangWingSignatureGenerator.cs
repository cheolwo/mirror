using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace HongdalApp.Services.Commerce.Coupang;

public sealed class HmacCoupangWingSignatureGenerator : ICoupangWingSignatureGenerator
{
    public string Generate(string method, string path, string query, string accessKey, string secretKey, DateTimeOffset signedAt)
    {
        var signedDate = signedAt.UtcDateTime.ToString("yyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
        var message = $"{signedDate}{method.ToUpperInvariant()}{path}{query}";

        using var hmac = new HMACSHA256(Encoding.ASCII.GetBytes(secretKey));
        var signature = Convert.ToHexString(hmac.ComputeHash(Encoding.ASCII.GetBytes(message))).ToLowerInvariant();

        return $"CEA algorithm=HmacSHA256, access-key={accessKey}, signed-date={signedDate}, signature={signature}";
    }
}
