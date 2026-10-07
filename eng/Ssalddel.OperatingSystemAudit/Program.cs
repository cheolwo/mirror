using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Ssalddel.ApiMetadata;
using Ssalddel.Contracts.Common.Versioning;

internal static class Program
{
    private const string AuditSchema = "operating-system-source-audit.r23";
    private const string ResultSchema = "operating-system-static-audit.v1";
    private static readonly StringComparison PathComparison = OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
    private static readonly StringComparer PathComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    // 카탈로그와 조사 근거만 읽는다. 서버·DB·외부 API·업무 상태 변경을 실행하지 않는다.
    public static int Main(string[] args)
    {
        try
        {
            return Run(args);
        }
        catch (AuditInputException ex)
        {
            return ReportInputFailure(ex.Code, ex.Message);
        }
        catch (JsonException)
        {
            return ReportInputFailure("audit-json-invalid", "The audit input is not valid JSON.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return ReportInputFailure("audit-io-error", "An input file could not be read or the output could not be saved.");
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
        {
            return ReportInputFailure("audit-path-invalid", "An input or output path is invalid.");
        }
        catch (Exception)
        {
            return ReportInputFailure("audit-execution-error", "The static audit could not be completed.");
        }
    }

    private static int Run(string[] args)
    {
        var options = ParseOptions(args);
        var root = Path.GetFullPath(options.GetValueOrDefault("--root") ?? Directory.GetCurrentDirectory());
        if (!Directory.Exists(root))
            throw new AuditInputException("root-directory-missing", "The repository root directory does not exist.");
        var output = Path.GetFullPath(options.GetValueOrDefault("--output")
            ?? "artifacts/local/os-lifecycle-audit-r23/catalog-validation.json", root);
        var auditPath = options.TryGetValue("--audit", out var auditOption)
            ? Path.GetFullPath(auditOption, root) : null;
        var protectedSources = new HashSet<string>(PathComparer);
        EnsureSafeOutput(root, output, auditPath, protectedSources);
        using var auditDocument = auditPath is null ? null : JsonDocument.Parse(File.ReadAllText(auditPath));
        if (auditDocument is not null)
        {
            ValidateAuditSchema(auditDocument.RootElement);
            foreach (var source in auditDocument.RootElement.GetProperty("sources").EnumerateArray())
                protectedSources.Add(ResolveExistingLinks(Path.GetFullPath(source.GetProperty("path").GetString()!, root)));
            EnsureSafeOutput(root, output, auditPath, protectedSources);
        }

        var errors = new List<string>();
        var checks = new List<object>();
        var sourcePaths = new HashSet<string>(PathComparer);
        var ids = OperatingSystemIds.All.ToArray();
        var lifecycles = OperatingSystemLifecycleCatalog.GetAll();
        var interactions = OperatingSystemInteractionCatalog.GetAll(); // 자체 정의 검증도 실행
        var apis = SsalddelOperatingSystems.GetAll();
        var engines = OperatingSystemEngineCatalog.GetAll();
        var workflowIds = Enum.GetValues<SsalddelWorkflow>();
        var relations = SsalddelWorkflowRelations.GetAll();
        var participants = SsalddelWorkflowParticipants.GetAll();
        var screens = SsalddelWorkflowScreens.GetAll();
        var policies = apis.SelectMany(os => os.SchedulingPolicies.Select(policy => new
        {
            OperatingSystemId = SsalddelOperatingSystems.GetCanonicalId(os.OperatingSystem),
            Policy = policy,
            DeclaredImplementationStatus = SchedulingPolicyImplementationCatalog.IsActive(policy.PolicyCode)
                ? RuntimeCapabilityStatuses.Active : RuntimeCapabilityStatuses.Declared
        })).ToArray();

        Check("canonical-ids-unique", ids.Distinct(StringComparer.Ordinal).Count() == ids.Length);
        Check("api-enum-catalog-complete", apis.Select(os => os.OperatingSystem).Order().SequenceEqual(Enum.GetValues<SsalddelOperatingSystem>().Order()));
        Check("api-ids-known-and-unique", apis.Select(os => SsalddelOperatingSystems.GetCanonicalId(os.OperatingSystem)).Distinct().Count() == apis.Count
            && apis.All(os => ids.Contains(SsalddelOperatingSystems.GetCanonicalId(os.OperatingSystem))));
        Check("os-workflows-known", apis.All(os => os.Workflows.All(workflowIds.Contains)));
        Check("workflow-relations-known", relations.All(item => workflowIds.Contains(item.Source) && workflowIds.Contains(item.Target)));
        Check("workflow-participants-known", participants.All(item => workflowIds.Contains(item.Workflow)));
        foreach (var screen in screens)
            Check($"workflow-screen-actor:{screen.Workflow}/{screen.ActorCode}/{screen.Route}", participants.Any(participant => participant.Workflow == screen.Workflow && participant.ActorCode == screen.ActorCode));
        var stages = lifecycles.SelectMany(os => os.Stages).ToArray();
        Check("lifecycle-stage-ids-globally-unique", stages.Select(stage => stage.StageId).Distinct().Count() == stages.Length);
        foreach (var os in lifecycles)
        {
            Check($"lifecycle:{os.OperatingSystemId}", ids.Contains(os.OperatingSystemId)
                && os.Stages.Count > 0 && os.Stages.All(stage => !string.IsNullOrWhiteSpace(stage.Name) && !string.IsNullOrWhiteSpace(stage.Responsibility))
                && os.Stages.Select(stage => stage.Sequence).SequenceEqual(os.Stages.Select(stage => stage.Sequence).Distinct().Order()));
        }
        foreach (var os in apis)
            Check($"policy-engine:{os.OperatingSystem}", os.SchedulingPolicies.All(policy => os.Engines.Any(engine => engine.EngineCode == policy.AppliedEngineCode)));
        foreach (var engine in engines)
            Check($"engine-family:{engine.OperatingSystemId}/{engine.ImplementationId}", ids.Contains(engine.OperatingSystemId)
                && EngineImplementationCatalog.TryGetFamilyId(engine.ImplementationId, out var family) && family == engine.EngineFamilyId);
        foreach (var interaction in interactions.Where(item => item.ReturnInteractionId is not null))
        {
            var resultReturn = OperatingSystemInteractionCatalog.Get(interaction.ReturnInteractionId!);
            Check($"handoff-return:{interaction.InteractionId}", interaction.SourceOperatingSystemId == resultReturn.TargetOperatingSystemId
                && interaction.TargetOperatingSystemId == resultReturn.SourceOperatingSystemId);
        }

        var evidenceCount = 0;
        var auditedOsCount = 0;
        var mappedStageCount = 0;
        if (auditDocument is not null)
        {
            var snapshot = auditDocument.RootElement;
            var audited = snapshot.GetProperty("operatingSystems").EnumerateArray().ToArray();
            var auditedIds = audited.Select(item => item.GetProperty("operatingSystemId").GetString()!).ToArray();
            auditedOsCount = audited.Length;
            Check("audit-os-universe-complete", auditedIds.Order().SequenceEqual(ids.Order()));
            foreach (var item in audited)
            {
                var id = item.GetProperty("operatingSystemId").GetString()!;
                if (!ids.Contains(id))
                {
                    Check($"audit-os-known:{id}", false);
                    continue;
                }
                var mapped = item.GetProperty("stages").EnumerateArray().ToArray();
                var expected = OperatingSystemLifecycleCatalog.TryGet(id, out var lifecycle)
                    ? lifecycle.Stages.Select(stage => stage.StageId).ToArray() : [];
                Check($"audit-stage-coverage:{id}", mapped.Select(stage => stage.GetProperty("stageId").GetString()!).Order().SequenceEqual(expected.Order()));
                mappedStageCount += mapped.Length;
                Check($"audit-procedures:{id}", mapped.All(stage => stage.GetProperty("procedures").GetArrayLength() > 0)
                    && item.GetProperty("procedures").GetArrayLength() > 0 && item.GetProperty("stateGroups").GetArrayLength() > 0);
            }
            foreach (var evidence in snapshot.GetProperty("sources").EnumerateArray())
            {
                evidenceCount++;
                var relative = evidence.GetProperty("path").GetString()!;
                var full = Path.GetFullPath(relative, root);
                Check($"source-unique:{relative}", sourcePaths.Add(NormalizeSourcePath(relative)));
                if (!IsWithinRoot(ResolveExistingLinks(full), ResolveExistingLinks(root)) || !File.Exists(full))
                {
                    Check($"source-exists:{relative}", false);
                    continue;
                }
                var digest = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(full))).ToLowerInvariant();
                Check($"source-current:{relative}", digest.Equals(evidence.GetProperty("sha256").GetString(), StringComparison.OrdinalIgnoreCase));
            }
            ValidateReferences(snapshot);
        }

        var result = new
        {
            Schema = ResultSchema, EvidenceLevel = "SourceAndCatalogOnly", Succeeded = errors.Count == 0,
            Summary = new { OperatingSystemCount = ids.Length, ApiOperatingSystemCount = apis.Count, LifecycleCount = lifecycles.Count,
                StageCount = stages.Length, InteractionCount = interactions.Count, EngineBindingCount = engines.Count, SchedulingPolicyCount = policies.Length,
                ActiveSchedulingPolicyCount = policies.Count(item => item.DeclaredImplementationStatus == RuntimeCapabilityStatuses.Active),
                WorkflowCount = workflowIds.Length, WorkflowRelationCount = relations.Count, WorkflowParticipantCount = participants.Count, WorkflowScreenCount = screens.Count,
                AuditedOsCount = auditedOsCount, MappedStageCount = mappedStageCount, EvidenceSourceCount = evidenceCount, CheckCount = checks.Count },
            OperatingSystems = ids.Select(id => new
            {
                OperatingSystemId = id, Aliases = OperatingSystemIds.GetAliases(id),
                Lifecycle = OperatingSystemLifecycleCatalog.TryGet(id, out var lifecycle) ? lifecycle : null,
                ApiMetadata = apis.SingleOrDefault(os => SsalddelOperatingSystems.GetCanonicalId(os.OperatingSystem) == id),
                EngineBindings = OperatingSystemEngineCatalog.GetByOperatingSystem(id)
            }),
            CurrentStructures = OperatingSystemInteractionCatalog.GetCurrentStructure(), Interactions = interactions,
            Workflows = workflowIds.Select(id => new { WorkflowId = id, Name = SsalddelWorkflowLabels.GetLabel(id) }),
            WorkflowRelations = relations, WorkflowParticipants = participants, WorkflowScreens = screens,
            SchedulingPolicies = policies, Checks = checks, Errors = errors,
            Limitations = new[] { "Sequence is a responsibility ordering, not an executable transition graph.",
                "Active/Declared values are catalog declarations, not proof of external execution.",
                "Source hashes detect drift; source references do not prove all guards, paths or runtime behavior.",
                "Test-class inventory and validation evidence use separate shapes and do not prove test execution.",
                "No database, external service, device, APK or UI runtime verification is performed." }
        };
        WriteOutput(root, output, auditPath, protectedSources, JsonSerializer.Serialize(result, CreateSerializer()));
        Console.WriteLine(JsonSerializer.Serialize(new { result.Succeeded, result.Summary, ErrorCount = errors.Count, Output = output, Errors = errors }, CreateSerializer()));
        return errors.Count == 0 ? 0 : 1;

        void Check(string code, bool passed)
        {
            checks.Add(new { Code = code, Passed = passed });
            if (!passed) errors.Add(code);
        }
        string NormalizeSourcePath(string relative)
            => Path.GetRelativePath(root, Path.GetFullPath(relative, root)).Replace('\\', '/');
        void ValidateReferences(JsonElement node, bool validationEvidence = false)
        {
            if (node.ValueKind == JsonValueKind.Object)
            {
                if (!validationEvidence && node.TryGetProperty("path", out var path) && node.TryGetProperty("anchor", out var anchor))
                {
                    var relative = path.GetString()!;
                    var full = Path.GetFullPath(relative, root);
                    var exists = IsWithinRoot(ResolveExistingLinks(full), ResolveExistingLinks(root)) && File.Exists(full);
                    Check($"source-covered:{relative}", sourcePaths.Contains(NormalizeSourcePath(relative)));
                    Check($"source-anchor:{relative}/{anchor.GetString()}", exists && File.ReadAllText(full).Contains(anchor.GetString()!, StringComparison.Ordinal));
                    if (exists)
                    {
                        var lineNumber = node.GetProperty("line").GetInt32(); // shape 검증을 통과한 양의 Int32
                        var lines = File.ReadAllLines(full);
                        Check($"source-line:{relative}:{lineNumber}", lineNumber <= lines.Length
                            && lines[lineNumber - 1].Contains(anchor.GetString()!, StringComparison.Ordinal));
                    }
                }
                foreach (var property in node.EnumerateObject())
                    ValidateReferences(property.Value, validationEvidence || property.NameEquals("validation"));
            }
            else if (node.ValueKind == JsonValueKind.Array)
                foreach (var child in node.EnumerateArray()) ValidateReferences(child, validationEvidence);
        }
    }

