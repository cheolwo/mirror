namespace 홍달.Services.External.Mfds;

public interface I해외제조업소조회Service
{
    Task<해외제조업소조회응답> 조회Async(
        해외제조업소조회요청 요청,
        CancellationToken 취소토큰 = default);
}
