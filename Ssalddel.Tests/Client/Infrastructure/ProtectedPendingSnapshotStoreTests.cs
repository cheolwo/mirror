using System.Text.Json;
using FDriverApp.Services;
using RestaurantDeskApp.Services;
using RestaurantDeskApp.Services.Security;
using Ssalddel.Client.Infrastructure.Security;
using Ssalddel.Contracts.Food;
using PlatformStorage = Microsoft.Maui.Storage.SecureStorage;

namespace Ssalddel.Tests.Client.Infrastructure
{
    public sealed class ProtectedPendingSnapshotStoreTests
    {
        public static TheoryData<string> Roles => new() { "driver", "restaurant" };

        [Theory]
        [MemberData(nameof(Roles))]
        public async Task 실제Facade는_기존키와WebJSON으로_저장복원하고같은소유자만삭제한다(string role)
        {
            var storage = new PendingRecordingStorage();
            using var scope = PlatformStorage.UseForTest(storage);
            var store = Facade(role);

            await store.Save(Snapshot(role), default);
            Assert.Equal(ExpectedJson(role), storage.Json);
            Assert.Equal("owner-a", Owner(await store.Load(default)));
            await store.Clear("owner-A", default);
            Assert.Equal(ExpectedJson(role), storage.Json);
            await store.Clear("owner-a", default);

            Assert.Null(storage.Json);
            Assert.Equal(["write:" + Key(role), "read:" + Key(role), "read:" + Key(role), "read:" + Key(role), "remove:" + Key(role)], storage.Calls);
        }

        [Theory]
        [MemberData(nameof(Roles))]
        public async Task 실제요청원문과요청Id및revision은_복원후에도그대로남는다(string role)
        {
            var storage = new PendingRecordingStorage();
            using var scope = PlatformStorage.UseForTest(storage);
            var store = Facade(role);
            var snapshot = DetailedSnapshot(role);

            await store.Save(snapshot, default);
            var originalJson = storage.Json;
            var loaded = await store.Load(default);
            await store.Save(loaded, default);

            Assert.Equal(originalJson, storage.Json);
            if (loaded is FDriverPendingOperationsSnapshot driver)
            {
                Assert.Equal(RequestId, driver.Arrival!.Request.클라이언트요청Id);
                Assert.Equal(7, driver.Arrival.Request.예상시도Revision);
                Assert.Equal("offer-a", driver.Arrival.OfferId);
                Assert.Equal("attempt-a", driver.Arrival.AttemptId);
            }
            else
            {
                var request = Assert.Single(Assert.IsType<RestaurantProgressPendingSnapshot>(loaded).Requests);
                Assert.Equal(RequestId, request.RequestId);
                Assert.Equal(7, request.ExpectedRevision);
                Assert.Equal("원문\n유지", request.Reason);
                Assert.Equal(20, request.PreparationMinutes);
            }
        }

        [Theory]
        [MemberData(nameof(Roles))]
        public async Task 빈저장은_null이며_JSONnull과손상및검증실패는_삭제하지않고전파한다(string role)
        {
            var storage = new PendingRecordingStorage();
            using var scope = PlatformStorage.UseForTest(storage);
            var store = Facade(role);
            foreach (var empty in new string?[] { null, "", " \r\n\t" })
            {
                storage.Json = empty;
                Assert.Null(await store.Load(default));
            }

            foreach (var json in new[] { "null", "{", "[]", "{}" })
            {
                storage.Json = json;
                var failure = await Record.ExceptionAsync(() => store.Load(default));
                if (json is "{" or "[]") Assert.IsAssignableFrom<JsonException>(failure);
                else Assert.Equal(json == "null" ? ReadMessage(role) : ValidateMessage(role), Assert.IsType<InvalidDataException>(failure).Message);
                Assert.Equal(json, storage.Json);
            }

            Assert.All(storage.Calls, call => Assert.Equal("read:" + Key(role), call));
        }

