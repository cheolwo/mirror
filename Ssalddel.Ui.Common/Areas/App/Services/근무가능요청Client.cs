using Ssalddel.Contracts.Common.Hr;

namespace Ssalddel.Ui.Common.Areas.App.Services;

public interface I근무가능요청Service
{
    Task<근무연락동의> 동의조회Async();
    Task<근무연락동의> 동의저장Async(근무연락동의 request);
    Task<근무가능문맥응답> 문맥Async(근무가능업무범위 scope);
    Task<근무가능목록응답> 목록Async(long? warehouseId, int page = 0);
    Task<근무가능요청응답> 생성Async(근무가능요청등록요청 request);
    Task<근무가능요청응답> 답변Async(Guid id, 근무가능답변요청 request, long? warehouseId);
}

public sealed class 근무가능요청Client(ISsalddelJsonApiClient api) : I근무가능요청Service
{
    private const string Base = "api/v1/hr/work-availability";
    private async Task<T> Read<T>(string path) => await api.GetAsync<T>(path, "근무 가능 요청 조회", allowNotFound: false)
        ?? throw new InvalidOperationException("조회 응답이 비어 있습니다.");
    private async Task<T> Send<TIn, T>(HttpMethod method, string path, TIn request)
        => await api.SendAsync<TIn, T>(method, path, request, "근무 가능 요청 저장")
            ?? throw new InvalidOperationException("저장 결과를 확인하지 못했습니다. 다시 조회해 주세요.");
    public Task<근무연락동의> 동의조회Async() => Read<근무연락동의>($"{Base}/preferences");
    public Task<근무연락동의> 동의저장Async(근무연락동의 request) => Send<근무연락동의, 근무연락동의>(HttpMethod.Put, $"{Base}/preferences", request);
    public Task<근무가능문맥응답> 문맥Async(근무가능업무범위 s)
        => Read<근무가능문맥응답>($"{Base}/warehouses/{s.WarehouseId}/context?stage={Uri.EscapeDataString(s.Stage)}&periodStartUtc={Uri.EscapeDataString(s.PeriodStartUtc.ToString("O"))}&periodEndUtc={Uri.EscapeDataString(s.PeriodEndUtc.ToString("O"))}&groupPage={s.GroupPage}&groupKey={Uri.EscapeDataString(s.GroupKey)}");
    public Task<근무가능목록응답> 목록Async(long? warehouseId, int page = 0) => Read<근무가능목록응답>(warehouseId.HasValue
        ? $"{Base}/warehouses/{warehouseId}/requests?page={page}" : $"{Base}/inbox?page={page}");
    public Task<근무가능요청응답> 생성Async(근무가능요청등록요청 request)
        => Send<근무가능요청등록요청, 근무가능요청응답>(HttpMethod.Post, $"{Base}/warehouses/{request.WarehouseId}/requests", request);
    public Task<근무가능요청응답> 답변Async(Guid id, 근무가능답변요청 request, long? warehouseId)
        => Send<근무가능답변요청, 근무가능요청응답>(HttpMethod.Post, warehouseId.HasValue
            ? $"{Base}/warehouses/{warehouseId}/invitations/{id:D}/confirm" : $"{Base}/invitations/{id:D}/reply", request);
}
