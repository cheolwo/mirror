using Hongdal.Contracts.Common.Community;

namespace Hongdal.Ui.Common.Areas.App.ViewModels;

public static class 공동구매업무영역코드
{
    public const string 의사결정 = "DecisionSupport";
    public const string 모집 = "Recruitment";
    public const string 합의 = "Agreement";
    public const string 공급 = "Supply";
    public const string 물류 = "Logistics";
    public const string 실행 = "Execution";
}

public sealed record 공동구매업무영역정의(
    string 코드,
    string 이름,
    string 설명,
    IReadOnlyList<string> 관련절차단계코드);

/// <summary>
/// 선택한 공동구매와 원장을 대상으로 실행되는 모든 공동구매 업무의 상위 계층입니다.
/// 구체 ViewModel은 API 호출과 입력 규칙만 추가하고 대상·원장·절차 문맥은 이 계층을 사용합니다.
/// </summary>
public abstract class 공동구매원장업무ViewModelBase : 공동구매작업ViewModelBase
{
    protected 공동구매원장업무ViewModelBase(
        공동구매화면상태ViewModel 화면상태,
        공동구매업무영역정의 업무영역)
    {
        this.화면상태 = 화면상태 ?? throw new ArgumentNullException(nameof(화면상태));
        this.업무영역 = 업무영역 ?? throw new ArgumentNullException(nameof(업무영역));
        현재사용자Context연결(화면상태.현재사용자Context);
    }

    protected 공동구매화면상태ViewModel 화면상태 { get; }

    public 공동구매업무영역정의 업무영역 { get; }
    public string 업무영역코드 => 업무영역.코드;
    public string 업무영역명 => 업무영역.이름;
    public IReadOnlyList<string> 관련절차단계코드 => 업무영역.관련절차단계코드;
    public CommunityVoteResponse? 선택된공동구매 => 화면상태.선택된공동구매;
    public Guid? 선택된공동구매Id => 화면상태.선택된공동구매Id;
    public string? 공동구매원장Id => 화면상태.공동구매원장Id;
    public long? 원장Revision => 화면상태.원장Revision;
    public bool 대상선택됨 => 선택된공동구매 is not null;
    public bool 현재업무영역인가 => 관련절차단계코드.Any(code =>
        string.Equals(code, 화면상태.현재단계코드, StringComparison.OrdinalIgnoreCase));

    protected bool 대상선택확인(
        out CommunityVoteResponse campaign,
        string validationMessage = "처리할 공동구매를 먼저 선택해 주세요.")
    {
        campaign = 선택된공동구매!;
        return campaign is not null || 유효성실패(validationMessage);
    }

    protected bool 원장연결확인(string validationMessage = "공동구매 원장을 먼저 연결해 주세요.")
        => !string.IsNullOrWhiteSpace(공동구매원장Id) || 유효성실패(validationMessage);
}

/// <summary>거래경로와 가격 비교가 확장하는 의사결정 기본 업무입니다.</summary>
public abstract class 공동구매의사결정업무ViewModelBase(공동구매화면상태ViewModel 화면상태)
    : 공동구매원장업무ViewModelBase(화면상태, 영역)
{
    private static readonly 공동구매업무영역정의 영역 = new(
        공동구매업무영역코드.의사결정,
        "의사결정",
        "거래경로와 시장가격 자료를 비교해 다음 업무 선택을 지원합니다.",
        [
            공동구매절차코드.거래경로,
            공동구매절차코드.수요모집,
            공동구매절차코드.공급조건협상
        ]);
}

/// <summary>제안, 목록 조회, 수요 참여와 이의 검토가 확장하는 모집 기본 업무입니다.</summary>
public abstract class 공동구매모집업무ViewModelBase(공동구매화면상태ViewModel 화면상태)
    : 공동구매원장업무ViewModelBase(화면상태, 영역)
{
    private static readonly 공동구매업무영역정의 영역 = new(
        공동구매업무영역코드.모집,
        "모집",
        "공동구매 제안부터 수요 모집과 이의 검토까지 관리합니다.",
        [
            공동구매절차코드.제안,
            공동구매절차코드.거래경로,
            공동구매절차코드.수요모집,
            공동구매절차코드.이의검토
        ]);
}

