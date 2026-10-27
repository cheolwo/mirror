using Ssalddel.Ui.Common.Areas.App.Models;
using Ssalddel.Contracts.Shipper.Request;
using Ssalddel.Contracts.Common.Operations;
using Ssalddel.Ui.Common.Areas.App.ViewModels.Transport;

namespace Ssalddel.Ui.Common.Areas.App.ViewModels;

public sealed class 운송의뢰작성ViewModel
{
    private readonly 차량추천조회Session _차량추천Session;

    public 운송의뢰작성ViewModel()
    {
        _차량추천Session = new(
            () => 적재입력.입력지문,
            () => 차량추천조회가능,
            차량추천입력생성,
            () => 차량종류,
            vehicle => 차량종류 = vehicle);
    }

    public static IReadOnlyList<string> 화물적재형태목록 => 운송정보입력정책.화물적재형태목록;

    public string 화물종류 { get; set; } = string.Empty;
    public string? 화물설명 { get; set; }
    public string 화물적재형태 { get; set; } = "일반 화물(박스/팔레트)";
    public int? 화물수량 { get; set; } = 1;
    public int? 화물길이Mm { get; set; }
    public int? 화물폭Mm { get; set; }
    public int? 화물높이Mm { get; set; }
    public int? 팔레트개수 { get; set; }
    public decimal? 화물중량Kg { get; set; }
    public decimal? 화물부피Cbm { get; set; }
    public bool? 화물바닥회전가능여부 { get; set; } = true;
    public bool? 화물적층가능여부 { get; set; } = true;
    public bool 화물파손주의여부 { get; set; }
    public string? 온도조건 { get; set; }

    public string 운송방식 { get; set; } = "혼적";
    private string _상차도로명주소 = string.Empty;
    public string 상차도로명주소
    {
        get => _상차도로명주소;
        set
        {
            if (!string.Equals(_상차도로명주소, value, StringComparison.Ordinal)) 상차위도 = 상차경도 = null;
            _상차도로명주소 = value;
        }
    }
    public decimal? 상차위도 { get; set; }
    public decimal? 상차경도 { get; set; }
    public string? 상차상세주소 { get; set; }
    public string 상차연락처이름 { get; set; } = string.Empty;
    public string 상차연락처전화번호 { get; set; } = string.Empty;
    public DateTime? 상차시간창시작일시 { get; set; }
    public DateTime? 상차시간창종료일시 { get; set; }
    public decimal? 상차작업시간분 { get; set; }
    private string _하차도로명주소 = string.Empty;
    public string 하차도로명주소
    {
        get => _하차도로명주소;
        set
        {
            if (!string.Equals(_하차도로명주소, value, StringComparison.Ordinal)) 하차위도 = 하차경도 = null;
            _하차도로명주소 = value;
        }
    }
    public decimal? 하차위도 { get; set; }
    public decimal? 하차경도 { get; set; }
    public string? 하차상세주소 { get; set; }
    public string 하차연락처이름 { get; set; } = string.Empty;
    public string 하차연락처전화번호 { get; set; } = string.Empty;
    public DateTime? 하차시간창시작일시 { get; set; }
    public DateTime? 하차시간창종료일시 { get; set; }
    public decimal? 하차작업시간분 { get; set; }
    public string? 작업시간오류 => 상차작업시간분 is < 0m || 하차작업시간분 is < 0m
        ? "상차·하차 작업시간은 0분 이상이어야 합니다." : null;
    public bool 작업시간확인됨 => 상차작업시간분 is >= 0m && 하차작업시간분 is >= 0m;
    public string? 서비스레벨 { get; set; }
    public string? 요청사항 { get; set; }
    public string? 전송요청사항 => 요청사항전송문구(요청사항, 화물적재형태);

