using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Hongdal.Contracts.Common.Content;
using Hongdal.Domain.Content;
using Microsoft.Extensions.Options;
using SkiaSharp;
using 홍달.Services.HIOPSAI;
using 홍달.Services.Options;

namespace Hongdal.Services.Content;

public sealed record YouTube영상재료인지업로드프레임(
    int? 영상구간초,
    string 콘텐츠유형,
    byte[] 콘텐츠);

public sealed record YouTube영상재료자동인지요청(
    bool 분석권한확인,
    string? 제공자막,
    IReadOnlyList<YouTube영상재료인지업로드프레임> 프레임목록);

public sealed record YouTube영상재료인지프레임입력(
    int? 영상구간초,
    string DataUrl);

public sealed record YouTube영상재료인지Engine입력(
    string VideoId,
    string 영상제목,
    string 영상설명,
    string? 제공자막,
    IReadOnlyList<YouTube영상재료인지프레임입력> 프레임목록);

public sealed record YouTube영상재료인지Engine후보(
    string 재료명,
    string 표준재료명,
    int? 영상구간초,
    string 근거유형,
    string 발견근거,
    decimal 신뢰도);

public sealed record YouTube영상재료인지Engine결과(
    bool 성공,
    string? 모델,
    IReadOnlyList<YouTube영상재료인지Engine후보> 후보목록,
    string? 불확실성메모,
    string? 실패사유);

public interface IYouTube영상재료인지Engine
{
    Task<YouTube영상재료인지Engine결과> 인지Async(
        YouTube영상재료인지Engine입력 입력,
        CancellationToken cancellationToken);
}

public interface IYouTube영상재료자동인지Service
{
    Task<YouTube영상재료자동인지결과Dto> 분석Async(
        string videoId,
        YouTube영상재료자동인지요청 요청,
        CancellationToken cancellationToken);
}

