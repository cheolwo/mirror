using System.Security.Cryptography;
using System.Text;
using Ssalddel.Contracts.Common.Dispatch;

namespace DriverApp.Services;

public sealed partial class DriverWorkStartRecoveryCoordinator
{
    private async Task<운영배차수신상태Dto?> ReadReceptionBeforeStartAsync(string owner, long revision, CancellationToken token)
    {
        if (_dispatchReception is null) return null;
        try
        {
            var state = await _dispatchReception.GetAvailabilityAsync(token);
            EnsureCurrent(owner, revision, token);
            return KnownReception(state, owner) ? state : null;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch { EnsureCurrent(owner, revision, token); return null; }
    }

    private async Task<bool> ConfirmInitialReceptionAsync(string owner, long revision, CancellationToken token)
    {
        var pending = Pending;
        // 기존 운행 복귀·구형 복구 기록·시작 전 조회 실패는 자동 수신 변경의 근거가 아니다.
        if (_dispatchReception is null || pending is null || !pending.ReceptionIntentObserved) return true;
        운영배차수신상태Dto current;
        try
        {
            current = await _dispatchReception.GetAvailabilityAsync(token);
            EnsureCurrent(owner, revision, token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch { EnsureCurrent(owner, revision, token); return false; }
        if (!KnownReception(current, owner)) return false;
        if (current.수신의사Code == 운영배차수신의사Code.On
            || !string.Equals(current.수신의사판본Token, pending.ReceptionIntentVersionToken, StringComparison.Ordinal))
            return true; // 자동 중지 또는 시작 중/응답 유실 뒤 기사 본인이 바꾼 최신 의사를 보존한다.

        var request = new 운영배차수신의사변경요청
        {
            클라이언트요청Id = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(
                $"cargo-reception-after-start:{pending.ClientRequestId:N}"))[..16]),
            수신의사Code = 운영배차수신의사Code.On,
            미응답자동중지해제 = false,
            최초On조건부적용 = true,
            예상수신의사판본Token = pending.ReceptionIntentVersionToken
        };
        try
        {
            var response = await _dispatchReception.ChangeIntentAsync(request, token);
            EnsureCurrent(owner, revision, token);
            return KnownReception(response, owner) && (response.수신의사Code == 운영배차수신의사Code.On
                || !string.Equals(response.수신의사판본Token, pending.ReceptionIntentVersionToken, StringComparison.Ordinal));
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch
        {
            EnsureCurrent(owner, revision, token);
            // POST 시작은 다시 보내지 않는다. 같은 수신 요청의 결과도 정본으로만 확인한다.
            var confirmed = await ReadReceptionBeforeStartAsync(owner, revision, token);
            return confirmed is not null && (confirmed.수신의사Code == 운영배차수신의사Code.On
                || !string.Equals(confirmed.수신의사판본Token, pending.ReceptionIntentVersionToken, StringComparison.Ordinal));
        }
    }

    private static bool KnownReception(운영배차수신상태Dto state, string owner)
        => string.Equals(state.주체Id, owner, StringComparison.Ordinal)
           && (state.수신의사Code == 운영배차수신의사Code.Off
               || state.수신의사Code == 운영배차수신의사Code.On && state.수신의사변경시각Utc.HasValue);
}
