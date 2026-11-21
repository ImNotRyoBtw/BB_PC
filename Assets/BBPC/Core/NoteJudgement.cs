using System;
using System.Collections.Generic;

namespace BBPC.Gameplay
{
    public readonly struct HoldCheckpoint
    {
        public readonly int Timestamp;
        public readonly HitResult? Result;
        public HoldCheckpoint(int timestamp, HitResult? result = null) { Timestamp = timestamp; Result = result; }
    }

    // Decisions extracted from 6.0.6. Selection of cells, callbacks and checkpoint generation live outside this boundary.
    public static class NoteJudgement
    {
        public static int HitAfterDelta(HitQuality quality) => quality == HitQuality.Good ? 150 : quality == HitQuality.Perfect ? 75 : 100;
        public static int HitScore(HitQuality quality) => unchecked((uint)((int)quality - 3)) <= 2 ? (int)quality - 2 : 0;

        // 0x2CFF504. int32 boundary arithmetic BEFORE conversion to float32 is significant.
        public static Detection Tap(int timestamp, float playingTime, int key, int noteKey)
        {
            playingTime = Float32Arithmetic.Round(playingTime);
            if (key != noteKey) return new Detection(DetectionState.None);
            if (TapContains(timestamp, playingTime, 75)) return Accepted(DetectionState.Effected, HitQuality.Perfect, playingTime);
            if (TapContains(timestamp, playingTime, 100)) return Accepted(DetectionState.Effected, HitQuality.Great, playingTime);
            if (TapContains(timestamp, playingTime, 150)) return Accepted(DetectionState.Effected, HitQuality.Good, playingTime);
            // FCMP/B.LE treats unordered as the early/no-action branch.
            return new Detection(float.IsNaN(playingTime) || playingTime <= Float32Arithmetic.Round(timestamp) ? DetectionState.None : DetectionState.Miss);
        }
        private static bool TapContains(int timestamp, float time, int delta) =>
            !float.IsNaN(time) && time >= 0 && time >= Float32Arithmetic.Round(unchecked(timestamp - delta)) && time <= Float32Arithmetic.Round(unchecked(timestamp + delta));

        // Head branch only of 0x2CFFAF0, called when both results are None. None means continue the outer hold logic.
        public static Detection HoldHead(int timestamp, float playingTime, int key, int noteKey)
        {
            playingTime = Float32Arithmetic.Round(playingTime);
            if (key != noteKey) return new Detection(DetectionState.None);
            float start = Float32Arithmetic.Round(timestamp); // Hold converts BEFORE +/- delta, unlike tap.
            if (!float.IsNaN(playingTime) && playingTime >= 0 && playingTime >= Float32Arithmetic.Subtract(start, 150f) && playingTime <= Float32Arithmetic.Add(start, 150f))
                return Accepted(DetectionState.Effecting, HitQuality.Perfect, playingTime);
            return new Detection(float.IsNaN(playingTime) || playingTime <= start ? DetectionState.None : DetectionState.Miss);
        }
        public static bool CheckpointContains(int timestamp, float playingTime) =>
            !float.IsNaN(playingTime) && Float32Arithmetic.Round(playingTime) >= 0 && Float32Arithmetic.Round(playingTime) >= Float32Arithmetic.Subtract(Float32Arithmetic.Round(timestamp), 60f) && Float32Arithmetic.Round(playingTime) <= Float32Arithmetic.Add(Float32Arithmetic.Round(timestamp), 60f);

        // First qualifying pending checkpoint in the supplied order; may skip an earlier out-of-window checkpoint.
        public static int FindCheckpoint(IReadOnlyList<HoldCheckpoint> checkpoints, float playingTime)
        {
            if (checkpoints == null) throw new ArgumentNullException(nameof(checkpoints));
            for (int i = 0; i < checkpoints.Count; i++)
                if (!checkpoints[i].Result.HasValue && CheckpointContains(checkpoints[i].Timestamp, playingTime)) return i;
            return -1;
        }

        // Non-checkpoint termination branch. Caller must retain InteractTouch identity across input samples.
        public static bool ShouldEndHold(int timestamp, int duration, float playingTime, HitQuality head,
            bool sameTouch, bool ended, int infosKey, int touchKey, int noteKey)
        {
            playingTime = Float32Arithmetic.Round(playingTime);
            return (head == HitQuality.None && playingTime >= Float32Arithmetic.Round(unchecked(timestamp + 150)))
                || playingTime >= Float32Arithmetic.Add(Float32Arithmetic.Round(unchecked(timestamp + duration)), 60f)
                || (sameTouch && ended && infosKey == noteKey)
                || (sameTouch && touchKey != noteKey);
        }
        public static HitQuality QualityOnHoldTermination(HitQuality head) =>
            head == HitQuality.None || head == HitQuality.Miss ? HitQuality.Miss : HitQuality.Perfect;

        // The predicate for list[Count-3].IsValidResult is injected until that method is recovered.
        public static HitQuality QualityAfterCheckpoint(int count, bool thirdFromLastIsValid)
        {
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
            return count < 3 || thirdFromLastIsValid ? HitQuality.Perfect : HitQuality.Miss;
        }
        public static bool MissDeadlinePassed(int timestamp, float playingTime) => Float32Arithmetic.Round(unchecked(timestamp + 150)) < Float32Arithmetic.Round(playingTime);
        public static bool IsPlayerHitNote(CellKind kind) => (uint)kind < 2;
        public static bool AutoHitTimePassed(int timestamp, float playingTime, CellKind kind) =>
            Float32Arithmetic.Round(playingTime) > Float32Arithmetic.Round(timestamp) && !IsPlayerHitNote(kind);
        private static Detection Accepted(DetectionState state, HitQuality quality, float time) => new Detection(state, new HitResult(quality, (double)time));
    }
}
