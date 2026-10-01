using System.Text;
using Hongdal.Contracts.Common.Community;

namespace Hongdal.Ui.Common.Areas.App.Services;

public enum CommunityDecorationTarget
{
    HomeNavigatorTheme,
    TraditionalMarketTheme,
    Bagua,
    DiagramNode,
    BaguaTransitionMotion
}

public sealed record BaguaTransitionMotionManifest(
    string Version,
    string RendererProfile,
    string ReviewStatus,
    string LicenseLabel,
    string PreviewSymbol,
    string AccentColor,
    double DurationScale,
    bool UseRoleAccent,
    string CharacterLabel,
    string TrailLabel,
    IReadOnlyList<string>? AppliesToSlotPatterns = null,
    int CoveredPerspectiveSlotCount = 125)
{
    public const int TotalPerspectiveSlotCount = 125;

    public IReadOnlyList<string> SlotPatterns
        => AppliesToSlotPatterns is { Count: > 0 } ? AppliesToSlotPatterns : ["*"];
}

public sealed record HomeNavigatorThemeSlot(
    string Key,
    string Title,
    string Color,
    string? ImageUrl = null,
    string? AltText = null)
{
    public bool HasImage => !string.IsNullOrWhiteSpace(ImageUrl);
}

public sealed record HomeNavigatorThemeManifest(
    string Version,
    string RendererProfile,
    string ReviewStatus,
    string LicenseLabel,
    string PreviewBackground,
    HomeNavigatorThemeSlot OuterUpaya,
    HomeNavigatorThemeSlot OuterPrajna,
    HomeNavigatorThemeSlot InnerCommunity,
    HomeNavigatorThemeSlot InnerStore,
    HomeNavigatorThemeSlot CenterGen,
    HomeNavigatorThemeSlot Frame,
    HomeNavigatorThemeSlot Labels,
    HomeNavigatorThemeSlot ClosedHandle)
{
    public IReadOnlyList<HomeNavigatorThemeSlot> Slots =>
    [
        OuterUpaya,
        OuterPrajna,
        InnerCommunity,
        InnerStore,
        CenterGen,
        Frame,
        Labels,
        ClosedHandle
    ];

    public string AccentColor => ClosedHandle.Color;
}

public sealed record CommunityDecorationAsset(
    string Key,
    string PackKey,
    string Title,
    string CreatorName,
    string Summary,
    CommunityDecorationTarget Target,
    string PreviewSymbol,
    string AccentColor,
    노드스티커이미지Response? NodeSticker = null,
    bool IsCustom = false);

public sealed record CommunityDecorationProduct(
    string Key,
    string PackKey,
    string Title,
    string CreatorName,
    string Summary,
    CommunityDecorationTarget Target,
    decimal PriceAmount,
    string CurrencyCode,
    IReadOnlyList<CommunityDecorationAsset> Assets,
    HomeNavigatorThemeManifest? HomeTheme = null,
    BaguaTransitionMotionManifest? BaguaMotion = null,
    bool IsCustom = false,
    ScriptureDecorationSource? ScriptureSource = null,
    TraditionalMarketThemeManifest? TraditionalMarketTheme = null)
{
    public bool IsFree => PriceAmount <= 0;

    public bool IsHomeTheme => Target == CommunityDecorationTarget.HomeNavigatorTheme && HomeTheme is not null;

    public bool IsBaguaMotion
        => Target == CommunityDecorationTarget.BaguaTransitionMotion && BaguaMotion is not null;

    public bool IsTraditionalMarketTheme
        => Target == CommunityDecorationTarget.TraditionalMarketTheme
           && TraditionalMarketTheme is not null;
}

public sealed class PlatformCommunityDecorationStateService
{
    private const string BasicBaguaAssetKey = "bagua-basic-blue";
    public const string DefaultHomeThemePackKey = "home-theme-hongdal-default-v1";
    public const string DefaultBaguaMotionPackKey = "bagua-motion-basic-runner-v1";
    private readonly HashSet<string> ownedPackKeys = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> sessionPurchasedPackKeys = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> serverOwnedPackKeys = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> activeBaguaMotionPackByScope = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> activeTraditionalMarketThemePackByScope = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<CommunityDecorationAsset> customAssets = [];
    private readonly List<CommunityDecorationProduct> products;

    public PlatformCommunityDecorationStateService()
    {
        products = BuildProducts();
        foreach (var product in products.Where(item => item.IsFree))
        {
            ownedPackKeys.Add(product.PackKey);
        }

        activeBaguaMotionPackByScope["*"] = DefaultBaguaMotionPackKey;
    }

    public event Action? Changed;

    public IReadOnlyList<CommunityDecorationProduct> Products => products;

    public IReadOnlyList<CommunityDecorationAsset> CustomAssets => customAssets;

