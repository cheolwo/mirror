using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace RoleWorkspacePreview;

public sealed record 살뜰마트표본상품(string ProductName, int Quantity);
public sealed record 살뜰마트표본주문(string SampleOrderId, IReadOnlyList<살뜰마트표본상품> Items);

/// <summary>선택한 비공개 기록의 상품명·수량만 표본으로 읽습니다. 원문 식별자·주소·가격은 화면에 넘기지 않습니다.</summary>
public sealed class SsalddelMartSampleInput
{
    public bool HasData { get; }
    public string Notice { get; } = "살뜰마트 표본 입력을 선택하지 않았습니다.";
    public IReadOnlyList<살뜰마트표본주문> Orders { get; } = [];

    public SsalddelMartSampleInput(IConfiguration configuration, IWebHostEnvironment environment)
    {
        if (!environment.IsDevelopment()
            || !string.Equals(configuration["SsalddelExecution:Mode"], "Simulation", StringComparison.OrdinalIgnoreCase))
        {
            Notice = "살뜰마트 표본은 Development·Simulation 전용입니다.";
            return;
        }
        var path = configuration["SSALDDEL_MART_SAMPLE_INPUT"];
        if (string.IsNullOrWhiteSpace(path)) return;
        try
        {
            var directory = new DirectoryInfo(environment.ContentRootPath);
            while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, ".git")) && !File.Exists(Path.Combine(directory.FullName, ".git")))
                directory = directory.Parent;
            if (directory is null) throw new InvalidDataException();
            var localRoot = Path.Combine(directory.FullName, "artifacts", "local");
            var manifestPath = Resolve(localRoot, path, absolute: true);
            var manifest = JsonSerializer.Deserialize<살뜰마트표본입력Manifest>(ReadLimited(manifestPath, 32 * 1024), JsonOptions)
                ?? throw new InvalidDataException();
            if (manifest.SchemaVersion != 1 || !manifest.LocalVerificationOnly
                || manifest.DatasetSha256 is null || manifest.DatasetSha256.Length != 64 || !manifest.DatasetSha256.All(Uri.IsHexDigit)
                || manifest.RecordIds is null || manifest.RecordIds.Length is < 1 or > 10
                || manifest.RecordIds.Any(string.IsNullOrWhiteSpace)
                || manifest.RecordIds.Distinct(StringComparer.Ordinal).Count() != manifest.RecordIds.Length)
                throw new InvalidDataException();
            var datasetPath = Resolve(localRoot, manifest.DatasetRelativePath, absolute: false);
            var bytes = ReadLimited(datasetPath, 20 * 1024 * 1024);
            if (!Convert.ToHexString(SHA256.HashData(bytes)).Equals(manifest.DatasetSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException();
            // 해시를 확인한 바로 그 바이트를 해석해 파일 교체 사이의 재읽기를 피합니다.
            var dataset = JsonSerializer.Deserialize<HistoricalDeliveryDataset>(bytes, JsonOptions) ?? throw new InvalidDataException();
            if (dataset.Records is null || dataset.Records.Any(row => row is null || string.IsNullOrWhiteSpace(row.RecordId)
                || row.MenuItems is null || row.MenuItems.Any(item => item is null))) throw new InvalidDataException();
            dataset.Validate();
            var records = dataset.Records.ToDictionary(x => x.RecordId, StringComparer.Ordinal);
            var selected = new List<살뜰마트표본주문>();
            foreach (var (id, index) in manifest.RecordIds.Select((id, index) => (id, index)))
            {
                if (!records.TryGetValue(id, out var row) || row.MenuItems.Length == 0) throw new InvalidDataException();
                // 봉투 비용은 상품 재고가 아닙니다. 다른 품목은 임의 추정·자동 제외하지 않습니다.
                var goods = row.MenuItems.Where(item => item.Name != "봉투값")
                    .Select(item => new 살뜰마트표본상품(item.Name, item.Quantity ?? throw new InvalidDataException())).ToArray();
                selected.Add(new($"sample-mart-order-{index + 1:D2}", goods));
            }
            ValidateOrders(selected);
            Orders = selected.AsReadOnly();
            HasData = true;
            Notice = "기록 상품명·수량을 사용한 살뜰마트 검증 표본입니다. 선반·SKU·재고·기사는 합성이며 실제 주문·배차가 아닙니다.";
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or ArgumentException or NotSupportedException)
        {
            Notice = "살뜰마트 표본 입력을 읽지 못했습니다. 선택 기록·자료 해시·파일 경계를 확인해 주세요.";
        }
    }

    // 같은 assembly에 결속한 합성 시험 전용. 호스트의 DI는 위 환경 검증 생성자만 사용합니다.
    internal SsalddelMartSampleInput(IReadOnlyList<살뜰마트표본주문> orders)
    {
        ValidateOrders(orders);
        Orders = orders.Select(order => order with { Items = Array.AsReadOnly(order.Items.ToArray()) }).ToArray();
        HasData = true;
        Notice = "합성 상품을 사용한 살뜰마트 검증 표본입니다.";
    }

    private static void ValidateOrders(IReadOnlyList<살뜰마트표본주문> orders)
    {
        if (orders.Count is < 1 or > 10 || orders.Any(order => order is null || string.IsNullOrWhiteSpace(order.SampleOrderId)
            || order.Items is null || order.Items.Count is < 1 or > 50)
            || orders.Select(order => order.SampleOrderId).Distinct(StringComparer.Ordinal).Count() != orders.Count)
            throw new InvalidDataException();
        foreach (var order in orders)
            if (order.Items.Any(item => item is null || string.IsNullOrWhiteSpace(item.ProductName) || item.ProductName.Length > 200
                || item.Quantity is < 1 or > 1000)) throw new InvalidDataException();
    }

    private static byte[] ReadLimited(string path, int maximumBytes)
    {
        using var stream = File.OpenRead(path);
        if (stream.Length is < 1 || stream.Length > maximumBytes) throw new InvalidDataException();
        var bytes = new byte[checked((int)stream.Length)];
        stream.ReadExactly(bytes);
        if (stream.ReadByte() != -1) throw new InvalidDataException();
        return bytes;
    }

    private static string Resolve(string root, string configured, bool absolute)
    {
        if (string.IsNullOrWhiteSpace(configured) || Path.IsPathFullyQualified(configured) != absolute
            || (!absolute && (configured.Contains(':') || configured.Contains('\\')))) throw new InvalidDataException();
        root = Path.GetFullPath(root);
        var full = Path.GetFullPath(absolute ? configured : Path.Combine(root, configured));
        if (!full.StartsWith(Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException();
        // root보다 위의 junction도 검증해 허용 디렉터리처럼 보이는 외부 경로를 거부합니다.
        for (var ancestor = new DirectoryInfo(root); ancestor is not null; ancestor = ancestor.Parent)
            if ((ancestor.Attributes & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException();
        var current = root;
        foreach (var segment in Path.GetRelativePath(root, full).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
        {
            if (segment is ".." or "." or "" || segment.Contains(':')) throw new InvalidDataException();
            current = Path.Combine(current, segment);
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException();
        }
        if (!File.Exists(full)) throw new InvalidDataException();
        return full;
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private sealed record 살뜰마트표본입력Manifest
    {
        public int SchemaVersion { get; init; }
        public bool LocalVerificationOnly { get; init; }
        public string DatasetRelativePath { get; init; } = "";
        public string DatasetSha256 { get; init; } = "";
        public string[] RecordIds { get; init; } = [];
    }
}
