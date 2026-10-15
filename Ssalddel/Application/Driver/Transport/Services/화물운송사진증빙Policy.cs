using System.Security.Cryptography;
using System.Text.Json.Nodes;
using FluentResults;
using Microsoft.EntityFrameworkCore;
using SkiaSharp;
using Ssalddel.Services.Storage;
using 살뜰.Data;
using 살뜰.도메인.운송;

namespace Ssalddel.Application.Driver.Transport;

/// <summary>일반 화물 사진의 서버 업로드 영수증과 현재 기사·운송·단계를 결속한다. 과거 경로 문자열은 업로드 증거가 아니다.</summary>
public static class 화물운송사진증빙Policy
{
    public const int MaximumBytes = 5 * 1024 * 1024;
    public const string UploadReceiptKind = "cargo-photo-upload-receipt-v1";

    public static string? Kind(string? commandName) => commandName?.Trim() switch
    {
        "TransportPickupComplete" or "운송상차완료Command" => "pickup-complete-photo",
        "TransportDropoffComplete" or "운송인수완료Command" => "dropoff-complete-photo",
        _ => null
    };

    public static bool CanUpload(운송원장 transport, string kind) => transport.상태 == (kind == "pickup-complete-photo"
        ? 기사운송상태코드.상차지도착 : 기사운송상태코드.하차지도착);

    public static bool IsPhoto(byte[] bytes)
    {
        if (bytes.Length is <= 0 or > MaximumBytes) return false;
        using var stream = new SKMemoryStream(bytes);
        using var codec = SKCodec.Create(stream);
        return codec is not null && codec.EncodedFormat is SKEncodedImageFormat.Jpeg or SKEncodedImageFormat.Png
            && codec.Info.Width is > 0 and <= 12000 && codec.Info.Height is > 0 and <= 12000
            && (long)codec.Info.Width * codec.Info.Height <= 40_000_000;
    }

    public static string Sha(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    public static string ClientAttachments(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return json ?? "";
        try
        {
            var entries = Read(json);
            var visible = new JsonArray();
            foreach (var entry in entries)
            {
                if (entry is JsonObject item && item["kind"] is JsonValue kind
                    && kind.TryGetValue<string>(out var name) && name == UploadReceiptKind) continue;
                visible.Add(entry?.DeepClone());
            }
            if (visible.Count == entries.Count) return json;
            return visible.Count == 0 ? "" : visible.ToJsonString();
        }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or InvalidOperationException or FormatException)
        {
            // 구형 일반 화물의 비 JSON 첨부 원문은 유지한다. 신규 내부 영수증이 섞인 손상 자료는 노출하지 않는다.
            return json.Contains(UploadReceiptKind, StringComparison.Ordinal) ? "" : json;
        }
    }

