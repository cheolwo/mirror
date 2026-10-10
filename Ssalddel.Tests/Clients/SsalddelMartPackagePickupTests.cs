using System.Text.Json;
using RoleWorkspacePreview;

namespace Ssalddel.Tests.Clients;

public sealed class SsalddelMartPackagePickupTests
{
    [Fact]
    public async Task 전표는포장전에발급하지만출력만으로피킹포장픽업이완료되지않는다()
    {
        await using var runtime = Runtime(); await runtime.InitializeAsync();
        var orderId = runtime.Orders[0].SampleOrderId;
        Assert.True(await runtime.IssueSlipAsync(orderId), runtime.Error);
        var slip = runtime.Orders[0].Slip!;
        Assert.Matches("^SAMPLE-PKG-[0-9a-f]{32}-1$", slip.QrCode);
        Assert.Matches("^[0-9]{4}$", slip.ConfirmationCode);
        Assert.True(slip.Active); Assert.False(slip.PickedUp); Assert.Null(slip.PickupAtUtc);
        Assert.True(await runtime.RecordPrintRequestAsync(orderId, slip.LabelId, slip.Revision));
        Assert.True(await runtime.RecordPrintRequestAsync(orderId, slip.LabelId, slip.Revision));
        Assert.True(await runtime.IssueSlipAsync(orderId));
        var current = runtime.Orders[0];
        Assert.Equal(slip.LabelId, current.Slip!.LabelId); Assert.Equal(slip.Revision, current.Slip.Revision);
        Assert.Equal(2, current.Slip.PrintRequestCount);
        Assert.False(current.AllPicked); Assert.False(current.AllPacked); Assert.False(current.MockHandedOff);
        Assert.Equal(0, runtime.PickupCount); Assert.Equal(0, runtime.AuditCount); Assert.Equal(0, runtime.EventCount);
    }

    [Fact]
    public async Task 전품목피킹후에도전표없는포장은차단한다()
    {
        await using var runtime = Runtime(); await runtime.InitializeAsync();
        await PickAsync(runtime, runtime.Orders[0].SampleOrderId);
        Assert.False(await runtime.PackOrderAsync(runtime.Orders[0].SampleOrderId, true, true));
        Assert.All(runtime.Orders[0].Lines, line => Assert.False(line.Packed));
        Assert.True(await runtime.IssueSlipAsync(runtime.Orders[0].SampleOrderId));
        Assert.True(await runtime.PackOrderAsync(runtime.Orders[0].SampleOrderId, true, true), runtime.Error);
        Assert.Equal("픽업 대기", runtime.Orders[0].Stage);
    }

    [Fact]
    public async Task 유효QR요청한번으로픽업완료를저장하고운영Event나재고차감을추가하지않는다()
    {
        await using var runtime = Runtime(); await runtime.InitializeAsync();
        var orderId = runtime.Orders[0].SampleOrderId;
        await PrepareAsync(runtime, orderId);
        var slip = runtime.Orders[0].Slip!;
        var counts = (runtime.AuditCount, runtime.EventCount);
        Assert.True(await QrAsync(runtime, orderId, slip), runtime.Error);
        Assert.True(runtime.Orders[0].Slip!.PickedUp); Assert.True(runtime.Orders[0].MockHandedOff);
        Assert.NotNull(runtime.Orders[0].Slip!.PickupAtUtc); Assert.Equal("표본 픽업 완료", runtime.Orders[0].Stage);
        Assert.Equal(1, runtime.PickupCount); Assert.Equal(counts, (runtime.AuditCount, runtime.EventCount));
        // 다른 명령이 읽는 DB 사본에서도 같은 인수가 유지됩니다.
        Assert.True(await runtime.IssueSlipAsync(orderId));
        Assert.Equal(1, runtime.PickupCount); Assert.True(runtime.Orders[0].Slip!.PickedUp);
        Assert.False(runtime.Orders[1].MockHandedOff); Assert.Null(runtime.Orders[1].Slip);
    }

    [Fact]
    public async Task 네자리제출은선택주문만픽업하고추가완료명령은필요없다()
    {
        await using var runtime = Runtime(); await runtime.InitializeAsync();
        var orderId = runtime.Orders[0].SampleOrderId;
        await PrepareAsync(runtime, orderId);
        var slip = runtime.Orders[0].Slip!;
        Assert.True(await NumberAsync(runtime, orderId, slip.ConfirmationCode, slip.Revision), runtime.Error);
        Assert.True(runtime.Orders[0].Slip!.PickedUp); Assert.Equal(1, runtime.PickupCount);
        Assert.False(runtime.Orders[1].MockHandedOff);
    }

