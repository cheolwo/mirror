namespace Hongdal.Ui.Common.Areas.App.ViewModels;

public sealed class 음식점식재료공급요청작성ViewModel : 조립ViewModelBase
{
    private 음식점식재료공급경로 _공급경로 = 음식점식재료공급경로.국내산지;

    public 음식점식재료공급요청작성ViewModel()
    {
        국내산지초안 = 기본초안(음식점식재료공급경로.국내산지);
        공동수입초안 = 기본초안(음식점식재료공급경로.공동수입);
    }

    public 음식점식재료공급요청Draft 국내산지초안 { get; }
    public 음식점식재료공급요청Draft 공동수입초안 { get; }

    public 음식점식재료공급경로 공급경로
    {
        get => _공급경로;
        private set
        {
            if (SetProperty(ref _공급경로, value))
            {
                OnPropertyChanged(nameof(현재초안));
                OnPropertyChanged(nameof(경로명));
                OnPropertyChanged(nameof(경로설명));
            }
        }
    }

    public 음식점식재료공급요청Draft 현재초안
        => 공급경로 == 음식점식재료공급경로.국내산지
            ? 국내산지초안
            : 공동수입초안;

    public string 경로명
        => 공급경로 == 음식점식재료공급경로.국내산지
            ? "국내 농수산물 산지 공급"
            : "수입 식재료 공동공급";

    public string 경로설명
        => 공급경로 == 음식점식재료공급경로.국내산지
            ? "생산자·산지조직과 여러 음식점의 수요를 묶어 시장 또는 지역 물류거점으로 받습니다."
            : "여러 음식점의 반복 수요를 모아 수입자·관세사·검역·물류 역할이 참여할 조건을 만듭니다.";

    public bool 경로선택(음식점식재료공급경로 route)
    {
        if (공급경로 == route)
        {
            return false;
        }

        공급경로 = route;
        return true;
    }

    public IReadOnlyList<string> 검증()
    {
        var draft = 현재초안;
        var errors = new List<string>();

        AddRequired(errors, draft.품목명, "품목명을 입력해 주세요.");
        AddRequired(errors, draft.품목분류, "품목 분류를 선택해 주세요.");
        AddRequired(errors, draft.규격, "납품 규격을 입력해 주세요.");
        AddRequired(errors, draft.수량단위, "수량 단위를 입력해 주세요.");
        AddRequired(errors, draft.납품지역, "납품 지역을 입력해 주세요.");
        AddRequired(errors, draft.사용목적, "식재료 사용 목적을 입력해 주세요.");

        if (draft.필요수량 <= 0)
        {
            errors.Add("필요 수량은 0보다 커야 합니다.");
        }

        if (draft.현재구매단가 <= 0)
        {
            errors.Add("현재 구매 단가를 입력해야 절감액을 비교할 수 있습니다.");
        }

        if (draft.희망도착단가 <= 0)
        {
            errors.Add("희망 도착 단가를 입력해 주세요.");
        }

        if (draft.희망납품일 is null || draft.희망납품일.Value.Date < DateTime.Today)
        {
            errors.Add("희망 납품일은 오늘 이후로 선택해 주세요.");
        }

        if (draft.공급경로 == 음식점식재료공급경로.공동수입)
        {
            AddRequired(errors, draft.희망원산지, "수입 경로는 희망 원산지 또는 허용 범위를 입력해 주세요.");
        }

        return errors;
    }

    public void 후보조건반영(음식점식재료공급후보 candidate)
    {
        if (candidate.공급경로 != 공급경로)
        {
            return;
        }

        현재초안.희망도착단가 = candidate.예상도착단가;
        현재초안.통화코드 = candidate.통화코드;
        OnPropertyChanged(nameof(현재초안));
    }

    public void 현재초안초기화()
    {
        var defaults = 기본초안(공급경로);
        Copy(defaults, 현재초안);
        OnPropertyChanged(nameof(현재초안));
    }

