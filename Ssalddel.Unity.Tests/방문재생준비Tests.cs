using System.Collections;
using System.Globalization;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Unity.Data.WorldProjection;

namespace Ssalddel.Unity.Tests;

[SsalddelEvidenceResponsibility(SsalddelEvidenceStage.E3,
    "합성 방문의 순서·위치 결속·재방문·불변 사본·동일 입력 fingerprint를 검증한다.",
    Boundary = "실제 주소 조사, DB, Unity 수신, Play Mode 및 Game View의 증거가 아니다.",
    SubmoduleKey = SsalddelEvidenceSubmoduleKeys.E3결정성검증)]
public sealed class 방문재생준비Tests
{
    private const string Revision = "synthetic-revision:1";
    private const string RecordId = "synthetic-record:1";
    private const string Area = "synthetic-area:a";
    private const string Frame = "synthetic-frame:enu-v1";

    [Fact]
    public void 순서를정렬하되_원본순서와_재방문을보존한다()
    {
        var a = Visit(1);
        var b = Visit(2);
        var revisit = Visit(3, a.StoreId);
        revisit.Address = a.Address;
        revisit.Menu = "재방문에서 관찰한 메뉴";
        var record = Record(revisit, b, a);
        var result = 방문재생준비.Prepare(record, Revision, new[] { Binding(b), Binding(a), Binding(revisit) });

        Assert.True(result.CanReplay);
        Assert.Empty(result.Diagnostics);
        Assert.Equal(new[] { 1, 2, 3 }, result.Visits.Select(v => v.Sequence));
        Assert.Equal(new[] { 3, 2, 1 }, record.Visits.Select(v => v.Sequence));
        Assert.Equal(result.Visits[0].StoreId, result.Visits[2].StoreId);
        Assert.NotEqual(result.Visits[0].VisitId, result.Visits[2].VisitId);
        Assert.Equal(revisit.Menu, result.Visits[2].Menu);
    }

    [Fact]
    public void 같은입력과_다른열거순서에서도_같은불변결과를만든다()
    {
        var a = Visit(1); var b = Visit(2);
        var record = Record(a, b);
        var first = 방문재생준비.Prepare(record, Revision, new[] { Binding(b), Binding(a) });
        var second = 방문재생준비.Prepare(Record(b, a), Revision, new[] { Binding(a), Binding(b) });
        Assert.True(first.CanReplay);
        Assert.Equal(first.InputFingerprint, second.InputFingerprint);
        Assert.Equal(64, first.InputFingerprint.Length);
        var fingerprint = first.InputFingerprint;
        a.Menu = "바뀐 입력";
        a.SourceUrls[0] = "synthetic-source:changed";
        record.Visits[0] = Visit(8);
        Assert.Equal("관찰 메뉴", first.Visits[0].Menu);
        Assert.Equal(fingerprint, first.InputFingerprint);
        Assert.Throws<NotSupportedException>(() => { ((IList)first.Visits)[0] = first.Visits[1]; });
        Assert.Throws<NotSupportedException>(() => { ((IList)first.Diagnostics).Add("changed"); });
        Assert.All(typeof(준비된방문).GetProperties(), property => Assert.Null(property.SetMethod));
    }

    [Fact]
    public void 메뉴와판본과근거변경은_fingerprint에반영한다()
    {
        var visit = Visit(1);
        var record = Record(visit);
        var first = 방문재생준비.Prepare(record, Revision, new[] { Binding(visit) });
        visit.Menu = "다른 메뉴";
        var menuChanged = 방문재생준비.Prepare(record, Revision, new[] { Binding(visit) });
        var evidenceChanged = 방문재생준비.Prepare(record, Revision,
            new[] { Binding(visit, evidence: "synthetic-evidence:2") });
        var revisionChanged = 방문재생준비.Prepare(record, "revision:2",
            new[] { Binding(visit, revision: "revision:2") });
        Assert.NotEqual(first.InputFingerprint, menuChanged.InputFingerprint);
        Assert.NotEqual(menuChanged.InputFingerprint, evidenceChanged.InputFingerprint);
        Assert.NotEqual(menuChanged.InputFingerprint, revisionChanged.InputFingerprint);
    }