    [Theory]
    [InlineData("미포장")]
    [InlineData("타기사")]
    [InlineData("타주문")]
    [InlineData("낮은판본")]
    [InlineData("높은판본")]
    public async Task 준비배정주문판본이맞지않으면인수하지않는다(string scenario)
    {
        await using var runtime = Runtime(); await runtime.InitializeAsync();
        var orderId = runtime.Orders[0].SampleOrderId;
        Assert.True(await runtime.IssueSlipAsync(orderId));
        if (scenario != "미포장") await PrepareAsync(runtime, orderId);
        var slip = runtime.Orders[0].Slip!;
        var targetOrder = scenario == "타주문" ? runtime.Orders[1].SampleOrderId : orderId;
        if (scenario == "타주문") await PrepareAsync(runtime, targetOrder);
        Assert.False(await runtime.PickupByQrAsync(targetOrder, slip.QrCode, Request(),
            scenario == "낮은판본" ? slip.Revision - 1 : scenario == "높은판본" ? slip.Revision + 1 : slip.Revision,
            scenario == "타기사" ? "other-synthetic-driver" : SsalddelMartSampleRuntime.SampleDriverId));
        Assert.Equal(0, runtime.PickupCount); Assert.All(runtime.Orders, row => Assert.False(row.MockHandedOff));
    }

    [Theory]
    [InlineData("")]
    [InlineData("SAMPLE-RACK-001")]
    [InlineData("https://example.invalid/secret")]
    [InlineData("SAMPLE-PKG-11111111111111111111111111111111-1")]
    [InlineData("SAMPLE-PKG-11111111111111111111111111111111-0")]
    [InlineData("SAMPLE-PKG-11111111111111111111111111111111-1234567")]
    [InlineData("<script>private-scan</script>")]
    public async Task 다른코드나URL은오류원문노출없이차단한다(string raw)
    {
        await using var runtime = Runtime(); await runtime.InitializeAsync();
        await PrepareAsync(runtime, runtime.Orders[0].SampleOrderId);
        var order = runtime.Orders[0];
        Assert.False(await runtime.PickupByQrAsync(order.SampleOrderId, raw, Request(), order.Slip!.Revision,
            SsalddelMartSampleRuntime.SampleDriverId));
        if (raw.Length > 0) Assert.DoesNotContain(raw, runtime.Error!);
        Assert.Equal(0, runtime.PickupCount); Assert.False(runtime.Orders[0].MockHandedOff);
    }

    [Fact]
    public async Task 미발급전표와잘못된요청형식은실패한다()
    {
        await using var runtime = Runtime(); await runtime.InitializeAsync();
        var orderId = runtime.Orders[0].SampleOrderId;
        Assert.False(await runtime.PickupByQrAsync(orderId, "SAMPLE-PKG-11111111111111111111111111111111-1", Request(), 1,
            SsalddelMartSampleRuntime.SampleDriverId));
        await PrepareAsync(runtime, orderId);
        var slip = runtime.Orders[0].Slip!;
        Assert.False(await runtime.PickupByQrAsync(orderId, slip.QrCode, "not-a-request", slip.Revision, SsalddelMartSampleRuntime.SampleDriverId));
        Assert.False(await runtime.PickupByQrAsync(orderId, slip.QrCode, Guid.Empty.ToString(), slip.Revision, SsalddelMartSampleRuntime.SampleDriverId));
        Assert.False(await runtime.PickupByQrAsync(orderId, new string('x', 81), Request(), slip.Revision, SsalddelMartSampleRuntime.SampleDriverId));
        Assert.False(await runtime.PickupByQrAsync(orderId, slip.QrCode, Request(), slip.Revision, ""));
        Assert.False(await runtime.PickupByQrAsync("unknown-order", slip.QrCode, Request(), slip.Revision, SsalddelMartSampleRuntime.SampleDriverId));
        Assert.Equal(0, runtime.PickupCount);
    }