    public bool IsBaguaDecorationEnabled { get; private set; } = true;

    public bool IsNodeDecorationEnabled { get; private set; } = true;

    public bool IsHomeThemeEnabled { get; private set; } = true;

    public bool IsBaguaMotionEnabled { get; private set; } = true;

    public bool IsTraditionalMarketThemeEnabled { get; private set; } = true;

    public IReadOnlyDictionary<string, string> ActiveTraditionalMarketThemePackByScope
        => activeTraditionalMarketThemePackByScope;

    public string? ActiveBaguaAssetKey { get; private set; } = BasicBaguaAssetKey;

    public string? ActiveNodeAssetKey { get; private set; }

    public string ActiveHomeThemePackKey { get; private set; } = DefaultHomeThemePackKey;

    public string ActiveBaguaMotionPackKey
        => activeBaguaMotionPackByScope.TryGetValue("*", out var packKey)
            ? packKey
            : DefaultBaguaMotionPackKey;

    public CommunityDecorationAsset? ActiveBaguaAsset
        => FindAsset(ActiveBaguaAssetKey);

    public CommunityDecorationAsset? ActiveNodeAsset
        => FindAsset(ActiveNodeAssetKey);

    public 노드스티커이미지Response? ActiveNodeSticker
        => ActiveNodeAsset?.NodeSticker;

    public HomeNavigatorThemeManifest ActiveHomeTheme
        => products.FirstOrDefault(item => item.IsHomeTheme &&
               string.Equals(item.PackKey, ActiveHomeThemePackKey, StringComparison.OrdinalIgnoreCase))?.HomeTheme
           ?? products.First(item => item.IsHomeTheme &&
               string.Equals(item.PackKey, DefaultHomeThemePackKey, StringComparison.OrdinalIgnoreCase)).HomeTheme!;

    public BaguaTransitionMotionManifest ActiveBaguaMotion
        => FindOwnedBaguaMotionProduct(ActiveBaguaMotionPackKey)?.BaguaMotion
           ?? DefaultBaguaMotion;

    public string BaguaSymbol
        => IsBaguaDecorationEnabled ? ActiveBaguaAsset?.PreviewSymbol ?? "☵" : "◎";

    public string BaguaAccentColor
        => IsBaguaDecorationEnabled ? ActiveBaguaAsset?.AccentColor ?? "#2563eb" : "#64748b";

    public string HomeThemeAccentColor
        => IsHomeThemeEnabled ? ActiveHomeTheme.AccentColor : "#64748b";

    public bool IsProductOwned(CommunityDecorationProduct product)
        => product.IsFree
           || ownedPackKeys.Contains(product.PackKey)
           || sessionPurchasedPackKeys.Contains(product.PackKey)
           || serverOwnedPackKeys.Contains(product.PackKey);

    public bool IsAssetActive(CommunityDecorationAsset asset)
        => asset.Target switch
        {
            CommunityDecorationTarget.Bagua => IsBaguaDecorationEnabled &&
                string.Equals(ActiveBaguaAssetKey, asset.Key, StringComparison.OrdinalIgnoreCase),
            CommunityDecorationTarget.DiagramNode => IsNodeDecorationEnabled &&
                string.Equals(ActiveNodeAssetKey, asset.Key, StringComparison.OrdinalIgnoreCase),
            _ => false
        };

    public bool IsProductActive(CommunityDecorationProduct product)
    {
        if (!IsProductOwned(product))
        {
            return false;
        }

        return product.IsHomeTheme
            ? IsHomeThemeEnabled && string.Equals(ActiveHomeThemePackKey, product.PackKey, StringComparison.OrdinalIgnoreCase)
            : product.IsBaguaMotion
                ? IsBaguaMotionEnabled && product.BaguaMotion!.SlotPatterns.All(pattern =>
                    activeBaguaMotionPackByScope.TryGetValue(pattern, out var packKey) &&
                    string.Equals(packKey, product.PackKey, StringComparison.OrdinalIgnoreCase))
                : product.IsTraditionalMarketTheme
                    ? IsTraditionalMarketThemeEnabled
                      && string.Equals(
                          ResolveTraditionalMarketThemeProduct(product.TraditionalMarketTheme!.MarketScopeKey)?.PackKey,
                          product.PackKey,
                          StringComparison.OrdinalIgnoreCase)
                    : product.Assets.Any(IsAssetActive);
    }

