using DriverApp.Services;
using Hongdal.Contracts.Driver.Home;

namespace DriverApp.Models.Driver.Home;

public sealed class 기사홈ViewModel
{
    public string 기사Id { get; init; } = string.Empty;
    public string 기사명 { get; init; } = string.Empty;
    public string 운행상태 { get; init; } = string.Empty;
    public string 홈상태문구 { get; init; } = string.Empty;
    public string 주요행동코드 { get; init; } = string.Empty;
    public string 주요행동문구 { get; init; } = string.Empty;
    public bool 운행중 { get; init; }
    public long? 현재근무Id { get; init; }
    public DateTime? 운행시작시각 { get; init; }
    public bool 진행중운송있음 { get; init; }
    public long? 현재운송Id { get; init; }
    public string? 현재운송단계 { get; init; }
    public int 추천콜수 { get; init; }
    public int 적합추천콜수 { get; init; }
    public int 오늘예약수 { get; init; }
    public DateTime? 다음예약시각 { get; init; }
    public int 이번달배차건수 { get; init; }
    public decimal 이번달이용료 { get; init; }
    public decimal 월상한금액 { get; init; }
    public decimal 월상한남은금액 { get; init; }
    public bool 정산결제완료 { get; init; }
    public bool 푸시토큰등록됨 { get; init; }
    public bool 알림정상 { get; init; }
    public IReadOnlyList<기사홈할일항목ViewModel> 오늘할일 { get; init; } = Array.Empty<기사홈할일항목ViewModel>();

    public bool 추천콜있음 => 추천콜수 > 0;
    public bool 예약임박 => 오늘예약수 > 0 && 다음예약시각.HasValue;
    public bool 정산확인필요 => !정산결제완료 && 이번달배차건수 > 0;
    public bool 알림오류 => !푸시토큰등록됨 || !알림정상;

    public string 이번달이용료원화 => $"{이번달이용료:0}원";
    public string 월상한남은금액원화 => $"{월상한남은금액:0}원";
    public string 정산상태문구 => 정산결제완료
        ? "이번 달 정산이 완료되었습니다."
        : 이번달배차건수 > 0
            ? "이번 달 이용료를 확인해 주세요."
            : "이번 달에는 아직 정산 대상이 없습니다.";

    public static 기사홈ViewModel From(기사홈요약응답 dto)
    {
        var 운행중 = LooksWorking(dto.운행상태);
        var 진행중운송있음 = dto.진행중운송수 > 0 || dto.현재운송Id.HasValue;
        var 알림정상 = dto.푸시토큰등록됨;
        var 정산확인필요 = !dto.정산결제완료 && dto.이번달배차건수 > 0;

        var (주요행동코드, 주요행동문구, 홈상태문구) = ResolvePrimaryAction(dto, 운행중, 진행중운송있음, 알림정상, 정산확인필요);
        var tasks = BuildTasks(dto, 운행중, 진행중운송있음, 알림정상, 정산확인필요);

        return new 기사홈ViewModel
        {
            기사Id = dto.DriverId,
            기사명 = dto.기사명,
            운행상태 = dto.운행상태,
            홈상태문구 = 홈상태문구,
            주요행동코드 = 주요행동코드,
            주요행동문구 = 주요행동문구,
            운행중 = 운행중,
            현재근무Id = dto.현재근무Id,
            운행시작시각 = dto.운행시작시각,
            진행중운송있음 = 진행중운송있음,
            현재운송Id = dto.현재운송Id,
            현재운송단계 = dto.현재운송단계,
            추천콜수 = dto.추천콜수,
            적합추천콜수 = dto.적합추천콜수,
            오늘예약수 = dto.오늘예약수,
            다음예약시각 = dto.다음예약시각,
            이번달배차건수 = dto.이번달배차건수,
            이번달이용료 = dto.이번달이용료,
            월상한금액 = dto.이번달이용료상한,
            월상한남은금액 = dto.남은이용료,
            정산결제완료 = dto.정산결제완료,
            푸시토큰등록됨 = dto.푸시토큰등록됨,
            알림정상 = 알림정상,
            오늘할일 = tasks
        };
    }

    public static 기사홈ViewModel Empty() => new()
    {
        홈상태문구 = "상태를 불러오는 중입니다.",
        주요행동코드 = "LOADING",
        주요행동문구 = "잠시만 기다려 주세요.",
        오늘할일 = Array.Empty<기사홈할일항목ViewModel>()
    };