    /// <summary>사용자가 선택한 취급 조건을 추천과 접수에 같은 문구로 보존한다. 초안의 원문은 바꾸지 않는다.</summary>
    public static string? 요청사항전송문구(string? 요청원문, string? 적재형태)
        => string.IsNullOrWhiteSpace(적재형태) ? 요청원문
            : string.IsNullOrWhiteSpace(요청원문) ? $"적재 형태: {적재형태}"
            : $"{요청원문}\n적재 형태: {적재형태}";

    public string? 차량종류 { get; set; }
    public decimal? 예상거리Km { get; set; }
    public string 결제수단 { get; set; } = "카드";
    public int? 결제예정금액 { get; set; }
    public decimal? 기준운임 { get; set; }
    public int? 기사지급예정운임 { get; set; }
    public decimal? 대기료 { get; set; }
    public decimal? 수작업비 { get; set; }
    public decimal? 할증 { get; set; }
    public int 알선단계 { get; set; } = 1;
    public bool 재알선금지 { get; set; } = true;
    public string? 알선소Id { get; set; }
    public string? 절차메모 { get; set; }
    public DateTime 작성일시 { get; set; } = DateTime.Now;

    public decimal 부가비용합계 => (대기료 ?? 0) + (수작업비 ?? 0) + (할증 ?? 0);

    private IReadOnlyList<string> 화물필수누락Code목록 => FreightCargoRegistrationPolicy.MissingFields(new(
            화물종류, 화물중량Kg, 화물길이Mm, 화물폭Mm, 화물높이Mm, 상차도로명주소, 하차도로명주소))
        .Where(code => code is not ("PickupAddress" or "DropoffAddress"))
        .ToArray();

    public IReadOnlyList<string> 화물필수보완목록 => 화물필수누락Code목록.Select(화물필수보완문구).ToArray();

    private static string 화물필수보완문구(string code) => code switch
        {
            "CargoType" => "화물 종류를 입력해 주세요.",
            "GrossWeightIncludingPackagingKg" => "포장·깔판을 포함한 전체 중량(kg)을 0보다 크게 입력해 주세요.",
            "LengthMm" => "포장한 화물의 길이(mm)를 0보다 크게 입력해 주세요.",
            "WidthMm" => "포장한 화물의 너비(mm)를 0보다 크게 입력해 주세요.",
            "HeightMm" => "포장한 화물의 높이(mm)를 0보다 크게 입력해 주세요.",
            _ => "화물 정보를 확인해 주세요."
        };

    public bool 화물정보입력됨 => 화물필수보완목록.Count == 0;

    public bool 운송정보입력됨 =>
        !string.IsNullOrWhiteSpace(상차도로명주소)
        && !string.IsNullOrWhiteSpace(하차도로명주소)
        && 상차시간창오류 is null
        && 하차시간창오류 is null;

    public string? 상차시간창오류 => !상차시간창시작일시.HasValue && !상차시간창종료일시.HasValue
        ? "상차 시간창의 시작과 종료 일시를 입력해 주세요."
        : 시간창오류("상차", 상차시간창시작일시, 상차시간창종료일시);
    public string? 하차시간창오류 => 시간창오류("하차", 하차시간창시작일시, 하차시간창종료일시);

    public bool 절차정보입력됨 =>
        !string.IsNullOrWhiteSpace(차량종류)
        && !string.IsNullOrWhiteSpace(결제수단);

    public bool 전체입력됨 => 화물정보입력됨 && 운송정보입력됨 && 절차정보입력됨;
    public bool 서버등록가능 => 필수입력오류목록.Count == 0;
    public string 권장운송방식 => 운송정보입력정책.Get권장운송방식(화물적재형태);
    public string 운송정보입력안내 => 운송정보입력정책.Get안내문구(화물적재형태);
    public string 요청사항입력힌트 => 운송정보입력정책.Get요청사항힌트(화물적재형태);
    public IReadOnlyList<운송정보입력요구사항> 운송정보요구사항목록 => 운송정보입력정책.Get요구사항(화물적재형태);
    public IReadOnlyList<string> 운송정보경고목록 =>
        운송정보입력정책.Get경고목록(화물적재형태, 상차도로명주소, 하차도로명주소, 요청사항);
    public IReadOnlyList<string> 요금정책경고목록 => Build요금정책경고목록();

