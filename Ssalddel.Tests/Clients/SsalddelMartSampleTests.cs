using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using RoleWorkspacePreview;

namespace Ssalddel.Tests.Clients;

public sealed class SsalddelMartSampleTests
{
    [Fact]
    public async Task 실제피킹포장UseCase를통과하고_모의인계는운영완료와분리한다()
    {
        await using var runtime = Runtime();
        await runtime.InitializeAsync();
        Assert.True(runtime.Initialized, runtime.Error);
        Assert.Equal(2, runtime.Orders.Count);
        var order = runtime.Orders[0];
        Assert.Equal("피킹 대기", order.Stage);
        Assert.False(await runtime.PackOrderAsync(order.SampleOrderId, true, true));
        Assert.False(await runtime.ConfirmMockHandoffAsync(order.SampleOrderId, true));
        Assert.True(await runtime.IssueSlipAsync(order.SampleOrderId));
        foreach (var line in order.Lines)
        {
            Assert.True(await runtime.StartPickingAsync(order.SampleOrderId, line.LineId), runtime.Error);
            Assert.True(await runtime.CompletePickingAsync(order.SampleOrderId, line.LineId, line.RackCode, true, true), runtime.Error);
        }
        Assert.Equal("포장 대기", runtime.Orders[0].Stage);
        Assert.True(await runtime.PackOrderAsync(order.SampleOrderId, true, true), runtime.Error);
        Assert.Equal("픽업 대기", runtime.Orders[0].Stage);
        Assert.False(await runtime.ConfirmMockHandoffAsync(order.SampleOrderId, false));
        Assert.False(await runtime.ConfirmMockHandoffAsync(order.SampleOrderId, true));
        var slip = runtime.Orders[0].Slip!;
        Assert.True(await runtime.PickupByQrAsync(order.SampleOrderId, slip.QrCode, Guid.NewGuid().ToString("N"),
            slip.Revision, SsalddelMartSampleRuntime.SampleDriverId), runtime.Error);
        Assert.Equal("표본 픽업 완료", runtime.Orders[0].Stage);
        Assert.Equal(1, runtime.PickupCount);
        Assert.True(runtime.Orders[0].MockHandedOff);
        Assert.Equal("피킹 대기", runtime.Orders[1].Stage);
        Assert.Equal(6, runtime.AuditCount);
        Assert.Equal(4, runtime.EventCount);
    }

    [Fact]
    public async Task 잘못된적재대와미확인수량과다른주문의품목은상태를변경하지않는다()
    {
        await using var runtime = Runtime(); await runtime.InitializeAsync();
        Assert.True(runtime.Initialized, runtime.Error);
        var order = runtime.Orders[0]; var line = order.Lines[0];
        Assert.False(await runtime.CompletePickingAsync(order.SampleOrderId, line.LineId, line.RackCode, true, true));
        Assert.True(await runtime.StartPickingAsync(order.SampleOrderId, line.LineId));
        Assert.False(await runtime.CompletePickingAsync(order.SampleOrderId, line.LineId, "WRONG", true, true));
        Assert.False(await runtime.CompletePickingAsync(order.SampleOrderId, line.LineId, line.RackCode, true, false));
        Assert.False(await runtime.CompletePickingAsync(runtime.Orders[1].SampleOrderId, line.LineId, line.RackCode, true, true));
        Assert.Equal("진행중", runtime.Orders[0].Lines[0].PickingStatus);
        Assert.Equal(1, runtime.AuditCount); Assert.Equal(0, runtime.EventCount);
        Assert.False(await runtime.PackOrderAsync(order.SampleOrderId, true, true));
        Assert.All(runtime.Orders.SelectMany(row => row.Lines), item => Assert.False(item.Packed));
    }

