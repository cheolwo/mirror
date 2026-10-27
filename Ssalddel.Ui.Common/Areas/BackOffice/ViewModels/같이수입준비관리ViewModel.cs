using CommunityToolkit.Mvvm.ComponentModel;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Contracts.Common.Orderer;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.BackOffice.Services;

namespace Ssalddel.Ui.Common.Areas.BackOffice.ViewModels;

public static class 같이수입준비작업대필터코드
{
    public const string 전체 = "All";
    public const string 인계승인필요 = "AwaitingHandoff";
    public const string 인계후보 = "HandoffCandidate";
}

[SsalddelCodeMetadata(
    SsalddelCodeFeatureKeys.GroupImportTradeReadiness,
    SsalddelCodeLayer.ViewModel,
    "관리자가 1.0 인계를 승인하고 기존 같이 수입 원장의 1.5 준비·포워더 인계 블록을 Revision과 멱등 키로 저장하도록 UI 상태를 조율합니다.",
    ContractType = typeof(I같이수입준비관리Client),
    FlowOrder = 50,
    Effects = SsalddelCodeEffect.NetworkCall | SsalddelCodeEffect.UiStateMutation,
    Boundary = "플랫폼은 집계·최소화·동의·인계 사실과 포워더 회신만 기록하며 업체 자동 선정, 외부 자동 전송, 계약, 결제, 신고, 운송 또는 창고 실행 상태를 만들지 않습니다.")]
public sealed class 같이수입준비관리ViewModel(I같이수입준비관리Client client) : ObservableObject
{
    private IReadOnlyList<공동구매자동집단요약응답> _작업대목록 = [];
    private 공동구매자동집단요약응답? _선택집단;
    private 공동구매수요모집Os상태응답? _운영상태;
    private 같이수입준비원장응답? _저장원장;
    private 같이수입준비원장응답? _미리보기;
    private 같이수입준비Os상태응답? _준비Os상태;
    private 같이수입준비원장저장요청 _초안 = new();
    private string _검색어 = string.Empty;
    private string _목록필터 = 같이수입준비작업대필터코드.전체;
    private string _추가재료집단Id = string.Empty;
    private string _승인사유 = string.Empty;
    private string _미확인항목Text = string.Empty;
    private string _전문검토수신자 = string.Empty;
    private string _전문검토범위 = "HS·HTS 품목분류와 도착국가 수입 규제 검토";
    private string _전문검토인계메모 = string.Empty;
    private string _관리자표시명 = "1.5 준비 관리자";
    private string? _메시지;
    private bool _오류메시지;
    private bool _처리중;
    private bool _초기화됨;
    private bool _초안저장후변경됨;
    private string? _대기저장지문;
    private string? _대기저장멱등키;

    public IReadOnlyList<공동구매자동집단요약응답> 작업대목록
    {
        get => _작업대목록;
        private set
        {
            if (SetProperty(ref _작업대목록, value))
            {
                OnPropertyChanged(nameof(필터된작업대목록));
                OnPropertyChanged(nameof(인계승인필요건수));
                OnPropertyChanged(nameof(확정집단건수));
                OnPropertyChanged(nameof(추가가능재료집단목록));
            }
        }
    }

    public 공동구매자동집단요약응답? 선택집단
    {
        get => _선택집단;
        private set
        {
            if (SetProperty(ref _선택집단, value))
            {
                RaiseStateProperties();
            }
        }
    }

    public 공동구매수요모집Os상태응답? 운영상태
    {
        get => _운영상태;
        private set
        {
            if (SetProperty(ref _운영상태, value))
            {
                RaiseStateProperties();
            }
        }
    }

    public 같이수입준비원장응답? 저장원장
    {
        get => _저장원장;
        private set
        {
            if (SetProperty(ref _저장원장, value))
            {
                RaiseStateProperties();
            }
        }
    }

    public 같이수입준비원장응답? 미리보기
    {
        get => _미리보기;
        private set
        {
            if (SetProperty(ref _미리보기, value))
            {
                RaiseStateProperties();
            }
        }
    }

    public 같이수입준비Os상태응답? 준비Os상태
    {
        get => _준비Os상태;
        private set
        {
            if (SetProperty(ref _준비Os상태, value))
            {
                RaiseStateProperties();
            }
        }
    }

