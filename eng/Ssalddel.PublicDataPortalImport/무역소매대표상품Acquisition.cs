using System.Security.Cryptography;
using System.Text.Json;

internal static class 무역소매대표상품Acquisition
{
    public const string Relative = "artifacts/local/public-data/trade-retail/kr-imports-2025-h6-r1";
    public const string TradeFile = "un-comtrade-kr-imports-2025-h6.json";
    public const string HsFile = "un-comtrade-h6-selected.json";
    public const string PartnerFile = "un-comtrade-partners-selected.json";
    public const string ReceiptFile = "acquisition.json";
    public const string ProductRelative = "eng/public-data/trade-retail/representative-products.reviewed.json";

    public const string TradeUrl = "https://comtradeapi.un.org/public/v1/preview/C/A/HS?period=2025&reporterCode=410&cmdCode=090121%2C080390%2C030617&flowCode=M&partner2Code=0&customsCode=C00&motCode=0&maxRecords=500&typeCode=C&freqCode=A&clCode=H6";
    public const string HsUrl = "https://comtradeapi.un.org/files/v1/app/reference/H6.json";
    public const string PartnerUrl = "https://comtradeapi.un.org/files/v1/app/reference/partnerAreas.json";

    private static readonly string[] HsCodes = ["030617", "080390", "090121"];
    private static readonly int[] PartnerCodes = [0, 32, 156, 188, 218, 380, 458, 528, 604, 608, 704, 757, 842];

    public static async Task RunAsync(string root, Dictionary<string, object?> result)
    {
        var folder = Path.Combine(root, Relative);
        Directory.CreateDirectory(folder);
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Ssalddel-PublicDataResearch/1.0");

        var collectedAt = DateTimeOffset.UtcNow;
        var tradeBytes = await http.GetByteArrayAsync(TradeUrl);
        var h6Bytes = await http.GetByteArrayAsync(HsUrl);
        var partnerBytes = await http.GetByteArrayAsync(PartnerUrl);

        var tradePath = Path.Combine(folder, TradeFile);
        await File.WriteAllBytesAsync(tradePath, tradeBytes);
        var hsPath = Path.Combine(folder, HsFile);
        await File.WriteAllBytesAsync(hsPath, SelectReferenceRows(h6Bytes, HsCodes));
        var partnerPath = Path.Combine(folder, PartnerFile);
        await File.WriteAllBytesAsync(partnerPath, SelectReferenceRows(partnerBytes, PartnerCodes.Select(x => x.ToString()).ToArray()));

        var files = new[]
        {
            ReceiptEntry(TradeFile, TradeUrl, tradePath, collectedAt),
            ReceiptEntry(HsFile, HsUrl, hsPath, collectedAt),
            ReceiptEntry(PartnerFile, PartnerUrl, partnerPath, collectedAt),
        };
        var receipt = JsonSerializer.SerializeToUtf8Bytes(new
        {
            schemaVersion = "public-data-acquisition-receipt.v1",
            collectedAtUtc = collectedAt,
            scope = "Korea annual imports 2025, H6 090121/080390/030617 and partner references",
            accessMethod = "UN Comtrade public API without credentials",
            limitations = new[] { "PublicPreviewEndpoint", "AnnualAggregateMayBeRevised", "NoIndividualProductProvenance" },
            files,
        }, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllBytesAsync(Path.Combine(folder, ReceiptFile), receipt);

        result["mode"] = "trade-retail-acquire";
        result["collectedAtUtc"] = collectedAt;
        result["files"] = files;
        result["productObservationFile"] = ProductRelative;
    }

    private static byte[] SelectReferenceRows(byte[] bytes, IReadOnlyCollection<string> ids)
    {
        using var document = JsonDocument.Parse(bytes);
        var selected = document.RootElement.GetProperty("results")
            .EnumerateArray()
            .Where(row => ids.Contains(row.GetProperty("id").ToString(), StringComparer.Ordinal))
            .OrderBy(row => row.GetProperty("id").ToString(), StringComparer.Ordinal)
            .Select(row => JsonSerializer.Deserialize<Dictionary<string, object?>>(row.GetRawText())!)
            .ToArray();
        if (selected.Length != ids.Count)
            throw new InvalidDataException("TradeReferenceSelectionCountChanged");
        return JsonSerializer.SerializeToUtf8Bytes(new
        {
            sourceUrl = ids.Count == HsCodes.Length ? HsUrl : PartnerUrl,
            selectedAtUtc = DateTimeOffset.UtcNow,
            results = selected,
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    private static object ReceiptEntry(string fileName, string sourceUrl, string filePath, DateTimeOffset collectedAt)
    {
        var bytes = File.ReadAllBytes(filePath);
        return new
        {
            fileName,
            sourceUrl,
            collectedAtUtc = collectedAt,
            contentLength = bytes.LongLength,
            sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),
        };
    }
}
