using System.Globalization;
using Ssalddel.Contracts.Common.Community;

namespace Ssalddel.Contracts.Common.PrivacySupport;

/// <summary>지원 목록의 읽기 조건과 안전한 복귀 경로만 보존하며 사건 처리 권한이나 명령을 만들지 않습니다.</summary>
public sealed record PrivacySupportNavigationContext
{
    public const string Root = "/privacy-support";
    public const string KindQuery = "kind";
    public const string PageQuery = "page";

    public string Kind { get; private init; } = 보호지원종류Codes.분쟁;
    public int Page { get; private init; } = 1;
    public string? ReturnPath { get; private init; }

    public static PrivacySupportNavigationContext Resolve(string? kind = null, int page = 1, string? returnPath = null)
        => new()
        {
            Kind = kind is 보호지원종류Codes.권리 or 보호지원종류Codes.사고 ? kind : 보호지원종류Codes.분쟁,
            Page = Math.Clamp(page, 1, 10_000),
            ReturnPath = PageNavigationContext.NormalizeReturnPath(returnPath)
        };

    public string ListPath(string? listRoot = null) => WithContext(ListRoot(listRoot));

    public string DetailPath(string caseId, string? listRoot = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(caseId);
        return WithContext(ListRoot(listRoot) + "/" + Uri.EscapeDataString(caseId));
    }

    public string CurrentPath(string? caseId, string? listRoot = null)
        => string.IsNullOrWhiteSpace(caseId) ? ListPath(listRoot) : DetailPath(caseId, listRoot);

    public string LoginPath(string? caseId)
        => "/login?returnUrl=" + Uri.EscapeDataString(CurrentPath(caseId));

    private string WithContext(string path)
    {
        var query = new List<string>();
        if (Kind != 보호지원종류Codes.분쟁) query.Add(KindQuery + "=" + Uri.EscapeDataString(Kind));
        if (Page > 1) query.Add(PageQuery + "=" + Page.ToString(CultureInfo.InvariantCulture));
        if (query.Count > 0) path += "?" + string.Join("&", query);
        return PageNavigationContext.WithReturnPath(path, ReturnPath);
    }

    private static string ListRoot(string? root)
    {
        var local = PageNavigationContext.NormalizeReturnPath(root);
        return local is null || local.Contains('?') || local.Contains('#') ? Root : local.TrimEnd('/');
    }
}