    public 같이수입준비원장저장요청 초안
    {
        get => _초안;
        private set
        {
            if (SetProperty(ref _초안, value))
            {
                OnPropertyChanged(nameof(미확인항목Text));
            }
        }
    }

    public string 검색어
    {
        get => _검색어;
        set
        {
            if (SetProperty(ref _검색어, value ?? string.Empty))
            {
                OnPropertyChanged(nameof(필터된작업대목록));
            }
        }
    }

    public string 목록필터
    {
        get => _목록필터;
        set
        {
            if (SetProperty(ref _목록필터, value ?? 같이수입준비작업대필터코드.전체))
            {
                OnPropertyChanged(nameof(필터된작업대목록));
            }
        }
    }

    public string 추가재료집단Id
    {
        get => _추가재료집단Id;
        set => SetProperty(ref _추가재료집단Id, value ?? string.Empty);
    }

    public IReadOnlyList<공동구매자동집단요약응답> 추가가능재료집단목록
    {
        get
        {
            var included = 초안.재료품목목록
                .Select(item => item.원천자동집단Id)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToHashSet(StringComparer.Ordinal);
            return 작업대목록
                .Where(item => !included.Contains(item.자동집단Id))
                .Where(item => 선택집단 is null
                               || 공동구매거래문맥정책.호환됨(
                                   선택집단.거래유형,
                                   선택집단.가격표시기준,
                                   item.거래유형,
                                   item.가격표시기준))
                .ToArray();
        }
    }

    public string 승인사유
    {
        get => _승인사유;
        set => SetProperty(ref _승인사유, value ?? string.Empty);
    }

    public string 미확인항목Text
    {
        get => _미확인항목Text;
        set
        {
            if (!SetProperty(ref _미확인항목Text, value ?? string.Empty))
            {
                return;
            }

            초안.미확인항목목록 = _미확인항목Text
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            초안변경됨();
        }
    }

    public string 전문검토수신자
    {
        get => _전문검토수신자;
        set
        {
            if (SetProperty(ref _전문검토수신자, value ?? string.Empty))
            {
                OnPropertyChanged(nameof(전문검토인계가능));
            }
        }
    }

    public string 전문검토범위
    {
        get => _전문검토범위;
        set
        {
            if (SetProperty(ref _전문검토범위, value ?? string.Empty))
            {
                OnPropertyChanged(nameof(전문검토인계가능));
            }
        }
    }

    public string 전문검토인계메모
    {
        get => _전문검토인계메모;
        set
        {
            if (SetProperty(ref _전문검토인계메모, value ?? string.Empty))
            {
                OnPropertyChanged(nameof(전문검토인계가능));
            }
        }
    }

    public string? 메시지
    {
        get => _메시지;
        private set => SetProperty(ref _메시지, value);
    }

    public bool 오류메시지
    {
        get => _오류메시지;
        private set => SetProperty(ref _오류메시지, value);
    }

    public bool 처리중
    {
        get => _처리중;
        private set
        {
            if (SetProperty(ref _처리중, value))
            {
                RaiseStateProperties();
            }
        }
    }

    public bool 초기화됨
    {
        get => _초기화됨;
        private set => SetProperty(ref _초기화됨, value);
    }

    public bool 초안저장후변경됨
    {
        get => _초안저장후변경됨;
        private set
        {
            if (SetProperty(ref _초안저장후변경됨, value))
            {
                RaiseStateProperties();
            }
        }
    }

    public IReadOnlyList<공동구매자동집단요약응답> 필터된작업대목록
        => 작업대목록
            .Where(item => 목록필터 switch
            {
                같이수입준비작업대필터코드.인계승인필요 =>
                    item.현재상태 == 공동구매자동집단상태코드.확정대기,
                같이수입준비작업대필터코드.인계후보 =>
                    item.현재상태 == 공동구매자동집단상태코드.확정,
                _ => true
            })
            .Where(item => string.IsNullOrWhiteSpace(검색어)
                           || Contains(item.상품명, 검색어)
                           || Contains(item.상품키, 검색어)
                           || Contains(item.HS코드, 검색어)
                           || Contains(item.배송권명, 검색어)
                           || Contains(item.자동집단Id, 검색어))
            .ToArray();

    public int 인계승인필요건수
        => 작업대목록.Count(item => item.현재상태 == 공동구매자동집단상태코드.확정대기);

    public int 확정집단건수
        => 작업대목록.Count(item => item.현재상태 == 공동구매자동집단상태코드.확정);

