using System.Security.Claims;
using FluentResults;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Ssalddel.Application.Community;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Controllers.Common;
using Ssalddel.Services.Community;

namespace Ssalddel.Tests.Controllers;

public sealed class OrderLedgerMutationDisclosureTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task Mutation_requeries_current_role_and_never_returns_hidden_child(bool remove, bool revoked)
    {
        var root = new 커뮤니티원장Dto { 원장Id = "order-1", Revision = 3 };
        var raw = new 주문원장통합Dto
        {
            주문원장 = root
        };
        raw.포함원장목록 = [new() { 원장Id = "hidden-1", 역할 = 주문원장포함역할.운송, 필수여부 = true,
            원장 = new() { 원장Id = "hidden-1", 상태 = 커뮤니티원장상태.완료, 제목 = "private-address" } }];
        raw.완료하위원장수 = 1;
        raw.필수하위원장완료여부 = true;
        var useCase = new MutationUseCase(raw);
        var handler = new ReadHandler(root, revoked);
        using var provider = new ServiceCollection()
            .AddSingleton<IRequestHandler<주문자주문원장조회Query, Result<주문원장역할별조회Dto>>>(handler)
            .BuildServiceProvider();
        var controller = new 주문원장Controller(useCase, null!, null!, new Mediator(provider))
        {
            ControllerContext = new() { HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "owner-1")], "test"))
            } }
        };

        var response = remove
            ? await controller.하위원장분리("order-1", "removed-1", 2, default)
            : await controller.하위원장연결("order-1", new() { 하위원장Id = "hidden-1" }, default);

        Assert.Equal(2, handler.Reads);
        Assert.True(useCase.Changed);
        if (revoked)
        {
            var error = Assert.IsAssignableFrom<ObjectResult>(response);
            Assert.Equal(403, error.StatusCode);
            Assert.DoesNotContain("private-address", System.Text.Json.JsonSerializer.Serialize(error.Value));
        }
        else
        {
            var result = Assert.IsType<주문원장통합Dto>(Assert.IsType<OkObjectResult>(response).Value);
            var item = Assert.Single(result.포함원장목록);
            Assert.Null(item.원장);
            Assert.Equal(원장조회근거.공개요청필요, item.조회상태);
            Assert.Equal(0, result.완료하위원장수);
            Assert.False(result.필수하위원장완료여부);
            Assert.Equal("공개요청필요", result.필수하위원장판정);
            Assert.DoesNotContain("private-address", System.Text.Json.JsonSerializer.Serialize(result));
        }
    }

    private sealed class ReadHandler(커뮤니티원장Dto root, bool revoked)
        : IRequestHandler<주문자주문원장조회Query, Result<주문원장역할별조회Dto>>
    {
        public int Reads { get; private set; }
        public Task<Result<주문원장역할별조회Dto>> Handle(주문자주문원장조회Query query, CancellationToken ct)
        {
            Reads++;
            Assert.Equal("owner-1", query.주문자UserId);
            if (revoked && Reads > 1)
                return Task.FromResult(Result.Fail<주문원장역할별조회Dto>(new Error("권한 철회").WithMetadata("StatusCode", 403)));
            return Task.FromResult(Result.Ok(new 주문원장역할별조회Dto
            {
                주문원장Id = root.원장Id, 주문원장상세 = root,
                관련원장목록 = [new() { 원장Id = "hidden-1", 주문안역할 = 주문원장포함역할.운송,
                    필수여부 = true, 조회근거 = 원장조회근거.공개요청필요 }]
            }));
        }
    }

    private sealed class MutationUseCase(주문원장통합Dto value) : I주문원장통합UseCase
    {
        public bool Changed { get; private set; }
        public Task<Result<주문원장통합Dto>> 하위원장연결Async(string id, 주문하위원장연결요청 request, string actor, CancellationToken ct)
        { Changed = true; return Task.FromResult(Result.Ok(value)); }
        public Task<Result<주문원장통합Dto>> 하위원장분리Async(string id, string child, long? revision, string actor, CancellationToken ct)
        { Changed = true; return Task.FromResult(Result.Ok(value)); }
        public Task<Result<주문원장통합Dto>> 조회Async(string id, CancellationToken ct) => throw new NotSupportedException();
        public Task<Result<주문자원장목록응답>> 주문자목록조회Async(string actor, 주문자원장목록조회요청 request, CancellationToken ct)
            => throw new NotSupportedException();
    }
}
