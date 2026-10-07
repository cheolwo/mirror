using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Ssalddel.Contracts.Common.Education;
using Ssalddel.Services.Community;
using Ssalddel.Services.Education;
using 살뜰.Services.Options;

namespace Ssalddel.Tests.Services.Education;

public sealed partial class 현장체험활동UseCaseTests
{
    [Fact]
    public async Task 전송완료후_추가한_활동기록은_새로운제출로_연결된다()
    {
        var useCase = CreateUseCase(out _, out var queue);
        var id = await ReadyAsync(useCase);
        var request = new 현장체험학교제출요청 { 전송방식 = 교육기관제출방식.이메일 };
        var first = await useCase.학교제출Async(id, request, "student-1", CancellationToken.None);
        var firstId = Assert.Single(first.Value.제출목록).제출Id;
        await queue.완료Async(firstId, 교육기관제출상태.전송완료, CancellationToken.None);
        var repeated = await useCase.학교제출Async(id, request, "student-1", CancellationToken.None);
        Assert.Single(repeated.Value.제출목록);

        var added = await useCase.활동기록Async(id, new()
        {
            활동명 = "추가 현장 기록", 활동내용 = "두 번째 활동", 수행역할 = "관찰",
            시작시각 = DateTimeOffset.UtcNow.AddMinutes(-20), 종료시각 = DateTimeOffset.UtcNow, 확인자표시명 = "담당자"
        }, "student-1", CancellationToken.None);
        Assert.True(added.IsSuccess);
        var next = await useCase.학교제출Async(id, request, "student-1", CancellationToken.None);
        Assert.True(next.IsSuccess);
        Assert.Equal(2, next.Value.제출목록.Count);
        Assert.Single(next.Value.제출목록.Where(x => x.제출Id != firstId && x.상태 == 교육기관제출상태.전송대기));
    }

    [Fact]
    public async Task 원장저장후_예약실패를_같은제출Id로_복구한다()
    {
        var useCase = CreateUseCase(out var store, out var queue);
        var id = await ReadyAsync(useCase);
        queue.FailReservationOnce = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => useCase.학교제출Async(id,
            new() { 전송방식 = 교육기관제출방식.이메일 }, "student-1", CancellationToken.None));
        var savedId = Assert.Single(store.Items[id].블록목록.Where(x => x.BlockType == 현장체험활동원장상수.학교제출Block)).BlockId;

