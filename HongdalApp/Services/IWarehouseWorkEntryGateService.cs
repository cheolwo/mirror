using Hongdal.Ui.Common.Areas.App.Models;

namespace HongdalApp.Services;

public interface IWarehouseWorkEntryGateService
{
    Task<WarehouseWorkOperatorVerificationResult> VerifyAsync(string processCode, string phoneLastEightDigits, CancellationToken cancellationToken = default);
}
