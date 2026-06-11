using UnityEngine;

namespace mil.Data
{
    [CreateAssetMenu(fileName = "NewEpisode", menuName = "mil/Data/Episode")]
    public sealed class EpisodeManifest : ScriptableObject
    {
        [SerializeField] private string episodeTitle;
        [SerializeField] private Sprite thumbnailImage;
        [SerializeField] private ShaderType visualShaderType;
        [SerializeField] private AnimationClip introAnimation;
        [SerializeField] private AnimationClip finalAnimation;
        [SerializeField] private Chapter[] chapters;
        [SerializeField] private string[] creditNames; 

        public string EpisodeTitle => episodeTitle;
        public Sprite ThumbnailImage => thumbnailImage;
        public ShaderType VisualShaderType => visualShaderType;
        public AnimationClip IntroAnimation => introAnimation;
        public AnimationClip FinalAnimation => finalAnimation;
        public Chapter[] Chapters => chapters;
        public string[] CreditNames => creditNames;
    }
}