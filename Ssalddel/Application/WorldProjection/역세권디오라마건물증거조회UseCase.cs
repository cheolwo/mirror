using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Common.WorldProjection;
using Ssalddel.Domain.PublicData;
using Ssalddel.Infrastructure.Persistence.PublicData;
using Ssalddel.WorkflowRules.Contracts;

namespace Ssalddel.Application.WorldProjection;

public interface I역세권디오라마건물증거조회UseCase
{
    Task<역세권디오라마건물증거ManifestDto?> ManifestAsync(
        string transitStationStableId,
        CancellationToken cancellationToken);

    Task<화면건물결속LedgerDto?> PresentationBuildingBindingsAsync(
        string transitStationStableId,
        CancellationToken cancellationToken);

    Task<화면건물주소LedgerDto?> PresentationBuildingAddressesAsync(
        string transitStationStableId,
        CancellationToken cancellationToken);
}

public sealed class 역세권디오라마건물증거UnavailableException(string code) : Exception(code)
{
    public string Code { get; } = code;
}

public sealed class 역세권디오라마건물증거정규Row
{
    public long RawSnapshotId { get; set; }
    public string RecordKey { get; set; } = string.Empty;
    public string StableId { get; set; } = string.Empty;
    public string SourceId { get; set; } = string.Empty;
    public string DatasetId { get; set; } = string.Empty;
    public string RegionStableId { get; set; } = string.Empty;
    public string MetricCode { get; set; } = string.Empty;
    public decimal? NumericValue { get; set; }
    public string TextValue { get; set; } = string.Empty;
    public string UnitCode { get; set; } = string.Empty;
    public DateTimeOffset EvidenceAsOfUtc { get; set; }
    public DateTimeOffset CollectedAtUtc { get; set; }
    public string SpatialPrecisionCode { get; set; } = string.Empty;
    public string TemporalPrecisionCode { get; set; } = string.Empty;
    public string QualityCode { get; set; } = string.Empty;
    public string LimitationCode { get; set; } = string.Empty;
    public string DimensionKey { get; set; } = string.Empty;
    public string SourceVersion { get; set; } = string.Empty;
    public string DataRevision { get; set; } = string.Empty;
    public DateTimeOffset FirstSeenAtUtc { get; set; }
    public DateTimeOffset LastSeenAtUtc { get; set; }
    public string RawSnapshotSourceId { get; set; } = string.Empty;
    public string RawSnapshotDatasetId { get; set; } = string.Empty;
    public string RawSnapshotSourceVersion { get; set; } = string.Empty;
    public DateTimeOffset? RawSnapshotEvidenceAsOfUtc { get; set; }
    public string RawSnapshotContentHashSha256 { get; set; } = string.Empty;
    public long RawSnapshotContentLength { get; set; }
    public string RawSnapshotContentType { get; set; } = string.Empty;
    public string RawSnapshotOriginalFileName { get; set; } = string.Empty;
    public string RawSnapshotStorageContainer { get; set; } = string.Empty;
    public string RawSnapshotStorageObjectName { get; set; } = string.Empty;
    public string RawSnapshotStorageLocation { get; set; } = string.Empty;
}

public interface I역세권디오라마건물증거RecordReader
{
    Task<IReadOnlyList<역세권디오라마건물증거정규Row>> ReadAsync(
        string sourceId,
        string datasetId,
        string regionStableId,
        CancellationToken cancellationToken);
}

public sealed class Ef역세권디오라마건물증거RecordReader(
    PublicDataIngestionDbContext db) : I역세권디오라마건물증거RecordReader
{
    public async Task<IReadOnlyList<역세권디오라마건물증거정규Row>> ReadAsync(
        string sourceId,
        string datasetId,
        string regionStableId,
        CancellationToken cancellationToken)
        => await db.NormalizedRecords.AsNoTracking()
            .Where(item => item.SourceId == sourceId
                           && item.DatasetId == datasetId
                           && item.RegionStableId == regionStableId)
            .OrderBy(item => item.StableId)
            .Select(item => new 역세권디오라마건물증거정규Row
            {
                RawSnapshotId = item.RawSnapshotId,
                RecordKey = item.RecordKey,
                StableId = item.StableId,
                SourceId = item.SourceId,
                DatasetId = item.DatasetId,
                RegionStableId = item.RegionStableId,
                MetricCode = item.MetricCode,
                NumericValue = item.NumericValue,
                TextValue = item.TextValue,
                UnitCode = item.UnitCode,
                EvidenceAsOfUtc = item.EvidenceAsOfUtc,
                CollectedAtUtc = item.CollectedAtUtc,
                SpatialPrecisionCode = item.SpatialPrecisionCode,
                TemporalPrecisionCode = item.TemporalPrecisionCode,
                QualityCode = item.QualityCode,
                LimitationCode = item.LimitationCode,
                DimensionKey = item.DimensionKey,
                SourceVersion = item.SourceVersion,
                DataRevision = item.DataRevision,
                FirstSeenAtUtc = item.FirstSeenAtUtc,
                LastSeenAtUtc = item.LastSeenAtUtc,
                RawSnapshotSourceId = item.RawSnapshot == null ? string.Empty : item.RawSnapshot.SourceId,
                RawSnapshotDatasetId = item.RawSnapshot == null ? string.Empty : item.RawSnapshot.DatasetId,
                RawSnapshotSourceVersion = item.RawSnapshot == null ? string.Empty : item.RawSnapshot.SourceVersion,
                RawSnapshotEvidenceAsOfUtc = item.RawSnapshot == null ? null : item.RawSnapshot.EvidenceAsOfUtc,
                RawSnapshotContentHashSha256 = item.RawSnapshot == null ? string.Empty : item.RawSnapshot.ContentHashSha256,
                RawSnapshotContentLength = item.RawSnapshot == null ? 0 : item.RawSnapshot.ContentLength,
                RawSnapshotContentType = item.RawSnapshot == null ? string.Empty : item.RawSnapshot.ContentType,
                RawSnapshotOriginalFileName = item.RawSnapshot == null ? string.Empty : item.RawSnapshot.OriginalFileName,
                RawSnapshotStorageContainer = item.RawSnapshot == null ? string.Empty : item.RawSnapshot.StorageContainer,
                RawSnapshotStorageObjectName = item.RawSnapshot == null ? string.Empty : item.RawSnapshot.StorageObjectName,
                RawSnapshotStorageLocation = item.RawSnapshot == null ? string.Empty : item.RawSnapshot.StorageLocation
            })
            .ToArrayAsync(cancellationToken);
}

