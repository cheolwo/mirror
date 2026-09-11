using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Ssalddel.Contracts.Common.Versioning;
using Ssalddel.Contracts.Common.WorldProjection;
using Ssalddel.Unity.Warehouse;
using Ssalddel.Unity.WorldProjection;

return OperationalUnityTransferProgram.Run(args);

internal static class OperationalUnityTransferProgram
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true
    };

    private static readonly Regex DbSetRegex = new(
        @"DbSet\s*<\s*(?<entity>[^>]+?)\s*>\s+(?<property>[\p{L}\p{N}_]+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex DbContextRegex = new(
        @"class\s+(?<context>[\p{L}\p{N}_]+)\s*:\s*(?:[\p{L}\p{N}_.,<>\s]+,\s*)?DbContext\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex MongoRegex = new(
        @"GetCollection\s*<\s*(?<document>[^>]+?)\s*>\s*\(\s*(?<collection>[^,\r\n\)]+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static int Run(string[] args)
    {
        try
        {
            var options = CommandOptions.Parse(args);
            var root = FindRepositoryRoot(Directory.GetCurrentDirectory());
            var policyPath = ResolvePath(root, options.PolicyPath ?? "eng/execution-ledgers/operational-unity-transfer-policy.json");
            var jsonOutputPath = ResolvePath(root, options.JsonOutputPath ?? "docs/AI/generated/operational-unity-transfer-catalog.json");
            var markdownOutputPath = ResolvePath(root, options.MarkdownOutputPath ?? "docs/AI/generated/operational-unity-transfer-catalog.md");

            var policy = LoadPolicy(policyPath);
            var catalog = BuildCatalog(root, policy);
            var json = NormalizeText(JsonSerializer.Serialize(catalog, JsonOptions));
            var markdown = NormalizeText(RenderMarkdown(catalog));

            if (!string.IsNullOrWhiteSpace(options.QueryKind))
            {
                var queried = Query(catalog, options.QueryKind!, options.QueryValue ?? string.Empty);
                Console.WriteLine(JsonSerializer.Serialize(queried, JsonOptions));
                return 0;
            }

            if (options.Write)
            {
                WriteIfChanged(jsonOutputPath, json);
                WriteIfChanged(markdownOutputPath, markdown);
                Console.WriteLine($"Written: {Relative(root, jsonOutputPath)}");
                Console.WriteLine($"Written: {Relative(root, markdownOutputPath)}");
            }
            else
            {
                EnsureCurrent(jsonOutputPath, json);
                EnsureCurrent(markdownOutputPath, markdown);
                Console.WriteLine("OperationalUnityTransferCatalog:Current");
            }

            foreach (var warning in catalog.Diagnostics.Warnings)
            {
                Console.WriteLine($"Warning:{warning}");
            }

            return 0;
        }
        catch (CatalogValidationException exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 2;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"OperationalUnityTransferCatalog:Failed:{exception.Message}");
            return 1;
        }
    }

    private static TransferCatalog BuildCatalog(string root, TransferPolicy policy)
    {
        var diagnostics = new List<string>();
        ValidatePolicy(root, policy);
        var observationProfiles = (policy.ObservationProfiles ?? Array.Empty<ObservationProfileDefinition>())
            .OrderBy(item => item.ProfileId, StringComparer.Ordinal)
            .ToArray();
        var appObservationBindings = (policy.AppObservationBindings ?? Array.Empty<AppObservationBindingDefinition>())
            .OrderBy(item => item.AppCode, StringComparer.Ordinal)
            .ThenBy(item => item.ObservationProfileRef, StringComparer.Ordinal)
            .ToArray();
        var roleObjectCandidates = (policy.RoleObjectCandidates ?? Array.Empty<OperationalRoleObjectCandidateDefinition>())
            .OrderBy(item => item.ObjectArchetypeId, StringComparer.Ordinal)
            .ToArray();

        var planningPath = ResolvePath(root, policy.Planning.DocumentRef);
        var actualPlanHash = Sha256(planningPath);
        if (!string.Equals(actualPlanHash, policy.Planning.DocumentSha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new CatalogValidationException(
                $"PlanningDocumentHashMismatch:{policy.Planning.DocumentRef}:expected={policy.Planning.DocumentSha256}:actual={actualPlanHash}");
        }

        var hCatalogPath = ResolvePath(root, "eng/world-seedbeds/synty-bottom-up-inventory/catalog.v3.json");
        var hCatalog = LoadSpatialIds(hCatalogPath);
        ValidateHReferences(policy, hCatalog.AllIds);

        if (hCatalog.DeclaredH2Count is not null && hCatalog.DeclaredH2Count != hCatalog.H2Ids.Count)
        {
            diagnostics.Add($"SpatialCatalogDeclaredH2CountDrift:{hCatalog.DeclaredH2Count}->{hCatalog.H2Ids.Count}");
        }

        if (hCatalog.DeclaredH3Count is not null && hCatalog.DeclaredH3Count != hCatalog.H3Ids.Count)
        {
            diagnostics.Add($"SpatialCatalogDeclaredH3CountDrift:{hCatalog.DeclaredH3Count}->{hCatalog.H3Ids.Count}");
        }

        if (hCatalog.DeclaredH4Count is not null && hCatalog.DeclaredH4Count != hCatalog.H4Ids.Count)
        {
            diagnostics.Add($"SpatialCatalogDeclaredH4CountDrift:{hCatalog.DeclaredH4Count}->{hCatalog.H4Ids.Count}");
        }

        ValidateFirstSlice(root, policy.FirstSlice);

        var persistence = ScanPersistence(root);
        var routeCatalog = PageWorldProjectionCatalog.RepresentativeRoutes
            .OrderBy(item => item.RoutePattern, StringComparer.Ordinal)
            .Select(item => new UnityRouteEntry(
                item.RoutePattern,
                item.BusinessName,
                item.WorldZoneCode,
                item.ProjectionTypeCodes,
                item.WorldObjectKey,
                item.InteractionCode,
                item.PanelCode,
                item.InteractionEffectCode,
                item.ProjectionStageCode,
                item.RequiresExplicitConfirmation,
                item.RequiresCanonicalStateRefresh))
            .ToArray();
        var routeLookup = routeCatalog.ToDictionary(item => item.RoutePattern, StringComparer.OrdinalIgnoreCase);

        var pageEntries = SsalddelPageCapabilityCatalog.GetAll()
            .OrderBy(item => VersionSort(item.IntroducedVersion))
            .ThenBy(item => item.AppCode, StringComparer.Ordinal)
            .ThenBy(item => item.PageKey, StringComparer.Ordinal)
            .Select(item => BuildPageEntry(item, policy, persistence, routeLookup))
            .ToArray();

        var duplicates = pageEntries
            .GroupBy(item => $"{item.AppCode}|{item.PageKey}", StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();
        if (duplicates.Length > 0)
        {
            throw new CatalogValidationException($"DuplicatePageCapability:{string.Join(',', duplicates)}");
        }

        var sourcePaths = new[]
        {
            policy.Planning.DocumentRef,
            "eng/execution-ledgers/operational-unity-transfer-policy.json",
            "eng/Ssalddel.OperationalUnityTransfer/Program.cs",
            "eng/Ssalddel.OperationalUnityTransfer/Ssalddel.OperationalUnityTransfer.csproj",
            "Ssalddel.Unity/Runtime/WorldProjection/PageWorldProjectionCatalog.cs",
            "eng/world-seedbeds/synty-bottom-up-inventory/catalog.v3.json",
            "eng/execution-ledgers/playable-loops.json",
            "eng/execution-ledgers/world-interactions.json",
            "Ssalddel/ApiMetadata/SsalddelApiVersionAttribute.cs",
            "Ssalddel.Simulation.Contracts/UnityPackage/Runtime/OperationalRoleGameObjectCatalogContracts.cs",
            "Ssalddel.Unity/Runtime/WorldProjection/운영역할GameObjectCatalogPolicy.cs"
        }.Concat(observationProfiles.SelectMany(item => item.UnityImplementationRefs))
        .Concat(roleObjectCandidates.SelectMany(item => item.UnityImplementationRefs))
        .Concat(Directory.EnumerateFiles(
                ResolvePath(root, "Ssalddel.Contracts/Common/Versioning"),
                "*PageCapabilityCatalog*.cs",
                SearchOption.TopDirectoryOnly)
            .Append(ResolvePath(root, "Ssalddel.Contracts/Common/Versioning/PageCapabilityDtos.cs"))
            .Select(path => Relative(root, path)))
        .Distinct(StringComparer.Ordinal)
        .OrderBy(path => path, StringComparer.Ordinal)
        .ToArray();
        var sourceFingerprints = sourcePaths
        .Where(path => File.Exists(ResolvePath(root, path)))
        .Select(path => new SourceFingerprint(path, Sha256(ResolvePath(root, path))))
        .OrderBy(item => item.Path, StringComparer.Ordinal)
        .ToArray();

        var classifications = pageEntries
            .GroupBy(item => item.TransferClassification, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        var versions = pageEntries
            .GroupBy(item => item.IntroducedVersion, StringComparer.Ordinal)
            .OrderBy(group => VersionSort(group.Key))
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

        return new TransferCatalog(
            "operational-unity-transfer-catalog.v3",
            policy.Revision,
            new PlanningBinding(
                policy.Planning.PlanningId,
                policy.Planning.DocumentRef,
                policy.Planning.Revision,
                actualPlanHash),
            new CatalogSummary(
                pageEntries.Length,
                persistence.DbSets.Length,
                persistence.MongoCollections.Length,
                routeCatalog.Length,
                roleObjectCandidates.Length,
                hCatalog.H1Ids.Count,
                hCatalog.H2Ids.Count,
                hCatalog.H3Ids.Count,
                hCatalog.H4Ids.Count,
                classifications,
                versions),
            pageEntries,
            persistence.DbSets,
            persistence.MongoCollections,
            routeCatalog,
            observationProfiles,
            appObservationBindings,
            roleObjectCandidates,
            policy.FirstLivingSceneProfileRef ?? string.Empty,
            policy.FirstSlice,
            sourceFingerprints,
            new CatalogDiagnostics(diagnostics.OrderBy(item => item, StringComparer.Ordinal).ToArray()));
    }

    private static PageTransferEntry BuildPageEntry(
        SsalddelPageCapabilityRule item,
        TransferPolicy policy,
        PersistenceInventory persistence,
        IReadOnlyDictionary<string, UnityRouteEntry> routeLookup)
    {
        var matchingRules = policy.MappingRules
            .Where(rule => IsMatch(item, rule))
            .OrderBy(rule => rule.Priority)
            .ThenBy(rule => rule.MappingId, StringComparer.Ordinal)
            .ToArray();
        var classification = Classify(item, policy.ServerOnly);
        var canonicalFeatureId = ResolveCanonicalFeatureId(item, policy.CanonicalGroups);
        var h1Refs = matchingRules.SelectMany(rule => rule.H1Refs).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        var h2Refs = matchingRules.SelectMany(rule => rule.H2Refs).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        var interactionRefs = matchingRules.SelectMany(rule => rule.WorldInteractionRefs).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        var loopRefs = matchingRules.SelectMany(rule => rule.PlayableLoopRefs).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        var persistencePatterns = matchingRules.SelectMany(rule => rule.PersistenceNamePatterns).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var persistenceCandidates = persistence.DbSets
            .Where(entry => persistencePatterns.Any(pattern => Contains(entry.EntityType, pattern) || Contains(entry.PropertyName, pattern)))
            .Select(entry => $"{entry.ContextName}.{entry.PropertyName}")
            .Concat(persistence.MongoCollections
                .Where(entry => persistencePatterns.Any(pattern => Contains(entry.DocumentType, pattern) || Contains(entry.CollectionExpression, pattern)))
                .Select(entry => $"Mongo:{entry.DocumentType}"))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .Take(20)
            .ToArray();

        routeLookup.TryGetValue(item.RoutePattern, out var route);
        var hMappingStatus = classification == "ServerOnly"
            ? "NotApplicable"
            : h1Refs.Length > 0 || h2Refs.Length > 0
                ? "MappedCandidate"
                : classification == "ReadOnlyContext"
                    ? "Optional"
                    : "HMappingRequired";

        return new PageTransferEntry(
            item.PageKey,
            item.AppCode,
            item.RoutePattern,
            item.MatchKind.ToString(),
            item.Stage.ToString(),
            item.Boundary.ToString(),
            item.RequiresAuthentication,
            item.HasExternalEffects,
            item.IntroducedVersion,
            item.FeatureKeys.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            item.WorkflowCodes.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            canonicalFeatureId,
            classification,
            ResolveSurfaceCode(classification, route),
            matchingRules.Select(rule => rule.MappingId).ToArray(),
            matchingRules.SelectMany(rule => rule.AreaCodes).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            hMappingStatus,
            h1Refs,
            h2Refs,
            interactionRefs,
            loopRefs,
            matchingRules.SelectMany(rule => rule.UnityImplementationRefs).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            persistenceCandidates,
            route,
            classification == "ServerOnly" ? "NotApplicable" : "E1",
            classification == "ServerOnly" ? "NotApplicable" : "E4",
            item.HasExternalEffects || item.Boundary == PageInteractionBoundary.PlatformPersistence,
            item.HasExternalEffects || item.Boundary == PageInteractionBoundary.PlatformPersistence,
            item.Notice);
    }

    private static string Classify(SsalddelPageCapabilityRule item, ServerOnlyPolicy policy)
    {
        if (policy.AppCodes.Contains(item.AppCode, StringComparer.OrdinalIgnoreCase)
            || MatchesAny(item.PageKey, policy.PageKeyPatterns)
            || MatchesAny(item.RoutePattern, policy.RoutePatterns))
        {
            return "ServerOnly";
        }

        return item.Boundary switch
        {
            PageInteractionBoundary.Simulation => "AmbientSimulation",
            PageInteractionBoundary.PlatformPersistence => "PlayableAction",
            _ => "ReadOnlyContext"
        };
    }

    private static string ResolveSurfaceCode(string classification, UnityRouteEntry? route)
    {
        if (classification == "ServerOnly")
        {
            return route?.InteractionEffectCode == WorldInteractionEffectCodes.WebHandoff ? "WebHandoff" : "HiddenOrWebHandoff";
        }

        if (route is not null && route.ProjectionTypeCodes.Contains(PageProjectionTypeCodes.Action, StringComparer.Ordinal))
        {
            return "WorldAction";
        }

        return classification == "AmbientSimulation" ? "AmbientWorld" : "PanelOrObject";
    }

    private static string ResolveCanonicalFeatureId(SsalddelPageCapabilityRule item, IReadOnlyList<CanonicalGroupRule> groups)
    {
        var group = groups.FirstOrDefault(candidate => MatchesAny(item.PageKey, candidate.PageKeyPatterns));
        if (group is not null)
        {
            return group.CanonicalFeatureId;
        }

        return $"page:{Slug(item.AppCode)}:{Slug(item.PageKey)}";
    }

    private static bool IsMatch(SsalddelPageCapabilityRule item, MappingRule rule)
    {
        return rule.WorkflowCodes.Any(code => item.WorkflowCodes.Contains(code, StringComparer.OrdinalIgnoreCase))
            || (MatchesAny(item.PageKey, rule.PageKeyPatterns) && rule.WorkflowCodes.Count == 0)
            || MatchesAny(item.RoutePattern, rule.RoutePatterns);
    }

    private static bool MatchesAny(string value, IReadOnlyList<string> patterns)
        => patterns.Any(pattern => Regex.IsMatch(value, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant));

    private static bool Contains(string value, string pattern)
        => value.Contains(pattern, StringComparison.OrdinalIgnoreCase);

    private static PersistenceInventory ScanPersistence(string root)
    {
        var dbSets = new List<DbSetEntry>();
        var mongoCollections = new List<MongoCollectionEntry>();
        foreach (var file in EnumerateSourceFiles(root))
        {
            var text = File.ReadAllText(file, Encoding.UTF8);
            var relativePath = Relative(root, file);
            var contextMatch = DbContextRegex.Match(text);
            var contextName = contextMatch.Success ? contextMatch.Groups["context"].Value : Path.GetFileNameWithoutExtension(file);
            foreach (Match match in DbSetRegex.Matches(text))
            {
                dbSets.Add(new DbSetEntry(
                    contextName,
                    match.Groups["property"].Value.Trim(),
                    match.Groups["entity"].Value.Trim(),
                    relativePath));
            }

            foreach (Match match in MongoRegex.Matches(text))
            {
                mongoCollections.Add(new MongoCollectionEntry(
                    match.Groups["document"].Value.Trim(),
                    match.Groups["collection"].Value.Trim(),
                    relativePath));
            }
        }

        return new PersistenceInventory(
            dbSets.Distinct().OrderBy(item => item.ContextName, StringComparer.Ordinal).ThenBy(item => item.PropertyName, StringComparer.Ordinal).ToArray(),
            mongoCollections.Distinct().OrderBy(item => item.DocumentType, StringComparer.Ordinal).ThenBy(item => item.SourcePath, StringComparer.Ordinal).ToArray());
    }

    private static IEnumerable<string> EnumerateSourceFiles(string root)
    {
        var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".git", ".vs", "bin", "obj", "artifacts", "vendor", "node_modules", "Packages", "Library", "Temp"
        };
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            foreach (var directory in Directory.EnumerateDirectories(current))
            {
                if (!excluded.Contains(Path.GetFileName(directory)))
                {
                    pending.Push(directory);
                }
            }

            foreach (var file in Directory.EnumerateFiles(current, "*.cs", SearchOption.TopDirectoryOnly))
            {
                yield return file;
            }
        }
    }

    private static SpatialIds LoadSpatialIds(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path, Encoding.UTF8));
        var allStrings = new HashSet<string>(StringComparer.Ordinal);
        CollectStrings(document.RootElement, allStrings);
        var h1 = allStrings.Where(value => value.StartsWith("h1-", StringComparison.Ordinal)).OrderBy(value => value, StringComparer.Ordinal).ToHashSet(StringComparer.Ordinal);
        var h2 = allStrings.Where(value => value.StartsWith("h2-", StringComparison.Ordinal)).OrderBy(value => value, StringComparer.Ordinal).ToHashSet(StringComparer.Ordinal);
        var h3 = allStrings.Where(value => value.StartsWith("h3-", StringComparison.Ordinal)).OrderBy(value => value, StringComparer.Ordinal).ToHashSet(StringComparer.Ordinal);
        var h4 = allStrings.Where(value => value.StartsWith("h4-", StringComparison.Ordinal)).OrderBy(value => value, StringComparer.Ordinal).ToHashSet(StringComparer.Ordinal);

        int? declaredH2 = null;
        int? declaredH3 = null;
        int? declaredH4 = null;
        if (document.RootElement.TryGetProperty("counts", out var counts))
        {
            declaredH2 = TryGetInt(counts, "h2CandidateCount") ?? TryGetInt(counts, "h2Count") ?? TryGetInt(counts, "h2");
            declaredH3 = TryGetInt(counts, "h3CandidateCount") ?? TryGetInt(counts, "h3Count") ?? TryGetInt(counts, "h3");
            declaredH4 = TryGetInt(counts, "h4BlueprintCount") ?? TryGetInt(counts, "h4Blueprint") ?? TryGetInt(counts, "h4Count") ?? TryGetInt(counts, "h4");
        }

        return new SpatialIds(h1, h2, h3, h4, allStrings, declaredH2, declaredH3, declaredH4);
    }

    private static int? TryGetInt(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var value) && value.TryGetInt32(out var result) ? result : null;

    private static void CollectStrings(JsonElement element, ISet<string> values)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    CollectStrings(property.Value, values);
                }
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    CollectStrings(item, values);
                }
                break;
            case JsonValueKind.String:
                var value = element.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    values.Add(value);
                }
                break;
        }
    }

    private static void ValidatePolicy(string root, TransferPolicy policy)
    {
        if (!string.Equals(policy.SchemaVersion, "operational-unity-transfer-policy.v1", StringComparison.Ordinal)
            && !string.Equals(policy.SchemaVersion, "operational-unity-transfer-policy.v2", StringComparison.Ordinal)
            && !string.Equals(policy.SchemaVersion, "operational-unity-transfer-policy.v3", StringComparison.Ordinal))
        {
            throw new CatalogValidationException($"UnsupportedPolicySchema:{policy.SchemaVersion}");
        }

        var observationProfiles = policy.ObservationProfiles ?? Array.Empty<ObservationProfileDefinition>();
        var appObservationBindings = policy.AppObservationBindings ?? Array.Empty<AppObservationBindingDefinition>();
        var roleObjectCandidates = policy.RoleObjectCandidates ?? Array.Empty<OperationalRoleObjectCandidateDefinition>();

        var duplicateRules = policy.MappingRules.GroupBy(rule => rule.MappingId, StringComparer.Ordinal).Where(group => group.Count() > 1).Select(group => group.Key).ToArray();
        if (duplicateRules.Length > 0)
        {
            throw new CatalogValidationException($"DuplicateMappingRule:{string.Join(',', duplicateRules)}");
        }

        foreach (var path in policy.MappingRules.SelectMany(rule => rule.UnityImplementationRefs)
            .Concat(observationProfiles.SelectMany(profile => profile.UnityImplementationRefs))
            .Concat(roleObjectCandidates.SelectMany(candidate => candidate.UnityImplementationRefs)).Concat(new[]
        {
            policy.FirstSlice.ServerContractRef,
            policy.FirstSlice.ServerUseCaseRef,
            policy.FirstSlice.UnityContractRef,
            policy.FirstSlice.UnityPresentationRef,
            policy.Planning.DocumentRef
        }).Distinct(StringComparer.Ordinal))
        {
            if (!File.Exists(ResolvePath(root, path)) && !Directory.Exists(ResolvePath(root, path)))
            {
                throw new CatalogValidationException($"ReferencedPathMissing:{path}");
            }
        }

        var duplicateProfileIds = observationProfiles
            .GroupBy(profile => profile.ProfileId, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();
        if (duplicateProfileIds.Length > 0)
        {
            throw new CatalogValidationException($"DuplicateObservationProfile:{string.Join(',', duplicateProfileIds)}");
        }

        var duplicateBindings = appObservationBindings
            .GroupBy(binding => $"{binding.AppCode}|{binding.ObservationProfileRef}", StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();
        if (duplicateBindings.Length > 0)
        {
            throw new CatalogValidationException($"DuplicateAppObservationBinding:{string.Join(',', duplicateBindings)}");
        }

        var profileIds = observationProfiles.Select(profile => profile.ProfileId).ToHashSet(StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(policy.FirstLivingSceneProfileRef)
            && !profileIds.Contains(policy.FirstLivingSceneProfileRef))
        {
            throw new CatalogValidationException($"FirstLivingSceneProfileMissing:{policy.FirstLivingSceneProfileRef}");
        }

        var appCodes = SsalddelPageCapabilityCatalog.GetAll()
            .Select(item => item.AppCode)
            .ToHashSet(StringComparer.Ordinal);
        var allowedRelations = new HashSet<string>(new[] { "SimulationAnalog", "ReadOnlyProjection", "NoUnityRepresentation" }, StringComparer.Ordinal);
        foreach (var binding in appObservationBindings)
        {
            if (!appCodes.Contains(binding.AppCode))
            {
                throw new CatalogValidationException($"ObservationBindingAppMissing:{binding.AppCode}");
            }
            if (!allowedRelations.Contains(binding.RelationCode))
            {
                throw new CatalogValidationException($"UnsupportedObservationRelation:{binding.RelationCode}");
            }
            if (!string.Equals(binding.RelationCode, "NoUnityRepresentation", StringComparison.Ordinal)
                && !profileIds.Contains(binding.ObservationProfileRef))
            {
                throw new CatalogValidationException($"ObservationBindingProfileMissing:{binding.ObservationProfileRef}");
            }
            if (policy.ServerOnly.AppCodes.Contains(binding.AppCode, StringComparer.Ordinal)
                && !string.Equals(binding.RelationCode, "NoUnityRepresentation", StringComparison.Ordinal))
            {
                throw new CatalogValidationException($"ServerOnlyAppObservationForbidden:{binding.AppCode}");
            }
        }

        var interactions = File.ReadAllText(ResolvePath(root, "eng/execution-ledgers/world-interactions.json"), Encoding.UTF8);
        foreach (var profile in observationProfiles)
        {
            if (!string.Equals(profile.SceneStableId, "SimulationWorldShell", StringComparison.Ordinal))
            {
                throw new CatalogValidationException($"ObservationSceneMustBeCanonical:{profile.ProfileId}:{profile.SceneStableId}");
            }
            if (!string.Equals(profile.CameraModeCode, "OverviewWithManualFocus", StringComparison.Ordinal))
            {
                throw new CatalogValidationException($"UnsupportedObservationCameraMode:{profile.ProfileId}:{profile.CameraModeCode}");
            }
            if (string.Equals(profile.ExperienceRoleCode, "AutonomousNpcWorld", StringComparison.Ordinal)
                && (!string.Equals(profile.AuthorityScopeCode, "SimulationSession", StringComparison.Ordinal)
                    || profile.AllowsOperationalActions
                    || !profile.ObservationPresentationOnly))
            {
                throw new CatalogValidationException($"AutonomousNpcObservationBoundaryInvalid:{profile.ProfileId}");
            }
            var missingInteractions = profile.WorldInteractionRefs
                .Where(reference => !interactions.Contains(reference, StringComparison.Ordinal))
                .ToArray();
            if (missingInteractions.Length > 0)
            {
                throw new CatalogValidationException($"ObservationWorldInteractionMissing:{profile.ProfileId}:{string.Join(',', missingInteractions)}");
            }
        }

        ValidateRoleObjectCandidates(roleObjectCandidates, profileIds);

        foreach (var pattern in policy.ServerOnly.PageKeyPatterns
            .Concat(policy.ServerOnly.RoutePatterns)
            .Concat(policy.CanonicalGroups.SelectMany(group => group.PageKeyPatterns))
            .Concat(policy.MappingRules.SelectMany(rule => rule.PageKeyPatterns.Concat(rule.RoutePatterns))))
        {
            try
            {
                _ = Regex.IsMatch(string.Empty, pattern);
            }
            catch (ArgumentException)
            {
                throw new CatalogValidationException($"InvalidRegex:{pattern}");
            }
        }
    }

    private static void ValidateRoleObjectCandidates(
        IReadOnlyList<OperationalRoleObjectCandidateDefinition> candidates,
        ISet<string> observationProfileIds)
    {
        var duplicateIds = candidates
            .GroupBy(candidate => candidate.ObjectArchetypeId, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();
        if (duplicateIds.Length > 0)
        {
            throw new CatalogValidationException($"DuplicateRoleObjectCandidate:{string.Join(',', duplicateIds)}");
        }

        var objectKinds = new HashSet<string>(new[] { "Actor", "Facility", "Vehicle", "WorkObject" }, StringComparer.Ordinal);
        var decisions = new HashSet<string>(new[] { "Candidate", "NoUnityRepresentation" }, StringComparer.Ordinal);
        var spawnModes = new HashSet<string>(new[] { "SnapshotProjection", "SimulationAnalog", "NotSpawnable" }, StringComparer.Ordinal);
        var identityPolicies = new HashSet<string>(new[] { "PseudonymousStableId", "AggregateStableId", "NoIdentity" }, StringComparer.Ordinal);

        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate.ObjectArchetypeId)
                || string.IsNullOrWhiteSpace(candidate.DisplayNameKo)
                || !objectKinds.Contains(candidate.ObjectKindCode)
                || !decisions.Contains(candidate.RepresentationDecisionCode)
                || !spawnModes.Contains(candidate.SpawnModeCode)
                || !identityPolicies.Contains(candidate.IdentityPolicyCode))
            {
                throw new CatalogValidationException($"RoleObjectCandidateShapeInvalid:{candidate.ObjectArchetypeId}");
            }

            if (candidate.IsExecutionAuthority || candidate.ContainsPrivateData)
            {
                throw new CatalogValidationException($"RoleObjectCandidateAuthorityOrPrivacyLeak:{candidate.ObjectArchetypeId}");
            }

            var unknownOperatingSystems = candidate.OperatingSystemIds
                .Where(value => !OperatingSystemIds.TryNormalize(value, out _))
                .ToArray();
            if (unknownOperatingSystems.Length > 0)
            {
                throw new CatalogValidationException(
                    $"RoleObjectCandidateOperatingSystemMissing:{candidate.ObjectArchetypeId}:{string.Join(',', unknownOperatingSystems)}");
            }

            var missingProfiles = candidate.ObservationProfileRefs
                .Where(reference => !observationProfileIds.Contains(reference))
                .ToArray();
            if (missingProfiles.Length > 0)
            {
                throw new CatalogValidationException(
                    $"RoleObjectCandidateObservationProfileMissing:{candidate.ObjectArchetypeId}:{string.Join(',', missingProfiles)}");
            }

            if (candidate.SourceActorCodes.Count == 0
                || candidate.OperatingSystemIds.Count == 0
                || candidate.WorkflowCodes.Count == 0)
            {
                throw new CatalogValidationException($"RoleObjectCandidateSourceOrOwnerMissing:{candidate.ObjectArchetypeId}");
            }

            if (candidate.RepresentationDecisionCode == "Candidate")
            {
                if (candidate.SpawnModeCode == "NotSpawnable"
                    || candidate.IdentityPolicyCode == "NoIdentity"
                    || string.IsNullOrWhiteSpace(candidate.VisualKey))
                {
                    throw new CatalogValidationException($"RoleObjectCandidateSpawnContractInvalid:{candidate.ObjectArchetypeId}");
                }
                if (candidate.VisualKey.Contains('/', StringComparison.Ordinal)
                    || candidate.VisualKey.Contains("Assets", StringComparison.OrdinalIgnoreCase)
                    || candidate.VisualKey.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                {
                    throw new CatalogValidationException($"RoleObjectCandidateVisualKeyIsAssetPath:{candidate.ObjectArchetypeId}");
                }
            }
            else if (candidate.SpawnModeCode != "NotSpawnable"
                     || candidate.IdentityPolicyCode != "NoIdentity"
                     || !string.IsNullOrWhiteSpace(candidate.VisualKey)
                     || candidate.PresentationStateCodes.Count > 0
                     || candidate.SceneReady
                     || candidate.PrefabReady)
            {
                throw new CatalogValidationException($"RoleObjectCandidateExcludedButSpawnable:{candidate.ObjectArchetypeId}");
            }

            if (candidate.SceneReady && !candidate.PrefabReady)
            {
                throw new CatalogValidationException($"RoleObjectCandidateSceneReadyWithoutPrefab:{candidate.ObjectArchetypeId}");
            }

            if (candidate.SourceActorCodes.Count != candidate.SourceActorCodes.Distinct(StringComparer.Ordinal).Count()
                || candidate.OperatingSystemIds.Count != candidate.OperatingSystemIds.Distinct(StringComparer.Ordinal).Count()
                || candidate.WorkflowCodes.Count != candidate.WorkflowCodes.Distinct(StringComparer.Ordinal).Count()
                || candidate.PresentationStateCodes.Count != candidate.PresentationStateCodes.Distinct(StringComparer.Ordinal).Count()
                || candidate.ObservationProfileRefs.Count != candidate.ObservationProfileRefs.Distinct(StringComparer.Ordinal).Count()
                || candidate.UnityImplementationRefs.Count != candidate.UnityImplementationRefs.Distinct(StringComparer.Ordinal).Count())
            {
                throw new CatalogValidationException($"RoleObjectCandidateDuplicateValue:{candidate.ObjectArchetypeId}");
            }
        }
    }

    private static void ValidateHReferences(TransferPolicy policy, ISet<string> allHIds)
    {
        var referenced = policy.MappingRules.SelectMany(rule => rule.H1Refs.Concat(rule.H2Refs))
            .Concat(policy.FirstSlice.H1Refs)
            .Concat(policy.FirstSlice.H2Refs)
            .Distinct(StringComparer.Ordinal)
            .Where(reference => !allHIds.Contains(reference))
            .OrderBy(reference => reference, StringComparer.Ordinal)
            .ToArray();
        if (referenced.Length > 0)
        {
            throw new CatalogValidationException($"SpatialReferenceMissing:{string.Join(',', referenced)}");
        }
    }

    private static void ValidateFirstSlice(string root, FirstSliceDefinition firstSlice)
    {
        if (!string.Equals(firstSlice.ServerRoute, WarehouseWorldSnapshotRoutes.AuthorizedSnapshot, StringComparison.Ordinal)
            || !string.Equals(firstSlice.ServerRoute, WarehouseWorldApiRoutes.AuthorizedSnapshot, StringComparison.Ordinal))
        {
            throw new CatalogValidationException("HubFirstSliceRouteMismatch");
        }

        var playableLoops = File.ReadAllText(ResolvePath(root, "eng/execution-ledgers/playable-loops.json"), Encoding.UTF8);
        if (!playableLoops.Contains(firstSlice.PlayableLoopRef, StringComparison.Ordinal))
        {
            throw new CatalogValidationException($"PlayableLoopMissing:{firstSlice.PlayableLoopRef}");
        }

        var interactions = File.ReadAllText(ResolvePath(root, "eng/execution-ledgers/world-interactions.json"), Encoding.UTF8);
        var missingInteractions = firstSlice.WorldInteractionRefs.Where(reference => !interactions.Contains(reference, StringComparison.Ordinal)).ToArray();
        if (missingInteractions.Length > 0)
        {
            throw new CatalogValidationException($"WorldInteractionMissing:{string.Join(',', missingInteractions)}");
        }
    }

    private static TransferPolicy LoadPolicy(string path)
        => JsonSerializer.Deserialize<TransferPolicy>(File.ReadAllText(path, Encoding.UTF8), JsonOptions)
            ?? throw new CatalogValidationException("PolicyDeserializationFailed");

    private static object Query(TransferCatalog catalog, string kind, string value)
    {
        if (string.Equals(kind, "roleobject", StringComparison.OrdinalIgnoreCase))
        {
            return catalog.RoleObjectCandidates.Where(candidate =>
                    string.Equals(candidate.ObjectArchetypeId, value, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(candidate.DisplayNameKo, value, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(candidate.ObjectKindCode, value, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(candidate.ObservationRoleCode, value, StringComparison.OrdinalIgnoreCase)
                    || candidate.SourceActorCodes.Contains(value, StringComparer.OrdinalIgnoreCase)
                    || candidate.OperatingSystemIds.Contains(value, StringComparer.OrdinalIgnoreCase)
                    || candidate.WorkflowCodes.Contains(value, StringComparer.OrdinalIgnoreCase))
                .ToArray();
        }

        Func<PageTransferEntry, bool> predicate = kind.ToLowerInvariant() switch
        {
            "version" => entry => string.Equals(entry.IntroducedVersion, value, StringComparison.OrdinalIgnoreCase),
            "workflow" => entry => entry.WorkflowCodes.Contains(value, StringComparer.OrdinalIgnoreCase),
            "classification" => entry => string.Equals(entry.TransferClassification, value, StringComparison.OrdinalIgnoreCase),
            "h1" => entry => entry.H1Refs.Contains(value, StringComparer.OrdinalIgnoreCase),
            "h2" => entry => entry.H2Refs.Contains(value, StringComparer.OrdinalIgnoreCase),
            "area" => entry => entry.AreaCodes.Contains(value, StringComparer.OrdinalIgnoreCase),
            "pagekey" => entry => string.Equals(entry.PageKey, value, StringComparison.OrdinalIgnoreCase),
            _ => throw new CatalogValidationException($"UnsupportedQueryKind:{kind}")
        };
        return catalog.PageCapabilities.Where(predicate).ToArray();
    }

    private static string RenderMarkdown(TransferCatalog catalog)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# 운영 서버에서 Mirror Unity로의 선별 이관 대장");
        builder.AppendLine();
        builder.AppendLine($"- 판본: `{catalog.Revision}`");
        builder.AppendLine($"- 기획: [{catalog.Planning.PlanningId}](../Planning/시스템/PLAN-ARCH-OPERATIONS-UNITY-TRANSFER-001/README.md)");
        builder.AppendLine($"- 기획 SHA-256: `{catalog.Planning.DocumentSha256}`");
        builder.AppendLine($"- 페이지 기능: {catalog.Summary.PageCapabilityCount}개");
        builder.AppendLine($"- EF Core DbSet: {catalog.Summary.DbSetCount}개");
        builder.AppendLine($"- MongoDB collection 호출: {catalog.Summary.MongoCollectionCount}개");
        builder.AppendLine($"- 기존 Unity 대표 경로: {catalog.Summary.UnityRepresentativeRouteCount}개");
        builder.AppendLine($"- 운영 역할 기반 객체 원형 후보: {catalog.Summary.RoleObjectCandidateCount}개");
        builder.AppendLine($"- 참조 가능한 H: H1 {catalog.Summary.H1Count} / H2 {catalog.Summary.H2Count} / H3 {catalog.Summary.H3Count} / H4 {catalog.Summary.H4Count}");
        builder.AppendLine();
        builder.AppendLine("이 대장은 자동 생성물이다. 페이지·저장 개체는 조사 모수이며 H1로 자동 승격되지 않는다. `MappedCandidate`도 실제 배치나 E5 증거가 아니다.");
        builder.AppendLine();
        builder.AppendLine("## 분류 요약");
        builder.AppendLine();
        builder.AppendLine("| 분류 | 수 | 기본 처리 |");
        builder.AppendLine("| --- | ---: | --- |");
        foreach (var item in catalog.Summary.ClassificationCounts)
        {
            builder.AppendLine($"| `{item.Key}` | {item.Value} | {ClassificationDescription(item.Key)} |");
        }
        builder.AppendLine();
        builder.AppendLine("## 생활 관찰 프로필");
        builder.AppendLine();
        builder.AppendLine("운영 앱과 Unity 생활 관찰은 다대다 관계다. `SimulationAnalog`는 앱의 운영 원장을 연결한다는 뜻이 아니라, 같은 업무 의미를 가상 Simulation 생활로 관찰한다는 뜻이다.");
        builder.AppendLine();
        builder.AppendLine("| 프로필 | 장면·영역 | 권위·경험 | 카메라 | 운영 행위 | WI |");
        builder.AppendLine("| --- | --- | --- | --- | --- | --- |");
        foreach (var profile in catalog.ObservationProfiles)
        {
            builder.AppendLine($"| `{Escape(profile.ProfileId)}`<br>{Escape(profile.DisplayNameKo)} | `{Escape(profile.SceneStableId)}` / `{Escape(profile.AreaCode)}` | `{Escape(profile.AuthorityScopeCode)}` / `{Escape(profile.ExperienceRoleCode)}` | `{Escape(profile.CameraModeCode)}` | {(profile.AllowsOperationalActions ? "허용" : "금지")} / 표현 전용 `{profile.ObservationPresentationOnly}` | {Escape(string.Join("<br>", profile.WorldInteractionRefs))} |");
        }
        if (catalog.ObservationProfiles.Count == 0) builder.AppendLine("| - | - | - | - | - | - |");
        builder.AppendLine();
        builder.AppendLine($"- 첫 생활 장면 프로필: `{Escape(catalog.FirstLivingSceneProfileRef)}`");
        builder.AppendLine();
        builder.AppendLine("## 앱과 관찰 프로필 연결");
        builder.AppendLine();
        builder.AppendLine("| 앱 | 관계 | 관찰 프로필 |");
        builder.AppendLine("| --- | --- | --- |");
        foreach (var binding in catalog.AppObservationBindings)
        {
            builder.AppendLine($"| `{Escape(binding.AppCode)}` | `{Escape(binding.RelationCode)}` | `{Escape(binding.ObservationProfileRef)}` |");
        }
        if (catalog.AppObservationBindings.Count == 0) builder.AppendLine("| - | - | - |");
        builder.AppendLine();
        builder.AppendLine("## 운영 역할 기반 객체 원형 후보");
        builder.AppendLine();
        builder.AppendLine("운영 계정이나 DB 행을 그대로 GameObject로 만들지 않는다. 이 목록은 비식별 상태 사본 또는 가상 Simulation에서 표현을 준비할 수 있는 원형이며, `sceneReady=false`인 항목은 실제 Scene 생성 승인이 아니다.");
        builder.AppendLine();
        builder.AppendLine("| 객체 원형 | 종류 | 운영 역할 | 소유 OS·업무 | 생성 방식 | 식별자 | 현재 준비 | 시각 키 |");
        builder.AppendLine("| --- | --- | --- | --- | --- | --- | --- | --- |");
        foreach (var candidate in catalog.RoleObjectCandidates)
        {
            var owner = candidate.OperatingSystemIds.Concat(candidate.WorkflowCodes).Select(Escape);
            builder.AppendLine($"| `{Escape(candidate.ObjectArchetypeId)}`<br>{Escape(candidate.DisplayNameKo)} | `{Escape(candidate.ObjectKindCode)}` | {Escape(string.Join(", ", candidate.SourceActorCodes))} | {Escape(string.Join("<br>", owner))} | `{Escape(candidate.RepresentationDecisionCode)}` / `{Escape(candidate.SpawnModeCode)}` | `{Escape(candidate.IdentityPolicyCode)}` | Prefab `{candidate.PrefabReady}` / Scene `{candidate.SceneReady}` | {(string.IsNullOrWhiteSpace(candidate.VisualKey) ? "-" : $"`{Escape(candidate.VisualKey)}`")} |");
        }
        if (catalog.RoleObjectCandidates.Count == 0) builder.AppendLine("| - | - | - | - | - | - | - | - |");
        builder.AppendLine();
        builder.AppendLine("## 첫 독립 표본");
        builder.AppendLine();
        builder.AppendLine($"- `{catalog.FirstSlice.SliceId}` / `{catalog.FirstSlice.PlayableLoopRef}`");
        builder.AppendLine($"- H1: {string.Join(", ", catalog.FirstSlice.H1Refs.Select(value => $"`{value}`"))}");
        builder.AppendLine($"- H2: {string.Join(", ", catalog.FirstSlice.H2Refs.Select(value => $"`{value}`"))}");
        builder.AppendLine($"- 상태: Presentation 현행 `{catalog.FirstSlice.CurrentPresentationEvidence}`, 다음 준비 목표 `{catalog.FirstSlice.TargetPresentationEvidence}`, E5 `{catalog.FirstSlice.E5Status}`");
        builder.AppendLine();
        builder.AppendLine("## 페이지별 이관 판정");
        builder.AppendLine();
        builder.AppendLine("| 판본 | 페이지 | 앱 | 이관 | H 상태 | 영역 | H1/H2 | 기존 Unity 경로 |");
        builder.AppendLine("| --- | --- | --- | --- | --- | --- | --- | --- |");
        foreach (var item in catalog.PageCapabilities)
        {
            var hRefs = item.H1Refs.Concat(item.H2Refs).Select(Escape).ToArray();
            builder.AppendLine($"| {Escape(item.IntroducedVersion)} | `{Escape(item.PageKey)}` | `{Escape(item.AppCode)}` | `{item.TransferClassification}` | `{item.HMappingStatus}` | {Escape(string.Join(", ", item.AreaCodes))} | {Escape(string.Join("<br>", hRefs))} | {(item.ExistingUnityRoute is null ? "-" : $"`{Escape(item.ExistingUnityRoute.ProjectionStageCode)}`")} |");
        }
        builder.AppendLine();
        builder.AppendLine("## 진단");
        builder.AppendLine();
        if (catalog.Diagnostics.Warnings.Length == 0)
        {
            builder.AppendLine("- 경고 없음");
        }
        else
        {
            foreach (var warning in catalog.Diagnostics.Warnings)
            {
                builder.AppendLine($"- `{warning}`");
            }
        }

        return builder.ToString();
    }

    private static string ClassificationDescription(string classification) => classification switch
    {
        "PlayableAction" => "명시적 선택·확인 뒤 서버 권위 결과 재조회",
        "ReadOnlyContext" => "선택형 상세 또는 World 패널",
        "AmbientSimulation" => "저장 없는 환경·상태 표현",
        "ServerOnly" => "Unity 직접 실행 금지, 필요 시 Web 인계",
        _ => "검토 필요"
    };

    private static string Escape(string value)
        => value.Replace("|", "\\|", StringComparison.Ordinal).Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);

    private static string Slug(string value)
    {
        var builder = new StringBuilder();
        foreach (var character in value.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
            }
            else if (builder.Length > 0 && builder[^1] != '-')
            {
                builder.Append('-');
            }
        }
        return builder.ToString().Trim('-');
    }

    private static int VersionSort(string version)
        => Version.TryParse(version, out var parsed) ? parsed.Major * 10000 + parsed.Minor * 100 + Math.Max(0, parsed.Build) : int.MaxValue;

    private static string FindRepositoryRoot(string start)
    {
        var current = new DirectoryInfo(start);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "AGENTS.md"))
                && Directory.Exists(Path.Combine(current.FullName, "Ssalddel")))
            {
                return current.FullName;
            }
            current = current.Parent;
        }
        throw new CatalogValidationException("RepositoryRootNotFound");
    }

    private static string ResolvePath(string root, string path)
        => Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar)));

    private static string Relative(string root, string path)
        => Path.GetRelativePath(root, path).Replace('\\', '/');

    private static string Sha256(string path)
        => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();

    private static string NormalizeText(string value)
        => value.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd() + "\n";

    private static void WriteIfChanged(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if (File.Exists(path) && NormalizeText(File.ReadAllText(path, Encoding.UTF8)) == content)
        {
            return;
        }
        File.WriteAllText(path, content, new UTF8Encoding(false));
    }

    private static void EnsureCurrent(string path, string expected)
    {
        if (!File.Exists(path))
        {
            throw new CatalogValidationException($"GeneratedOutputMissing:{path}");
        }
        var actual = NormalizeText(File.ReadAllText(path, Encoding.UTF8));
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
        {
            throw new CatalogValidationException($"GeneratedOutputStale:{path}");
        }
    }
}