    public bool 차량추천조회중 => _차량추천Session.차량추천조회중;
    public string? 차량추천조회오류 => _차량추천Session.차량추천조회오류;
    public 화물적재입력결과 적재입력 => 화물적재입력Policy.계산(new()
    {
        화물종류 = 화물종류, 설명 = 화물설명, 수량 = 화물수량,
        길이Mm = 화물길이Mm, 폭Mm = 화물폭Mm, 높이Mm = 화물높이Mm,
        총중량Kg = 화물중량Kg, 입력부피Cbm = 화물부피Cbm, 팔레트개수 = 팔레트개수,
        온도조건 = 온도조건, 바닥회전가능여부 = 화물바닥회전가능여부,
        적층가능여부 = 화물적층가능여부, 파손주의여부 = 화물파손주의여부,
        운송방식 = 운송방식, 서비스레벨 = 서비스레벨, 요청사항 = 전송요청사항
    });
    public bool 차량추천조회가능 => 화물정보입력됨 && 적재입력.유효여부
        && 화물바닥회전가능여부.HasValue && 화물적층가능여부.HasValue;
    public bool 차량추천현재유효 => _차량추천Session.차량추천현재유효;
    public bool 차량추천조건변경됨 => _차량추천Session.차량추천조건변경됨;
    public bool 적용차량재확인필요 => _차량추천Session.적용차량재확인필요;
    public IReadOnlyList<차량추천후보응답> 서버추천후보 => _차량추천Session.서버추천후보;
    public IReadOnlyList<string> 서버추천사유 => _차량추천Session.서버추천사유;
    public IReadOnlyList<string> 서버추천경고 => _차량추천Session.서버추천경고;
    public IReadOnlyList<string> 추천차량종류목록 => _차량추천Session.추천차량종류목록;
    public string 추천운송설명 => Build추천운송설명();
    public IReadOnlyList<string> 결제후속절차목록 => Build결제후속절차목록();
    public string 결제후속절차요약 => string.Join(" → ", 결제후속절차목록);
    public IReadOnlyList<운송의뢰검증메시지> 입력검증메시지목록 => Build입력검증메시지목록();
    public IReadOnlyList<운송의뢰검증메시지> 필수입력오류목록 => 입력검증메시지목록.Where(x => x.필수).ToArray();

    /// <summary>같은 필수 규칙을 현재 모바일 입력 문맥에만 투영한다. 앞 단계·연락처에 새 필수를 만들지 않는다.</summary>
    public IReadOnlyList<string> GetMobileStepErrors(ShipperRequestMobileStep step)
    {
        _ = ShipperRequestMobileStepCatalog.Get(step);
        return 필수입력오류목록.Where(x => step == ShipperRequestMobileStep.Review || x.MobileStep == step)
            .Select(x => x.내용).ToArray();
    }

    public IReadOnlyList<운송의뢰작성단계> 단계목록 =>
    [
        new(ShipperRequestAuthoringStep.Cargo, "화물 정보", "품목, 전체 중량, 포장 크기", 화물정보입력됨),
        new(ShipperRequestAuthoringStep.Transport, "운송 정보", "상차지, 하차지, 연락처", 운송정보입력됨),
        new(ShipperRequestAuthoringStep.Procedure, "절차/결제 정보", "차량, 운임, 알선 절차", 절차정보입력됨),
        new(ShipperRequestAuthoringStep.Review, "최종 확인", "전체 입력값 확인", 전체입력됨)
    ];

    public void 화물기반추천적용()
    {
        if (추천차량종류목록.FirstOrDefault() is { } vehicle) 서버추천차량선택(vehicle);
    }

    public bool 서버추천차량선택(string vehicle)
        => _차량추천Session.서버추천차량선택(vehicle);

