using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Ssalddel.Domain.AgriculturalFisheries;
using Ssalddel.Infrastructure.Persistence.AgriculturalFisheries;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Simulation.Persistence;
using Ssalddel.Simulation.Hosting;

namespace Ssalddel.Simulation.Tests;

[Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
    Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E3,
    "Simulation·Unity 계약과 결정성 및 회귀 증거를 검증한다.",
    Boundary = "자동 시험 통과와 실제 Play Mode·Game View·E 승격 증거를 구분한다.")]
public sealed class SimulationSharedPublicDataTests
{
    [Fact]
    public async Task 공공데이터_전용Context는_저장을_거부한다()
    {
        var options = new DbContextOptionsBuilder<AgriculturalFisheriesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .AddInterceptors(new AgriculturalFisheriesReadOnlySaveChangesInterceptor())
            .Options;
        await using var context = new AgriculturalFisheriesDbContext(options);
        context.KamisPriceObservations.Add(CreateObservation(1, "감자", "2026-08-11"));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => context.SaveChangesAsync());

        Assert.Equal(
            AgriculturalFisheriesReadOnlySaveChangesInterceptor.ErrorCode,
            error.Message);
    }

    [Fact]
    public async Task KAMIS조회는_품목을_제한하고_추적하지_않는다()
    {
        var options = new DbContextOptionsBuilder<AgriculturalFisheriesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        await using var context = new AgriculturalFisheriesDbContext(options);
        context.KamisPriceObservations.AddRange(
            CreateObservation(1, "감자", "2026-08-10"),
            CreateObservation(2, "감자", "2026-08-11"),
            CreateObservation(3, "양파", "2026-08-12"));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var reader = new Simulation공유공공데이터Reader(
            context,
            Options.Create(new SimulationSharedPublicDataQueryOptions { MaxItems = 50 }));

        var result = await reader.Kamis가격관측조회Async(
            " 감자 ",
            1,
            CancellationToken.None);

        var item = Assert.Single(result.Items);
        Assert.Equal("public-data:kamis:kamis-2", item.StableId);
        Assert.Equal("2026-08-11", item.SurveyDate);
        Assert.Equal("감자", item.ItemName);
        Assert.Equal("KAMIS", result.SourceCode);
        Assert.Equal("SharedOperationalPublicDataDatabaseReadOnly", result.BoundaryCode);
        Assert.Equal(
            new DateTimeOffset(2026, 8, 12, 0, 0, 0, TimeSpan.Zero),
            result.ReferenceTimeUtc);
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task KAMIS관측이없으면_기준시각을임의의현재시각으로만들지않는다()
    {
        var options = new DbContextOptionsBuilder<AgriculturalFisheriesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        await using var context = new AgriculturalFisheriesDbContext(options);
        var reader = new Simulation공유공공데이터Reader(
            context,
            Options.Create(new SimulationSharedPublicDataQueryOptions { MaxItems = 50 }));

        var result = await reader.Kamis가격관측조회Async(
            "존재하지 않는 품목",
            20,
            CancellationToken.None);

        Assert.Empty(result.Items);
        Assert.Null(result.ReferenceTimeUtc);
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task 공유공공데이터가_비활성화되면_조회경로는_503을_반환한다()
    {
        using var factory = CreateFactory(sharedPublicDataEnabled: false);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            SimulationSharedPublicDataRoutes.KamisPriceObservations);
        var error = await response.Content.ReadFromJsonAsync<SimulationErrorResponse>();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.NotNull(error);
        Assert.Equal(DisabledSimulation공유공공데이터Reader.ErrorCode, error.ErrorCode);
    }

    [Fact]
    public async Task 공유공공데이터_조회경로는_읽기결과를_반환한다()
    {
        using var factory = CreateFactory(
            sharedPublicDataEnabled: false,
            reader: new FixtureReader());
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            SimulationSharedPublicDataRoutes.KamisPriceObservations
            + "?itemName=%EA%B0%90%EC%9E%90&limit=1");
        var result = await response.Content
            .ReadFromJsonAsync<Simulation공유공공데이터조회결과>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(result);
        Assert.Equal("감자", Assert.Single(result.Items).ItemName);
    }

    [Fact]
    public void 활성화된_공유공공데이터에_연결문자열이없으면_시작구성을거부한다()
    {
        var configuration = Configuration(new Dictionary<string, string?>
        {
            ["SimulationSharedPublicData:Enabled"] = "true",
            ["SimulationSharedPublicData:ConnectionStringName"] = "SharedPublicData",
        });
        var services = new ServiceCollection();

        var error = Assert.Throws<InvalidOperationException>(
            () => services.AddSsalddelSimulationModule(configuration));

        Assert.Equal(
            SsalddelSimulationHostingServiceCollectionExtensions.ConnectionStringMissingErrorCode,
            error.Message);
    }

    [Fact]
    public void 연결문자열Fallback은_설정에서명시한경우에만사용한다()
    {
        var configuration = Configuration(new Dictionary<string, string?>
        {
            ["SimulationSharedPublicData:Enabled"] = "true",
            ["SimulationSharedPublicData:ConnectionStringName"] = "SharedPublicData",
            ["SimulationSharedPublicData:FallbackConnectionStringName"] = "DefaultConnection",
            ["ConnectionStrings:DefaultConnection"] =
                "Server=localhost;Database=test;User=test;Password=test;",
        });
        var services = new ServiceCollection();

        services.AddSsalddelSimulationModule(configuration);

        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(ISimulation공유공공데이터조회Port)
            && descriptor.ImplementationType == typeof(Simulation공유공공데이터Reader));
    }

    [Fact]
    public void 최대조회건수_설정범위를벗어나면_Options검증에서거부한다()
    {
        var configuration = Configuration(new Dictionary<string, string?>
        {
            ["SimulationSharedPublicData:Enabled"] = "false",
            ["SimulationSharedPublicData:MaxItems"] = "0",
        });
        var services = new ServiceCollection();
        services.AddSsalddelSimulationModule(configuration);
        using var provider = services.BuildServiceProvider();

        Assert.Throws<OptionsValidationException>(() => provider
            .GetRequiredService<IOptions<SimulationSharedPublicDataOptions>>()
            .Value);
    }

    private static WebApplicationFactory<Program> CreateFactory(
        bool sharedPublicDataEnabled,
        ISimulation공유공공데이터조회Port? reader = null)
        => new SimulationWebApplicationFactory()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.ConfigureAppConfiguration((_, configuration) =>
                {
                    configuration.AddInMemoryCollection(
                        new Dictionary<string, string?>
                        {
                            ["SsalddelExecution:Mode"] = "Operational",
                            ["SsalddelSimulation:AllowUnauthenticatedTesting"] = "true",
                            ["SimulationSharedPublicData:Enabled"] =
                                sharedPublicDataEnabled.ToString(),
                        });
                });

                if (reader is not null)
                {
                    builder.ConfigureTestServices(services =>
                    {
                        services.RemoveAll<ISimulation공유공공데이터조회Port>();
                        services.AddSingleton(reader);
                    });
                }
            });

    private static IConfiguration Configuration(
        IReadOnlyDictionary<string, string?> values)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

    private static KamisPriceObservation CreateObservation(
        long id,
        string itemName,
        string surveyDate)
        => new()
        {
            Id = id,
            FirstCollectionRunId = 1,
            RecordKey = "kamis-" + id,
            ProductClassCode = "01",
            ProductClassName = "소매",
            CategoryCode = "100",
            CategoryName = "식량작물",
            CountryCode = "KR",
            CountryName = "대한민국",
            RequestedDate = DateOnly.Parse(surveyDate),
            SurveyDate = DateOnly.Parse(surveyDate),
            ItemName = itemName,
            ItemCode = id.ToString(),
            KindName = "대표 품종",
            KindCode = "01",
            RankName = "상품",
            RankCode = "01",
            Unit = "1kg",
            SourcePackageLabel = "KAMIS 가격정보",
            ComparisonUnit = "1kg",
            PriceNormalizationCode = "PerKilogram",
            PriceNormalizationBasis = "테스트",
            PriceRaw = "1000",
            PriceKrw = 1000 + id,
            SourceUrl = "https://www.kamis.or.kr/",
            LastSeenAtUtc = new DateTime(2026, 8, 12, 0, 0, 0, DateTimeKind.Utc),
        };

    private sealed class FixtureReader : ISimulation공유공공데이터조회Port
    {
        public Task<Simulation공유공공데이터조회결과> Kamis가격관측조회Async(
            string? itemName,
            int limit,
            CancellationToken cancellationToken)
            => Task.FromResult(new Simulation공유공공데이터조회결과
            {
                ReferenceTimeUtc = DateTimeOffset.UtcNow,
                Items = new[]
                {
                    new SimulationKamis가격관측
                    {
                        StableId = "public-data:kamis:fixture-potato",
                        SurveyDate = "2026-08-12",
                        ItemName = itemName ?? "감자",
                        Unit = "1kg",
                        PriceKrw = 1000,
                        SourcePackageLabel = "KAMIS 가격정보",
                        SourceUrl = "https://www.kamis.or.kr/",
                    },
                },
            });
    }
}