    public bool 후속기능활성 => 운영상태?.후속워크플로우활성여부 == true;

    public bool 인계승인됨
        => string.Equals(
            운영상태?.인계상태,
            공동구매수요모집인계상태코드.승인후속대기,
            StringComparison.Ordinal);

    public bool 인계승인가능
        => !처리중 && 인계승인조건충족;

    private bool 인계승인조건충족
        => 선택집단 is not null
           && 운영상태 is not null
           && 후속기능활성
           && !인계승인됨
           && 선택집단.현재상태 is 공동구매자동집단상태코드.확정대기
               or 공동구매자동집단상태코드.확정
           && !string.IsNullOrWhiteSpace(승인사유);

    public bool 미리보기가능 => !처리중 && 미리보기조건충족;

    private bool 미리보기조건충족 => 선택집단 is not null && 후속기능활성;

    public bool 저장가능 => !처리중 && 저장조건충족;

    private bool 저장조건충족 => 미리보기조건충족 && 인계승인됨;

    public bool Os작업실행가능
        => !처리중 && Os작업실행조건충족;

    private bool Os작업실행조건충족
        => 저장원장 is not null
           && 준비Os상태?.기능활성여부 == true
           && !초안저장후변경됨;

    public bool 전문검토인계가능
        => !처리중 && 전문검토인계조건충족;

    private bool 전문검토인계조건충족
        => Os작업실행조건충족
           && 준비Os상태?.전문검토인계가능 == true
           && 준비Os상태.전문검토인계기록 is null
           && !string.IsNullOrWhiteSpace(전문검토수신자)
           && !string.IsNullOrWhiteSpace(전문검토범위)
           && !string.IsNullOrWhiteSpace(전문검토인계메모);

    public 같이수입준비원장평가응답? 현재평가 => 미리보기?.평가 ?? 저장원장?.평가;

    public int 완료검토항목수
    {
        get
        {
            var evaluation = 현재평가;
            if (evaluation is null)
            {
                return 0;
            }

            return new[]
            {
                evaluation.재료품목구조완료,
                evaluation.공급자근거구조완료,
                evaluation.견적구조완료,
                evaluation.예상비용구조완료,
                evaluation.품목분류후보구조완료,
                evaluation.국가별검토구조완료,
                evaluation.포워더인계구조완료,
                evaluation.국제운송검토구조완료,
                evaluation.책임초안구조완료
            }.Count(value => value);
        }
    }

    public async Task 초기화Async(string? 관리자표시명, CancellationToken cancellationToken = default)
    {
        if (초기화됨 || 처리중)
        {
            return;
        }

        _관리자표시명 = string.IsNullOrWhiteSpace(관리자표시명)
            ? "1.5 준비 관리자"
            : 관리자표시명.Trim();
        await 새로고침Async(cancellationToken);
    }

    public Task 새로고침Async(CancellationToken cancellationToken = default)
        => RunAsync(async () =>
        {
            var selectedId = 선택집단?.자동집단Id;
            작업대목록 = await client.작업대목록조회Async(cancellationToken);
            초기화됨 = true;
            var target = 작업대목록.FirstOrDefault(item => item.자동집단Id == selectedId)
                         ?? 작업대목록.FirstOrDefault();
            if (target is null)
            {
                선택초기화();
                메시지 = "현재 1.5 준비로 인계할 확정 검토 집단이 없습니다.";
                return;
            }

            await 선택CoreAsync(target, cancellationToken);
        });

    public Task 선택Async(공동구매자동집단요약응답 item, CancellationToken cancellationToken = default)
        => RunAsync(() => 선택CoreAsync(item, cancellationToken));

    public Task 인계승인Async(CancellationToken cancellationToken = default)
        => RunAsync(async () =>
        {
            if (!인계승인조건충족 || 선택집단 is null)
            {
                throw new InvalidOperationException("1.5 인계 승인 조건과 승인 사유를 확인해 주세요.");
            }

            var result = await client.인계승인Async(
                선택집단.자동집단Id,
                new 공동구매수요모집인계승인요청
                {
                    요청멱등키 = $"admin-handoff-{Guid.NewGuid():N}",
                    승인사유 = 승인사유.Trim()
                },
                cancellationToken);
            운영상태 = result.운영상태;
            승인사유 = string.Empty;
            메시지 = result.안내;
        });

