using Hongdal.Contracts.Common.Content;
using Hongdal.Contracts.Common.Metadata;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using 홍달.Data;
using 홍달.Services.External.Google;
using 홍달.Services.Images;
using 홍달.Services.Options;
using 홍달.도메인.공통;

namespace Hongdal.Services.Content;

public interface ICommunityAuthoringImageService
{
    Task<CommunityAuthoringImageTaskResponse> GenerateAsync(
        CommunityAuthoringImageGenerateRequest request,
        CancellationToken cancellationToken = default);

    Task<CommunityAuthoringImageTaskResponse?> GetAsync(
        string jobCode,
        bool refreshProvider,
        CancellationToken cancellationToken = default);

    Task<CommunityAuthoringGeneratedImageFile> OpenCompletedImageAsync(
        string jobCode,
        CancellationToken cancellationToken = default);
}

public sealed record CommunityAuthoringGeneratedImageFile(
    byte[] Content,
    string FileName,
    string ContentType);

[HongdalCodeMetadata(
    HongdalCodeFeatureKeys.CommunityAuthoringImage,
    HongdalCodeLayer.Application,
    "AI 이미지 작업을 영속화하고 provider 상태 갱신·완료 파일 조회를 조율",
    ContractType = typeof(ICommunityAuthoringImageService),
    FlowOrder = 50,
    Effects = HongdalCodeEffect.NetworkCall
              | HongdalCodeEffect.PersistentRead
              | HongdalCodeEffect.PersistentWrite
              | HongdalCodeEffect.ObjectStorageRead
              | HongdalCodeEffect.ObjectStorageWrite
              | HongdalCodeEffect.ThirdPartyApiCall
              | HongdalCodeEffect.MayIncurExternalCost,
    Boundary = "생성물은 AI 이미지로 공개하며 실제 거래·현장·통계 증빙으로 취급하지 않습니다.")]
public sealed class CommunityAuthoringImageService : ICommunityAuthoringImageService
{
    private const string TargetType = "CommunityPostDraft";
    private const string Disclosure = "Kie.ai GPT Image API로 생성한 AI 이미지입니다. 사실 자료나 현장 증빙으로 사용하기 전에 사람이 검토해야 합니다.";

    private readonly HongdalContext _db;
    private readonly I샘플이미지생성Service _imageGenerationService;
    private readonly IGoogleCloudStorageService _storageService;
    private readonly KieAiOptions _options;

    public CommunityAuthoringImageService(
        HongdalContext db,
        I샘플이미지생성Service imageGenerationService,
        IGoogleCloudStorageService storageService,
        IOptions<KieAiOptions> options)
    {
        _db = db;
        _imageGenerationService = imageGenerationService;
        _storageService = storageService;
        _options = options.Value;
    }

    public async Task<CommunityAuthoringImageTaskResponse> GenerateAsync(
        CommunityAuthoringImageGenerateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var prompt = NormalizePrompt(request.Prompt);
        var aspectRatio = NormalizeAspectRatio(request.AspectRatio);
        var entity = await _imageGenerationService.생성요청Async(
            new 이미지생성요청
            {
                이미지용도 = 생성이미지용도.커뮤니티글쓰기이미지,
                대상타입 = TargetType,
                대상식별자 = $"authoring-{Guid.NewGuid():N}",
                제목 = prompt.Length <= 80 ? prompt : prompt[..80],
                설명 = prompt,
                추가맥락 = "AI 생성 이미지는 커뮤니티 글의 시각 자료이며 실제 거래·현장 증빙이 아닙니다.",
                종횡비 = aspectRatio,
                해상도 = "provider-default",
                샘플데이터여부 = false
            },
            cancellationToken);

        return ToResponse(entity);
    }