    public CommunityDecorationProduct? ResolveTraditionalMarketThemeProduct(string? marketScopeKey)
    {
        if (!IsTraditionalMarketThemeEnabled || string.IsNullOrWhiteSpace(marketScopeKey))
        {
            return null;
        }

        var normalizedScope = marketScopeKey.Trim();
        if (activeTraditionalMarketThemePackByScope.TryGetValue(normalizedScope, out var packKey))
        {
            var selected = FindOwnedTraditionalMarketThemeProduct(packKey);
            if (selected?.TraditionalMarketTheme is { } selectedTheme
                && selectedTheme.IsOfficiallyApplicable
                && string.Equals(selectedTheme.MarketScopeKey, normalizedScope, StringComparison.OrdinalIgnoreCase))
            {
                return selected;
            }
        }

        return products.FirstOrDefault(product =>
            product.IsTraditionalMarketTheme
            && product.TraditionalMarketTheme!.IsOfficiallyApplicable
            && IsProductOwned(product)
            && string.Equals(
                product.TraditionalMarketTheme.MarketScopeKey,
                normalizedScope,
                StringComparison.OrdinalIgnoreCase));
    }

    public TraditionalMarketThemeManifest? ResolveTraditionalMarketTheme(string? marketScopeKey)
        => ResolveTraditionalMarketThemeProduct(marketScopeKey)?.TraditionalMarketTheme;

    public BaguaTransitionMotionManifest? ResolveBaguaMotion(
        string assetSlotKey,
        string motionKind)
    {
        if (!IsBaguaMotionEnabled)
        {
            return null;
        }

        foreach (var scope in BuildBaguaMotionScopePriority(assetSlotKey, motionKind))
        {
            if (activeBaguaMotionPackByScope.TryGetValue(scope, out var packKey) &&
                FindOwnedBaguaMotionProduct(packKey)?.BaguaMotion is { } manifest)
            {
                return manifest;
            }
        }

        return DefaultBaguaMotion;
    }

    public void Purchase(CommunityDecorationProduct product)
    {
        sessionPurchasedPackKeys.Add(product.PackKey);
        Changed?.Invoke();
    }

    public void SynchronizeServerOwnedPacks(IEnumerable<string> packKeys)
    {
        ArgumentNullException.ThrowIfNull(packKeys);

        serverOwnedPackKeys.Clear();
        foreach (var packKey in packKeys)
        {
            if (!string.IsNullOrWhiteSpace(packKey))
            {
                serverOwnedPackKeys.Add(packKey.Trim());
            }
        }

        RemoveInactiveBaguaMotionSelections();
        RemoveInactiveTraditionalMarketThemeSelections();
        Changed?.Invoke();
    }

    public void ClearServerOwnedPacks()
    {
        if (serverOwnedPackKeys.Count == 0)
        {
            return;
        }

        serverOwnedPackKeys.Clear();
        RemoveInactiveBaguaMotionSelections();
        RemoveInactiveTraditionalMarketThemeSelections();
        Changed?.Invoke();
    }

    public void ClearAccountOwnedPacks()
    {
        if (serverOwnedPackKeys.Count == 0 && sessionPurchasedPackKeys.Count == 0)
        {
            return;
        }

        serverOwnedPackKeys.Clear();
        sessionPurchasedPackKeys.Clear();
        RemoveInactiveBaguaMotionSelections();
        RemoveInactiveTraditionalMarketThemeSelections();
        Changed?.Invoke();
    }

    public bool ApplyProduct(CommunityDecorationProduct product)
    {
        if (!IsProductOwned(product))
        {
            return false;
        }

        if (product.IsHomeTheme)
        {
            ActiveHomeThemePackKey = product.PackKey;
            IsHomeThemeEnabled = true;
            Changed?.Invoke();
            return true;
        }

        if (product.IsBaguaMotion)
        {
            if (product.BaguaMotion!.SlotPatterns.Contains("*", StringComparer.OrdinalIgnoreCase))
            {
                activeBaguaMotionPackByScope.Clear();
            }

            foreach (var pattern in product.BaguaMotion!.SlotPatterns)
            {
                activeBaguaMotionPackByScope[pattern] = product.PackKey;
            }

            IsBaguaMotionEnabled = true;
            Changed?.Invoke();
            return true;
        }

        if (product.IsTraditionalMarketTheme)
        {
            var theme = product.TraditionalMarketTheme!;
            if (!theme.IsOfficiallyApplicable)
            {
                return false;
            }

            activeTraditionalMarketThemePackByScope[theme.MarketScopeKey] = product.PackKey;
            IsTraditionalMarketThemeEnabled = true;
            Changed?.Invoke();
            return true;
        }

        return product.Assets.FirstOrDefault() is { } firstAsset && Apply(firstAsset);
    }

    public bool ApplyHomeThemePack(string? packKey)
    {
        if (string.IsNullOrWhiteSpace(packKey))
        {
            return false;
        }

        var product = products.FirstOrDefault(candidate =>
            candidate.IsHomeTheme &&
            string.Equals(candidate.PackKey, packKey.Trim(), StringComparison.OrdinalIgnoreCase));
        return product is not null && ApplyProduct(product);
    }

