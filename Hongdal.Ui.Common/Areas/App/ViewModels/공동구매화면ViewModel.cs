namespace Hongdal.Ui.Common.Areas.App.ViewModels;

/// <summary>
/// 공동구매 페이지가 하나만 주입받아도 공통 모집·합의와
/// 국내 공동구매·공동수입과 이후 국내 판매·해외 수출 하위 기능을 조립할 수 있는 루트 ViewModel입니다.
/// 하위 ViewModel은 각각 DI 등록되어 필요한 세부 페이지에서 따로 주입할 수도 있습니다.
/// </summary>
public sealed class 공동구매화면ViewModel : 조립ViewModelBase
{
    public 공동구매화면ViewModel(
        공동구매화면상태ViewModel 상태,
        공동구매모집기능ViewModel 모집,
        공동구매합의기능ViewModel 합의,
        공동구매거래경로분기ViewModel 거래경로분기,
        국내공동구매분기ViewModel 국내공동구매,
        공동수입분기ViewModel 공동수입,
        국내판매ViewModel 국내판매,
        해외수출ViewModel 해외수출,
        공동구매실행기능ViewModel 실행)
    {
        this.상태 = 하위ViewModel등록(상태, 수명소유: false);
        this.모집 = 하위ViewModel등록(모집);
        this.합의 = 하위ViewModel등록(합의);
        this.거래경로분기 = 하위ViewModel등록(거래경로분기, 수명소유: false);
        this.국내공동구매 = 하위ViewModel등록(국내공동구매);
        this.공동수입 = 하위ViewModel등록(공동수입);
        this.국내판매 = 하위ViewModel등록(국내판매);
        this.해외수출 = 하위ViewModel등록(해외수출);
        this.실행 = 하위ViewModel등록(실행);
        this.국내판매.실행연결(this.실행);
        this.해외수출.실행연결(this.실행);

        업무목록 =
        [
            this.거래경로분기,
            this.가격의사결정,
            this.모집.목록,
            this.모집.제안,
            this.모집.수요참여,
            this.모집.이의검토,
            this.합의.모집마감,
            this.합의.결의,
            this.합의.전자서명,
            this.공급.생산자연결,
            this.공급.공급제안,
            this.공급.공급적합성,
            this.공급.협상,
            this.물류.이행계획,
            this.실행.자동집단,
            this.실행.주문원장.조회,
            this.실행.주문원장.하위원장,
            this.실행.주문원장.서명,
            this.실행.커머스이행,
            this.국내판매,
            this.해외수출
        ];
        업무영역별목록 = 업무목록
            .GroupBy(x => x.업무영역코드, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                x => x.Key,
                x => (IReadOnlyList<공동구매원장업무ViewModelBase>)x.ToArray(),
                StringComparer.OrdinalIgnoreCase);
        절차세부업무영역별목록 = new Dictionary<string, IReadOnlyList<I업무조각ViewModel>>(
            StringComparer.OrdinalIgnoreCase)
        {
            [공동구매업무영역코드.모집] = this.모집.세부업무목록,
            [공동구매업무영역코드.합의] = this.합의.세부업무목록,
            [공동구매업무영역코드.공급] = this.공급.세부업무목록,
            [공동구매업무영역코드.물류] = this.물류.세부업무목록,
            ["group-import"] = this.공동수입.세부업무목록,
            [공동구매업무영역코드.실행] = this.실행.세부업무목록
        };
        절차세부업무목록 = 절차세부업무영역별목록.Values.SelectMany(items => items).ToArray();
    }

    public 공동구매화면상태ViewModel 상태 { get; }
    public 공동구매모집기능ViewModel 모집 { get; }
    public 공동구매합의기능ViewModel 합의 { get; }
    public 공동구매거래경로분기ViewModel 거래경로분기 { get; }
    public 국내공동구매분기ViewModel 국내공동구매 { get; }
    public 공동수입분기ViewModel 공동수입 { get; }
    public 국내판매ViewModel 국내판매 { get; }
    public 해외수출ViewModel 해외수출 { get; }
    public 공동구매가격의사결정ViewModel 가격의사결정 => 모집.가격의사결정;
    public 공동구매공급기능ViewModel 공급 => 국내공동구매.공급;
    public 공동구매물류기능ViewModel 물류 => 국내공동구매.물류;
    public 공동구매실행기능ViewModel 실행 { get; }
    public IReadOnlyList<공동구매원장업무ViewModelBase> 업무목록 { get; }
    public IReadOnlyDictionary<string, IReadOnlyList<공동구매원장업무ViewModelBase>> 업무영역별목록 { get; }
    public IReadOnlyList<I업무조각ViewModel> 절차세부업무목록 { get; }
    public IReadOnlyDictionary<string, IReadOnlyList<I업무조각ViewModel>> 절차세부업무영역별목록 { get; }

    public IReadOnlyList<공동구매원장업무ViewModelBase> 업무영역조회(string areaCode)
        => 업무영역별목록.TryGetValue(areaCode, out var viewModels) ? viewModels : [];

    public bool 처리중
        => 모집.처리중
           || 합의.처리중
           || 국내공동구매.처리중
           || 공동수입.처리중
           || 국내판매.처리중
           || 해외수출.처리중
           || 가격의사결정.처리중
           || 실행.처리중;

    public async Task<bool> 초기화Async(
        string? communityScope = null,
        CancellationToken cancellationToken = default)
    {
        await 모집.제안.거래경로.운영시장초기화Async(cancellationToken);
        return await 모집.목록.목록조회Async(communityScope, cancellationToken);
    }

    public Task<bool> HS코드별초기화Async(
        string hsCode,
        string? communityScope = null,
        CancellationToken cancellationToken = default)
        => 모집.목록.HS코드별목록조회Async(hsCode, communityScope, cancellationToken);

    public Task<bool> 거래경로별초기화Async(
        string routeFilterCode,
        string? communityScope = null,
        CancellationToken cancellationToken = default)
        => 모집.목록.거래경로별목록조회Async(
            routeFilterCode,
            communityScope,
            cancellationToken);
}