    private static Dictionary<string, string> ParseOptions(string[] args)
    {
        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 0; index < args.Length; index += 2)
        {
            var key = args[index];
            if (key is not ("--root" or "--output" or "--audit"))
                throw new AuditInputException("cli-option-unknown", $"Unknown option: {key}");
            if (options.ContainsKey(key))
                throw new AuditInputException("cli-option-duplicate", $"Duplicate option: {key}");
            if (index + 1 >= args.Length || string.IsNullOrWhiteSpace(args[index + 1]) || args[index + 1].StartsWith("--", StringComparison.Ordinal))
                throw new AuditInputException("cli-option-value-missing", $"A path value is required for {key}.");
            options.Add(key, args[index + 1]);
        }
        return options;
    }

    private static void ValidateAuditSchema(JsonElement snapshot)
    {
        RequireObject(snapshot, "$", "audit-schema-invalid");
        if (RequireString(snapshot, "schema", "$", "audit-schema-invalid") != AuditSchema)
            throw new AuditInputException("audit-schema-invalid", $"$.schema must be {AuditSchema}.");
        var operatingSystems = RequireArray(snapshot, "operatingSystems", "$", "audit-schema-invalid");
        var sources = RequireArray(snapshot, "sources", "$", "audit-schema-invalid");
        if (sources.GetArrayLength() == 0)
            throw new AuditInputException("audit-source-invalid", "$.sources must contain evidence files.");
        foreach (var os in operatingSystems.EnumerateArray())
        {
            RequireObject(os, "$.operatingSystems[]", "audit-schema-invalid");
            RequireString(os, "operatingSystemId", "$.operatingSystems[]", "audit-schema-invalid");
            var stages = RequireArray(os, "stages", "$.operatingSystems[]", "audit-schema-invalid");
            var stateGroups = RequireArray(os, "stateGroups", "$.operatingSystems[]", "audit-schema-invalid");
            foreach (var group in stateGroups.EnumerateArray())
            {
                RequireObject(group, "$.operatingSystems[].stateGroups[]", "audit-schema-invalid");
                RequireString(group, "name", "$.operatingSystems[].stateGroups[]", "audit-schema-invalid");
                RequireArray(group, "values", "$.operatingSystems[].stateGroups[]", "audit-schema-invalid");
                var refs = RequireArray(group, "sourceRefs", "$.operatingSystems[].stateGroups[]", "audit-reference-invalid");
                if (refs.GetArrayLength() == 0)
                    throw new AuditInputException("audit-reference-invalid", "$.operatingSystems[].stateGroups[].sourceRefs must contain source references.");
            }
            ValidateProcedures(RequireArray(os, "procedures", "$.operatingSystems[]", "audit-schema-invalid"), "$.operatingSystems[].procedures");
            foreach (var stage in stages.EnumerateArray())
            {
                RequireObject(stage, "$.operatingSystems[].stages[]", "audit-schema-invalid");
                RequireString(stage, "stageId", "$.operatingSystems[].stages[]", "audit-schema-invalid");
                ValidateProcedures(RequireArray(stage, "procedures", "$.operatingSystems[].stages[]", "audit-schema-invalid"), "$.operatingSystems[].stages[].procedures");
            }
        }
        ValidateReferenceShape(snapshot, "$", ReferenceKind.Normal);
    }

    private static void ValidateProcedures(JsonElement procedures, string location)
    {
        foreach (var procedure in procedures.EnumerateArray())
        {
            RequireObject(procedure, location + "[]", "audit-schema-invalid");
            RequireString(procedure, "name", location + "[]", "audit-schema-invalid");
            var refs = RequireArray(procedure, "sourceRefs", location + "[]", "audit-reference-invalid");
            if (refs.GetArrayLength() == 0)
                throw new AuditInputException("audit-reference-invalid", $"{location}[].sourceRefs must contain source references.");
        }
    }

    private static void ValidateReferenceShape(JsonElement node, string location, ReferenceKind kind)
    {
        if (node.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var child in node.EnumerateArray())
            {
                var childLocation = $"{location}[{index++}]";
                if (kind is ReferenceKind.Source or ReferenceKind.Reference or ReferenceKind.TestClass)
                    RequireObject(child, childLocation, kind == ReferenceKind.Source ? "audit-source-invalid" : "audit-reference-invalid");
                ValidateReferenceShape(child, childLocation, kind);
            }
            return;
        }
        if (node.ValueKind != JsonValueKind.Object)
        {
            if (kind is ReferenceKind.Source or ReferenceKind.Reference or ReferenceKind.TestClass)
                throw new AuditInputException("audit-reference-invalid", $"{location} must be an object.");
            return;
        }
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in node.EnumerateObject())
            if (!names.Add(property.Name))
                throw new AuditInputException("audit-schema-invalid", $"{location} contains a duplicate property: {property.Name}.");
        if (kind == ReferenceKind.Source)
        {
            RequireRelativePath(node, location, "audit-source-invalid");
            var hash = RequireString(node, "sha256", location, "audit-source-invalid");
            if (hash.Length != 64 || hash.Any(character => !Uri.IsHexDigit(character)))
                throw new AuditInputException("audit-source-invalid", $"{location}.sha256 must contain 64 hexadecimal characters.");
        }
        else if (kind == ReferenceKind.TestClass)
        {
            RequireString(node, "name", location, "audit-reference-invalid");
            RequireRelativePath(node, location, "audit-reference-invalid");
            if (node.TryGetProperty("line", out _)) RequireLine(node, location);
            if (node.TryGetProperty("anchor", out _)) ValidateSourceReference(node, location);
        }
        else if (kind == ReferenceKind.Reference
            || kind == ReferenceKind.Normal && (node.TryGetProperty("path", out _) || node.TryGetProperty("anchor", out _)))
            ValidateSourceReference(node, location);
        foreach (var property in node.EnumerateObject())
        {
            var childKind = kind == ReferenceKind.Validation || property.NameEquals("validation") ? ReferenceKind.Validation
                : property.NameEquals("sources") ? ReferenceKind.Source
                : property.NameEquals("testClasses") ? ReferenceKind.TestClass
                : IsReferenceCollection(property.Name) ? ReferenceKind.Reference : ReferenceKind.Normal;
            if (childKind is ReferenceKind.Source or ReferenceKind.TestClass or ReferenceKind.Reference)
                if (property.Value.ValueKind != JsonValueKind.Array)
                    throw new AuditInputException("audit-reference-invalid", $"{location}.{property.Name} must be an array.");
            ValidateReferenceShape(property.Value, $"{location}.{property.Name}", childKind);
        }
    }

    private static bool IsReferenceCollection(string name)
        => name.EndsWith("Refs", StringComparison.Ordinal) || name.StartsWith("sourceRefs", StringComparison.Ordinal);
    private static void ValidateSourceReference(JsonElement node, string location)
    {
        RequireRelativePath(node, location, "audit-reference-invalid");
        RequireString(node, "anchor", location, "audit-reference-invalid");
        RequireLine(node, location);
    }
    private static string RequireRelativePath(JsonElement node, string location, string code)
    {
        var relative = RequireString(node, "path", location, code);
        if (Path.IsPathRooted(relative))
            throw new AuditInputException(code, $"{location}.path must be relative to --root.");
        return relative;
    }
    private static int RequireLine(JsonElement node, string location)
    {
        if (!node.TryGetProperty("line", out var line) || line.ValueKind != JsonValueKind.Number
            || !line.TryGetInt32(out var number) || number <= 0)
            throw new AuditInputException("audit-reference-invalid", $"{location}.line must be a positive Int32 JSON number.");
        return number;
    }
    private static void RequireObject(JsonElement node, string location, string code)
    {
        if (node.ValueKind != JsonValueKind.Object)
            throw new AuditInputException(code, $"{location} must be an object.");
    }
    private static string RequireString(JsonElement node, string name, string location, string code)
    {
        if (!node.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
            throw new AuditInputException(code, $"{location}.{name} must be a nonblank string.");
        return value.GetString()!;
    }
    private static JsonElement RequireArray(JsonElement node, string name, string location, string code)
    {
        if (!node.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Array)
            throw new AuditInputException(code, $"{location}.{name} must be an array.");
        return value;
    }

    private static bool IsWithinRoot(string full, string root)
    {
        var prefix = Path.EndsInDirectorySeparator(root) ? root : root + Path.DirectorySeparatorChar;
        return full.Equals(root, PathComparison) || full.StartsWith(prefix, PathComparison);
    }
    private static string ResolveExistingLinks(string full)
    {
        var current = Path.GetPathRoot(full)!;
        foreach (var segment in Path.GetRelativePath(current, full).Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries))
        {
            var next = Path.Combine(current, segment);
            FileSystemInfo? info = Directory.Exists(next) ? new DirectoryInfo(next) : File.Exists(next) ? new FileInfo(next) : null;
            if (info is not null && (info.Attributes & FileAttributes.ReparsePoint) != 0)
                current = info.ResolveLinkTarget(returnFinalTarget: true)?.FullName
                    ?? throw new AuditInputException("path-link-unresolved", "An input or output link target could not be resolved safely.");
            else current = next;
        }
        return Path.GetFullPath(current);
    }
    private static void EnsureSafeOutput(string root, string output, string? auditPath, HashSet<string> protectedSources)
    {
        var resolvedOutput = ResolveExistingLinks(output);
        if (auditPath is not null && resolvedOutput.Equals(ResolveExistingLinks(auditPath), PathComparison))
            throw new AuditInputException("output-input-collision", "The output must not overwrite the audit input.");
        if (protectedSources.Contains(resolvedOutput))
            throw new AuditInputException("output-source-collision", "The output must not overwrite an evidence source.");
        var resolvedRoot = ResolveExistingLinks(root);
        var artifactRoot = Path.Combine(resolvedRoot, "artifacts", "local");
        if (IsWithinRoot(resolvedOutput, resolvedRoot) && !IsWithinRoot(resolvedOutput, artifactRoot))
            throw new AuditInputException("output-repository-path-forbidden", "Outputs inside the repository are allowed only below artifacts/local.");
        if (resolvedOutput.Equals(artifactRoot, PathComparison) || Directory.Exists(output))
            throw new AuditInputException("audit-path-invalid", "The output must be a file path.");
    }
    private static void WriteOutput(string root, string output, string? auditPath, HashSet<string> protectedSources, string json)
    {
        EnsureSafeOutput(root, output, auditPath, protectedSources);
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        EnsureSafeOutput(root, output, auditPath, protectedSources);
        var temporary = Path.Combine(Path.GetDirectoryName(output)!, $".{Path.GetFileName(output)}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(temporary, json + Environment.NewLine);
            EnsureSafeOutput(root, output, auditPath, protectedSources);
            File.Move(temporary, output, overwrite: true); // hard link의 기존 내용도 직접 수정하지 않는다.
        }
        finally
        {
            try { if (File.Exists(temporary)) File.Delete(temporary); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
    }
    private static int ReportInputFailure(string code, string message)
    {
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            Schema = ResultSchema, EvidenceLevel = "SourceAndCatalogOnly", Succeeded = false,
            Checks = new[] { new { Code = code, Passed = false } }, Errors = new[] { code },
            ErrorDetails = new[] { new { Code = code, Message = message } }, Output = (string?)null
        }, CreateSerializer()));
        return 1; // 입력/출력 보호 실패에서는 결과 파일을 새로 만들거나 기존 파일을 변경하지 않는다.
    }
    private static JsonSerializerOptions CreateSerializer()
    {
        var serializer = new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        serializer.Converters.Add(new JsonStringEnumConverter());
        return serializer;
    }
    private enum ReferenceKind { Normal, Source, Reference, TestClass, Validation }
    private sealed class AuditInputException(string code, string message) : Exception(message)
    {
        public string Code { get; } = code;
    }
}
