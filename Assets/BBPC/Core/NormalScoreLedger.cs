using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace BBPC.Gameplay
{
    public sealed class NormalScoreState
    {
        public int Score, CurrentCombo, MaxCombo;
        // Blood..cctor 0x2D0EAC4. This is port initialization from the observed constants, not recovered bootstrap.
        public int Blood = 100, CurrentMaxBlood = 100, OneMissReduce = 1;
        public float? LatelyDeadTimestamp;
    }

    public sealed class NormalRecord
    {
        public readonly object ModelIdentity;
        public readonly int NodeId;
        public readonly CellKind Kind;
        public readonly ResultCounts Counts;
        public readonly HitQuality Head;
        public HitQuality Hit { get; internal set; } // CPU branch forces this even for a duplicate.
        public NormalRecord(object modelIdentity, int nodeId, CellKind kind, ResultCounts counts, HitQuality hit, HitQuality head)
        {
            ModelIdentity = modelIdentity ?? throw new ArgumentNullException(nameof(modelIdentity));
            NodeId = nodeId; Kind = kind; Counts = counts; Hit = hit; Head = head;
        }
    }

    [Flags]
    public enum ScoreEffects { None = 0, ScoreChanged = 1, BloodChanged = 2, ResultChanged = 4 }
    public readonly struct RecordOutcome
    {
        public readonly bool Accepted;
        public readonly ScoreEffects Effects;
        public readonly HitQuality NotificationQuality;
        public RecordOutcome(bool accepted, ScoreEffects effects, HitQuality quality) { Accepted = accepted; Effects = effects; NotificationQuality = quality; }
    }

    // Normal mode direct writes of 0x2CF76D0/0x2CF7CD4. No Challenge formula, VS protocol or unknown delegate bodies.
    public sealed class NormalScoreLedger
    {
        public readonly NormalScoreState State;
        private readonly HashSet<string> recorded = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> valid = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<object> models = new HashSet<object>(new IdentityComparer());
        public int RecordedIdCount => recorded.Count;
        public int ValidIdCount => valid.Count;
        public int RecordedModelCount => models.Count;
        public bool HasRecordedId(string id) => recorded.Contains(id);
        public bool HasValidId(string id) => valid.Contains(id);
        public NormalScoreLedger(NormalScoreState state) { State = state ?? throw new ArgumentNullException(nameof(state)); }

        // Return effects are integration notifications, not fake callbacks. Consumers must implement observed handlers later.
        public RecordOutcome Record(NormalRecord record, Func<float> readPlayingTime)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));
            if (record.Counts.Present < 1) return Outcome(false, ScoreEffects.None, record.Hit);
            string id = record.NodeId.ToString();
            if (record.Kind == CellKind.CpuSingleHit || record.Kind == CellKind.CpuSingleHold)
            {
                record.Hit = HitQuality.Perfect;
                return Outcome(recorded.Add(id), ScoreEffects.None, record.Hit);
            }
            if (record.Kind != CellKind.SingleHit && record.Kind != CellKind.SingleHold)
                return Outcome(false, ScoreEffects.ResultChanged, record.Hit);
            string validId = id;
            HitQuality judgement = record.Hit;
            if (record.Kind == CellKind.SingleHold)
            {
                id += "_" + record.Counts.Present.ToString();
                if (ResultCounts.IsValid(record.Head) && record.Hit == HitQuality.Miss) id += "_miss";
                validId += "_" + record.Counts.Valid.ToString();
                judgement = record.Head;
            }
            if (!recorded.Add(id)) return Outcome(false, ScoreEffects.ResultChanged, judgement);
            models.Add(record.ModelIdentity);
            if (record.Hit == HitQuality.Miss)
            {
                ApplyMiss(readPlayingTime);
                return Outcome(true, ScoreEffects.ScoreChanged | ScoreEffects.BloodChanged | ScoreEffects.ResultChanged, record.Hit);
            }
            ScoreEffects effects = ScoreEffects.ResultChanged;
            if (valid.Add(validId))
            {
                State.CurrentCombo = unchecked(State.CurrentCombo + 1);
                if (State.MaxCombo < State.CurrentCombo) State.MaxCombo = State.CurrentCombo;
                State.Score = unchecked(State.Score + NoteJudgement.HitScore(judgement));
                effects |= ScoreEffects.ScoreChanged;
            }
            return Outcome(true, effects, judgement);
        }
        private void ApplyMiss(Func<float> readPlayingTime)
        {
            State.CurrentCombo = 0;
            State.Blood = Math.Max(0, unchecked(State.Blood - State.OneMissReduce));
            if (State.LatelyDeadTimestamp.HasValue && State.LatelyDeadTimestamp.Value > 0)
            {
                float delta = Float32Arithmetic.Subtract(ReadTime(readPlayingTime), State.LatelyDeadTimestamp.Value);
                if (delta < 3000f) State.Blood = State.CurrentMaxBlood;
            }
            if (State.Blood <= 0) State.LatelyDeadTimestamp = ReadTime(readPlayingTime);
        }
        private static float ReadTime(Func<float> read)
        {
            if (read == null) throw new ArgumentNullException(nameof(read), "This miss path requires PlayingTime.");
            return Float32Arithmetic.Round(read());
        }
        private static RecordOutcome Outcome(bool accepted, ScoreEffects effects, HitQuality quality) => new RecordOutcome(accepted, effects, quality);
        private sealed class IdentityComparer : IEqualityComparer<object>
        {
            public new bool Equals(object a, object b) => ReferenceEquals(a, b);
            public int GetHashCode(object value) => RuntimeHelpers.GetHashCode(value);
        }
    }
}