    public 차량추천요청 차량추천입력생성() => new()
    {
        화물종류 = 화물종류, 화물설명 = 화물설명, 화물수량 = 화물수량,
        화물길이Mm = 화물길이Mm, 화물폭Mm = 화물폭Mm, 화물높이Mm = 화물높이Mm,
        화물중량Kg = 화물중량Kg, 화물부피Cbm = 화물부피Cbm, 팔레트개수 = 팔레트개수,
        화물온도조건 = 온도조건, 화물바닥회전가능여부 = 화물바닥회전가능여부,
        화물적층가능여부 = 화물적층가능여부, 화물파손주의여부 = 화물파손주의여부,
        운송방식 = 운송방식, 서비스레벨 = 서비스레벨, 요청사항 = 전송요청사항
    };

    public Task 차량추천조회Async(Func<차량추천요청, CancellationToken, Task<차량추천응답>> provider,
        CancellationToken cancellationToken = default)
        => _차량추천Session.조회Async(provider, cancellationToken);

    public void 차량추천초기화()
        => _차량추천Session.추천초기화();

    /// <summary>현재 검증된 선택을 같은 접수 호출 안에서만 고정한다. 저장된 초안의 복원에는 사용하지 않는다.</summary>
    public 운송의뢰작성ViewModel 접수입력고정복사()
    {
        var frozen = new 운송의뢰작성ViewModel();
        frozen.ApplyDraft(ToDraft());
        _차량추천Session.접수선택복사(frozen._차량추천Session);
        return frozen;
    }

    public 운송모델작성Draft ToDraft()
    {
        var 정책경고목록 = new List<string>();

        if (!화물정보입력됨)
        {
            정책경고목록.Add("화물 정보가 아직 충분히 입력되지 않았습니다.");
        }

        if (!운송정보입력됨)
        {
            정책경고목록.Add("상차지와 하차지 정보가 아직 충분히 입력되지 않았습니다.");
        }

        if (!절차정보입력됨)
        {
            정책경고목록.Add("차량 또는 결제 절차 정보가 아직 충분히 입력되지 않았습니다.");
        }

        foreach (var warning in 운송정보경고목록)
        {
            정책경고목록.Add($"운송정보확인: {warning}");
        }

        정책경고목록.AddRange(요금정책경고목록);

        return new 운송모델작성Draft
        {
            작성출처 = "공용 운송 의뢰 작성",
            화물종류 = 화물종류,
            화물설명 = 화물설명,
            화물적재형태 = 화물적재형태,
            화물수량 = 화물수량,
            화물길이Mm = 화물길이Mm,
            화물폭Mm = 화물폭Mm,
            화물높이Mm = 화물높이Mm,
            팔레트개수 = 팔레트개수,
            화물중량Kg = 화물중량Kg,
            화물부피Cbm = 화물부피Cbm,
            화물바닥회전가능여부 = 화물바닥회전가능여부,
            화물적층가능여부 = 화물적층가능여부,
            화물파손주의여부 = 화물파손주의여부,
            추천차량재확인필요 = _차량추천Session.초안저장시재확인필요,
            온도조건 = 온도조건,
            운송방식 = 운송방식,
            픽업도로명주소 = 상차도로명주소,
            픽업위도 = 상차위도,
            픽업경도 = 상차경도,
            픽업상세주소 = 상차상세주소,
            픽업연락처이름 = 상차연락처이름,
            픽업연락처전화번호 = 상차연락처전화번호,
            픽업시간창시작일시 = 상차시간창시작일시,
            픽업시간창종료일시 = 상차시간창종료일시,
            픽업작업시간분 = 상차작업시간분,
            하차도로명주소 = 하차도로명주소,
            하차위도 = 하차위도,
            하차경도 = 하차경도,
            하차상세주소 = 하차상세주소,
            하차연락처이름 = 하차연락처이름,
            하차연락처전화번호 = 하차연락처전화번호,
            하차시간창시작일시 = 하차시간창시작일시,
            하차시간창종료일시 = 하차시간창종료일시,
            하차작업시간분 = 하차작업시간분,
            서비스레벨 = 서비스레벨,
            요청사항 = 요청사항,
            차량종류 = 차량종류,
            예상거리Km = 예상거리Km,
            결제수단 = 결제수단,
            결제예정금액 = 결제예정금액,
            기준운임 = 기준운임,
            기사지급예정운임 = 기사지급예정운임,
            대기료 = 대기료,
            수작업비 = 수작업비,
            할증 = 할증,
            알선단계 = 알선단계,
            재알선금지 = 재알선금지,
            알선소Id = 알선소Id,
            정책경고목록 = 정책경고목록,
            절차메모 = 절차메모,
            작성일시 = 작성일시
        };
    }

