using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using FluentResults;
using Microsoft.Extensions.Options;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.Metadata;
using 살뜰.Services.Options;

namespace Ssalddel.Services.Community;

public interface I화물정보공고UseCase
{
    Task<Result<IReadOnlyList<CargoNoticeRegion>>> 지역목록Async(CancellationToken ct = default);
    Task<Result<CargoNoticeListResponse>> 공개목록Async(int page = 1, CancellationToken ct = default);
    Task<Result<CargoNoticePublicDto>> 공개상세Async(string noticeId, CancellationToken ct = default);
    Task<Result<CargoNoticeOwnerListResponse>> 내목록Async(int page = 1, CancellationToken ct = default);
    Task<Result<CargoNoticeOwnerDto>> 내상세Async(string noticeId, CancellationToken ct = default);
    Task<Result<CargoNoticeCommandResponse>> 요청결과Async(Guid requestId, string? noticeId = null, CancellationToken ct = default);
    Task<Result<CargoNoticeCommandResponse>> 초안생성Async(CargoNoticeDraftRequest request, CancellationToken ct = default);
    Task<Result<CargoNoticeCommandResponse>> 수정Async(string noticeId, CargoNoticeDraftRequest request, CancellationToken ct = default);
    Task<Result<CargoNoticeCommandResponse>> 공개Async(string noticeId, CargoNoticePublishRequest request, CancellationToken ct = default);
    Task<Result<CargoNoticeCommandResponse>> 철회Async(string noticeId, CargoNoticeMutationRequest request, CancellationToken ct = default);
}

[SsalddelCommunityV0Module(SsalddelCommunityV0ModuleKeys.Content, SsalddelModuleKind.Application,
    "인증한 작성자의 독립 화물 정보 공고 초안·공개 동의·만료·철회와 원 요청 결과",
    ReleaseStage = SsalddelCommunityV0ReleaseStages.Persistence,
    Boundary = "공고만 변경합니다. 실제 운송 원장·OS 인계·추천·배차·금융·외부 알림·일반 게시 발행을 호출하지 않습니다.")]
