using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RestaurantDeskApp.Options;

namespace RestaurantDeskApp.Services;

public sealed class 주문알림Service(
    IOptions<RestaurantOrderAlertOptions> options,
    ILogger<주문알림Service> logger) : I주문알림Service
{
    public async Task 신규주문알림재생Async(CancellationToken cancellationToken = default)
    {
        _ = logger;
        var settings = Normalize(options.Value);
        if (!settings.Enabled)
        {
            return;
        }

        for (var i = 0; i < settings.RepeatCount; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PlaySystemNotificationSound();

            if (settings.UseBeepTone)
            {
                PlayBeepTone(settings);
            }

            if (i + 1 < settings.RepeatCount)
            {
                await Task.Delay(settings.IntervalMilliseconds, cancellationToken);
            }
        }
    }

    private static RestaurantOrderAlertOptions Normalize(RestaurantOrderAlertOptions value)
    {
        return new RestaurantOrderAlertOptions
        {
            Enabled = value.Enabled,
            RepeatCount = Math.Clamp(value.RepeatCount, 1, 10),
            IntervalMilliseconds = Math.Clamp(value.IntervalMilliseconds, 100, 5_000),
            UseBeepTone = value.UseBeepTone,
            BeepFrequency = Math.Clamp(value.BeepFrequency, 37, 15_000),
            BeepDurationMilliseconds = Math.Clamp(value.BeepDurationMilliseconds, 50, 2_000)
        };
    }

    private void PlaySystemNotificationSound()
    {
#if WINDOWS
        try
        {
            System.Media.SystemSounds.Exclamation.Play();
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Windows 시스템 주문 알림음 재생에 실패했습니다.");
        }
#endif
    }

    private void PlayBeepTone(RestaurantOrderAlertOptions settings)
    {
#if WINDOWS
        try
        {
            Console.Beep(settings.BeepFrequency, settings.BeepDurationMilliseconds);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Windows 주문 알림 beep 톤 재생에 실패했습니다.");
        }
#endif
    }
}
