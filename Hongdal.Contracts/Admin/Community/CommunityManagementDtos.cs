namespace Hongdal.Contracts.Admin.Community;

public sealed class CommunityManagementUserResponse
{
    public string UserId { get; set; } = string.Empty;
    public bool AccountExists { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public IReadOnlyList<string> Roles { get; set; } = [];
    public IReadOnlyList<CommunityManagementPostResponse> Posts { get; set; } = [];
}

public sealed class CommunityManagementPostResponse
{
    public long Id { get; set; }
    public string AppKey { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string WorkflowTag { get; set; } = string.Empty;
    public string RoleTag { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Nickname { get; set; } = string.Empty;
    public bool IsSystemGenerated { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public IReadOnlyList<CommunityManagementCommentResponse> Comments { get; set; } = [];
    public IReadOnlyList<CommunityManagementAttachmentResponse> Attachments { get; set; } = [];
}

public sealed class CommunityManagementCommentResponse
{
    public long Id { get; set; }
    public string Nickname { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public int ReportCount { get; set; }
    public bool IsOperatorHidden { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class CommunityManagementAttachmentResponse
{
    public long Id { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public DateTime UploadedAtUtc { get; set; }
    public IReadOnlyList<CommunityManagementAttachmentCommentResponse> Comments { get; set; } = [];
}

public sealed class CommunityManagementAttachmentCommentResponse
{
    public long Id { get; set; }
    public long AttachmentId { get; set; }
    public string Nickname { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public int ReportCount { get; set; }
    public bool IsOperatorHidden { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class CommunityManagementPostUpdateRequest
{
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}

public sealed class CommunityManagementVisibilityRequest
{
    public bool Hidden { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public sealed class CommunityManagementContactRequest
{
    public string Channel { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
}

public sealed class CommunityManagementActionResponse
{
    public bool Succeeded { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime RecordedAtUtc { get; set; }
}