internal sealed record CommandOptions(bool Write, string? PolicyPath, string? JsonOutputPath, string? MarkdownOutputPath, string? QueryKind, string? QueryValue)
{
    public static CommandOptions Parse(string[] args)
    {
        var write = false;
        string? policy = null;
        string? json = null;
        string? markdown = null;
        string? queryKind = null;
        string? queryValue = null;
        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--write": write = true; break;
                case "--policy": policy = Next(args, ref index); break;
                case "--output-json": json = Next(args, ref index); break;
                case "--output-markdown": markdown = Next(args, ref index); break;
                case "--query-kind": queryKind = Next(args, ref index); break;
                case "--query-value": queryValue = Next(args, ref index); break;
                default: throw new CatalogValidationException($"UnknownArgument:{args[index]}");
            }
        }
        if (string.IsNullOrWhiteSpace(queryKind) != string.IsNullOrWhiteSpace(queryValue))
        {
            throw new CatalogValidationException("QueryKindAndValueMustBeProvidedTogether");
        }
        return new CommandOptions(write, policy, json, markdown, queryKind, queryValue);
    }

    private static string Next(string[] args, ref int index)
    {
        index++;
        if (index >= args.Length)
        {
            throw new CatalogValidationException("ArgumentValueMissing");
        }
        return args[index];
    }
}

