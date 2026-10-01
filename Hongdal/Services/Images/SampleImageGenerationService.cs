using Microsoft.EntityFrameworkCore;
using 홍달.Data;
using 홍달.도메인.공통;
using 홍달.Services.External.KieAi;
using 홍달.Services.Options;

namespace 홍달.Services.Images;

public interface I샘플이미지생성Service
{
    Task<생성이미지작업> 생성요청Async(이미지생성요청 request, CancellationToken cancellationToken = default);
    Task<생성이미지작업?> 작업조회Async(long 작업Id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<생성이미지작업>> 작업목록조회Async(샘플이미지작업조회조건 request, CancellationToken cancellationToken = default);
    Task<생성이미지작업> 작업재시도Async(long 작업Id, CancellationToken cancellationToken = default);
    Task<bool> 작업후처리Async(long 작업Id, string? rawJson = null, CancellationToken cancellationToken = default);
    Task<int> 미완료작업처리Async(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<생성이미지작업>> 누락샘플이미지생성Async(string 대상타입, string 이미지용도, int maxCount, bool includeFailed, CancellationToken cancellationToken = default);
}

public sealed class 샘플이미지작업조회조건
{
    public string? 대상타입 { get; set; }
    public string? 이미지용도 { get; set; }
    public string? 상태 { get; set; }
    public bool? 샘플데이터여부 { get; set; }
    public string? 대상식별자 { get; set; }
    public int 최대건수 { get; set; } = 50;
}

public sealed class 샘플이미지생성Service : I샘플이미지생성Service
{
    private readonly HongdalContext _db;
    private readonly IKieAiImageGenerationClient _kieAiClient;
    private readonly 이미지프롬프트생성기Resolver _promptResolver;
    private readonly I샘플이미지대상ResolverResolver _targetResolverResolver;
    private readonly IGoogleCloudStorageService _googleCloudStorageService;
    private readonly KieAiOptions _options;

    public 샘플이미지생성Service(
        HongdalContext db,
        IKieAiImageGenerationClient kieAiClient,
        이미지프롬프트생성기Resolver promptResolver,
        I샘플이미지대상ResolverResolver targetResolverResolver,
        IGoogleCloudStorageService googleCloudStorageService,
        Microsoft.Extensions.Options.IOptions<KieAiOptions> options)
    {
        _db = db;
        _kieAiClient = kieAiClient;
        _promptResolver = promptResolver;
        _targetResolverResolver = targetResolverResolver;
        _googleCloudStorageService = googleCloudStorageService;
        _options = options.Value;
    }

    public async Task<생성이미지작업> 생성요청Async(이미지생성요청 request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var promptGenerator = _promptResolver.Resolve(request.이미지용도);
        var prompt = promptGenerator.CreatePrompt(request);
        var entity = new 생성이미지작업
        {
            이미지용도 = request.이미지용도,
            대상타입 = request.대상타입.Trim(),
            대상식별자 = request.대상식별자.Trim(),
            샘플데이터여부 = request.샘플데이터여부,
            중복방지키 = BuildDedupKey(request.대상타입.Trim(), request.대상식별자.Trim(), request.이미지용도),
            프롬프트 = prompt,
            종횡비 = request.종횡비,
            해상도 = request.해상도,
            외부모델명 = _options.Model
        };

        var submitted = await 제출Async(entity, cancellationToken);
        await TryMarkTargetRequestedAsync(submitted, cancellationToken);
        return submitted;
    }

    public Task<생성이미지작업?> 작업조회Async(long 작업Id, CancellationToken cancellationToken = default)
    {
        return _db.생성이미지작업.AsNoTracking().FirstOrDefaultAsync(x => x.Id == 작업Id, cancellationToken);
    }

    public async Task<IReadOnlyList<생성이미지작업>> 작업목록조회Async(샘플이미지작업조회조건 request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var query = _db.생성이미지작업.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.대상타입))
        {
            query = query.Where(x => x.대상타입 == request.대상타입.Trim());
        }

        if (!string.IsNullOrWhiteSpace(request.이미지용도))
        {
            query = query.Where(x => x.이미지용도 == request.이미지용도.Trim());
        }

        if (!string.IsNullOrWhiteSpace(request.상태))
        {
            query = query.Where(x => x.상태 == request.상태.Trim());
        }

        if (request.샘플데이터여부.HasValue)
        {
            query = query.Where(x => x.샘플데이터여부 == request.샘플데이터여부.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.대상식별자))
        {
            query = query.Where(x => x.대상식별자 == request.대상식별자.Trim());
        }

        var take = Math.Clamp(request.최대건수, 1, 200);
        return await query
            .OrderByDescending(x => x.생성시각)
            .Take(take)
            .ToArrayAsync(cancellationToken);
    }