    public Task 미리보기Async(CancellationToken cancellationToken = default)
        => RunAsync(async () =>
        {
            if (!미리보기조건충족 || 선택집단 is null)
            {
                throw new InvalidOperationException("1.5 기능이 활성화된 집단을 선택해 주세요.");
            }

            동기화미확인항목();
            미리보기 = await client.미리보기Async(
                선택집단.자동집단Id,
                Clone(초안),
                cancellationToken);
            메시지 = 미리보기.평가.전문검토인계가능
                ? "구조 검토를 통과했습니다. 사람의 전문 검토를 위한 자료로 저장할 수 있습니다."
                : $"차단 사유 {미리보기.평가.차단사유목록.Count}건을 확인해 주세요.";
        });

    public Task 저장Async(CancellationToken cancellationToken = default)
        => RunAsync(async () =>
        {
            if (!저장조건충족 || 선택집단 is null)
            {
                throw new InvalidOperationException("1.0 수요 집단의 1.5 인계 승인을 먼저 완료해 주세요.");
            }

            동기화미확인항목();
            var request = Clone(초안);
            request.기대Revision = 저장원장?.Revision;
            var fingerprint = Fingerprint(request);
            if (!string.Equals(_대기저장지문, fingerprint, StringComparison.Ordinal))
            {
                _대기저장지문 = fingerprint;
                _대기저장멱등키 = $"admin-readiness-{Guid.NewGuid():N}";
            }
            request.요청멱등키 = _대기저장멱등키!;

            저장원장 = await client.저장Async(선택집단.자동집단Id, request, cancellationToken);
            초안 = Clone(저장원장.준비자료);
            _미확인항목Text = string.Join(Environment.NewLine, 초안.미확인항목목록);
            미리보기 = 저장원장;
            초안저장후변경됨 = false;
            준비Os상태 = await client.준비Os상태조회Async(선택집단.자동집단Id, cancellationToken);
            메시지 = 저장원장.평가.전문검토인계가능
                ? "기존 같이 수입 원장의 1.5 준비 블록을 저장했습니다. 계약·결제·신고·운송 실행은 열리지 않았습니다."
                : "기존 같이 수입 원장에 미완성 준비 블록을 저장했습니다. 차단 사유를 해소한 뒤 다시 미리 확인해 주세요.";
        });

    public Task Os작업실행Async(
        string 작업코드,
        bool 재시도여부,
        CancellationToken cancellationToken = default)
        => RunAsync(async () =>
        {
            if (!Os작업실행조건충족 || 선택집단 is null || 준비Os상태 is null)
            {
                throw new InvalidOperationException(초안저장후변경됨
                    ? "편집 중인 자료를 먼저 저장한 뒤 OS 점검을 실행해 주세요."
                    : "준비 블록이 저장된 같이 수입 원장과 활성 OS가 필요합니다.");
            }

            준비Os상태 = await client.준비Os작업실행Async(
                선택집단.자동집단Id,
                new 같이수입준비Os작업실행요청
                {
                    요청멱등키 = $"admin-os-{Guid.NewGuid():N}",
                    기대Revision = 준비Os상태.원장Revision,
                    작업코드 = 작업코드,
                    재시도여부 = 재시도여부
                },
                cancellationToken);
            if (저장원장 is not null)
            {
                저장원장.Revision = 준비Os상태.원장Revision;
            }
            메시지 = 재시도여부
                ? "선택한 1.5 OS 작업을 다시 점검했습니다. 외부 실행은 발생하지 않았습니다."
                : "1.5 OS가 저장 원장의 근거와 최신성을 다시 점검했습니다.";
        });

    public Task 전문검토인계Async(CancellationToken cancellationToken = default)
        => RunAsync(async () =>
        {
            if (!전문검토인계조건충족 || 선택집단 is null || 준비Os상태 is null)
            {
                throw new InvalidOperationException("전문 검토 인계 조건과 수신자·범위·메모를 확인해 주세요.");
            }

            준비Os상태 = await client.전문검토인계Async(
                선택집단.자동집단Id,
                new 같이수입준비Os전문검토인계요청
                {
                    요청멱등키 = $"admin-qualified-review-{Guid.NewGuid():N}",
                    기대Revision = 준비Os상태.원장Revision,
                    검토수신자표시명 = 전문검토수신자.Trim(),
                    검토범위 = 전문검토범위.Trim(),
                    인계메모 = 전문검토인계메모.Trim()
                },
                cancellationToken);
            if (저장원장 is not null)
            {
                저장원장.Revision = 준비Os상태.원장Revision;
            }
            전문검토인계메모 = string.Empty;
            메시지 = "전문 검토 인계를 원장에 기록했습니다. 이 기록은 전문 판단 완료나 다음 단계 실행 승인이 아닙니다.";
        });

