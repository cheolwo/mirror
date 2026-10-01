using Hongdal.Contracts.Common.Content;
using Hongdal.Services.External.Apify;

namespace Hongdal.Services.Content;

public interface IAmazon상품참고자료Service
{
    Task<Amazon상품참고자료Dto> 미리보기Async(
        Amazon상품참고자료조회요청Dto 요청,
        CancellationToken cancellationToken);
}

public sealed class Amazon상품참고자료Service : IAmazon상품참고자료Service
{
    private readonly IApifyAmazonProductClient _client;

    public Amazon상품참고자료Service(IApifyAmazonProductClient client)
    {
        _client = client;
    }

    public async Task<Amazon상품참고자료Dto> 미리보기Async(
        Amazon상품참고자료조회요청Dto 요청,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(요청);
        var productUri = AmazonProductUrlPolicy.ValidateProductUrl(요청.상품Url);
        var inputAsin = AmazonProductUrlPolicy.ExtractAsin(productUri);
        var observedAtUtc = DateTime.UtcNow;
        var product = await _client.상품상세조회Async(productUri, cancellationToken)
            ?? throw new InvalidOperationException("Apify가 Amazon 상품 상세 결과를 반환하지 않았습니다.");

        var countryCode = product.국가코드 ?? AmazonProductUrlPolicy.ResolveCountryCode(productUri.Host);
        var canonicalUrl = AmazonProductUrlPolicy.ValidateReturnedUrl(product.원문Url, productUri).AbsoluteUri;
        var referenceKey = $"amazon:{countryCode.ToLowerInvariant()}:{product.Asin.ToLowerInvariant()}";
        var currency = product.현재가격.통화코드
            ?? product.정가.통화코드
            ?? product.배송비.통화코드;
        var externalReferences = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["SourceProvider"] = "Apify",
            ["SourceMarketplace"] = "Amazon",
            ["AmazonAsin"] = product.Asin,
            ["AmazonInputAsin"] = inputAsin,
            ["AmazonProductUrl"] = canonicalUrl,
            ["MarketplaceCountryCode"] = countryCode,
            ["ObservedAtUtc"] = observedAtUtc.ToString("O")
        };

        return new Amazon상품참고자료Dto(
            referenceKey,
            product.Asin,
            product.상품명,
            product.브랜드명,
            canonicalUrl,
            countryCode,
            new 외부상품가격스냅샷Dto(
                product.현재가격.금액,
                product.정가.금액,
                product.배송비.금액,
                currency),
            product.재고여부,
            product.재고표시문구,
            product.평점,
            product.리뷰수,
            product.카테고리경로,
            product.썸네일Url,
            product.이미지Url목록,
            product.특징목록,
            product.속성목록
                .Select(attribute => new 외부상품속성Dto(attribute.항목명, attribute.값))
                .ToArray(),
            observedAtUtc,
            외부상품참고자료검수상태코드.대기,
            externalReferences,
            "Amazon 페이지의 외부 관측 자료입니다. 가격·재고·브랜드·원산지·수입 가능성을 Hongdal의 확정 상품 정보로 자동 전환하지 말고 운영자 검수와 참여자 직접 판단에만 사용하세요.");
    }

}
