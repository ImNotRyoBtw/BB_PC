using System;

namespace BBPC.UnityRuntime
{
    // TimeHelper.GetRemainingTimeString (Ghidra, VA 0x2C71D30): days >= 2 -> common_end_day (days); else minutes >= 90 -> common_end_hour (int total hours);
    // else minutes >= 1 -> common_end_min; else seconds -> common_end_sec. Empty when the time has run out.
    // Lottery panels/dialogs prefix it with "day_left_draw" ("Ends in _$$_"). Strings come from the packaged original locale table.
    public static class RecoveredTimeText
    {
        private static string Fill(string key, string fallback, int value) => RecoveredLocalization.Text(key, fallback).Replace("_$$_", value.ToString());

        public static string RemainingString(long remainingMs)
        {
            if (remainingMs < 1) return "";
            var span = TimeSpan.FromMilliseconds(remainingMs);
            if (span.Days >= 2) return Fill("common_end_day", "_$$_ Day(s)", span.Days);
            int minutes = (int)span.TotalMinutes;
            if (minutes < 90)
            {
                if (minutes >= 1) return Fill("common_end_min", "_$$_ Min(s)", minutes);
                int seconds = (int)span.TotalSeconds;
                return seconds < 0 ? "" : Fill("common_end_sec", "_$$_ Sec(s)", seconds);
            }
            return Fill("common_end_hour", "_$$_ Hour(s)", (int)span.TotalHours);
        }

        // "day_left_draw" = "Ends in _$$_" (es: "Termina en _$$_").
        public static string EndsInDraw(long expirationMs)
        {
            string time = RemainingString(expirationMs - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            if (time.Length == 0) return "";
            return RecoveredLocalization.Text("day_left_draw", "Ends in _$$_").Replace("_$$_", time);
        }
    }
}
