using System.Collections.Concurrent;
using System.Text.Json;

namespace HongdalAdmin.Services;

public sealed class 문서관리메모리Service
{
    private readonly ConcurrentDictionary<long, 문서정책요약응답> _policies = new();
    private readonly ConcurrentDictionary<long, 문서조회요약응답> _documents = new();
    private readonly ConcurrentDictionary<long, 문서조회로그요약응답> _logs = new();
    private readonly ConcurrentDictionary<long, byte[]> _content = new();
    private long _policyId;
    private long _documentId;
    private long _logId;

    public 문서관리메모리Service()
    {
        SeedDefaults();
    }

    public IReadOnlyList<문서정책요약응답> GetPolicies()
    {
        return _policies.Values.OrderBy(x => x.문서코드).ToArray();
    }

    public 문서정책요약응답? UpdatePolicy(string documentCode, 문서정책수정요청 request)
    {
        var policy = _policies.Values.FirstOrDefault(x => string.Equals(x.문서코드, documentCode.Trim(), StringComparison.OrdinalIgnoreCase));
        if (policy is null)
        {
            return null;
        }

        policy.사용여부 = request.사용여부;
        policy.암호화여부 = request.암호화여부;
        policy.다운로드허용여부 = request.다운로드허용여부;
        policy.서명필요여부 = request.서명필요여부;
        policy.자동생성시점 = request.자동생성시점?.Trim() ?? string.Empty;
        policy.조회가능역할목록Json = NormalizeJson(request.조회가능역할목록Json);
        policy.보관일수 = Math.Max(0, request.보관일수);
        policy.수정가능여부 = request.수정가능여부;
        policy.감사로그여부 = request.감사로그여부;
        policy.수정일시 = DateTime.UtcNow;
        return policy;
    }

