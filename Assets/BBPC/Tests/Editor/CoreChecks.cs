using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using BBPC.Gameplay;

namespace BBPC.Checks
{
    // Runs the SAME production sources in Unity Editor and under the installed Mono runtime.
    public static class CoreChecks
    {
        private static int assertions, oracleCases, namedChecks;
        private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;
        private static void Equal<T>(T actual, T expected, string label)
        {
            assertions++;
            if (!EqualityComparer<T>.Default.Equals(actual, expected))
                throw new Exception(label + ": expected " + expected + ", got " + actual);
        }
        private static void Named(string name, Action test)
        {
            try { test(); namedChecks++; }
            catch (Exception e) { throw new Exception(name + ": " + e.Message, e); }
        }
        private static int I(string value) => int.Parse(value, Culture);
        private static float F(string value) => float.Parse(value, Culture);
        private static double D(string value) => double.Parse(value, Culture);
        private static HitQuality Q(string value) => (HitQuality)I(value);
        private static NormalRecord TapRecord(object model, int id, HitQuality quality, CellKind kind = CellKind.SingleHit) =>
            new NormalRecord(model, id, kind, ResultCounts.Tap(quality), quality, HitQuality.None);

        public static int Run(string fixtureDirectory, string receiptPath)
        {
            assertions = oracleCases = namedChecks = 0;
            try
            {
                Named("tap-result-to-normal-score", () =>
                {
                    Detection hit = NoteJudgement.Tap(1000, 1050, 2, 2);
                    Equal(hit.State, DetectionState.Effected, "detection");
                    Equal(hit.Result.Value.Timestamp, 1050.0, "timestamp");
                    var ledger = new NormalScoreLedger(new NormalScoreState()); var model = new object();
                    Equal(ledger.Record(TapRecord(model, 7, hit.Result.Value.Quality), () => 1050).Accepted, true, "first");
                    Equal(ledger.State.Score, 3, "Perfect score"); Equal(ledger.State.CurrentCombo, 1, "combo");
                    Equal(ledger.Record(TapRecord(model, 7, HitQuality.Great), () => 1060).Accepted, false, "duplicate");
                    Equal(ledger.State.Score, 3, "duplicate score"); Equal(ledger.RecordedModelCount, 1, "identity");
                });
                Named("checkpoint-order-and-presence", () =>
                {
                    var cp = new[] { new HoldCheckpoint(500), new HoldCheckpoint(1000), new HoldCheckpoint(1010) };
                    Equal(NoteJudgement.FindCheckpoint(cp, 1000), 1, "skip expired pending");
                    cp[1] = new HoldCheckpoint(1000, new HitResult(HitQuality.None, 1000));
                    Equal(NoteJudgement.FindCheckpoint(cp, 1000), 2, "non-null None counts as occupied");
                    Equal(NoteJudgement.FindCheckpoint(cp, 3000), -1, "no checkpoint");
                });
                Named("hold-counts-and-valid-dedup", () =>
                {
                    var model = new object(); var ledger = new NormalScoreLedger(new NormalScoreState());
                    HitResult?[] cp = { null, new HitResult(HitQuality.None, 0), new HitResult(HitQuality.Great, 1000) };
                    ResultCounts counts = ResultCounts.Hold(HitQuality.Perfect, cp);
                    Equal(counts.Present, 3, "present"); Equal(counts.Valid, 2, "valid");
                    ledger.Record(new NormalRecord(model, 40, CellKind.SingleHold, counts, HitQuality.None, HitQuality.Perfect), () => 1000);
                    cp[0] = new HitResult(HitQuality.Miss, 1100); counts = ResultCounts.Hold(HitQuality.Perfect, cp);
                    ledger.Record(new NormalRecord(model, 40, CellKind.SingleHold, counts, HitQuality.None, HitQuality.Perfect), () => 1100);
                    Equal(ledger.State.Score, 3, "same valid ID"); Equal(ledger.State.CurrentCombo, 1, "combo");
                    ledger.Record(new NormalRecord(model, 40, CellKind.SingleHold, counts, HitQuality.Miss, HitQuality.Perfect), () => 1200);
                    Equal(ledger.HasRecordedId("40_4_miss"), true, "miss suffix");
                    Equal(ledger.State.Blood, 99, "observed initial penalty"); Equal(ledger.State.CurrentCombo, 0, "reset");
                });
                Named("hold-final-quality-branches", () =>
                {
                    for (int n = 0; n < 3; n++) Equal(NoteJudgement.QualityAfterCheckpoint(n, false), HitQuality.Perfect, "short list");
                    Equal(NoteJudgement.QualityAfterCheckpoint(3, false), HitQuality.Miss, "Count-3 invalid");
                    Equal(NoteJudgement.QualityAfterCheckpoint(3, true), HitQuality.Perfect, "Count-3 valid");
                    foreach (HitQuality h in new[] { HitQuality.None, HitQuality.Miss }) Equal(NoteJudgement.QualityOnHoldTermination(h), HitQuality.Miss, "head failure");
                    Equal(NoteJudgement.QualityOnHoldTermination(HitQuality.Great), HitQuality.Perfect, "valid head");
                    Equal(NoteJudgement.HoldHead(1000, 1000, 1, 0).State, DetectionState.None, "wrong key");
                });
                Named("cpu-gate-and-forced-perfect", () =>
                {
                    var ledger = new NormalScoreLedger(new NormalScoreState()); var model = new object();
                    var empty = new NormalRecord(model, 1, CellKind.CpuSingleHit, new ResultCounts(0, 0), HitQuality.Miss, HitQuality.None);
                    Equal(ledger.Record(empty, null).Accepted, false, "empty gate"); Equal(empty.Hit, HitQuality.Miss, "gate before mutation");
                    var cpu = TapRecord(model, 1, HitQuality.Miss, CellKind.CpuSingleHit);
                    Equal(ledger.Record(cpu, null).Effects, ScoreEffects.None, "CPU effects"); Equal(cpu.Hit, HitQuality.Perfect, "CPU mutation");
                    cpu = TapRecord(model, 1, HitQuality.Miss, CellKind.CpuSingleHit);
                    Equal(ledger.Record(cpu, null).Accepted, false, "CPU duplicate"); Equal(cpu.Hit, HitQuality.Perfect, "forced before dedup");
                    Equal(ledger.State.Score, 0, "CPU score"); Equal(ledger.RecordedModelCount, 0, "CPU models");
                });
                Named("miss-grace-boundary-and-clock-calls", () =>
                {
                    var state = new NormalScoreState { Blood = 1, CurrentMaxBlood = 120, LatelyDeadTimestamp = 1000 };
                    var ledger = new NormalScoreLedger(state); int calls = 0;
                    ledger.Record(TapRecord(new object(), 1, HitQuality.Miss), () => { calls++; return 3999; });
                    Equal(state.Blood, 120, "grace below 3000"); Equal(calls, 1, "grace sample");
                    state.Blood = 1; state.LatelyDeadTimestamp = 1000; calls = 0;
                    ledger.Record(TapRecord(new object(), 2, HitQuality.Miss), () => { calls++; return calls == 1 ? 4000 : 4100; });
                    Equal(state.Blood, 0, "equal 3000 excluded"); Equal(state.LatelyDeadTimestamp.Value, 4100f, "resample at death"); Equal(calls, 2, "two observed call sites");
                });
                Named("zero-penalty-overflow-and-notifications", () =>
                {
                    var state = new NormalScoreState { Score = int.MaxValue, CurrentCombo = int.MaxValue, MaxCombo = int.MaxValue, OneMissReduce = 0 };
                    var ledger = new NormalScoreLedger(state);
                    ledger.Record(TapRecord(new object(), 1, HitQuality.Good), null);
                    Equal(state.Score, int.MinValue, "score wrap"); Equal(state.CurrentCombo, int.MinValue, "combo wrap"); Equal(state.MaxCombo, int.MaxValue, "max signed");
                    RecordOutcome miss = ledger.Record(TapRecord(new object(), 2, HitQuality.Miss), null);
                    Equal(state.CurrentCombo, 0, "zero penalty still resets"); Equal(state.Blood, 100, "zero penalty");
                    Equal(miss.Effects, ScoreEffects.ScoreChanged | ScoreEffects.BloodChanged | ScoreEffects.ResultChanged, "effects");
                });
                Named("binary32-at-input-boundaries", () =>
                {
                    float tiny = .00001f;
                    Equal(NoteJudgement.CheckpointContains(1000, 1060f + tiny), true, "Infos float32 store");
                    Equal(NoteJudgement.MissDeadlinePassed(1000, 1150f + tiny), false, "strict deadline after float32 store");
                    Equal(NoteJudgement.HitAfterDelta((HitQuality)0), 100, "default delta");
                    foreach (int value in new[] { int.MinValue, -1, 0, 6, int.MaxValue }) Equal(NoteJudgement.HitScore((HitQuality)value), 0, "score enum guard");
                });
                Named("native-default-number-format", () =>
                {
                    CultureInfo prior = System.Threading.Thread.CurrentThread.CurrentCulture;
                    try
                    {
                        var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
                        culture.NumberFormat.NegativeSign = "~";
                        System.Threading.Thread.CurrentThread.CurrentCulture = culture;
                        var ledger = new NormalScoreLedger(new NormalScoreState());
                        ledger.Record(TapRecord(new object(), -7, HitQuality.Good), null);
                        Equal(ledger.HasRecordedId("~7"), true, "parameterless Int32.ToString");
                    }
                    finally { System.Threading.Thread.CurrentThread.CurrentCulture = prior; }
                });
                Named("clock-explicit-sync-and-rounding", () =>
                {
                    var clock = new RecoveredAudioClock(new AudioClockState { Mode = 1 }, () => 0, () => 0);
                    bool failed = false; try { clock.PlayingTimestamp(); } catch (InvalidOperationException) { failed = true; }
                    Equal(failed, true, "missing sync must fail even start zero");
                    Equal(RecoveredAudioClock.TruncateArm64(double.NaN), 0L, "NaN conversion");
                    Equal(RecoveredAudioClock.TruncateArm64(double.PositiveInfinity), long.MaxValue, "ARM saturation");
                    Equal(RecoveredAudioClock.TruncateArm64(double.NegativeInfinity), long.MinValue, "negative saturation");
                    Equal(RecoveredAudioClock.TruncateArm64(-.9), 0L, "toward zero");
                });
                Named("miss-deadline-and-cpu-classification", () =>
                {
                    Equal(NoteJudgement.MissDeadlinePassed(1000, 1150), false, "strict deadline");
                    Equal(NoteJudgement.MissDeadlinePassed(1000, 1150.001f), true, "after deadline");
                    Equal(NoteJudgement.IsPlayerHitNote(CellKind.SingleHold), true, "human");
                    Equal(NoteJudgement.IsPlayerHitNote((CellKind)(-1)), false, "unsigned invalid");
                    Equal(NoteJudgement.AutoHitTimePassed(1000, 1000, CellKind.CpuSingleHit), false, "strict CPU");
                    Equal(NoteJudgement.AutoHitTimePassed(1000, 1001, CellKind.CpuSingleHit), true, "CPU after");
                    Equal(NoteJudgement.AutoHitTimePassed(1000, 1001, CellKind.SingleHit), false, "human not automatic");
                });
                NoteOracles(Path.Combine(fixtureDirectory, "note-oracles.txt"));
                ClockOracles(Path.Combine(fixtureDirectory, "clock-oracles.txt"));
                ScoreOracles(Path.Combine(fixtureDirectory, "score-oracles.txt"));
                ConversionOracles(Path.Combine(fixtureDirectory, "conversion-oracles.txt"));
                File.WriteAllText(receiptPath, "{\"success\":true,\"named_checks\":" + namedChecks + ",\"oracle_cases\":" + oracleCases + ",\"assertions\":" + assertions + ",\"executes_production_csharp\":true,\"executes_original_game\":false}\n");
                return assertions;
            }
            catch (Exception e)
            {
                File.WriteAllText(receiptPath, "{\"success\":false,\"assertions_before_failure\":" + assertions + "}\n");
                throw new Exception("C# verification failed: " + e.Message, e);
            }
        }