    public void RestoreDefaultHomeTheme()
    {
        ActiveHomeThemePackKey = DefaultHomeThemePackKey;
        IsHomeThemeEnabled = true;
        Changed?.Invoke();
    }

    public bool ApplyTraditionalMarketThemePack(string? marketScopeKey, string? packKey)
    {
        if (string.IsNullOrWhiteSpace(marketScopeKey) || string.IsNullOrWhiteSpace(packKey))
        {
            return false;
        }

        var normalizedScope = marketScopeKey.Trim();
        var product = products.FirstOrDefault(candidate =>
            candidate.IsTraditionalMarketTheme
            && string.Equals(candidate.PackKey, packKey.Trim(), StringComparison.OrdinalIgnoreCase)
            && string.Equals(
                candidate.TraditionalMarketTheme!.MarketScopeKey,
                normalizedScope,
                StringComparison.OrdinalIgnoreCase));
        return product is not null && ApplyProduct(product);
    }

    public void RestoreDefaultTraditionalMarketTheme(string? marketScopeKey)
    {
        if (string.IsNullOrWhiteSpace(marketScopeKey))
        {
            return;
        }

        activeTraditionalMarketThemePackByScope.Remove(marketScopeKey.Trim());
        IsTraditionalMarketThemeEnabled = true;
        Changed?.Invoke();
    }

    public void RestoreDefaultBaguaMotion()
    {
        activeBaguaMotionPackByScope.Clear();
        activeBaguaMotionPackByScope["*"] = DefaultBaguaMotionPackKey;
        IsBaguaMotionEnabled = true;
        Changed?.Invoke();
    }

    public bool Apply(CommunityDecorationAsset asset)
    {
        var product = products.FirstOrDefault(item =>
            item.Assets.Any(candidate => string.Equals(candidate.Key, asset.Key, StringComparison.OrdinalIgnoreCase)));
        if (product is not null && !IsProductOwned(product))
        {
            return false;
        }

        if (asset.Target == CommunityDecorationTarget.Bagua)
        {
            ActiveBaguaAssetKey = asset.Key;
            IsBaguaDecorationEnabled = true;
        }
        else if (asset.Target == CommunityDecorationTarget.DiagramNode)
        {
            ActiveNodeAssetKey = asset.Key;
            IsNodeDecorationEnabled = true;
        }
        else
        {
            return false;
        }

        Changed?.Invoke();
        return true;
    }

    public void UseAutomaticNodeDecoration()
    {
        ActiveNodeAssetKey = null;
        IsNodeDecorationEnabled = true;
        Changed?.Invoke();
    }

    public void SetTargetEnabled(CommunityDecorationTarget target, bool enabled)
    {
        switch (target)
        {
            case CommunityDecorationTarget.HomeNavigatorTheme:
                IsHomeThemeEnabled = enabled;
                break;
            case CommunityDecorationTarget.TraditionalMarketTheme:
                IsTraditionalMarketThemeEnabled = enabled;
                break;
            case CommunityDecorationTarget.Bagua:
                IsBaguaDecorationEnabled = enabled;
                break;
            case CommunityDecorationTarget.DiagramNode:
                IsNodeDecorationEnabled = enabled;
                break;
            case CommunityDecorationTarget.BaguaTransitionMotion:
                IsBaguaMotionEnabled = enabled;
                break;
            default:
                return;
        }

        Changed?.Invoke();
    }