    [Fact]
    public async Task 재발급은구전표를무효화하며현재전표만인수한다()
    {
        await using var runtime = Runtime(); await runtime.InitializeAsync();
        var orderId = runtime.Orders[0].SampleOrderId;
        Assert.True(await runtime.IssueSlipAsync(orderId));
        var oldSlip = runtime.Orders[0].Slip!;
        Assert.True(await runtime.RecordPrintRequestAsync(orderId, oldSlip.LabelId, oldSlip.Revision));
        Assert.True(await runtime.ReissueSlipAsync(orderId));
        var current = runtime.Orders[0].Slip!;
        Assert.Equal(oldSlip.Revision + 1, current.Revision); Assert.NotEqual(oldSlip.LabelId, current.LabelId);
        Assert.NotEqual(oldSlip.ConfirmationCode, current.ConfirmationCode);
        Assert.NotEqual(oldSlip.QrCode, current.QrCode); Assert.Equal(0, current.PrintRequestCount);
        Assert.False(await runtime.RecordPrintRequestAsync(orderId, oldSlip.LabelId, oldSlip.Revision));
        await PrepareAsync(runtime, orderId);
        Assert.False(await QrAsync(runtime, orderId, oldSlip));
        Assert.False(await runtime.PickupByQrAsync(orderId, oldSlip.QrCode, Request(), current.Revision,
            SsalddelMartSampleRuntime.SampleDriverId));
        Assert.True(await QrAsync(runtime, orderId, current)); Assert.Equal(1, runtime.PickupCount);
    }

    [Fact]
    public async Task 포장후에는재발급할수없고단순재출력은같은판본을유지한다()
    {
        await using var runtime = Runtime(); await runtime.InitializeAsync();
        await PrepareAsync(runtime, runtime.Orders[0].SampleOrderId);
        var order = runtime.Orders[0]; var slip = order.Slip!;
        Assert.False(await runtime.ReissueSlipAsync(order.SampleOrderId));
        Assert.True(await runtime.RecordPrintRequestAsync(order.SampleOrderId, slip.LabelId, slip.Revision));
        Assert.Equal(slip.LabelId, runtime.Orders[0].Slip!.LabelId); Assert.Equal(slip.Revision, runtime.Orders[0].Slip!.Revision);
        Assert.Equal(1, runtime.Orders[0].Slip!.PrintRequestCount); Assert.Equal(0, runtime.PickupCount);
    }

    [Fact]
    public async Task 같은요청재처리와새요청중복스캔은한개의인수만남긴다()
    {
        await using var runtime = Runtime(); await runtime.InitializeAsync();
        await PrepareAsync(runtime, runtime.Orders[0].SampleOrderId);
        var order = runtime.Orders[0]; var slip = order.Slip!; var request = Request();
        Assert.True(await QrAsync(runtime, order.SampleOrderId, slip, request));
        var confirmedAt = runtime.Orders[0].Slip!.PickupAtUtc;
        Assert.True(await QrAsync(runtime, order.SampleOrderId, slip, request));
        Assert.True(await QrAsync(runtime, order.SampleOrderId, slip));
        Assert.True(await NumberAsync(runtime, order.SampleOrderId, slip.ConfirmationCode, slip.Revision));
        Assert.Equal(1, runtime.PickupCount); Assert.Equal(confirmedAt, runtime.Orders[0].Slip!.PickupAtUtc);
    }

    [Fact]
    public async Task 같은요청번호에다른입력이나기사나방식을넣으면거부한다()
    {
        await using var runtime = Runtime(); await runtime.InitializeAsync();
        await PrepareAsync(runtime, runtime.Orders[0].SampleOrderId);
        var order = runtime.Orders[0]; var slip = order.Slip!; var request = Request();
        Assert.True(await QrAsync(runtime, order.SampleOrderId, slip, request));
        Assert.False(await runtime.PickupByQrAsync(order.SampleOrderId, slip.QrCode + "x", request, slip.Revision, SsalddelMartSampleRuntime.SampleDriverId));
        Assert.False(await runtime.PickupByQrAsync(order.SampleOrderId, slip.QrCode, request, slip.Revision, "other"));
        Assert.False(await runtime.PickupByNumberAsync(order.SampleOrderId, slip.ConfirmationCode, request, slip.Revision, SsalddelMartSampleRuntime.SampleDriverId));
        Assert.Equal(1, runtime.PickupCount);
    }

    [Fact]
    public async Task 미준비실패요청은준비뒤에도같은실패를돌려주고새스캔으로이어간다()
    {
        await using var runtime = Runtime(); await runtime.InitializeAsync();
        var orderId = runtime.Orders[0].SampleOrderId;
        Assert.True(await runtime.IssueSlipAsync(orderId));
        var slip = runtime.Orders[0].Slip!; var request = Request();
        Assert.False(await QrAsync(runtime, orderId, slip, request));
        await PrepareAsync(runtime, orderId);
        Assert.False(await QrAsync(runtime, orderId, slip, request)); Assert.Equal(0, runtime.PickupCount);
        Assert.True(await QrAsync(runtime, orderId, slip)); Assert.Equal(1, runtime.PickupCount);
    }

