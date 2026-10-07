namespace FDriverApp.Services;

/// <summary>재인증 후 같은 기사에게만 접수 진입을 복원합니다. 고객 정보·접수 원문·자동 제출은 보관하지 않습니다.</summary>
public sealed class FDriverSupportReturnContext(TimeProvider? clock = null)
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;
    private string? _orderNo;
    private string? _owner;
    private DateTimeOffset _expires;
    public bool Pending { get; private set; }
    public void Capture(string orderNo, string? owner)
    {
        Clear();
        if (string.IsNullOrWhiteSpace(orderNo) || string.IsNullOrWhiteSpace(owner)) return;
        _orderNo = orderNo; _owner = owner; _expires = _clock.GetUtcNow().AddMinutes(15);
    }
    public void Arm() { if (_orderNo is not null && _expires > _clock.GetUtcNow()) Pending = true; else Clear(); }
    public string? Take(string? owner, bool hasDriverAccess)
    {
        if (_expires <= _clock.GetUtcNow() || !string.IsNullOrWhiteSpace(owner) && owner != _owner) { Clear(); return null; }
        if (!Pending || !hasDriverAccess || owner != _owner) return null;
        var result = _orderNo; Clear(); return result;
    }
    public void Clear() { _orderNo = _owner = null; _expires = default; Pending = false; }
}
