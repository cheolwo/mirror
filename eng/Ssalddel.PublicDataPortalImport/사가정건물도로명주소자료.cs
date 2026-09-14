using System.Globalization;
using System.IO.Compression;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Domain.PublicData;
using Ssalddel.Infrastructure.Persistence.PublicData;
using 살뜰.Services.External.PublicData.Korea;
using static 면목동공간결속;

// 사가정 1km 동결 건물 602개에 공식 개별/공유 도로명주소 결속 상태를 부여하는 비공개 자료 파이프라인.
// 가까운 주소, 상세 동·층·호, 출입구, 배달·가격·Unity 적용 권한은 생성하지 않는다.
internal static class 사가정건물도로명주소자료
{
    private const string Relative = "artifacts/local/public-data/sagajeong-building-address-20260914-r1";
    private const string MapPath = "C:/Users/user/ssalddel/Assets/Ssalddel/Resources/SagajeongReference.json";
    private const string OsmPath = "artifacts/local/neighborhood-source-acquisition/sagajeong-r2/map.osm";
    private const string Source = "mois-road-address-building-db";
    private const string Dataset = "sagajeong-building-road-address-assignment";
    private const string Revision = "sagajeong-building-address-assignment.r1";
    private const string SourceVintage = "202608";
    private const string Region = "area:kr:seoul:jungnang:sagajeong-station-0722-one-kilometer";
    private const string ZipName = "202608_건물DB_전체분.zip";
    private const string SeoulName = "build_seoul.txt";
    private const string MetadataName = "data-go-kr-metadata.json";
    private const string DownloadListName = "download-list.json";
    private const string AcquisitionName = "acquisition.json";
    private const string AssignmentName = "building-address-assignments.json";
    private const string ManifestName = "manifest.json";
    private const long ZipBytes = 149_263_413;
    private const string ZipHash = "4AA70C569AAF14F550313B5836346A1BA438491FB4CEC61877E615C9E602AFA7";
    private const string SeoulHash = "285286D60409E1CC93898146952D9BC4A805C8F955A765C9F0D72E8A6EA16081";
    private const string GuideHash = "C024FEFFF0AA1DD0C812F2CF76DB5296440575831E6CBA4B313245071CC994C1";
    private const string MapHash = 면목동주소공간연결.MapHash;
    private const string OsmHash = "3BDF9E9D36360FB32CB65C7C216215DCE60C762F7918FB7556D6695288A2D0A3";
    private const string Boundary = "PrivateReviewOnly;NoDetailedAddress;NoNearestAddressInference;NoEntranceInference;DistributionApprovedFalse;DeliveryEligibleFalse;PriceObservationEligibleFalse;UnityApplyFalse";
    private static readonly DateTimeOffset EvidenceAt = new(2026, 8, 31, 0, 0, 0, TimeSpan.Zero);
    private static readonly Regex HouseNumber = new(@"^\d{1,5}(?:-\d{1,5})?$", RegexOptions.CultureInvariant);

    private sealed record OfficialAddress(
        string MatchKey,
        string CanonicalRoadAddress,
        string OfficialCompositeKey,
        string BuildingManagementNumber,
        string LegalDongCode,
        string AdministrativeDongCode,
        string AdministrativeDongName,
        string BuildingRegisterName,
        string DetailedBuildingName,
        string SigunguBuildingName,
        string ApartmentFlag,
        string DetailedAddressFlag,
        string NoticeDate);

    private sealed record Candidate(string MatchKey, string CanonicalRoadAddress, string Method, string EvidenceRef, decimal Confidence, string MissingReason);

    private sealed record Projected(JsonObject Document, JsonObject Manifest, JsonArray Buildings, JsonObject Summary, string AssignmentHash, DateTimeOffset CollectedAt);

    private static void Check(bool ok, string code) => 면목동공간결속.Require(ok, "SagajeongBuildingAddress:" + code);

    public static async Task RunAsync(string mode, string root, Dictionary<string, object?> result)
    {
        Check(mode is "acquire" or "self-test" or "prepare" or "preview" or "replay" or "apply" or "verify", "Mode");
        root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        if (mode == "self-test")
        {
            result["selfTestsPassed"] = SelfTest();
            return;
        }

        var folder = Path.Combine(root, Relative);
        if (mode == "acquire")
        {
            await AcquireAsync(folder);
            var acquisition = ValidateAcquisition(folder);
            result["sourceVintage"] = SourceVintage;
            result["sourceHash"] = ZipHash.ToLowerInvariant();
            result["seoulHash"] = SeoulHash.ToLowerInvariant();
            result["license"] = S(Read(Path.Combine(folder, MetadataName)), "license");
            result["collectedAtUtc"] = S(acquisition, "collectedAtUtc");
            result["databaseWriteAttempted"] = false;
            return;
        }

        var projected = Build(root, folder);
        result["sourceVintage"] = SourceVintage;
        result["projectionHash"] = projected.AssignmentHash.ToLowerInvariant();
        result["summary"] = projected.Summary.DeepClone();
        if (mode == "prepare")
        {
            WriteFrozen(folder, AssignmentName, projected.Document);
            WriteFrozen(folder, ManifestName, projected.Manifest);
            result["prepared"] = true;
            return;
        }

        if (mode == "preview")
        {
            result["databaseWriteAttempted"] = false;
            return;
        }

        ValidateFrozenProjection(folder, projected);
        if (mode == "replay")
        {
            result["replayMatched"] = true;
            result["databaseWriteAttempted"] = false;
            return;
        }

        await PersistAsync(root, folder, projected, mode, result);
    }

