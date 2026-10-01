namespace 홍달.Services.External.Mfds;

public interface I수입식품제품조회Service
{
    Task<수입식품제품조회응답DTO> 조회Async(
        수입식품제품조회요청DTO 요청,
        CancellationToken 취소토큰 = default);
}
