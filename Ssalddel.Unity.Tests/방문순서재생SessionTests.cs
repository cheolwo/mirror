using System.Text.Json;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Unity.Data.WorldProjection;

namespace Ssalddel.Unity.Tests;

[SsalddelEvidenceResponsibility(SsalddelEvidenceStage.E3,
    "순서 방문의 연출 시간·정지·선택·배속·끝과 입력 재전달 안정성을 가짜 시계로 검증한다.",
    Boundary = "실제 Unity 화면·GPS·업무 완료·DB·전송 시험이 아니다.",
    SubmoduleKey = SsalddelEvidenceSubmoduleKeys.E3결정성검증)]
public sealed class 방문순서재생SessionTests
{
    private const string Area = "region:kr:hjd:1126057500";

    private static 방문재생준비Result 준비(string revision = "r1", bool samePosition = false,
        int count = 3, string menu = "합성 메뉴")
    {
        var record = new RegionalPickupRecord
        {
            SchemaVersion = "delivery-visit-record.v2", RecordId = "test-record",
            LocalReviewOnly = true, IsOperationalState = false,
            Visits = Enumerable.Range(1, count).Select(i => new RegionalPickupVisit
            {
                VisitId = "v" + i, StoreId = samePosition ? "store1" : "store" + i,
                Sequence = i, Name = "합성 가게", Menu = menu, Address = "합성 위치 " + (samePosition ? 1 : i),
                Latitude = 37.5 + (samePosition ? 1 : i) * .001, Longitude = 127.05,
                AdministrativeAreaStableId = Area, AreaBindingStatus = "Verified",
                AreaBindingEvidence = "fixture-boundary", AddressStatus = "Verified",
                CoordinateStatus = "Verified", VerificationNote = "Synthetic fixture only",
                SourceUrls = new[] { "https://example.invalid/fixture" },
                EventKind = "RecordedVisit", PickupCompletionConfirmed = false
            }).Reverse().ToArray()
        };
        var bindings = record.Visits.Select(v => new 방문위치결속(
            record.RecordId, revision, v.VisitId, v.StoreId, v.Address,
            v.Latitude, v.Longitude, Area, "fixture-enu-frame",
            samePosition ? 0 : (v.Sequence - 1) * 100, 0,
            "fixture-coordinate-r1", "fixture-boundary-r1", "fixture-location-evidence")).ToArray();
        // 실제 소비 순서인 v2 JSON → decoder → 준비 → Session을 같은 fixture로 연결합니다.
        var decoded = new 방문기록JsonDecoder().Decode(JsonSerializer.Serialize(record,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
        var result = 방문재생준비.Prepare(decoded, revision, bindings);
        Assert.True(result.CanReplay, string.Join(",", result.Diagnostics));
        return result;
    }

    private static 방문순서재생Session 실행()
    {
        var session = new 방문순서재생Session();
        Assert.True(session.Load(준비(), 0));
        Assert.True(session.Play(0));
        return session;
    }

    [Fact]
    public void 준비만으로는자동재생하지않고_정차이동도착끝을구분한다()
    {
        var session = new 방문순서재생Session();
        session.Load(준비(), 0);
        session.Advance(10);
        Assert.Equal(방문재생상태.Ready, session.State);
        Assert.Equal("v1", session.CurrentFrame.VisitId);
        Assert.Equal(0, session.CurrentFrame.X);
        session.Play(10);
        session.Advance(14);
        Assert.True(session.CurrentFrame.IsMoving);
        Assert.Equal("v2", session.CurrentFrame.NextVisitId);
        Assert.Equal(50, session.CurrentFrame.X);
        session.Advance(16);
        Assert.Equal("v2", session.CurrentFrame.VisitId);
        Assert.False(session.CurrentFrame.IsMoving);
        session.Advance(24);
        Assert.Equal(방문재생상태.Ended, session.State);
        Assert.Equal("v3", session.CurrentFrame.VisitId);
        Assert.Equal(200, session.CurrentFrame.X);
        Assert.Equal(14, session.DurationSeconds);
    }

    [Fact]
    public void 정지중시간은재생에더하지않는다()
    {
        var session = 실행();
        session.Pause(3);
        Assert.Equal(25, session.CurrentFrame.X);
        Assert.False(session.CurrentFrame.IsMoving);
        Assert.Equal("v2", session.CurrentFrame.NextVisitId);
        session.Advance(100);
        Assert.Equal(25, session.CurrentFrame.X);
        session.Play(100);
        session.Advance(101);
        Assert.Equal(50, session.CurrentFrame.X);
    }

    [Fact]
    public void 배속변경전시간은이전배속으로계산한다()
    {
        var session = 실행();
        Assert.True(session.SetPlaybackRate(2, 3));
        Assert.Equal(25, session.CurrentFrame.X);
        session.Advance(4);
        Assert.Equal(75, session.CurrentFrame.X);
        Assert.False(session.SetPlaybackRate(0, 5));
        Assert.Equal(75, session.CurrentFrame.X);
        Assert.Equal(2, session.PlaybackRate);
    }

    [Fact]
    public void 번호선택은해당방문에서정지하며끝에서돌아올수있다()
    {
        var session = 실행();
        session.Advance(100);
        Assert.Equal(방문재생상태.Ended, session.State);
        session.Play(100);
        Assert.Equal(방문재생상태.Ended, session.State);
        Assert.True(session.SeekVisit("v2", 101));
        Assert.Equal(방문재생상태.Paused, session.State);
        Assert.Equal(100, session.CurrentFrame.X);
        session.Play(101);
        session.Advance(105);
        Assert.Equal(150, session.CurrentFrame.X);
    }

    [Fact]
    public void 같은판본재전달은재생을초기화하지않고새판본은준비에서시작한다()
    {
        var session = 실행();
        session.Advance(3);
        Assert.True(session.Load(준비(), 4));
        Assert.Equal(방문재생상태.Playing, session.State);
        Assert.Equal(50, session.CurrentFrame.X);
        Assert.True(session.Load(준비("r2"), 5));
        Assert.Equal(방문재생상태.Ready, session.State);
        Assert.Equal("r2", session.RecordRevision);
        Assert.Equal(0, session.CurrentFrame.PresentationSeconds);
    }

    [Fact]
    public void 준비실패와잘못된선택은기존사본을보존한다()
    {
        var session = 실행();
        session.Advance(4);
        var fingerprint = session.InputFingerprint;
        Assert.False(session.Load(null!, 5));
        Assert.False(session.SeekVisit("missing", 5));
        Assert.Equal(50, session.CurrentFrame.X);
        Assert.Equal(fingerprint, session.InputFingerprint);
        Assert.Equal(방문재생상태.Playing, session.State);
    }

    [Fact]
    public void 같은판본의다른내용은충돌로거절하고기존진행을보존한다()
    {
        var session = 실행();
        session.Advance(4);
        var fingerprint = session.InputFingerprint;
        Assert.False(session.Load(준비(menu: "변경된 메뉴"), 5));
        Assert.Equal("VisitPlaybackRevisionConflict", session.DiagnosticCode);
        Assert.Equal(fingerprint, session.InputFingerprint);
        Assert.Equal(50, session.CurrentFrame.X);
        Assert.Equal(방문재생상태.Playing, session.State);
        Assert.True(session.Load(준비("r2", menu: "변경된 메뉴"), 5));
        Assert.Equal(방문재생상태.Ready, session.State);
    }

    [Fact]
    public void 소수연출시간의선택과종료는반올림으로이전방문에남지않는다()
    {
        var session = new 방문순서재생Session(0, .7);
        session.Load(준비(count: 4), 0);
        Assert.False(session.CurrentFrame.IsMoving);
        session.SeekVisit("v4", 0);
        Assert.Equal("v4", session.CurrentFrame.VisitId);
        Assert.Equal(300, session.CurrentFrame.X);
        session.SeekVisit("v1", 0);
        session.Play(0);
        session.Advance(session.DurationSeconds);
        Assert.Equal(방문재생상태.Ended, session.State);
        Assert.Equal("v4", session.CurrentFrame.VisitId);
        Assert.Equal(300, session.CurrentFrame.X);
        Assert.False(session.CurrentFrame.IsMoving);
        Assert.Equal(string.Empty, session.CurrentFrame.NextVisitId);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(-1)]
    [InlineData(2)]
    public void 비정상또는역순시계는위치를바꾸지않는다(double seconds)
    {
        var session = 실행();
        session.Advance(4);
        Assert.False(session.Advance(seconds));
        Assert.Equal(50, session.CurrentFrame.X);
    }

    [Fact]
    public void 프레임분할과무관하게같은최종시각은같은표현위치다()
    {
        var first = 실행();
        var second = 실행();
        for (var i = 1; i <= 99; i++) first.Advance(i / 10d);
        second.Advance(9.9);
        Assert.Equal(first.CurrentFrame.X, second.CurrentFrame.X);
        Assert.Equal(first.CurrentFrame.VisitId, second.CurrentFrame.VisitId);
        Assert.Equal(first.CurrentFrame.PresentationSeconds, second.CurrentFrame.PresentationSeconds);
    }

    [Fact]
    public void 같은가게의0거리재방문도순서가남는다()
    {
        var session = new 방문순서재생Session();
        Assert.True(session.Load(준비(samePosition: true), 0));
        session.Play(0);
        session.Advance(6);
        Assert.Equal("v2", session.CurrentFrame.VisitId);
        Assert.Equal(0, session.CurrentFrame.X);
        Assert.Equal(3, session.Visits.Count);
    }

    [Fact]
    public void 한방문과정차0초도종료되며Clear는사본을비운다()
    {
        var session = new 방문순서재생Session(0, 4);
        Assert.True(session.Load(준비(count: 1), 0));
        session.Play(0);
        session.Advance(0);
        Assert.Equal(방문재생상태.Ended, session.State);
        Assert.Equal("v1", session.CurrentFrame.VisitId);
        session.Clear();
        Assert.Equal(방문재생상태.Empty, session.State);
        Assert.Empty(session.Visits);
        Assert.Equal(string.Empty, session.RecordId);
        Assert.Equal(string.Empty, session.CurrentFrame.VisitId);
        Assert.False(session.Play(1));
        Assert.True(session.Load(준비(), 0));
    }

    [Fact]
    public void 매우큰경과시간은끝에고정되고잘못된연출시간은거절한다()
    {
        var session = 실행();
        session.SetPlaybackRate(16, 0);
        Assert.True(session.Advance(double.MaxValue));
        Assert.Equal(방문재생상태.Ended, session.State);
        Assert.Equal(200, session.CurrentFrame.X);
        Assert.Throws<ArgumentException>(() => new 방문순서재생Session(-1, 4));
        Assert.Throws<ArgumentException>(() => new 방문순서재생Session(1, 0));
        Assert.Throws<ArgumentException>(() => new 방문순서재생Session(double.MaxValue, double.MaxValue));
    }
}