    public async Task<생성이미지작업> 작업재시도Async(long 작업Id, CancellationToken cancellationToken = default)
    {
        var original = await _db.생성이미지작업
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == 작업Id, cancellationToken)
            ?? throw new InvalidOperationException("재시도할 샘플 이미지 작업을 찾을 수 없습니다.");

        var hasRunningJob = await _db.생성이미지작업.AnyAsync(
            x => x.Id != 작업Id
                 && x.중복방지키 == original.중복방지키
                 && (x.상태 == 생성이미지작업상태.생성대기
                     || x.상태 == 생성이미지작업상태.생성요청됨
                     || x.상태 == 생성이미지작업상태.생성중
                     || x.상태 == 생성이미지작업상태.업로드중),
            cancellationToken);

        if (hasRunningJob)
        {
            throw new InvalidOperationException("동일 대상의 진행 중인 이미지 생성 작업이 있어 재시도할 수 없습니다.");
        }

        var retry = new 생성이미지작업
        {
            이미지용도 = original.이미지용도,
            대상타입 = original.대상타입,
            대상식별자 = original.대상식별자,
            샘플데이터여부 = original.샘플데이터여부,
            중복방지키 = original.중복방지키,
            프롬프트 = original.프롬프트,
            종횡비 = original.종횡비,
            해상도 = original.해상도,
            외부모델명 = string.IsNullOrWhiteSpace(original.외부모델명) ? _options.Model : original.외부모델명,
            재시도횟수 = original.재시도횟수 + 1
        };