internal sealed class CatalogValidationException(string message) : Exception(message);

internal sealed record TransferPolicy(
    string SchemaVersion,
    string Revision,
    PlanningPolicy Planning,
    ServerOnlyPolicy ServerOnly,
    IReadOnlyList<CanonicalGroupRule> CanonicalGroups,
    IReadOnlyList<MappingRule> MappingRules,
    FirstSliceDefinition FirstSlice,
    IReadOnlyList<ObservationProfileDefinition>? ObservationProfiles = null,
    IReadOnlyList<AppObservationBindingDefinition>? AppObservationBindings = null,
    IReadOnlyList<OperationalRoleObjectCandidateDefinition>? RoleObjectCandidates = null,
    string? FirstLivingSceneProfileRef = null);

internal sealed record PlanningPolicy(string PlanningId, string DocumentRef, string Revision, string DocumentSha256);
internal sealed record ServerOnlyPolicy(IReadOnlyList<string> AppCodes, IReadOnlyList<string> PageKeyPatterns, IReadOnlyList<string> RoutePatterns);
internal sealed record CanonicalGroupRule(string CanonicalFeatureId, IReadOnlyList<string> PageKeyPatterns);
internal sealed record MappingRule(
    string MappingId,
    int Priority,
    IReadOnlyList<string> WorkflowCodes,
    IReadOnlyList<string> PageKeyPatterns,
    IReadOnlyList<string> RoutePatterns,
    IReadOnlyList<string> AreaCodes,
    IReadOnlyList<string> H1Refs,
    IReadOnlyList<string> H2Refs,
    IReadOnlyList<string> WorldInteractionRefs,
    IReadOnlyList<string> PlayableLoopRefs,
    IReadOnlyList<string> UnityImplementationRefs,
    IReadOnlyList<string> PersistenceNamePatterns);

