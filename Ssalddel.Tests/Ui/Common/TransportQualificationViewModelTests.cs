using Ssalddel.Contracts.Driver.Profile;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Qualification;

namespace Ssalddel.Tests.Ui.Common;

public sealed class TransportQualificationViewModelTests
{
    [Fact]
    public async Task 제출은_현재판본을보내고_재조회정본으로만_검토중을표시한다()
    {
        var client = new Client(); using var model = new 운송자격ViewModel(client);
        await model.LoadAsync(); Fill(model); await model.SubmitAsync();
        Assert.Equal(1, client.Writes); Assert.Equal(2, client.Reads);
        Assert.Equal(4, client.Submission!.ExpectedRevision);
        Assert.Equal(운송자격Codes.Pending, model.Current!.Status);
        Assert.False(model.NeedsResultCheck); Assert.True(model.CanEdit);
    }

    [Fact]
    public async Task 응답유실은_자동재전송하지않고_조회로같은자격의최신상태를확인한다()
    {
        var client = new Client { LoseResponse = true }; using var model = new 운송자격ViewModel(client);
        await model.LoadAsync(); Fill(model); await model.SubmitAsync();
        Assert.True(model.NeedsResultCheck); Assert.False(model.CanEdit);
        await model.SubmitAsync(); Assert.Equal(1, client.Writes);
        await model.LoadAsync(); Assert.Equal(1, client.Writes);
        Assert.Equal(5, model.Current!.Revision); Assert.False(model.NeedsResultCheck);
    }

    [Fact]
    public async Task 저장후이전판본조회는_성공으로표시하지않고_계속차단한다()
    {
        var client = new Client { ReturnStale = true }; using var model = new 운송자격ViewModel(client);
        await model.LoadAsync(); Fill(model); await model.SubmitAsync();
        Assert.True(model.NeedsResultCheck); Assert.False(model.CanEdit);
        Assert.NotNull(model.Error); Assert.Equal(1, client.Writes);
    }

    [Fact]
    public async Task 계정변경후_이전자료와행동을폐기한다()
    {
        var identity = "driver-one"; var client = new Client();
        using var model = new 운송자격ViewModel(client, sessionKey: () => identity);
        await model.LoadAsync(); Fill(model); identity = "driver-two"; model.Invalidate();
        Assert.Null(model.Current); Assert.Empty(model.Rows); await model.SubmitAsync();
        Assert.Equal(0, client.Writes);
    }

    [Fact]
    public async Task 승인에는_근거별확인과기간검토및미래재확인일이필요하다()
    {
        var client = new Client(); using var model = new 운송자격ViewModel(client, true);
        await model.LoadAsync(); model.ReviewReason = "차량과 자료를 검토했습니다.";
        model.RecheckDate = DateTime.Today.AddMonths(1).ToString("yyyy-MM-dd");
        await model.ReviewAsync(운송자격Codes.Valid); Assert.Equal(0, client.Writes);
        foreach (var row in model.Rows) { row.Checked = true; row.Summary = "적용 범위와 증빙을 확인"; row.ValidUntilUtc = DateTime.UtcNow.AddMonths(3); }
        await model.ReviewAsync(운송자격Codes.Valid); Assert.Equal(1, client.Writes);
        Assert.All(client.Review!.Evidence, item => Assert.Equal(운송자격Codes.DocumentReview, item.VerificationLevel));
    }

    private static void Fill(운송자격ViewModel model)
    {
        model.VehicleProfileValue = "프로필 차량"; model.ActualVehicleReference = "vehicle:" + Guid.NewGuid();
        foreach (var row in model.Rows) { row.Reference = "private-evidence:" + Guid.NewGuid(); row.Summary = "검토용 자료 설명"; }
    }
    private sealed class Client : I운송자격Client
    {
        private 운송자격상세Dto state = new() { DriverId = "driver", Revision = 4,
            Status = 운송자격Codes.Pending, VehicleKind = 운송자격Codes.Motorcycle, ActualVehicleReference = "vehicle:" + Guid.NewGuid(),
            RequiredEvidenceCodes = 운송자격Codes.RequiredFor(운송자격Codes.Motorcycle) };
        public int Writes { get; private set; }
        public int Reads { get; private set; }
        public bool LoseResponse { get; init; }
        public bool ReturnStale { get; init; }
        public 운송자격제출요청? Submission { get; private set; }
        public 운송자격검토요청? Review { get; private set; }
        public Task<운송자격상세Dto> 조회Async(CancellationToken ct)
        { Reads++; return Task.FromResult(ReturnStale && Reads > 1 ? state with { Revision = 4 } : state); }
        public Task<운송자격상세Dto> 제출Async(운송자격제출요청 request, CancellationToken ct)
        {
            Writes++; Submission = request; state = state with { Revision = 5, Status = 운송자격Codes.Pending, Evidence = request.Evidence };
            if (LoseResponse) throw new HttpRequestException(); return Task.FromResult(state);
        }
        public Task<운송자격상세Dto> 검토Async(운송자격검토요청 request, CancellationToken ct)
        { Writes++; Review = request; state = state with { Revision = 5, Status = request.Decision }; return Task.FromResult(state); }
    }
}
