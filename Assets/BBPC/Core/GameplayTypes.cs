using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace BBPC.Gameplay
{
    // CLI/Mono may retain wider intermediates. The overlapping integer read forces an actual binary32 store.
    internal static class Float32Arithmetic
    {
        [StructLayout(LayoutKind.Explicit)]
        private struct Bits
        {
            [FieldOffset(0)] public float Value;
            [FieldOffset(0)] public int Raw;
        }
        public static float Round(float value)
        {
            int bits = new Bits { Value = value }.Raw;
            return new Bits { Raw = bits }.Value;
        }
        public static float Add(float a, float b) => Round(a + b);
        public static float Subtract(float a, float b) => Round(a - b);
    }

    // 6.0.6 dump enums. These domains intentionally remain separate.
    public enum HitQuality { None = 1, Miss = 2, Good = 3, Great = 4, Perfect = 5 }
    public enum DetectionState { None = 0, Effecting = 1, Effected = 2, Miss = 3 }
    public enum CellState { Idle = 0, Visible = 1, Miss = 2, Effecting = 3, Effected = 4, Invisible = 5 }
    public enum CellKind { SingleHit = 0, SingleHold = 1, CpuSingleHit = 2, CpuSingleHold = 3 }

    public readonly struct HitResult
    {
        public readonly HitQuality Quality;
        public readonly double Timestamp;
        public HitResult(HitQuality quality, double timestamp) { Quality = quality; Timestamp = timestamp; }
    }

    public readonly struct Detection
    {
        public readonly DetectionState State;
        public readonly HitResult? Result;
        public Detection(DetectionState state, HitResult? result = null) { State = state; Result = result; }
    }

    public readonly struct ResultCounts
    {
        public readonly int Present;
        public readonly int Valid;
        public ResultCounts(int present, int valid) { Present = present; Valid = valid; }
        public static bool IsValid(HitQuality quality) => quality != HitQuality.None && quality != HitQuality.Miss;
        public static ResultCounts Tap(HitQuality hit) => new ResultCounts(hit == HitQuality.None ? 0 : 1, IsValid(hit) ? 1 : 0);

        // Native 0x2CFA738/0x2CFA8C0: a non-null checkpoint with quality None counts as present.
        public static ResultCounts Hold(HitQuality head, IReadOnlyList<HitResult?> checkpoints)
        {
            if (checkpoints == null) throw new ArgumentNullException(nameof(checkpoints));
            int present = head == HitQuality.None ? 0 : 1, valid = IsValid(head) ? 1 : 0;
            foreach (HitResult? checkpoint in checkpoints)
            {
                if (!checkpoint.HasValue) continue;
                present = unchecked(present + 1);
                if (IsValid(checkpoint.Value.Quality)) valid = unchecked(valid + 1);
            }
            return new ResultCounts(present, valid);
        }
    }
}
