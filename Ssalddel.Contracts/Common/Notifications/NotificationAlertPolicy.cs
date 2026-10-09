namespace Ssalddel.Contracts.Common.Notifications;

public sealed record NotificationAlertPreferences(bool Enabled = true, bool Sound = true,
    bool Vibrate = true, bool QuietAtNight = false);

public sealed record NotificationAlertPlan(bool Enabled, bool Sound, bool Vibrate);
public sealed record NotificationChannelState(string Id, bool Blocked, bool Sound, bool Vibrate);

public static class NotificationAlertPolicy
{
    // The existing night switch now has one displayed, deterministic local-time rule.
    public static NotificationAlertPlan Evaluate(NotificationAlertPreferences preferences, DateTimeOffset now)
    {
        var hour = now.ToOffset(TimeSpan.FromHours(9)).Hour;
        var quiet = preferences.QuietAtNight && (hour >= 22 || hour < 7);
        return new(preferences.Enabled, preferences.Sound && !quiet, preferences.Vibrate && !quiet);
    }

    public static NotificationAlertPlan PreserveLegacy(NotificationAlertPlan plan, NotificationChannelState? legacy,
        IEnumerable<NotificationChannelState> existingModes)
    {
        // Creating a new app channel must not escape an existing OS channel block or mute.
        var modes = existingModes.ToArray();
        if (legacy?.Blocked == true || modes.Any(x => x.Blocked)) return new(false, false, false);
        // A silent mode created by the app differs from a user muting an audible OS channel.
        // Preserve that user choice when switching sound/vibration combinations.
        plan = plan with
        {
            Sound = plan.Sound && !modes.Any(x => (x.Id.EndsWith(".s1v0", StringComparison.Ordinal) || x.Id.EndsWith(".s1v1", StringComparison.Ordinal)) && !x.Sound),
            Vibrate = plan.Vibrate && !modes.Any(x => (x.Id.EndsWith(".s0v1", StringComparison.Ordinal) || x.Id.EndsWith(".s1v1", StringComparison.Ordinal)) && !x.Vibrate)
        };
        return legacy is null ? plan : plan with
        { Sound = plan.Sound && legacy.Sound, Vibrate = plan.Vibrate && legacy.Vibrate };
    }

    public static string ChannelId(string prefix, NotificationAlertPlan plan)
        => $"{prefix}.s{(plan.Sound ? 1 : 0)}v{(plan.Vibrate ? 1 : 0)}";
}
