using UnityEngine;

namespace mil.Data
{
    [CreateAssetMenu(fileName = "NewEpisode", menuName = "mil/Data/Episode")]
    public sealed class Episode : ScriptableObject
    {
        [SerializeField] private string episodeTitle;
        [SerializeField] private Sprite thumbnailImage;
        [SerializeField] private ShaderType shaderType;
        [SerializeField] private EpisodeAnimation mesterIntroAnimationBlock;
        [SerializeField] private EpisodeAnimation masterFinalAnimationBlock;
        [SerializeField] private Chapter[] chapters;
        [SerializeField] private string[] creditNames; 

        public string EpisodeTitle => episodeTitle;
        public Sprite ThumbnailImage => thumbnailImage;
        public ShaderType ShaderType => shaderType;
        public EpisodeAnimation MesterIntroAnimationBlock => mesterIntroAnimationBlock;
        public EpisodeAnimation MasterFinalAnimationBlock => masterFinalAnimationBlock;
        public Chapter[] Chapters => chapters;
        public string[] CreditNames => creditNames;
    }
}