using System.Buffers.Binary;
using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Domain.PublicData;
using Ssalddel.Infrastructure.Persistence.PublicData;
using 살뜰.Services.External.PublicData.Korea;

// 동북서울 30개 행정동 r2 건물의 AL_D010 A2 PNU와 행정안전부 도로명주소 후보군만
// 비공개 검토 원장에 기록한다. 건물 정확 주소·출입구·필지 도형·배달·Unity 권위는 만들지 않는다.
internal static class 행정동건물주소후보
{
    private const string SourceId = "vworld-al-d010-mois-juso-private-review";
    private const string DatasetId = "northeast-seoul-admin-dong-building-address-candidate";
    private const string AlInputDatasetId = DatasetId + "-al-d010-input";
    private const string JusoInputDatasetId = DatasetId + "-juso-input";
    private const string ScopeDefinitionInputDatasetId = DatasetId + "-scope-definition-input";
    private const string ScopeManifestInputDatasetId = DatasetId + "-scope-manifest-input";
    private const string MetricCode = "administrative-dong-building-parcel-address-candidate";
    private const string Revision = "northeast-seoul-admin-dong-building-address-candidate.r1";
    private const string SchemaVersion = "administrative-dong-building-address-candidate.v1";
    private const string SourceVintage = "al-d010-20260809+juso-202608+admin-dong-r2";
    private const string ScopeStableId = "scope:administrative-dong-diorama:northeast-seoul-rider:r2";
    private const string ScopeRevision = "northeast-seoul-administrative-dong-dioramas.r2";
    private const string ScopeRelative = "artifacts/local/public-data/admin-dong-diorama-northeast-seoul-20260914-r2";
    private const string ScopeDefinitionRelative = "eng/world-seedbeds/administrative-dong-dioramas/northeast-seoul-rider.r2.json";
    private const string AlRelative = "artifacts/local/neighborhood-source-acquisition/AL_D010_11_20260809.zip";
    private const string JusoFolderRelative = "artifacts/local/public-data/sagajeong-building-address-20260914-r1";
    private const string JusoZipName = "202608_건물DB_전체분.zip";
    private const string JusoSeoulName = "build_seoul.txt";
    private const string AlSha256 = "674C5A9583996DD6B8946525EDAD8197BE79A634F1DB00D39E2B3133D0D2A755";
    private const string JusoZipSha256 = "4AA70C569AAF14F550313B5836346A1BA438491FB4CEC61877E615C9E602AFA7";
    private const string JusoSeoulSha256 = "285286D60409E1CC93898146952D9BC4A805C8F955A765C9F0D72E8A6EA16081";
    private const string ScopeDefinitionSha256 = "CAAFC2BC60AE4EBF3A0B8C1D2AD6B0143141FF706637BECC9B6743924AF9E58B";
    private const long AlBytes = 135_675_376;
    private const long JusoZipBytes = 149_263_413;
    private const long JusoSeoulBytes = 94_007_115;
    private const string CandidateState = "ParcelAddressCandidate";
    private const string MultipleState = "MultipleParcelAddressCandidates";
    private const string MissingState = "NoCandidateInFrozenJusoVintage";
    private const string RelationCode = "SameParcelCandidateNotBuildingIdentity";
    private const string Limitation = "PrivateReviewOnly;ParcelCandidateOnly;NoExactBuildingAddress;NoEntrance;NoParcelGeometry;NoDeliveryEligibility;NoUnity;DistributionApprovedFalse";
    private const int ExpectedAreaCount = 30;
    private const int ExpectedBuildingCount = 61_897;
    private const int ExpectedUniqueSourceBuildingCount = 61_874;
    private const int ExpectedRecordSuffixCount = 23;
    private const int ExpectedUniquePnuCount = 54_296;
    private const int ExpectedJusoTargetRowCount = 50_339;
    private const int ExpectedMatchedPnuCount = 45_251;
    private const int ExpectedCandidateBuildingCount = 51_227;
    private const int ExpectedMissingBuildingCount = 10_670;
    private const int ExpectedMultipleAddressGroupPnuCount = 361;
    private const int ExpectedMultipleManagementNumberPnuCount = 2_326;
    private const int ExpectedAlRecordCount = 695_761;
    private static readonly DateTimeOffset EvidenceAtUtc = new(2026, 8, 31, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset DerivedAtUtc = new(2026, 9, 14, 0, 0, 0, TimeSpan.Zero);
    private static readonly Regex BuildingStableIdPattern = new(
        @"^vworld:al-d010:(?<a1>[0-9]{28})(?<suffix>:record:[0-9]+)?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly JsonSerializerOptions CompactJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private sealed record ModuleBuilding(
        string AdministrativeAreaStableId,
        string BuildingStableId,
        string SourceBuildingIdentifierA1);

    private sealed record CandidateFact(
        string AdministrativeAreaStableId,
        string BuildingStableId,
        string SourceBuildingIdentifierA1,
        string ParcelIdentifierPnu,
        string CandidateStateCode,
        int AddressCandidateCount,
        int BuildingManagementCandidateCount,
        string AddressCandidateSetHashSha256,
        string BuildingManagementCandidateSetHashSha256,
        string SingleAddressCandidateStableId,
        string SingleBuildingManagementCandidateStableId);

    private sealed record Projection(
        IReadOnlyList<CandidateFact> Facts,
        string ProjectionHashSha256,
        string ScopeManifestSha256,
        int MultipleCandidateBuildingCount,
        int CandidateBuildingCount,
        int MissingBuildingCount,
        int UniquePnuCount,
        int MatchedPnuCount,
        int MultipleAddressGroupPnuCount,
        int MultipleManagementNumberPnuCount);

    private sealed record SourcePaths(
        string AlZip,
        string JusoZip,
        string JusoSeoul,
        string ScopeDefinition,
        string ScopeManifest,
        string ScopeDirectory);

    public static async Task RunAsync(string mode, string root, Dictionary<string, object?> result)
    {
        Require(mode is "self-test" or "preview" or "apply" or "verify", "Mode");
        root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        result["mode"] = mode;
        result["schemaVersion"] = SchemaVersion;
        result["dataRevision"] = Revision;
        result["reviewState"] = "PendingHumanReview";
        result["authority"] = AuthoritySummary();

        if (mode == "self-test")
        {
            result["selfTestsPassed"] = SelfTest();
            return;
        }

        var paths = ResolveAndValidatePaths(root);
        var projection = BuildProjection(paths);
        result["sourceVintage"] = SourceVintage;
        result["sourceHashes"] = new
        {
            alD010Sha256 = AlSha256.ToLowerInvariant(),
            jusoZipSha256 = JusoZipSha256.ToLowerInvariant(),
            jusoSeoulSha256 = JusoSeoulSha256.ToLowerInvariant(),
            scopeDefinitionSha256 = ScopeDefinitionSha256.ToLowerInvariant(),
            scopeManifestSha256 = projection.ScopeManifestSha256.ToLowerInvariant()
        };
        result["sourceByteLengths"] = new
        {
            alD010 = AlBytes,
            jusoZip = JusoZipBytes,
            jusoSeoul = JusoSeoulBytes,
            scopeDefinition = new FileInfo(paths.ScopeDefinition).Length,
            scopeManifest = new FileInfo(paths.ScopeManifest).Length
        };
        result["projectionHashSha256"] = projection.ProjectionHashSha256.ToLowerInvariant();
        result["summary"] = Summary(projection);

        if (mode == "preview")
        {
            result["databaseWriteAttempted"] = false;
            result["committed"] = false;
            return;
        }

        var rows = BuildRows(projection);
        var options = await 로컬공공자료Db.OptionsAsync(root);
        if (mode == "apply")
            await ApplyAsync(options, paths, projection, rows, result);
        else
        {
            result["databaseWriteAttempted"] = false;
            result["committed"] = false;
        }

        await VerifyReadbackAsync(options, projection, rows, result);
    }

    private static SourcePaths ResolveAndValidatePaths(string root)
    {
        var scopeDirectory = Path.Combine(root, ScopeRelative.Replace('/', Path.DirectorySeparatorChar));
        var paths = new SourcePaths(
            Path.Combine(root, AlRelative.Replace('/', Path.DirectorySeparatorChar)),
            Path.Combine(root, JusoFolderRelative.Replace('/', Path.DirectorySeparatorChar), JusoZipName),
            Path.Combine(root, JusoFolderRelative.Replace('/', Path.DirectorySeparatorChar), JusoSeoulName),
            Path.Combine(root, ScopeDefinitionRelative.Replace('/', Path.DirectorySeparatorChar)),
            Path.Combine(scopeDirectory, "scope-manifest.json"),
            scopeDirectory);
        Require(File.Exists(paths.AlZip) && File.Exists(paths.JusoZip) && File.Exists(paths.JusoSeoul)
            && File.Exists(paths.ScopeDefinition) && File.Exists(paths.ScopeManifest), "FrozenInputMissing");
        Require(new FileInfo(paths.AlZip).Length == AlBytes, "AlD010LengthDrift");
        Require(new FileInfo(paths.JusoZip).Length == JusoZipBytes, "JusoZipLengthDrift");
        Require(new FileInfo(paths.JusoSeoul).Length == JusoSeoulBytes, "JusoSeoulLengthDrift");
        Require(HashFile(paths.AlZip) == AlSha256, "AlD010HashDrift");
        Require(HashFile(paths.JusoZip) == JusoZipSha256, "JusoZipHashDrift");
        Require(HashFile(paths.JusoSeoul) == JusoSeoulSha256, "JusoSeoulHashDrift");
        Require(HashFile(paths.ScopeDefinition) == ScopeDefinitionSha256, "ScopeDefinitionHashDrift");
        return paths;
    }

    private static Projection BuildProjection(SourcePaths paths)
    {
        var scopeManifestHash = HashFile(paths.ScopeManifest);
        var moduleBuildings = LoadExactModuleBuildings(paths);
        var targetA1 = moduleBuildings.Select(item => item.SourceBuildingIdentifierA1)
            .ToHashSet(StringComparer.Ordinal);
        Require(targetA1.Count == ExpectedUniqueSourceBuildingCount, "UniqueSourceBuildingCount");

        var pnuByA1 = LoadPnuByA1(paths.AlZip, targetA1);
        Require(pnuByA1.Count == targetA1.Count, "AlD010TargetCoverage");
        var targetPnus = pnuByA1.Values.ToHashSet(StringComparer.Ordinal);
        Require(targetPnus.Count == ExpectedUniquePnuCount, "UniquePnuCount");

        var (addressGroups, managementNumbers, targetJusoRows) = LoadJusoCandidateSets(paths.JusoSeoul, targetPnus);
        Require(targetJusoRows == ExpectedJusoTargetRowCount, "JusoTargetRowCount");
        Require(addressGroups.Count == ExpectedMatchedPnuCount, "MatchedPnuCount");
        var multipleAddressPnus = addressGroups.Count(pair => pair.Value.Count > 1);
        var multipleManagementPnus = managementNumbers.Count(pair => pair.Value.Count > 1);
        Require(multipleAddressPnus == ExpectedMultipleAddressGroupPnuCount, "MultipleAddressGroupPnuCount");
        Require(multipleManagementPnus == ExpectedMultipleManagementNumberPnuCount, "MultipleManagementNumberPnuCount");

        var facts = new List<CandidateFact>(moduleBuildings.Count);
        foreach (var building in moduleBuildings.OrderBy(item => item.AdministrativeAreaStableId, StringComparer.Ordinal)
                     .ThenBy(item => item.BuildingStableId, StringComparer.Ordinal))
        {
            var pnu = pnuByA1[building.SourceBuildingIdentifierA1];
            var groups = addressGroups.GetValueOrDefault(pnu) ?? EmptySet();
            var management = managementNumbers.GetValueOrDefault(pnu) ?? EmptySet();
            var state = State(groups.Count);
            facts.Add(new CandidateFact(
                building.AdministrativeAreaStableId,
                building.BuildingStableId,
                building.SourceBuildingIdentifierA1,
                pnu,
                state,
                groups.Count,
                management.Count,
                SetHash(groups),
                SetHash(management),
                groups.Count == 1 ? StableReference("parcel-road-address-candidate", groups.Single()) : string.Empty,
                management.Count == 1 ? StableReference("building-management-candidate", management.Single()) : string.Empty));
        }

        Require(facts.Count == ExpectedBuildingCount, "ProjectionBuildingCount");
        var candidateBuildings = facts.Count(item => item.CandidateStateCode != MissingState);
        var missingBuildings = facts.Count(item => item.CandidateStateCode == MissingState);
        Require(candidateBuildings == ExpectedCandidateBuildingCount, "CandidateBuildingCount");
        Require(missingBuildings == ExpectedMissingBuildingCount, "MissingBuildingCount");
        Require(facts.All(item => item.CandidateStateCode is CandidateState or MultipleState or MissingState), "CandidateStateDomain");
        var projectionHash = ProjectionHash(facts, scopeManifestHash);
        return new Projection(
            facts,
            projectionHash,
            scopeManifestHash,
            facts.Count(item => item.CandidateStateCode == MultipleState),
            candidateBuildings,
            missingBuildings,
            targetPnus.Count,
            addressGroups.Count,
            multipleAddressPnus,
            multipleManagementPnus);
    }

    private static List<ModuleBuilding> LoadExactModuleBuildings(SourcePaths paths)
    {
        using var definition = JsonDocument.Parse(File.ReadAllBytes(paths.ScopeDefinition));
        var definitionRoot = definition.RootElement;
        Require(Text(definitionRoot, "schemaVersion") == "administrative-dong-diorama-generation-scope.v1", "ScopeDefinitionSchema");
        Require(Text(definitionRoot, "scopeStableId") == ScopeStableId && Text(definitionRoot, "revision") == ScopeRevision, "ScopeDefinitionIdentity");
        Require(Int(definitionRoot, "expectedAdministrativeAreaCount") == ExpectedAreaCount, "ScopeDefinitionAreaCount");
        var definitionAreas = definitionRoot.GetProperty("administrativeAreas").EnumerateArray()
            .Select(item => Text(item, "administrativeAreaStableId"))
            .ToHashSet(StringComparer.Ordinal);
        Require(definitionAreas.Count == ExpectedAreaCount, "ScopeDefinitionAreaSet");

        using var manifest = JsonDocument.Parse(File.ReadAllBytes(paths.ScopeManifest));
        var root = manifest.RootElement;
        Require(Text(root, "schemaVersion") == "administrative-dong-diorama-batch-scope.v1", "ScopeManifestSchema");
        Require(Text(root, "scopeStableId") == ScopeStableId && Text(root, "revision") == ScopeRevision, "ScopeManifestIdentity");
        Require(!Bool(root, "distributionApproved") && !Bool(root, "traversalReady") && !Bool(root, "gameplayReady"), "ScopeAuthorityUnexpected");
        Require(Text(root.GetProperty("toolchain"), "scopeDefinitionSha256") == ScopeDefinitionSha256, "ScopeDefinitionReceipt");
        var counts = root.GetProperty("counts");
        Require(Int(counts, "administrativeAreaCount") == ExpectedAreaCount && Int(counts, "buildingCount") == ExpectedBuildingCount, "ScopeManifestCounts");
        var alReceipt = root.GetProperty("sources").EnumerateArray().Single(item => Text(item, "datasetId") == "data-go-kr-15083092");
        Require(Text(alReceipt, "contentHashSha256") == AlSha256 && Text(alReceipt, "licenseCode") == "RightsConflictUnresolved", "AlD010Receipt");

        var entries = root.GetProperty("modules").EnumerateArray().ToArray();
        Require(entries.Length == ExpectedAreaCount, "ModuleEntryCount");
        var moduleAreas = new HashSet<string>(StringComparer.Ordinal);
        var buildingIds = new HashSet<string>(StringComparer.Ordinal);
        var output = new List<ModuleBuilding>(ExpectedBuildingCount);
        var suffixCount = 0;
        var safeRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(paths.ScopeDirectory)) + Path.DirectorySeparatorChar;
        foreach (var entry in entries.OrderBy(item => Text(item, "administrativeAreaStableId"), StringComparer.Ordinal))
        {
            var area = Text(entry, "administrativeAreaStableId");
            Require(moduleAreas.Add(area), "DuplicateModuleArea");
            var relative = Text(entry, "relativePath");
            Require(relative.Replace('\\', '/').StartsWith("modules/", StringComparison.Ordinal), "ModuleRelativePath");
            var modulePath = Path.GetFullPath(Path.Combine(paths.ScopeDirectory, relative.Replace('/', Path.DirectorySeparatorChar)));
            Require(modulePath.StartsWith(safeRoot, StringComparison.OrdinalIgnoreCase) && File.Exists(modulePath), "ModulePathEscapeOrMissing");
            Require(HashFile(modulePath) == Text(entry, "sha256"), "ModuleHashDrift");
            using var module = JsonDocument.Parse(File.ReadAllBytes(modulePath));
            var moduleRoot = module.RootElement;
            Require(Text(moduleRoot, "schemaVersion") == "administrative-dong-diorama-batch-input.v1", "ModuleSchema");
            Require(Text(moduleRoot, "administrativeAreaStableId") == area, "ModuleAreaIdentity");
            Require(Text(moduleRoot, "contentHashSha256") == Text(entry, "contentHashSha256"), "ModuleContentHashReceipt");
            var buildings = moduleRoot.GetProperty("buildings");
            Require(buildings.GetArrayLength() == Int(entry, "buildingCount"), "ModuleBuildingCount");
            foreach (var building in buildings.EnumerateArray())
            {
                var id = Text(building, "buildingStableId");
                Require(buildingIds.Add(id), "DuplicateModuleBuilding");
                var match = BuildingStableIdPattern.Match(id);
                Require(match.Success, "ModuleBuildingStableIdContract");
                if (match.Groups["suffix"].Success) suffixCount++;
                output.Add(new ModuleBuilding(area, id, match.Groups["a1"].Value));
            }
        }
        Require(moduleAreas.SetEquals(definitionAreas), "ExactAdministrativeAreaSet");
        Require(output.Count == ExpectedBuildingCount && buildingIds.Count == ExpectedBuildingCount, "ExactBuildingSet");
        Require(suffixCount == ExpectedRecordSuffixCount, "RecordSuffixCount");
        return output;
    }

