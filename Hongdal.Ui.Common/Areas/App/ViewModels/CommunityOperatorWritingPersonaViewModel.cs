using CommunityToolkit.Mvvm.ComponentModel;

namespace Hongdal.Ui.Common.Areas.App.ViewModels;

public sealed record CommunityOperatorWritingPersona(
    string Key,
    string Nickname,
    string Perspective);

public sealed class CommunityOperatorWritingPersonaViewModel : ObservableObject
{
    private string _selectedKey;

    public CommunityOperatorWritingPersonaViewModel()
    {
        _selectedKey = Personas[0].Key;
    }

    public IReadOnlyList<CommunityOperatorWritingPersona> Personas { get; } =
    [
        new("vow-recorder", "서원을 적는 이 · 운영자", "사람들이 같이 이루고 싶은 마음을 기록합니다."),
        new("people-finder", "함께 찾는 사람 · 운영자", "일을 함께 풀 사람과 업체를 살펴봅니다."),
        new("neighborhood-notes", "동네 살림 기록자 · 운영자", "가까운 이웃의 필요와 일상을 살펴봅니다."),
        new("market-bridge", "시장과 잇는 사람 · 운영자", "생산자·상인·구매자가 만날 조건을 살펴봅니다."),
        new("logistics-observer", "물류길 살피는 사람 · 운영자", "보관·운송·통관에 필요한 관계자와 조건을 살펴봅니다.")
    ];

    public string SelectedKey
    {
        get => _selectedKey;
        private set
        {
            if (SetProperty(ref _selectedKey, value))
            {
                OnPropertyChanged(nameof(Selected));
            }
        }
    }

    public CommunityOperatorWritingPersona Selected
        => Personas.First(persona => string.Equals(persona.Key, SelectedKey, StringComparison.Ordinal));

    public void Select(string key)
    {
        var selected = Personas.FirstOrDefault(persona =>
            string.Equals(persona.Key, key, StringComparison.OrdinalIgnoreCase));
        if (selected is null)
        {
            throw new ArgumentException("지원하지 않는 운영자 필명입니다.", nameof(key));
        }

        SelectedKey = selected.Key;
    }

    public void SelectNext()
    {
        var currentIndex = Personas
            .Select((persona, index) => (persona, index))
            .First(item => string.Equals(item.persona.Key, SelectedKey, StringComparison.Ordinal))
            .index;
        SelectedKey = Personas[(currentIndex + 1) % Personas.Count].Key;
    }
}
