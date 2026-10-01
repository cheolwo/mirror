using CommunityToolkit.Mvvm.ComponentModel;
using DriverApp.Models.Driver.Samples;
using MudBlazor;
using Color = MudBlazor.Color;

namespace DriverApp.ViewModels.Driver.Transport;

public sealed partial class 기사하차화물ViewModel(기사운송작업상태ViewModel 상태) : ObservableObject
{
    private readonly HashSet<string> _하차완료코드 = [];
    private 기사운송샘플항목? _운송;
    private string? _선택화물코드;

    [ObservableProperty]
    public partial string? 바코드입력 { get; set; }

    public IReadOnlySet<string> 하차완료코드 => _하차완료코드;

    public IReadOnlyList<기사상차대상화물> 정렬화물
        => _운송?.상차대상화물목록
            .OrderBy(item => item.하차순번)
            .ThenByDescending(item => item.적재순번)
            .ToArray()
           ?? [];

    public 기사상차대상화물? 선택화물
        => _운송?.상차대상화물목록.FirstOrDefault(item =>
            string.Equals(item.Code, _선택화물코드, StringComparison.Ordinal));

    public 기사상차대상화물? 다음화물
        => 정렬화물.FirstOrDefault(item => !_하차완료코드.Contains(item.Code));

    public bool 선택화물순서다름
        => _운송?.적재순번필요 == true
           && 선택화물 is not null
           && !하차확인됨(선택화물.Code)
           && 다음화물 is not null
           && !string.Equals(선택화물.Code, 다음화물.Code, StringComparison.Ordinal);

    public bool 화물하차완료
        => _운송 is null
           || _운송.상차대상화물목록.Count == 0
           || _운송.상차대상화물목록.All(item => _하차완료코드.Contains(item.Code));

    public string 안내
        => _운송?.적재순번필요 == true
            ? "상차 때 정한 하차 순서대로 바코드를 확인하고 내려줍니다."
            : "동일 규격 화물은 수량과 외관을 중심으로 확인합니다.";

    public string 진행라벨
        => _운송 is null || _운송.상차대상화물목록.Count == 0
            ? "개별 확인 없음"
            : $"{_하차완료코드.Count}/{_운송.상차대상화물목록.Count} 하차 확인";

    public Color 진행색 => 화물하차완료 ? Color.Success : Color.Info;

    public void 운송설정(기사운송샘플항목? transport)
    {
        _운송 = transport;
        _하차완료코드.Clear();
        _선택화물코드 = null;
        바코드입력 = null;
        OnPropertyChanged(string.Empty);
    }

    public void 바코드조회()
    {
        var barcode = 바코드입력?.Trim();
        if (string.IsNullOrWhiteSpace(barcode))
        {
            상태.설정("하차 바코드를 입력하거나 스캔해 주세요.", Severity.Warning);
            return;
        }

        var item = _운송?.상차대상화물목록.FirstOrDefault(candidate =>
            string.Equals(candidate.Barcode, barcode, StringComparison.OrdinalIgnoreCase)
            || string.Equals(candidate.Code, barcode, StringComparison.OrdinalIgnoreCase));

        if (item is null)
        {
            상태.설정("현재 운송 건에 포함되지 않은 하차 바코드입니다.", Severity.Warning);
            return;
        }

        화물선택(item);
        if (_운송?.적재순번필요 == true
            && 다음화물 is not null
            && !string.Equals(item.Code, 다음화물.Code, StringComparison.Ordinal))
        {
            상태.설정($"권장 다음 하차는 {다음화물.하차위치}입니다. 순서를 바꾸기 전에 적재 위치를 확인해 주세요.", Severity.Warning);
            return;
        }

        상태.설정($"{item.하차위치} 하차 정보를 불러왔습니다.", Severity.Info);
    }

    public void 화물선택(기사상차대상화물 item)
    {
        _선택화물코드 = item.Code;
        바코드입력 = item.Barcode;
        OnPropertyChanged(string.Empty);
    }

    public void 다음화물선택()
    {
        if (다음화물 is null)
        {
            return;
        }

        var item = 다음화물;
        화물선택(item);
        상태.설정($"{item.하차위치} 화물을 권장 하차 대상으로 선택했습니다.", Severity.Info);
    }

    public void 선택화물확인()
    {
        if (선택화물 is null)
        {
            상태.설정("먼저 하차 바코드를 조회해 주세요.", Severity.Warning);
            return;
        }

        var selectedCargo = 선택화물;
        var recommendedCargo = 다음화물;
        var outOfSequence = _운송?.적재순번필요 == true
                            && recommendedCargo is not null
                            && !string.Equals(selectedCargo.Code, recommendedCargo.Code, StringComparison.Ordinal);

        _하차완료코드.Add(selectedCargo.Code);
        OnPropertyChanged(string.Empty);
        상태.설정(
            outOfSequence
                ? $"{selectedCargo.하차위치} 화물을 권장 순서와 다르게 하차했습니다. 남은 화물의 적재 상태를 확인해 주세요."
                : $"{selectedCargo.하차위치} 하차 확인을 기록했습니다.",
            outOfSequence ? Severity.Warning : Severity.Success);
    }

    public bool 하차확인됨(string code) => _하차완료코드.Contains(code);

    public string 화물칩라벨(기사상차대상화물 item)
    {
        if (하차확인됨(item.Code))
        {
            return "하차 완료";
        }

        if (다음화물인가(item))
        {
            return "지금 하차";
        }

        return _운송?.적재순번필요 == true ? $"{item.하차순번표시} 대기" : "수량 확인";
    }

    public Color 화물칩색(기사상차대상화물 item)
    {
        if (하차확인됨(item.Code))
        {
            return Color.Success;
        }

        return 다음화물인가(item) ? Color.Info : Color.Default;
    }

    private bool 다음화물인가(기사상차대상화물 item)
        => 다음화물 is not null
           && string.Equals(item.Code, 다음화물.Code, StringComparison.Ordinal);
}
