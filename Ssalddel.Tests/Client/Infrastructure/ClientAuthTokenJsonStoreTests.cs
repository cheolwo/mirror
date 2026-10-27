using System.Text.Json;
using Ssalddel.Client.Infrastructure.Security;

namespace Ssalddel.Tests.Client.Infrastructure
{
    public sealed class ClientAuthTokenJsonStoreTests
    {
        private const string SnapshotJson = "{\"accessToken\":\"access\",\"accessTokenExpiresAtUtc\":\"2026-10-20T01:02:03Z\",\"refreshToken\":\"refresh\",\"refreshTokenExpiresAtUtc\":\"2026-10-21T01:02:03Z\",\"userId\":\"user-1\",\"userName\":\"rider\",\"roles\":[\"role-a\",\"role-b\"],\"preferredLanguageCode\":\"ko\"}";

        public static TheoryData<string> Roles => new()
        {
            "shipper", "driver", "orderer", "restaurant", "warehouse", "seller", "human-resources"
        };

        [Theory]
        [MemberData(nameof(Roles))]
        public async Task 실제역할Facade는_기존키와WebJSON으로_저장복원삭제한다(string role)
        {
            var storage = new TokenStoreRecordingStorage();
            using var scope = TokenStoreTestStorage.Use(storage);
            var store = CreateFacade(role);

            await store.SaveAsync(Snapshot());
            var loaded = await store.LoadAsync();
            await store.ClearAsync();

            Assert.Equal(SnapshotJson, storage.WrittenJson);
            AssertSnapshot(loaded);
            Assert.Equal(["write:" + Key(role), "read:" + Key(role), "remove:" + Key(role)], storage.Calls);
        }

        [Theory]
        [MemberData(nameof(Roles))]
        public async Task 빈값과JSON리터럴null은_삭제하지않는다(string role)
        {
            foreach (var json in new string?[] { null, "", " \r\n\t", "null" })
            {
                var storage = new TokenStoreRecordingStorage { Json = json };
                using var scope = TokenStoreTestStorage.Use(storage);

                Assert.Null(await CreateFacade(role).LoadAsync());
                Assert.Equal(["read:" + Key(role)], storage.Calls);
            }
        }

        [Theory]
        [MemberData(nameof(Roles))]
        public async Task 손상JSON은_같은역할키만삭제하고null을반환한다(string role)
        {
            foreach (var json in new[] { "{", "{\"roles\":123}" })
            {
                var storage = new TokenStoreRecordingStorage { Json = json };
                using var scope = TokenStoreTestStorage.Use(storage);

                Assert.Null(await CreateFacade(role).LoadAsync());
                Assert.Equal(["read:" + Key(role), "remove:" + Key(role)], storage.Calls);
            }
        }

        [Theory]
        [MemberData(nameof(Roles))]
        public async Task 삭제의false결과는_손상처리와명시삭제에서_기존대로무시한다(string role)
        {
            var storage = new TokenStoreRecordingStorage { Json = "{", RemoveResult = false };
            using var scope = TokenStoreTestStorage.Use(storage);
            var store = CreateFacade(role);

            Assert.Null(await store.LoadAsync());
            await store.ClearAsync();

            Assert.Equal(["read:" + Key(role), "remove:" + Key(role), "remove:" + Key(role)], storage.Calls);
        }

        [Theory]
        [MemberData(nameof(Roles))]
        public async Task 읽기실패는_JsonException도삼키거나삭제하지않는다(string role)
        {
            foreach (var failure in Failures())
            {
                var storage = new TokenStoreRecordingStorage { ReadFailure = failure };
                using var scope = TokenStoreTestStorage.Use(storage);

                Assert.Same(failure, await Record.ExceptionAsync(() => CreateFacade(role).LoadAsync()));
                Assert.Equal(["read:" + Key(role)], storage.Calls);
            }
        }

        [Theory]
        [MemberData(nameof(Roles))]
        public async Task 손상후삭제실패와명시삭제실패는_JsonException도전파한다(string role)
        {
            foreach (var failure in Failures())
            {
                var storage = new TokenStoreRecordingStorage { Json = "{", RemoveFailure = failure };
                using var scope = TokenStoreTestStorage.Use(storage);
                var store = CreateFacade(role);

                Assert.Same(failure, await Record.ExceptionAsync(() => store.LoadAsync()));
                Assert.Same(failure, Record.Exception(() => { _ = store.ClearAsync(); }));
                Assert.Equal(["read:" + Key(role), "remove:" + Key(role), "remove:" + Key(role)], storage.Calls);
            }
        }

        [Theory]
        [MemberData(nameof(Roles))]
        public async Task 쓰기실패는_다시쓰기나삭제없이전파한다(string role)
        {
            foreach (var failure in Failures())
            {
                var storage = new TokenStoreRecordingStorage { WriteFailure = failure };
                using var scope = TokenStoreTestStorage.Use(storage);

                Assert.Same(failure, await Record.ExceptionAsync(() => CreateFacade(role).SaveAsync(Snapshot())));
                Assert.Equal(["write:" + Key(role)], storage.Calls);
            }
        }

