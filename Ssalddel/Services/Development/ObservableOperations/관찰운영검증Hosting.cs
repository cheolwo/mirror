using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Ssalddel.Application.WorldProjection;

namespace Ssalddel.Services.Development.ObservableOperations;

public static class 관찰운영검증Hosting
{
    public static void Add관찰운영검증(this WebApplicationBuilder builder)
    {
        var options = builder.Configuration.GetSection(관찰운영검증Options.Section)
            .Get<관찰운영검증Options>() ?? new 관찰운영검증Options();
        if (!options.Enabled) return;
        options.Validate(builder.Configuration, builder.Environment);

        // 이 호스트는 외부 알림·수집·지급 작업을 실행하지 않습니다.
        builder.Services.RemoveAll<IHostedService>();
        builder.Services.AddSingleton(options);
        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ObservableOperationsDefaultConnectionRequired");
        builder.Services.AddDbContextFactory<관찰운영검증DbContext>(db =>
            db.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 4, 0))));
        builder.Services.AddSingleton<Mongo관찰운영검증ProjectionStore>();
        builder.Services.AddSingleton<I관찰운영검증ProjectionWriter>(provider =>
            provider.GetRequiredService<Mongo관찰운영검증ProjectionStore>());
        builder.Services.Replace(ServiceDescriptor.Singleton<I관찰운영검증ProjectionReader>(provider =>
            provider.GetRequiredService<Mongo관찰운영검증ProjectionStore>()));
        builder.Services.AddSingleton<I관찰운영검증RunStateWriter, Redis관찰운영검증RunStateWriter>();
        builder.Services.AddSingleton<관찰운영검증Runner>();
        builder.Services.AddSingleton<I관찰운영검증UseCase, 관찰운영검증UseCase>();
        builder.Services.AddHostedService(provider => provider.GetRequiredService<관찰운영검증Runner>());
    }
}