    public void 초안변경됨()
    {
        초안저장후변경됨 = 저장원장 is not null;
        OnPropertyChanged(nameof(추가가능재료집단목록));
        if (미리보기 is not null && !ReferenceEquals(미리보기, 저장원장))
        {
            미리보기 = null;
        }
    }

    public Task 재료집단추가Async(CancellationToken cancellationToken = default)
        => RunAsync(async () =>
        {
            if (선택집단 is null || string.IsNullOrWhiteSpace(추가재료집단Id))
            {
                throw new InvalidOperationException("추가할 재료의 1.0 수요 집단을 선택해 주세요.");
            }

            var group = 작업대목록.FirstOrDefault(item => string.Equals(
                item.자동집단Id,
                추가재료집단Id.Trim(),
                StringComparison.Ordinal))
                ?? throw new InvalidOperationException("추가할 재료 수요 집단을 대기열에서 찾을 수 없습니다.");
            if (!공동구매거래문맥정책.호환됨(
                    선택집단.거래유형,
                    선택집단.가격표시기준,
                    group.거래유형,
                    group.가격표시기준))
            {
                throw new InvalidOperationException(
                    "B2B/B2C 또는 부가세 표시 기준이 다른 수요는 같은 같이 수입 원장에 합칠 수 없습니다.");
            }
            var state = await client.운영상태조회Async(group.자동집단Id, cancellationToken)
                ?? throw new InvalidOperationException("추가할 재료의 1.0 수요 OS 상태를 찾을 수 없습니다.");
            if (!string.Equals(
                    state.인계상태,
                    공동구매수요모집인계상태코드.승인후속대기,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"'{group.상품명}' 수요 집단의 1.5 인계를 먼저 승인해 주세요.");
            }

            초안.재료품목목록.Add(Material(group));
            초안.품목분류후보목록.Add(NewClassification(group, 초안));
            추가재료집단Id = string.Empty;
            초안변경됨();
            메시지 = $"'{group.상품명}'을 같은 같이 수입 준비 묶음에 추가했습니다.";
        });

    public void 재료삭제(같이수입준비재료품목 item)
    {
        if (선택집단 is not null
            && string.Equals(item.원천자동집단Id, 선택집단.자동집단Id, StringComparison.Ordinal))
        {
            메시지 = "경로의 기준 수요 집단 재료는 묶음에서 제거할 수 없습니다.";
            오류메시지 = true;
            return;
        }

        초안.재료품목목록.Remove(item);
        초안.견적목록.RemoveAll(quote => string.Equals(quote.재료키, item.재료키, StringComparison.OrdinalIgnoreCase));
        초안.예상비용목록.RemoveAll(cost => string.Equals(cost.재료키, item.재료키, StringComparison.OrdinalIgnoreCase));
        초안.품목분류후보목록.RemoveAll(candidate => string.Equals(candidate.재료키, item.재료키, StringComparison.OrdinalIgnoreCase));
        초안변경됨();
    }

    public void 포워더인계상태변경(string? value)
    {
        var state = 같이수입준비포워더인계상태코드.지원목록.Contains(value?.Trim() ?? string.Empty)
            ? value!.Trim()
            : 같이수입준비포워더인계상태코드.초안;
        초안.포워더인계.인계상태코드 = state;
        if (string.Equals(state, 같이수입준비포워더인계상태코드.인계기록됨, StringComparison.OrdinalIgnoreCase))
        {
            초안.포워더인계.인계기록자표시명 = _관리자표시명;
            초안.포워더인계.인계시각Utc = DateTimeOffset.UtcNow;
        }
        else if (string.Equals(state, 같이수입준비포워더인계상태코드.초안, StringComparison.OrdinalIgnoreCase))
        {
            초안.포워더인계.인계기록자표시명 = string.Empty;
            초안.포워더인계.인계시각Utc = null;
        }
        초안변경됨();
    }