    public async Task<CommunityAuthoringImageTaskResponse?> GetAsync(
        string jobCode,
        bool refreshProvider,
        CancellationToken cancellationToken = default)
    {
        var entity = await FindAsync(jobCode, tracking: false, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        if (refreshProvider && IsProviderPending(entity))
        {
            await _imageGenerationService.작업후처리Async(entity.Id, cancellationToken: cancellationToken);
            entity = await FindAsync(jobCode, tracking: false, cancellationToken)
                     ?? throw new InvalidOperationException("이미지 생성 작업을 다시 조회하지 못했습니다.");
        }

        return ToResponse(entity);
    }

    public async Task<CommunityAuthoringGeneratedImageFile> OpenCompletedImageAsync(
        string jobCode,
        CancellationToken cancellationToken = default)
    {
        var entity = await FindAsync(jobCode, tracking: false, cancellationToken)
                     ?? throw new KeyNotFoundException("글쓰기 이미지 생성 작업을 찾을 수 없습니다.");
        if (!string.Equals(entity.상태, 생성이미지작업상태.완료, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(entity.저장Bucket)
            || string.IsNullOrWhiteSpace(entity.저장ObjectName))
        {
            throw new InvalidOperationException("완료된 이미지만 게시글에 첨부할 수 있습니다.");
        }

        var content = await _storageService.DownloadAsync(
            entity.저장Bucket,
            entity.저장ObjectName,
            cancellationToken);
        var extension = ResolveExtension(entity.저장ObjectName, entity.저장Url);
        return new CommunityAuthoringGeneratedImageFile(
            content,
            $"hongdal-ai-{entity.작업코드}{extension}",
            ResolveContentType(extension));
    }

    private Task<생성이미지작업?> FindAsync(
        string jobCode,
        bool tracking,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(jobCode))
        {
            return Task.FromResult<생성이미지작업?>(null);
        }

        var query = _db.생성이미지작업
            .Where(entity => entity.작업코드 == jobCode.Trim())
            .Where(entity => entity.대상타입 == TargetType)
            .Where(entity => entity.이미지용도 == 생성이미지용도.커뮤니티글쓰기이미지);
        return (tracking ? query : query.AsNoTracking()).SingleOrDefaultAsync(cancellationToken);
    }

    private CommunityAuthoringImageTaskResponse ToResponse(생성이미지작업 entity)
    {
        var status = entity.상태 switch
        {
            생성이미지작업상태.완료 => CommunityAuthoringImageTaskStatusCodes.Completed,
            생성이미지작업상태.실패 => CommunityAuthoringImageTaskStatusCodes.Failed,
            생성이미지작업상태.생성대기 or 생성이미지작업상태.생성요청됨
                => CommunityAuthoringImageTaskStatusCodes.Queued,
            _ => CommunityAuthoringImageTaskStatusCodes.Processing
        };
        var success = status == CommunityAuthoringImageTaskStatusCodes.Completed;
        var terminal = success || status == CommunityAuthoringImageTaskStatusCodes.Failed;
        var message = status switch
        {
            CommunityAuthoringImageTaskStatusCodes.Completed => "이미지 생성을 완료했습니다.",
            CommunityAuthoringImageTaskStatusCodes.Failed => "이미지를 생성하지 못했습니다. Kie.ai 설정과 프롬프트를 확인해 주세요.",
            CommunityAuthoringImageTaskStatusCodes.Queued => "Kie.ai 이미지 생성 대기열에 등록했습니다.",
            _ => "Kie.ai에서 이미지를 생성하고 있습니다."
        };

        return new CommunityAuthoringImageTaskResponse(
            entity.작업코드,
            status,
            message,
            entity.프롬프트,
            entity.종횡비,
            string.IsNullOrWhiteSpace(entity.외부모델명) ? _options.Model : entity.외부모델명,
            entity.저장Url,
            terminal,
            success,
            success ? 100 : null,
            entity.생성시각,
            entity.완료시각,
            Disclosure);
    }

    private static bool IsProviderPending(생성이미지작업 entity)
        => entity.상태 is 생성이미지작업상태.생성요청됨 or 생성이미지작업상태.생성중;

    private static string NormalizePrompt(string? prompt)
    {
        var normalized = prompt?.Trim() ?? string.Empty;
        if (normalized.Length < CommunityAuthoringImageLimits.MinimumPromptLength)
        {
            throw new ArgumentException($"이미지 프롬프트를 {CommunityAuthoringImageLimits.MinimumPromptLength}자 이상 입력해 주세요.", nameof(prompt));
        }

        if (normalized.Length > CommunityAuthoringImageLimits.MaximumPromptLength)
        {
            throw new ArgumentException($"이미지 프롬프트는 {CommunityAuthoringImageLimits.MaximumPromptLength:N0}자 이하여야 합니다.", nameof(prompt));
        }

        return normalized;
    }

    private static string NormalizeAspectRatio(string? aspectRatio)
    {
        var normalized = string.IsNullOrWhiteSpace(aspectRatio)
            ? CommunityAuthoringImageAspectRatios.Landscape
            : aspectRatio.Trim().ToLowerInvariant();
        if (!CommunityAuthoringImageAspectRatios.All.Contains(normalized, StringComparer.Ordinal))
        {
            throw new ArgumentException("지원하지 않는 이미지 비율입니다.", nameof(aspectRatio));
        }

        return normalized;
    }

    private static string ResolveExtension(string objectName, string? imageUrl)
    {
        var extension = Path.GetExtension(objectName);
        if (string.IsNullOrWhiteSpace(extension)
            && Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri))
        {
            extension = Path.GetExtension(uri.AbsolutePath);
        }

        return extension?.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => ".jpg",
            ".webp" => ".webp",
            _ => ".png"
        };
    }

    private static string ResolveContentType(string extension)
        => extension switch
        {
            ".jpg" => "image/jpeg",
            ".webp" => "image/webp",
            _ => "image/png"
        };
}
