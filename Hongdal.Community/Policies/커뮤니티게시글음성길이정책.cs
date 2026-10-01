namespace Hongdal.Services.Community;

public sealed record 커뮤니티게시글음성길이판정(
    bool 음성화대상,
    int 글자수,
    int 최소글자수,
    int 최대글자수미만);

public static class 커뮤니티게시글음성길이정책
{
    public static 커뮤니티게시글음성길이판정 판정(
        int 글자수,
        int 최소글자수,
        int 최대글자수미만)
    {
        var normalizedMin = Math.Max(1, 최소글자수);
        var normalizedMaxExclusive = Math.Max(normalizedMin + 1, 최대글자수미만);
        return new 커뮤니티게시글음성길이판정(
            글자수 >= normalizedMin && 글자수 < normalizedMaxExclusive,
            Math.Max(0, 글자수),
            normalizedMin,
            normalizedMaxExclusive);
    }
}