public sealed class 화물정보공고UseCase(I화물정보공고Store store, ICurrentUserAccessor currentUser,
    I생활교류공개지역Source regions, IOptions<화물정보공고Options> options, TimeProvider? clock = null) : I화물정보공고UseCase
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private const int PageSize = 20;
    private const int MaximumOperations = 256;
    private static readonly Regex PrivateText = new(
        @"(?:\+?82[-.\s]?)?0?1[016789][-.\s]?\d{3,4}[-.\s]?\d{4}|0[2-6]\d?[-.\s]?\d{3,4}[-.\s]?\d{4}|\d{6}[-\s]?[1-4]\d{6}|[^\s@]+@[^\s@]+\.[^\s@]+|https?://|www\.|[가-힣A-Za-z0-9]+(?:대로|로|길)\s*\d+|\d+\s*(?:동|호|층)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    private DateTime Now => (clock ?? TimeProvider.System).GetUtcNow().UtcDateTime;
    private string? Actor => string.IsNullOrWhiteSpace(currentUser.UserId) ? null : currentUser.UserId.Trim();

    public async Task<Result<IReadOnlyList<CargoNoticeRegion>>> 지역목록Async(CancellationToken ct = default)
    {
        if (Disabled<IReadOnlyList<CargoNoticeRegion>>() is { } disabled) return disabled;
        var directory = await DirectoryAsync(ct);
        return Result.Ok<IReadOnlyList<CargoNoticeRegion>>(directory.Select(x => new CargoNoticeRegion
            { RegionKey = x.RegionKey, DisplayName = x.DisplayName }).ToArray());
    }

    public async Task<Result<CargoNoticeListResponse>> 공개목록Async(int page = 1, CancellationToken ct = default)
    {
        if (Disabled<CargoNoticeListResponse>() is { } disabled) return disabled;
        if (page < 1 || page > 100_000) return Fail<CargoNoticeListResponse>("InvalidInput", "조회 페이지를 확인해 주세요.");
        var directory = await DirectoryAsync(ct);
        var now = Now;
        var found = await store.목록Async(null, true, directory.Select(x => x.RegionKey).ToArray(), now, page, PageSize, ct);
        return Result.Ok(new CargoNoticeListResponse { Page = page, TotalCount = found.Total,
            Items = found.Items.Where(x => IsPublic(x, now)).Select(x => Public(x, directory)).ToArray() });
    }

    public async Task<Result<CargoNoticePublicDto>> 공개상세Async(string noticeId, CancellationToken ct = default)
    {
        if (Disabled<CargoNoticePublicDto>() is { } disabled) return disabled;
        var row = ValidId(noticeId) ? await store.조회Async(noticeId, ct) : null;
        if (row is null || !IsPublic(row, Now)) return Missing<CargoNoticePublicDto>();
        var directory = await DirectoryAsync(ct);
        if (!KnownRegions(row.Content, directory)) return Missing<CargoNoticePublicDto>();
        return Result.Ok(Public(row, directory));
    }

    public async Task<Result<CargoNoticeOwnerListResponse>> 내목록Async(int page = 1, CancellationToken ct = default)
    {
        if (Disabled<CargoNoticeOwnerListResponse>() is { } disabled) return disabled;
        if (Actor is not { } actor) return Login<CargoNoticeOwnerListResponse>();
        if (page < 1 || page > 100_000) return Fail<CargoNoticeOwnerListResponse>("InvalidInput", "조회 페이지를 확인해 주세요.");
        var now = Now;
        var found = await store.목록Async(actor, false, [], now, page, PageSize, ct);
        return Result.Ok(new CargoNoticeOwnerListResponse { Page = page, TotalCount = found.Total,
            Items = found.Items.Where(x => x.OwnerUserId == actor).Select(x => Owner(x, now)).ToArray() });
    }

    public async Task<Result<CargoNoticeOwnerDto>> 내상세Async(string noticeId, CancellationToken ct = default)
    {
        if (Disabled<CargoNoticeOwnerDto>() is { } disabled) return disabled;
        if (Actor is not { } actor) return Login<CargoNoticeOwnerDto>();
        var row = ValidId(noticeId) ? await store.조회Async(noticeId, ct) : null;
        return row is null || row.OwnerUserId != actor ? Missing<CargoNoticeOwnerDto>() : Result.Ok(Owner(row, Now));
    }

    public async Task<Result<CargoNoticeCommandResponse>> 요청결과Async(Guid requestId, string? noticeId = null, CancellationToken ct = default)
    {
        if (Disabled<CargoNoticeCommandResponse>() is { } disabled) return disabled;
        if (Actor is not { } actor) return Login<CargoNoticeCommandResponse>();
        if (requestId == Guid.Empty) return Fail<CargoNoticeCommandResponse>("InvalidInput", "원 요청 번호를 확인해 주세요.");
        var key = RequestKey(actor, requestId);
        var row = await store.요청조회Async(key, ct);
        if (row is null || row.OwnerUserId != actor || noticeId is not null && row.NoticeId != noticeId)
            return Missing<CargoNoticeCommandResponse>();
        return Result.Ok(ReadReceipt(row, key, requestId));
    }

    public Task<Result<CargoNoticeCommandResponse>> 초안생성Async(CargoNoticeDraftRequest request, CancellationToken ct = default)
        => DraftAsync(null, request, ct);

    public Task<Result<CargoNoticeCommandResponse>> 수정Async(string noticeId, CargoNoticeDraftRequest request, CancellationToken ct = default)
        => DraftAsync(noticeId, request, ct);

    private async Task<Result<CargoNoticeCommandResponse>> DraftAsync(string? noticeId, CargoNoticeDraftRequest request, CancellationToken ct)
    {
        if (Disabled<CargoNoticeCommandResponse>() is { } disabled) return disabled;
        if (Actor is not { } actor) return Login<CargoNoticeCommandResponse>();
        if (request is null || request.ClientRequestId == Guid.Empty || request.ExpectedRevision < 0)
            return Fail<CargoNoticeCommandResponse>("InvalidInput", "요청 번호와 현재 개정을 확인해 주세요.");
        var content = Normalize(request.Content);
        if (ContentError(content) is { } invalid) return Fail<CargoNoticeCommandResponse>("InvalidInput", invalid);
        var create = noticeId is null;
        noticeId ??= "cargo-notice:" + RequestKey(actor, request.ClientRequestId);
        var operation = create ? "create" : "update";
        var fingerprint = Fingerprint(new { operation, noticeId, request.ExpectedRevision, content });
        return await ExecuteAsync(actor, noticeId, request.ClientRequestId, request.ExpectedRevision, fingerprint, operation,
            create, async row =>
            {
                if (!KnownRegions(content, await DirectoryAsync(ct))) return ("InvalidInput", "현재 목록에서 공개 지역을 선택해 주세요.");
                row.Content = content; row.ContentRevision++;
                row.Status = CargoNoticeStatus.Draft; row.ConsentedContentRevision = null;
                row.PublishedAtUtc = null; row.ExpiresAtUtc = null;
                return null;
            }, ct);
    }

    public async Task<Result<CargoNoticeCommandResponse>> 공개Async(string noticeId, CargoNoticePublishRequest request, CancellationToken ct = default)
    {
        if (Disabled<CargoNoticeCommandResponse>() is { } disabled) return disabled;
        if (Actor is not { } actor) return Login<CargoNoticeCommandResponse>();
        if (request is null) return Fail<CargoNoticeCommandResponse>("InvalidInput", "공개 확인 내용을 입력해 주세요.");
        var fingerprint = Fingerprint(new { operation = "publish", noticeId, request.ClientRequestId,
            request.ExpectedRevision, request.ExpectedContentRevision, request.ConsentToPublicPublication,
            request.ExpiresAtUtc });
        return await ExecuteAsync(actor, noticeId, request.ClientRequestId, request.ExpectedRevision, fingerprint, "publish", false,
            async row =>
            {
                if (!request.ConsentToPublicPublication) return ("PublicConsentRequired", "현재 공고 내용을 공개하는 데 명시적으로 동의해 주세요.");
                if (request.ExpectedContentRevision != row.ContentRevision)
                    return ("ContentRevisionConflict", "공고 내용이 바뀌었습니다. 최신 내용을 읽고 다시 동의해 주세요.");
                var now = Now;
                if (request.ExpiresAtUtc.Offset != TimeSpan.Zero || request.ExpiresAtUtc.UtcDateTime <= now
                    || request.ExpiresAtUtc.UtcDateTime > now.AddDays(30))
                    return ("InvalidInput", "공개 만료는 UTC 기준 현재 이후부터 30일 이내로 입력해 주세요.");
                if (ContentError(row.Content) is { } invalid) return ("InvalidInput", invalid);
                if (!KnownRegions(row.Content, await DirectoryAsync(ct))) return ("InvalidInput", "현재 공개 지역을 다시 선택해 주세요.");
                row.Status = CargoNoticeStatus.Published; row.ConsentedContentRevision = row.ContentRevision;
                row.PublishedAtUtc = now; row.ExpiresAtUtc = request.ExpiresAtUtc.UtcDateTime;
                return null;
            }, ct);
    }

    public async Task<Result<CargoNoticeCommandResponse>> 철회Async(string noticeId, CargoNoticeMutationRequest request, CancellationToken ct = default)
    {
        if (Disabled<CargoNoticeCommandResponse>() is { } disabled) return disabled;
        if (Actor is not { } actor) return Login<CargoNoticeCommandResponse>();
        if (request is null) return Fail<CargoNoticeCommandResponse>("InvalidInput", "철회 요청을 확인해 주세요.");
        var fingerprint = Fingerprint(new { operation = "withdraw", noticeId, request.ClientRequestId, request.ExpectedRevision });
        return await ExecuteAsync(actor, noticeId, request.ClientRequestId, request.ExpectedRevision, fingerprint, "withdraw", false,
            row => { row.Status = CargoNoticeStatus.Withdrawn; row.ConsentedContentRevision = null;
                return Task.FromResult<(string Code, string Message)?>(null); }, ct);
    }

    private async Task<Result<CargoNoticeCommandResponse>> ExecuteAsync(string actor, string noticeId, Guid requestId,
        long expectedRevision, string fingerprint, string operation, bool create,
        Func<화물정보공고문서, Task<(string Code, string Message)?>> apply, CancellationToken ct)
    {
        if (!ValidId(noticeId) || requestId == Guid.Empty || expectedRevision < 0 || create && expectedRevision != 0)
            return Fail<CargoNoticeCommandResponse>("InvalidInput", "공고와 원 요청 번호·개정을 확인해 주세요.");
        var key = RequestKey(actor, requestId);
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var previous = await store.요청조회Async(key, ct);
            if (previous is not null)
            {
                var evidence = previous.Operations.Single(x => x.RequestKey == key);
                if (previous.OwnerUserId != actor || previous.NoticeId != noticeId || evidence.Fingerprint != fingerprint)
                    return Fail<CargoNoticeCommandResponse>("RequestConflict", "같은 요청 번호에 다른 내용을 사용할 수 없습니다.", 409);
                return Result.Ok(ReadReceipt(previous, key, requestId));
            }
            var existing = await store.조회Async(noticeId, ct);
            if (existing is not null && existing.OwnerUserId != actor) return Missing<CargoNoticeCommandResponse>();
            if (!create && existing is null) return Missing<CargoNoticeCommandResponse>();
            if ((existing?.Revision ?? 0) != expectedRevision || create && existing is not null)
                return Fail<CargoNoticeCommandResponse>("RevisionConflict", "다른 요청으로 공고가 바뀌었습니다. 최신 내용을 확인해 주세요.", 409);
            // 이력 한도에서도 공개 중인 공고를 숨길 권한은 남깁니다. 한 번 철회한 뒤에는
            // 같은 요청 영수증만 재사용하고 새 철회/수정으로 이력을 계속 늘릴 수 없습니다.
            if (existing?.Operations.Count >= MaximumOperations
                && !(operation == "withdraw" && existing.Status == CargoNoticeStatus.Published))
                return Fail<CargoNoticeCommandResponse>("RequestHistoryFull", "공고 변경 이력이 한도에 도달했습니다. 새 공고를 작성해 주세요.", 409);
            var row = existing is null ? new 화물정보공고문서 { NoticeId = noticeId, OwnerUserId = actor }
                : JsonSerializer.Deserialize<화물정보공고문서>(JsonSerializer.Serialize(existing, Json), Json)!;
            if (await apply(row) is { } invalid)
                return Fail<CargoNoticeCommandResponse>(invalid.Code, invalid.Message,
                    invalid.Code == "ContentRevisionConflict" ? 409 : 400);
            row.Revision = expectedRevision + 1; row.UpdatedAtUtc = Now;
            var response = new CargoNoticeCommandResponse { ClientRequestId = requestId, Operation = operation,
                AppliedAtUtc = Utc(row.UpdatedAtUtc), Notice = Owner(row, row.UpdatedAtUtc) };
            row.Operations.Add(new 화물정보공고요청증적 { RequestKey = key, Fingerprint = fingerprint,
                ResultJson = JsonSerializer.Serialize(response, Json) });
            if (await store.저장Async(row, expectedRevision, ct)) return Result.Ok(response);
        }
        return Fail<CargoNoticeCommandResponse>("RevisionConflict", "동시 변경 결과를 다시 조회해 주세요. 자동으로 새 요청을 만들지 않았습니다.", 409);
    }

    private async Task<IReadOnlyList<NeighborhoodPublicRegionDto>> DirectoryAsync(CancellationToken ct)
    {
        var values = await regions.목록Async(ct);
        if (values is null || values.Count == 0 || values.Any(x => string.IsNullOrWhiteSpace(x.RegionKey) || string.IsNullOrWhiteSpace(x.DisplayName))
            || values.Select(x => x.RegionKey).Distinct(StringComparer.Ordinal).Count() != values.Count)
            throw new InvalidOperationException("CargoNoticePublicRegionsUnavailable");
        return values;
    }

    private static CargoNoticeContent Normalize(CargoNoticeContent? value) => new()
    {
        Title = Text(value?.Title), GoodsName = Text(value?.GoodsName), Quantity = value?.Quantity ?? 0,
        WeightKg = value?.WeightKg, VehicleKind = Text(value?.VehicleKind),
        PickupRegionKey = Text(value?.PickupRegionKey), DropoffRegionKey = Text(value?.DropoffRegionKey),
        DesiredFromUtc = value?.DesiredFromUtc, DesiredUntilUtc = value?.DesiredUntilUtc
    };

    private static string Text(string? value) => (value ?? "").Normalize(NormalizationForm.FormKC).Trim();
    private static string? ContentError(CargoNoticeContent content)
    {
        if (string.IsNullOrWhiteSpace(content.Title) || content.Title.Length > 80 || string.IsNullOrWhiteSpace(content.GoodsName)
            || content.GoodsName.Length > 80 || content.Quantity is < 1 or > 1_000_000 || content.WeightKg is <= 0 or > 1_000_000
            || !CargoNoticeVehicleKinds.IsSupported(content.VehicleKind)) return "공고 제목·화물명·양수 수량과 차량 분류를 확인해 주세요.";
        // 연락처·주소의 대표 형식만 검사합니다. 모든 개인정보를 탐지한다고 보장하지 않습니다.
        if (new[] { content.Title, content.GoodsName }.Any(value => value.Any(character => char.IsControl(character)
                || char.GetUnicodeCategory(character) == System.Globalization.UnicodeCategory.Format) || PrivateText.IsMatch(value)))
            return "공개 제목·화물명에는 연락처·상세주소·식별번호·링크를 입력하지 마세요.";
        if (content.PickupRegionKey.Length > 80 || content.DropoffRegionKey.Length > 80) return "목록에서 공개 지역을 선택해 주세요.";
        if (content.DesiredFromUtc.HasValue != content.DesiredUntilUtc.HasValue
            || content.DesiredFromUtc is { } from && (from.Offset != TimeSpan.Zero
                || content.DesiredUntilUtc!.Value.Offset != TimeSpan.Zero || content.DesiredUntilUtc <= from))
            return "희망 시간은 시작과 끝을 함께 UTC로 입력해 주세요.";
        return null;
    }

    private static bool KnownRegions(CargoNoticeContent content, IReadOnlyList<NeighborhoodPublicRegionDto> directory)
        => directory.Any(x => x.RegionKey == content.PickupRegionKey) && directory.Any(x => x.RegionKey == content.DropoffRegionKey);
    private static bool IsPublic(화물정보공고문서 row, DateTime now)
        => row.Status == CargoNoticeStatus.Published && row.ContentRevision > 0 && row.ConsentedContentRevision == row.ContentRevision
            && row.PublishedAtUtc <= now && row.ExpiresAtUtc > now;
    private static CargoNoticeOwnerDto Owner(화물정보공고문서 row, DateTime now) => new()
    {
        NoticeId = row.NoticeId, Revision = row.Revision, ContentRevision = row.ContentRevision,
        Status = row.Status == CargoNoticeStatus.Published && row.ExpiresAtUtc <= now ? CargoNoticeStatus.Expired : row.Status,
        Content = Normalize(row.Content), ConsentedContentRevision = row.ConsentedContentRevision,
        PublishedAtUtc = row.PublishedAtUtc is { } published ? Utc(published) : null,
        ExpiresAtUtc = row.ExpiresAtUtc is { } expiry ? Utc(expiry) : null, UpdatedAtUtc = Utc(row.UpdatedAtUtc)
    };
    private static CargoNoticePublicDto Public(화물정보공고문서 row, IReadOnlyList<NeighborhoodPublicRegionDto> directory)
    {
        if (ContentError(row.Content) is not null) throw new InvalidOperationException("CargoNoticeStoredContentInvalid");
        return new() { NoticeId = row.NoticeId, Revision = row.Revision, Content = Normalize(row.Content),
            PickupRegionName = directory.Single(x => x.RegionKey == row.Content.PickupRegionKey).DisplayName,
            DropoffRegionName = directory.Single(x => x.RegionKey == row.Content.DropoffRegionKey).DisplayName,
            PublishedAtUtc = Utc(row.PublishedAtUtc!.Value), ExpiresAtUtc = Utc(row.ExpiresAtUtc!.Value) };
    }
    private static CargoNoticeCommandResponse ReadReceipt(화물정보공고문서 row, string key, Guid requestId)
    {
        var evidence = row.Operations.Single(x => x.RequestKey == key);
        var result = JsonSerializer.Deserialize<CargoNoticeCommandResponse>(evidence.ResultJson, Json);
        if (result is null || result.ClientRequestId != requestId || result.Notice.NoticeId != row.NoticeId
            || result.Notice.Revision < 1 || result.Notice.Revision > row.Revision)
            throw new InvalidOperationException("CargoNoticeRequestReceiptInvalid");
        return result;
    }
    private static DateTimeOffset Utc(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
    private static string RequestKey(string actor, Guid requestId) => Fingerprint(new { actor, requestId });
    private static string Fingerprint<T>(T value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, Json)))).ToLowerInvariant();
    private static bool ValidId(string? value) => value is { Length: 77 } && value.StartsWith("cargo-notice:", StringComparison.Ordinal)
        && value[13..].All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
    private Result<T>? Disabled<T>() => options.Value.Enabled ? null : Fail<T>("FeatureDisabled", "화물 정보 공고를 아직 제공하지 않습니다.", 404);
    private static Result<T> Login<T>() => Fail<T>("AuthenticationRequired", "로그인한 뒤 내 공고를 확인해 주세요.", 401);
    private static Result<T> Missing<T>() => Fail<T>("NotFound", "현재 확인할 수 있는 공고나 요청 결과를 찾을 수 없습니다.", 404);
    private static Result<T> Fail<T>(string code, string message, int status = 400)
        => Result.Fail<T>(new Error(message).WithMetadata("Code", "CargoNotice." + code)
            .WithMetadata("ErrorCode", "CargoNotice." + code)
            .WithMetadata("errorCode", "CargoNotice." + code).WithMetadata("StatusCode", status));
}
