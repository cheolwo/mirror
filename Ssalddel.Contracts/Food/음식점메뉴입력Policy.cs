namespace Ssalddel.Contracts.Food;

/// <summary>메뉴 저장 경계와 화면이 공유하는 입력 규칙. URL을 다운로드하지 않는다.</summary>
public static class 음식점메뉴입력Policy
{
    public const int 메뉴명최대길이 = 200;
    public const int 설명최대길이 = 1000;
    public const int 사진주소최대길이 = 1000;

    public static string? 오류조회(string? 메뉴명, string? 설명, decimal 판매가, string? 사진주소)
    {
        if (string.IsNullOrWhiteSpace(메뉴명)) return "메뉴명이 필요합니다.";
        if (메뉴명.Trim().Length > 메뉴명최대길이) return "메뉴명은 200자 이하여야 합니다.";
        if ((설명?.Trim().Length ?? 0) > 설명최대길이) return "메뉴 소개는 1000자 이하여야 합니다.";
        if (판매가 < 0) return "판매가는 0 이상이어야 합니다.";
        if (string.IsNullOrWhiteSpace(사진주소)) return null;
        var 주소 = 사진주소.Trim();
        if (주소.Length > 사진주소최대길이) return "사진 주소는 1000자 이하여야 합니다.";
        if (!Uri.TryCreate(주소, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || string.IsNullOrEmpty(uri.Host))
            return "사진 주소는 HTTPS 절대 주소여야 합니다.";
        return null;
    }
}
