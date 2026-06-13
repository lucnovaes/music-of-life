using UnityEngine;

namespace mil.Data
{
    [CreateAssetMenu(fileName = "NewEpisodeAnimation", menuName = "mil/Data/Episode Animation")]
    public sealed class EpisodeAnimation : ScriptableObject
    {
        [Header("Visual Asset")]
        [SerializeField] private GameObject animation;
        [SerializeField] private float durationSeconds;

        [Header("Audio Tracking (FMOD Event Paths)")]
        [SerializeField] private string mainAudioEventPath;
        [SerializeField] private string textureAudioEventPath;
        [SerializeField] private string soundtrackAudioEventPath;


        public GameObject Animation => animation;
        public float DurationSeconds => durationSeconds;
        public string MainAudioEventPath => mainAudioEventPath;
        public string TextureAudioEventPath => textureAudioEventPath;
        public string SoundtrackAudioEventPath => soundtrackAudioEventPath;
    }
}
