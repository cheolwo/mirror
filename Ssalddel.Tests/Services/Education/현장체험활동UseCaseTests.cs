using Ssalddel.Contracts.Common.Education;
using Ssalddel.Controllers.Common;
using Ssalddel.Services.Community;
using Ssalddel.Services.Education;
using Microsoft.AspNetCore.Authorization;
using 살뜰.Data;

namespace Ssalddel.Tests.Services.Education;

public sealed partial class 현장체험활동UseCaseTests
{
    [Fact]
    public void 선생님과_현장체험지도자의_API권한은_겹치지_않는다()
    {
        var controllerType = typeof(현장체험활동Controller);
        var schoolDecisionRoles = controllerType.GetMethod("학교결정")!
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Single()
            .Roles!;
        var fieldVerificationRoles = controllerType.GetMethod("현장지도자확인")!
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Single()
            .Roles!;

        Assert.Contains(역할명.선생님, schoolDecisionRoles);
        Assert.DoesNotContain(역할명.현장체험지도자, schoolDecisionRoles);
        Assert.Contains(역할명.현장체험지도자, fieldVerificationRoles);
        Assert.DoesNotContain(역할명.선생님, fieldVerificationRoles);
    }

    [Fact]
    public async Task 생성은_비공개_교육원장과_필수블록을_만든다()
    {
        var ledgerStore = new FakeLedgerStore();
        var useCase = new 현장체험활동UseCase(ledgerStore, new FakeSubmissionQueue());

        var result = await useCase.생성Async(CreateRequest(), "student-1", CancellationToken.None);

        Assert.True(result.IsSuccess);
        var saved = Assert.Single(ledgerStore.Items.Values);
        Assert.Equal("education-private", saved.커뮤니티Id);
        Assert.Equal(현장체험활동원장상수.원장템플릿Key, saved.원장템플릿Key);
        Assert.Contains(saved.블록목록, x => x.BlockType == 현장체험활동원장상수.학생계획Block);
        Assert.Contains(saved.블록목록, x => x.BlockType == 현장체험활동원장상수.활동계획Block);
        Assert.Contains(saved.블록목록, x => x.BlockType == 현장체험활동원장상수.보호자승인Block);
        Assert.Contains(saved.참여자목록, x => x.UserId == "student-1" && x.RoleLabel == "학생");
        Assert.Contains(saved.참여자목록, x => x.UserId == "guardian-1" && x.RoleLabel == "보호자");
        Assert.Contains(saved.참여자목록, x => x.UserId == "field-guide-1" && x.RoleLabel == "현장체험지도자");

    }

    [Fact]
    public async Task 활동기록과_보호자승인_전에는_학교제출을_막는다()
    {
        var useCase = CreateUseCase(out _, out _);
        var created = await useCase.생성Async(CreateRequest(), "student-1", CancellationToken.None);

        var result = await useCase.학교제출Async(
            created.Value.원장Id,
            new 현장체험학교제출요청 { 전송방식 = 교육기관제출방식.문서 },
            "student-1",
            CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Contains("활동 기록 1건 이상", result.Errors[0].Message);
    }

    [Fact]
    public async Task 학생기록_보호자승인_문서제출_학교결정이_순서대로_완료된다()
    {
        var useCase = CreateUseCase(out var ledgerStore, out var queue);
        var created = await useCase.생성Async(CreateRequest(), "student-1", CancellationToken.None);

        var activity = await useCase.활동기록Async(
            created.Value.원장Id,
            new 현장체험활동기록요청
            {
                활동명 = "물류 현장 분류 체험",
                활동내용 = "안전 교육 후 상품 분류 과정을 관찰하고 기록함",
                수행역할 = "관찰 및 기록",
                시작시각 = new DateTimeOffset(2026, 7, 15, 9, 0, 0, TimeSpan.FromHours(9)),
                종료시각 = new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.FromHours(9)),
                확인자표시명 = "현장 담당자",
                증빙파일Url목록 = ["https://files.example/activity-1.jpg"]
            },
            "student-1",
            CancellationToken.None);
        Assert.True(activity.IsSuccess);
        Assert.Equal(1, activity.Value.활동기록수);
        Assert.Equal(0, activity.Value.현장확인완료수);
        Assert.Equal(1, activity.Value.증빙파일수);

        var approval = await useCase.보호자승인Async(
            created.Value.원장Id,
            new 현장체험보호자승인요청
            {
                승인여부 = true,
                보호자표시명 = "보호자"
            },
            "guardian-1",
            CancellationToken.None);
        Assert.True(approval.IsSuccess);
        Assert.False(approval.Value.학교제출요건충족);

        var activityRecordId = ledgerStore.Items[created.Value.원장Id].블록목록
            .Single(x => x.BlockType == 현장체험활동원장상수.활동기록Block)
            .BlockId;
        var wrongGuide = await useCase.현장지도자확인Async(
            created.Value.원장Id,
            activityRecordId,
            new 현장체험지도자확인요청
            {
                실제활동확인여부 = true,
                지도자표시명 = "다른 지도자"
            },
            "other-guide",
            CancellationToken.None);
        Assert.True(wrongGuide.IsFailed);

        var fieldVerification = await useCase.현장지도자확인Async(
            created.Value.원장Id,
            activityRecordId,
            new 현장체험지도자확인요청
            {
                실제활동확인여부 = true,
                지도자표시명 = "현장 지도자",
                확인내용 = "안전 교육과 상품 분류 체험을 확인함"
            },
            "field-guide-1",
            CancellationToken.None);
        Assert.True(fieldVerification.IsSuccess);
        Assert.Equal(1, fieldVerification.Value.현장확인완료수);
        Assert.True(fieldVerification.Value.학교제출요건충족);

        var submission = await useCase.학교제출Async(
            created.Value.원장Id,
            new 현장체험학교제출요청 { 전송방식 = 교육기관제출방식.문서 },
            "student-1",
            CancellationToken.None);
        Assert.True(submission.IsSuccess);
        Assert.Equal(교육기관제출상태.수동제출준비, Assert.Single(queue.Items.Values).상태);

        var denied = await useCase.학교결정Async(
            created.Value.원장Id,
            DecisionRequest(),
            "student-1",
            검토학교Key: "other-school",
            운영자권한: false,
            CancellationToken.None);
        Assert.True(denied.IsFailed);

        var decision = await useCase.학교결정Async(
            created.Value.원장Id,
            DecisionRequest(),
            "school-reviewer-1",
            검토학교Key: "ssalddel-middle",
            운영자권한: false,
            CancellationToken.None);
        Assert.True(decision.IsSuccess);
        Assert.Equal(현장체험활동상태.출석인정, decision.Value.상태);
        Assert.True(decision.Value.출석인정여부);
    }