        var retry = await useCase.학교제출Async(id,
            new() { 전송방식 = 교육기관제출방식.이메일 }, "student-1", CancellationToken.None);
        Assert.True(retry.IsSuccess);
        Assert.Equal(savedId, Assert.Single(retry.Value.제출목록).제출Id);
        Assert.Single(store.Items[id].블록목록.Where(x => x.BlockType == 현장체험활동원장상수.학교제출Block));
    }

    [Fact]
    public async Task 조회와_저장사이_다른수정은_덮어쓰지_않고_409를_반환한다()
    {
        var useCase = CreateUseCase(out var store, out _);
        var id = await ReadyAsync(useCase);
        store.BeforeSave = () => { store.Items[id].Revision++; store.Items[id].제목 = "먼저 변경한 제목"; };
        var result = await useCase.보호자승인Async(id, new() { 승인여부 = false, 보호자표시명 = "보호자" }, "guardian-1", CancellationToken.None);
        Assert.True(result.IsFailed);
        Assert.Equal(409, result.Errors[0].Metadata["StatusCode"]);
        Assert.Equal("먼저 변경한 제목", store.Items[id].제목);
    }

    [Theory]
    [InlineData(현장체험활동상태.출석인정)]
    [InlineData(현장체험활동상태.출석미인정)]
    public async Task 결정후_제출처리는_학교결정을_심사중으로_되돌리지_않는다(string finalState)
    {
        var useCase = CreateUseCase(out var store, out var queue);
        var id = await ReadyAsync(useCase);
        var submitted = await useCase.학교제출Async(id, new() { 전송방식 = 교육기관제출방식.이메일 }, "student-1", CancellationToken.None);
        var submissionId = Assert.Single(submitted.Value.제출목록).제출Id;
        store.Items[id].상태 = finalState;
        queue.Work.Enqueue(new(submissionId, id, 교육기관제출방식.이메일, null, "teacher@example.com", 1));
        var sender = new CountingSender();
        using var services = WorkerServices(store, sender);
        await Worker(queue, services).ProcessPendingAsync(new 교육기관제출Options(), CancellationToken.None);
        Assert.Equal(finalState, store.Items[id].상태);
        Assert.Contains(submissionId, queue.Projected);
        Assert.Equal(1, sender.Calls);
    }

    [Fact]
    public async Task 외부전송후_원장반영실패는_재전송없이_복구한다()
    {
        var useCase = CreateUseCase(out var store, out var queue);
        var id = await ReadyAsync(useCase);
        var submitted = await useCase.학교제출Async(id, new() { 전송방식 = 교육기관제출방식.이메일 }, "student-1", CancellationToken.None);
        var submissionId = Assert.Single(submitted.Value.제출목록).제출Id;
        queue.Work.Enqueue(new(submissionId, id, 교육기관제출방식.이메일, null, "teacher@example.com", 1));
        store.FailProjectionOnce = true;
        var sender = new CountingSender();
        using var services = WorkerServices(store, sender);
        var worker = Worker(queue, services);
        await worker.ProcessPendingAsync(new 교육기관제출Options(), CancellationToken.None);
        Assert.Equal(현장체험활동상태.제출대기, store.Items[id].상태);
        Assert.NotNull(queue.Items[submissionId].전송완료시각Utc);
        Assert.DoesNotContain(submissionId, queue.Projected);

        queue.Work.Enqueue(new(submissionId, id, 교육기관제출방식.이메일, null, "teacher@example.com", 2, 전송완료: true));
        await worker.ProcessPendingAsync(new 교육기관제출Options(), CancellationToken.None);
        Assert.Equal(현장체험활동상태.학교심사중, store.Items[id].상태);
        Assert.Contains(submissionId, queue.Projected);
        Assert.Equal(1, sender.Calls);
    }

    private static async Task<string> ReadyAsync(현장체험활동UseCase useCase)
    {
        var request = CreateRequest();
        request.현장체험지도자UserId = null;
        request.현장체험지도자표시명 = null;
        var created = await useCase.생성Async(request, "student-1", CancellationToken.None);
        Assert.True(created.IsSuccess);
        var id = created.Value.원장Id;
        Assert.True((await useCase.활동기록Async(id, new()
        {
            활동명 = "현장 관찰", 활동내용 = "업무 흐름 기록", 수행역할 = "관찰",
            시작시각 = DateTimeOffset.UtcNow.AddHours(-1), 종료시각 = DateTimeOffset.UtcNow, 확인자표시명 = "담당자"
        }, "student-1", CancellationToken.None)).IsSuccess);
        Assert.True((await useCase.보호자승인Async(id, new() { 승인여부 = true, 보호자표시명 = "보호자" }, "guardian-1", CancellationToken.None)).IsSuccess);
        return id;
    }

    private static ServiceProvider WorkerServices(FakeLedgerStore store, CountingSender sender)
        => new ServiceCollection().AddSingleton<I커뮤니티원장저장소>(store)
            .AddSingleton<I교육기관제출전송Service>(sender).BuildServiceProvider();

    private static 교육기관제출Worker Worker(FakeSubmissionQueue queue, ServiceProvider services)
        => new(queue, services.GetRequiredService<IServiceScopeFactory>(), new StaticOptions(), NullLogger<교육기관제출Worker>.Instance);

    private sealed class CountingSender : I교육기관제출전송Service
    {
        public int Calls { get; private set; }
        public Task<교육기관제출전송결과> 전송Async(교육기관제출작업 work, 커뮤니티원장Dto ledger, CancellationToken cancellationToken)
        { Calls++; return Task.FromResult(교육기관제출전송결과.완료()); }
    }

    private sealed class StaticOptions : IOptionsMonitor<교육기관제출Options>
    {
        public 교육기관제출Options CurrentValue { get; } = new();
        public 교육기관제출Options Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<교육기관제출Options, string?> listener) => null;
    }
}
