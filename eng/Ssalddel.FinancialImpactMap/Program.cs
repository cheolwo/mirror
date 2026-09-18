using System.Reflection;
using System.Text;
using System.Text.Json;
using Ssalddel.Contracts.Common.Finance;
using Ssalddel.Controllers.Shipper.Payment02;

var repositoryRoot = FindRepositoryRoot();
var assembly = typeof(화주결제Controller).Assembly;
var bindings = assembly.GetTypes()
    .OrderBy(type => type.FullName, StringComparer.Ordinal)
    .SelectMany(type => ReadBindings(type))
    .OrderBy(item => item.ComponentType, StringComparer.Ordinal)
    .ThenBy(item => item.MemberName, StringComparer.Ordinal)
    .ThenBy(item => item.ProfileStableId, StringComparer.Ordinal)
    .ToArray();

var profiles = 재무영향ProfileCatalog.GetAll()
    .OrderBy(item => item.StableId, StringComparer.Ordinal)
    .Select(profile => new FinancialProfileRow
    {
        StableId = profile.StableId,
        FinancialMeaningCode = profile.FinancialMeaningCode,
        ImpactKindCode = profile.ImpactKind.ToString(),
        RecognitionTimingCode = profile.RecognitionTiming.ToString(),
        AmountBasisCode = profile.AmountBasisCode,
        MappingRevision = profile.MappingRevision,
        ApprovalStatusCode = profile.ApprovalStatusCode,
        IsSimulationOnly = profile.IsSimulationOnly,
        OperationalPostingAllowed = profile.OperationalPostingAllowed,
        Accounts = profile.Accounts
            .Select(account => new FinancialProfileAccountRow
            {
                ManagementAccountStableId = account.ManagementAccountStableId,
                RoleCode = account.Role.ToString()
            })
            .ToArray()
    })
    .ToArray();

var unknownProfileBindings = bindings
    .Where(binding => 재무영향ProfileCatalog.Find(binding.ProfileStableId) is null)
    .ToArray();
var unknownAccounts = profiles
    .SelectMany(profile => profile.Accounts)
    .Where(account => 관리계정Catalog.Find(account.ManagementAccountStableId) is null)
    .ToArray();
if (unknownProfileBindings.Length > 0 || unknownAccounts.Length > 0)
{
    Console.Error.WriteLine("등록되지 않은 재무 프로필 또는 관리계정 참조가 있습니다.");
    return 1;
}

var document = new FinancialImpactMapDocument
{
    SchemaVersion = "ssalddel-financial-impact-code-map.v1",
    Boundary = "설명·검증용 관리 메타데이터이며 실제 전표·지급·세무 신고 권한이 아닙니다.",
    Accounts = 관리계정Catalog.GetAll()
        .OrderBy(item => item.StableId, StringComparer.Ordinal)
        .Select(account => new ManagementAccountRow
        {
            StableId = account.StableId,
            DisplayName = account.DisplayName,
            CategoryCode = account.CategoryCode,
            NormalBalanceSideCode = account.NormalBalanceSide.ToString(),
            Meaning = account.Meaning,
            IsActualAccountingAccountApproved = account.IsActualAccountingAccountApproved
        })
        .ToArray(),
    Profiles = profiles,
    Bindings = bindings
};

var jsonOptions = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    WriteIndented = true
};
var json = JsonSerializer.Serialize(document, jsonOptions) + Environment.NewLine;
var markdown = BuildMarkdown(document);
var jsonPath = Path.Combine(repositoryRoot, "docs", "AI", "generated", "financial-impact-code-map.json");
var markdownPath = Path.Combine(repositoryRoot, "docs", "AI", "generated", "financial-impact-code-map.md");

if (args.Contains("--write", StringComparer.Ordinal))
{
    Directory.CreateDirectory(Path.GetDirectoryName(jsonPath)!);
    File.WriteAllText(jsonPath, json, new UTF8Encoding(false));
    File.WriteAllText(markdownPath, markdown, new UTF8Encoding(false));
    Console.WriteLine("재무 영향 코드 지도를 갱신했습니다.");
    return 0;
}

if (!File.Exists(jsonPath)
    || !File.Exists(markdownPath)
    || File.ReadAllText(jsonPath, Encoding.UTF8) != json
    || File.ReadAllText(markdownPath, Encoding.UTF8) != markdown)
{
    Console.Error.WriteLine("재무 영향 코드 지도가 현재 소스와 다릅니다. --write로 갱신하세요.");
    return 2;
}

Console.WriteLine("재무 영향 코드 지도가 현재 소스와 일치합니다.");
return 0;

static IEnumerable<FinancialBindingRow> ReadBindings(Type type)
{
    foreach (var attribute in type.GetCustomAttributes<Ssalddel재무영향ProfileAttribute>(inherit: true))
    {
        yield return new FinancialBindingRow
        {
            ComponentType = type.FullName ?? type.Name,
            MemberName = string.Empty,
            ProfileStableId = attribute.ProfileStableId,
            ConditionCode = attribute.ConditionCode
        };
    }

    foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.DeclaredOnly))
    {
        foreach (var attribute in method.GetCustomAttributes<Ssalddel재무영향ProfileAttribute>(inherit: true))
        {
            yield return new FinancialBindingRow
            {
                ComponentType = type.FullName ?? type.Name,
                MemberName = method.Name,
                ProfileStableId = attribute.ProfileStableId,
                ConditionCode = attribute.ConditionCode
            };
        }
    }
}