public static class 사가정화면건물증거DataContract
{
    public const string SourceId = "sagajeong-presentation-building-evidence";
    public const string BindingDatasetId = "sagajeong-presentation-building-binding-ledger";
    public const string AddressDatasetId = "sagajeong-presentation-building-address-ledger";
    public const string RegionStableId =
        "area:kr:seoul:jungnang:sagajeong-station-0722-one-kilometer";
    public const string BindingRevision = "sagajeong-presentation-building-binding.r1";
    public const string AddressRevision = "sagajeong-presentation-building-address-assignment.r1";
    public const string BindingContentSha256 =
        "7B47519C1FC9D4D89A04B734710F48FE64BFE8C0EEC9264C947D5DC6206D1411";
    public const string AddressContentSha256 =
        "9908B700914DC9CB9AB22592E96A5A699FA3AE108F90063DB010CC9C8B870C33";
    public const string SourceVintage = "202608";
    public const string PendingHumanReview = "PendingHumanReview";
    public const string LimitationCode =
        "PrivateReviewOnly;ObservationPresentationOnly;NoParcelGeometry;NoOfficialPromotion;NoNearestInference;NoPublicApi;NoPublicDistribution;NoOperationalAuthority;NoUnityApply;NoDelivery;NoPrice;NoBusinessLocation;NoTraversal;NoGameplay";
    public const string PresentationOverlayRevision = "sagajeong-spatial-presentation.private-review.r2";
    public const string PresentationOverlayFileSha256 =
        "3EC6B95DC083F13F99042989E0B58957B3EE031E06C501F94022BE455DEC0A44";
    public const string PresentationOverlayContentSha256 =
        "138D1A47B286CE350CF339C1F69C6FFAD7778EBA7B6C95F3CF9F172F8248EBB1";
    public const string ReferenceMapRevision = "sagajeong-reference.r3";
    public const string ReferenceMapSha256 =
        "4B81E60C3C389A69AA765C8CC8D20E4457102C359A5FFBCD7EBDFA880F7E84E3";
    public const string BindingFileSha256 =
        "A11460EB08BA3D27188AE2BD5FFA7E3BCA22861140EB2973EE869078B5F17149";
    public const string AddressFileSha256 =
        "E751344083E98C28E3F4D6B153FA71E7679A7C47534D71B9721DEBE467596984";
    public const string BindingFileName = "presentation-building-bindings.json";
    public const string AddressFileName = "presentation-building-addresses.json";
    public const long BindingFileLength = 1_486_195;
    public const long AddressFileLength = 7_601_750;
    public const string RawSnapshotStorageContainer = "local-private-public-spatial";
    public const string RawSnapshotStorageFolder =
        "artifacts/local/public-data/sagajeong-presentation-building-evidence-20260914-r1";
    public const string BindingNormalizedDataRowsSha256 =
        "2426E8ABB083EFA3BA07B06675584768ECD1A30E97436E6CE1AED4A4ABA1CA47";
    public const string AddressNormalizedDataRowsSha256 =
        "07AF37D600F0B0DC080847F6E65FC1738AFFC827ED333E3ED173A6DC9758F20F";
    public const string AlD010Sha256 =
        "674C5A9583996DD6B8946525EDAD8197BE79A634F1DB00D39E2B3133D0D2A755";
    public const string MoisBuildingDatabaseZipSha256 =
        "4AA70C569AAF14F550313B5836346A1BA438491FB4CEC61877E615C9E602AFA7";
    public const string MoisBuildingRowsSha256 =
        "285286D60409E1CC93898146952D9BC4A805C8F955A765C9F0D72E8A6EA16081";
    public const string MoisRelatedParcelRowsSha256 =
        "DF8DBCA1823F1404CDE056C2E2255D97B8525164B2E7BEE4B52A0754B7519555";
    public const string ReferenceAddressLedgerSha256 =
        "86D318AFDC34BD3AF0E6470CCD49F29692A3F71B888FA0252C0B4AAAC84C80BF";
    public static readonly DateTimeOffset EvidenceAsOfUtc =
        new(2026, 8, 31, 0, 0, 0, TimeSpan.Zero);
    public static readonly DateTimeOffset DerivedAtUtc =
        new(2026, 9, 14, 0, 0, 0, TimeSpan.Zero);

    public const string BindingManifestMetric = "presentation-building-binding-manifest";
    public const string BindingSourceReceiptMetric = "presentation-building-binding-source-receipt";
    public const string PresentationBindingMetric = "presentation-building-binding-state";
    public const string ReferenceBindingMetric = "reference-building-binding-state";
    public const string BindingEdgeMetric = "presentation-reference-building-binding";
    public const string AddressManifestMetric = "presentation-building-address-manifest";
    public const string AddressSourceReceiptMetric = "presentation-building-address-source-receipt";
    public const string AddressAssignmentMetric = "presentation-building-address-assignment";
    public const string AddressCandidateMetric = "road-address-candidate-catalog";
}

public sealed class 역세권디오라마건물증거조회UseCase : I역세권디오라마건물증거조회UseCase
{
    private readonly I역세권디오라마건물증거RecordReader _reader;
    private readonly string _bindingNormalizedDataRowsSha256;
    private readonly string _addressNormalizedDataRowsSha256;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public 역세권디오라마건물증거조회UseCase(
        I역세권디오라마건물증거RecordReader reader)
        : this(
            reader,
            사가정화면건물증거DataContract.BindingNormalizedDataRowsSha256,
            사가정화면건물증거DataContract.AddressNormalizedDataRowsSha256)
    {
    }

    internal 역세권디오라마건물증거조회UseCase(
        I역세권디오라마건물증거RecordReader reader,
        string bindingNormalizedDataRowsSha256,
        string addressNormalizedDataRowsSha256)
    {
        _reader = reader;
        _bindingNormalizedDataRowsSha256 = bindingNormalizedDataRowsSha256;
        _addressNormalizedDataRowsSha256 = addressNormalizedDataRowsSha256;
    }

