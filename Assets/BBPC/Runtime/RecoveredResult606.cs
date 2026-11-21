using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace BBPC.UnityRuntime
{
    // Reconstructed from the supplied BB606_report_resultroom_spec.md, sections 2 and 4.
    // Pure calculations; this class never sends requests or grants rewards.
    public static class RecoveredResult606
    {
        public static string ReportQueryValue(int score, string udid, string auid, string roundId, string sid)
        {
            if (udid == null || auid == null || sid == null) throw new ArgumentNullException();
            if (!Guid.TryParseExact(roundId, "D", out var parsed) || roundId != parsed.ToString("D"))
                throw new FormatException("RoundID must be a lowercase dashed UUID");
            string input = score.ToString(CultureInfo.InvariantCulture) + udid + auid + roundId + sid;
            using (var md5 = MD5.Create())
            {
                var bytes = md5.ComputeHash(Encoding.UTF8.GetBytes(input));
                long tail = 0;
                for (int i = bytes.Length - 6; i < bytes.Length; i++) tail = (tail << 8) | bytes[i];
                return tail.ToString(CultureInfo.InvariantCulture);
            }
        }

        public static float CursorX(float elapsedSeconds)
        {
            if (float.IsNaN(elapsedSeconds) || float.IsInfinity(elapsedSeconds) || elapsedSeconds < 0)
                throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
            float phase = elapsedSeconds % 3.34f;
            float fraction = phase <= 1.67f ? phase / 1.67f : (3.34f - phase) / 1.67f;
            return -292f + 584f * fraction;
        }

        public static int DeathRemaining(float elapsedSeconds)
        {
            if (float.IsNaN(elapsedSeconds) || float.IsInfinity(elapsedSeconds) || elapsedSeconds < 0)
                throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
            return elapsedSeconds >= 5 ? 0 : 5 - (int)elapsedSeconds;
        }

        public static int FactorIndex(float x)
        {
            if (float.IsNaN(x) || float.IsInfinity(x)) throw new ArgumentOutOfRangeException(nameof(x));
            float distance = Math.Abs(x);
            if (distance < 27.5f) return 3;
            if (distance < 102.5f) return x < 0 ? 2 : 4;
            if (distance < 186.5f) return x < 0 ? 1 : 5;
            return x < 0 ? 0 : 6;
        }
    }
}
