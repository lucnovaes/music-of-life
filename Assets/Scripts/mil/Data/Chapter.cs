using UnityEngine;

namespace mil.Data
{
    [CreateAssetMenu(fileName = "NewChapter", menuName = "mil/Data/Chapter")]
    public sealed class Chapter : ScriptableObject
    {
        [SerializeField] private string chapterName;
        [SerializeField] private int bpm;
        [SerializeField] private ShaderType shaderType;
        [SerializeField] private EpisodeAnimation introAnimationBlock;
        [SerializeField] private EpisodeAnimation finalAnimationBlock;
        [SerializeField] private Song[] songs;

        public string ChapterName => chapterName;
        public int Bpm => bpm;
        public ShaderType ShaderType => shaderType;
        public EpisodeAnimation IntroAnimationBlock => introAnimationBlock;
        public EpisodeAnimation FinalAnimationBlock => finalAnimationBlock;
        public Song[] Songs => songs;
    }
}