        [Theory]
        [MemberData(nameof(Roles))]
        public async Task 잘못된업무snapshot과null은_쓰기전에실패하며_삭제나자동보정을하지않는다(string role)
        {
            var storage = new PendingRecordingStorage();
            using var scope = PlatformStorage.UseForTest(storage);
            var store = Facade(role);

            Assert.Equal(ValidateMessage(role), (await Assert.ThrowsAsync<InvalidDataException>(() => store.Save(InvalidSnapshot(role), default))).Message);
            await Assert.ThrowsAsync<NullReferenceException>(() => store.Save(null, default));
            Assert.Empty(storage.Calls);

            await store.Save(Snapshot(role), default);
            Assert.Equal(ExpectedJson(role), storage.Json);
        }

        [Theory]
        [MemberData(nameof(Roles))]
        public async Task 저장소실패는_JsonException도전파하며_잠금을해제한다(string role)
        {
            var storage = new PendingRecordingStorage();
            using var scope = PlatformStorage.UseForTest(storage);
            var store = Facade(role);
            foreach (var failure in new Exception[] { new IOException("fixture storage failure"), new JsonException("fixture JSON failure") })
            {
                storage.ReadFailure = failure;
                Assert.Same(failure, await Record.ExceptionAsync(() => store.Load(default)));
                Assert.Same(failure, await Record.ExceptionAsync(() => store.Clear("owner-a", default)));
                storage.ReadFailure = null;
                storage.WriteFailure = failure;
                Assert.Same(failure, await Record.ExceptionAsync(() => store.Save(Snapshot(role), default)));
                storage.WriteFailure = null;
                storage.Json = ExpectedJson(role);
                storage.RemoveFailure = failure;
                Assert.Same(failure, await Record.ExceptionAsync(() => store.Clear("owner-a", default)));
                storage.RemoveFailure = null;

                Assert.Equal("owner-a", Owner(await store.Load(default).WaitAsync(TimeSpan.FromSeconds(5))));
            }
        }

        [Theory]
        [MemberData(nameof(Roles))]
        public async Task 삭제false는_전문오류문구의IOException이며_이후저장잠금을막지않는다(string role)
        {
            var storage = new PendingRecordingStorage { Json = ExpectedJson(role), RemoveResult = false };
            using var scope = PlatformStorage.UseForTest(storage);
            var store = Facade(role);

            Assert.Equal(RemoveMessage(role), (await Assert.ThrowsAsync<IOException>(() => store.Clear("owner-a", default))).Message);
            Assert.Equal(ExpectedJson(role), storage.Json);
            await store.Save(Snapshot(role), default).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(ExpectedJson(role), storage.Json);
        }

        [Theory]
        [MemberData(nameof(Roles))]
        public async Task 빈저장의null소유자삭제는_기존null비교대로Remove를호출한다(string role)
        {
            var storage = new PendingRecordingStorage();
            using var scope = PlatformStorage.UseForTest(storage);
            var store = Facade(role);

            await store.Clear("owner-a", default);
            await store.Clear(null, default);

            Assert.Equal(["read:" + Key(role), "read:" + Key(role), "remove:" + Key(role)], storage.Calls);
        }