    public void ApplyDraft(운송모델작성Draft draft)
    {
        차량추천초기화();
        _차량추천Session.초안복원출처설정(draft.추천차량재확인필요);
        화물종류 = draft.화물종류;
        화물설명 = draft.화물설명;
        화물적재형태 = string.IsNullOrWhiteSpace(draft.화물적재형태) ? "일반 화물(박스/팔레트)" : draft.화물적재형태;
        화물수량 = draft.화물수량;
        화물길이Mm = draft.화물길이Mm;
        화물폭Mm = draft.화물폭Mm;
        화물높이Mm = draft.화물높이Mm;
        팔레트개수 = draft.팔레트개수;
        화물중량Kg = draft.화물중량Kg;
        화물부피Cbm = draft.화물부피Cbm;
        화물바닥회전가능여부 = draft.화물바닥회전가능여부;
        화물적층가능여부 = draft.화물적층가능여부;
        화물파손주의여부 = draft.화물파손주의여부;
        온도조건 = draft.온도조건;
        운송방식 = draft.운송방식;
        상차도로명주소 = draft.픽업도로명주소;
        상차위도 = draft.픽업위도;
        상차경도 = draft.픽업경도;
        상차상세주소 = draft.픽업상세주소;
        상차연락처이름 = draft.픽업연락처이름;
        상차연락처전화번호 = draft.픽업연락처전화번호;
        상차시간창시작일시 = draft.픽업시간창시작일시;
        상차시간창종료일시 = draft.픽업시간창종료일시;
        상차작업시간분 = draft.픽업작업시간분;
        하차도로명주소 = draft.하차도로명주소;
        하차위도 = draft.하차위도;
        하차경도 = draft.하차경도;
        하차상세주소 = draft.하차상세주소;
        하차연락처이름 = draft.하차연락처이름;
        하차연락처전화번호 = draft.하차연락처전화번호;
        하차시간창시작일시 = draft.하차시간창시작일시;
        하차시간창종료일시 = draft.하차시간창종료일시;
        하차작업시간분 = draft.하차작업시간분;
        서비스레벨 = draft.서비스레벨;
        요청사항 = draft.요청사항;
        차량종류 = draft.차량종류;
        예상거리Km = draft.예상거리Km;
        결제수단 = draft.결제수단;
        결제예정금액 = draft.결제예정금액;
        기준운임 = draft.기준운임;
        기사지급예정운임 = draft.기사지급예정운임;
        대기료 = draft.대기료;
        수작업비 = draft.수작업비;
        할증 = draft.할증;
        알선단계 = draft.알선단계;
        재알선금지 = draft.재알선금지;
        알선소Id = draft.알선소Id;
        절차메모 = draft.절차메모;
        작성일시 = draft.작성일시;
    }

