using Ssalddel.Contracts.Admin.Food;
using Ssalddel.Contracts.Food;

namespace Ssalddel.Ui.Common.Areas.BackOffice.Services;

public interface IFoodOrderInterruptionReviewClient
{
    Task<음식주문운영추적응답?> 조회Async(string orderNo, CancellationToken cancellationToken = default);
    Task<음식배달시도운영응답> 중단검토Async(string attemptId, 음식배달중단검토요청 body, CancellationToken cancellationToken = default);
}
