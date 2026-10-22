using System.Text.Json;
using Ssalddel.Contracts.Common.Inbound;

namespace Ssalddel.Tests.Contracts;

public sealed class InboundSubmissionRecoveryContractTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    [Fact]
    public void 구판저장요청과응답은_요청번호미지원으로읽고_재시도성공으로바꾸지않는다()
    {
        var request = JsonSerializer.Deserialize<입고요청저장요청>("{\"창고Id\":17,\"공급처명\":\"공급처\"}", Web)!;
        Assert.Null(request.ClientRequestId);
        Assert.Equal(17, request.창고Id);
        var response = JsonSerializer.Deserialize<입고요청항목응답>("{\"id\":31,\"상태\":\"입고예정\"}", Web)!;
        Assert.Null(response.ClientRequestId);
        Assert.False(response.멱등재시도여부);
        Assert.Equal(31, response.Id);
    }

    [Fact]
    public void 원요청번호는_요청과정본응답에서_같은선택적계약으로왕복한다()
    {
        var id = Guid.NewGuid();
        var request = new 입고요청저장요청 { ClientRequestId = id, 창고Id = 17, 공급처명 = "공급처" };
        var requestJson = JsonSerializer.Serialize(request, Web);
        using var doc = JsonDocument.Parse(requestJson);
        Assert.Equal(id, doc.RootElement.GetProperty("clientRequestId").GetGuid());
        Assert.Equal(id, JsonSerializer.Deserialize<입고요청저장요청>(requestJson, Web)!.ClientRequestId);
        var response = new 입고요청항목응답 { Id = 31, ClientRequestId = id, 멱등재시도여부 = true };
        var readback = JsonSerializer.Deserialize<입고요청항목응답>(JsonSerializer.Serialize(response, Web), Web)!;
        Assert.Equal(31, readback.Id);
        Assert.Equal(id, readback.ClientRequestId);
        Assert.True(readback.멱등재시도여부);
    }
}