    public void Reset()
    {
        _차량추천Session.새초안초기화();
        화물종류 = string.Empty;
        화물설명 = null;
        화물적재형태 = "일반 화물(박스/팔레트)";
        화물수량 = 1;
        화물길이Mm = 화물폭Mm = 화물높이Mm = 팔레트개수 = null;
        화물중량Kg = null;
        화물부피Cbm = null;
        화물바닥회전가능여부 = 화물적층가능여부 = true;
        화물파손주의여부 = false;
        온도조건 = null;
        운송방식 = "혼적";
        상차도로명주소 = string.Empty;
        상차위도 = 상차경도 = null;
        상차상세주소 = null;
        상차연락처이름 = string.Empty;
        상차연락처전화번호 = string.Empty;
        상차시간창시작일시 = null;
        상차시간창종료일시 = null;
        상차작업시간분 = null;
        하차도로명주소 = string.Empty;
        하차위도 = 하차경도 = null;
        하차상세주소 = null;
        하차연락처이름 = string.Empty;
        하차연락처전화번호 = string.Empty;
        하차시간창시작일시 = null;
        하차시간창종료일시 = null;
        하차작업시간분 = null;
        서비스레벨 = null;
        요청사항 = null;
        차량종류 = null;
        예상거리Km = null;
        결제수단 = "카드";
        결제예정금액 = null;
        기준운임 = null;
        기사지급예정운임 = null;
        대기료 = null;
        수작업비 = null;
        할증 = null;
        알선단계 = 1;
        재알선금지 = true;
        알선소Id = null;
        절차메모 = null;
        작성일시 = DateTime.Now;
    }

    private string Build추천운송설명()
    {
        if (차량추천조회중) return "현재 포장 조건으로 차량을 확인하고 있습니다.";
        if (차량추천조회오류 is not null) return 차량추천조회오류;
        if (차량추천조건변경됨 || 적용차량재확인필요) return "화물 조건이 바뀌었습니다. 차량 추천을 다시 확인해 주세요.";
        if (!차량추천현재유효) return "전체 중량·포장 크기와 회전·적층 조건을 입력한 뒤 차량을 확인해 주세요.";
        return 서버추천후보.Count == 0
            ? "현재 조건에 맞는 차량을 찾지 못했습니다. 입력한 규격과 조건을 확인해 주세요."
            : $"현재 적재 조건에 맞는 차량 {서버추천후보.Count}종을 확인했습니다. 시간·경로와 기사 배차는 다음 절차에서 별도로 확인합니다.";
    }

    private IReadOnlyList<string> Build결제후속절차목록()
    {
        if (결제수단.Contains("인수증", StringComparison.OrdinalIgnoreCase))
        {
            return ["인수증 거래로 등록", "상차/하차 증빙 사진 확인", "필요 시 서명 또는 인수증 번호 보관", "운송 완료 후 정산 확인"];
        }

        if (결제수단.Contains("카드", StringComparison.OrdinalIgnoreCase))
        {
            return ["서버 등록 후 카드 결제 대기", "결제 승인 확인", "배차 추천 진행", "운송 완료 후 정산 예정일 표시"];
        }

        if (결제수단.Contains("가상계좌", StringComparison.OrdinalIgnoreCase))
        {
            return ["서버 등록 후 가상계좌 발급", "입금 기한 안내", "입금 확인 후 배차 추천", "운송 완료 후 기사 정산"];
        }

        if (결제수단.Contains("계좌", StringComparison.OrdinalIgnoreCase) || 결제수단.Contains("이체", StringComparison.OrdinalIgnoreCase))
        {
            return ["입금 계좌와 입금자명 확인", "관리자 입금 확인", "배차 추천 진행", "운송 완료 후 정산 기록"];
        }

        if (결제수단.Contains("현금", StringComparison.OrdinalIgnoreCase) || 결제수단.Contains("정산", StringComparison.OrdinalIgnoreCase))
        {
            return ["현장 또는 별도 정산 조건 확인", "증빙 필요 여부 확인", "운송 완료 후 정산 알림", "입금 완료 상태 기록"];
        }

        return ["결제 조건 확인", "서버 등록", "배차 추천", "운송 완료 후 정산 확인"];
    }