        [Theory]
        [MemberData(nameof(Roles))]
        public async Task 사전취소는_플랫폼접근전발생하고_Clear의동기예외를유지한다(string role)
        {
            var storage = new TokenStoreRecordingStorage();
            using var scope = TokenStoreTestStorage.Use(storage);
            var store = CreateFacade(role);
            var cancellation = new CancellationToken(true);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.LoadAsync(cancellation));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.SaveAsync(Snapshot(), cancellation));
            Assert.ThrowsAny<OperationCanceledException>(() => { _ = store.ClearAsync(cancellation); });
            Assert.Empty(storage.Calls);
        }

        [Theory]
        [MemberData(nameof(Roles))]
        public async Task null과취소의우선순위는_4개명시검사와3개직렬화를구분한다(string role)
        {
            var storage = new TokenStoreRecordingStorage();
            using var scope = TokenStoreTestStorage.Use(storage);
            var store = CreateFacade(role);
            var cancelled = new CancellationToken(true);

            // 기존 async Save는 null 검사도 Task 실패로 전달합니다.
            Task? save = null;
            Assert.Null(Record.Exception(() => { save = store.SaveAsync(null!, cancelled); }));
            var failure = await Record.ExceptionAsync(() => save!);
            if (HasNullGuard(role))
            {
                Assert.Equal("snapshot", Assert.IsType<ArgumentNullException>(failure).ParamName);
                var nullFailure = await Assert.ThrowsAsync<ArgumentNullException>(() => store.SaveAsync(null!));
                Assert.Equal("snapshot", nullFailure.ParamName);
            }
            else
            {
                Assert.IsAssignableFrom<OperationCanceledException>(failure);
                await store.SaveAsync(null!);
                Assert.Equal("null", storage.WrittenJson);
            }

            Assert.Equal(HasNullGuard(role) ? Array.Empty<string>() : ["write:" + Key(role)], storage.Calls);
        }

        [Theory]
        [MemberData(nameof(Roles))]
        public async Task 플랫폼IO뒤취소는_새검사나손상삭제생략을추가하지않는다(string role)
        {
            var storage = new TokenStoreRecordingStorage { Json = SnapshotJson };
            using var scope = TokenStoreTestStorage.Use(storage);
            var store = CreateFacade(role);
            using var readCancellation = new CancellationTokenSource();
            storage.AfterRead = readCancellation.Cancel;
            AssertSnapshot(await store.LoadAsync(readCancellation.Token));

            storage.Json = "{";
            using var corruptCancellation = new CancellationTokenSource();
            storage.AfterRead = corruptCancellation.Cancel;
            Assert.Null(await store.LoadAsync(corruptCancellation.Token));

            using var writeCancellation = new CancellationTokenSource();
            storage.AfterWrite = writeCancellation.Cancel;
            await store.SaveAsync(Snapshot(), writeCancellation.Token);

            using var removeCancellation = new CancellationTokenSource();
            storage.AfterRemove = removeCancellation.Cancel;
            await store.ClearAsync(removeCancellation.Token);

            Assert.Equal(["read:" + Key(role), "read:" + Key(role), "remove:" + Key(role), "write:" + Key(role), "remove:" + Key(role)], storage.Calls);
        }

        [Fact]
        public async Task 플랫폼비의존Core는_전달한delegate와키만사용하며_null직렬화를허용한다()
        {
            var storage = new TokenStoreRecordingStorage();
            var store = new ClientAuthTokenJsonStore("isolated-key", storage.GetAsync, storage.SetAsync, storage.Remove);

            await store.SaveAsync(Snapshot());
            Assert.Equal(SnapshotJson, storage.WrittenJson);
            AssertSnapshot(await store.LoadAsync());
            await store.SaveAsync(null!);
            Assert.Null(await store.LoadAsync());
            storage.Json = "{";
            Assert.Null(await store.LoadAsync());
            await store.ClearAsync();

            Assert.Equal(["write:isolated-key", "read:isolated-key", "write:isolated-key", "read:isolated-key", "read:isolated-key", "remove:isolated-key", "remove:isolated-key"], storage.Calls);
        }

        private static IClientSecureTokenStore CreateFacade(string role) => role switch
        {
            "shipper" => new SsalddelApp.Services.Security.MauiSecureTokenStore(),
            "driver" => new DriverApp.Services.Security.MauiSecureTokenStore(),
            "orderer" => new OrdererApp.Services.Security.OrdererMauiSecureTokenStore(),
            "restaurant" => new RestaurantDeskApp.Services.Security.RestaurantMauiSecureTokenStore(),
            "warehouse" => new WarehouseManagerApp.Services.Security.WarehouseMauiSecureTokenStore(),
            "seller" => new SellerApp.Services.MauiSecureTokenStore(),
            "human-resources" => new HumanResourcesManagerApp.Services.Security.HumanResourcesMauiSecureTokenStore(),
            _ => throw new ArgumentOutOfRangeException(nameof(role))
        };

        private static string Key(string role) => role == "seller"
            ? "ssalddel.seller.auth_token.v1"
            : $"ssalddel.{role}.authToken.v1";

        private static bool HasNullGuard(string role) => role is "orderer" or "restaurant" or "warehouse" or "human-resources";
        private static Exception[] Failures() => [new IOException("test storage failure"), new JsonException("test storage JSON failure")];
        private static ClientAuthTokenSnapshot Snapshot() => new("access", new(2026, 10, 20, 1, 2, 3, DateTimeKind.Utc), "refresh", new(2026, 10, 21, 1, 2, 3, DateTimeKind.Utc), "user-1", "rider", ["role-a", "role-b"], "ko");

        private static void AssertSnapshot(ClientAuthTokenSnapshot? actual)
        {
            Assert.NotNull(actual);
            var expected = Snapshot();
            Assert.Equal(expected with { Roles = actual.Roles }, actual);
            Assert.Equal(expected.Roles, actual.Roles);
        }
    }

    // 실제 7개 facade 소스를 실행하기 위한 장치 경계입니다. MAUI/운영 보안 저장 증거가 아닙니다.
    internal static class TokenStoreTestStorage
    {
        private static readonly AsyncLocal<TokenStoreRecordingStorage?> Current = new();
        public static TokenStoreRecordingStorage Default => Current.Value ?? throw new InvalidOperationException("토큰 저장소 시험 범위가 필요합니다.");
        public static IDisposable Use(TokenStoreRecordingStorage storage)
        {
            var previous = Current.Value;
            Current.Value = storage;
            return new Scope(previous);
        }

        private sealed class Scope(TokenStoreRecordingStorage? previous) : IDisposable
        {
            public void Dispose() => Current.Value = previous;
        }
    }

    internal sealed class TokenStoreRecordingStorage
    {
        public string? Json { get; set; }
        public string? WrittenJson { get; private set; }
        public List<string> Calls { get; } = [];
        public bool RemoveResult { get; init; } = true;
        public Exception? ReadFailure { get; init; }
        public Exception? WriteFailure { get; init; }
        public Exception? RemoveFailure { get; init; }
        public Action? AfterRead { get; set; }
        public Action? AfterWrite { get; set; }
        public Action? AfterRemove { get; set; }

        public Task<string?> GetAsync(string key)
        {
            Calls.Add("read:" + key);
            if (ReadFailure is { } failure) throw failure;
            AfterRead?.Invoke();
            return Task.FromResult(Json);
        }

        public Task SetAsync(string key, string json)
        {
            Calls.Add("write:" + key);
            if (WriteFailure is { } failure) throw failure;
            WrittenJson = Json = json;
            AfterWrite?.Invoke();
            return Task.CompletedTask;
        }

        public bool Remove(string key)
        {
            Calls.Add("remove:" + key);
            if (RemoveFailure is { } failure) throw failure;
            Json = null;
            AfterRemove?.Invoke();
            return RemoveResult;
        }
    }
}

