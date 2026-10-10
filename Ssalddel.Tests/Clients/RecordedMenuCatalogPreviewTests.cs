using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using RoleWorkspacePreview;

namespace Ssalddel.Tests.Clients;

public sealed class RecordedMenuCatalogPreviewTests
{
    [Theory]
    [InlineData("Operational")]
    [InlineData("Registration")]
    [InlineData("PublicDisplay")]
    [InlineData("PublicUse")]
    [InlineData("DuplicateBranch")]
    [InlineData("UnknownOrderBranch")]
    [InlineData("UnknownMenuBranch")]
    [InlineData("UnknownObservationOrder")]
    [InlineData("DifferentObservationBranch")]
    [InlineData("UnknownAsset")]
    [InlineData("BrandReferenceOnly")]
    [InlineData("CandidatePublicUse")]
    [InlineData("CandidateOptionsMatch")]
    [InlineData("CandidateRightsApproved")]
    [InlineData("HistoricalVersionConfirmed")]
    [InlineData("NegativeItemAmount")]
    [InlineData("NegativeUnitPrice")]
    [InlineData("ZeroQuantity")]
    [InlineData("InvalidMime")]
    [InlineData("InvalidHash")]
    [InlineData("NullOptions")]
    public void 검토경계또는연결이틀린자료를정상목록으로승격하지않는다(string scenario)
    {
        var data = Dataset();
        var menu = data.Menus[0];
        var observation = menu.Observations[0];
        var photo = menu.ImageCandidates[0];
        data = scenario switch
        {
            "Operational" => data with { ExecutionModeCode = "Operational" },
            "Registration" => data with { OperationalRegistration = true },
            "PublicDisplay" => data with { PublicDisplayApproved = true },
            "PublicUse" => data with { PublicUseApproved = true },
            "DuplicateBranch" => data with { Branches = [.. data.Branches, data.Branches[0]] },
            "UnknownOrderBranch" => data with { Orders = [data.Orders[0] with { BranchId = "unknown" }] },
            "UnknownMenuBranch" => data with { Menus = [menu with { BranchId = "unknown" }] },
            "UnknownObservationOrder" => data with { Menus = [menu with { Observations = [observation with { RecordId = "unknown" }] }] },
            "DifferentObservationBranch" => data with { Branches = [.. data.Branches, new() { BranchId = "branch-b", DisplayName = "다른 지점" }], Menus = [menu with { BranchId = "branch-b" }] },
            "UnknownAsset" => data with { Menus = [menu with { ImageCandidates = [photo with { AssetId = "unknown" }] }] },
            "BrandReferenceOnly" => data with { Menus = [menu with { ImageCandidates = [photo with { MatchLevel = "brand_reference_only" }] }] },
            "CandidatePublicUse" => data with { Menus = [menu with { ImageCandidates = [photo with { PublicUseApproved = true }] }] },
            "CandidateOptionsMatch" => data with { Menus = [menu with { ImageCandidates = [photo with { OptionsMatch = true }] }] },
            "CandidateRightsApproved" => data with { Menus = [menu with { ImageCandidates = [photo with { RightsStatus = "approved" }] }] },
            "HistoricalVersionConfirmed" => data with { Menus = [menu with { ImageCandidates = [photo with { HistoricalProductVersionVerified = true }] }] },
            "NegativeItemAmount" => data with { Menus = [menu with { Observations = [observation with { DisplayedItemAmountKrw = -1 }] }] },
            "NegativeUnitPrice" => data with { Menus = [menu with { Observations = [observation with { UnitPriceKrw = -1 }] }] },
            "ZeroQuantity" => data with { Menus = [menu with { Observations = [observation with { Quantity = 0 }] }] },
            "InvalidMime" => data with { Assets = [data.Assets[0] with { MimeType = "image/svg+xml" }] },
            "InvalidHash" => data with { Assets = [data.Assets[0] with { Sha256 = "not-a-hash" }] },
            "NullOptions" => data with { Menus = [menu with { Observations = [observation with { Options = [null!] }] }] },
            _ => throw new ArgumentOutOfRangeException(nameof(scenario))
        };
        Assert.Throws<InvalidDataException>(data.Validate);
    }