/// <summary>모집 마감, 결의와 전자서명이 확장하는 합의 기본 업무입니다.</summary>
public abstract class 공동구매합의업무ViewModelBase(공동구매화면상태ViewModel 화면상태)
    : 공동구매원장업무ViewModelBase(화면상태, 영역)
{
    private static readonly 공동구매업무영역정의 영역 = new(
        공동구매업무영역코드.합의,
        "합의",
        "모집 결과를 확정하고 결의문과 전자서명으로 합의를 기록합니다.",
        [공동구매절차코드.확정안, 공동구매절차코드.전자서명]);
}

/// <summary>거래 상대 연결, 공급 제안, 적합성 검토와 협상이 확장하는 공급 기본 업무입니다.</summary>
public abstract class 공동구매공급업무ViewModelBase(공동구매화면상태ViewModel 화면상태)
    : 공동구매원장업무ViewModelBase(화면상태, 영역)
{
    private static readonly 공동구매업무영역정의 영역 = new(
        공동구매업무영역코드.공급,
        "공급",
        "생산자·대표 연결과 공급 조건 검토 및 협상을 관리합니다.",
        [공동구매절차코드.거래상대연결, 공동구매절차코드.공급조건협상]);
}

/// <summary>국내 물류와 공동수입 이행계획이 확장하는 물류 기본 업무입니다.</summary>
public abstract class 공동구매물류업무ViewModelBase(공동구매화면상태ViewModel 화면상태)
    : 공동구매원장업무ViewModelBase(화면상태, 영역)
{
    private static readonly 공동구매업무영역정의 영역 = new(
        공동구매업무영역코드.물류,
        "물류",
        "합의된 조건을 입고·출고·운송 가능한 이행계획으로 전환합니다.",
        [공동구매절차코드.이행계획]);
}

/// <summary>자동집단, 주문원장, 창고와 커머스 이행이 확장하는 실행 기본 업무입니다.</summary>
public abstract class 공동구매실행업무ViewModelBase(공동구매화면상태ViewModel 화면상태)
    : 공동구매원장업무ViewModelBase(화면상태, 영역)
{
    private static readonly 공동구매업무영역정의 영역 = new(
        공동구매업무영역코드.실행,
        "실행",
        "합의와 이행계획을 주문·입출고·커머스 작업으로 실행합니다.",
        [공동구매절차코드.실행, 공동구매절차코드.커머스]);
}

/// <summary>공동구매 이후 판매 실행의 기본 계약이며 국내 판매와 해외 수출이 이를 확장합니다.</summary>
public abstract class 공동구매판매실행업무ViewModelBase(
    공동구매화면상태ViewModel 화면상태,
    string 판매경로코드) : 공동구매실행업무ViewModelBase(화면상태)
{
    public string 판매경로코드 { get; } = 판매경로코드;
    public abstract bool 활성 { get; }
    public abstract IReadOnlyList<string> 지원채널목록 { get; }
}

/// <summary>주문 루트 원장의 조회, 하위 원장 구성과 서명이 확장하는 주문원장 기본 업무입니다.</summary>
public abstract class 공동구매주문원장실행업무ViewModelBase(
    공동구매화면상태ViewModel 화면상태,
    공동구매실행상태ViewModel 실행상태) : 공동구매실행업무ViewModelBase(화면상태)
{
    protected 공동구매실행상태ViewModel 실행상태 { get; } = 실행상태;
    public string? 현재주문원장Id => 실행상태.선택된주문원장Id;
    public string? 현재주문집계원장Id => 실행상태.공동구매주문집계원장Id;
    public bool 주문원장선택됨 => !string.IsNullOrWhiteSpace(현재주문원장Id);
}
