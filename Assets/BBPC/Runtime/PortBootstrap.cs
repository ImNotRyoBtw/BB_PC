using BBPC.Gameplay;
using UnityEngine;

namespace BBPC.UnityRuntime
{
    // Permanent entry point for the port. No song, menu or mode is fabricated when content is unavailable.
    public sealed class PortBootstrap : MonoBehaviour
    {
        public RecoveredAudioClock Clock { get; private set; }
        public NormalScoreLedger NormalScores { get; private set; }
        private void Awake()
        {
            Clock = UnityAudioClock.Create(new AudioClockState());
            NormalScores = new NormalScoreLedger(new NormalScoreState());
        }
    }
}