internal sealed record FirstSliceDefinition(
    string SliceId,
    string AreaCode,
    string PlayableLoopRef,
    IReadOnlyList<string> WorldInteractionRefs,
    IReadOnlyList<string> H1Refs,
    IReadOnlyList<string> H2Refs,
    string ServerRoute,
    string ServerContractRef,
    string ServerUseCaseRef,
    string UnityContractRef,
    string UnityPresentationRef,
    string CurrentPresentationEvidence,
    string TargetPresentationEvidence,
    string E5Status);

internal sealed record ObservationProfileDefinition(
    string ProfileId,
    string DisplayNameKo,
    string AuthorityScopeCode,
    string ExperienceRoleCode,
    string SceneStableId,
    string AreaCode,
    string CameraModeCode,
    IReadOnlyList<string> ActorRoleCodes,
    IReadOnlyList<string> FacilityCodes,
    IReadOnlyList<string> WorkflowCodes,
    IReadOnlyList<string> WorldInteractionRefs,
    IReadOnlyList<string> UnityImplementationRefs,
    bool AllowsOperationalActions,
    bool ObservationPresentationOnly);

internal sealed record AppObservationBindingDefinition(
    string AppCode,
    string ObservationProfileRef,
    string RelationCode);

internal sealed record OperationalRoleObjectCandidateDefinition(
    string ObjectArchetypeId,
    string DisplayNameKo,
    string ObjectKindCode,
    IReadOnlyList<string> SourceActorCodes,
    IReadOnlyList<string> OperatingSystemIds,
    IReadOnlyList<string> WorkflowCodes,
    string ObservationRoleCode,
    string VisualKey,
    string RepresentationDecisionCode,
    string SpawnModeCode,
    string IdentityPolicyCode,
    IReadOnlyList<string> PresentationStateCodes,
    IReadOnlyList<string> ObservationProfileRefs,
    IReadOnlyList<string> UnityImplementationRefs,
    bool IsExecutionAuthority,
    bool ContainsPrivateData,
    bool PrefabReady,
    bool SceneReady,
    string Boundary);

