using Hongdal.Contracts.Common.Community;
using System.Security.Cryptography;

namespace Hongdal.Services.Community;

internal static class CommunityPostingIdentityPolicy
{
    public static string ResolveNickname(
        string category,
        string? requestedNickname,
        string? existingNickname,
        string? userId)
    {
        if (!string.IsNullOrWhiteSpace(userId)
            || CommunityBoardCatalog.Find(category)?.AllowsAnonymousPosting != true)
        {
            return Normalize(requestedNickname, "익명", 40);
        }

        var baseName = CommunityAnonymousNicknameCatalog.ResolveBaseName(category);
        if (!string.IsNullOrWhiteSpace(existingNickname)
            && existingNickname.StartsWith(baseName, StringComparison.Ordinal))
        {
            return Normalize(existingNickname, baseName, 40);
        }

        var discriminator = Convert.ToHexString(RandomNumberGenerator.GetBytes(2));
        return CommunityAnonymousNicknameCatalog.Create(category, discriminator);
    }

    public static bool RequiresSuppliedNickname(string category, string? userId)
        => !string.IsNullOrWhiteSpace(userId)
           || CommunityBoardCatalog.Find(category)?.AllowsAnonymousPosting != true;

    public static string? ValidateComment(
        PlatformCommunityPostCommentCreateRequest request,
        bool requiresSuppliedNickname)
        => Validate(
            request.Nickname,
            request.Password,
            request.Body,
            "댓글",
            requiresSuppliedNickname);

    public static string? ValidateAttachmentComment(
        PlatformCommunityPostAttachmentCommentCreateRequest request,
        bool requiresSuppliedNickname)
        => Validate(
            request.Nickname,
            request.Password,
            request.Body,
            "첨부 댓글",
            requiresSuppliedNickname);

    private static string? Validate(
        string? nickname,
        string? password,
        string? body,
        string bodyLabel,
        bool requiresSuppliedNickname)
    {
        if ((requiresSuppliedNickname && string.IsNullOrWhiteSpace(nickname))
            || (!string.IsNullOrWhiteSpace(nickname) && nickname.Trim().Length > 40))
        {
            return "닉네임은 1자 이상 40자 이하로 입력해야 합니다.";
        }

        if (string.IsNullOrWhiteSpace(password)
            || password.Trim().Length < 4
            || password.Trim().Length > 100)
        {
            return "비밀번호는 4자 이상 100자 이하로 입력해야 합니다.";
        }

        if (string.IsNullOrWhiteSpace(body) || body.Trim().Length > 1000)
        {
            return $"{bodyLabel}은 1자 이상 1000자 이하로 입력해야 합니다.";
        }

        return null;
    }

    public static string Normalize(string? value, string fallback, int maxLength)
    {
        var text = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        return text.Length <= maxLength ? text : text[..maxLength];
    }
}
