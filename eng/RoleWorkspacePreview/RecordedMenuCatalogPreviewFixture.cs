using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Ssalddel.Ui.Common.Areas.App.Components.Food;

namespace RoleWorkspacePreview;

/// <summary>개인 자료를 읽기 전용 화면에만 공급합니다. 운영 메뉴 공급자·원장·네트워크를 호출하지 않습니다.</summary>
public sealed class RecordedMenuCatalogPreviewFixture
{
    private readonly string? _localRoot;
    private readonly IReadOnlyDictionary<string, RecordedMenuCatalogAsset> _assets = new Dictionary<string, RecordedMenuCatalogAsset>();
    public RecordedMenuCatalogDataset? Dataset { get; }
    public IReadOnlyList<OrdererRecordedMenuObservation> Items { get; } = [];
    public bool HasData => Dataset is not null;
    public string Notice { get; }
    public const string ImageRoutePrefix = "/_local/orderer-menu-images/";

    public RecordedMenuCatalogPreviewFixture(IConfiguration configuration, IWebHostEnvironment environment)
    {
        // 실제 운영 설정이 이 전용 호스트에 전달돼도 개인 검토 자료를 운영 자료처럼 제공하지 않습니다.
        var mode = configuration["SsalddelExecution:Mode"];
        if (mode is not null && !string.Equals(mode, "Simulation", StringComparison.OrdinalIgnoreCase))
        {
            Notice = "이 화면에서는 개인 검토용 자료만 볼 수 있습니다.";
            return;
        }
        var path = configuration["SSALDDEL_ORDERER_MENU_CATALOG"];
        if (string.IsNullOrWhiteSpace(path))
        {
            Notice = "로컬 메뉴 자료를 선택하지 않았습니다.";
            return;
        }
        try
        {
            var directory = new DirectoryInfo(environment.ContentRootPath);
            while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, ".git")) && !File.Exists(Path.Combine(directory.FullName, ".git")))
                directory = directory.Parent;
            if (directory is null) throw new InvalidDataException();
            _localRoot = Path.GetFullPath(Path.Combine(directory.FullName, "artifacts", "local"));
            var full = ResolveLocalFile(_localRoot, path, requireAbsolute: true, requireExists: true);
            var dataset = RecordedMenuCatalogDataset.Read(full);
            foreach (var asset in dataset.Assets)
            {
                ResolveLocalFile(_localRoot, asset.RelativePath, requireAbsolute: false, requireExists: false);
                if (!HasMatchingExtension(asset)) throw new InvalidDataException();
            }
            var branches = dataset.Branches.ToDictionary(x => x.BranchId, StringComparer.Ordinal);
            var orders = dataset.Orders.ToDictionary(x => x.RecordId, StringComparer.Ordinal);
            Items = dataset.Menus.SelectMany(menu => menu.Observations.Select((observation, index) =>
            {
                var order = orders[observation.RecordId];
                var photo = menu.ImageCandidates.FirstOrDefault();
                return new OrdererRecordedMenuObservation
                {
                    Id = menu.MenuId + ":" + index, BranchId = menu.BranchId, BranchName = branches[menu.BranchId].DisplayName,
                    MenuName = menu.RawMenu, ServiceDate = order.ServiceDate, Quantity = observation.Quantity,
                    UnitPriceKrw = observation.UnitPriceKrw, DisplayedItemAmountKrw = observation.DisplayedItemAmountKrw,
                    OrderAmountKrw = order.FoodOrderAmountKrw, Options = observation.Options, SourceRecordId = order.RecordId,
                    ImageUrl = photo is null ? null : ImageRoutePrefix + Uri.EscapeDataString(photo.AssetId),
                    ImageMenuName = photo?.OfficialMenuName,
                    ImageMenuMatchConfirmed = photo?.MatchLevel == "official_same_menu",
                    ImageOptionsMatchConfirmed = photo?.OptionsMatch == true,
                    PublicUseApproved = false, ImageSourceUrl = photo?.SourcePageUrl
                };
            })).OrderBy(x => x.BranchName, StringComparer.Ordinal).ThenBy(x => x.MenuName, StringComparer.Ordinal).ThenBy(x => x.ServiceDate).ToArray();
            _assets = dataset.Assets.ToDictionary(x => x.AssetId, StringComparer.Ordinal);
            Dataset = dataset;
            Notice = "과거 주문의 메뉴·관측 금액과 공식 메뉴 참고 사진을 대조하는 개인 검토입니다. 현재 판매가·실제 지점 사진·공개 이용 허락을 확정하지 않습니다.";
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or System.Text.Json.JsonException or ArgumentException or NotSupportedException)
        {
            Notice = "로컬 메뉴 자료를 읽지 못했습니다. 선택한 자료를 확인해 주세요.";
        }
    }

    /// <summary>allowlist 안의 같은 파일 바이트만 제공합니다. 실패 시 다른 파일이나 외부 URL로 대체하지 않습니다.</summary>
    public bool TryReadImage(string assetId, out byte[] bytes, out string mimeType)
    {
        bytes = [];
        mimeType = "";
        if (!HasData || _localRoot is null || !_assets.TryGetValue(assetId, out var asset)) return false;
        try
        {
            var path = ResolveLocalFile(_localRoot, asset.RelativePath, requireAbsolute: false, requireExists: true);
            var file = new FileInfo(path);
            if (file.Length != asset.Bytes || file.Length is < 1 or > 20 * 1024 * 1024) return false;
            var content = File.ReadAllBytes(path);
            if (content.LongLength != asset.Bytes || !string.Equals(Convert.ToHexString(SHA256.HashData(content)), asset.Sha256, StringComparison.OrdinalIgnoreCase)
                || !HasMatchingSignature(content, asset.MimeType)) return false;
            bytes = content;
            mimeType = asset.MimeType;
            return true;
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

    private static string ResolveLocalFile(string root, string configured, bool requireAbsolute, bool requireExists)
    {
        if (string.IsNullOrWhiteSpace(configured) || Path.IsPathFullyQualified(configured) != requireAbsolute
            || (!requireAbsolute && (configured.Contains(':') || configured.Contains('\\'))))
            throw new InvalidDataException();
        var full = Path.GetFullPath(requireAbsolute ? configured : Path.Combine(root, configured));
        var prefix = Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar;
        if (!full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException();
        var relative = Path.GetRelativePath(root, full);
        var current = root;
        // junction/symlink 경유로 artifacts/local 밖의 개인 파일에 닿지 않게 각 경로를 검사합니다.
        if ((File.GetAttributes(Path.GetDirectoryName(root)!) & FileAttributes.ReparsePoint) != 0
            || (File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException();
        foreach (var segment in relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
        {
            if (segment is ".." or "." or "" || segment.Contains(':')) throw new InvalidDataException();
            current = Path.Combine(current, segment);
            if (File.Exists(current) || Directory.Exists(current))
            {
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException();
            }
            else if (requireExists) throw new FileNotFoundException();
        }
        if (requireExists && !File.Exists(full)) throw new FileNotFoundException();
        return full;
    }

    private static bool HasMatchingExtension(RecordedMenuCatalogAsset asset)
        => (asset.MimeType, Path.GetExtension(asset.RelativePath).ToLowerInvariant()) switch
        {
            ("image/jpeg", ".jpg" or ".jpeg") or ("image/png", ".png") or ("image/webp", ".webp") or ("image/gif", ".gif") => true,
            _ => false
        };

    private static bool HasMatchingSignature(byte[] content, string mime)
        => mime switch
        {
            "image/jpeg" => content.Length >= 3 && content[0] == 0xff && content[1] == 0xd8 && content[2] == 0xff,
            "image/png" => content.AsSpan().StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
            "image/gif" => content.AsSpan().StartsWith("GIF87a"u8) || content.AsSpan().StartsWith("GIF89a"u8),
            "image/webp" => content.Length >= 12 && content.AsSpan(0, 4).SequenceEqual("RIFF"u8) && content.AsSpan(8, 4).SequenceEqual("WEBP"u8),
            _ => false
        };
}
