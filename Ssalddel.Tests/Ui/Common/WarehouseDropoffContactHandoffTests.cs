using DriverApp.Services;
using Ssalddel.Contracts.Common.Inventory;
using Ssalddel.Contracts.Common.Warehouse;
using Ssalddel.Contracts.Driver.Transport;
using Ssalddel.Contracts.Shipper.Request;
using Ssalddel.Ui.Common.Areas.App.Models;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.ViewModels;

namespace Ssalddel.Tests.Ui.Common;

public sealed class WarehouseDropoffContactHandoffTests
{
    [Fact]
    public async Task Reviewed_contact_reaches_existing_api_and_same_plan_reload()
    {
        var query = new PlanQuery();
        var api = new RecordingApi();
        var page = CreatePage(query, api);
        Assert.True(await page.초기화Async(17));
        CompleteInput(page.초안);
        page.초안.하차담당자명 = "  현장 담당자  ";
        page.초안.하차연락처 = "  010-1234-5678  ";
        Assert.True(page.초안.입력값검토());
        var reviewed = page.초안.검토결과!;
        Assert.Equal("현장 담당자", reviewed.DestinationContactName);
        Assert.Equal("010-1234-5678", reviewed.DestinationContactPhone);
        Assert.Empty(api.Requests);

        Assert.True(await page.서버저장Async());

        var request = Assert.Single(api.Requests);
        Assert.Equal(17, request.출고예정Id);
        Assert.Equal(71, request.입고상품Id);
        Assert.Equal("현장 담당자", request.하차담당자명);
        Assert.Equal("010-1234-5678", request.하차연락처);
        Assert.Equal(new long[] { 17, 17 }, query.DetailIds);
        Assert.Equal("warehouse-outbound-17", page.원장.원장!.TransportRequestId);
    }