        var submitted = await 제출Async(retry, cancellationToken);
        await TryMarkTargetRequestedAsync(submitted, cancellationToken);
        return submitted;
    }

    public async Task<IReadOnlyList<생성이미지작업>> 누락샘플이미지생성Async(string 대상타입, string 이미지용도, int maxCount, bool includeFailed, CancellationToken cancellationToken = default)
    {
        var resolver = _targetResolverResolver.Resolve(대상타입, 이미지용도);
        var targets = await resolver.GetMissingImageTargetsAsync(maxCount, includeFailed, cancellationToken);
        var created = new List<생성이미지작업>();

        foreach (var target in targets)
        {
            var dedupKey = BuildDedupKey(target.대상타입, target.대상식별자, target.이미지용도);
            var hasRunningJob = await _db.생성이미지작업.AnyAsync(
                x => x.중복방지키 == dedupKey
                     && (x.상태 == 생성이미지작업상태.생성대기
                         || x.상태 == 생성이미지작업상태.생성요청됨
                         || x.상태 == 생성이미지작업상태.생성중
                         || x.상태 == 생성이미지작업상태.업로드중),
                cancellationToken);

            if (hasRunningJob)
            {
                continue;
            }

            var job = await 생성요청Async(new 이미지생성요청
            {
                이미지용도 = target.이미지용도,
                대상타입 = target.대상타입,
                대상식별자 = target.대상식별자,
                제목 = target.제목,
                설명 = target.설명,
                추가맥락 = target.추가맥락,
                종횡비 = target.종횡비,
                해상도 = target.해상도,
                샘플데이터여부 = target.샘플데이터여부
            }, cancellationToken);

            created.Add(job);
        }

        return created;
    }

    private async Task<생성이미지작업> 제출Async(생성이미지작업 entity, CancellationToken cancellationToken)
    {
        var callbackUrl = BuildCallbackUrl();
        entity.콜백Url = callbackUrl;
        entity.상태 = 생성이미지작업상태.생성대기;
        entity.생성시각 = DateTime.UtcNow;
        entity.수정시각 = DateTime.UtcNow;
        entity.완료시각 = null;
        entity.실패사유 = null;
        entity.최종실패시각 = null;
        entity.외부원본이미지Url = null;
        entity.저장Bucket = null;
        entity.저장ObjectName = null;
        entity.저장Url = null;
        entity.외부TaskId = null;
        entity.최근응답원문 = null;

        _db.생성이미지작업.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        try
        {
            var result = await _kieAiClient.CreateTextToImageTaskAsync(
                new KieAiCreateTaskRequest(
                    entity.프롬프트,
                    entity.종횡비,
                    Quality: null,
                    CallBackUrl: callbackUrl),
                cancellationToken);

            entity.외부TaskId = result.TaskId;
            entity.최근응답원문 = result.RawJson;
            entity.상태 = 생성이미지작업상태.생성요청됨;
            entity.수정시각 = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            entity.상태 = 생성이미지작업상태.실패;
            entity.실패사유 = ex.Message;
            entity.최종실패시각 = DateTime.UtcNow;
            entity.수정시각 = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
            await TryMarkTargetFailedAsync(entity, ex.Message, cancellationToken);
            throw;
        }

        return entity;
    }

    public async Task<bool> 작업후처리Async(long 작업Id, string? rawJson = null, CancellationToken cancellationToken = default)
    {
        var entity = await _db.생성이미지작업.FirstOrDefaultAsync(x => x.Id == 작업Id, cancellationToken);
        if (entity is null || string.IsNullOrWhiteSpace(entity.외부TaskId))
        {
            return false;
        }

        var detail = await _kieAiClient.GetTaskDetailAsync(entity.외부TaskId, cancellationToken);
        entity.최근응답원문 = rawJson ?? detail.RawJson;
        entity.수정시각 = DateTime.UtcNow;

        if (!detail.IsTerminal)
        {
            entity.상태 = 생성이미지작업상태.생성중;
            await _db.SaveChangesAsync(cancellationToken);
            return false;
        }

        if (!detail.IsSuccess || string.IsNullOrWhiteSpace(detail.ImageUrl))
        {
            entity.상태 = 생성이미지작업상태.실패;
            entity.실패사유 = detail.RawJson;
            entity.재시도횟수 += 1;
            entity.최종실패시각 = DateTime.UtcNow;
            entity.완료시각 = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
            await TryMarkTargetFailedAsync(entity, detail.RawJson, cancellationToken);
            return true;
        }

        entity.상태 = 생성이미지작업상태.업로드중;
        entity.외부원본이미지Url = detail.ImageUrl;
        await _db.SaveChangesAsync(cancellationToken);

        await using var imageStream = await _kieAiClient.DownloadImageAsync(detail.ImageUrl, cancellationToken);
        var extension = ResolveFileExtension(detail.ImageUrl);
        var fileName = $"{entity.작업코드}{extension}";
        var rootFolder = entity.샘플데이터여부 ? "sample-images" : "generated-images";
        var folder = $"{rootFolder}/{entity.이미지용도}/{entity.대상타입}/{entity.대상식별자}";
        var uploadResult = await _googleCloudStorageService.UploadAsync(imageStream, fileName, ResolveContentType(extension), folder, cancellationToken);

        entity.저장Bucket = uploadResult.BucketName;
        entity.저장ObjectName = uploadResult.ObjectName;
        entity.저장Url = uploadResult.PublicUrl;
        entity.상태 = 생성이미지작업상태.완료;
        entity.완료시각 = DateTime.UtcNow;
        entity.수정시각 = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        await TryMarkTargetCompletedAsync(entity, uploadResult.PublicUrl, cancellationToken);
        return true;
    }

    private async Task TryMarkTargetRequestedAsync(생성이미지작업 entity, CancellationToken cancellationToken)
    {
        try
        {
            var resolver = _targetResolverResolver.Resolve(entity.대상타입, entity.이미지용도);
            await resolver.MarkRequestedAsync(entity.대상식별자, cancellationToken);
        }
        catch
        {
        }
    }

    public async Task<int> 미완료작업처리Async(CancellationToken cancellationToken = default)
    {
        var threshold = DateTime.UtcNow.AddMinutes(-Math.Max(1, _options.MaxPollingMinutes));
        var items = await _db.생성이미지작업
            .Where(x => x.상태 == 생성이미지작업상태.생성요청됨 || x.상태 == 생성이미지작업상태.생성중)
            .Where(x => x.생성시각 >= threshold)
            .OrderBy(x => x.Id)
            .Take(20)
            .ToListAsync(cancellationToken);

        var completed = 0;
        foreach (var item in items)
        {
            if (await 작업후처리Async(item.Id, null, cancellationToken))
            {
                completed++;
            }
        }

        return completed;
    }

    private string? BuildCallbackUrl()
    {
        if (string.IsNullOrWhiteSpace(_options.CallbackBaseUrl))
        {
            return null;
        }

        return $"{_options.CallbackBaseUrl.TrimEnd('/')}/api/v1/kie-ai/callback";
    }

    private static string ResolveFileExtension(string imageUrl)
    {
        if (Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri))
        {
            var extension = Path.GetExtension(uri.AbsolutePath);
            if (!string.IsNullOrWhiteSpace(extension))
            {
                return extension;
            }
        }

        return ".png";
    }

    private static string ResolveContentType(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            _ => "image/png"
        };
    }

    private static string BuildDedupKey(string 대상타입, string 대상식별자, string 이미지용도)
    {
        return $"{대상타입}::{대상식별자}::{이미지용도}";
    }

    private async Task TryMarkTargetCompletedAsync(생성이미지작업 entity, string imageUrl, CancellationToken cancellationToken)
    {
        try
        {
            var resolver = _targetResolverResolver.Resolve(entity.대상타입, entity.이미지용도);
            await resolver.MarkCompletedAsync(entity.대상식별자, imageUrl, cancellationToken);
        }
        catch
        {
        }
    }

    private async Task TryMarkTargetFailedAsync(생성이미지작업 entity, string? reason, CancellationToken cancellationToken)
    {
        try
        {
            var resolver = _targetResolverResolver.Resolve(entity.대상타입, entity.이미지용도);
            await resolver.MarkFailedAsync(entity.대상식별자, reason, cancellationToken);
        }
        catch
        {
        }
    }
}
