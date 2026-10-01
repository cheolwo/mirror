namespace HongdalApp.Services.Commerce.Coupang;

public interface ICoupangWingSignatureGenerator
{
    string Generate(string method, string path, string query, string accessKey, string secretKey, DateTimeOffset signedAt);
}