        [Theory]
        [MemberData(nameof(Roles))]
        public async Task 사전취소는_null검증보다먼저발생하며_저장소에접근하지않는다(string role)
        {
            var storage = new PendingRecordingStorage();
            using var scope = PlatformStorage.UseForTest(storage);
            var store = Facade(role);
            var cancelled = new CancellationToken(true);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.Load(cancelled));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.Save(null, cancelled));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.Clear(null, cancelled));
            Assert.Empty(storage.Calls);
        }

        [Theory]
        [MemberData(nameof(Roles))]
        public async Task 읽기후취소는_역직렬화와업무검증뒤확인한다(string role)
        {
            var storage = new PendingRecordingStorage();
            using var scope = PlatformStorage.UseForTest(storage);
            var store = Facade(role);
            foreach (var json in new string?[] { ExpectedJson(role), null, "{", "null", "{}" })
            {
                using var cancellation = new CancellationTokenSource();
                storage.Json = json;
                storage.AfterRead = cancellation.Cancel;
                var failure = await Record.ExceptionAsync(() => store.Load(cancellation.Token));
                if (json is null || json == ExpectedJson(role)) Assert.IsAssignableFrom<OperationCanceledException>(failure);
                else if (json == "{") Assert.IsAssignableFrom<JsonException>(failure);
                else Assert.IsType<InvalidDataException>(failure);
            }

            Assert.DoesNotContain(storage.Calls, call => call.StartsWith("remove:", StringComparison.Ordinal));
        }

        [Theory]
        [MemberData(nameof(Roles))]
        public async Task Clear읽기중취소는_소유자가일치할때만확인한다(string role)
        {
            foreach (var owner in new string?[] { "owner-a", "other-owner", null })
            {
                using var cancellation = new CancellationTokenSource();
                var storage = new PendingRecordingStorage { Json = ExpectedJson(role), AfterRead = cancellation.Cancel };
                using var scope = PlatformStorage.UseForTest(storage);
                var store = Facade(role);

                if (owner == "owner-a") await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.Clear(owner, cancellation.Token));
                else await store.Clear(owner, cancellation.Token);
                Assert.Equal(["read:" + Key(role)], storage.Calls);
            }
        }

        [Theory]
        [MemberData(nameof(Roles))]
        public async Task 쓰기후취소는_실제쓰기뒤실패하며_저장한원문을되돌리지않는다(string role)
        {
            using var cancellation = new CancellationTokenSource();
            var storage = new PendingRecordingStorage { AfterWrite = cancellation.Cancel };
            using var scope = PlatformStorage.UseForTest(storage);
            var store = Facade(role);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.Save(Snapshot(role), cancellation.Token));

            Assert.Equal(ExpectedJson(role), storage.Json);
            Assert.Equal("owner-a", Owner(await store.Load(default)));
            Assert.Equal(["write:" + Key(role), "read:" + Key(role)], storage.Calls);
        }

        [Theory]
        [MemberData(nameof(Roles))]
        public async Task Remove뒤취소는_새검사를추가하지않고_false결과도숨기지않는다(string role)
        {
            foreach (var removeResult in new[] { true, false })
            {
                using var cancellation = new CancellationTokenSource();
                var storage = new PendingRecordingStorage { Json = ExpectedJson(role), RemoveResult = removeResult, AfterRemove = cancellation.Cancel };
                using var scope = PlatformStorage.UseForTest(storage);
                var store = Facade(role);

                if (removeResult) await store.Clear("owner-a", cancellation.Token);
                else Assert.Equal(RemoveMessage(role), (await Assert.ThrowsAsync<IOException>(() => store.Clear("owner-a", cancellation.Token))).Message);
                Assert.Equal(["read:" + Key(role), "remove:" + Key(role)], storage.Calls);
            }
        }

        [Theory]
        [MemberData(nameof(Roles))]
        public async Task 같은Facade의LoadSaveClear는_진행중I오를겹치지않게직렬실행한다(string role)
        {
            var readEntered = Signal(); var releaseRead = Signal();
            var writeEntered = Signal(); var releaseWrite = Signal();
            var storage = new PendingRecordingStorage
            {
                Json = ExpectedJson(role),
                BeforeRead = async () => { readEntered.TrySetResult(); await releaseRead.Task; },
                BeforeWrite = async () => { writeEntered.TrySetResult(); await releaseWrite.Task; }
            };
            using var scope = PlatformStorage.UseForTest(storage);
            var store = Facade(role);
            var load = store.Load(default);
            await readEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var save = store.Save(Snapshot(role), default);
            Assert.Equal(["read:" + Key(role)], storage.Calls);

            releaseRead.SetResult();
            await load;
            await writeEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            storage.BeforeRead = null;
            var clear = store.Clear("owner-a", default);
            Assert.Equal(["read:" + Key(role), "write:" + Key(role)], storage.Calls);
            releaseWrite.SetResult();
            await save;
            await clear.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Equal(["read:" + Key(role), "write:" + Key(role), "read:" + Key(role), "remove:" + Key(role)], storage.Calls);
        }

        [Theory]
        [MemberData(nameof(Roles))]
        public async Task 다른Facade인스턴스는_같은역할이어도잠금을공유하지않는다(string role)
        {
            var readEntered = Signal(); var releaseRead = Signal();
            var storage = new PendingRecordingStorage
            {
                Json = ExpectedJson(role),
                BeforeRead = async () => { readEntered.TrySetResult(); await releaseRead.Task; }
            };
            using var scope = PlatformStorage.UseForTest(storage);
            var first = Facade(role); var second = Facade(role);
            var load = first.Load(default);
            await readEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));

            await second.Save(Snapshot(role), default).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(load.IsCompleted);
            releaseRead.SetResult();
            await load;
            Assert.Equal(["read:" + Key(role), "write:" + Key(role)], storage.Calls);
        }

        [Theory]
        [MemberData(nameof(Roles))]
        public async Task 잠금대기취소는_IO와업무검증을실행하지않고_다음호출의잠금을보존한다(string role)
        {
            var readEntered = Signal(); var releaseRead = Signal();
            var storage = new PendingRecordingStorage { BeforeRead = async () => { readEntered.TrySetResult(); await releaseRead.Task; } };
            using var scope = PlatformStorage.UseForTest(storage);
            var store = Facade(role);
            var load = store.Load(default);
            await readEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            using var cancellation = new CancellationTokenSource();
            var save = store.Save(null, cancellation.Token);
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => save);
            Assert.Equal(["read:" + Key(role)], storage.Calls);

            storage.BeforeRead = null;
            releaseRead.SetResult();
            await load;
            await store.Save(Snapshot(role), default).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(ExpectedJson(role), storage.Json);
        }

        [Fact]
        public async Task 공통Core의Validate는_잠금안에서실행하고_검증중취소뒤에도쓰기후검사를유지한다()
        {
            using var cancellation = new CancellationTokenSource();
            var storage = new PendingRecordingStorage();
            var validations = 0;
            var store = new ProtectedPendingSnapshotStore<PendingCoreSnapshot>("core-key", storage.GetAsync, storage.SetAsync,
                storage.Remove, _ => { validations++; cancellation.Cancel(); }, value => value.OwnerId, "read-error", "remove-error");

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.SaveAsync(new("owner-a"), cancellation.Token));

            Assert.Equal(1, validations);
            Assert.Equal("{\"ownerId\":\"owner-a\"}", storage.Json);
            Assert.Equal(["write:core-key"], storage.Calls);
        }

        [Fact]
        public async Task 공통Core의잠금대기동안은_Validate도시작하지않는다()
        {
            var readEntered = Signal(); var releaseRead = Signal();
            var storage = new PendingRecordingStorage { BeforeRead = async () => { readEntered.TrySetResult(); await releaseRead.Task; } };
            var validations = 0;
            var store = new ProtectedPendingSnapshotStore<PendingCoreSnapshot>("core-key", storage.GetAsync, storage.SetAsync,
                storage.Remove, _ => { validations++; throw new InvalidDataException("validation"); }, value => value.OwnerId, "read-error", "remove-error");
            var load = store.LoadAsync();
            await readEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var save = store.SaveAsync(new("owner-a"));
            Assert.Equal(0, validations);

            releaseRead.SetResult();
            await load;
            Assert.Equal("validation", (await Assert.ThrowsAsync<InvalidDataException>(() => save)).Message);
            Assert.Equal(1, validations);
            Assert.Equal(["read:core-key"], storage.Calls);
        }

        private static readonly Guid RequestId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        private sealed record PendingCoreSnapshot(string OwnerId);
        private sealed record StoreFacade(Func<CancellationToken, Task<object?>> Load, Func<object?, CancellationToken, Task> Save, Func<string?, CancellationToken, Task> Clear);
        private static StoreFacade Facade(string role)
        {
            if (role == "driver")
            {
                var store = new FDriverSecurePendingOperationStore();
                return new(async token => await store.LoadAsync(token), (snapshot, token) => store.SaveAsync((FDriverPendingOperationsSnapshot)snapshot!, token),
                    (owner, token) => store.ClearAsync(owner!, token));
            }
            var restaurant = new RestaurantSecureProgressPendingStore();
            return new(async token => await restaurant.LoadAsync(token), (snapshot, token) => restaurant.SaveAsync((RestaurantProgressPendingSnapshot)snapshot!, token),
                (owner, token) => restaurant.ClearAsync(owner!, token));
        }

        private static object Snapshot(string role) => role == "driver" ? new FDriverPendingOperationsSnapshot("owner-a", null, null, null) : new RestaurantProgressPendingSnapshot("owner-a", []);
        private static object InvalidSnapshot(string role) => role == "driver" ? new FDriverPendingOperationsSnapshot("", null, null, null) : new RestaurantProgressPendingSnapshot("owner-a", [new("order-a", Guid.Empty, 7, 음식점주문진행작업코드.전체[0], 20, "", null)]);
        private static object DetailedSnapshot(string role) => role == "driver"
            ? new FDriverPendingOperationsSnapshot("owner-a", new("offer-a", "attempt-a", new() { 클라이언트요청Id = RequestId, 예상시도Revision = 7 }), null, null)
            : new RestaurantProgressPendingSnapshot("owner-a", [new("order-a", RequestId, 7, 음식점주문진행작업코드.전체[0], 20, "원문\n유지", null)]);
        private static string? Owner(object? snapshot) => snapshot switch { FDriverPendingOperationsSnapshot driver => driver.OwnerId, RestaurantProgressPendingSnapshot restaurant => restaurant.OwnerId, _ => null };
        private static string Key(string role) => role == "driver" ? "ssalddel.fdriver.food-pending.v1" : "ssalddel.restaurant.progress-pending.v1";
        private static string ExpectedJson(string role) => role == "driver" ? "{\"ownerId\":\"owner-a\",\"arrival\":null,\"dispatchIntent\":null,\"interruption\":null,\"startReceptionRequestId\":null,\"startRequestId\":null}" : "{\"ownerId\":\"owner-a\",\"requests\":[]}";
        private static string ReadMessage(string role) => role == "driver" ? "이전 요청 기록을 읽을 수 없습니다." : "진행 요청 기록을 읽을 수 없습니다.";
        private static string ValidateMessage(string role) => role == "driver" ? "이전 요청 기록을 확인해 주세요." : "진행 요청 기록을 확인해 주세요.";
        private static string RemoveMessage(string role) => role == "driver" ? "이전 요청 기록을 지우지 못했습니다." : "진행 요청 기록을 지우지 못했습니다.";
        private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    internal sealed class PendingRecordingStorage
    {
        public string? Json { get; set; }
        public List<string> Calls { get; } = [];
        public bool RemoveResult { get; set; } = true;
        public Exception? ReadFailure { get; set; }
        public Exception? WriteFailure { get; set; }
        public Exception? RemoveFailure { get; set; }
        public Func<Task>? BeforeRead { get; set; }
        public Func<Task>? BeforeWrite { get; set; }
        public Action? AfterRead { get; set; }
        public Action? AfterWrite { get; set; }
        public Action? AfterRemove { get; set; }

        public async Task<string?> GetAsync(string key)
        {
            Calls.Add("read:" + key);
            if (ReadFailure is { } failure) throw failure;
            var json = Json;
            if (BeforeRead is { } before) await before();
            AfterRead?.Invoke();
            return json;
        }

        public async Task SetAsync(string key, string json)
        {
            Calls.Add("write:" + key);
            if (WriteFailure is { } failure) throw failure;
            if (BeforeWrite is { } before) await before();
            Json = json;
            AfterWrite?.Invoke();
        }

        public bool Remove(string key)
        {
            Calls.Add("remove:" + key);
            if (RemoveFailure is { } failure) throw failure;
            if (RemoveResult) Json = null;
            AfterRemove?.Invoke();
            return RemoveResult;
        }
    }
}

// 원래 두 adapter의 완전한 MAUI 저장 연결만 대체합니다. 실제 단말 보안 저장 검증은 아닙니다.
namespace Microsoft.Maui.Storage
{
    internal static class SecureStorage
    {
        private static readonly AsyncLocal<Ssalddel.Tests.Client.Infrastructure.PendingRecordingStorage?> Current = new();
        public static Ssalddel.Tests.Client.Infrastructure.PendingRecordingStorage Default => Current.Value ?? throw new InvalidOperationException("보류 요청 저장소 시험 범위가 필요합니다.");
        internal static IDisposable UseForTest(Ssalddel.Tests.Client.Infrastructure.PendingRecordingStorage storage)
        {
            var previous = Current.Value;
            Current.Value = storage;
            return new Scope(previous);
        }
        private sealed class Scope(Ssalddel.Tests.Client.Infrastructure.PendingRecordingStorage? previous) : IDisposable
        {
            public void Dispose() => Current.Value = previous;
        }
    }
}
