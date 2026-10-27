using System.Text.Json;
using Ssalddel.Ui.Common.Areas.App.ViewModels.Transport;

namespace Ssalddel.Tests.Clients;

public sealed class ShipperSubmissionResultSessionTests
{
    [Theory]
    [InlineData("found", ShipperSubmissionResultKind.Confirmed, 1)]
    [InlineData("missing", ShipperSubmissionResultKind.NotFound, 0)]
    [InlineData("failed", ShipperSubmissionResultKind.Failed, 0)]
    public async Task 접수확인은_한번조회하고_미발견과실패를구분한다(
        string response, ShipperSubmissionResultKind expected, int expectedConfirmations)
    {
        using var session = new ShipperSubmissionResultSession(() => new("owner-a", "revision-1", true), "로그인 필요");
        var reads = 0;
        var confirmations = 0;
        var failure = new IOException("조회 실패");
        var result = await session.ReconcileAsync(session.Capture(), () =>
        {
            reads++;
            return response switch
            {
                "found" => Task.FromResult<Receipt?>(new("request-a")),
                "missing" => Task.FromResult<Receipt?>(null),
                _ => Task.FromException<Receipt?>(failure)
            };
        }, receipt =>
        {
            Assert.Equal("request-a", receipt.RequestId);
            confirmations++;
            return Task.CompletedTask;
        });
        Assert.Equal(expected, result.Kind);
        Assert.Equal(1, reads);
        Assert.Equal(expectedConfirmations, confirmations);
        Assert.Same(response == "failed" ? failure : null, result.Error);
    }

    [Theory]
    [InlineData("owner")]
    [InlineData("session-version")]
    [InlineData("generation")]
    [InlineData("disposed")]
    public async Task 계정이나세션수명이바뀐뒤_늦은조회는정본확인으로넘기지않는다(string change)
    {
        var identity = new ShipperSubmissionIdentity("owner-a", "version-1", true);
        using var session = new ShipperSubmissionResultSession(() => identity, "로그인 필요");
        var context = session.Capture();
        var response = new TaskCompletionSource<Receipt?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var confirmations = 0;
        var checking = session.ReconcileAsync(context, () => response.Task, _ =>
        {
            confirmations++;
            return Task.CompletedTask;
        });
        switch (change)
        {
            case "owner": identity = new("owner-b", "version-1", true); break;
            case "session-version": identity = new("owner-a", "version-2", true); break;
            case "generation": session.Invalidate(); break;
            case "disposed": session.Dispose(); break;
        }
        response.SetResult(new("request-a"));
        var result = await checking;
        Assert.Equal(ShipperSubmissionResultKind.Superseded, result.Kind);
        Assert.Equal(0, confirmations);
        Assert.Null(result.Error);
    }

    [Fact]
    public async Task 이전계정의늦은조회실패는_현재접수의실패로전파하지않는다()
    {
        using var session = new ShipperSubmissionResultSession(() => new("owner-a", "version-1", true), "로그인 필요");
        var response = new TaskCompletionSource<Receipt?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var checking = session.ReconcileAsync(session.Capture(), () => response.Task, _ => throw new InvalidOperationException("확인하면 안 됩니다."));
        session.Invalidate();
        response.SetException(new IOException("이전 조회 실패"));
        var result = await checking;
        Assert.Equal(ShipperSubmissionResultKind.Superseded, result.Kind);
        Assert.Null(result.Error);
    }

    [Fact]
    public async Task 조회성공뒤_정본검증이나보호기록정리실패는_확인완료가아니다()
    {
        using var session = new ShipperSubmissionResultSession(() => new("owner-a", "version-1", true), "로그인 필요");
        var failure = new InvalidDataException("접수 번호 불일치");
        var result = await session.ReconcileAsync(session.Capture(),
            () => Task.FromResult<Receipt?>(new("foreign-request")), _ => Task.FromException(failure));
        Assert.Equal(ShipperSubmissionResultKind.Failed, result.Kind);
        Assert.Same(failure, result.Error);
    }

    [Fact]
    public void 메모리계정비교값은_문자열표현이나JSON에노출하지않는다()
    {
        var identity = new ShipperSubmissionIdentity("owner-a", "token-do-not-log", true);
        using var session = new ShipperSubmissionResultSession(() => identity, "로그인 필요");
        var context = session.Capture();
        var json = new JsonSerializerOptions { IncludeFields = true };
        Assert.DoesNotContain("token-do-not-log", identity.ToString());
        Assert.DoesNotContain("token-do-not-log", context.ToString());
        Assert.DoesNotContain("token-do-not-log", JsonSerializer.Serialize(identity, json));
        Assert.DoesNotContain("token-do-not-log", JsonSerializer.Serialize(context, json));
        Assert.True(session.IsCurrent(context));
        Assert.Equal(new ShipperSubmissionIdentity("owner-a", "token-do-not-log", true), identity);
        Assert.NotEqual(new ShipperSubmissionIdentity("owner-a", "other-token", true), identity);
    }

    private sealed record Receipt(string RequestId);
}