    public void 포워더전달정보범위변경(string? value)
    {
        var scope = 같이수입준비포워더전달정보범위코드.지원목록.Contains(value?.Trim() ?? string.Empty)
            ? value!.Trim()
            : 같이수입준비포워더전달정보범위코드.집계수요전용;
        초안.포워더인계.전달정보범위코드 = scope;
        초안.포워더인계.개인정보포함여부 = string.Equals(
            scope,
            같이수입준비포워더전달정보범위코드.동의된사용자별최소정보,
            StringComparison.OrdinalIgnoreCase);
        초안변경됨();
    }

    public void 포워더회신방식변경(string? value)
    {
        var proposal = value?.Trim().ToUpperInvariant() ?? string.Empty;
        초안.국제운송검토.포워더제안방식코드 = proposal;
        초안.국제운송검토.선택방식코드 = string.Empty;
        초안.국제운송검토.검토상태코드 = string.IsNullOrWhiteSpace(proposal)
            ? 같이수입준비국제운송검토상태코드.검토필요
            : 같이수입준비국제운송검토상태코드.포워더회신완료;
        if (string.IsNullOrWhiteSpace(proposal))
        {
            초안.국제운송검토.회신업체표시명 = string.Empty;
            초안.국제운송검토.회신기록자표시명 = string.Empty;
            초안.국제운송검토.회신시각Utc = null;
        }
        else
        {
            초안.국제운송검토.회신업체표시명 = 초안.포워더인계.전달대상업체명;
            초안.국제운송검토.회신기록자표시명 = _관리자표시명;
            초안.국제운송검토.회신시각Utc = DateTimeOffset.UtcNow;
            if (string.Equals(
                    초안.포워더인계.인계상태코드,
                    같이수입준비포워더인계상태코드.인계기록됨,
                    StringComparison.OrdinalIgnoreCase))
            {
                초안.포워더인계.인계상태코드 = 같이수입준비포워더인계상태코드.회신기록됨;
            }
        }
        초안변경됨();
    }

    public void 국제운송선택변경(string? value)
        => 포워더회신방식변경(value);

    public void 도착국가변경(string? value)
    {
        var country = 같이수입준비국가코드.정규화(value);
        초안.도착국가코드 = country;
        var system = 같이수입준비품목분류체계코드.대상국가체계(country);
        foreach (var item in 초안.품목분류후보목록)
        {
            item.관할국가코드 = country;
            item.분류체계코드 = system;
        }
        foreach (var item in 초안.국가별검토항목목록)
        {
            item.관할국가코드 = country;
        }
        초안변경됨();
    }

    public void 공급자추가()
    {
        var sequence = 초안.공급자근거목록.Count + 1;
        초안.공급자근거목록.Add(new 같이수입공급자근거
        {
            공급자후보키 = $"supplier-{sequence}",
            확인시각Utc = DateTimeOffset.UtcNow,
            검토자표시명 = _관리자표시명,
            검토시각Utc = DateTimeOffset.UtcNow,
            최신상태재확인필요 = true,
            플랫폼자동선정여부 = false
        });
        초안변경됨();
    }

    public void 공급자삭제(같이수입공급자근거 item)
    {
        초안.공급자근거목록.Remove(item);
        초안변경됨();
    }

    public void 견적추가()
    {
        var sequence = 초안.견적목록.Count + 1;
        초안.견적목록.Add(new 같이수입견적근거
        {
            견적키 = $"quote-{sequence}",
            재료키 = 초안.재료품목목록.FirstOrDefault()?.재료키 ?? string.Empty,
            공급자후보키 = 초안.공급자근거목록.FirstOrDefault()?.공급자후보키 ?? string.Empty,
            통화코드 = 초안.기준통화코드,
            Incoterms후보 = 같이수입준비Incoterms코드.Fca,
            유효기한Utc = DateTimeOffset.UtcNow.AddDays(30),
            확인시각Utc = DateTimeOffset.UtcNow
        });
        초안변경됨();
    }

    public void 견적삭제(같이수입견적근거 item)
    {
        초안.견적목록.Remove(item);
        초안변경됨();
    }

    public void 비용추가()
    {
        초안.예상비용목록.Add(NewCost($"other-{초안.예상비용목록.Count + 1}", "Other", "기타 예상비용", 초안.기준통화코드));
        초안변경됨();
    }

    public void 비용삭제(같이수입예상비용근거 item)
    {
        초안.예상비용목록.Remove(item);
        초안변경됨();
    }