    [Fact]
    public void 현재문화권은_fingerprint에영향을주지않는다()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            var visit = Visit(1); var record = Record(visit); var receipt = Binding(visit, x: 1.25);
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ko-KR");
            var first = 방문재생준비.Prepare(record, Revision, new[] { receipt });
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            var second = 방문재생준비.Prepare(record, Revision, new[] { receipt });
            Assert.Equal(first.InputFingerprint, second.InputFingerprint);
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Fact]
    public void 문자열Verified만으로재생을허용하지않고_중간방문을건너뛰지않는다()
    {
        var a = Visit(1); var b = Visit(2); var c = Visit(3);
        var result = 방문재생준비.Prepare(Record(a, b, c), Revision, new[] { Binding(a), Binding(c) });
        Assert.False(result.CanReplay);
        Assert.Empty(result.Visits);
        Assert.Contains("VisitLocationBindingMissing:" + b.VisitId, result.Diagnostics);
    }

    [Theory]
    [InlineData(-1, "VisitSequenceInvalid")]
    [InlineData(0, "VisitSequenceInvalid")]
    [InlineData(1, "DuplicateVisitSequence")]
    [InlineData(3, "VisitSequenceMissing")]
    public void 순서의음수영중복과누락은보류한다(int secondSequence, string diagnostic)
    {
        var a = Visit(1); var b = Visit(2); b.Sequence = secondSequence;
        var result = 방문재생준비.Prepare(Record(a, b), Revision, new[] { Binding(a), Binding(b) });
        Assert.False(result.CanReplay);
        Assert.Empty(result.Visits);
        Assert.Contains(result.Diagnostics, value => value.StartsWith(diagnostic, StringComparison.Ordinal));
    }

    [Fact]
    public void 중복방문과중복근거를거부한다()
    {
        var a = Visit(1); var b = Visit(2); b.VisitId = a.VisitId;
        var duplicateVisit = 방문재생준비.Prepare(Record(a, b), Revision, new[] { Binding(a) });
        Assert.Contains("DuplicateVisitId:" + a.VisitId, duplicateVisit.Diagnostics);
        var duplicateReceipt = 방문재생준비.Prepare(Record(a), Revision, new[] { Binding(a), Binding(a) });
        Assert.Contains("DuplicateVisitLocationBinding:" + a.VisitId, duplicateReceipt.Diagnostics);
        Assert.False(duplicateReceipt.CanReplay);
    }

    [Theory]
    [InlineData("record")]
    [InlineData("revision")]
    [InlineData("store")]
    [InlineData("address")]
    [InlineData("latitude")]
    [InlineData("longitude")]
    [InlineData("area")]
    public void 기록과위치근거의의미필드가달라지면보류한다(string field)
    {
        var visit = Visit(1);
        var receipt = Binding(visit,
            recordId: field == "record" ? "other-record" : RecordId,
            revision: field == "revision" ? "other-revision" : Revision,
            store: field == "store" ? "other-store" : visit.StoreId,
            address: field == "address" ? "other-public-address" : visit.Address,
            latitude: field == "latitude" ? 38 : visit.Latitude,
            longitude: field == "longitude" ? 128 : visit.Longitude,
            area: field == "area" ? "other-area" : Area);
        var result = 방문재생준비.Prepare(Record(visit), Revision, new[] { receipt });
        Assert.False(result.CanReplay);
        Assert.Contains("VisitLocationBindingMismatch:" + visit.VisitId, result.Diagnostics);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(91)]
    [InlineData(0)]
    public void 누락된TypedDto기본값이나유효하지않은좌표는근거가같아도보류한다(double latitude)
    {
        var visit = Visit(1); visit.Latitude = latitude;
        var result = 방문재생준비.Prepare(Record(visit), Revision, new[] { Binding(visit) });
        Assert.False(result.CanReplay);
        Assert.Contains("VisitCoordinatesMissingOrInvalid:" + visit.VisitId, result.Diagnostics);
    }

    [Fact]
    public void 좌표Missing표시와지역미결속은보류하고_메뉴미확인은허용한다()
    {
        var visit = Visit(1); visit.CoordinateStatus = "Missing";
        Assert.False(방문재생준비.Prepare(Record(visit), Revision, new[] { Binding(visit) }).CanReplay);
        visit.CoordinateStatus = "Verified"; visit.AreaBindingStatus = "Unresolved";
        Assert.False(방문재생준비.Prepare(Record(visit), Revision, new[] { Binding(visit) }).CanReplay);
        visit.AreaBindingStatus = "Verified"; visit.Menu = ""; visit.MenuStatus = "unverified";
        var result = 방문재생준비.Prepare(Record(visit), Revision, new[] { Binding(visit) });
        Assert.True(result.CanReplay);
        Assert.Empty(result.Visits[0].Menu);
    }

    [Fact]
    public void 좌표계혼합과근거누락과비유한공통좌표는보류한다()
    {
        var a = Visit(1); var b = Visit(2);
        var mixed = 방문재생준비.Prepare(Record(a, b), Revision,
            new[] { Binding(a), Binding(b, frame: "other-frame") });
        Assert.Contains("CoordinateFrameMismatch", mixed.Diagnostics);
        Assert.False(mixed.CanReplay);
        foreach (var badReceipt in new[] { Binding(a, evidence: ""), Binding(a, coordinateRevision: ""),
                     Binding(a, boundaryRevision: ""), Binding(a, x: double.NaN), Binding(a, frame: "") })
            Assert.False(방문재생준비.Prepare(Record(a), Revision, new[] { badReceipt }).CanReplay);
    }

    [Fact]
    public void 같은매장을다른공통위치로재사용하지않는다()
    {
        var a = Visit(1); var b = Visit(2, a.StoreId); b.Address = a.Address;
        var result = 방문재생준비.Prepare(Record(a, b), Revision,
            new[] { Binding(a), Binding(b, x: 90) });
        Assert.False(result.CanReplay);
        Assert.Contains("StoreLocationConflict:" + a.StoreId, result.Diagnostics);
    }

    [Fact]
    public void 운영사본과픽업완료와빈기록을거부한다()
    {
        var visit = Visit(1); var record = Record(visit); record.IsOperationalState = true;
        Assert.False(방문재생준비.Prepare(record, Revision, new[] { Binding(visit) }).CanReplay);
        record.IsOperationalState = false; visit.PickupCompletionConfirmed = true;
        Assert.False(방문재생준비.Prepare(record, Revision, new[] { Binding(visit) }).CanReplay);
        Assert.False(방문재생준비.Prepare(Record(), Revision, Array.Empty<방문위치결속>()).CanReplay);
    }

    private static RegionalPickupVisit Visit(int sequence, string? store = null) => new()
    {
        VisitId = "synthetic-visit:" + sequence, StoreId = store ?? "synthetic-store:" + sequence,
        Sequence = sequence, Name = "합성 가게", Menu = "관찰 메뉴", MenuStatus = "unverified",
        Address = "합성 공개주소 " + sequence, Latitude = 37.5, Longitude = 127.1,
        CoordinateStatus = "Verified", AddressStatus = "Verified", AreaBindingStatus = "Verified",
        AreaBindingEvidence = "synthetic-area-evidence", AdministrativeAreaStableId = Area,
        SourceUrls = new[] { "synthetic-source:1" }
    };

    private static RegionalPickupRecord Record(params RegionalPickupVisit[] visits) => new()
    {
        SchemaVersion = "delivery-visit-record.v2", RecordId = RecordId,
        LocalReviewOnly = true, Visits = visits
    };

    private static 방문위치결속 Binding(RegionalPickupVisit visit, string recordId = RecordId,
        string revision = Revision, string? store = null, string? address = null,
        double? latitude = null, double? longitude = null, string area = Area,
        string frame = Frame, double x = 10, double z = 20, string coordinateRevision = "synthetic-location:v1",
        string boundaryRevision = "synthetic-boundary:v1", string evidence = "synthetic-evidence:1")
        => new(recordId, revision, visit.VisitId, store ?? visit.StoreId, address ?? visit.Address,
            latitude ?? visit.Latitude, longitude ?? visit.Longitude, area, frame, x, z,
            coordinateRevision, boundaryRevision, evidence);
}
