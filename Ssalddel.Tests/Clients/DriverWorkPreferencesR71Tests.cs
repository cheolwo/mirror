using Ssalddel.Contracts.Driver.Settings;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.DriverPreferences;

namespace Ssalddel.Tests.Clients;

public sealed class DriverWorkPreferencesR71Tests
{
    [Fact]
    public async Task 복수희망과피킹은_확인한판본으로한번저장하고_정본조회뒤적용한다()
    {
        var client = new Client(); using var model = new DriverWorkParticipationViewModel(client);
        await model.LoadAsync();
        model.FoodDelivery = model.QuickDelivery = model.CargoTransport = model.DirectPicking = true;
        model.VehicleKind = DriverVehicleKinds.Motorcycle; model.MaxConcurrentJobs = 3;
        model.CapacityKg = 15; model.CapacityLitres = 60; model.FoodCargoSeparationConfirmed = true;
        await model.SaveAsync();
        Assert.Equal(1, client.Writes); Assert.Equal(2, client.Reads);
        Assert.Equal(4, client.Written!.ExpectedRevision); Assert.Equal(4, client.Written.Settings.Revision);
        Assert.True(model.FoodDelivery && model.QuickDelivery && model.CargoTransport && model.DirectPicking);
        Assert.Equal(5, model.ConfirmedRevision); Assert.True(model.CanEdit); Assert.False(model.NeedsResultCheck);
        Assert.Contains("확인했습니다", model.Message);
    }

    [Fact]
    public async Task 제원미확인은_null이고_추가배차끄기는_다른선택과별도로저장한다()
    {
        var client = new Client(); using var model = new DriverWorkParticipationViewModel(client); await model.LoadAsync();
        model.ReceiveAdditionalOffers = false; model.CapacityKg = model.CapacityLitres = null;
        await model.SaveAsync();
        Assert.False(client.Written!.Settings.ReceiveAdditionalOffers);
        Assert.Null(client.Written.Settings.CapacityKg); Assert.Null(client.Written.Settings.CapacityLitres);
    }

    [Fact]
    public async Task 모든업무끄기는_자격생성없이저장할수있다()
    {
        var client = new Client(); using var model = new DriverWorkParticipationViewModel(client); await model.LoadAsync();
        model.FoodDelivery = model.QuickDelivery = model.CargoTransport = model.DirectPicking = false;
        await model.SaveAsync(); Assert.Equal(1, client.Writes); Assert.False(client.Written!.Settings.FoodDelivery);
    }

    [Fact]
    public async Task 기능OFF와자격미확인은_희망준비를막거나_자격을만들지않는다()
    {
        var client = new Client { Value = Response() with { SharedAdmissionEnabled = false, FoodEligible = false, CargoEligible = false } };
        using var model = new DriverWorkParticipationViewModel(client); await model.LoadAsync();
        model.FoodDelivery = model.CargoTransport = true; await model.SaveAsync();
        Assert.Equal(1, client.Writes); Assert.False(model.SharedAdmissionEnabled || model.FoodEligible || model.CargoEligible);
    }

    [Theory]
    [InlineData("vehicle")]
    [InlineData("capacity-kg")]
    [InlineData("capacity-litres")]
    [InlineData("jobs-low")]
    [InlineData("jobs-high")]
    [InlineData("picking")]
    public async Task 입력오류는_PUT를보내지않는다(string invalid)
    {
        var client = new Client(); using var model = new DriverWorkParticipationViewModel(client); await model.LoadAsync();
        switch (invalid)
        {
            case "vehicle": model.VehicleKind = "unknown"; break;
            case "capacity-kg": model.CapacityKg = 0; break;
            case "capacity-litres": model.CapacityLitres = -1; break;
            case "jobs-low": model.MaxConcurrentJobs = 0; break;
            case "jobs-high": model.MaxConcurrentJobs = 5; break;
            case "picking": model.DirectPicking = true; model.QuickDelivery = model.CargoTransport = false; break;
        }
        await model.SaveAsync(); Assert.Equal(0, client.Writes); Assert.NotNull(model.Error); Assert.True(model.CanEdit);
    }