    public void 품목분류추가()
    {
        초안.품목분류후보목록.Add(new 같이수입품목분류후보
        {
            후보키 = $"classification-{초안.품목분류후보목록.Count + 1}",
            재료키 = 초안.재료품목목록.FirstOrDefault()?.재료키 ?? string.Empty,
            관할국가코드 = 초안.도착국가코드,
            분류체계코드 = 같이수입준비품목분류체계코드.대상국가체계(초안.도착국가코드),
            품목코드 = 초안.재료품목목록.FirstOrDefault()?.원천Hs코드 ?? 선택집단?.HS코드 ?? string.Empty,
            검토상태코드 = 같이수입준비검토상태코드.전문가검토필요,
            전문가검토필요 = true,
            확인시각Utc = DateTimeOffset.UtcNow
        });
        초안변경됨();
    }

    public void 품목분류삭제(같이수입품목분류후보 item)
    {
        초안.품목분류후보목록.Remove(item);
        초안변경됨();
    }

    public void 국가검토추가()
    {
        초안.국가별검토항목목록.Add(new 같이수입국가별검토항목
        {
            관할국가코드 = 초안.도착국가코드,
            항목코드 = $"review-{초안.국가별검토항목목록.Count + 1}",
            검토상태코드 = 같이수입준비검토상태코드.미확인,
            책임역할코드 = 같이수입준비책임역할코드.수입자,
            확인시각Utc = DateTimeOffset.UtcNow
        });
        초안변경됨();
    }

    public void 국가검토삭제(같이수입국가별검토항목 item)
    {
        초안.국가별검토항목목록.Remove(item);
        초안변경됨();
    }

    public void 책임추가()
    {
        초안.책임초안목록.Add(new 같이수입책임초안());
        초안변경됨();
    }

    public void 책임삭제(같이수입책임초안 item)
    {
        초안.책임초안목록.Remove(item);
        초안변경됨();
    }

    private async Task 선택CoreAsync(공동구매자동집단요약응답 item, CancellationToken cancellationToken)
    {
        선택집단 = item;
        운영상태 = await client.운영상태조회Async(item.자동집단Id, cancellationToken);
        저장원장 = 운영상태?.후속워크플로우활성여부 == true
            ? await client.준비원장조회Async(item.자동집단Id, cancellationToken)
            : null;
        준비Os상태 = 저장원장 is null
            ? null
            : await client.준비Os상태조회Async(item.자동집단Id, cancellationToken);
        미리보기 = 저장원장;
        초안 = 저장원장 is null ? CreateDraft(item) : Clone(저장원장.준비자료);
        if (초안.재료품목목록.Count == 0)
        {
            초안.재료품목목록.Add(Material(item));
        }
        초안.국제운송검토 ??= new 같이수입준비국제운송검토();
        초안.포워더인계 ??= new 같이수입준비포워더인계();
        초안.포워더인계.전달항목코드목록 ??= [.. 같이수입준비포워더전달항목코드.기본집계목록];
        if (초안.재료품목목록.Count == 1)
        {
            var materialKey = 초안.재료품목목록[0].재료키;
            foreach (var quote in 초안.견적목록.Where(quote => string.IsNullOrWhiteSpace(quote.재료키)))
            {
                quote.재료키 = materialKey;
            }
            foreach (var classification in 초안.품목분류후보목록.Where(candidate => string.IsNullOrWhiteSpace(candidate.재료키)))
            {
                classification.재료키 = materialKey;
            }
        }
        초안저장후변경됨 = false;
        _미확인항목Text = string.Join(Environment.NewLine, 초안.미확인항목목록);
        OnPropertyChanged(nameof(미확인항목Text));
        승인사유 = string.Empty;
        _대기저장지문 = null;
        _대기저장멱등키 = null;
        추가재료집단Id = string.Empty;
        OnPropertyChanged(nameof(추가가능재료집단목록));
        if (운영상태 is null)
        {
            메시지 = "선택한 집단의 모집 OS 상태를 찾을 수 없습니다.";
            오류메시지 = true;
        }
        else if (!후속기능활성)
        {
            메시지 = "1.5 공급·가격·무역 준비 기능이 비활성입니다. 운영 승인 전에는 자료를 저장하지 않습니다.";
        }
    }