public sealed class YouTube영상재료인지Engine : IYouTube영상재료인지Engine
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly JsonElement OutputSchema = CreateOutputSchema();
    private readonly IHIOPSAIClient _aiClient;

    public YouTube영상재료인지Engine(IHIOPSAIClient aiClient)
    {
        _aiClient = aiClient;
    }

    public async Task<YouTube영상재료인지Engine결과> 인지Async(
        YouTube영상재료인지Engine입력 입력,
        CancellationToken cancellationToken)
    {
        var images = 입력.프레임목록
            .Select((frame, index) => new HIOPSAIImageInput(
                frame.DataUrl,
                "high",
                frame.영상구간초.HasValue
                    ? $"프레임 {index + 1}: 영상 {frame.영상구간초.Value}초"
                    : $"프레임 {index + 1}: 영상 시각 미지정"))
            .ToArray();
        var completion = await _aiClient.CompleteAsync(
            new HIOPSAICompletionRequest(
                Purpose: "youtube-food-ingredient-recognition",
                Messages:
                [
                    new HIOPSAIMessage(
                        "developer",
                        """
                        당신은 음식 영상에서 식재료 후보만 알아차리는 엔진이다.
                        화면, 제공 자막, 제목과 설명에 명시적으로 보이거나 언급된 재료만 반환한다.
                        완성 요리명, 사람의 신원·국적·건강 특성은 추론하지 않는다.
                        포장 상품은 실제 재료명이 확인될 때만 재료로 기록한다.
                        애매하거나 근거가 약하면 누락하고, 근거 문장은 짧게 요약한다.
                        동일한 재료는 한 번만 반환하고 표준재료명에는 가장 일반적인 한국어 명칭을 사용한다.
                        """),
                    new HIOPSAIMessage(
                        "user",
                        BuildUserPrompt(입력),
                        images)
                ],
                MaxOutputTokens: 700,
                CorrelationId: 입력.VideoId,
                OutputJsonSchema: new HIOPSAIJsonSchema(
                    "youtube_food_ingredient_recognition",
                    OutputSchema)),
            cancellationToken);

        if (!completion.Success)
        {
            return new YouTube영상재료인지Engine결과(
                false,
                completion.Model,
                [],
                null,
                completion.BlockedReason ?? "재료 인지 AI 호출이 실행되지 않았습니다.");
        }

        try
        {
            var output = JsonSerializer.Deserialize<ModelOutput>(completion.Text, JsonOptions)
                         ?? new ModelOutput();
            var candidates = (output.Ingredients ?? [])
                .Where(value => value is not null)
                .Select(value => value!)
                .Select(ToCandidate)
                .Where(candidate => candidate is not null)
                .Cast<YouTube영상재료인지Engine후보>()
                .GroupBy(candidate => candidate.표준재료명, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.OrderByDescending(candidate => candidate.신뢰도).First())
                .OrderByDescending(candidate => candidate.신뢰도)
                .Take(20)
                .ToArray();
            return new YouTube영상재료인지Engine결과(
                true,
                completion.Model,
                candidates,
                NormalizeOptional(output.UncertaintyNote, 500),
                null);
        }
        catch (JsonException)
        {
            return new YouTube영상재료인지Engine결과(
                false,
                completion.Model,
                [],
                null,
                "재료 인지 결과를 구조화된 데이터로 해석하지 못했습니다.");
        }
    }

    private static string BuildUserPrompt(YouTube영상재료인지Engine입력 input)
        => $"""
            VideoId: {input.VideoId}
            영상 제목: {input.영상제목}
            영상 설명:
            {input.영상설명}

            권한 확인 후 제공된 자막:
            {(string.IsNullOrWhiteSpace(input.제공자막) ? "(없음)" : input.제공자막)}

            출력의 evidenceType은 Metadata, Transcript, Frame, Multimodal 중 하나여야 한다.
            timestampSeconds는 근거 프레임 시각을 알 수 있을 때만 입력하고 아니면 null로 둔다.
            confidence는 0부터 1까지의 보수적인 값으로 반환한다.
            """;

    private static YouTube영상재료인지Engine후보? ToCandidate(ModelIngredient value)
    {
        var name = NormalizeOptional(value.DisplayName, 200);
        var normalizedName = NormalizeOptional(value.NormalizedName, 200) ?? name;
        if (name is null || normalizedName is null)
        {
            return null;
        }

        var evidenceType = YouTube재료인지근거유형코드.전체.Contains(value.EvidenceType)
            ? value.EvidenceType
            : YouTube재료인지근거유형코드.복합;
        var evidence = NormalizeOptional(value.Evidence, 500);
        if (evidence is null)
        {
            return null;
        }

        return new YouTube영상재료인지Engine후보(
            name,
            normalizedName,
            value.TimestampSeconds is < 0 ? null : value.TimestampSeconds,
            evidenceType,
            evidence,
            Math.Clamp(value.Confidence, 0m, 1m));
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        var normalized = string.IsNullOrWhiteSpace(value)
            ? null
            : string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return normalized is null
            ? null
            : normalized[..Math.Min(normalized.Length, maxLength)];
    }

    private static JsonElement CreateOutputSchema()
    {
        using var document = JsonDocument.Parse(
            """
            {
              "type": "object",
              "additionalProperties": false,
              "properties": {
                "ingredients": {
                  "type": "array",
                  "maxItems": 20,
                  "items": {
                    "type": "object",
                    "additionalProperties": false,
                    "properties": {
                      "displayName": { "type": "string" },
                      "normalizedName": { "type": "string" },
                      "evidenceType": {
                        "type": "string",
                        "enum": ["Metadata", "Transcript", "Frame", "Multimodal"]
                      },
                      "evidence": { "type": "string" },
                      "timestampSeconds": { "type": ["integer", "null"] },
                      "confidence": { "type": "number", "minimum": 0, "maximum": 1 }
                    },
                    "required": [
                      "displayName",
                      "normalizedName",
                      "evidenceType",
                      "evidence",
                      "timestampSeconds",
                      "confidence"
                    ]
                  }
                },
                "uncertaintyNote": { "type": "string" }
              },
              "required": ["ingredients", "uncertaintyNote"]
            }
            """);
        return document.RootElement.Clone();
    }

    private sealed class ModelOutput
    {
        public IReadOnlyList<ModelIngredient?>? Ingredients { get; set; } = [];
        public string UncertaintyNote { get; set; } = string.Empty;
    }

    private sealed class ModelIngredient
    {
        public string DisplayName { get; set; } = string.Empty;
        public string NormalizedName { get; set; } = string.Empty;
        public string EvidenceType { get; set; } = string.Empty;
        public string Evidence { get; set; } = string.Empty;
        public int? TimestampSeconds { get; set; }
        public decimal Confidence { get; set; }
    }
}

public sealed class YouTube영상재료자동인지Service : IYouTube영상재료자동인지Service
{
    private static readonly IReadOnlySet<string> SupportedImageTypes = new HashSet<string>(
        ["image/jpeg", "image/png", "image/webp"],
        StringComparer.OrdinalIgnoreCase);

    private readonly IYouTube음식상품발견저장소 _저장소;
    private readonly IYouTube영상재료인지Engine _engine;
    private readonly YouTubeOptions _options;

