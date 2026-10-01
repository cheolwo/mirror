namespace 홍달.Services.External.Mfds;

public interface I수입식품한글표시사항조회Service
{
    Task<수입식품한글표시사항조회응답DTO> 조회Async(
        수입식품한글표시사항조회요청DTO 요청,
        CancellationToken 취소토큰 = default);
}
