namespace 살뜰.Services.Dispatch.Coordination;

public sealed record 배달권기반배차조율요청
{
    public int 최대운송의뢰수 { get; init; } = 30;

    public int 최대기사수 { get; init; } = 100;

    public int 기사당최대추천건수 { get; init; } = 1;

    public bool 인접배달권기사포함 { get; init; } = true;
}

public sealed record 배달권배차조율실행계획(
    string 배달권키,
    IReadOnlyList<string> 의뢰Ids,
    IReadOnlyList<string> 기사Ids,
    IReadOnlyList<string> 인접배달권Keys);

public sealed record 배달권기반배차조율실행결과(
    string 배달권키,
    국내화물배차조율입력 Input,
    국내화물배차조율결과 Result,
    국내화물배차조율적용결과 ApplyResult);

public interface I배달권기반배차조율계획Service
{
    Task<IReadOnlyList<배달권배차조율실행계획>> 계획Async(
        배달권기반배차조율요청 요청,
        CancellationToken cancellationToken = default);
}

public interface I배달권기반배차조율실행Service
{
    Task<IReadOnlyList<배달권기반배차조율실행결과>> 실행Async(
        배달권기반배차조율요청 요청,
        CancellationToken cancellationToken = default);
}
