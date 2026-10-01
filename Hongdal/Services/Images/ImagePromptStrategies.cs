using 홍달.도메인.공통;

namespace 홍달.Services.Images;

public interface I이미지프롬프트생성기
{
    string 이미지용도 { get; }
    string CreatePrompt(이미지생성요청 request);
}

public sealed class 상품상세페이지이미지프롬프트생성기 : 이미지프롬프트생성기Base
{
    public override string 이미지용도 => 생성이미지용도.상품상세페이지생성이미지;

    protected override string BuildDomainPrompt(이미지생성요청 request)
    {
        var summary = string.IsNullOrWhiteSpace(request.설명)
            ? request.제목
            : request.설명;

        return $"Generate ecommerce-ready product detail images from verified logistics assets for '{request.제목}'. Keep product identity, shape, components, labels, and package condition consistent with source assets. Use clean composition and neutral background. Context: {summary}.";
    }
}

public sealed class 이미지생성요청
{
    public string 이미지용도 { get; set; } = 생성이미지용도.화주상품사진;
    public string 대상타입 { get; set; } = string.Empty;
    public string 대상식별자 { get; set; } = string.Empty;
    public bool 샘플데이터여부 { get; set; }
    public string 제목 { get; set; } = string.Empty;
    public string? 설명 { get; set; }
    public string? 추가맥락 { get; set; }
    public string 종횡비 { get; set; } = "auto";
    public string 해상도 { get; set; } = "1K";
}

public sealed class 커뮤니티글쓰기이미지프롬프트생성기 : I이미지프롬프트생성기
{
    public string 이미지용도 => 생성이미지용도.커뮤니티글쓰기이미지;

    public string CreatePrompt(이미지생성요청 request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var authorPrompt = string.IsNullOrWhiteSpace(request.설명)
            ? request.제목
            : request.설명.Trim();
        var context = string.IsNullOrWhiteSpace(request.추가맥락)
            ? string.Empty
            : $" Context: {request.추가맥락.Trim()}.";

        return $"Create one editorial image for a community article. {authorPrompt}.{context} "
               + "Use a clear focal subject and natural composition. Do not add a watermark or brand logo. "
               + "Do not present invented labels, official seals, statistics, or documentary evidence unless explicitly requested.";
    }
}

public abstract class 이미지프롬프트생성기Base : I이미지프롬프트생성기
{
    public abstract string 이미지용도 { get; }

    public string CreatePrompt(이미지생성요청 request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var basePrompt = "Create a realistic commercial sample photo for app UI usage, clean composition, natural lighting, high detail, no watermark, no text overlay.";
        var domainPrompt = BuildDomainPrompt(request);
        var optionalContext = string.IsNullOrWhiteSpace(request.추가맥락)
            ? string.Empty
            : $" Additional context: {request.추가맥락.Trim()}.";

        return $"{basePrompt} {domainPrompt}{optionalContext}".Trim();
    }

    protected abstract string BuildDomainPrompt(이미지생성요청 request);
}

public sealed class 화주상품사진프롬프트생성기 : 이미지프롬프트생성기Base
{
    public override string 이미지용도 => 생성이미지용도.화주상품사진;

    protected override string BuildDomainPrompt(이미지생성요청 request)
    {
        return $"Product listing photo of '{request.제목}', centered subject, simple background, ecommerce style, clearly visible shape and texture. Description: {request.설명 ?? request.제목}.";
    }
}

public sealed class 기사상차인증사진프롬프트생성기 : 이미지프롬프트생성기Base
{
    public override string 이미지용도 => 생성이미지용도.기사상차인증사진;

    protected override string BuildDomainPrompt(이미지생성요청 request)
    {
        return $"A logistics loading confirmation scene for '{request.제목}', cargo being loaded onto a delivery truck, realistic field environment, documentary style, suitable as a sample proof image. Description: {request.설명 ?? request.제목}.";
    }
}

public sealed class 기사배차완료인증사진프롬프트생성기 : 이미지프롬프트생성기Base
{
    public override string 이미지용도 => 생성이미지용도.기사배차완료인증사진;

    protected override string BuildDomainPrompt(이미지생성요청 request)
    {
        return $"A delivery completion confirmation scene for '{request.제목}', package handoff completed, realistic destination environment, documentary style, suitable as a sample completion proof image. Description: {request.설명 ?? request.제목}.";
    }
}

public sealed class 음식상품썸네일프롬프트생성기 : 이미지프롬프트생성기Base
{
    public override string 이미지용도 => 생성이미지용도.음식상품썸네일;

    protected override string BuildDomainPrompt(이미지생성요청 request)
    {
        return $"Restaurant menu thumbnail for '{request.제목}', appetizing presentation, top-tier food photography, commercial menu style, vibrant but realistic colors. Description: {request.설명 ?? request.제목}.";
    }
}

public sealed class 주문후기사진프롬프트생성기 : 이미지프롬프트생성기Base
{
    public override string 이미지용도 => 생성이미지용도.주문후기사진;

    protected override string BuildDomainPrompt(이미지생성요청 request)
    {
        return $"Customer review photo for '{request.제목}', casual user-taken feeling, natural framing, authentic lifestyle shot, suitable as a sample review attachment image. Description: {request.설명 ?? request.제목}.";
    }
}

public interface 이미지프롬프트생성기Resolver
{
    I이미지프롬프트생성기 Resolve(string 이미지용도);
}

public sealed class 기본이미지프롬프트생성기Resolver : 이미지프롬프트생성기Resolver
{
    private readonly IReadOnlyDictionary<string, I이미지프롬프트생성기> _map;

    public 기본이미지프롬프트생성기Resolver(IEnumerable<I이미지프롬프트생성기> generators)
    {
        _map = generators.ToDictionary(x => x.이미지용도, StringComparer.Ordinal);
    }

    public I이미지프롬프트생성기 Resolve(string 이미지용도)
    {
        if (string.IsNullOrWhiteSpace(이미지용도))
        {
            throw new InvalidOperationException("이미지용도 is required.");
        }

        if (_map.TryGetValue(이미지용도, out var generator))
        {
            return generator;
        }

        throw new InvalidOperationException($"지원하지 않는 이미지용도입니다: {이미지용도}");
    }
}