    [Fact]
    public async Task Korean_input_stays_local_in_review_and_round_trips_through_utc_to_driver()
    {
        var api = new RecordingApi();
        var page = CreatePage(new(), api);
        await page.초기화Async(17);
        CompleteInput(page.초안);
        Assert.True(page.초안.입력값검토());
        Assert.Equal(new DateTime(2026, 10, 5, 9, 0, 0), page.초안.검토결과!.PickupAt);
        Assert.True(await page.서버저장Async());

        var request = Assert.Single(api.Requests);
        Assert.Equal(new DateTime(2026, 10, 5, 0, 0, 0, DateTimeKind.Utc), request.희망상차일시);
        Assert.Equal(DateTimeKind.Utc, request.희망상차일시!.Value.Kind);
        Assert.Equal(new DateTime(2026, 10, 5, 2, 0, 0, DateTimeKind.Utc), request.희망도착일시);
        var driver = 기사운송표시Mapper.Map(new 기사운송요약응답
        {
            상차시간창시작일시 = request.희망상차일시,
            상차시간창종료일시 = request.희망상차일시!.Value.AddHours(1)
        });
        Assert.Equal("10.05 09:00 ~ 10.05 10:00 (한국시간)", driver.상차시간창표시);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Changing_contact_invalidates_prior_review(bool changeName)
    {
        var draft = ReadyDraft();
        draft.하차담당자명 = "기존 담당자";
        draft.하차연락처 = "010-1234-5678";
        Assert.True(draft.입력값검토());

        if (changeName) draft.하차담당자명 = "다른 담당자";
        else draft.하차연락처 = "010-8765-4321";

        Assert.Null(draft.검토결과);
        Assert.False(draft.검토완료);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Oversized_contacts_fail_review(bool oversizedName)
    {
        var draft = ReadyDraft();
        if (oversizedName) draft.하차담당자명 = new string('가', 101);
        else draft.하차연락처 = new string('1', 51);

        Assert.False(draft.입력값검토());
        Assert.Contains("100자", draft.검증오류);
        Assert.Null(draft.검토결과);
    }

    [Fact]
    public async Task Optional_blank_contact_stays_blank_without_substituting_warehouse_or_user()
    {
        var api = new RecordingApi();
        var page = CreatePage(new(), api);
        await page.초기화Async(17);
        CompleteInput(page.초안);
        page.초안.하차담당자명 = "  ";
        page.초안.하차연락처 = "  ";
        Assert.True(page.초안.입력값검토());
        Assert.True(await page.서버저장Async());

        var request = Assert.Single(api.Requests);
        Assert.Equal(string.Empty, request.하차담당자명);
        Assert.Equal(string.Empty, request.하차연락처);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(18L)]
    public async Task Initializing_another_or_empty_plan_clears_private_input(long? nextPlanId)
    {
        var page = CreatePage(new(), new());
        await page.초기화Async(17);
        CompleteInput(page.초안);
        page.초안.하차담당자명 = "이전 담당자";
        page.초안.하차연락처 = "010-1234-5678";
        Assert.True(page.초안.입력값검토());

        await page.초기화Async(nextPlanId);

        Assert.Equal(string.Empty, page.초안.하차담당자명);
        Assert.Equal(string.Empty, page.초안.하차연락처);
        Assert.Null(page.초안.검토결과);
    }

    [Theory]
    [InlineData(18L)]
    [InlineData(null)]
    public async Task New_selection_clears_contact_immediately_and_loads_latest_after_inflight_query(long? nextPlanId)
    {
        var query = new PlanQuery();
        var page = CreatePage(query, new());
        await page.초기화Async(7);
        page.초안.하차담당자명 = "이전 담당자";
        page.초안.하차연락처 = "010-1234-5678";
        query.BlockedId = 17;
        var earlier = page.초기화Async(17);
        await query.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(string.Empty, page.초안.하차담당자명);
        var latest = page.초기화Async(nextPlanId);
        Assert.Null(page.원장.원장);
        Assert.Null(page.초안.원장);
        Assert.Equal(string.Empty, page.초안.하차연락처);

        query.Completion.SetResult(Plan(17));
        Assert.False(await earlier);
        Assert.True(await latest);

        Assert.True(page.초기화됨);
        Assert.Equal(nextPlanId, page.원장.조회대상Id);
        Assert.Equal(nextPlanId, page.원장.원장?.OutboundPlanId);
        Assert.Equal(nextPlanId, page.초안.원장?.OutboundPlanId);
        Assert.Equal(nextPlanId.HasValue ? new long[] { 7, 17, 18 } : new long[] { 7, 17 }, query.DetailIds);
        Assert.Equal(string.Empty, page.초안.하차연락처);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("  ", "")]
    [InlineData("  담당자  ", "담당자")]
    public void Detached_copy_preserves_omitted_versus_explicit_contact(string? value, string? expected)
    {
        var source = new 재고운송의뢰생성요청
        {
            출고예정Id = 17, 입고상품Id = 71, 요청수량 = 4,
            하차담당자명 = value, 하차연락처 = value,
            하차지주소 = "서울특별시 송파구 올림픽로 300",
            하차지상세주소 = "동문", 차량종류 = "1톤 카고", 화물종류 = "감자",
            희망상차일시 = new DateTime(2026, 10, 5, 0, 0, 0, DateTimeKind.Utc),
            희망도착일시 = new DateTime(2026, 10, 5, 2, 0, 0, DateTimeKind.Utc), 취급메모 = "냉장"
        };
        var copy = WarehouseTransportHandoffDraft.Copy(source);
        source.하차담당자명 = "변경된 입력";
        source.하차연락처 = "변경된 입력";

        Assert.NotSame(source, copy);
        Assert.Equal(expected, copy.하차담당자명);
        Assert.Equal(expected, copy.하차연락처);
        Assert.Equal(17, copy.출고예정Id);
        Assert.Equal(71, copy.입고상품Id);
        Assert.Equal(4, copy.요청수량);
        Assert.Equal(source.희망상차일시, copy.희망상차일시);
        Assert.Equal(source.희망도착일시, copy.희망도착일시);
        Assert.Equal("동문", copy.하차지상세주소);
        Assert.Equal("냉장", copy.취급메모);
    }

    [Fact]
    public void Inventory_change_clears_both_consumer_drafts_but_same_selection_preserves_input()
    {
        var state = InventoryState();
        using var outbound = new 출고ViewModel(new 입출고작업Service(new RecordingApi()), state);
        outbound.운송인계초안.하차담당자명 = "첫 담당자";
        outbound.운송인계초안.하차연락처 = "010-1234-5678";
        outbound.운송인계.초안적용(InventoryDraft(51));

        Assert.True(state.재고선택(52));
        Assert.Null(outbound.운송인계초안.하차담당자명);
        Assert.Null(outbound.운송인계초안.하차연락처);
        Assert.Null(outbound.운송인계.초안.하차담당자명);
        Assert.Null(outbound.운송인계.초안.하차연락처);

        outbound.운송인계초안.하차담당자명 = "두 번째 담당자";
        outbound.운송인계.초안적용(InventoryDraft(52));
        Assert.True(state.재고선택(52));
        Assert.Equal("두 번째 담당자", outbound.운송인계초안.하차담당자명);
        Assert.Equal("현장 담당자", outbound.운송인계.초안.하차담당자명);

        state.재고목록적용([]);
        Assert.Null(outbound.운송인계초안.하차연락처);
        Assert.Null(outbound.운송인계.초안.하차연락처);
    }

    [Fact]
    public async Task Request_snapshot_and_late_response_do_not_overwrite_new_inventory_input()
    {
        var api = new RecordingApi { Delay = true };
        var state = InventoryState();
        using var handoff = new 출고운송인계ViewModel(new 입출고작업Service(api), state);
        var editable = InventoryDraft(51);
        handoff.초안적용(editable);
        editable.하차연락처 = "원본 초안 변경";
        var task = handoff.실행Async();
        await api.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var request = Assert.Single(api.Requests);
        handoff.초안.하차연락처 = "저장 중 입력 변경";
        Assert.Equal("010-1234-5678", request.하차연락처);

        Assert.True(state.재고선택(52));
        var nextDraft = InventoryDraft(52);
        nextDraft.하차담당자명 = "새 재고 담당자";
        handoff.초안적용(nextDraft);
        api.Completion.SetResult(api.Response);
        Assert.True(await task);

        Assert.Equal(52, state.선택된재고!.입고상품Id);
        Assert.Equal("새 재고 담당자", handoff.초안.하차담당자명);
        Assert.Null(state.최근운송의뢰);
    }

    [Fact]
    public async Task Failed_handoff_keeps_current_private_draft_for_retry()
    {
        var api = new RecordingApi { Failure = new InvalidOperationException("저장 실패") };
        using var handoff = new 출고운송인계ViewModel(new 입출고작업Service(api), InventoryState());
        handoff.초안적용(InventoryDraft(51));

        Assert.False(await handoff.실행Async());

        Assert.Equal("현장 담당자", handoff.초안.하차담당자명);
        Assert.Equal("010-1234-5678", handoff.초안.하차연락처);
    }

    private static 운송의뢰초안PageViewModel CreatePage(PlanQuery query, RecordingApi api)
        => new(new(query), new(), new(new 입출고작업Service(api)));

    private static 운송의뢰초안작성ViewModel ReadyDraft()
    {
        var draft = new 운송의뢰초안작성ViewModel();
        draft.원장설정(Plan(17));
        CompleteInput(draft);
        return draft;
    }

    private static void CompleteInput(운송의뢰초안작성ViewModel draft)
    {
        draft.하차지주소 = "서울특별시 송파구 올림픽로 300";
        draft.희망상차일 = new DateTime(2026, 10, 5);
        draft.희망상차시각 = new TimeSpan(9, 0, 0);
        draft.희망도착일 = new DateTime(2026, 10, 5);
        draft.희망도착시각 = new TimeSpan(11, 0, 0);
        draft.차량유형 = "1톤 카고";
        draft.상품수량확인 = true;
    }

    private static 출고예정검토상세응답 Plan(long id, bool saved = false)
        => new()
        {
            OutboundPlanId = id, InboundItemId = 71, ProductName = "감자", Quantity = 4,
            CanStartTransportRequestDraft = !saved,
            TransportRequestId = saved ? $"warehouse-outbound-{id}" : null
        };

    private static 입출고화면상태ViewModel InventoryState()
    {
        var state = new 입출고화면상태ViewModel();
        state.창고목록적용([new 창고요약응답 { Id = 1, 기본창고여부 = true }]);
        state.재고목록적용([
            new 재고항목응답 { 입고상품Id = 51, 창고Id = 1, 가용수량 = 8 },
            new 재고항목응답 { 입고상품Id = 52, 창고Id = 1, 가용수량 = 8 }
        ]);
        return state;
    }

    private static 재고운송의뢰생성요청 InventoryDraft(long inventoryId)
        => new()
        {
            입고상품Id = inventoryId, 요청수량 = 1, 하차지주소 = "서울특별시 송파구 올림픽로 300",
            하차담당자명 = "현장 담당자", 하차연락처 = "010-1234-5678"
        };

    private sealed class PlanQuery : I출고예정검토페이지Service
    {
        public List<long> DetailIds { get; } = [];
        public long? BlockedId { get; set; }
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<출고예정검토상세응답?> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<출고예정검토목록페이지응답> 목록조회Async(출고예정검토목록조회요청 request, CancellationToken cancellationToken = default)
            => Task.FromResult(new 출고예정검토목록페이지응답());
        public async Task<출고예정검토상세응답?> 상세조회Async(long outboundPlanId, CancellationToken cancellationToken = default)
        {
            var saved = DetailIds.Contains(outboundPlanId);
            DetailIds.Add(outboundPlanId);
            if (BlockedId == outboundPlanId)
            {
                Started.TrySetResult();
                return await Completion.Task.WaitAsync(cancellationToken);
            }
            return Plan(outboundPlanId, saved);
        }
        public Task<출고운송인계완료응답> 인계완료Async(long outboundPlanId, 출고운송인계완료요청 request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class RecordingApi : ISsalddelJsonApiClient
    {
        public List<재고운송의뢰생성요청> Requests { get; } = [];
        public 화주운송의뢰응답 Response { get; } = new() { 의뢰Id = "warehouse-outbound-17" };
        public bool Delay { get; init; }
        public Exception? Failure { get; init; }
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<화주운송의뢰응답> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<TResponse?> GetAsync<TResponse>(string path, string operationName, bool allowNotFound = true, CancellationToken cancellationToken = default)
            => Task.FromResult<TResponse?>(default);
        public Task<TResponse?> SendAsync<TResponse>(HttpMethod method, string path, string operationName, bool allowNotFound = false, CancellationToken cancellationToken = default)
            => Task.FromResult<TResponse?>(default);
        public async Task<TResponse?> SendAsync<TRequest, TResponse>(HttpMethod method, string path, TRequest request, string operationName, bool allowNotFound = false, CancellationToken cancellationToken = default)
        {
            if (request is not 재고운송의뢰생성요청 transportRequest) return default;
            Requests.Add(transportRequest);
            Started.TrySetResult();
            if (Failure is not null) throw Failure;
            var response = Delay ? await Completion.Task.WaitAsync(cancellationToken) : Response;
            return response is TResponse typed ? typed : default;
        }
        public Task SendAsync(HttpMethod method, string path, string operationName, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
        public Task SendAsync<TRequest>(HttpMethod method, string path, TRequest request, string operationName, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