    public IReadOnlyList<문서조회요약응답> GetDocuments(string? documentCode = null, string? requestId = null, string? status = null)
    {
        IEnumerable<문서조회요약응답> query = _documents.Values;
        if (!string.IsNullOrWhiteSpace(documentCode))
        {
            query = query.Where(x => string.Equals(x.문서코드, documentCode.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(requestId))
        {
            query = query.Where(x => string.Equals(x.의뢰Id, requestId.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(x => string.Equals(x.생성상태, status.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        return query.OrderByDescending(x => x.생성일시).ToArray();
    }

    public IReadOnlyList<문서조회로그요약응답> GetLogs(long? documentId = null)
    {
        IEnumerable<문서조회로그요약응답> query = _logs.Values;
        if (documentId.HasValue)
        {
            query = query.Where(x => x.문서Id == documentId.Value);
        }

        return query.OrderByDescending(x => x.생성일시).ToArray();
    }

    public async Task<문서조회요약응답> UploadDocumentAsync(Stream fileStream, string fileName, string contentType, string documentCode, string documentName, string requestId, long? transportId = null, bool? encrypt = null, bool? allowDownload = null, string? createdBy = null, CancellationToken cancellationToken = default)
    {
        var policy = _policies.Values.FirstOrDefault(x => string.Equals(x.문서코드, documentCode.Trim(), StringComparison.OrdinalIgnoreCase));
        if (policy is null)
        {
            policy = _policies.Values.First();
        }

        using var memory = new MemoryStream();
        await fileStream.CopyToAsync(memory, cancellationToken);
        var bytes = memory.ToArray();

        var id = Interlocked.Increment(ref _documentId);
        var document = new 문서조회요약응답
        {
            Id = id,
            의뢰Id = requestId.Trim(),
            운송원장Id = transportId,
            문서코드 = policy.문서코드,
            문서명 = string.IsNullOrWhiteSpace(documentName) ? policy.문서명 : documentName.Trim(),
            파일명 = fileName,
            생성상태 = "생성완료",
            암호화됨 = encrypt ?? policy.암호화여부,
            다운로드허용여부 = allowDownload ?? policy.다운로드허용여부,
            수정가능여부 = policy.수정가능여부,
            생성일시 = DateTime.UtcNow,
            보관만료일시 = policy.보관일수 > 0 ? DateTime.UtcNow.AddDays(policy.보관일수) : null
        };

        _documents[id] = document;
        _content[id] = bytes;
        AddLog(id, "생성", createdBy);
        return document;
    }

    public byte[]? GetContent(long id)
    {
        return _content.TryGetValue(id, out var bytes) ? bytes : null;
    }

    public void AddLog(long documentId, string action, string? userName = null)
    {
        var id = Interlocked.Increment(ref _logId);
        _logs[id] = new 문서조회로그요약응답
        {
            Id = id,
            문서Id = documentId,
            행위 = action,
            사용자Id = string.Empty,
            사용자명 = userName ?? string.Empty,
            역할명 = string.Empty,
            ClientIp = string.Empty,
            UserAgent = string.Empty,
            생성일시 = DateTime.UtcNow
        };
    }

    public void SeedDefaults()
    {
        var now = DateTime.UtcNow;
        SeedPolicy(new 문서정책요약응답 { 문서코드 = "인수증", 문서명 = "인수증", 사용여부 = true, 암호화여부 = true, 다운로드허용여부 = true, 서명필요여부 = true, 자동생성시점 = "운송인수완료", 조회가능역할목록Json = "[\"화주\",\"기사\",\"서버관리자\"]", 보관일수 = 365 * 5, 수정가능여부 = false, 감사로그여부 = true, 생성일시 = now });
        SeedPolicy(new 문서정책요약응답 { 문서코드 = "운송확인서", 문서명 = "운송확인서", 사용여부 = true, 암호화여부 = true, 다운로드허용여부 = true, 서명필요여부 = false, 자동생성시점 = "운송완료", 조회가능역할목록Json = "[\"화주\",\"기사\",\"서버관리자\"]", 보관일수 = 365 * 5, 수정가능여부 = false, 감사로그여부 = true, 생성일시 = now });
        SeedPolicy(new 문서정책요약응답 { 문서코드 = "정산내역서", 문서명 = "정산내역서", 사용여부 = true, 암호화여부 = true, 다운로드허용여부 = false, 서명필요여부 = false, 자동생성시점 = "정산확정", 조회가능역할목록Json = "[\"화주\",\"서버관리자\"]", 보관일수 = 365 * 3, 수정가능여부 = false, 감사로그여부 = true, 생성일시 = now });
        SeedPolicy(new 문서정책요약응답 { 문서코드 = "세금계산서연결정보", 문서명 = "세금계산서 연결정보", 사용여부 = true, 암호화여부 = true, 다운로드허용여부 = false, 서명필요여부 = false, 자동생성시점 = "결제완료", 조회가능역할목록Json = "[\"서버관리자\"]", 보관일수 = 365 * 5, 수정가능여부 = true, 감사로그여부 = true, 생성일시 = now });
        SeedPolicy(new 문서정책요약응답 { 문서코드 = "결제영수증", 문서명 = "결제영수증", 사용여부 = true, 암호화여부 = true, 다운로드허용여부 = true, 서명필요여부 = false, 자동생성시점 = "결제완료", 조회가능역할목록Json = "[\"화주\",\"기사\",\"서버관리자\"]", 보관일수 = 365 * 3, 수정가능여부 = false, 감사로그여부 = true, 생성일시 = now });
        SeedPolicy(new 문서정책요약응답 { 문서코드 = "환불확인서", 문서명 = "환불확인서", 사용여부 = true, 암호화여부 = true, 다운로드허용여부 = true, 서명필요여부 = false, 자동생성시점 = "환불처리", 조회가능역할목록Json = "[\"화주\",\"서버관리자\"]", 보관일수 = 365 * 3, 수정가능여부 = false, 감사로그여부 = true, 생성일시 = now });
        SeedPolicy(new 문서정책요약응답 { 문서코드 = "배차확정서", 문서명 = "배차확정서", 사용여부 = true, 암호화여부 = false, 다운로드허용여부 = true, 서명필요여부 = false, 자동생성시점 = "배차확정", 조회가능역할목록Json = "[\"화주\",\"기사\",\"서버관리자\"]", 보관일수 = 365 * 3, 수정가능여부 = false, 감사로그여부 = true, 생성일시 = now });
        SeedPolicy(new 문서정책요약응답 { 문서코드 = "사고분쟁기록", 문서명 = "사고/분쟁기록", 사용여부 = true, 암호화여부 = true, 다운로드허용여부 = false, 서명필요여부 = false, 자동생성시점 = "사고신고", 조회가능역할목록Json = "[\"서버관리자\"]", 보관일수 = 365 * 5, 수정가능여부 = true, 감사로그여부 = true, 생성일시 = now });

        if (_documents.IsEmpty)
        {
            var sampleId = Interlocked.Increment(ref _documentId);
            _documents[sampleId] = new 문서조회요약응답
            {
                Id = sampleId,
                의뢰Id = "REQ-SAMPLE-001",
                운송원장Id = 101,
                문서코드 = "인수증",
                문서명 = "인수증",
                파일명 = "인수증-REQ-SAMPLE-001.pdf",
                생성상태 = "생성완료",
                암호화됨 = true,
                다운로드허용여부 = true,
                수정가능여부 = false,
                생성일시 = now,
                보관만료일시 = now.AddYears(5)
            };

            _content[sampleId] = JsonSerializer.SerializeToUtf8Bytes(new { title = "살뜰 인수증 샘플", requestId = "REQ-SAMPLE-001" });
            AddLog(sampleId, "생성", "sample");
        }
    }

    private void SeedPolicy(문서정책요약응답 policy)
    {
        policy.Id = policy.Id <= 0 ? Interlocked.Increment(ref _policyId) : policy.Id;
        _policies[policy.Id] = policy;
    }

    private static string NormalizeJson(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "[]";
        }

        try
        {
            using var doc = JsonDocument.Parse(value);
            return doc.RootElement.ValueKind == JsonValueKind.Array ? value : "[]";
        }
        catch
        {
            return "[]";
        }
    }
}
