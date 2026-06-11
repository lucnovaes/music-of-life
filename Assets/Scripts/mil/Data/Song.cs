using UnityEngine;

namespace mil.Data
{
    [CreateAssetMenu(fileName = "NewSong", menuName = "mil/Data/Song")]
    public sealed class Song : ScriptableObject
    {
        [SerializeField] private string songName;
        [SerializeField] private string backgroundLoopAudioEvent;
        [SerializeField] private string mainAudioEvent;
        [SerializeField] private AnimationClip backgroundLoopAnimation;
        [SerializeField] private TrackTimeline[] difficultyChallenges;
        [SerializeField] private int loopMeasurement;

        public string SongName => songName;
        public string BackgroundLoopAudioEvent => backgroundLoopAudioEvent;
        public string MainAudioEvent => mainAudioEvent;
        public AnimationClip BackgroundLoopAnimation => backgroundLoopAnimation;
        public TrackTimeline[] DifficultyChallenges => difficultyChallenges;
        public int LoopMeasurement => loopMeasurement;
    }
}