    [Theory]
    [InlineData("50000.001", "1")]
    [InlineData("50001", "1")]
    [InlineData("1.0001", "1")]
    [InlineData("0.0001", "1")]
    [InlineData("1", "1000000.001")]
    [InlineData("1", "1000001")]
    [InlineData("1", "1.0001")]
    [InlineData("1", "0.0001")]
    public async Task 제원범위나소수자리오류는_입력그대로유지하고_서버전송없이안내한다(string kgText, string litresText)
    {
        var client = new Client(); using var model = new DriverWorkParticipationViewModel(client); await model.LoadAsync();
        var kg = decimal.Parse(kgText, System.Globalization.CultureInfo.InvariantCulture);
        var litres = decimal.Parse(litresText, System.Globalization.CultureInfo.InvariantCulture);
        model.CapacityKg = kg; model.CapacityLitres = litres;
        await model.SaveAsync();
        Assert.Equal(0, client.Writes); Assert.Equal(1, client.Reads);
        Assert.Equal(kg, model.CapacityKg); Assert.Equal(litres, model.CapacityLitres);
        Assert.True(model.IsLoaded && model.CanEdit); Assert.False(model.NeedsResultCheck);
        Assert.Contains("소수 3자리", model.Error); Assert.Contains("50,000kg", model.Error); Assert.Contains("1,000,000L", model.Error);
    }

    [Theory]
    [InlineData("0.001", "0.001")]
    [InlineData("50000", "1000000")]
    public async Task 제원최소단위와상한경계는_저장하고정본으로확인한다(string kgText, string litresText)
    {
        var client = new Client(); using var model = new DriverWorkParticipationViewModel(client); await model.LoadAsync();
        model.CapacityKg = decimal.Parse(kgText, System.Globalization.CultureInfo.InvariantCulture);
        model.CapacityLitres = decimal.Parse(litresText, System.Globalization.CultureInfo.InvariantCulture);
        await model.SaveAsync(); Assert.Equal(1, client.Writes); Assert.Equal(2, client.Reads); Assert.True(model.CanEdit); Assert.Null(model.Error);
    }

