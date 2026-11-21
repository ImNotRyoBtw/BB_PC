using System;

namespace BBPC.Gameplay
{
    // Native AudioPlayer fields; initialization/pause/resume policy is deliberately not inferred here.
    public sealed class AudioClockState
    {
        public int Mode;
        public double DspStartTime = -1;
        public double TimeStartTime;
        public double PlayedDuration;
        public long VsMatchStartTime;
    }

    public sealed class RecoveredAudioClock
    {
        public readonly AudioClockState State;
        private readonly Func<double> readDsp;
        private readonly Func<float> readTime;
        private readonly Func<long> readSync;
        public RecoveredAudioClock(AudioClockState state, Func<double> dsp, Func<float> time, Func<long> sync = null)
        {
            State = state ?? throw new ArgumentNullException(nameof(state));
            readDsp = dsp ?? throw new ArgumentNullException(nameof(dsp));
            readTime = time ?? throw new ArgumentNullException(nameof(time));
            readSync = sync;
        }

        // 6.0.6 0x2CE7F74. A missing synchronizer is an explicit failure, never a local fallback.
        public float GetTime()
        {
            if (State.Mode == 0) return Float32Arithmetic.Round(readTime());
            if (State.Mode == 1) return ToFloat32(ReadSync());
            return 0;
        }
        private long ReadSync()
        {
            if (readSync == null) throw new InvalidOperationException("RealTime requires an explicitly supplied synchronizer.");
            return readSync();
        }

        // 6.0.6 0x2CDEDC4; docs/m1/audio-clock.md. DSP is a guard, NOT the elapsed-time source.
        public long PlayingTimestamp()
        {
            if (State.Mode == 1)
            {
                long now = ReadSync(); // Must happen even when start < 1.
                long start = State.VsMatchStartTime;
                return start < 1 || now < start ? 0 : unchecked(now - start);
            }
            long accumulated = State.PlayedDuration > 0 ? MillisecondCast(State.PlayedDuration * 1000.0) : 0;
            if (State.DspStartTime < 0 || double.IsNaN(State.DspStartTime)) return Math.Max(0, accumulated);
            double dspNow = readDsp();
            double startDsp = State.DspStartTime; // Native reload occurs after the callback.
            float current = GetTime();
            double dspDelta = (dspNow - startDsp) * 1000.0;
            if (double.IsPositiveInfinity(dspDelta) || TruncateArm64(dspDelta) < 0)
            {
                State.TimeStartTime = GetTime();
                GetTime(); // Third observed call; return value intentionally discarded.
            }
            else
            {
                long elapsed = MillisecondCast(((double)current - State.TimeStartTime) * 1000.0);
                accumulated = unchecked(accumulated + elapsed);
            }
            return Math.Max(0, accumulated);
        }

        // PlayRoomCenter.PlayingTime 0x2CDEDA0: direct integer -> float, no extra offset or scale.
        public float PlayingTime() => ToFloat32(PlayingTimestamp());
        private static long MillisecondCast(double value) => double.IsPositiveInfinity(value) ? long.MinValue : TruncateArm64(value);
        public static long TruncateArm64(double value)
        {
            if (double.IsNaN(value)) return 0;
            if (value >= 9223372036854775808.0) return long.MaxValue;
            if (value <= -9223372036854775808.0) return long.MinValue;
            return (long)value;
        }

        // Direct SCVTF round-to-nearest/ties-even; avoid a long -> double -> float double rounding.
        public static float ToFloat32(long value)
        {
            ulong magnitude = value < 0 ? unchecked((ulong)(-(value + 1))) + 1 : (ulong)value;
            int bits = 0;
            for (ulong n = magnitude; n != 0; n >>= 1) bits++;
            int shift = Math.Max(0, bits - 24);
            if (shift > 0)
            {
                ulong quotient = magnitude >> shift;
                ulong remainder = magnitude & ((1UL << shift) - 1);
                ulong halfway = 1UL << (shift - 1);
                if (remainder > halfway || (remainder == halfway && (quotient & 1) != 0)) quotient++;
                magnitude = quotient << shift;
            }
            float rounded = (float)magnitude;
            return value < 0 ? -rounded : rounded;
        }
    }
}
