using System.Globalization;
using Hongdal.Contracts.Common.PublicData;
using Hongdal.Ui.Common.Areas.App.Services;
using Microsoft.AspNetCore.Components;

namespace Hongdal.Ui.Common.Areas.App.Components.Community;

public partial class CommunityGroupImportPriceSignal
{
    [Inject]
    public PlatformCommunityService CommunityService { get; set; } = null!;

    [Parameter, EditorRequired]
    public string HsCode { get; set; } = string.Empty;

    [Parameter]
    public string CountryCode { get; set; } = "CN";

    [Parameter]
    public string ReferenceMonth { get; set; } = string.Empty;

    [Parameter]
    public int LookbackMonths { get; set; } = 3;

    [Parameter]
    public decimal FxRateKrwPerUsd { get; set; } = 1350m;

    private HsCountryImportUnitPriceSimulationResult? result;
    private string criteriaKey = string.Empty;
    private string message = string.Empty;
    private bool isLoading;

    protected override void OnParametersSet()
    {
        var nextKey = BuildCriteriaKey();
        if (string.Equals(criteriaKey, nextKey, StringComparison.Ordinal))
        {
            return;
        }

        criteriaKey = nextKey;
        result = null;
        message = string.Empty;
    }

    private async Task LoadAsync()
    {
        if (isLoading)
        {
            return;
        }

        if (!TryValidateCriteria(out var validationMessage))
        {
            message = validationMessage;
            result = null;
            return;
        }

        var requestedKey = BuildCriteriaKey();
        isLoading = true;
        message = string.Empty;
        try
        {
            var response = await CommunityService.GetGroupImportUnitPriceAsync(new HsCountryMonthlyTradeUnitPriceRequest
            {
                HsCode = HsCode,
                CountryCode = CountryCode.Trim(),
                Month = ReferenceMonth.Trim(),
                LookbackMonths = Math.Clamp(LookbackMonths, 1, 12),
                ExpectedFxRateKrwPerUsd = FxRateKrwPerUsd > 0 ? FxRateKrwPerUsd : null
            });

            if (!string.Equals(requestedKey, BuildCriteriaKey(), StringComparison.Ordinal))
            {
                return;
            }

            result = response;
            if (response is not { Success: true })
            {
                message = FailureMessage(response?.ErrorMessage);
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            result = null;
            message = "평균단가를 불러오지 못했습니다. 잠시 뒤 다시 조회해 주세요.";
        }
        finally
        {
            isLoading = false;
        }
    }

    private bool TryValidateCriteria(out string validationMessage)
    {
        var country = new string(CountryCode.Where(char.IsLetterOrDigit).ToArray());
        if (country.Length is < 2 or > 3)
        {
            validationMessage = "수출국 코드를 영문 2~3자로 입력해 주세요.";
            return false;
        }

        if (!DateTime.TryParseExact(
                ReferenceMonth,
                "yyyyMM",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out _))
        {
            validationMessage = "기준월을 YYYYMM 형식으로 입력해 주세요.";
            return false;
        }

        if (FxRateKrwPerUsd <= 0)
        {
            validationMessage = "가정 환율을 0보다 크게 입력해 주세요.";
            return false;
        }

        validationMessage = string.Empty;
        return true;
    }

    private string BuildCriteriaKey()
        => string.Join(
            '|',
            HsCode,
            CountryCode.Trim().ToUpperInvariant(),
            ReferenceMonth.Trim(),
            Math.Clamp(LookbackMonths, 1, 12).ToString(CultureInfo.InvariantCulture),
            FxRateKrwPerUsd.ToString(CultureInfo.InvariantCulture));

    private static string FailureMessage(string? errorMessage)
    {
        if (string.IsNullOrWhiteSpace(errorMessage))
        {
            return "해당 조건의 수입 평균단가를 확인하지 못했습니다.";
        }

        if (errorMessage.Contains("No import statistics", StringComparison.OrdinalIgnoreCase))
        {
            return "해당 국가와 기간에는 집계된 수입 신고 자료가 없습니다.";
        }

        if (errorMessage.Contains("ServiceKey", StringComparison.OrdinalIgnoreCase))
        {
            return "서버의 관세청 수출입 통계 연동 설정이 필요합니다.";
        }

        return "수입 평균단가를 계산하지 못했습니다. 조회 기준을 확인해 주세요.";
    }

    private static string KrwPerKg(decimal? value)
        => value.HasValue ? $"약 {value.Value:N0}원/kg" : "원화 환산 없음";

    private static string UsdPerKg(decimal? value)
        => value.HasValue ? $"USD {value.Value:N2}/kg" : "단가 계산 불가";

    private static string FormatPeriod(string startMonth, string endMonth)
        => $"{FormatMonth(startMonth)}~{FormatMonth(endMonth)}";

    private static string FormatMonth(string month)
        => DateTime.TryParseExact(month, "yyyyMM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var value)
            ? value.ToString("yyyy.MM", CultureInfo.InvariantCulture)
            : month;

    private static string CountryLabel(string countryCode)
        => countryCode.ToUpperInvariant() switch
        {
            "CN" => "중국",
            "VN" => "베트남",
            "JP" => "일본",
            "US" => "미국",
            "TH" => "태국",
            "ID" => "인도네시아",
            "DE" => "독일",
            _ => countryCode.ToUpperInvariant()
        };
}