internal sealed record TransferCatalog(
    string SchemaVersion,
    string Revision,
    PlanningBinding Planning,
    CatalogSummary Summary,
    IReadOnlyList<PageTransferEntry> PageCapabilities,
    IReadOnlyList<DbSetEntry> DbSets,
    IReadOnlyList<MongoCollectionEntry> MongoCollections,
    IReadOnlyList<UnityRouteEntry> UnityRepresentativeRoutes,
    IReadOnlyList<ObservationProfileDefinition> ObservationProfiles,
    IReadOnlyList<AppObservationBindingDefinition> AppObservationBindings,
    IReadOnlyList<OperationalRoleObjectCandidateDefinition> RoleObjectCandidates,
    string FirstLivingSceneProfileRef,
    FirstSliceDefinition FirstSlice,
    IReadOnlyList<SourceFingerprint> SourceFingerprints,
    CatalogDiagnostics Diagnostics);

internal sealed record PlanningBinding(string PlanningId, string DocumentRef, string Revision, string DocumentSha256);
internal sealed record CatalogSummary(
    int PageCapabilityCount,
    int DbSetCount,
    int MongoCollectionCount,
    int UnityRepresentativeRouteCount,
    int RoleObjectCandidateCount,
    int H1Count,
    int H2Count,
    int H3Count,
    int H4Count,
    IReadOnlyDictionary<string, int> ClassificationCounts,
    IReadOnlyDictionary<string, int> VersionCounts);

