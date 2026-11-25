using Ssalddel.BatchMonitor.Windows;

// Claude 작업본의 기사 무념·경 시간·등급(r124~r126)과 생산·소비 흐름(r134) 규칙 점검.
// 실행: dotnet run --project tools/Ssalddel.BatchMonitor.Windows.Claude.Checks
var failures = 0;
var checks = 0;
void Check(bool condition, string name)
{
    checks++;
    if (condition) Console.WriteLine($"통과  {name}");
    else { failures++; Console.WriteLine($"실패  {name}"); }
}

var t0 = new DateTime(2026, 1, 1, 11, 30, 0);

// 1. 시작 프로필: 기사마다 고정된 시작 등급과 유지 가능 시간
var book = RiderMindBook.InMemory();
var a = book.Get("management-driver-jungnang-a");
var b = book.Get("management-driver-jungnang-b");
var g = book.Get("management-driver-gwangjin-a");
Check(a.GradeName == "신사" && a.CapacityMinutes == 6 && a.RemainingMinutes == 6, "중랑구 A는 신사·유지 가능 6분으로 시작");
Check(b.GradeName == "문사" && b.CapacityMinutes == 8, "중랑구 B는 문사·8분으로 시작");
Check(g.GradeName == "학사" && g.CapacityMinutes == 10, "광진구 A는 학사·10분으로 시작");

// 2. 업무 시간만큼 경 시간이 줄고, 남은 경 시간 안이면 무념 유지
var work = new RiderWork("o1", t0.AddSeconds(30), t0.AddSeconds(30 + 144), 3_600, true);
var live = RiderMindEvaluator.Evaluate(a.DriverId, 6, false, [work], t0.AddMinutes(10));
Check(Math.Abs(live.UsedMinutes - 2.4) < 1e-9 && Math.Abs(live.RemainingMinutes - 3.6) < 1e-9, "2분 24초 업무 뒤 경 3분 36초 남음");
Check(!live.Broken && live.BrokenAt is null, "남은 경 시간 안의 업무는 무념 유지");
Check(live.LinkedIncome == 3_600 && Math.Abs(live.IncomePerMaintainedHour!.Value - 90_000) < 1e-6,
    "무념 유지 업무의 보수 3,600원 전부 연결 · 경 시간당 90,000원");

// 3. 진행 중인 업무도 지금 시각까지 계산
var running = RiderMindEvaluator.Evaluate(a.DriverId, 6, false,
    [new RiderWork("o1", t0.AddSeconds(30), null, 0, false)], t0.AddSeconds(90));
Check(running.Working && Math.Abs(running.UsedMinutes - 1) < 1e-9, "운행 중에는 지금까지 1분 사용으로 계산");

// 4. 남은 경 시간을 넘기는 순간 무념이 깨지고 연결 소득은 유지 비율만큼
var broken = RiderMindEvaluator.Evaluate(a.DriverId, 1.2, false, [work], t0.AddMinutes(10));
Check(broken.Broken && broken.BrokeThisRound, "경 1분 12초로 2분 24초 업무를 맡으면 무념 깨짐");
Check(broken.BrokenAt == t0.AddSeconds(30).AddMinutes(1.2), "깨진 시각은 출발 뒤 1분 12초");
Check(broken.RemainingMinutes == 0 && Math.Abs(broken.MaintainedMinutes - 1.2) < 1e-9, "남은 경 0 · 유지 1분 12초");
Check(broken.LinkedIncome == 1_800, "보수 3,600원 중 유지 구간 절반인 1,800원만 경 구간에 연결");

// 5. r125 예상 업무 + 여유 1분 판단
var task = RiderMindRules.EstimateTaskMinutes(t0, t0.AddSeconds(30 + 64), t0.AddMinutes(2), 0.26);
Check(Math.Abs(task - 146.0 / 60) < 0.01, "준비 대기를 포함한 예상 업무 약 2분 26초");
Check(RiderMindRules.HasEnough(3.6, task) && !RiderMindRules.HasEnough(2.5, task), "여유 1분을 더해 충분·부족 판정");

