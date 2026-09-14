using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Ssalddel.Application.WorldProjection;
using Ssalddel.Contracts.Common.WorldProjection;
using Ssalddel.Domain.PublicData;
using Ssalddel.WorkflowRules.Contracts;

namespace Ssalddel.Tests.Application.WorldProjection;

public sealed class 역세권디오라마건물증거조회UseCaseTests
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    [Fact]
    public async Task Importer모양의두원장을_독립적으로검증하고_주소필지형상을분리한Manifest를반환한다()
    {
        var fixture = Fixture.Create();
        var useCase = fixture.UseCase();

        var manifest = await useCase.ManifestAsync(
            StationDioramaPolicy.SagajeongTransitStationStableId,
            CancellationToken.None);

        Assert.NotNull(manifest);
        Assert.Equal(4_062, manifest.Summary.PresentationBuildingCount);
        Assert.Equal(544, manifest.Summary.BindingCount);
        Assert.Equal(3_774, manifest.Summary.UniqueParcelIdentifierCount);
        Assert.Equal(203, manifest.Summary.UnresolvedAddressBuildingCount);
        Assert.False(manifest.Boundary.DistributionApproved);
        Assert.False(manifest.Boundary.UnityApplyAllowed);
        var road = Assert.Single(manifest.EvidenceChecks, item => item.EvidenceKindCode == "RoadAddress");
        Assert.Equal("Collected", road.CollectionStatusCode);
        Assert.Equal("Complete", road.CompletenessCode);
        Assert.Contains("RawParcelAddressCandidateMissing:206", road.LimitationCode, StringComparison.Ordinal);
        Assert.Contains("ResolutionStateUnresolved:203", road.LimitationCode, StringComparison.Ordinal);
        var parcelGeometry = Assert.Single(manifest.EvidenceChecks, item => item.EvidenceKindCode == "ParcelGeometry");
        Assert.Equal("NotCollected", parcelGeometry.CollectionStatusCode);
        Assert.Equal("Missing", parcelGeometry.CompletenessCode);
        Assert.False(parcelGeometry.UseAuthorityGranted);
    }

    [Fact]
    public async Task 정규화Row하나가누락되면_표본으로대체하지않고Unavailable이다()
    {
        var fixture = Fixture.Create();
        fixture.AddressRows.RemoveAt(0);
        var useCase = fixture.UseCase();

        var error = await Assert.ThrowsAsync<역세권디오라마건물증거UnavailableException>(() =>
            useCase.PresentationBuildingAddressesAsync(
                StationDioramaPolicy.SagajeongTransitStationStableId,
                CancellationToken.None));

        Assert.Equal("SagajeongPresentationBuildingEvidenceRowCountMismatch", error.Code);
    }

    [Fact]
    public async Task 도로명주소후보Text가변조되면_고정된정규RowHash가거부한다()
    {
        var fixture = Fixture.Create();
        var candidate = fixture.AddressRows.First(item =>
            item.MetricCode == 사가정화면건물증거DataContract.AddressCandidateMetric);
        var payload = JsonNode.Parse(candidate.TextValue)!.AsObject();
        payload["canonicalRoadAddress"] = "변조된 도로명주소";
        candidate.TextValue = payload.ToJsonString(Json);
        var useCase = fixture.UseCase();

        var error = await Assert.ThrowsAsync<역세권디오라마건물증거UnavailableException>(() =>
            useCase.PresentationBuildingAddressesAsync(
                StationDioramaPolicy.SagajeongTransitStationStableId,
                CancellationToken.None));

        Assert.Equal("SagajeongPresentationBuildingAddressNormalizedRowsMismatch", error.Code);
    }

    [Fact]
    public async Task 동일한공식CompositeKey에잘못된도로명주소StableId를붙이면_생성공식검증이거부한다()
    {
        var fixture = Fixture.Create();
        var candidate = fixture.AddressRows.First(item =>
            item.MetricCode == 사가정화면건물증거DataContract.AddressCandidateMetric);
        var payload = JsonNode.Parse(candidate.TextValue)!.AsObject();
        var invalidStableId = "road-address:kr:" + new string('f', 24);
        Assert.NotEqual(candidate.StableId, invalidStableId);
        candidate.StableId = invalidStableId;
        payload["addressStableId"] = invalidStableId;
        candidate.TextValue = payload.ToJsonString(Json);
        fixture.RefreshAddressHash();
        var useCase = fixture.UseCase();

        var error = await Assert.ThrowsAsync<역세권디오라마건물증거UnavailableException>(() =>
            useCase.PresentationBuildingAddressesAsync(
                StationDioramaPolicy.SagajeongTransitStationStableId,
                CancellationToken.None));

        Assert.Equal("RoadAddressCandidateIdentityMismatch", error.Code);
    }

    [Fact]
    public async Task 비권위Limitation에임의토큰이추가되면_계보Metadata불일치로거부한다()
    {
        var fixture = Fixture.Create();
        fixture.BindingRows[0].LimitationCode += ";UnexpectedAuthorityToken";
        var useCase = fixture.UseCase();

        var error = await Assert.ThrowsAsync<역세권디오라마건물증거UnavailableException>(() =>
            useCase.PresentationBuildingBindingsAsync(
                StationDioramaPolicy.SagajeongTransitStationStableId,
                CancellationToken.None));

        Assert.Equal("SagajeongPresentationBuildingEvidenceMetadataMismatch", error.Code);
    }

    [Fact]
    public async Task 정규Row의단위나수집시각이변조되면_계보Metadata불일치로거부한다()
    {
        var fixture = Fixture.Create();
        fixture.BindingRows[0].UnitCode = "tampered-unit";
        fixture.BindingRows[1].CollectedAtUtc = fixture.BindingRows[1].CollectedAtUtc.AddSeconds(1);
        var useCase = fixture.UseCase();

        var error = await Assert.ThrowsAsync<역세권디오라마건물증거UnavailableException>(() =>
            useCase.PresentationBuildingBindingsAsync(
                StationDioramaPolicy.SagajeongTransitStationStableId,
                CancellationToken.None));

        Assert.Equal("SagajeongPresentationBuildingEvidenceMetadataMismatch", error.Code);
    }

    [Fact]
    public async Task 정규Row의원본Snapshot결속이변조되면_고정원본계보불일치로거부한다()
    {
        var fixture = Fixture.Create();
        fixture.AddressRows[0].RawSnapshotContentHashSha256 = new string('0', 64);
        var useCase = fixture.UseCase();

        var error = await Assert.ThrowsAsync<역세권디오라마건물증거UnavailableException>(() =>
            useCase.PresentationBuildingAddressesAsync(
                StationDioramaPolicy.SagajeongTransitStationStableId,
                CancellationToken.None));

        Assert.Equal("SagajeongPresentationBuildingEvidenceMetadataMismatch", error.Code);
    }

    [Fact]
    public async Task 사가정역이아닌경우_원장을조회하지않는다()
    {
        var fixture = Fixture.Create();
        var reader = new FakeReader(fixture.BindingRows, fixture.AddressRows);
        var useCase = new 역세권디오라마건물증거조회UseCase(
            reader, fixture.BindingHash, fixture.AddressHash);

        var value = await useCase.ManifestAsync(
            StationDioramaPolicy.MyeonmokTransitStationStableId,
            CancellationToken.None);

        Assert.Null(value);
        Assert.Equal(0, reader.ReadCount);
    }

    [Fact]
    public void 운영생성자는_Importer가동결한NormalizedRowHash를사용한다()
    {
        Assert.Equal("2426E8ABB083EFA3BA07B06675584768ECD1A30E97436E6CE1AED4A4ABA1CA47",
            사가정화면건물증거DataContract.BindingNormalizedDataRowsSha256);
        Assert.Equal("07AF37D600F0B0DC080847F6E65FC1738AFFC827ED333E3ED173A6DC9758F20F",
            사가정화면건물증거DataContract.AddressNormalizedDataRowsSha256);
    }

    private sealed class Fixture
    {
        public List<역세권디오라마건물증거정규Row> BindingRows { get; } = [];
        public List<역세권디오라마건물증거정규Row> AddressRows { get; } = [];
        public string BindingHash { get; private set; } = string.Empty;
        public string AddressHash { get; private set; } = string.Empty;

        public static Fixture Create()
        {
            var fixture = new Fixture();
            fixture.BuildBindings();
            fixture.BuildAddresses();
            return fixture;
        }

        public 역세권디오라마건물증거조회UseCase UseCase()
            => new(new FakeReader(BindingRows, AddressRows), BindingHash, AddressHash);

        public void RefreshAddressHash()
        {
            AddressHash = 역세권디오라마건물증거조회UseCase.NormalizedDataRowsHash(
                AddressRows, 사가정화면건물증거DataContract.AddressManifestMetric);
            var manifest = AddressRows.Single(item =>
                item.MetricCode == 사가정화면건물증거DataContract.AddressManifestMetric);
            var payload = JsonNode.Parse(manifest.TextValue)!.AsObject();
            payload["normalizedDataRowsSha256"] = AddressHash;
            manifest.TextValue = payload.ToJsonString(Json);
        }

        private void BuildBindings()
        {
            var presentationIds = new string[4_062];
            var referenceIds = new string[602];
            for (var index = 0; index < presentationIds.Length; index++)
            {
                var source = index.ToString("D28");
                var presentation = "vworld:al-d010:" + source;
                presentationIds[index] = presentation;
                var state = index switch
                {
                    < 544 => "Bound",
                    < 632 => "AmbiguousGlobalAliasExcluded",
                    < 634 => "AmbiguousMultipleFootprintsExcluded",
                    < 639 => "WeakFootprintCandidateExcluded",
                    _ => "BackdropOnly"
                };
                var references = index < 544 ? new[] { "osm:way:" + (1_000_000 + index) } : [];
                AddBinding(사가정화면건물증거DataContract.PresentationBindingMetric,
                    "presentation-building-binding:" + presentation,
                    "kind=presentation-binding;presentation=" + presentation,
                    "binding-state", "presentation-footprint-binding-candidate",
                    new
                    {
                        rowType = "presentationBinding",
                        presentationBuildingStableId = presentation,
                        sourceFeatureId = source,
                        bindingState = state,
                        referenceBuildingStableIds = references,
                        bindingMethod = index < 544 ? "FootprintOverlap" : state.StartsWith("Ambiguous", StringComparison.Ordinal) ? state : "None",
                        ambiguous = state.StartsWith("Ambiguous", StringComparison.Ordinal)
                    });
            }
            for (var index = 0; index < referenceIds.Length; index++)
            {
                var reference = "osm:way:" + (1_000_000 + index);
                referenceIds[index] = reference;
                AddBinding(사가정화면건물증거DataContract.ReferenceBindingMetric,
                    "reference-building-binding:" + reference,
                    "kind=reference-binding;reference=" + reference,
                    "binding-state", "reference-footprint-binding-candidate",
                    new
                    {
                        rowType = "referenceBinding",
                        referenceBuildingStableId = reference,
                        bindingState = index < 544 ? "Bound" : "UnresolvedNoUniquePresentation",
                        presentationBuildingStableId = index < 544 ? presentationIds[index] : string.Empty
                    });
            }
            for (var index = 0; index < 544; index++)
            {
                var pair = presentationIds[index] + "|" + referenceIds[index];
                AddBinding(사가정화면건물증거DataContract.BindingEdgeMetric,
                    "presentation-reference-building-binding:" + ShortHash(pair),
                    "kind=binding-edge;presentation=" + presentationIds[index] + ";reference=" + referenceIds[index],
                    "binding-edge", "one-to-one-footprint-binding-candidate",
                    new
                    {
                        rowType = "bindingEdge",
                        presentationBuildingStableId = presentationIds[index],
                        referenceBuildingStableId = referenceIds[index],
                        bindingMethod = "FootprintOverlap",
                        centroidDistanceMeters = 1.25m,
                        intersectionOverUnion = 0.75m,
                        smallerFootprintCoverage = 0.8m
                    });
            }
            AddBindingReceipt("PresentationOverlay", "private-review.json",
                사가정화면건물증거DataContract.PresentationOverlayFileSha256,
                사가정화면건물증거DataContract.PresentationOverlayRevision,
                사가정화면건물증거DataContract.PresentationOverlayContentSha256);
            AddBindingReceipt("ReferenceMap", "SagajeongReference.json",
                사가정화면건물증거DataContract.ReferenceMapSha256,
                사가정화면건물증거DataContract.ReferenceMapRevision, null);

            BindingHash = 역세권디오라마건물증거조회UseCase.NormalizedDataRowsHash(
                BindingRows, 사가정화면건물증거DataContract.BindingManifestMetric);
            AddBinding(사가정화면건물증거DataContract.BindingManifestMetric,
                "station-diorama-evidence:sagajeong:binding-manifest:r1",
                "kind=binding-manifest;revision=" + 사가정화면건물증거DataContract.BindingRevision,
                "ledger-manifest", "private-derived-ledger",
                new
                {
                    rowType = "bindingManifest",
                    schemaVersion = 역세권디오라마건물증거SchemaVersions.BindingLedgerV1,
                    revision = 사가정화면건물증거DataContract.BindingRevision,
                    stationStableId = StationDioramaPolicy.SagajeongTransitStationStableId,
                    regionStableId = 사가정화면건물증거DataContract.RegionStableId,
                    presentationOverlayRevision = 사가정화면건물증거DataContract.PresentationOverlayRevision,
                    presentationOverlayFileSha256 = 사가정화면건물증거DataContract.PresentationOverlayFileSha256,
                    presentationOverlayContentSha256 = 사가정화면건물증거DataContract.PresentationOverlayContentSha256,
                    referenceMapRevision = 사가정화면건물증거DataContract.ReferenceMapRevision,
                    referenceMapSha256 = 사가정화면건물증거DataContract.ReferenceMapSha256,
                    fileSha256 = 사가정화면건물증거DataContract.BindingFileSha256,
                    contentSha256 = 사가정화면건물증거DataContract.BindingContentSha256,
                    normalizedDataRowsSha256 = BindingHash,
                    summary = new
                    {
                        presentationBuildingCount = 4_062,
                        referenceBuildingCount = 602,
                        bindingCount = 544,
                        unresolvedReferenceBuildingCount = 58,
                        presentationBindingStates = new Dictionary<string, int>(StringComparer.Ordinal)
                        {
                            ["AmbiguousGlobalAliasExcluded"] = 88,
                            ["AmbiguousMultipleFootprintsExcluded"] = 2,
                            ["BackdropOnly"] = 3_423,
                            ["Bound"] = 544,
                            ["WeakFootprintCandidateExcluded"] = 5
                        }
                    },
                    boundary = BindingBoundary()
                });
            Assert.Equal(5_211, BindingRows.Count);
        }

        private void BuildAddresses()
        {
            for (var index = 0; index < 4_062; index++)
            {
                var source = index.ToString("D28");
                var presentation = "vworld:al-d010:" + source;
                var candidateCount = index < 3_796 ? 1 : index < 3_856 ? 2 : 0;
                var candidateRefs = new List<CandidateRef>();
                for (var candidateIndex = 0; candidateIndex < candidateCount; candidateIndex++)
                {
                    var selected = (index + candidateIndex) % 3_514;
                    candidateRefs.Add(new CandidateRef(AddressId(selected), ["DirectOfficialParcel"]));
                }
                AddAddress(사가정화면건물증거DataContract.AddressAssignmentMetric,
                    "presentation-building-address:" + presentation,
                    "kind=address-assignment;presentation=" + presentation,
                    "address-assignment", "exact-pnu-address-candidate",
                    new
                    {
                        rowType = "addressAssignment",
                        presentationBuildingStableId = presentation,
                        sourceFeatureId = source,
                        parcelIdentifierPnu = "1126010100" + ((index % 3_774) + 1).ToString("D9"),
                        legalDongCode = "1126010100",
                        legalDongName = "면목동",
                        jibun = ((index % 900) + 1).ToString(),
                        sourceBuildingIdentifier = index < 3_796 ? "building-" + index : string.Empty,
                        sourceUfid = index < 4_034 ? "ufid-" + index : string.Empty,
                        referenceBuildingStableId = index < 544 ? "osm:way:" + (1_000_000 + index) : string.Empty,
                        referenceAddressResolutionState = index < 511 ? "OfficialUnique" : string.Empty,
                        referenceOfficialCompositeKeys = index < 511 ? new[] { "official-key-" + index } : [],
                        candidateRefs,
                        resolutionState = ResolutionState(index),
                        reconciliationState = ReconciliationState(index),
                        assignmentMethod = "FrozenPresentationBindingThenExactPnuAndOfficialRelatedParcel",
                        unresolvedReason = index >= 3_859 ? "NoOfficialRoadAddressCandidate" : string.Empty
                    });
            }
            for (var index = 0; index < 3_514; index++)
            {
                var addressId = AddressId(index);
                AddAddress(사가정화면건물증거DataContract.AddressCandidateMetric,
                    addressId,
                    "kind=address-candidate;address=" + addressId,
                    "road-address-candidate", "official-road-address-candidate",
                    new
                    {
                        rowType = "addressCandidate",
                        addressStableId = addressId,
                        officialCompositeKey = OfficialCompositeKey(index),
                        canonicalRoadAddress = "서울특별시 중랑구 사가정로 " + index,
                        officialBuildingManagementNumbers = new[] { index.ToString("D25") }
                    });
            }
            AddAddressReceipt("PresentationBuildingAndParcelIdentifier", "AL_D010_11_20260809.zip",
                사가정화면건물증거DataContract.AlD010Sha256, "AL_D010:Seoul:20260809");
            AddAddressReceipt("RoadAddressBuildingDatabase", "202608_건물DB_전체분.zip",
                사가정화면건물증거DataContract.MoisBuildingDatabaseZipSha256, "202608");
            AddAddressReceipt("RoadAddressBuildingRows", "build_seoul.txt",
                사가정화면건물증거DataContract.MoisBuildingRowsSha256, "202608");
            AddAddressReceipt("RoadAddressRelatedParcelRows", "jibun_seoul.txt",
                사가정화면건물증거DataContract.MoisRelatedParcelRowsSha256, "202608");
            AddAddressReceipt("ReferenceBuildingAddressAssignment", "building-address-assignments.json",
                사가정화면건물증거DataContract.ReferenceAddressLedgerSha256,
                "sagajeong-building-address-assignment.r1");

            AddressHash = 역세권디오라마건물증거조회UseCase.NormalizedDataRowsHash(
                AddressRows, 사가정화면건물증거DataContract.AddressManifestMetric);
            AddAddress(사가정화면건물증거DataContract.AddressManifestMetric,
                "station-diorama-evidence:sagajeong:address-manifest:r1",
                "kind=address-manifest;revision=" + 사가정화면건물증거DataContract.AddressRevision,
                "ledger-manifest", "private-derived-ledger",
                new
                {
                    rowType = "addressManifest",
                    schemaVersion = 역세권디오라마건물증거SchemaVersions.AddressLedgerV1,
                    revision = 사가정화면건물증거DataContract.AddressRevision,
                    stationStableId = StationDioramaPolicy.SagajeongTransitStationStableId,
                    regionStableId = 사가정화면건물증거DataContract.RegionStableId,
                    sourceVintage = 사가정화면건물증거DataContract.SourceVintage,
                    evidenceAsOf = "2026-08-31",
                    bindingLedgerRevision = 사가정화면건물증거DataContract.BindingRevision,
                    bindingLedgerContentSha256 = 사가정화면건물증거DataContract.BindingContentSha256,
                    fileSha256 = 사가정화면건물증거DataContract.AddressFileSha256,
                    contentSha256 = 사가정화면건물증거DataContract.AddressContentSha256,
                    normalizedDataRowsSha256 = AddressHash,
                    summary = new
                    {
                        presentationBuildingCount = 4_062,
                        parcelIdentifierCount = 4_062,
                        uniqueParcelIdentifierCount = 3_774,
                        legalDongAndJibunCount = 4_062,
                        sourceBuildingIdentifierCount = 3_796,
                        sourceUfidCount = 4_034,
                        rawParcelAddressCandidates = new { single = 3_796, multiple = 60, none = 206 },
                        referenceOfficialAddressCount = 511,
                        crossSourceReconciliation = new { agreement = 491, conflict = 18, parcelCandidateMissing = 2 },
                        resolutionStates = new Dictionary<string, int>(StringComparer.Ordinal)
                        {
                            ["CrossSourceConflict"] = 18,
                            ["MultipleAddressCandidates"] = 47,
                            ["ParcelAddressCandidate"] = 3_278,
                            ["ReferenceBindingCandidate"] = 516,
                            ["Unresolved"] = 203
                        },
                        officialPromotedCount = 0,
                        nearestAddressInferenceCount = 0
                    },
                    boundary = AddressBoundary()
                });
            Assert.Equal(7_582, AddressRows.Count);
        }

        private void AddBindingReceipt(string role, string file, string hash, string revision, string? contentHash)
            => AddBinding(사가정화면건물증거DataContract.BindingSourceReceiptMetric,
                "station-diorama-evidence:sagajeong:binding-source:" + role.ToLowerInvariant(),
                "kind=binding-source-receipt;role=" + role,
                "source-receipt", "frozen-source-hash",
                new { rowType = "bindingSourceReceipt", roleCode = role, fileName = file, sha256 = hash, sourceRevision = revision, contentSha256 = contentHash });

        private void AddAddressReceipt(string role, string file, string hash, string revision)
            => AddAddress(사가정화면건물증거DataContract.AddressSourceReceiptMetric,
                "station-diorama-evidence:sagajeong:address-source:" + role.ToLowerInvariant(),
                "kind=address-source-receipt;role=" + role,
                "source-receipt", "frozen-source-hash",
                new { rowType = "addressSourceReceipt", roleCode = role, fileName = file, sha256 = hash, sourceRevision = revision });

        private void AddBinding(string metric, string stableId, string dimension, string unit, string spatial, object value)
            => BindingRows.Add(Row(사가정화면건물증거DataContract.BindingDatasetId,
                사가정화면건물증거DataContract.BindingRevision, metric, stableId, dimension, unit, spatial, value));

        private void AddAddress(string metric, string stableId, string dimension, string unit, string spatial, object value)
            => AddressRows.Add(Row(사가정화면건물증거DataContract.AddressDatasetId,
                사가정화면건물증거DataContract.AddressRevision, metric, stableId, dimension, unit, spatial, value));

        private static 역세권디오라마건물증거정규Row Row(
            string dataset, string revision, string metric, string stableId, string dimension, string unit, string spatial, object value)
        {
            var text = JsonSerializer.Serialize(value, Json);
            var binding = dataset == 사가정화면건물증거DataContract.BindingDatasetId;
            var sourceVersion = revision + ";content-sha256="
                + (binding
                    ? 사가정화면건물증거DataContract.BindingContentSha256
                    : 사가정화면건물증거DataContract.AddressContentSha256).ToLowerInvariant();
            var fileName = binding
                ? 사가정화면건물증거DataContract.BindingFileName
                : 사가정화면건물증거DataContract.AddressFileName;
            var storageObjectName = 사가정화면건물증거DataContract.RawSnapshotStorageFolder + "/" + fileName;
            return new 역세권디오라마건물증거정규Row
            {
                RawSnapshotId = binding ? 101 : 102,
                RecordKey = 외부데이터RecordKey.Create(사가정화면건물증거DataContract.SourceId, dataset,
                    사가정화면건물증거DataContract.RegionStableId, metric,
                    사가정화면건물증거DataContract.EvidenceAsOfUtc, dimension),
                StableId = stableId,
                SourceId = 사가정화면건물증거DataContract.SourceId,
                DatasetId = dataset,
                RegionStableId = 사가정화면건물증거DataContract.RegionStableId,
                MetricCode = metric,
                NumericValue = null,
                TextValue = text,
                UnitCode = unit,
                EvidenceAsOfUtc = 사가정화면건물증거DataContract.EvidenceAsOfUtc,
                CollectedAtUtc = 사가정화면건물증거DataContract.DerivedAtUtc,
                SpatialPrecisionCode = spatial,
                TemporalPrecisionCode = "month-end-derived-snapshot",
                QualityCode = 사가정화면건물증거DataContract.PendingHumanReview,
                LimitationCode = 사가정화면건물증거DataContract.LimitationCode,
                DimensionKey = dimension,
                SourceVersion = sourceVersion,
                DataRevision = revision,
                FirstSeenAtUtc = 사가정화면건물증거DataContract.DerivedAtUtc,
                LastSeenAtUtc = 사가정화면건물증거DataContract.DerivedAtUtc,
                RawSnapshotSourceId = 사가정화면건물증거DataContract.SourceId,
                RawSnapshotDatasetId = dataset,
                RawSnapshotSourceVersion = sourceVersion,
                RawSnapshotEvidenceAsOfUtc = 사가정화면건물증거DataContract.EvidenceAsOfUtc,
                RawSnapshotContentHashSha256 = binding
                    ? 사가정화면건물증거DataContract.BindingFileSha256
                    : 사가정화면건물증거DataContract.AddressFileSha256,
                RawSnapshotContentLength = binding
                    ? 사가정화면건물증거DataContract.BindingFileLength
                    : 사가정화면건물증거DataContract.AddressFileLength,
                RawSnapshotContentType = "application/json",
                RawSnapshotOriginalFileName = fileName,
                RawSnapshotStorageContainer = 사가정화면건물증거DataContract.RawSnapshotStorageContainer,
                RawSnapshotStorageObjectName = storageObjectName,
                RawSnapshotStorageLocation = "private-file://" + storageObjectName
            };
        }

        private static Dictionary<string, object> BindingBoundary() => new(StringComparer.Ordinal)
        {
            ["observationPresentationOnly"] = true, ["distributionApproved"] = false,
            ["deliveryEligible"] = false, ["priceObservationEligible"] = false,
            ["businessLocationEligible"] = false, ["unityApplyAllowed"] = false,
            ["traversalReady"] = false, ["gameplayReady"] = false,
            ["geometryIncluded"] = false, ["nearestBindingInferenceAllowed"] = false
        };

        private static Dictionary<string, object> AddressBoundary() => new(StringComparer.Ordinal)
        {
            ["observationPresentationOnly"] = true, ["distributionApproved"] = false,
            ["deliveryEligible"] = false, ["priceObservationEligible"] = false,
            ["businessLocationEligible"] = false, ["unityApplyAllowed"] = false,
            ["traversalReady"] = false, ["gameplayReady"] = false,
            ["parcelGeometryCollected"] = false, ["nearestAddressInferenceAllowed"] = false,
            ["officialBuildingIdentityConfirmed"] = false
        };

        private static string ResolutionState(int index) => index switch
        {
            < 18 => "CrossSourceConflict",
            < 65 => "MultipleAddressCandidates",
            < 3_343 => "ParcelAddressCandidate",
            < 3_859 => "ReferenceBindingCandidate",
            _ => "Unresolved"
        };

        private static string ReconciliationState(int index) => index switch
        {
            < 491 => "Agreement",
            < 509 => "Conflict",
            < 4_037 => "NotBound",
            < 4_039 => "ParcelCandidateMissing",
            _ => "ReferenceCandidateOnly"
        };

        private static string OfficialCompositeKey(int index)
            => "1126010100|road|0|" + index.ToString("D5") + "|00000";

        private static string AddressId(int index)
            => "road-address:kr:" + Convert.ToHexString(SHA256.HashData(
                Encoding.UTF8.GetBytes(OfficialCompositeKey(index))))[..24].ToLowerInvariant();
        private static string ShortHash(string value)
            => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))[..24].ToLowerInvariant();
    }

    private sealed record CandidateRef(string AddressStableId, string[] EvidenceMethods);

    private sealed class FakeReader(
        IReadOnlyList<역세권디오라마건물증거정규Row> bindingRows,
        IReadOnlyList<역세권디오라마건물증거정규Row> addressRows) : I역세권디오라마건물증거RecordReader
    {
        public int ReadCount { get; private set; }

        public Task<IReadOnlyList<역세권디오라마건물증거정규Row>> ReadAsync(
            string sourceId, string datasetId, string regionStableId, CancellationToken cancellationToken)
        {
            ReadCount++;
            IReadOnlyList<역세권디오라마건물증거정규Row> value = datasetId == 사가정화면건물증거DataContract.BindingDatasetId
                ? bindingRows
                : addressRows;
            return Task.FromResult(value);
        }
    }
}
