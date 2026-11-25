using System.IO;
using System.Text.Json;

namespace Ssalddel.BatchMonitor.Windows;

// 기사 무념·경 시간·등급 (PLAN-SYSTEM-REGIONAL-OPERATOR-SIMULATION r124·r125·r126 첫 시제품).
// 이 파일은 WPF와 Sagajeong.Core 타입에 기대지 않는 순수 규칙이다. 나중에 Core로 옮길 수 있게 분리했다.

internal static class RiderGrades
{
    internal static readonly string[] Names = ["신사", "문사", "학사", "양사", "경사", "수사", "정사", "대사", "대종사"];

    // 등급에 들어서기 위한 유효 근무의 무념 유지 시간(가상 분). 신사 6·문사 8·학사 10은 r125 첫 시험 기준이고
    // 양사부터는 기획 미정이라 2분 간격의 임시값이다.
    internal static readonly double[] EntryMinutes = [6, 8, 10, 12, 14, 16, 18, 20, 22];

    // r126 등급별 오토바이 색: 빨강 → 주황 → 노랑 → 초록 → 파랑 → 남색 → 보라 → 흰색 → 검정
    internal static readonly string[] Colors =
        ["#D64541", "#E67E22", "#E2B007", "#2E9E5B", "#2F6FD1", "#25327A", "#7D3FB8", "#FFFFFF", "#1B1B1B"];

    internal const int ShiftsForPromotion = 2;

    internal static string Name(int grade) => Names[Math.Clamp(grade, 0, Names.Length - 1)];
    internal static string Color(int grade) => Colors[Math.Clamp(grade, 0, Colors.Length - 1)];
    internal static bool IsProvisional(int grade) => grade >= 3;
}

internal static class RiderMindRules
{
    // r125: 다음 주문 예상 수행시간에 여유 1분을 더해 남은 경 시간과 비교한다.
    internal const double SafetyMarginMinutes = 1;

    // r125: 유효 근무 기록 한 건마다 다음 근무의 유지 가능 시간이 가상 1분 늘어나는 안.
    internal const double CapacityGrowthMinutes = 1;

    // 흐름 패널의 배송 여력 판단용 대표 업무 시간. SeoulEast 표본에서 출발→전달이 약 2분 24초여서 2분 30초로 둔다.
    internal const double TypicalTaskMinutes = 2.5;

    // Core의 FoodManagementSession과 같은 이동 가정(시속 18km, 최소 30초, 출발 대기 30초, 픽업 30초).
    private const double SpeedKilometersPerHour = 18;
    private static readonly TimeSpan DepartureDelay = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan PickupDelay = TimeSpan.FromSeconds(30);

    internal static double TravelMinutes(double kilometers) =>
        Math.Max(30, kilometers / SpeedKilometersPerHour * 3600) / 60;

    // 출발부터 전달까지(매장 도착 뒤 음식 준비를 기다리는 시간 포함)를 업무 시간으로 본다.
    internal static double EstimateTaskMinutes(DateTime now, DateTime arrivalAt, DateTime readyAt, double deliveryKilometers)
    {
        var departure = now + DepartureDelay;
        var pickupAt = arrivalAt + PickupDelay > readyAt ? arrivalAt + PickupDelay : readyAt;
        return Math.Max(0, (pickupAt - departure).TotalMinutes) + TravelMinutes(deliveryKilometers);
    }

    internal static bool HasEnough(double remainingMinutes, double taskMinutes) =>
        remainingMinutes >= taskMinutes + SafetyMarginMinutes;

    internal static bool CanTakeTypicalOrder(RiderLive live) =>
        !live.Broken && HasEnough(live.RemainingMinutes, TypicalTaskMinutes);
}

internal sealed record RiderProfile(int StartGrade, string Tarot);

internal static class RiderProfiles
{
    // r126: 기사마다 이름·모습·시작 등급이 고정된 한 묶음이다. 첫 시제품은 신사·문사·학사 시작 인물을 섞는다.
    // 개인 타로 슬롯의 장착 카드는 기획 미정이라 경영 타로 4장(여제·전차·정의·절제)을 시안용으로 고정 배정한다.
    private static readonly Dictionary<string, RiderProfile> Profiles = new(StringComparer.Ordinal)
    {
        ["management-driver-a"] = new(0, "전차"),
        ["management-driver-b"] = new(1, "절제"),
        ["management-driver-jungnang-a"] = new(0, "전차"),
        ["management-driver-jungnang-b"] = new(1, "절제"),
        ["management-driver-dongdaemun-a"] = new(1, "정의"),
        ["management-driver-dongdaemun-b"] = new(0, "여제"),
        ["management-driver-gwangjin-a"] = new(2, "전차"),
        ["management-driver-gwangjin-b"] = new(0, "정의"),
        ["management-driver-nowon-a"] = new(0, "절제"),
        ["management-driver-nowon-b"] = new(1, "여제"),
    };

    internal static RiderProfile For(string driverId) =>
        Profiles.TryGetValue(driverId, out var profile) ? profile : new(0, "전차");
}

