using MudBlazor;
using Ssalddel.Contracts.Common.Orderer;
using Ssalddel.Contracts.Common.Operations;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace OrdererApp.Components.GroupPurchase;

/// <summary>모집권 선택과 명령 뒤 정본 조회의 상태를 소유하며 인증 표시와 명시 제출은 Panel이 조립합니다.</summary>
internal sealed class GroupPurchaseDemandSession
{
    private string? _currentProductId;
    internal string GroupScopeKey { get; private set; } = string.Empty;
    internal string GroupScopeName { get; private set; } = string.Empty;
    internal OperatingMarketDeliveryScopePlan? DeliveryScopePlan { get; private set; }
    internal bool IsDeliveryScopeLoading { get; private set; }
    internal string? DeliveryScopeMessage { get; private set; }
    internal Severity DeliveryScopeSeverity { get; private set; } = Severity.Info;
    internal bool DeliveryScopeConfirmed { get; set; }
    internal 공동구매자동집단요약응답? RegisteredGroup { get; private set; }
    internal string RegisteredDemandSourceKey { get; private set; } = string.Empty;
    internal bool IsSubmitting { get; private set; }
    internal string? StatusMessage { get; set; }
    internal Severity StatusSeverity { get; set; } = Severity.Info;

    internal void ChangeProduct(string productId)
    {
        if (!string.Equals(_currentProductId, productId, StringComparison.Ordinal))
        {
            _currentProductId = productId;
            RegisteredGroup = null;
            StatusMessage = null;
        }
    }

    internal void ClearResult()
    {
        RegisteredGroup = null;
        StatusMessage = null;
    }

    internal async Task ResolveDeliveryScopesAsync(string operatingMarketCode, string deliveryAddressQuery,
        Func<OperatingMarketDeliveryScopeResolveRequest, Task<OperatingMarketDeliveryScopePlan?>> resolve)
    {
        if (string.IsNullOrWhiteSpace(deliveryAddressQuery))
        {
            DeliveryScopeSeverity = Severity.Warning;
            DeliveryScopeMessage = "모집권을 확인할 주소를 입력해 주세요.";
            return;
        }

        IsDeliveryScopeLoading = true;
        DeliveryScopeMessage = null;
        DeliveryScopePlan = null;
        GroupScopeKey = string.Empty;
        GroupScopeName = string.Empty;
        DeliveryScopeConfirmed = false;
        RegisteredGroup = null;

        try
        {
            DeliveryScopePlan = await resolve(
                new OperatingMarketDeliveryScopeResolveRequest
                {
                    MarketCode = operatingMarketCode,
                    Address = deliveryAddressQuery,
                    ParticipantCount = 1
                });

            if (DeliveryScopePlan is null)
            {
                DeliveryScopeSeverity = Severity.Error;
                DeliveryScopeMessage = "주소 서비스에 연결하지 못했습니다. 잠시 후 다시 시도해 주세요.";
                return;
            }

            if (!DeliveryScopePlan.Success || DeliveryScopePlan.Items.Count == 0)
            {
                DeliveryScopeSeverity = Severity.Warning;
                DeliveryScopeMessage = DeliveryScopePlan.ErrorMessage
                    ?? "이 주소에서 사용할 모집권 후보를 찾지 못했습니다.";
                return;
            }

            var recommended = DeliveryScopePlan.Items.FirstOrDefault(scope =>
                                  scope.IsRecommendedRecruitmentScope)
                              ?? DeliveryScopePlan.Items[0];
            SelectDeliveryScope(recommended);
            DeliveryScopeSeverity = Severity.Success;
            DeliveryScopeMessage = $"{recommended.DisplayName}을 권장 모집권으로 찾았습니다. 사용자가 최종 확인해야 등록됩니다.";
        }
        finally
        {
            IsDeliveryScopeLoading = false;
        }
    }

    internal void SelectDeliveryScope(OperatingMarketDeliveryScopeCandidate scope)
    {
        GroupScopeKey = scope.ScopeKey;
        GroupScopeName = scope.DisplayName;
        DeliveryScopeConfirmed = false;
        RegisteredGroup = null;
    }

