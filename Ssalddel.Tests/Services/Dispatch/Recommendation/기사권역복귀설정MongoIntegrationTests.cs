using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Ssalddel.Contracts.Driver.Settings;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.Services.Options;

namespace Ssalddel.Tests.Services.Dispatch.Recommendation;

public sealed class 기사권역복귀설정MongoIntegrationTests
{
    [기사권역복귀MongoFact]
    public async Task 실제격리Mongo에서보호설정다중hostCAS와GPS재시도를검증한다()
    {
        var connection = Environment.GetEnvironmentVariable("SSALDDEL_RETURN_AREA_MONGO_TEST_CONNECTION")
                         ?? throw new InvalidOperationException("Isolated Mongo connection is required.");
        if (!Uri.TryCreate(connection, UriKind.Absolute, out var uri) || uri.Host is not ("127.0.0.1" or "localhost" or "::1"))
            throw new InvalidOperationException("Return area tests require loopback Mongo.");
        const string prefix = "ssalddel_return_area_r42_";
        var databaseName = prefix + Guid.NewGuid().ToString("N");
        var client = new MongoClient(connection);
        var options = Options.Create(new MongoDbOptions { Database = databaseName });
        var protection = new EphemeralDataProtectionProvider();
        var first = new Mongo기사권역복귀설정Store(client, options, protection);
        var second = new Mongo기사권역복귀설정Store(new MongoClient(connection), options, protection);
        try
        {
            await client.GetDatabase(databaseName).RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));
            var blank = await first.조회Async("private-driver");
            Assert.Equal(0, blank.설정.Revision); Assert.Equal("off", blank.설정.Mode); Assert.Null(blank.설정.기준위도);
            var competing = await Task.WhenAll(Create(first), Create(second));
            Assert.Equal(1, competing.Count(x => x));
            var current = await second.조회Async("private-driver");
            Assert.Equal(1, current.설정.Revision);
            Assert.Equal(37.543219m, current.설정.기준위도);
            var raw = (await client.GetDatabase(databaseName).GetCollection<BsonDocument>(Mongo기사권역복귀설정Store.CollectionName)
                .Find(FilterDefinition<BsonDocument>.Empty).SingleAsync()).ToJson();
            Assert.DoesNotContain("private-driver", raw); Assert.DoesNotContain("private-anchor", raw);
            Assert.DoesNotContain("37.543219", raw); Assert.DoesNotContain("127.123456", raw);
            var gps = new DateTime(2026, 10, 8, 3, 0, 0, DateTimeKind.Utc);
            Assert.True(await first.활성상태갱신Async("private-driver", 1, true, gps));
            Assert.True(await second.활성상태갱신Async("private-driver", 1, true, gps));
            Assert.False(await first.활성상태갱신Async("private-driver", 1, false, gps));
            Assert.False(await second.활성상태갱신Async("private-driver", 1, false, gps.AddSeconds(-1)));
            Assert.False(await first.활성상태갱신Async("private-driver", 2, false, gps.AddSeconds(1)));
            var active = await second.조회Async("private-driver");
            Assert.Equal(1, active.설정.Revision); Assert.True(active.활성여부); Assert.Equal(gps, active.위치판정시각Utc);
            var replacement = Settings(); replacement.Revision = 1; replacement.Mode = "off";
            var changed = await second.저장Async("private-driver", replacement, 1);
            Assert.Equal(2, changed.설정.Revision); Assert.False(changed.활성여부); Assert.Null(changed.위치판정시각Utc);
            Assert.False(await first.활성상태갱신Async("private-driver", 1, true, gps.AddMinutes(1)));
            await Assert.ThrowsAsync<기사권역복귀설정ConflictException>(() => first.저장Async("private-driver", replacement, 1));
            Assert.Equal(0, (await first.조회Async("other-driver")).설정.Revision);
            Assert.Equal(1, replacement.Revision); // caller's configuration was not changed by persistence.
        }
        finally
        {
            if (!databaseName.StartsWith(prefix, StringComparison.Ordinal) || databaseName.Length != prefix.Length + 32)
                throw new InvalidOperationException("Test database ownership mismatch.");
            await client.DropDatabaseAsync(databaseName);
        }

        static async Task<bool> Create(Mongo기사권역복귀설정Store store)
        {
            try { await store.저장Async("private-driver", Settings(), 0); return true; }
            catch (기사권역복귀설정ConflictException) { return false; }
        }
    }

    private static 기사권역복귀설정 Settings() => new() { Mode = "automatic", 기준위치명 = "private-anchor", 기준위도 = 37.543219m, 기준경도 = 127.123456m };
}

public sealed class 기사권역복귀MongoFactAttribute : FactAttribute
{
    public 기사권역복귀MongoFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SSALDDEL_RETURN_AREA_MONGO_TEST_CONNECTION")))
            Skip = "격리된 권역 복귀 Mongo 시험용 loopback 연결이 지정되지 않았습니다.";
    }
}