    public async Task<역세권디오라마건물증거ManifestDto?> ManifestAsync(
        string transitStationStableId,
        CancellationToken cancellationToken)
    {
        if (!IsSagajeong(transitStationStableId)) return null;
        var bindings = await PresentationBuildingBindingsAsync(transitStationStableId, cancellationToken)
            ?? throw Unavailable("SagajeongPresentationBuildingBindingLedgerMissing");
        var addresses = await PresentationBuildingAddressesAsync(transitStationStableId, cancellationToken)
            ?? throw Unavailable("SagajeongPresentationBuildingAddressLedgerMissing");

        var roadAddressCandidateBuildingCount = addresses.Assignments.Count(item => item.ParcelAddressCandidates.Count > 0);
        var projectionHash = Hash(bindings.ProjectionHashSha256, addresses.ProjectionHashSha256);
        return new 역세권디오라마건물증거ManifestDto
        {
            TransitStationStableId = StationDioramaPolicy.SagajeongTransitStationStableId,
            RegionStableId = 사가정화면건물증거DataContract.RegionStableId,
            SourceVintage = addresses.SourceVintage,
            EvidenceAsOf = DateOnly.ParseExact(addresses.EvidenceAsOf, "yyyy-MM-dd"),
            BindingLedgerRevision = bindings.Revision,
            BindingLedgerContentSha256 = bindings.ContentSha256,
            AddressLedgerRevision = addresses.Revision,
            AddressLedgerContentSha256 = addresses.ContentSha256,
            ProjectionHashSha256 = projectionHash,
            Summary = new 역세권디오라마건물증거집계Dto
            {
                PresentationBuildingCount = bindings.Summary.PresentationBuildingCount,
                ReferenceBuildingCount = bindings.Summary.ReferenceBuildingCount,
                BindingCount = bindings.Summary.BindingCount,
                UnresolvedReferenceBuildingCount = bindings.Summary.UnresolvedReferenceBuildingCount,
                ParcelIdentifierCount = addresses.Summary.ParcelIdentifierCount,
                UniqueParcelIdentifierCount = addresses.Summary.UniqueParcelIdentifierCount,
                RoadAddressCandidateBuildingCount = roadAddressCandidateBuildingCount,
                UnresolvedAddressBuildingCount = addresses.Summary.ResolutionStates["Unresolved"],
                OfficialPromotedCount = addresses.Summary.OfficialPromotedCount
            },
            EvidenceChecks =
            [
                new()
                {
                    EvidenceKindCode = 역세권디오라마건물증거KindCodes.RoadAddress,
                    CollectionStatusCode = 역세권디오라마건물증거CollectionStatusCodes.Collected,
                    CompletenessCode = 역세권디오라마건물증거CompletenessCodes.Complete,
                    CoveredBuildingCount = addresses.Summary.PresentationBuildingCount,
                    UseAuthorityGranted = false,
                    LimitationCode = $"PendingHumanReview;RawParcelAddressCandidateMissing:{addresses.Summary.RawParcelAddressCandidates.None};ResolutionStateUnresolved:{addresses.Summary.ResolutionStates["Unresolved"]}"
                },
                new()
                {
                    EvidenceKindCode = 역세권디오라마건물증거KindCodes.ParcelIdentifier,
                    CollectionStatusCode = 역세권디오라마건물증거CollectionStatusCodes.Collected,
                    CompletenessCode = 역세권디오라마건물증거CompletenessCodes.Complete,
                    CoveredBuildingCount = addresses.Summary.ParcelIdentifierCount,
                    UseAuthorityGranted = false,
                    LimitationCode = "ParcelIdentifierDoesNotProveParcelGeometry"
                },
                new()
                {
                    EvidenceKindCode = 역세권디오라마건물증거KindCodes.ParcelGeometry,
                    CollectionStatusCode = 역세권디오라마건물증거CollectionStatusCodes.NotCollected,
                    CompletenessCode = 역세권디오라마건물증거CompletenessCodes.Missing,
                    CoveredBuildingCount = 0,
                    UseAuthorityGranted = false,
                    LimitationCode = "ParcelGeometryNotCollected"
                }
            ],
            Boundary = PrivateBoundary()
        };
    }

    public async Task<화면건물결속LedgerDto?> PresentationBuildingBindingsAsync(
        string transitStationStableId,
        CancellationToken cancellationToken)
    {
        if (!IsSagajeong(transitStationStableId)) return null;
        var rows = await _reader.ReadAsync(
            사가정화면건물증거DataContract.SourceId,
            사가정화면건물증거DataContract.BindingDatasetId,
            사가정화면건물증거DataContract.RegionStableId,
            cancellationToken);
        ValidateRows(rows, 사가정화면건물증거DataContract.BindingDatasetId,
            사가정화면건물증거DataContract.BindingRevision, 5_211);

        var manifestRow = Single(rows, 사가정화면건물증거DataContract.BindingManifestMetric);
        RequireStableId(manifestRow, "station-diorama-evidence:sagajeong:binding-manifest:r1");
        var ledger = Deserialize<화면건물결속LedgerDto>(manifestRow.TextValue, "BindingManifestJsonInvalid");
        if (!string.Equals(ledger.NormalizedDataRowsSha256,
                _bindingNormalizedDataRowsSha256,
                StringComparison.Ordinal)
            || !string.Equals(NormalizedDataRowsHash(rows, 사가정화면건물증거DataContract.BindingManifestMetric),
                _bindingNormalizedDataRowsSha256,
                StringComparison.Ordinal))
            throw Unavailable("SagajeongPresentationBuildingBindingNormalizedRowsMismatch");
        var receipts = Rows(rows, 사가정화면건물증거DataContract.BindingSourceReceiptMetric)
            .Select(row => ParseReceipt(row, "binding-source"))
            .OrderBy(item => item.RoleCode, StringComparer.Ordinal)
            .ToList();
        var presentation = Rows(rows, 사가정화면건물증거DataContract.PresentationBindingMetric)
            .Select(ParsePresentationBinding)
            .OrderBy(item => item.PresentationBuildingStableId, StringComparer.Ordinal)
            .ToList();
        var references = Rows(rows, 사가정화면건물증거DataContract.ReferenceBindingMetric)
            .Select(ParseReferenceBinding)
            .OrderBy(item => item.ReferenceBuildingStableId, StringComparer.Ordinal)
            .ToList();
        var edges = Rows(rows, 사가정화면건물증거DataContract.BindingEdgeMetric)
            .Select(ParseBindingEdge)
            .OrderBy(item => item.PresentationBuildingStableId, StringComparer.Ordinal)
            .ToList();

        ledger.SourceReceipts = receipts;
        ledger.PresentationBuildings = presentation;
        ledger.ReferenceBuildings = references;
        ledger.Bindings = edges;
        ValidateBindingReceipts(receipts);
        ValidateBindingLedger(ledger);
        ledger.ProjectionHashSha256 = ledger.NormalizedDataRowsSha256;
        return ledger;
    }

    public async Task<화면건물주소LedgerDto?> PresentationBuildingAddressesAsync(
        string transitStationStableId,
        CancellationToken cancellationToken)
    {
        if (!IsSagajeong(transitStationStableId)) return null;
        var rows = await _reader.ReadAsync(
            사가정화면건물증거DataContract.SourceId,
            사가정화면건물증거DataContract.AddressDatasetId,
            사가정화면건물증거DataContract.RegionStableId,
            cancellationToken);
        ValidateRows(rows, 사가정화면건물증거DataContract.AddressDatasetId,
            사가정화면건물증거DataContract.AddressRevision, 7_582);

        var manifestRow = Single(rows, 사가정화면건물증거DataContract.AddressManifestMetric);
        RequireStableId(manifestRow, "station-diorama-evidence:sagajeong:address-manifest:r1");
        var ledger = Deserialize<화면건물주소LedgerDto>(manifestRow.TextValue, "AddressManifestJsonInvalid");
        if (!string.Equals(ledger.NormalizedDataRowsSha256,
                _addressNormalizedDataRowsSha256,
                StringComparison.Ordinal)
            || !string.Equals(NormalizedDataRowsHash(rows, 사가정화면건물증거DataContract.AddressManifestMetric),
                _addressNormalizedDataRowsSha256,
                StringComparison.Ordinal))
            throw Unavailable("SagajeongPresentationBuildingAddressNormalizedRowsMismatch");
        var receipts = Rows(rows, 사가정화면건물증거DataContract.AddressSourceReceiptMetric)
            .Select(row => ParseReceipt(row, "address-source"))
            .OrderBy(item => item.RoleCode, StringComparer.Ordinal)
            .ToList();
        var candidates = Rows(rows, 사가정화면건물증거DataContract.AddressCandidateMetric)
            .Select(ParseAddressCandidate)
            .ToDictionary(item => item.AddressStableId, StringComparer.Ordinal);
        var sourceHashes = SourceHashes(receipts);
        var assignments = Rows(rows, 사가정화면건물증거DataContract.AddressAssignmentMetric)
            .Select(row => ParseAddressAssignment(row, candidates, sourceHashes))
            .OrderBy(item => item.PresentationBuildingStableId, StringComparer.Ordinal)
            .ToList();

        ledger.SourceReceipts = receipts;
        ledger.Assignments = assignments;
        ValidateAddressReceipts(receipts);
        ValidateAddressLedger(ledger, candidates);
        ledger.ProjectionHashSha256 = ledger.NormalizedDataRowsSha256;
        return ledger;
    }

