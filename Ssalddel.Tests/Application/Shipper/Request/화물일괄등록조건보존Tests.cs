using System.Text;
using FluentResults;
using MediatR;
using Ssalddel.Application.Shipper.Request;
using Ssalddel.Contracts.Shipper.Request;

namespace Ssalddel.Tests.Application.Shipper.Request;

public sealed class 화물일괄등록조건보존Tests
{
    [Fact]
    public async Task CSV의명시시간대와연락처및false조건이_생성명령까지보존된다()
    {
        const string csv = "행번호,화물종류,픽업도로명주소,하차도로명주소,픽업연락처이름,픽업연락처전화번호,하차연락처이름,하차연락처전화번호,픽업시간창시작일시,픽업시간창종료일시,하차시간창시작일시,하차시간창종료일시,화물바닥회전가능여부,화물적층가능여부\n1,상자,합성 상차지,합성 하차지,가상 상차담당,010-0000-0001,가상 하차담당,010-0000-0002,2026-10-20T09:00:00+09:00,2026-10-20T10:00:00+09:00,2026-10-20T11:00:00+09:00,2026-10-20T12:00:00+09:00,false,false";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var parsed = await new 화주운송의뢰일괄등록파서Service().ParseAsync(stream, "orders.csv");
        Assert.Empty(parsed.오류목록);
        var row = Assert.Single(parsed.행목록);
        Assert.Empty(화주운송의뢰일괄등록지원.ValidateRow(row));
        var command = 화주운송의뢰일괄등록지원.To의뢰생성Command(row, new() { 운송방식 = "혼적", 차량종류 = "1톤 카고" });
        Assert.Equal(new DateTime(2026, 10, 20, 0, 0, 0, DateTimeKind.Utc), command.픽업시간창시작일시);
        Assert.Equal(new DateTime(2026, 10, 20, 1, 0, 0, DateTimeKind.Utc), command.픽업시간창종료일시);
        Assert.Equal(new DateTime(2026, 10, 20, 3, 0, 0, DateTimeKind.Utc), command.하차시간창종료일시);
        Assert.Equal("가상 상차담당", command.픽업연락처이름);
        Assert.Equal("010-0000-0002", command.하차연락처전화번호);
        Assert.False(command.화물바닥회전가능여부);
        Assert.False(command.화물적층가능여부);
    }