        private static void NoteOracles(string file)
        {
            foreach (string line in File.ReadLines(file))
            {
                string[] p = line.Split('\t'); string label = "note " + line; oracleCases++;
                switch (p[0])
                {
                    case "tap": case "head":
                        Detection d = p[0] == "tap" ? NoteJudgement.Tap(I(p[1]), F(p[2]), I(p[3]), I(p[4])) : NoteJudgement.HoldHead(I(p[1]), F(p[2]), I(p[3]), I(p[4]));
                        Equal((int)d.State, I(p[5]), label); Equal((int)(d.Result.HasValue ? d.Result.Value.Quality : HitQuality.None), I(p[6]), label); break;
                    case "cp": Equal(NoteJudgement.CheckpointContains(I(p[1]), F(p[2])), I(p[3]) != 0, label); break;
                    case "miss": Equal(NoteJudgement.MissDeadlinePassed(I(p[1]), F(p[2])), I(p[3]) != 0, label); break;
                    case "end": Equal(NoteJudgement.ShouldEndHold(I(p[1]), I(p[2]), F(p[3]), Q(p[4]), I(p[5]) != 0, I(p[6]) != 0, I(p[7]), I(p[8]), I(p[9])), I(p[10]) != 0, label); break;
                    default: throw new Exception("Unknown oracle " + p[0]);
                }
            }
        }
        private static void ClockOracles(string file)
        {
            foreach (string line in File.ReadLines(file))
            {
                string[] p = line.Split('\t'); oracleCases++; var events = new List<string>();
                var times = new Queue<string>(p[7] == "-" ? new string[0] : p[7].Split(';'));
                var syncs = new Queue<string>(p[8] == "-" ? new string[0] : p[8].Split(';'));
                var state = new AudioClockState { Mode = I(p[0]), DspStartTime = D(p[1]), TimeStartTime = D(p[2]), PlayedDuration = D(p[3]), VsMatchStartTime = long.Parse(p[4], Culture) };
                Func<long> sync = I(p[5]) == 0 ? (Func<long>)null : () => { events.Add("sync"); return long.Parse(syncs.Dequeue(), Culture); };
                var clock = new RecoveredAudioClock(state, () => { events.Add("dsp"); return D(p[6]); }, () => { events.Add("time"); return F(times.Dequeue()); }, sync);
                long result = 0; int error = 0;
                try { result = clock.PlayingTimestamp(); } catch (InvalidOperationException) { events.Add("missing"); error = 1; }
                Equal(error, I(p[9]), "clock exception " + line); Equal(result, long.Parse(p[10], Culture), "clock timestamp " + line);
                Equal(state.TimeStartTime, D(p[11]), "clock mutation " + line);
                Equal(events.Count == 0 ? "-" : string.Join(",", events), p[12], "clock order " + line);
            }
        }
        private static void ScoreOracles(string file)
        {
            int group = -1; NormalScoreLedger ledger = null; var identities = new Dictionary<int, object>();
            foreach (string line in File.ReadLines(file))
            {
                string[] p = line.Split('\t'); oracleCases++;
                if (I(p[0]) != group) { group = I(p[0]); ledger = new NormalScoreLedger(new NormalScoreState { OneMissReduce = I(p[1]) }); identities.Clear(); }
                int id = I(p[2]); if (!identities.ContainsKey(id)) identities.Add(id, new object());
                var record = new NormalRecord(identities[id], id, (CellKind)I(p[3]), new ResultCounts(I(p[4]), I(p[5])), Q(p[7]), Q(p[6]));
                RecordOutcome o = ledger.Record(record, () => 0);
                Equal(o.Accepted, I(p[8]) != 0, "score accepted " + line); Equal(ledger.State.Score, I(p[9]), "score " + line);
                Equal(ledger.State.Blood, I(p[10]), "blood " + line); Equal(ledger.State.CurrentCombo, I(p[11]), "combo " + line);
                Equal(ledger.State.MaxCombo, I(p[12]), "max " + line); Equal((int)o.Effects, I(p[13]), "effects " + line);
                Equal(ledger.RecordedIdCount, I(p[14]), "recorded " + line); Equal(ledger.ValidIdCount, I(p[15]), "valid " + line);
                Equal((int)record.Hit, I(p[16]), "CPU mutation " + line);
            }
        }
        private static void ConversionOracles(string file)
        {
            foreach (string line in File.ReadLines(file))
            {
                string[] p = line.Split('\t'); oracleCases++;
                float converted = RecoveredAudioClock.ToFloat32(long.Parse(p[0], Culture));
                Equal(BitConverter.ToUInt32(BitConverter.GetBytes(converted), 0), uint.Parse(p[1], NumberStyles.HexNumber, Culture), "SCVTF " + line);
            }
        }
#if !UNITY_5_3_OR_NEWER
        public static int Main(string[] args)
        {
            try { Console.WriteLine("PASS assertions=" + Run(args[0], args[1])); return 0; }
            catch (Exception e) { Console.Error.WriteLine(e); return 1; }
        }
#endif
    }
}