    private static void ValidateRows(
        IReadOnlyList<역세권디오라마건물증거정규Row> rows,
        string datasetId,
        string revision,
        int expectedCount)
    {
        if (rows.Count == 0) throw Unavailable("SagajeongPresentationBuildingEvidenceMissing");
        if (rows.Count != expectedCount) throw Unavailable("SagajeongPresentationBuildingEvidenceRowCountMismatch");
        var contentHash = string.Equals(datasetId, 사가정화면건물증거DataContract.BindingDatasetId, StringComparison.Ordinal)
            ? 사가정화면건물증거DataContract.BindingContentSha256
            : 사가정화면건물증거DataContract.AddressContentSha256;
        var expectedSourceVersion = revision + ";content-sha256=" + contentHash.ToLowerInvariant();
        if (rows.Any(row => !string.Equals(row.SourceId, 사가정화면건물증거DataContract.SourceId, StringComparison.Ordinal)
                            || !string.Equals(row.DatasetId, datasetId, StringComparison.Ordinal)
                            || !string.Equals(row.RegionStableId, 사가정화면건물증거DataContract.RegionStableId, StringComparison.Ordinal)
                            || !string.Equals(row.DataRevision, revision, StringComparison.Ordinal)
                            || row.NumericValue is not null
                            || row.EvidenceAsOfUtc.ToUniversalTime() != 사가정화면건물증거DataContract.EvidenceAsOfUtc
                            || row.CollectedAtUtc.ToUniversalTime() != 사가정화면건물증거DataContract.DerivedAtUtc
                            || row.FirstSeenAtUtc.ToUniversalTime() != 사가정화면건물증거DataContract.DerivedAtUtc
                            || row.LastSeenAtUtc.ToUniversalTime() != 사가정화면건물증거DataContract.DerivedAtUtc
                            || !string.Equals(row.QualityCode, 사가정화면건물증거DataContract.PendingHumanReview, StringComparison.Ordinal)
                            || string.IsNullOrWhiteSpace(row.RecordKey)
                            || string.IsNullOrWhiteSpace(row.StableId)
                            || string.IsNullOrWhiteSpace(row.TextValue)
                            || !string.Equals(row.LimitationCode, 사가정화면건물증거DataContract.LimitationCode, StringComparison.Ordinal)
                            || !string.Equals(row.TemporalPrecisionCode, "month-end-derived-snapshot", StringComparison.Ordinal)
                            || !string.Equals(row.SourceVersion, expectedSourceVersion, StringComparison.Ordinal)
                            || !string.Equals(row.RecordKey, 외부데이터RecordKey.Create(
                                row.SourceId,
                                row.DatasetId,
                                row.RegionStableId,
                                row.MetricCode,
                                row.EvidenceAsOfUtc,
                                row.DimensionKey), StringComparison.Ordinal)
                            || !ValidateRowContract(row)
                            || !ValidateRawSnapshotContract(row, datasetId, expectedSourceVersion)))
            throw Unavailable("SagajeongPresentationBuildingEvidenceMetadataMismatch");
        if (rows.GroupBy(row => row.RecordKey, StringComparer.Ordinal).Any(group => group.Count() != 1)
            || rows.GroupBy(row => row.StableId, StringComparer.Ordinal).Any(group => group.Count() != 1)
            || rows.Select(row => row.RawSnapshotId).Distinct().Count() != 1)
            throw Unavailable("SagajeongPresentationBuildingEvidenceDuplicateRow");
    }

