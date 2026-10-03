using System.Threading.Channels;
using Microsoft.Extensions.Options;
using Ssalddel.Client.Infrastructure;
using Ssalddel.Client.Infrastructure.Security;
using Ssalddel.Client.Infrastructure.Transport;
using Ssalddel.Contracts.Common.Inbound;
using Ssalddel.Contracts.Common.Inventory;
using Ssalddel.Contracts.Common.Warehouse;
using Ssalddel.Contracts.Shipper.Request;
using SsalddelApp.Models.Shipper;
using SsalddelApp.Services;
using SsalddelApp.ViewModels.Shipper;

namespace Ssalddel.Tests.Clients
{
    internal sealed class ShipperRecoveryTokenStore : IClientSecureTokenStore
    {
        public ClientAuthTokenSnapshot? Snapshot { get; set; }
        public Func<Task<ClientAuthTokenSnapshot?>>? Load { get; set; }
        public Func<ClientAuthTokenSnapshot, Task>? Save { get; set; }
        public bool FailClear { get; set; }
        public Task<ClientAuthTokenSnapshot?> LoadAsync(CancellationToken cancellationToken = default)
            => Load?.Invoke() ?? Task.FromResult(Snapshot);
        public async Task SaveAsync(ClientAuthTokenSnapshot snapshot, CancellationToken cancellationToken = default)
        {
            if (Save is not null) await Save(snapshot);
            Snapshot = snapshot;
        }
        public Task ClearAsync(CancellationToken cancellationToken = default)
        {
            if (FailClear) throw new IOException("test secure storage unavailable");
            Snapshot = null;
            return Task.CompletedTask;
        }
    }

    internal sealed class ShipperRecoveryFixture : IDisposable
    {
        public ShipperRecoveryTokenStore Store { get; } = new();
        public AuthSession Auth { get; }
        public ShipperRecoveryOperations Operations { get; } = new();
        public TransportRequestLedgerObserver Observer { get; } = new();
        public ShipperRequestDetailPageViewModel Model { get; }
        private readonly HttpClient _httpClient = new() { BaseAddress = new Uri("https://test.invalid/") };

        public ShipperRecoveryFixture()
        {
            Auth = new(Store, new ClientSessionGuard());
            var options = Options.Create(new ClientDataModeOptions());
            Model = new(Operations, Observer, new FakeShipperPaymentService(_httpClient, Auth, options), options, Auth);
        }

        public static ClientAuthTokenSnapshot Snapshot(string user = "shipper-a", string token = "access-a")
            => new(token, DateTime.UtcNow.AddMinutes(30), $"refresh-{token}", DateTime.UtcNow.AddDays(1), user, user, ["화주"]);

        public void Dispose() { Model.Dispose(); _httpClient.Dispose(); }
    }

    internal sealed class ShipperRecoveryOperations : IShipperOperationsService
    {
        internal sealed record Pending(string Id, CancellationToken Token, TaskCompletionSource<ShipperRequestItem?> Completion);
        private readonly Channel<Pending> _started = Channel.CreateUnbounded<Pending>();
        public int Calls { get; private set; }
        public async Task<Pending> NextAsync()
            => await _started.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        public Task<ShipperRequestItem?> GetRequestAsync(string requestId, CancellationToken cancellationToken = default)
        {
            Calls++;
            var pending = new Pending(requestId, cancellationToken, new(TaskCreationOptions.RunContinuationsAsynchronously));
            _started.Writer.TryWrite(pending);
            // Cancellation is deliberately ignored to exercise stale response guards, not just cooperative HTTP.
            return pending.Completion.Task;
        }
        public Task<IReadOnlyList<ShipperRequestItem>> GetRequestsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<공개화물요약응답>> GetPublicCargoAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<창고요약응답>> GetWarehousesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<입고요청항목응답>> GetInboundsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<재고항목응답>> GetInventoryAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<string>> GetVehicleTypesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<decimal> EstimateFareAsync(string vehicleType, decimal distanceKm, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ShipperRequestItem> AddRequestAsync(ShipperRequestItem request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ShipperRequestItem> UpdateRequestAsync(ShipperRequestItem request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteRequestAsync(string requestId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}

// Compile-only MAUI device boundary for the linked production decoration dependency.
// No test invokes device storage; this is not evidence for Android SecureStorage or decoration sync.
namespace SsalddelApp.Services
{
    internal static class SecureStorage
    {
        public static UnavailableStorage Default { get; } = new();
        public sealed class UnavailableStorage
        {
            public Task<string?> GetAsync(string key) => throw new NotSupportedException("실제 기기 저장소는 시험 범위 밖입니다.");
            public Task SetAsync(string key, string value) => throw new NotSupportedException("실제 기기 저장소는 시험 범위 밖입니다.");
            public bool Remove(string key) => throw new NotSupportedException("실제 기기 저장소는 시험 범위 밖입니다.");
        }
    }
}