namespace SsalddelApp.Services.Security
{
    internal static class SecureStorage { public static Ssalddel.Tests.Client.Infrastructure.TokenStoreRecordingStorage Default => Ssalddel.Tests.Client.Infrastructure.TokenStoreTestStorage.Default; }
}
namespace DriverApp.Services.Security
{
    internal static class SecureStorage { public static Ssalddel.Tests.Client.Infrastructure.TokenStoreRecordingStorage Default => Ssalddel.Tests.Client.Infrastructure.TokenStoreTestStorage.Default; }
}
namespace OrdererApp.Services.Security
{
    internal static class SecureStorage { public static Ssalddel.Tests.Client.Infrastructure.TokenStoreRecordingStorage Default => Ssalddel.Tests.Client.Infrastructure.TokenStoreTestStorage.Default; }
}
namespace RestaurantDeskApp.Services.Security
{
    internal static class SecureStorage { public static Ssalddel.Tests.Client.Infrastructure.TokenStoreRecordingStorage Default => Ssalddel.Tests.Client.Infrastructure.TokenStoreTestStorage.Default; }
}
namespace WarehouseManagerApp.Services.Security
{
    internal static class SecureStorage { public static Ssalddel.Tests.Client.Infrastructure.TokenStoreRecordingStorage Default => Ssalddel.Tests.Client.Infrastructure.TokenStoreTestStorage.Default; }
}
namespace SellerApp.Services
{
    internal static class SecureStorage { public static Ssalddel.Tests.Client.Infrastructure.TokenStoreRecordingStorage Default => Ssalddel.Tests.Client.Infrastructure.TokenStoreTestStorage.Default; }
}
namespace HumanResourcesManagerApp.Services.Security
{
    internal static class SecureStorage { public static Ssalddel.Tests.Client.Infrastructure.TokenStoreRecordingStorage Default => Ssalddel.Tests.Client.Infrastructure.TokenStoreTestStorage.Default; }
}