    private static (string Code, string Message, string Status) ResolvePrimaryAction(
        기사홈요약응답 dto,
        bool 운행중,
        bool 진행중운송있음,
        bool 알림정상,
        bool 정산확인필요)
    {
        if (진행중운송있음)
        {
            return ("VIEW_CURRENT_TRANSPORT", BuildTransportMessage(dto), "운송진행중");
        }

        if (!운행중)
        {
            return ("START_WORK", "운행을 시작하면 추천콜을 받을 수 있습니다.", "대기중");
        }

        if (dto.추천콜수 > 0)
        {
            return ("VIEW_RECOMMENDATIONS", $"받을 만한 추천콜 {dto.추천콜수}건이 있습니다.", "추천확인필요");
        }

        if (dto.오늘예약수 > 0)
        {
            return ("VIEW_RESERVATION", BuildReservationMessage(dto), "예약대기");
        }

        if (!알림정상)
        {
            return ("CHECK_NOTIFICATION", "푸시 알림 등록이 필요합니다.", "알림오류");
        }

        if (정산확인필요)
        {
            return ("VIEW_SETTLEMENT", "이번 달 이용료를 확인해 주세요.", "정산확인필요");
        }

        return ("REFRESH_RECOMMENDATIONS", "현재 바로 눌러볼 다음 행동이 없습니다.", "대기중");
    }

    private static List<기사홈할일항목ViewModel> BuildTasks(
        기사홈요약응답 dto,
        bool 운행중,
        bool 진행중운송있음,
        bool 알림정상,
        bool 정산확인필요)
    {
        var items = new List<기사홈할일항목ViewModel>();

        if (진행중운송있음)
        {
            items.Add(new 기사홈할일항목ViewModel
            {
                종류 = "진행중 운송",
                제목 = BuildTransportMessage(dto),
                설명 = dto.현재운송단계 ?? "현재 운송을 확인해 주세요.",
                이동경로 = DriverRoutes.CurrentTransport,
                우선순위 = 1
            });
        }

        if (운행중 && dto.추천콜수 > 0)
        {
            items.Add(new 기사홈할일항목ViewModel
            {
                종류 = "추천콜",
                제목 = $"받을 만한 추천콜 {dto.추천콜수}건이 있습니다.",
                설명 = dto.적합추천콜수 > 0
                    ? $"적합 추천 {dto.적합추천콜수}건 포함"
                    : "추천콜을 확인해 보세요.",
                이동경로 = DriverRoutes.Recommendations,
                우선순위 = 2
            });
        }

        if (dto.오늘예약수 > 0)
        {
            items.Add(new 기사홈할일항목ViewModel
            {
                종류 = "오늘 예약",
                제목 = BuildReservationMessage(dto),
                설명 = "예약을 확인하고 운행 가능 시간을 점검하세요.",
                이동경로 = DriverRoutes.Reservations,
                우선순위 = 3
            });
        }

        if (!알림정상)
        {
            items.Add(new 기사홈할일항목ViewModel
            {
                종류 = "알림 확인",
                제목 = "푸시 알림 등록이 필요합니다.",
                설명 = "배차추천 알림을 받으려면 알림 설정을 확인해야 합니다.",
                이동경로 = DriverRoutes.NotificationSettings,
                우선순위 = 4
            });
        }

        if (정산확인필요)
        {
            items.Add(new 기사홈할일항목ViewModel
            {
                종류 = "정산 확인",
                제목 = "이번 달 이용료를 확인해 주세요.",
                설명 = $"현재 이용료 {dto.이번달이용료:0}원 / 남은 금액 {dto.남은이용료:0}원",
                이동경로 = DriverRoutes.CurrentMonthSettlement,
                우선순위 = 5
            });
        }

        return items
            .OrderBy(x => x.우선순위)
            .ToList();
    }

    private static string BuildTransportMessage(기사홈요약응답 dto)
    {
        if (!string.IsNullOrWhiteSpace(dto.현재운송단계))
        {
            if (dto.현재운송단계.Contains("상차", StringComparison.OrdinalIgnoreCase))
            {
                return "상차지로 이동중입니다.";
            }

            if (dto.현재운송단계.Contains("하차", StringComparison.OrdinalIgnoreCase))
            {
                return "하차지로 이동중입니다.";
            }
        }

        return "현재 진행중인 운송이 있습니다.";
    }

    private static string BuildReservationMessage(기사홈요약응답 dto)
    {
        return dto.다음예약시각.HasValue
            ? $"{dto.다음예약시각.Value:HH:mm} 운행 예약이 있습니다."
            : "오늘 예약이 있습니다.";
    }

    private static bool LooksWorking(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return value.Contains("운행", StringComparison.OrdinalIgnoreCase)
            && !value.Contains("대기", StringComparison.OrdinalIgnoreCase)
            && !value.Contains("오프라인", StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class 기사홈할일항목ViewModel
{
    public string 종류 { get; init; } = string.Empty;
    public string 제목 { get; init; } = string.Empty;
    public string 설명 { get; init; } = string.Empty;
    public string 이동경로 { get; init; } = string.Empty;
    public int 우선순위 { get; init; }
}