    internal async Task RegisterDemandAsync(string demandSourceKey,
        Func<공동구매자동수요등록Command> createRequest,
        Func<공동구매자동수요등록Command, Task<공동구매자동집단사용자응답?>> register,
        Func<string, string, Task<IReadOnlyList<공동구매자동집단요약응답>?>> listGroups,
        Func<HS먹거리공동구매상품카드> currentProduct,
        Func<Task> authenticationExpired)
    {
        IsSubmitting = true;
        RegisteredGroup = null;
        try
        {
            var group = await register(createRequest());

            if (group is null)
            {
                StatusSeverity = Severity.Error;
                StatusMessage = "공동 주문 수요를 서버에 등록하지 못했습니다. 잠시 후 다시 시도해 주세요.";
                return;
            }

            RegisteredDemandSourceKey = demandSourceKey;

            var groups = await listGroups(currentProduct().상품카드Id, GroupScopeKey);
            RegisteredGroup = groups?.FirstOrDefault(item =>
                string.Equals(item.자동집단Id, group.자동집단Id, StringComparison.Ordinal));

            if (RegisteredGroup is null)
            {
                StatusSeverity = Severity.Warning;
                StatusMessage = $"{currentProduct().상품명} 수요는 등록되었지만 자동집단을 다시 읽지 못했습니다. 새로고침 후 확인해 주세요.";
                return;
            }

            StatusSeverity = Severity.Success;
            StatusMessage = $"수요 등록 후 서버 원장을 다시 확인했습니다. 현재 {RegisteredGroup.수요건수:N0}건, {RegisteredGroup.총희망수량:N0}{RegisteredGroup.수량단위}이 같은 상품·모집권에 모였습니다.";
        }
        catch (SsalddelApiException ex)
        {
            StatusSeverity = Severity.Error;
            StatusMessage = ex.StatusCode == 401
                ? "로그인 세션이 만료되었습니다. 다시 로그인해 주세요."
                : "공동주문 수요 원장을 저장하거나 다시 읽지 못했습니다. 잠시 후 다시 시도해 주세요.";
            if (ex.StatusCode == 401)
            {
                await authenticationExpired();
            }
        }
        catch (InvalidOperationException)
        {
            StatusSeverity = Severity.Error;
            StatusMessage = "공동주문 API에 연결하지 못했습니다. 서버 상태를 확인해 주세요.";
        }
        finally
        {
            IsSubmitting = false;
        }
    }

    internal async Task WithdrawDemandAsync(
        Func<string, string, Task<공동구매자동수요철회응답?>> withdraw,
        Func<string, string, Task<IReadOnlyList<공동구매자동집단요약응답>?>> listGroups,
        Func<HS먹거리공동구매상품카드> currentProduct,
        Func<Task> authenticationExpired)
    {
        IsSubmitting = true;
        try
        {
            var response = await withdraw(
                RegisteredDemandSourceKey,
                "주문자 앱에서 철회");
            if (response is null || !response.철회완료)
            {
                StatusSeverity = Severity.Error;
                StatusMessage = response?.안내 ?? "비구속 수요를 철회하지 못했습니다. 잠시 후 다시 시도해 주세요.";
                return;
            }

            var withdrawnGroupId = !string.IsNullOrWhiteSpace(response.자동집단Id)
                ? response.자동집단Id
                : RegisteredGroup?.자동집단Id;
            RegisteredDemandSourceKey = string.Empty;
            RegisteredGroup = null;

            try
            {
                var groups = await listGroups(
                    currentProduct().상품카드Id,
                    GroupScopeKey);
                RegisteredGroup = groups?.FirstOrDefault(item =>
                    string.Equals(item.자동집단Id, withdrawnGroupId, StringComparison.Ordinal));

                StatusSeverity = Severity.Success;
                StatusMessage = RegisteredGroup is null
                    ? "비구속 수요를 철회하고 서버 원장을 다시 확인했습니다. 이 상품·배송권에는 현재 공개할 자동집단이 없습니다."
                    : $"비구속 수요를 철회한 뒤 서버 원장을 다시 확인했습니다. 남은 수요 {RegisteredGroup.수요건수:N0}건, {RegisteredGroup.총희망수량:N0}{RegisteredGroup.수량단위}입니다.";
            }
            catch (Exception ex) when (ex is SsalddelApiException or InvalidOperationException)
            {
                StatusSeverity = Severity.Warning;
                StatusMessage = "비구속 수요 철회는 완료됐지만 최신 자동집단 원장을 다시 읽지 못했습니다. 새로고침 후 모집 현황을 확인해 주세요.";
            }
        }
        catch (SsalddelApiException ex)
        {
            StatusSeverity = Severity.Error;
            StatusMessage = ex.StatusCode == 401
                ? "로그인 세션이 만료되었습니다. 다시 로그인해 주세요."
                : "비구속 수요를 철회하지 못했습니다. 잠시 후 다시 시도해 주세요.";
            if (ex.StatusCode == 401)
            {
                await authenticationExpired();
            }
        }
        catch (InvalidOperationException)
        {
            StatusSeverity = Severity.Error;
            StatusMessage = "공동주문 API에 연결하지 못했습니다. 서버 상태를 확인해 주세요.";
        }
        finally
        {
            IsSubmitting = false;
        }
    }

    internal static string CreateDemandSourceKey(string? userId, string productCardId)
    {
        if (userId is null) throw new InvalidOperationException("로그인 사용자 식별자를 찾을 수 없습니다.");
        var material = System.Text.Encoding.UTF8.GetBytes($"{userId.Trim()}|{productCardId}");
        var digest = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(material))
            .ToLowerInvariant();
        return $"orderer-app:{digest[..32]}";
    }

}