internal sealed record PageTransferEntry(
    string PageKey,
    string AppCode,
    string RoutePattern,
    string MatchKind,
    string Stage,
    string Boundary,
    bool RequiresAuthentication,
    bool HasExternalEffects,
    string IntroducedVersion,
    IReadOnlyList<string> FeatureKeys,
    IReadOnlyList<string> WorkflowCodes,
    string CanonicalFeatureId,
    string TransferClassification,
    string UnitySurfaceCode,
    IReadOnlyList<string> MappingRuleIds,
    IReadOnlyList<string> AreaCodes,
    string HMappingStatus,
    IReadOnlyList<string> H1Refs,
    IReadOnlyList<string> H2Refs,
    IReadOnlyList<string> WorldInteractionRefs,
    IReadOnlyList<string> PlayableLoopRefs,
    IReadOnlyList<string> UnityImplementationRefs,
    IReadOnlyList<string> PersistenceCandidates,
    UnityRouteEntry? ExistingUnityRoute,
    string EarliestEvidenceStage,
    string TargetEvidenceStage,
    bool RequiresExplicitConfirmation,
    bool RequiresCanonicalStateRefresh,
    string Notice);

internal sealed record DbSetEntry(string ContextName, string PropertyName, string EntityType, string SourcePath);
internal sealed record MongoCollectionEntry(string DocumentType, string CollectionExpression, string SourcePath);
internal sealed record PersistenceInventory(DbSetEntry[] DbSets, MongoCollectionEntry[] MongoCollections);
internal sealed record UnityRouteEntry(
    string RoutePattern,
    string BusinessName,
    string WorldZoneCode,
    IReadOnlyList<string> ProjectionTypeCodes,
    string WorldObjectKey,
    string InteractionCode,
    string PanelCode,
    string InteractionEffectCode,
    string ProjectionStageCode,
    bool RequiresExplicitConfirmation,
    bool RequiresCanonicalStateRefresh);
internal sealed record SourceFingerprint(string Path, string Sha256);
internal sealed record CatalogDiagnostics(string[] Warnings);
internal sealed record SpatialIds(
    HashSet<string> H1Ids,
    HashSet<string> H2Ids,
    HashSet<string> H3Ids,
    HashSet<string> H4Ids,
    HashSet<string> AllIds,
    int? DeclaredH2Count,
    int? DeclaredH3Count,
    int? DeclaredH4Count);
