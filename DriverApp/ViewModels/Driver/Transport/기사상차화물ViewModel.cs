using CommunityToolkit.Mvvm.ComponentModel;
using DriverApp.Models.Driver.Samples;
using MudBlazor;
using Color = MudBlazor.Color;

namespace DriverApp.ViewModels.Driver.Transport;

public sealed partial class 기사상차화물ViewModel(기사운송작업상태ViewModel 상태) : ObservableObject
{
    private readonly HashSet<string> _체크완료코드 = [];
    private readonly HashSet<string> _상차완료코드 = [];
    private 기사운송샘플항목? _운송;
    private string? _선택화물코드;

    [ObservableProperty]
    public partial string? 바코드입력 { get; set; }

    public IReadOnlySet<string> 상차완료코드 => _상차완료코드;

    public bool 체크목록완료
        => _운송 is null
           || _운송.상차체크목록.Count == 0
           || _운송.상차체크목록.All(item => _체크완료코드.Contains(item.Code));

    public bool 화물상차완료
        => _운송 is null
           || _운송.상차대상화물목록.Count == 0
           || _운송.상차대상화물목록.All(item => _상차완료코드.Contains(item.Code));

    public IReadOnlyList<기사상차대상화물> 정렬화물
        => _운송?.상차대상화물목록
            .OrderBy(item => item.적재순번)
            .ThenByDescending(item => item.하차순번)
            .ThenBy(item => item.Code, StringComparer.Ordinal)
            .ToArray()
           ?? [];

    public 기사상차대상화물? 다음화물
        => 정렬화물.FirstOrDefault(item => !_상차완료코드.Contains(item.Code));

    public 기사상차대상화물? 선택화물
        => _운송?.상차대상화물목록.FirstOrDefault(item =>
            string.Equals(item.Code, _선택화물코드, StringComparison.Ordinal));

    public bool 선택화물순서다름
        => _운송?.적재순번필요 == true
           && 선택화물 is not null
           && !상차확인됨(선택화물.Code)
           && 다음화물 is not null
           && !string.Equals(선택화물.Code, 다음화물.Code, StringComparison.Ordinal);

    public Color 상차유형색
        => _운송?.IsFcl == true
            ? Color.Warning
            : _운송?.IsLcl == true
                ? Color.Info
                : Color.Default;

    public void 운송설정(기사운송샘플항목? transport)
    {
        _운송 = transport;
        _체크완료코드.Clear();
        _상차완료코드.Clear();
        _선택화물코드 = null;
        바코드입력 = null;
        OnPropertyChanged(string.Empty);
    }

    public bool 체크확인됨(string code) => _체크완료코드.Contains(code);

    public void 체크설정(string code, bool value)
    {
        if (value)
        {
            _체크완료코드.Add(code);
        }
        else
        {
            _체크완료코드.Remove(code);
        }

        OnPropertyChanged(nameof(체크목록완료));
    }

    public void 바코드조회()
    {
        var barcode = 바코드입력?.Trim();
        if (string.IsNullOrWhiteSpace(barcode))
        {
            상태.설정("상차 바코드를 입력하거나 스캔해 주세요.", Severity.Warning);
            return;
        }

        var item = _운송?.상차대상화물목록.FirstOrDefault(candidate =>
            string.Equals(candidate.Barcode, barcode, StringComparison.OrdinalIgnoreCase)
            || string.Equals(candidate.Code, barcode, StringComparison.OrdinalIgnoreCase));

        if (item is null)
        {
            상태.설정("현재 운송 건에 포함되지 않은 상차 바코드입니다.", Severity.Warning);
            return;
        }

        화물선택(item);
        if (_운송?.적재순번필요 == true
            && 다음화물 is not null
            && !string.Equals(item.Code, 다음화물.Code, StringComparison.Ordinal))
        {
            상태.설정($"권장 다음 화물은 {다음화물.Label}입니다. 순서를 바꾸기 전에 적재 위치를 확인해 주세요.", Severity.Warning);
            return;
        }

        상태.설정($"{item.Label} 상차 정보를 불러왔습니다.", Severity.Info);
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
        상태.설정($"{item.Label}을(를) 권장 상차 화물로 선택했습니다.", Severity.Info);
    }

    public void 선택화물확인()
    {
        if (선택화물 is null)
        {
            상태.설정("먼저 상차 바코드를 조회해 주세요.", Severity.Warning);
            return;
        }

        var selectedCargo = 선택화물;
        var recommendedCargo = 다음화물;
        var outOfSequence = _운송?.적재순번필요 == true
                            && recommendedCargo is not null
                            && !string.Equals(selectedCargo.Code, recommendedCargo.Code, StringComparison.Ordinal);

        _상차완료코드.Add(selectedCargo.Code);
        OnPropertyChanged(string.Empty);
        상태.설정(
            outOfSequence
                ? $"{selectedCargo.Label}을(를) 권장 순서와 다르게 상차했습니다. 실제 적재 위치를 다시 확인해 주세요."
                : $"{selectedCargo.Label} 상차 확인을 기록했습니다.",
            outOfSequence ? Severity.Warning : Severity.Success);
    }

    public bool 상차확인됨(string code) => _상차완료코드.Contains(code);

    public string 화물칩라벨(기사상차대상화물 item)
    {
        if (상차확인됨(item.Code))
        {
            return "상차 완료";
        }

        if (다음화물인가(item))
        {
            return "지금 상차";
        }

        return _운송?.적재순번필요 == true ? $"{item.적재순번표시} 대기" : "수량 확인";
    }

    public Color 화물칩색(기사상차대상화물 item)
    {
        if (상차확인됨(item.Code))
        {
            return Color.Success;
        }

        if (다음화물인가(item))
        {
            return Color.Warning;
        }

        return _운송?.적재순번필요 == true ? Color.Default : Color.Info;
    }

    public Variant 화물버튼모양(기사상차대상화물 item)
        => 상차확인됨(item.Code) || 다음화물인가(item)
            ? Variant.Filled
            : Variant.Outlined;

    public Color 화물버튼색(기사상차대상화물 item)
        => 상차확인됨(item.Code)
            ? Color.Success
            : 다음화물인가(item)
                ? Color.Warning
                : Color.Secondary;

    private bool 다음화물인가(기사상차대상화물 item)
        => 다음화물 is not null
           && string.Equals(item.Code, 다음화물.Code, StringComparison.Ordinal);
}