    private static bool ValidateRowContract(역세권디오라마건물증거정규Row row)
    {
        var expected = row.MetricCode switch
        {
            사가정화면건물증거DataContract.BindingManifestMetric =>
                ("bindingManifest", "kind=binding-manifest;", "private-derived-ledger", "ledger-manifest"),
            사가정화면건물증거DataContract.BindingSourceReceiptMetric =>
                ("bindingSourceReceipt", "kind=binding-source-receipt;", "frozen-source-hash", "source-receipt"),
            사가정화면건물증거DataContract.PresentationBindingMetric =>
                ("presentationBinding", "kind=presentation-binding;", "presentation-footprint-binding-candidate", "binding-state"),
            사가정화면건물증거DataContract.ReferenceBindingMetric =>
                ("referenceBinding", "kind=reference-binding;", "reference-footprint-binding-candidate", "binding-state"),
            사가정화면건물증거DataContract.BindingEdgeMetric =>
                ("bindingEdge", "kind=binding-edge;", "one-to-one-footprint-binding-candidate", "binding-edge"),
            사가정화면건물증거DataContract.AddressManifestMetric =>
                ("addressManifest", "kind=address-manifest;", "private-derived-ledger", "ledger-manifest"),
            사가정화면건물증거DataContract.AddressSourceReceiptMetric =>
                ("addressSourceReceipt", "kind=address-source-receipt;", "frozen-source-hash", "source-receipt"),
            사가정화면건물증거DataContract.AddressAssignmentMetric =>
                ("addressAssignment", "kind=address-assignment;", "exact-pnu-address-candidate", "address-assignment"),
            사가정화면건물증거DataContract.AddressCandidateMetric =>
                ("addressCandidate", "kind=address-candidate;", "official-road-address-candidate", "road-address-candidate"),
            _ => default
        };
        if (expected == default
            || !row.DimensionKey.StartsWith(expected.Item2, StringComparison.Ordinal)
            || !string.Equals(row.SpatialPrecisionCode, expected.Item3, StringComparison.Ordinal)
            || !string.Equals(row.UnitCode, expected.Item4, StringComparison.Ordinal))
            return false;
        try
        {
            using var document = JsonDocument.Parse(row.TextValue);
            return document.RootElement.ValueKind == JsonValueKind.Object
                   && document.RootElement.TryGetProperty("rowType", out var rowType)
                   && string.Equals(rowType.GetString(), expected.Item1, StringComparison.Ordinal);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool ValidateRawSnapshotContract(
        역세권디오라마건물증거정규Row row,
        string datasetId,
        string expectedSourceVersion)
    {
        var binding = string.Equals(
            datasetId,
            사가정화면건물증거DataContract.BindingDatasetId,
            StringComparison.Ordinal);
        var fileName = binding
            ? 사가정화면건물증거DataContract.BindingFileName
            : 사가정화면건물증거DataContract.AddressFileName;
        var fileHash = binding
            ? 사가정화면건물증거DataContract.BindingFileSha256
            : 사가정화면건물증거DataContract.AddressFileSha256;
        var fileLength = binding
            ? 사가정화면건물증거DataContract.BindingFileLength
            : 사가정화면건물증거DataContract.AddressFileLength;
        var storageObjectName = 사가정화면건물증거DataContract.RawSnapshotStorageFolder + "/" + fileName;

        return row.RawSnapshotId > 0
               && string.Equals(row.RawSnapshotSourceId, 사가정화면건물증거DataContract.SourceId, StringComparison.Ordinal)
               && string.Equals(row.RawSnapshotDatasetId, datasetId, StringComparison.Ordinal)
               && string.Equals(row.RawSnapshotSourceVersion, expectedSourceVersion, StringComparison.Ordinal)
               && row.RawSnapshotEvidenceAsOfUtc?.ToUniversalTime() == 사가정화면건물증거DataContract.EvidenceAsOfUtc
               && string.Equals(row.RawSnapshotContentHashSha256, fileHash, StringComparison.OrdinalIgnoreCase)
               && row.RawSnapshotContentLength == fileLength
               && string.Equals(row.RawSnapshotContentType, "application/json", StringComparison.Ordinal)
               && string.Equals(row.RawSnapshotOriginalFileName, fileName, StringComparison.Ordinal)
               && string.Equals(row.RawSnapshotStorageContainer,
                   사가정화면건물증거DataContract.RawSnapshotStorageContainer, StringComparison.Ordinal)
               && string.Equals(row.RawSnapshotStorageObjectName, storageObjectName, StringComparison.Ordinal)
               && string.Equals(row.RawSnapshotStorageLocation, "private-file://" + storageObjectName, StringComparison.Ordinal);
    }

    private static void ValidateBindingLedger(화면건물결속LedgerDto ledger)
    {
        if (!string.Equals(ledger.SchemaVersion, 역세권디오라마건물증거SchemaVersions.BindingLedgerV1, StringComparison.Ordinal)
            || !string.Equals(ledger.Revision, 사가정화면건물증거DataContract.BindingRevision, StringComparison.Ordinal)
            || !string.Equals(ledger.StationStableId, StationDioramaPolicy.SagajeongTransitStationStableId, StringComparison.Ordinal)
            || !string.Equals(ledger.RegionStableId, 사가정화면건물증거DataContract.RegionStableId, StringComparison.Ordinal)
            || !string.Equals(ledger.ContentSha256, 사가정화면건물증거DataContract.BindingContentSha256, StringComparison.Ordinal)
            || !string.Equals(ledger.PresentationOverlayRevision, 사가정화면건물증거DataContract.PresentationOverlayRevision, StringComparison.Ordinal)
            || !string.Equals(ledger.PresentationOverlayFileSha256, 사가정화면건물증거DataContract.PresentationOverlayFileSha256, StringComparison.Ordinal)
            || !string.Equals(ledger.PresentationOverlayContentSha256, 사가정화면건물증거DataContract.PresentationOverlayContentSha256, StringComparison.Ordinal)
            || !string.Equals(ledger.ReferenceMapRevision, 사가정화면건물증거DataContract.ReferenceMapRevision, StringComparison.Ordinal)
            || !string.Equals(ledger.ReferenceMapSha256, 사가정화면건물증거DataContract.ReferenceMapSha256, StringComparison.Ordinal)
            || !string.Equals(ledger.FileSha256, 사가정화면건물증거DataContract.BindingFileSha256, StringComparison.Ordinal)
            || ledger.SourceReceipts.Count != 2
            || ledger.PresentationBuildings.Count != 4_062
            || ledger.ReferenceBuildings.Count != 602
            || ledger.Bindings.Count != 544
            || ledger.Summary.PresentationBuildingCount != 4_062
            || ledger.Summary.ReferenceBuildingCount != 602
            || ledger.Summary.BindingCount != 544
            || ledger.Summary.UnresolvedReferenceBuildingCount != 58)
            throw Unavailable("SagajeongPresentationBuildingBindingManifestMismatch");
        ValidatePrivateBoundary(ledger.Boundary, binding: true);

        var expectedStates = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["AmbiguousGlobalAliasExcluded"] = 88,
            ["AmbiguousMultipleFootprintsExcluded"] = 2,
            ["BackdropOnly"] = 3_423,
            ["Bound"] = 544,
            ["WeakFootprintCandidateExcluded"] = 5
        };
        var actualStates = ledger.PresentationBuildings.GroupBy(item => item.BindingState, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        if (!DictionaryEqual(ledger.Summary.PresentationBindingStates, expectedStates)
            || !DictionaryEqual(actualStates, expectedStates)
            || ledger.PresentationBuildings.GroupBy(item => item.PresentationBuildingStableId, StringComparer.Ordinal).Any(group => group.Count() != 1)
            || ledger.ReferenceBuildings.GroupBy(item => item.ReferenceBuildingStableId, StringComparer.Ordinal).Any(group => group.Count() != 1))
            throw Unavailable("SagajeongPresentationBuildingBindingStateMismatch");

        var presentation = ledger.PresentationBuildings.ToDictionary(
            item => item.PresentationBuildingStableId, StringComparer.Ordinal);
        var references = ledger.ReferenceBuildings.ToDictionary(
            item => item.ReferenceBuildingStableId, StringComparer.Ordinal);
        var edgePairs = ledger.Bindings.Select(item => item.PresentationBuildingStableId + "|" + item.ReferenceBuildingStableId)
            .ToHashSet(StringComparer.Ordinal);
        if (edgePairs.Count != 544
            || ledger.Bindings.Any(edge => !presentation.ContainsKey(edge.PresentationBuildingStableId)
                                           || !references.ContainsKey(edge.ReferenceBuildingStableId)
                                           || !string.Equals(edge.BindingMethod, "FootprintOverlap", StringComparison.Ordinal))
            || ledger.PresentationBuildings.Any(item =>
                string.Equals(item.BindingState, "Bound", StringComparison.Ordinal)
                    ? item.ReferenceBuildingStableIds.Count != 1
                      || !edgePairs.Contains(item.PresentationBuildingStableId + "|" + item.ReferenceBuildingStableIds[0])
                    : item.ReferenceBuildingStableIds.Count != 0)
            || ledger.ReferenceBuildings.Any(item =>
                string.Equals(item.BindingState, "Bound", StringComparison.Ordinal)
                    ? string.IsNullOrWhiteSpace(item.PresentationBuildingStableId)
                      || !edgePairs.Contains(item.PresentationBuildingStableId + "|" + item.ReferenceBuildingStableId)
                    : !string.Equals(item.BindingState, "UnresolvedNoUniquePresentation", StringComparison.Ordinal)
                      || !string.IsNullOrEmpty(item.PresentationBuildingStableId)))
            throw Unavailable("SagajeongPresentationBuildingBindingRelationshipMismatch");
    }

    private static void ValidateAddressLedger(
        화면건물주소LedgerDto ledger,
        IReadOnlyDictionary<string, 도로명주소후보Dto> candidates)
    {
        if (!string.Equals(ledger.SchemaVersion, 역세권디오라마건물증거SchemaVersions.AddressLedgerV1, StringComparison.Ordinal)
            || !string.Equals(ledger.Revision, 사가정화면건물증거DataContract.AddressRevision, StringComparison.Ordinal)
            || !string.Equals(ledger.StationStableId, StationDioramaPolicy.SagajeongTransitStationStableId, StringComparison.Ordinal)
            || !string.Equals(ledger.RegionStableId, 사가정화면건물증거DataContract.RegionStableId, StringComparison.Ordinal)
            || !string.Equals(ledger.SourceVintage, 사가정화면건물증거DataContract.SourceVintage, StringComparison.Ordinal)
            || !string.Equals(ledger.EvidenceAsOf, "2026-08-31", StringComparison.Ordinal)
            || !string.Equals(ledger.BindingLedgerRevision, 사가정화면건물증거DataContract.BindingRevision, StringComparison.Ordinal)
            || !string.Equals(ledger.BindingLedgerContentSha256, 사가정화면건물증거DataContract.BindingContentSha256, StringComparison.Ordinal)
            || !string.Equals(ledger.ContentSha256, 사가정화면건물증거DataContract.AddressContentSha256, StringComparison.Ordinal)
            || !string.Equals(ledger.FileSha256, 사가정화면건물증거DataContract.AddressFileSha256, StringComparison.Ordinal)
            || ledger.SourceReceipts.Count != 5
            || ledger.Assignments.Count != 4_062
            || candidates.Count != 3_514)
            throw Unavailable("SagajeongPresentationBuildingAddressManifestMismatch");
        ValidatePrivateBoundary(ledger.Boundary, binding: false);

        var summary = ledger.Summary;
        var expectedResolutionStates = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["CrossSourceConflict"] = 18,
            ["MultipleAddressCandidates"] = 47,
            ["ParcelAddressCandidate"] = 3_278,
            ["ReferenceBindingCandidate"] = 516,
            ["Unresolved"] = 203
        };
        if (summary.PresentationBuildingCount != 4_062
            || summary.ParcelIdentifierCount != 4_062
            || summary.UniqueParcelIdentifierCount != 3_774
            || summary.LegalDongAndJibunCount != 4_062
            || summary.SourceBuildingIdentifierCount != 3_796
            || summary.SourceUfidCount != 4_034
            || summary.RawParcelAddressCandidates.Single != 3_796
            || summary.RawParcelAddressCandidates.Multiple != 60
            || summary.RawParcelAddressCandidates.None != 206
            || summary.ReferenceOfficialAddressCount != 511
            || summary.CrossSourceReconciliation.Agreement != 491
            || summary.CrossSourceReconciliation.Conflict != 18
            || summary.CrossSourceReconciliation.ParcelCandidateMissing != 2
            || summary.OfficialPromotedCount != 0
            || summary.NearestAddressInferenceCount != 0
            || !DictionaryEqual(summary.ResolutionStates, expectedResolutionStates))
            throw Unavailable("SagajeongPresentationBuildingAddressSummaryMismatch");

        var actualResolutionStates = ledger.Assignments.GroupBy(item => item.ResolutionState, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        var actualReconciliationStates = ledger.Assignments.GroupBy(item => item.ReconciliationState, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        var expectedReconciliationStates = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["Agreement"] = 491,
            ["Conflict"] = 18,
            ["NotBound"] = 3_528,
            ["ParcelCandidateMissing"] = 2,
            ["ReferenceCandidateOnly"] = 23
        };
        var actualCandidatePartition = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["single"] = ledger.Assignments.Count(item => item.ParcelAddressCandidates.Count == 1),
            ["multiple"] = ledger.Assignments.Count(item => item.ParcelAddressCandidates.Count > 1),
            ["none"] = ledger.Assignments.Count(item => item.ParcelAddressCandidates.Count == 0)
        };
        var expectedCandidatePartition = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["single"] = 3_796,
            ["multiple"] = 60,
            ["none"] = 206
        };
        var referencedCandidateIds = ledger.Assignments
            .SelectMany(item => item.ParcelAddressCandidates)
            .Select(item => item.AddressStableId)
            .ToHashSet(StringComparer.Ordinal);
        if (!DictionaryEqual(actualResolutionStates, expectedResolutionStates)
            || !DictionaryEqual(actualReconciliationStates, expectedReconciliationStates)
            || !DictionaryEqual(actualCandidatePartition, expectedCandidatePartition)
            || ledger.Assignments.GroupBy(item => item.PresentationBuildingStableId, StringComparer.Ordinal).Any(group => group.Count() != 1)
            || ledger.Assignments.Select(item => item.ParcelIdentifierPnu).Distinct(StringComparer.Ordinal).Count() != 3_774
            || ledger.Assignments.Count(item => !string.IsNullOrEmpty(item.SourceBuildingIdentifier)) != 3_796
            || ledger.Assignments.Count(item => !string.IsNullOrEmpty(item.SourceUfid)) != 4_034
            || ledger.Assignments.Count(item => item.ReferenceAddressResolutionState.StartsWith("Official", StringComparison.Ordinal)) != 511
            || ledger.Assignments.Any(item => item.ParcelIdentifierPnu.Length != 19
                                              || item.ParcelIdentifierPnu.Any(character => !char.IsAsciiDigit(character))
                                              || !item.ParcelIdentifierPnu.StartsWith(item.LegalDongCode, StringComparison.Ordinal)
                                              || !string.Equals(item.LegalDongCode, "1126010100", StringComparison.Ordinal)
                                              || !string.Equals(item.LegalDongName, "면목동", StringComparison.Ordinal)
                                              || string.IsNullOrWhiteSpace(item.Jibun)
                                              || !string.Equals(item.AssignmentMethod,
                                                  "FrozenPresentationBindingThenExactPnuAndOfficialRelatedParcel",
                                                  StringComparison.Ordinal)
                                              || item.ParcelAddressCandidates.GroupBy(candidate => candidate.AddressStableId, StringComparer.Ordinal)
                                                  .Any(group => group.Count() != 1)
                                              || item.ParcelAddressCandidates.Any(candidate => candidate.EvidenceMethods.Count == 0)
                                              || !string.Equals(item.SourceVintage, 사가정화면건물증거DataContract.SourceVintage, StringComparison.Ordinal)
                                              || !item.ObservationPresentationOnly
                                              || item.DistributionApproved
                                              || item.DeliveryEligible
                                              || item.PriceObservationEligible
                                              || item.BusinessLocationEligible
                                              || item.UnityApplyAllowed
                                              || item.TraversalReady
                                              || item.GameplayReady)
            || !referencedCandidateIds.SetEquals(candidates.Keys))
            throw Unavailable("SagajeongPresentationBuildingAddressAssignmentMismatch");
        if (candidates.Values.Any(candidate => string.IsNullOrWhiteSpace(candidate.OfficialCompositeKey)
                                               || candidate.OfficialBuildingManagementNumbers.Any(number =>
                                                   number.Length != 25 || number.Any(character => !char.IsAsciiDigit(character)))))
            throw Unavailable("SagajeongPresentationBuildingAddressCandidateMismatch");
    }

