using Ssalddel.Contracts.Common.Hr;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.Ui.Common.Areas.App.ViewModels;

/// <summary>현재 로그인과 적용한 묶음 조회 문맥에만 결과를 연결합니다.</summary>
public sealed class 근무가능요청ViewModel(I근무가능요청Service service)
{
    public 근무가능문맥응답? Context { get; private set; }
    public 근무가능목록응답? Requests { get; private set; }
    public 근무연락동의? Preference { get; private set; }
    public 근무가능업무범위? Scope { get; private set; }
    public bool Busy { get; private set; }
    public string? Error { get; private set; }
    public int? ErrorStatus { get; private set; }
    public string? Notice { get; private set; }
    public 근무가능요청등록요청? Pending { get; private set; }
    public int RequestPage { get; private set; }
    private long generation;

    public void Reset()
    {
        generation++; Busy = false; Context = null; Requests = null; Preference = null;
        Scope = null; Pending = null; Error = null; ErrorStatus = null; Notice = null; RequestPage = 0;
    }
    public async Task LoadAsync(근무가능업무범위? scope)
    {
        Reset(); Scope = scope;
        await Run(async () =>
        {
            if (scope is null)
            {
                var preference = await service.동의조회Async();
                var requests = await service.목록Async(null);
                return () => { Preference = preference; Requests = requests; };
            }
            var context = await service.문맥Async(scope);
            var list = await service.목록Async(scope.WarehouseId);
            return () => { Context = context; Requests = list; };
        });
    }
    public async Task RefreshAsync()
    {
        if (Busy) return;
        var scope = Scope;
        await Run(async () =>
        {
            var context = scope is null ? null : await service.문맥Async(scope);
            var preference = scope is null ? await service.동의조회Async() : null;
            var list = await service.목록Async(scope?.WarehouseId, RequestPage);
            return () => { Context = context; Preference = preference; Requests = list; };
        });
    }
    public Task RequestPageAsync(int page) => Run(async () =>
    {
        var list = await service.목록Async(Scope?.WarehouseId, page);
        return () => { RequestPage = page; Requests = list; };
    });
    public Task SavePreferenceAsync(근무연락동의 value) => Run(async () =>
    {
        var saved = await service.동의저장Async(value);
        return () => { Preference = saved; Notice = "수신 설정을 저장했습니다."; };
    });
    public Task CreateAsync(근무가능요청등록요청 value) => Run(async () =>
    {
        Pending ??= value;
        var created = await service.생성Async(Pending);
        return () => { Pending = null; Upsert(created); Notice = "요청을 등록했습니다. 답변과 인력 계획 확정을 이어 확인해 주세요."; };
    });
    public Task ReplyAsync(근무가능대상응답 target, string status) => Run(async () =>
    {
        var result = await service.답변Async(target.InvitationId, new() { Status = status, Revision = target.Revision }, Scope?.WarehouseId);
        var preference = Scope is null ? await service.동의조회Async() : null;
        return () => { if (preference is not null) Preference = preference; Upsert(result); Notice = Scope is null ? "내 답변을 저장했습니다." : "인력 계획에 반영했습니다. 고용·출근 확정은 별도입니다."; };
    });
    private void Upsert(근무가능요청응답 result)
    {
        var items = Requests?.Items ?? [];
        Requests = new() { Items = new[] { result }.Concat(items.Where(x => x.RequestId != result.RequestId)).ToArray(), HasMore = Requests?.HasMore ?? false };
    }
    private async Task Run(Func<Task<Action>> action)
    {
        if (Busy) return;
        var current = generation;
        Busy = true; Error = null; ErrorStatus = null; Notice = null;
        try { var apply = await action(); if (current == generation) apply(); }
        catch (Exception ex)
        {
            if (current == generation)
            {
                if (ex is SsalddelApiException { StatusCode: >= 400 and < 500 }) Pending = null;
                ErrorStatus = (ex as SsalddelApiException)?.StatusCode;
                if (ErrorStatus is 401 or 403) { Context = null; Requests = null; Preference = null; }
                Error = ex is SsalddelApiException api ? api.StatusCode switch
                {
                    400 => "대상·필요 인원·업무 시간·답변 기한을 확인해 주세요. 수정한 뒤 다시 요청할 수 있습니다.",
                    401 => "로그인 세션이 만료되었습니다. 다시 로그인한 뒤 확인해 주세요.",
                    403 => "이 기능의 사용 권한 또는 활성 설정을 확인해 주세요.",
                    404 => "현재 계정에서 이 창고나 요청을 확인할 수 없습니다.",
                    409 => "현재 수량·수신 동의·답변 또는 확정 인원이 바뀌었습니다. 다시 조회해 주세요.",
                    _ => "조회에 실패했습니다. 연결을 확인한 뒤 다시 조회해 주세요."
                } : "저장 결과를 확인하지 못했습니다. 같은 요청 결과 확인으로 중복 없이 확인해 주세요.";
            }
        }
        finally { if (current == generation) Busy = false; }
    }
}