// 저장되는 기사 한 명의 무념 상태. System.Text.Json으로 읽고 쓰기 위해 공개 속성을 둔다.
internal sealed class RiderMind
{
    public string DriverId { get; set; } = "";
    public int Grade { get; set; }
    public double CapacityMinutes { get; set; }
    public double RemainingMinutes { get; set; }
    public bool Broken { get; set; }
    public double ShiftWorkMinutes { get; set; }
    public double ShiftMaintainedMinutes { get; set; }
    public int ShiftDeliveries { get; set; }
    public int ValidShifts { get; set; }
    public int QualifyingShifts { get; set; }
    public double BestShiftMinutes { get; set; }
    public string Tarot { get; set; } = "전차";

    internal string GradeName => RiderGrades.Name(Grade);
    internal bool HasNextGrade => Grade + 1 < RiderGrades.Names.Length;
    internal string? NextGradeName => HasNextGrade ? RiderGrades.Name(Grade + 1) : null;
    internal double? NextGradeMinutes => HasNextGrade ? RiderGrades.EntryMinutes[Grade + 1] : null;

    internal static RiderMind Create(string driverId)
    {
        var profile = RiderProfiles.For(driverId);
        var capacity = RiderGrades.EntryMinutes[profile.StartGrade];
        return new()
        {
            DriverId = driverId,
            Grade = profile.StartGrade,
            CapacityMinutes = capacity,
            RemainingMinutes = capacity,
            Tarot = profile.Tarot,
        };
    }
}

internal sealed record RiderWork(string OrderId, DateTime Start, DateTime? End, int Pay, bool Settled);

internal sealed record RiderOrderMind(string OrderId, double WorkMinutes, double MaintainedMinutes, int Pay,
    int LinkedIncome, bool Settled);

internal sealed record RiderLive(string DriverId, double StartRemainingMinutes, bool StartBroken, double UsedMinutes,
    double RemainingMinutes, bool Broken, DateTime? BrokenAt, bool Working, IReadOnlyList<RiderOrderMind> Orders)
{
    internal double MaintainedMinutes => Orders.Sum(order => order.MaintainedMinutes);
    internal int Pay => Orders.Sum(order => order.Pay);
    internal int LinkedIncome => Orders.Sum(order => order.LinkedIncome);
    internal int SettledDeliveries => Orders.Count(order => order.Settled);
    internal bool BrokeThisRound => Broken && !StartBroken;

    // r133: 경 유지 구간에 연결된 가상 순소득 ÷ 그 구간의 시간. 비용 범위가 아직 없어 순소득은 가상 기사 보수다.
    internal double? IncomePerMaintainedHour => MaintainedMinutes > 0 ? LinkedIncome / (MaintainedMinutes / 60) : null;
}

internal static class RiderMindEvaluator
{
    // 회차 시작의 남은 경 시간에서 실제 업무 시간(출발→전달)을 뺀다. 남은 경 시간을 넘어서는 순간 무념이 깨진다(r124).
    internal static RiderLive Evaluate(string driverId, double startRemaining, bool startBroken,
        IEnumerable<RiderWork> works, DateTime now)
    {
        var budget = startBroken ? 0 : Math.Max(0, startRemaining);
        var used = 0.0;
        DateTime? brokenAt = null;
        var working = false;
        var orders = new List<RiderOrderMind>();
        foreach (var work in works.Where(work => work.Start <= now).OrderBy(work => work.Start))
        {
            var end = work.End is { } finished && finished <= now ? finished : now;
            if (work.End is null || work.End > now) working = true;
            var minutes = Math.Max(0, (end - work.Start).TotalMinutes);
            var maintained = Math.Clamp(budget - used, 0, minutes);
            if (!startBroken && brokenAt is null && used + minutes > budget)
            {
                brokenAt = work.Start.AddMinutes(Math.Max(0, budget - used));
            }

            used += minutes;
            var pay = work.Settled ? work.Pay : 0;
            var linked = minutes > 0 ? (int)Math.Round(pay * maintained / minutes) : pay;
            orders.Add(new(work.OrderId, minutes, maintained, pay, linked, work.Settled));
        }

        return new(driverId, startRemaining, startBroken, used, Math.Max(0, startRemaining - used),
            startBroken || brokenAt is not null, brokenAt, working, orders.AsReadOnly());
    }

    internal static RiderLive Idle(RiderMind mind) =>
        new(mind.DriverId, mind.RemainingMinutes, mind.Broken, 0, mind.RemainingMinutes, mind.Broken, null, false,
            Array.Empty<RiderOrderMind>());
}

internal sealed record RiderRestResult(string Message, bool Valid, bool Promoted);

internal sealed class RiderMindBook
{
    private const int SchemaVersion = 1;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly string? path;
    private readonly Dictionary<string, RiderMind> riders = new(StringComparer.Ordinal);

    private RiderMindBook(string? path) => this.path = path;

    internal string? Warning { get; private set; }

    internal static RiderMindBook InMemory() => new(null);

