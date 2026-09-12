using Microsoft.Extensions.Logging.Abstractions;
using Ssalddel.Application.Driver.Transport;
using Ssalddel.Contracts.Common.Operations;
using Ssalddel.Services.Operations;

namespace Ssalddel.Tests.Application.Driver.Transport;

public sealed class 운송완료화주인수인계EventHandlerTests
{
    [Fact]
    public async Task 인수완료Event는_하차증빙확인을완료결과인계에전달한다()
    {
        var service = new RecordingHandoffService();
        var handler = new 운송완료화주인수인계EventHandler(
            service,
            NullLogger<운송완료화주인수인계EventHandler>.Instance);

        await handler.Handle(
            new 운송인수완료됨Event(
                701,
                "request-701",
                "driver-701",
                "출발지",
                "도착지",
                "인수완료",
                DateTime.UtcNow,
                "trace-701",
                new 운송하차완료증빙("private-object-name", "private-url")),
            CancellationToken.None);

        Assert.Equal(701, service.운송Id);
        Assert.True(service.하차완료증빙확인여부);
    }

    private sealed class RecordingHandoffService : I화물운송완료화주인수인계Service
    {
        public long 운송Id { get; private set; }
        public bool 하차완료증빙확인여부 { get; private set; }

        public Task<운영체제업무인계Dto> 완료결과요청Async(
            long 운송Id,
            bool 하차완료증빙확인여부,
            CancellationToken cancellationToken = default)
        {
            this.운송Id = 운송Id;
            this.하차완료증빙확인여부 = 하차완료증빙확인여부;
            return Task.FromResult(new 운영체제업무인계Dto
            {
                인계StableId = "os-handoff:test",
                상태Code = 운영체제업무인계상태Codes.요청됨
            });
        }

        public Task<운영체제업무인계Dto?> 화주인수Async(
            string 운송의뢰Id,
            CancellationToken cancellationToken = default)
            => Task.FromResult<운영체제업무인계Dto?>(null);
    }
}
