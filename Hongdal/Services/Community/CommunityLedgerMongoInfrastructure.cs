using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using 홍달.Services.Options;

namespace Hongdal.Services.Community;

internal static class 커뮤니티원장MongoCollectionFactory
{
    private const string CollectionName = "community_ledgers";

    public static IMongoCollection<커뮤니티원장문서> Create(
        IMongoClient mongoClient,
        IOptions<MongoDbOptions> options)
    {
        var databaseName = options.Value.Database;
        if (string.IsNullOrWhiteSpace(databaseName))
        {
            throw new InvalidOperationException("MongoDb:Database configuration is required.");
        }

        return mongoClient
            .GetDatabase(databaseName.Trim())
            .GetCollection<커뮤니티원장문서>(CollectionName);
    }
}

internal static class 커뮤니티원장EventIdFactory
{
    public static string Create(string 원장Id, long revision)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{원장Id}:{revision}"));
        return $"ledger-{Convert.ToHexString(bytes).ToLowerInvariant()}";
    }
}
