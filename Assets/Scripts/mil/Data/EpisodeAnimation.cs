using UnityEngine;

namespace mil.Data
{
    [CreateAssetMenu(fileName = "NewEpisodeAnimation", menuName = "mil/Data/Episode Animation")]
    public sealed class EpisodeAnimation : ScriptableObject
    {
        [Header("Visual Asset")]
        [SerializeField] private AnimationClip animationClip;
        [SerializeField] private float durationSeconds;

        [Header("Audio Tracking (FMOD Event Paths)")]
        [SerializeField] private string mainAudioEventPath;
        [SerializeField] private string backgroundMoodAudioPath;

        public AnimationClip AnimationClip => animationClip;
        public float DurationSeconds => durationSeconds;
        public string MainAudioEventPath => mainAudioEventPath;
        public string BackgroundMoodAudioPath => backgroundMoodAudioPath;
    }
}
