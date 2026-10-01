using Hongdal.Contracts.Common.Hr;
using Hongdal.Ui.Common.Areas.App.Models;

namespace HongdalApp.Services;

public sealed class SampleWarehouseWorkEntryGateService : IWarehouseWorkEntryGateService
{
    private static readonly IReadOnlyList<WarehouseWorker> Workers =
    [
        new("01012345678", "김입고", HrDetailedRoleCodes.WarehouseInboundOperator, WarehouseWorkProcessCodes.Inbound),
        new("01023456789", "박출고", HrDetailedRoleCodes.WarehouseDispatchOperator, WarehouseWorkProcessCodes.Outbound),
        new("01034567890", "이포장", HrDetailedRoleCodes.WarehouseDispatchOperator, WarehouseWorkProcessCodes.Packing),
        new("01045678901", "정마켓", HrDetailedRoleCodes.WarehouseDispatchOperator, WarehouseWorkProcessCodes.MarketFulfillment),
        new("01056789012", "한해외", HrDetailedRoleCodes.ShippingAgencyOperator, WarehouseWorkProcessCodes.InternationalForwarding),
        new("01067890123", "오배송", HrDetailedRoleCodes.ShippingAgencyOperator, WarehouseWorkProcessCodes.DeliveryAgency),
        new("01099998888", "최관리", HrDetailedRoleCodes.WarehouseManager, WarehouseWorkProcessCodes.Inbound, WarehouseWorkProcessCodes.Outbound, WarehouseWorkProcessCodes.Packing, WarehouseWorkProcessCodes.MarketFulfillment, WarehouseWorkProcessCodes.InternationalForwarding, WarehouseWorkProcessCodes.DeliveryAgency)
    ];

    public Task<WarehouseWorkOperatorVerificationResult> VerifyAsync(string processCode, string phoneLastEightDigits, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var suffix = new string((phoneLastEightDigits ?? string.Empty).Where(char.IsDigit).ToArray());
        if (suffix.Length != 8)
        {
            return Task.FromResult(Deny("휴대폰 번호 뒤 8자리를 숫자만 입력해 주세요."));
        }

        var normalizedProcess = NormalizeProcess(processCode);
        var worker = Workers.FirstOrDefault(x => x.PhoneNumber.EndsWith(suffix, StringComparison.Ordinal));
        if (worker is null)
        {
            return Task.FromResult(Deny("등록된 창고 작업자를 찾지 못했습니다."));
        }

        if (!worker.ProcessCodes.Contains(normalizedProcess, StringComparer.OrdinalIgnoreCase))
        {
            return Task.FromResult(new WarehouseWorkOperatorVerificationResult(
                false,
                worker.Name,
                worker.RoleCode,
                $"{worker.Name} 작업자는 이 공정 역할을 부여받지 않았습니다."));
        }

        return Task.FromResult(new WarehouseWorkOperatorVerificationResult(
            true,
            worker.Name,
            ResolveRoleName(worker.RoleCode),
            $"{worker.Name} 작업자 확인이 완료되었습니다."));
    }

    private static WarehouseWorkOperatorVerificationResult Deny(string message)
        => new(false, string.Empty, string.Empty, message);

    private static string NormalizeProcess(string processCode)
        => processCode switch
        {
            WarehouseWorkProcessCodes.Outbound => WarehouseWorkProcessCodes.Outbound,
            WarehouseWorkProcessCodes.Packing => WarehouseWorkProcessCodes.Packing,
            WarehouseWorkProcessCodes.MarketFulfillment => WarehouseWorkProcessCodes.MarketFulfillment,
            WarehouseWorkProcessCodes.InternationalForwarding => WarehouseWorkProcessCodes.InternationalForwarding,
            WarehouseWorkProcessCodes.DeliveryAgency => WarehouseWorkProcessCodes.DeliveryAgency,
            _ => WarehouseWorkProcessCodes.Inbound
        };

    private static string ResolveRoleName(string roleCode)
        => roleCode switch
        {
            HrDetailedRoleCodes.WarehouseManager => "창고 관리자",
            HrDetailedRoleCodes.WarehouseInboundOperator => "입고 담당자",
            HrDetailedRoleCodes.WarehouseDispatchOperator => "출고/포장 담당자",
            HrDetailedRoleCodes.WarehouseInventoryOperator => "재고 담당자",
            HrDetailedRoleCodes.ShippingAgencyOperator => "배송대행 담당자",
            _ => roleCode
        };

    private sealed record WarehouseWorker(string PhoneNumber, string Name, string RoleCode, params string[] ProcessCodes);
}
