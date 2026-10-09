namespace 살뜰.도메인.창고;

public sealed class 분산사진증빙Record
{
    public string EvidenceId { get; set; } = "";
    public string ActorUserId { get; set; } = "";
    public Guid UploadRequestId { get; set; }
    public Guid CompletionRequestId { get; set; }
    public string Kind { get; set; } = "";
    public string AggregateId { get; set; } = "";
    public string BindingDigest { get; set; } = "";
    public string InputSha256 { get; set; } = "";
    public string StoredSha256 { get; set; } = "";
    public string ContentType { get; set; } = "image/jpeg";
    public long Bytes { get; set; }
    // Exact server-derived binding, object identifiers and generated import lineage are protected at persistence.
    public string PrivateDetailsJson { get; set; } = "{}";
    public string StatusCode { get; set; } = "Uploading";
    public long Revision { get; set; } = 1;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UploadedAtUtc { get; set; }
    public DateTime? BoundAtUtc { get; set; }
    public DateTime? CustodyEndedAtUtc { get; set; }
    public DateTime? DeleteAfterUtc { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public bool ActiveObjectAbsent { get; set; }
    public bool HistoricalCopiesVerifiedAbsent { get; set; }
    public string DeletionLimitationCode { get; set; } = "";
}
public sealed class 분산사진보존정지Record
{
    public string CaseId { get; set; } = "";
    public string SourceKind { get; set; } = "";
    public string SourceId { get; set; } = "";
    public long Sequence { get; set; }
    public bool Active { get; set; }
    public DateTime? ReviewAtUtc { get; set; }
    public string LegalBasisJson { get; set; } = "{}";
    public long Revision { get; set; } = 1;
}