    [Fact]
    public async Task 시작완료재요청은완료사실을중복생성하지않고_포장재확인뒤QR재요청도멱등이다()
    {
        await using var runtime = Runtime(); await runtime.InitializeAsync();
        Assert.True(runtime.Initialized, runtime.Error);
        var order = runtime.Orders[0];
        Assert.True(await runtime.IssueSlipAsync(order.SampleOrderId));
        foreach (var line in order.Lines)
        {
            Assert.True(await runtime.StartPickingAsync(order.SampleOrderId, line.LineId));
            Assert.True(await runtime.StartPickingAsync(order.SampleOrderId, line.LineId));
            var requests = await Task.WhenAll(
                runtime.CompletePickingAsync(order.SampleOrderId, line.LineId, line.RackCode, true, true),
                runtime.CompletePickingAsync(order.SampleOrderId, line.LineId, line.RackCode, true, true));
            Assert.All(requests, value => Assert.True(value));
        }
        Assert.True(await runtime.PackOrderAsync(order.SampleOrderId, true, true), runtime.Error);
        var completionEventCount = runtime.EventCount;
        Assert.Equal(6, runtime.AuditCount);
        Assert.Equal(4, completionEventCount);
        Assert.True(await runtime.PackOrderAsync(order.SampleOrderId, true, true), runtime.Error);
        Assert.Equal(6, runtime.AuditCount);
        // 전용 포장 UseCase 시험이 완료 사실과 배차 재확인 Event의 종류를 검증합니다.
        // 이 표본에서는 동일 포장 재요청의 품목별 재확인만 추가되고 감사는 늘지 않아야 합니다.
        var dispatchRecheckEventCount = runtime.EventCount - completionEventCount;
        Assert.Equal(order.Lines.Count, dispatchRecheckEventCount);
        Assert.Equal(2, dispatchRecheckEventCount);
        var beforePickupCounts = (runtime.AuditCount, runtime.EventCount);
        var slip = runtime.Orders[0].Slip!;
        var requestId = Guid.NewGuid().ToString("N");
        Assert.True(await runtime.PickupByQrAsync(order.SampleOrderId, slip.QrCode, requestId, slip.Revision,
            SsalddelMartSampleRuntime.SampleDriverId));
        Assert.True(await runtime.PickupByQrAsync(order.SampleOrderId, slip.QrCode, requestId, slip.Revision,
            SsalddelMartSampleRuntime.SampleDriverId));
        Assert.Equal(1, runtime.PickupCount);
        Assert.Equal(beforePickupCounts, (runtime.AuditCount, runtime.EventCount));
    }

    [Fact]
    public async Task 초기화는멱등하고리셋과Dispose가메모리자료를비운다()
    {
        var runtime = Runtime(); await runtime.InitializeAsync();
        Assert.True(runtime.Initialized, runtime.Error);
        var order = runtime.Orders[0]; var line = order.Lines[0];
        Assert.True(await runtime.StartPickingAsync(order.SampleOrderId, line.LineId));
        await runtime.InitializeAsync();
        Assert.Equal("진행중", runtime.Orders[0].Lines[0].PickingStatus);
        Assert.Equal(1, runtime.AuditCount);
        await runtime.ResetAsync();
        Assert.False(runtime.Initialized); Assert.Empty(runtime.Orders); Assert.Equal(0, runtime.AuditCount); Assert.Equal(0, runtime.EventCount);
        await runtime.InitializeAsync();
        Assert.True(runtime.Initialized, runtime.Error);
        Assert.All(runtime.Orders, row => Assert.Equal("피킹 대기", row.Stage));
        await runtime.DisposeAsync();
        Assert.False(runtime.Initialized); Assert.Empty(runtime.Orders); Assert.Equal(0, runtime.AuditCount); Assert.Equal(0, runtime.EventCount);
        await runtime.DisposeAsync();
    }

    [Fact]
    public async Task 입력없음은빈안내이고명령이나초기화가예시를자동생성하지않는다()
    {
        using var files = new InputFiles();
        var input = files.Load(missingManifest: true);
        Assert.False(input.HasData);
        await using var runtime = new SsalddelMartSampleRuntime(input);
        await runtime.InitializeAsync();
        Assert.False(runtime.Initialized); Assert.Empty(runtime.Orders);
        Assert.False(await runtime.StartPickingAsync("unknown", "unknown"));
        Assert.Equal(0, runtime.EventCount);
    }