    private static Dictionary<string, string> LoadPnuByA1(string zipPath, HashSet<string> targetA1)
    {
        using var archive = ZipFile.OpenRead(zipPath);
        var entries = archive.Entries.Where(item => item.FullName.EndsWith(".dbf", StringComparison.OrdinalIgnoreCase)).ToArray();
        Require(entries.Length == 1 && entries[0].FullName == "AL_D010_11_20260809.dbf", "AlD010DbfEntry");
        using var stream = entries[0].Open();
        using var reader = new SelectiveDbfReader(stream);
        Require(reader.RecordCount == ExpectedAlRecordCount, "AlD010RecordCount");
        var output = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var row in reader.Read("A1", "A2"))
        {
            var a1 = row[0];
            if (!targetA1.Contains(a1)) continue;
            var pnu = row[1];
            Require(IsDigits(pnu, 19), "AlD010PnuContract");
            if (output.TryGetValue(a1, out var existing))
                Require(existing == pnu, "AlD010A1MultiplePnu");
            else
                output.Add(a1, pnu);
        }
        return output;
    }

    private static (Dictionary<string, HashSet<string>> AddressGroups, Dictionary<string, HashSet<string>> ManagementNumbers, int TargetRows)
        LoadJusoCandidateSets(string path, HashSet<string> targetPnus)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var targetLegalCodes = targetPnus.Select(pnu => pnu[..10]).ToHashSet(StringComparer.Ordinal);
        var addressGroups = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var managementNumbers = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var targetRows = 0;
        using var reader = new StreamReader(path, Encoding.GetEncoding(949), false, 1024 * 1024);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            var fields = line.Split('|');
            Require(fields.Length >= 31, "JusoColumnCount");
            if (!targetLegalCodes.Contains(fields[0])) continue;
            var pnu = PnuFromJuso(fields[0], fields[5], fields[6], fields[7]);
            if (!targetPnus.Contains(pnu)) continue;
            var roadCode = fields[8].Trim();
            var undergroundCode = fields[10].Trim();
            Require(IsDigits(roadCode, 12), "JusoRoadCodeContract");
            Require(undergroundCode is "0" or "1" or "2" or "3", "JusoUndergroundCodeContract");
            var buildingMain = ParseUnsigned(fields[11], 99_999, "JusoBuildingMainNumber");
            var buildingSub = ParseUnsigned(fields[12], 99_999, "JusoBuildingSubNumber");
            var managementNumber = fields[15].Trim();
            Require(IsDigits(managementNumber, 25), "JusoBuildingManagementNumberContract");
            var group = string.Join('|', fields[0], roadCode, undergroundCode,
                buildingMain.ToString("D5", CultureInfo.InvariantCulture),
                buildingSub.ToString("D5", CultureInfo.InvariantCulture));
            SetFor(addressGroups, pnu).Add(group);
            SetFor(managementNumbers, pnu).Add(managementNumber);
            targetRows++;
        }
        Require(addressGroups.Keys.All(managementNumbers.ContainsKey) && managementNumbers.Keys.All(addressGroups.ContainsKey), "JusoCandidateSetKeyMismatch");
        return (addressGroups, managementNumbers, targetRows);
    }

    private static List<외부데이터정규화Record> BuildRows(Projection projection)
    {
        var rows = new List<외부데이터정규화Record>(projection.Facts.Count);
        foreach (var fact in projection.Facts)
        {
            var payload = JsonSerializer.Serialize(new
            {
                schemaVersion = SchemaVersion,
                fact.AdministrativeAreaStableId,
                fact.BuildingStableId,
                fact.SourceBuildingIdentifierA1,
                fact.ParcelIdentifierPnu,
                candidateState = fact.CandidateStateCode,
                fact.AddressCandidateCount,
                fact.BuildingManagementCandidateCount,
                fact.AddressCandidateSetHashSha256,
                fact.BuildingManagementCandidateSetHashSha256,
                fact.SingleAddressCandidateStableId,
                fact.SingleBuildingManagementCandidateStableId,
                relationCode = RelationCode,
                reviewState = "PendingHumanReview",
                sourceVintage = SourceVintage,
                sourceHashes = new
                {
                    alD010Sha256 = AlSha256.ToLowerInvariant(),
                    jusoZipSha256 = JusoZipSha256.ToLowerInvariant(),
                    jusoSeoulSha256 = JusoSeoulSha256.ToLowerInvariant(),
                    scopeDefinitionSha256 = ScopeDefinitionSha256.ToLowerInvariant(),
                    scopeManifestSha256 = projection.ScopeManifestSha256.ToLowerInvariant()
                },
                projectionHashSha256 = projection.ProjectionHashSha256.ToLowerInvariant(),
                exactBuildingAddressEstablished = false,
                officialAddressClaimed = false,
                entranceEstablished = false,
                parcelGeometryEstablished = false,
                distributionApproved = false,
                deliveryEligible = false,
                traversalReady = false,
                gameplayReady = false,
                unityApplyAllowed = false
            }, CompactJson);
            Require(payload.Length <= 2_000, "NormalizedPayloadBudget");
            var dimension = "building=" + fact.BuildingStableId;
            rows.Add(new 외부데이터정규화Record
            {
                RecordKey = 외부데이터RecordKey.Create(SourceId, DatasetId, fact.AdministrativeAreaStableId, MetricCode, EvidenceAtUtc, dimension),
                StableId = "administrative-dong-building-address-candidate:" + fact.BuildingStableId,
                SourceId = SourceId,
                DatasetId = DatasetId,
                RegionStableId = fact.AdministrativeAreaStableId,
                MetricCode = MetricCode,
                NumericValue = null,
                TextValue = payload,
                UnitCode = "building-parcel-address-candidate",
                EvidenceAsOfUtc = EvidenceAtUtc,
                CollectedAtUtc = DerivedAtUtc,
                SpatialPrecisionCode = "source-parcel-identifier-candidate",
                TemporalPrecisionCode = "frozen-monthly-cross-reference",
                QualityCode = "PendingHumanReview",
                LimitationCode = Limitation,
                DimensionKey = dimension,
                SourceVersion = SourceVintage + ";projection-sha256=" + projection.ProjectionHashSha256.ToLowerInvariant(),
                DataRevision = Revision,
                FirstSeenAtUtc = DerivedAtUtc,
                LastSeenAtUtc = DerivedAtUtc
            });
        }
        Require(rows.Count == ExpectedBuildingCount && rows.Select(row => row.RecordKey).Distinct(StringComparer.Ordinal).Count() == ExpectedBuildingCount, "NormalizedExactSet");
        return rows;
    }

    private static async Task ApplyAsync(
        DbContextOptions<PublicDataIngestionDbContext> options,
        SourcePaths paths,
        Projection projection,
        IReadOnlyList<외부데이터정규화Record> rows,
        Dictionary<string, object?> result)
    {
        await using (var preflight = new PublicDataIngestionDbContext(options))
        {
            var existing = await LoadScopedRowsAsync(preflight);
            await ValidateExistingAsync(preflight, existing, rows, requireComplete: false);
            result["beforeCount"] = existing.Count;
            if (existing.Count == rows.Count)
            {
                result["databaseWriteAttempted"] = false;
                result["committed"] = false;
                result["inserted"] = 0;
                result["existing"] = existing.Count;
                result["updated"] = 0;
                result["rawSnapshotInserted"] = 0;
                return;
            }
        }

        await using var db = new PublicDataIngestionDbContext(options);
        await db.Database.OpenConnectionAsync();
        var locked = false;
        try
        {
            await using (var command = db.Database.GetDbConnection().CreateCommand())
            {
                command.CommandText = "SELECT GET_LOCK('mirror:public-data:admin-dong-address-candidate-r1',0)";
                locked = Convert.ToInt32(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture) == 1;
            }
            Require(locked, "ImportBusy");
            var lockedExisting = await LoadScopedRowsAsync(db);
            await ValidateExistingAsync(db, lockedExisting, rows, requireComplete: false);
            if (lockedExisting.Count == rows.Count)
            {
                result["databaseWriteAttempted"] = false;
                result["committed"] = false;
                result["inserted"] = 0;
                result["existing"] = lockedExisting.Count;
                result["updated"] = 0;
                result["rawSnapshotInserted"] = 0;
                return;
            }

            result["databaseWriteAttempted"] = true;
            await using var transaction = await db.Database.BeginTransactionAsync();
            var registrationService = new 평창군공공공간원본등록Service(db);
            var al = await registrationService.RegisterFileAsync(paths.AlZip,
                Registration(AlInputDatasetId, "AL_D010:Seoul:20260809", "application/zip", AlRelative));
            var juso = await registrationService.RegisterFileAsync(paths.JusoZip,
                Registration(JusoInputDatasetId, "JUSO-building-db:202608", "application/zip", JusoFolderRelative + "/" + JusoZipName));
            var scopeDefinition = await registrationService.RegisterFileAsync(paths.ScopeDefinition,
                Registration(ScopeDefinitionInputDatasetId, ScopeRevision, "application/json", ScopeDefinitionRelative));
            var scopeManifest = await registrationService.RegisterFileAsync(paths.ScopeManifest,
                Registration(ScopeManifestInputDatasetId, ScopeRevision, "application/json", ScopeRelative + "/scope-manifest.json"));
            Require(al.SourceHashSha256.Equals(AlSha256, StringComparison.OrdinalIgnoreCase), "AlSnapshotHash");
            Require(juso.SourceHashSha256.Equals(JusoZipSha256, StringComparison.OrdinalIgnoreCase), "JusoSnapshotHash");
            Require(scopeDefinition.SourceHashSha256.Equals(ScopeDefinitionSha256, StringComparison.OrdinalIgnoreCase), "ScopeDefinitionSnapshotHash");
            Require(scopeManifest.SourceHashSha256.Equals(projection.ScopeManifestSha256, StringComparison.OrdinalIgnoreCase), "ScopeManifestSnapshotHash");
            foreach (var row in rows) row.RawSnapshotId = juso.RawSnapshotId;

            var inserted = 0;
            var existing = 0;
            foreach (var batch in rows.Chunk(500))
            {
                var saved = await new EfExternalDataIngestionStore(db).UpsertNormalizedAsync(batch);
                Require(saved.UpdatedCount == 0, "UnexpectedNormalizedUpdate");
                inserted += saved.InsertedCount;
                existing += saved.ExistingCount;
                db.ChangeTracker.Clear();
            }
            Require(inserted + existing == rows.Count, "NormalizedSaveCount");
            await transaction.CommitAsync();
            result["committed"] = true;
            result["inserted"] = inserted;
            result["existing"] = existing;
            result["updated"] = 0;
            result["rawSnapshotInserted"] = new[] { al.Inserted, juso.Inserted, scopeDefinition.Inserted, scopeManifest.Inserted }.Count(value => value);
            result["rawSnapshotIds"] = new[] { al.RawSnapshotId, juso.RawSnapshotId, scopeDefinition.RawSnapshotId, scopeManifest.RawSnapshotId }.Order().ToArray();
        }
        finally
        {
            if (locked)
            {
                await using var release = db.Database.GetDbConnection().CreateCommand();
                release.CommandText = "SELECT RELEASE_LOCK('mirror:public-data:admin-dong-address-candidate-r1')";
                _ = await release.ExecuteScalarAsync();
            }
        }
    }

    private static 공공공간원본등록Request Registration(string dataset, string sourceVersion, string contentType, string location)
        => new(SourceId, dataset, sourceVersion, Revision, EvidenceAtUtc, contentType, location);

    private static async Task VerifyReadbackAsync(
        DbContextOptions<PublicDataIngestionDbContext> options,
        Projection projection,
        IReadOnlyList<외부데이터정규화Record> rows,
        Dictionary<string, object?> result)
    {
        await using var verify = new PublicDataIngestionDbContext(options);
        var stored = await LoadScopedRowsAsync(verify);
        await ValidateExistingAsync(verify, stored, rows, requireComplete: true);
        var facts = stored.OrderBy(row => row.RegionStableId, StringComparer.Ordinal).ThenBy(row => row.StableId, StringComparer.Ordinal)
            .Select(FactFromStoredPayload).ToArray();
        var readbackHash = ProjectionHash(facts, projection.ScopeManifestSha256);
        Require(readbackHash == projection.ProjectionHashSha256, "ReadbackProjectionHash");
        var stateCounts = facts.GroupBy(fact => fact.CandidateStateCode, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        Require(stateCounts.GetValueOrDefault(CandidateState) + stateCounts.GetValueOrDefault(MultipleState) == ExpectedCandidateBuildingCount
            && stateCounts.GetValueOrDefault(MissingState) == ExpectedMissingBuildingCount, "ReadbackStateDistribution");
        result["verifiedRows"] = stored.Count;
        result["verifiedAdministrativeAreas"] = stored.Select(row => row.RegionStableId).Distinct(StringComparer.Ordinal).Count();
        result["readbackProjectionHashSha256"] = readbackHash.ToLowerInvariant();
        result["readbackStateCounts"] = stateCounts;
        result["firstId"] = stored.Min(row => row.Id);
        result["lastId"] = stored.Max(row => row.Id);
        result["target"] = "hongdal-mysql-1 / hongdal_dev";
        result["independentReadback"] = true;
    }

    private static Task<List<외부데이터정규화Record>> LoadScopedRowsAsync(PublicDataIngestionDbContext db)
        => db.NormalizedRecords.AsNoTracking()
            .Where(row => row.SourceId == SourceId && row.DatasetId == DatasetId)
            .ToListAsync();

    private static async Task ValidateExistingAsync(
        PublicDataIngestionDbContext db,
        IReadOnlyCollection<외부데이터정규화Record> existing,
        IReadOnlyCollection<외부데이터정규화Record> expected,
        bool requireComplete)
    {
        var expectedByKey = expected.ToDictionary(row => row.RecordKey, StringComparer.Ordinal);
        Require(existing.Select(row => row.RecordKey).Distinct(StringComparer.Ordinal).Count() == existing.Count, "DuplicateStoredRecordKey");
        foreach (var stored in existing)
        {
            Require(expectedByKey.TryGetValue(stored.RecordKey, out var candidate), "UnexpectedStoredRecord");
            Require(Equivalent(stored, candidate!), "StoredRecordConflict");
        }
        if (requireComplete) Require(existing.Count == expected.Count, "StoredExactSetIncomplete");
        if (existing.Count == 0) return;

        var rawIds = existing.Select(row => row.RawSnapshotId).Distinct().ToArray();
        Require(rawIds.Length == 1, "CandidateRawSnapshotSet");
        var raw = await db.RawSnapshots.AsNoTracking().SingleAsync(item => item.Id == rawIds[0]);
        Require(raw.SourceId == SourceId && raw.DatasetId == JusoInputDatasetId
            && raw.ContentHashSha256.Equals(JusoZipSha256, StringComparison.OrdinalIgnoreCase), "CandidateRawSnapshotMismatch");
        await RequireSourceSnapshotAsync(db, AlInputDatasetId, AlSha256);
        await RequireSourceSnapshotAsync(db, JusoInputDatasetId, JusoZipSha256);
        await RequireSourceSnapshotAsync(db, ScopeDefinitionInputDatasetId, ScopeDefinitionSha256);
        var manifestHash = JsonDocument.Parse(existing.First().TextValue).RootElement.GetProperty("sourceHashes").GetProperty("scopeManifestSha256").GetString()!;
        await RequireSourceSnapshotAsync(db, ScopeManifestInputDatasetId, manifestHash);
    }

    private static async Task RequireSourceSnapshotAsync(PublicDataIngestionDbContext db, string dataset, string hash)
    {
        var count = await db.RawSnapshots.AsNoTracking().CountAsync(item => item.SourceId == SourceId
            && item.DatasetId == dataset && item.ContentHashSha256 == hash.ToLower());
        Require(count == 1, "SourceSnapshotMissingOrDuplicate");
    }

    private static bool Equivalent(외부데이터정규화Record stored, 외부데이터정규화Record expected)
        => stored.RecordKey == expected.RecordKey
            && stored.StableId == expected.StableId
            && stored.SourceId == expected.SourceId
            && stored.DatasetId == expected.DatasetId
            && stored.RegionStableId == expected.RegionStableId
            && stored.MetricCode == expected.MetricCode
            && stored.NumericValue == expected.NumericValue
            && stored.TextValue == expected.TextValue
            && stored.UnitCode == expected.UnitCode
            && stored.EvidenceAsOfUtc == expected.EvidenceAsOfUtc
            && stored.CollectedAtUtc == expected.CollectedAtUtc
            && stored.SpatialPrecisionCode == expected.SpatialPrecisionCode
            && stored.TemporalPrecisionCode == expected.TemporalPrecisionCode
            && stored.QualityCode == expected.QualityCode
            && stored.LimitationCode == expected.LimitationCode
            && stored.DimensionKey == expected.DimensionKey
            && stored.SourceVersion == expected.SourceVersion
            && stored.DataRevision == expected.DataRevision
            && stored.FirstSeenAtUtc == expected.FirstSeenAtUtc
            && stored.LastSeenAtUtc == expected.LastSeenAtUtc;

    private static CandidateFact FactFromStoredPayload(외부데이터정규화Record row)
    {
        using var document = JsonDocument.Parse(row.TextValue);
        var root = document.RootElement;
        Require(Text(root, "schemaVersion") == SchemaVersion && Text(root, "reviewState") == "PendingHumanReview", "ReadbackPayloadIdentity");
        foreach (var flag in new[]
        {
            "exactBuildingAddressEstablished", "officialAddressClaimed", "entranceEstablished", "parcelGeometryEstablished",
            "distributionApproved", "deliveryEligible", "traversalReady", "gameplayReady", "unityApplyAllowed"
        }) Require(!Bool(root, flag), "ReadbackAuthorityLeak");
        Require(Text(root, "relationCode") == RelationCode, "ReadbackRelationCode");
        return new CandidateFact(
            Text(root, "administrativeAreaStableId"),
            Text(root, "buildingStableId"),
            Text(root, "sourceBuildingIdentifierA1"),
            Text(root, "parcelIdentifierPnu"),
            Text(root, "candidateState"),
            Int(root, "addressCandidateCount"),
            Int(root, "buildingManagementCandidateCount"),
            Text(root, "addressCandidateSetHashSha256"),
            Text(root, "buildingManagementCandidateSetHashSha256"),
            Text(root, "singleAddressCandidateStableId"),
            Text(root, "singleBuildingManagementCandidateStableId"));
    }

    private static object Summary(Projection projection) => new
    {
        administrativeAreaCount = ExpectedAreaCount,
        buildingCount = projection.Facts.Count,
        uniqueSourceBuildingIdentifierA1Count = ExpectedUniqueSourceBuildingCount,
        sourceRecordSuffixBuildingCount = ExpectedRecordSuffixCount,
        uniquePnuCount = projection.UniquePnuCount,
        matchedPnuCount = projection.MatchedPnuCount,
        parcelAddressCandidateBuildingCount = projection.CandidateBuildingCount - projection.MultipleCandidateBuildingCount,
        multipleParcelAddressCandidatesBuildingCount = projection.MultipleCandidateBuildingCount,
        addressCandidateBuildingCount = projection.CandidateBuildingCount,
        noCandidateInFrozenJusoVintageBuildingCount = projection.MissingBuildingCount,
        addressCandidateCoveragePercent = decimal.Round(100m * projection.CandidateBuildingCount / projection.Facts.Count, 2),
        multipleAddressGroupPnuCount = projection.MultipleAddressGroupPnuCount,
        multipleBuildingManagementNumberPnuCount = projection.MultipleManagementNumberPnuCount,
        exactAdministrativeAreaSet = true,
        exactBuildingSet = true
    };

    private static object AuthoritySummary() => new
    {
        privateReviewOnly = true,
        exactBuildingAddressEstablished = false,
        officialAddressClaimed = false,
        entranceEstablished = false,
        parcelGeometryEstablished = false,
        distributionApproved = false,
        deliveryEligible = false,
        traversalReady = false,
        gameplayReady = false,
        unityApplyAllowed = false
    };

    private static string ProjectionHash(IEnumerable<CandidateFact> facts, string scopeManifestHash)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var header in new[]
        {
            SchemaVersion, Revision, AlSha256, JusoZipSha256, JusoSeoulSha256, ScopeDefinitionSha256, scopeManifestHash
        }) Append(hash, header);
        foreach (var fact in facts.OrderBy(item => item.AdministrativeAreaStableId, StringComparer.Ordinal)
                     .ThenBy(item => item.BuildingStableId, StringComparer.Ordinal))
        {
            Append(hash, fact.AdministrativeAreaStableId);
            Append(hash, fact.BuildingStableId);
            Append(hash, fact.SourceBuildingIdentifierA1);
            Append(hash, fact.ParcelIdentifierPnu);
            Append(hash, fact.CandidateStateCode);
            Append(hash, fact.AddressCandidateCount.ToString(CultureInfo.InvariantCulture));
            Append(hash, fact.BuildingManagementCandidateCount.ToString(CultureInfo.InvariantCulture));
            Append(hash, fact.AddressCandidateSetHashSha256);
            Append(hash, fact.BuildingManagementCandidateSetHashSha256);
        }
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private static string SetHash(IEnumerable<string> values)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var value in values.Order(StringComparer.Ordinal)) Append(hash, value);
        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static string StableReference(string kind, string raw)
        => kind + ":kr:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant()[..24];

    private static void Append(IncrementalHash hash, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, bytes.Length);
        hash.AppendData(length);
        hash.AppendData(bytes);
    }

    private static string PnuFromJuso(string legalDongCode, string mountainFlag, string mainText, string subText)
    {
        Require(IsDigits(legalDongCode, 10), "JusoLegalDongCodeContract");
        var landKind = mountainFlag.Trim() switch
        {
            "0" => "1",
            "1" => "2",
            _ => throw new InvalidDataException("AdministrativeDongBuildingAddressCandidate:JusoMountainFlag")
        };
        var main = ParseUnsigned(mainText, 9_999, "JusoParcelMainNumber");
        var sub = ParseUnsigned(subText, 9_999, "JusoParcelSubNumber");
        return legalDongCode + landKind
            + main.ToString("D4", CultureInfo.InvariantCulture)
            + sub.ToString("D4", CultureInfo.InvariantCulture);
    }

    private static int ParseUnsigned(string value, int maximum, string code)
    {
        Require(int.TryParse(value.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var parsed)
            && parsed >= 0 && parsed <= maximum, code);
        return parsed;
    }

    private static string State(int addressGroupCount)
        => addressGroupCount switch
        {
            0 => MissingState,
            1 => CandidateState,
            _ => MultipleState
        };

    private static HashSet<string> SetFor(Dictionary<string, HashSet<string>> values, string key)
    {
        if (!values.TryGetValue(key, out var result))
        {
            result = new HashSet<string>(StringComparer.Ordinal);
            values.Add(key, result);
        }
        return result;
    }

    private static HashSet<string> EmptySet() => new(StringComparer.Ordinal);

    private static int SelfTest()
    {
        var passed = 0;
        Require(PnuFromJuso("1126010100", "0", "123", "4") == "1126010100101230004", "SelfTestLandPnu");
        passed++;
        Require(PnuFromJuso("1126010100", "1", "7", "0") == "1126010100200070000", "SelfTestMountainPnu");
        passed++;
        Require(State(0) == MissingState && State(1) == CandidateState && State(2) == MultipleState, "SelfTestStateDomain");
        passed++;
        Require(SetHash(new[] { "b", "a" }) == SetHash(new[] { "a", "b" }), "SelfTestSetDeterminism");
        passed++;
        var facts = new[]
        {
            new CandidateFact("region:kr:hjd:2", "building:2", "2222222222222222222222222222", "1126010100100020000", MissingState, 0, 0, SetHash([]), SetHash([]), "", ""),
            new CandidateFact("region:kr:hjd:1", "building:1", "1111111111111111111111111111", "1126010100100010000", CandidateState, 1, 1, SetHash(["a"]), SetHash(["m"]), StableReference("parcel-road-address-candidate", "a"), StableReference("building-management-candidate", "m"))
        };
        Require(ProjectionHash(facts, "SCOPE") == ProjectionHash(facts.Reverse(), "SCOPE"), "SelfTestProjectionDeterminism");
        passed++;
        using var authority = JsonDocument.Parse(JsonSerializer.Serialize(AuthoritySummary(), CompactJson));
        Require(Bool(authority.RootElement, "privateReviewOnly"), "SelfTestPrivateReviewBoundary");
        foreach (var flag in new[]
        {
            "exactBuildingAddressEstablished", "officialAddressClaimed", "entranceEstablished", "parcelGeometryEstablished",
            "distributionApproved", "deliveryEligible", "traversalReady", "gameplayReady", "unityApplyAllowed"
        }) Require(!Bool(authority.RootElement, flag), "SelfTestAuthorityShape");
        passed++;
        return passed;
    }

    private static string HashFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static string Text(JsonElement element, string property)
        => element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : throw new InvalidDataException("AdministrativeDongBuildingAddressCandidate:JsonTextContract");

    private static int Int(JsonElement element, string property)
        => element.TryGetProperty(property, out var value) && value.TryGetInt32(out var result)
            ? result
            : throw new InvalidDataException("AdministrativeDongBuildingAddressCandidate:JsonIntegerContract");

    private static bool Bool(JsonElement element, string property)
        => element.TryGetProperty(property, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : throw new InvalidDataException("AdministrativeDongBuildingAddressCandidate:JsonBooleanContract");

    private static bool IsDigits(string value, int length)
        => value.Length == length && value.All(character => character is >= '0' and <= '9');

    private static void Require(bool condition, string code)
    {
        if (!condition) throw new InvalidDataException("AdministrativeDongBuildingAddressCandidate:" + code);
    }

    private sealed class SelectiveDbfReader : IDisposable
    {
        private readonly Stream stream;
        private readonly int recordLength;
        private readonly IReadOnlyDictionary<string, (int Offset, int Length)> fields;

        public SelectiveDbfReader(Stream stream)
        {
            this.stream = stream;
            var header = new byte[32];
            stream.ReadExactly(header);
            RecordCount = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(4, 4));
            var headerLength = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(8, 2));
            recordLength = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(10, 2));
            Require(RecordCount is > 0 and <= 2_000_000 && recordLength > 1, "DbfShape");
            var descriptors = new List<(string Name, int Length)>();
            var consumed = 32;
            while (true)
            {
                var marker = stream.ReadByte();
                Require(marker >= 0, "DbfHeaderTruncated");
                consumed++;
                if (marker == 0x0D) break;
                var descriptor = new byte[32];
                descriptor[0] = (byte)marker;
                stream.ReadExactly(descriptor.AsSpan(1));
                consumed += 31;
                var name = Encoding.ASCII.GetString(descriptor, 0, 11).TrimEnd('\0', ' ');
                descriptors.Add((name, descriptor[16]));
            }
            Require(consumed <= headerLength, "DbfHeaderLength");
            if (consumed < headerLength)
            {
                var padding = new byte[headerLength - consumed];
                stream.ReadExactly(padding);
            }
            var offset = 1;
            var lookup = new Dictionary<string, (int Offset, int Length)>(StringComparer.OrdinalIgnoreCase);
            foreach (var field in descriptors)
            {
                Require(field.Length > 0 && lookup.TryAdd(field.Name, (offset, field.Length)), "DbfFieldContract");
                offset += field.Length;
            }
            Require(offset <= recordLength && lookup.ContainsKey("A1") && lookup.ContainsKey("A2"), "DbfRequiredFields");
            fields = lookup;
        }

        public int RecordCount { get; }

        public IEnumerable<string[]> Read(params string[] names)
        {
            var selected = names.Select(name => fields.TryGetValue(name, out var field)
                    ? field
                    : throw new InvalidDataException("AdministrativeDongBuildingAddressCandidate:DbfSelectedFieldMissing"))
                .ToArray();
            var buffer = new byte[recordLength];
            for (var index = 0; index < RecordCount; index++)
            {
                stream.ReadExactly(buffer);
                if (buffer[0] == (byte)'*') continue;
                var output = new string[selected.Length];
                for (var fieldIndex = 0; fieldIndex < selected.Length; fieldIndex++)
                {
                    var field = selected[fieldIndex];
                    output[fieldIndex] = Encoding.ASCII.GetString(buffer, field.Offset, field.Length).Trim('\0', ' ');
                }
                yield return output;
            }
        }

        public void Dispose() => stream.Dispose();
    }
}