    public static string? CompletedPhotoDigest(운송원장 transport)
    {
        try
        {
            var entries = Read(transport.첨부_json).OfType<JsonObject>().ToArray();
            var completed = entries.LastOrDefault(x => Text(x, "kind") == "dropoff-complete-photo"
                && Text(x, "uploadedBy") == transport.기사_운송자);
            var requestId = string.IsNullOrWhiteSpace(transport.의뢰Id) ? transport.운송번호 : transport.의뢰Id;
            var receipt = entries.LastOrDefault(x => Text(x, "kind") == UploadReceiptKind
                && Text(x, "photoKind") == "dropoff-complete-photo" && Text(x, "uploadedBy") == transport.기사_운송자
                && x["transportId"]?.GetValue<long>() == transport.Id && Text(x, "requestId") == requestId
                && x["recommendationRound"]?.GetValue<int>() == transport.추천라운드
                && Text(x, "objectName") == (completed is null ? "" : Text(completed, "objectName")));
            if (completed is null || receipt is null || Text(receipt, "sha256").Length != 64) return null;
            return Sha(System.Text.Encoding.UTF8.GetBytes(string.Join("\n", transport.Id, requestId, transport.기사_운송자,
                transport.추천라운드, Text(receipt, "objectName"), Text(receipt, "sha256"), receipt["bytes"]?.GetValue<long>())));
        }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or InvalidOperationException or FormatException)
        { return null; }
    }

    public static void RecordUpload(운송원장 transport, string actor, string kind,
        ObjectStorageUploadResult uploaded, byte[] bytes)
    {
        var entries = Read(transport.첨부_json);
        entries.Add(new JsonObject
        {
            ["kind"] = UploadReceiptKind, ["photoKind"] = kind, ["transportId"] = transport.Id,
            ["requestId"] = string.IsNullOrWhiteSpace(transport.의뢰Id) ? transport.운송번호 : transport.의뢰Id,
            ["recommendationRound"] = transport.추천라운드,
            ["uploadedBy"] = actor, ["containerName"] = uploaded.ContainerName,
            ["objectName"] = uploaded.ObjectName, ["url"] = uploaded.Url,
            ["sha256"] = Sha(bytes), ["bytes"] = bytes.LongLength,
            ["recordedAtUtc"] = DateTime.UtcNow
        });
        transport.첨부_json = entries.ToJsonString();
    }

    public static async Task<Result<화물운송확인사진>> VerifyAsync(SsalddelContext? db, IObjectStorageService? storage,
        long id, string actor, string kind, string? objectName, CancellationToken ct)
    {
        if (db is null)
            return Failure("CargoPhotoStorageUnavailable", "비공개 사진 저장소를 확인한 뒤 다시 시도해 주세요.", 503);
        var transport = await db.운송원장.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id && x.기사_운송자 == actor, ct);
        if (transport is null) return Failure("CargoPhotoBindingMismatch", "현재 배정된 운송의 사진을 다시 확인해 주세요.");
        // 전용 업무의 진행 차단을 일반 사진 오류로 숨기지 않는다. 해당 업무의 상태는 바꾸지 않는다.
        if (살뜰.Services.Dispatch.Engine.운송의뢰배차원천유형.Is살뜰마트음식주문(transport.원본의뢰유형))
            return Failure("MartDriverWorkflowRequired", "살뜰마트 배송 화면에서 현재 전표와 배송 상태를 확인해 주세요.");
        var requestId = string.IsNullOrWhiteSpace(transport.의뢰Id) ? transport.운송번호 : transport.의뢰Id;
        if (!string.IsNullOrWhiteSpace(requestId) && await db.출고예정.AsNoTracking()
            .Where(x => x.운송의뢰Id == requestId && x.입고상품Id != null)
            .Join(db.입고상품.AsNoTracking(), x => x.입고상품Id!.Value, x => x.Id, (_, item) => item)
            .AnyAsync(x => x.보관위치.StartsWith("DLOC-"), ct))
            return Result.Fail<화물운송확인사진>(new Error("분산 물품은 현재 분산 순회 화면에서 피킹·전달을 확인해 주세요.")
                .WithMetadata("Code", "DistributedCargoCustodyRequired").WithMetadata("ErrorCode", "DistributedCargoCustodyRequired")
                .WithMetadata("StatusCode", 409));
        if (storage is null || !storage.IsConfigured(ObjectStorageAccess.Private))
            return Failure("CargoPhotoStorageUnavailable", "비공개 사진 저장소를 확인한 뒤 다시 시도해 주세요.", 503);
        JsonObject? receipt;
        try
        {
            receipt = Read(transport.첨부_json).OfType<JsonObject>().LastOrDefault(x => Text(x, "kind") == UploadReceiptKind
                && Text(x, "photoKind") == kind && Text(x, "uploadedBy") == actor
                && Text(x, "objectName") == objectName?.Trim() && x["transportId"]?.GetValue<long>() == id
                && Text(x, "requestId") == requestId && x["recommendationRound"]?.GetValue<int>() == transport.추천라운드);
        }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or InvalidOperationException or FormatException)
        { return Failure("CargoPhotoReceiptInvalid", "사진 업로드 확인 기록을 읽지 못했습니다. 다시 촬영·업로드해 주세요."); }
        if (receipt is null)
            return Failure("CargoPhotoUploadReceiptRequired", "현재 운송의 사진 촬영·업로드 확인이 필요합니다. 이전 사진 경로만으로 완료할 수 없습니다.");
        try
        {
            var bytes = await storage.DownloadAsync(Text(receipt, "containerName"), Text(receipt, "objectName"), ct);
            if (!IsPhoto(bytes) || bytes.LongLength != receipt["bytes"]?.GetValue<long>() || Sha(bytes) != Text(receipt, "sha256"))
                return Failure("CargoPhotoIntegrityMismatch", "업로드한 사진과 저장된 사진이 다릅니다. 다시 촬영·업로드해 주세요.");
            return Result.Ok(new 화물운송확인사진(Text(receipt, "objectName"), Text(receipt, "url")));
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception)
        { return Failure("CargoPhotoStorageVerificationPending", "사진 저장 결과를 확인하지 못했습니다. 완료 처리 전에 다시 확인해 주세요.", 503); }
    }

    private static JsonArray Read(string? json) => string.IsNullOrWhiteSpace(json) ? []
        : JsonNode.Parse(json) as JsonArray ?? throw new InvalidOperationException("CargoPhotoReceiptInvalid");
    private static string Text(JsonObject node, string name) => node[name]?.GetValue<string>() ?? "";
    private static Result<화물운송확인사진> Failure(string code, string message, int status = 409)
        => Result.Fail<화물운송확인사진>(new Error(message).WithMetadata("ErrorCode", code).WithMetadata("StatusCode", status));
}

public sealed record 화물운송확인사진(string ObjectName, string Url);