    [Fact]
    public async Task 등록되지_않은_사용자는_원장을_조회할_수_없다()
    {
        var useCase = CreateUseCase(out _, out _);
        var created = await useCase.생성Async(CreateRequest(), "student-1", CancellationToken.None);

        var result = await useCase.조회Async(
            created.Value.원장Id,
            "other-user",
            검토학교Key: null,
            운영자권한: false,
            CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal(403, result.Errors[0].Metadata["StatusCode"]);
    }

    private static 현장체험활동UseCase CreateUseCase(
        out FakeLedgerStore ledgerStore,
        out FakeSubmissionQueue queue)
    {
        ledgerStore = new FakeLedgerStore();
        queue = new FakeSubmissionQueue();
        return new 현장체험활동UseCase(ledgerStore, queue);
    }

    private static 현장체험활동생성요청 CreateRequest()
        => new()
        {
            제목 = "생활 물류 현장 체험",
            학생표시명 = "학생",
            학교식별Key = "ssalddel-middle",
            학교명 = "살뜰중학교",
            학년반 = "2학년 1반",
            보호자UserId = "guardian-1",
            보호자표시명 = "보호자",
            현장체험지도자UserId = "field-guide-1",
            현장체험지도자표시명 = "현장 지도자",
            활동목표 = "생활 물류 업무의 흐름을 이해한다.",
            활동장소 = "살뜰 물류 체험장",
            시작예정시각 = new DateTimeOffset(2026, 7, 15, 9, 0, 0, TimeSpan.FromHours(9)),
            종료예정시각 = new DateTimeOffset(2026, 7, 15, 16, 0, 0, TimeSpan.FromHours(9)),
            계획활동 = ["안전 교육", "상품 분류 관찰"],
            학교담당이메일 = "teacher@example.com"
        };

    private static 현장체험학교결정요청 DecisionRequest()
        => new()
        {
            출석인정여부 = true,
            결정기관명 = "살뜰중학교",
            결정자표시명 = "담임교사",
            결정문서번호 = "SCHOOL-2026-001"
        };

    private sealed class FakeLedgerStore : I커뮤니티원장저장소
    {
        public Dictionary<string, 커뮤니티원장Dto> Items { get; } = new(StringComparer.Ordinal);
        public Action? BeforeSave { get; set; }
        public bool FailProjectionOnce { get; set; }

        public Task<커뮤니티원장Dto> 원장저장Async(
            커뮤니티원장저장요청 request,
            string updatedBy,
            CancellationToken cancellationToken = default)
        {
            var id = string.IsNullOrWhiteSpace(request.원장Id) ? $"ledger-{Guid.NewGuid():N}" : request.원장Id;
            BeforeSave?.Invoke();
            var createdAt = Items.TryGetValue(id, out var existing) ? existing.생성시각Utc : DateTime.UtcNow;
            if (request.기대Revision.HasValue && request.기대Revision != (existing?.Revision ?? 0))
                throw new InvalidOperationException("원장의 현재 상태가 다른 요청에서 먼저 변경되었습니다.");
            var item = new 커뮤니티원장Dto
            {
                Revision = (existing?.Revision ?? 0) + 1,
                원장Id = id,
                커뮤니티Id = request.커뮤니티Id,
                원장템플릿Key = request.원장템플릿Key,
                제목 = request.제목,
                원함 = request.원함,
                상태 = request.상태 ?? "초안",
                현재단계Key = request.현재단계Key,
                대상OsCode = request.대상OsCode,
                대상OsName = request.대상OsName,
                생성자UserId = request.생성자UserId,
                생성자표시명 = request.생성자표시명 ?? "익명 참여자",
                블록목록 = request.블록목록,
                참여자목록 = request.참여자목록,
                다이어그램스냅샷 = request.다이어그램스냅샷,
                외부참조 = request.외부참조,
                확장속성 = request.확장속성,
                생성시각Utc = createdAt,
                수정시각Utc = DateTime.UtcNow
            };
            Items[id] = item;
            return Task.FromResult(item);
        }

        public Task<커뮤니티원장Dto?> 원장조회Async(string 원장Id, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.GetValueOrDefault(원장Id));

        public Task<IReadOnlyList<커뮤니티원장Dto>> 원장목록조회Async(
            커뮤니티원장조회조건 query,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<커뮤니티원장Dto>>(Items.Values.ToArray());

        public Task<커뮤니티원장Dto?> 원장상태변경Async(
            커뮤니티원장상태변경요청 request,
            string updatedBy,
            CancellationToken cancellationToken = default)
        {
            if (!Items.TryGetValue(request.원장Id, out var item))
            {
                return Task.FromResult<커뮤니티원장Dto?>(null);
            }

            if (FailProjectionOnce) { FailProjectionOnce = false; throw new InvalidOperationException("injected projection failure"); }
            if (request.기대Revision != item.Revision || request.이전상태 != item.상태)
                throw new InvalidOperationException("원장의 현재 상태가 다른 요청에서 먼저 변경되었습니다.");
            item.Revision++;
            item.상태 = request.상태;
            item.현재단계Key = request.현재단계Key;
            item.수정시각Utc = DateTime.UtcNow;
            return Task.FromResult<커뮤니티원장Dto?>(item);
        }
    }

    private sealed class FakeSubmissionQueue : I교육기관제출대기열
    {
        public Dictionary<string, 현장체험제출상태응답> Items { get; } = new(StringComparer.Ordinal);
        public Queue<교육기관제출작업> Work { get; } = new();
        public HashSet<string> Projected { get; } = new();
        public bool FailReservationOnce { get; set; }

        public Task<현장체험제출상태응답> 예약Async(
            string 제출Id,
            string 원장Id,
            string 전송방식,
            string? 제출처Key,
            string? 담당이메일,
            CancellationToken cancellationToken)
        {
            if (FailReservationOnce) { FailReservationOnce = false; throw new InvalidOperationException("injected reservation failure"); }
            if (Items.TryGetValue(제출Id, out var existing)) return Task.FromResult(existing);
            var item = new 현장체험제출상태응답
            {
                제출Id = 제출Id,
                전송방식 = 전송방식,
                상태 = 교육기관제출상태.전송대기,
                제출처 = 제출처Key ?? 담당이메일,
                생성시각Utc = DateTime.UtcNow
            };
            Items[제출Id] = item;
            LedgerIds[제출Id] = 원장Id;
            return Task.FromResult(item);
        }

        public Task<교육기관제출작업?> 다음작업확보Async(CancellationToken cancellationToken)
            => Task.FromResult<교육기관제출작업?>(Work.Count == 0 ? null : Work.Dequeue());

        public Task 원장반영완료Async(string 제출Id, CancellationToken cancellationToken)
        {
            Projected.Add(제출Id);
            return Task.CompletedTask;
        }

        public Task 완료Async(string 제출Id, string 상태, CancellationToken cancellationToken)
        {
            Items[제출Id].상태 = 상태;
            Items[제출Id].전송완료시각Utc = DateTime.UtcNow;
            return Task.CompletedTask;
        }

        public Task 실패Async(
            string 제출Id,
            string 오류,
            bool 설정대기,
            int 최대시도횟수,
            CancellationToken cancellationToken)
        {
            Items[제출Id].상태 = Items[제출Id].전송완료시각Utc.HasValue
                ? 교육기관제출상태.전송완료
                : 설정대기 ? 교육기관제출상태.설정대기 : 교육기관제출상태.전송실패;
            Items[제출Id].마지막오류 = 오류;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<현장체험제출상태응답>> 원장별조회Async(
            string 원장Id,
            CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<현장체험제출상태응답>>(
                Items.Where(x => LedgerIds.GetValueOrDefault(x.Key) == 원장Id).Select(x => x.Value).ToArray());

        private Dictionary<string, string> LedgerIds { get; } = new(StringComparer.Ordinal);
    }
}
