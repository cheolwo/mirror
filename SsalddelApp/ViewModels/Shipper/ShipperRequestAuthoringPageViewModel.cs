using Microsoft.AspNetCore.Components;
using MudBlazor;
using Ssalddel.Ui.Common.Areas.App.ViewModels;
using SsalddelApp.Models.Shipper;
using SsalddelApp.Services;

namespace SsalddelApp.ViewModels.Shipper;

/// <summary>
/// 모바일 route가 서버/sample adapter 호출과 화면 상태만 조율합니다.
/// 입력 draft와 validation은 Web과 같은 Ui.Common ViewModel을 사용합니다.
/// </summary>
public sealed class ShipperRequestAuthoringPageViewModel
{
    private readonly IShipperOperationsService _operations;
    private readonly NavigationManager _navigation;
    private bool _initialized;

    public ShipperRequestAuthoringPageViewModel(
        운송의뢰작성ViewModel state,
        IShipperOperationsService operations,
        NavigationManager navigation)
    {
        State = state;
        _operations = operations;
        _navigation = navigation;
    }

    public 운송의뢰작성ViewModel State { get; }
    public IReadOnlyList<string> VehicleTypes { get; private set; } = [];
    public bool? RegistrationEnabled { get; private set; }
    public bool IsBusy { get; private set; }
    public string StatusMessage { get; private set; } = string.Empty;
    public Severity StatusSeverity { get; private set; } = Severity.Info;

    public string RegistrationBoundaryMessage { get; private set; }
        = "앱의 운송 원장 adapter를 확인하고 있습니다. 이 단계에서 자동 배차·계약·결제를 확정하지 않습니다.";

    public async Task InitializeAsync()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        try
        {
            VehicleTypes = await _operations.GetVehicleTypesAsync();
            RegistrationEnabled = true;
            RegistrationBoundaryMessage =
                "등록 버튼은 구성된 운송 원장 adapter만 호출합니다. 개발 데이터 모드에서는 sample 원장으로 대체될 수 있으며, 등록만으로 자동 배차·계약·결제는 확정되지 않습니다.";
        }
        catch (Exception ex)
        {
            VehicleTypes = [];
            RegistrationEnabled = false;
            StatusSeverity = Severity.Warning;
            StatusMessage = $"차량 후보를 불러오지 못했습니다: {ex.Message}";
            RegistrationBoundaryMessage = "운송 원장 adapter를 확인하지 못해 등록을 보류했습니다. 입력 내용은 현재 앱 세션에 유지됩니다.";
        }
    }

    public Task SaveAsync()
    {
        StatusSeverity = State.서버등록가능 ? Severity.Success : Severity.Warning;
        StatusMessage = State.서버등록가능
            ? "현재 앱 세션에 초안을 유지했습니다. 등록 전 실행 경계를 다시 확인해 주세요."
            : "현재 앱 세션에 초안을 유지했습니다. 등록 전 필수 입력을 보완해 주세요.";
        return Task.CompletedTask;
    }

    public Task AutoSaveAsync()
        => Task.CompletedTask;

    public Task ResetAsync()
    {
        State.Reset();
        StatusSeverity = Severity.Info;
        StatusMessage = "운송 의뢰 입력값을 초기화했습니다.";
        return Task.CompletedTask;
    }

    public async Task<decimal?> EstimateFareAsync(string vehicleType, decimal distanceKm)
        => await _operations.EstimateFareAsync(vehicleType, distanceKm);

    public async Task SubmitAsync()
    {
        if (IsBusy) return;

        if (RegistrationEnabled != true)
        {
            StatusSeverity = Severity.Warning;
            StatusMessage = "현재 앱에서는 운송 원장 등록을 안전하게 보류하고 있습니다.";
            return;
        }

        if (!State.서버등록가능)
        {
            StatusSeverity = Severity.Warning;
            StatusMessage = "등록 전 점검 목록의 필수 항목을 보완해 주세요.";
            return;
        }

        IsBusy = true;
        StatusSeverity = Severity.Info;
        StatusMessage = "운송 의뢰 원장 등록을 요청하고 있습니다.";

        try
        {
            var draft = State.ToDraft();
            var request = new ShipperRequestItem
            {
                의뢰Id = $"SHP-{DateTime.Now:yyyyMMddHHmmss}",
                화물종류 = draft.화물종류,
                화물적재형태 = draft.화물적재형태,
                의뢰상태 = "접수",
                결제상태 = draft.결제예정금액.HasValue ? "결제대기" : "대기",
                배차상태 = "매칭중",
                운송방식 = draft.운송방식,
                차량종류 = draft.차량종류 ?? "1톤 카고",
                결제수단 = draft.결제수단,
                결제예정금액 = draft.결제예정금액,
                예상거리Km = draft.예상거리Km,
                기준운임 = draft.기준운임,
                기사지급예정운임 = draft.기사지급예정운임,
                알선단계 = draft.알선단계,
                재알선금지 = draft.재알선금지,
                재알선의심 = draft.재알선의심,
                정책위반 = draft.정책위반,
                정책경고목록 = draft.정책경고목록,
                생성일시 = DateTime.Now,
                픽업지 = draft.픽업도로명주소,
                하차지 = draft.하차도로명주소,
                픽업정보 = ShipperRequestHandoffMapper.CreatePickup(draft),
                하차정보 = ShipperRequestHandoffMapper.CreateDropoff(draft)
            };

            var created = await _operations.AddRequestAsync(request);
            StatusSeverity = Severity.Success;
            StatusMessage = "운송 의뢰 원장을 등록했습니다.";
            _navigation.NavigateTo(ShipperRoutes.RequestDetailFor(created.의뢰Id));
        }
        catch (Exception ex)
        {
            StatusSeverity = Severity.Error;
            StatusMessage = $"운송 의뢰 원장 등록 실패: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