    public YouTube영상재료자동인지Service(
        IYouTube음식상품발견저장소 저장소,
        IYouTube영상재료인지Engine engine,
        IOptions<YouTubeOptions> options)
    {
        _저장소 = 저장소;
        _engine = engine;
        _options = options.Value;
    }

    public async Task<YouTube영상재료자동인지결과Dto> 분석Async(
        string videoId,
        YouTube영상재료자동인지요청 요청,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(요청);
        var normalizedVideoId = NormalizeRequired(videoId, nameof(videoId), 100);
        if (!_options.AutomaticIngredientRecognitionEnabled)
        {
            return Failed(normalizedVideoId, "YouTube 자동 재료 인지 기능이 비활성화되어 있습니다.");
        }

        if (!요청.분석권한확인)
        {
            throw new ArgumentException(
                "자막과 영상 프레임을 분석할 권한이 있음을 확인해야 합니다.",
                nameof(요청.분석권한확인));
        }

        var video = await _저장소.영상추적조회Async(normalizedVideoId, cancellationToken)
            ?? throw new InvalidOperationException("자동 재료 인지를 실행할 YouTube 영상을 찾지 못했습니다.");
        if (video.감시채널?.음식채널여부 != true)
        {
            throw new InvalidOperationException("음식 채널로 검수된 영상만 자동 재료 인지를 실행할 수 있습니다.");
        }

        var transcript = NormalizeTranscript(요청.제공자막);
        var frames = 요청.프레임목록 ?? [];
        var maxFrames = Math.Clamp(_options.MaxIngredientRecognitionFrames, 1, 12);
        if (frames.Count > maxFrames)
        {
            throw new ArgumentOutOfRangeException(
                nameof(요청.프레임목록),
                $"한 번에 분석할 수 있는 영상 프레임은 최대 {maxFrames}장입니다.");
        }

        if (frames.Count == 0 && transcript is null)
        {
            throw new ArgumentException("자동 영상 인지에는 권한이 확인된 프레임 또는 자막이 필요합니다.");
        }

        var normalizedFrames = frames
            .Select(frame => new YouTube영상재료인지프레임입력(
                frame.영상구간초,
                NormalizeFrameDataUrl(frame)))
            .ToArray();
        var engineResult = await _engine.인지Async(
            new YouTube영상재료인지Engine입력(
                video.VideoId,
                video.제목,
                video.설명,
                transcript,
                normalizedFrames),
            cancellationToken);
        if (!engineResult.성공)
        {
            return Failed(
                normalizedVideoId,
                engineResult.실패사유 ?? "자동 재료 인지를 실행하지 못했습니다.",
                engineResult.모델,
                normalizedFrames.Length,
                transcript is not null);
        }

        var minimumConfidence = Math.Clamp(_options.MinimumIngredientRecognitionConfidence, 0m, 1m);
        var detections = engineResult.후보목록
            .Where(candidate => candidate.신뢰도 >= minimumConfidence)
            .ToArray();
        var responseItems = new List<YouTube영상재료인지항목Dto>(detections.Length);
        var addedCount = 0;
        var duplicateCount = 0;
        foreach (var detection in detections)
        {
            var productKey = BuildProductKey(detection.표준재료명);
            var isDuplicate = await _저장소.상품후보중복여부Async(
                video.Id,
                productKey,
                cancellationToken);
            if (isDuplicate)
            {
                duplicateCount++;
            }
            else
            {
                var now = DateTime.UtcNow;
                _저장소.상품후보추가(new YouTube영상상품후보
                {
                    YouTube채널영상Id = video.Id,
                    영상 = video,
                    상품키 = productKey,
                    상품명 = detection.재료명,
                    온도코드 = "검토필요",
                    물류방식 = "검토필요",
                    후보유형 = YouTube상품후보유형코드.식재료,
                    영상구간초 = detection.영상구간초,
                    발견근거 = $"[{detection.근거유형}] {detection.발견근거}",
                    추출방식 = ToExtractionMethod(detection.근거유형),
                    신뢰도 = detection.신뢰도,
                    검수상태 = YouTube상품후보검수상태코드.대기,
                    협찬표시상태 = YouTube협찬표시상태코드.미확인,
                    허용의향유형 = string.Join(',',
                        YouTube상품구매의향유형코드.구매관심,
                        YouTube상품구매의향유형코드.수입검토),
                    검수메모 = string.IsNullOrWhiteSpace(engineResult.모델)
                        ? "자동 재료 인지 결과"
                        : $"자동 재료 인지 모델: {engineResult.모델}",
                    생성일시Utc = now,
                    수정일시Utc = now
                });
                addedCount++;
            }

            responseItems.Add(new YouTube영상재료인지항목Dto(
                detection.재료명,
                detection.표준재료명,
                detection.영상구간초,
                detection.근거유형,
                detection.발견근거,
                detection.신뢰도,
                !isDuplicate));
        }

        if (addedCount > 0)
        {
            await _저장소.저장Async(cancellationToken);
        }

        return new YouTube영상재료자동인지결과Dto(
            normalizedVideoId,
            true,
            engineResult.모델,
            normalizedFrames.Length,
            transcript is not null,
            detections.Length,
            addedCount,
            duplicateCount,
            responseItems,
            addedCount > 0
                ? $"자동으로 알아차린 재료 {addedCount}건을 검수 대기 후보로 등록했습니다."
                : "신규로 등록할 자동 재료 인지 후보가 없습니다.");
    }