    private static async Task AcquireAsync(string folder)
    {
        Directory.CreateDirectory(folder);
        using var client = new HttpClient(new HttpClientHandler { AutomaticDecompression = System.Net.DecompressionMethods.All })
        {
            Timeout = TimeSpan.FromMinutes(5)
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Ssalddel-Sagajeong-Building-Address/1.0");

        var metadataPath = Path.Combine(folder, MetadataName);
        if (!File.Exists(metadataPath))
        {
            var bytes = await BoundedGetAsync(client, "https://www.data.go.kr/catalog/15050424/fileData.json", 2 * 1024 * 1024);
            await WriteNewAsync(metadataPath, bytes);
        }
        var metadata = Read(metadataPath);
        Check(S(metadata, "name").Contains("도로명주소 건물DB", StringComparison.Ordinal), "MetadataDataset");
        Check(S(metadata, "creator") == string.Empty || metadata["creator"]?["name"]?.ToString() == "행정안전부", "MetadataProvider");
        Check(S(metadata, "license") == "이용허락범위 제한 없음", "MetadataLicense");

        var listPath = Path.Combine(folder, DownloadListName);
        if (!File.Exists(listPath))
        {
            using var response = await client.PostAsJsonAsync(
                "https://business.juso.go.kr/api/jst/selectAttrbDBDwldList",
                new { rtlDtaDtlSn = "7", year = 2026, month = 9, expand = "Y" });
            response.EnsureSuccessStatusCode();
            var bytes = await BoundedReadAsync(response.Content, 4 * 1024 * 1024);
            await WriteNewAsync(listPath, bytes);
        }
        ValidateDownloadList(listPath);

        var zipPath = Path.Combine(folder, ZipName);
        if (!File.Exists(zipPath))
        {
            var query = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["reqType"] = "ALLRDNM", ["ctprvnCd"] = "00", ["stdde"] = SourceVintage,
                ["fileName"] = ZipName, ["realFileName"] = "202608ALLRDNM00.zip",
                ["intFileNo"] = "0", ["intNum"] = "0", ["regYmd"] = "2026"
            };
            var url = "https://business.juso.go.kr/api/jst/download?" + string.Join("&", query.OrderBy(x => x.Key, StringComparer.Ordinal)
                .Select(x => Uri.EscapeDataString(x.Key) + "=" + Uri.EscapeDataString(x.Value)));
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Referrer = new Uri("https://business.juso.go.kr/jst/jstAddressDownload?menu=7");
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            Check(response.Content.Headers.ContentLength is > 0 and <= 200_000_000, "DownloadLengthHeader");
            var partial = zipPath + ".partial-" + Guid.NewGuid().ToString("N");
            try
            {
                await using var source = await response.Content.ReadAsStreamAsync();
                await using var target = new FileStream(partial, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1024 * 1024, true);
                var buffer = new byte[1024 * 1024];
                long total = 0;
                while (await source.ReadAsync(buffer) is var read && read > 0)
                {
                    total += read;
                    Check(total <= 200_000_000, "DownloadTooLarge");
                    await target.WriteAsync(buffer.AsMemory(0, read));
                }
                await target.FlushAsync();
                Check(total == ZipBytes, "DownloadedZipLength");
                Check(HashFile(partial).Equals(ZipHash, StringComparison.OrdinalIgnoreCase), "DownloadedZipHash");
                File.Move(partial, zipPath);
            }
            finally
            {
                if (File.Exists(partial)) File.Delete(partial);
            }
        }
        Check(new FileInfo(zipPath).Length == ZipBytes && HashFile(zipPath).Equals(ZipHash, StringComparison.OrdinalIgnoreCase), "FrozenZipDrift");

        ExtractExpected(zipPath, folder, SeoulName, SeoulHash);
        ExtractGuide(zipPath, folder);

        var acquisitionPath = Path.Combine(folder, AcquisitionName);
        if (!File.Exists(acquisitionPath))
        {
            var now = DateTimeOffset.UtcNow;
            now = DateTimeOffset.FromUnixTimeMilliseconds(now.ToUnixTimeMilliseconds());
            var receipt = new
            {
                schemaVersion = "sagajeong-building-address-acquisition.v1",
                collectedAtUtc = now,
                sourceVintage = SourceVintage,
                evidenceAsOf = "2026-08-31",
                provider = "행정안전부 주소기반산업지원서비스",
                datasetPage = "https://www.data.go.kr/data/15050424/fileData.do",
                downloadPage = "https://business.juso.go.kr/jst/jstAddressDownload?menu=7",
                license = "이용허락범위 제한 없음",
                sourceFiles = new[]
                {
                    new { name = ZipName, bytes = ZipBytes, sha256 = ZipHash.ToLowerInvariant(), contentType = "application/zip" },
                    new { name = SeoulName, bytes = new FileInfo(Path.Combine(folder, SeoulName)).Length, sha256 = SeoulHash.ToLowerInvariant(), contentType = "text/plain; charset=MS949" },
                    new { name = "building-db-guide.pdf", bytes = new FileInfo(Path.Combine(folder, "building-db-guide.pdf")).Length, sha256 = GuideHash.ToLowerInvariant(), contentType = "application/pdf" },
                    new { name = MetadataName, bytes = new FileInfo(metadataPath).Length, sha256 = HashFile(metadataPath).ToLowerInvariant(), contentType = "application/ld+json" },
                    new { name = DownloadListName, bytes = new FileInfo(listPath).Length, sha256 = HashFile(listPath).ToLowerInvariant(), contentType = "application/json" }
                },
                sourceSchema = new { delimiter = "|", encoding = "MS949", buildingColumns = 31, buildingManagementNumberColumn = 16 },
                boundary = "PrivateLocalSource;MonthlyFullSnapshot;NoDailyChanges;NoDetailedAddressProjection;NoDistribution"
            };
            await Save(folder, AcquisitionName, receipt);
        }
        ValidateAcquisition(folder);
    }