    private static 음식점식재료공급요청Draft 기본초안(음식점식재료공급경로 route)
        => route == 음식점식재료공급경로.국내산지
            ? new()
            {
                공급경로 = route,
                품목명 = "양파",
                품목분류 = "농산물",
                규격 = "중품 15kg 망",
                필요수량 = 120,
                수량단위 = "kg",
                납품주기 = "매주",
                희망납품일 = DateTime.Today.AddDays(7),
                현재구매단가 = 2300,
                희망도착단가 = 2000,
                통화코드 = "KRW",
                희망원산지 = "국내 산지",
                납품지역 = "서울 강서구",
                보관방식 = 음식점식재료보관방식.상온,
                사용목적 = "찌개·볶음 조리",
                추가조건 = "주 1회 오전 입고, 무름과 발아 제외",
                공동수요집계동의 = true,
                산지Lot추적필수 = true
            }
            : new()
            {
                공급경로 = route,
                품목명 = "냉동 다진마늘",
                품목분류 = "가공 농산물",
                규격 = "1kg x 10팩",
                필요수량 = 300,
                수량단위 = "kg",
                납품주기 = "매월",
                희망납품일 = DateTime.Today.AddDays(35),
                현재구매단가 = 5600,
                희망도착단가 = 4700,
                통화코드 = "KRW",
                희망원산지 = "중국 또는 검증 가능한 생산국",
                납품지역 = "서울 강서구",
                보관방식 = 음식점식재료보관방식.냉동,
                사용목적 = "양념·볶음 조리",
                추가조건 = "개별 밀봉, 제조 lot·유통기한 표시",
                공동수요집계동의 = true,
                산지Lot추적필수 = true
            };

    private static void AddRequired(List<string> errors, string? value, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add(message);
        }
    }

    private static void Copy(
        음식점식재료공급요청Draft source,
        음식점식재료공급요청Draft target)
    {
        var copy = source.복사();
        target.공급경로 = copy.공급경로;
        target.품목명 = copy.품목명;
        target.품목분류 = copy.품목분류;
        target.규격 = copy.규격;
        target.필요수량 = copy.필요수량;
        target.수량단위 = copy.수량단위;
        target.납품주기 = copy.납품주기;
        target.희망납품일 = copy.희망납품일;
        target.현재구매단가 = copy.현재구매단가;
        target.희망도착단가 = copy.희망도착단가;
        target.통화코드 = copy.통화코드;
        target.희망원산지 = copy.희망원산지;
        target.납품지역 = copy.납품지역;
        target.보관방식 = copy.보관방식;
        target.사용목적 = copy.사용목적;
        target.추가조건 = copy.추가조건;
        target.공동수요집계동의 = copy.공동수요집계동의;
        target.원산지대체허용 = copy.원산지대체허용;
        target.산지Lot추적필수 = copy.산지Lot추적필수;
    }
}

public sealed class 음식점식재료공급비교ViewModel : 조립ViewModelBase
{
    private IReadOnlyList<음식점식재료공급후보> _후보목록 = [];
    private string? _선택후보Id;

    public IReadOnlyList<음식점식재료공급후보> 후보목록
    {
        get => _후보목록;
        private set => SetProperty(ref _후보목록, value);
    }

    public string? 선택후보Id
    {
        get => _선택후보Id;
        private set
        {
            if (SetProperty(ref _선택후보Id, value))
            {
                OnPropertyChanged(nameof(선택후보));
            }
        }
    }

    public 음식점식재료공급후보? 선택후보
        => 후보목록.FirstOrDefault(candidate => candidate.후보Id == 선택후보Id);

    public 음식점식재료공급후보? 예상도착단가최저후보
        => 후보목록
            .Where(candidate => candidate.예상도착단가 > 0)
            .OrderBy(candidate => candidate.예상도착단가)
            .FirstOrDefault();

    public void 교체(IReadOnlyList<음식점식재료공급후보> candidates)
    {
        후보목록 = candidates;
        선택후보Id = null;
        OnPropertyChanged(nameof(예상도착단가최저후보));
    }

    public bool 선택(string candidateId)
    {
        if (!후보목록.Any(candidate => candidate.후보Id == candidateId))
        {
            return false;
        }

        선택후보Id = candidateId;
        return true;
    }
}

public sealed class 음식점식재료공급진행조회ViewModel : 조립ViewModelBase
{
    private IReadOnlyList<음식점식재료공급요청Snapshot> _요청목록 = [];

    public IReadOnlyList<음식점식재료공급요청Snapshot> 요청목록
    {
        get => _요청목록;
        private set => SetProperty(ref _요청목록, value);
    }

    public void 교체(IReadOnlyList<음식점식재료공급요청Snapshot> requests)
        => 요청목록 = requests
            .OrderByDescending(request => request.생성시각)
            .ToArray();
}

public sealed class 음식점식재료공급요청PageViewModel : PageViewModelBase
{
    private readonly I음식점식재료공급요청Service _service;
    private string? _메시지;
    private 음식점식재료공급메시지종류 _메시지종류 = 음식점식재료공급메시지종류.정보;
    private bool _명령처리중;

    public 음식점식재료공급요청PageViewModel(
        I음식점식재료공급요청Service service,
        음식점식재료공급요청작성ViewModel 작성,
        음식점식재료공급비교ViewModel 비교,
        음식점식재료공급진행조회ViewModel 진행)
    {
        _service = service;
        this.작성 = 하위ViewModel등록(작성);
        this.비교 = 하위ViewModel등록(비교);
        this.진행 = 하위ViewModel등록(진행);
    }

