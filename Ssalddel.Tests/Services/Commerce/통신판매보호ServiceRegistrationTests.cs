using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using Ssalddel.Extensions;
using Ssalddel.Services.Commerce;
using Ssalddel.Services.Community;
using Ssalddel.Services.PrivacyRetention;
using Ssalddel.Services.PrivacySupport;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Options;

namespace Ssalddel.Tests.Services.Commerce;

public sealed class 통신판매보호ServiceRegistrationTests
{
    [Fact]
    public void 제품등록은Mongo원장과실제거래Guard를배치하고샘플저장소를등록하지않는다()
    {
        var services = new ServiceCollection(); var configuration = new ConfigurationBuilder().Build();
        services.Add통신판매보호Services(configuration).Add개인정보보존Services(configuration).Add거래개인정보지원Services(configuration);
        Assert.Equal(typeof(Mongo판매자확인Store), services.Single(x => x.ServiceType == typeof(I판매자확인Store)).ImplementationType);
        Assert.Equal(typeof(Mongo거래고지증적Store), services.Single(x => x.ServiceType == typeof(I거래고지증적Store)).ImplementationType);
        Assert.Equal(typeof(통신판매거래Guard), services.Single(x => x.ServiceType == typeof(I통신판매거래Guard)).ImplementationType);
        Assert.Equal(typeof(Mongo보호지원Store), services.Single(x => x.ServiceType == typeof(I보호지원Store)).ImplementationType);
        Assert.Equal(typeof(보호지원SourceResolver), services.Single(x => x.ServiceType == typeof(I보호지원SourceResolver)).ImplementationType);
        Assert.Equal(typeof(Mongo개인정보보존Store), services.Single(x => x.ServiceType == typeof(I개인정보보존Store)).ImplementationType);
        Assert.DoesNotContain(services, x => x.ServiceType == typeof(I보호지원권리실행확인));
    }

    [Fact]
    public async Task 제품Scope에서필수Guard원장을해결하고미연결운영거래를저장전에차단한다()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();
        services.AddLogging(); services.AddAuthorization(); services.AddHttpContextAccessor();
        services.Configure<MongoDbOptions>(x => x.Database = "wiring-no-network-database");
        services.AddSingleton<IMongoClient>(new MongoClient("mongodb://127.0.0.1:27017"));
        services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
        services.Configure<IsmsPProtectedDataOptions>(x => { x.Aes256GcmKeyBase64 = Convert.ToBase64String(new byte[32]); x.HashSalt = "wiring-only"; });
        services.AddSingleton<IIsmsPProtectedDataCryptoService, AesGcmIsmsPProtectedDataCryptoService>();
        services.AddSingleton<IPersonalDataEncryptionService, PassThrough>();
        services.Configure<SsalddelExecutionOptions>(x => x.Mode = SsalddelExecutionMode.Operational);
        services.AddSingleton<ISsalddelExecutionModePolicy, SsalddelExecutionModePolicy>();
        services.AddDbContext<SsalddelContext>(x => x.UseInMemoryDatabase("wiring-source-only"));
        services.AddScoped<I생활협업연결Query, NoCollaboration>();
        // 실제 보존 원장 graph는 별도 integration에서 검증합니다. 이 fixture의 외부 adapter는 쓰지 않는 실패 경계입니다.
        services.AddScoped<I개인정보보존조정Service, NoRetention>();
        services.Add통신판매보호Services(configuration).Add거래개인정보지원Services(configuration);
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scope = provider.CreateScope();
        var scoped = scope.ServiceProvider;
        Assert.IsType<Mongo판매자확인Store>(scoped.GetRequiredService<I판매자확인Store>());
        Assert.IsType<Mongo거래고지증적Store>(scoped.GetRequiredService<I거래고지증적Store>());
        Assert.IsType<Mongo보호지원Store>(scoped.GetRequiredService<I보호지원Store>());
        Assert.IsType<보호지원SourceResolver>(scoped.GetRequiredService<I보호지원SourceResolver>());
        Assert.NotNull(scoped.GetRequiredService<보호지원UseCase>());
        Assert.False(scoped.GetRequiredService<I거래신원확인Gateway>().IsConfigured);
        Assert.Contains(scoped.GetRequiredService<통신판매안내Service>().운영부족(), x => x.Contains("권리 요청"));
        var failure = await Assert.ThrowsAsync<거래보호Exception>(() => scoped.GetRequiredService<I통신판매거래Guard>()
            .요구Async("actor", null, null, "wiring-source", "request-1", default));
        Assert.Equal("CommerceOperationsNotReady", failure.Code);
    }

    private sealed class PassThrough : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
    private sealed class NoCollaboration : I생활협업연결Query
    {
        public Task<생활협업연결Context?> 조회Async(string stableId, CancellationToken ct = default)
            => Task.FromResult<생활협업연결Context?>(null);
    }
    private sealed class NoRetention : I개인정보보존조정Service
    {
        public Task 보존정지Async(string source, string record, string caseId, string reason, DateTime review, CancellationToken ct = default) => throw new NotSupportedException();
        public Task 보존정지해제Async(string source, string record, string caseId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Ssalddel.Contracts.Common.PrivacyRetention.개인정보파기결과Dto?> 결과Async(string source, string record, string owner, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<int> 실행Async(CancellationToken ct = default) => throw new NotSupportedException();
    }
}