    [Fact]
    public void 단가와항목금액및주문합계는별도로투영하고실제판매메뉴로변환하지않는다()
    {
        using var scope = new LocalFixtureScope();
        var fixture = scope.Load(Dataset());
        Assert.True(fixture.HasData);
        var item = Assert.Single(fixture.Items);
        Assert.Null(item.UnitPriceKrw);
        Assert.Equal(2, item.Quantity);
        Assert.Equal(14000m, item.DisplayedItemAmountKrw);
        Assert.Equal(22000m, item.OrderAmountKrw);
        Assert.False(item.PublicUseApproved);
        Assert.False(item.ImageMenuMatchConfirmed);
        Assert.False(item.ImageOptionsMatchConfirmed);
        Assert.Equal("/_local/orderer-menu-images/asset-test", item.ImageUrl);
        Assert.True(fixture.TryReadImage("asset-test", out var bytes, out var mime));
        Assert.Equal(Png, bytes);
        Assert.Equal("image/png", mime);
        Assert.False(fixture.TryReadImage("not-registered", out _, out _));
    }

    [Theory]
    [InlineData("Traversal")]
    [InlineData("AbsoluteAsset")]
    [InlineData("BackslashAsset")]
    [InlineData("AlternateDataStream")]
    [InlineData("MimeExtension")]
    [InlineData("OutsideCatalog")]
    public void 개인로컬경로외부파일또는위장확장자를공급하지않는다(string scenario)
    {
        using var scope = new LocalFixtureScope();
        var data = Dataset();
        var asset = data.Assets[0];
        data = scenario switch
        {
            "Traversal" => data with { Assets = [asset with { RelativePath = "../../outside.png" }] },
            "AbsoluteAsset" => data with { Assets = [asset with { RelativePath = scope.AssetPath }] },
            "BackslashAsset" => data with { Assets = [asset with { RelativePath = "assets\\test.png" }] },
            "AlternateDataStream" => data with { Assets = [asset with { RelativePath = "assets/test.png:private" }] },
            "MimeExtension" => data with { Assets = [asset with { RelativePath = "assets/test.html" }] },
            _ => data
        };
        var outside = scenario == "OutsideCatalog" ? Path.Combine(scope.Root, "outside.json") : null;
        var fixture = scope.Load(data, catalogPath: outside);
        Assert.False(fixture.HasData);
        Assert.Empty(fixture.Items);
        Assert.False(fixture.TryReadImage("asset-test", out _, out _));
    }

    [Fact]
    public void 파일이바뀌면로드한허용목록의해시와비교해사진제공을중단한다()
    {
        using var scope = new LocalFixtureScope();
        var fixture = scope.Load(Dataset());
        Assert.True(fixture.TryReadImage("asset-test", out _, out _));
        var changed = Png.ToArray();
        changed[^1] ^= 1;
        File.WriteAllBytes(scope.AssetPath, changed);
        Assert.False(fixture.TryReadImage("asset-test", out _, out _));
        File.WriteAllBytes(scope.AssetPath, Png[..^1]);
        Assert.False(fixture.TryReadImage("asset-test", out _, out _));
        File.Delete(scope.AssetPath);
        Assert.False(fixture.TryReadImage("asset-test", out _, out _));
    }

    [Fact]
    public void 해시와확장자가맞아도HTML을사진으로제공하지않는다()
    {
        using var scope = new LocalFixtureScope();
        var html = Encoding.UTF8.GetBytes("<html><script>alert('not an image')</script></html>");
        var data = Dataset();
        data = data with { Assets = [data.Assets[0] with { Sha256 = Sha(html), Bytes = html.Length }] };
        var fixture = scope.Load(data, html);
        Assert.True(fixture.HasData);
        Assert.False(fixture.TryReadImage("asset-test", out _, out _));
    }

    [Theory]
    [InlineData("Operational")]
    [InlineData("UnknownMode")]
    public void 운영또는알수없는모드에서개인메뉴와사진을제공하지않는다(string mode)
    {
        using var scope = new LocalFixtureScope();
        var fixture = scope.Load(Dataset(), mode: mode);
        Assert.False(fixture.HasData);
        Assert.Empty(fixture.Items);
        Assert.False(fixture.TryReadImage("asset-test", out _, out _));
    }