    private static void ValidateBindingReceipts(
        IReadOnlyList<화면건물주소출처영수증Dto> receipts)
    {
        var expected = new Dictionary<string, (string File, string Hash, string Revision, string? ContentHash)>(StringComparer.Ordinal)
        {
            ["PresentationOverlay"] = (
                "private-review.json",
                사가정화면건물증거DataContract.PresentationOverlayFileSha256,
                사가정화면건물증거DataContract.PresentationOverlayRevision,
                사가정화면건물증거DataContract.PresentationOverlayContentSha256),
            ["ReferenceMap"] = (
                "SagajeongReference.json",
                사가정화면건물증거DataContract.ReferenceMapSha256,
                사가정화면건물증거DataContract.ReferenceMapRevision,
                null)
        };
        ValidateReceiptSet(receipts, expected, "SagajeongPresentationBuildingBindingSourceReceiptMismatch");
    }

    private static void ValidateAddressReceipts(
        IReadOnlyList<화면건물주소출처영수증Dto> receipts)
    {
        var expected = new Dictionary<string, (string File, string Hash, string Revision, string? ContentHash)>(StringComparer.Ordinal)
        {
            ["PresentationBuildingAndParcelIdentifier"] = (
                "AL_D010_11_20260809.zip", 사가정화면건물증거DataContract.AlD010Sha256, "AL_D010:Seoul:20260809", null),
            ["RoadAddressBuildingDatabase"] = (
                "202608_건물DB_전체분.zip", 사가정화면건물증거DataContract.MoisBuildingDatabaseZipSha256, "202608", null),
            ["RoadAddressBuildingRows"] = (
                "build_seoul.txt", 사가정화면건물증거DataContract.MoisBuildingRowsSha256, "202608", null),
            ["RoadAddressRelatedParcelRows"] = (
                "jibun_seoul.txt", 사가정화면건물증거DataContract.MoisRelatedParcelRowsSha256, "202608", null),
            ["ReferenceBuildingAddressAssignment"] = (
                "building-address-assignments.json", 사가정화면건물증거DataContract.ReferenceAddressLedgerSha256,
                "sagajeong-building-address-assignment.r1", null)
        };
        ValidateReceiptSet(receipts, expected, "SagajeongPresentationBuildingAddressSourceReceiptMismatch");
    }

