using System.Security.Cryptography;
using System.Text;
using Hongdal.Contracts.Common.Content;
using Hongdal.Contracts.Common.Orderer;
using Hongdal.Domain.Content;
using Hongdal.Services.Orderer;

namespace Hongdal.Services.Content;

public interface IYouTube음식상품발견Service
{
    Task<IReadOnlyList<YouTube음식채널Dto>> 음식채널목록조회Async(
        int take,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<YouTube음식채널Dto>> 음식채널목록조회Async(
        string? 국가코드,
        int take,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<YouTube음식채널국가집계Dto>> 음식채널국가집계조회Async(
        CancellationToken cancellationToken);

    Task<IReadOnlyList<YouTube상품후보Dto>> 상품후보목록조회Async(
        string? 검수상태,
        int take,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<YouTube상품후보Dto>> 공개상품후보목록조회Async(
        string? channelId,
        string? 후보유형,
        int take,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<YouTube상품후보Dto>> 공개상품후보목록조회Async(
        string? channelId,
        string? 국가코드,
        string? 후보유형,
        int take,
        CancellationToken cancellationToken);

    Task<YouTube상품후보Dto> 상품후보등록Async(
        YouTube상품후보등록요청Dto 요청,
        CancellationToken cancellationToken);

    Task<YouTube상품후보Dto> 상품후보검수Async(
        long 후보Id,
        YouTube상품후보검수요청Dto 요청,
        string 검수자UserId,
        CancellationToken cancellationToken);

    Task<공동구매처리결과<YouTube상품구매의향응답Dto>> 구매의향등록Async(
        long 후보Id,
        YouTube상품구매의향등록요청Dto 요청,
        string 사용자Id,
        string 사용자표시명,
        CancellationToken cancellationToken);
}

public sealed class YouTube음식상품발견Service : IYouTube음식상품발견Service
{
    private readonly IYouTube음식상품발견저장소 _저장소;
    private readonly I공동구매자동집단화UseCase _공동구매UseCase;

    public YouTube음식상품발견Service(
        IYouTube음식상품발견저장소 저장소,
        I공동구매자동집단화UseCase 공동구매UseCase)
    {
        _저장소 = 저장소;
        _공동구매UseCase = 공동구매UseCase;
    }

    public async Task<IReadOnlyList<YouTube음식채널Dto>> 음식채널목록조회Async(
        int take,
        CancellationToken cancellationToken)
        => await 음식채널목록조회Async(null, take, cancellationToken);

    public async Task<IReadOnlyList<YouTube음식채널Dto>> 음식채널목록조회Async(
        string? 국가코드,
        int take,
        CancellationToken cancellationToken)
        => (await _저장소.음식채널목록조회Async(
                NormalizeCollectionCountryCode(국가코드),
                take,
                cancellationToken))
            .Select(ToFoodChannelDto)
            .ToArray();

    public async Task<IReadOnlyList<YouTube음식채널국가집계Dto>> 음식채널국가집계조회Async(
        CancellationToken cancellationToken)
        => (await _저장소.음식채널국가집계대상조회Async(cancellationToken))
            .GroupBy(
                channel => YouTube채널수집국가코드.정규화(channel.국가코드),
                StringComparer.Ordinal)
            .Select(group => new YouTube음식채널국가집계Dto(
                group.Key,
                YouTube채널수집국가코드.표시명(group.Key),
                group.Count(),
                group.Count(channel => channel.초기동기화완료여부),
                group.Max(channel => channel.마지막동기화일시Utc)))
            .OrderBy(item => item.국가코드 == YouTube채널수집국가코드.한국
                ? 0
                : item.국가코드 == YouTube채널수집국가코드.미국 ? 1 : 2)
            .ThenBy(item => item.국가코드, StringComparer.Ordinal)
            .ToArray();

    public async Task<IReadOnlyList<YouTube상품후보Dto>> 상품후보목록조회Async(
        string? 검수상태,
        int take,
        CancellationToken cancellationToken)
    {
        var normalizedStatus = string.IsNullOrWhiteSpace(검수상태) ? null : 검수상태.Trim();
        if (normalizedStatus is not null && !YouTube상품후보검수상태코드.전체.Contains(normalizedStatus))
        {
            throw new ArgumentException("지원하지 않는 상품 후보 검수 상태입니다.", nameof(검수상태));
        }

        return (await _저장소.상품후보목록조회Async(normalizedStatus, take, cancellationToken))
            .Select(ToProductCandidateDto)
            .ToArray();
    }

    public async Task<IReadOnlyList<YouTube상품후보Dto>> 공개상품후보목록조회Async(
        string? channelId,
        string? 후보유형,
        int take,
        CancellationToken cancellationToken)
        => await 공개상품후보목록조회Async(
            channelId,
            null,
            후보유형,
            take,
            cancellationToken);

    public async Task<IReadOnlyList<YouTube상품후보Dto>> 공개상품후보목록조회Async(
        string? channelId,
        string? 국가코드,
        string? 후보유형,
        int take,
        CancellationToken cancellationToken)
    {
        var normalizedType = string.IsNullOrWhiteSpace(후보유형) ? null : 후보유형.Trim();
        if (normalizedType is not null && !YouTube상품후보유형코드.전체.Contains(normalizedType))
        {
            throw new ArgumentException("지원하지 않는 YouTube 상품 후보 유형입니다.", nameof(후보유형));
        }

        return (await _저장소.공개상품후보목록조회Async(
                NormalizeOptional(channelId, 100),
                NormalizeCollectionCountryCode(국가코드),
                normalizedType,
                take,
                cancellationToken))
            .Select(ToProductCandidateDto)
            .ToArray();
    }

    public async Task<YouTube상품후보Dto> 상품후보등록Async(
        YouTube상품후보등록요청Dto 요청,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(요청);
        var videoId = NormalizeRequired(요청.VideoId, nameof(요청.VideoId), 100);
        var productKey = NormalizeRequired(요청.상품키, nameof(요청.상품키), 200);
        var video = await _저장소.영상추적조회Async(videoId, cancellationToken)
            ?? throw new InvalidOperationException("상품 후보를 연결할 YouTube 영상을 찾지 못했습니다.");
        if (video.감시채널?.음식채널여부 != true)
        {
            throw new InvalidOperationException("음식 채널로 검수된 영상에만 상품 후보를 등록할 수 있습니다.");
        }

        if (await _저장소.상품후보중복여부Async(video.Id, productKey, cancellationToken))
        {
            throw new InvalidOperationException("이 영상에는 같은 상품키의 후보가 이미 등록되어 있습니다.");
        }

        var candidateType = NormalizeRequired(요청.후보유형, nameof(요청.후보유형), 40);
        if (!YouTube상품후보유형코드.전체.Contains(candidateType))
        {
            throw new ArgumentException("지원하지 않는 YouTube 상품 후보 유형입니다.", nameof(요청.후보유형));
        }

        if (요청.영상구간초 is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(요청.영상구간초), "영상 구간은 0초 이상이어야 합니다.");
        }

        if (요청.신뢰도 is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(요청.신뢰도), "신뢰도는 0부터 1까지 입력해야 합니다.");
        }

        var extractionMethod = NormalizeRequired(요청.추출방식, nameof(요청.추출방식), 40);
        if (!YouTube상품후보추출방식코드.전체.Contains(extractionMethod))
        {
            throw new ArgumentException("지원하지 않는 상품 후보 추출 방식입니다.", nameof(요청.추출방식));
        }

        var now = DateTime.UtcNow;
        var candidate = new YouTube영상상품후보
        {
            YouTube채널영상Id = video.Id,
            영상 = video,
            상품키 = productKey,
            상품명 = NormalizeRequired(요청.상품명, nameof(요청.상품명), 300),
            브랜드명 = NormalizeOptional(요청.브랜드명, 200),
            원산지국가코드 = NormalizeCountryCode(요청.원산지국가코드),
            HS코드후보 = NormalizeOptional(요청.HS코드후보, 20),
            온도코드 = NormalizeRequired(요청.온도코드, nameof(요청.온도코드), 30),
            물류방식 = NormalizeRequired(요청.물류방식, nameof(요청.물류방식), 30),
            후보유형 = candidateType,
            영상구간초 = 요청.영상구간초,
            발견근거 = NormalizeRequired(요청.발견근거, nameof(요청.발견근거), 4000),
            추출방식 = extractionMethod,
            신뢰도 = 요청.신뢰도,
            검수상태 = YouTube상품후보검수상태코드.대기,
            협찬표시상태 = YouTube협찬표시상태코드.미확인,
            허용의향유형 = JoinIntentTypes(요청.허용의향유형목록, requireAny: true),
            생성일시Utc = now,
            수정일시Utc = now
        };

        _저장소.상품후보추가(candidate);
        await _저장소.저장Async(cancellationToken);
        return ToProductCandidateDto(candidate);
    }

    public async Task<YouTube상품후보Dto> 상품후보검수Async(
        long 후보Id,
        YouTube상품후보검수요청Dto 요청,
        string 검수자UserId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(요청);
        var candidate = await _저장소.상품후보추적조회Async(후보Id, cancellationToken)
            ?? throw new InvalidOperationException("검수할 YouTube 상품 후보를 찾지 못했습니다.");
        var status = NormalizeRequired(요청.검수상태, nameof(요청.검수상태), 30);
        if (!YouTube상품후보검수상태코드.전체.Contains(status))
        {
            throw new ArgumentException("지원하지 않는 상품 후보 검수 상태입니다.", nameof(요청.검수상태));
        }

        var disclosure = NormalizeRequired(요청.협찬표시상태, nameof(요청.협찬표시상태), 30);
        if (!YouTube협찬표시상태코드.전체.Contains(disclosure))
        {
            throw new ArgumentException("지원하지 않는 협찬 표시 상태입니다.", nameof(요청.협찬표시상태));
        }

        candidate.검수상태 = status;
        candidate.협찬표시상태 = disclosure;
        candidate.공식구매Url = NormalizeHttpsUrl(요청.공식구매Url, nameof(요청.공식구매Url));
        candidate.검수메모 = NormalizeOptional(요청.검수메모, 1000);
        candidate.원산지국가코드 = NormalizeCountryCode(요청.원산지국가코드);
        candidate.HS코드후보 = NormalizeOptional(요청.HS코드후보, 20);
        candidate.온도코드 = NormalizeRequired(요청.온도코드, nameof(요청.온도코드), 30);
        candidate.물류방식 = NormalizeRequired(요청.물류방식, nameof(요청.물류방식), 30);
        candidate.허용의향유형 = JoinIntentTypes(
            요청.허용의향유형목록,
            requireAny: status == YouTube상품후보검수상태코드.승인);
        candidate.검수자UserId = NormalizeRequired(검수자UserId, nameof(검수자UserId), 450);
        candidate.검수일시Utc = DateTime.UtcNow;
        candidate.수정일시Utc = candidate.검수일시Utc.Value;

        await _저장소.저장Async(cancellationToken);
        return ToProductCandidateDto(candidate);
    }

    public async Task<공동구매처리결과<YouTube상품구매의향응답Dto>> 구매의향등록Async(
        long 후보Id,
        YouTube상품구매의향등록요청Dto 요청,
        string 사용자Id,
        string 사용자표시명,
        CancellationToken cancellationToken)
    {
        if (요청 is null)
        {
            return 공동구매처리결과<YouTube상품구매의향응답Dto>.잘못된요청("구매 의향 요청이 필요합니다.");
        }

        var candidate = await _저장소.상품후보추적조회Async(후보Id, cancellationToken);
        if (candidate is null
            || candidate.검수상태 != YouTube상품후보검수상태코드.승인
            || candidate.영상?.공유상태 != YouTube채널영상.공개상태
            || candidate.영상.감시채널?.음식채널여부 != true
            || candidate.영상.감시채널.활성화여부 != true)
        {
            return 공동구매처리결과<YouTube상품구매의향응답Dto>.찾을수없음(
                "구매 의향을 등록할 수 있는 공개 상품 후보를 찾지 못했습니다.");
        }

        var intentType = 요청.의향유형?.Trim() ?? string.Empty;
        if (!YouTube상품구매의향유형코드.전체.Contains(intentType))
        {
            return 공동구매처리결과<YouTube상품구매의향응답Dto>.잘못된요청(
                "지원하지 않는 구매 의향 유형입니다.");
        }

        if (!SplitIntentTypes(candidate.허용의향유형).Contains(intentType, StringComparer.Ordinal))
        {
            return 공동구매처리결과<YouTube상품구매의향응답Dto>.잘못된요청(
                "이 상품 후보에는 선택한 구매 의향 유형이 허용되지 않습니다.");
        }

        if (string.IsNullOrWhiteSpace(요청.배송권키) || 요청.희망수량 <= 0)
        {
            return 공동구매처리결과<YouTube상품구매의향응답Dto>.잘못된요청(
                "배송권키와 0보다 큰 희망수량을 입력해야 합니다.");
        }

        try
        {
            var command = new 공동구매자동수요등록Command
            {
                수요출처키 = CreateDemandSourceKey(candidate.Id, 사용자Id),
                상품키 = candidate.상품키,
                상품명 = candidate.상품명,
                HS코드 = candidate.HS코드후보 ?? string.Empty,
                온도코드 = candidate.온도코드,
                물류방식 = candidate.물류방식,
                주문자키 = NormalizeRequired(사용자Id, nameof(사용자Id), 450),
                주문자표시명 = NormalizeRequired(사용자표시명, nameof(사용자표시명), 200),
                배송권키 = NormalizeRequired(요청.배송권키, nameof(요청.배송권키), 200),
                배송권명 = NormalizeOptional(요청.배송권명, 200) ?? 요청.배송권키.Trim(),
                도착창고Id = 요청.도착창고Id,
                도착창고유형 = NormalizeOptional(요청.도착창고유형, 100) ?? string.Empty,
                도착창고명 = NormalizeOptional(요청.도착창고명, 200) ?? string.Empty,
                수령지주소참조키 = NormalizeOptional(요청.수령지주소참조키, 300) ?? string.Empty,
                수령지표시명 = NormalizeOptional(요청.수령지표시명, 200) ?? string.Empty,
                희망수량 = 요청.희망수량,
                수량단위 = NormalizeRequired(요청.수량단위, nameof(요청.수량단위), 30),
                수요유형 = 공동구매자동수요유형코드.관심표시,
                결제상태 = 공동구매자동결제상태코드.미결제,
                메모 = BuildDemandMemo(candidate.Id, intentType, 요청.메모),
                목표참여자수 = 요청.목표참여자수,
                목표수량 = 요청.목표수량
            };

            var groupResult = await _공동구매UseCase.수요등록Async(command, cancellationToken);
            if (!groupResult.성공 || groupResult.값 is null)
            {
                return groupResult.상태코드 == 404
                    ? 공동구매처리결과<YouTube상품구매의향응답Dto>.찾을수없음(groupResult.메시지)
                    : 공동구매처리결과<YouTube상품구매의향응답Dto>.잘못된요청(groupResult.메시지);
            }

            var group = groupResult.값;
            return 공동구매처리결과<YouTube상품구매의향응답Dto>.성공결과(new(
                candidate.Id,
                intentType,
                group.자동집단Id,
                group.현재상태,
                group.수요건수,
                group.총희망수량,
                group.수량단위,
                "구매 의향을 비결제 수요로 등록했습니다. 결제나 수입은 자동 실행되지 않습니다."));
        }
        catch (ArgumentException ex)
        {
            return 공동구매처리결과<YouTube상품구매의향응답Dto>.잘못된요청(ex.Message);
        }
    }

    internal static string CreateDemandSourceKey(long candidateId, string userId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(userId.Trim()));
        var userHash = Convert.ToHexString(hash.AsSpan(0, 12)).ToLowerInvariant();
        return $"youtube-food:{candidateId}:{userHash}";
    }

    private static string BuildDemandMemo(long candidateId, string intentType, string? note)
    {
        var prefix = $"YouTube상품후보={candidateId};의향유형={intentType};비결제검토";
        var normalizedNote = NormalizeOptional(note, 500);
        return normalizedNote is null ? prefix : $"{prefix};메모={normalizedNote}";
    }

    private static YouTube음식채널Dto ToFoodChannelDto(YouTube감시채널 channel)
        => new(
            channel.ChannelId,
            channel.채널명,
            channel.Handle,
            channel.썸네일Url,
            YouTube채널수집국가코드.정규화(channel.국가코드),
            channel.기본언어코드,
            SplitCommaSeparated(channel.음식콘텐츠분류),
            channel.구매발견점수,
            channel.수입발견점수,
            !string.IsNullOrWhiteSpace(channel.Handle)
                ? $"https://www.youtube.com/{channel.Handle}"
                : $"https://www.youtube.com/channel/{Uri.EscapeDataString(channel.ChannelId)}",
            channel.마지막영상게시일시Utc);

    private static YouTube상품후보Dto ToProductCandidateDto(YouTube영상상품후보 candidate)
    {
        var video = candidate.영상
            ?? throw new InvalidOperationException("YouTube 상품 후보의 영상 연결이 필요합니다.");
        return new YouTube상품후보Dto(
            candidate.Id,
            candidate.상품키,
            candidate.상품명,
            candidate.브랜드명,
            candidate.원산지국가코드,
            candidate.HS코드후보,
            candidate.온도코드,
            candidate.물류방식,
            candidate.후보유형,
            candidate.영상구간초,
            candidate.발견근거,
            candidate.추출방식,
            candidate.신뢰도,
            candidate.검수상태,
            candidate.협찬표시상태,
            SplitIntentTypes(candidate.허용의향유형),
            candidate.공식구매Url,
            candidate.검수메모,
            video.VideoId,
            video.제목,
            video.설명,
            video.게시일시Utc,
            video.썸네일Url,
            $"https://www.youtube.com/watch?v={Uri.EscapeDataString(video.VideoId)}",
            video.ChannelId,
            video.감시채널?.채널명 ?? string.Empty,
            YouTube채널수집국가코드.정규화(video.감시채널?.국가코드),
            candidate.생성일시Utc,
            candidate.수정일시Utc);
    }

    private static string JoinIntentTypes(IEnumerable<string>? values, bool requireAny)
    {
        var normalized = (values ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var unknown = normalized.FirstOrDefault(value => !YouTube상품구매의향유형코드.전체.Contains(value));
        if (unknown is not null)
        {
            throw new ArgumentException($"지원하지 않는 구매 의향 유형입니다: {unknown}");
        }

        if (requireAny && normalized.Length == 0)
        {
            throw new ArgumentException("승인할 상품 후보에는 하나 이상의 구매 의향 유형이 필요합니다.");
        }

        return string.Join(',', normalized);
    }

    private static IReadOnlyList<string> SplitIntentTypes(string? value)
        => SplitCommaSeparated(value)
            .Where(YouTube상품구매의향유형코드.전체.Contains)
            .ToArray();

    private static IReadOnlyList<string> SplitCommaSeparated(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Distinct(StringComparer.Ordinal)
                .ToArray();

    private static string NormalizeRequired(string? value, string parameterName, int maxLength)
    {
        var normalized = NormalizeOptional(value, maxLength);
        return normalized ?? throw new ArgumentException("필수 입력값이 비어 있습니다.", parameterName);
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        if (normalized?.Length > maxLength)
        {
            throw new ArgumentOutOfRangeException(nameof(value), $"입력값은 {maxLength}자 이하여야 합니다.");
        }

        return normalized;
    }

    private static string? NormalizeCountryCode(string? value)
    {
        var normalized = NormalizeOptional(value, 2)?.ToUpperInvariant();
        if (normalized is not null
            && (normalized.Length != 2 || normalized.Any(character => character is < 'A' or > 'Z')))
        {
            throw new ArgumentException("국가 코드는 ISO 3166-1 alpha-2 형식이어야 합니다.", nameof(value));
        }

        return normalized;
    }

    private static string? NormalizeCollectionCountryCode(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? null
            : YouTube채널수집국가코드.정규화(value);

    private static string? NormalizeHttpsUrl(string? value, string parameterName)
    {
        var normalized = NormalizeOptional(value, 1000);
        if (normalized is not null
            && (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri)
                || !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException("공식 구매 URL은 HTTPS 절대 주소여야 합니다.", parameterName);
        }

        return normalized;
    }
}
