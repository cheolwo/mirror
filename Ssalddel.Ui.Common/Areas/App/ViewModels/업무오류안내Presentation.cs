namespace Ssalddel.Ui.Common.Areas.App.ViewModels;

/// <summary>기술 원문은 분류에만 사용하고, 사용자에게는 복구 행동만 안내합니다.</summary>
public sealed record 업무오류안내Presentation(string 분류, string 제목, string 설명, string 재조회문구 = "다시 시도")
{
    public bool 재조회허용 { get; init; } = true;
    public bool 복귀허용 { get; init; } = true;

    /// <summary>서버 사유와 현재 업무 단계로 표시합니다. 오류 메시지를 분석하지 않습니다.</summary>
    public static 업무오류안내Presentation 생성(Api작업오류 error,
        Ssalddel.Contracts.Common.Workflow.업무실패맥락Dto? context = null, bool outcomeUnknown = false)
        => 업무맥락오류Presentation.생성(error, context, outcomeUnknown);

    public static 업무오류안내Presentation? 생성(string? message, bool outcomeUnknown = false)
    {
        if (string.IsNullOrWhiteSpace(message)) return null;
        if (outcomeUnknown)
            return new("unknown-outcome", "결과 확인 필요", "처리 결과를 아직 확인하지 못했습니다. 다시 요청하지 말고 결과를 확인해 주세요.", "결과 확인") { 복귀허용 = false };
        if (포함(message, "NotFound", "Not Found", "찾지 못", "찾을 수 없", "존재하지 않") || 상태인가(message, 404))
            return new("not-found", "정보를 찾지 못했습니다", "업무 목록에서 대상을 다시 선택해 주세요.");
        if (포함(message, "Unauthorized", "Forbidden", "로그인", "인증", "세션") || 상태인가(message, 401) || 상태인가(message, 403))
            return new("session", "로그인 확인 필요", "로그인 상태를 확인한 뒤 다시 조회해 주세요.");
        return new("read-failure", "연결 확인 필요", "정보를 불러오지 못했습니다. 잠시 후 다시 시도해 주세요.");
    }

    public static bool 조회오류인가(string? message)
        => !string.IsNullOrWhiteSpace(message)
           && (message.StartsWith("운송 의뢰 원장 조회 실패:", StringComparison.Ordinal)
               || message.StartsWith("로그인 세션 조회 실패:", StringComparison.Ordinal));

    public static bool 기술실패인가(Api작업오류? error)
        => error is not null && error.필드오류?.Count is not > 0 && !error.충돌
           && (string.IsNullOrWhiteSpace(error.실패분류Code)
               || error.실패분류Code == Ssalddel.Contracts.Common.Workflow.업무실패분류Codes.일시기술장애)
           && (error.Http상태코드 is >= 500 or 408
               || error.코드 is "timeout" or "transport-failure");

    private static bool 포함(string value, params string[] parts)
        => parts.Any(part => value.Contains(part, StringComparison.OrdinalIgnoreCase));

    private static bool 상태인가(string value, int status)
        => System.Text.RegularExpressions.Regex.IsMatch(value,
            $@"\b(?:HTTP(?:/\d(?:\.\d)?)?[\s:-]+|status(?:\s+code)?[\s:'""=]+){status}\b",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);
}

/// <summary>반복 조회의 같은 오류와 새 사용자 재시도를 구별하는 화면 수명 상태입니다.</summary>
public sealed class 업무오류안내Session
{
    private string? _context;
    private string? _category;
    private bool _notified;
    public long Revision { get; private set; }

    public bool 갱신(string context, string? category, bool busy)
    {
        if (!string.Equals(_context, context, StringComparison.Ordinal))
        {
            _context = context;
            _category = category;
            _notified = false;
            Revision++;
            return true;
        }
        // 재조회 중 잠시 빈 메시지가 되어도 닫힌 창을 다시 띄우지 않습니다.
        if (busy && category is null) return false;
        if (string.Equals(_category, category, StringComparison.Ordinal)) return false;
        _category = category;
        _notified = false;
        Revision++;
        return true;
    }

    public bool 열기(bool manual = false)
    {
        if (_category is null || (_notified && !manual)) return false;
        _notified = true;
        return true;
    }

    public void 재조회시작() => _notified = false;
}
