using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Ssalddel.Contracts.Common.Drivers;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Dispatch.Notification;
using 살뜰.Services.Notifications;
using 살뜰.Services.Storage.Local;

namespace Ssalddel.Tests.Services.Dispatch.Notification;

public sealed class 배차추천알림계약Tests
{
    [Theory]
    [InlineData("DriverDispatchRecommendation")]
    [InlineData("driverdispatchrecommendation")]
    [InlineData("DispatchRecommendation")]
    public void 현재와이전_푸시유형을_배차추천으로인식한다(string type)
    {
        Assert.True(기사배차추천알림계약.Is배차추천(type));
    }

    [Fact]
    public async Task 새_배차추천Outbox는_현재푸시유형을사용한다()
    {
        await using var db = CreateContext();
        var service = new 배차추천알림Service(
            db,
            new NoOpPushTokenStore(),
            new NoOpFcmPushService(),
            NullLogger<배차추천알림Service>.Instance,
            신규업무자격: Ssalddel.Tests.Support.Simulation운송자격Guard.Instance);

        await service.추천알림요청생성Async(
            배차대기Id: 10,
            의뢰Id: "REQUEST-PUSH-1",
            기사Id: "DRIVER-PUSH-1",
            추천라운드: 1);

        var outbox = Assert.Single(db.배차추천알림Outbox);
        using var document = JsonDocument.Parse(outbox.DataJson);
        Assert.Equal(
            기사배차추천알림계약.현재유형,
            document.RootElement.GetProperty("type").GetString());
    }

    private static SsalddelContext CreateContext()
        => new(
            new DbContextOptionsBuilder<SsalddelContext>()
                .UseInMemoryDatabase($"dispatch-push-contract-{Guid.NewGuid():N}")
                .Options,
            new DummyPersonalDataEncryptionService());

    private sealed class NoOpPushTokenStore : IDriverPushTokenStore
    {
        public Task SetAsync(string driverId, string pushToken, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<string?> GetAsync(string driverId, CancellationToken cancellationToken = default)
            => Task.FromResult<string?>(null);

        public Task ClearAsync(string driverId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class NoOpFcmPushService : IFcmPushService
    {
        public Task<bool> SendAsync(FcmPushMessage message, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task<bool> SendToTokenAsync(
            string token,
            string title,
            string body,
            IReadOnlyDictionary<string, string> data,
            CancellationToken cancellationToken = default)
            => Task.FromResult(true);
    }

    private sealed class DummyPersonalDataEncryptionService : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;

        public string? Unprotect(string? value) => value;
    }
}
