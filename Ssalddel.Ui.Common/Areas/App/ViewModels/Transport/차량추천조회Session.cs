using Ssalddel.Contracts.Shipper.Request;

namespace Ssalddel.Ui.Common.Areas.App.ViewModels.Transport;

/// <summary>작성 화면 한 개의 차량 추천 응답과 선택 출처를 현재 입력에 대조합니다.</summary>
internal sealed class 차량추천조회Session(
    Func<string> currentFingerprint,
    Func<bool> canQuery,
    Func<차량추천요청> createRequest,
    Func<string?> currentVehicle,
    Action<string> selectVehicle)
{
    private readonly Func<string> _현재입력지문 = currentFingerprint;
    private 차량추천응답? _차량추천;
    private string? _추천입력지문;
    private string? _적용입력지문;
    private bool _복원추천차량재확인필요;
    private long _추천요청순번;

    public bool 차량추천조회중 { get; private set; }
    public string? 차량추천조회오류 { get; private set; }
    public bool 차량추천현재유효 => _차량추천 is not null && _추천입력지문 == _현재입력지문()
        && _차량추천.적재조건?.계산판본 == 화물적재입력Policy.CurrentRevision;
    public bool 차량추천조건변경됨 => _추천입력지문 is not null && _추천입력지문 != _현재입력지문();
    public bool 적용차량재확인필요 => _복원추천차량재확인필요
        || _적용입력지문 is not null && (_적용입력지문 != _현재입력지문() || !차량추천현재유효
            || !추천차량종류목록.Contains(currentVehicle() ?? string.Empty, StringComparer.Ordinal));
    public IReadOnlyList<차량추천후보응답> 서버추천후보 => 차량추천현재유효
        ? _차량추천!.후보목록.Where(x => x.단일운송가능여부 && !string.IsNullOrWhiteSpace(x.차량종류)).ToArray() : [];
    public IReadOnlyList<string> 서버추천사유 => 차량추천현재유효 ? _차량추천!.추천사유 : [];
    public IReadOnlyList<string> 서버추천경고 => 차량추천현재유효 ? _차량추천!.경고목록 : [];
    public IReadOnlyList<string> 추천차량종류목록 => 서버추천후보.Select(x => x.차량종류).Distinct(StringComparer.Ordinal).ToArray();
    public bool 초안저장시재확인필요 => _적용입력지문 is not null || _복원추천차량재확인필요;

    public bool 서버추천차량선택(string vehicle)
    {
        if (!차량추천현재유효 || !추천차량종류목록.Contains(vehicle, StringComparer.Ordinal)) return false;
        selectVehicle(vehicle);
        _적용입력지문 = _현재입력지문();
        _복원추천차량재확인필요 = false;
        return true;
    }

    public async Task 조회Async(Func<차량추천요청, CancellationToken, Task<차량추천응답>> provider,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(provider);
        var sequence = ++_추천요청순번;
        차량추천조회오류 = null;
        _차량추천 = null;
        if (!canQuery())
        {
            차량추천조회중 = false;
            차량추천조회오류 = "전체 중량·포장 크기와 회전·적층 조건을 먼저 확인해 주세요.";
            return;
        }
        var request = createRequest();
        var fingerprint = _현재입력지문();
        차량추천조회중 = true;
        try
        {
            var response = await provider(request, cancellationToken);
            if (sequence != _추천요청순번 || fingerprint != _현재입력지문() || cancellationToken.IsCancellationRequested) return;
            if (response.적재조건 is not { 유효여부: true } evidence
                || evidence.계산판본 != 화물적재입력Policy.CurrentRevision || evidence.입력지문 != fingerprint)
                throw new InvalidOperationException("현재 화물 조건과 일치하는 추천 근거를 확인하지 못했습니다.");
            _차량추천 = response;
            _추천입력지문 = fingerprint;
        }
        catch (OperationCanceledException)
        {
            if (sequence == _추천요청순번 && fingerprint == _현재입력지문())
                차량추천조회오류 = "추천 확인이 중단되었습니다. 입력을 유지했으니 다시 확인해 주세요.";
        }
        catch (Exception)
        {
            if (sequence == _추천요청순번 && fingerprint == _현재입력지문())
                차량추천조회오류 = "차량 추천을 확인하지 못했습니다. 입력을 유지했으니 다시 확인해 주세요.";
        }
        finally { if (sequence == _추천요청순번) 차량추천조회중 = false; }
    }

    public void 추천초기화()
    {
        ++_추천요청순번;
        _복원추천차량재확인필요 |= _적용입력지문 is not null;
        _차량추천 = null; _추천입력지문 = _적용입력지문 = null;
        차량추천조회중 = false; 차량추천조회오류 = null;
    }

    public void 초안복원출처설정(bool 추천차량재확인필요)
        => _복원추천차량재확인필요 = 추천차량재확인필요;

    public void 새초안초기화()
    {
        추천초기화();
        _복원추천차량재확인필요 = false;
    }

    /// <summary>저장 초안과 달리 같은 접수의 현재 선택만 별도 세션으로 옮깁니다. 응답 참조는 기존대로 보존합니다.</summary>
    public void 접수선택복사(차량추천조회Session target)
    {
        if (차량추천현재유효 && !적용차량재확인필요 && _적용입력지문 == _현재입력지문()
            && target._현재입력지문() == _현재입력지문())
        {
            target._차량추천 = _차량추천;
            target._추천입력지문 = _추천입력지문;
            target._적용입력지문 = _적용입력지문;
            target._복원추천차량재확인필요 = false;
        }
    }
}
