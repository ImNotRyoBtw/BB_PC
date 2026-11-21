using System;
using BBPC.Gameplay;
using UnityEngine;

namespace BBPC.UnityRuntime
{
    public static class UnityAudioClock
    {
        // Windows adaptation: measured Time.time/audio drift exceeds 2 seconds under frame stalls.
        // Keep P1 native clock unchanged; provide one sample clock for the Windows audio and judgement timeline.
        public static RecoveredAudioClock CreateForWindowsPlayback(AudioClockState state) =>
            new RecoveredAudioClock(state, () => AudioSettings.dspTime, () => (float)AudioSettings.dspTime);

        // Unity 2022.3: retain Time.time float32. Do not replace it by unscaledTime/timeAsDouble or the DSP delta.
        public static RecoveredAudioClock Create(AudioClockState state, Func<long> synchronizedTime = null) =>
            new RecoveredAudioClock(state, () => AudioSettings.dspTime, () => Time.time, synchronizedTime);
    }
}
