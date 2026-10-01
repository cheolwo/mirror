using Hongdal.Contracts.Common.Customs;

namespace 홍달.Services.External.PublicData;

public interface IHs공공데이터수집기
{
    string SourceKey { get; }

    Task<Hs공공데이터출처응답> 수집Async(
        Hs공공데이터수집요청 request,
        CancellationToken cancellationToken = default);
}

public interface IHs공공데이터수집Service
{
    Task<Hs공공데이터묶음응답> 수집Async(
        Hs공공데이터수집요청 request,
        CancellationToken cancellationToken = default);
}