    private 같이수입준비원장저장요청 CreateDraft(공동구매자동집단요약응답 group)
    {
        var draft = 같이수입준비초안Factory.CreateBase(group);
        foreach (var category in 같이수입준비비용범주코드.필수목록)
        {
            draft.예상비용목록.Add(NewCost($"cost-{category}", category, CostLabel(category), draft.기준통화코드));
        }
        draft.품목분류후보목록.Add(NewClassification(group, draft));
        draft.국가별검토항목목록.Add(new 같이수입국가별검토항목
        {
            관할국가코드 = 같이수입준비국가코드.대한민국,
            항목코드 = "KR-MFDS-IMPORT-FOOD",
            표시명 = "수입식품·한글표시·해외제조업소 검토",
            검토상태코드 = 같이수입준비검토상태코드.미확인,
            책임역할코드 = 같이수입준비책임역할코드.수입자,
            공식원출처Url = "https://impfood.mfds.go.kr/",
            확인시각Utc = DateTimeOffset.UtcNow,
            미확인사유 = "실제 상품·제조업소·표시사항을 자격 있는 담당자가 확인해야 합니다."
        });
        foreach (var role in 같이수입준비책임역할코드.필수초안역할목록)
        {
            draft.책임초안목록.Add(new 같이수입책임초안 { 역할코드 = role });
        }
        return draft;
    }

    private static 같이수입준비재료품목 Material(공동구매자동집단요약응답 group)
        => 같이수입준비초안Factory.Material(group);

    private static 같이수입품목분류후보 NewClassification(
        공동구매자동집단요약응답 group,
        같이수입준비원장저장요청 draft)
        => 같이수입준비초안Factory.NewClassification(group, draft, DateTimeOffset.UtcNow);

    private static 같이수입예상비용근거 NewCost(string key, string category, string label, string currency)
        => 같이수입준비초안Factory.NewCost(key, category, label, currency, DateTimeOffset.UtcNow);

    private static string CostLabel(string category)
        => 같이수입준비초안Factory.CostLabel(category);

    private async Task RunAsync(Func<Task> action)
    {
        if (처리중)
        {
            return;
        }

        처리중 = true;
        메시지 = null;
        오류메시지 = false;
        try
        {
            await action();
        }
        catch (Exception exception)
        {
            오류메시지 = true;
            메시지 = FriendlyMessage(exception);
        }
        finally
        {
            처리중 = false;
        }
    }

    private void 선택초기화()
    {
        선택집단 = null;
        운영상태 = null;
        저장원장 = null;
        미리보기 = null;
        준비Os상태 = null;
        초안 = new 같이수입준비원장저장요청();
        초안저장후변경됨 = false;
        _미확인항목Text = string.Empty;
        추가재료집단Id = string.Empty;
        OnPropertyChanged(nameof(추가가능재료집단목록));
    }

    private void 동기화미확인항목()
        => 초안.미확인항목목록 = _미확인항목Text
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private void RaiseStateProperties()
    {
        OnPropertyChanged(nameof(후속기능활성));
        OnPropertyChanged(nameof(인계승인됨));
        OnPropertyChanged(nameof(인계승인가능));
        OnPropertyChanged(nameof(미리보기가능));
        OnPropertyChanged(nameof(저장가능));
        OnPropertyChanged(nameof(Os작업실행가능));
        OnPropertyChanged(nameof(전문검토인계가능));
        OnPropertyChanged(nameof(현재평가));
        OnPropertyChanged(nameof(완료검토항목수));
    }

    private static 같이수입준비원장저장요청 Clone(같이수입준비원장저장요청 source)
        => 같이수입준비초안Codec.Clone(source);

    private static string Fingerprint(같이수입준비원장저장요청 request)
        => 같이수입준비초안Codec.Fingerprint(request);

    private static bool Contains(string? value, string query)
        => value?.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase) == true;

    private static string FriendlyMessage(Exception exception)
        => exception switch
        {
            SsalddelApiException { StatusCode: 401 } => "관리자 로그인이 만료되었습니다. 다시 로그인해 주세요.",
            SsalddelApiException { StatusCode: 403 } => "이 작업에는 서버관리자 권한이 필요합니다.",
            SsalddelApiException { StatusCode: 404 } => "대상을 찾을 수 없거나 해당 버전 기능이 비활성입니다.",
            SsalddelApiException apiException => apiException.Message,
            _ => exception.Message
        };
}
