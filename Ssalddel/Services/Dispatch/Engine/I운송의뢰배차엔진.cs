using 살뜰.도메인.배차;
using 살뜰.Services.Dispatch.Queue;

namespace 살뜰.Services.Dispatch.Engine;

public sealed record 운송의뢰배차Engine입력Context(string? 화물운송방식 = null);

public interface I운송의뢰배차엔진
{
    string 운영체제Id { get; }

    string 논리엔진코드 { get; }

    string 엔진코드 { get; }

    string 표시명 { get; }

    int 배차업무유형 { get; }

    Task<배차추천후보선정결과> 다음후보선정Async(
        운송원장 queue,
        운송의뢰배차Engine입력Context? context = null,
        string? 제외기사Id = null,
        CancellationToken cancellationToken = default);
}
