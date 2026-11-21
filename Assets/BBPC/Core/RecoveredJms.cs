using System;
using System.Globalization;
using System.Text;
namespace BBPC.Network
{
    // Equivalent math, not recovered source. Byte/instruction evidence and oracle receipts: analysis/p2-jms.
    public static class RecoveredJms
    {
        public const int DefaultJmo = 0xf29c; // AppConfigManager.Jmo fallback, not rjm.
        // Bionic rand/srand delegate to default TYPE_3 random/srandom (31 words, separation 3).
        // This is NOT System.Random or Windows CRT rand. No shared host RNG state is changed.
        public static int BionicFirst(uint seed)
        {
            var state = new int[31]; state[0] = unchecked((int)seed);
            for (int i = 1; i < state.Length; i++)
            {
                long previous = state[i - 1];
                long next = 16807 * (previous % 127773) - 2836 * (previous / 127773);
                if (next <= 0) next += int.MaxValue;
                state[i] = unchecked((int)next);
            }
            int result = 0, front = 3, rear = 0;
            for (int i = 0; i <= 310; i++)
            {
                state[front] = unchecked(state[front] + state[rear]);
                result = (int)((uint)state[front] >> 1);
                front = (front + 1) % 31; rear = (rear + 1) % 31;
            }
            return result;
        }
        public static long RnFromRand(int rand)
        {
            if (rand < 0) throw new ArgumentOutOfRangeException(nameof(rand));
            long n = rand % 100000; if (n == 0) n = 100000;
            n *= 10;
            for (int i = 0; i < 4; i++) if (n < 100000) n *= 10;
            return n;
        }
        // AArch64 SDIV: division by zero is zero; MIN/-1 wraps, unlike CLR division.
        private static long Divide(long n, long d) => d == 0 ? 0 : n == long.MinValue && d == -1 ? long.MinValue : n / d;
        private static long Remainder(long n, long d) => unchecked(n - Divide(n, d) * d);
        public static long PrtFromRand(byte[] bytes, int length, long timestamp, long jmo, int rand)
        {
            if (bytes == null || length > bytes.Length) throw new ArgumentException("Native byte range unavailable");
            unchecked
            {
                long rn = RnFromRand(rand), value = timestamp * 1000000 + rn, divisor = jmo;
                for (long n = rn; n > 0; n /= 10) divisor += n % 10;
                for (int i = 0; i < length; i++) divisor += bytes[i]; // LDRB: unsigned bytes.
                return Remainder(Divide(value, divisor), 10000) * 1000000000
                    + rn * 1000 + Remainder(Remainder(value, divisor), 1000);
            }
        }
        public static string Format(long value)
        {
            string text = value.ToString(CultureInfo.InvariantCulture);
            return value < 0 ? "-" + text.Substring(1).PadLeft(12, '0') : text.PadLeft(13, '0');
        }
        public static string Compute(int nonce, string ownUdid, long timestamp, int jmo = DefaultJmo)
        {
            if (string.IsNullOrEmpty(ownUdid)) throw new ArgumentException("Own server-issued UDID required");
            if (ownUdid.IndexOf((char)0) >= 0) throw new ArgumentException("Embedded NUL would truncate the native marshaller");
            // IL2CPP passes UTF-8 char*, but length is managed String.Length (UTF-16 code units).
            // Reject malformed UTF-16 rather than silently substitute an unverified marshaller fallback.
            byte[] bytes = new UTF8Encoding(false, true).GetBytes(ownUdid);
            uint seed = unchecked((uint)(timestamp + (uint)nonce));
            return Format(PrtFromRand(bytes, ownUdid.Length, timestamp, jmo, BionicFirst(seed)));
        }
    }
}