    private static async Task<byte[]> BoundedGetAsync(HttpClient client, string url, int maxBytes)
    {
        using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();
        return await BoundedReadAsync(response.Content, maxBytes);
    }

    private static async Task<byte[]> BoundedReadAsync(HttpContent content, int maxBytes)
    {
        if (content.Headers.ContentLength is > 0 && content.Headers.ContentLength > maxBytes)
            throw new InvalidDataException("SagajeongBuildingAddress:ResponseTooLarge");
        await using var source = await content.ReadAsStreamAsync();
        using var output = new MemoryStream();
        var buffer = new byte[64 * 1024];
        while (await source.ReadAsync(buffer) is var read && read > 0)
        {
            Check(output.Length + read <= maxBytes, "ResponseTooLarge");
            await output.WriteAsync(buffer.AsMemory(0, read));
        }
        return output.ToArray();
    }

    private static async Task WriteNewAsync(string path, byte[] bytes)
    {
        면목동사업체수집.Safe(path);
        await using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await stream.WriteAsync(bytes);
    }

    private static void ValidateDownloadList(string path)
    {
        var list = Read(path);
        Check(S(list, "status") == "200", "DownloadListStatus");
        var rows = list["results"]?["allMonthFileList"]?.AsArray() ?? throw new InvalidDataException("SagajeongBuildingAddress:DownloadListShape");
        var row = rows.SingleOrDefault(x => S(x, "crtrYm") == SourceVintage && S(x, "ctpvClsfCd") == "00");
        Check(row != null && S(row, "isExist") == "Y" && S(row, "fileNm") == ZipName && S(row, "tmprFileNm") == "202608ALLRDNM00.zip", "DownloadListVintage");
    }

    private static JsonNode ValidateAcquisition(string folder)
    {
        var acquisition = Read(Path.Combine(folder, AcquisitionName));
        Check(S(acquisition, "schemaVersion") == "sagajeong-building-address-acquisition.v1", "AcquisitionSchema");
        Check(S(acquisition, "sourceVintage") == SourceVintage && S(acquisition, "license") == "이용허락범위 제한 없음", "AcquisitionIdentity");
        Check(HashFile(Path.Combine(folder, ZipName)).Equals(ZipHash, StringComparison.OrdinalIgnoreCase), "AcquisitionZip");
        Check(HashFile(Path.Combine(folder, SeoulName)).Equals(SeoulHash, StringComparison.OrdinalIgnoreCase), "AcquisitionSeoul");
        Check(HashFile(Path.Combine(folder, "building-db-guide.pdf")).Equals(GuideHash, StringComparison.OrdinalIgnoreCase), "AcquisitionGuide");
        foreach (var file in acquisition["sourceFiles"]!.AsArray())
        {
            var name = S(file, "name");
            Check(Path.GetFileName(name) == name, "AcquisitionFileName");
            Check(File.Exists(Path.Combine(folder, name)), "AcquisitionFileMissing:" + name);
            Check(HashFile(Path.Combine(folder, name)).Equals(S(file, "sha256"), StringComparison.OrdinalIgnoreCase), "AcquisitionFileDrift:" + name);
        }
        return acquisition;
    }