    [Fact]
    public void 선택기록의상품명수량만읽고봉투값과사업자원문ID주소금액은제외한다()
    {
        using var files = new InputFiles();
        var before = files.DatasetHash;
        var input = files.Load();
        Assert.True(input.HasData, input.Notice);
        var order = Assert.Single(input.Orders);
        Assert.Equal("sample-mart-order-01", order.SampleOrderId);
        var item = Assert.Single(order.Items);
        Assert.Equal("합성 감자", item.ProductName); Assert.Equal(2, item.Quantity);
        var json = JsonSerializer.Serialize(input.Orders);
        Assert.DoesNotContain("봉투값", json);
        Assert.DoesNotContain("replay-private-source", json);
        Assert.DoesNotContain("private-branch", json);
        Assert.DoesNotContain("비공개 출처 업체", json);
        Assert.DoesNotContain("비공개 주소", json);
        Assert.DoesNotContain("Amount", json, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(before, files.DatasetHash);
    }

    [Theory]
    [InlineData("Operational")]
    [InlineData("UnknownMode")]
    [InlineData("MissingMode")]
    [InlineData("Production")]
    [InlineData("HashMismatch")]
    [InlineData("UnknownRecord")]
    [InlineData("NullQuantity")]
    [InlineData("ZeroQuantity")]
    [InlineData("NullItems")]
    [InlineData("BagFeeOnly")]
    [InlineData("TooLongName")]
    [InlineData("DuplicateRecordSelection")]
    [InlineData("OutsideManifest")]
    [InlineData("TraversalDataset")]
    public void 환경입력해시관계가틀리면fallback없이거절한다(string scenario)
    {
        using var files = new InputFiles(scenario);
        var before = files.DatasetHash;
        var input = files.Load(scenario);
        Assert.False(input.HasData);
        Assert.Empty(input.Orders);
        Assert.Equal(before, files.DatasetHash);
        Assert.DoesNotContain(files.Root, input.Notice, StringComparison.OrdinalIgnoreCase);
    }

    private static SsalddelMartSampleRuntime Runtime() => new(new SsalddelMartSampleInput([
        new("sample-mart-order-01", [new("합성 감자", 2), new("합성 두부", 1)]),
        new("sample-mart-order-02", [new("합성 우유", 3)])]));

    private sealed class InputFiles : IDisposable
    {
        private readonly string _parent;
        private readonly string _datasetPath;
        private readonly string _manifestPath;
        private readonly string _local;
        public string Root { get; }
        public string DatasetHash => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(_datasetPath)));
        public InputFiles(string? scenario = null)
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, ".git")) && !File.Exists(Path.Combine(directory.FullName, ".git"))) directory = directory.Parent;
            if (directory is null) throw new InvalidOperationException("테스트 저장소를 찾을 수 없습니다.");
            _parent = Path.GetFullPath(Path.Combine(directory.FullName, "artifacts", "local", "ssalddel-mart-r52", "fixture-tests"));
            Root = Path.Combine(_parent, Guid.NewGuid().ToString("N"));
            _local = Path.Combine(Root, "artifacts", "local"); Directory.CreateDirectory(_local);
            File.WriteAllText(Path.Combine(Root, ".git"), "synthetic test boundary");
            _datasetPath = Path.Combine(_local, "dataset.json");
            _manifestPath = Path.Combine(_local, "manifest.json");
            var goods = new HistoricalDeliveryMenuItem { Name = "합성 감자", Quantity = 2, DisplayedItemAmountKrw = 9876 };
            goods = scenario switch
            {
                "NullQuantity" => goods with { Quantity = null },
                "ZeroQuantity" => goods with { Quantity = 0 },
                "TooLongName" => goods with { Name = new string('가', 201) },
                _ => goods
            };
            var row = new HistoricalDeliveryRecord
            {
                RecordId = "replay-private-source", BranchId = "private-branch", ServiceDate = new(2026, 10, 9), Sequence = 1,
                FoodOrderAmountKrw = 9976, SourceRefs = [new() { RelativePath = "private/source.sqlite3" }],
                MenuItems = [goods, new() { Name = "봉투값", Quantity = 1, DisplayedItemAmountKrw = 100 }]
            };
            if (scenario == "NullItems") row = row with { MenuItems = null! };
            if (scenario == "BagFeeOnly") row = row with { MenuItems = [row.MenuItems[1]] };
            var dataset = new HistoricalDeliveryDataset
            {
                LocalVerificationOnly = true, CoverageStart = row.ServiceDate, CoverageEnd = row.ServiceDate,
                Branches = [new() { BranchId = "private-branch", DisplayName = "비공개 출처 업체", PublicPickupAddress = "비공개 주소" }], Records = [row]
            };
            File.WriteAllText(_datasetPath, JsonSerializer.Serialize(dataset, JsonOptions));
            var selected = scenario == "UnknownRecord" ? new[] { "replay-not-found" }
                : scenario == "DuplicateRecordSelection" ? new[] { row.RecordId, row.RecordId } : new[] { row.RecordId };
            File.WriteAllText(_manifestPath, JsonSerializer.Serialize(new
            {
                schemaVersion = 1, localVerificationOnly = true,
                datasetRelativePath = scenario == "TraversalDataset" ? "../outside.json" : "dataset.json",
                datasetSha256 = scenario == "HashMismatch" ? new string('a', 64) : DatasetHash,
                recordIds = selected
            }, JsonOptions));
        }
        public SsalddelMartSampleInput Load(string? scenario = null, bool missingManifest = false)
        {
            var values = new Dictionary<string, string?>
            {
                ["SsalddelExecution:Mode"] = scenario switch { "Operational" => "Operational", "UnknownMode" => "Unknown", "MissingMode" => null, _ => "Simulation" },
                ["SSALDDEL_MART_SAMPLE_INPUT"] = missingManifest ? null : scenario == "OutsideManifest" ? Path.Combine(Root, "outside.json") : _manifestPath
            };
            return new(new ConfigurationBuilder().AddInMemoryCollection(values).Build(), new TestEnvironment
            { ContentRootPath = Root, EnvironmentName = scenario == "Production" ? "Production" : "Development" });
        }
        public void Dispose()
        {
            var full = Path.GetFullPath(Root);
            if (!full.StartsWith(_parent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(Path.GetDirectoryName(full), _parent, StringComparison.OrdinalIgnoreCase)
                || !Guid.TryParseExact(Path.GetFileName(full), "N", out _)
                || (File.GetAttributes(_parent) & FileAttributes.ReparsePoint) != 0
                || (File.GetAttributes(full) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("전용 표본 시험 디렉터리 밖은 정리하지 않습니다.");
            Directory.Delete(full, true);
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private sealed class TestEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "SsalddelMartSampleTests";
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = "";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