    private string? NormalizeTranscript(string? value)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        var maxCharacters = Math.Clamp(
            _options.MaxIngredientRecognitionTranscriptCharacters,
            1000,
            50_000);
        if (normalized?.Length > maxCharacters)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                $"제공 자막은 {maxCharacters:N0}자 이하여야 합니다.");
        }

        return normalized;
    }

    private string NormalizeFrameDataUrl(YouTube영상재료인지업로드프레임 frame)
    {
        if (frame.영상구간초 is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(frame), "영상 프레임 시각은 0초 이상이어야 합니다.");
        }

        if (!SupportedImageTypes.Contains(frame.콘텐츠유형))
        {
            throw new ArgumentException("영상 프레임은 JPEG, PNG 또는 WEBP 형식이어야 합니다.", nameof(frame));
        }

        var maxBytes = Math.Clamp(_options.MaxIngredientRecognitionFrameBytes, 64 * 1024, 8 * 1024 * 1024);
        if (frame.콘텐츠.Length == 0 || frame.콘텐츠.Length > maxBytes)
        {
            throw new ArgumentOutOfRangeException(
                nameof(frame),
                $"영상 프레임은 비어 있지 않아야 하며 장당 {maxBytes:N0}바이트 이하여야 합니다.");
        }

        using var data = SKData.CreateCopy(frame.콘텐츠);
        using var codec = SKCodec.Create(data)
            ?? throw new ArgumentException("해석할 수 없는 영상 프레임입니다.", nameof(frame));
        var info = codec.Info;
        if (info.Width is < 1 or > 2048
            || info.Height is < 1 or > 2048
            || (long)info.Width * info.Height > 4_194_304)
        {
            throw new ArgumentOutOfRangeException(
                nameof(frame),
                "영상 프레임 해상도는 각 변 2,048픽셀, 전체 4,194,304픽셀 이하여야 합니다.");
        }

        using var bitmap = SKBitmap.Decode(data)
            ?? throw new ArgumentException("영상 프레임 디코딩에 실패했습니다.", nameof(frame));
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Jpeg, 85);
        return $"data:image/jpeg;base64,{Convert.ToBase64String(encoded.ToArray())}";
    }

    private static string ToExtractionMethod(string evidenceType)
        => evidenceType switch
        {
            YouTube재료인지근거유형코드.메타데이터 => YouTube상품후보추출방식코드.메타데이터자동인지,
            YouTube재료인지근거유형코드.자막 => YouTube상품후보추출방식코드.자막자동인지,
            _ => YouTube상품후보추출방식코드.영상프레임자동인지
        };

    private static string BuildProductKey(string normalizedName)
    {
        var normalized = normalizedName.Trim().ToUpperInvariant();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return $"youtube-ingredient:{Convert.ToHexString(hash).ToLowerInvariant()}";
    }

    private static string NormalizeRequired(string? value, string parameterName, int maxLength)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        if (normalized is null)
        {
            throw new ArgumentException("필수 입력값입니다.", parameterName);
        }

        if (normalized.Length > maxLength)
        {
            throw new ArgumentOutOfRangeException(parameterName, $"입력값은 {maxLength}자 이하여야 합니다.");
        }

        return normalized;
    }

    private static YouTube영상재료자동인지결과Dto Failed(
        string videoId,
        string message,
        string? model = null,
        int frameCount = 0,
        bool transcriptUsed = false)
        => new(
            videoId,
            false,
            model,
            frameCount,
            transcriptUsed,
            0,
            0,
            0,
            [],
            message);
}