    private static void ValidateReceiptSet(
        IReadOnlyList<화면건물주소출처영수증Dto> receipts,
        IReadOnlyDictionary<string, (string File, string Hash, string Revision, string? ContentHash)> expected,
        string code)
    {
        if (receipts.Count != expected.Count
            || receipts.GroupBy(item => item.RoleCode, StringComparer.Ordinal).Any(group => group.Count() != 1)
            || receipts.Any(item => !expected.TryGetValue(item.RoleCode, out var value)
                                    || !string.Equals(item.FileName, value.File, StringComparison.Ordinal)
                                    || !string.Equals(item.Sha256, value.Hash, StringComparison.Ordinal)
                                    || !string.Equals(item.SourceRevision, value.Revision, StringComparison.Ordinal)
                                    || !string.Equals(item.ContentSha256, value.ContentHash, StringComparison.Ordinal)))
            throw Unavailable(code);
    }

    private static 화면건물주소출처영수증Dto ParseReceipt(
        역세권디오라마건물증거정규Row row,
        string stableIdSegment)
    {
        var value = Deserialize<화면건물주소출처영수증Dto>(row.TextValue, "EvidenceSourceReceiptJsonInvalid");
        if (string.IsNullOrWhiteSpace(value.RoleCode)
            || string.IsNullOrWhiteSpace(value.FileName)
            || !IsSha256(value.Sha256)
            || string.IsNullOrWhiteSpace(value.SourceRevision)
            || !string.Equals(row.StableId,
                $"station-diorama-evidence:sagajeong:{stableIdSegment}:{value.RoleCode.ToLowerInvariant()}",
                StringComparison.Ordinal))
            throw Unavailable("EvidenceSourceReceiptMismatch");
        return value;
    }

    private static 화면건물결속상태Dto ParsePresentationBinding(
        역세권디오라마건물증거정규Row row)
    {
        var value = Deserialize<화면건물결속상태Dto>(row.TextValue, "PresentationBuildingBindingJsonInvalid");
        if (!string.Equals(row.StableId, "presentation-building-binding:" + value.PresentationBuildingStableId, StringComparison.Ordinal)
            || !string.Equals(value.PresentationBuildingStableId, "vworld:al-d010:" + value.SourceFeatureId, StringComparison.Ordinal))
            throw Unavailable("PresentationBuildingBindingIdentityMismatch");
        return value;
    }

    private static 참고건물결속상태Dto ParseReferenceBinding(
        역세권디오라마건물증거정규Row row)
    {
        var value = Deserialize<참고건물결속상태Dto>(row.TextValue, "ReferenceBuildingBindingJsonInvalid");
        if (!string.Equals(row.StableId, "reference-building-binding:" + value.ReferenceBuildingStableId, StringComparison.Ordinal))
            throw Unavailable("ReferenceBuildingBindingIdentityMismatch");
        return value;
    }

