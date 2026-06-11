using UnityEngine;

namespace mil.Data
{
    [CreateAssetMenu(fileName = "NewTrackTimeline", menuName = "mil/Data/Track Timeline")]
    public sealed class TrackTimeline : ScriptableObject
    {
        [SerializeField] private string fmodEventPath;
        [SerializeField] private float[] noteTimestampsMs;
        [SerializeField] private int[] noteTypes; 
        [SerializeField] private int difficultyLevel; 

        public string FmodEventPath => fmodEventPath;
        public float[] NoteTimestampsMs => noteTimestampsMs;
        public int[] NoteTypes => noteTypes;
        public int DifficultyLevel => difficultyLevel;
    }
}