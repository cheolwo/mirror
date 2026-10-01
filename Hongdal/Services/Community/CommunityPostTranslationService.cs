using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using FluentResults;
using Hongdal.Contracts.Common.Community;
using Hongdal.Contracts.Common.Localization;
using Hongdal.Domain.Community;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using 홍달.Data;
using 홍달.Services.Options;

namespace Hongdal.Services.Community;

public interface ICommunityPostTranslationService
{
    Task<Result<PlatformCommunityPostTranslationResponse>> GetOrCreateAsync(
        long postId,
        string? targetLanguageCode,
        CancellationToken cancellationToken);
}

public sealed class CommunityPostTranslationService : ICommunityPostTranslationService
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> TranslationGates = new();
    private readonly HongdalContext _db;
    private readonly ICommunityTextTranslationProvider _provider;
    private readonly CommunityPostTranslationOptions _options;
    private readonly ILogger<CommunityPostTranslationService> _logger;

    public CommunityPostTranslationService(
        HongdalContext db,
        ICommunityTextTranslationProvider provider,
        IOptions<CommunityPostTranslationOptions> options,
        ILogger<CommunityPostTranslationService> logger)
    {
        _db = db;
        _provider = provider;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<Result<PlatformCommunityPostTranslationResponse>> GetOrCreateAsync(
        long postId,
        string? targetLanguageCode,
        CancellationToken cancellationToken)
    {
        if (!DisplayLanguageCodes.TryNormalize(targetLanguageCode, out var targetLanguage))
        {
            return Fail(
                "지원하지 않는 표시 언어입니다. 현재 ko-KR과 en-US만 사용할 수 있습니다.",
                StatusCodes.Status400BadRequest);
        }

        var supportedLanguages = _options.SupportedLanguageCodes
            .Select(x => DisplayLanguageCodes.Normalize(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!supportedLanguages.Contains(targetLanguage))
        {
            return Fail("현재 번역 대상으로 개방되지 않은 언어입니다.", StatusCodes.Status400BadRequest);
        }

        var post = await _db.PlatformCommunityPosts
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == postId && !x.IsDeleted, cancellationToken);
        if (post is null)
        {
            return Fail("게시글을 찾을 수 없습니다.", StatusCodes.Status404NotFound);
        }

        if ((post.IsReportBoardPost || IsReportCategory(post.Category)) && !_options.TranslateReportPosts)
        {
            return Fail(
                "신고·분쟁 글은 개인정보 보호를 위해 자동 번역 대상에서 제외됩니다.",
                StatusCodes.Status403Forbidden);
        }

        var translatableBody = CommunityEvidenceChartTextCodec.StripBlocks(post.Body);
        var sourceLanguage = CommunityPostLanguageResolver.Resolve(
            post.OriginalLanguageCode,
            post.Title,
            translatableBody);
        if (string.Equals(sourceLanguage, targetLanguage, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Ok(ToOriginalResponse(post, sourceLanguage));
        }

        if (!_options.Enabled || !_provider.IsAvailable)
        {
            return Fail(
                "게시글 번역 기능이 아직 활성화되지 않았습니다. Azure Translator 설정을 확인해 주세요.",
                StatusCodes.Status503ServiceUnavailable);
        }

        var contentHash = ComputeContentHash(sourceLanguage, post.Title, post.Body);
        var gateKey = $"{post.Id}:{targetLanguage}:{contentHash}";
        var gate = TranslationGates.GetOrAdd(gateKey, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);

        try
        {
            var cached = await FindCachedAsync(post.Id, targetLanguage, contentHash, cancellationToken);
            if (cached is not null)
            {
                return Result.Ok(ToResponse(post, cached, isCached: true));
            }

            CommunityTextTranslationResult translated;
            try
            {
                translated = await _provider.TranslateAsync(
                    post.Title,
                    translatableBody,
                    sourceLanguage,
                    targetLanguage,
                    cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "게시글 자동 번역 호출에 실패했습니다. PostId={PostId}, TargetLanguage={TargetLanguage}",
                    post.Id,
                    targetLanguage);
                return Fail("자동 번역 서비스 호출에 실패했습니다. 잠시 후 다시 시도해 주세요.", StatusCodes.Status502BadGateway);
            }

            if (translated.Title.Length > 500)
            {
                return Fail("번역된 제목이 저장 가능한 길이를 초과했습니다.", StatusCodes.Status502BadGateway);
            }

            var now = DateTime.UtcNow;
            var entity = new PlatformCommunityPostTranslation
            {
                PostId = post.Id,
                SourceLanguageCode = sourceLanguage,
                TargetLanguageCode = targetLanguage,
                SourceContentHash = contentHash,
                TranslatedTitle = translated.Title,
                TranslatedBody = translated.Body,
                Provider = translated.Provider,
                ProviderModelVersion = translated.ProviderModelVersion,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

            _db.PlatformCommunityPostTranslations.Add(entity);
            await _db.SaveChangesAsync(cancellationToken);

            var staleTranslations = await _db.PlatformCommunityPostTranslations
                .Where(x => x.PostId == post.Id
                            && x.TargetLanguageCode == targetLanguage
                            && x.SourceContentHash != contentHash)
                .ToListAsync(cancellationToken);
            if (staleTranslations.Count > 0)
            {
                _db.PlatformCommunityPostTranslations.RemoveRange(staleTranslations);
                await _db.SaveChangesAsync(cancellationToken);
            }

            return Result.Ok(ToResponse(post, entity, isCached: false));
        }
        finally
        {
            gate.Release();
        }
    }

    private Task<PlatformCommunityPostTranslation?> FindCachedAsync(
        long postId,
        string targetLanguageCode,
        string contentHash,
        CancellationToken cancellationToken)
        => _db.PlatformCommunityPostTranslations
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.PostId == postId
                     && x.TargetLanguageCode == targetLanguageCode
                     && x.SourceContentHash == contentHash,
                cancellationToken);

    private static PlatformCommunityPostTranslationResponse ToOriginalResponse(
        PlatformCommunityPost post,
        string languageCode)
        => new()
        {
            PostId = post.Id,
            SourceLanguageCode = languageCode,
            TargetLanguageCode = languageCode,
            OriginalTitle = post.Title,
            OriginalBody = post.Body,
            TranslatedTitle = post.Title,
            TranslatedBody = post.Body,
            Provider = "Original",
            IsMachineTranslated = false,
            IsCached = true,
            CreatedAtUtc = post.CreatedAtUtc
        };

    private static PlatformCommunityPostTranslationResponse ToResponse(
        PlatformCommunityPost post,
        PlatformCommunityPostTranslation translation,
        bool isCached)
        => new()
        {
            PostId = post.Id,
            SourceLanguageCode = translation.SourceLanguageCode,
            TargetLanguageCode = translation.TargetLanguageCode,
            OriginalTitle = post.Title,
            OriginalBody = post.Body,
            TranslatedTitle = translation.TranslatedTitle,
            TranslatedBody = translation.TranslatedBody,
            Provider = translation.Provider,
            IsMachineTranslated = true,
            IsCached = isCached,
            IsHumanReviewed = translation.IsHumanReviewed,
            CreatedAtUtc = translation.CreatedAtUtc
        };

    private static string ComputeContentHash(string sourceLanguageCode, string title, string body)
        => Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes($"{sourceLanguageCode}\n{title}\n{body}")));

    private static bool IsReportCategory(string? category)
        => !string.IsNullOrWhiteSpace(category)
           && (category.Contains("신고", StringComparison.OrdinalIgnoreCase)
               || category.Contains("분쟁", StringComparison.OrdinalIgnoreCase)
               || category.Contains("report", StringComparison.OrdinalIgnoreCase));

    private static Result<PlatformCommunityPostTranslationResponse> Fail(string message, int statusCode)
        => Result.Fail<PlatformCommunityPostTranslationResponse>(
            new Error(message).WithMetadata("StatusCode", statusCode));
}