    [Theory]
    [InlineData("2026-10-20T09:00:00")]
    [InlineData("언젠가")]
    public async Task 시간대가없거나해석할수없는CSV시각을_PC시간으로추정하지않는다(string value)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes($"화물종류,픽업시간창시작일시\n상자,{value}"));
        var result = await new 화주운송의뢰일괄등록파서Service().ParseAsync(stream, "orders.csv");
        Assert.Contains(result.오류목록, error => error.Contains("시간대가 있는 날짜"));
        Assert.Null(Assert.Single(result.행목록).픽업시간창시작일시);
    }

    [Fact]
    public async Task 연락처와약속이없으면_추천이있어도임시정보로접수하지않는다()
    {
        var row = ValidRow(); row.픽업연락처전화번호 = null; row.픽업시간창시작일시 = null;
        var sender = new RecordingSender();
        var handler = new 화주운송의뢰일괄확정등록CommandHandler(sender, new 화주운송의뢰추천Service(), new VehicleService());
        var result = await handler.Handle(new([new() { 행번호 = 1, 원본행 = row }]), default);
        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.실패행수);
        Assert.Contains(result.Value.행결과목록[0].오류, error => error.Contains("상차 담당자"));
        Assert.Contains(result.Value.행결과목록[0].오류, error => error.Contains("상차 가능한"));
        Assert.Null(sender.Command);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task 추천후보가없거나근거가다르면_기본차량이나수동문자열로통과하지않는다(bool empty, bool mismatched)
    {
        var row = ValidRow(); row.차량종류 = "1톤";
        var sender = new RecordingSender(); var vehicle = new VehicleService { Empty = empty, Mismatched = mismatched };
        var preview = await new 화주운송의뢰일괄미리보기CommandHandler(new 화주운송의뢰추천Service(), vehicle)
            .Handle(new([row], []), default);
        Assert.False(Assert.Single(preview.Value.행목록).유효함);
        var confirm = await new 화주운송의뢰일괄확정등록CommandHandler(sender, new 화주운송의뢰추천Service(), vehicle)
            .Handle(new([new() { 행번호 = 1, 원본행 = row, 최종선택차량종류 = "1톤" }]), default);
        Assert.Equal(1, confirm.Value.실패행수);
        Assert.Null(sender.Command);
    }

    [Fact]
    public async Task 현재행의설명과취급요청및시간조건을_재추천과생성에동일하게넘긴다()
    {
        var row = ValidRow();
        var sender = new RecordingSender(); var vehicle = new VehicleService();
        var result = await new 화주운송의뢰일괄확정등록CommandHandler(sender, new 화주운송의뢰추천Service(), vehicle)
            .Handle(new([new() { 행번호 = 1, 원본행 = row, 최종선택차량종류 = "1톤 카고" }]), default);
        Assert.True(Assert.Single(result.Value.행결과목록).성공);
        var sent = Assert.IsType<의뢰생성Command>(sender.Command);
        Assert.Equal(row.화물설명, vehicle.Input!.화물설명);
        Assert.Equal(row.서비스레벨, vehicle.Input.서비스레벨);
        Assert.Equal(row.요청사항, vehicle.Input.요청사항);
        Assert.Equal(sent.운송방식, vehicle.Input.운송방식);
        Assert.Equal(row.픽업시간창시작일시, sent.픽업시간창시작일시);
        Assert.Equal(row.하차시간창종료일시, sent.하차시간창종료일시);
        Assert.Equal(row.화물바닥회전가능여부, sent.화물바닥회전가능여부);
    }

    private static 화주운송의뢰일괄등록행입력 ValidRow() => new()
    {
        행번호 = 1, 화물종류 = "상자", 화물설명 = "비 맞으면 안 됨", 화물수량 = 2,
        화물길이Mm = 400, 화물폭Mm = 300, 화물높이Mm = 200, 화물중량Kg = 12.5m,
        화물바닥회전가능여부 = false, 화물적층가능여부 = false, 운송방식 = "혼적", 서비스레벨 = "리프트", 요청사항 = "측면 상차",
        픽업도로명주소 = "합성 상차지", 하차도로명주소 = "합성 하차지",
        픽업연락처이름 = "가상 상차담당", 픽업연락처전화번호 = "010-0000-0001",
        하차연락처이름 = "가상 하차담당", 하차연락처전화번호 = "010-0000-0002",
        픽업시간창시작일시 = new(2026, 10, 20, 0, 0, 0, DateTimeKind.Utc),
        픽업시간창종료일시 = new(2026, 10, 20, 1, 0, 0, DateTimeKind.Utc),
        하차시간창시작일시 = new(2026, 10, 20, 2, 0, 0, DateTimeKind.Utc),
        하차시간창종료일시 = new(2026, 10, 20, 3, 0, 0, DateTimeKind.Utc)
    };

    private sealed class VehicleService : I차량추천Service
    {
        public bool Empty { get; init; }
        public bool Mismatched { get; init; }
        public 차량추천요청? Input { get; private set; }
        public Task<차량추천응답> 추천Async(차량추천요청 request, CancellationToken cancellationToken)
        {
            Input = request;
            return Task.FromResult(new 차량추천응답
            {
                적재조건 = 화물적재입력Policy.계산(화물적재입력Policy.입력생성(request) with
                    { 수량 = Mismatched ? request.화물수량 + 1 : request.화물수량 }),
                추천차량종류 = Empty ? string.Empty : "1톤 카고",
                후보목록 = Empty ? [] : [new() { 차량종류 = "1톤 카고", 단일운송가능여부 = true }]
            });
        }
    }

    private sealed class RecordingSender : ISender
    {
        public 의뢰생성Command? Command { get; private set; }
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            Command = Assert.IsType<의뢰생성Command>(request);
            return Task.FromResult((TResponse)(object)Result.Ok(new 화주운송의뢰응답 { 의뢰Id = "synthetic-order" }));
        }
        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest => throw new NotSupportedException();
        public Task<object?> Send(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