    private static 화면참고건물결속Dto ParseBindingEdge(
        역세권디오라마건물증거정규Row row)
    {
        var value = Deserialize<화면참고건물결속Dto>(row.TextValue, "PresentationReferenceBuildingBindingJsonInvalid");
        var expected = "presentation-reference-building-binding:"
                       + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
                           value.PresentationBuildingStableId + "|" + value.ReferenceBuildingStableId)))[..24].ToLowerInvariant();
        if (!string.Equals(row.StableId, expected, StringComparison.Ordinal))
            throw Unavailable("PresentationReferenceBuildingBindingIdentityMismatch");
        return value;
    }

    private static 도로명주소후보Dto ParseAddressCandidate(
        역세권디오라마건물증거정규Row row)
    {
        var value = Deserialize<도로명주소후보Dto>(row.TextValue, "RoadAddressCandidateJsonInvalid");
        var expectedStableId = string.IsNullOrWhiteSpace(value.OfficialCompositeKey)
            ? string.Empty
            : "road-address:kr:" + Convert.ToHexString(SHA256.HashData(
                Encoding.UTF8.GetBytes(value.OfficialCompositeKey)))[..24].ToLowerInvariant();
        if (!string.Equals(row.StableId, value.AddressStableId, StringComparison.Ordinal)
            || !string.Equals(value.AddressStableId, expectedStableId, StringComparison.Ordinal))
            throw Unavailable("RoadAddressCandidateIdentityMismatch");
        return value;
    }

    private static 화면건물주소할당Dto ParseAddressAssignment(
        역세권디오라마건물증거정규Row row,
        IReadOnlyDictionary<string, 도로명주소후보Dto> candidates,
        화면건물주소원천HashDto sourceHashes)
    {
        var value = Deserialize<AddressAssignmentPayload>(row.TextValue, "PresentationBuildingAddressAssignmentJsonInvalid");
        if (!string.Equals(row.StableId, "presentation-building-address:" + value.PresentationBuildingStableId, StringComparison.Ordinal)
            || !string.Equals(value.PresentationBuildingStableId, "vworld:al-d010:" + value.SourceFeatureId, StringComparison.Ordinal))
            throw Unavailable("PresentationBuildingAddressAssignmentIdentityMismatch");
        var joined = new List<도로명주소후보Dto>();
        foreach (var reference in value.CandidateRefs)
        {
            if (!candidates.TryGetValue(reference.AddressStableId, out var candidate))
                throw Unavailable("PresentationBuildingAddressCandidateMissing");
            joined.Add(new 도로명주소후보Dto
            {
                AddressStableId = candidate.AddressStableId,
                OfficialCompositeKey = candidate.OfficialCompositeKey,
                CanonicalRoadAddress = candidate.CanonicalRoadAddress,
                OfficialBuildingManagementNumbers = [.. candidate.OfficialBuildingManagementNumbers],
                EvidenceMethods = [.. reference.EvidenceMethods]
            });
        }
        return new 화면건물주소할당Dto
        {
            PresentationBuildingStableId = value.PresentationBuildingStableId,
            SourceFeatureId = value.SourceFeatureId,
            ParcelIdentifierPnu = value.ParcelIdentifierPnu,
            LegalDongCode = value.LegalDongCode,
            LegalDongName = value.LegalDongName,
            Jibun = value.Jibun,
            SourceBuildingIdentifier = value.SourceBuildingIdentifier,
            SourceUfid = value.SourceUfid,
            ReferenceBuildingStableId = value.ReferenceBuildingStableId,
            ReferenceAddressResolutionState = value.ReferenceAddressResolutionState,
            ReferenceOfficialCompositeKeys = [.. value.ReferenceOfficialCompositeKeys],
            ParcelAddressCandidates = joined,
            ResolutionState = value.ResolutionState,
            ReconciliationState = value.ReconciliationState,
            AssignmentMethod = value.AssignmentMethod,
            UnresolvedReason = value.UnresolvedReason,
            SourceVintage = 사가정화면건물증거DataContract.SourceVintage,
            SourceHashes = sourceHashes,
            ObservationPresentationOnly = true,
            DistributionApproved = false,
            DeliveryEligible = false,
            PriceObservationEligible = false,
            BusinessLocationEligible = false,
            UnityApplyAllowed = false,
            TraversalReady = false,
            GameplayReady = false
        };
    }

    private static 화면건물주소원천HashDto SourceHashes(
        IReadOnlyList<화면건물주소출처영수증Dto> receipts)
    {
        string HashFor(string role) => receipts.SingleOrDefault(item => string.Equals(item.RoleCode, role, StringComparison.Ordinal))?.Sha256
                                       ?? throw Unavailable("SagajeongPresentationBuildingAddressSourceReceiptMissing");
        return new 화면건물주소원천HashDto
        {
            AlD010Sha256 = HashFor("PresentationBuildingAndParcelIdentifier"),
            MoisBuildingDatabaseZipSha256 = HashFor("RoadAddressBuildingDatabase"),
            MoisRelatedParcelSha256 = HashFor("RoadAddressRelatedParcelRows")
        };
    }

    private static void ValidatePrivateBoundary(역세권디오라마비권위경계Dto boundary, bool binding)
    {
        if (!boundary.ObservationPresentationOnly
            || boundary.DistributionApproved
            || boundary.DeliveryEligible
            || boundary.PriceObservationEligible
            || boundary.BusinessLocationEligible
            || boundary.UnityApplyAllowed
            || boundary.TraversalReady
            || boundary.GameplayReady
            || binding && (boundary.GeometryIncluded || boundary.NearestBindingInferenceAllowed)
            || !binding && (boundary.ParcelGeometryCollected
                            || boundary.NearestAddressInferenceAllowed
                            || boundary.OfficialBuildingIdentityConfirmed))
            throw Unavailable("SagajeongPresentationBuildingEvidenceAuthorityBoundaryMismatch");
    }

    private static 역세권디오라마비권위경계Dto PrivateBoundary() => new()
    {
        ObservationPresentationOnly = true,
        DistributionApproved = false,
        DeliveryEligible = false,
        PriceObservationEligible = false,
        BusinessLocationEligible = false,
        UnityApplyAllowed = false,
        TraversalReady = false,
        GameplayReady = false,
        GeometryIncluded = false,
        NearestBindingInferenceAllowed = false,
        ParcelGeometryCollected = false,
        NearestAddressInferenceAllowed = false,
        OfficialBuildingIdentityConfirmed = false
    };

    private static T Deserialize<T>(string json, string code) where T : class
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object) throw Unavailable(code);
            return JsonSerializer.Deserialize<T>(document.RootElement, JsonOptions) ?? throw Unavailable(code);
        }
        catch (역세권디오라마건물증거UnavailableException)
        {
            throw;
        }
        catch (JsonException)
        {
            throw Unavailable(code);
        }
    }

    private static 역세권디오라마건물증거정규Row Single(
        IReadOnlyList<역세권디오라마건물증거정규Row> rows,
        string metric)
    {
        var matches = Rows(rows, metric);
        return matches.Count == 1 ? matches[0] : throw Unavailable("SagajeongPresentationBuildingEvidenceManifestCountMismatch");
    }

    private static IReadOnlyList<역세권디오라마건물증거정규Row> Rows(
        IReadOnlyList<역세권디오라마건물증거정규Row> rows,
        string metric) => rows.Where(row => string.Equals(row.MetricCode, metric, StringComparison.Ordinal)).ToArray();

    private static void RequireStableId(역세권디오라마건물증거정규Row row, string expected)
    {
        if (!string.Equals(row.StableId, expected, StringComparison.Ordinal))
            throw Unavailable("SagajeongPresentationBuildingEvidenceManifestIdentityMismatch");
    }

    internal static string NormalizedDataRowsHash(
        IReadOnlyList<역세권디오라마건물증거정규Row> rows,
        string manifestMetric)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var row in rows
                     .Where(item => !string.Equals(item.MetricCode, manifestMetric, StringComparison.Ordinal))
                     .OrderBy(item => item.RecordKey, StringComparer.Ordinal))
        {
            Append(hash, row.RecordKey);
            Append(hash, row.StableId);
            Append(hash, row.MetricCode);
            Append(hash, row.TextValue);
            Append(hash, row.DataRevision);
        }
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private static void Append(IncrementalHash hash, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, bytes.Length);
        hash.AppendData(length);
        hash.AppendData(bytes);
    }

    private static string Hash(params string[] values)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("|", values))));

    private static bool DictionaryEqual(
        IReadOnlyDictionary<string, int> left,
        IReadOnlyDictionary<string, int> right)
        => left.Count == right.Count && left.All(pair => right.TryGetValue(pair.Key, out var value) && value == pair.Value);

    private static bool IsSha256(string value)
        => value.Length == 64 && value.All(Uri.IsHexDigit);

    private static bool IsSagajeong(string value)
        => string.Equals(value, StationDioramaPolicy.SagajeongTransitStationStableId, StringComparison.Ordinal);

    private static 역세권디오라마건물증거UnavailableException Unavailable(string code) => new(code);

    private sealed class AddressAssignmentPayload
    {
        public string PresentationBuildingStableId { get; set; } = string.Empty;
        public string SourceFeatureId { get; set; } = string.Empty;
        public string ParcelIdentifierPnu { get; set; } = string.Empty;
        public string LegalDongCode { get; set; } = string.Empty;
        public string LegalDongName { get; set; } = string.Empty;
        public string Jibun { get; set; } = string.Empty;
        public string SourceBuildingIdentifier { get; set; } = string.Empty;
        public string SourceUfid { get; set; } = string.Empty;
        public string ReferenceBuildingStableId { get; set; } = string.Empty;
        public string ReferenceAddressResolutionState { get; set; } = string.Empty;
        public List<string> ReferenceOfficialCompositeKeys { get; set; } = [];
        public List<AddressCandidateReferencePayload> CandidateRefs { get; set; } = [];
        public string ResolutionState { get; set; } = string.Empty;
        public string ReconciliationState { get; set; } = string.Empty;
        public string AssignmentMethod { get; set; } = string.Empty;
        public string UnresolvedReason { get; set; } = string.Empty;
    }

    private sealed class AddressCandidateReferencePayload
    {
        public string AddressStableId { get; set; } = string.Empty;
        public List<string> EvidenceMethods { get; set; } = [];
    }
}