    [Fact]
    public void 자료선택이없으면기본탐색이나예시대신빈안내만보인다()
    {
        using var scope = new LocalFixtureScope();
        var fixture = new RecordedMenuCatalogPreviewFixture(new ConfigurationBuilder().Build(), scope.Environment);
        Assert.False(fixture.HasData);
        Assert.Empty(fixture.Items);
        Assert.Contains("선택하지 않았습니다", fixture.Notice);
        Assert.False(fixture.TryReadImage("asset-test", out _, out _));
    }

    private static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jqSIAAAAASUVORK5CYII=");
    private static string Sha(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    private static RecordedMenuCatalogDataset Dataset() => new()
    {
        SchemaVersion = 1, LocalVerificationOnly = true, ExecutionModeCode = "Simulation", SourceDatasetSha256 = new('a', 64),
        Sources = [new() { Kind = "SyntheticTest", Sha256 = new('b', 64) }],
        Branches = [new() { BranchId = "branch-a", DisplayName = "합성 검토 지점" }],
        Orders = [new() { RecordId = "order-a", BranchId = "branch-a", ServiceDate = new(2026, 10, 1), Sequence = 1, FoodOrderAmountKrw = 22000 }],
        Menus = [new() { MenuId = "menu-a", BranchId = "branch-a", RawMenu = "검토 메뉴", SourceKind = "structured",
            Observations = [new() { RecordId = "order-a", MenuItemIndex = 0, Quantity = 2, DisplayedItemAmountKrw = 14000, Options = ["검토 옵션"] }],
            ImageCandidates = [new() { CandidateId = "candidate-a", AssetId = "asset-test", OfficialMenuName = "검토 메뉴", OfficialSourceStatus = "verified", MatchLevel = "same_menu_name_candidate" }] }],
        Assets = [new() { AssetId = "asset-test", RelativePath = "assets/test.png", Sha256 = Sha(Png), Bytes = Png.Length, MimeType = "image/png", Width = 1, Height = 1 }]
    };

    private sealed class LocalFixtureScope : IDisposable
    {
        private readonly string _fixtureParent;
        public string Root { get; }
        private string Local { get; }
        public string AssetPath => Path.Combine(Local, "assets", "test.png");
        public IWebHostEnvironment Environment { get; }
        public LocalFixtureScope()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, ".git")) && !File.Exists(Path.Combine(directory.FullName, ".git")))
                directory = directory.Parent;
            if (directory is null) throw new InvalidOperationException("테스트 저장소 위치를 찾지 못했습니다.");
            _fixtureParent = Path.GetFullPath(Path.Combine(directory.FullName, "artifacts", "local", "orderer-menu-catalog-r55", "fixture-tests"));
            Root = Path.GetFullPath(Path.Combine(_fixtureParent, Guid.NewGuid().ToString("N")));
            Local = Path.Combine(Root, "artifacts", "local");
            Directory.CreateDirectory(Path.Combine(Local, "assets"));
            File.WriteAllText(Path.Combine(Root, ".git"), "synthetic fixture boundary");
            Environment = new TestEnvironment { ContentRootPath = Root };
        }
        public RecordedMenuCatalogPreviewFixture Load(RecordedMenuCatalogDataset data, byte[]? content = null, string? mode = null, string? catalogPath = null)
        {
            File.WriteAllBytes(AssetPath, content ?? Png);
            var jsonPath = catalogPath ?? Path.Combine(Local, "catalog.json");
            File.WriteAllText(jsonPath, JsonSerializer.Serialize(data, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
            var values = new Dictionary<string, string?> { ["SSALDDEL_ORDERER_MENU_CATALOG"] = jsonPath };
            if (mode is not null) values["SsalddelExecution:Mode"] = mode;
            return new(new ConfigurationBuilder().AddInMemoryCollection(values).Build(), Environment);
        }
        public void Dispose()
        {
            var full = Path.GetFullPath(Root);
            if (!full.StartsWith(_fixtureParent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(Path.GetDirectoryName(full), _fixtureParent, StringComparison.OrdinalIgnoreCase)
                || !Guid.TryParseExact(Path.GetFileName(full), "N", out _)
                || (File.GetAttributes(_fixtureParent) & FileAttributes.ReparsePoint) != 0
                || (File.GetAttributes(full) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("테스트 전용 임시 경로 밖을 정리하지 않습니다.");
            Directory.Delete(full, true);
        }
    }

    private sealed class TestEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "RecordedMenuCatalogTests";
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = "";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