    [Fact]
    public async Task 제원오류를고치면_재조회없이처음저장할수있다()
    {
        var client = new Client(); using var model = new DriverWorkParticipationViewModel(client); await model.LoadAsync();
        model.CapacityKg = 1.2345m; await model.SaveAsync(); Assert.Equal(0, client.Writes);
        model.CapacityKg = 1.234m; await model.SaveAsync(); Assert.Equal(1, client.Writes); Assert.Equal(2, client.Reads); Assert.Null(model.Error);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 저장결과유실은_재전송을막고_조회로변경전과저장완료를구분한다(bool applied)
    {
        var client = new Client();
        client.Update = (request, _) =>
        {
            if (applied) client.Value = client.Value with { Settings = request.Settings with { Revision = request.ExpectedRevision + 1 } };
            throw new HttpRequestException("응답 유실");
        };
        using var model = new DriverWorkParticipationViewModel(client); await model.LoadAsync(); model.QuickDelivery = true;
        await model.SaveAsync(); await model.SaveAsync();
        Assert.Equal(1, client.Writes); Assert.True(model.NeedsResultCheck); Assert.False(model.CanEdit);
        await model.LoadAsync();
        Assert.False(model.NeedsResultCheck); Assert.True(model.CanEdit); Assert.Equal(applied, model.QuickDelivery);
        Assert.Contains(applied ? "저장한" : "변경 전", model.Message);
    }

    [Fact]
    public async Task 저장후조회실패도_다시PUT하지않고_최신다른설정으로복구한다()
    {
        var client = new Client(); using var model = new DriverWorkParticipationViewModel(client); await model.LoadAsync();
        client.Get = _ => throw new HttpRequestException(); model.QuickDelivery = true;
        await model.SaveAsync(); await model.SaveAsync(); Assert.Equal(1, client.Writes);
        client.Get = null;
        client.Value = client.Value with { Settings = client.Value.Settings with { Revision = 6, QuickDelivery = false, CargoTransport = true } };
        await model.LoadAsync(); Assert.False(model.QuickDelivery); Assert.True(model.CargoTransport); Assert.Contains("최신 설정", model.Message);
    }

    [Fact]
    public async Task 저장중같은화면의두번클릭은_한번만전송한다()
    {
        var client = new Client(); var entered = Signal(); var reply = new TaskCompletionSource<DriverWorkParticipationResponse>();
        client.Update = async (_, _) => { entered.SetResult(); return await reply.Task; };
        using var model = new DriverWorkParticipationViewModel(client); await model.LoadAsync();
        var save = model.SaveAsync(); await entered.Task; await model.SaveAsync(); Assert.Equal(1, client.Writes);
        client.Value = client.Value with { Settings = client.Written!.Settings with { Revision = 5 } };
        reply.SetResult(client.Value); await save; Assert.True(model.CanEdit);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 계정변경과같은계정재로그인은_늦은조회응답을버린다(bool sameAccount)
    {
        var client = new Client(); var reply = new TaskCompletionSource<DriverWorkParticipationResponse>();
        client.Get = _ => reply.Task;
        using var model = new DriverWorkParticipationViewModel(client); var load = model.LoadAsync();
        client.Change(new(sameAccount ? "driver-a" : "driver-b", 2, true));
        reply.SetResult(Response()); await load;
        Assert.False(model.IsLoaded || model.IsBusy || model.FoodDelivery); Assert.Null(model.ConfirmedRevision);
    }

    [Fact]
    public async Task 저장중로그아웃은_늦은저장응답뒤정본조회도막는다()
    {
        var client = new Client(); var reply = new TaskCompletionSource<DriverWorkParticipationResponse>();
        client.Update = (_, _) => reply.Task;
        using var model = new DriverWorkParticipationViewModel(client); await model.LoadAsync(); var save = model.SaveAsync();
        client.Change(new(null, 2, false)); reply.SetResult(Response(5)); await save;
        Assert.Equal(1, client.Reads); Assert.True(model.RequiresLogin); Assert.False(model.CanEdit || model.NeedsResultCheck);
    }

    [Fact]
    public async Task 이벤트없는계정변경도_응답을버리고바쁜상태를해제한다()
    {
        var client = new Client(); var reply = new TaskCompletionSource<DriverWorkParticipationResponse>(); client.Get = _ => reply.Task;
        using var model = new DriverWorkParticipationViewModel(client); var load = model.LoadAsync();
        client.Identity = new("driver-b", 2, true); reply.SetResult(Response()); await load;
        Assert.False(model.IsLoaded || model.IsBusy); Assert.Null(model.ConfirmedRevision);
    }

    [Fact]
    public async Task 화면종료뒤응답은_다른화면에적용하지않는다()
    {
        var client = new Client(); var reply = new TaskCompletionSource<DriverWorkParticipationResponse>(); client.Get = _ => reply.Task;
        var model = new DriverWorkParticipationViewModel(client); var load = model.LoadAsync(); model.Dispose();
        reply.SetResult(Response()); await load; Assert.False(model.IsLoaded || model.CanEdit || model.CanLoad);
    }

    [Theory]
    [InlineData("owner")]
    [InlineData("revision")]
    [InlineData("vehicle")]
    [InlineData("capacity")]
    [InlineData("operational-max")]
    [InlineData("settings-null")]
    public async Task 잘못된정본은_편집가능상태로승격하지않는다(string invalid)
    {
        var value = Response();
        value = invalid switch
        {
            "owner" => value with { DriverId = "DRIVER-A" },
            "revision" => value with { Settings = value.Settings with { Revision = -1 } },
            "vehicle" => value with { Settings = value.Settings with { VehicleKind = "other" } },
            "capacity" => value with { Settings = value.Settings with { CapacityKg = 0 } },
            "operational-max" => value with { OperationalMaxConcurrentJobs = 0 },
            _ => value with { Settings = null! }
        };
        var client = new Client { Value = value }; using var model = new DriverWorkParticipationViewModel(client);
        await model.LoadAsync(); Assert.False(model.CanEdit || model.IsLoaded); Assert.NotNull(model.Error);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 저장응답또는같은판본정본의다른내용은_성공으로표시하지않는다(bool readMismatch)
    {
        var client = new Client(); using var model = new DriverWorkParticipationViewModel(client); await model.LoadAsync();
        if (readMismatch) client.Get = _ => Task.FromResult(Response(5) with { Settings = Response(5).Settings with { QuickDelivery = true } });
        else client.Update = (request, _) => Task.FromResult(Response(5) with { Settings = request.Settings with { Revision = 5, QuickDelivery = !request.Settings.QuickDelivery } });
        await model.SaveAsync(); Assert.False(model.CanEdit || model.IsLoaded); Assert.True(model.NeedsResultCheck); Assert.NotNull(model.Error);
    }

    [Fact]
    public async Task 저장중다른화면변경은_더최신판본을그대로보여준다()
    {
        var client = new Client(); using var model = new DriverWorkParticipationViewModel(client); await model.LoadAsync();
        client.Get = _ => Task.FromResult(Response(6) with { Settings = Response(6).Settings with { QuickDelivery = true } });
        await model.SaveAsync(); Assert.Equal(6, model.ConfirmedRevision); Assert.True(model.QuickDelivery); Assert.Contains("최신 설정", model.Message);
    }

    [Fact]
    public async Task 초기미설정과이전판본조회는_구별한다()
    {
        var client = new Client { Value = Response(0) with { Settings = new() } };
        using var model = new DriverWorkParticipationViewModel(client); await model.LoadAsync(); Assert.True(model.IsLoaded); Assert.Equal("", model.VehicleKind);
        model.VehicleKind = DriverVehicleKinds.Walking; await model.SaveAsync(); Assert.Equal(1, model.ConfirmedRevision);
        client.Value = Response(0); await model.LoadAsync(); Assert.False(model.IsLoaded); Assert.NotNull(model.Error);
    }

    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal static DriverWorkParticipationResponse Response(long revision = 4) => new()
    {
        DriverId = "driver-a", SharedAdmissionEnabled = true, FoodEligible = true, CargoEligible = true,
        OperationalMaxConcurrentJobs = 4,
        Settings = new() { Revision = revision, FoodDelivery = true, VehicleKind = DriverVehicleKinds.Motorcycle, MaxConcurrentJobs = 1 }
    };
    internal sealed class Client : IDriverWorkParticipationClient
    {
        public DriverWorkParticipationIdentity Identity { get; set; } = new("driver-a", 1, true);
        public event Action? SessionChanged;
        public int Reads { get; private set; }
        public int Writes { get; private set; }
        public DriverWorkParticipationResponse Value { get; set; } = Response();
        public DriverWorkParticipationUpdate? Written { get; private set; }
        public Func<CancellationToken, Task<DriverWorkParticipationResponse>>? Get { get; set; }
        public Func<DriverWorkParticipationUpdate, CancellationToken, Task<DriverWorkParticipationResponse>>? Update { get; set; }
        public Task<DriverWorkParticipationResponse> GetAsync(CancellationToken cancellationToken = default)
        { Reads++; return Get?.Invoke(cancellationToken) ?? Task.FromResult(Value); }
        public Task<DriverWorkParticipationResponse> UpdateAsync(DriverWorkParticipationUpdate request, CancellationToken cancellationToken = default)
        {
            Writes++; Written = request;
            if (Update is not null) return Update(request, cancellationToken);
            Value = Value with { Settings = request.Settings with { Revision = request.ExpectedRevision + 1 } };
            return Task.FromResult(Value);
        }
        public void Change(DriverWorkParticipationIdentity value) { Identity = value; SessionChanged?.Invoke(); }
    }
}