    public bool TryCreateCustomAsset(
        CommunityDecorationTarget target,
        string? title,
        string? symbol,
        string? accentColor,
        string? imageUrl,
        out CommunityDecorationAsset? asset,
        out string message)
    {
        if (target is not CommunityDecorationTarget.Bagua and not CommunityDecorationTarget.DiagramNode)
        {
            asset = null;
            message = "사용자 제작 항목은 괘상 또는 다이어그램 노드 유형으로 만들어 주세요.";
            return false;
        }

        var normalizedTitle = title?.Trim();
        var normalizedSymbol = symbol?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedTitle))
        {
            asset = null;
            message = "꾸미기 이름을 입력해 주세요.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(normalizedSymbol) && string.IsNullOrWhiteSpace(imageUrl))
        {
            asset = null;
            message = "표시 기호나 사용 권한이 있는 이미지 주소 중 하나를 입력해 주세요.";
            return false;
        }

        normalizedSymbol = string.IsNullOrWhiteSpace(normalizedSymbol) ? "나" : normalizedSymbol[..Math.Min(2, normalizedSymbol.Length)];
        var normalizedColor = NormalizeColor(accentColor);
        var key = $"custom-{target.ToString().ToLowerInvariant()}-{Guid.NewGuid():N}";
        노드스티커이미지Response? nodeSticker = null;
        if (target == CommunityDecorationTarget.DiagramNode)
        {
            nodeSticker = new()
            {
                이미지Key = key,
                팩Key = "user-custom",
                표시명 = normalizedTitle,
                이미지Url = string.IsNullOrWhiteSpace(imageUrl)
                    ? BuildNodeStickerDataUrl(normalizedColor, normalizedSymbol, normalizedTitle)
                    : imageUrl.Trim(),
                대체Text = $"{normalizedTitle} 사용자 제작 노드 이미지",
                MimeType = string.IsNullOrWhiteSpace(imageUrl) ? "image/svg+xml" : "image/*",
                원본너비Px = 512,
                원본높이Px = 512,
                노드종류목록 = ["product", "sales-channel", "place", "warehouse", "work", "delivery", "confirm"],
                스타일Tags = ["user-custom"],
                라이선스Code = 노드스티커라이선스Code.플랫폼노드사용,
                검수상태 = 노드스티커검수상태.초안
            };
        }

        asset = new(
            key,
            "user-custom",
            normalizedTitle,
            "내 제작함",
            target == CommunityDecorationTarget.Bagua
                ? "내가 만든 사방 이동판 표시입니다."
                : "내가 만든 다이어그램 노드 이미지입니다.",
            target,
            normalizedSymbol,
            normalizedColor,
            nodeSticker,
            IsCustom: true);
        customAssets.Insert(0, asset);
        products.Insert(0, new(
            $"store-{key}",
            "user-custom",
            normalizedTitle,
            "내 제작함",
            target == CommunityDecorationTarget.Bagua
                ? "내가 직접 만든 사방 이동판 꾸미기입니다."
                : "내가 직접 만든 다이어그램 노드 이미지입니다.",
            target,
            0,
            "KRW",
            [asset],
            IsCustom: true));
        ownedPackKeys.Add("user-custom");
        Apply(asset);
        message = $"{normalizedTitle}을(를) 내 제작함에 저장하고 적용했습니다.";
        return true;
    }

    public bool TryCreateHomeThemePackage(
        string? title,
        string? creatorName,
        string? summary,
        decimal priceAmount,
        HomeNavigatorThemeManifest manifest,
        out CommunityDecorationProduct? product,
        out string message)
    {
        var normalizedTitle = title?.Trim();
        var normalizedCreator = creatorName?.Trim();
        var normalizedSummary = summary?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedTitle))
        {
            product = null;
            message = "테마 상품명을 입력해 주세요.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(normalizedCreator))
        {
            product = null;
            message = "디자이너 표시명을 입력해 주세요.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(normalizedSummary))
        {
            product = null;
            message = "테마 설명을 입력해 주세요.";
            return false;
        }

        if (priceAmount < 0)
        {
            product = null;
            message = "판매 가격은 0원 이상이어야 합니다.";
            return false;
        }

        var keySuffix = Guid.NewGuid().ToString("N");
        var packKey = $"home-theme-draft-{keySuffix}";
        var normalizedManifest = NormalizeThemeManifest(manifest with { ReviewStatus = "초안" });
        product = new(
            $"store-{packKey}",
            packKey,
            normalizedTitle,
            normalizedCreator,
            normalizedSummary,
            CommunityDecorationTarget.HomeNavigatorTheme,
            priceAmount,
            "KRW",
            [],
            normalizedManifest,
            IsCustom: true);

        products.Insert(0, product);
        ownedPackKeys.Add(packKey);
        ApplyProduct(product);
        message = $"{normalizedTitle} 패키지를 내 제작함에 초안으로 저장하고 적용했습니다.";
        return true;
    }

    public bool TryCreateTraditionalMarketThemePackage(
        string? title,
        string? creatorName,
        string? summary,
        decimal priceAmount,
        TraditionalMarketThemeManifest manifest,
        out CommunityDecorationProduct? product,
        out string message)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        var normalizedTitle = title?.Trim();
        var normalizedCreator = creatorName?.Trim();
        var normalizedSummary = summary?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedTitle))
        {
            product = null;
            message = "시장 꾸미기 팩 이름을 입력해 주세요.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(normalizedCreator))
        {
            product = null;
            message = "디자이너 표시명을 입력해 주세요.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(normalizedSummary))
        {
            product = null;
            message = "시장 꾸미기 팩 설명을 입력해 주세요.";
            return false;
        }

        if (priceAmount < 0)
        {
            product = null;
            message = "판매 가격은 0원 이상이어야 합니다.";
            return false;
        }

        if (!TraditionalMarketThemePolicy.TryPrepareDraft(
                manifest,
                out var normalizedManifest,
                out message))
        {
            product = null;
            return false;
        }

        var keySuffix = Guid.NewGuid().ToString("N");
        var packKey = $"traditional-market-theme-draft-{keySuffix}";
        product = new(
            $"store-{packKey}",
            packKey,
            normalizedTitle,
            normalizedCreator,
            normalizedSummary,
            CommunityDecorationTarget.TraditionalMarketTheme,
            priceAmount,
            "KRW",
            [],
            IsCustom: true,
            TraditionalMarketTheme: normalizedManifest);

        products.Insert(0, product);
        ownedPackKeys.Add(packKey);
        Changed?.Invoke();
        message = $"{normalizedTitle} 팩을 초안으로 저장했습니다. 플랫폼 검수와 상인회 승인을 모두 받은 뒤 공식 화면에 적용할 수 있습니다.";
        return true;
    }

    private CommunityDecorationAsset? FindAsset(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        return products.SelectMany(item => item.Assets)
                   .Concat(customAssets)
                   .FirstOrDefault(item => string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase));
    }

    private BaguaTransitionMotionManifest DefaultBaguaMotion
        => products.First(item => item.IsBaguaMotion &&
            string.Equals(item.PackKey, DefaultBaguaMotionPackKey, StringComparison.OrdinalIgnoreCase)).BaguaMotion!;

    private CommunityDecorationProduct? FindOwnedBaguaMotionProduct(string packKey)
    {
        var product = products.FirstOrDefault(item => item.IsBaguaMotion &&
            string.Equals(item.PackKey, packKey, StringComparison.OrdinalIgnoreCase));
        return product is not null && IsProductOwned(product) ? product : null;
    }

    private CommunityDecorationProduct? FindOwnedTraditionalMarketThemeProduct(string packKey)
    {
        var product = products.FirstOrDefault(item => item.IsTraditionalMarketTheme
            && string.Equals(item.PackKey, packKey, StringComparison.OrdinalIgnoreCase));
        return product is not null && IsProductOwned(product) ? product : null;
    }

    private void RemoveInactiveBaguaMotionSelections()
    {
        foreach (var scope in activeBaguaMotionPackByScope
                     .Where(pair => FindOwnedBaguaMotionProduct(pair.Value) is null)
                     .Select(pair => pair.Key)
                     .ToArray())
        {
            activeBaguaMotionPackByScope.Remove(scope);
        }

        activeBaguaMotionPackByScope.TryAdd("*", DefaultBaguaMotionPackKey);
    }

    private void RemoveInactiveTraditionalMarketThemeSelections()
    {
        foreach (var scope in activeTraditionalMarketThemePackByScope
                     .Where(pair => FindOwnedTraditionalMarketThemeProduct(pair.Value)?.TraditionalMarketTheme is not { IsOfficiallyApplicable: true } theme
                                    || !string.Equals(theme.MarketScopeKey, pair.Key, StringComparison.OrdinalIgnoreCase))
                     .Select(pair => pair.Key)
                     .ToArray())
        {
            activeTraditionalMarketThemePackByScope.Remove(scope);
        }
    }

    private static IEnumerable<string> BuildBaguaMotionScopePriority(
        string assetSlotKey,
        string motionKind)
    {
        if (!string.IsNullOrWhiteSpace(assetSlotKey))
        {
            yield return assetSlotKey;

            var parts = assetSlotKey.Split(':', 3, StringSplitOptions.TrimEntries);
            if (parts.Length == 3)
            {
                yield return $"bagua-motion:*:{parts[2]}";
                yield return $"bagua-motion:{parts[1]}:*";
            }
        }

        if (!string.IsNullOrWhiteSpace(motionKind))
        {
            yield return $"motion-kind:{motionKind.Trim()}";
        }

        yield return "*";
    }

    private static List<CommunityDecorationProduct> BuildProducts()
    {
        var products = new List<CommunityDecorationProduct>
        {
            new(
                "store-home-theme-hongdal-default-v1",
                DefaultHomeThemePackKey,
                "홍달 반야·방편 기본",
                "Hongdal",
                "방편의 붉은 흐름과 반야의 푸른 물결, 검은 커뮤니티와 나무빛 상점을 담은 기본 홈 테마입니다.",
                CommunityDecorationTarget.HomeNavigatorTheme,
                0,
                "KRW",
                [],
                CreateDefaultHomeTheme()),
            new(
                "store-home-theme-moonlit-voyage-v1",
                "home-theme-moonlit-voyage-v1",
                "달빛 항해",
                "모래별 공방",
                "깊은 물 위에 나무배가 떠 있는 인상을 살린 차분한 야간형 홈 테마입니다.",
                CommunityDecorationTarget.HomeNavigatorTheme,
                4900,
                "KRW",
                [],
                new(
                    "1.0.0",
                    "neutral-taegeuk-v1",
                    "승인",
                    "홍달 앱 내 개인 사용",
                    "#071426",
                    new("outer-upaya", "바깥 방편", "#7A2E2E", AltText: "짙은 적갈색 방편 영역"),
                    new("outer-prajna", "바깥 반야", "#103D66", AltText: "깊은 물빛 반야 영역"),
                    new("inner-community", "커뮤니티", "#090D12", AltText: "밤하늘색 커뮤니티 영역"),
                    new("inner-store", "상점", "#9A6238", AltText: "나무배색 상점 영역"),
                    new("center-gen", "가운데 간괘", "#D6B77A", AltText: "달빛 간괘 중심"),
                    new("frame", "원형 테두리", "#E8D6AD", AltText: "모래빛 테두리"),
                    new("labels", "라벨", "#FFF6DD", AltText: "달빛 라벨"),
                    new("closed-handle", "접힌 손잡이", "#C18C45", AltText: "황동색 접힌 손잡이"))),
            new(
                "store-bagua-basic",
                "bagua-basic",
                "후천 사방 기본",
                "Hongdal",
                "기능을 방해하지 않는 단정한 기본 괘상입니다.",
                CommunityDecorationTarget.Bagua,
                0,
                "KRW",
                [new(BasicBaguaAssetKey, "bagua-basic", "푸른 감괘", "Hongdal", "기본 후천 사방 표시", CommunityDecorationTarget.Bagua, "☵", "#2563eb")]),
            new(
                "store-bagua-wonbanggak",
                "bagua-wonbanggak",
                "원방각 사람과 업무",
                "Hongdal Community",
                "원·방·각 철학을 중심점에 담은 커뮤니티용 표시입니다.",
                CommunityDecorationTarget.Bagua,
                0,
                "KRW",
                [new("bagua-wonbanggak", "bagua-wonbanggak", "원방각", "Hongdal Community", "원방각 커뮤니티 표시", CommunityDecorationTarget.Bagua, "○□△", "#ec4899")]),
            new(
                "store-bagua-night",
                "bagua-night",
                "별빛 사방판",
                "모래별 공방",
                "어두운 남보라색과 별 표식으로 만든 창작자 테마입니다.",
                CommunityDecorationTarget.Bagua,
                900,
                "KRW",
                [new("bagua-night-star", "bagua-night", "별빛 중심", "모래별 공방", "별빛 사방판 중심 표시", CommunityDecorationTarget.Bagua, "✦", "#7c3aed")]),
            new(
                "store-bagua-motion-basic-runner-v1",
                DefaultBaguaMotionPackKey,
                "기본 업무 달리기",
                "Hongdal Motion",
                "작은 벡터 인물이 역할별 업무를 들고 출발괘에서 도착괘까지 달리는 기본 전환 모션입니다.",
                CommunityDecorationTarget.BaguaTransitionMotion,
                0,
                "KRW",
                [],
                BaguaMotion: new(
                    "1.0.0",
                    "runner-v1",
                    "승인",
                    "홍달 앱 내 개인 사용",
                    "🏃",
                    "#2563eb",
                    1.0,
                    true,
                    "기본 벡터 주자",
                    "점선 업무 경로")),
            new(
                "store-bagua-motion-courier-sprint-v1",
                "bagua-motion-courier-sprint-v1",
                "꼬마 운반대 릴레이",
                "달리는상자 스튜디오",
                "문서와 상자를 등에 멘 꼬마 운반대가 업무를 빠르게 인계하는 경쾌한 모션 팩입니다.",
                CommunityDecorationTarget.BaguaTransitionMotion,
                1900,
                "KRW",
                [],
                BaguaMotion: new(
                    "1.0.0",
                    "courier-sprint-v1",
                    "승인",
                    "홍달 앱 내 개인 사용",
                    "📦",
                    "#0ea5e9",
                    0.82,
                    false,
                    "꼬마 운반대",
                    "두 줄 릴레이 궤적",
                    ["bagua-motion:*:order-to-transport"],
                    5)),
            new(
                "store-bagua-motion-light-trail-v1",
                "bagua-motion-light-trail-v1",
                "빛의 서명 전달자",
                "모래별 모션 공방",
                "합의와 전자서명의 확정 기록이 빛의 궤적을 따라 다음 업무로 전달되는 모션 팩입니다.",
                CommunityDecorationTarget.BaguaTransitionMotion,
                2900,
                "KRW",
                [],
                BaguaMotion: new(
                    "1.0.0",
                    "light-trail-v1",
                    "승인",
                    "홍달 앱 내 개인 사용",
                    "✦",
                    "#f59e0b",
                    1.08,
                    false,
                    "빛의 전달자",
                    "발광 서명 궤적"))
        };

        products.AddRange(ScriptureDecorationCatalog.CreateProducts());
        products.AddRange(TraditionalMarketThemeCatalog.CreateProducts());

        foreach (var pack in 노드스티커Catalog.기본팩목록)
        {
            var assets = pack.이미지목록
                .Select(image => new CommunityDecorationAsset(
                    image.이미지Key,
                    pack.팩Key,
                    image.표시명,
                    pack.창작자표시명,
                    image.대체Text,
                    CommunityDecorationTarget.DiagramNode,
                    image.표시명[..1],
                    ResolveNodeAccent(image.이미지Key),
                    image))
                .ToArray();
            products.Add(new(
                $"store-{pack.팩Key}",
                pack.팩Key,
                pack.제목,
                pack.창작자표시명,
                pack.요약,
                CommunityDecorationTarget.DiagramNode,
                pack.거래정책.가격금액,
                pack.거래정책.통화Code,
                assets));
        }

        return products;
    }

    public static HomeNavigatorThemeManifest CreateDefaultHomeTheme()
        => new(
            "1.0.0",
            "neutral-taegeuk-v1",
            "승인",
            "홍달 앱 내 개인 사용",
            "#F8FAFC",
            new("outer-upaya", "바깥 방편", "#CD2E3A", AltText: "붉은 방편 영역"),
            new("outer-prajna", "바깥 반야", "#0047A0", AltText: "푸른 반야 영역"),
            new("inner-community", "커뮤니티", "#171717", AltText: "검은 커뮤니티 영역"),
            new("inner-store", "상점", "#8B5E3C", AltText: "나무빛 상점 영역"),
            new("center-gen", "가운데 간괘", "#1F2937", AltText: "간괘 중심"),
            new("frame", "원형 테두리", "#F4EADC", AltText: "상아색 테두리"),
            new("labels", "라벨", "#FFFFFF", AltText: "흰색 라벨"),
            new("closed-handle", "접힌 손잡이", "#7C3AED", AltText: "보라색 접힌 손잡이"));

    private static HomeNavigatorThemeManifest NormalizeThemeManifest(HomeNavigatorThemeManifest manifest)
        => manifest with
        {
            PreviewBackground = NormalizeColor(manifest.PreviewBackground),
            OuterUpaya = NormalizeThemeSlot(manifest.OuterUpaya),
            OuterPrajna = NormalizeThemeSlot(manifest.OuterPrajna),
            InnerCommunity = NormalizeThemeSlot(manifest.InnerCommunity),
            InnerStore = NormalizeThemeSlot(manifest.InnerStore),
            CenterGen = NormalizeThemeSlot(manifest.CenterGen),
            Frame = NormalizeThemeSlot(manifest.Frame),
            Labels = NormalizeThemeSlot(manifest.Labels),
            ClosedHandle = NormalizeThemeSlot(manifest.ClosedHandle)
        };

    private static HomeNavigatorThemeSlot NormalizeThemeSlot(HomeNavigatorThemeSlot slot)
        => slot with
        {
            Color = NormalizeColor(slot.Color),
            ImageUrl = string.IsNullOrWhiteSpace(slot.ImageUrl) ? null : slot.ImageUrl.Trim()
        };

    private static string NormalizeColor(string? color)
    {
        var candidate = color?.Trim();
        return candidate is { Length: 7 } && candidate[0] == '#' && candidate[1..].All(Uri.IsHexDigit)
            ? candidate
            : "#2563eb";
    }

    private static string ResolveNodeAccent(string key)
        => key switch
        {
            var value when value.Contains("warehouse", StringComparison.OrdinalIgnoreCase) => "#16a34a",
            var value when value.Contains("delivery", StringComparison.OrdinalIgnoreCase) => "#7c3aed",
            var value when value.Contains("confirm", StringComparison.OrdinalIgnoreCase) => "#0891b2",
            var value when value.Contains("work", StringComparison.OrdinalIgnoreCase) => "#d97706",
            _ => "#2563eb"
        };

    private static string BuildNodeStickerDataUrl(string accentColor, string symbol, string title)
    {
        var safeSymbol = System.Net.WebUtility.HtmlEncode(symbol);
        var safeTitle = System.Net.WebUtility.HtmlEncode(title.Length > 8 ? title[..8] : title);
        var svg = $$"""
            <svg xmlns="http://www.w3.org/2000/svg" width="512" height="512" viewBox="0 0 512 512">
              <rect width="512" height="512" rx="128" fill="#ffffff"/>
              <circle cx="256" cy="224" r="132" fill="{{accentColor}}"/>
              <text x="256" y="270" text-anchor="middle" font-size="112" font-family="sans-serif" font-weight="700" fill="#ffffff">{{safeSymbol}}</text>
              <rect x="96" y="360" width="320" height="64" rx="32" fill="#111827"/>
              <text x="256" y="402" text-anchor="middle" font-size="30" font-family="sans-serif" font-weight="700" fill="#ffffff">{{safeTitle}}</text>
            </svg>
            """;
        return $"data:image/svg+xml;base64,{Convert.ToBase64String(Encoding.UTF8.GetBytes(svg))}";
    }
}