    private IReadOnlyList<운송의뢰검증메시지> Build입력검증메시지목록()
    {
        var messages = new List<운송의뢰검증메시지>();
        if (작업시간오류 is { } handlingError)
            messages.Add(new("운송 정보", handlingError, ShipperRequestAuthoringStep.Transport, true)
            { MobileStep = ShipperRequestMobileStep.Schedule });
        else if (!작업시간확인됨)
            messages.Add(new("운송 정보", "상차·하차 예상 작업시간을 확인하면 다른 운송과 함께 수행 가능한지 검사할 수 있습니다. 빈값은 미확인으로 남습니다.", ShipperRequestAuthoringStep.Transport, false)
            { MobileStep = ShipperRequestMobileStep.Schedule });
        var missingCargoFields = 화물필수누락Code목록;

        foreach (var missingCode in missingCargoFields)
            messages.Add(new("화물 정보", 화물필수보완문구(missingCode), ShipperRequestAuthoringStep.Cargo, true)
            { MobileStep = missingCode == "CargoType" ? ShipperRequestMobileStep.Cargo : ShipperRequestMobileStep.Package });

        if (string.IsNullOrWhiteSpace(상차도로명주소))
        {
            messages.Add(new("운송 정보", "상차 도로명 주소를 입력해야 합니다.", ShipperRequestAuthoringStep.Transport, true)
            { MobileStep = ShipperRequestMobileStep.Pickup });
        }

        if (string.IsNullOrWhiteSpace(하차도로명주소))
        {
            messages.Add(new("운송 정보", "하차 도로명 주소를 입력해야 합니다.", ShipperRequestAuthoringStep.Transport, true)
            { MobileStep = ShipperRequestMobileStep.Dropoff });
        }

        if (string.IsNullOrWhiteSpace(상차연락처전화번호))
        {
            messages.Add(new("운송 정보", "상차 담당자 연락처가 있으면 기사님 도착/상차 준비 알림이 쉬워집니다.", ShipperRequestAuthoringStep.Transport, false)
            { MobileStep = ShipperRequestMobileStep.Pickup });
        }

        if (string.IsNullOrWhiteSpace(하차연락처전화번호))
        {
            messages.Add(new("운송 정보", "하차 담당자 연락처가 있으면 부재/예외 상황 대응이 쉬워집니다.", ShipperRequestAuthoringStep.Transport, false)
            { MobileStep = ShipperRequestMobileStep.Dropoff });
        }

        if (상차시간창오류 is { } pickupWindowError)
        {
            messages.Add(new("운송 정보", pickupWindowError, ShipperRequestAuthoringStep.Transport, true)
            { MobileStep = ShipperRequestMobileStep.Schedule });
        }

        if (하차시간창오류 is { } dropoffWindowError)
        {
            messages.Add(new("운송 정보", dropoffWindowError, ShipperRequestAuthoringStep.Transport, true)
            { MobileStep = ShipperRequestMobileStep.Schedule });
        }

        if (string.IsNullOrWhiteSpace(차량종류))
        {
            messages.Add(new("절차/결제 정보", "차량 종류를 선택해야 합니다.", ShipperRequestAuthoringStep.Procedure, true)
            { MobileStep = ShipperRequestMobileStep.Vehicle });
        }

        if (적용차량재확인필요)
        {
            messages.Add(new("절차/결제 정보", "화물 조건이 바뀌었으니 차량 추천을 다시 확인하고 선택해 주세요.", ShipperRequestAuthoringStep.Procedure, true)
            { MobileStep = ShipperRequestMobileStep.Vehicle });
        }

        foreach (var error in 적재입력.오류)
        {
            var duplicatedField = error switch
            {
                "총중량은(는) 0보다 커야 합니다." => "GrossWeightIncludingPackagingKg",
                "포장 길이은(는) 0보다 커야 합니다." => "LengthMm",
                "포장 폭은(는) 0보다 커야 합니다." => "WidthMm",
                "포장 높이은(는) 0보다 커야 합니다." => "HeightMm",
                _ => null
            };
            if (duplicatedField is not null && missingCargoFields.Contains(duplicatedField, StringComparer.Ordinal)) continue;
            messages.Add(new("화물 정보", error, ShipperRequestAuthoringStep.Cargo, true)
            { MobileStep = ShipperRequestMobileStep.Package });
        }

        if (string.IsNullOrWhiteSpace(결제수단))
        {
            messages.Add(new("절차/결제 정보", "결제 수단을 선택해야 합니다.", ShipperRequestAuthoringStep.Procedure, true)
            { MobileStep = ShipperRequestMobileStep.Payment });
        }

        if (!결제예정금액.HasValue || 결제예정금액.Value <= 0)
        {
            messages.Add(new("절차/결제 정보", "결제 예정 금액을 입력해야 정산 흐름을 검증할 수 있습니다.", ShipperRequestAuthoringStep.Procedure, true)
            { MobileStep = ShipperRequestMobileStep.Payment });
        }

        foreach (var warning in 운송정보경고목록)
        {
            messages.Add(new("운송 정보", warning, ShipperRequestAuthoringStep.Transport, false)
            {
                MobileStep = warning switch
                {
                    "상차지 주소를 입력해야 합니다." => ShipperRequestMobileStep.Pickup,
                    "하차지 주소를 입력해야 합니다." => ShipperRequestMobileStep.Dropoff,
                    _ => ShipperRequestMobileStep.Schedule
                }
            });
        }

        foreach (var warning in 요금정책경고목록)
        {
            messages.Add(new("절차/결제 정보", warning, ShipperRequestAuthoringStep.Procedure, false)
            { MobileStep = ShipperRequestMobileStep.Payment });
        }

        return messages;
    }

