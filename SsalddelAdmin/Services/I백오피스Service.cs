using Ssalddel.Contracts.CommonContents;
using Ssalddel.Contracts.Common.Documents;

namespace SsalddelAdmin.Services;

public interface I백오피스Service
{
    Task<관리자대시보드요약응답> 대시보드조회Async(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<화주운송의뢰응답>> 의뢰목록조회Async(string? 결제상태 = null, string? 배차상태 = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<공개화물요약응답>> 공개화물요약조회Async(CancellationToken cancellationToken = default);
    Task<화주운송의뢰응답?> 의뢰상세조회Async(string requestId, CancellationToken cancellationToken = default);
    Task 의뢰취소환불처리Async(
        string requestId,
        string 확인의뢰Id,
        string 사유,
        CancellationToken cancellationToken = default);
    Task<운송워크플로우관제상세응답?> 운송워크플로우관제상세조회Async(string requestId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<결제목록응답>> 결제목록조회Async(string? 결제상태 = null, string? 의뢰Id = null, CancellationToken cancellationToken = default);
    Task<토스결제환경응답> 토스결제환경조회Async(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<배차대기응답>> 배차대기목록조회Async(CancellationToken cancellationToken = default);
    Task<배차대기응답?> 배차대기상태변경Async(long id, string status, CancellationToken cancellationToken = default);
    Task 배차대기삭제Async(long id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<기사목록응답>> 기사목록조회Async(string? 운행상태 = null, string? 활동지역검색어 = null, CancellationToken cancellationToken = default);
    Task<기사상세응답?> 기사상세조회Async(string driverId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<기사배차내역응답>> 기사배차내역조회Async(string driverId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<기사월정산관리응답>> 기사월정산목록조회Async(int? year = null, int? month = null, string? driverId = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<운송진행응답>> 운송진행목록조회Async(string? 상태 = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<운송진행응답>> 운송진행목록조회Async(string? 상태, string? businessCode, CancellationToken cancellationToken = default)
        => businessCode is null ? 운송진행목록조회Async(상태, cancellationToken)
            : Task.FromException<IReadOnlyList<운송진행응답>>(new NotSupportedException("이 조회 서비스에서는 업무별 운송 목록을 확인할 수 없습니다."));
    Task<IReadOnlyList<운송이벤트로그응답>> 운송이벤트조회Async(string? requestId = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<업체관리응답>> 업체목록조회Async(string? 상태 = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<화주관리응답>> 화주목록조회Async(CancellationToken cancellationToken = default);
    Task<관리자연락처검색응답> 연락처뒤8자리검색Async(string phoneLast8, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<파일POD응답>> 파일POD목록조회Async(string? fileType = null, string? requestId = null, CancellationToken cancellationToken = default);
    Task<파일POD응답?> 파일POD상태변경Async(Guid id, string uploadStatus, CancellationToken cancellationToken = default);
    Task<파일POD응답?> 파일POD업로드Async(Stream fileStream, string fileName, string contentType, string fileType, string? requestId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<문서정책요약응답>> 문서정책목록조회Async(CancellationToken cancellationToken = default);
    Task<문서정책요약응답?> 문서정책수정Async(string documentCode, 문서정책수정요청 request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<문서조회요약응답>> 문서목록조회Async(string? documentCode = null, string? requestId = null, string? status = null, CancellationToken cancellationToken = default);
    Task<문서관계그래프응답> 문서관계그래프조회Async(string stableId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<문서조회로그요약응답>> 문서로그목록조회Async(long? documentId = null, CancellationToken cancellationToken = default);
    Task<문서조회요약응답?> 문서생명주기변경Async(long documentId, 문서생명주기변경요청 request, CancellationToken cancellationToken = default);
    Task<문서조회요약응답?> 문서업로드Async(Stream fileStream, string fileName, string contentType, string documentCode, string documentName, string requestId, long? transportId = null, bool? encrypt = null, bool? allowDownload = null, string? createdBy = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<관리자공통콘텐츠요약응답>> 공통콘텐츠목록조회Async(CancellationToken cancellationToken = default);
    Task<관리자공통콘텐츠상세응답?> 공통콘텐츠상세조회Async(long id, CancellationToken cancellationToken = default);
    Task<관리자공통콘텐츠상세응답?> 공통콘텐츠등록Async(관리자공통콘텐츠저장요청 request, CancellationToken cancellationToken = default);
    Task<관리자공통콘텐츠상세응답?> 공통콘텐츠수정Async(long id, 관리자공통콘텐츠저장요청 request, CancellationToken cancellationToken = default);
    Task 공통콘텐츠활성화변경Async(long id, bool enabled, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<공통콘텐츠보상정책Dto>> 공통콘텐츠보상정책목록조회Async(CancellationToken cancellationToken = default);
    Task<공통콘텐츠보상정책Dto?> 공통콘텐츠보상정책등록Async(공통콘텐츠보상정책Dto request, CancellationToken cancellationToken = default);
}
