using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Controllers.Common;
using Ssalddel.Controllers.Admin;
using Ssalddel.Domain.Community;
using Ssalddel.Services.Community;
using Ssalddel.Services.PrivacySupport;
using 살뜰.Services.Options;

namespace Ssalddel.Tests.Services.Community;

public sealed class NeighborhoodMemberDeliveryApiTests
{
    [Fact]
    public async Task 실제HTTP_SQLite는일반회원인증과다른계정격리및동일수락재요청을검증한다()
    {
        await using var f=await NeighborhoodMemberDeliveryTests.Fixture.CreateAsync(); var offer=await f.Ready();
        var builder=WebApplication.CreateEmptyBuilder(new WebApplicationOptions { EnvironmentName="Testing",ContentRootPath=AppContext.BaseDirectory });
        builder.WebHost.UseKestrel(k=>k.Listen(IPAddress.Loopback,0));
        builder.Services.AddLogging();builder.Services.AddRouting();builder.Services.AddHttpContextAccessor();
        builder.Services.AddControllers().ConfigureApplicationPartManager(m=> { m.ApplicationParts.Clear();m.FeatureProviders.Clear();m.FeatureProviders.Add(new OnlyControllers()); }).AddControllersAsServices();
        builder.Services.AddScoped(_=>f.OpenDb());
        builder.Services.AddScoped<ICurrentUserAccessor,HttpContextCurrentUserAccessor>();
        builder.Services.AddScoped(sp=>new NeighborhoodMemberDeliveryUseCase(sp.GetRequiredService<살뜰.Data.SsalddelContext>(),sp.GetRequiredService<ICurrentUserAccessor>(),
            Options.Create(new NeighborhoodMemberDeliveryOptions { Enabled=true }),new SsalddelExecutionModePolicy(Options.Create(new SsalddelExecutionOptions { Mode=SsalddelExecutionMode.Simulation })),
            f.Clock,new NeighborhoodMemberDeliveryTests.Encryption(),f.Consent,f.Routes,f.Storage));
        builder.Services.AddSingleton<I보호지원Store>(new NeighborhoodMemberDeliveryRecoveryTests.Cases());
        builder.Services.AddAuthentication("Fixture").AddScheme<AuthenticationSchemeOptions,Auth>("Fixture",_=>{});
        builder.Services.AddAuthorization(o=>o.AddPolicy("서버관리자전용",p=>p.RequireAuthenticatedUser().RequireRole("admin")));
        await using var app=builder.Build();app.UseRouting();app.UseAuthentication();app.UseAuthorization();app.MapControllers();
        using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(25));await app.StartAsync(deadline.Token);
        try
        {
            using var handler=new HttpClientHandler { UseProxy=false,AllowAutoRedirect=false };
            using var client=new HttpClient(handler){BaseAddress=new Uri(Assert.Single(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses))};
            var root="/"+NeighborhoodMemberDeliveryRoutes.Api;
            using var anon=await client.GetAsync(root+"/offers",deadline.Token);Assert.Equal(HttpStatusCode.Unauthorized,anon.StatusCode);
            client.DefaultRequestHeaders.Add("X-Member","member-a");
            using var scoped=await client.GetAsync(root+"/offers/"+offer.OfferId,deadline.Token);Assert.Equal(HttpStatusCode.OK,scoped.StatusCode);Assert.True(scoped.Headers.CacheControl!.NoStore);
            var command=f.Accept(offer);
            using var first=await client.PostAsJsonAsync(root+"/offers/"+offer.OfferId+"/accept",command,deadline.Token);
            Assert.Equal(HttpStatusCode.OK,first.StatusCode);var work=(await first.Content.ReadFromJsonAsync<NeighborhoodMemberWorkResponse>(deadline.Token))!;
            using var replay=await client.PostAsJsonAsync(root+"/offers/"+offer.OfferId+"/accept",command,deadline.Token);
            Assert.Equal(HttpStatusCode.OK,replay.StatusCode);Assert.Equal(work.Revision,(await replay.Content.ReadFromJsonAsync<NeighborhoodMemberWorkResponse>(deadline.Token))!.Revision);
            using var forbidden=await client.PostAsJsonAsync("/api/v1/admin/neighborhood-member-deliveries/request-1/recover",new NeighborhoodMemberRecoveryRequest(),deadline.Token);
            Assert.Equal(HttpStatusCode.Forbidden,forbidden.StatusCode);
            client.DefaultRequestHeaders.Remove("X-Member");client.DefaultRequestHeaders.Add("X-Member","other");
            using var wrong=await client.GetAsync(root+"/work/"+work.RequestId,deadline.Token);Assert.Equal(HttpStatusCode.NotFound,wrong.StatusCode);
            using var otherOffer=await client.GetAsync(root+"/offers/"+offer.OfferId,deadline.Token);Assert.Equal(HttpStatusCode.NotFound,otherOffer.StatusCode);
            await using var inspection=f.OpenDb();Assert.Equal(1,await inspection.Set<NeighborhoodMemberReceipt>().CountAsync(x=>x.Kind=="Accept"));
            Assert.Null((await inspection.운송원장.SingleAsync()).확정기사Id);
        }
        finally { using var stop=new CancellationTokenSource(TimeSpan.FromSeconds(5));await app.StopAsync(stop.Token); }
    }

    [Fact]
    public async Task 새migration은다섯테이블의전체열과키및기존큐기본값을모델에맞춘다()
    {
        await using var f=await NeighborhoodMemberDeliveryTests.Fixture.CreateAsync();
        var migration=new Ssalddel.Migrations.AddNeighborhoodMemberDelivery();
        // 최초 테이블과 후속 운영 알림의 keyset 조회 index를 함께 대조한다. 원 migration은 변경하지 않는다.
        var indexOperations=migration.UpOperations.Concat(new Ssalddel.Migrations.AddOperationsAlerts().UpOperations).OfType<CreateIndexOperation>().ToArray();
        var tables=migration.UpOperations.OfType<CreateTableOperation>().ToArray();Assert.Equal(5,tables.Length);
        var models=f.Db.Model.GetEntityTypes().Where(t=>t.ClrType.Namespace==typeof(NeighborhoodMemberDelivery).Namespace && t.GetTableName()!.StartsWith("neighborhood_member_")).ToArray();
        Assert.Equal(5,models.Length);
        foreach(var model in models)
        {
            var table=Assert.Single(tables,t=>t.Name==model.GetTableName());var identifier=StoreObjectIdentifier.Table(table.Name,null);
            Assert.Equal(model.GetProperties().Select(p=>p.GetColumnName(identifier)).Order(),table.Columns.Select(c=>c.Name).Order());
            Assert.Equal(model.FindPrimaryKey()!.Properties.Select(p=>p.GetColumnName(identifier)),table.PrimaryKey!.Columns);
            foreach(var index in model.GetIndexes())
                Assert.Contains(indexOperations,i=>i.Table==table.Name && i.Columns.SequenceEqual(index.Properties.Select(p=>p.GetColumnName(identifier))) && i.IsUnique==index.IsUnique);
        }
        var memberColumn=Assert.Single(migration.UpOperations.OfType<AddColumnOperation>(),x=>x.Name=="neighborhood_member_only");
        Assert.Equal("운송실행투영",memberColumn.Table);Assert.Equal(false,memberColumn.DefaultValue);Assert.False(memberColumn.IsNullable);
        Assert.Contains("neighborhood_member_offer",f.Db.Database.GenerateCreateScript());
        var snapshotType=typeof(Ssalddel.Migrations.AddNeighborhoodMemberDelivery).Assembly.GetType("Ssalddel.Migrations.SsalddelContextModelSnapshot")!;
        var snapshot=(Microsoft.EntityFrameworkCore.Infrastructure.ModelSnapshot)Activator.CreateInstance(snapshotType,nonPublic:true)!;
        foreach(var table in tables)
        {
            var frozen=Assert.Single(snapshot.Model.GetEntityTypes(),x=>x.GetTableName()==table.Name);
            var key=StoreObjectIdentifier.Table(table.Name,null);
            Assert.Equal(table.Columns.Select(c=>c.Name).Order(),frozen.GetProperties().Select(p=>p.GetColumnName(key)).Order());
            foreach(var column in table.Columns)
            {
                var property=Assert.Single(frozen.GetProperties(),p=>p.GetColumnName(key)==column.Name);
                Assert.Equal(column.IsNullable,property.IsNullable);Assert.Equal(column.ColumnType,property.GetColumnType());
            }
        }
    }

    private sealed class OnlyControllers : IApplicationFeatureProvider<ControllerFeature>
    { public void PopulateFeature(IEnumerable<ApplicationPart> parts,ControllerFeature feature) { feature.Controllers.Add(typeof(NeighborhoodMemberDeliveryController).GetTypeInfo());feature.Controllers.Add(typeof(NeighborhoodMemberDeliveryRecoveryController).GetTypeInfo()); } }
    public sealed class Auth(IOptionsMonitor<AuthenticationSchemeOptions> options,ILoggerFactory logger,UrlEncoder encoder):AuthenticationHandler<AuthenticationSchemeOptions>(options,logger,encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var actor=Request.Headers["X-Member"].ToString();
            if(string.IsNullOrWhiteSpace(actor))return Task.FromResult(AuthenticateResult.NoResult());
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,actor),new Claim(ClaimTypes.Role,"일반회원")],Scheme.Name)),Scheme.Name)));
        }
        protected override Task HandleChallengeAsync(AuthenticationProperties p){Response.StatusCode=401;return Task.CompletedTask;}
    }
}