static string FindRepositoryRoot()
{
    for (var current = new DirectoryInfo(Directory.GetCurrentDirectory()); current is not null; current = current.Parent)
    {
        if (File.Exists(Path.Combine(current.FullName, "AGENTS.md"))
            && Directory.Exists(Path.Combine(current.FullName, "docs", "AI")))
        {
            return current.FullName;
        }
    }

    throw new InvalidOperationException("저장소 루트를 찾을 수 없습니다.");
}

static string BuildMarkdown(FinancialImpactMapDocument document)
{
    var builder = new StringBuilder();
    builder.AppendLine("# 재무 영향 코드 지도");
    builder.AppendLine();
    builder.AppendLine("> `Ssalddel재무영향ProfileAttribute`와 관리계정·프로필 Catalog에서 자동 생성한다. 직접 수정하지 않는다.");
    builder.AppendLine();
    builder.AppendLine(document.Boundary);
    builder.AppendLine();
    builder.AppendLine("## 처리 구조");
    builder.AppendLine();
    builder.AppendLine("```text");
    builder.AppendLine("Command/API 특성 (설명·검사)");
    builder.AppendLine("  → 재무 영향 Profile Stable ID");
    builder.AppendLine("  → 승인된 매핑 판본과 관리계정 후보");
    builder.AppendLine("  → Outbox가 발행한 업무 Event");
    builder.AppendLine("  → 멱등 Projector");
    builder.AppendLine("  → 재무 사건·관리계정 전기·대사 예외 읽기 모델");
    builder.AppendLine("```");
    builder.AppendLine();
    builder.AppendLine("## 재무 영향 프로필");
    builder.AppendLine();
    foreach (var profile in document.Profiles)
    {
        builder.Append("- `").Append(profile.StableId).Append("` · ")
            .Append(profile.FinancialMeaningCode).Append(" · `")
            .Append(profile.ImpactKindCode).Append(" / ")
            .Append(profile.RecognitionTimingCode).Append("` · 매핑 `")
            .Append(profile.MappingRevision).Append("` · 운영 전표 쓰기 `")
            .Append(profile.OperationalPostingAllowed).AppendLine("`");
        foreach (var account in profile.Accounts)
        {
            builder.Append("  - ").Append(account.RoleCode).Append(": `")
                .Append(account.ManagementAccountStableId).AppendLine("`");
        }
    }

    builder.AppendLine();
    builder.AppendLine("## Command·API 결속");
    builder.AppendLine();
    foreach (var binding in document.Bindings)
    {
        builder.Append("- `").Append(binding.ComponentType);
        if (!string.IsNullOrWhiteSpace(binding.MemberName))
        {
            builder.Append('.').Append(binding.MemberName);
        }
        builder.Append("` → `").Append(binding.ProfileStableId).Append('`');
        if (!string.IsNullOrWhiteSpace(binding.ConditionCode))
        {
            builder.Append(" · 조건 `").Append(binding.ConditionCode).Append('`');
        }
        builder.AppendLine();
    }

    return builder.ToString();
}

internal sealed class FinancialImpactMapDocument
{
    public string SchemaVersion { get; set; } = string.Empty;
    public string Boundary { get; set; } = string.Empty;
    public ManagementAccountRow[] Accounts { get; set; } = [];
    public FinancialProfileRow[] Profiles { get; set; } = [];
    public FinancialBindingRow[] Bindings { get; set; } = [];
}

internal sealed class ManagementAccountRow
{
    public string StableId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string CategoryCode { get; set; } = string.Empty;
    public string NormalBalanceSideCode { get; set; } = string.Empty;
    public string Meaning { get; set; } = string.Empty;
    public bool IsActualAccountingAccountApproved { get; set; }
}

internal sealed class FinancialProfileRow
{
    public string StableId { get; set; } = string.Empty;
    public string FinancialMeaningCode { get; set; } = string.Empty;
    public string ImpactKindCode { get; set; } = string.Empty;
    public string RecognitionTimingCode { get; set; } = string.Empty;
    public string AmountBasisCode { get; set; } = string.Empty;
    public string MappingRevision { get; set; } = string.Empty;
    public string ApprovalStatusCode { get; set; } = string.Empty;
    public bool IsSimulationOnly { get; set; }
    public bool OperationalPostingAllowed { get; set; }
    public FinancialProfileAccountRow[] Accounts { get; set; } = [];
}

internal sealed class FinancialProfileAccountRow
{
    public string ManagementAccountStableId { get; set; } = string.Empty;
    public string RoleCode { get; set; } = string.Empty;
}

internal sealed class FinancialBindingRow
{
    public string ComponentType { get; set; } = string.Empty;
    public string MemberName { get; set; } = string.Empty;
    public string ProfileStableId { get; set; } = string.Empty;
    public string ConditionCode { get; set; } = string.Empty;
}
