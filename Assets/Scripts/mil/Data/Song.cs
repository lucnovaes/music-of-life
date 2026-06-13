using UnityEngine;

namespace mil.Data
{
    [CreateAssetMenu(fileName = "NewSong", menuName = "mil/Data/Song")]
    public sealed class Song : ScriptableObject
    {
        [SerializeField] private string songName;
        [SerializeField] private string backgroundLoopAudioEvent;
        [SerializeField] private string mainAudioEvent;
        [SerializeField] private GameObject backgroundLoopAnimation;
        [Header("MIDI Integration")]
        [SerializeField] private string midiFileName; // Ex: "Músicas/fase1_expert.mid"
        
        [SerializeField] private int loopMeasurement;

        public string SongName => songName;
        public string BackgroundLoopAudioEvent => backgroundLoopAudioEvent;
        public string MainAudioEvent => mainAudioEvent;
        public GameObject BackgroundLoopAnimation => backgroundLoopAnimation;
        public string MidiFileName => midiFileName;
        public int LoopMeasurement => loopMeasurement;
    }
}
