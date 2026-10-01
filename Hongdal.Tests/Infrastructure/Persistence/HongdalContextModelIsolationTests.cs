using Hongdal.Domain.AgriculturalFisheries;
using Hongdal.Domain.Community;
using Hongdal.Domain.TraditionalMarkets;
using Microsoft.EntityFrameworkCore;
using 홍달.Data;
using 홍달.Infrastructure.Security;

namespace Hongdal.Tests.Infrastructure.Persistence;

public sealed class HongdalContextModelIsolationTests
{
    [Fact]
    public void MainContext_ExcludesEntitiesOwnedByDedicatedContexts()
    {
        var options = new DbContextOptionsBuilder<HongdalContext>()
            .UseInMemoryDatabase($"hongdal-context-isolation-{Guid.NewGuid():N}")
            .Options;
        using var context = new HongdalContext(
            options,
            new DummyPersonalDataEncryptionService());

        Assert.NotNull(context.Model.FindEntityType(typeof(PlatformCommunityPost)));
        Assert.Null(context.Model.FindEntityType(typeof(KamisPriceObservation)));
        Assert.Null(context.Model.FindEntityType(typeof(UsdaNassPriceObservation)));
        Assert.Null(context.Model.FindEntityType(typeof(TraditionalMarket)));
        Assert.Null(context.Model.FindEntityType(typeof(TraditionalMarketSyncRun)));
    }

    private sealed class DummyPersonalDataEncryptionService : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;

        public string? Unprotect(string? value) => value;
    }
}
