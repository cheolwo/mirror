using System.Security.Cryptography;
using FluentResults;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using Ssalddel.Application.Security;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Infrastructure.Storage.Memory;
using Ssalddel.Services.Community;
using Ssalddel.Services.Security;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.ViewModels;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Options;

namespace NeighborhoodExchangePreview;

// 루프백·세 개의 명시적 가상 계정 전용. 운영 로그인·외부 배차·송금의 증거가 아닙니다.
internal static class CollaborationPreview
{
    public static void AddCollaborationPreview(this WebApplicationBuilder builder, string evidence)
    {
        builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(evidence, "private-keys"))).SetApplicationName("NeighborhoodCollaborationPreview");
        builder.Services.AddSingleton<IPersonalDataEncryptionService, DataProtectionPersonalDataEncryptionService>();
        var dbNamePath = Path.Combine(evidence, "mongo-database.txt");
        if (!File.Exists(dbNamePath) || File.ReadAllText(dbNamePath).Trim().Length > 63)
            File.WriteAllText(dbNamePath, "neighborhood_preview_" + Guid.NewGuid().ToString("N"));
        var connection = Environment.GetEnvironmentVariable("SSALDDEL_PREVIEW_MONGO_CONNECTION") ?? builder.Configuration["MongoDb:ConnectionString"];
        if (string.IsNullOrWhiteSpace(connection)) connection = "mongodb://127.0.0.1:27017/?serverSelectionTimeoutMS=5000";
        if (MongoUrl.Create(connection).Servers.Any(server => server.Host is not ("127.0.0.1" or "localhost" or "::1")))
            throw new InvalidOperationException("The collaboration preview requires a loopback Mongo server.");
        builder.Services.Configure<MongoDbOptions>(options => { options.Database = File.ReadAllText(dbNamePath).Trim(); options.ConnectionString = connection; });
        builder.Services.AddSingleton<IMongoClient>(_ => new MongoClient(connection));
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddScoped<I생활협업Store, Mongo생활협업Store>();
        builder.Services.AddScoped<I생활협업보관예약Guard, 생활협업보관예약Guard>();
        builder.Services.AddScoped<생활협업UseCase>();
        builder.Services.AddScoped<I생활협업UseCase>(sp => sp.GetRequiredService<생활협업UseCase>());
        builder.Services.AddScoped<I생활협업연결Query>(sp => sp.GetRequiredService<생활협업UseCase>());
        builder.Services.AddScoped<I생활협업배송Source, Ef생활협업배송Source>();
        builder.Services.AddScoped<I생활협업공간Source, 생활협업공간Source>();
        builder.Services.AddScoped<I생활협업보관상태Source, 생활협업보관상태Source>();
        builder.Services.AddScoped<I생활보관공간Store, Mongo생활보관공간Store>();
        builder.Services.AddScoped<생활보관인계정보Protection>();
        builder.Services.AddScoped<I생활보관공간Service, 생활보관공간Service>();
        builder.Services.AddScoped<I생활보관협업ContextSource, 생활보관협업ContextSource>();
        builder.Services.AddScoped<ISsalddel현재사용자Context>(sp => sp.GetRequiredService<AnonymousPreviewUser>());
        builder.Services.AddScoped<INeighborhoodCollaborationClient, NeighborhoodCollaborationClient>();
        builder.Services.AddScoped<INeighborhoodStorageClient, NeighborhoodStorageClient>();
        builder.Services.AddScoped<NeighborhoodCollaborationDraftSession>();
        builder.Services.AddScoped<NeighborhoodStorageDraftSession>();
        builder.Services.AddTransient<NeighborhoodCollaborationAuthoringViewModel>();
        builder.Services.AddTransient<NeighborhoodCollaborationQueryViewModel>();
        builder.Services.AddTransient<NeighborhoodCollaborationSourceViewModel>();
        builder.Services.AddTransient<NeighborhoodStorageAuthoringViewModel>();
        builder.Services.AddTransient<NeighborhoodStorageQueryViewModel>();
        using var rsa = RSA.Create(2048);
        var privateKey = rsa.ExportPkcs8PrivateKeyPem(); var publicKey = rsa.ExportSubjectPublicKeyInfoPem();
        builder.Services.Configure<IsmsPProtectedDataOptions>(o => { o.TransportKeyId = "local-preview"; o.TransportPrivateKeyPem = privateKey; o.TransportPublicKeyPem = publicKey; });
        builder.Services.AddSingleton<IIsmsPClientTransportProtectionService, RsaOaepAesGcmClientTransportProtectionService>();
        builder.Services.AddSingleton<IIsmsPTransportKeyStatusStore, InMemoryIsmsPTransportKeyStatusStore>();
        builder.Services.AddScoped<IISMSP전송보호UseCase, ISMSP전송보호UseCase>();
    }

    public static void MapCollaborationPreview(this WebApplication app)
    {
        app.MapGet("/preview/account/{account}", (string account, HttpContext context) =>
        {
            if (account is not ("owner" or "requester" or "helper" or "driver-a" or "driver-b" or "anonymous")) return Results.NotFound();
            context.Response.Cookies.Append("preview-account", account, new() { HttpOnly = true, SameSite = SameSiteMode.Lax, Path = "/" });
            return Results.Redirect("/community/map");
        });
        app.MapGet("/api/v1/security/isms-p/transport/public-key", async (IISMSP전송보호UseCase service, CancellationToken ct) => Results.Json(await service.공개키발급Async(ct)));
        var c = app.MapGroup("/" + NeighborhoodCollaborationRoutes.Api).AddEndpointFilter<PrivateAccountFilter>();
        c.MapPost("", async (NeighborhoodCollaborationCreateRequest r, I생활협업UseCase s, CancellationToken ct) => Json(await s.신청Async(r, ct)));
        c.MapGet("/mine", async (string? scope, int? page, I생활협업UseCase s, CancellationToken ct) => Results.Json(await s.내목록Async(scope ?? "all", page ?? 1, ct)));
        c.MapGet("/requests/{requestId:guid}", async (Guid requestId, string? stableId, I생활협업UseCase s, CancellationToken ct) => Read(await s.요청결과Async(requestId, stableId, ct)));
        c.MapGet("/{id}", async (string id, I생활협업UseCase s, CancellationToken ct) => Read(await s.상세Async(id, ct)));
        c.MapPost("/{id}/commands", async (string id, NeighborhoodCollaborationCommandRequest r, I생활협업UseCase s, CancellationToken ct) => Json(await s.변경Async(id, r, ct)));
        app.MapGet("/" + NeighborhoodCollaborationRoutes.Api + "/opportunities", async (long sourcePostId, I생활협업UseCase s, CancellationToken ct) => Results.Json(await s.참여기회Async(sourcePostId, ct)));
        app.MapGet("/" + NeighborhoodCollaborationRoutes.Api + "/public-history", async (long sourcePostId, I생활협업UseCase s, CancellationToken ct) => Results.Json(await s.공개이력Async(sourcePostId, ct)));
        var path = "/" + NeighborhoodStorageRoutes.Api;
        app.MapGet(path, async (string? publicNeighborhoodRegionKey, int? page, I생활보관공간Service s, CancellationToken ct) => Results.Json(await s.공개목록Async(publicNeighborhoodRegionKey, page ?? 1, ct)));
        app.MapGet(path + "/map", async (I생활보관공간Service s, CancellationToken ct) => Results.Json(await s.공개지도Async(ct)));
        app.MapGet(path + "/{id}", async (string id, I생활보관공간Service s, CancellationToken ct) => Read(await s.공개상세Async(id, ct)));
        var storage = app.MapGroup(path).AddEndpointFilter<PrivateAccountFilter>();
        storage.MapPost("", async (NeighborhoodStorageSpaceRequest r, I생활보관공간Service s, CancellationToken ct) => Json(await s.등록Async(r, ct)));
        storage.MapGet("/mine", async (int? page, I생활보관공간Service s, CancellationToken ct) => Results.Json(await s.내목록Async(page ?? 1, ct)));
        storage.MapGet("/requests/{requestId:guid}", async (Guid requestId, string? spaceId, string? collaborationId, I생활보관공간Service s, CancellationToken ct) => Read(await s.요청결과Async(requestId, spaceId, collaborationId, ct)));
        storage.MapGet("/{id}/private", async (string id, string? collaborationId, I생활보관공간Service s, CancellationToken ct) => Read(await s.비공개상세Async(id, collaborationId, ct)));
        storage.MapPost("/{id}/update", async (string id, NeighborhoodStorageSpaceRequest r, I생활보관공간Service s, CancellationToken ct) => Json(await s.수정Async(id, r, ct)));
        foreach (var action in new[] { "publish", "pause", "close" })
        {
            var status = action == "publish" ? NeighborhoodStorageStatus.Published : action == "pause" ? NeighborhoodStorageStatus.Paused : NeighborhoodStorageStatus.Closed;
            storage.MapPost("/{id}/" + action, async (string id, NeighborhoodStorageMutationRequest r, I생활보관공간Service s, CancellationToken ct) => Json(await s.상태변경Async(id, status, r, ct)));
        }
        storage.MapPost("/{id}/reservations", async (string id, NeighborhoodStorageReservationRequest r, I생활보관공간Service s, CancellationToken ct) => Json(await s.예약Async(id, r, ct)));
        storage.MapPost("/{id}/reservations/{collaborationId}/action", async (string id, string collaborationId, NeighborhoodStorageReservationActionRequest r, I생활보관공간Service s, CancellationToken ct) => Json(await s.예약변경Async(id, collaborationId, r, ct)));
    }

    private static IResult Read<T>(T? value) => value is null ? Results.NotFound() : Results.Json(value);
    private static IResult Json<T>(Result<T> result)
    {
        if (result.IsSuccess) return Results.Json(result.Value);
        var error = result.Errors.FirstOrDefault(); var status = error?.Metadata.GetValueOrDefault("StatusCode") as int? ?? 400;
        return Results.Problem(statusCode: status, detail: error?.Message, extensions: new Dictionary<string, object?> { ["errorCode"] = error?.Metadata.GetValueOrDefault("ErrorCode") });
    }
    private sealed class PrivateAccountFilter : IEndpointFilter
    {
        public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
        {
            context.HttpContext.Response.Headers.CacheControl = "private, no-store";
            if (context.HttpContext.RequestServices.GetRequiredService<AnonymousPreviewUser>().UserId is null) return Results.Unauthorized();
            return await next(context);
        }
    }
}