// 6. 회차 반영과 휴식·점검: 유효 근무면 유지 가능 시간 +1분
book.Commit([live]);
Check(Math.Abs(a.RemainingMinutes - 3.6) < 1e-9 && a.ShiftDeliveries == 1, "회차 정산 뒤 근무 기록 반영");
var rest = book.Rest(a.DriverId);
Check(rest.Valid && a.CapacityMinutes == 7 && a.RemainingMinutes == 7 && a.ValidShifts == 1, "유효 근무 마감 → 다음 근무 7분");
Check(!rest.Promoted && a.QualifyingShifts == 0, "8분 미만 유지 근무는 승급 집계 없음");

// 7. 무념이 깨진 근무는 유효 기록 없이 회복
book.Commit([RiderMindEvaluator.Evaluate(a.DriverId, a.RemainingMinutes, false,
    [new RiderWork("o2", t0, t0.AddMinutes(8), 3_600, true)], t0.AddMinutes(9))]);
Check(a.Broken, "7분 경으로 8분 업무 → 기록상 무념 깨짐");
var restBroken = book.Rest(a.DriverId);
Check(!restBroken.Valid && !a.Broken && a.CapacityMinutes == 7 && a.RemainingMinutes == 7, "깨진 근무는 성장 없이 휴식으로 무념 회복");

// 8. 두 근무에서 다음 등급 기준 이상을 유지하면 승급 (신사 → 문사 8분)
var c = book.Get("management-driver-nowon-a");
c.CapacityMinutes = 9; c.RemainingMinutes = 9;
for (var shift = 0; shift < 2; shift++)
{
    book.Commit([RiderMindEvaluator.Evaluate(c.DriverId, c.RemainingMinutes, false,
        [new RiderWork($"s{shift}", t0, t0.AddMinutes(8.5), 3_600, true)], t0.AddMinutes(9))]);
    rest = book.Rest(c.DriverId);
}
Check(rest.Promoted && c.GradeName == "문사" && c.QualifyingShifts == 0, "8분 이상 유효 근무 두 번 → 문사 승급");

// 9. r134 생산·소비 흐름
var balanced = ProductionFlow.Evaluate(new("중랑구", 2, 2, 2));
Check(balanced.Bottleneck == FlowBottleneck.Balanced && balanced.Limit == 2, "주문2·준비2·배송2 → 균형");
var delivery = ProductionFlow.Evaluate(new("중랑구", 2, 2, 1));
Check(delivery.Bottleneck == FlowBottleneck.Delivery && delivery.Limit == 1, "배송 여력 1명 → 배송 병목, 완료 상한 1");
var prep = ProductionFlow.Evaluate(new("중랑구", 3, 2, 3));
Check(prep.Bottleneck == FlowBottleneck.Preparation && prep.Limit == 2, "준비 2건 → 준비 병목");
var demand = ProductionFlow.Evaluate(new("중랑구", 1, 2, 2));
Check(demand.Bottleneck == FlowBottleneck.Demand && demand.Limit == 1, "주문 1건 < 여력 → 수요 연결 여지");
var summary = ProductionFlow.Summarize([new("중랑구", 1, 2, 2), new("동대문구", 2, 2, 1)]);
Check(summary.Limit == 2 && summary.Orders == 3 && summary.Bottleneck == FlowBottleneck.Delivery && summary.Focus.Contains("동대문구"),
    "여러 지역 합계와 먼저 볼 지역");

// 10. 저장과 다시 읽기
var directory = Path.Combine(Path.GetTempPath(), "claude-rider-mind-check-" + Guid.NewGuid().ToString("N"));
var file = Path.Combine(directory, "rider-mind-v1.json");
var saved = RiderMindBook.Load(file);
saved.Get("management-driver-dongdaemun-a");
saved.Commit([RiderMindEvaluator.Evaluate("management-driver-dongdaemun-a", 8, false, [work], t0.AddMinutes(10))]);
var reloaded = RiderMindBook.Load(file).Get("management-driver-dongdaemun-a");
Check(Math.Abs(reloaded.RemainingMinutes - 5.6) < 1e-9 && reloaded.ShiftDeliveries == 1 && reloaded.GradeName == "문사",
    "저장 파일을 다시 열어도 같은 기사 상태");
Directory.Delete(directory, true);

Console.WriteLine($"\n{checks - failures}/{checks} 통과");
return failures == 0 ? 0 : 1;