    private static string? 시간창오류(string label, DateTime? start, DateTime? end)
    {
        if (!start.HasValue && !end.HasValue) return null;
        if (!start.HasValue || !end.HasValue) return $"{label} 시간창의 시작과 종료 일시를 함께 입력해 주세요.";
        if (start.Value < DateTime.MinValue.AddHours(9) || end.Value < DateTime.MinValue.AddHours(9))
            return $"{label} 시간창에 올바른 날짜와 시간을 입력해 주세요.";
        return end.Value <= start.Value ? $"{label} 종료 일시는 시작 일시보다 늦어야 합니다." : null;
    }

    private IReadOnlyList<string> Build요금정책경고목록()
    {
        var warnings = new List<string>();

        if (기준운임.HasValue && 결제예정금액.HasValue && 결제예정금액.Value < 기준운임.Value)
        {
            warnings.Add($"기준운임미달: 결제예정금액이 기준운임 {기준운임:N0}원보다 낮습니다.");
        }

        if (!기사지급예정운임.HasValue)
        {
            warnings.Add("기사 지급 예정 운임을 입력해야 재알선/수수료 구조를 확인할 수 있습니다.");
        }
        else if (기준운임.HasValue && 기사지급예정운임.Value < 기준운임.Value * 0.7m)
        {
            warnings.Add("기사 지급 예정 운임이 기준운임의 70%보다 낮아 알선 단계 확인이 필요합니다.");
        }

        if (재알선금지 && 알선단계 > 1)
        {
            warnings.Add("재알선차단필요: 재알선 금지 의뢰인데 알선 단계가 2단계 이상입니다.");
        }

        if (!string.IsNullOrWhiteSpace(알선소Id) && 알선단계 > 1)
        {
            warnings.Add($"재알선의심: 알선소 {알선소Id}를 통한 {알선단계}단계 알선입니다.");
        }

        return warnings;
    }
}

public sealed record 운송의뢰작성단계(
    ShipperRequestAuthoringStep 단계,
    string 제목,
    string 설명,
    bool 완료);

public sealed record 운송의뢰검증메시지(
    string 구분,
    string 내용,
    ShipperRequestAuthoringStep 단계,
    bool 필수)
{
    public ShipperRequestMobileStep? MobileStep { get; init; }
}
