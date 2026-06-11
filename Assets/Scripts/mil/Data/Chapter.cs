using UnityEngine;

namespace mil.Data
{
    [CreateAssetMenu(fileName = "NewChapter", menuName = "mil/Data/Chapter")]
    public sealed class Chapter : ScriptableObject
    {
        [SerializeField] private string chapterName;
        [SerializeField] private ShaderType visualShaderType;
        [SerializeField] private AnimationClip introAnimation;
        [SerializeField] private AnimationClip finalAnimation;
        [SerializeField] private Song[] songs;

        public string ChapterName => chapterName;
        public ShaderType VisualShaderType => visualShaderType;
        public AnimationClip IntroAnimation => introAnimation;
        public AnimationClip FinalAnimation => finalAnimation;
        public Song[] Songs => songs;
    }
}