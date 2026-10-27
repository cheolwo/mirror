namespace Ssalddel.Ui.Common.Areas.App.Services;

/// <summary>Opened는 전화 앱을 연 결과이며 실제 발신·통화·연락 완료가 아닙니다.</summary>
public enum 업무전화앱결과 { Opened, NotSupported, InvalidNumber, Failed }

/// <summary>명시적으로 선택한 번호를 전화 앱에만 전달합니다. 연락처 조회·저장이나 실제 발신을 실행하지 않습니다.</summary>
public interface I업무전화앱Adapter
{
    bool IsSupported { get; }
    Task<업무전화앱결과> OpenAsync(string number, CancellationToken ct = default);
}

/// <summary>명령·URI·내선 기호를 전화번호로 바꾸지 않고 거절합니다.</summary>
public static class 업무전화번호Policy
{
    public static bool TryNormalize(string? raw, out string number)
    {
        number = "";
        if (raw is null || raw.Length is 0 or > 48) return false;
        Span<char> buffer = stackalloc char[48];
        var length = 0;
        var digits = 0;
        foreach (var character in raw)
        {
            if (character is ' ' or '-') continue;
            if (character == '+' && length == 0) { buffer[length++] = character; continue; }
            if (character is < '0' or > '9') return false;
            buffer[length++] = character;
            if (++digits > 15) return false;
        }
        if (digits < 7) return false;
        number = new string(buffer[..length]);
        return true;
    }
}

/// <summary>Web 등 전화 앱 열기를 보장하지 않는 호스트의 기본값입니다. 외부 링크를 실행하지 않습니다.</summary>
public sealed class 미지원업무전화앱Adapter : I업무전화앱Adapter
{
    public bool IsSupported => false;
    public Task<업무전화앱결과> OpenAsync(string number, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(업무전화번호Policy.TryNormalize(number, out _)
            ? 업무전화앱결과.NotSupported : 업무전화앱결과.InvalidNumber);
    }
}