    internal static RiderMindBook Load(string? file = null)
    {
        var directory = Environment.GetEnvironmentVariable("SSALDDEL_BATCH_MONITOR_CLAUDE_DIRECTORY");
        if (string.IsNullOrWhiteSpace(directory))
        {
            directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Ssalddel", "BatchMonitor.Claude");
        }

        var book = new RiderMindBook(Path.GetFullPath(file ?? Path.Combine(directory, "rider-mind-v1.json")));
        try
        {
            if (File.Exists(book.path))
            {
                var stored = JsonSerializer.Deserialize<BookFile>(File.ReadAllText(book.path!), JsonOptions);
                if (stored is { SchemaVersion: SchemaVersion, Riders: { } list })
                {
                    foreach (var rider in list.Where(rider => !string.IsNullOrWhiteSpace(rider.DriverId)))
                    {
                        book.riders[rider.DriverId] = rider;
                    }
                }
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            book.Warning = "기사 무념 기록을 읽지 못했습니다. 처음 상태로 시작합니다.";
        }

        return book;
    }

    internal RiderMind Get(string driverId)
    {
        if (!riders.TryGetValue(driverId, out var mind))
        {
            mind = RiderMind.Create(driverId);
            riders[driverId] = mind;
        }

        return mind;
    }

    // 정산이 끝난 회차의 결과를 근무 기록에 더한다. 같은 회차를 두 번 넣지 않는 것은 호출하는 쪽이 보장한다.
    internal void Commit(IEnumerable<RiderLive> results)
    {
        foreach (var result in results)
        {
            var mind = Get(result.DriverId);
            mind.RemainingMinutes = result.RemainingMinutes;
            if (result.BrokeThisRound) mind.Broken = true;
            mind.ShiftWorkMinutes += result.UsedMinutes;
            mind.ShiftMaintainedMinutes += result.MaintainedMinutes;
            mind.ShiftDeliveries += result.SettledDeliveries;
        }

        Save();
    }

    // r125: 휴식·자기 점검으로 근무를 마감한다. 점검 뒤 시작해 배달 1건 이상을 정산까지 마치고 무념을 유지한 채
    // 끝낸 근무만 유효 기록이 된다. 유효 기록마다 유지 가능 시간 +1분, 다음 등급 기준 이상을 두 근무에서 쌓으면 승급.
    internal RiderRestResult Rest(string driverId)
    {
        var mind = Get(driverId);
        var valid = false;
        var promoted = false;
        string message;
        if (mind.ShiftWorkMinutes <= 0 && mind.ShiftDeliveries == 0 && !mind.Broken)
        {
            message = "휴식과 점검을 마쳤습니다. 이번 근무에는 배달 기록이 없습니다.";
        }
        else if (mind.Broken)
        {
            message = "무념이 깨진 근무라 유효 기록 없이 마감했습니다. 휴식과 점검으로 무념을 회복했습니다.";
        }
        else if (mind.ShiftDeliveries == 0)
        {
            message = "정산까지 마친 배달이 없어 유효 기록 없이 마감했습니다.";
        }
        else
        {
            valid = true;
            mind.ValidShifts++;
            mind.BestShiftMinutes = Math.Max(mind.BestShiftMinutes, mind.ShiftMaintainedMinutes);
            mind.CapacityMinutes += RiderMindRules.CapacityGrowthMinutes;
            message = $"유효 근무 기록 · 무념 유지 {FormatMinutes(mind.ShiftMaintainedMinutes)} · " +
                $"다음 근무 유지 가능 시간 {FormatMinutes(mind.CapacityMinutes)}";
            if (mind.NextGradeMinutes is { } required && mind.ShiftMaintainedMinutes >= required)
            {
                mind.QualifyingShifts++;
                if (mind.QualifyingShifts >= RiderGrades.ShiftsForPromotion)
                {
                    mind.Grade++;
                    mind.QualifyingShifts = 0;
                    promoted = true;
                    message += $" · {mind.GradeName}로 승급했습니다";
                }
            }
        }

        mind.Broken = false;
        mind.RemainingMinutes = mind.CapacityMinutes;
        mind.ShiftWorkMinutes = 0;
        mind.ShiftMaintainedMinutes = 0;
        mind.ShiftDeliveries = 0;
        Save();
        return new(message, valid, promoted);
    }

    internal static string FormatMinutes(double minutes)
    {
        var seconds = (int)Math.Round(Math.Max(0, minutes) * 60);
        return seconds % 60 == 0 ? $"{seconds / 60}분" : $"{seconds / 60}분 {seconds % 60}초";
    }

    private void Save()
    {
        if (path is null) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var temporary = path + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(
                new BookFile(SchemaVersion, riders.Values.OrderBy(rider => rider.DriverId, StringComparer.Ordinal).ToList()),
                JsonOptions));
            File.Move(temporary, path, overwrite: true);
            Warning = null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            Warning = "기사 무념 기록을 저장하지 못했습니다.";
        }
    }

    private sealed record BookFile(int SchemaVersion, List<RiderMind> Riders);
}
