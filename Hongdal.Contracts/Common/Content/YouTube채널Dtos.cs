namespace Hongdal.Contracts.Common.Content;

public sealed record YouTube감시채널등록요청Dto(
    string ChannelId,
    string? 표시이름 = null,
    string? 국가코드 = null,
    bool 음식채널여부 = false);

public static class YouTube채널수집국가코드
{
    public const string 한국 = "KR";
    public const string 미국 = "US";
    public const string 미분류 = "ZZ";

    public static string 정규화(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return 미분류;
        }

        var normalized = value.Trim().ToUpperInvariant();
        if (normalized.Length != 2 || normalized.Any(character => character is < 'A' or > 'Z'))
        {
            throw new ArgumentException("국가 코드는 ISO 3166-1 alpha-2 형식이어야 합니다.", nameof(value));
        }

        return normalized;
    }

    public static string 표시명(string value)
        => 정규화(value) switch
        {
            한국 => "한국",
            미국 => "미국",
            미분류 => "미분류",
            var code => code
        };
}

public sealed record YouTube영상공개설정요청Dto(bool 공개여부);

public sealed record YouTube반야게시채널설정요청Dto(bool 허용여부);

public sealed record YouTube지식성찰채널프로필설정요청Dto(
    bool 지식성찰채널여부,
    string? Handle,
    string 국가코드,
    string 기본언어코드,
    IReadOnlyList<string> 주제코드목록,
    string 관점표시,
    string? 공식출처Url,
    DateTime? 자료확인일시Utc);

public sealed record YouTube감시채널Dto(
    string ChannelId,
    string 채널명,
    string? 썸네일Url,
    bool 활성화여부,
    bool 초기동기화완료여부,
    DateTime? 마지막동기화일시Utc,
    string? 마지막영상Id,
    DateTime? 마지막영상게시일시Utc,
    bool 음식채널여부,
    string? Handle,
    string 국가코드,
    string 기본언어코드,
    IReadOnlyList<string> 음식콘텐츠분류목록,
    int 구매발견점수,
    int 수입발견점수,
    string? 조사근거Url,
    string? 조사메모,
    DateTime? 조사확인일시Utc,
    bool 지식성찰채널여부,
    IReadOnlyList<string> 지식성찰분류목록,
    string 관점표시,
    string? 공식출처Url,
    DateTime? 자료확인일시Utc,
    bool 반야게시허용여부);

public sealed record YouTube채널영상Dto(
    string VideoId,
    string ChannelId,
    string 채널명,
    string 채널국가코드,
    string 제목,
    string 설명,
    DateTime 게시일시Utc,
    string? 썸네일Url,
    string 시청Url,
    bool 신규업로드여부,
    string 공유상태,
    DateTime 최초감지일시Utc);

public sealed record YouTube채널동기화결과Dto(
    bool 실행됨,
    int 처리채널수,
    int 수신영상수,
    int 추가영상수,
    int 신규업로드수,
    DateTime? 완료일시Utc,
    string 메시지);

public sealed record YouTube국가별채널동기화결과Dto(
    string 국가코드,
    string 국가표시명,
    YouTube채널동기화결과Dto 동기화결과);

public sealed record YouTube재생목록Dto(
    string PlaylistId,
    string ChannelId,
    string 제목,
    string 설명,
    DateTime 게시일시Utc,
    int 영상수,
    string? 썸네일Url,
    string 재생목록Url);

public sealed record YouTube재생목록영상Dto(
    string VideoId,
    string ChannelId,
    string 제목,
    string 설명,
    DateTime 게시일시Utc,
    string? 썸네일Url,
    string 시청Url);