    public 음식점식재료공급요청작성ViewModel 작성 { get; }
    public 음식점식재료공급비교ViewModel 비교 { get; }
    public 음식점식재료공급진행조회ViewModel 진행 { get; }
    public bool SimulationMode => _service.SimulationMode;

    public string? 메시지
    {
        get => _메시지;
        private set => SetProperty(ref _메시지, value);
    }

    public 음식점식재료공급메시지종류 메시지종류
    {
        get => _메시지종류;
        private set => SetProperty(ref _메시지종류, value);
    }

    public bool 명령처리중
    {
        get => _명령처리중;
        private set => SetProperty(ref _명령처리중, value);
    }

    public async Task<bool> 공급경로선택Async(
        음식점식재료공급경로 route,
        CancellationToken cancellationToken = default)
    {
        var previousRoute = 작성.공급경로;
        if (!작성.경로선택(route))
        {
            return false;
        }

        명령처리중 = true;
        try
        {
            비교.교체(await _service.공급후보조회Async(작성.현재초안.복사(), cancellationToken));
            메시지 = null;
            OnPropertyChanged(nameof(작성));
            OnPropertyChanged(nameof(비교));
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            작성.경로선택(previousRoute);
            throw;
        }
        catch (Exception ex)
        {
            작성.경로선택(previousRoute);
            SetMessage(ex.Message, 음식점식재료공급메시지종류.오류);
            return false;
        }
        finally
        {
            명령처리중 = false;
        }
    }

    public void 현재공급조건초기화()
    {
        작성.현재초안초기화();
        비교.교체([]);
        SetMessage(
            $"{작성.경로명} 입력값을 기본값으로 되돌렸습니다. 조건으로 비교해 공급 후보를 다시 확인해 주세요.",
            음식점식재료공급메시지종류.정보);
    }

    public async Task<bool> 조건으로비교Async(CancellationToken cancellationToken = default)
    {
        var errors = 작성.검증();
        if (errors.Count > 0)
        {
            SetMessage(errors[0], 음식점식재료공급메시지종류.경고);
            return false;
        }

        명령처리중 = true;
        try
        {
            비교.교체(await _service.공급후보조회Async(작성.현재초안.복사(), cancellationToken));
            SetMessage(
                $"{작성.현재초안.품목명} 공급 후보 {비교.후보목록.Count}건의 예상 도착단가를 다시 계산했습니다.",
                음식점식재료공급메시지종류.정보);
            return true;
        }
        catch (Exception ex)
        {
            SetMessage(ex.Message, 음식점식재료공급메시지종류.오류);
            return false;
        }
        finally
        {
            명령처리중 = false;
        }
    }

    public bool 공급후보선택(string candidateId)
    {
        if (!비교.선택(candidateId) || 비교.선택후보 is not { } selected)
        {
            return false;
        }

        작성.후보조건반영(selected);
        SetMessage(
            $"{selected.공급주체명} 조건을 요청 초안의 희망 도착단가에 반영했습니다. 공급 계약은 아직 생성되지 않았습니다.",
            음식점식재료공급메시지종류.정보);
        return true;
    }

    public async Task<bool> 초안저장Async(CancellationToken cancellationToken = default)
    {
        var errors = 작성.검증();
        if (errors.Count > 0)
        {
            SetMessage(string.Join(" ", errors.Take(3)), 음식점식재료공급메시지종류.경고);
            return false;
        }

        명령처리중 = true;
        try
        {
            var saved = await _service.초안저장Async(
                작성.현재초안.복사(),
                비교.선택후보Id,
                cancellationToken);
            var requests = await _service.요청목록조회Async(cancellationToken);
            진행.교체(requests);
            SetMessage(
                $"{saved.요청Id} 공급 요청 초안을 저장했습니다. 공급자 선정·계약·수입·통관의 운영 효력은 발생하지 않습니다.",
                음식점식재료공급메시지종류.성공);
            return true;
        }
        catch (Exception ex)
        {
            SetMessage(ex.Message, 음식점식재료공급메시지종류.오류);
            return false;
        }
        finally
        {
            명령처리중 = false;
        }
    }

    protected override async Task 불러오기Async(
        bool 새로고침,
        CancellationToken cancellationToken)
    {
        var candidates = await _service.공급후보조회Async(작성.현재초안.복사(), cancellationToken);
        var requests = await _service.요청목록조회Async(cancellationToken);
        비교.교체(candidates);
        진행.교체(requests);
        메시지 = null;
    }

    private void SetMessage(string message, 음식점식재료공급메시지종류 kind)
    {
        메시지종류 = kind;
        메시지 = message;
    }
}