    private static void ExtractExpected(string zipPath, string folder, string entryName, string expectedHash)
    {
        var output = Path.Combine(folder, entryName);
        if (!File.Exists(output))
        {
            using var archive = ZipFile.OpenRead(zipPath);
            var entry = archive.Entries.Single(x => x.FullName == entryName);
            using var source = entry.Open();
            using var target = new FileStream(output, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            source.CopyTo(target);
        }
        Check(HashFile(output).Equals(expectedHash, StringComparison.OrdinalIgnoreCase), "ExtractedFileDrift:" + entryName);
    }

    private static void ExtractGuide(string zipPath, string folder)
    {
        const string outputName = "building-db-guide.pdf";
        var output = Path.Combine(folder, outputName);
        if (!File.Exists(output))
        {
            using var archive = ZipFile.OpenRead(zipPath);
            var entry = archive.Entries.Single(x => x.FullName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase));
            using var source = entry.Open();
            using var target = new FileStream(output, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            source.CopyTo(target);
        }
        Check(HashFile(output).Equals(GuideHash, StringComparison.OrdinalIgnoreCase), "GuideDrift");
    }

    private static Projected Build(string root, string folder)
    {
        var acquisition = ValidateAcquisition(folder);
        var map = Read(MapPath);
        Check(HashFile(MapPath).Equals(MapHash, StringComparison.OrdinalIgnoreCase), "MapHash");
        Check(S(map, "revision") == "sagajeong-reference.r3" && S(map, "rawSha256") == OsmHash, "MapRevision");
        var osmPath = Path.Combine(root, OsmPath);
        Check(HashFile(osmPath).Equals(OsmHash, StringComparison.OrdinalIgnoreCase), "OsmHash");

        var official = LoadOfficial(Path.Combine(folder, SeoulName));
        var officialByAddress = official.GroupBy(x => x.MatchKey, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.OrderBy(y => y.BuildingManagementNumber, StringComparer.Ordinal).ToArray(), StringComparer.Ordinal);
        var contained = LoadContainedOsmAddressCandidates(osmPath, map["buildings"]!.AsArray());
        var preliminary = new Dictionary<string, Candidate?>(StringComparer.Ordinal);
        foreach (var building in map["buildings"]!.AsArray())
        {
            var id = S(building, "id");
            Check(id.Length > 0 && !preliminary.ContainsKey(id), "DuplicateBuilding");
            preliminary[id] = DirectCandidate(building!) ?? contained.GetValueOrDefault(id);
        }

        foreach (var building in map["buildings"]!.AsArray())
        {
            var id = S(building, "id");
            if (preliminary[id] != null) continue;
            var name = S(building, "name").Trim();
            if (name.Length < 3) continue;
            var nameMatches = official.Where(x => OfficialNames(x).Any(n => n.Contains(name, StringComparison.OrdinalIgnoreCase))).ToArray();
            var addressKeys = nameMatches.Select(x => x.MatchKey).Distinct(StringComparer.Ordinal).ToArray();
            if (addressKeys.Length != 1) continue;
            var match = nameMatches.OrderBy(x => x.BuildingManagementNumber, StringComparer.Ordinal).First();
            preliminary[id] = new Candidate(match.MatchKey, match.CanonicalRoadAddress, "UniqueBuildingNameContainsOfficialRecord", "osm-way-name:" + name, 0.90m, "");
        }

        var dioramaMemberCounts = preliminary.Values.Where(x => x != null)
            .GroupBy(x => x!.MatchKey, StringComparer.Ordinal).ToDictionary(x => x.Key, x => x.Count(), StringComparer.Ordinal);
        var output = new JsonArray();
        foreach (var building in map["buildings"]!.AsArray().OrderBy(x => S(x, "id"), StringComparer.Ordinal))
        {
            var id = S(building, "id");
            var candidate = preliminary[id];
            var officialMatches = candidate != null && officialByAddress.TryGetValue(candidate.MatchKey, out var found) ? found : [];
            var state = ResolutionState(candidate, officialMatches.Length, candidate == null ? 0 : dioramaMemberCounts[candidate.MatchKey]);
            var officialIds = officialMatches.Select(x => x.BuildingManagementNumber).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            var compositeKeys = officialMatches.Select(x => x.OfficialCompositeKey).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            var addressStableId = compositeKeys.Length == 0 ? "" : "road-address:kr:" + ShortHash(string.Join("|", compositeKeys));
            var unresolvedReason = state switch
            {
                "RoadAddressCandidate" => "OfficialMonthlySnapshotHasNoExactAddress",
                "Unresolved" when S(building, "buildingKind") == "roof" => "RoofOrAccessoryStructureWithoutAddressEvidence",
                "Unresolved" => candidate?.MissingReason is { Length: > 0 } reason ? reason : "NoRoadAddressEvidence",
                _ => ""
            };
            output.Add(new JsonObject
            {
                ["dioramaBuildingStableId"] = id,
                ["resolutionState"] = state,
                ["addressStableId"] = addressStableId,
                ["canonicalRoadAddress"] = candidate?.CanonicalRoadAddress ?? "",
                ["officialCompositeKeys"] = JsonSerializer.SerializeToNode(compositeKeys),
                ["officialBuildingManagementNumbers"] = JsonSerializer.SerializeToNode(officialIds),
                ["officialRecordCount"] = officialMatches.Length,
                ["dioramaAddressMemberCount"] = candidate == null ? 0 : dioramaMemberCounts[candidate.MatchKey],
                ["assignmentMethod"] = candidate?.Method ?? "None",
                ["evidenceRef"] = candidate?.EvidenceRef ?? "",
                ["confidence"] = candidate?.Confidence ?? 0m,
                ["administrativeDongCodes"] = JsonSerializer.SerializeToNode(officialMatches.Select(x => x.AdministrativeDongCode).Where(x => x.Length > 0).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray()),
                ["administrativeDongNames"] = JsonSerializer.SerializeToNode(officialMatches.Select(x => x.AdministrativeDongName).Where(x => x.Length > 0).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray()),
                ["unresolvedReason"] = unresolvedReason,
                ["sourceVintage"] = SourceVintage,
                ["sourceHash"] = ZipHash.ToLowerInvariant(),
                ["mapRevision"] = S(map, "revision"),
                ["mapHash"] = MapHash.ToLowerInvariant(),
                ["distributionApproved"] = false,
                ["deliveryEligible"] = false,
                ["priceObservationEligible"] = false,
                ["unityApplyAllowed"] = false
            });
        }
        Check(output.Count == 602 && output.Select(x => S(x, "dioramaBuildingStableId")).Distinct(StringComparer.Ordinal).Count() == 602, "ProjectionCount");
        var allowed = new[] { "OfficialIndividualAddress", "OfficialSharedComplexAddress", "RoadAddressCandidate", "NoIndependentRoadAddress", "Unresolved" };
        Check(output.All(x => allowed.Contains(S(x, "resolutionState"), StringComparer.Ordinal)), "ResolutionState");
        Check(output.All(x => !x!["distributionApproved"]!.GetValue<bool>() && !x["deliveryEligible"]!.GetValue<bool>() && !x["priceObservationEligible"]!.GetValue<bool>() && !x["unityApplyAllowed"]!.GetValue<bool>()), "AuthorityLeak");

        var summary = new JsonObject
        {
            ["totalBuildings"] = output.Count,
            ["officialIndividualAddress"] = output.Count(x => S(x, "resolutionState") == "OfficialIndividualAddress"),
            ["officialSharedComplexAddress"] = output.Count(x => S(x, "resolutionState") == "OfficialSharedComplexAddress"),
            ["officialResolvedBuildings"] = output.Count(x => S(x, "resolutionState").StartsWith("Official", StringComparison.Ordinal)),
            ["roadAddressCandidate"] = output.Count(x => S(x, "resolutionState") == "RoadAddressCandidate"),
            ["noIndependentRoadAddress"] = output.Count(x => S(x, "resolutionState") == "NoIndependentRoadAddress"),
            ["unresolved"] = output.Count(x => S(x, "resolutionState") == "Unresolved"),
            ["sharedAddressGroups"] = output.Where(x => S(x, "resolutionState") == "OfficialSharedComplexAddress").Select(x => S(x, "addressStableId")).Distinct(StringComparer.Ordinal).Count(),
            ["directOsmCandidates"] = preliminary.Values.Count(x => x?.Method == "ExactOsmRoadAddressToOfficialCompositeKey"),
            ["containedOsmAddressCandidates"] = preliminary.Values.Count(x => x?.Method == "ContainedOsmAddressNodeToOfficialCompositeKey"),
            ["uniqueOfficialNameCandidates"] = preliminary.Values.Count(x => x?.Method == "UniqueBuildingNameContainsOfficialRecord"),
            ["jungnangOfficialRecords"] = official.Count
        };
        Check(summary["officialResolvedBuildings"]!.GetValue<int>() + summary["roadAddressCandidate"]!.GetValue<int>() + summary["noIndependentRoadAddress"]!.GetValue<int>() + summary["unresolved"]!.GetValue<int>() == 602, "SummaryCount");

        var collectedAt = DateTimeOffset.Parse(S(acquisition, "collectedAtUtc"), CultureInfo.InvariantCulture);
        var document = new JsonObject
        {
            ["schemaVersion"] = "building-address-assignment.v1",
            ["revision"] = Revision,
            ["regionStableId"] = Region,
            ["sourceVintage"] = SourceVintage,
            ["evidenceAsOf"] = "2026-08-31",
            ["sourceHash"] = ZipHash.ToLowerInvariant(),
            ["mapRevision"] = S(map, "revision"),
            ["mapHash"] = MapHash.ToLowerInvariant(),
            ["osmRawHash"] = OsmHash.ToLowerInvariant(),
            ["sharedAddressPolicy"] = "OfficialSharedComplexAddress",
            ["summary"] = summary.DeepClone(),
            ["buildings"] = output
        };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(document, Json);
        var assignmentHash = Convert.ToHexString(SHA256.HashData(bytes));
        var manifest = new JsonObject
        {
            ["schemaVersion"] = "building-address-assignment-manifest.v1",
            ["revision"] = Revision,
            ["regionStableId"] = Region,
            ["sourceVintage"] = SourceVintage,
            ["sourceHash"] = ZipHash.ToLowerInvariant(),
            ["assignmentSha256"] = assignmentHash.ToLowerInvariant(),
            ["summary"] = summary.DeepClone(),
            ["observationPresentationOnly"] = true,
            ["distributionApproved"] = false,
            ["traversalReady"] = false,
            ["gameplayReady"] = false,
            ["boundary"] = Boundary
        };
        return new Projected(document, manifest, output, summary, assignmentHash, collectedAt);
    }

    private static List<OfficialAddress> LoadOfficial(string path)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        using var reader = new StreamReader(path, Encoding.GetEncoding(949), false, 1024 * 1024);
        var output = new List<OfficialAddress>(30_000);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            var fields = line.Split('|');
            Check(fields.Length >= 31, "OfficialColumnCount");
            if (fields[2] != "중랑구") continue;
            var main = ParseNumber(fields[11], "OfficialMainNumber");
            var sub = ParseNumber(fields[12], "OfficialSubNumber");
            var number = main.ToString(CultureInfo.InvariantCulture) + (sub == 0 ? "" : "-" + sub.ToString(CultureInfo.InvariantCulture));
            var prefix = fields[10] switch { "0" => "", "1" => "지하 ", "2" => "공중 ", "3" => "수상 ", _ => throw new InvalidDataException("SagajeongBuildingAddress:OfficialUndergroundCode") };
            var canonical = $"서울특별시 중랑구 {fields[9]} {prefix}{number}";
            var matchKey = MatchKey(fields[9], number);
            var id = fields[15];
            Check(Regex.IsMatch(id, @"^\d{25}$") && ids.Add(id), "OfficialBuildingManagementNumber");
            var composite = string.Join("|", fields[0], fields[8], fields[10], main.ToString("D5", CultureInfo.InvariantCulture), sub.ToString("D5", CultureInfo.InvariantCulture));
            output.Add(new OfficialAddress(matchKey, canonical, composite, id, fields[0], fields[17], fields[18], fields[13], fields[14], fields[25], fields[26], fields[28], fields[23]));
        }
        Check(output.Count > 20_000, "JungnangOfficialCoverage");
        return output;
    }

    private static int ParseNumber(string value, string code)
    {
        Check(int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var number) && number >= 0, code);
        return number;
    }

    private static Candidate? DirectCandidate(JsonNode building)
    {
        var street = S(building, "street").Trim();
        var number = NormalizeHouseNumber(S(building, "houseNumber"));
        return street.Length == 0 || number.Length == 0 ? null : new Candidate(
            MatchKey(street, number), $"서울특별시 중랑구 {street} {number}",
            "ExactOsmRoadAddressToOfficialCompositeKey", "map-building-address:" + S(building, "id"), 0.98m, "");
    }

    private static Dictionary<string, Candidate?> LoadContainedOsmAddressCandidates(string osmPath, JsonArray buildings)
    {
        var document = XDocument.Load(osmPath, LoadOptions.None);
        var root = document.Root ?? throw new InvalidDataException("SagajeongBuildingAddress:OsmRoot");
        var nodes = root.Elements("node").ToDictionary(x => x.Attribute("id")!.Value, x => new
        {
            X = double.Parse(x.Attribute("lon")!.Value, CultureInfo.InvariantCulture),
            Y = double.Parse(x.Attribute("lat")!.Value, CultureInfo.InvariantCulture),
            Tags = x.Elements("tag").ToDictionary(t => t.Attribute("k")!.Value, t => t.Attribute("v")!.Value, StringComparer.Ordinal)
        }, StringComparer.Ordinal);
        var ways = root.Elements("way").ToDictionary(x => x.Attribute("id")!.Value, x => x.Elements("nd").Select(n => n.Attribute("ref")!.Value).ToArray(), StringComparer.Ordinal);
        var addressNodes = nodes.Where(x => x.Value.Tags.TryGetValue("addr:street", out var street) && street.Length > 0
                && x.Value.Tags.TryGetValue("addr:housenumber", out var number) && NormalizeHouseNumber(number).Length > 0)
            .ToArray();
        var output = new Dictionary<string, Candidate?>(StringComparer.Ordinal);
        foreach (var building in buildings.Where(x => DirectCandidate(x!) == null))
        {
            var id = S(building, "id");
            var wayId = id.StartsWith("osm:way:", StringComparison.Ordinal) ? id[8..] : "";
            Check(ways.TryGetValue(wayId, out var refs) && refs.Length >= 4, "MissingOsmWay:" + id);
            var polygon = refs!.Select(x => (nodes[x].X, nodes[x].Y)).ToArray();
            var candidates = addressNodes.Where(x => PointInPolygon(x.Value.X, x.Value.Y, polygon))
                .Select(x => new
                {
                    NodeId = x.Key,
                    Street = x.Value.Tags["addr:street"].Trim(),
                    Number = NormalizeHouseNumber(x.Value.Tags["addr:housenumber"])
                }).GroupBy(x => MatchKey(x.Street, x.Number), StringComparer.Ordinal).ToArray();
            if (candidates.Length == 1)
            {
                var candidate = candidates[0].OrderBy(x => x.NodeId, StringComparer.Ordinal).First();
                output[id] = new Candidate(candidates[0].Key, $"서울특별시 중랑구 {candidate.Street} {candidate.Number}",
                    "ContainedOsmAddressNodeToOfficialCompositeKey", "osm:node:" + candidate.NodeId, 0.94m, "");
            }
            else
            {
                output[id] = candidates.Length > 1
                    ? new Candidate("", "", "None", "", 0m, "MultipleContainedOsmAddressNodes")
                    : null;
            }
        }
        return output;
    }

    private static bool PointInPolygon(double x, double y, (double X, double Y)[] polygon)
    {
        var inside = false;
        for (var i = 0; i < polygon.Length; i++)
        {
            var a = polygon[(i + polygon.Length - 1) % polygon.Length];
            var b = polygon[i];
            if (PointOnSegment(x, y, a, b)) return true;
            if ((a.Y > y) != (b.Y > y) && x < (b.X - a.X) * (y - a.Y) / (b.Y - a.Y) + a.X)
                inside = !inside;
        }
        return inside;
    }

    private static bool PointOnSegment(double x, double y, (double X, double Y) a, (double X, double Y) b)
    {
        var cross = (x - a.X) * (b.Y - a.Y) - (y - a.Y) * (b.X - a.X);
        if (Math.Abs(cross) > 1e-12) return false;
        return x >= Math.Min(a.X, b.X) - 1e-12 && x <= Math.Max(a.X, b.X) + 1e-12
            && y >= Math.Min(a.Y, b.Y) - 1e-12 && y <= Math.Max(a.Y, b.Y) + 1e-12;
    }

    private static string ResolutionState(Candidate? candidate, int officialCount, int dioramaCount)
    {
        if (candidate == null || candidate.MatchKey.Length == 0) return "Unresolved";
        if (officialCount == 0) return "RoadAddressCandidate";
        return officialCount > 1 || dioramaCount > 1 ? "OfficialSharedComplexAddress" : "OfficialIndividualAddress";
    }

    private static IEnumerable<string> OfficialNames(OfficialAddress row)
        => new[] { row.BuildingRegisterName, row.DetailedBuildingName, row.SigunguBuildingName }.Where(x => x.Length > 0);

    private static string MatchKey(string street, string number) => street.Trim() + "|" + NormalizeHouseNumber(number);

    private static string NormalizeHouseNumber(string value)
    {
        value = value.Trim();
        if (!HouseNumber.IsMatch(value)) return "";
        var parts = value.Split('-');
        var main = int.Parse(parts[0], CultureInfo.InvariantCulture);
        var sub = parts.Length == 1 ? 0 : int.Parse(parts[1], CultureInfo.InvariantCulture);
        return main.ToString(CultureInfo.InvariantCulture) + (sub == 0 ? "" : "-" + sub.ToString(CultureInfo.InvariantCulture));
    }

    private static string ShortHash(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant()[..24];

    private static void WriteFrozen(string folder, string name, JsonNode value)
    {
        var path = Path.Combine(folder, name);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, Json);
        Check(bytes.Length <= 16 * 1024 * 1024, "ProjectionBudget");
        if (File.Exists(path))
        {
            Check(File.ReadAllBytes(path).SequenceEqual(bytes), "FrozenProjectionDrift:" + name);
            return;
        }
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        stream.Write(bytes);
    }

    private static void ValidateFrozenProjection(string folder, Projected projected)
    {
        var assignmentPath = Path.Combine(folder, AssignmentName);
        var manifestPath = Path.Combine(folder, ManifestName);
        Check(File.Exists(assignmentPath) && File.Exists(manifestPath), "ProjectionNotPrepared");
        Check(HashFile(assignmentPath).Equals(projected.AssignmentHash, StringComparison.OrdinalIgnoreCase), "AssignmentReplay");
        Check(JsonNode.DeepEquals(Read(assignmentPath), projected.Document), "AssignmentContentReplay");
        Check(JsonNode.DeepEquals(Read(manifestPath), projected.Manifest), "ManifestReplay");
    }

    private static async Task PersistAsync(string root, string folder, Projected projected, string mode, Dictionary<string, object?> result)
    {
        var hash = projected.AssignmentHash.ToLowerInvariant();
        var rows = projected.Buildings.Select(building =>
        {
            var id = S(building, "dioramaBuildingStableId");
            var state = S(building, "resolutionState");
            var dimension = "building=" + id + ";assignment-sha256=" + hash;
            var text = JsonSerializer.Serialize(building, Compact);
            Check(text.Length <= 3000, "PayloadBudget:" + id);
            return new 외부데이터정규화Record
            {
                SourceId = Source,
                DatasetId = Dataset,
                StableId = "building-address-assignment:" + id,
                RegionStableId = Region,
                MetricCode = "building-road-address-resolution",
                DimensionKey = dimension,
                RecordKey = 외부데이터RecordKey.Create(Source, Dataset, Region, "building-road-address-resolution", EvidenceAt, dimension),
                TextValue = text,
                NumericValue = null,
                UnitCode = "building-address-assignment",
                SourceVersion = SourceVintage + "-zip-sha256:" + ZipHash.ToLowerInvariant(),
                DataRevision = Revision,
                EvidenceAsOfUtc = EvidenceAt,
                CollectedAtUtc = projected.CollectedAt,
                FirstSeenAtUtc = projected.CollectedAt,
                LastSeenAtUtc = projected.CollectedAt,
                SpatialPrecisionCode = S(building, "assignmentMethod"),
                TemporalPrecisionCode = "month-end-full-snapshot",
                QualityCode = state.StartsWith("Official", StringComparison.Ordinal) ? "OfficialAddressCrossReference" : "PendingHumanReview",
                LimitationCode = Boundary
            };
        }).ToList();
        var keys = rows.Select(x => x.RecordKey).ToList();
        Check(rows.Count == 602 && keys.Distinct(StringComparer.Ordinal).Count() == 602, "PersistenceCount");
        var options = await 로컬공공자료Db.OptionsAsync(root);

        await using (var db = new PublicDataIngestionDbContext(options))
        {
            var existing = await db.NormalizedRecords.AsNoTracking().Include(x => x.RawSnapshot).Where(x => keys.Contains(x.RecordKey)).ToListAsync();
            Check(existing.All(a => rows.Any(b => Equivalent(a, b))), "ExistingConflict");
            if (mode == "apply" && existing.Count != rows.Count)
            {
                result["databaseWriteAttempted"] = true;
                await db.Database.OpenConnectionAsync();
                await using var command = db.Database.GetDbConnection().CreateCommand();
                command.CommandText = "SELECT GET_LOCK('mirror:public-data:sagajeong-building-address-r1',0)";
                Check(Convert.ToInt32(await command.ExecuteScalarAsync()) == 1, "ImportBusy");
                await using var transaction = await db.Database.BeginTransactionAsync();
                var registration = await new 평창군공공공간원본등록Service(db).RegisterFileAsync(
                    Path.Combine(folder, ZipName),
                    new(Source, Dataset, SourceVintage + "-zip-sha256:" + ZipHash.ToLowerInvariant(), Revision, EvidenceAt, "application/zip", Relative + "/" + ZipName));
                foreach (var row in rows) row.RawSnapshotId = registration.RawSnapshotId;
                var saved = await new EfExternalDataIngestionStore(db).UpsertNormalizedAsync(rows);
                Check(saved.UpdatedCount == 0, "UnexpectedUpdate");
                await transaction.CommitAsync();
                result["committed"] = true;
                result["inserted"] = saved.InsertedCount;
                result["existing"] = saved.ExistingCount;
                result["rawSnapshotInserted"] = registration.Inserted;
            }
            else
            {
                result["databaseWriteAttempted"] = false;
                result["committed"] = false;
                result["inserted"] = 0;
                result["existing"] = existing.Count;
            }
        }

        await using var verify = new PublicDataIngestionDbContext(options);
        var stored = await verify.NormalizedRecords.AsNoTracking().Include(x => x.RawSnapshot).Where(x => keys.Contains(x.RecordKey)).ToListAsync();
        if (mode != "preview") Check(stored.Count == 602, "ReadbackCount");
        Check(stored.All(a => rows.Any(b => Equivalent(a, b)) && a.RawSnapshot != null
            && a.RawSnapshot.ContentHashSha256.Equals(ZipHash, StringComparison.OrdinalIgnoreCase)
            && a.RawSnapshot.EvidenceAsOfUtc == EvidenceAt), "ReadbackMismatch");
        result["verifiedRows"] = stored.Count;
        result["rawSnapshotIds"] = stored.Select(x => x.RawSnapshotId).Distinct().Order().ToArray();
        result["firstId"] = stored.Count == 0 ? null : stored.Min(x => x.Id);
        result["lastId"] = stored.Count == 0 ? null : stored.Max(x => x.Id);
        result["target"] = "hongdal-mysql-1 / hongdal_dev";
        await Save(folder, mode + "-" + DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture) + ".json", result);
    }

    private static bool Equivalent(외부데이터정규화Record a, 외부데이터정규화Record b)
        => a.RecordKey == b.RecordKey && a.StableId == b.StableId && a.SourceId == b.SourceId && a.DatasetId == b.DatasetId
        && a.RegionStableId == b.RegionStableId && a.MetricCode == b.MetricCode && a.DimensionKey == b.DimensionKey
        && a.TextValue == b.TextValue && a.NumericValue == null && a.UnitCode == b.UnitCode && a.SourceVersion == b.SourceVersion
        && a.DataRevision == b.DataRevision && a.EvidenceAsOfUtc == b.EvidenceAsOfUtc && a.CollectedAtUtc == b.CollectedAtUtc
        && a.FirstSeenAtUtc == b.FirstSeenAtUtc && a.SpatialPrecisionCode == b.SpatialPrecisionCode
        && a.TemporalPrecisionCode == b.TemporalPrecisionCode && a.QualityCode == b.QualityCode && a.LimitationCode == b.LimitationCode;

    private static int SelfTest()
    {
        var count = 0;
        void Test(bool ok, string code) { Check(ok, "SelfTest:" + code); count++; }
        Test(NormalizeHouseNumber("003-02") == "3-2", "HouseNumber");
        Test(NormalizeHouseNumber("15-0") == "15", "ZeroSubNumber");
        Test(NormalizeHouseNumber("x") == "", "InvalidHouseNumber");
        Test(ResolutionState(new Candidate("a", "a", "m", "e", 1m, ""), 1, 1) == "OfficialIndividualAddress", "Individual");
        Test(ResolutionState(new Candidate("a", "a", "m", "e", 1m, ""), 2, 1) == "OfficialSharedComplexAddress", "OfficialShared");
        Test(ResolutionState(new Candidate("a", "a", "m", "e", 1m, ""), 1, 2) == "OfficialSharedComplexAddress", "DioramaShared");
        Test(ResolutionState(new Candidate("a", "a", "m", "e", 1m, ""), 0, 1) == "RoadAddressCandidate", "Candidate");
        Test(ResolutionState(null, 0, 0) == "Unresolved", "Unresolved");
        var square = new[] { (0d, 0d), (10d, 0d), (10d, 10d), (0d, 10d), (0d, 0d) };
        Test(PointInPolygon(5, 5, square), "PointInside");
        Test(PointInPolygon(0, 5, square), "PointBoundary");
        Test(!PointInPolygon(11, 5, square), "PointOutside");
        Test(MatchKey("사가정로", "003-00") == "사가정로|3", "MatchKey");
        Test(Boundary.Contains("NoNearestAddressInference", StringComparison.Ordinal), "NoNearestInference");
        Test(Boundary.Contains("NoDetailedAddress", StringComparison.Ordinal), "NoDetailedAddress");
        return count;
    }
}