    [Fact]
    public async Task 동시스캔은직렬검증하고인수기록이누적되지않는다()
    {
        await using var runtime = Runtime(); await runtime.InitializeAsync();
        await PrepareAsync(runtime, runtime.Orders[0].SampleOrderId);
        var order = runtime.Orders[0]; var slip = order.Slip!;
        var results = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => QrAsync(runtime, order.SampleOrderId, slip)));
        Assert.All(results, result => Assert.True(result)); Assert.Equal(1, runtime.PickupCount);
    }

    [Fact]
    public async Task 틀린번호다섯번은번호입력을육십초제한하며시간후정상번호를허용한다()
    {
        var clock = new SampleClock();
        await using var runtime = Runtime(clock); await runtime.InitializeAsync();
        await PrepareAsync(runtime, runtime.Orders[0].SampleOrderId);
        var order = runtime.Orders[0]; var slip = order.Slip!;
        var wrong = slip.ConfirmationCode == "0000" ? "0001" : "0000";
        for (var index = 0; index < 5; index++) Assert.False(await NumberAsync(runtime, order.SampleOrderId, wrong, slip.Revision));
        Assert.False(await NumberAsync(runtime, order.SampleOrderId, slip.ConfirmationCode, slip.Revision));
        Assert.Equal(0, runtime.PickupCount); Assert.Contains("잠시", runtime.Error!);
        clock.Advance(TimeSpan.FromSeconds(60));
        Assert.True(await NumberAsync(runtime, order.SampleOrderId, slip.ConfirmationCode, slip.Revision));
        Assert.Equal(clock.GetUtcNow(), runtime.Orders[0].Slip!.PickupAtUtc); Assert.Equal(1, runtime.PickupCount);
    }

    [Fact]
    public async Task 번호제한중에도QR은사용할수있고실패요청재전송은시도수를늘리지않는다()
    {
        await using var runtime = Runtime(); await runtime.InitializeAsync();
        await PrepareAsync(runtime, runtime.Orders[0].SampleOrderId);
        var order = runtime.Orders[0]; var slip = order.Slip!; var request = Request();
        var wrong = slip.ConfirmationCode == "0000" ? "0001" : "0000";
        for (var index = 0; index < 8; index++)
            Assert.False(await runtime.PickupByNumberAsync(order.SampleOrderId, wrong, request, slip.Revision, SsalddelMartSampleRuntime.SampleDriverId));
        // 같은 요청 8회는 실패 시도 하나이며 네 번의 다른 시도가 더해져야 제한됩니다.
        for (var index = 0; index < 4; index++) Assert.False(await NumberAsync(runtime, order.SampleOrderId, wrong, slip.Revision));
        Assert.False(await NumberAsync(runtime, order.SampleOrderId, slip.ConfirmationCode, slip.Revision));
        Assert.Contains("잠시", runtime.Error!);
        Assert.True(await QrAsync(runtime, order.SampleOrderId, slip)); Assert.Equal(1, runtime.PickupCount);
    }

    [Fact]
    public async Task 타기사의번호요청은배정기사의번호시도횟수나차단시간을바꾸지않는다()
    {
        await using var runtime = Runtime(); await runtime.InitializeAsync();
        await PrepareAsync(runtime, runtime.Orders[0].SampleOrderId);
        var order = runtime.Orders[0]; var slip = order.Slip!;
        for (var index = 0; index < 7; index++)
            Assert.False(await runtime.PickupByNumberAsync(order.SampleOrderId, "0000", Request(), slip.Revision, "other-driver"));
        Assert.True(await NumberAsync(runtime, order.SampleOrderId, slip.ConfirmationCode, slip.Revision));
        Assert.Equal(1, runtime.PickupCount);
    }

    [Fact]
    public async Task 같은GUID의D형식과N형식은동일한멱등요청으로처리한다()
    {
        await using var runtime = Runtime(); await runtime.InitializeAsync();
        await PrepareAsync(runtime, runtime.Orders[0].SampleOrderId);
        var order = runtime.Orders[0]; var slip = order.Slip!; var request = Guid.NewGuid();
        Assert.True(await QrAsync(runtime, order.SampleOrderId, slip, request.ToString("D")));
        var firstAt = runtime.Orders[0].Slip!.PickupAtUtc;
        Assert.True(await QrAsync(runtime, order.SampleOrderId, slip, request.ToString("N")));
        Assert.Equal(firstAt, runtime.Orders[0].Slip!.PickupAtUtc); Assert.Equal(1, runtime.PickupCount);
    }

    [Fact]
    public async Task 기존체크박스인계우회는포장후에도거부한다()
    {
        await using var runtime = Runtime(); await runtime.InitializeAsync();
        await PrepareAsync(runtime, runtime.Orders[0].SampleOrderId);
        Assert.False(await runtime.ConfirmMockHandoffAsync(runtime.Orders[0].SampleOrderId, true));
        Assert.Contains("QR", runtime.Error!); Assert.Equal(0, runtime.PickupCount);
    }

    [Fact]
    public async Task 리셋과종료는전표요청인수를지우고이전전표는재사용할수없다()
    {
        var runtime = Runtime(); await runtime.InitializeAsync();
        await PrepareAsync(runtime, runtime.Orders[0].SampleOrderId);
        var order = runtime.Orders[0]; var slip = order.Slip!; var request = Request();
        Assert.True(await QrAsync(runtime, order.SampleOrderId, slip, request));
        await runtime.ResetAsync(); Assert.Equal(0, runtime.PickupCount); Assert.Empty(runtime.Orders);
        await runtime.InitializeAsync(); Assert.All(runtime.Orders, row => Assert.Null(row.Slip));
        await PrepareAsync(runtime, runtime.Orders[0].SampleOrderId);
        Assert.False(await QrAsync(runtime, order.SampleOrderId, slip, request)); Assert.Equal(0, runtime.PickupCount);
        await runtime.DisposeAsync(); Assert.Empty(runtime.Orders); Assert.Equal(0, runtime.PickupCount);
        await runtime.DisposeAsync();
    }

    [Fact]
    public async Task 서로다른주문은각각의전표와인수결과를갖는다()
    {
        await using var runtime = Runtime(); await runtime.InitializeAsync();
        var firstId = runtime.Orders[0].SampleOrderId; var secondId = runtime.Orders[1].SampleOrderId;
        await PrepareAsync(runtime, firstId); await PrepareAsync(runtime, secondId);
        var first = runtime.Orders[0].Slip!; var second = runtime.Orders[1].Slip!;
        Assert.NotEqual(first.LabelId, second.LabelId);
        Assert.True(await QrAsync(runtime, firstId, first)); Assert.False(runtime.Orders[1].Slip!.PickedUp);
        Assert.True(await QrAsync(runtime, secondId, second)); Assert.Equal(2, runtime.PickupCount);
        var json = JsonSerializer.Serialize(runtime.Orders);
        Assert.DoesNotContain("PickupAddress", json); Assert.DoesNotContain("Phone", json); Assert.DoesNotContain("Token", json);
    }

    private static SsalddelMartSampleRuntime Runtime(TimeProvider? clock = null) => new(new SsalddelMartSampleInput([
        new("sample-mart-order-01", [new("합성 감자", 2), new("합성 두부", 1)]),
        new("sample-mart-order-02", [new("합성 우유", 3)])]), clock);

    private static async Task PickAsync(SsalddelMartSampleRuntime runtime, string orderId)
    {
        foreach (var line in runtime.Orders.Single(order => order.SampleOrderId == orderId).Lines)
        {
            Assert.True(await runtime.StartPickingAsync(orderId, line.LineId), runtime.Error);
            Assert.True(await runtime.CompletePickingAsync(orderId, line.LineId, line.RackCode, true, true), runtime.Error);
        }
    }

    private static async Task PrepareAsync(SsalddelMartSampleRuntime runtime, string orderId)
    {
        Assert.True(await runtime.IssueSlipAsync(orderId), runtime.Error);
        await PickAsync(runtime, orderId);
        Assert.True(await runtime.PackOrderAsync(orderId, true, true), runtime.Error);
    }

    private static string Request() => Guid.NewGuid().ToString("N");
    private static Task<bool> QrAsync(SsalddelMartSampleRuntime runtime, string orderId, 살뜰마트표본전표상태 slip, string? requestId = null)
        => runtime.PickupByQrAsync(orderId, slip.QrCode, requestId ?? Request(), slip.Revision, SsalddelMartSampleRuntime.SampleDriverId);
    private static Task<bool> NumberAsync(SsalddelMartSampleRuntime runtime, string orderId, string code, long revision)
        => runtime.PickupByNumberAsync(orderId, code, Request(), revision, SsalddelMartSampleRuntime.SampleDriverId);

    private sealed class SampleClock : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 10, 10, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan delta) => _now += delta;
    }
}
